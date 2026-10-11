using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Gece/gündüz döngüsü. ActionPointManager'ın zaman event'lerine abone olur:
    ///   • Her DİLİM değişiminde o dilimin atmosferini (DayNightProfile) uygular —
    ///     güneş/ay açısı, rengi, şiddeti, gölge sertliği, ortam ışığı, gökyüzü.
    ///     Işık, gölge ve gökyüzü mevcut değerlerinden yeni dilime yumuşakça geçer.
    ///   • İlk dört dilim gündüz, son iki dilim gece. Görüş kuralı sınırda güncellenir;
    ///     sis ve karo parlaklığı kendi görsel geçişlerini birlikte yapar.
    ///
    /// Karo renklerine DOKUNMAZ — karo/bulut renkleri MaterialPropertyBlock ile
    /// FogOfWarManager'ın sorumluluğunda; burası sahne ışığını sürer, karolar doğal etkilenir.
    /// </summary>
    public class DayNightCycle : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private ActionPointManager _apManager;
        [Tooltip("Sahnedeki Directional Light — güneş/ay.")]
        [SerializeField] private Light _sun;
        [Tooltip("Gökyüzü rengi için kamera (SolidColor ile temizlenir). Boşsa Camera.main denenir.")]
        [SerializeField] private Camera _camera;
        [SerializeField] private DayNightProfile _profile;

        [Header("Gece/Gündüz Sınırı")]
        [Tooltip("Gece görüş daralması için sis yöneticisi. Boşsa görüş kuralı uygulanmaz.")]
        [SerializeField] private FogOfWarManager _fog;
        [Tooltip("Sis kuralı değişince görüşü yenilemek için oyuncu. Boşsa sis kendi son konumunu kullanır.")]
        [SerializeField] private PlayerController _player;

        [Header("Geçiş")]
        [Tooltip("Dilim değişince ışık, gölge ve gökyüzü birlikte bu sürede yumuşar. Art arda AP harcamak mevcut geçişi keskinleştirmez.")]
        [SerializeField, Min(0f)] private float _transitionSeconds = 2.4f;
        public float TransitionSeconds => _transitionSeconds;
        public bool IsTransitioning => _transition != null;

        [Header("Neyi sürsün?")]
        [Tooltip("Kapatılırsa sahnenin ortam (ambient) ışığına dokunulmaz.")]
        [SerializeField] private bool _controlAmbient = true;
        [Tooltip("Kapatılırsa kamera arkaplan (gökyüzü) rengine dokunulmaz.")]
        [SerializeField] private bool _controlSky = true;

        private Coroutine _transition;

        // Sahnenin başlangıç değerleri — profil/ışık eksikse geri dönülecek güvenli taban.
        private AmbientMode _originalAmbientMode;
        private bool        _ambientModeCaptured;
        private bool _started;
        private Snapshot _original;
        private Light _originalSun;

        private void OnEnable()
        {
            if (_apManager != null)
            {
                _apManager.OnTimeAdvanced   += HandleTimeAdvanced;
                _apManager.OnDayNightChanged += HandleDayNightChanged;
            }
            if (_started)
            {
                if (_controlAmbient) RenderSettings.ambientMode = AmbientMode.Flat;
                ApplySlot(_apManager != null ? _apManager.CurrentSlot : 0, instant: false);
                ApplyNightVision(_apManager != null && _apManager.IsNight);
            }
        }

        private void OnDisable()
        {
            if (_apManager != null)
            {
                _apManager.OnTimeAdvanced   -= HandleTimeAdvanced;
                _apManager.OnDayNightChanged -= HandleDayNightChanged;
            }

            if (_transition != null) { StopCoroutine(_transition); _transition = null; }
        }

        private void OnDestroy()
        {
            // Editor Play çıkışında sahnenin ışığı ve global ortam ayarları sızmasın.
            if (_ambientModeCaptured)
            {
                RenderSettings.ambientLight = _original.Ambient;
                RenderSettings.ambientMode = _originalAmbientMode;
            }
            if (_started && RenderSettings.sun == _sun) RenderSettings.sun = _originalSun;
            if (_started && _sun != null)
            { _sun.transform.rotation = _original.SunRotation; _sun.color = _original.SunColor; _sun.intensity = _original.SunIntensity; _sun.shadowStrength = _original.ShadowStrength; }
            if (_started && _controlSky && _camera != null) _camera.backgroundColor = _original.Sky;
        }

        private void Start()
        {
            // Find* çağrıları yalnızca Start içinde serbest (CLAUDE.md) — wiring eksikse son çare.
            if (_camera == null) _camera = Camera.main;
            _original = ReadCurrent();
            _originalSun = RenderSettings.sun;
            if (_sun != null) RenderSettings.sun = _sun;

            if (_profile == null || _profile.Count == 0)
            {
                Debug.LogWarning("[DayNightCycle] DayNightProfile atanmamis/bos — gece-gunduz dongusu pasif.");
                enabled = false;
                return;
            }

            if (_controlAmbient)
            {
                _originalAmbientMode = RenderSettings.ambientMode;
                _ambientModeCaptured = true;
                // Düz ambient rengi ancak Flat modunda okunur; Skybox modunda ayar yok sayılırdı.
                RenderSettings.ambientMode = AmbientMode.Flat;
            }

            // Açılış: mevcut dilimi geçişsiz uygula (oyun başlarken gösteri/fade görülmesin).
            ApplySlot(_apManager != null ? _apManager.CurrentSlot : 0, instant: true);
            if (_fog != null) _fog.SetNightMode(_apManager != null && _apManager.IsNight);
            _started = true;
        }

        private void HandleTimeAdvanced(int day, int slot, string slotName)
        {
            ApplySlot(slot, instant: false);
        }

        // Mantık anında güncellenir, görsel sis kendi fade'ini uygular.
        private void HandleDayNightChanged(bool isNight) => ApplyNightVision(isNight);

        private void ApplyNightVision(bool night)
        {
            if (_fog == null) return;
            _fog.SetNightMode(night);
            // Oyuncu duruyorsa sis kendiliğinden güncellenmez → görüşü elle tazele.
            if (_player != null) _player.RefreshVision();
        }

        /// <summary>Verilen dilimin atmosferine geçer. instant=true ise lerp olmadan anında uygular.</summary>
        public void ApplySlot(int slotIndex, bool instant)
        {
            DayNightProfile.SlotAtmosphere target = _profile != null ? _profile.GetSlot(slotIndex) : null;
            if (target == null) return;

            if (_transition != null)
            {
                StopCoroutine(_transition);
                _transition = null;
            }

            if (instant || _transitionSeconds <= 0f || !isActiveAndEnabled)
            {
                Apply(target, 1f, ReadCurrent());
                return;
            }

            // Geçiş, o anki GERÇEK değerlerden başlar → dilim ortasında yeni geçiş gelse bile pop olmaz.
            _transition = StartCoroutine(TransitionRoutine(ReadCurrent(), target));
        }

        private IEnumerator TransitionRoutine(Snapshot from, DayNightProfile.SlotAtmosphere target)
        {
            float elapsed = 0f;
            while (elapsed < _transitionSeconds)
            {
                float t = Mathf.Clamp01(elapsed / _transitionSeconds);
                Apply(target, Mathf.SmoothStep(0f, 1f, t), from);
                yield return null;
                elapsed += Time.deltaTime;
            }

            Apply(target, 1f, from);
            _transition = null;
        }

        // ── Uygulama ──────────────────────────────────────────────────────────

        private void Apply(DayNightProfile.SlotAtmosphere target, float t, Snapshot from)
        {
            if (_sun != null)
            {
                Vector3 euler = from.SunRotation.eulerAngles;
                _sun.transform.rotation = Quaternion.Euler(Mathf.LerpAngle(euler.x,target.sunEuler.x,t),
                    Mathf.LerpAngle(euler.y,target.sunEuler.y,t),Mathf.LerpAngle(euler.z,target.sunEuler.z,t));
                _sun.color          = Color.Lerp(from.SunColor,       target.sunColor,       t);
                _sun.intensity      = Mathf.Lerp(from.SunIntensity,   target.sunIntensity,   t);
                _sun.shadowStrength = Mathf.Lerp(from.ShadowStrength, target.shadowStrength, t);
            }

            if (_controlAmbient)
                RenderSettings.ambientLight = Color.Lerp(from.Ambient, target.ambientColor, t);

            if (_controlSky && _camera != null)
                _camera.backgroundColor = Color.Lerp(from.Sky, target.skyColor, t);
        }

        private Snapshot ReadCurrent() => new Snapshot
        {
            SunRotation    = _sun != null ? _sun.transform.rotation : Quaternion.identity,
            SunColor       = _sun != null ? _sun.color          : Color.white,
            SunIntensity   = _sun != null ? _sun.intensity      : 1f,
            ShadowStrength = _sun != null ? _sun.shadowStrength : 1f,
            Ambient        = RenderSettings.ambientLight,
            Sky            = _camera != null ? _camera.backgroundColor : Color.black
        };

        /// <summary>Geçişin başlangıç değerleri — struct, her karede çöp üretmesin diye.</summary>
        private struct Snapshot
        {
            public Quaternion SunRotation;
            public Color      SunColor;
            public float      SunIntensity;
            public float      ShadowStrength;
            public Color      Ambient;
            public Color      Sky;
        }
    }
}
