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
    public class MinimapPanZoom : MonoBehaviour, IBeginDragHandler, IDragHandler, IScrollHandler
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

        private Vector2 _baseSize;         // zoom 1'deki boyut (oranı korunmuş, viewport'a sığar)
        private float   _zoom = 1f;
        private Vector2 _pan;

        private Vector2 _dragStartLocal;   // sürüklemenin başladığı yerel nokta
        private Vector2 _panAtDragStart;

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

        public void OnScroll(PointerEventData e)
        {
            if (Mathf.Abs(e.scrollDelta.y) < 0.01f) return;
            ZoomBy(e.scrollDelta.y > 0f ? _zoomStep : 1f / _zoomStep);
        }

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
