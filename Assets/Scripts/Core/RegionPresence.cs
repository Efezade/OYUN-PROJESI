using System;
using UnityEngine;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// OYUNCU HANGİ BÖLGEDE (2026-10-10) — bölge kurallarının oyuncuya değen ucu.
    ///
    /// TEK SORUMLULUK: oyuncunun karosunun bölgesini izlemek; bölge değişince
    ///   • bölgenin OYNANIŞ kuralını uygular (şimdilik görüş: Fısıltı Bataklığı'nda sis −1),
    ///   • <see cref="OnRegionEntered"/> yayar (ileride: bölge müziği, ışık, düşman havuzu),
    ///   • ekranın üstünde bölgenin adını ve tek satırlık atmosferini kısa süre gösterir
    ///     (For The King / Minecraft'taki "yeni biyoma girdin" anı).
    /// Bölgesiz haritada hiçbir şey yapmaz.
    /// </summary>
    public class RegionPresence : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private PlayerController    _player;
        [SerializeField] private ChapterMapGenerator _map;
        [SerializeField] private GameStateManager    _state;

        [Header("Giriş yazısı")]
        [SerializeField] private bool  _showBanner   = true;
        [SerializeField] private float _bannerHold   = 2.6f;
        [SerializeField] private float _bannerFade   = 0.6f;
        [SerializeField] private int   _titleSize    = 34;
        [SerializeField] private int   _subtitleSize = 16;
        [SerializeField] private float _bannerTop    = 96f;
        [SerializeField] private Color _subtitleColor = new(0.92f, 0.88f, 0.78f);

        /// <summary>Oyuncu yeni bir bölgeye girdi (bölgesiz alana çıkınca null).</summary>
        public event Action<RegionSO> OnRegionEntered;

        /// <summary>Oyuncunun şu an içinde olduğu bölge (yoksa null).</summary>
        public RegionSO Current { get; private set; }

        private float     _bannerStart = -100f;
        private RegionSO  _bannerRegion;
        private GUIStyle  _titleStyle, _subStyle;

        private bool InOverworld => _state == null || _state.State == GameState.Overworld;

        private void OnEnable()
        {
            if (_player != null) _player.OnMoved     += HandleMoved;
            if (_map    != null) _map.OnMapGenerated += HandleMapGenerated;
        }

        private void OnDisable()
        {
            if (_player != null) _player.OnMoved     -= HandleMoved;
            if (_map    != null) _map.OnMapGenerated -= HandleMapGenerated;
        }

        private void Start()
        {
            if (_player != null) Evaluate(_player.CurrentCoord, announce: true);
        }

        private void HandleMapGenerated()
        {
            Current = null;                  // yeni harita: ilk değerlendirme yazıyı yeniden göstersin
            if (_player != null) Evaluate(_player.CurrentCoord, announce: true);
        }

        private void HandleMoved(HexCoordinate at) => Evaluate(at, announce: true);

        private void Evaluate(HexCoordinate at, bool announce)
        {
            if (_map == null || !InOverworld) return;
            RegionSO region = _map.HasRegions ? _map.RegionAt(at) : null;
            if (region == Current) return;

            Current = region;
            if (_player != null)
            {
                _player.RegionVisionDelta = region != null ? region.VisionDelta : 0;
                _player.RefreshVision();
            }

            if (region != null && announce && _showBanner)
            {
                _bannerRegion = region;
                _bannerStart  = Time.time;
            }
            OnRegionEntered?.Invoke(region);
        }

        // ── Giriş yazısı (IMGUI, diğer HUD'larla aynı ölçek) ─────────────────

        private void OnGUI()
        {
            if (MenuState.HudsHidden || !InOverworld || _bannerRegion == null) return;
            float t = Time.time - _bannerStart;
            float total = _bannerFade * 2f + _bannerHold;
            if (t < 0f || t > total) return;

            float a = t < _bannerFade ? t / _bannerFade
                    : t > _bannerFade + _bannerHold ? 1f - (t - _bannerFade - _bannerHold) / _bannerFade
                    : 1f;

            _titleStyle ??= new GUIStyle(GUI.skin.label)
                { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = _titleSize };
            _subStyle ??= new GUIStyle(GUI.skin.label)
                { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic, fontSize = _subtitleSize, wordWrap = true };

            using var _ = HudScale.Scaled();
            float w = 760f, x = (HudScale.Width - w) * 0.5f;
            var title = new Rect(x, _bannerTop, w, 46f);
            var sub   = new Rect(x, _bannerTop + 44f, w, 44f);

            Color c = _bannerRegion.Color;
            DrawShadowed(title, _bannerRegion.DisplayName, _titleStyle, new Color(c.r, c.g, c.b, a), a);

            string flavor = FirstSentence(_bannerRegion.Flavor);
            if (!string.IsNullOrEmpty(flavor))
                DrawShadowed(sub, flavor, _subStyle,
                             new Color(_subtitleColor.r, _subtitleColor.g, _subtitleColor.b, a * 0.9f), a);
        }

        private static void DrawShadowed(Rect r, string text, GUIStyle style, Color color, float alpha)
        {
            style.normal.textColor = new Color(0f, 0f, 0f, 0.65f * alpha);
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, style);
            style.normal.textColor = color;
            GUI.Label(r, text, style);
        }

        private static string FirstSentence(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            int i = s.IndexOf('.');
            return i > 0 && i < s.Length - 1 ? s.Substring(0, i + 1) : s;
        }
    }
}
