using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TacticalRPG.Core;

namespace TacticalRPG.UI
{
    /// <summary>
    /// AYARLAR ekranının GÖRÜNÜMÜ: slider ve butonları ses/görüntü "modellerine" (<see cref="GameAudio"/>,
    /// <see cref="DisplaySettings"/>) bağlar. Kendi kalıcılığı YOKTUR — kaydetme/uygulama modellerde;
    /// bu sınıf yalnız (a) panel her açıldığında widget'ları modeldeki güncel değerlerle senkronlar,
    /// (b) kullanıcı oynayınca modelin setter'ını çağırır (tek yönlü bağımlılık, CLAUDE.md).
    ///
    /// Panel gizliyken GameObject SetActive(false) → bu bileşen uyur; yeniden görünürken <see cref="OnEnable"/>
    /// modelden taze okur. Tüm referanslar Inspector'dan atanır (Whiteboxing) — editör aracı bağlar.
    /// </summary>
    [DisallowMultipleComponent]
    public class SettingsController : MonoBehaviour
    {
        [Header("Modeller")]
        [SerializeField] private GameAudio _audio;
        [SerializeField] private DisplaySettings _display;

        [Header("Ses Slider'ları (0..1)")]
        [SerializeField] private Slider _masterSlider;
        [SerializeField] private Slider _musicSlider;
        [SerializeField] private Slider _sfxSlider;
        [SerializeField] private TextMeshProUGUI _masterValue;
        [SerializeField] private TextMeshProUGUI _musicValue;
        [SerializeField] private TextMeshProUGUI _sfxValue;

        [Header("Görüntü")]
        [SerializeField] private Slider _brightnessSlider;
        [SerializeField] private TextMeshProUGUI _brightnessValue;
        [SerializeField] private TextMeshProUGUI _qualityValue;   // kalite adı etiketi
        [SerializeField] private TextMeshProUGUI _fullscreenValue; // AÇIK/KAPALI
        [SerializeField] private TextMeshProUGUI _vsyncValue;      // AÇIK/KAPALI

        [Header("Kamera Yakınlığı (2026-08-12 — test ayarı)")]
        [Tooltip("Overworld ve savaş için AYRI zoom. Küçük değer = daha yakın. Beğenilen değer " +
                 "sabitlenip bu ayar kaldırılabilir; şimdilik test için açık.")]
        [SerializeField] private CameraZoomSettings _zoom;
        [SerializeField] private Slider _overworldZoomSlider;
        [SerializeField] private Slider _combatZoomSlider;
        [SerializeField] private TextMeshProUGUI _overworldZoomValue;
        [SerializeField] private TextMeshProUGUI _combatZoomValue;

        private bool _syncing; // OnEnable senkronunda onValueChanged'i yut

        private void Awake()
        {
            if (_masterSlider != null) _masterSlider.onValueChanged.AddListener(OnMaster);
            if (_musicSlider  != null) _musicSlider.onValueChanged.AddListener(OnMusic);
            if (_sfxSlider    != null) _sfxSlider.onValueChanged.AddListener(OnSfx);
            if (_brightnessSlider != null) _brightnessSlider.onValueChanged.AddListener(OnBrightness);
            if (_overworldZoomSlider != null) _overworldZoomSlider.onValueChanged.AddListener(OnOverworldZoom);
            if (_combatZoomSlider    != null) _combatZoomSlider.onValueChanged.AddListener(OnCombatZoom);
        }

        private void OnEnable() => SyncFromModel();

        private void SyncFromModel()
        {
            _syncing = true;
            if (_audio != null)
            {
                if (_masterSlider != null) _masterSlider.value = _audio.Master;
                if (_musicSlider  != null) _musicSlider.value  = _audio.Music;
                if (_sfxSlider    != null) _sfxSlider.value    = _audio.Sfx;
                UpdatePercent(_masterValue, _audio.Master);
                UpdatePercent(_musicValue,  _audio.Music);
                UpdatePercent(_sfxValue,    _audio.Sfx);
            }
            if (_zoom != null)
            {
                if (_overworldZoomSlider != null)
                {
                    _overworldZoomSlider.minValue = _zoom.MinScale;
                    _overworldZoomSlider.maxValue = _zoom.MaxScale;
                    _overworldZoomSlider.value    = _zoom.OverworldScale;
                }
                if (_combatZoomSlider != null)
                {
                    _combatZoomSlider.minValue = _zoom.MinScale;
                    _combatZoomSlider.maxValue = _zoom.MaxScale;
                    _combatZoomSlider.value    = _zoom.CombatScale;
                }
                UpdateZoom(_overworldZoomValue, _zoom.OverworldScale);
                UpdateZoom(_combatZoomValue,    _zoom.CombatScale);
            }
            if (_display != null)
            {
                if (_brightnessSlider != null)
                {
                    _brightnessSlider.minValue = _display.BrightnessMin;
                    _brightnessSlider.maxValue = _display.BrightnessMax;
                    _brightnessSlider.value    = _display.Brightness;
                }
                UpdatePercent(_brightnessValue, _display.Brightness);
                if (_qualityValue != null)
                {
                    var names = _display.QualityNames;
                    int lvl = _display.QualityLevel;
                    _qualityValue.text = (lvl >= 0 && lvl < names.Length) ? names[lvl] : "?";
                }
                UpdateOnOff(_fullscreenValue, _display.IsFullscreen);
                UpdateOnOff(_vsyncValue, _display.VSyncOn);
            }
            _syncing = false;
        }

        private void OnMaster(float v)     { if (_syncing) return; _audio?.SetMaster(v);  UpdatePercent(_masterValue, v); }
        private void OnMusic(float v)      { if (_syncing) return; _audio?.SetMusic(v);   UpdatePercent(_musicValue, v); }
        private void OnSfx(float v)        { if (_syncing) return; _audio?.SetSfx(v);     UpdatePercent(_sfxValue, v); }
        private void OnBrightness(float v) { if (_syncing) return; _display?.SetBrightness(v); UpdatePercent(_brightnessValue, v); }

        private void OnOverworldZoom(float v)
        {
            if (_syncing) return;
            _zoom?.SetOverworldScale(v);
            UpdateZoom(_overworldZoomValue, v);
        }

        private void OnCombatZoom(float v)
        {
            if (_syncing) return;
            _zoom?.SetCombatScale(v);
            UpdateZoom(_combatZoomValue, v);
        }

        // Zoom "%" değil "x" ile gösterilir — 0.5x, 1.0x gibi okunsun (kullanıcının konuştuğu birim).
        private static void UpdateZoom(TextMeshProUGUI label, float v)
        {
            if (label != null) label.text = v.ToString("0.00") + "x";
        }

        /// <summary>Kalite döngü butonu (editör aracı onClick'e bağlar).</summary>
        public void OnCycleQuality()
        {
            if (_display == null) return;
            string name = _display.CycleQuality();
            if (_qualityValue != null) _qualityValue.text = name;
        }

        /// <summary>Tam ekran aç/kapat butonu.</summary>
        public void OnToggleFullscreen()
        {
            if (_display == null) return;
            _display.SetFullscreen(!_display.IsFullscreen);
            UpdateOnOff(_fullscreenValue, _display.IsFullscreen);
        }

        /// <summary>VSync aç/kapat butonu.</summary>
        public void OnToggleVSync()
        {
            if (_display == null) return;
            _display.SetVSync(!_display.VSyncOn);
            UpdateOnOff(_vsyncValue, _display.VSyncOn);
        }

        private static void UpdatePercent(TextMeshProUGUI label, float v01)
        {
            if (label != null) label.text = Mathf.RoundToInt(v01 * 100f) + "%";
        }

        private static void UpdateOnOff(TextMeshProUGUI label, bool on)
        {
            if (label != null) label.text = on ? "AÇIK" : "KAPALI";
        }
    }
}
