using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TacticalRPG.UI
{
    /// <summary>
    /// MİNİHARİTAYI YAKINLAŞTIRMA / KAYDIRMA — fare ile sürükleyerek gezinme + / − düğmeleri
    /// ve tekerlek (kullanıcı isteği 2026-08-17).
    ///
    /// YÖNTEM: dokunun <c>uvRect</c>'i değil, İÇERİK NESNESİNİN BOYUTU ve KONUMU değiştirilir.
    /// Sebep: işaretler (market/savaş/öz) dokunun ÇOCUĞU ve konumları anchor'a (0..1) bağlı —
    /// içerik büyüyüp kayınca işaretler kendiliğinden doğru yerde kalır, tek satır ek hesap
    /// gerekmez. uvRect yolu seçilseydi her işaretin ekran konumu elle yeniden hesaplanırdı.
    /// Ayrıca işaretler <c>sizeDelta</c>'sını korur → yakınlaştırınca devleşmezler.
    ///
    /// Bu bileşen MASKELİ ALANIN (viewport) üstünde durur: fare olayları onun grafiğine düşer,
    /// sürükleme buraya baloncuklanır. Taşma <see cref="RectMask2D"/> ile kırpılır.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class MinimapPanZoom : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        [Header("Bağımlılıklar")]
        [Tooltip("Maskeli görüş alanı (genellikle bu nesnenin kendi RectTransform'u).")]
        [SerializeField] private RectTransform _viewport;
        [Tooltip("Kaydırılıp ölçeklenecek içerik — harita dokusunun RectTransform'u.")]
        [SerializeField] private RectTransform _content;

        [Header("Düğmeler")]
        [SerializeField] private Button _zoomInButton;
        [SerializeField] private Button _zoomOutButton;

        [Header("Yakınlaştırma")]
        [Tooltip("1 = harita tamamı görünür. Alt sınır bunun altına inmez.")]
        [SerializeField, Min(0.2f)] private float _minZoom = 1f;
        [SerializeField, Min(1f)]   private float _maxZoom = 5f;
        [Tooltip("Bir kademede kaç kat yakınlaşılır (düğme ve tekerlek).")]
        [SerializeField, Min(1.05f)] private float _zoomStep = 1.35f;

        [Header("Oyuncuyu takip (yakınlaştırılmışken)")]
        [Tooltip("Karakter YÜRÜRKEN harita onu görüş alanında tutsun mu?")]
        [SerializeField] private bool _followPlayer = true;
        [Tooltip("ÖLÜ BÖLGE payı: nokta görüş alanının kenarından bu orana kadar yaklaşınca " +
                 "harita kaymaya başlar. 0.22 = ortadaki %56'lık alanda hiç kaydırma yok.")]
        [SerializeField, Range(0.05f, 0.45f)] private float _followMargin = 0.22f;
        [Tooltip("Takibin yumuşaklığı — büyük değer daha çabuk yetişir.")]
        [SerializeField, Min(1f)] private float _followSpeed = 8f;

        private Vector2 _baseSize;         // zoom 1'deki boyut (oranı korunmuş, viewport'a sığar)
        private float   _zoom = 1f;
        private Vector2 _pan;

        private Vector2 _dragStartLocal;   // sürüklemenin başladığı yerel nokta
        private Vector2 _panAtDragStart;
        private bool    _dragging;         // sürüklerken takip susar — el ile çekişmesin

        private void Reset() => _viewport = GetComponent<RectTransform>();

        private void OnEnable()
        {
            if (_viewport == null) _viewport = GetComponent<RectTransform>();
            if (_zoomInButton  != null) _zoomInButton.onClick.AddListener(ZoomIn);
            if (_zoomOutButton != null) _zoomOutButton.onClick.AddListener(ZoomOut);
            Apply();
        }

        private void OnDisable()
        {
            if (_zoomInButton  != null) _zoomInButton.onClick.RemoveListener(ZoomIn);
            if (_zoomOutButton != null) _zoomOutButton.onClick.RemoveListener(ZoomOut);
        }

        /// <summary>Zoom 1'deki içerik boyutunu bildirir (harita dokusu oranı korunarak sığdırılmış).
        /// Doku değişince <see cref="MinimapView"/> çağırır.</summary>
        public void SetBaseSize(Vector2 size)
        {
            _baseSize = size;
            Apply();
        }

        /// <summary>Görünümü başa alır: tam harita, ortalanmış. Harita ekranı her açıldığında
        /// çağrılır — oyuncu bıraktığı yakınlaştırmayla değil, bütün haritayla karşılaşsın.</summary>
        public void ResetView()
        {
            _zoom = _minZoom;
            _pan  = Vector2.zero;
            Apply();
        }

        public void ZoomIn()  => ZoomBy(_zoomStep);
        public void ZoomOut() => ZoomBy(1f / _zoomStep);

        private void ZoomBy(float factor)
        {
            float next = Mathf.Clamp(_zoom * factor, _minZoom, _maxZoom);
            if (Mathf.Approximately(next, _zoom)) return;

            // Merkeze göre yakınlaş: kaydırma da aynı oranda büyür ki ekranın ortasındaki yer
            // ortada kalsın (yoksa yakınlaştırma haritayı kaydırıyormuş gibi hissettirir).
            _pan *= next / _zoom;
            _zoom = next;
            Apply();
        }

        // ── Fare ─────────────────────────────────────────────────────────────

        public void OnBeginDrag(PointerEventData e)
        {
            _dragging       = true;
            _panAtDragStart = _pan;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _viewport, e.position, e.pressEventCamera, out _dragStartLocal);
        }

        public void OnDrag(PointerEventData e)
        {
            // Ekran pikseli yerine YEREL koordinat farkı kullanılır: canvas ölçeği ne olursa olsun
            // harita farenin altında birebir kayar (e.delta ölçek bilmez, kayma hızı bozulurdu).
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _viewport, e.position, e.pressEventCamera, out Vector2 local)) return;

            _pan = _panAtDragStart + (local - _dragStartLocal);
            Apply();
        }

        public void OnEndDrag(PointerEventData e) => _dragging = false;

        public void OnScroll(PointerEventData e)
        {
            if (Mathf.Abs(e.scrollDelta.y) < 0.01f) return;
            ZoomBy(e.scrollDelta.y > 0f ? _zoomStep : 1f / _zoomStep);
        }

        // ── Oyuncuyu görüş alanında tutma ────────────────────────────────────

        /// <summary>
        /// YAKINLAŞTIRILMIŞKEN KARAKTERİ TAKİP ET — nokta görüş alanının kenarına yaklaşınca harita
        /// kendiliğinden kayar (kullanıcı isteği 2026-08-19). <paramref name="uv"/> oyuncunun
        /// harita dokusundaki normalize konumudur.
        ///
        /// ÖLÜ BÖLGE (dead zone) kuralı: nokta ortadaki bölgedeyken HİÇ kaydırma yapılmaz. Sürekli
        /// ortalasaydık harita karakterin her adımında sallanır, oyuncu haritayı okuyamazdı —
        /// bu, kamera takibinde standart çözümdür. Nokta kenar bandına girince yalnız GİRDİĞİ KADAR
        /// geri itilir, yani takip haritayı en az rahatsız edecek şekilde çalışır.
        ///
        /// İki güvenlik: (1) sürükleme sırasında susar, oyuncunun eliyle çekişmez; (2) yalnız
        /// karakter HAREKET EDERKEN çağrılır (<see cref="MinimapView"/>) — duran karakterde
        /// takip çalışsaydı, haritanın başka bir köşesini incelemek için yapılan her sürükleme
        /// bırakılır bırakılmaz geri çekilirdi.
        ///
        /// Zoom 1'de kendiliğinden etkisiz: harita görüş alanına sığdığı için <see cref="Apply"/>
        /// kaydırmayı zaten sıfıra kırpar.
        /// </summary>
        public void KeepVisible(Vector2 uv)
        {
            if (!_followPlayer || _dragging) return;
            if (_content == null || _viewport == null) return;
            if (_baseSize.x <= 0.01f || _baseSize.y <= 0.01f) return;

            Rect    view  = _viewport.rect;
            Vector2 size  = _content.sizeDelta;
            Vector2 limit = new(view.width  * 0.5f * (1f - 2f * _followMargin),
                                view.height * 0.5f * (1f - 2f * _followMargin));

            // Noktanın GÖRÜŞ ALANI MERKEZİNE göre konumu: içerik merkezi _pan'da, nokta da
            // içeriğin merkezinden (uv - 0.5) kadar uzakta.
            Vector2 point = _pan + new Vector2((uv.x - 0.5f) * size.x, (uv.y - 0.5f) * size.y);

            Vector2 target = new(_pan.x - Overflow(point.x, limit.x),
                                 _pan.y - Overflow(point.y, limit.y));
            if (target == _pan) return;                 // ölü bölgenin içinde — kaydırma yok

            // Kare hızından bağımsız yumuşak yaklaşım. Menü açıkken oyun durdurulabildiği için
            // unscaledDeltaTime.
            _pan = Vector2.Lerp(_pan, target, 1f - Mathf.Exp(-_followSpeed * Time.unscaledDeltaTime));
            Apply();
        }

        /// <summary>Değerin ±limit bandından NE KADAR taştığı (bandın içindeyse 0).</summary>
        private static float Overflow(float value, float limit)
            => value >  limit ? value - limit
             : value < -limit ? value + limit
             : 0f;

        // ── Uygulama ─────────────────────────────────────────────────────────

        private void Apply()
        {
            if (_content == null || _viewport == null) return;
            if (_baseSize.x <= 0.01f || _baseSize.y <= 0.01f) return;

            _content.sizeDelta = _baseSize * _zoom;

            // Kaydırma sınırı: harita görüş alanından TAŞTIĞI kadar kaydırılabilir. Sığıyorsa
            // (zoom 1) hiç kaydırılmaz — harita ekrandan kaçırılamaz.
            Rect view = _viewport.rect;
            float maxX = Mathf.Max(0f, (_content.sizeDelta.x - view.width)  * 0.5f);
            float maxY = Mathf.Max(0f, (_content.sizeDelta.y - view.height) * 0.5f);

            _pan.x = Mathf.Clamp(_pan.x, -maxX, maxX);
            _pan.y = Mathf.Clamp(_pan.y, -maxY, maxY);
            _content.anchoredPosition = _pan;

            if (_zoomInButton  != null) _zoomInButton.interactable  = _zoom < _maxZoom - 0.001f;
            if (_zoomOutButton != null) _zoomOutButton.interactable = _zoom > _minZoom + 0.001f;
        }
    }
}
