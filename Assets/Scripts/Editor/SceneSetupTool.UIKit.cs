using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

namespace TacticalRPG.Editor
{
    /// <summary>
    /// Menü ekranları için ORTAK "parşömen UI kiti" (game UI.pdf estetiği: el çizimi mürekkep + krem kâğıt).
    /// Yuvarlak köşeli panel/çerçeve/madalyon yardımcıları — Unity'nin built-in 9-slice sprite'larıyla
    /// (Background.psd / Knob.psd) düz keskin dikdörtgenler yerine yumuşak, kitap/valiz/harita hissi verir.
    /// KİTAP/ÇANTA/HARİTA populate metodları bu kiti kullanır (görsel katman; veri bağlama değişmez).
    /// </summary>
    public static partial class SceneSetupTool
    {
        // ── Palet (krem kâğıt + koyu mürekkep) ────────────────────────────────
        private static readonly Color Parchment   = new(0.91f, 0.85f, 0.70f); // kâğıt yüzeyi
        private static readonly Color ParchmentHi = new(0.96f, 0.92f, 0.80f); // açık vurgu
        private static readonly Color ParchmentLo = new(0.83f, 0.75f, 0.58f); // koyu kâğıt (yuva/gölge)
        private static readonly Color Ink         = new(0.20f, 0.16f, 0.11f); // koyu metin/çizgi
        private static readonly Color InkSoft     = new(0.36f, 0.29f, 0.20f); // ikincil metin
        private static readonly Color FrameDark   = new(0.28f, 0.21f, 0.13f); // çerçeve mürekkebi

        // ── Built-in sprite'lar (yuvarlak 9-slice panel + dolu daire) ─────────
        private static Sprite _roundSprite, _circleSprite;
        private static Sprite RoundSprite =>
            _roundSprite != null ? _roundSprite
            : (_roundSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"));
        private static Sprite CircleSprite =>
            _circleSprite != null ? _circleSprite
            : (_circleSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"));

        /// <summary>Yuvarlak köşeli (9-slice) panel/kutu.</summary>
        private static Image Sliced(Transform parent, string name, Vector2 anchor,
            Vector2 pos, Vector2 size, Color color, bool raycast = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.sprite = RoundSprite; img.type = Image.Type.Sliced;
            img.color = color; img.raycastTarget = raycast;
            return img;
        }

        /// <summary>Dolu daire (madalyon/pin/pusula/öz kabı).</summary>
        private static Image Circle(Transform parent, string name, Vector2 anchor,
            Vector2 pos, float diam, Color color, bool raycast = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(diam, diam);
            var img = go.GetComponent<Image>();
            img.sprite = CircleSprite; img.type = Image.Type.Simple;
            img.color = color; img.raycastTarget = raycast;
            return img;
        }

        /// <summary>Çerçeveli parşömen panel: koyu mürekkep dış çerçeve + krem iç dolgu. İÇERİK EBEVEYNİ (Fill) döner.</summary>
        private static RectTransform FramedPanel(Transform parent, string name, Vector2 anchor,
            Vector2 pos, Vector2 size, float pad, bool raycast = false)
            => FramedPanel(parent, name, anchor, pos, size, pad, Parchment, FrameDark, raycast);

        private static RectTransform FramedPanel(Transform parent, string name, Vector2 anchor,
            Vector2 pos, Vector2 size, float pad, Color fill, Color frame, bool raycast = false)
        {
            Image f = Sliced(parent, name, anchor, pos, size, frame, raycast);
            var fo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fo.transform.SetParent(f.transform, false);
            var rt = fo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(pad, pad); rt.offsetMax = new Vector2(-pad, -pad);
            var img = fo.GetComponent<Image>();
            img.sprite = RoundSprite; img.type = Image.Type.Sliced;
            img.color = fill; img.raycastTarget = raycast;
            return rt;
        }

        /// <summary>İnce çizgi (ayraç/spine/kenar). Dikey için size=(kalınlık, uzunluk).</summary>
        private static Image Line(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
            => Sliced(parent, name, anchor, pos, size, color);

        // ── EL ÇİZİMİ MÜREKKEP KATMANI (2026-09-04) ──────────────────────────
        // game UI.pdf'in dili: krem kâğıt + dalgalı mürekkep kontur + ikonlu daireler. Aşağıdaki
        // üç yardımcı, çizimi InkArtFactory'den alır. ESKİ yardımcılar (Sliced/FramedPanel)
        // DURUYOR: HARİTA ekranı onları kullanıyor ve minimap'e dokunulmayacak (Efe, 2026-09-04).

        /// <summary>Verilen sprite'ı taşıyan basit Image (ikon/dal/daire).</summary>
        private static Image InkImage(Transform parent, string name, Sprite sprite, Vector2 anchor,
            Vector2 pos, Vector2 size, Color color, bool raycast = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos; rt.sizeDelta = size;

            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.type   = Image.Type.Simple;
            img.color  = color;
            img.raycastTarget = raycast;
            return img;
        }

        /// <summary>
        /// El çizimi panel: kâğıt zemin + TAM O ÖLÇÜDE üretilmiş dalgalı mürekkep çerçeve.
        /// Çerçeve 9-slice ile GERİLMEZ — gerilen dalgalı çizgi "yayılmış" görünüyordu; her ölçü
        /// için kendi PNG'si üretilir (dosyalar ölçüye göre önbelleklenir).
        /// İÇERİK EBEVEYNİ olarak panelin kendi RectTransform'u döner.
        /// </summary>
        private static RectTransform InkPanel(Transform parent, string name, Vector2 anchor,
            Vector2 pos, Vector2 size, int radius, float alpha = 1f)
        {
            int w = Mathf.Max(32, Mathf.RoundToInt(size.x));
            int h = Mathf.Max(32, Mathf.RoundToInt(size.y));

            Image paper = InkImage(parent, name, InkArtFactory.Paper("paper_soft", 96, 96, Color.white),
                                   anchor, pos, size,
                                   new Color(ParchmentHi.r, ParchmentHi.g, ParchmentHi.b, alpha), raycast: true);
            paper.type = Image.Type.Sliced;

            // KÖŞE SÜSLERİ YALNIZ BÜYÜK PANELLERDE: küçük kart/düğme/rozetlerde süsler içeri taşıp
            // yazının üstünden geçiyordu (YÜKSELT düğmesinin üstünde çarpı gibi duruyordu).
            bool flourish = w >= 460 && h >= 400;
            Image frame = InkImage(paper.transform, "InkFrame",
                                   InkArtFactory.Frame($"frame_{w}x{h}_r{radius}", w, h, radius,
                                                       flourish: flourish),
                                   new Vector2(0.5f, 0.5f), Vector2.zero, size, InkArtFactory.Ink);
            frame.raycastTarget = false;

            return paper.rectTransform;
        }

        /// <summary>El çizimi düğme: kâğıt + çerçeve + mürekkep etiket.</summary>
        private static Button InkButton(Transform parent, string name, string label,
            Vector2 anchor, Vector2 pos, Vector2 size, float fontSize = 26f)
        {
            RectTransform panel = InkPanel(parent, name, anchor, pos, size, 16);

            var btn = panel.gameObject.AddComponent<Button>();
            btn.targetGraphic = panel.GetComponent<Image>();
            ColorBlock cb = btn.colors;
            cb.normalColor      = Color.white;
            cb.highlightedColor = new Color(1.06f, 1.04f, 1.00f);
            cb.pressedColor     = new Color(0.86f, 0.82f, 0.74f);
            cb.disabledColor    = new Color(0.80f, 0.78f, 0.74f, 0.55f);
            cb.fadeDuration     = 0.08f;
            btn.colors = cb;

            CreateCenteredLabel(panel, "Label", label, new Vector2(0.5f, 0.5f), Vector2.zero,
                                new Vector2(size.x - 24f, size.y - 18f), Ink, fontSize);
            return btn;
        }

        /// <summary>Süslü bölüm başlığı: krem şerit + iki yan çizgi + koyu mürekkep metin. Ortalanmış.</summary>
        private static void SectionHeader(Transform parent, string name, string text, Vector2 anchor,
            Vector2 pos, float width, float fontSize)
        {
            var lbl = CreateCenteredLabel(parent, name, text, anchor, pos, new Vector2(width, 46f), Ink, fontSize);
            // yan tırnaklar (mockup'taki başlık çizgileri)
            Line(lbl.transform, "L", new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(width * 0.22f, 3f), InkSoft);
            Line(lbl.transform, "R", new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(width * 0.22f, 3f), InkSoft);
        }
    }
}
