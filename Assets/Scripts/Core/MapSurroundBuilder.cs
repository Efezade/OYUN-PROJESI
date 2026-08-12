using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// HARİTANIN DIŞINI DOLDURUR — hiçbir yönde boşluk görünmesin (kullanıcı isteği 2026-08-12).
    ///
    /// SORUN: tahta dikdörtgen, kıta organik. Kıtanın dışındaki koordinatlarda <c>HexGridManager</c>
    /// hücre ÜRETMEZ (bu bilinçli: kare siluet olmasın diye). Sonuç: kıyının ötesinde hiçlik —
    /// kamera biraz kayınca haritanın nerede bittiği görünüyor. Aynısı savaş arenasında da var,
    /// üstelik orada tahta çok daha küçük (10×8) olduğu için daha belirgin.
    ///
    /// ÇÖZÜM — ARAŞTIRMADAN GELEN ÜÇ KATMAN (bkz. Docs/DECISION_LOG.md):
    /// tür oyunlarında (Civ, Age of Wonders, For The King) sınır üç şeyle gizlenir ve üçü BİRLİKTE
    /// kullanılır; teki eksik olursa bir kamera açısında mutlaka açık kalır:
    ///   1. DÜZLEM  — ufka kadar tek yüzey (Civ'in okyanusu). Garantiyi bu verir: altta her zaman
    ///      bir şey vardır.
    ///   2. BANT    — tahtanın dışına birkaç halka GERÇEK hex karosu. Karo/düzlem geçişindeki
    ///      keskin çizgiyi kırar (mesh terminolojisinde "skirt").
    ///   3. SÜSLEME — bandın ötesine serpilmiş ağaç/kaya/sis. "Devam eden dünya" hissini veren
    ///      katman; düz renk tek başına "arka plan" gibi durur.
    /// Sınır DİYEJETİK: görünmez duvar yok. Kıta zaten tahta kenarına değmiyor
    /// (<c>TerrainGenerator</c> margin kuralı), dışı su/orman — yani doğal olarak geçilemez.
    ///
    /// PERFORMANS: bant ve süsler HALKA BAŞINA TEK MESH'e birleştirilir (yüzlerce GameObject değil).
    /// 700 karoluk bir bant ~22k vertex → tek çizim çağrısı. Collider YOK: tıklama ışını hiçbir
    /// şeye çarpmaz, hücre olmadığı için <c>TryGetCell</c> de başarısız olur → yürünemez.
    /// Sis sistemine de girmez (sis yalnız <c>Cells</c> üzerinde çalışır).
    /// </summary>
    [DefaultExecutionOrder(-45)]   // HexGridManager (-100) grid'i kurduktan sonra
    public class MapSurroundBuilder : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private HexGridManager   _grid;
        [Tooltip("Opsiyonel — hangi profilin kullanılacağını duruma göre seçer (savaşta orman, " +
                 "overworld'de okyanus). Boşsa hep overworld profili kullanılır.")]
        [SerializeField] private GameStateManager _state;

        [Header("Profiller")]
        [SerializeField] private MapSurroundProfileSO _overworldProfile;
        [SerializeField] private MapSurroundProfileSO _combatProfile;

        private Transform _root;
        private readonly List<Mesh>     _ownedMeshes    = new();
        private readonly List<Material> _ownedMaterials = new();

        // Aynı harita için iki kez kurmayı önler (grid + durum olayı arka arkaya gelebiliyor).
        private MapSurroundProfileSO _builtProfile;
        private int                  _builtSignature;

        // ── Yaşam döngüsü ────────────────────────────────────────────────────

        private void OnEnable()
        {
            if (_grid  != null) _grid.OnGridRegenerated  += HandleGridRegenerated;
            if (_state != null) _state.OnStateChanged    += HandleStateChanged;
            Rebuild();
        }

        private void OnDisable()
        {
            if (_grid  != null) _grid.OnGridRegenerated  -= HandleGridRegenerated;
            if (_state != null) _state.OnStateChanged    -= HandleStateChanged;
        }

        private void HandleGridRegenerated() => Rebuild();

        // Durum değişimi de tetikler: arena, state Deployment'a geçmeden ÖNCE üretiliyor
        // (GameStateManager.EnterDeployment sırası) — yalnız grid olayını dinleseydik savaşın
        // ilk karesinde okyanus profili kalırdı.
        private void HandleStateChanged(GameState state) => Rebuild();

        private MapSurroundProfileSO ActiveProfile
        {
            get
            {
                bool combat = _state != null &&
                              (_state.State == GameState.Combat || _state.State == GameState.Deployment);
                MapSurroundProfileSO p = combat ? _combatProfile : _overworldProfile;
                return p != null ? p : _overworldProfile;
            }
        }

        // ── Kurulum ──────────────────────────────────────────────────────────

        /// <summary>Çevreyi (yeniden) kurar. Aynı harita+profil için tekrar çağrılırsa iş yapmaz.</summary>
        public void Rebuild(bool force = false)
        {
            if (_grid == null) return;
            MapSurroundProfileSO profile = ActiveProfile;
            if (profile == null) return;

            var cells = _grid.Cells;
            if (cells == null || cells.Count == 0) { Clear(); return; }

            int signature = Signature(cells);
            if (!force && profile == _builtProfile && signature == _builtSignature) return;

            Clear();
            _builtProfile   = profile;
            _builtSignature = signature;

            EnsureRoot();
            Build(profile, cells, signature);
        }

        /// <summary>Üretilmiş her şeyi siler (harita değişiminde ilk adım).</summary>
        public void Clear()
        {
            if (_root != null) SafeDestroy(_root.gameObject);
            _root = null;

            foreach (var m in _ownedMeshes)    if (m != null) SafeDestroy(m);
            foreach (var m in _ownedMaterials) if (m != null) SafeDestroy(m);
            _ownedMeshes.Clear();
            _ownedMaterials.Clear();

            _builtProfile   = null;
            _builtSignature = 0;
        }

        private void EnsureRoot()
        {
            var go = new GameObject("MapSurround");
            // Sahne dosyasına YAZILMAZ: üretilmiş yüz binlerce vertex xd.unity'yi şişirirdi
            // (sahne zaten 5 MB). Editörde önizleme için üretilse bile kaydedilmez.
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(transform, false);
            _root = go.transform;
        }

        private static int Signature(IReadOnlyDictionary<HexCoordinate, HexCell> cells)
        {
            // Hücre sayısı + sınırlar: farklı harita → farklı imza. (Tam hash gereksiz; amaç
            // yalnız "aynı haritayı iki kez kurma".)
            int minQ = int.MaxValue, maxQ = int.MinValue, minR = int.MaxValue, maxR = int.MinValue;
            foreach (var c in cells.Keys)
            {
                if (c.Q < minQ) minQ = c.Q;
                if (c.Q > maxQ) maxQ = c.Q;
                if (c.R < minR) minR = c.R;
                if (c.R > maxR) maxR = c.R;
            }
            unchecked { return ((cells.Count * 397) ^ minQ) * 397 ^ maxQ * 31 ^ minR * 17 ^ maxR; }
        }

        // ── Üretim ───────────────────────────────────────────────────────────

        private void Build(MapSurroundProfileSO p, IReadOnlyDictionary<HexCoordinate, HexCell> cells,
                           int seed)
        {
            // 1) Bant halkalarını hesapla: tahtadan dışa doğru genişleyen dalga.
            var ringOf = new Dictionary<HexCoordinate, int>(cells.Count * 4);
            foreach (var c in cells.Keys) ringOf[c] = 0;                 // 0 = oynanan tahta

            var frontier = new List<HexCoordinate>(cells.Keys);
            var next     = new List<HexCoordinate>();
            var bands    = new List<List<HexCoordinate>>();

            for (int ring = 1; ring <= p.bandRings; ring++)
            {
                next.Clear();
                foreach (var c in frontier)
                    for (int d = 0; d < 6; d++)
                    {
                        HexCoordinate n = c.GetNeighbor(d);
                        if (ringOf.ContainsKey(n)) continue;
                        ringOf[n] = ring;
                        next.Add(n);
                    }
                if (next.Count == 0) break;
                bands.Add(new List<HexCoordinate>(next));
                frontier = new List<HexCoordinate>(next);
            }

            // 2) Dünya sınırları (düzlem ve süs serpme alanı bundan türer).
            ComputeBounds(cells.Keys, out Vector3 min, out Vector3 max);

            // 3) Düzlem — en altta, ufka kadar.
            BuildPlane(p, min, max);

            // 4) Bant — halka başına tek mesh, rengi dıştaki düzleme doğru kayar.
            var rnd = new System.Random(seed);
            for (int i = 0; i < bands.Count; i++)
            {
                float t = bands.Count <= 1 ? 1f : i / (float)(bands.Count - 1);
                Color c = Color.Lerp(p.bandColorNear, p.bandColorFar, t);
                BuildBandRing(p, bands[i], c, rnd, i, t);
            }

            // 5) Süsleme — bandın ötesine serpilir, oynanan tahtaya yaklaştırılmaz.
            int props = BuildProps(p, ringOf, min, max, rnd);

            // Play'e basmadan da doğrulanabilsin diye sayılar loglanır (Unity'yi açmadan batch
            // koşturup "çevre gerçekten üretildi mi" sorusunun cevabı bu satırdır).
            int bandTiles = 0;
            foreach (var b in bands) bandTiles += b.Count;
            Debug.Log($"[Cevre] {p.displayName}: {cells.Count} tahta karosu cevrelendi — " +
                      $"{bands.Count} halka / {bandTiles} bant karosu, {props} susleme, " +
                      $"duzlem {(max.x - min.x) + p.planeMargin * 2f:F0}x{(max.z - min.z) + p.planeMargin * 2f:F0} birim.");
        }

        private void ComputeBounds(IEnumerable<HexCoordinate> coords, out Vector3 min, out Vector3 max)
        {
            min = new Vector3(float.MaxValue, 0f, float.MaxValue);
            max = new Vector3(float.MinValue, 0f, float.MinValue);
            float size = _grid.HexSize;

            foreach (var c in coords)
            {
                Vector3 w = c.ToWorldPosition(size);
                if (w.x < min.x) min.x = w.x;
                if (w.x > max.x) max.x = w.x;
                if (w.z < min.z) min.z = w.z;
                if (w.z > max.z) max.z = w.z;
            }
        }

        // ── Katman 1: sonsuz düzlem ──────────────────────────────────────────

        private void BuildPlane(MapSurroundProfileSO p, Vector3 min, Vector3 max)
        {
            float m = p.planeMargin;
            var mesh = new Mesh { name = "SurroundPlane" };
            float x0 = min.x - m, x1 = max.x + m, z0 = min.z - m, z1 = max.z + m;
            float y  = p.planeHeight;

            mesh.vertices  = new[] { new Vector3(x0, y, z0), new Vector3(x0, y, z1),
                                     new Vector3(x1, y, z1), new Vector3(x1, y, z0) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.normals   = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.RecalculateBounds();

            SpawnMesh("Plane", mesh, MakeMaterial(p.planeColor, p, p.planeSmoothness));
        }

        // ── Katman 2: geçiş bandı ────────────────────────────────────────────

        /// <param name="t">0 = tahtaya en yakın halka, 1 = en dış halka.</param>
        private void BuildBandRing(MapSurroundProfileSO p, List<HexCoordinate> coords, Color color,
                                   System.Random rnd, int ringIndex, float t)
        {
            if (coords.Count == 0) return;

            // ETEK (skirt): bant dışa gittikçe düzlemin yüksekliğine iner. Bu olmadan bandın son
            // halkası ile düzlem arasında karo kalınlığı kadar (0.3) bir basamak kalır ve tam da
            // gizlemeye çalıştığımız "harita burada bitiyor" çizgisi ortaya çıkar.
            // +0.02: son halka düzlemle ÇAKIŞMASIN (z-fighting titremesi).
            float topY  = Mathf.Lerp(HexMetrics.TileHeight, p.planeHeight + 0.02f, t);
            float scaleY = topY / HexMetrics.TileHeight;

            Mesh proto = HexMetrics.CreateHexMesh(p.tileScale);
            Vector3[] pv = proto.vertices;
            int[]     pt = proto.triangles;
            SafeDestroy(proto);                       // yalnız şablon olarak kullanıldı

            var verts = new List<Vector3>(coords.Count * pv.Length);
            var tris  = new List<int>(coords.Count * pt.Length);
            float size = _grid.HexSize;

            foreach (var c in coords)
            {
                Vector3 origin = c.ToWorldPosition(size);
                // Yükseklik oynaması: karonun ÜST yüzü hafifçe alçalıp yükselir (düz bir tabaka
                // yerine dalgalı bir yüzey). Tabanı yerinde kalır, yalnız tepe oynar.
                float h = scaleY + (float)(rnd.NextDouble() * 2.0 - 1.0) * (p.heightJitter / HexMetrics.TileHeight);
                if (h < 0.02f) h = 0.02f;

                int baseIndex = verts.Count;
                for (int i = 0; i < pv.Length; i++)
                {
                    Vector3 v = pv[i];
                    v.y *= h;
                    verts.Add(origin + v);
                }
                for (int i = 0; i < pt.Length; i++) tris.Add(baseIndex + pt[i]);
            }

            var mesh = new Mesh { name = $"SurroundBand_{ringIndex}" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;   // 65k vertex sınırını aşabilir
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            SpawnMesh($"Band_{ringIndex}", mesh, MakeMaterial(color, p, 0.08f));
        }

        // ── Katman 3: süsleme ────────────────────────────────────────────────

        /// <returns>Yerleştirilen süs sayısı.</returns>
        private int BuildProps(MapSurroundProfileSO p, Dictionary<HexCoordinate, int> ringOf,
                               Vector3 min, Vector3 max, System.Random rnd)
        {
            if (p.propChance <= 0f || p.propLimit <= 0 || p.propMargin <= 0.01f) return 0;

            Mesh proto = ConeMesh(7);
            Vector3[] pv = proto.vertices;
            int[]     pt = proto.triangles;
            SafeDestroy(proto);

            var verts = new List<Vector3>();
            var tris  = new List<int>();

            float step = Mathf.Max(0.5f, p.propSpacing);
            float x0 = min.x - p.propMargin, x1 = max.x + p.propMargin;
            float z0 = min.z - p.propMargin, z1 = max.z + p.propMargin;
            int   placed = 0;

            // Tavan aşılacaksa OLASILIĞI düşür, döngüyü erken kesme. Kesseydik süsler taramanın
            // başladığı köşeden dolar, tahtanın diğer yanı ÇIPLAK kalırdı — tam da kapatmaya
            // çalıştığımız boşluğu bir kenarda geri açardı.
            int   slotsX   = Mathf.Max(1, Mathf.FloorToInt((x1 - x0) / step) + 1);
            int   slotsZ   = Mathf.Max(1, Mathf.FloorToInt((z1 - z0) / step) + 1);
            float expected = slotsX * (float)slotsZ * p.propChance;
            float chance   = expected > p.propLimit ? p.propChance * (p.propLimit / expected) : p.propChance;

            for (float x = x0; x <= x1 && placed < p.propLimit; x += step)
                for (float z = z0; z <= z1 && placed < p.propLimit; z += step)
                {
                    if (rnd.NextDouble() > chance) continue;

                    // Izgarayı kır: düzenli sıralar "dikilmiş fidanlık" gibi görünür.
                    float px = x + (float)(rnd.NextDouble() - 0.5) * step * 0.9f;
                    float pz = z + (float)(rnd.NextDouble() - 0.5) * step * 0.9f;

                    // Oynanan tahtanın üstüne ve dibine süs konmaz.
                    HexCoordinate hc = _grid.WorldToHex(new Vector3(px, 0f, pz));
                    if (ringOf.TryGetValue(hc, out int ring) && ring <= p.propKeepOut) continue;

                    float w = Mathf.Lerp(p.propWidth.x,  p.propWidth.y,  (float)rnd.NextDouble());
                    float h = Mathf.Lerp(p.propHeight.x, p.propHeight.y, (float)rnd.NextDouble());
                    float yaw = (float)rnd.NextDouble() * 360f;
                    var rot = Quaternion.Euler(0f, yaw, 0f);
                    var origin = new Vector3(px, p.planeHeight - 0.02f, pz);

                    int baseIndex = verts.Count;
                    for (int i = 0; i < pv.Length; i++)
                    {
                        Vector3 v = pv[i];
                        v = new Vector3(v.x * w, v.y * h, v.z * w);
                        verts.Add(origin + rot * v);
                    }
                    for (int i = 0; i < pt.Length; i++) tris.Add(baseIndex + pt[i]);
                    placed++;
                }

            if (placed == 0) return 0;

            var mesh = new Mesh { name = "SurroundProps" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            SpawnMesh("Props", mesh, MakeMaterial(p.propColor, p, 0.05f));
            return placed;
        }

        /// <summary>Düşük-poli koni (taban y=0, tepe y=1, yarıçap 0.5) — ağaç/kaya/sis öbeği.
        /// Runtime'da üretilir: editörden asset referansı taşımak bu kadar basit bir şekil için
        /// gereksiz bir bağ olurdu (referans boş kalırsa çevre sessizce çıplak kalır).</summary>
        private static Mesh ConeMesh(int sides)
        {
            var verts = new Vector3[sides + 2];
            verts[0] = Vector3.zero;
            verts[1] = Vector3.up;
            for (int i = 0; i < sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                verts[2 + i] = new Vector3(Mathf.Cos(a) * 0.5f, 0f, Mathf.Sin(a) * 0.5f);
            }

            var tris = new List<int>(sides * 6);
            for (int i = 0; i < sides; i++)
            {
                int cur = 2 + i, nxt = 2 + (i + 1) % sides;
                tris.Add(1); tris.Add(nxt); tris.Add(cur);
                tris.Add(0); tris.Add(cur); tris.Add(nxt);
            }

            var mesh = new Mesh { name = "SurroundCone", vertices = verts, triangles = tris.ToArray() };
            mesh.RecalculateNormals();
            return mesh;
        }

        // ── Yardımcılar ──────────────────────────────────────────────────────

        private void SpawnMesh(string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(_root, false);

            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial     = mat;
            // Gölge YOK: dev bir düzlemin gölge geçişine girmesi bedava değil ve çevre zaten
            // dekor — oynanan tahtanın gölgelerini bozmasın.
            mr.shadowCastingMode  = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows     = false;

            _ownedMeshes.Add(mesh);
        }

        private Material MakeMaterial(Color color, MapSurroundProfileSO p, float smoothness)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(sh) { name = $"Surround_{color}" };

            Color c = color * p.brightness;
            c.a = 1f;
            if (m.HasProperty("_BaseColor"))  m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color"))      m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            m.enableInstancing = true;

            _ownedMaterials.Add(m);
            return m;
        }

        // Editörde (Play dışı) Destroy çalışmaz; önizleme üretimi de bu yoldan temizlenmeli.
        private static void SafeDestroy(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else                       DestroyImmediate(o);
        }
    }
}
