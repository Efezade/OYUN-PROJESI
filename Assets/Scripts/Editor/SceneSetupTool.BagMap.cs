using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using TacticalRPG.Core;
using TacticalRPG.Data;
using TacticalRPG.Grid;
using TacticalRPG.UI;

namespace TacticalRPG.Editor
{
    /// <summary>
    /// SceneSetupTool'un ÇANTA + HARİTA parçası — game UI.pdf s.5-6 (valiz) ve s.2 (parşömen harita)
    /// mockup'larına göre PARŞÖMEN estetiğinde (SceneSetupTool.UIKit yardımcıları). "ÇANTA/HARİTA" yazısı
    /// YOK; şeklin kendisi (valiz sapı+sekmeler / yırtık harita+pusula) kimliği taşır.
    ///
    ///  • ÇANTA — sap + sol dikey sekmeler (EŞYALAR · POTLAR). 2026-10-03: EŞYALAR'da karakterlere
    ///    sürükle-bırak eşya (<see cref="BagInventoryView"/>), POTLAR'da iç (<see cref="BagPotionView"/>).
    ///  • HARİTA — bölümün GERÇEK minihatitası (keşfedilen arazi + önemli karo işaretleri),
    ///    sağda işaret açıklamaları, sürüklenip yakınlaştırılabilir.
    ///    2026-08-17'de KALDIRILANLAR (kullanıcı isteği): 8 bölümlük ilerleme yolu (aynı bilgi TAB
    ///    şeridinde), "Bölüm N — tema" başlığı ve pusula. Ekran tamamen haritaya ayrıldı.
    ///    Bunlarla birlikte <see cref="WorldMapView"/> de bu panelde kullanılmıyor (sınıf duruyor).
    ///    1 bölüm = 1 harita (GAME_DESIGN.md §0). Eski 3×3 snake dünya: TASK-004 ile alternatife alındı,
    ///    bkz <c>Docs/Alternatif_Tasarimlar/3x3_Dunya_Haritasi/</c>.
    /// </summary>
    public static partial class SceneSetupTool
    {
        // ─────────────────────────────────────────────────────────────────────
        // ÇANTA — valiz
        // ─────────────────────────────────────────────────────────────────────

        private static void PopulateBagScreen(GameObject panelGO)
        {
            Transform t = panelGO.transform;
            GameObject bagRoot = panelGO;      // sayfa çevirici ve öz görünümü buraya takılır

            // ── Valiz gövdesi (el çizimi mürekkep — game UI.pdf s.5) ──────────
            RectTransform bag = InkPanel(t, "BagBody", new Vector2(0.5f, 0.5f),
                new Vector2(0f, -6f), new Vector2(1440f, 690f), 26);

            // Sap (üstte yatay bar + iki kayış)
            Sliced(bag, "HandleBar",  new Vector2(0.5f, 1f), new Vector2(0f, 66f), new Vector2(360f, 28f), FrameDark);
            Sliced(bag, "HandleL",    new Vector2(0.5f, 1f), new Vector2(-150f, 34f), new Vector2(28f, 74f), FrameDark);
            Sliced(bag, "HandleR",    new Vector2(0.5f, 1f), new Vector2( 150f, 34f), new Vector2(28f, 74f), FrameDark);

            // ── SEKMELER: yalnız EŞYALAR ve POTLAR (Efe, 2026-09-06) ──────────
            // BÜYÜ/ZIRH kaldırıldı: sistemleri yok, boş sekme "bozuk mu" hissi veriyordu.
            // ÖZ de sekme DEĞİL — çantanın köşesinde her sekmede görünen küçük bir şerit
            // ("açılır kapanır olmasın, hep yazsın").
            RectTransform pageItems = BagPageRoot(bag, "Page_Items");
            RectTransform pageKam   = BagPageRoot(bag, "Page_Kam");    // 2026-10-03: Kam'ın donanımı
            RectTransform pagePots  = BagPageRoot(bag, "Page_Pots");

            Image itemsTabBg, kamTabBg, potsTabBg;
            Button itemsTab = BagTab(bag, "EŞYALAR", InkIcon.Bag,   150f, out itemsTabBg);
            Button kamTab   = BagTab(bag, "KAM",     InkIcon.Hand,   16f, out kamTabBg);
            Button potsTab  = BagTab(bag, "POTLAR",  InkIcon.Drop, -118f, out potsTabBg);

            var pager = bagRoot.AddComponent<TacticalRPG.UI.BookmarkPager>();
            var pgSO = new SerializedObject(pager);
            SerializedProperty pages = pgSO.FindProperty("_pages");
            pages.arraySize = 3;
            WireBookmark(pages.GetArrayElementAtIndex(0), pageItems.gameObject, itemsTab, itemsTabBg);
            WireBookmark(pages.GetArrayElementAtIndex(1), pageKam.gameObject,   kamTab,   kamTabBg);
            WireBookmark(pages.GetArrayElementAtIndex(2), pagePots.gameObject,  potsTab,  potsTabBg);
            pgSO.ApplyModifiedProperties();

            // ÖZ ŞERİDİ: sayfa köklerinin DIŞINDA, doğrudan valiz gövdesinde → sekme değişse de
            // durur. Sağ üst köşe: sap ile çakışmıyor, içerik alanının dışında kalıyor.
            CreateEssenceStrip(bag, bagRoot);

            // ── DONANIM sayfaları (2026-10-03, Efe'nin isteği): giydirme bebeği ──────────
            // EŞYALAR = Kam dışı birlik (solda karakter listesi), KAM = yalnız komutan. İkisi de
            // ortada silüet + altı vücut bölgesi yuvası, sağda çanta. Sürükleme hayaleti ortak.
            var ghostGO = new GameObject("DragGhost", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            ghostGO.transform.SetParent(bag, false);
            ((RectTransform)ghostGO.transform).sizeDelta = new Vector2(64f, 64f);
            var ghost = ghostGO.GetComponent<Image>();
            ghost.raycastTarget = false;
            ghostGO.SetActive(false);

            BuildEquipmentPage(pageItems, commanderOnly: false, ghost);
            BuildEquipmentPage(pageKam,   commanderOnly: true,  ghost);

            // ── POTLAR sayfası: eldeki potlar (İÇ) + etkin potlar ──────────────
            SectionHeader(pagePots, "PotsHeader", "POTLAR — iç, süresi boyunca etkili", new Vector2(0.5f, 0.5f),
                new Vector2(-180f, 232f), 760f, 28f);
            RectTransform potContent = CreateScrollList(pagePots, "PotScroll",
                new Vector2(-200f, -46f), new Vector2(780f, 500f), grid: false);

            RectTransform activePanel = InkPanel(pagePots, "ActivePots", new Vector2(0.5f, 0.5f),
                new Vector2(470f, -80f), new Vector2(400f, 420f), 14);
            var activeLabel = CreateCenteredLabel(activePanel, "ActiveText", "ETKİN POTLAR", new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(370f, 400f), Ink, 19f);
            activeLabel.alignment = TextAlignmentOptions.TopLeft;
            activeLabel.richText  = true;

            var potView = pagePots.gameObject.AddComponent<BagPotionView>();
            var pvSO = new SerializedObject(potView);
            pvSO.FindProperty("_inventory").objectReferenceValue   = EnsureInventory();
            pvSO.FindProperty("_buffs").objectReferenceValue       = FindComponentAnywhere<PlayerBuffs>();
            pvSO.FindProperty("_content").objectReferenceValue     = potContent;
            pvSO.FindProperty("_activeLabel").objectReferenceValue = activeLabel;
            pvSO.FindProperty("_cellSprite").objectReferenceValue  = InkArtFactory.Paper("paper_soft", 96, 96, Color.white);
            pvSO.FindProperty("_potSprite").objectReferenceValue   = InkArtFactory.Icon(InkIcon.Drop, 64);
            pvSO.ApplyModifiedProperties();

            CreateCenteredLabel(t, "BagHint",
                "Eşyayı karakterin uygun BÖLGESİNE sürükle · yuvadan çantaya sürükle ya da sağ tık = çıkar · potu İÇ · Kapat: Esc",
                new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(1400f, 40f),
                new Color(0.62f, 0.57f, 0.48f), 24f);
        }

        /// <summary>
        /// GİYDİRME BEBEĞİ sayfası: (EŞYALAR'da) solda karakter listesi · ortada büst + ad +
        /// tam boy silüet ve çevresinde altı bölge yuvası + toplam etki satırı · sağda çanta +
        /// künye. Yuvalar silüetin iki yanında, bölgeye yakın hizada durur (kafa/boyun üstte,
        /// kollar ortada, gövde/ayak altta).
        /// </summary>
        private static void BuildEquipmentPage(RectTransform page, bool commanderOnly, Image ghost)
        {
            Sprite paper = InkArtFactory.Paper("paper_soft", 96, 96, Color.white);

            // ── Sol: karakter listesi (yalnız EŞYALAR) ────────────────────────
            RectTransform rosterContent = null;
            if (!commanderOnly)
            {
                SectionHeader(page, "RosterHeader", "KARAKTERLER", new Vector2(0.5f, 0.5f),
                    new Vector2(-560f, 250f), 240f, 24f);
                rosterContent = CreateScrollList(page, "RosterScroll",
                    new Vector2(-560f, -36f), new Vector2(250f, 540f), grid: false);
            }

            // ── Orta: büst + ad + silüet + yuvalar ────────────────────────────
            float cx = commanderOnly ? -300f : -140f;
            RectTransform doll = InkPanel(page, "DollPanel", new Vector2(0.5f, 0.5f),
                new Vector2(cx, -20f), new Vector2(560f, 600f), 18, 0.85f);

            var bustBox = InkPanel(doll, "Bust", new Vector2(0f, 1f), new Vector2(54f, -52f), new Vector2(84f, 84f), 10, 0.95f);
            var portrait = CreateImage(bustBox, "Img", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(74f, 74f), Ink, false);
            portrait.preserveAspect = true;

            var nameLabel = CreateCenteredLabel(doll, "Name", commanderOnly ? "KAM" : "—", new Vector2(0.5f, 1f),
                new Vector2(40f, -36f), new Vector2(380f, 44f), Ink, 30f);
            nameLabel.richText = true;

            var body = InkImage(doll, "Silhouette", InkArtFactory.Doll(220, 340), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -10f), new Vector2(220f, 340f), new Color(Ink.r, Ink.g, Ink.b, 0.80f));
            body.preserveAspect = true;

            // Yuvalar: sol sütun (kafa · sağ kol · gövde), sağ sütun (boyun · sol kol · ayak).
            var regions = new (EquipSlot region, Vector2 pos)[]
            {
                (EquipSlot.Kafa,   new Vector2(-185f,  150f)),
                (EquipSlot.Boyun,  new Vector2( 185f,  150f)),
                (EquipSlot.SagKol, new Vector2(-185f,   20f)),
                (EquipSlot.SolKol, new Vector2( 185f,   20f)),
                (EquipSlot.Govde,  new Vector2(-185f, -110f)),
                (EquipSlot.Ayak,   new Vector2( 185f, -110f)),
            };

            var slotParts = new List<(EquipSlot region, Image bg, RectTransform holder, TextMeshProUGUI label)>();
            foreach (var (region, pos) in regions)
            {
                RectTransform slot = InkPanel(doll, $"Slot_{region}", new Vector2(0.5f, 0.5f), pos, new Vector2(98f, 98f), 12);
                Image bg = slot.GetComponent<Image>();

                // Simge tutucu yuvanın ÇOCUĞU: simgenin üstüne bırakılan eşya yuvaya kabarcıklanır.
                var holderGO = new GameObject("IconHolder", typeof(RectTransform));
                holderGO.transform.SetParent(slot, false);
                var holder = (RectTransform)holderGO.transform;
                StretchFull(holder);

                var label = CreateCenteredLabel(doll, $"SlotLabel_{region}", EquipSlots.Label(region),
                    new Vector2(0.5f, 0.5f), pos + new Vector2(0f, -62f), new Vector2(150f, 22f), InkSoft, 15f);
                slotParts.Add((region, bg, holder, label));
            }

            var stats = CreateCenteredLabel(doll, "Stats", "", new Vector2(0.5f, 0f),
                new Vector2(0f, 40f), new Vector2(530f, 64f), Ink, 15f);
            stats.richText = true;

            if (commanderOnly)
            {
                // KAM sayfasına özel not — Kam'ın eşyası da diğerleri gibi savaşta işler.
                RectTransform note = InkPanel(page, "KamNote", new Vector2(0.5f, 0.5f),
                    new Vector2(-600f, -20f), new Vector2(160f, 600f), 14, 0.8f);
                var noteText = CreateCenteredLabel(note, "Text",
                    "KAM\n\nKomutan.\nSavaşa ZORUNLU iner, ölürse bölüm kaybedilir.\n\nTakılan eşyalar her savaşta işler.\n\nBüyüler davuldan, 10 mana.",
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140f, 580f), InkSoft, 15f);
                noteText.alignment = TextAlignmentOptions.Top;
            }

            // ── Sağ: çanta + künye ────────────────────────────────────────────
            SectionHeader(page, "BagHeader", "ÇANTA", new Vector2(0.5f, 0.5f),
                new Vector2(300f, 250f), 220f, 26f);
            RectTransform bagDrop = InkPanel(page, "BagDropArea", new Vector2(0.5f, 0.5f),
                new Vector2(420f, 20f), new Vector2(500f, 340f), 14, 0.55f);
            RectTransform bagContent = CreateScrollList(bagDrop, "BagScroll", Vector2.zero,
                new Vector2(482f, 322f), grid: true);

            RectTransform detailPanel = InkPanel(page, "ItemDetail", new Vector2(0.5f, 0.5f),
                new Vector2(420f, -240f), new Vector2(500f, 150f), 14);
            var detail = CreateCenteredLabel(detailPanel, "DetailText", "", new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(470f, 136f), Ink, 17f);
            detail.alignment = TextAlignmentOptions.TopLeft;
            detail.richText  = true;

            // ── Görünümü bağla ────────────────────────────────────────────────
            var view = page.gameObject.AddComponent<BagInventoryView>();
            var so = new SerializedObject(view);
            so.FindProperty("_inventory").objectReferenceValue     = EnsureInventory();
            so.FindProperty("_party").objectReferenceValue         = FindComponentAnywhere<PartyManager>();
            so.FindProperty("_commanderOnly").boolValue            = commanderOnly;
            so.FindProperty("_rosterContent").objectReferenceValue = rosterContent;
            so.FindProperty("_portrait").objectReferenceValue      = portrait;
            so.FindProperty("_nameLabel").objectReferenceValue     = nameLabel;
            so.FindProperty("_statsLabel").objectReferenceValue    = stats;
            so.FindProperty("_bagContent").objectReferenceValue    = bagContent;
            so.FindProperty("_bagDropArea").objectReferenceValue   = bagDrop;
            so.FindProperty("_detailLabel").objectReferenceValue   = detail;
            so.FindProperty("_ghost").objectReferenceValue         = ghost;
            so.FindProperty("_cellSprite").objectReferenceValue    = paper;

            SerializedProperty arr = so.FindProperty("_slots");
            arr.arraySize = slotParts.Count;
            for (int i = 0; i < slotParts.Count; i++)
            {
                SerializedProperty el = arr.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("_region").enumValueIndex       = (int)slotParts[i].region;
                el.FindPropertyRelative("_background").objectReferenceValue = slotParts[i].bg;
                el.FindPropertyRelative("_iconHolder").objectReferenceValue = slotParts[i].holder;
                el.FindPropertyRelative("_label").objectReferenceValue      = slotParts[i].label;
            }
            so.ApplyModifiedProperties();
        }

        /// <summary>
        /// Kaydırılabilir liste: görünüm (maske) + içerik (dikey liste ya da ızgara, boyu içeriğe
        /// göre büyür). Satırları çalışma zamanında görünüm bileşenleri doldurur.
        /// </summary>
        private static RectTransform CreateScrollList(Transform parent, string name, Vector2 pos, Vector2 size, bool grid)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect));
            root.transform.SetParent(parent, false);
            var rt = (RectTransform)root.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var bg = root.GetComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.001f);       // görünmez ama sürükleme/kaydırma raycast'i alır

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(rt, false);
            var vrt = (RectTransform)viewport.transform;
            StretchFull(vrt);

            var content = new GameObject("Content", typeof(RectTransform), typeof(ContentSizeFitter));
            content.transform.SetParent(vrt, false);
            var crt = (RectTransform)content.transform;
            crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = Vector2.zero;
            crt.sizeDelta = new Vector2(0f, 0f);

            if (grid)
            {
                var g = content.AddComponent<GridLayoutGroup>();
                g.cellSize = new Vector2(84f, 84f);
                g.spacing  = new Vector2(10f, 10f);
                g.padding  = new RectOffset(8, 8, 8, 8);
            }
            else
            {
                var v = content.AddComponent<VerticalLayoutGroup>();
                v.spacing = 8f;
                v.padding = new RectOffset(6, 6, 6, 6);
                v.childAlignment = TextAnchor.UpperCenter;
                v.childForceExpandWidth = false;
                v.childForceExpandHeight = false;
            }
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = root.GetComponent<ScrollRect>();
            scroll.viewport = vrt;
            scroll.content  = crt;
            scroll.horizontal = false;
            scroll.vertical   = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            return crt;
        }

        /// <summary>Valizin içindeki bir SAYFA TAKIMI kökü (gövdeyi kaplar, görünürlüğü pager çevirir).</summary>
        private static RectTransform BagPageRoot(Transform bag, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(bag, false);
            var rt = go.GetComponent<RectTransform>();
            StretchFull(rt);
            return rt;
        }

        /// <summary>Valizin sol kenarındaki ikonlu sekme.</summary>
        private static Button BagTab(Transform bag, string label, InkIcon icon, float y, out Image background)
        {
            RectTransform tab = InkPanel(bag, "Tab_" + label, new Vector2(0f, 0.5f),
                new Vector2(-36f, y), new Vector2(96f, 112f), 12);
            InkImage(tab, "Icon", InkArtFactory.Icon(icon, 64), new Vector2(0.5f, 1f),
                new Vector2(0f, -10f), new Vector2(46f, 46f), Ink);
            CreateCenteredLabel(tab, "L", label, new Vector2(0.5f, 0f), new Vector2(0f, 8f),
                new Vector2(90f, 30f), Ink, 18f);

            background = tab.GetComponent<Image>();
            var btn = tab.gameObject.AddComponent<Button>();
            btn.targetGraphic = background;
            return btn;
        }

        /// <summary>
        /// ÖZ ŞERİDİ (2026-09-06, Efe): çantanın sağ üst köşesinde HER SEKMEDE duran küçük sayaç.
        /// Sekme yapılmadı — "açılır kapanır olmasın, çantayı açınca hep yazsın". KİTAP'tan
        /// kaldırıldı çünkü karakter sayfasında kese bilgisi ilgisizdi.
        /// </summary>
        private static void CreateEssenceStrip(Transform bag, GameObject bagRoot)
        {
            RectTransform strip = InkPanel(bag, "OzStrip", new Vector2(1f, 1f),
                new Vector2(-28f, -24f), new Vector2(360f, 122f), 14, 0.95f);

            CreateCenteredLabel(strip, "OzTitle", "ÖZ", new Vector2(0.5f, 1f),
                new Vector2(0f, -6f), new Vector2(200f, 30f), InkSoft, 20f);

            CreateEssenceCounter(strip, new Vector2(-80f, -18f), out var amtA, out var nameA, out var swA);
            CreateEssenceCounter(strip, new Vector2( 80f, -18f), out var amtS, out var nameS, out var swS);

            var view = bagRoot.GetComponent<EssenceStorageView>();
            if (view == null) view = bagRoot.AddComponent<EssenceStorageView>();

            var vso = new SerializedObject(view);
            vso.FindProperty("_wallet").objectReferenceValue = FindComponentAnywhere<EssenceWallet>();
            vso.FindProperty("_config").objectReferenceValue = FindEssenceConfig();
            SerializedProperty counters = vso.FindProperty("_counters");
            counters.arraySize = 2;
            WireEssenceCounter(counters.GetArrayElementAtIndex(0), EssenceType.Tas,  amtA, nameA, swA);
            WireEssenceCounter(counters.GetArrayElementAtIndex(1), EssenceType.Doga, amtS, nameS, swS);
            vso.ApplyModifiedProperties();
        }

        // ─────────────────────────────────────────────────────────────────────
        // HARİTA — parşömen
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// HARİTA EKRANI — bölümün GERÇEK haritası (kullanıcı isteği 2026-08-17).
        ///
        /// Eskiden burada 8 bölümlük ilerleme yolu vardı; o bilgi TAB şeridinde zaten duruyor
        /// (<see cref="TacticalRPG.UI.MinimapHUD"/>) ve oyuncunun haritayı açtığında görmek
        /// istediği şey BULUNDUĞU bölümün arazisi: nereyi keşfetti, market/savaş alanı/öz nerede.
        ///
        /// Harita dokusunu <see cref="MinimapRenderer"/> verinin kendisinden boyar, işaretleri
        /// <see cref="TacticalRPG.UI.MinimapView"/> üstüne yerleştirir. Bu araç yalnız YERLEŞİMİ
        /// kurar ve referansları bağlar.
        /// </summary>
        private static void PopulateMapScreen(GameObject panelGO)
        {
            Transform t = panelGO.transform;

            RectTransform map = FramedPanel(t, "MapBody", new Vector2(0.5f, 0.5f),
                new Vector2(60f, -6f), new Vector2(1360f, 710f), 12f);

            // İç çerçeve (çift kenar hissi) — 4 ince mürekkep çizgi
            InnerBorder(map, 22f, InkSoft);

            // ── Harita yüzeyi: koyu parşömen yuvası + doku ─────────────────────
            // Yuva KREM DEĞİL koyu: keşfedilmemiş bölge hiç çizilmiyor, altındaki koyu zemin
            // "burası daha çizilmedi" hissini veriyor.
            RectTransform board = FramedPanel(map, "MinimapBoard", new Vector2(0.5f, 0.5f),
                new Vector2(-170f, 0f), new Vector2(980f, 650f), 10f,
                new Color(0.20f, 0.17f, 0.13f), FrameDark);

            // Yuva artık MASKELİ GÖRÜŞ ALANI: yakınlaştırılan harita taşınca kırpılsın.
            // Ayrıca fareyi yakalaması gerek — sürükleme olayı bu grafikten baloncuklanıyor.
            var boardImg = board.GetComponent<Image>();
            if (boardImg != null) boardImg.raycastTarget = true;
            board.gameObject.AddComponent<RectMask2D>();

            var rawGO = new GameObject("MinimapImage", typeof(RectTransform), typeof(RawImage));
            rawGO.transform.SetParent(board, false);
            var rawRT = rawGO.GetComponent<RectTransform>();
            rawRT.anchorMin = rawRT.anchorMax = rawRT.pivot = new Vector2(0.5f, 0.5f);
            rawRT.anchoredPosition = Vector2.zero;
            rawRT.sizeDelta = new Vector2(940f, 620f);        // MinimapView oranı koruyarak düzeltir
            var raw = rawGO.GetComponent<RawImage>();
            raw.raycastTarget = false;

            // İşaret katmanı dokunun ÇOCUĞU ve onu tam kaplar → doku yeniden boyutlanınca
            // işaretler de kendiliğinden doğru yerde kalır.
            var iconGO = new GameObject("Icons", typeof(RectTransform));
            iconGO.transform.SetParent(rawRT, false);
            var iconRT = iconGO.GetComponent<RectTransform>();
            iconRT.anchorMin = Vector2.zero;
            iconRT.anchorMax = Vector2.one;
            iconRT.offsetMin = iconRT.offsetMax = Vector2.zero;

            // Seyahat işaretleri (seçim halkası + rota noktaları) AYRI katman: harita ekranı her
            // açıldığında ikonlar sıfırdan kurulur, seçim onunla birlikte silinmesin.
            var travelGO = new GameObject("TravelMarkers", typeof(RectTransform));
            travelGO.transform.SetParent(rawRT, false);
            var travelRT = travelGO.GetComponent<RectTransform>();
            travelRT.anchorMin = Vector2.zero;
            travelRT.anchorMax = Vector2.one;
            travelRT.offsetMin = travelRT.offsetMax = Vector2.zero;

            // Parlama katmanı: haritanın ÜSTÜNDE saydam renk. rawGO'dan SONRA eklendiği için
            // ikonların da üstünde kalır → hız tokeni parlaması tüm yüzeyi kaplar.
            var surfaceGO = new GameObject("GlowSurface", typeof(RectTransform), typeof(Image));
            surfaceGO.transform.SetParent(board, false);
            var surfaceRT = surfaceGO.GetComponent<RectTransform>();
            surfaceRT.anchorMin = Vector2.zero;
            surfaceRT.anchorMax = Vector2.one;
            surfaceRT.offsetMin = surfaceRT.offsetMax = Vector2.zero;
            var surfaceImg = surfaceGO.GetComponent<Image>();
            surfaceImg.color         = new Color(1f, 1f, 1f, 0f);
            surfaceImg.raycastTarget = false;   // tıklama/sürükleme haritaya geçsin

            // Çerçeve şeritleri: maskenin DIŞINDA (yuvanın çerçevesinde) → harita kaysa bile
            // kenarda sabit dururlar.
            Transform frameT = board.parent;
            var borders = new Image[4];
            borders[0] = GlowStrip(frameT, "GlowTop",    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 7f));
            borders[1] = GlowStrip(frameT, "GlowRight",  new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(7f, 0f));
            borders[2] = GlowStrip(frameT, "GlowBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 7f));
            borders[3] = GlowStrip(frameT, "GlowLeft",   new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(7f, 0f));

            TextMeshProUGUI empty = CreateCenteredLabel(board, "MinimapEmpty",
                "Harita henüz üretilmedi", new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(700f, 60f), new Color(0.72f, 0.66f, 0.55f), 30f);

            // ── Yakınlaştırma düğmeleri (haritanın sağ alt köşesinde, MASKENİN DIŞINDA) ──
            // Yuvanın çocuğu olsalardı maske onları da kırpardı ve harita kayarken beraber
            // kayarlardı; MapBody'ye asılıyorlar → sabit dururlar.
            Button zoomIn  = CreateUIButton(map, "Btn_ZoomIn",  "+", new Vector2(0.5f, 0.5f),
                new Vector2(268f, -216f), new Vector2(54f, 54f), new Color(0.16f, 0.13f, 0.10f, 0.92f), 40f);
            // "-" ASCII kısa çizgi: yazı tipi atlasında kesin var. En-dash/minus işareti eksik
            // glyph riski taşıyor (TMP fallback atlası eksik karakteri kutu olarak çizer).
            Button zoomOut = CreateUIButton(map, "Btn_ZoomOut", "-", new Vector2(0.5f, 0.5f),
                new Vector2(268f, -278f), new Vector2(54f, 54f), new Color(0.16f, 0.13f, 0.10f, 0.92f), 40f);

            var pan = board.gameObject.AddComponent<MinimapPanZoom>();
            var pzo = new SerializedObject(pan);
            pzo.FindProperty("_viewport").objectReferenceValue       = board;
            pzo.FindProperty("_content").objectReferenceValue        = rawRT;
            pzo.FindProperty("_zoomInButton").objectReferenceValue   = zoomIn;
            pzo.FindProperty("_zoomOutButton").objectReferenceValue  = zoomOut;
            pzo.ApplyModifiedProperties();

            // ── Sağdaki açıklama şeridi (legend) ──────────────────────────────
            RectTransform legend = FramedPanel(map, "LegendPanel", new Vector2(0.5f, 0.5f),
                new Vector2(500f, 0f), new Vector2(320f, 650f), 10f, ParchmentHi, FrameDark);
            SectionHeader(legend, "LegendHeader", "İŞARETLER", new Vector2(0.5f, 1f),
                new Vector2(0f, -18f), 240f, 28f);

            var rows = new (MinimapIconKind kind, string label)[]
            {
                (MinimapIconKind.Market,     "Ticaret Hanı"),
                (MinimapIconKind.Encounter,  "Savaş Alanı"),
                (MinimapIconKind.Dungeon,    "Zindan"),
                (MinimapIconKind.Mandatory,  "Zorunlu Görev"),
                (MinimapIconKind.Watchtower, "Gözetleme Kulesi"),
                (MinimapIconKind.Essence,    "Öz Yatağı"),
            };

            var legendIcons = new Image[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                // Satır aralığı sıkıldı: altta güçlü yol taşı düğmesi + sayacı duracak.
                float y = 230f - i * 52f;
                var icoGO = new GameObject("LegendIcon_" + rows[i].kind, typeof(RectTransform), typeof(Image));
                icoGO.transform.SetParent(legend, false);
                var icoRT = icoGO.GetComponent<RectTransform>();
                icoRT.anchorMin = icoRT.anchorMax = icoRT.pivot = new Vector2(0f, 0.5f);
                icoRT.anchoredPosition = new Vector2(28f, y);
                icoRT.sizeDelta = new Vector2(34f, 34f);
                legendIcons[i] = icoGO.GetComponent<Image>();
                legendIcons[i].raycastTarget = false;
                // Sprite ÇALIŞMA ZAMANINDA üretiliyor (MinimapIcons) → MinimapView atar.

                CreateCenteredLabel(legend, "LegendLbl_" + rows[i].kind, rows[i].label,
                    new Vector2(0f, 0.5f), new Vector2(74f, y), new Vector2(220f, 36f), Ink, 22f);
            }

            // NOT ŞERİDİNE DOKUNULMADI: bu etiketlerin pivotu ALT kenar, yani metin YUKARI doğru
            // büyür ve hemen üstünde işaret satırları duruyor — dördüncü bir satır onların içine
            // taşardı. Yol belirlemenin anlatımı düğmenin kendi yazısında ve kip açılınca alttaki
            // onay şeridinde veriliyor.
            CreateCenteredLabel(legend, "LegendNote",
                "Sürükle: kaydır · +/−: yakınlaştır\nSeyahat için GÜÇLÜ YOL TAŞI kullan.\n" +
                "Rota yalnız KEŞFETTİĞİN karolardan geçer.",
                new Vector2(0.5f, 0f), new Vector2(0f, 236f), new Vector2(280f, 80f), InkSoft, 18f);

            // ── Güçlü yol taşı: seyahatin tek anahtarı ────────────────────────
            // Mesafeye göre birkaç taş harcanır, karşılığında AP ve zaman HİÇ harcanmaz.
            // (Ucuz "Yol Taşı" düğmesi 2026-08-19'da kullanıcı isteğiyle kaldırıldı.)
            TextMeshProUGUI powerLabel = CreateCenteredLabel(legend, "PowerStoneCount", "Güçlü yol taşı: 0",
                new Vector2(0.5f, 0f), new Vector2(0f, RouteBarLayout.CountY), new Vector2(280f, 30f),
                new Color(0.42f, 0.34f, 0.22f), 20f);

            Button powerButton = CreateUIButton(legend, "Btn_PowerStone", "GÜÇLÜ YOL TAŞI KULLAN",
                new Vector2(0.5f, 0f), new Vector2(0f, RouteBarLayout.PowerY),
                new Vector2(252f, RouteBarLayout.TallHeight),
                new Color(0.20f, 0.30f, 0.36f, 0.98f), 17f);

            // ── Yol belirle: taşın ALTINDAKİ ikinci bar (kullanıcı isteği 2026-09-01) ──
            // Seyahatle aynı yere konuyor ama işi bambaşka: hiçbir kaynak harcamaz, kimseyi
            // yürütmez — yalnız 3B haritada takip edilecek bir işaret bırakır. Rengi de ayrı
            // (kızıl), çünkü açıkken harita kırmızımsı parlıyor.
            Button routeButton = CreateUIButton(legend, "Btn_RouteMark", "YOL BELİRLE",
                new Vector2(0.5f, 0f), new Vector2(0f, RouteBarLayout.RouteY),
                new Vector2(252f, RouteBarLayout.TallHeight),
                new Color(0.36f, 0.16f, 0.14f, 0.98f), 17f);

            // Hemen ALTINDA YOLU SİL (kullanıcı isteği 2026-09-02): tek tıkla duraklar,
            // minihatita işaretleri ve 3B patika birden gider. Ayrı düğme, çünkü rota kip
            // kapalıyken de duruyor — silmek için önce YOL BELİRLE'yi açmak saçmaydı.
            Button routeClearButton = CreateUIButton(legend, "Btn_RouteClear", "YOLU SİL",
                new Vector2(0.5f, 0f), new Vector2(0f, RouteBarLayout.ClearY),
                new Vector2(252f, RouteBarLayout.ShortHeight),
                new Color(0.30f, 0.13f, 0.12f, 0.98f), 16f);

            // EN ALTTA KARO GERİ GETİR (madde 10): çukurun kenarında kazanılan hak burada,
            // tanrısal bakışla, haritanın istenen çukuruna harcanır.
            Button tileRestoreButton = CreateUIButton(legend, "Btn_TileRestore", "KARO GERİ GETİR",
                new Vector2(0.5f, 0f), new Vector2(0f, RouteBarLayout.RestoreY),
                new Vector2(252f, RouteBarLayout.ShortHeight),
                new Color(0.13f, 0.30f, 0.19f, 0.98f), 16f);

            // NOT: pusula ve "Bölüm 1 — …" başlığı 2026-08-17'de KALDIRILDI (kullanıcı isteği).
            // Bölüm adı TAB şeridinde duruyor; başlıksız ekran haritaya daha çok yer bırakıyor.
            // Başlık gidince WorldMapView'in bu panelde yapacak işi kalmadı → eklenmiyor.

            // ── Seyahat onayı: haritanın alt kenarına oturan şerit (maskenin DIŞINDA) ──
            var promptGO = new GameObject("TravelPrompt", typeof(RectTransform));
            promptGO.transform.SetParent(map, false);
            var promptRT = promptGO.GetComponent<RectTransform>();
            promptRT.anchorMin = promptRT.anchorMax = promptRT.pivot = new Vector2(0.5f, 0.5f);
            promptRT.anchoredPosition = new Vector2(-170f, -262f);
            promptRT.sizeDelta = new Vector2(640f, 88f);

            Sliced(promptGO.transform, "PromptBg", new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(640f, 88f), new Color(0.10f, 0.085f, 0.065f, 0.95f), raycast: true);

            TextMeshProUGUI costLabel = CreateCenteredLabel(promptGO.transform, "CostLabel",
                "—", new Vector2(0.5f, 0.5f), new Vector2(-110f, 0f), new Vector2(380f, 56f),
                new Color(0.94f, 0.90f, 0.78f), 25f);

            Button confirm = CreateUIButton(promptGO.transform, "Btn_Confirm", "ONAYLA",
                new Vector2(0.5f, 0.5f), new Vector2(148f, 0f), new Vector2(140f, 54f),
                new Color(0.24f, 0.42f, 0.22f, 0.98f), 24f);
            Button cancel = CreateUIButton(promptGO.transform, "Btn_Cancel", "VAZGEÇ",
                new Vector2(0.5f, 0.5f), new Vector2(262f, 0f), new Vector2(96f, 54f),
                new Color(0.30f, 0.20f, 0.16f, 0.98f), 20f);

            promptGO.SetActive(false);   // yalnız karo seçilince görünür

            // Parlama efekti: token kullanılınca çerçeve ve yüzey renklenip parlar.
            var glow = board.gameObject.AddComponent<MinimapGlowEffect>();
            var gso  = new SerializedObject(glow);
            SerializedProperty borderProp = gso.FindProperty("_border");
            borderProp.arraySize = borders.Length;
            for (int i = 0; i < borders.Length; i++)
                borderProp.GetArrayElementAtIndex(i).objectReferenceValue = borders[i];
            gso.FindProperty("_surface").objectReferenceValue = surfaceImg;
            gso.FindProperty("_frame").objectReferenceValue   = frameT.GetComponent<Image>();
            gso.ApplyModifiedProperties();

            // HIZLI SEYAHAT GÖSTERİSİ: yolculuk onaylanınca harita ekranı kapanmaz, yuva sol alt
            // köşeye küçülüp saydamlaşır (kullanıcı isteği 2026-08-19). Küçülen parça yuvanın
            // ÇERÇEVESİDİR (frameT) — parlama şeritleri onun çocuğu, birlikte gitsinler.
            TravelPresenter presenter = WireTravelPresenter(panelGO, frameT, board, pan);

            WireTravelSelector(board, rawRT, travelRT, promptGO, costLabel, confirm, cancel,
                               glow, powerButton, powerLabel, presenter, routeButton, routeClearButton,
                               tileRestoreButton);

            WireMinimapView(panelGO, raw, iconRT, empty, pan, legendIcons, rows);

            CreateCenteredLabel(t, "MapHint",
                "Keşfettiğin arazi · önemli karolar işaretli · sis çizilmez · " +
                "sürükle: kaydır · tekerlek/+/−: yakınlaştır · Kapat: Esc",
                new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(1400f, 40f),
                new Color(0.62f, 0.57f, 0.48f), 24f);
        }

        /// <summary>Haritadan seyahat seçicisini kurar (tıkla → rota + bedel → onayla → yürü).
        /// Sürükleme/yakınlaştırma ile AYNI nesnede durur ama birbirlerini bilmezler: biri fareyi
        /// kaydırma, öbürü tıklama olarak okur.</summary>
        /// <summary>Parlama şeridi: kenara yapışan ince, başlangıçta görünmez bir bant.</summary>
        private static Image GlowStrip(Transform parent, string name, Vector2 anchorMin,
                                       Vector2 anchorMax, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot     = (anchorMin + anchorMax) * 0.5f;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;   // 0 olan eksen kenar boyunca GERİLİR

            var img = go.GetComponent<Image>();
            img.color         = new Color(1f, 1f, 1f, 0f);
            img.raycastTarget = false;
            return img;
        }

        private static void WireTravelSelector(RectTransform board, RectTransform content,
                                               RectTransform markerLayer, GameObject prompt,
                                               TextMeshProUGUI costLabel, Button confirm, Button cancel,
                                               MinimapGlowEffect glow,
                                               Button powerButton, TextMeshProUGUI powerLabel,
                                               TravelPresenter presenter, Button routeButton,
                                               Button routeClearButton, Button tileRestoreButton)
        {
            var sel = board.gameObject.AddComponent<MinimapTravelSelector>();
            var so  = new SerializedObject(sel);

            so.FindProperty("_renderer").objectReferenceValue = FindComponentAnywhere<MinimapRenderer>();
            so.FindProperty("_grid").objectReferenceValue     = FindComponentAnywhere<HexGridManager>();
            so.FindProperty("_fog").objectReferenceValue      = FindComponentAnywhere<FogOfWarManager>();
            so.FindProperty("_player").objectReferenceValue   = FindComponentAnywhere<PlayerController>();
            so.FindProperty("_ap").objectReferenceValue       = FindComponentAnywhere<ActionPointManager>();
            so.FindProperty("_state").objectReferenceValue    = FindComponentAnywhere<GameStateManager>();
            so.FindProperty("_run").objectReferenceValue      = FindComponentAnywhere<ChapterRunManager>();
            so.FindProperty("_nav").objectReferenceValue      = FindComponentAnywhere<TacticalRPG.UI.MenuNavigator>();

            so.FindProperty("_content").objectReferenceValue     = content;
            so.FindProperty("_markerLayer").objectReferenceValue = markerLayer;
            so.FindProperty("_promptRoot").objectReferenceValue  = prompt;
            so.FindProperty("_costLabel").objectReferenceValue   = costLabel;
            so.FindProperty("_confirmButton").objectReferenceValue = confirm;
            so.FindProperty("_cancelButton").objectReferenceValue  = cancel;

            so.FindProperty("_buffs").objectReferenceValue       = FindComponentAnywhere<PlayerBuffs>();
            so.FindProperty("_glow").objectReferenceValue        = glow;
            so.FindProperty("_powerButton").objectReferenceValue = powerButton;
            so.FindProperty("_powerLabel").objectReferenceValue  = powerLabel;
            so.FindProperty("_presenter").objectReferenceValue   = presenter;

            // Yol işareti: düğme burada, işaretin kendisi GameManager'da (3B haritayı o çiziyor).
            so.FindProperty("_routeButton").objectReferenceValue      = routeButton;
            so.FindProperty("_routeClearButton").objectReferenceValue = routeClearButton;
            so.FindProperty("_routeMarker").objectReferenceValue      = EnsureRouteMarker();

            // Karo geri getirme (madde 10): düğme burada, hak/onarım GameManager'daki bileşende.
            so.FindProperty("_restoreButton").objectReferenceValue    = tileRestoreButton;
            so.FindProperty("_recovery").objectReferenceValue         = EnsureTileRecovery();
            so.ApplyModifiedProperties();
        }

        /// <summary>
        /// Seyahat gösterisini kurar: yolculuk boyunca panelin çerçevesi silinir, harita yuvası
        /// sol alt köşeye küçülüp saydamlaşır, varışta geri büyür.
        ///
        /// KÜÇÜLEN PARÇA <paramref name="boardFrame"/>'dir (yuvanın çerçeve nesnesi), Fill DEĞİL:
        /// parlama şeritleri ve maskeli harita onun altında duruyor, hepsi birlikte gitmeli.
        /// </summary>
        private static TravelPresenter WireTravelPresenter(GameObject panelGO, Transform boardFrame,
                                                           RectTransform boardFill, MinimapPanZoom panZoom)
        {
            // Saydamlık için CanvasGroup — tek tek Image alfası yazmak haritanın üstündeki
            // işaretleri kapsamazdı.
            // `??` KULLANILMAZ: GetComponent bulamadığında SAHTE NULL döndürebiliyor, `??` onu
            // "dolu" sayıp AddComponent'i hiç çağırmıyor (2026-08-19'da tam olarak bu yaşandı,
            // bileşen sahneye hiç eklenmedi). Unity'nin == aşırı yüklemesi sahte null'ı bilir.
            CanvasGroup group = boardFrame.GetComponent<CanvasGroup>();
            if (group == null) group = boardFrame.gameObject.AddComponent<CanvasGroup>();

            var presenter = panelGO.AddComponent<TravelPresenter>();
            var pso = new SerializedObject(presenter);

            pso.FindProperty("_orb").objectReferenceValue        = EnsureTravelOrb();
            pso.FindProperty("_panZoom").objectReferenceValue    = panZoom;
            pso.FindProperty("_panelRoot").objectReferenceValue  = panelGO.GetComponent<RectTransform>();
            pso.FindProperty("_backdrop").objectReferenceValue   = panelGO.GetComponent<Image>();
            pso.FindProperty("_board").objectReferenceValue      = boardFrame as RectTransform;
            pso.FindProperty("_boardGroup").objectReferenceValue = group;

            // Yuva grafikleri: koyu çerçeve + içindeki koyu zemin. İkisi de kapanınca geriye
            // yalnız boyanmış harita ve işaretler kalır. Panelin üst katmanlarındaki parşömen
            // TravelPresenter'ın zincir yürüyüşüyle kendiliğinden gizleniyor, burada sayılmıyor.
            SetImageArray(pso.FindProperty("_boardChrome"),
                          boardFrame.GetComponent<Image>(),
                          boardFill.GetComponent<Image>());

            pso.ApplyModifiedProperties();
            return presenter;
        }

        private static void SetImageArray(SerializedProperty prop, params Image[] images)
        {
            // arraySize++ SON ELEMANI KOPYALAR — her eleman açıkça yazılır.
            prop.arraySize = images.Length;
            for (int i = 0; i < images.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = images[i];
        }

        /// <summary>Miniharita görüntüleyicisini kurar ve sahnedeki veri kaynaklarına bağlar.</summary>
        private static void WireMinimapView(GameObject panelGO, RawImage raw, RectTransform iconLayer,
                                            TextMeshProUGUI empty, MinimapPanZoom panZoom,
                                            Image[] legendIcons,
                                            (MinimapIconKind kind, string label)[] rows)
        {
            var mv  = panelGO.AddComponent<MinimapView>();
            var mso = new SerializedObject(mv);

            MinimapRenderer renderer = FindComponentAnywhere<MinimapRenderer>();
            if (renderer == null)
                Debug.LogWarning("[HARİTA] Sahnede MinimapRenderer yok — harita bos gorunur. " +
                                 "Once 'TacticalRPG → Bolum - Tek Haritali Dunya Kur' calistir (ya da TAM KURULUM).");

            mso.FindProperty("_renderer").objectReferenceValue = renderer;
            mso.FindProperty("_grid").objectReferenceValue     = FindComponentAnywhere<HexGridManager>();
            mso.FindProperty("_fog").objectReferenceValue      = FindComponentAnywhere<FogOfWarManager>();
            mso.FindProperty("_nodes").objectReferenceValue    = FindComponentAnywhere<ChapterNodeManager>();
            mso.FindProperty("_field").objectReferenceValue    = FindComponentAnywhere<EssenceFieldManager>();
            mso.FindProperty("_player").objectReferenceValue   = FindComponentAnywhere<PlayerController>();
            mso.FindProperty("_style").objectReferenceValue    = EnsureMinimapStyle();

            mso.FindProperty("_image").objectReferenceValue      = raw;
            mso.FindProperty("_iconLayer").objectReferenceValue  = iconLayer;
            mso.FindProperty("_panZoom").objectReferenceValue    = panZoom;
            mso.FindProperty("_emptyLabel").objectReferenceValue = empty;
            mso.FindProperty("_maxSize").vector2Value            = new Vector2(940f, 620f);

            SerializedProperty legend = mso.FindProperty("_legend");
            legend.arraySize = legendIcons.Length;
            for (int i = 0; i < legendIcons.Length; i++)
            {
                SerializedProperty e = legend.GetArrayElementAtIndex(i);
                // DİKKAT: dizi büyütülürken Unity son elemanı KOPYALAR → her alan açıkça yazılır.
                e.FindPropertyRelative("icon").objectReferenceValue = legendIcons[i];
                e.FindPropertyRelative("kind").enumValueIndex       = (int)rows[i].kind;
            }
            mso.ApplyModifiedProperties();
        }

        /// <summary>SerializedObject dizisini verilen Unity nesneleriyle doldurur (boyut dahil).</summary>
        private static void FillObjectArray(SerializedObject so, string propertyPath, UnityEngine.Object[] values)
        {
            SerializedProperty arr = so.FindProperty(propertyPath);
            if (arr == null) return;
            arr.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        /// <summary>Parşömen pusulası (N/E/S/W). HARİTA ekranından 2026-08-17'de kaldırıldı ama
        /// yardımcı DURUYOR — başka bir ekranda istenirse tek satırla geri gelir.</summary>
        private static void CreateCompass(Transform parent, Vector2 pos, float diam)
        {
            Circle(parent, "CompassRing", new Vector2(0.5f, 0.5f), pos, diam + 12f, FrameDark);
            RectTransform disc = Circle(parent, "CompassDisc", new Vector2(0.5f, 0.5f), pos, diam, ParchmentHi).rectTransform;
            CreateCenteredLabel(disc, "N", "N", new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(40f, 30f), Ink, 24f);
            CreateCenteredLabel(disc, "S", "S", new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(40f, 30f), Ink, 24f);
            CreateCenteredLabel(disc, "E", "E", new Vector2(1f, 0.5f), new Vector2(-6f, 0f), new Vector2(30f, 30f), Ink, 24f);
            CreateCenteredLabel(disc, "W", "W", new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(30f, 30f), Ink, 24f);
            Line(disc, "NeedleN", new Vector2(0.5f, 0.5f), new Vector2(0f, 22f), new Vector2(8f, 44f), new Color(0.66f, 0.26f, 0.22f));
            Line(disc, "NeedleS", new Vector2(0.5f, 0.5f), new Vector2(0f, -22f), new Vector2(8f, 44f), InkSoft);
            Circle(disc, "Hub", new Vector2(0.5f, 0.5f), Vector2.zero, 16f, FrameDark);
        }

        /// <summary>Bir RectTransform'un içine ince mürekkep dikdörtgen kenarlık çizer (4 çizgi).</summary>
        private static void InnerBorder(RectTransform fill, float inset, Color color)
        {
            Line(fill, "BdrT", new Vector2(0.5f, 1f), new Vector2(0f, -inset), new Vector2(fillWidthGuess, 3f), color);
            Line(fill, "BdrB", new Vector2(0.5f, 0f), new Vector2(0f,  inset), new Vector2(fillWidthGuess, 3f), color);
            Line(fill, "BdrL", new Vector2(0f, 0.5f), new Vector2(inset, 0f),  new Vector2(3f, fillHeightGuess), color);
            Line(fill, "BdrR", new Vector2(1f, 0.5f), new Vector2(-inset, 0f), new Vector2(3f, fillHeightGuess), color);
        }

        // İç kenarlık çizgileri için kaba boyut (map fill ~1336x686). Stretch anchor'lu olmadığı için
        // sabit; harita boyutu değişirse burada güncellenir.
        private const float fillWidthGuess  = 1300f;
        private const float fillHeightGuess = 650f;
    }
}
