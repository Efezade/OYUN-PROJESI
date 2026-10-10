using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// OYUK TEPELER · KOVUK TÜNELLERİ (2026-10-10). Hikâye §5: "ağaç kovukları ve ruhların
    /// eski tapınakları".
    ///
    /// KURAL:
    ///   • Tepelerdeki ağaç kovukları (<c>agac_kovugu</c> karosu) köklerin altından birbirine bağlı.
    ///   • Kovuğun üstünde duran oyuncu küçük bir panelden başka bir kovuğu seçer ve AP ödeyerek
    ///     ANINDA oraya çıkar. Görmediği kovuk "bilinmeyen kovuk" diye listelenir — oraya çıkmak
    ///     keşif de demektir.
    /// Haritada yeterince kovuk yoksa kural bölgenin düzlüklerine birkaç kovuk açar.
    /// </summary>
    public class HollowTunnels : RegionMechanicBase
    {
        [SerializeField] private string _hollowTile = TileCatalog.AgacKovugu;
        [SerializeField, Min(2)] private int _minHollows = 3;
        [Tooltip("Tünelden geçmenin bedeli (AP).")]
        [SerializeField, Min(0)] private int _apCost = 1;
        [SerializeField] private GameObject _markerPrefab;
        [SerializeField] private Color _markerColor = new(0.95f, 0.70f, 0.30f);

        private readonly List<HexCoordinate> _hollows = new();
        private readonly Dictionary<HexCoordinate, GameObject> _markers = new();
        private GUIStyle _title, _button;
        private bool          _hasPending;      // panelde seçildi; geçiş OnGUI dışında (Update) yapılır
        private HexCoordinate _pending;

        private static readonly Color Info = new(0.95f, 0.80f, 0.50f);

        protected override void OnMapReady()
        {
            _hollows.Clear(); _markers.Clear();
            if (!Active) return;

            foreach (var c in RegionCells(c => _map.TerrainIdAt(c) == _hollowTile)) _hollows.Add(c);
            EnsureMinimum();
            foreach (var c in _hollows)
            {
                var go = MakeMarker(_markerPrefab, PrimitiveType.Cylinder, _markerColor,
                                    new Vector3(0.35f, 0.02f, 0.35f), 0.8f, 1.5f);
                go.name = $"Kovuk_{c}";
                go.transform.position = SurfaceOf(c) + Vector3.up * 0.08f;
                _markers[c] = go;
            }
            RefreshMarkers();
        }

        private void EnsureMinimum()
        {
            int need = _minHollows - _hollows.Count;
            if (need <= 0 || TileCatalog.Get(_hollowTile) == null) return;
            var pool = RegionCells(c =>
            {
                if (!IsFreeWalkable(c)) return false;
                var e = TileCatalog.Get(_map.TerrainIdAt(c));
                return e != null && e.Family == TileFamily.Plain;
            });
            foreach (var c in PickSpaced(pool, need, 4, Rng(89), _hollows))
            {
                _map.SetTile(c, _hollowTile);
                _hollows.Add(c);
            }
        }

        protected override void OnPlayerMoved(HexCoordinate at) => RefreshMarkers();
        protected override void OnOverworldRestored() => RefreshMarkers();

        /// <summary>Kovuk işareti yalnız keşfedilmiş kovukta görünür.</summary>
        private void RefreshMarkers()
        {
            foreach (var kv in _markers)
                if (kv.Value != null) kv.Value.SetActive(_fog == null || _fog.IsKnown(kv.Key));
        }

        private bool OnHollow(out HexCoordinate at)
        {
            at = _player != null ? _player.CurrentCoord : default;
            return _player != null && !_player.IsMoving && _hollows.Contains(at);
        }

        private void Travel(HexCoordinate to)
        {
            if (_grid == null || !_grid.TryGetCell(to, out HexCell cell) || !cell.IsWalkable) return;
            if (_ap != null)
            {
                if (_apCost > 0) _ap.SpendAP(_apCost);
                _ap.GrantForcedMove();               // çıkış bir adım sayılmaz
            }
            _player.TeleportTo(cell);
            Notify("Köklerin altından geçip başka bir kovuktan çıktın.", Info);
        }

        private void Update()
        {
            if (!_hasPending) return;
            _hasPending = false;
            if (Active && InOverworld && OnHollow(out _)) Travel(_pending);
        }

        private void OnGUI()
        {
            if (!Active || !InOverworld || MenuState.HudsHidden) return;
            if (!OnHollow(out HexCoordinate here)) return;

            _title  ??= new GUIStyle(GUI.skin.label)  { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 17 };
            _button ??= new GUIStyle(GUI.skin.button) { fontSize = 15 };
            _title.normal.textColor = Info;

            var others = new List<HexCoordinate>();
            foreach (var c in _hollows)
                if (!c.Equals(here) && _grid.TryGetCell(c, out HexCell cell) && cell.IsWalkable) others.Add(c);
            others.Sort((a, b) => a.DistanceTo(here).CompareTo(b.DistanceTo(here)));

            using var _ = HudScale.Scaled();
            const float w = 300f, rowH = 30f;
            float h = 40f + Mathf.Max(1, others.Count) * (rowH + 4f) + 8f;
            var rect = new Rect(HudScale.Width - w - 24f, HudScale.Height * 0.5f - h * 0.5f, w, h);
            ImguiBlocker.Register(rect);
            GUI.Box(rect, GUIContent.none);
            GUI.Label(new Rect(rect.x, rect.y + 6f, w, 26f), $"KOVUK AĞI — {_apCost} AP", _title);

            if (others.Count == 0)
            {
                GUI.Label(new Rect(rect.x + 10f, rect.y + 40f, w - 20f, rowH), "Bağlı başka kovuk yok.", _button);
                return;
            }
            for (int i = 0; i < others.Count; i++)
            {
                HexCoordinate c = others[i];
                bool known = _fog == null || _fog.IsKnown(c);
                string label = known ? $"Kovuk · {c.DistanceTo(here)} karo öte" : "Bilinmeyen kovuk · ?";
                var r = new Rect(rect.x + 10f, rect.y + 40f + i * (rowH + 4f), w - 20f, rowH);
                bool affordable = _ap == null || _ap.APRemainingToday >= _apCost;
                GUI.enabled = affordable;
                if (GUI.Button(r, label, _button)) { _pending = c; _hasPending = true; }
                GUI.enabled = true;
            }
        }
    }
}
