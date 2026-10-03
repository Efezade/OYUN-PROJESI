using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TacticalRPG.Core;
using TacticalRPG.Data;

namespace TacticalRPG.UI
{
    /// <summary>
    /// ÇANTA → POTLAR sayfası (Efe'nin isteği 2026-10-03): eldeki potlar (adet + etki + süre) ve
    /// her birinin yanında İÇ düğmesi; sağda ŞU AN ETKİN potlar ve kalan süreleri.
    ///
    /// İçmek: <see cref="Inventory.TryConsumePotion"/> → <see cref="PlayerBuffs.ApplyPotion"/>.
    /// Savaş potu sonraki savaşa yazılır; ana harita potu adım saymaya başlar. Satırlar çalışma
    /// zamanında üretilir (içerik değişken); kapsayıcılar sahnede kurulur (SceneSetupTool.BagMap).
    /// </summary>
    public class BagPotionView : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private Inventory   _inventory;
        [SerializeField] private PlayerBuffs _buffs;

        [Header("Kapsayıcılar (sahnede kurulur)")]
        [SerializeField] private RectTransform   _content;
        [SerializeField] private TextMeshProUGUI _activeLabel;
        [SerializeField] private Sprite          _cellSprite;
        [SerializeField] private Sprite          _potSprite;

        [Header("Renk")]
        [SerializeField] private Color _ink     = new(0.13f, 0.10f, 0.07f);
        [SerializeField] private Color _inkSoft = new(0.36f, 0.29f, 0.20f);
        [SerializeField] private Color _paper   = new(0.93f, 0.88f, 0.76f);

        private readonly List<string> _active = new();
        private bool _dirty;

        private void OnEnable()
        {
            if (_inventory != null) _inventory.OnChanged      += MarkDirty;
            if (_buffs     != null) _buffs.OnPotionsChanged   += MarkDirty;
            Rebuild();
        }

        private void OnDisable()
        {
            if (_inventory != null) _inventory.OnChanged      -= MarkDirty;
            if (_buffs     != null) _buffs.OnPotionsChanged   -= MarkDirty;
        }

        private void MarkDirty() => _dirty = true;

        private void LateUpdate()
        {
            if (!_dirty) return;
            _dirty = false;
            Rebuild();
        }

        private void Drink(PotionSO potion)
        {
            if (_inventory == null || _buffs == null) return;
            if (_inventory.TryConsumePotion(potion)) _buffs.ApplyPotion(potion);
        }

        private void Rebuild()
        {
            if (_content != null)
            {
                for (int i = _content.childCount - 1; i >= 0; i--) Destroy(_content.GetChild(i).gameObject);

                bool any = false;
                if (_inventory != null)
                    foreach (var kv in _inventory.Potions)
                    {
                        if (kv.Key == null || kv.Value <= 0) continue;
                        BuildRow(kv.Key, kv.Value);
                        any = true;
                    }
                if (!any) Label(_content, "Empty", "Çantada pot yok — marketten al.", 20f, _inkSoft, new Vector2(740f, 50f));
            }

            if (_activeLabel != null)
            {
                if (_buffs != null) _buffs.FillActivePotions(_active); else _active.Clear();
                _activeLabel.text = "ETKİN POTLAR\n" + (_active.Count == 0 ? "<i>—</i>" : "• " + string.Join("\n• ", _active));
            }
        }

        private void BuildRow(PotionSO potion, int count)
        {
            var row = Rect("Row", _content, new Vector2(740f, 82f));
            var bg = row.gameObject.AddComponent<Image>();
            bg.sprite = _cellSprite; bg.type = Image.Type.Sliced; bg.color = new Color(_paper.r, _paper.g, _paper.b, 0.6f);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 14f; h.padding = new RectOffset(10, 10, 6, 6);
            h.childAlignment = TextAnchor.MiddleLeft; h.childForceExpandWidth = false; h.childForceExpandHeight = false;

            var icon = Rect("Icon", row, new Vector2(62f, 62f));
            var img = icon.gameObject.AddComponent<Image>();
            img.sprite = _potSprite != null ? _potSprite : potion.Icon;
            img.color = potion.Color; img.preserveAspect = true; img.raycastTarget = false;

            Label(row, "Text", $"<b>{potion.DisplayName}</b>  ×{count}\n<size=75%>{potion.Description}  ·  {potion.DurationText}</size>",
                  20f, _ink, new Vector2(520f, 72f), TextAlignmentOptions.Left);

            var btnRT = Rect("Drink", row, new Vector2(100f, 56f));
            var btnImg = btnRT.gameObject.AddComponent<Image>();
            btnImg.sprite = _cellSprite; btnImg.type = Image.Type.Sliced; btnImg.color = new Color(0.55f, 0.75f, 0.45f);
            var btn = btnRT.gameObject.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.onClick.AddListener(() => Drink(potion));
            Label(btnRT, "L", "İÇ", 24f, _ink, new Vector2(96f, 52f));
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

        private static TextMeshProUGUI Label(RectTransform parent, string name, string text, float size, Color color,
                                             Vector2 box, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var rt = Rect(name, parent, box);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
            t.raycastTarget = false; t.richText = true;
            return t;
        }
    }
}
