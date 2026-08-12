using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TacticalRPG.Core;
using TacticalRPG.Grid;

namespace TacticalRPG.UI
{
    /// <summary>
    /// DAVUL KARO DRAFTI EKRANI — TFT/LoL augment seçimi gibi 3 büyük kart.
    ///
    /// Kendi arayüzünü ÇALIŞMA ZAMANINDA kurar (Canvas + kartlar). Neden: 3 kart × başlık + açıklama
    /// + etki alanı satırı + rozet = ~20 uGUI nesnesi; bunları editör aracıyla kurup Inspector'dan
    /// tek tek bağlamak hem kırılgan hem bakımı zor olurdu. Böylece kurulum "bileşeni ekle, bitti".
    ///
    /// Akış: davul çalar → panel açılır (oyun girdisi bloklanır) → kart seçilir → panel kapanır,
    /// haritada Kam'ın menzilindeki karolar IŞIKLANIR → tıklanan karoya karo konur.
    /// </summary>
    [DefaultExecutionOrder(-30)]
    public class AugmentDraftHUD : MonoBehaviour
    {
        [SerializeField] private CombatDrumManager _drum;
        [SerializeField] private HexGridManager    _grid;

        [Header("Görünüm")]
        // 4 kart (3 karo + 1 garanti büyü) yan yana sığsın diye kart daraltıldı:
        // 4×330 + 3×40 = 1440 < 1560 satır genişliği.
        [SerializeField] private Vector2 _cardSize = new(330f, 500f);
        [SerializeField] private float   _cardGap  = 40f;
        [SerializeField] private Color   _validCellColor = new(0.30f, 0.90f, 1f, 0.55f);

        // Grup rengi — kartın nadirlik/tür rozeti. Oyuncu bir bakışta "bu artı mı eksi mi" görsün.
        private static readonly Dictionary<AugmentGroup, Color> GroupColor = new()
        {
            { AugmentGroup.Kut,       new Color(0.35f, 0.75f, 0.42f) },
            { AugmentGroup.Kargis,    new Color(0.80f, 0.32f, 0.30f) },
            { AugmentGroup.Notr,      new Color(0.55f, 0.55f, 0.62f) },
            { AugmentGroup.Patlayici, new Color(0.90f, 0.60f, 0.20f) },
            { AugmentGroup.Sinifsal,  new Color(0.62f, 0.42f, 0.85f) },
        };

        private static readonly Dictionary<AugmentGroup, string> GroupName = new()
        {
            { AugmentGroup.Kut,       "KUT · yandaşa" },
            { AugmentGroup.Kargis,    "KARGIŞ · düşmana" },
            { AugmentGroup.Notr,      "NÖTR · herkese" },
            { AugmentGroup.Patlayici, "PATLAYICI" },
            { AugmentGroup.Sinifsal,  "SINIFSAL · nadir" },
        };

        private GameObject _root;
        private RectTransform _cardRow;
        private TextMeshProUGUI _hint;
        private readonly List<GameObject> _cards = new();
        private readonly List<GameObject> _cellMarkers = new();
        private Transform _markerRoot;
        private Material  _markerMat;

        private void Awake()
        {
            BuildUI();
            _markerRoot = new GameObject("AugmentPlacementMarkers").transform;
            _markerRoot.SetParent(transform, false);
            Hide();
        }

        private void OnEnable()
        {
            if (_drum == null) return;
            _drum.OnChoicesOffered += ShowChoices;
            _drum.OnPlacementBegan += ShowPlacement;
            _drum.OnAugmentPlaced  += HandlePlaced;
            _drum.OnDraftClosed    += HandleDraftClosed;   // büyü atıldı → panel kapansın
        }

        private void OnDisable()
        {
            MenuState.IsDraftOpen = false;   // bileşen kapanırsa bayrak asılı kalmasın
            if (_drum == null) return;
            _drum.OnChoicesOffered -= ShowChoices;
            _drum.OnPlacementBegan -= ShowPlacement;
            _drum.OnAugmentPlaced  -= HandlePlaced;
            _drum.OnDraftClosed    -= HandleDraftClosed;
        }

        private void HandleDraftClosed()
        {
            ClearCards();
            ClearMarkers();
            Hide();
        }

        // ── Akış ─────────────────────────────────────────────────────────────

        private void ShowChoices()
        {
            ClearCards();
            ClearMarkers();
            if (_drum == null) return;

            for (int i = 0; i < _drum.Choices.Count; i++)
                _cards.Add(BuildCard(_drum.Choices[i], i));

            _hint.text = "DAVUL ÇALDI — bir karo seç";
            _root.SetActive(true);
            // IMGUI HUD'ları (can barları, sıra barı, "Geri Dön") SUSTUR — IMGUI Canvas'ın üstüne
            // çizdiği için aksi halde kartların içinden geçerler (2026-08-12 ekran görüntüsü).
            MenuState.IsDraftOpen = true;
        }

        private void ShowPlacement()
        {
            _root.SetActive(false);       // panel kapanır, harita görünür olsun
            // Kart seçildi → HUD'lar geri gelir (kullanıcı: "seçtikten sonra geri gelebilir").
            // Yerleştirme sırasında can barlarını görmek zaten gerekli: nereye koyacağına
            // düşmanın canına bakarak karar veriyorsun.
            MenuState.IsDraftOpen = false;
            DrawValidCells();
        }

        private void HandlePlaced(HexCoordinate coord, AugmentCatalog.Entry entry)
        {
            ClearMarkers();
            Hide();
        }

        private void Hide()
        {
            if (_root != null) _root.SetActive(false);
            MenuState.IsDraftOpen = false;
        }

        // ── Kart yapımı ──────────────────────────────────────────────────────

        /// <summary>LoL augment kartı gibi: dış ÇERÇEVE (grup rengi) + iç koyu panel + içerik.
        ///
        /// KRİTİK: karta <see cref="LayoutElement"/> eklenmeli. HorizontalLayoutGroup çocuk boyutunu
        /// "preferred size"tan okur; düz bir RectTransform'un preferred'i 0 olduğu için üç kart da
        /// sıfır genişliğe çöküp ÜST ÜSTE biniyordu (2026-08-12 hata raporu: "yazıları bile
        /// okunmuyor"). sizeDelta tek başına layout grubuna hiçbir şey söylemez.</summary>
        private GameObject BuildCard(CombatDrumManager.DraftCard card, int index)
        {
            // Büyü kartı KENDİ rengini taşır (göstergeyle aynı renk → kartla alan arasındaki bağ
            // gözle kurulur). Karo kartı grup rengini kullanır.
            Color accent = card.IsSkill
                ? new Color(card.Skill.R, card.Skill.G, card.Skill.B)
                : (GroupColor.TryGetValue(card.Tile.Group, out var c) ? c : Color.gray);

            string groupLabel = card.IsSkill
                ? "BÜYÜ · Kam"
                : (GroupName.TryGetValue(card.Tile.Group, out var gn) ? gn : "");

            string id = card.Id;

            // ── Dış çerçeve ──
            var cardGO = new GameObject($"Card_{id}", typeof(RectTransform), typeof(Image),
                                        typeof(Button), typeof(LayoutElement));
            var rt = (RectTransform)cardGO.transform;
            rt.SetParent(_cardRow, false);

            var le = cardGO.GetComponent<LayoutElement>();
            le.preferredWidth  = _cardSize.x;
            le.preferredHeight = _cardSize.y;
            le.flexibleWidth   = 0f;
            le.flexibleHeight  = 0f;
            le.minWidth        = _cardSize.x;
            le.minHeight       = _cardSize.y;

            cardGO.GetComponent<Image>().color = accent;        // çerçevenin kendisi

            // ── İç panel (çerçeveden 5px içeri) ──
            var innerGO = new GameObject("Inner", typeof(RectTransform), typeof(Image));
            var inner = (RectTransform)innerGO.transform;
            inner.SetParent(rt, false);
            inner.anchorMin = Vector2.zero; inner.anchorMax = Vector2.one;
            inner.offsetMin = new Vector2(5f, 5f); inner.offsetMax = new Vector2(-5f, -5f);
            innerGO.GetComponent<Image>().color = new Color(0.07f, 0.07f, 0.10f, 0.98f);
            innerGO.GetComponent<Image>().raycastTarget = false;

            // ── Üst şerit: grup adı ──
            var strip = NewRect("Strip", inner, new Vector2(0.5f, 1f), Vector2.zero,
                                new Vector2(_cardSize.x - 10f, 52f));
            var stripImg = strip.gameObject.AddComponent<Image>();
            stripImg.color = accent;
            stripImg.raycastTarget = false;
            Label(strip, "GroupName", groupLabel,
                  new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(_cardSize.x - 20f, 48f),
                  new Color(0.05f, 0.05f, 0.07f), 21f, FontStyles.Bold);

            // ── Başlık ──
            Label(inner, "Name", card.Name, new Vector2(0.5f, 1f), new Vector2(0f, -64f),
                  new Vector2(_cardSize.x - 28f, 64f), new Color(0.97f, 0.94f, 0.84f), 32f, FontStyles.Bold);

            // ── Ayraç ──
            var sep = NewRect("Sep", inner, new Vector2(0.5f, 1f), new Vector2(0f, -132f),
                              new Vector2(_cardSize.x - 60f, 2f));
            var sepImg = sep.gameObject.AddComponent<Image>();
            sepImg.color = new Color(accent.r, accent.g, accent.b, 0.5f);
            sepImg.raycastTarget = false;

            // ── Açıklama ──
            var desc = Label(inner, "Desc", card.Description, new Vector2(0.5f, 1f), new Vector2(0f, -150f),
                             new Vector2(_cardSize.x - 44f, 200f), new Color(0.86f, 0.84f, 0.80f), 23f);
            desc.alignment = TextAlignmentOptions.Top;

            // ── ETKİ ALANI (kullanıcı isteği: yarıçap kartın ALTINDA yazsın) ──
            var areaBg = NewRect("AreaBg", inner, new Vector2(0.5f, 0f), new Vector2(0f, 62f),
                                 new Vector2(_cardSize.x - 40f, 46f));
            var areaImg = areaBg.gameObject.AddComponent<Image>();
            areaImg.color = new Color(accent.r * 0.30f, accent.g * 0.30f, accent.b * 0.30f, 0.85f);
            areaImg.raycastTarget = false;
            Label(areaBg, "Area", card.AreaLabel, new Vector2(0.5f, 0.5f), Vector2.zero,
                  new Vector2(_cardSize.x - 48f, 42f), accent, 20f, FontStyles.Bold);

            // Alt satır kartın TÜRÜNÜ söyler: karo yerleştirilir, büyü hedeflenip atılır.
            Label(inner, "Pick", card.IsSkill ? "SEÇ → hedefle, ÇİFT TIK" : "SEÇ → Kam'ın çevresine koy",
                  new Vector2(0.5f, 0f), new Vector2(0f, 22f),
                  new Vector2(_cardSize.x - 28f, 36f), new Color(0.60f, 0.58f, 0.54f), 19f);

            int captured = index;
            cardGO.GetComponent<Button>().onClick.AddListener(() => OnCardClicked(captured));
            return cardGO;
        }

        private void OnCardClicked(int index)
        {
            if (_drum == null) return;

            bool isSkill = index >= 0 && index < _drum.Choices.Count && _drum.Choices[index].IsSkill;

            if (!_drum.Choose(index))
            {
                _hint.text = isSkill
                    ? "Büyü sistemi bağlı değil — TacticalRPG/Savas kurulumunu çalıştır."
                    : "Kam'ın çevresinde uygun karo yok — Kam'ı hareket ettirip tekrar dene.";
                return;
            }

            // Büyü seçildi → panel kapanır, harita görünür olur (hedefleme oradadır).
            // Karo seçiminde bunu ShowPlacement yapıyor; büyünün kendi yolu var.
            if (isSkill)
            {
                _root.SetActive(false);
                MenuState.IsDraftOpen = false;
                ClearMarkers();
            }
        }

        // ── Harita üstü geçerli karo işaretleri ──────────────────────────────

        /// <summary>Kam'ın menzilindeki uygun karoları ışıklandırır. Oyuncu "nereye koyabilirim"i
        /// tahmin etmek zorunda kalmamalı — yarıçap kuralı ancak GÖRÜNÜRSE taktik karar olur.</summary>
        private void DrawValidCells()
        {
            ClearMarkers();
            if (_drum == null || _grid == null) return;

            if (_markerMat == null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                _markerMat = new Material(sh);
                if (_markerMat.HasProperty("_BaseColor")) _markerMat.SetColor("_BaseColor", _validCellColor);
                if (_markerMat.HasProperty("_Color"))     _markerMat.SetColor("_Color", _validCellColor);
            }

            foreach (var coord in _drum.ValidCells)
            {
                if (!_grid.TryGetCell(coord, out HexCell cell)) continue;
                var m = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(m.GetComponent<Collider>());
                m.transform.SetParent(_markerRoot, false);
                m.transform.position   = cell.WorldPosition + Vector3.up * (cell.SurfaceHeight + 0.06f);
                m.transform.rotation   = Quaternion.Euler(90f, 0f, 0f);
                m.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
                m.GetComponent<Renderer>().sharedMaterial = _markerMat;
                _cellMarkers.Add(m);
            }
        }

        private void ClearMarkers()
        {
            foreach (var m in _cellMarkers) if (m != null) Destroy(m);
            _cellMarkers.Clear();
        }

        private void ClearCards()
        {
            foreach (var c in _cards) if (c != null) Destroy(c);
            _cards.Clear();
        }

        // ── uGUI iskeleti ────────────────────────────────────────────────────

        private void BuildUI()
        {
            _root = new GameObject("AugmentDraftCanvas", typeof(Canvas), typeof(CanvasScaler),
                                   typeof(GraphicRaycaster));
            _root.transform.SetParent(transform, false);

            var canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;                       // savaş HUD'larının üstünde

            var scaler = _root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight  = 0.5f;

            // Karartma perdesi — panel açıkken harita tıklaması geçmesin
            var dim = NewRect("Dim", (RectTransform)_root.transform, new Vector2(0.5f, 0.5f),
                              Vector2.zero, Vector2.zero);
            dim.anchorMin = Vector2.zero; dim.anchorMax = Vector2.one;
            dim.offsetMin = Vector2.zero; dim.offsetMax = Vector2.zero;
            dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);

            _hint = Label((RectTransform)_root.transform, "Hint", "", new Vector2(0.5f, 1f),
                          new Vector2(0f, -90f), new Vector2(1400f, 70f),
                          new Color(0.95f, 0.88f, 0.60f), 48f, FontStyles.Bold);

            _cardRow = NewRect("CardRow", (RectTransform)_root.transform, new Vector2(0.5f, 0.5f),
                               new Vector2(0f, -30f), new Vector2(1500f, _cardSize.y));
            var layout = _cardRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = _cardGap;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        private static RectTransform NewRect(string name, RectTransform parent, Vector2 anchor,
                                             Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, anchor.y);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        private static TextMeshProUGUI Label(RectTransform parent, string name, string text,
                                             Vector2 anchor, Vector2 pos, Vector2 size,
                                             Color color, float fontSize,
                                             FontStyles style = FontStyles.Normal)
        {
            var rt = NewRect(name, parent, anchor, pos, size);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text      = text;
            tmp.color     = color;
            tmp.fontSize  = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;                        // tıklama karta gitsin, yazıya değil
            return tmp;
        }
    }
}
