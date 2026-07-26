using UnityEngine;
using UnityEngine.UI;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Görüntü ayarları "modeli": KALİTE seviyesi, PARLAKLIK, TAM EKRAN ve VSYNC. Değerleri PlayerPrefs'e
    /// KALICI yazar ve AÇILIŞTA (Awake) uygular — ayar paneli açılmasa da geçerli olur. Ayarlar ekranı
    /// (<c>SettingsController</c>) yalnızca setter'ları çağıran bir GÖRÜNÜM (tek yönlü bağımlılık, CLAUDE.md).
    ///
    /// PARLAKLIK gerçek monitör parlaklığını değiştiremez; bunun yerine tam-ekran bir kaplama (Image) alpha'sı
    /// ile taklit edilir: 1.0 = nötr (görünmez), &lt;1.0 = SİYAH kaplama (karart), &gt;1.0 = BEYAZ kaplama
    /// (aydınlat). Kaplama en üstteki (yüksek sortingOrder) canvas'ta, raycast HEDEFİ DEĞİL (girişi engellemez).
    ///
    /// Sahne kökünde, HER ZAMAN AKTİF bir GameObject'e eklenir. Kaplama Image'i Inspector'dan atanır (Whiteboxing).
    /// </summary>
    [DisallowMultipleComponent]
    public class DisplaySettings : MonoBehaviour
    {
        private const string KEY_QUALITY    = "gfx.quality";
        private const string KEY_BRIGHTNESS = "gfx.brightness";
        private const string KEY_FULLSCREEN = "gfx.fullscreen";
        private const string KEY_VSYNC      = "gfx.vsync";

        [Header("Parlaklık Kaplaması")]
        [Tooltip("Tam-ekran kaplama Image'i (en üstte, raycast kapalı). Parlaklık bunun renk/alpha'sı ile taklit edilir.")]
        [SerializeField] private Image _brightnessOverlay;

        [Tooltip("Parlaklık slider aralığı: min .. max (1 = nötr).")]
        [SerializeField] private float _brightnessMin = 0.5f;
        [SerializeField] private float _brightnessMax = 1.5f;

        [Header("Varsayılanlar")]
        [SerializeField, Range(0.5f, 1.5f)] private float _defaultBrightness = 1f;
        [SerializeField] private bool _defaultFullscreen = true;
        [SerializeField] private bool _defaultVSync      = true;

        public float Brightness { get; private set; }
        public int   QualityLevel  => QualitySettings.GetQualityLevel();
        public string[] QualityNames => QualitySettings.names;
        public bool  IsFullscreen { get; private set; }
        public bool  VSyncOn       { get; private set; }

        public float BrightnessMin => _brightnessMin;
        public float BrightnessMax => _brightnessMax;

        private void Awake()
        {
            int q = PlayerPrefs.GetInt(KEY_QUALITY, QualitySettings.GetQualityLevel());
            QualitySettings.SetQualityLevel(Mathf.Clamp(q, 0, QualitySettings.names.Length - 1), true);

            Brightness   = PlayerPrefs.GetFloat(KEY_BRIGHTNESS, _defaultBrightness);
            IsFullscreen = PlayerPrefs.GetInt(KEY_FULLSCREEN, _defaultFullscreen ? 1 : 0) == 1;
            VSyncOn      = PlayerPrefs.GetInt(KEY_VSYNC,      _defaultVSync ? 1 : 0) == 1;

            ApplyBrightness();
            Screen.fullScreen = IsFullscreen;
            QualitySettings.vSyncCount = VSyncOn ? 1 : 0;
        }

        public void SetBrightness(float v)
        {
            Brightness = Mathf.Clamp(v, _brightnessMin, _brightnessMax);
            ApplyBrightness();
            PlayerPrefs.SetFloat(KEY_BRIGHTNESS, Brightness);
        }

        /// <summary>Kalite seviyesini bir tıkla döngüsel ilerletir; adı geri döner.</summary>
        public string CycleQuality()
        {
            int count = QualitySettings.names.Length;
            int next  = (QualitySettings.GetQualityLevel() + 1) % count;
            QualitySettings.SetQualityLevel(next, true);
            PlayerPrefs.SetInt(KEY_QUALITY, next);
            return QualitySettings.names[next];
        }

        public void SetFullscreen(bool on)
        {
            IsFullscreen = on;
            Screen.fullScreen = on;
            PlayerPrefs.SetInt(KEY_FULLSCREEN, on ? 1 : 0);
        }

        public void SetVSync(bool on)
        {
            VSyncOn = on;
            QualitySettings.vSyncCount = on ? 1 : 0;
            PlayerPrefs.SetInt(KEY_VSYNC, on ? 1 : 0);
        }

        private void ApplyBrightness()
        {
            if (_brightnessOverlay == null) return;
            // 1.0 = nötr. Altı → siyah kaplama; üstü → beyaz kaplama. Alpha = sapma miktarı.
            if (Brightness < 1f)
                _brightnessOverlay.color = new Color(0f, 0f, 0f, (1f - Brightness) * 0.9f);
            else
                _brightnessOverlay.color = new Color(1f, 1f, 1f, (Brightness - 1f) * 0.5f);
        }
    }
}
