using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TacticalRPG.Core;
using TacticalRPG.Grid;

namespace TacticalRPG.UI
{
    /// <summary>
    /// HIZLI SEYAHAT GÖSTERİSİ — güçlü yol taşıyla yolculuk onaylanınca harita ekranı KAPANMAZ,
    /// KÜÇÜLÜR: parşömen çerçeve, açıklama şeridi ve düğmeler silinir, geriye yalnız harita yüzeyi
    /// kalır; o da sol alt köşeye çekilip saydamlaşır. Böylece oyuncu küreye dönüşmüş karakteri
    /// ana haritada izlerken minihatitadan nerede olduğunu da görür — ve saydamlık sayesinde
    /// haritanın altındaki karolar okunmaya devam eder (kullanıcı isteği 2026-08-19).
    /// Varışta küçük hâl yeniden büyüyüp tam ekran harita olur.
    ///
    /// NEDEN PANEL KAPANMIYOR: kapansaydı <see cref="MinimapView"/> devre dışı kalır, canlı oyuncu
    /// noktası ve sis tazelemesi dururdu — yani tam da yolculuk sırasında harita ölürdü.
    ///
    /// TEK BİR İLERLEME DEĞERİ (<see cref="_t"/>: 0 = tam ekran, 1 = köşede) her şeyi sürer:
    /// konum, ölçek, saydamlık ve zeminin sönmesi. Küçülme ile büyüme aynı yolun iki yönüdür,
    /// ayrı animasyon kodu yoktur.
    ///
    /// ÇERÇEVE GİZLEME EL YAZIMI LİSTEYLE DEĞİL, ZİNCİR YÜRÜYÜŞÜYLE: yuvadan panel köküne çıkılır,
    /// her katmanda KARDEŞLER gizlenir. Böylece panele sonradan ne eklenirse eklensin (ipucu yazısı,
    /// yeni düğme, yeni şerit) seyahatte kendiliğinden silinir; yalnız yuvaya giden zincir ayakta
    /// kalır. Elle sayılan bir liste ilk eklemede sessizce eksik kalırdı.
    ///
    /// SIRAYI BU BİLEŞEN YÖNETMEZ: yolculuğun sahibi <see cref="TravelOrbVisual"/>'dir (karakterin
    /// üstünde, hep sahnede). Oyuncu yol alırken harita ekranını kapatabiliyor; sıra burada
    /// koşsaydı coroutine ölür, karakter toz hâlinde asılı kalırdı. Burası yalnız iki olayı dinler
    /// (başladı / bitti) ve küçülme animasyonu OTURUNCA "hazırım" der — dönüşüm ancak o zaman
    /// başlar, yani karakter yarı ekranın ortasında toza dönüşmez.
    /// </summary>
    public class TravelPresenter : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [Tooltip("Karakteri toza ayırıp küreye çeviren bileşen (Player üstünde). Yolculuğun sahibi odur.")]
        [SerializeField] private TravelOrbVisual _orb;
        [Tooltip("Küçülmeden önce harita 1× yakınlaştırmaya çekilir — köşede TÜM harita görünsün.")]
        [SerializeField] private MinimapPanZoom _panZoom;

        [Header("Panel")]
        [Tooltip("Tam ekran panel kökü (Panel_Map). Köşe konumu bunun dikdörtgeninden hesaplanır " +
                 "ve çerçeve gizleme buraya kadar yürür.")]
        [SerializeField] private RectTransform _panelRoot;
        [Tooltip("Panelin arkasındaki koyu zemin — küçülürken sönümlenir.")]
        [SerializeField] private Image _backdrop;

        [Header("Küçülen parça")]
        [Tooltip("Harita yuvası — köşeye giden tek parça budur (yuvanın ÇERÇEVE nesnesi).")]
        [SerializeField] private RectTransform _board;
        [Tooltip("Yuvanın koyu çerçevesi ve zemini. Küçük hâlde kapatılır: 'sadece harita kısmı'.")]
        [SerializeField] private Image[] _boardChrome;
        [Tooltip("Küçük hâldeki saydamlığı buradan gelir.")]
        [SerializeField] private CanvasGroup _boardGroup;

        [Header("Küçük hâl")]
        [SerializeField, Range(0.1f, 0.8f)] private float _miniScale = 0.34f;
        [Tooltip("Sol alt köşeden boşluk (piksel).")]
        [SerializeField] private Vector2 _miniMargin = new(28f, 28f);
        [Tooltip("Küçük hâlin saydamlığı — altındaki karolar okunabilsin.")]
        [SerializeField, Range(0.1f, 1f)] private float _miniAlpha = 0.55f;

        [Header("Zamanlama")]
        [SerializeField, Min(0.05f)] private float _shrinkSeconds  = 0.45f;
        [SerializeField, Min(0.05f)] private float _restoreSeconds = 0.55f;

        private float   _t;              // 0 = tam ekran, 1 = köşede
        private float   _target;
        private Vector2 _homeAnchored;   // yuvanın tam ekrandaki yeri
        private Color   _backdropColor;
        private bool    _chromeHidden;
        private bool    _stageReported;   // küreye "yerleştim" denildi mi (bir kez)

        // Gizlenenler AÇIKÇA saklanır: yalnız BİZİM kapattığımız şeyler geri açılır. "Hepsini aç"
        // deseydik zaten kapalı duran seyahat istemi de görünür hâle gelirdi.
        private readonly List<GameObject> _hidden = new();
        private readonly List<Image>      _dimmed = new();

        private void Awake()
        {
            if (_board    != null) _homeAnchored  = _board.anchoredPosition;
            if (_backdrop != null) _backdropColor = _backdrop.color;
        }

        private void OnEnable()
        {
            if (_orb != null)
            {
                _orb.OnTravelBegan    += HandleTravelBegan;
                _orb.OnTravelFinished += HandleTravelFinished;
            }

            // Yolculuk sürerken ekran yeniden açıldıysa doğrudan küçük hâlde açılsın —
            // büyüyüp hemen tekrar küçülen bir sıçrama olmasın.
            bool travelling = _orb != null && _orb.Travelling;
            _stageReported  = travelling;   // yolculuk sürüyorsa dönüşüm çoktan geçti
            _target = _t = travelling ? 1f : 0f;
            if (travelling) HideChrome(); else ShowChrome();
            Apply();
        }

        private void OnDisable()
        {
            if (_orb != null)
            {
                _orb.OnTravelBegan    -= HandleTravelBegan;
                _orb.OnTravelFinished -= HandleTravelFinished;
            }

            // Panel kapanırken yuva EVİNE dönmeli: bir dahaki açılışta köşede küçük ve saydam
            // bir harita bulunmasın.
            _target = _t = 0f;
            ShowChrome();
            Apply();
        }

        // ── Dış API (harita ekranındaki seyahat seçicisi çağırır) ────────────

        /// <summary>Yolculuk onaylandı. Sırayı küre bileşeni yürütür; burası yalnız onu başlatır.</summary>
        public void BeginTravel(List<HexCell> path, float speedMultiplier)
        {
            if (_orb != null) _orb.StartTravel(path, speedMultiplier);
        }

        // Küre "başladım" dedi: harita köşeye çekilsin, üstteki UI kapansın.
        private void HandleTravelBegan()
        {
            if (_panZoom != null) _panZoom.ResetView();   // köşede TÜM harita görünsün
            HideChrome();
            _stageReported = false;
            _target = 1f;
        }

        private void HandleTravelFinished() => _target = 0f;   // çerçeve büyümenin SONUNDA döner

        // ── Döngü ────────────────────────────────────────────────────────────

        private void Update()
        {
            if (Mathf.Approximately(_t, _target))
            {
                ReportStageReady();     // küçülme oturdu → dönüşüm başlayabilir
                return;                 // duruyorken başka hiçbir şey yazılmaz
            }

            float seconds = _target > _t ? _shrinkSeconds : _restoreSeconds;
            _t = Mathf.MoveTowards(_t, _target, Time.unscaledDeltaTime / seconds);
            Apply();

            if (_t <= 0.0001f) ShowChrome();
        }

        /// <summary>Küçülme animasyonu KÖŞEDE OTURDUĞUNDA küreye bir kez haber verilir. Dönüşüm
        /// ancak bundan sonra başlar — kullanıcı isteği: "önce minimap yerine yerleşsin, işlem
        /// orada bitsin, sonra karakter topa dönüşsün".</summary>
        private void ReportStageReady()
        {
            if (_stageReported || _t < 0.999f || _orb == null || !_orb.Travelling) return;
            _stageReported = true;
            _orb.NotifyStageReady();
        }

        private void Apply()
        {
            if (_board == null || _panelRoot == null) return;

            float k = _t * _t * (3f - 2f * _t);   // smoothstep: uçlarda yumuşak

            // EV KONUMU HER SEFERİNDE YENİDEN ÖLÇÜLÜR: pencere yeniden boyutlanmış olabilir,
            // saklanmış bir dünya konumu o zaman bayatlar. Anchor'ı eve yazıp dünya karşılığını
            // okumak, anchor/pivot düzeninden bağımsız tek doğru yöntem.
            _board.anchoredPosition = _homeAnchored;
            Vector3 home = _board.position;

            _board.position   = Vector3.Lerp(home, MiniWorldPosition(), k);
            _board.localScale = Vector3.one * Mathf.Lerp(1f, _miniScale, k);

            if (_boardGroup != null)
            {
                _boardGroup.alpha          = Mathf.Lerp(1f, _miniAlpha, k);
                _boardGroup.blocksRaycasts = k < 0.5f;   // köşedeyken tıklama ana haritaya gitsin
                _boardGroup.interactable   = k < 0.5f;
            }

            // "Sadece harita kısmı": yuvanın çerçevesi ve koyu zemini daha ilk kıpırtıda gider,
            // geriye boyanmış harita yüzeyi ile işaretler kalır.
            SetEnabled(_boardChrome, k < 0.02f);

            // Zemin görünmez olur ama KAPANMAZ: raycast'i yutmaya devam etsin. Kapatsaydık,
            // dönüşüm sürerken (karakter daha yola çıkmadan) 3B haritaya yapılan bir tık normal
            // yürüyüş başlatıp bedava hamleleri yakar ve gösteriyi bozardı — MapInputHandler
            // zaten "fare bir UI'ın üstünde mi" diye soruyor.
            if (_backdrop != null)
            {
                Color c = _backdropColor;
                c.a = _backdropColor.a * (1f - k);
                _backdrop.color   = c;
                _backdrop.enabled = true;
            }
        }

        /// <summary>Küçük hâlin DÜNYA konumu: panelin sol alt köşesi + boşluk + küçülmüş yarı boy.
        /// Panel tam ekran olduğu için bu doğrudan ekranın sol alt köşesidir.</summary>
        private Vector3 MiniWorldPosition()
        {
            Rect    r    = _panelRoot.rect;
            Vector2 half = _board.rect.size * (0.5f * _miniScale);
            var local = new Vector3(r.xMin + _miniMargin.x + half.x,
                                    r.yMin + _miniMargin.y + half.y, 0f);
            return _panelRoot.TransformPoint(local);
        }

        // ── Çerçeve: yuvaya giden zincir DIŞINDAKİ her şey ───────────────────

        private void HideChrome()
        {
            if (_chromeHidden || _board == null || _panelRoot == null) return;
            _chromeHidden = true;
            _hidden.Clear();
            _dimmed.Clear();

            // Yuvadan CANVAS'a kadar çık. Panel kökünü de geçiyoruz: köşedeki haritanın arkasında
            // kalan kalıcı sekme çubuğu da (panelin KARDEŞİ) böylece kendiliğinden kapanıyor —
            // kullanıcı isteği "minimapin geçtiği bölgedeki UI kapansın, sonra geri gelsin".
            for (Transform node = _board; node != null; node = node.parent)
            {
                Transform parent = node.parent;
                if (parent == null) break;

                for (int i = 0; i < parent.childCount; i++)
                {
                    Transform sibling = parent.GetChild(i);
                    if (sibling == node || !sibling.gameObject.activeSelf) continue;
                    sibling.gameObject.SetActive(false);
                    _hidden.Add(sibling.gameObject);
                }

                // Katmanın KENDİ grafiği (parşömen dolgu, koyu çerçeve) de gider. İSTİSNA panel
                // kökünün zemini: o kapanmaz, yumuşakça saydamlaşır ve tıklamayı yutmaya devam eder.
                if (parent != _panelRoot)
                {
                    Image graphic = parent.GetComponent<Image>();
                    if (graphic != null && graphic.enabled) { graphic.enabled = false; _dimmed.Add(graphic); }
                }

                if (parent.GetComponent<Canvas>() != null) break;
            }

            // MenuState'e DOKUNULMAZ: menü açık sayılmaya devam eder, yani IMGUI HUD'ları gizli
            // kalır. Eskiden burada "menü kapandı" deniyordu ve sol alttaki bölüm düğümü HUD'ı
            // tam da küçülen haritanın arkasında beliriyordu (kullanıcı şikâyeti).
        }

        private void ShowChrome()
        {
            if (!_chromeHidden) return;
            _chromeHidden = false;

            foreach (GameObject go in _hidden) if (go != null) go.SetActive(true);
            foreach (Image img in _dimmed)     if (img != null) img.enabled = true;
            _hidden.Clear();
            _dimmed.Clear();
        }

        private static void SetEnabled(Image[] graphics, bool on)
        {
            if (graphics == null) return;
            foreach (Image g in graphics)
                if (g != null && g.enabled != on) g.enabled = on;
        }
    }
}
