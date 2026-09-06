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
            PopulateMapScreen(mapPanel.gameObject);                   // GERÇEK içerik (bölümün minihatitası + işaretler)
            PopulateSettingsScreen(setPanel.gameObject);              // GERÇEK içerik (ses/parlaklık/kalite)

            // ── Kalıcı çubuk (panellerden SONRA → üstte çizilir, sekmeler açık panelde de tıklanır)
            GameObject bar = new GameObject("PersistentBar", typeof(RectTransform));
            bar.transform.SetParent(canvasGO.transform, false);
            StretchFull(bar.GetComponent<RectTransform>());

            // Sekmeler mockup'taki gibi ÇİZİLMİŞ İKON + altında ad (game UI.pdf s.1). Eskiden koyu
            // dikdörtgen + yazıydı; oyunun geri kalanı el çizimi mürekkep diliyken tek başına
            // "editör düğmesi" gibi duruyordu.
            Button bookTab = InkTabButton(bar.transform, "Tab_Book", "KİTAP",  InkIcon.Book,
                                          new Vector2(1f, 0f), new Vector2(-330f, 34f));
            Button bagTab  = InkTabButton(bar.transform, "Tab_Bag",  "ÇANTA",  InkIcon.Bag,
                                          new Vector2(1f, 0f), new Vector2(-190f, 34f));
            Button mapTab  = InkTabButton(bar.transform, "Tab_Map",  "HARİTA", InkIcon.Scroll,
                                          new Vector2(1f, 0f), new Vector2(-50f,  34f));
            Button setBtn  = InkTabButton(bar.transform, "Btn_Settings", "",    InkIcon.Gear,
                                          new Vector2(1f, 1f), new Vector2(-60f, -60f), 78f);

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
            // Overworld'e OZEL sekmeler — savasta gizlenir. Ayar dislisi BU LISTEDE YOK:
            // her durumda erisilebilir kalmali (2026-08-12: savasta ayarlara ulasilamiyordu).
            var tabsProp = so.FindProperty("_overworldOnlyTabs");
            tabsProp.arraySize = 3;
            tabsProp.GetArrayElementAtIndex(0).objectReferenceValue = bookTab.gameObject;
            tabsProp.GetArrayElementAtIndex(1).objectReferenceValue = bagTab.gameObject;
            tabsProp.GetArrayElementAtIndex(2).objectReferenceValue = mapTab.gameObject;
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
                    "  • HARİTA — 8 BÖLÜMLÜK ilerleme yolu (bulunulan bölüm CANLI) + HAN/ŞİFACI/MARKET pinleri\n" +
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
            // Kitap gövdesi de el çizimi mürekkep (game UI.pdf s.3) — düz 9-slice dikdörtgen
            // yerine dalgalı kontur + köşe süsleri.
            RectTransform book = InkPanel(t, "BookBody", new Vector2(0.5f, 0.5f),
                new Vector2(0f, -10f), new Vector2(1500f, 780f), 30);
            Transform b = book;

            // İki sayfa ayrımı (spine) — orta dikey mürekkep çizgisi
            Line(b, "Spine", new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(6f, 700f),
                new Color(FrameDark.r, FrameDark.g, FrameDark.b, 0.55f));

            // ── YER İMLERİ (2026-09-04): kitap artık İKİ sayfa takımı taşıyor ────
            // Yetenek ağacı ayrı bir tam-ekran menü DEĞİL, kitabın bir sayfası (Efe'nin kararı).
            // Sayfa takımları aynı gövdenin içinde; kenardaki yer imleri arasında geçiş yapılır.
            RectTransform pageClasses = PageRoot(b, "Page_Classes");
            RectTransform pageSkills  = PageRoot(b, "Page_Skills");

            Image bmClassesBg;
            Button bmClasses = Bookmark(b, "Bookmark_Classes", "KARAKTER",  110f, out bmClassesBg);
            Image bmSkillsBg;
            Button bmSkills  = Bookmark(b, "Bookmark_Skills",  "YETENEK",  -110f, out bmSkillsBg);

            // Sınıf sayfası ARTIK kitap gövdesine değil, kendi sayfa köküne çizilir.
            b = pageClasses;

            // ÖZ DEPOSU BURADAN KALDIRILDI (2026-09-06, Efe): "okçuya bakarken kese sayacını
            // görmek gereksiz". Sayaclar ÇANTA'ya taşındı (Panel_Bag → ÖZ sekmesi) — eşya
            // ekonomisi zaten orada duruyor.

            // ── KARAKTERLER: bir karakter = bir kitap açılışı, sayfa sayfa çevrilir ──
            // (2026-09-06, Efe'nin isteği). Eski dört kutuluk ızgara kaldırıldı: ikisi "kilitli"
            // yer tutucuydu, büst de maliyet de sığmıyordu.
            PopulateCharacterPage(b, panelGO);

            // ── Sağ kenar EVRİM yer imi (dışa taşan sekme — placeholder) ───────
            RectTransform evo = InkPanel(b, "EvoBookmark", new Vector2(1f, 0.5f),
                new Vector2(122f, 120f), new Vector2(150f, 300f), 14, 0.9f);
            CreateCenteredLabel(evo, "EvoLabel", "LEVEL\nEVRİM\nÖRG\n———\n+4",
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(130f, 260f), Ink, 26f);

            // (Alt köşedeki sabit "1 / 2" sayfa numaraları kaldırıldı: karakter sayfası artık
            //  GERÇEK sayfa sayacı gösteriyor — iki sayı yan yana kafa karıştırıyordu.)

            CreateCenteredLabel(t, "BookHint",
                "ÖZ DEPOSU canlı · evrim/kart etkileşimi sonraki adım · Kapat: Esc",
                new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(1300f, 40f),
                new Color(0.62f, 0.57f, 0.48f), 24f);

            // ── İkinci sayfa takımı: KAM'IN YETENEK AĞACI ─────────────────────
            PopulateSkillPage(pageSkills, panelGO);

            // Yer imi çevirici (hangi sayfa takımı görünür).
            var pager = panelGO.AddComponent<BookmarkPager>();
            var pso = new SerializedObject(pager);
            SerializedProperty pages = pso.FindProperty("_pages");
            pages.arraySize = 2;
            WireBookmark(pages.GetArrayElementAtIndex(0), pageClasses.gameObject, bmClasses, bmClassesBg);
            WireBookmark(pages.GetArrayElementAtIndex(1), pageSkills.gameObject,  bmSkills,  bmSkillsBg);
            pso.ApplyModifiedProperties();
        }

        /// <summary>
        /// Mockup'taki alt sekme: çizilmiş mürekkep ikon + altında ad. Arkasında ÇOK SOLUK bir
        /// kâğıt lekesi var — mockup'ta yok ama orada zemin beyaz; oyunda ikon 3B haritanın
        /// üstüne düşüyor ve leke olmadan koyu arazide kayboluyor.
        /// </summary>
        private static Button InkTabButton(Transform parent, string name, string label,
            InkIcon icon, Vector2 anchor, Vector2 pos, float iconSize = 92f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(iconSize + 28f, iconSize + (string.IsNullOrEmpty(label) ? 24f : 56f));

            Image bg = go.GetComponent<Image>();
            bg.sprite = InkArtFactory.Paper("paper_soft", 96, 96, Color.white);
            bg.type   = Image.Type.Sliced;
            bg.color  = new Color(ParchmentHi.r, ParchmentHi.g, ParchmentHi.b, 0.55f);
            bg.raycastTarget = true;

            Button btn = go.GetComponent<Button>();
            btn.targetGraphic = bg;
            ColorBlock cb = btn.colors;
            cb.normalColor      = Color.white;
            cb.highlightedColor = new Color(1.08f, 1.05f, 1.00f);
            cb.pressedColor     = new Color(0.85f, 0.80f, 0.72f);
            cb.fadeDuration     = 0.08f;
            btn.colors = cb;

            InkImage(go.transform, "Icon", InkArtFactory.Icon(icon, 128), new Vector2(0.5f, 1f),
                     new Vector2(0f, -8f), new Vector2(iconSize, iconSize), Ink);

            if (!string.IsNullOrEmpty(label))
                CreateCenteredLabel(go.transform, "Label", label, new Vector2(0.5f, 0f),
                    new Vector2(0f, 6f), new Vector2(iconSize + 24f, 32f), Ink, 22f);

            return btn;
        }

        /// <summary>Kitabın içindeki bir SAYFA TAKIMI kökü (gövdeyi tam kaplar, görünürlüğü
        /// <see cref="BookmarkPager"/> çevirir).</summary>
        private static RectTransform PageRoot(Transform book, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(book, false);
            var rt = go.GetComponent<RectTransform>();
            StretchFull(rt);
            return rt;
        }

        /// <summary>Kitabın SOL kenarından dışa taşan yer imi düğmesi. Sağ kenar DOLU
        /// (EVRİM yer imi orada) — iki yer imi üst üste binmesin.</summary>
        private static Button Bookmark(Transform book, string name, string label, float y, out Image background)
        {
            Button btn = CreateUIButton(book, name, label, new Vector2(0f, 0.5f),
                new Vector2(-96f, y), new Vector2(150f, 150f), ParchmentLo, 22f);
            background = btn.GetComponent<Image>();
            return btn;
        }

        private static void WireBookmark(SerializedProperty el, GameObject root, Button bookmark, Image bg)
        {
            el.FindPropertyRelative("_root").objectReferenceValue               = root;
            el.FindPropertyRelative("_bookmark").objectReferenceValue           = bookmark;
            el.FindPropertyRelative("_bookmarkBackground").objectReferenceValue = bg;
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
            RectTransform box = InkPanel(parent, "Class_" + header, new Vector2(0.5f, 0.5f),
                anchoredPos, new Vector2(660f, 210f), 16, 0.92f);

            SectionHeader(box, "Header", header, new Vector2(0.5f, 1f), new Vector2(0f, -6f), 300f, 28f);

            // Portre çerçevesi (mürekkep kenar + iç portre görseli ClassBookEntry'nin boyadığı)
            RectTransform pf = InkPanel(box, "PortraitFrame", new Vector2(0f, 0.5f),
                new Vector2(22f, -14f), new Vector2(132f, 132f), 12);
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
