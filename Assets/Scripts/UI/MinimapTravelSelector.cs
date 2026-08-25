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
        private Vector2        _lastMarkerSize;
        private Vector2        _pressScreen;
        private HexPathfinder  _pathfinder;

        private void Awake() => _pathfinder = new HexPathfinder();

        private void OnEnable()
        {
            if (_confirmButton != null) _confirmButton.onClick.AddListener(Confirm);
            if (_cancelButton  != null) _cancelButton.onClick.AddListener(Clear);
            if (_powerButton   != null) _powerButton.onClick.AddListener(TogglePower);
            if (_buffs != null) _buffs.OnTravelStonesChanged += RefreshStoneUI;

            // Ekran her açıldığında SİLAHSIZ başlar: bir önceki seferden kalan "hazır" durumuyla
            // farkında olmadan taş harcanmasın.
            SetMode(TravelMode.None);
            Clear();
        }

        private void OnDisable()
        {
            if (_confirmButton != null) _confirmButton.onClick.RemoveListener(Confirm);
            if (_cancelButton  != null) _cancelButton.onClick.RemoveListener(Clear);
            if (_powerButton   != null) _powerButton.onClick.RemoveListener(TogglePower);
            if (_buffs != null) _buffs.OnTravelStonesChanged -= RefreshStoneUI;

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
        private enum TravelMode { None = 0, Power = 1 }

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

        private void SetMode(TravelMode mode)
        {
            _mode = mode;
            if (_glow != null) _glow.SetSustained(mode != TravelMode.None);
            RefreshStoneUI();
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
            // TAŞSIZ SEYAHAT YOK: önce bir yol taşı kullanılmalı. Sessizce hiçbir şey yapmak
            // yerine sebebini yazıyoruz — yoksa oyuncu haritanın bozuk olduğunu sanır.
            if (_mode == TravelMode.None) { ShowHint("Gitmek için önce GÜÇLÜ YOL TAŞI kullan."); return; }

            if (_renderer == null || _grid == null || _player == null) { Clear(); return; }
            if (_state != null && _state.State != GameState.Overworld) { Clear(); return; }
            if (_run != null && _run.ChapterLost) { Clear(); return; }   // sert kesim: ilerleme yok
            if (_player.IsMoving) { Clear(); return; }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _content, e.position, e.pressEventCamera, out Vector2 local)) { Clear(); return; }

            Rect r = _content.rect;
            var uv = new Vector2((local.x - r.xMin) / r.width, (local.y - r.yMin) / r.height);
            if (uv.x < 0f || uv.x > 1f || uv.y < 0f || uv.y > 1f) { Clear(); return; }

            if (!_renderer.TryGetCoordAt(uv, out HexCoordinate clicked)) { Clear(); return; }

            // Kendi karona tıklamak seçim değil, iptaldir. Aksi halde "en yakın geçerli karo"
            // araması rastgele bir komşuyu seçerdi — oyuncunun istemediği bir hamle.
            if (clicked.Equals(_player.CurrentCoord)) { Clear(); return; }

            if (!TryResolveTarget(clicked, out HexCoordinate target)) { Clear(); return; }

            ShowRoute(target);
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

        private Image AddMarker(HexCoordinate coord, MinimapIconKind kind, Color color, float scale)
        {
            if (_markerLayer == null || !_renderer.TryGetUV(coord, out Vector2 uv)) return null;

            var go = new GameObject($"Travel_{kind}", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_markerLayer, false);
            rt.anchorMin = rt.anchorMax = uv;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = MarkerSize() * scale;

            var img = go.GetComponent<Image>();
            img.sprite        = MinimapIcons.Get(kind);
            img.color         = color;
            img.raycastTarget = false;

            _markers.Add(go);
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
            if (_markers.Count == 0) return;

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
            }

            if (_selectionImage == null) return;
            float k = (Mathf.Sin(Time.time * (Mathf.PI * 2f / _pulsePeriod)) + 1f) * 0.5f;
            Color c = _selectionColor;
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
