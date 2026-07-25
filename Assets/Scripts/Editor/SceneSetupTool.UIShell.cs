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
            AddPlaceholderContent(bagPanel.transform, "ÇANTA");       // iskelet
            AddPlaceholderContent(mapPanel.transform, "HARİTA");      // iskelet (dekoratif harita sonra)
            AddPlaceholderContent(setPanel.transform, "AYARLAR");     // iskelet

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
                    "Gezinme kabuğu (KİTAP·ÇANTA·HARİTA + ⚙) + KİTAP ekranı içeriği kuruldu:\n\n" +
                    "  • ÖZ DEPOSU — 3 canlı öz sayacı (EssenceWallet'e bağlı)\n" +
                    "  • Sınıf roster'ı — WARRIOR/RANGER (portre+evrim maliyeti), HEALER/MAGE kilitli\n\n" +
                    "Play → KİTAP sekmesine bas. Esc ile kapat. Savaşta kabuk gizlenir.",
                    "Tamam");

            Debug.Log("[TacticalRPG] Menu UI iskeleti + KİTAP kuruldu (MenuShell_Canvas).");
        }

        // ─────────────────────────────────────────────────────────────────────
        // KİTAP ekranı içeriği
        // ─────────────────────────────────────────────────────────────────────

        private static void PopulateBookScreen(GameObject panelGO)
        {
            Transform t = panelGO.transform;

            // ── ÖZ DEPOSU başlık + 3 canlı sayaç ──────────────────────────────
            CreateCenteredLabel(t, "OzDeposuTitle", "ÖZ DEPOSU",
                new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(600f, 70f),
                new Color(0.95f, 0.90f, 0.75f), 46f);

            CreateEssenceCounter(t, new Vector2(-260f, -110f), out var amtA, out var nameA, out var swA);
            CreateEssenceCounter(t, new Vector2(   0f, -110f), out var amtS, out var nameS, out var swS);
            CreateEssenceCounter(t, new Vector2( 260f, -110f), out var amtT, out var nameT, out var swT);

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

            CreateClassEntry(t, "WARRIOR", warrior, new Vector2(-470f,  110f));
            CreateClassEntry(t, "HEALER",  null,    new Vector2(-470f, -170f)); // henüz yok → kilitli
            CreateClassEntry(t, "MAGE",    null,    new Vector2( 470f,  110f)); // henüz yok → kilitli
            CreateClassEntry(t, "RANGER",  ranger,  new Vector2( 470f, -170f));

            // ── Sağ kenar evrim paneli (placeholder — etkileşim sonra) ────────
            Image evo = CreateImage(t, "EvoPanel", new Vector2(1f, 0.5f), new Vector2(-30f, 0f),
                new Vector2(150f, 520f), new Color(0.16f, 0.13f, 0.10f, 0.85f), false);
            CreateCenteredLabel(evo.transform, "EvoLabel", "LEVEL\nEVRİM\n———\n+ ?",
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140f, 320f),
                new Color(0.85f, 0.80f, 0.65f), 28f);

            CreateCenteredLabel(t, "BookHint",
                "ÖZ DEPOSU canlı · evrim/kart etkileşimi sonraki adım · Kapat: Esc",
                new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(1300f, 40f),
                new Color(0.60f, 0.55f, 0.48f), 24f);
        }

        /// <summary>Bir öz sayacı widget'ı (renk kutusu + miktar + ad). Etiket ref'lerini out ile döner.</summary>
        private static void CreateEssenceCounter(Transform parent, Vector2 anchoredPos,
            out TextMeshProUGUI amount, out TextMeshProUGUI nameLabel, out Image swatch)
        {
            GameObject c = new GameObject("Counter", typeof(RectTransform));
            c.transform.SetParent(parent, false);
            var rt = c.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(220f, 90f);

            swatch    = CreateImage(c.transform, "Swatch", new Vector2(0f, 0.5f), new Vector2(0f, 0f),
                new Vector2(46f, 46f), Color.white, false);
            amount    = CreateCenteredLabel(c.transform, "Amount", "0", new Vector2(0f, 0.5f),
                new Vector2(64f, 12f), new Vector2(150f, 50f), Color.white, 40f);
            nameLabel = CreateCenteredLabel(c.transform, "Name", "", new Vector2(0f, 0.5f),
                new Vector2(64f, -26f), new Vector2(150f, 32f), new Color(0.80f, 0.75f, 0.65f), 22f);
        }

        private static void WireEssenceCounter(SerializedProperty el, EssenceType type,
            TextMeshProUGUI amount, TextMeshProUGUI nameLabel, Image swatch)
        {
            el.FindPropertyRelative("type").enumValueIndex        = (int)type; // enum 0..2 sıralı
            el.FindPropertyRelative("amountLabel").objectReferenceValue = amount;
            el.FindPropertyRelative("nameLabel").objectReferenceValue   = nameLabel;
            el.FindPropertyRelative("swatch").objectReferenceValue      = swatch;
        }

        /// <summary>Bir sınıf bölümü: başlık + portre kutusu + evrim maliyeti + (data null → kilitli).</summary>
        private static void CreateClassEntry(Transform parent, string header, CharacterClassData data, Vector2 anchoredPos)
        {
            GameObject box = new GameObject("Class_" + header, typeof(RectTransform), typeof(Image));
            box.transform.SetParent(parent, false);
            var rt = box.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(560f, 240f);
            Image boxBg = box.GetComponent<Image>();
            boxBg.color         = new Color(0.16f, 0.13f, 0.10f, 0.55f);
            boxBg.raycastTarget = false;

            CreateCenteredLabel(box.transform, "Header", header,
                new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(520f, 46f),
                new Color(0.95f, 0.90f, 0.75f), 34f);

            Image portrait = CreateImage(box.transform, "Portrait", new Vector2(0f, 0.5f),
                new Vector2(30f, -14f), new Vector2(150f, 150f), Color.gray, false);

            TextMeshProUGUI cost = CreateCenteredLabel(box.transform, "Cost", "",
                new Vector2(0f, 0.5f), new Vector2(210f, -14f), new Vector2(330f, 130f),
                new Color(0.85f, 0.82f, 0.70f), 26f);

            // Kilitli kaplaması (data null → aktif)
            GameObject overlay = new GameObject("LockedOverlay", typeof(RectTransform), typeof(Image));
            overlay.transform.SetParent(box.transform, false);
            StretchFull(overlay.GetComponent<RectTransform>());
            Image ovImg = overlay.GetComponent<Image>();
            ovImg.color         = new Color(0.05f, 0.04f, 0.03f, 0.78f);
            ovImg.raycastTarget = false;
            CreateCenteredLabel(overlay.transform, "LockLabel", "🔒 KİLİTLİ",
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 60f),
                new Color(0.70f, 0.65f, 0.55f), 30f);

            ClassBookEntry entry = box.AddComponent<ClassBookEntry>();
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
            bg.color         = new Color(0.10f, 0.08f, 0.06f, 0.97f);
            bg.raycastTarget = true; // tüm ekranı kaplar → tıklama sızmaz

            MenuScreenPanel panel = go.AddComponent<MenuScreenPanel>();
            var pso = new SerializedObject(panel);
            pso.FindProperty("_screen").enumValueIndex = (int)screen;
            pso.ApplyModifiedProperties();

            go.SetActive(false);
            return panel;
        }

        /// <summary>Jenerik iskelet içeriği (başlık + ipucu) — henüz doldurulmamış paneller için.</summary>
        private static void AddPlaceholderContent(Transform panel, string title)
        {
            CreateCenteredLabel(panel, "Title", title,
                new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(1000f, 130f),
                new Color(0.95f, 0.90f, 0.75f), 72f);
            CreateCenteredLabel(panel, "Hint",
                "İskelet — içerik yakında.\n(Kapat: Esc veya aynı sekmeye tekrar bas)",
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1300f, 220f),
                new Color(0.70f, 0.65f, 0.55f), 38f);
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
