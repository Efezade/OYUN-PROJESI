using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// BÖLGE KURALI TABANI (2026-10-10) — bir bölgeye özgü oynanış kuralının ortak iskeleti
    /// (Gözcü Kuzgun, Alaz ışıkları, uyanış sayacı, kovuk tünelleri). Kural yalnız
    /// <see cref="_region"/> haritada varsa uyanır; bölgesiz haritada hiçbir şey yapmaz.
    ///
    /// Tabanın işi: bağımlılıklar, olay bağlantıları (harita / adım / zaman / durum), bölge karosu
    /// sorguları, haritaya bağlı tekrarlanabilir rastgelelik ve whitebox işaretçi üretimi. Alt
    /// sınıf yalnız KURALI yazar. İşaretçiler savaşta gizlenir, overworld'e dönünce geri gelir.
    /// </summary>
    public abstract class RegionMechanicBase : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] protected ChapterMapGenerator _map;
        [SerializeField] protected HexGridManager      _grid;
        [SerializeField] protected PlayerController    _player;
        [SerializeField] protected GameStateManager    _state;
        [SerializeField] protected FogOfWarManager     _fog;
        [SerializeField] protected ActionPointManager  _ap;
        [SerializeField] protected NoticeFeed          _notice;
        [Tooltip("Düğüm olan karoya kural nesnesi konmasın diye.")]
        [SerializeField] protected ChapterNodeManager  _nodes;

        [Header("Bölge")]
        [Tooltip("Kuralın işlediği bölge. Bu haritada yoksa kural uyur.")]
        [SerializeField] protected RegionSO _region;

        /// <summary>Kuralın bölgesinin bu haritadaki indisi (-1 = yok → kural uyur).</summary>
        protected int  RegionIndex { get; private set; } = -1;
        protected bool Active      => RegionIndex >= 0;
        protected bool InOverworld => _state == null || _state.State == GameState.Overworld;
        protected Transform MarkerRoot { get; private set; }

        private static Material _litTemplate;

        protected virtual void Awake()
        {
            MarkerRoot = new GameObject($"{GetType().Name}_Isaretler").transform;
            MarkerRoot.SetParent(transform, false);
        }

        protected virtual void OnEnable()
        {
            if (_map    != null) _map.OnMapGenerated   += HandleMapGenerated;
            if (_player != null) _player.OnMoved       += HandleMoved;
            if (_ap     != null) _ap.OnTimeAdvanced    += HandleTime;
            if (_state  != null) _state.OnStateChanged += HandleState;
        }

        protected virtual void OnDisable()
        {
            if (_map    != null) _map.OnMapGenerated   -= HandleMapGenerated;
            if (_player != null) _player.OnMoved       -= HandleMoved;
            if (_ap     != null) _ap.OnTimeAdvanced    -= HandleTime;
            if (_state  != null) _state.OnStateChanged -= HandleState;
        }

        private void HandleMapGenerated()
        {
            // Editörde (TAM KURULUM önizlemesi) sahneye kural nesnesi yazılmaz.
            if (!Application.isPlaying) return;
            RegionIndex = -1;
            if (_map != null && _region != null)
                for (int i = 0; i < _map.Regions.Count; i++)
                    if (_map.Regions[i] == _region) { RegionIndex = i; break; }

            ClearMarkers();
            // Bir kare sonra kur: aynı olayı dinleyen düğüm yöneticisi karolarını yerleştirmiş olsun
            // (kural nesnesi görev/market karosunun üstüne düşmesin).
            StopAllCoroutines();
            StartCoroutine(MapReadyNextFrame());
        }

        private System.Collections.IEnumerator MapReadyNextFrame()
        {
            yield return null;
            OnMapReady();
        }

        private void HandleMoved(HexCoordinate c)
        {
            if (Active && InOverworld) OnPlayerMoved(c);
        }

        private void HandleTime(int day, int slot, string slotName)
        {
            if (!Active) return;
            if (slot == 0) OnNewDay(day);
            OnTimeSlot(day, slot);
        }

        private void HandleState(GameState s)
        {
            if (MarkerRoot != null) MarkerRoot.gameObject.SetActive(s == GameState.Overworld);
            if (Active && s == GameState.Overworld) OnOverworldRestored();
        }

        // ── Alt sınıfın dolduracağı kancalar ─────────────────────────────────

        /// <summary>Yeni harita (işaretçiler temizlendi). <see cref="Active"/> burada belli olur.</summary>
        protected virtual void OnMapReady() { }
        protected virtual void OnPlayerMoved(HexCoordinate at) { }
        protected virtual void OnNewDay(int day) { }
        protected virtual void OnTimeSlot(int day, int slot) { }
        protected virtual void OnOverworldRestored() { }

        // ── Bölge karoları ───────────────────────────────────────────────────

        /// <summary>Bölgenin karoları (tahta koordinatı). Koşul verilirse süzülür.</summary>
        protected List<HexCoordinate> RegionCells(System.Predicate<HexCoordinate> where = null)
        {
            var list = new List<HexCoordinate>();
            if (!Active || _grid == null || _grid.Cells == null) return list;
            foreach (var kv in _grid.Cells)
            {
                if (_map.RegionIndexAt(kv.Key) != RegionIndex) continue;
                if (where != null && !where(kv.Key)) continue;
                list.Add(kv.Key);
            }
            return list;
        }

        /// <summary>Kural nesnesi konabilecek karo: yürünür, ayakta, üstünde düğüm ve oyuncu yok.</summary>
        protected bool IsFreeWalkable(HexCoordinate c)
        {
            if (_grid == null || !_grid.TryGetCell(c, out HexCell cell)) return false;
            if (!cell.IsWalkable || cell.CellType != CellType.Normal) return false;
            if (_nodes != null && _nodes.NodeAt(c) != null) return false;
            if (_player != null && _player.CurrentCoord.Equals(c)) return false;
            return true;
        }

        /// <summary>Karonun bölgesi bu kuralın bölgesi mi?</summary>
        protected bool InRegion(HexCoordinate c) => Active && _map.RegionIndexAt(c) == RegionIndex;

        /// <summary>Havuzdan birbirine en az <paramref name="spacing"/> uzak <paramref name="count"/> karo.</summary>
        protected static List<HexCoordinate> PickSpaced(List<HexCoordinate> pool, int count, int spacing,
                                                        System.Random rnd, ICollection<HexCoordinate> avoid = null)
        {
            for (int i = pool.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); (pool[i], pool[j]) = (pool[j], pool[i]); }
            var picked = new List<HexCoordinate>();
            foreach (var c in pool)
            {
                if (picked.Count >= count) break;
                bool ok = true;
                foreach (var p in picked) if (p.DistanceTo(c) < spacing) { ok = false; break; }
                if (ok && avoid != null) foreach (var p in avoid) if (p.DistanceTo(c) < spacing) { ok = false; break; }
                if (ok) picked.Add(c);
            }
            return picked;
        }

        /// <summary>Haritaya + güne + tuza bağlı tekrarlanabilir rastgelelik (teşhis edilebilir).</summary>
        protected System.Random Rng(int salt)
            => new System.Random(unchecked((_map != null ? _map.CurrentSeed * 7919 + _map.CurrentLayoutSeed : 0)
                                           + salt * 104729 + (_ap != null ? _ap.CurrentDay * 131 : 0)));

        /// <summary>Karonun üst yüzeyi (dünya).</summary>
        protected Vector3 SurfaceOf(HexCoordinate c)
            => _grid != null && _grid.TryGetCell(c, out HexCell cell)
               ? cell.WorldPosition + Vector3.up * cell.SurfaceHeight
               : Vector3.zero;

        protected void Notify(string text, Color color, float seconds = 3.5f)
        {
            if (_notice != null) _notice.Post(text, color, seconds);
            else Debug.Log($"[Bolge] {text}");
        }

        // ── Whitebox işaretçiler (gerçek görsel gelince prefab atanır) ───────

        /// <summary>Basit işaretçi: verilen prefab ya da renkli ilkel şekil (+ isteğe bağlı ışık).</summary>
        protected GameObject MakeMarker(GameObject prefab, PrimitiveType shape, Color color, Vector3 scale,
                                        float lightIntensity = 0f, float lightRange = 2f)
        {
            GameObject go;
            if (prefab != null) go = Instantiate(prefab, MarkerRoot);
            else
            {
                go = GameObject.CreatePrimitive(shape);
                go.transform.SetParent(MarkerRoot, false);
                go.transform.localScale = scale;
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);              // tıklama ışını alttaki karoyu görsün
                var rend = go.GetComponent<Renderer>();
                if (rend != null)
                {
                    rend.sharedMaterial = ColoredMaterial(color);
                    rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
            if (lightIntensity > 0f)
            {
                var l = go.AddComponent<Light>();
                l.type = LightType.Point; l.color = color; l.intensity = lightIntensity;
                l.range = lightRange; l.shadows = LightShadows.None;
            }
            return go;
        }

        protected void ClearMarkers()
        {
            if (MarkerRoot == null) return;
            for (int i = MarkerRoot.childCount - 1; i >= 0; i--) Destroy(MarkerRoot.GetChild(i).gameObject);
        }

        private static readonly Dictionary<Color, Material> MatCache = new();

        private static Material ColoredMaterial(Color c)
        {
            if (MatCache.TryGetValue(c, out Material m) && m != null) return m;
            if (_litTemplate == null)
                _litTemplate = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            m = new Material(_litTemplate) { color = c };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * 0.6f);
            }
            MatCache[c] = m;
            return m;
        }
    }
}
