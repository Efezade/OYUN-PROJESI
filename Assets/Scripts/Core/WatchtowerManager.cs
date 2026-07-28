using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Kule (Watchtower) karolarının GÖREVİ: bulunulan HARİTA ADASININ sisini KALICI kaldırmak.
    ///   • Kam bir kulenin 1 karo yakınına gelince alt-orta IMGUI istemi çıkar ("Sisi Kaldir").
    ///   • Onaylanınca: o adanın sisi kalıcı açılır + epik ışık-hüzmesi/halka animasyonu oynar,
    ///     bulutlar süzülüp kaybolur. Aksi halde sis DİNAMİK kalır (baloncuk karakterle gezer).
    ///   • Açılan adalar hatırlanır (CurrentMap index); o adaya geri dönünce sis açık gelir.
    /// Sadece Overworld state'inde çalışır (savaşta grid zaten tam görüş).
    /// </summary>
    public class WatchtowerManager : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private HexGridManager    _grid;
        [SerializeField] private PlayerController  _player;
        [SerializeField] private FogOfWarManager   _fog;
        [Tooltip("Hangi ada (CurrentMap) — 3x3 dünya. Yoksa tek ada (1) varsayılır.")]
        [SerializeField] private WorldGridManager  _world;
        [Tooltip("İstemi yalnız Overworld'de göstermek için — atanmazsa hep gösterilir.")]
        [SerializeField] private GameStateManager  _state;

        [Header("Etkileşim")]
        [Tooltip("Kuleye kaç hex yakında istem çıksın.")]
        [SerializeField] private int _promptRange = 1;

        [Header("Epik Açılış Efekti")]
        [SerializeField] private float _fxDuration   = 1.6f;
        [SerializeField] private float _beamHeight    = 14f;
        [SerializeField] private float _beamIntensity = 3f;
        [SerializeField] private float _ringMaxRadius = 22f;
        [SerializeField] private float _lightIntensity = 8f;
        [SerializeField] private Color _beamColor = new Color(1f, 0.95f, 0.6f);
        [SerializeField] private Color _ringColor = new Color(0.6f, 0.85f, 1f);

        [Header("Kule Göstergesi (sisin üstünde SİYAH İNCİ küre)")]
        [SerializeField] private Color _markerColor  = new Color(0.03f, 0.03f, 0.05f); // koyu (beyaz bulutta net)
        [SerializeField] private float _markerHeight = 1.5f;   // karo üstü — sisin üstünde dursun
        [SerializeField] private float _markerSize   = 0.55f;

        private Transform _markerRoot;
        private Material  _markerMat;
        private readonly Dictionary<HexCoordinate, GameObject> _towerMarkers = new();

        private readonly HashSet<int> _revealedMaps = new();
        private bool    _busy;
        private HexCell _nearbyTower;   // istem için son bulunan yakın kule (yoksa null)

        private int CurrentMap => _world != null ? _world.CurrentMap : 1;

        private void OnEnable()
        {
            if (_player != null) _player.OnMoved       += HandleMoved;
            if (_world  != null) _world.OnMapChanged   += HandleMapChanged;
            if (_state  != null) _state.OnStateChanged += HandleStateChanged;
            if (_grid   != null) _grid.OnGridRegenerated += BuildTowerMarkers; // harita değişince yenile
        }

        private void OnDisable()
        {
            if (_player != null) _player.OnMoved       -= HandleMoved;
            if (_world  != null) _world.OnMapChanged   -= HandleMapChanged;
            if (_state  != null) _state.OnStateChanged -= HandleStateChanged;
            if (_grid   != null) _grid.OnGridRegenerated -= BuildTowerMarkers;
        }

        private void Start()
        {
            ApplyRevealStateForCurrentMap();
            BuildTowerMarkers();
        }

        // Her kule karosunun üstüne sisin üstünde parlayan bir küre koy (kulenin yerini belli eder).
        private void BuildTowerMarkers()
        {
            if (_grid == null || _grid.Cells == null) return;
            if (_markerRoot == null)
            {
                _markerRoot = new GameObject("KuleGosterge").transform;
                _markerRoot.SetParent(transform, false);
            }
            foreach (var kv in _towerMarkers) if (kv.Value != null) Destroy(kv.Value);
            _towerMarkers.Clear();

            if (_markerMat == null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _markerMat = new Material(sh);
                if (_markerMat.HasProperty("_BaseColor")) _markerMat.SetColor("_BaseColor", _markerColor);
                if (_markerMat.HasProperty("_Color"))     _markerMat.SetColor("_Color",     _markerColor);
                // Siyah inci: emissive DEĞİL (bloom'a kaçıp beyazlaşmasın) — parıltı cilalı yüzeyden.
                if (_markerMat.HasProperty("_Smoothness")) _markerMat.SetFloat("_Smoothness", 0.95f);
                if (_markerMat.HasProperty("_Metallic"))   _markerMat.SetFloat("_Metallic",   0.6f);
                _markerMat.DisableKeyword("_EMISSION");
            }

            // YALNIZ boyanmış "kule" karosu → gösterge (sabit/rastgele karolarda değil).
            var tileMap = _grid.TileMap;
            if (tileMap == null) return;
            foreach (var cell in _grid.Cells.Values)
            {
                if (tileMap.GetTileId(cell.Coordinate) != "kule") continue;
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                var col = go.GetComponent<Collider>(); if (col != null) Destroy(col);
                go.name = $"KuleGosterge_{cell.Coordinate}";
                go.transform.SetParent(_markerRoot, false);
                go.transform.position   = cell.WorldPosition + Vector3.up * (cell.SurfaceHeight + _markerHeight);
                go.transform.localScale = Vector3.one * _markerSize;
                go.GetComponent<MeshRenderer>().sharedMaterial = _markerMat;
                _towerMarkers[cell.Coordinate] = go;
            }
        }

        // Sis varken göster (sisin üstünde parlar), sis yokken gizle (kuleyi zaten görüyoruz) +
        // hafif salınım (ışıltı hissi).
        private void Update()
        {
            if (_towerMarkers.Count == 0 || _fog == null) return;
            float bob = Mathf.Sin(Time.time * 2f) * 0.06f;
            foreach (var kv in _towerMarkers)
            {
                if (kv.Value == null) continue;
                bool fogged = !_fog.IsVisible(kv.Key);
                if (kv.Value.activeSelf != fogged) kv.Value.SetActive(fogged);
                if (fogged && _grid.TryGetCell(kv.Key, out HexCell c))
                    kv.Value.transform.position = c.WorldPosition + Vector3.up * (c.SurfaceHeight + _markerHeight + bob);
            }
        }

        // ── Sis durumunu adaya göre uygula ───────────────────────────────────
        private void HandleMapChanged() => ApplyRevealStateForCurrentMap();

        private void HandleStateChanged(GameState state)
        {
            // Savaştan Overworld'e dönünce sis durumunu geri kur (grid savaşta yeniden üretildi).
            if (state == GameState.Overworld) ApplyRevealStateForCurrentMap();
        }

        /// <summary>Mevcut adanın kalıcı-açık durumunu sise uygular (açıksa hepsi görünür,
        /// değilse dinamik baloncuk yeniden kurulur).</summary>
        public void ApplyRevealStateForCurrentMap()
        {
            if (_fog == null) return;

            bool revealed = _revealedMaps.Contains(CurrentMap);
            _fog.SetFullReveal(revealed);
            if (!revealed && _player != null) _player.RefreshVision(); // baloncuğu yeniden kur

            _nearbyTower = revealed ? null
                         : FindNearbyTower(_player != null ? _player.CurrentCoord : default);
        }

        // ── Yakınlık takibi ──────────────────────────────────────────────────
        private void HandleMoved(HexCoordinate coord)
        {
            _nearbyTower = _revealedMaps.Contains(CurrentMap) ? null : FindNearbyTower(coord);
        }

        private HexCell FindNearbyTower(HexCoordinate from)
        {
            if (_grid == null || _grid.Cells == null) return null;
            HexCell best = null;
            int bestD = int.MaxValue;
            foreach (var cell in _grid.Cells.Values)
            {
                if (cell.CellType != CellType.Watchtower) continue;
                int d = from.DistanceTo(cell.Coordinate);
                if (d <= _promptRange && d < bestD) { bestD = d; best = cell; }
            }
            return best;
        }

        // ── İstem (whitebox IMGUI, cila aşamasında uGUI'ye taşınacak) ─────────
        private void OnGUI()
        {
            if (_busy || _nearbyTower == null) return;
            if (_state != null && _state.State != GameState.Overworld) return;
            if (_player != null && _player.IsMoving) return;

            // Sanal 1920x1080 ekrana ciz -> her cozunurlukte ayni oran.
            using var _scale = HudScale.Scaled();

            const float w = 340f, h = 96f;
            var rect = new Rect((HudScale.Width - w) * 0.5f, HudScale.Height - h - 24f, w, h);
            ImguiBlocker.Register(rect);   // istem üstündeki tık haritaya düşmesin
            GUILayout.BeginArea(rect, GUI.skin.box);
            // _world YOKSA tek haritali (bolum) dunya → "Ada N" demek anlamsiz, harita de.
            GUILayout.Label(_world != null
                ? $"Kule yakinda — Ada {CurrentMap} sisini KALICI kaldir?"
                : "Kule yakinda — haritanin sisini KALICI kaldir?");
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Sisi Kaldir", GUILayout.Height(34))) Activate();
            if (GUILayout.Button("Vazgec",      GUILayout.Height(34))) _nearbyTower = null;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void Activate()
        {
            if (_busy || _nearbyTower == null) return;
            _revealedMaps.Add(CurrentMap);
            HexCell tower = _nearbyTower;
            _nearbyTower = null;
            StartCoroutine(ActivateRoutine(tower));
        }

        // ── Epik açılış: ışık hüzmesi + genişleyen halka + ışık patlaması + sis kalkışı ──
        private IEnumerator ActivateRoutine(HexCell tower)
        {
            _busy = true;
            Vector3 basePos = tower.WorldPosition + Vector3.up * tower.SurfaceHeight;

            var root = new GameObject("KuleAcilisFx").transform;

            // 1) Işık hüzmesi — dikey emissive silindir (gökten inen sütun).
            Material beamMat = MakeEmissive(_beamColor, _beamIntensity);
            var beam = MakePrimitive(PrimitiveType.Cylinder, root, beamMat);
            beam.position   = basePos + Vector3.up * (_beamHeight * 0.5f);
            beam.localScale = new Vector3(0.6f, _beamHeight * 0.5f, 0.6f);

            // 2) Zeminde dışa açılan emissive halka (yassı silindir disk).
            Material ringMat = MakeEmissive(_ringColor, _beamIntensity);
            var ring = MakePrimitive(PrimitiveType.Cylinder, root, ringMat);
            ring.position   = basePos + Vector3.up * 0.06f;
            ring.localScale = new Vector3(0.5f, 0.02f, 0.5f);

            // 3) Anlık ışık patlaması.
            var lightGO = new GameObject("KuleLight");
            lightGO.transform.SetParent(root, false);
            lightGO.transform.position = basePos + Vector3.up * 3f;
            var light = lightGO.AddComponent<Light>();
            light.type  = LightType.Point;
            light.range = 45f;
            light.color = _beamColor;

            // Sis animasyonla kalksın (bulutlar süzülüp kaybolur, zemin renklenir).
            if (_fog != null) _fog.RevealAllAnimated(_fxDuration);

            Debug.Log($"[Kule] Ada {CurrentMap} sisi KALICI kaldirildi (kule {tower.Coordinate}).");

            float t = 0f;
            while (t < _fxDuration)
            {
                t += Time.deltaTime;
                float k     = Mathf.Clamp01(t / _fxDuration);
                float pulse = Mathf.Sin(k * Mathf.PI);   // 0→1→0 (belir, sön)

                // hüzme: kalınlığı belirip söner (boy sabit).
                float thick = 0.6f * pulse + 0.05f;
                beam.localScale = new Vector3(thick, _beamHeight * 0.5f, thick);

                // halka: dışa genişler + parıltısı söner.
                float r = Mathf.Lerp(0.5f, _ringMaxRadius, k);
                ring.localScale = new Vector3(r, 0.02f, r);
                SetEmission(ringMat, _ringColor, _beamIntensity * (1f - k));

                // ışık: parla → sön.
                light.intensity = _lightIntensity * pulse;

                yield return null;
            }

            Destroy(root.gameObject);
            Destroy(beamMat);
            Destroy(ringMat);
            _busy = false;
        }

        // ── Yardımcılar ──────────────────────────────────────────────────────
        private static Transform MakePrimitive(PrimitiveType type, Transform parent, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);           // efekt tıklamayı/ışını engellemesin
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }

        private static Material MakeEmissive(Color color, float intensity)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(sh);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color"))     m.SetColor("_Color", color);
            SetEmission(m, color, intensity);
            return m;
        }

        private static void SetEmission(Material m, Color color, float intensity)
        {
            if (!m.HasProperty("_EmissionColor")) return;
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", color * Mathf.Max(0f, intensity));
        }
    }
}
