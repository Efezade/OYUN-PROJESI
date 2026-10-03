using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using TacticalRPG.Core;
using TacticalRPG.Data;

namespace TacticalRPG.UI
{
    /// <summary>
    /// ÇANTA'NIN DONANIM SAYFASI — giydirme bebeği (Efe'nin isteği 2026-10-03):
    ///   • solda KARAKTERLER — tıklanan karakter seçilir (KAM sayfasında liste yok, hep Kam);
    ///   • ortada seçili karakterin BÜSTÜ + adı + tam boy SİLÜETİ ve çevresinde altı VÜCUT
    ///     BÖLGESİ yuvası: kafa · boyun · gövde · sağ kol (silah) · sol kol (kalkan) · ayak;
    ///   • sağda ÇANTA (sahip olunan eşyalar) + künye.
    ///
    /// Eşya çantadan SÜRÜKLENİP kendi bölgesinin yuvasına bırakılınca takılır — sürükleme
    /// başlayınca UYGUN yuva yeşile döner, diğerleri söner (kafaya kılıç takılmaz). Dolu yuvaya
    /// bırakmak yer değiştirir; yuvadan çantaya sürüklemek ya da sağ tık çıkarır.
    ///
    /// Aynı sınıf iki sayfada çalışır: EŞYALAR (<see cref="_commanderOnly"/> kapalı — Kam dışı
    /// birlik) ve KAM (açık — yalnız komutan). Kurallar <see cref="Inventory"/>'de; burası gösterir.
    /// Yuvalar ve kapsayıcılar sahnede kurulur, liste satırları/simgeler çalışma zamanında üretilir.
    /// </summary>
    public class BagInventoryView : MonoBehaviour
    {
        /// <summary>Bir vücut bölgesi yuvasının sahnedeki parçaları.</summary>
        [System.Serializable]
        public class SlotView
        {
            [SerializeField] private EquipSlot       _region;
            [SerializeField] private Image           _background;
            [SerializeField] private RectTransform   _iconHolder;
            [SerializeField] private TextMeshProUGUI _label;

            public EquipSlot       Region     => _region;
            public Image           Background => _background;
            public RectTransform   IconHolder => _iconHolder;
            public TextMeshProUGUI Label      => _label;
        }

        [Header("Bağımlılıklar")]
        [SerializeField] private Inventory    _inventory;
        [SerializeField] private PartyManager _party;
        [Tooltip("Açık = KAM sayfası: karakter listesi yok, hep komutan gösterilir.")]
        [SerializeField] private bool _commanderOnly;

        [Header("Karakter (orta)")]
        [Tooltip("Karakter seçme listesi (KAM sayfasında boş bırakılır).")]
        [SerializeField] private RectTransform   _rosterContent;
        [SerializeField] private Image           _portrait;
        [SerializeField] private TextMeshProUGUI _nameLabel;
        [SerializeField] private TextMeshProUGUI _statsLabel;
        [SerializeField] private SlotView[]      _slots;

        [Header("Çanta (sağ)")]
        [SerializeField] private RectTransform   _bagContent;
        [Tooltip("Çanta alanı — buraya bırakılan yuva eşyası çantaya döner.")]
        [SerializeField] private RectTransform   _bagDropArea;
        [SerializeField] private TextMeshProUGUI _detailLabel;
        [Tooltip("Sürüklerken fareyi izleyen hayalet simge (raycast almaz).")]
        [SerializeField] private Image           _ghost;
        [SerializeField] private Sprite          _cellSprite;

        [Header("Renk")]
        [SerializeField] private Color _ink        = new(0.13f, 0.10f, 0.07f);
        [SerializeField] private Color _inkSoft    = new(0.36f, 0.29f, 0.20f);
        [SerializeField] private Color _paper      = new(0.93f, 0.88f, 0.76f);
        [SerializeField] private Color _slotEmpty  = new(0.80f, 0.74f, 0.62f, 0.85f);
        [SerializeField] private Color _slotValid  = new(0.45f, 0.85f, 0.45f, 0.95f);
        [SerializeField] private Color _slotDim    = new(0.55f, 0.50f, 0.42f, 0.45f);
        [SerializeField] private Color _selectedRow = new(0.96f, 0.82f, 0.45f, 0.85f);
        [SerializeField] private Color _common     = new(0.80f, 0.78f, 0.72f);
        [SerializeField] private Color _rare       = new(0.45f, 0.65f, 0.95f);
        [SerializeField] private Color _epic       = new(0.72f, 0.48f, 0.92f);

        private CharacterCard  _selected;
        private ItemDragHandle _dragging;
        private bool _dirty;

        private void Awake()
        {
            if (_bagDropArea != null)
            {
                // '??' KULLANILMAZ: editörde eksik bileşen "sahte null" döner, ?? onu null saymaz.
                var t = _bagDropArea.GetComponent<ItemDropTarget>();
                if (t == null) t = _bagDropArea.gameObject.AddComponent<ItemDropTarget>();
                t.View = this; t.Card = null; t.Slot = -1;
            }

            if (_slots != null)
                foreach (SlotView s in _slots)
                {
                    if (s?.Background == null) continue;
                    var target = s.Background.GetComponent<ItemDropTarget>();
                    if (target == null) target = s.Background.gameObject.AddComponent<ItemDropTarget>();
                    target.View = this;
                    target.Slot = EquipSlots.IndexOf(s.Region);
                }

            if (_ghost != null) { _ghost.raycastTarget = false; _ghost.gameObject.SetActive(false); }
        }

        private void OnEnable()
        {
            if (_inventory != null) _inventory.OnChanged   += MarkDirty;
            if (_party     != null) _party.OnRosterChanged += MarkDirty;
            Rebuild();
        }

        private void OnDisable()
        {
            if (_inventory != null) _inventory.OnChanged   -= MarkDirty;
            if (_party     != null) _party.OnRosterChanged -= MarkDirty;
            EndDrag();
        }

        private void MarkDirty() => _dirty = true;

        // Yeniden kurulum olay ANINDA değil: bırakma olayının içinde sürüklenen nesneyi yok etmek
        // EndDrag'i ölü nesneye gönderirdi.
        private void LateUpdate()
        {
            if (!_dirty || _dragging != null) return;
            _dirty = false;
            Rebuild();
        }

        // ── Sürükleme ────────────────────────────────────────────────────────

        public void BeginDrag(ItemDragHandle handle, PointerEventData e)
        {
            _dragging = handle;
            Select(handle.Item);
            HighlightSlots(handle.Item);
            if (_ghost == null) return;
            _ghost.sprite = handle.Item != null ? handle.Item.Icon : null;
            _ghost.color  = _ink;
            _ghost.gameObject.SetActive(true);
            _ghost.transform.SetAsLastSibling();
            Drag(e);
        }

        public void Drag(PointerEventData e)
        {
            if (_ghost == null || !_ghost.gameObject.activeSelf) return;
            var parent = (RectTransform)_ghost.transform.parent;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, e.position, e.pressEventCamera, out Vector3 w))
                _ghost.transform.position = w;
        }

        public void EndDrag()
        {
            bool was = _dragging != null;
            _dragging = null;
            if (_ghost != null) _ghost.gameObject.SetActive(false);
            if (was) RefreshSlots();          // vurgu renkleri normale dönsün
        }

        /// <summary>Bırakma kuralı: yuvaya → tak (ya da yer değiştir); çantaya → çıkar.</summary>
        public void Drop(ItemDropTarget target, ItemDragHandle handle)
        {
            if (_inventory == null || handle == null || handle.Item == null) return;

            if (target.Slot < 0)                                      // çanta alanı
            {
                if (handle.FromCard != null) _inventory.Unequip(handle.FromCard, handle.FromSlot);
                return;
            }

            CharacterCard card = _selected;
            if (card == null) return;
            if (!Inventory.Fits(handle.Item, target.Slot))
            {
                if (_detailLabel != null)
                    _detailLabel.text = $"<b>{handle.Item.DisplayName}</b> yalnız " +
                                        $"<b>{EquipSlots.Label(handle.Item.Slot)}</b> yuvasına takılır.";
                return;
            }
            _inventory.Equip(handle.Item, card, target.Slot, handle.FromCard, handle.FromSlot);
        }

        public void QuickUnequip(ItemDragHandle handle)
        {
            if (_inventory != null && handle.FromCard != null) _inventory.Unequip(handle.FromCard, handle.FromSlot);
        }

        public void Select(ItemSO item)
        {
            if (_detailLabel == null) return;
            _detailLabel.text = item == null
                ? "Bir eşyaya tıkla: etkisi burada yazar. Eşyayı karakterin uygun bölgesine SÜRÜKLE; " +
                  "yuvadan çantaya sürükle (ya da sağ tık) çıkar."
                : $"<b>{item.DisplayName}</b>  <i>{RarityName(item.Rarity)} · {EquipSlots.Label(item.Slot)}</i>\n" +
                  $"{item.EffectText()}\n<size=85%>{item.Description}</size>";
        }

        private void SelectCharacter(CharacterCard card)
        {
            _selected = card;
            Rebuild();
        }

        // ── Kurulum ──────────────────────────────────────────────────────────

        private void Rebuild()
        {
            EnsureSelection();
            BuildRoster();
            RefreshCharacter();
            RefreshSlots();
            BuildBag();
            if (_detailLabel != null && string.IsNullOrEmpty(_detailLabel.text)) Select(null);
        }

        /// <summary>Seçim geçerli değilse ilk uygun karaktere düş (KAM sayfasında hep Kam).</summary>
        private void EnsureSelection()
        {
            if (_party == null) { _selected = null; return; }
            bool valid = false;
            foreach (CharacterCard c in _party.Party)
                if (c == _selected && c != null && c.IsCommander == _commanderOnly) { valid = true; break; }
            if (valid) return;

            _selected = null;
            foreach (CharacterCard c in _party.Party)
                if (c != null && c.IsCommander == _commanderOnly) { _selected = c; break; }
        }

        private void BuildRoster()
        {
            if (_rosterContent == null) return;
            Clear(_rosterContent);
            if (_party == null) return;

            int index = 0;
            foreach (CharacterCard card in _party.Party)
            {
                if (card == null || card.IsCommander) continue;     // Kam'ın kendi sayfası var
                index++;
                var row = Rect("Row", _rosterContent, new Vector2(230f, 74f));
                var bg = row.gameObject.AddComponent<Image>();
                bg.sprite = _cellSprite; bg.type = Image.Type.Sliced;
                bg.color = card == _selected ? _selectedRow : new Color(_paper.r, _paper.g, _paper.b, 0.6f);
                var btn = row.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                CharacterCard captured = card;
                btn.onClick.AddListener(() => SelectCharacter(captured));

                var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                h.spacing = 8f; h.padding = new RectOffset(6, 6, 5, 5);
                h.childAlignment = TextAnchor.MiddleLeft; h.childForceExpandWidth = false; h.childForceExpandHeight = false;

                AddBust(row, card, 62f);
                int worn = 0;
                for (int s = 0; s < Inventory.SlotsPerCharacter; s++) if (_inventory != null && _inventory.EquippedAt(card, s) != null) worn++;
                Label(row, "Name", $"{ClassName(card)}\n<size=70%>Sv{card.Level} · #{index} · {worn}/{Inventory.SlotsPerCharacter} eşya</size>",
                      19f, _ink, new Vector2(150f, 64f), TextAlignmentOptions.Left);
            }
            if (index == 0)
                Label(_rosterContent, "Empty", "Henüz birim yok — savaş öncesi üret.", 16f, _inkSoft, new Vector2(220f, 60f));
        }

        private void RefreshCharacter()
        {
            if (_portrait != null)
            {
                Sprite p = _selected != null && _selected.Data != null ? _selected.Data.Portrait : null;
                _portrait.sprite  = p;
                _portrait.enabled = p != null;
                _portrait.color   = _ink;
            }

            if (_nameLabel != null)
                _nameLabel.text = _selected == null
                    ? (_commanderOnly ? "Kam bulunamadı" : "Karakter seç")
                    : $"{ClassName(_selected).ToUpperInvariant()}  <size=60%>Sv{_selected.Level}</size>";

            if (_statsLabel != null)
            {
                if (_selected == null) { _statsLabel.text = ""; return; }
                string items = _inventory != null ? _inventory.TraitsFor(_selected).Describe() : "";
                _statsLabel.text =
                    $"CAN {_selected.MaxHP} · SALDIRI {_selected.Attack} · SAVUNMA {_selected.Defense} · " +
                    $"HAREKET {_selected.MoveRange} · MENZİL {_selected.AttackRange}\n" +
                    $"<b>EŞYALARDAN:</b> {(string.IsNullOrEmpty(items) ? "—" : items)}";
            }
        }

        /// <summary>Altı bölge yuvasını seçili karaktere göre doldurur.</summary>
        private void RefreshSlots()
        {
            if (_slots == null) return;
            foreach (SlotView s in _slots)
            {
                if (s == null) continue;
                int index = EquipSlots.IndexOf(s.Region);

                var target = s.Background != null ? s.Background.GetComponent<ItemDropTarget>() : null;
                if (target != null) target.Card = _selected;

                if (s.Label != null) s.Label.text = SlotCaption(s.Region);
                if (s.IconHolder != null) Clear(s.IconHolder);

                ItemSO item = _selected != null && _inventory != null ? _inventory.EquippedAt(_selected, index) : null;
                if (s.Background != null) s.Background.color = item != null ? RarityColor(item.Rarity) : _slotEmpty;
                if (item != null && s.IconHolder != null) AddIcon(s.IconHolder, item, _selected, index, 64f);
            }
        }

        /// <summary>Sürükleme sırasında: eşyanın bölgesine UYAN yuva yeşil, diğerleri sönük.</summary>
        private void HighlightSlots(ItemSO item)
        {
            if (_slots == null || item == null) return;
            foreach (SlotView s in _slots)
                if (s?.Background != null)
                    s.Background.color = s.Region == item.Slot ? _slotValid : _slotDim;
        }

        private void BuildBag()
        {
            if (_bagContent == null) return;
            Clear(_bagContent);
            if (_inventory == null) return;

            foreach (ItemSO item in _inventory.BagItems)
            {
                if (item == null) continue;
                var cell = Rect("Item", _bagContent, new Vector2(84f, 84f));
                var bg = cell.gameObject.AddComponent<Image>();
                bg.sprite = _cellSprite; bg.type = Image.Type.Sliced; bg.color = RarityColor(item.Rarity);
                AddIcon(cell, item, null, -1, 56f);
                // Bölge etiketi — hangi yuvaya gideceği sürüklemeden okunsun.
                var tag = Label(cell, "Slot", EquipSlots.Label(item.Slot), 11f, _ink, new Vector2(82f, 16f));
                var trt = (RectTransform)tag.transform;
                trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0f);
                trt.pivot = new Vector2(0.5f, 0f);
                trt.anchoredPosition = new Vector2(0f, 2f);
                tag.GetComponent<LayoutElement>().ignoreLayout = true;
            }
            if (_inventory.BagItems.Count == 0)
                Label(_bagContent, "Empty", "Çanta boş — marketten eşya al.", 16f, _inkSoft, new Vector2(240f, 40f));
        }

        /// <summary>Eşya simgesi + sürükleme tutamağı (merkezlenmiş, layout dışı).</summary>
        private void AddIcon(RectTransform parent, ItemSO item, CharacterCard fromCard, int fromSlot, float size)
        {
            var go = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 4f);
            rt.sizeDelta = new Vector2(size, size);
            var img = go.GetComponent<Image>();
            img.sprite = item.Icon; img.color = _ink; img.preserveAspect = true;
            if (item.Icon == null) img.color = new Color(_ink.r, _ink.g, _ink.b, 0.35f);

            var handle = go.AddComponent<ItemDragHandle>();
            handle.View = this; handle.Item = item; handle.FromCard = fromCard; handle.FromSlot = fromSlot;
        }

        private void AddBust(RectTransform parent, CharacterCard card, float size)
        {
            var box = Rect("Bust", parent, new Vector2(size, size));
            box.gameObject.AddComponent<Image>().color = _paper;
            Sprite portrait = card.Data != null ? card.Data.Portrait : null;
            if (portrait == null) return;
            var go = new GameObject("Img", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(box, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size - 6f, size - 6f);
            var img = go.GetComponent<Image>();
            img.sprite = portrait; img.color = _ink; img.preserveAspect = true; img.raycastTarget = false;
        }

        // ── Yardımcılar ──────────────────────────────────────────────────────

        private static string ClassName(CharacterCard c) => c != null && c.Data != null ? c.Data.ClassName : "?";

        private static string SlotCaption(EquipSlot s) => s switch
        {
            EquipSlot.SagKol => "SAĞ KOL · silah",
            EquipSlot.SolKol => "SOL KOL · kalkan",
            _                => EquipSlots.Label(s)
        };

        private static void Clear(RectTransform root)
        {
            if (root == null) return;
            for (int i = root.childCount - 1; i >= 0; i--) Destroy(root.GetChild(i).gameObject);
        }

        private static RectTransform Rect(string name, RectTransform parent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = size;
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = size.x; le.preferredHeight = size.y;
            return rt;
        }

        private TextMeshProUGUI Label(RectTransform parent, string name, string text, float size, Color color,
                                      Vector2 box, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var rt = Rect(name, parent, box);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
            t.raycastTarget = false; t.richText = true;
            return t;
        }

        private Color RarityColor(ItemRarity r) => r switch
        {
            ItemRarity.Nadir    => _rare,
            ItemRarity.Destansi => _epic,
            _                   => _common
        };

        private static string RarityName(ItemRarity r) => r switch
        {
            ItemRarity.Nadir    => "Nadir",
            ItemRarity.Destansi => "Destansı",
            _                   => "Sıradan"
        };
    }
}
