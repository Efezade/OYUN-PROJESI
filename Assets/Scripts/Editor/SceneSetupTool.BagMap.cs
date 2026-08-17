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
    ///  • ÇANTA — sap + sol dikey sekmeler + iki sütun (POTLAR | KAM KARTLARI, noktalı ayraç). Kartlar
    ///    gerçek <see cref="KamAbilityData"/> asset'lerine <see cref="AbilityCardView"/> ile bağlı.
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
        private static readonly string[] KamAbilityPaths =
        {
            "Assets/Data/Abilities/AtesTopu.asset",
            "Assets/Data/Abilities/RuhKalkani.asset",
            "Assets/Data/Abilities/Sifa.asset",
        };

        // ─────────────────────────────────────────────────────────────────────
        // ÇANTA — valiz
        // ─────────────────────────────────────────────────────────────────────

        private static void PopulateBagScreen(GameObject panelGO)
        {
            Transform t = panelGO.transform;

            // ── Valiz gövdesi ──────────────────────────────────────────────────
            RectTransform bag = FramedPanel(t, "BagBody", new Vector2(0.5f, 0.5f),
                new Vector2(0f, -6f), new Vector2(1440f, 690f), 14f);

            // Sap (üstte yatay bar + iki kayış)
            Sliced(bag, "HandleBar",  new Vector2(0.5f, 1f), new Vector2(0f, 66f), new Vector2(360f, 28f), FrameDark);
            Sliced(bag, "HandleL",    new Vector2(0.5f, 1f), new Vector2(-150f, 34f), new Vector2(28f, 74f), FrameDark);
            Sliced(bag, "HandleR",    new Vector2(0.5f, 1f), new Vector2( 150f, 34f), new Vector2(28f, 74f), FrameDark);

            // Sol dikey sekmeler (dışa taşar; aktif = KART)
            string[] tabs = { "KART", "POT", "BÜYÜ", "ZIRH" };
            for (int i = 0; i < tabs.Length; i++)
            {
                Color fill = i == 0 ? ParchmentHi : ParchmentLo;
                RectTransform tab = FramedPanel(bag, "Tab_" + tabs[i], new Vector2(0f, 0.5f),
                    new Vector2(-36f, 210f - i * 128f), new Vector2(96f, 112f), 5f, fill, FrameDark);
                CreateCenteredLabel(tab, "L", tabs[i], new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(90f, 40f), Ink, 22f);
            }

            // Orta noktalı ayraç
            for (float y = 244f; y >= -244f; y -= 34f)
                Sliced(bag, "Dot", new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(6f, 18f), InkSoft);

            // ── Sol sütun: POTLAR (placeholder — envanter yok) ─────────────────
            SectionHeader(bag, "PotsHeader", "POTLAR", new Vector2(0.5f, 0.5f), new Vector2(-360f, 244f), 340f, 30f);
            Circle(bag, "PotEmblem", new Vector2(0.5f, 0.5f), new Vector2(-360f, 168f), 70f, ParchmentLo);
            CreatePotRow(bag, "ŞİFA",  "×15", new Color(0.66f, 0.26f, 0.22f), new Vector2(-470f, 78f));
            CreatePotRow(bag, "MANA",  "×00", new Color(0.26f, 0.36f, 0.62f), new Vector2(-470f, -8f));
            CreatePotRow(bag, "?????", "×??", new Color(0.34f, 0.30f, 0.24f), new Vector2(-470f, -94f));

            // ── Sağ sütun: KAM KARTLARI (gerçek büyü verisi) ───────────────────
            SectionHeader(bag, "CardsHeader", "KAM KARTLARI", new Vector2(0.5f, 0.5f), new Vector2(360f, 244f), 420f, 30f);
            for (int i = 0; i < 5; i++) // 3 gerçek + 2 boş
            {
                KamAbilityData data = i < KamAbilityPaths.Length
                    ? AssetDatabase.LoadAssetAtPath<KamAbilityData>(KamAbilityPaths[i])
                    : null;
                CreateAbilityCardRow(bag, data, new Vector2(360f, 150f - i * 96f));
            }

            CreateCenteredLabel(t, "BagHint",
                "KAM KARTLARI canlı · potlar/skill-tree envanter sistemiyle gelecek · Kapat: Esc",
                new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(1400f, 40f),
                new Color(0.62f, 0.57f, 0.48f), 24f);
        }

        private static void CreatePotRow(Transform parent, string name, string count, Color potColor, Vector2 pos)
        {
            Circle(parent, "Pot_" + name, new Vector2(0f, 0.5f), pos, 54f, potColor);
            CreateCenteredLabel(parent, "PotName_" + name, name, new Vector2(0f, 0.5f),
                new Vector2(pos.x + 76f, pos.y + 14f), new Vector2(200f, 34f), Ink, 26f);
            CreateCenteredLabel(parent, "PotCount_" + name, count, new Vector2(0f, 0.5f),
                new Vector2(pos.x + 76f, pos.y - 18f), new Vector2(200f, 32f), InkSoft, 24f);
        }

        /// <summary>Bir Kam kartı SATIRI (thumb + ad + stat), AbilityCardView'e bağlı.</summary>
        private static void CreateAbilityCardRow(Transform parent, KamAbilityData data, Vector2 pos)
        {
            RectTransform row = FramedPanel(parent, "Card", new Vector2(0.5f, 0.5f),
                pos, new Vector2(600f, 90f), 5f, Parchment, FrameDark);

            RectTransform thumb = FramedPanel(row, "Thumb", new Vector2(0f, 0.5f),
                new Vector2(20f, 0f), new Vector2(78f, 78f), 4f, ParchmentLo, FrameDark);
            Image icon = CreateImage(thumb, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(66f, 66f), Color.gray, false);

            TextMeshProUGUI nameLbl = CreateCenteredLabel(row, "Name", "", new Vector2(0f, 0.5f),
                new Vector2(120f, 16f), new Vector2(440f, 38f), Ink, 26f);
            TextMeshProUGUI statLbl = CreateCenteredLabel(row, "Stat", "", new Vector2(0f, 0.5f),
                new Vector2(120f, -18f), new Vector2(440f, 28f), InkSoft, 19f);
            TextMeshProUGUI descLbl = CreateCenteredLabel(row, "Desc", "", new Vector2(1f, 0.5f),
                new Vector2(-14f, 16f), new Vector2(220f, 60f), new Color(0.40f, 0.33f, 0.24f), 16f);

            GameObject empty = new GameObject("EmptyOverlay", typeof(RectTransform), typeof(Image));
            empty.transform.SetParent(row, false);
            StretchFull(empty.GetComponent<RectTransform>());
            Image eImg = empty.GetComponent<Image>();
            eImg.sprite = RoundSprite; eImg.type = Image.Type.Sliced;
            eImg.color = new Color(0.83f, 0.75f, 0.58f, 0.92f); eImg.raycastTarget = false;
            CreateCenteredLabel(empty.transform, "EmptyLabel", "BOŞ SLOT",
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 40f), InkSoft, 24f);

            AbilityCardView view = row.gameObject.AddComponent<AbilityCardView>();
            var vso = new SerializedObject(view);
            vso.FindProperty("_ability").objectReferenceValue      = data;
            vso.FindProperty("_icon").objectReferenceValue         = icon;
            vso.FindProperty("_nameLabel").objectReferenceValue    = nameLbl;
            vso.FindProperty("_statLabel").objectReferenceValue    = statLbl;
            vso.FindProperty("_descLabel").objectReferenceValue    = descLbl;
            vso.FindProperty("_emptyOverlay").objectReferenceValue = empty;
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
                // Satır aralığı sıkıldı: altta İKİ yol taşı düğmesi + sayaçları duracak.
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

            CreateCenteredLabel(legend, "LegendNote",
                "Sürükle: kaydır · +/−: yakınlaştır\nSeyahat için önce bir YOL TAŞI kullan.",
                new Vector2(0.5f, 0f), new Vector2(0f, 250f), new Vector2(280f, 60f), InkSoft, 18f);

            // ── Yol taşları: seyahatin anahtarı ───────────────────────────────
            // YOL TAŞI  → koşarak git, AP ve zaman normal işler (1 taş / yolculuk)
            // GÜÇLÜ TAŞ → mesafeye göre birkaç taş, ama AP ve zaman HİÇ harcanmaz
            TextMeshProUGUI roadLabel = CreateCenteredLabel(legend, "RoadStoneCount", "Yol taşı: 0",
                new Vector2(0.5f, 0f), new Vector2(0f, 200f), new Vector2(280f, 30f),
                new Color(0.42f, 0.34f, 0.22f), 20f);

            Button roadButton = CreateUIButton(legend, "Btn_RoadStone", "YOL TAŞI KULLAN",
                new Vector2(0.5f, 0f), new Vector2(0f, 158f), new Vector2(252f, 44f),
                new Color(0.26f, 0.22f, 0.34f, 0.98f), 18f);

            TextMeshProUGUI powerLabel = CreateCenteredLabel(legend, "PowerStoneCount", "Güçlü yol taşı: 0",
                new Vector2(0.5f, 0f), new Vector2(0f, 104f), new Vector2(280f, 30f),
                new Color(0.42f, 0.34f, 0.22f), 20f);

            Button powerButton = CreateUIButton(legend, "Btn_PowerStone", "GÜÇLÜ YOL TAŞI KULLAN",
                new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(252f, 44f),
                new Color(0.20f, 0.30f, 0.36f, 0.98f), 17f);

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

            WireTravelSelector(board, rawRT, travelRT, promptGO, costLabel, confirm, cancel,
                               glow, roadButton, roadLabel, powerButton, powerLabel);

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
                                               Button roadButton,  TextMeshProUGUI roadLabel,
                                               Button powerButton, TextMeshProUGUI powerLabel)
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
            so.FindProperty("_roadButton").objectReferenceValue  = roadButton;
            so.FindProperty("_roadLabel").objectReferenceValue   = roadLabel;
            so.FindProperty("_powerButton").objectReferenceValue = powerButton;
            so.FindProperty("_powerLabel").objectReferenceValue  = powerLabel;
            so.ApplyModifiedProperties();
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
