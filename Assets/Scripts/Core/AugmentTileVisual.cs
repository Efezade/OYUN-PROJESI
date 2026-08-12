using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TacticalRPG.Core
{
    /// <summary>
    /// KAM'IN KOYDUĞU KARONUN CANLANDIRMASI — karo yerleşirken yerden yükselir, oturur ve
    /// oyun boyunca ruh oymaları nabız gibi parlar.
    ///
    /// NEDEN VAR (kullanıcı geri bildirimi 2026-08-12): "karolar çalışmıyor" şikâyetinin yarısı
    /// mekanik (etki çözümlenmiyordu), yarısı GERİ BİLDİRİMDİ — karo bir anda tahtada beliriyordu,
    /// oyuncu bir şey olduğunu anlamıyordu. Bir mekanik ancak GÖRÜLDÜĞÜ kadar vardır.
    ///
    /// Karo prefabında bulunur (üreteç ekler) ya da yoksa çalışma zamanında eklenir. Parlayan
    /// parçalar ADLARINDAN bulunur: <see cref="AccentPrefix"/> ile başlayan çocuk renderer'lar.
    /// Böylece bu bileşenin serileşmiş referansa ihtiyacı yok — prefab üreteci değişse de
    /// bağ kopmaz (Inspector'da boş kalan referans en sık sessiz kırılma sebebi).
    /// </summary>
    public class AugmentTileVisual : MonoBehaviour
    {
        /// <summary>Üretecin parlayan parçalara verdiği ad öneki.</summary>
        public const string AccentPrefix = "Accent";

        [Header("Yerleşme")]
        [Tooltip("Karo yerden çıkarken ne kadar aşağıdan başlasın.")]
        [SerializeField] private float _riseFrom = 1.4f;
        [SerializeField] private float _riseDuration = 0.55f;
        [Tooltip("Yerleşirken kaç derece dönsün (ruh çağırma hissi).")]
        [SerializeField] private float _spinDegrees = 120f;

        [Header("Nabız")]
        [Tooltip("Oymaların bir tam nabız döngüsü (sn). 0 = nabız yok.")]
        [SerializeField] private float _pulsePeriod = 2.2f;
        [Tooltip("Nabzın en sönük/en parlak arası (emisyon çarpanı).")]
        [SerializeField] private Vector2 _pulseRange = new(0.55f, 1.6f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId     = Shader.PropertyToID("_Color");
        private static readonly int EmissionId  = Shader.PropertyToID("_EmissionColor");

        private readonly List<Renderer> _accents = new();
        private readonly List<Color>    _accentBase = new();
        private MaterialPropertyBlock   _mpb;
        private Coroutine               _anim;
        private float                   _flashUntil;
        private Color                   _flashColor;
        private bool                    _pulsing = true;

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            CollectAccents();
        }

        /// <summary>Parlayacak parçaları toplar. Hiç "Accent" adlı çocuk yoksa (üreteç henüz
        /// koşmamış, karo düz placeholder) tüm renderer'lara düşer — geri bildirim yine olsun.</summary>
        private void CollectAccents()
        {
            _accents.Clear();
            _accentBase.Clear();

            foreach (var r in GetComponentsInChildren<Renderer>(true))
                if (r != null && r.gameObject.name.StartsWith(AccentPrefix)) _accents.Add(r);

            if (_accents.Count == 0)
                foreach (var r in GetComponentsInChildren<Renderer>(true))
                    if (r != null) _accents.Add(r);

            foreach (var r in _accents)
            {
                Material m = r.sharedMaterial;
                Color c = m == null ? Color.white
                        : m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId)
                        : m.HasProperty(ColorId)     ? m.GetColor(ColorId) : Color.white;
                _accentBase.Add(c);
            }
        }

        /// <summary>Karo tahtaya YENİ kondu: yerden yüksel + dön + otur.</summary>
        public void PlayPlacement(float delay = 0f)
        {
            if (!gameObject.activeInHierarchy) return;
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(RiseRoutine(delay));
        }

        /// <summary>Karo TETİKLENDİ (tuzak patladı, ocak can verdi): kısa parlak çakış.</summary>
        public void Flash(Color color, float seconds = 0.45f)
        {
            _flashColor = color;
            _flashUntil = Time.time + seconds;
        }

        /// <summary>Duvar/moloz gibi "ölü" karolarda nabız kapatılır — her şey nabız atarsa
        /// hiçbir şey dikkat çekmez.</summary>
        public void SetPulsing(bool on) => _pulsing = on;

        private IEnumerator RiseRoutine(float delay)
        {
            Transform t = transform;
            Vector3 endPos   = t.position;
            Vector3 startPos = endPos - Vector3.up * _riseFrom;
            float   endYaw   = t.eulerAngles.y;

            t.position = startPos;
            t.localScale = new Vector3(0.55f, 0.55f, 0.55f);

            if (delay > 0f)
            {
                // Çok karolu kartlarda (duvar) karolar sırayla çıksın — hepsi aynı anda
                // fırlarsa tek bir "pop" olur, duvarın ÖRÜLDÜĞÜ okunmaz.
                float d = 0f;
                while (d < delay) { d += Time.deltaTime; yield return null; }
            }

            float e = 0f;
            while (e < _riseDuration)
            {
                e += Time.deltaTime;
                float k = Mathf.Clamp01(e / _riseDuration);
                float ease = EaseOutBack(k);

                t.position    = Vector3.LerpUnclamped(startPos, endPos, ease);
                t.localScale  = Vector3.LerpUnclamped(new Vector3(0.55f, 0.55f, 0.55f), Vector3.one, ease);
                t.eulerAngles = new Vector3(0f, endYaw + _spinDegrees * (1f - ease), 0f);

                // Çıkarken oymalar en parlak — sonra normal nabza düşer.
                _flashColor = Color.white;
                _flashUntil = Mathf.Max(_flashUntil, Time.time + 0.05f);
                yield return null;
            }

            t.position    = endPos;
            t.localScale  = Vector3.one;
            t.eulerAngles = new Vector3(0f, endYaw, 0f);
            _anim = null;
        }

        // Hafif aşma (overshoot) — karo yerine "tok" oturur.
        private static float EaseOutBack(float k)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float p = k - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }

        private void Update()
        {
            if (_accents.Count == 0) return;

            float glow;
            Color tint;

            if (Time.time < _flashUntil)
            {
                glow = 3.2f;                       // tetiklenme çakışı
                tint = _flashColor;
            }
            else if (_pulsing && _pulsePeriod > 0.01f)
            {
                float k = (Mathf.Sin(Time.time * (Mathf.PI * 2f / _pulsePeriod)) + 1f) * 0.5f;
                glow = Mathf.Lerp(_pulseRange.x, _pulseRange.y, k);
                tint = Color.clear;                // kendi rengini kullan
            }
            else
            {
                glow = _pulseRange.x;
                tint = Color.clear;
            }

            for (int i = 0; i < _accents.Count; i++)
            {
                Renderer r = _accents[i];
                if (r == null) continue;

                Color baseCol = _accentBase[i];
                Color c = tint == Color.clear ? baseCol : Color.Lerp(baseCol, tint, 0.6f);

                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, c);
                _mpb.SetColor(ColorId,     c);
                _mpb.SetColor(EmissionId,  c * glow);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
