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
        [Tooltip("Hangi ada (CurrentMap) — 3x3 dünya. Yoksa tek ada (1) varsayılır.")]
        [SerializeField] private WorldGridManager   _world;
        [Tooltip("Yalnız Overworld'de collapse uygula (savaş grid'inde değil). Atanmazsa hep uygulanır.")]
        [SerializeField] private GameStateManager   _state;
        [Tooltip("Karo çökünce üstündeki özü de siler. Boşsa Awake'te sahnede aranır.")]
        [SerializeField] private EssenceNodeManager _essenceNodes;
        [Tooltip("Çöküş anında kırmızı su-dalgası efekti (göle taş atma). 3x3'te uzak adada çöküş " +
                 "olursa gecikmeyle gelir. Atanmazsa dalga çizilmez (çöküş yine olur).")]
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

        public int  TotalRemovedTiles { get; private set; }
        public bool IsCollapseActive  { get; private set; }
        public event Action<int, int> OnTileCollapsed;

        private int _lastProcessedDay = 0;

        // ── Ada başına çöküş durumu ──────────────────────────────────────────
        // doomed    = bu gün sonunda çökecek (işaretli) karolar
        // collapsed = kalıcı silinmiş karolar (o adaya dönünce yeniden uygulanır)
        private class MapCollapse
        {
            public readonly List<HexCoordinate>    doomed    = new();
            public readonly HashSet<HexCoordinate> collapsed = new();
        }
        private readonly Dictionary<int, MapCollapse> _mapStates = new();
        private MapCollapse State(int map)
        {
            if (!_mapStates.TryGetValue(map, out var s)) { s = new MapCollapse(); _mapStates[map] = s; }
            return s;
        }
        private int CurrentMap => _world != null ? _world.CurrentMap : 1;
        private bool InOverworld => _state == null || _state.State == GameState.Overworld;

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
            if (_essenceNodes == null) _essenceNodes = FindObjectOfType<EssenceNodeManager>();
            _outlineRoot = new GameObject("CollapseWarnings").transform;
            _outlineRoot.SetParent(transform, false);
        }

        private void OnEnable()
        {
            if (_apManager   != null) _apManager.OnTimeAdvanced += HandleTimeAdvanced;
            // Harita geçişi / savaştan dönüş → aktif adanın çöküş durumunu yeniden uygula
            // (ClearWarnings YERİNE — durum artık ada başına saklandığı için sıfırlanmaz).
            if (_gridManager != null) _gridManager.OnGridRegenerated += ApplyCollapseStateForCurrentMap;
            if (_world       != null) _world.OnMapChanged            += ApplyCollapseStateForCurrentMap;
            if (_state       != null) _state.OnStateChanged          += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (_apManager   != null) _apManager.OnTimeAdvanced -= HandleTimeAdvanced;
            if (_gridManager != null) _gridManager.OnGridRegenerated -= ApplyCollapseStateForCurrentMap;
            if (_world       != null) _world.OnMapChanged            -= ApplyCollapseStateForCurrentMap;
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
            // 0) Dün işaretlenenleri (BUGÜN çökecekler) tüm adalardan topla ve listelerden düş.
            var todays = new List<(int map, List<HexCoordinate> coords)>();
            foreach (int map in new List<int>(_mapStates.Keys))
            {
                MapCollapse s = State(map);
                if (s.doomed.Count == 0) continue;
                todays.Add((map, new List<HexCoordinate>(s.doomed)));
                s.doomed.Clear();
            }
            if (todays.Count > 0) ClearOutlines();   // çökenlerin eski kırmızı çerçeveleri

            // 1) Bu gün sonunda çökecek YENİ karoları ŞİMDİ seç (veri hemen kesinleşir; ışınlanma
            //    sayacı bozamaz). GÖRSEL açıklama (kırmızı çerçeve + sayaç) hemen DEĞİL — dalga
            //    cephesi karonun üstünden geçerken YILDIRIMLA gelir (PlayWave → RevealDoomedTile).
            HashSet<HexCoordinate> collapsingNow = null;
            foreach (var (map, coords) in todays)
                if (map == CurrentMap) { collapsingNow = new HashSet<HexCoordinate>(coords); break; }
            int count = _config != null ? _config.GetRemovalCount(day) : 0;
            List<HexCell> newDoomed = (count > 0 && InOverworld) ? PickDoomed(count, collapsingNow) : null;
            bool revealsAssigned = false;

            // İlk çıkan dalga açıklamaları taşır; diğerleri sade dalga.
            void PlayWave(Vector3 c, float d)
            {
                if (_wave == null) return;
                if (!revealsAssigned && newDoomed != null && newDoomed.Count > 0)
                { _wave.PlayWithReveals(c, d, newDoomed, RevealDoomedTile); revealsAssigned = true; }
                else _wave.Play(c, d);
            }

            // 2) Çöküşler: aktif ada = deprem + görsel silme + karodan dalga; uzak ada = veri +
            //    sanal-konumlu dalga (halka o adanın yönünden gerçek mesafeyi katedip gelir).
            foreach (var (map, coords) in todays)
            {
                MapCollapse s = State(map);
                if (map == CurrentMap && InOverworld)
                {
                    IsCollapseActive = true;
                    foreach (var coord in coords)
                    {
                        s.collapsed.Add(coord);
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
                    int n = 0;
                    foreach (var coord in coords)
                    {
                        if (!s.collapsed.Add(coord)) continue;
                        TotalRemovedTiles++;
                        OnTileCollapsed?.Invoke(1, TotalRemovedTiles);

                        if (_world != null && InOverworld)
                        {
                            Vector3 local = coord.ToWorldPosition(_gridManager.HexSize);
                            PlayWave(_world.VirtualPositionOnCurrentMap(map, local), n * 0.15f);
                        }
                        n++;
                    }
                }
            }

            // 3) Hiç dalga çıkmadıysa (örn. İLK kıyamet günü — henüz çöküş yok) yıldırımlar
            //    dalgasız, art arda çakarak yeni işaretleri açıklar.
            if (newDoomed != null && newDoomed.Count > 0 && !revealsAssigned)
            {
                if (_wave != null) _wave.StrikeSeries(newDoomed, RevealDoomedTile);
                else foreach (var c in newDoomed) RevealDoomedTile(c);
            }
        }

        // Yeni işaretlenecek karoları SEÇER (veri: s.doomed + _pendingReveal). Kırmızı çerçeve /
        // sayaç BURADA ÇİZİLMEZ — dalga cephesi karonun üstünden geçerken yıldırımla açıklanır
        // (RevealDoomedTile). alsoExclude = şu an çökmekte olanlar (yeniden seçilmesinler).
        private List<HexCell> PickDoomed(int count, HashSet<HexCoordinate> alsoExclude)
        {
            MapCollapse s = State(CurrentMap);
            HexCoordinate playerCoord = _player != null ? _player.CurrentCoord : default;
            var tileMap = _gridManager.TileMap;   // portal muafiyeti için boyalı id'ye bak
            var candidates = new List<HexCell>();
            foreach (HexCell cell in _gridManager.Cells.Values)
            {
                if (!cell.IsWalkable)                        continue;
                if (cell.Coordinate == playerCoord)          continue;
                if (cell.CellType == CellType.Watchtower)    continue;
                if (s.doomed.Contains(cell.Coordinate))      continue;
                if (s.collapsed.Contains(cell.Coordinate))   continue;
                if (alsoExclude != null && alsoExclude.Contains(cell.Coordinate)) continue;
                // Portal karoları kıyametten MUAF — adalar arası tek geçiş yolu yok olmasın.
                string id = tileMap != null ? tileMap.GetTileId(cell.Coordinate) : null;
                if (id != null && id.StartsWith("portal", StringComparison.Ordinal)) continue;
                candidates.Add(cell);
            }

            var picked = new List<HexCell>();
            for (int i = 0; i < count && candidates.Count > 0; i++)
            {
                int idx = UnityEngine.Random.Range(0, candidates.Count);
                HexCell cell = candidates[idx];
                candidates.RemoveAt(idx);
                s.doomed.Add(cell.Coordinate);
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

        /// <summary>Aktif adaya girince: kalıcı silinmişleri (collapsed) grid'e uygula +
        /// işaretli (doomed) karoların kırmızı çizgilerini yeniden çiz. Savaş grid'inde çalışmaz.</summary>
        public void ApplyCollapseStateForCurrentMap()
        {
            if (_gridManager == null || _gridManager.Cells == null) return;
            ClearOutlines();
            _pendingReveal.Clear();   // dalga yarıda kaldıysa: bu ada işaretlerini direkt çiz
            if (!InOverworld) return;                 // savaş grid'ine overworld çöküşünü uygulama

            MapCollapse s = State(CurrentMap);
            foreach (var coord in s.collapsed)
                if (_gridManager.TryGetCell(coord, out HexCell cell) && cell.IsWalkable)
                    RemoveTile(cell);

            foreach (var coord in s.doomed)
                if (_gridManager.TryGetCell(coord, out HexCell cell))
                    CreateOutline(cell);
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
            Debug.Log($"[Collapse] Karo silindi (Ada {CurrentMap}): {cell.Coordinate} | Toplam: {TotalRemovedTiles}");
        }

        private void RemoveTile(HexCell cell)
        {
            cell.IsWalkable = false;
            cell.CellType   = CellType.Obstacle;

            if (_essenceNodes != null) _essenceNodes.RemoveNodeAt(cell.Coordinate); // öz de yok olsun

            if (cell.MeshRenderer != null && _collapsedMaterial != null)
                cell.MeshRenderer.sharedMaterial = _collapsedMaterial;
            else if (cell.Visual != null)
                cell.Visual.SetActive(false);
        }

        // ── Karo üstü "kalan AP" etiketi (yalnız aktif adanın doomed'ları) ───
        private void OnGUI()
        {
            if (_camera == null || _apManager == null || !InOverworld) return;
            MapCollapse s = State(CurrentMap);
            if (s.doomed.Count == 0) return;

            if (_labelStyle == null)
                _labelStyle = new GUIStyle(GUI.skin.label)
                { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 14 };
            _labelStyle.normal.textColor = _labelColor;

            // Sanal 1920x1080 ekrana çiz → sayaç yazısı her çözünürlükte aynı oranda.
            using var _scale = HudScale.Scaled();

            int ap = _apManager.APRemainingToday;
            foreach (var coord in s.doomed)
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
