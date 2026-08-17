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
    ///   4. Altta bedel yazar: kaç karo, kaç AP, kaç zaman dilimi devirir.
    ///   5. ONAYLA → oyuncu yürümeye başlar, harita ekranı kapanır.
    ///
    /// SİS KURALI: yalnız KEŞFEDİLMİŞ karo hedef seçilebilir. Bu, 3B haritadaki kuralın aynısı
    /// (<c>MapInputHandler._freeMoveOnExplored</c>: keşfedilmiş yere mesafe sınırsız, karanlığa
    /// 2 karo). Karanlığa minihatitadan gidilemez — zaten orası çizilmiyor.
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

        [Header("Yol taşları")]
        [SerializeField] private PlayerBuffs       _buffs;
        [SerializeField] private MinimapGlowEffect _glow;
        [Tooltip("YOL TAŞI: koşarak git, AP ve zaman normal işler. 1 taş / yolculuk.")]
        [SerializeField] private Button            _roadButton;
        [SerializeField] private TextMeshProUGUI   _roadLabel;
        [Tooltip("GÜÇLÜ YOL TAŞI: mesafeye göre birkaç taş, ama AP ve zaman HARCANMAZ.")]
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
            if (_roadButton    != null) _roadButton.onClick.AddListener(ToggleRoad);
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
            if (_roadButton    != null) _roadButton.onClick.RemoveListener(ToggleRoad);
            if (_powerButton   != null) _powerButton.onClick.RemoveListener(TogglePower);
            if (_buffs != null) _buffs.OnTravelStonesChanged -= RefreshStoneUI;

            SetMode(TravelMode.None);
            Clear();
        }

        // ── Yol taşı modları ─────────────────────────────────────────────────

        /// <summary>
        /// Haritadan seyahat TAMAMEN TAŞA BAĞLI (kullanıcı kararı 2026-08-17): taş kullanmadan
        /// harita ekranından yürünemez. Önce bir taş "kullan"ılır (ekran parlar), sonra hedef
        /// seçilir, sonra onaylanır. Taş ONAY anında harcanır — vazgeçen oyuncu taşını yakmaz.
        /// </summary>
        private enum TravelMode { None = 0, Road = 1, Power = 2 }

        private TravelMode _mode = TravelMode.None;
        private int        _stonesNeeded;   // Power modunda bu yolculuğun kaç taş ettiği

        private void ToggleRoad()  => Toggle(TravelMode.Road,  PlayerBuffs.TravelStone.Road);
        private void TogglePower() => Toggle(TravelMode.Power, PlayerBuffs.TravelStone.Power);

        private void Toggle(TravelMode mode, PlayerBuffs.TravelStone stone)
        {
            if (_mode == mode) { SetMode(TravelMode.None); Clear(); return; }
            if (_buffs != null && !_buffs.HasStones(stone)) return;   // elde taş yok

            SetMode(mode);
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
            PaintStoneButton(_roadButton,  _roadLabel,  PlayerBuffs.TravelStone.Road,
                             TravelMode.Road,  "YOL TAŞI KULLAN",        "YOL TAŞI: AÇIK",        "Yol taşı");
            PaintStoneButton(_powerButton, _powerLabel, PlayerBuffs.TravelStone.Power,
                             TravelMode.Power, "GÜÇLÜ YOL TAŞI KULLAN",  "GÜÇLÜ YOL TAŞI: AÇIK",  "Güçlü yol taşı");
        }

        private void PaintStoneButton(Button button, TextMeshProUGUI counter,
                                      PlayerBuffs.TravelStone stone, TravelMode mode,
                                      string idleText, string armedText, string counterName)
        {
            bool armed = _mode == mode;

            if (button != null)
            {
                button.interactable = armed || _buffs == null || _buffs.HasStones(stone);
                var label = button.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = armed ? armedText : idleText;
            }

            if (counter == null) return;

            // "∞" YAZILMIYOR: yazı tipi atlasında bulunmayan karakter TMP'de kutu olarak çizilir.
            string count = _buffs == null ? "0"
                         : _buffs.UnlimitedTravelTokens ? "sınırsız"
                         : _buffs.Stones(stone).ToString();
            counter.text = $"{counterName}: {count}";
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
            if (_mode == TravelMode.None) { ShowHint("Gitmek için önce bir YOL TAŞI kullan."); return; }

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

        // ── Rota + istem ─────────────────────────────────────────────────────

        private void ShowRoute(HexCoordinate target)
        {
            ClearMarkers();

            if (!_grid.TryGetCell(_player.CurrentCoord, out HexCell start) ||
                !_grid.TryGetCell(target, out HexCell goal)) { Clear(); return; }

            _path = _pathfinder.FindPath(start, goal, _grid);
            if (_path == null || _path.Count < 2) { Clear(); return; }

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

            _stonesNeeded = _mode == TravelMode.Power
                ? Mathf.Max(1, Mathf.CeilToInt(moves / (float)TilesPerPowerStone()))
                : 1;

            bool affordable = _buffs == null || _buffs.HasStones(StoneOfMode(), _stonesNeeded);

            if (_confirmButton != null)
            {
                _confirmButton.gameObject.SetActive(true);
                _confirmButton.interactable = affordable;
            }

            if (_costLabel == null) return;

            if (_mode == TravelMode.Power)
            {
                // GÜÇLÜ YOL TAŞI: yolculuk bedava — AP düşmez, zaman ilerlemez. Bedel yalnız taş.
                _costLabel.text = affordable
                    ? $"{moves} karo  ·  {_stonesNeeded} güçlü yol taşı  ·  AP ve zaman harcanmaz"
                    : $"{moves} karo  ·  {_stonesNeeded} güçlü yol taşı gerekir — yeterli taşın yok";
                return;
            }

            // YOL TAŞI: normal bedel işler, taş yalnız yolu koşarak kat ettirir.
            if (_ap == null) { _costLabel.text = $"{moves} karo  ·  1 yol taşı"; return; }

            _ap.PreviewCost(moves, out int apCost, out int slots);
            string time = slots > 0 ? $"{slots} zaman dilimi" : "aynı zaman diliminde";
            _costLabel.text = $"{moves} karo  ·  {apCost} AP  ·  {time}  ·  1 yol taşı";
        }

        private PlayerBuffs.TravelStone StoneOfMode()
            => _mode == TravelMode.Power ? PlayerBuffs.TravelStone.Power : PlayerBuffs.TravelStone.Road;

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

        /// <summary>ONAYLA: oyuncu rotayı HIZLANDIRILMIŞ yürüyüşle kat eder, harita ekranı kapanır
        /// (yürüyüşü görsün). Hız yalnız görseldir — AP ve zaman karo başına normal harcanır.</summary>
        public void Confirm()
        {
            if (_path == null || _path.Count < 2 || _player == null) { Clear(); return; }

            if (_mode == TravelMode.None) { Clear(); return; }

            // Taş BURADA harcanır (düğmeye basınca değil): seçimden vazgeçen oyuncu taşını yakmaz.
            if (_buffs != null && !_buffs.TrySpendStones(StoneOfMode(), _stonesNeeded))
            {
                ShowHint("Yeterli taşın yok.");
                return;
            }

            List<HexCell> path  = _path;
            int           moves = path.Count - 1;
            bool          free  = _mode == TravelMode.Power;

            // GÜÇLÜ YOL TAŞI: yolculuk boyunca AP düşmesin, zaman dilimi ilerlemesin, gün dönmesin.
            // Bedava hamle stoku hamle başına eriyor → yolculuk bitince kendiliğinden kapanıyor.
            if (free && _ap != null) _ap.GrantFreeMoves(moves);

            Clear();
            SetMode(TravelMode.None);
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
