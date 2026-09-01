using UnityEngine;
using UnityEngine.UI;

namespace TacticalRPG.UI
{
    /// <summary>
    /// HARİTA EKRANI PARLAMA EFEKTİ — hız tokeni kullanılınca çerçeve renklenip parlar, harita
    /// yüzeyi renge bürünür (kullanıcı isteği 2026-08-17: "Google'ın parlaması gibi").
    ///
    /// İKİ KATMAN:
    ///   • ÇERÇEVE — kenar boyunca dizilmiş dört şerit. Her birinin RENK TONU aynı hızda ama
    ///     FARKLI FAZDA ilerler → renk çerçevenin etrafında dolaşıyor gibi görünür. Asıl "Google
    ///     parlaması" hissi bu dolaşan tondan gelir; sabit renkli bir çerçeve sadece yanıp söner.
    ///   • YÜZEY — haritanın üstünde saydam bir renk katmanı. Alfa arttıkça harita o renge doğru
    ///     çekilir; hem "renklileşme" hem "aydınlanma" bundan çıkar.
    ///
    /// NEDEN ÇARPAN/HDR DEĞİL: uGUI'de <c>Graphic.color</c> Color32'ye sıkıştırılır, 1'in üstüne
    /// çıkamaz — yani dokuyu "beyazın ötesinde" parlatmak mümkün değil. Parlaklık, üstüne serilen
    /// AÇIK RENKLİ katmanla taklit edilir.
    ///
    /// İKİ MOD: <see cref="Play"/> tek seferlik gösteri (belirir → söner), <see cref="SetSustained"/>
    /// ise "hazır" durumunda sönük ama sürekli bir parıltı bırakır — token silahlandığı sürece
    /// ekran bunu göstererek durumu hatırlatır.
    ///
    /// TEK RENK KİPİ (<see cref="SetTint"/>, 2026-09-01): "YOL BELİRLE" açıkken ekran GÖKKUŞAĞI
    /// DEĞİL kırmızımsı parlar — iki mod bakışta ayrılsın diye (kullanıcı isteği). Animasyon
    /// aynı kalır: ton dönmek yerine PARLAKLIK aynı fazlarla kenarda dolaşır.
    /// </summary>
    public class MinimapGlowEffect : MonoBehaviour
    {
        [Header("Katmanlar")]
        [Tooltip("Çerçeve şeritleri (üst/alt/sol/sağ). Sıra önemsiz — faz dizideki indisten türer.")]
        [SerializeField] private Image[] _border;
        [Tooltip("Haritanın üstündeki saydam renk katmanı.")]
        [SerializeField] private Image _surface;
        [Tooltip("Harita yuvasının koyu çerçevesi — parlarken o da renge bürünür.")]
        [SerializeField] private Image _frame;

        [Header("Renk")]
        [Tooltip("Renk tonunun saniyede kaç tur attığı.")]
        [SerializeField, Min(0.05f)] private float _hueSpeed = 0.5f;
        [SerializeField, Range(0f, 1f)] private float _saturation = 0.85f;
        [Tooltip("Yüzey katmanının en yüksek saydamlığı — harita ne kadar renge bürünsün.")]
        [SerializeField, Range(0f, 1f)] private float _surfaceStrength = 0.34f;

        [Header("Zamanlama")]
        [Tooltip("Tek seferlik gösterinin süresi (sn).")]
        [SerializeField, Min(0.2f)] private float _burstSeconds = 1.25f;
        [Tooltip("'Hazır' durumundaki sürekli parıltının şiddeti (0 = kapalı).")]
        [SerializeField, Range(0f, 1f)] private float _sustainLevel = 0.32f;
        [Tooltip("Sürekli parıltının nefes alma periyodu (sn).")]
        [SerializeField, Min(0.2f)] private float _sustainPeriod = 1.8f;

        private float _burstStart = -999f;
        private bool  _sustained;
        private bool  _painted;          // en son bir şey çizdik mi (0'a inince BİR KEZ temizlemek için)
        private Color _frameBase = Color.clear;
        private bool  _tinted;           // true → gökkuşağı yok, tek renk dalgalanır
        private Color _tint    = Color.red;
        private Color _tintDim = Color.black;

        private void Awake()
        {
            if (_frame != null) _frameBase = _frame.color;
            Clear();
        }

        private void OnDisable()
        {
            _sustained  = false;
            _burstStart = -999f;
            Clear();
        }

        /// <summary>Tek seferlik parlama gösterisi (token kullanıldı).</summary>
        public void Play() => _burstStart = Time.unscaledTime;

        /// <summary>"Hazır" parıltısı: token silahlıyken sönük ama sürekli.</summary>
        public void SetSustained(bool on) => _sustained = on;

        /// <summary>TEK RENK kipi: gökkuşağı yerine bu tonun koyusu ↔ açığı arasında dalgalanır
        /// ("YOL BELİRLE" için kırmızımsı). Aynı dolaşan-ışık animasyonu, tek renkle.</summary>
        public void SetTint(Color tint)
        {
            _tinted = true;
            _tint   = tint;
            _tintDim = Color.Lerp(Color.black, tint, 0.30f);
        }

        /// <summary>Gökkuşağına döner (güçlü yol taşı kipi).</summary>
        public void ClearTint() => _tinted = false;

        private void Update()
        {
            float intensity = Mathf.Max(BurstEnvelope(), SustainEnvelope());

            if (intensity <= 0.001f)
            {
                if (_painted) { Clear(); _painted = false; }   // sönünce BİR KEZ temizle
                return;
            }

            _painted = true;
            float hue = Mathf.Repeat(Time.unscaledTime * _hueSpeed, 1f);

            // Çerçeve şeritleri: her biri turun farklı bir noktasında → ışık kenarda dolaşır.
            if (_border != null)
                for (int i = 0; i < _border.Length; i++)
                {
                    if (_border[i] == null) continue;
                    Color c = Shade(hue + i / (float)_border.Length, 1f);
                    c.a = intensity;
                    _border[i].color = c;
                }

            if (_surface != null)
            {
                Color c = Shade(hue + 0.5f, 0.8f);
                c.a = intensity * _surfaceStrength;
                _surface.color = c;
            }

            if (_frame != null)
                _frame.color = Color.Lerp(_frameBase, Shade(hue, 1f), intensity * 0.75f);
        }

        /// <summary>Fazın rengi. Gökkuşağı kipinde faz = TON; tek renk kipinde faz = PARLAKLIK
        /// (aynı dolaşma hissi, tek tonla). Doygunluk çarpanı yalnız gökkuşağında anlamlı.</summary>
        private Color Shade(float phase, float saturationScale)
        {
            phase = Mathf.Repeat(phase, 1f);
            if (!_tinted) return Color.HSVToRGB(phase, _saturation * saturationScale, 1f);

            float wave = (Mathf.Sin(phase * Mathf.PI * 2f) + 1f) * 0.5f;
            return Color.Lerp(_tintDim, _tint, Mathf.Lerp(0.25f, 1f, wave));
        }

        // 0 → 1 → 0: gösteri belirir ve söner.
        private float BurstEnvelope()
        {
            float t = Time.unscaledTime - _burstStart;
            if (t < 0f || t > _burstSeconds) return 0f;
            return Mathf.Sin(t / _burstSeconds * Mathf.PI);
        }

        private float SustainEnvelope()
        {
            if (!_sustained || _sustainLevel <= 0.001f) return 0f;
            float k = (Mathf.Sin(Time.unscaledTime * (Mathf.PI * 2f / _sustainPeriod)) + 1f) * 0.5f;
            return Mathf.Lerp(_sustainLevel * 0.45f, _sustainLevel, k);
        }

        private void Clear()
        {
            if (_border != null)
                foreach (Image b in _border)
                    if (b != null) b.color = new Color(1f, 1f, 1f, 0f);

            if (_surface != null) _surface.color = new Color(1f, 1f, 1f, 0f);
            if (_frame != null && _frameBase != Color.clear) _frame.color = _frameBase;
        }
    }
}
