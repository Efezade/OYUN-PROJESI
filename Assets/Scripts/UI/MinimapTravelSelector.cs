using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using TacticalRPG.Core;
using TacticalRPG.Grid;

namespace TacticalRPG.UI
{
    /// <summary>
    /// HARİTADAN SEYAHAT — minihatitada bir karoya tıkla, rotayı ve bedelini gör, onayla, git
    /// (kullanıcı isteği 2026-08-17).
    ///
    /// AKIŞ:
    ///   1. Tıklanan noktanın karosu bulunur (doku→dünya→hex, 3B haritayla AYNI dönüşüm).
    ///   2. Karo YÜRÜNEMEZSE (dağ, su) seçim iptal edilmez — en yakın YÜRÜNEBİLİR karoya kayar.
    ///      Böylece dağa tıklamak "hiçbir şey olmadı" hissi vermez, niyeti okur.
    ///   3. Hedef karo parlar, oraya giden EN KISA rota yarı saydam noktalarla çizilir.
    ///   4. Altta bedel yazar: kaç karo, kaç güçlü yol taşı.
    ///   5. ONAYLA → gösteri sırası başlar: harita ekranı köşeye yerleşir, karakter ağır ağır
    ///      toza ayrılıp rengarenk bir küreye dönüşür, sonra yol hızlıca kat edilir
    ///      (<see cref="TravelPresenter"/> + <c>TravelOrbVisual</c>).
    ///
    /// SİS KURALI (İKİ KATMANLI):
    ///   • HEDEF yalnız KEŞFEDİLMİŞ karo olabilir. Bu, 3B haritadaki kuralın aynısı
    ///     (<c>MapInputHandler._freeMoveOnExplored</c>: keşfedilmiş yere mesafe sınırsız).
    ///   • ROTA da yalnız keşfedilmiş karolardan geçer (2026-08-19). Eskiden yalnız hedef
    ///     denetleniyordu; A* kestirmeyi sisin içinden bulup karakteri hiç görmediği araziden
    ///     geçiriyordu → sisi açmanın bir anlamı kalmıyordu. Artık "keşfedilmiş karolar arasında
    ///     en kısa yol" aranır; keşif ağı kopuksa yolculuk hiç önerilmez, sebebi yazılır.
    ///
    /// İKİNCİ İŞ — YOL BELİRLE / DURAKLI ROTA (kullanıcı isteği 2026-09-01): aynı harita, aynı
    /// tıklama boru hattı, TAMAMEN FARKLI amaç. Taş harcanmaz, kimse yürümez; konan duraklar
    /// <see cref="RouteMarker"/>'a gider, o da 3B arazide karo karo giden patikayı çizer.
    /// Ayrımlar bilinçli:
    ///   • Seyahatin HEDEFİ tektir ve keşfedilmiş olmalıdır; rotanın DURAKLARI çok olabilir
    ///     (Google Maps deseni) ve sisin içinde de durabilir — oraya gidilmiyor, oraya BAKILIYOR.
    ///   • Seyahatte tık seçer, alttaki ONAYLA yürütür; rotada her tık bir durak EKLER, durağa
    ///     tıklamak SİLER, şeritteki düğme hepsini birden temizler. Onay adımı yok.
    ///   • Ekran parlaması seyahatte GÖKKUŞAĞI, yol belirlemede KIRMIZIMSI: hangi kipte olduğun
    ///     haritanın kendisinden okunur.
    ///
    /// Fare olayları maskeli harita yuvasına düşer; sürükleme <see cref="MinimapPanZoom"/>'un,
    /// tıklama bu bileşenin. İkisi aynı nesnede durur, biri diğerini bilmez.
    /// </summary>
    public class MinimapTravelSelector : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private MinimapRenderer    _renderer;
        [SerializeField] private HexGridManager     _grid;
        [SerializeField] private FogOfWarManager    _fog;
        [SerializeField] private PlayerController   _player;
        [SerializeField] private ActionPointManager _ap;
        [SerializeField] private GameStateManager   _state;
        [SerializeField] private ChapterRunManager  _run;
        [Tooltip("Seyahat gösterisi: karakteri küreye çevirir, harita ekranını köşeye küçültür.")]
        [SerializeField] private TravelPresenter    _presenter;
        [Tooltip("YEDEK — gösterici bağlı değilse harita ekranı eskisi gibi tamamen kapatılır.")]
        [SerializeField] private MenuNavigator      _nav;

        [Header("Görsel")]
        [Tooltip("Harita dokusunun RectTransform'u — tıklama koordinatı buna göre çözülür.")]
        [SerializeField] private RectTransform _content;
        [Tooltip("Seçim halkası ve rota noktalarının konduğu katman (dokunun çocuğu, onu kaplar).")]
        [SerializeField] private RectTransform _markerLayer;

        [Header("Onay istemi")]
        [SerializeField] private GameObject      _promptRoot;
        [SerializeField] private TextMeshProUGUI _costLabel;
        [SerializeField] private Button          _confirmButton;
        [SerializeField] private Button          _cancelButton;

        [Header("Yol taşı")]
        [SerializeField] private PlayerBuffs       _buffs;
        [SerializeField] private MinimapGlowEffect _glow;
        [Tooltip("GÜÇLÜ YOL TAŞI: mesafeye göre birkaç taş, ama AP ve zaman HARCANMAZ. " +
                 "Haritadan seyahatin TEK yolu (normal 'Yol Taşı' 2026-08-19'da kaldırıldı).")]
        [SerializeField] private Button            _powerButton;
        [SerializeField] private TextMeshProUGUI   _powerLabel;
        [Tooltip("Bir GÜÇLÜ YOL TAŞI haritanın kaçta biri kadar yol açar. 4 = harita boyunca " +
                 "gitmek ~4 taş eder (kullanıcı isteği 2026-08-17).")]
        [SerializeField, Range(2, 12)] private int _powerStoneDivisions = 4;

        [Header("Yol belirle (duraklı rota)")]
        [Tooltip("Güçlü yol taşı düğmesinin ALTINDAKİ bar. Basınca harita kırmızımsı parlar; " +
                 "her tık bir DURAK ekler, durağa tıklamak siler.")]
        [SerializeField] private Button      _routeButton;
        [Tooltip("YOL BELİRLE'nin ALTINDAKİ düğme (kullanıcı isteği 2026-09-02): tek tıkla " +
                 "duraklar, minihatita işaretleri ve 3B patika birden silinir. Kip açık " +
                 "olmasa da çalışır — rota kalıcıdır, silmesi de her an mümkün olmalı.")]
        [SerializeField] private Button      _routeClearButton;
        [Tooltip("Durakları tutan ve patikayı 3B haritada çizen bileşen (GameManager üstünde).")]
        [SerializeField] private RouteMarker _routeMarker;
        [Tooltip("Kip açıkken harita KIRMIZIMSI parlar — güçlü yol taşının gökkuşağından ayrılsın.")]
        [SerializeField] private Color _routeGlowColor      = new(1f, 0.26f, 0.20f);
        [Tooltip("Minihatitadaki durak bayrakları ve bacak noktalarının rengi.")]
        [SerializeField] private Color _routeSelectionColor = new(1f, 0.48f, 0.40f);

        [Header("Karo geri getirme (tanrısal yerleştirme)")]
        [Tooltip("KARO GERİ GETİR barı: kip açılınca haritadaki tüm çukurlar işaretlenir ve " +
                 "tıklananı hak harcayarak geri getirir (kullanıcı isteği 2026-09-02, madde 10).")]
        [SerializeField] private Button              _restoreButton;
        [Tooltip("Hakları tutan ve geri getirmeyi yapan bileşen (GameManager üstünde).")]
        [SerializeField] private TileRecoveryManager _recovery;
        [Tooltip("Kip açıkken haritanın parlama rengi — yeşilimsi 'onarım' tonu.")]
        [SerializeField] private Color _restoreGlowColor      = new(0.35f, 1f, 0.55f);
        [Tooltip("Çukur işaretlerinin rengi.")]
        [SerializeField] private Color _restoreSelectionColor = new(0.55f, 1f, 0.70f);

        [Header("Ayarlar")]
        [Tooltip("Yürünemez karoya tıklanınca en yakın yürünebilir karo kaç halka içinde aranır.")]
        [SerializeField, Range(1, 8)] private int _nearestSearchRings = 4;
        [Tooltip("Rota noktalarının saydamlığı — 'hafif şeffaf' olsun, haritayı boğmasın.")]
        [SerializeField, Range(0.05f, 1f)] private float _pathAlpha = 0.42f;
        [Tooltip("Rota noktasının karo boyutuna oranı.")]
        [SerializeField, Range(0.15f, 1f)] private float _pathDotScale = 0.46f;
        [SerializeField] private Color _pathColor      = new(1f, 0.93f, 0.55f);
        [SerializeField] private Color _selectionColor = new(0.35f, 1f, 0.85f);
        [Tooltip("Seçim halkasının nabız periyodu (sn) — 'parlasın'.")]
        [SerializeField, Min(0.1f)] private float _pulsePeriod = 1.1f;
        [Tooltip("Bu kadar pikselden fazla oynayan basış TIKLAMA değil SÜRÜKLEMEDİR.")]
        [SerializeField, Min(1f)] private float _dragThreshold = 6f;

        [Tooltip("HARİTADAN SEYAHATE ÖZEL yürüme hızı çarpanı. Onlarca karoluk rota normal hızda " +
                 "dakikalar sürerdi. YALNIZ GÖRSEL: AP ve zaman dilimi karo başına normal işler, " +
                 "bedel hiç değişmez. Elle yürümeye (3B haritaya tıklama) UYGULANMAZ.")]
        [SerializeField, Range(1f, 12f)] private float _travelSpeedMultiplier = 6f;

        private readonly List<GameObject> _markers = new();
        private List<HexCell>  _path;
        private Image          _selectionImage;
        private GameObject     _selectionGO;
        private Color          _pulseColor;      // seçim halkasının nabız rengi (kipe göre değişir)
        // Rota işaretleri (duraklar + bacak noktaları) AYRI tutulur: _markers her seçimde
        // silinir, rota ise kalıcı bir plandır — ekran açık kaldığı sürece durmalı.
        private readonly List<(GameObject go, float scale)> _routeMarkers = new();
        // Çukur işaretleri: yalnız GERİ GETİR kipi açıkken durur (kip kapanınca harita
        // temizlenmeli, yoksa oyuncu her açtığında haritayı çukur ikonlarıyla dolu bulur).
        private readonly List<(GameObject go, float scale)> _restoreMarkers = new();
        private Vector2        _lastMarkerSize;
        private Vector2        _pressScreen;
        private HexPathfinder  _pathfinder;

        private void Awake()
        {
            _pathfinder = new HexPathfinder();
            // Kurulum bağlamayı atlamış olsa bile yol işareti çalışsın (bkz. CLAUDE.md: kritik
            // bağ yalnız editör kurulumuna bırakılmaz).
            if (_routeMarker == null) _routeMarker = FindFirstObjectByType<RouteMarker>();
            if (_recovery    == null) _recovery    = FindFirstObjectByType<TileRecoveryManager>();
        }

        private void OnEnable()
        {
            if (_confirmButton != null) _confirmButton.onClick.AddListener(Confirm);
            if (_cancelButton  != null) _cancelButton.onClick.AddListener(OnCancel);
            if (_powerButton   != null) _powerButton.onClick.AddListener(TogglePower);
            if (_routeButton   != null) _routeButton.onClick.AddListener(ToggleRoute);
            if (_routeClearButton != null) _routeClearButton.onClick.AddListener(ClearRoute);
            if (_restoreButton    != null) _restoreButton.onClick.AddListener(ToggleRestore);
            if (_buffs != null) _buffs.OnTravelStonesChanged += RefreshStoneUI;
            if (_routeMarker != null) _routeMarker.OnChanged += RefreshRouteMarkers;
            if (_recovery    != null) _recovery.OnChanged    += RefreshRestoreMarkers;

            // Ekran her açıldığında SİLAHSIZ başlar: bir önceki seferden kalan "hazır" durumuyla
            // farkında olmadan taş harcanmasın.
            SetMode(TravelMode.None);
            Clear();
            RefreshRouteMarkers();     // duran rota ekran açılınca yine görünsün
        }

        private void OnDisable()
        {
            if (_confirmButton != null) _confirmButton.onClick.RemoveListener(Confirm);
            if (_cancelButton  != null) _cancelButton.onClick.RemoveListener(OnCancel);
            if (_powerButton   != null) _powerButton.onClick.RemoveListener(TogglePower);
            if (_routeButton   != null) _routeButton.onClick.RemoveListener(ToggleRoute);
            if (_routeClearButton != null) _routeClearButton.onClick.RemoveListener(ClearRoute);
            if (_restoreButton    != null) _restoreButton.onClick.RemoveListener(ToggleRestore);
            if (_recovery         != null) _recovery.OnChanged -= RefreshRestoreMarkers;
            if (_buffs != null) _buffs.OnTravelStonesChanged -= RefreshStoneUI;
            if (_routeMarker != null) _routeMarker.OnChanged -= RefreshRouteMarkers;

            SetMode(TravelMode.None);
            Clear();
        }

        // ── Yol taşı modları ─────────────────────────────────────────────────

        /// <summary>
        /// Haritadan seyahat TAMAMEN TAŞA BAĞLI (kullanıcı kararı 2026-08-17): taş kullanmadan
        /// harita ekranından yürünemez. Önce taş "kullan"ılır (ekran parlar), sonra hedef seçilir,
        /// sonra onaylanır. Taş ONAY anında harcanır — vazgeçen oyuncu taşını yakmaz.
        ///
        /// Tek taş türü var: GÜÇLÜ YOL TAŞI. Normal "Yol Taşı" (AP ve zamanı normal işleten ucuz
        /// tür) 2026-08-19'da kullanıcı isteğiyle KALDIRILDI — iki taşlı seçim ekranı, ikisi de
        /// aynı işi yaptığı için yalnız kafa karıştırıyordu.
        /// </summary>
        private enum TravelMode { None = 0, Power = 1, Route = 2, Restore = 3 }

        private TravelMode _mode = TravelMode.None;
        private int        _stonesNeeded;   // bu yolculuğun kaç taş ettiği

        private void TogglePower()
        {
            if (_mode == TravelMode.Power) { SetMode(TravelMode.None); Clear(); return; }
            if (_buffs != null && !_buffs.HasStones()) return;   // elde taş yok

            SetMode(TravelMode.Power);
            Clear();                          // mod değişti → eski seçim/rota geçersiz
            if (_glow != null) _glow.Play();  // parlama gösterisi
        }

        /// <summary>YOL BELİRLE barı: taş harcamaz, kimseyi yürütmez — durak koydurur.
        /// Aynı düğme kipi açıp kapatır; kip açıkken her tık bir durak ekler ya da siler.
        /// Kip kapansa bile ROTA DURUR: konan duraklar kalıcı bir plandır.</summary>
        private void ToggleRoute()
        {
            if (_mode == TravelMode.Route) { SetMode(TravelMode.None); Clear(); return; }

            SetMode(TravelMode.Route);
            Clear();
            if (_glow != null) _glow.Play();
            ShowPromptText("Karoya tıkla: DURAK ekle (sisin içi de olur)  ·  durağa tıkla: sil  ·  " +
                           "YOLU SİL: rotanın tamamını temizler.");
            RefreshRouteMarkers();
        }

        /// <summary>KARO GERİ GETİR barı (madde 10): kip açılınca haritadaki BÜTÜN çukurlar
        /// işaretlenir — "tanrısal bakış". Tıklanan çukur bir hak harcayarak geri gelir.
        /// Hak yoksa kip açılmaz: boş bir kipe girip neden çalışmadığını aramak kötü UI.</summary>
        private void ToggleRestore()
        {
            if (_mode == TravelMode.Restore) { SetMode(TravelMode.None); Clear(); return; }

            if (_recovery == null)
            { ShowHint("Geri getirme bileşeni sahnede yok (TileRecoveryManager)."); return; }

            if (_recovery.Credits <= 0)
            { ShowHint("Karo geri getirme hakkın yok — çökmüş bir karonun kenarında kazanılır."); return; }

            SetMode(TravelMode.Restore);
            Clear();
            if (_glow != null) _glow.Play();

            int holes = _recovery.RestorableTiles().Count;
            ShowPromptText(holes > 0
                ? $"{holes} çukur işaretlendi  ·  hak: {_recovery.Credits}  ·  " +
                  "geri getirmek istediğin çukura tıkla."
                : "Haritada geri getirilebilecek çukur yok (sisin içindekiler sayılmaz).");
        }

        private void SetMode(TravelMode mode)
        {
            _mode = mode;
            if (_glow != null)
            {
                // Kip rengi: yol belirlemede kırmızımsı, geri getirmede yeşilimsi, seyahatte gökkuşağı.
                if      (mode == TravelMode.Route)   _glow.SetTint(_routeGlowColor);
                else if (mode == TravelMode.Restore) _glow.SetTint(_restoreGlowColor);
                else                                 _glow.ClearTint();
                _glow.SetSustained(mode != TravelMode.None);
            }
            RefreshStoneUI();
            RefreshRouteUI();
            RefreshRestoreMarkers();   // kip kapandıysa çukur işaretlerini de toplar
        }

        private void RefreshRouteUI()
        {
            bool armed = _mode == TravelMode.Route;
            int  stops = _routeMarker != null ? _routeMarker.StopCount : 0;

            if (_routeButton != null)
            {
                var label = _routeButton.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                    label.text = armed     ? $"YOL BELİRLE: AÇIK ({stops} durak)"
                               : stops > 0 ? $"YOL BELİRLE ({stops} durak)"
                                           : "YOL BELİRLE";
            }

            // YOLU SİL: rota varken açık, yokken sönük — basacak bir şey olmadığında düğmenin
            // tepki vermemesi, "bastım ama olmadı" duygusundan iyidir.
            if (_routeClearButton != null)
            {
                _routeClearButton.interactable = stops > 0;
                var clearLabel = _routeClearButton.GetComponentInChildren<TextMeshProUGUI>();
                if (clearLabel != null)
                    clearLabel.text = stops > 0 ? $"YOLU SİL ({stops} durak)" : "YOLU SİL";
            }

            // Şeritteki düğme kipe göre iş değiştirir: seyahatte seçimi bırakır, rota kipinde
            // TÜM durakları siler. Yazısı da onu söylemeli, yoksa "vazgeç" yanlış vaat olur.
            if (_cancelButton != null)
            {
                var cancelLabel = _cancelButton.GetComponentInChildren<TextMeshProUGUI>();
                if (cancelLabel != null)
                    cancelLabel.text = armed && stops > 0 ? "ROTAYI SİL" : "VAZGEÇ";
            }
        }

        /// <summary>YOLU SİL düğmesi (kullanıcı isteği 2026-09-02): rotaya ait HER ŞEYİ siler —
        /// duraklar, minihatitadaki bayrak/nokta işaretleri ve 3B haritadaki patika + bayraklar.
        /// Kip açık olmasa da çalışır: rota kalıcı bir plan olduğu için silmek de her an
        /// mümkün olmalı, önce YOL BELİRLE'yi açmak zorunda kalmadan.</summary>
        private void ClearRoute()
        {
            if (_routeMarker == null || !_routeMarker.HasRoute)
            {
                ShowHint("Silinecek rota yok.");
                return;
            }

            // ClearAll → OnChanged → RefreshRouteMarkers: minihatita işaretleri ve 3B patika
            // (RouteMarker durak listesi boşalınca kendini gizler) birlikte gider.
            _routeMarker.ClearAll();
            ShowPromptText("Yol silindi — haritada ve arazide rota işareti kalmadı.");
        }

        /// <summary>Şeritteki düğme: rota kipinde TÜM durakları siler, değilse seçimi bırakır.</summary>
        private void OnCancel()
        {
            if (_mode == TravelMode.Route && _routeMarker != null && _routeMarker.HasRoute)
            {
                _routeMarker.ClearAll();
                ShowPromptText("Rota silindi. Karoya tıklayarak yeni durak ekleyebilirsin.");
                return;
            }
            Clear();
        }

        private void RefreshStoneUI()
        {
            bool armed = _mode == TravelMode.Power;

            if (_powerButton != null)
            {
                _powerButton.interactable = armed || _buffs == null || _buffs.HasStones();
                var label = _powerButton.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = armed ? "GÜÇLÜ YOL TAŞI: AÇIK" : "GÜÇLÜ YOL TAŞI KULLAN";
            }

            if (_powerLabel == null) return;

            // "∞" YAZILMIYOR: yazı tipi atlasında bulunmayan karakter TMP'de kutu olarak çizilir.
            string count = _buffs == null ? "0"
                         : _buffs.UnlimitedTravelTokens ? "sınırsız"
                         : _buffs.Stones().ToString();
            _powerLabel.text = $"Güçlü yol taşı: {count}";
        }

        /// <summary>Bir GÜÇLÜ YOL TAŞININ kaç karoluk yol açtığı — haritanın uzun kenarının
        /// <see cref="_powerStoneDivisions"/>'te biri. Böylece "harita boyu ≈ 4 taş" kuralı
        /// harita boyutu değişse de kendini korur.</summary>
        private int TilesPerPowerStone()
        {
            int span = _grid != null ? Mathf.Max(_grid.Width, _grid.Height) : 36;
            return Mathf.Max(1, Mathf.CeilToInt(span / (float)_powerStoneDivisions));
        }

        // ── Girdi ────────────────────────────────────────────────────────────

        public void OnPointerDown(PointerEventData e) => _pressScreen = e.position;

        public void OnPointerClick(PointerEventData e)
        {
            // Haritayı kaydırmak da "aynı nesnede basıp bırakmak"tır → Unity bunu da tıklama
            // sayar. Basış noktasından ne kadar uzaklaşıldığına bakıp ayırıyoruz.
            if ((e.position - _pressScreen).sqrMagnitude > _dragThreshold * _dragThreshold) return;

            SelectAt(e);
        }

        private void SelectAt(PointerEventData e)
        {
            if (_mode == TravelMode.Route)   { MarkRouteAt(e);  return; }
            if (_mode == TravelMode.Restore) { RestoreTileAt(e); return; }

            // TAŞSIZ SEYAHAT YOK: önce bir yol taşı kullanılmalı. Sessizce hiçbir şey yapmak
            // yerine sebebini yazıyoruz — yoksa oyuncu haritanın bozuk olduğunu sanır.
            if (_mode == TravelMode.None) { ShowHint("Gitmek için GÜÇLÜ YOL TAŞI, yalnız işaret koymak için YOL BELİRLE kullan."); return; }

            if (_renderer == null || _grid == null || _player == null) { Clear(); return; }
            if (_state != null && _state.State != GameState.Overworld) { Clear(); return; }
            if (_run != null && _run.ChapterLost) { Clear(); return; }   // sert kesim: ilerleme yok
            if (_player.IsMoving) { Clear(); return; }

            if (!TryCoordFromPointer(e, out HexCoordinate clicked)) { Clear(); return; }

            // Kendi karona tıklamak seçim değil, iptaldir. Aksi halde "en yakın geçerli karo"
            // araması rastgele bir komşuyu seçerdi — oyuncunun istemediği bir hamle.
            if (clicked.Equals(_player.CurrentCoord)) { Clear(); return; }

            if (!TryResolveTarget(clicked, out HexCoordinate target)) { Clear(); return; }

            ShowRoute(target);
        }

        /// <summary>Tıklanan ekran noktasının karosu. Doku→UV→dünya→hex; 3B haritayla aynı dönüşüm.</summary>
        private bool TryCoordFromPointer(PointerEventData e, out HexCoordinate coord)
        {
            coord = default;
            if (_renderer == null || _content == null) return false;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _content, e.position, e.pressEventCamera, out Vector2 local)) return false;

            Rect r = _content.rect;
            var uv = new Vector2((local.x - r.xMin) / r.width, (local.y - r.yMin) / r.height);
            if (uv.x < 0f || uv.x > 1f || uv.y < 0f || uv.y > 1f) return false;

            return _renderer.TryGetCoordAt(uv, out coord);
        }

        // ── Rota / duraklar (YOL BELİRLE kipi) ──────────────────────────────

        /// <summary>
        /// YOL BELİRLE tıklaması — GOOGLE MAPS "durak ekle" deseni:
        ///   • Boş bir karoya tık → SONA yeni durak eklenir (rota o sırayla gezilir).
        ///   • Var olan durağa tık → o durak SİLİNİR. Ayrı bir silme düğmesi gerekmiyor.
        ///   • Alttaki şeritteki "ROTAYI TEMİZLE" hepsini birden siler.
        /// Seyahatten farkları: taş harcanmaz, kimse yürümez, sis denetlenmez (sisin içindeki
        /// karo da durak olabilir). Yürünemez karoya tıklanırsa EN YAKIN yürünebilir karoya
        /// kayar — Maps'in durağı en yakın yola oturtması gibi; yoksa oraya patika çizilemezdi.
        ///
        /// TEK TIK yeter (eski çift tık kaldırıldı): beş duraklı bir rota on isabetli tık
        /// isterdi. Yanlışlıkla eklenen durak zaten tek tıkla siliniyor, sürükleme de
        /// <see cref="_dragThreshold"/> ile tıklamadan ayrılıyor.
        /// </summary>
        private void MarkRouteAt(PointerEventData e)
        {
            if (_routeMarker == null)
            { ShowHint("Rota bileşeni sahnede yok (RouteMarker)."); return; }

            if (_grid == null || _renderer == null) return;
            if (_state != null && _state.State != GameState.Overworld) return;

            if (!TryCoordFromPointer(e, out HexCoordinate coord)) return;

            // Kendi karona durak konmaz: "en yakın uygun karo" araması rastgele bir komşuyu
            // seçip oyuncunun istemediği bir durak bırakırdı.
            if (_player != null && coord.Equals(_player.CurrentCoord))
            { ShowPromptText("Zaten oradasın — durak başka bir karoya konur."); return; }

            // Var olan durağa tıklamak = silmek. Kaydırmadan ÖNCE bakılır, yoksa durağın
            // yanına tıklayan oyuncu onu silmek yerine ikinci bir durak koymuş olurdu.
            if (_routeMarker.IndexOf(coord) >= 0)
            {
                _routeMarker.RemoveStop(coord);
                RouteFeedback("Durak silindi.");
                return;
            }

            if (!TryResolveStopTile(coord, out HexCoordinate stop))
            { ShowHint("Oraya durak konamaz — yakınında yürünebilir karo yok."); return; }

            if (_routeMarker.IndexOf(stop) >= 0)
            { _routeMarker.RemoveStop(stop); RouteFeedback("Durak silindi."); return; }

            if (_routeMarker.IsFull)
            { ShowHint($"En fazla {_routeMarker.MaxStops} durak konabilir."); return; }

            _routeMarker.AddStop(stop);
            RouteFeedback($"{_routeMarker.StopCount}. durak eklendi.");
        }

        /// <summary>Durak karosu: YÜRÜNEBİLİR olmalı (patika oradan geçecek) ama sisli olabilir.
        /// Tıklanan karo uymuyorsa halka halka dışa doğru en yakın uygun karo aranır.</summary>
        private bool TryResolveStopTile(HexCoordinate clicked, out HexCoordinate stop)
        {
            stop = clicked;
            if (IsStopTile(clicked)) return true;

            for (int radius = 1; radius <= _nearestSearchRings; radius++)
            {
                HexCoordinate c = clicked;
                for (int i = 0; i < radius; i++) c = c.GetNeighbor(4);

                for (int side = 0; side < 6; side++)
                    for (int step = 0; step < radius; step++)
                    {
                        if (IsStopTile(c)) { stop = c; return true; }
                        c = c.GetNeighbor(side);
                    }
            }
            return false;
        }

        private bool IsStopTile(HexCoordinate c)
            => !c.Equals(_player != null ? _player.CurrentCoord : default) &&
               _grid.TryGetCell(c, out HexCell cell) && cell.IsWalkable;

        /// <summary>Durak eklendi/silindi: şeritte özet, işaretler yeniden çizilir.
        /// Kip AÇIK KALIR — arka arkaya durak eklemek Maps'te de tek akıştır.</summary>
        private void RouteFeedback(string what)
        {
            // İşaretleri BURADA yenilemeye gerek yok: durak ekleme/silme RouteMarker.OnChanged'i
            // tetikliyor, o da RefreshRouteMarkers'ı çağırıyor. İkinci kez çağırmak aynı
            // nesneleri bir karede iki kez yok edip yeniden kurmak olurdu.
            if (!_routeMarker.HasRoute) { ShowPromptText(what + " Rota boş."); return; }

            string summary = $"{what}  ·  {_routeMarker.StopCount} durak  ·  {_routeMarker.TotalTiles} karo";
            if (_routeMarker.HasEstimate) summary += "  ·  sisin ötesi TAHMİNİ";
            ShowPromptText(summary + "  ·  haritayı kapat: patika arazide görünür.");
        }

        /// <summary>Rotanın minihatitadaki gösterimi: bacaklar nokta nokta, duraklar numaralı
        /// bayrakla. Kip kapalıyken de durur — rota kalıcı bir plandır, bir seçim değil.</summary>
        private void RefreshRouteMarkers()
        {
            foreach ((GameObject go, float _) in _routeMarkers) if (go != null) Destroy(go);
            _routeMarkers.Clear();

            if (_routeMarker == null || !_routeMarker.HasRoute) { RefreshRouteUI(); return; }

            // Bacaklar: planın ara karoları. Duraklar ve oyuncunun karosu atlanır (birinin
            // üstünde bayrak, öbüründe canlı oyuncu noktası var).
            IReadOnlyList<RouteMarker.Step> plan = _routeMarker.Plan;
            for (int i = 1; i < plan.Count; i++)
            {
                RouteMarker.Step step = plan[i];
                if (_routeMarker.IndexOf(step.Coord) >= 0) continue;

                Color dot = _routeSelectionColor;
                dot.a = step.Estimated ? _pathAlpha * 0.5f : _pathAlpha;
                AddRouteMarker(step.Coord, MinimapIconKind.PathDot, dot, _pathDotScale, null);
            }

            // Duraklar: bayrak + sıra numarası. Sıradaki durak tam parlak, sonrakiler sönük
            // (Maps'in aktif bacağı vurgulaması).
            IReadOnlyList<HexCoordinate> stops = _routeMarker.Stops;
            for (int i = 0; i < stops.Count; i++)
            {
                Color c = _routeSelectionColor;
                c.a = i == 0 ? 1f : 0.62f;
                AddRouteMarker(stops[i], MinimapIconKind.Waypoint, c, 1f, (i + 1).ToString());
            }

            RefreshRouteUI();
        }

        /// <summary>Rota işareti ekler; isteğe bağlı numara etiketiyle.</summary>
        private void AddRouteMarker(HexCoordinate coord, MinimapIconKind kind, Color color,
                                    float scale, string number)
        {
            Image img = AddMarker(coord, kind, color, scale, track: false);
            if (img == null) return;

            if (number != null)
            {
                var lblGO = new GameObject("No", typeof(RectTransform));
                var lblRT = (RectTransform)lblGO.transform;
                lblRT.SetParent(img.rectTransform, false);
                lblRT.anchorMin = lblRT.anchorMax = new Vector2(1f, 1f);
                lblRT.pivot     = new Vector2(0f, 0f);
                lblRT.anchoredPosition = Vector2.zero;
                lblRT.sizeDelta = new Vector2(26f, 26f);

                var tmp = lblGO.AddComponent<TextMeshProUGUI>();
                tmp.text          = number;
                tmp.fontSize      = 18f;
                tmp.color         = new Color(1f, 0.96f, 0.88f, color.a);
                tmp.alignment     = TextAlignmentOptions.Center;
                tmp.fontStyle     = FontStyles.Bold;
                tmp.raycastTarget = false;
            }

            _routeMarkers.Add((img.gameObject, scale));
        }

        // ── Karo geri getirme (KARO GERİ GETİR kipi, madde 10) ──────────────

        /// <summary>
        /// Tanrısal yerleştirme tıklaması: tıklanan karo bir ÇUKURSA hak harcanıp geri getirilir.
        ///
        /// EN YAKINA KAYMA YOK (rotadaki <see cref="TryResolveStopTile"/>'ın tersine): burada
        /// yanlış karoyu onarmak geri alınamaz bir kaynak harcamasıdır. Işıksız bir tıklama
        /// sessizce komşu çukuru onarsaydı, oyuncu hakkını istemediği yere yakmış olurdu.
        /// </summary>
        private void RestoreTileAt(PointerEventData e)
        {
            if (_recovery == null)
            { ShowHint("Geri getirme bileşeni sahnede yok (TileRecoveryManager)."); return; }

            if (!TryCoordFromPointer(e, out HexCoordinate coord)) return;

            if (!_recovery.TryRestore(coord, out string message))
            {
                ShowHint(message);
                return;
            }

            int left = _recovery.Credits;
            if (left <= 0)
            {
                // Hak bitti → kipte kalmanın anlamı yok, kendiliğinden kapanır.
                SetMode(TravelMode.None);
                ShowPromptText(message);
                return;
            }
            ShowPromptText($"{message}  ·  başka bir çukura tıklayabilirsin.");
        }

        /// <summary>Çukur işaretleri: YALNIZ kip açıkken çizilir. Kip kapalıyken harita
        /// temiz kalır — çukurlar zaten arazide gözle görünüyor.</summary>
        private void RefreshRestoreMarkers()
        {
            foreach ((GameObject go, float _) in _restoreMarkers) if (go != null) Destroy(go);
            _restoreMarkers.Clear();

            if (_mode == TravelMode.Restore && _recovery != null)
            {
                IReadOnlyList<HexCoordinate> holes = _recovery.RestorableTiles();
                Color c = _restoreSelectionColor;
                for (int i = 0; i < holes.Count; i++)
                {
                    Image img = AddMarker(holes[i], MinimapIconKind.Collapsed, c, 1f, track: false);
                    if (img != null) _restoreMarkers.Add((img.gameObject, 1f));
                }
            }

            RefreshRestoreUI();
        }

        private void RefreshRestoreUI()
        {
            if (_restoreButton == null) return;

            int credits = _recovery != null ? _recovery.Credits : 0;
            bool armed  = _mode == TravelMode.Restore;

            _restoreButton.interactable = credits > 0 || armed;
            var label = _restoreButton.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
                label.text = armed      ? $"GERİ GETİR: AÇIK ({credits} hak)"
                           : credits > 0 ? $"KARO GERİ GETİR ({credits})"
                                         : "KARO GERİ GETİR";
        }

        /// <summary>Tıklanan karo geçerli hedef mi? Değilse (dağ/su/bilinmeyen) EN YAKIN geçerli
        /// karoya kayar — halka halka dışa doğru aranır, ilk bulunan en yakınıdır.</summary>
        private bool TryResolveTarget(HexCoordinate clicked, out HexCoordinate target)
        {
            target = clicked;
            if (IsValidTarget(clicked)) return true;

            for (int radius = 1; radius <= _nearestSearchRings; radius++)
            {
                // Halka: merkezden radius uzaklıktaki tüm karolar (redblobgames "ring").
                HexCoordinate c = clicked;
                for (int i = 0; i < radius; i++) c = c.GetNeighbor(4);   // bir kenara yürü

                for (int side = 0; side < 6; side++)
                    for (int step = 0; step < radius; step++)
                    {
                        if (IsValidTarget(c)) { target = c; return true; }
                        c = c.GetNeighbor(side);
                    }
            }
            return false;
        }

        /// <summary>Hedef olabilecek karo: keşfedilmiş + yürünebilir + oyuncunun bastığı karo değil.</summary>
        private bool IsValidTarget(HexCoordinate c)
        {
            if (c.Equals(_player.CurrentCoord)) return false;
            if (_fog != null && !_fog.IsKnown(c)) return false;
            return _grid.TryGetCell(c, out HexCell cell) && cell.IsWalkable;
        }

        /// <summary>Rotanın üzerinden geçebileceği karo mu? Sis KALICI olduğu için bu "oyuncunun
        /// bir kez gördüğü yer" demektir — yani rota hep bilinen araziyi izler.</summary>
        private bool IsExplored(HexCell cell)
            => _fog == null || _fog.IsKnown(cell.Coordinate);

        // ── Rota + istem ─────────────────────────────────────────────────────

        private void ShowRoute(HexCoordinate target)
        {
            ClearMarkers();

            if (!_grid.TryGetCell(_player.CurrentCoord, out HexCell start) ||
                !_grid.TryGetCell(target, out HexCell goal)) { Clear(); return; }

            // ROTA YALNIZ KEŞFEDİLMİŞ KAROLARDAN GEÇER (2026-08-19). Filtresiz A* kestirmeyi
            // sisin içinden buluyor, karakter hiç görmediği araziden geçiyordu; o zaman da sisi
            // açmanın oyun içinde bir karşılığı kalmıyordu.
            _path = _pathfinder.FindPath(start, goal, _grid, IsExplored);
            if (_path == null || _path.Count < 2)
            {
                // Hedef keşfedilmiş ama oraya keşfedilmiş karolardan gidilemiyor (arada sisli bir
                // boğaz var, ya da kulenin açtığı bölge keşif izine hiç bağlanmamış). Sessizce
                // hiçbir şey yapmak "harita bozuk" hissi verirdi.
                ShowHint("Oraya keşfettiğin karolardan gidebileceğin bir yol yok.");
                return;
            }

            // Rota: ara karolar yarı saydam noktalarla. Başlangıç ve hedef dışarıda —
            // biri oyuncunun altında, öbürü seçim halkasıyla zaten işaretli.
            Color dot = _pathColor; dot.a = _pathAlpha;
            for (int i = 1; i < _path.Count - 1; i++)
                AddMarker(_path[i].Coordinate, MinimapIconKind.PathDot, dot, _pathDotScale);

            _pulseColor     = _selectionColor;
            _selectionImage = AddMarker(target, MinimapIconKind.Selection, _selectionColor, 1f);
            _selectionGO    = _selectionImage != null ? _selectionImage.gameObject : null;
            _lastMarkerSize = MarkerSize();

            ShowPrompt(_path.Count - 1);
        }

        /// <summary>Seçim yapılamadığında sebebi gösteren şerit (onay düğmesi gizli).</summary>
        private void ShowHint(string message)
        {
            ClearMarkers();
            _path = null;
            ShowPromptText(message);
        }

        /// <summary>Şeride yalnız YAZI koyar — işaretlere dokunmaz. Yol belirleme kipinde seçim
        /// halkası ekranda dururken açıklama yazmak gerekiyor.</summary>
        private void ShowPromptText(string message)
        {
            if (_promptRoot != null) _promptRoot.SetActive(true);
            if (_costLabel  != null) _costLabel.text = message;
            if (_confirmButton != null) _confirmButton.gameObject.SetActive(false);
        }

        private void ShowPrompt(int moves)
        {
            if (_promptRoot != null) _promptRoot.SetActive(true);

            _stonesNeeded = Mathf.Max(1, Mathf.CeilToInt(moves / (float)TilesPerPowerStone()));

            bool affordable = _buffs == null || _buffs.HasStones(_stonesNeeded);

            if (_confirmButton != null)
            {
                _confirmButton.gameObject.SetActive(true);
                _confirmButton.interactable = affordable;
            }

            if (_costLabel == null) return;

            // Yolculuk bedava — AP düşmez, zaman ilerlemez. Bedel yalnız taş.
            _costLabel.text = affordable
                ? $"{moves} karo  ·  {_stonesNeeded} güçlü yol taşı  ·  AP ve zaman harcanmaz"
                : $"{moves} karo  ·  {_stonesNeeded} güçlü yol taşı gerekir — yeterli taşın yok";
        }

        /// <summary>Bayrak gibi ASİMETRİK simgelerin pivotu ortada olamaz: direğin DİBİ karonun
        /// üstüne oturmalı, yoksa işaret komşu karoyu gösteriyormuş gibi durur. Desendeki direk
        /// dibinin normalize konumu (11×11 desende ~2.5 sütun, en alt satır).</summary>
        private static readonly Vector2 FlagPivot = new(0.22f, 0.06f);

        private Image AddMarker(HexCoordinate coord, MinimapIconKind kind, Color color, float scale,
                                bool track = true)
        {
            if (_markerLayer == null || _renderer == null ||
                !_renderer.TryGetUV(coord, out Vector2 uv)) return null;

            var go = new GameObject($"Travel_{kind}", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_markerLayer, false);
            rt.anchorMin = rt.anchorMax = uv;
            rt.pivot = kind == MinimapIconKind.Waypoint ? FlagPivot : new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = MarkerSize() * scale;

            var img = go.GetComponent<Image>();
            img.sprite        = MinimapIcons.Get(kind);
            img.color         = color;
            img.raycastTarget = false;

            if (track) _markers.Add(go);
            return img;
        }

        /// <summary>Bir karonun EKRANDAKİ ölçüsü. Yakınlaştırma dokuyu büyüttüğü için her
        /// seferinde dokunun güncel ölçek çarpanından türetilir — işaretler karoyla aynı büyür.</summary>
        private Vector2 MarkerSize()
        {
            Texture2D tex = _renderer.Texture;
            if (tex == null || tex.width == 0 || _content == null) return new Vector2(16f, 18f);

            float scale = _content.rect.width / tex.width;
            return _renderer.HexPixelSize * scale;
        }

        // Nabız + yakınlaştırmaya uyum. Yalnız seçim VARKEN çalışır (CLAUDE.md §6: boşta iş yok).
        private void Update()
        {
            if (_markers.Count == 0 && _routeMarkers.Count == 0 && _restoreMarkers.Count == 0) return;

            // Boyut YALNIZ değiştiyse yazılır. Her karede sizeDelta yazmak RectTransform'u kirletir
            // ve Canvas'ı her karede yeniden kurdurur — 30 rota noktasıyla bu boşuna bir maliyet.
            Vector2 size = MarkerSize();
            if ((size - _lastMarkerSize).sqrMagnitude > 0.0001f)
            {
                _lastMarkerSize = size;
                foreach (GameObject go in _markers)
                {
                    if (go == null) continue;
                    float scale = go == _selectionGO ? 1f : _pathDotScale;
                    ((RectTransform)go.transform).sizeDelta = size * scale;
                }
                foreach ((GameObject go, float scale) in _routeMarkers)
                    if (go != null) ((RectTransform)go.transform).sizeDelta = size * scale;
                foreach ((GameObject go, float scale) in _restoreMarkers)
                    if (go != null) ((RectTransform)go.transform).sizeDelta = size * scale;
            }

            if (_selectionImage == null) return;
            float k = (Mathf.Sin(Time.time * (Mathf.PI * 2f / _pulsePeriod)) + 1f) * 0.5f;
            Color c = _pulseColor;
            c.a = Mathf.Lerp(0.55f, 1f, k);
            _selectionImage.color = c;   // renk yazmak layout'u kirletmez, nabız serbest
        }

        // ── Eylemler ─────────────────────────────────────────────────────────

        /// <summary>ONAYLA: oyuncu rotayı HIZLANDIRILMIŞ yürüyüşle kat eder. Harita ekranı KAPANMAZ,
        /// sol alt köşeye küçülüp saydamlaşır (<see cref="TravelPresenter"/>) — oyuncu hem küreye
        /// dönüşmüş karakteri ana haritada izler hem minihatitadan nerede olduğunu görür.
        /// Hız yalnız görseldir.</summary>
        public void Confirm()
        {
            if (_path == null || _path.Count < 2 || _player == null) { Clear(); return; }

            if (_mode == TravelMode.None) { Clear(); return; }

            // Taş BURADA harcanır (düğmeye basınca değil): seçimden vazgeçen oyuncu taşını yakmaz.
            if (_buffs != null && !_buffs.TrySpendStones(_stonesNeeded))
            {
                ShowHint("Yeterli taşın yok.");
                return;
            }

            List<HexCell> path  = _path;
            int           moves = path.Count - 1;

            // GÜÇLÜ YOL TAŞI: yolculuk boyunca AP düşmesin, zaman dilimi ilerlemesin, gün dönmesin.
            // Bedava hamle stoku hamle başına eriyor → yolculuk bitince kendiliğinden kapanıyor.
            if (_ap != null) _ap.GrantFreeMoves(moves);

            // Seçim ve mod ÖNCE temizlenir: gösterici ekranı küçültürken onay şeridi hâlâ açık
            // olsaydı, "gizlemeden önce açıktı" diye kaydedilir ve varışta geri gelirdi.
            Clear();
            SetMode(TravelMode.None);

            // HAREKETİ ARTIK BURASI BAŞLATMAZ: gösteri sırası (harita köşeye yerleşir → karakter
            // ağır ağır küreye dönüşür → yol hızlıca kat edilir) TravelOrbVisual'da yürüyor.
            if (_presenter != null)
            {
                _presenter.BeginTravel(path, _travelSpeedMultiplier);
                return;
            }

            // Yedek: gösterici bağlı değilse eski davranış — ekran kapanır, yürüyüş hemen başlar.
            _player.MoveAlongPath(path, _travelSpeedMultiplier);
            if (_nav != null) _nav.CloseScreen();
        }

        /// <summary>Seçimi ve rotayı temizler, istemi gizler.</summary>
        public void Clear()
        {
            ClearMarkers();
            _path         = null;
            _stonesNeeded = 0;
            if (_promptRoot    != null) _promptRoot.SetActive(false);
            if (_confirmButton != null) _confirmButton.gameObject.SetActive(true);
        }

        private void ClearMarkers()
        {
            foreach (GameObject go in _markers) if (go != null) Destroy(go);
            _markers.Clear();
            _selectionImage = null;
            _selectionGO    = null;
            _lastMarkerSize = Vector2.zero;
        }
    }
}
