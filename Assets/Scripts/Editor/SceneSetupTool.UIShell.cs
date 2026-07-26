using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using TacticalRPG.Core;
using TacticalRPG.Data;
using TacticalRPG.UI;

namespace TacticalRPG.Editor
{
    /// <summary>
    /// SceneSetupTool'un UI parçası: ana menü GEZİNME İSKELETİ + KİTAP ekranı içeriğini programatik
    /// kurar (bu projede tüm sahne/HUD elle prefab yerine kodla kurulur — SetupDebugHUD deseni).
    ///
    /// Kurulan: MenuShell_Canvas (ScreenSpaceOverlay, 1920x1080 CanvasScaler — HudScale ile aynı) +
    /// EventSystem (yoksa) + 4 tam-ekran panel + kalıcı çubuk (3 sekme + ⚙) + <see cref="MenuNavigator"/>.
    /// KİTAP paneli GERÇEK veriye bağlı: ÖZ DEPOSU (EssenceWallet + EssenceConfigSO, canlı) + sınıf
    /// roster'ı (CharacterClassData portre/evrim maliyeti; Mage/Healer henüz yok → kilitli).
    ///
    /// NOT: IMGUI (OnGUI) HUD'ları ScreenSpaceOverlay Canvas'ın ÜSTÜNE çizer; menü açıkken overworld
    /// IMGUI panellerini gizlemek ayrı iş (MenuNavigator.OnMenuOpenChanged'e abone olup).
    /// </summary>
    public static partial class SceneSetupTool
    {
        private const string MenuShellCanvasName = "MenuShell_Canvas";
        private const string WarriorClassPath    = "Assets/Data/Characters/Savascı.asset";
        private const string RangerClassPath     = "Assets/Data/Characters/Ranger.asset";

        [MenuItem("TacticalRPG/UI - Menu Iskeleti Kur", false, 22)]
        public static void SetupUIShell()
        {
            EnsureEventSystem();

            GameObject sceneRoot = GameObject.Find(SceneRootName);

            GameObject old = GameObject.Find(MenuShellCanvasName);
            if (old != null) Object.DestroyImmediate(old);

            GameObject canvasGO = new GameObject(MenuShellCanvasName, typeof(RectTransform));
            if (sceneRoot != null) canvasGO.transform.SetParent(sceneRoot.transform, false);

            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            // ── Tam-ekran paneller (başlangıçta gizli; zeminleri raycast hedefi → tıklama sızmaz)
            MenuScreenPanel bookPanel = CreateMenuPanel(canvasGO.transform, "Panel_Book",     MenuScreen.Book);
            MenuScreenPanel bagPanel  = CreateMenuPanel(canvasGO.transform, "Panel_Bag",      MenuScreen.Bag);
            MenuScreenPanel mapPanel  = CreateMenuPanel(canvasGO.transform, "Panel_Map",      MenuScreen.Map);
            MenuScreenPanel setPanel  = CreateMenuPanel(canvasGO.transform, "Panel_Settings", MenuScreen.Settings);

            PopulateBookScreen(bookPanel.gameObject);                 // GERÇEK içerik
            PopulateBagScreen(bagPanel.gameObject);                   // GERÇEK içerik (potlar + Kam kartları)
            PopulateMapScreen(mapPanel.gameObject);                   // GERÇEK içerik (3x3 snake dünya + pinler)
            PopulateSettingsScreen(setPanel.gameObject);              // GERÇEK içerik (ses/parlaklık/kalite)

            // ── Kalıcı çubuk (panellerden SONRA → üstte çizilir, sekmeler açık panelde de tıklanır)
            GameObject bar = new GameObject("PersistentBar", typeof(RectTransform));
            bar.transform.SetParent(canvasGO.transform, false);
            StretchFull(bar.GetComponent<RectTransform>());

            Color tabBg = new Color(0.16f, 0.13f, 0.10f, 0.92f);

            Button bookTab = CreateUIButton(bar.transform, "Tab_Book",  "KİTAP",  new Vector2(1f, 0f), new Vector2(-340f, 40f), new Vector2(130f, 130f), tabBg, 26f);
            Button bagTab  = CreateUIButton(bar.transform, "Tab_Bag",   "ÇANTA",  new Vector2(1f, 0f), new Vector2(-190f, 40f), new Vector2(130f, 130f), tabBg, 26f);
            Button mapTab  = CreateUIButton(bar.transform, "Tab_Map",   "HARİTA", new Vector2(1f, 0f), new Vector2(-40f,  40f), new Vector2(130f, 130f), tabBg, 24f);
            Button setBtn  = CreateUIButton(bar.transform, "Btn_Settings", "⚙",   new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(100f, 100f), tabBg, 48f);

            // ── Navigator (hepsini bağlar)
            MenuNavigator nav = canvasGO.AddComponent<MenuNavigator>();
            GameStateManager gsm = FindComponentAnywhere<GameStateManager>();

            var so = new SerializedObject(nav);
            SerializedProperty panelsProp = so.FindProperty("_panels");
            MenuScreenPanel[] panels = { bookPanel, bagPanel, mapPanel, setPanel };
            panelsProp.arraySize = panels.Length;
            for (int i = 0; i < panels.Length; i++)
                panelsProp.GetArrayElementAtIndex(i).objectReferenceValue = panels[i];

            so.FindProperty("_persistentBar").objectReferenceValue  = bar;
            so.FindProperty("_bookTab").objectReferenceValue        = bookTab;
            so.FindProperty("_bagTab").objectReferenceValue         = bagTab;
            so.FindProperty("_mapTab").objectReferenceValue         = mapTab;
            so.FindProperty("_settingsButton").objectReferenceValue = setBtn;
            so.FindProperty("_stateManager").objectReferenceValue   = gsm;
            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            if (!_silentSetup)
                EditorUtility.DisplayDialog(
                    "UI İskeleti + KİTAP Kuruldu",
                    "Gezinme kabuğu (KİTAP·ÇANTA·HARİTA + ⚙) + 4 ekranın içeriği kuruldu:\n\n" +
                    "  • KİTAP — ÖZ DEPOSU (3 canlı sayaç) + sınıf roster'ı (WARRIOR/RANGER gerçek)\n" +
                    "  • ÇANTA — POTLAR (placeholder) + KAM KARTLARI (AteşTopu/RuhKalkanı/Şifa gerçek)\n" +
                    "  • HARİTA — 3x3 SNAKE dünya (CurrentMap CANLI vurgu) + HAN/ŞİFACI/MARKET pinleri\n" +
                    "  • AYARLAR — MASTER/MÜZİK/SFX + PARLAKLIK, KALİTE/TAM EKRAN/VSYNC + telifsiz müzik\n\n" +
                    "Play → sekmelere bas. Esc ile kapat. Savaşta kabuk gizlenir.",
                    "Tamam");

            Debug.Log("[TacticalRPG] Menu UI iskeleti + KİTAP kuruldu (MenuShell_Canvas).");
        }

        // ─────────────────────────────────────────────────────────────────────
        // KİTAP ekranı içeriği
        // ─────────────────────────────────────────────────────────────────────

        private static void PopulateBookScreen(GameObject panelGO)
        {
            Transform t = panelGO.transform;

            // ── Açık KİTAP gövdesi (çerçeveli krem parşömen) ───────────────────
            RectTransform book = FramedPanel(t, "BookBody", new Vector2(0.5f, 0.5f),
                new Vector2(0f, -10f), new Vector2(1500f, 780f), 14f);
            Transform b = book;

            // İki sayfa ayrımı (spine) — orta dikey mürekkep çizgisi
            Line(b, "Spine", new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(6f, 700f),
                new Color(FrameDark.r, FrameDark.g, FrameDark.b, 0.55f));

            // ── ÖZ DEPOSU süslü şeridi (üst-orta) + 3 CANLI sayaç ──────────────
            RectTransform banner = FramedPanel(b, "OzBanner", new Vector2(0.5f, 1f),
                new Vector2(0f, 42f), new Vector2(640f, 150f), 8f, ParchmentHi, FrameDark);
            SectionHeader(banner, "OzTitle", "ÖZ DEPOSU", new Vector2(0.5f, 1f),
                new Vector2(0f, -8f), 360f, 30f);

            CreateEssenceCounter(banner, new Vector2(-190f, -46f), out var amtA, out var nameA, out var swA);
            CreateEssenceCounter(banner, new Vector2(   0f, -46f), out var amtS, out var nameS, out var swS);
            CreateEssenceCounter(banner, new Vector2( 190f, -46f), out var amtT, out var nameT, out var swT);

            EssenceWallet   wallet = FindComponentAnywhere<EssenceWallet>();
            EssenceConfigSO config = FindEssenceConfig();

            EssenceStorageView view = panelGO.AddComponent<EssenceStorageView>();
            var vso = new SerializedObject(view);
            vso.FindProperty("_wallet").objectReferenceValue = wallet;
            vso.FindProperty("_config").objectReferenceValue = config;
            SerializedProperty counters = vso.FindProperty("_counters");
            counters.arraySize = 3;
            WireEssenceCounter(counters.GetArrayElementAtIndex(0), EssenceType.Ates,   amtA, nameA, swA);
            WireEssenceCounter(counters.GetArrayElementAtIndex(1), EssenceType.Su,     amtS, nameS, swS);
            WireEssenceCounter(counters.GetArrayElementAtIndex(2), EssenceType.Toprak, amtT, nameT, swT);
            vso.ApplyModifiedProperties();

            // ── Sınıf bölümleri (mockup: sol WARRIOR/HEALER, sağ MAGE/RANGER) ──
            CharacterClassData warrior = AssetDatabase.LoadAssetAtPath<CharacterClassData>(WarriorClassPath);
            CharacterClassData ranger  = AssetDatabase.LoadAssetAtPath<CharacterClassData>(RangerClassPath);

            CreateClassEntry(b, "WARRIOR", warrior, new Vector2(-372f,  128f));
            CreateClassEntry(b, "HEALER",  null,    new Vector2(-372f, -112f)); // henüz yok → kilitli
            CreateClassEntry(b, "MAGE",    null,    new Vector2( 372f,  128f)); // henüz yok → kilitli
            CreateClassEntry(b, "RANGER",  ranger,  new Vector2( 372f, -112f));

            // ── Sağ kenar EVRİM yer imi (dışa taşan sekme — placeholder) ───────
            RectTransform evo = FramedPanel(b, "EvoBookmark", new Vector2(1f, 0.5f),
                new Vector2(122f, 120f), new Vector2(150f, 300f), 8f, ParchmentLo, FrameDark);
            CreateCenteredLabel(evo, "EvoLabel", "LEVEL\nEVRİM\nÖRG\n———\n+4",
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(130f, 260f), Ink, 26f);

            // Sayfa numaraları (kitap alt köşeleri)
            CreateCenteredLabel(b, "PageL", "1", new Vector2(0f, 0f), new Vector2(70f, 44f), new Vector2(60f, 50f), InkSoft, 34f);
            CreateCenteredLabel(b, "PageR", "2", new Vector2(1f, 0f), new Vector2(-70f, 44f), new Vector2(60f, 50f), InkSoft, 34f);

            CreateCenteredLabel(t, "BookHint",
                "ÖZ DEPOSU canlı · evrim/kart etkileşimi sonraki adım · Kapat: Esc",
                new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(1300f, 40f),
                new Color(0.62f, 0.57f, 0.48f), 24f);
        }

        /// <summary>Bir öz madalyonu (renkli daire + üstünde miktar + altında ad). Ref'leri out ile döner.</summary>
        private static void CreateEssenceCounter(Transform parent, Vector2 pos,
            out TextMeshProUGUI amount, out TextMeshProUGUI nameLabel, out Image swatch)
        {
            GameObject c = new GameObject("Counter", typeof(RectTransform));
            c.transform.SetParent(parent, false);
            var rt = c.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(150f, 100f);

            // mürekkep halka + renkli öz madalyonu (renk EssenceStorageView'den gelir)
            Circle(c.transform, "Ring", new Vector2(0.5f, 1f), new Vector2(0f, 4f), 62f, FrameDark);
            swatch    = Circle(c.transform, "Swatch", new Vector2(0.5f, 1f), new Vector2(0f, 0f), 54f, Color.white);
            amount    = CreateCenteredLabel(c.transform, "Amount", "0", new Vector2(0.5f, 1f),
                new Vector2(0f, -14f), new Vector2(70f, 44f), Ink, 30f);
            nameLabel = CreateCenteredLabel(c.transform, "Name", "", new Vector2(0.5f, 1f),
                new Vector2(0f, -62f), new Vector2(150f, 30f), InkSoft, 20f);
        }

        private static void WireEssenceCounter(SerializedProperty el, EssenceType type,
            TextMeshProUGUI amount, TextMeshProUGUI nameLabel, Image swatch)
        {
            el.FindPropertyRelative("type").enumValueIndex        = (int)type; // enum 0..2 sıralı
            el.FindPropertyRelative("amountLabel").objectReferenceValue = amount;
            el.FindPropertyRelative("nameLabel").objectReferenceValue   = nameLabel;
            el.FindPropertyRelative("swatch").objectReferenceValue      = swatch;
        }

        /// <summary>Bir sınıf bölümü (parşömen kart): başlık şeridi + portre çerçevesi + evrim maliyeti +
        /// dekoratif kart yuva sırası + (data null → KİLİTLİ kaplaması). ClassBookEntry bağlanır.</summary>
        private static void CreateClassEntry(Transform parent, string header, CharacterClassData data, Vector2 anchoredPos)
        {
            RectTransform box = FramedPanel(parent, "Class_" + header, new Vector2(0.5f, 0.5f),
                anchoredPos, new Vector2(660f, 210f), 6f, ParchmentLo, FrameDark);

            SectionHeader(box, "Header", header, new Vector2(0.5f, 1f), new Vector2(0f, -6f), 300f, 28f);

            // Portre çerçevesi (mürekkep kenar + iç portre görseli ClassBookEntry'nin boyadığı)
            RectTransform pf = FramedPanel(box, "PortraitFrame", new Vector2(0f, 0.5f),
                new Vector2(22f, -14f), new Vector2(132f, 132f), 5f, Parchment, FrameDark);
            Image portrait = CreateImage(pf, "Portrait", new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(120f, 120f), Color.gray, false);
            var prt = portrait.rectTransform; prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);

            // Dekoratif kart yuvaları (mockup: portre yanında bir sıra boş kart)
            for (int i = 0; i < 3; i++)
                Sliced(box, "Slot" + i, new Vector2(0f, 0.5f),
                    new Vector2(176f + i * 116f, 20f), new Vector2(104f, 124f), Parchment);

            TextMeshProUGUI cost = CreateCenteredLabel(box, "Cost", "",
                new Vector2(0f, 0.5f), new Vector2(176f, -72f), new Vector2(400f, 40f), InkSoft, 24f);

            // Kilitli kaplaması (data null → aktif)
            GameObject overlay = new GameObject("LockedOverlay", typeof(RectTransform), typeof(Image));
            overlay.transform.SetParent(box, false);
            StretchFull(overlay.GetComponent<RectTransform>());
            Image ovImg = overlay.GetComponent<Image>();
            ovImg.sprite        = RoundSprite; ovImg.type = Image.Type.Sliced;
            ovImg.color         = new Color(0.14f, 0.11f, 0.07f, 0.82f);
            ovImg.raycastTarget = false;
            CreateCenteredLabel(overlay.transform, "LockLabel", "KİLİTLİ",
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 60f),
                new Color(0.82f, 0.75f, 0.60f), 32f);

            ClassBookEntry entry = box.gameObject.AddComponent<ClassBookEntry>();
            var eso = new SerializedObject(entry);
            eso.FindProperty("_data").objectReferenceValue          = data;
            eso.FindProperty("_portrait").objectReferenceValue      = portrait;
            eso.FindProperty("_costLabel").objectReferenceValue     = cost;
            eso.FindProperty("_lockedOverlay").objectReferenceValue = overlay;
            eso.ApplyModifiedProperties();
        }

        private static EssenceConfigSO FindEssenceConfig()
        {
            string[] guids = AssetDatabase.FindAssets("t:EssenceConfigSO");
            if (guids != null && guids.Length > 0)
                return AssetDatabase.LoadAssetAtPath<EssenceConfigSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
            return null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Genel UI yardımcıları
        // ─────────────────────────────────────────────────────────────────────

        private static void EnsureEventSystem()
        {
            if (FindComponentAnywhere<EventSystem>() != null) return;

            GameObject go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            GameObject sceneRoot = GameObject.Find(SceneRootName);
            if (sceneRoot != null) go.transform.SetParent(sceneRoot.transform, false);
            Debug.Log("[TacticalRPG] EventSystem olusturuldu (StandaloneInputModule).");
        }

        /// <summary>Boş tam-ekran panel (koyu zemin + MenuScreenPanel). İçerik ayrıca eklenir. Gizli döner.</summary>
        private static MenuScreenPanel CreateMenuPanel(Transform parent, string name, MenuScreen screen)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            StretchFull(go.GetComponent<RectTransform>());

            Image bg = go.GetComponent<Image>();
            bg.color         = new Color(0.07f, 0.055f, 0.04f, 0.94f); // sıcak koyu backdrop (parşömen öne çıksın)
            bg.raycastTarget = true; // tüm ekranı kaplar → tıklama sızmaz

            MenuScreenPanel panel = go.AddComponent<MenuScreenPanel>();
            var pso = new SerializedObject(panel);
            pso.FindProperty("_screen").enumValueIndex = (int)screen;
            pso.ApplyModifiedProperties();

            go.SetActive(false);
            return panel;
        }

        private static Image CreateImage(Transform parent, string name,
            Vector2 anchor, Vector2 anchoredPos, Vector2 size, Color color, bool raycast)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = anchor;
            rt.anchoredPosition = anchoredPos; rt.sizeDelta = size;
            Image img = go.GetComponent<Image>();
            img.color         = color;
            img.raycastTarget = raycast;
            return img;
        }

        private static TextMeshProUGUI CreateCenteredLabel(
            Transform parent, string name, string text,
            Vector2 anchor, Vector2 anchoredPos, Vector2 size, Color color, float fontSize)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = anchor;
            rt.anchoredPosition = anchoredPos; rt.sizeDelta = size;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text               = text;
            tmp.fontSize           = fontSize;
            tmp.color              = color;
            tmp.alignment          = TextAlignmentOptions.Center;
            tmp.fontStyle          = FontStyles.Bold;
            tmp.enableWordWrapping = true;
            tmp.raycastTarget      = false;
            return tmp;
        }

        private static Button CreateUIButton(
            Transform parent, string name, string label,
            Vector2 anchor, Vector2 anchoredPos, Vector2 size, Color bg, float fontSize)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = anchor;
            rt.anchoredPosition = anchoredPos; rt.sizeDelta = size;

            Image img = go.GetComponent<Image>();
            img.color         = bg;
            img.raycastTarget = true;

            Button btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            ColorBlock cb = btn.colors;
            cb.normalColor      = new Color(0.90f, 0.90f, 0.90f, 1f);
            cb.highlightedColor = Color.white;
            cb.pressedColor     = new Color(0.70f, 0.70f, 0.70f, 1f);
            cb.selectedColor    = new Color(0.90f, 0.90f, 0.90f, 1f);
            cb.fadeDuration     = 0.08f;
            btn.colors = cb;

            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(go.transform, false);
            var lrt = labelGO.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;

            var tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.text               = label;
            tmp.fontSize           = fontSize;
            tmp.color              = new Color(0.95f, 0.90f, 0.75f);
            tmp.alignment          = TextAlignmentOptions.Center;
            tmp.fontStyle          = FontStyles.Bold;
            tmp.enableWordWrapping = false;
            tmp.raycastTarget      = false;

            return btn;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot     = new Vector2(0.5f, 0.5f);
        }
    }
}
