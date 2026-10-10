using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// UYUYANLAR VADİSİ · UYANIŞ SAYACI (2026-10-10). Hikâye §5: "dev ağaçlar, yosun kaplı
    /// uyuyan dev heykelleri".
    ///
    /// KURAL:
    ///   • Vadideki uyuyan dev heykellerinin (<c>dusmus_dev</c> karosu) yanından geçen her adım
    ///     UYANIŞ sayacını doldurur (dibinden geçmek 2, iki karo öteden 1).
    ///   • Sayaç her gün biraz söner — vadide sessizce ve hızlı geçen kurtulur.
    ///   • Sayaç dolarsa en yakın dev UYANIR: pusu savaşı açılır, o dev bir daha uyanmaz.
    ///   • Devin gözleri sayaçla birlikte kızarır; vadideyken ekranda sayaç görünür.
    /// Haritada yeterince heykel yoksa kural bölgenin düzlüklerine birkaç heykel koyar.
    /// </summary>
    public class SleeperWatch : RegionMechanicBase
    {
        [Header("Heykeller")]
        [SerializeField] private string _statueTile = TileCatalog.DusmusDev;
        [SerializeField, Min(0)] private int _minStatues = 2;
        [SerializeField] private Color _eyeColor = new(1f, 0.45f, 0.2f);
        [SerializeField] private float _eyeMaxIntensity = 4f;

        [Header("Sayaç")]
        [Tooltip("Bu yarıçap içindeki her adım sayacı doldurur (dibi 2, öbürü 1).")]
        [SerializeField, Min(1)] private int _noiseRadius = 2;
        [SerializeField, Min(1)] private int _threshold   = 8;
        [Tooltip("Her gün başında sayaçtan düşen miktar.")]
        [SerializeField, Min(0)] private int _decayPerDay = 3;

        [Header("Uyanış")]
        [SerializeField] private MissionData    _wakeAmbush;
        [SerializeField] private AmbushLauncher _ambush;

        [Header("Ekran")]
        [SerializeField] private bool _showMeter = true;

        private readonly List<HexCoordinate> _statues = new();
        private readonly HashSet<HexCoordinate> _calmed = new();
        private readonly Dictionary<HexCoordinate, Light> _eyes = new();
        private int _noise;
        private int _warned;                 // hangi uyarı eşiği söylendi (0 / 1 / 2)
        private GUIStyle _style;

        private static readonly Color Warn = new(1f, 0.6f, 0.35f);

        public int Noise => _noise;
        public int Threshold => _threshold;

        protected override void OnMapReady()
        {
            _statues.Clear(); _calmed.Clear(); _eyes.Clear();
            _noise = 0; _warned = 0;
            if (!Active) return;

            foreach (var c in RegionCells(c => _map.TerrainIdAt(c) == _statueTile)) _statues.Add(c);
            EnsureMinimum();
            foreach (var c in _statues) AddEyes(c);
            RefreshEyes();
        }

        /// <summary>Az heykel çıktıysa bölgenin boş düzlüklerine (birbirinden uzak) heykel dikilir.</summary>
        private void EnsureMinimum()
        {
            int need = _minStatues - _statues.Count;
            if (need <= 0 || TileCatalog.Get(_statueTile) == null) return;

            HexCoordinate player = _player != null ? _player.CurrentCoord : default;
            var pool = RegionCells(c =>
            {
                if (!IsFreeWalkable(c) || c.DistanceTo(player) < 4) return false;
                var e = TileCatalog.Get(_map.TerrainIdAt(c));
                return e != null && e.Family == TileFamily.Plain;
            });
            foreach (var c in PickSpaced(pool, need, 5, Rng(71), _statues))
            {
                _map.SetTile(c, _statueTile);
                _statues.Add(c);
            }
        }

        private void AddEyes(HexCoordinate c)
        {
            var go = new GameObject($"DevGozleri_{c}");
            go.transform.SetParent(MarkerRoot, false);
            go.transform.position = SurfaceOf(c) + Vector3.up * 1.1f;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = _eyeColor; l.range = 3f; l.intensity = 0f;
            l.shadows = LightShadows.None;
            _eyes[c] = l;
        }

        private void RefreshEyes()
        {
            float k = _threshold > 0 ? Mathf.Clamp01(_noise / (float)_threshold) : 0f;
            foreach (var kv in _eyes)
                if (kv.Value != null) kv.Value.intensity = _calmed.Contains(kv.Key) ? 0f : k * k * _eyeMaxIntensity;
        }

        protected override void OnNewDay(int day)
        {
            if (_noise == 0) return;
            _noise = Mathf.Max(0, _noise - _decayPerDay);
            if (_noise < _threshold / 2) _warned = 0;
            RefreshEyes();
        }

        protected override void OnPlayerMoved(HexCoordinate at)
        {
            if (_statues.Count == 0) return;

            int bestD = int.MaxValue; HexCoordinate nearest = default;
            foreach (var s in _statues)
            {
                if (_calmed.Contains(s)) continue;
                int d = s.DistanceTo(at);
                if (d < bestD) { bestD = d; nearest = s; }
            }
            if (bestD > _noiseRadius) return;

            _noise += bestD <= 1 ? 2 : 1;
            RefreshEyes();

            if (_noise >= _threshold) { Wake(nearest); return; }
            if (_warned < 2 && _noise >= _threshold * 3 / 4) { _warned = 2; Notify("Yer titriyor… Dev uyanmak üzere!", Warn); }
            else if (_warned < 1 && _noise >= _threshold / 2) { _warned = 1; Notify("Uyuyan dev kıpırdandı. Sessiz ol.", Warn); }
        }

        private void Wake(HexCoordinate statue)
        {
            _noise = 0; _warned = 0;
            _calmed.Add(statue);                 // bu dev bir daha uyanmaz
            RefreshEyes();
            if (_ambush == null || _wakeAmbush == null)
            {
                Notify("Uyuyan dev uyandı! (pusu görevi atanmamış — savaş açılmadı)", Warn);
                return;
            }
            _ambush.Launch(_wakeAmbush, "UYUYAN DEV UYANDI!", Warn);
        }

        // Vadideyken ya da sayaç doluyken alt ortada küçük gösterge.
        private void OnGUI()
        {
            if (!_showMeter || !Active || !InOverworld || MenuState.HudsHidden || _player == null) return;
            if (_noise <= 0 && !InRegion(_player.CurrentCoord)) return;

            _style ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = 16 };
            using var _ = HudScale.Scaled();
            const float w = 360f, h = 30f;
            var rect = new Rect((HudScale.Width - w) * 0.5f, HudScale.Height - 176f, w, h);

            var bar = new System.Text.StringBuilder("UYANIŞ  ");
            for (int i = 0; i < _threshold; i++) bar.Append(i < _noise ? '■' : '□');
            float k = Mathf.Clamp01(_noise / (float)Mathf.Max(1, _threshold));
            _style.normal.textColor = Color.Lerp(new Color(0.8f, 0.9f, 0.75f), Warn, k);
            GUI.Box(rect, bar.ToString(), _style);
        }
    }
}
