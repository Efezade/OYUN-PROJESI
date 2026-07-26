using UnityEngine;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Merkezî ses "modeli": arka plan müziğini çalar ve üç seviyeyi (MASTER / MÜZİK / SFX) tutar,
    /// PlayerPrefs'e KALICI yazar ve AÇILIŞTA (Awake) uygular — böylece ayar paneli hiç açılmasa da
    /// kayıtlı seviyeler geçerli olur. Ayarlar ekranı (<c>SettingsController</c>) yalnızca bir GÖRÜNÜM:
    /// bu sınıfın setter'larını çağırır; ses mantığını kendisi bilmez (tek yönlü bağımlılık, CLAUDE.md).
    ///
    /// MASTER → <see cref="AudioListener.volume"/> (tüm sesler). MÜZİK → müzik AudioSource'u.
    /// SFX → statik <see cref="SfxVolume"/>; ileride ateşlenecek SFX çalımları bunu çarpan olarak kullanır.
    ///
    /// Sahne kökünde, HER ZAMAN AKTİF bir GameObject'e eklenir (menü panelleri gizliyken de çalışsın).
    /// Müzik klibi/kaynağı Inspector'dan atanır (Whiteboxing) — <see cref="_musicSource"/> boşsa Awake ekler.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameAudio : MonoBehaviour
    {
        private const string KEY_MASTER = "audio.master";
        private const string KEY_MUSIC  = "audio.music";
        private const string KEY_SFX    = "audio.sfx";

        [Header("Müzik")]
        [Tooltip("Döngüde çalınacak arka plan müziği (telifsiz placeholder — sonradan değiştirilebilir).")]
        [SerializeField] private AudioClip _backgroundMusic;

        [Tooltip("Müzik için AudioSource. Boşsa Awake'te otomatik eklenir (loop + playOnAwake kapalı).")]
        [SerializeField] private AudioSource _musicSource;

        [Header("Varsayılan Seviyeler (0..1)")]
        [SerializeField, Range(0f, 1f)] private float _defaultMaster = 0.8f;
        [SerializeField, Range(0f, 1f)] private float _defaultMusic  = 0.5f;
        [SerializeField, Range(0f, 1f)] private float _defaultSfx    = 0.8f;

        /// <summary>Kayıtlı SFX seviyesi (0..1). SFX çalan sistemler bunu ses çarpanı olarak kullanır.</summary>
        public static float SfxVolume { get; private set; } = 1f;

        public float Master { get; private set; }
        public float Music  { get; private set; }
        public float Sfx    { get; private set; }

        private void Awake()
        {
            if (_musicSource == null)
                _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.clip         = _backgroundMusic;
            _musicSource.loop         = true;
            _musicSource.playOnAwake  = false;
            _musicSource.spatialBlend = 0f; // 2D (konumdan bağımsız)

            Master = PlayerPrefs.GetFloat(KEY_MASTER, _defaultMaster);
            Music  = PlayerPrefs.GetFloat(KEY_MUSIC,  _defaultMusic);
            Sfx    = PlayerPrefs.GetFloat(KEY_SFX,    _defaultSfx);

            ApplyMaster();
            ApplyMusic();
            SfxVolume = Sfx;
        }

        private void Start()
        {
            if (_backgroundMusic != null && !_musicSource.isPlaying)
                _musicSource.Play();
        }

        public void SetMaster(float v)
        {
            Master = Mathf.Clamp01(v);
            ApplyMaster();
            PlayerPrefs.SetFloat(KEY_MASTER, Master);
        }

        public void SetMusic(float v)
        {
            Music = Mathf.Clamp01(v);
            ApplyMusic();
            PlayerPrefs.SetFloat(KEY_MUSIC, Music);
        }

        public void SetSfx(float v)
        {
            Sfx = Mathf.Clamp01(v);
            SfxVolume = Sfx;
            PlayerPrefs.SetFloat(KEY_SFX, Sfx);
        }

        private void ApplyMaster() => AudioListener.volume = Master;
        private void ApplyMusic()  { if (_musicSource != null) _musicSource.volume = Music; }
    }
}
