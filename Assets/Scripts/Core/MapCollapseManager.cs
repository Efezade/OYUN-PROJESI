using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Kıyamet Sayacı — 1 GÜN ÖNCEDEN UYARILI çöküş, ADA-BAĞIMSIZ (3x3 dünya):
    ///   • Bir gün başında, o günün SONUNDA çökecek karolar AKTİF adada seçilir + İŞARETLENİR
    ///     (kırmızı kenar çizgisi + üstünde "kalan AP" sayısı).
    ///   • Gün bitince (AP=0 → yeni gün) TÜM adalardaki işaretli karolar çöker. Oyuncu hangi
    ///     adadaysa orada BÖLGESEL deprem + görsel silme; DİĞER adalarda VERİ olarak silinir
    ///     (o adaya dönünce kalıcı çökmüş gelir) ve aktif adadaki karolar UYARI için titrer.
    /// Durum ada başına saklanır (<see cref="_mapStates"/>), böylece ışınlanmak sayacı SIFIRLAMAZ.
    /// Oyuncu üstündeki karo / kule / PORTAL karoları korunur (portal = adalar arası tek geçiş).
    /// Sadece Overworld'de işler (savaş grid'inde uygulanmaz).
    /// </summary>
    public class MapCollapseManager : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private HexGridManager     _gridManager;
        [SerializeField] private ActionPointManager _apManager;
        [SerializeField] private PlayerController   _player;
        [SerializeField] private CollapseConfig     _config;
        [SerializeField] private Camera             _camera;
        [Tooltip("Yalnız Overworld'de collapse uygula (savaş grid'inde değil). Atanmazsa hep uygulanır.")]
        [SerializeField] private GameStateManager   _state;
        [Tooltip("Çöküş anında kırmızı su-dalgası efekti (göle taş atma). Atanmazsa dalga çizilmez " +
                 "(çöküş yine olur).")]
        [SerializeField] private CollapseWaveEffect _wave;

        [Header("Çöküş Görseli")]
        [SerializeField] private Material _collapsedMaterial;

        [Header("Uyarı (kırmızı çizgi + AP)")]
        [SerializeField] private Color  _outlineColor = new Color(1f, 0.15f, 0.1f);
        [SerializeField] private float  _outlineWidth = 0.08f;
        [SerializeField] private float  _outlineLift  = 0.06f;
        [SerializeField] private Color  _labelColor   = new Color(1f, 0.35f, 0.25f);

        [Header("Deprem (bölgesel sarsıntı — aktif adada silinen karo)")]
        [SerializeField] private float _shakeDuration  = 0.7f;
        [SerializeField] private float _shakeMagnitude = 0.12f;

        // ── Çöküşten MUAF karolar (TASK-007) ────────────────────────────────
        // Zorunlu görev karoları buraya girer (ChapterNodeManager bildirir): bunlar silinirse bölüm
        // bitirilemez hale gelirdi. Kule ve portal muafiyeti ayrıca PickDoomed içinde.
        private readonly HashSet<HexCoordinate> _protectedTiles = new();

        /// <summary>Çöküşten muaf karoları bildir (zorunlu görevler). Her yeni haritada yenilenir.</summary>
        public void SetProtectedTiles(IEnumerable<HexCoordinate> coords)
        {
            _protectedTiles.Clear();
            if (coords != null) foreach (var c in coords) _protectedTiles.Add(c);
        }

        public int  TotalRemovedTiles { get; private set; }
        public bool IsCollapseActive  { get; private set; }
        public event Action<int, int> OnTileCollapsed;

        private int _lastProcessedDay = 0;

        // ── Çöküş durumu (1 bölüm = 1 harita → tek durum) ────────────────────
        // _doomed    = işaretlenmiş karolar + HANGİ GÜN silinecekleri. Karo, silineceği günden
        //              CollapseConfig.TelegraphDays kadar ÖNCE işaretlenir → oyuncu görmeden
        //              bir karo ASLA kaybolmaz (TASK-007: "sessiz silinme yok").
        // _collapsed = kalıcı silinmiş karolar (savaştan dönünce yeniden uygulanır)
        private readonly List<(HexCoordinate coord, int removeDay)> _doomed = new();
        private readonly HashSet<HexCoordinate> _collapsed = new();

        private bool IsDoomed(HexCoordinate c)
        {
            for (int i = 0; i < _doomed.Count; i++) if (_doomed[i].coord.Equals(c)) return true;
            return false;
        }

        private bool InOverworld => _state == null || _state.State == GameState.Overworld;

        /// <summary>Silinecek karo işaretlenirken kaç gün önceden uyarılır.</summary>
        private int TelegraphDays => _config != null ? _config.TelegraphDays : 2;

        // Seçildi ama henüz AÇIKLANMADI (dalga+yıldırım bekliyor) → çerçeve/sayaç gizli.
        private readonly HashSet<HexCoordinate> _pendingReveal = new();

        // Aktif adanın kırmızı uyarı çizgileri (yalnız görünen ada için tutulur).
        private readonly Dictionary<HexCoordinate, LineRenderer> _outlines = new();
        private Transform _outlineRoot;
        private Material  _lineMat;
        private GUIStyle  _labelStyle;

        private void Awake()
        {
            if (_camera == null) _camera = Camera.main;
            _outlineRoot = new GameObject("CollapseWarnings").transform;
            _outlineRoot.SetParent(transform, false);
        }

        private void OnEnable()
        {
            if (_apManager   != null) _apManager.OnTimeAdvanced += HandleTimeAdvanced;
            // Savaştan dönüş / harita yeniden üretimi → çöküş durumunu yeniden uygula.
            if (_gridManager != null) _gridManager.OnGridRegenerated += ApplyCollapseStateForCurrentMap;
            if (_state       != null) _state.OnStateChanged          += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (_apManager   != null) _apManager.OnTimeAdvanced -= HandleTimeAdvanced;
            if (_gridManager != null) _gridManager.OnGridRegenerated -= ApplyCollapseStateForCurrentMap;
            if (_state       != null) _state.OnStateChanged          -= HandleStateChanged;
        }

        private void Start() => ApplyCollapseStateForCurrentMap();

        private void HandleStateChanged(GameState state)
        {
            if (state == GameState.Overworld) ApplyCollapseStateForCurrentMap();
        }

        private void HandleTimeAdvanced(int day, int slot, string slotName)
        {
            if (slot != 0) return;                 // yalnız gün sınırında
            if (day <= _lastProcessedDay) return;
            _lastProcessedDay = day;
            StartCoroutine(DayBoundaryRoutine(day));
        }

        private IEnumerator DayBoundaryRoutine(int day)
        {
            // 0) Vadesi GELMİŞ (removeDay <= bugün) işaretleri topla ve listeden düş. Uyarı süresi
            //    TelegraphDays gün olduğu için bunlar en az o kadar gündür kırmızı duruyordu.
            var todays = new List<HexCoordinate>();
            for (int i = _doomed.Count - 1; i >= 0; i--)
                if (_doomed[i].removeDay <= day) { todays.Add(_doomed[i].coord); _doomed.RemoveAt(i); }
            todays.Reverse();                        // işaretlenme sırası korunsun
            if (todays.Count > 0) ClearOutlines();   // çökenlerin eski kırmızı çerçeveleri

            // 1) İLERİDE (day + TelegraphDays) çökecek YENİ karoları ŞİMDİ seç — veri hemen
            //    kesinleşir. GÖRSEL açıklama (kırmızı çerçeve + sayaç) hemen DEĞİL: dalga cephesi
            //    karonun üstünden geçerken YILDIRIMLA gelir (PlayWave → RevealDoomedTile).
            int targetDay = day + TelegraphDays;
            var collapsingNow = new HashSet<HexCoordinate>(todays);
            int count = _config != null ? _config.GetRemovalCount(targetDay) : 0;
            List<HexCell> newDoomed = (count > 0 && InOverworld)
                ? PickDoomed(count, targetDay, collapsingNow)
                : null;
            bool revealsAssigned = false;

            // İlk çıkan dalga açıklamaları taşır; diğerleri sade dalga.
            void PlayWave(Vector3 c, float d)
            {
                if (_wave == null) return;
                if (!revealsAssigned && newDoomed != null && newDoomed.Count > 0)
                { _wave.PlayWithReveals(c, d, newDoomed, RevealDoomedTile); revealsAssigned = true; }
                else _wave.Play(c, d);
            }

            // 2) Çöküşler: deprem + görsel silme + karodan dalga.
            if (todays.Count > 0 && InOverworld)
            {
                IsCollapseActive = true;
                foreach (var coord in todays)
                {
                    _collapsed.Add(coord);
                    if (_gridManager.TryGetCell(coord, out HexCell cell) && cell.IsWalkable)
                    {
                        PlayWave(cell.WorldPosition, 0f);
                        StartCoroutine(ShakeAndRemove(cell));
                    }
                    yield return new WaitForSeconds(0.12f);
                }
                yield return new WaitForSeconds(_shakeDuration);
                IsCollapseActive = false;
            }
            else
            {
                // Savaştayken gün döndüyse: veri işlensin, görseli overworld'e dönünce uygulanır.
                foreach (var coord in todays)
                {
                    if (!_collapsed.Add(coord)) continue;
                    TotalRemovedTiles++;
                    OnTileCollapsed?.Invoke(1, TotalRemovedTiles);
                }
            }

            // 3) Hiç dalga çıkmadıysa (örn. İLK uyarı günü — henüz çöküş yok) yıldırımlar
            //    dalgasız, art arda çakarak yeni işaretleri açıklar.
            if (newDoomed != null && newDoomed.Count > 0 && !revealsAssigned)
            {
                if (_wave != null) _wave.StrikeSeries(newDoomed, RevealDoomedTile);
                else foreach (var c in newDoomed) RevealDoomedTile(c);
            }
        }

        // Yeni işaretlenecek karoları SEÇER (veri: _doomed + _pendingReveal). Kırmızı çerçeve /
        // sayaç BURADA ÇİZİLMEZ — dalga cephesi karonun üstünden geçerken yıldırımla açıklanır
        // (RevealDoomedTile). alsoExclude = şu an çökmekte olanlar (yeniden seçilmesinler).
        // removeDay = bu karoların SİLİNECEĞİ gün (bugün + uyarı süresi).
        private List<HexCell> PickDoomed(int count, int removeDay, HashSet<HexCoordinate> alsoExclude)
        {
            HexCoordinate playerCoord = _player != null ? _player.CurrentCoord : default;
            var candidates = new List<HexCell>();
            foreach (HexCell cell in _gridManager.Cells.Values)
            {
                if (!cell.IsWalkable)                        continue;
                if (cell.Coordinate == playerCoord)          continue;
                if (cell.CellType == CellType.Watchtower)    continue;
                if (IsDoomed(cell.Coordinate))               continue;
                if (_collapsed.Contains(cell.Coordinate))    continue;
                if (alsoExclude != null && alsoExclude.Contains(cell.Coordinate)) continue;
                // ZORUNLU GÖREV karoları asla silinmez (TASK-007) — bölüm bitirilemez hale gelmesin.
                if (_protectedTiles.Contains(cell.Coordinate)) continue;
                candidates.Add(cell);
            }

            var picked = new List<HexCell>();
            for (int i = 0; i < count && candidates.Count > 0; i++)
            {
                int idx = UnityEngine.Random.Range(0, candidates.Count);
                HexCell cell = candidates[idx];
                candidates.RemoveAt(idx);
                _doomed.Add((cell.Coordinate, removeDay));
                _pendingReveal.Add(cell.Coordinate);
                picked.Add(cell);
            }
            return picked;
        }

        // Dalga cephesi işaretli karonun üstünden geçti → yıldırım çaktı → çerçeve + sayaç
        // ANCAK ŞİMDİ görünür olur (CollapseWaveEffect callback'i).
        private void RevealDoomedTile(HexCell cell)
        {
            if (cell == null) return;
            if (!_pendingReveal.Remove(cell.Coordinate)) return;   // harita değişti / zaten açıklandı
            if (!InOverworld) return;
            CreateOutline(cell);
        }

        /// <summary>Overworld'e dönünce: kalıcı silinmişleri grid'e uygula + işaretli karoların
        /// kırmızı çizgilerini yeniden çiz. Savaş grid'inde çalışmaz.</summary>
        public void ApplyCollapseStateForCurrentMap()
        {
            if (_gridManager == null || _gridManager.Cells == null) return;
            ClearOutlines();
            _pendingReveal.Clear();   // dalga yarıda kaldıysa: işaretleri direkt çiz
            if (!InOverworld) return;                 // savaş grid'ine overworld çöküşünü uygulama

            foreach (var coord in _collapsed)
                if (_gridManager.TryGetCell(coord, out HexCell cell) && cell.IsWalkable)
                    RemoveTile(cell);

            foreach (var (coord, _) in _doomed)
                if (_gridManager.TryGetCell(coord, out HexCell cell))
                    CreateOutline(cell);
        }

        /// <summary>Yeni bölüm/harita başlarken çöküş durumunu tamamen sıfırlar (TASK-007 retry).</summary>
        public void ResetCollapse()
        {
            _doomed.Clear();
            _collapsed.Clear();
            _pendingReveal.Clear();
            TotalRemovedTiles = 0;
            _lastProcessedDay = 0;
            ClearOutlines();
        }

        // ── Kırmızı hex çizgisi ──────────────────────────────────────────────
        private void CreateOutline(HexCell cell)
        {
            if (_lineMat == null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
                _lineMat = new Material(sh);
                if (_lineMat.HasProperty("_BaseColor")) _lineMat.SetColor("_BaseColor", _outlineColor);
                if (_lineMat.HasProperty("_Color"))     _lineMat.SetColor("_Color",     _outlineColor);
            }

            var go = new GameObject($"Warn_{cell.Coordinate}");
            go.transform.SetParent(_outlineRoot, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace  = true;
            lr.loop           = true;
            lr.positionCount  = 6;
            lr.widthMultiplier = _outlineWidth;
            lr.material       = _lineMat;
            lr.startColor = lr.endColor = _outlineColor;
            lr.numCornerVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Vector3 baseP = cell.WorldPosition + Vector3.up * (cell.SurfaceHeight + _outlineLift);
            for (int i = 0; i < 6; i++)
            {
                Vector3 c = HexMetrics.Corners[i] * 0.95f; // karo footprint'i (0.95)
                lr.SetPosition(i, baseP + new Vector3(c.x, 0f, c.z));
            }
            _outlines[cell.Coordinate] = lr;
        }

        private void ClearOutlines()
        {
            foreach (var kv in _outlines) if (kv.Value != null) Destroy(kv.Value.gameObject);
            _outlines.Clear();
        }

        // ── Bölgesel deprem + silme (aktif ada) ──────────────────────────────
        private IEnumerator ShakeAndRemove(HexCell cell)
        {
            Transform vis = cell.Visual != null ? cell.Visual.transform : null;
            Vector3 basePos = vis != null ? vis.position : cell.WorldPosition;

            float t = 0f;
            while (t < _shakeDuration && vis != null)
            {
                t += Time.deltaTime;
                float damp = 1f - (t / _shakeDuration);              // sönümlenen sarsıntı
                Vector3 j = new Vector3(
                    (UnityEngine.Random.value - 0.5f),
                    (UnityEngine.Random.value - 0.5f) * 0.6f,
                    (UnityEngine.Random.value - 0.5f)) * (_shakeMagnitude * damp);
                vis.position = basePos + j;
                yield return null;
            }
            if (vis != null) vis.position = basePos;

            RemoveTile(cell);
            TotalRemovedTiles++;
            OnTileCollapsed?.Invoke(1, TotalRemovedTiles);
            Debug.Log($"[Collapse] Karo silindi: {cell.Coordinate} | Toplam: {TotalRemovedTiles}");
        }

        private void RemoveTile(HexCell cell)
        {
            cell.IsWalkable = false;
            cell.CellType   = CellType.Obstacle;

            if (cell.MeshRenderer != null && _collapsedMaterial != null)
                cell.MeshRenderer.sharedMaterial = _collapsedMaterial;
            else if (cell.Visual != null)
                cell.Visual.SetActive(false);
        }

        // ── Karo üstü "kalan AP" etiketi (işaretli karolar) ─────────────────
        private void OnGUI()
        {
            if (MenuState.HudsHidden) return;   // augment karti / tam-ekran menu aciksa IMGUI cizilmez
            if (_camera == null || _apManager == null || !InOverworld) return;
            if (_doomed.Count == 0) return;

            if (_labelStyle == null)
                _labelStyle = new GUIStyle(GUI.skin.label)
                { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 14 };
            _labelStyle.normal.textColor = _labelColor;

            // Sanal 1920x1080 ekrana çiz → sayaç yazısı her çözünürlükte aynı oranda.
            using var _scale = HudScale.Scaled();

            int ap = _apManager.APRemainingToday;
            foreach (var (coord, _) in _doomed)
            {
                if (_pendingReveal.Contains(coord)) continue;   // yıldırım çakana dek sayaç gizli
                if (!_gridManager.TryGetCell(coord, out HexCell cell)) continue;
                Vector3 world = cell.WorldPosition + Vector3.up * (cell.SurfaceHeight + 0.5f);
                Vector3 sp = _camera.WorldToScreenPoint(world);
                if (sp.z <= 0f) continue;                            // kamera arkası
                // WorldToScreenPoint gerçek PİKSEL verir → ölçekli GUI uzayına çevir.
                Vector2 g = HudScale.ToGui(sp);
                var rect = new Rect(g.x - 24f, g.y - 12f, 48f, 24f);
                GUI.Label(rect, ap.ToString(), _labelStyle);
            }
        }
    }
}
