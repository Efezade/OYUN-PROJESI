using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using TacticalRPG.Core;
using TacticalRPG.Data;
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
    ///  • HARİTA — çerçeveli harita + sol PINS paneli (HAN/ŞİFACI/MARKET) + **8 bölümlük ilerleme yolu**
    ///    (map pin olarak, <see cref="WorldMapView"/> ile <see cref="ChapterProgress"/>'e CANLI) + pusula.
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

        private static void PopulateMapScreen(GameObject panelGO)
        {
            Transform t = panelGO.transform;

            RectTransform map = FramedPanel(t, "MapBody", new Vector2(0.5f, 0.5f),
                new Vector2(60f, -6f), new Vector2(1360f, 710f), 12f);

            // İç çerçeve (çift kenar hissi) — 4 ince mürekkep çizgi
            InnerBorder(map, 22f, InkSoft);

            // ── Sol PINS paneli (dışa taşan parşömen) ──────────────────────────
            RectTransform pins = FramedPanel(map, "PinsPanel", new Vector2(0f, 0.5f),
                new Vector2(-70f, 40f), new Vector2(300f, 440f), 10f, ParchmentHi, FrameDark);
            SectionHeader(pins, "PinsHeader", "PINS", new Vector2(0.5f, 1f), new Vector2(0f, -14f), 200f, 32f);
            string[] svc = { "HAN", "ŞİFACI", "MARKET" };
            for (int i = 0; i < svc.Length; i++)
            {
                float y = 90f - i * 96f;
                Circle(pins, "Pin_" + svc[i], new Vector2(0f, 0.5f), new Vector2(48f, y), 44f, new Color(0.66f, 0.26f, 0.22f));
                CreateCenteredLabel(pins, "SvcLbl_" + svc[i], svc[i], new Vector2(0f, 0.5f),
                    new Vector2(110f, y), new Vector2(190f, 40f), Ink, 26f);
            }

            // ── 8 BÖLÜMLÜK İLERLEME YOLU (1 bölüm = 1 harita, GAME_DESIGN.md §0) ─
            // Yılan yol: üst sıra 1-2-3-4 (soldan sağa), sağdan aşağı, alt sıra 5-6-7-8 (sağdan sola).
            // (Eski 3×3 snake dünya buradaydı — TASK-004 ile alternatife alındı, bkz
            //  Docs/Alternatif_Tasarimlar/3x3_Dunya_Haritasi/.)
            TextMeshProUGUI title = CreateCenteredLabel(map, "ChapterTitle", "Bölüm 1",
                new Vector2(0.5f, 0.5f), new Vector2(160f, 282f), new Vector2(900f, 52f), Ink, 34f);

            const float node = 110f;
            var nodeBgs    = new Image[ChapterCount];
            var nodeLabels = new TextMeshProUGUI[ChapterCount];
            for (int c = 1; c <= ChapterCount; c++)
                nodeBgs[c - 1] = CreateChapterNode(map, c, ChapterNodePos(c), node, out nodeLabels[c - 1]);

            // Düğümleri bağlayan yol parçaları: index 0 = 1→2 … 6 = 7→8
            var connectors = new Image[ChapterCount - 1];
            for (int c = 1; c < ChapterCount; c++)
            {
                Vector2 a = ChapterNodePos(c), b = ChapterNodePos(c + 1);
                Vector2 mid = (a + b) * 0.5f;
                bool horizontal = Mathf.Abs(a.y - b.y) < 0.5f;
                Vector2 size = horizontal
                    ? new Vector2(Mathf.Abs(b.x - a.x) - node, 7f)
                    : new Vector2(7f, Mathf.Abs(b.y - a.y) - node);
                connectors[c - 1] = Line(map, $"Path_{c}_{c + 1}", new Vector2(0.5f, 0.5f), mid, size, InkSoft);
                connectors[c - 1].transform.SetAsFirstSibling();   // yol, düğümlerin ALTINDA kalsın
            }

            // ── Pusula (sağ alt) ───────────────────────────────────────────────
            CreateCompass(map, new Vector2(500f, -250f), 140f);

            WorldMapView view = panelGO.AddComponent<WorldMapView>();
            ChapterProgress progress = FindComponentAnywhere<ChapterProgress>();
            if (progress == null)
                Debug.LogWarning("[HARİTA] Sahnede ChapterProgress yok — ekran boş/varsayılan görünür. " +
                                 "Once 'TacticalRPG → Bolum - 8 Bolum Ilerlemesi Kur' calistir (ya da TAM KURULUM).");
            var vso = new SerializedObject(view);
            vso.FindProperty("_progress").objectReferenceValue    = progress;
            vso.FindProperty("_titleLabel").objectReferenceValue  = title;
            FillObjectArray(vso, "_nodeBackgrounds", nodeBgs);
            FillObjectArray(vso, "_nodeLabels",      nodeLabels);
            FillObjectArray(vso, "_connectors",      connectors);
            vso.ApplyModifiedProperties();

            CreateCenteredLabel(t, "MapHint",
                "8 bölüm — her bölüm kendi haritası ve temalı elementi · bulunduğun bölüm CANLI · Kapat: Esc",
                new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(1400f, 40f),
                new Color(0.62f, 0.57f, 0.48f), 24f);
        }

        /// <summary>Toplam bölüm sayısı (GAME_DESIGN.md §3). UI yerleşimi bu sayıya göre kurulur.</summary>
        private const int ChapterCount = 8;

        /// <summary>Bölüm düğümünün HARİTA gövdesindeki yeri — yılan yol: üst 1-2-3-4, alt 8-7-6-5.</summary>
        private static Vector2 ChapterNodePos(int chapter)
        {
            const float cx = 160f, gapX = 240f, topY = 130f, botY = -60f;
            int   col = chapter <= 4 ? chapter - 1 : 8 - chapter;   // alt sıra sağdan sola
            float x   = cx + (col - 1.5f) * gapX;
            return new Vector2(x, chapter <= 4 ? topY : botY);
        }

        /// <summary>Bir bölüm düğümü = daire "map pin" + numara + altında durum yazısı.
        /// Renkleri/yazıları <see cref="WorldMapView"/> canlı olarak günceller.</summary>
        private static Image CreateChapterNode(Transform parent, int chapter, Vector2 pos, float size,
                                               out TextMeshProUGUI stateLabel)
        {
            Circle(parent, "NodeRing_" + chapter, new Vector2(0.5f, 0.5f), pos, size + 10f, FrameDark);
            Image bg = Circle(parent, "Node_" + chapter, new Vector2(0.5f, 0.5f), pos, size,
                new Color(0.83f, 0.75f, 0.58f, 1f));
            CreateCenteredLabel(bg.transform, "Num", chapter.ToString(),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size), Ink, 46f);
            stateLabel = CreateCenteredLabel(parent, "State_" + chapter, "",
                new Vector2(0.5f, 0.5f), new Vector2(pos.x, pos.y - size * 0.5f - 24f),
                new Vector2(190f, 30f), InkSoft, 20f);
            return bg;
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
