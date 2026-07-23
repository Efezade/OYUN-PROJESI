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
    ///     Geçiş KESKİNdir (varsayılan 0 sn) — akışkan lerp istenmedi.
    ///   • GÜNDÜZ↔GECE sınırında ayrıca gösteri oynatılır: ışık sert geçer, ardından tüm
    ///     karolar aşağı süzülüp yerlerine yukarıdan inen kopyaları geçer (DayNightTileSwap),
    ///     sonunda gece sis kuralı devreye girer (görüş yarıya iner, kule açık olsa bile
    ///     karanlık tavanı uygulanır — FogOfWarManager.SetNightMode).
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
        [Tooltip("Dilim değişince yeni atmosfere kaç saniyede geçilir. 0 = KESKİN (istenen davranış).")]
        [SerializeField, Min(0f)] private float _transitionSeconds = 0f;

        [Header("Neyi sürsün?")]
        [Tooltip("Kapatılırsa sahnenin ortam (ambient) ışığına dokunulmaz.")]
        [SerializeField] private bool _controlAmbient = true;
        [Tooltip("Kapatılırsa kamera arkaplan (gökyüzü) rengine dokunulmaz.")]
        [SerializeField] private bool _controlSky = true;

        private Coroutine _transition;

        // Sahnenin başlangıç değerleri — profil/ışık eksikse geri dönülecek güvenli taban.
        private AmbientMode _originalAmbientMode;
        private bool        _ambientModeCaptured;

        private void OnEnable()
        {
            if (_apManager != null)
            {
                _apManager.OnTimeAdvanced   += HandleTimeAdvanced;
                _apManager.OnDayNightChanged += HandleDayNightChanged;
            }
        }

        private void OnDisable()
        {
            if (_apManager != null)
            {
                _apManager.OnTimeAdvanced   -= HandleTimeAdvanced;
                _apManager.OnDayNightChanged -= HandleDayNightChanged;
            }

            // Play modundan çıkarken sahnenin ambient ayarını bulduğumuz gibi bırak.
            if (_ambientModeCaptured)
                RenderSettings.ambientMode = _originalAmbientMode;
        }

        private void Start()
        {
            // Find* çağrıları yalnızca Start içinde serbest (CLAUDE.md) — wiring eksikse son çare.
            if (_camera == null) _camera = Camera.main;

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
        }

        private void HandleTimeAdvanced(int day, int slot, string slotName)
        {
            // Dilim atmosferi. Gündüz↔gece sınırıysa bu çağrı ışığı ZATEN geceye çevirir;
            // hemen ardından gelen OnDayNightChanged gösteriyi oynatır → karolar karanlıkta takas olur.
            ApplySlot(slot, instant: false);
        }

        // ── Gündüz ↔ gece sınırı: ışık zaten HandleTimeAdvanced'te sert geçti; burada görüş kuralı ──
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
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _transitionSeconds);
                Apply(target, Mathf.SmoothStep(0f, 1f, t), from);
                yield return null;
            }

            Apply(target, 1f, from);
            _transition = null;
        }

        // ── Uygulama ──────────────────────────────────────────────────────────

        private void Apply(DayNightProfile.SlotAtmosphere target, float t, Snapshot from)
        {
            if (_sun != null)
            {
                _sun.transform.rotation = Quaternion.Slerp(from.SunRotation,
                                                           Quaternion.Euler(target.sunEuler), t);
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
