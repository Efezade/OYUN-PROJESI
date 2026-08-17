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

            // Çerçeve şeritleri: her biri turun farklı bir noktasında → renk kenarda dolaşır.
            if (_border != null)
                for (int i = 0; i < _border.Length; i++)
                {
                    if (_border[i] == null) continue;
                    float h = Mathf.Repeat(hue + i / (float)_border.Length, 1f);
                    Color c = Color.HSVToRGB(h, _saturation, 1f);
                    c.a = intensity;
                    _border[i].color = c;
                }

            if (_surface != null)
            {
                Color c = Color.HSVToRGB(Mathf.Repeat(hue + 0.5f, 1f), _saturation * 0.8f, 1f);
                c.a = intensity * _surfaceStrength;
                _surface.color = c;
            }

            if (_frame != null)
                _frame.color = Color.Lerp(_frameBase, Color.HSVToRGB(hue, _saturation, 1f), intensity * 0.75f);
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
