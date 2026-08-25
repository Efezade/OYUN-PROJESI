using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// HIZLI SEYAHAT SAHNESİ — güçlü yol taşıyla yolculuk onaylanınca sırayla:
    ///   1. SAHNE TOPLANIR — harita ekranı köşeye çekilir, üstündeki UI kapanır. Bu adım
    ///      bitene kadar beklenir (<see cref="NotifyStageReady"/>), yani dönüşüm yarım ekranın
    ///      ortasında başlamaz.
    ///   2. DÖNÜŞÜM — karakterin gövdesi yüzlerce toz zerresine ayrılıp savrulur, yerinde sürekli
    ///      renk değiştiren rengarenk bir küre toplanır. AĞIR AĞIR (birkaç saniye), oyuncu görsün.
    ///   3. YOLCULUK — küre rotayı hızlıca kat eder.
    ///   4. GERİ DÖNÜŞÜM — tozlar toplanır, karakter yeniden oluşur; harita ekranı geri büyür.
    /// (Kullanıcı isteği 2026-08-19.)
    ///
    /// HİÇBİR GÖRSEL SIFIRDAN YAZILMADI — ikisi de projede zaten vardı:
    ///   • TOZA AYRIŞMA: <see cref="TeleportDustEffect"/>. Silinen portal sisteminden kalmıştı ve
    ///     hiçbir yerden çağrılmıyordu; tozları karakterin MESH'İNDEN örneklediği için dağılma
    ///     gerçekten karakter şeklinden başlar.
    ///   • KÜRE: öz kürelerinin TOZ biçimi (<c>Oz_Toz.prefab</c>) — kabuğun içinde dönen ince
    ///     zerre bulutu. Farkı yalnız ölçeği ve RENGİ: <see cref="EssenceOrbVisual.SetRainbow"/>
    ///     ile her parça gökkuşağının başka bir tonunu alır, ton da sürekli döner — harita
    ///     ekranındaki parlamayla aynı numara, aynı varsayılan hız.
    ///
    /// SIRANIN SAHİBİ BU BİLEŞENDİR, harita ekranı DEĞİL. Sebep tek ve kesin: oyuncu yol alırken
    /// harita ekranı kapanabiliyor; sıra orada koşsaydı coroutine ölür, karakter toz hâlinde asılı
    /// kalırdı. Karakter hep sahnede olduğu için sıra burada güvende.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class TravelOrbVisual : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private PlayerController _player;
        [Tooltip("Karakteri toz zerrelerine ayırıp geri toplayan efekt (aynı GameObject'te).")]
        [SerializeField] private TeleportDustEffect _dust;
        [Tooltip("Kürenin modeli — öz kürelerinin TOZ biçimi (Assets/Prefabs/Essence/Oz_Toz.prefab).")]
        [SerializeField] private GameObject _orbPrefab;
        [Tooltip("YEDEK yol: toz efekti yoksa bu model küçültülerek gizlenir. Boşsa 'Model' " +
                 "çocuğu aranır, o da yoksa render'lar doğrudan kapatılır.")]
        [SerializeField] private Transform _characterModel;

        [Header("Küre")]
        [Tooltip("Kürenin karakter yerine geçen boyutu (öz küresi haritada çok daha küçük durur).")]
        [SerializeField, Min(0.05f)] private float _orbScale = 1.1f;
        [Tooltip("Kürenin zeminden yüksekliği — karakterin gövde hizası.")]
        [SerializeField] private float _orbHeight = 0.8f;

        [Header("Renk")]
        [Tooltip("Ton saniyede kaç tur atsın. Varsayılan, harita ekranı parlamasıyla aynı hız.")]
        [SerializeField, Min(0.05f)] private float _hueSpeed = 0.5f;
        [SerializeField, Range(0f, 1f)] private float _saturation = 0.85f;
        [Tooltip("Kürenin üstünde AYNI ANDA kaç tonluk yelpaze görünsün. 1 = tam gökkuşağı.")]
        [SerializeField, Range(0f, 1f)] private float _hueSpread = 0.8f;
        [Tooltip("Emisyon şiddeti — küre kendi ışığıyla parlasın.")]
        [SerializeField, Min(0f)] private float _glow = 2.4f;

        [Header("Sıra / zamanlama")]
        [Tooltip("Harita ekranının köşeye yerleşmesi için beklenecek EN FAZLA süre (sn). Ekran " +
                 "'hazırım' derse daha erken devam edilir; hiç demezse yolculuk asılı kalmaz.")]
        [SerializeField, Min(0.1f)] private float _stageTimeout = 3f;
        [Tooltip("Sahne yerleştikten sonra dönüşüme başlamadan önceki nefes (sn).")]
        [SerializeField, Min(0f)] private float _beforeMorph = 0.25f;
        [Tooltip("Toz savrulmaya başladıktan KAÇ SANİYE sonra küre toplanmaya başlasın. Küre " +
                 "hemen büyüseydi hâlâ karakter şeklinde duran tozun içinden çıkardı.")]
        [SerializeField, Min(0f)] private float _orbDelay = 0.5f;
        [Tooltip("Kürenin toplanma/dağılma süresi (sn). 'Yavaş yavaş dönüşsün' — kullanıcı isteği.")]
        [SerializeField, Min(0.05f)] private float _morphSeconds = 1.8f;
        [Tooltip("Dönüşüm bittikten sonra yola çıkmadan önceki bekleme (sn).")]
        [SerializeField, Min(0f)] private float _beforeMove = 0.25f;
        [Tooltip("Küre belirirken normal boyutunu ne kadar aşsın — toz püskürmesi hissi.")]
        [SerializeField, Range(0f, 1f)] private float _popOvershoot = 0.35f;

        /// <summary>Şu an bir hızlı seyahat sürüyor mu? (sahne toplanmasından varışa kadar)</summary>
        public bool Travelling => _travelling;

        /// <summary>Yolculuk BAŞLADI — harita ekranı köşeye çekilsin, üstteki UI kapansın.</summary>
        public event System.Action OnTravelBegan;

        /// <summary>Yolculuk BİTTİ — harita ekranı geri büyüsün, UI dönsün.</summary>
        public event System.Action OnTravelFinished;

        private bool  _travelling;
        private bool  _stageReady;    // sahne (harita ekranı) yerine oturdu mu
        private float _morph;         // 0 = karakter, 1 = küre
        private float _morphTarget;

        private GameObject       _orb;
        private EssenceOrbVisual _orbVisual;
        private Coroutine        _dustRoutine;
        private Coroutine        _routine;

        // Yedek yol (toz efekti yoksa)
        private Vector3    _modelScale = Vector3.one;
        private Renderer[] _fallbackRenderers;

        private void Awake()
        {
            if (_player == null) _player = GetComponent<PlayerController>();
            if (_dust   == null) _dust   = GetComponent<TeleportDustEffect>();
            if (_characterModel == null) _characterModel = transform.Find("Model");

            if (_characterModel != null) _modelScale = _characterModel.localScale;
            // Model yoksa (kapsül placeholder) ölçek animasyonu yapılamaz — kökü küçültmek küreyi
            // de küçültürdü, çünkü küre kökün ÇOCUĞU. Render'ları kapatmakla yetiniyoruz.
            else _fallbackRenderers = GetComponentsInChildren<Renderer>(true);
        }

        // ── Dış API ──────────────────────────────────────────────────────────

        /// <summary>Hızlı seyahati başlatır: sahne toplanır, karakter küreye dönüşür, yol kat
        /// edilir, karakter geri gelir. Hareketi BU bileşen başlatır — dönüşüm bitmeden yola
        /// çıkılmamalı.</summary>
        public void StartTravel(List<HexCell> path, float speedMultiplier)
        {
            if (_travelling || path == null || path.Count < 2 || _player == null) return;
            _routine = StartCoroutine(TravelRoutine(path, speedMultiplier));
        }

        /// <summary>Harita ekranı "köşeye yerleştim" diyor — dönüşüm başlayabilir.</summary>
        public void NotifyStageReady() => _stageReady = true;

        // ── Sıra ─────────────────────────────────────────────────────────────

        private IEnumerator TravelRoutine(List<HexCell> path, float speedMultiplier)
        {
            _travelling = true;
            _stageReady = false;
            OnTravelBegan?.Invoke();

            // 1) SAHNE: harita ekranı köşeye çekilsin. Dinleyen yoksa ya da ekran arada kapandıysa
            //    tolerans dolunca yine de devam edilir — yolculuk asla asılı kalmaz.
            for (float w = 0f; !_stageReady && w < _stageTimeout; w += Time.deltaTime)
                yield return null;
            if (_beforeMorph > 0f) yield return new WaitForSeconds(_beforeMorph);

            // 2) DÖNÜŞÜM: önce toz savrulur, küre biraz gecikmeyle toplanır.
            PlayDust(true);
            if (_orbDelay > 0f) yield return new WaitForSeconds(_orbDelay);
            _morphTarget = 1f;
            while (_morph < 1f) yield return null;
            if (_beforeMove > 0f) yield return new WaitForSeconds(_beforeMove);

            // 3) YOLCULUK: rota hızlıca kat edilir. GÖRÜŞ KAPALI — taş taşır, keşif yaptırmaz.
            _player.MoveAlongPath(path, speedMultiplier, revealFog: false);
            yield return null;                       // IsMoving bir sonraki karede kesinleşsin
            while (_player.IsMoving) yield return null;

            // 4) GERİ DÖNÜŞÜM: tozlar toplanır, karakter oluşur, harita ekranı büyür.
            PlayDust(false);
            _morphTarget = 0f;
            _travelling  = false;
            _routine     = null;
            OnTravelFinished?.Invoke();
        }

        // ── Döngü ────────────────────────────────────────────────────────────

        private void Update()
        {
            bool moving = !Mathf.Approximately(_morph, _morphTarget);
            if (moving) _morph = Mathf.MoveTowards(_morph, _morphTarget, Time.deltaTime / _morphSeconds);

            if (moving || _morph > 0f) ApplyMorph();
            if (_morph > 0f) PaintRainbow();
        }

        private void ApplyMorph()
        {
            if (_dust == null || !_dust.HasModel) ApplyFallbackModel();

            if (_morph <= 0.001f) { ReleaseOrb(); return; }

            EnsureOrb();
            if (_orb == null) return;

            // Küre normal boyutunu aşarak belirir, sonra oturur: toz püsküren, sonra toplanan bir
            // bulut hissi. Düz bir lerp "balon şişiyor" gibi cansız durur.
            _orb.transform.localScale = Vector3.one * (_orbScale * PopScale(_morph));
        }

        /// <summary>0'dan 1'i aşıp geri oturan yumuşama (ease-out-back).</summary>
        private float PopScale(float t)
        {
            float c1 = _popOvershoot * 4.7f;   // klasik katsayı 1.70158 ≈ 0.35 × 4.7
            float u  = t - 1f;
            return 1f + (c1 + 1f) * u * u * u + c1 * u * u;
        }

        // ── Toza ayrışma / geri toplanma ─────────────────────────────────────

        private void PlayDust(bool dissolve)
        {
            if (_dust == null || !_dust.HasModel) return;

            if (_dustRoutine != null) StopCoroutine(_dustRoutine);
            _dustRoutine = StartCoroutine(dissolve ? _dust.Dissolve() : _dust.Reassemble());
        }

        // ── Küre ─────────────────────────────────────────────────────────────

        private void EnsureOrb()
        {
            if (_orb != null || _orbPrefab == null) return;

            _orb = Instantiate(_orbPrefab, transform);
            _orb.name = "TravelOrb";
            _orb.transform.localPosition = new Vector3(0f, _orbHeight, 0f);
            _orb.transform.localRotation = Quaternion.identity;
            _orb.transform.localScale    = Vector3.zero;

            _orbVisual = _orb.GetComponent<EssenceOrbVisual>();
            // Süzülme merkezi konum YAZILDIKTAN SONRA alınmalı: prefab (0,0,0)'da doğar, Awake'teki
            // değer kalsaydı küre ilk karede karakterin ayağına düşerdi.
            if (_orbVisual != null) _orbVisual.RecenterFloat();
        }

        private void ReleaseOrb()
        {
            if (_orb == null) return;
            Destroy(_orb);
            _orb       = null;
            _orbVisual = null;
        }

        private void PaintRainbow()
        {
            if (_orbVisual == null) return;
            _orbVisual.SetRainbow(Mathf.Repeat(Time.time * _hueSpeed, 1f),
                                  _hueSpread, _saturation, _glow);
        }

        // ── Yedek yol: toz efekti yoksa modeli küçülterek gizle ──────────────

        private void ApplyFallbackModel()
        {
            bool visible = _morph < 0.999f;

            if (_characterModel != null)
            {
                if (_characterModel.gameObject.activeSelf != visible)
                    _characterModel.gameObject.SetActive(visible);
                if (visible) _characterModel.localScale = _modelScale * (1f - _morph * _morph);
                return;
            }

            if (_fallbackRenderers == null) return;
            foreach (Renderer r in _fallbackRenderers)
            {
                // Küre Awake'ten SONRA doğduğu için bu listede yok; yine de ucuz bir sigorta.
                if (r == null || (_orb != null && r.transform.IsChildOf(_orb.transform))) continue;
                if (r.enabled != visible) r.enabled = visible;
            }
        }

        private void OnDisable()
        {
            // Karakter küre/toz hâlinde sahneden çıkmasın (savaşa geçiş, sahne yenileme).
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            _travelling  = false;
            _dustRoutine = null;                 // bileşen kapanınca coroutine zaten durdu
            _morph = _morphTarget = 0f;
            ReleaseOrb();

            if (_dust != null && _dust.HasModel) _dust.ForceModelVisible();
            else
            {
                if (_characterModel != null) _characterModel.localScale = _modelScale;
                ApplyFallbackModel();
            }
        }
    }
}
