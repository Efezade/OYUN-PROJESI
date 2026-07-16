using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Karo çöküşünün "göle taş atma" dalgası: verilen MERKEZDEN dışa yayılan KIRMIZI halkalar;
    /// cephe bir karonun üstünden geçerken karo YUKARI kabarır ve KIRMIZIYA boyanır (cephenin
    /// arkasında kısa bir kırmızı İZ bırakıp söner). Sisli karoda boyama karodan görünmez diye
    /// karonun üstündeki BULUT da aynı oranda kızartılır (FogOfWarManager.TintCloud).
    /// Merkez ada DIŞINDA olabilir (uzak adadaki çöküşün sanal konumu) — halka ada sınırları
    /// dışında HİÇ çizilmez; cephe adaya ulaştığı anda kenardan yay olarak girer.
    /// YENİ İŞARETLEME AÇIKLAMASI: dalgaya "reveal" hedefleri verilebilir
    /// (<see cref="PlayWithReveals"/>) — cephe hedef karonun üstünden geçtiği anda gökten
    /// KIRMIZI YILDIRIM çakar ve callback (kırmızı çerçeve + sayaç) o anda tetiklenir.
    /// Dalga yoksa <see cref="StrikeSeries"/> yıldırımları art arda çakar.
    /// Harita/grid yenilenirse (_gridVersion) aktif dalgalar kendini iptal eder.
    /// </summary>
    public class CollapseWaveEffect : MonoBehaviour
    {
        [Header("Bağımlılık")]
        [SerializeField] private HexGridManager _grid;
        [Tooltip("Karo boyamasının geri alımı (sis-doğru renk) + bulut kızartması için.")]
        [SerializeField] private FogOfWarManager _fog;

        [Header("Dalga Halkaları (kırmızı)")]
        [SerializeField] private Color _waveColor = new Color(1f, 0.12f, 0.08f, 0.9f);
        [Tooltip("Aynı dalgada art arda kaç halka (su gibi çok katlı).")]
        [SerializeField, Range(1, 5)] private int _ringCount = 3;
        [Tooltip("Halkalar arası başlama gecikmesi (sn).")]
        [SerializeField] private float _ringStagger = 0.3f;
        [Tooltip("Dalganın yayılma HIZI (m/sn). Uzak adadan geliş süresi mesafe/hız.")]
        [SerializeField] private float _waveSpeed = 14f;
        [SerializeField] private float _ringWidth = 0.18f;
        [Tooltip("Halka çemberinin segment sayısı (büyük yarıçapta düzgünlük).")]
        [SerializeField] private int   _segments  = 64;
        [Tooltip("Halkanın zeminden yüksekliği (karo üstü 0.3 → üstünde kalsın).")]
        [SerializeField] private float _lift      = 0.45f;
        [Tooltip("Halkanın ada sınırının ne kadar dışına taşabileceği (m). Ötesi HİÇ çizilmez.")]
        [SerializeField] private float _edgeMargin = 1.2f;

        [Header("Karo Tepkisi (cephe geçerken: kabar + kırmızıya boyan)")]
        [Tooltip("Dalga cephesinin karoyu etkilediği bant genişliği (dünya birimi).")]
        [SerializeField] private float _bandWidth = 1.6f;
        [Tooltip("Karonun en fazla ne kadar yükseleceği (cephe tam üstündeyken).")]
        [SerializeField] private float _bobHeight = 0.22f;
        [Tooltip("Cephe üstündeki karonun (ve bulutunun) boyanacağı renk.")]
        [SerializeField] private Color _tileFlashColor = new Color(1f, 0.15f, 0.1f);
        [Tooltip("Boyama şiddeti (0=kapalı, 1=tam kırmızı).")]
        [SerializeField, Range(0f, 1f)] private float _tileFlashStrength = 0.85f;
        [Tooltip("Cephenin ARKASINDA kırmızı izin kaç metrede söndüğü (geçtiği yer bir süre kızarık kalır).")]
        [SerializeField] private float _tileFlashTrail = 3f;

        [Header("Yıldırım (yeni işaretlenen karonun açıklanması)")]
        [SerializeField] private Color _strikeColor    = new Color(1f, 0.22f, 0.12f);
        [Tooltip("Yıldırımın gökten indiği yükseklik (sisin/bulutların üstünden görünür).")]
        [SerializeField] private float _strikeHeight   = 9f;
        [SerializeField] private float _strikeWidth    = 0.12f;
        [Tooltip("Çakma süresi (titrek parlarken + sönerken).")]
        [SerializeField] private float _strikeDuration = 0.45f;

        [Header("Performans")]
        [Tooltip("Aynı anda en fazla kaç dalga (fazlası sessizce atlanır; reveal'lar yıldırıma düşer).")]
        [SerializeField] private int _maxWaves = 10;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId     = Shader.PropertyToID("_Color");
        private MaterialPropertyBlock _mpb;
        private int _activeWaves;
        private int _gridVersion;   // grid yenilenince artar → aktif dalgalar kendini iptal eder

        private class WTile
        {
            public Transform t;
            public Vector3   basePos;
            public float     dist;
            public HexCell   cell;
            public bool      tinted;
        }

        private void OnEnable()  { if (_grid != null) _grid.OnGridRegenerated += BumpVersion; }
        private void OnDisable() { if (_grid != null) _grid.OnGridRegenerated -= BumpVersion; }
        private void BumpVersion() => _gridVersion++;

        /// <summary>Dalgayı başlat (reveal'sız). center ada dışı sanal nokta olabilir.</summary>
        public void Play(Vector3 center, float delay = 0f) =>
            PlayInternal(center, delay, null, null);

        /// <summary>Dalga + işaretleme açıklaması: cephe her reveal karosunun üstünden geçerken
        /// yıldırım çakar ve onReveal(karo) çağrılır (çerçeve/sayaç o anda başlar).</summary>
        public void PlayWithReveals(Vector3 center, float delay,
                                    List<HexCell> reveals, System.Action<HexCell> onReveal) =>
            PlayInternal(center, delay, reveals, onReveal);

        /// <summary>Dalgasız açıklama (örn. ilk kıyamet günü — henüz çöküş yok): yıldırımlar
        /// art arda çakar, her birinde onReveal tetiklenir.</summary>
        public void StrikeSeries(List<HexCell> cells, System.Action<HexCell> onReveal)
        {
            if (cells == null || cells.Count == 0) return;
            StartCoroutine(StrikeSeriesRoutine(new List<HexCell>(cells), onReveal));
        }

        private void PlayInternal(Vector3 center, float delay,
                                  List<HexCell> reveals, System.Action<HexCell> onReveal)
        {
            if (_grid == null || _grid.Cells == null)
            {   // dalga çizilemiyor → açıklamaları yine de kaybetme
                if (reveals != null) foreach (var c in reveals) onReveal?.Invoke(c);
                return;
            }
            if (_activeWaves >= _maxWaves)
            {
                if (reveals != null) StrikeSeries(reveals, onReveal);
                return;
            }
            StartCoroutine(WaveRoutine(center, delay,
                reveals != null ? new List<HexCell>(reveals) : null, onReveal));
        }

        private IEnumerator WaveRoutine(Vector3 center, float delay,
                                        List<HexCell> reveals, System.Action<HexCell> onReveal)
        {
            _activeWaves++;
            int version = _gridVersion;
            if (delay > 0f) yield return new WaitForSeconds(delay);

            // Aktif adanın karoları + merkeze uzaklıkları + ada sınırları (klip için).
            var tiles = new List<WTile>();
            float maxDist = 1f;
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            Vector2 c2 = new Vector2(center.x, center.z);
            foreach (HexCell cell in _grid.Cells.Values)
            {
                if (cell.Visual == null) continue;
                Vector3 p = cell.Visual.transform.position;
                if (p.x < minX) minX = p.x;  if (p.x > maxX) maxX = p.x;
                if (p.z < minZ) minZ = p.z;  if (p.z > maxZ) maxZ = p.z;
                float d = Vector2.Distance(new Vector2(p.x, p.z), c2);
                tiles.Add(new WTile { t = cell.Visual.transform, basePos = p, dist = d, cell = cell });
                if (d > maxDist) maxDist = d;
            }
            if (tiles.Count == 0) { _activeWaves--; yield break; }
            minX -= _edgeMargin; maxX += _edgeMargin;
            minZ -= _edgeMargin; maxZ += _edgeMargin;

            float outerRadius = maxDist + _bandWidth;
            float ringLife    = outerRadius / Mathf.Max(1f, _waveSpeed);

            // Reveal hedefleri: cephe (lider halka) uzaklıklarına ulaşınca yıldırım + callback.
            List<(HexCell cell, float dist)> pending = null;
            if (reveals != null && reveals.Count > 0)
            {
                pending = new List<(HexCell, float)>(reveals.Count);
                foreach (HexCell cell in reveals)
                {
                    if (cell == null) continue;
                    Vector3 p = cell.WorldPosition;
                    pending.Add((cell, Vector2.Distance(new Vector2(p.x, p.z), c2)));
                }
            }

            var rings    = new LineRenderer[_ringCount];
            var ringMats = new Material[_ringCount];
            for (int i = 0; i < _ringCount; i++)
            {
                ringMats[i] = MakeRingMaterial();
                rings[i]    = MakeRing(ringMats[i]);
            }
            var pts = new Vector3[_segments];
            var vis = new bool[_segments];

            float total = (_ringCount - 1) * _ringStagger + ringLife;
            float t = 0f;
            bool aborted = false;
            while (t < total)
            {
                if (_gridVersion != version) { aborted = true; break; }   // harita değişti
                t += Time.deltaTime;
                float frontR = t * _waveSpeed;   // lider (ilk) halkanın cephesi

                for (int i = 0; i < _ringCount; i++)
                {
                    float rt = t - i * _ringStagger;
                    if (rt < 0f || rt > ringLife) { rings[i].enabled = false; continue; }
                    SetRingAlpha(ringMats[i], EndFade(rt / ringLife));
                    DrawRingClipped(rings[i], center, rt * _waveSpeed, minX, maxX, minZ, maxZ, pts, vis);
                }

                // Cephe reveal hedefine ulaştı → YILDIRIM + açıklama (çerçeve/sayaç başlar).
                if (pending != null)
                    for (int i = pending.Count - 1; i >= 0; i--)
                        if (pending[i].dist <= frontR)
                        {
                            HexCell cell = pending[i].cell;
                            pending.RemoveAt(i);
                            StartCoroutine(StrikeRoutine(
                                cell.WorldPosition + Vector3.up * cell.SurfaceHeight));
                            onReveal?.Invoke(cell);
                        }

                // Karo tepkisi: cephe bantları + lider cephenin arkasındaki kırmızı iz.
                for (int i = 0; i < tiles.Count; i++)
                {
                    WTile tile = tiles[i];
                    if (tile.t == null) continue;

                    float presence = 0f;
                    for (int r = 0; r < _ringCount; r++)
                    {
                        float rt = t - r * _ringStagger;
                        if (rt < 0f || rt > ringLife) continue;
                        float delta = Mathf.Abs(tile.dist - rt * _waveSpeed);
                        if (delta >= _bandWidth) continue;
                        float env = (1f - delta / _bandWidth) * EndFade(rt / ringLife);
                        if (env > presence) presence = env;
                    }
                    // İz: lider cephe geçti → kırmızılık _tileFlashTrail metre boyunca söner.
                    float behind = frontR - tile.dist;
                    if (behind > 0f && behind < _tileFlashTrail && t <= ringLife)
                    {
                        float trail = (1f - behind / _tileFlashTrail) * EndFade(t / ringLife);
                        if (trail > presence) presence = trail;
                    }

                    tile.t.position = tile.basePos +
                        Vector3.up * (Mathf.Sin(presence * Mathf.PI * 0.5f) * _bobHeight);

                    if (presence > 0.001f) { TintTile(tile.cell, presence); tile.tinted = true; }
                    else if (tile.tinted)  { RestoreTile(tile.cell);        tile.tinted = false; }
                }
                yield return null;
            }

            // Açıklanmamış reveal kalmasın (dalga iptal olsa bile veri görseli gelsin).
            if (pending != null && !aborted)
                foreach (var p in pending) onReveal?.Invoke(p.cell);

            for (int i = 0; i < tiles.Count; i++)
            {
                if (tiles[i].t != null) tiles[i].t.position = tiles[i].basePos;
                if (tiles[i].tinted) RestoreTile(tiles[i].cell);
            }
            for (int i = 0; i < _ringCount; i++)
            {
                if (rings[i]    != null) Destroy(rings[i].gameObject);
                if (ringMats[i] != null) Destroy(ringMats[i]);
            }
            _activeWaves--;
        }

        // ── Yıldırım: gökten karoya titrek kırmızı huzme ─────────────────────
        private IEnumerator StrikeSeriesRoutine(List<HexCell> cells, System.Action<HexCell> onReveal)
        {
            int version = _gridVersion;
            foreach (HexCell cell in cells)
            {
                if (_gridVersion != version) yield break;
                if (cell != null)
                {
                    StartCoroutine(StrikeRoutine(cell.WorldPosition + Vector3.up * cell.SurfaceHeight));
                    onReveal?.Invoke(cell);
                }
                yield return new WaitForSeconds(0.2f);
            }
        }

        private IEnumerator StrikeRoutine(Vector3 groundPos)
        {
            int version = _gridVersion;
            Material mat = MakeRingMaterial();
            SetMatColor(mat, _strikeColor, 1f);

            var go = new GameObject("DoomStrike");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace     = true;
            lr.loop              = false;
            lr.positionCount     = 6;
            lr.widthMultiplier   = _strikeWidth;
            lr.material          = mat;
            lr.numCornerVertices = 1;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows    = false;

            float t = 0f, rejitter = 0f;
            while (t < _strikeDuration && _gridVersion == version)
            {
                t += Time.deltaTime;
                rejitter -= Time.deltaTime;
                if (rejitter <= 0f) { JitterBolt(lr, groundPos); rejitter = 0.05f; }  // titrek çakma
                // Son %60'ta sön.
                float fade = 1f - Mathf.Clamp01((t - _strikeDuration * 0.4f) / (_strikeDuration * 0.6f));
                SetMatColor(mat, _strikeColor, fade);
                yield return null;
            }
            Destroy(go);
            Destroy(mat);
        }

        private void JitterBolt(LineRenderer lr, Vector3 ground)
        {
            Vector3 top = ground + Vector3.up * _strikeHeight;
            int n = lr.positionCount;
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)(n - 1);                     // 0 = tepe, 1 = zemin
                Vector3 p = Vector3.Lerp(top, ground + Vector3.up * 0.15f, k);
                if (i > 0 && i < n - 1)                            // uçlar sabit, ortası kırık
                    p += new Vector3(Random.Range(-0.5f, 0.5f), 0f, Random.Range(-0.5f, 0.5f))
                         * (0.4f + 0.6f * k);
                lr.SetPosition(i, p);
            }
        }

        // Halkayı YALNIZ ada sınırları içindeki yay(lar)ıyla çizer (dalganın doğuşu görünmez).
        private void DrawRingClipped(LineRenderer lr, Vector3 center, float radius,
                                     float minX, float maxX, float minZ, float maxZ,
                                     Vector3[] pts, bool[] vis)
        {
            int n = _segments, visCount = 0;
            for (int i = 0; i < n; i++)
            {
                float ang = (i / (float)n) * Mathf.PI * 2f;
                float x = center.x + Mathf.Cos(ang) * radius;
                float z = center.z + Mathf.Sin(ang) * radius;
                pts[i] = new Vector3(x, _lift, z);
                vis[i] = x >= minX && x <= maxX && z >= minZ && z <= maxZ;
                if (vis[i]) visCount++;
            }

            if (visCount == 0) { lr.enabled = false; return; }
            lr.enabled = true;

            if (visCount == n)
            {
                lr.loop = true;
                lr.positionCount = n;
                for (int i = 0; i < n; i++) lr.SetPosition(i, pts[i]);
                return;
            }

            int bestStart = 0, bestLen = 0;
            for (int i = 0; i < n; i++)
            {
                if (!vis[i] || vis[(i + n - 1) % n]) continue;    // koşu başı
                int len = 0, j = i;
                while (len < n && vis[j % n]) { len++; j++; }
                if (len > bestLen) { bestLen = len; bestStart = i; }
            }
            lr.loop = false;
            lr.positionCount = bestLen;
            for (int k = 0; k < bestLen; k++) lr.SetPosition(k, pts[(bestStart + k) % n]);
        }

        // ── Karo + bulut boyama (fog ile aynı MPB kanalı) ────────────────────
        private void TintTile(HexCell cell, float redness)
        {
            float k = Mathf.Clamp01(redness * _tileFlashStrength);
            // Sisli karoda görünen şey karo değil BULUT → bulutu da aynı oranda kızart.
            if (_fog != null) _fog.TintCloud(cell.Coordinate, _tileFlashColor, k);

            MeshRenderer mr = cell.MeshRenderer;
            if (mr == null) return;
            _mpb ??= new MaterialPropertyBlock();
            Color c = Color.Lerp(cell.BaseColor, _tileFlashColor, k);
            c.a = 1f;
            mr.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, c);
            _mpb.SetColor(ColorId,     c);
            mr.SetPropertyBlock(_mpb);
        }

        private void RestoreTile(HexCell cell)
        {
            if (cell == null) return;
            if (_fog != null)
            {
                _fog.TintCloud(cell.Coordinate, _tileFlashColor, 0f);   // bulut kendi rengine
                if (cell.MeshRenderer != null) _fog.ReapplyCellBrightness(cell);
            }
            else TintTile(cell, 0f);
        }

        // Sonuna kadar tam görünür; yalnız son %30'da yumuşakça söner.
        private static float EndFade(float k) => Mathf.Clamp01((1f - k) / 0.3f);

        private Material MakeRingMaterial()
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
            var m = new Material(sh);
            if (m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface",  1f);
                m.SetFloat("_Blend",    0f);
                m.SetFloat("_ZWrite",   0f);
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.DisableKeyword("_SURFACE_TYPE_OPAQUE");
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return m;
        }

        private void SetRingAlpha(Material m, float alphaMul) => SetMatColor(m, _waveColor, alphaMul);

        private static void SetMatColor(Material m, Color color, float alphaMul)
        {
            Color c = color;
            c.a *= alphaMul;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color"))     m.SetColor("_Color",     c);
        }

        private LineRenderer MakeRing(Material mat)
        {
            var go = new GameObject("CollapseWaveRing");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace     = true;
            lr.loop              = true;
            lr.positionCount     = 0;
            lr.widthMultiplier   = _ringWidth;
            lr.material          = mat;
            lr.numCornerVertices = 1;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows    = false;
            lr.enabled           = false;
            return lr;
        }
    }
}
