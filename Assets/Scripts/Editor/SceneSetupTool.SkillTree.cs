using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using TacticalRPG.Core;
using TacticalRPG.Data;
using TacticalRPG.Grid;
using TacticalRPG.UI;

namespace TacticalRPG.Editor
{
    /// <summary>
    /// KAM'IN YETENEK AĞACI kurulumu (2026-09-04): ayar asset'i + <see cref="KamSkillProgress"/>
    /// bileşeni + KİTAP'taki ağaç sayfası + davul/bölüm bağlantıları.
    ///
    /// GÖRSEL DİL — `Gerekli belgeler/game UI.pdf` s.6 (Efe: "profesyonel görünsün"): ağaç sayfanın
    /// ALTINDAKİ GÖVDEDEN yukarı dallanır, her yetenek ikonlu bir mürekkep dairesidir, kilitli
    /// olanlarda asma kilit durur. Çizgiler <see cref="InkArtFactory"/> ile prosedürel üretilir —
    /// Unity'nin düz sprite'ları mockup'ın el çizimi diline hiç benzemiyordu.
    ///
    /// SAYFA KİTABIN İÇİNDE (Efe'nin kararı): ayrı tam-ekran menü değil, KİTAP'ın yer imiyle açılan
    /// ikinci sayfası. (Mockup ağacı ÇANTA'ya koyuyor; taşımak istenirse tek iş sayfayı Panel_Bag'e
    /// kurmak — çizim kodu aynen çalışır.)
    ///
    /// ASSET İDEMPOTENT: `KamSkillTree.asset` VARSA dokunulmaz (CLAUDE.md §9.1).
    /// </summary>
    public static partial class SceneSetupTool
    {
        private const string SkillTreeAssetPath = "Assets/Data/Config/KamSkillTree.asset";

        // Ağacın gövdesinin dibi (KİTAP gövdesi 1500x780, merkez 0,0). Dallar buradan yukarı çıkar.
        private static readonly Vector2 SkillTrunkRoot = new(0f, -150f);

        // Künye kartı sayfanın ALTINI boydan boya kaplar; ağaç onun üstünde durur.
        private const float SkillCardHeight = 196f;
        private const float SkillCardY      = -286f;

        [MenuItem("TacticalRPG/UI - Kam Yetenek Agacini Kur", false, 28)]
        public static void SetupSkillTreeMenu()
        {
            int n = ApplySkillTree();
            EditorUtility.DisplayDialog("Kam Yetenek Agaci",
                n > 0 ? "Agac kuruldu: KamSkillTree.asset (15 dugum / 5 dal), KamSkillProgress " +
                        "bileseni, KITAP'ta YETENEK yer imi ve davul/bolum baglantilari hazir.\n\n" +
                        "SAHNEYI KAYDET (Ctrl+S)."
                      : "Kurulamadi: sahnede MenuShell_Canvas yok. Once 'UI - Menu Iskeleti Kur'.",
                "Tamam");
        }

        /// <summary>Batch girişi — Unity KAPALIYKEN kurulum + doğrulama.</summary>
        public static void SetupSkillTreeBatch()
        {
            var scene = EditorSceneManager.OpenScene(BatchScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[Yetenek] Sahne acilamadi: {BatchScenePath}");
                EditorApplication.Exit(1);
                return;
            }

            if (ApplySkillTree() == 0) { EditorApplication.Exit(1); return; }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Ortak gövde (menü + batch). 0 = kurulamadı.</summary>
        private static int ApplySkillTree()
        {
            KamSkillTreeSO tree = EnsureSkillTreeAsset();
            KamSkillProgress progress = EnsureSkillProgress(tree);

            // KABUK HER KOŞUDA YENİDEN KURULUR. Sayfa sahnede DURUYOR diye atlamak, kod
            // değiştiğinde eski çizimi bırakıyordu (2026-09-04: mürekkep sanatı hiç üretilmedi,
            // sahnede önceki koşunun düz sayfası kaldı). Kabuk zaten kendini yıkıp kuran bir
            // üretici (SetupUIShell) — tekrar koşturmak güvenli ve tek doğru davranış.
            bool prevSilent = _silentSetup;
            _silentSetup = true;                           // batch'te diyalog AÇILMAMALI (kilitler)
            try { SetupUIShell(); } finally { _silentSetup = prevSilent; }

            GameObject canvas = GameObject.Find(MenuShellCanvasName);
            Transform panel = canvas != null ? canvas.transform.Find("Panel_Book") : null;
            var view = panel != null ? panel.GetComponentInChildren<KamSkillTreeView>(true) : null;
            if (view == null)
            {
                Debug.LogError("[Yetenek] Yetenek sayfasi kurulamadi — KITAP paneli uretilemedi.");
                return 0;
            }

            var vso = new SerializedObject(view);
            vso.FindProperty("_progress").objectReferenceValue = progress;
            vso.FindProperty("_wallet").objectReferenceValue   = FindComponentAnywhere<EssenceWallet>();
            vso.ApplyModifiedProperties();

            WireSkillTreeConsumers(progress);

            int nodeCount = tree != null ? tree.Nodes.Count : 0;
            Debug.Log($"[Yetenek] DOGRULAMA — asset:{(tree != null)} dugum:{nodeCount} " +
                      $"ilerleme:{(progress != null)} sayfa:{(view != null)}");
            return 1;
        }

        /// <summary>Davul draftı ve bölüm yöneticisi ağacı tanısın (ikisi de opsiyonel alan).</summary>
        private static void WireSkillTreeConsumers(KamSkillProgress progress)
        {
            var drum = FindComponentAnywhere<CombatDrumManager>();
            if (drum != null)
            {
                var dso = new SerializedObject(drum);
                dso.FindProperty("_skillTree").objectReferenceValue = progress;
                dso.ApplyModifiedProperties();
            }

            // Ağaç ÖLÜNCE SIFIRLANIR (Efe'nin kuralı) — bağlanmazsa ilerleme sessizce taşınır.
            var run = FindComponentAnywhere<ChapterRunManager>();
            if (run != null)
            {
                var rso = new SerializedObject(run);
                rso.FindProperty("_skills").objectReferenceValue = progress;
                rso.ApplyModifiedProperties();
            }

            Debug.Log($"[Yetenek] DOGRULAMA — davul:{(drum != null)} bolum-sifirlama:{(run != null)}");
        }

        // ── Bileşen ──────────────────────────────────────────────────────────

        private static KamSkillProgress EnsureSkillProgress(KamSkillTreeSO tree)
        {
            // İlerleme, öz cüzdanıyla aynı nesnede dursun: ikisi de "run boyunca yaşayan ekonomi".
            EssenceWallet wallet = FindComponentAnywhere<EssenceWallet>();
            GameObject host = wallet != null ? wallet.gameObject : GameObject.Find(SceneRootName);
            if (host == null) return null;

            var progress = host.GetComponent<KamSkillProgress>();
            if (progress == null) progress = host.AddComponent<KamSkillProgress>();

            var so = new SerializedObject(progress);
            so.FindProperty("_tree").objectReferenceValue   = tree;
            so.FindProperty("_wallet").objectReferenceValue = wallet;
            so.ApplyModifiedProperties();
            return progress;
        }

        // ── Ayar asset'i ─────────────────────────────────────────────────────

        /// <summary>
        /// KamSkillTree.asset'i yükler; YOKSA varsayılan ağaçla üretir: BEŞ DAL (ateş · şifa · yel ·
        /// bağlama · girdap), her dalda üç basamak. Dalın ilk basamağı ön koşulsuz (hangi dala
        /// yatırım yapılacağı oyuncunun kararı), sonrakiler bir öncekine bağlı.
        ///
        /// Var olan asset EZİLMEZ — Efe maliyet/yerleşim değiştirirse TAM KURULUM silmesin.
        /// </summary>
        private static KamSkillTreeSO EnsureSkillTreeAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<KamSkillTreeSO>(SkillTreeAssetPath);
            if (existing != null) return existing;

            System.IO.Directory.CreateDirectory("Assets/Data/Config");
            var tree = ScriptableObject.CreateInstance<KamSkillTreeSO>();
            AssetDatabase.CreateAsset(tree, SkillTreeAssetPath);

            var so = new SerializedObject(tree);
            SerializedProperty nodes = so.FindProperty("_nodes");
            nodes.arraySize = 15;
            int i = 0;

            // ATEŞ DALI — sol dış. Kök: Gök Ateşi AÇIK BAŞLAR (davul her vuruşta bir büyü sunmak
            // zorunda; havuz boş kalırsa o söz sessizce bozulurdu).
            WriteNode(nodes.GetArrayElementAtIndex(i++), "gok_atesi",     "",             true,
                      new Vector2(-570f,  10f),  0,  0,  6, 4, magnitude: 3, radius: 0, push: 0, stun: 0);
            WriteNode(nodes.GetArrayElementAtIndex(i++), "kor_yagmuru",   "gok_atesi",    false,
                      new Vector2(-648f, 175f), 14,  9,  7, 5, magnitude: 4, radius: 0, push: 0, stun: 0);
            WriteNode(nodes.GetArrayElementAtIndex(i++), "yildiz_dusumu", "kor_yagmuru",  false,
                      new Vector2(-520f, 296f), 22, 14,  9, 6, magnitude: 2, radius: 1, push: 0, stun: 0);

            // ŞİFA DALI — sol iç.
            WriteNode(nodes.GetArrayElementAtIndex(i++), "umay_sifasi",   "",             false,
                      new Vector2(-292f,  44f),  8,  6,  6, 4, magnitude: 2, radius: 0, push: 0, stun: 0);
            WriteNode(nodes.GetArrayElementAtIndex(i++), "ak_sut",        "umay_sifasi",  false,
                      new Vector2(-360f, 200f), 14,  9,  7, 5, magnitude: 3, radius: 0, push: 0, stun: 0);
            WriteNode(nodes.GetArrayElementAtIndex(i++), "yasam_agaci",   "ak_sut",       false,
                      new Vector2(-236f, 302f), 22, 14,  9, 6, magnitude: 2, radius: 1, push: 0, stun: 0);

            // YEL DALI — orta.
            WriteNode(nodes.GetArrayElementAtIndex(i++), "yel_ata",       "",             false,
                      new Vector2(   4f,  76f),  8,  5,  6, 4, magnitude: 0, radius: 0, push: 1, stun: 0);
            WriteNode(nodes.GetArrayElementAtIndex(i++), "yel_kamcisi",   "yel_ata",      false,
                      new Vector2( -72f, 224f), 14,  9,  7, 5, magnitude: 0, radius: 0, push: 2, stun: 0);
            WriteNode(nodes.GetArrayElementAtIndex(i++), "boran",         "yel_kamcisi",  false,
                      new Vector2(  64f, 312f), 22, 14,  9, 6, magnitude: 0, radius: 1, push: 1, stun: 0);

            // BAĞLAMA DALI — sağ iç.
            WriteNode(nodes.GetArrayElementAtIndex(i++), "tas_kesilme",   "",             false,
                      new Vector2( 300f,  44f),  8,  6,  6, 4, magnitude: 0, radius: 0, push: 0, stun: 1);
            WriteNode(nodes.GetArrayElementAtIndex(i++), "buz_bagi",      "tas_kesilme",  false,
                      new Vector2( 224f, 200f), 14,  9,  7, 5, magnitude: 0, radius: 1, push: 0, stun: 0);
            WriteNode(nodes.GetArrayElementAtIndex(i++), "kok_zinciri",   "buz_bagi",     false,
                      new Vector2( 348f, 302f), 22, 14,  9, 6, magnitude: 0, radius: 0, push: 0, stun: 1);

            // GİRDAP DALI — sağ dış.
            WriteNode(nodes.GetArrayElementAtIndex(i++), "kara_kasirga",  "",             false,
                      new Vector2( 578f,  10f),  8,  5,  6, 4, magnitude: 0, radius: 1, push: 0, stun: 0);
            WriteNode(nodes.GetArrayElementAtIndex(i++), "girdap",        "kara_kasirga", false,
                      new Vector2( 506f, 175f), 14,  9,  7, 5, magnitude: 0, radius: 1, push: 0, stun: 0);
            WriteNode(nodes.GetArrayElementAtIndex(i++), "ruh_cagrisi",   "girdap",       false,
                      new Vector2( 628f, 296f), 22, 14,  9, 6, magnitude: 0, radius: 1, push: 0, stun: 0);

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(tree);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Yetenek] {SkillTreeAssetPath} uretildi — 15 dugum / 5 dal (sayilar TASLAK).");
            return tree;
        }

        private static void WriteNode(SerializedProperty el, string skillId, string requires,
                                      bool openAtStart, Vector2 pos,
                                      int unlockTas, int unlockDoga, int levelTas, int levelDoga,
                                      int magnitude, int radius, int push, int stun)
        {
            el.FindPropertyRelative("_skillId").stringValue          = skillId;
            el.FindPropertyRelative("_requires").stringValue         = requires;
            el.FindPropertyRelative("_unlockedAtStart").boolValue    = openAtStart;
            el.FindPropertyRelative("_maxLevel").intValue            = 3;
            el.FindPropertyRelative("_magnitudePerLevel").intValue   = magnitude;
            el.FindPropertyRelative("_radiusPerLevel").intValue      = radius;
            el.FindPropertyRelative("_pushPerLevel").intValue        = push;
            el.FindPropertyRelative("_stunPerLevel").intValue        = stun;
            el.FindPropertyRelative("_graphPos").vector2Value        = pos;

            WriteCost(el.FindPropertyRelative("_unlockCost"), unlockTas, unlockDoga);
            WriteCost(el.FindPropertyRelative("_levelCost"),  levelTas,  levelDoga);
        }

        private static void WriteCost(SerializedProperty arr, int tas, int doga)
        {
            var parts = new List<(EssenceType type, int amount)>();
            if (tas  > 0) parts.Add((EssenceType.Tas,  tas));
            if (doga > 0) parts.Add((EssenceType.Doga, doga));

            arr.arraySize = parts.Count;
            for (int i = 0; i < parts.Count; i++)
            {
                SerializedProperty el = arr.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("type").enumValueIndex = (int)parts[i].type;
                el.FindPropertyRelative("amount").intValue     = parts[i].amount;
            }
        }

        // ── KİTAP'taki ağaç sayfası ──────────────────────────────────────────

        /// <summary>
        /// Ağaç sayfasını çizer: başlık · gövde + dallar (TEK mürekkep dokusu) · ikonlu düğümler ·
        /// sayfanın altında künye kartı (ad · açıklama · bedel · AÇ/YÜKSELT · kese).
        /// </summary>
        private static void PopulateSkillPage(Transform page, GameObject panelGO)
        {
            KamSkillTreeSO tree = EnsureSkillTreeAsset();

            // ── Başlık: mockup'taki gibi avuç içi ikonu + el yazısı ───────────
            var titleRow = new GameObject("SkillTitle", typeof(RectTransform));
            titleRow.transform.SetParent(page, false);
            // Başlık SOL ÜSTTE: ortada dururken orta daldaki üst düğümün (Boran) üstüne biniyordu.
            var trt = titleRow.GetComponent<RectTransform>();
            trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0f, 1f);
            trt.anchoredPosition = new Vector2(34f, -16f);
            trt.sizeDelta = new Vector2(460f, 56f);

            InkImage(titleRow.transform, "TitleIcon", InkArtFactory.Icon(InkIcon.Hand, 64),
                     new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(44f, 44f), Ink);
            var titleText = CreateCenteredLabel(titleRow.transform, "TitleText", "YETENEK AĞACI",
                new Vector2(0f, 0.5f), new Vector2(72f, 0f), new Vector2(380f, 50f), Ink, 32f);
            titleText.alignment = TMPro.TextAlignmentOptions.Left;

            // ── Dallar: TEK doku (kesişimler kopuk görünmesin) ────────────────
            var edges = new List<(Vector2 from, Vector2 to)>();
            var rootChildren = new List<Vector2>();
            foreach (KamSkillTreeSO.Node n in tree.Nodes)
            {
                if (n == null) continue;
                if (string.IsNullOrEmpty(n.Requires)) { rootChildren.Add(n.GraphPos); continue; }
                KamSkillTreeSO.Node parent = tree.Find(n.Requires);
                if (parent != null) edges.Add((parent.GraphPos, n.GraphPos));
            }

            const int texW = 1500, texH = 780;
            Vector2 ToTex(Vector2 p) => new(p.x + texW * 0.5f, p.y + texH * 0.5f);

            var texEdges = new List<(Vector2, Vector2)>();
            foreach (var e in edges) texEdges.Add((ToTex(e.from), ToTex(e.to)));
            var texRoots = new List<Vector2>();
            foreach (Vector2 c in rootChildren) texRoots.Add(ToTex(c));

            // Doku adı YERLEŞİMDEN türer: düğümleri kaydırınca dallar da yeniden üretilsin
            // (aynı yerleşimde ise diskteki dosya korunur, üretim tekrarlanmaz).
            string branchName = $"tree_branches_{LayoutHash(tree)}";
            Sprite branches = InkArtFactory.Branches(branchName, texW, texH,
                                                     ToTex(SkillTrunkRoot), texEdges, texRoots);
            InkImage(page, "Branches", branches, new Vector2(0.5f, 0.5f), Vector2.zero,
                     new Vector2(texW, texH), Ink);

            // ── Düğümler ──────────────────────────────────────────────────────
            Sprite disc     = InkArtFactory.Disc("node_disc_92", 92);
            Sprite ring     = InkArtFactory.Node("node_ring_104", 104);
            Sprite lockIcon = InkArtFactory.Icon(InkIcon.Lock, 64);

            var views = new List<SkillNodeParts>();
            foreach (KamSkillTreeSO.Node n in tree.Nodes)
            {
                if (n == null) continue;
                views.Add(CreateSkillNode(page, n, disc, ring, lockIcon));
            }

            // ── Künye kartı (sayfanın altı, boydan boya) ──────────────────────
            RectTransform card = InkPanel(page, "SkillCard", new Vector2(0.5f, 0.5f),
                new Vector2(0f, SkillCardY), new Vector2(1360f, SkillCardHeight), 22);

            var detailName = CreateCenteredLabel(card, "DetailName", "—",
                new Vector2(0f, 1f), new Vector2(28f, -12f), new Vector2(520f, 44f), Ink, 29f);
            detailName.alignment = TextAlignmentOptions.TopLeft;

            var detailBody = CreateCenteredLabel(card, "DetailBody", "",
                new Vector2(0f, 1f), new Vector2(28f, -58f), new Vector2(800f, 118f), InkSoft, 20f);
            detailBody.alignment = TextAlignmentOptions.TopLeft;

            var detailCost = CreateCenteredLabel(card, "DetailCost", "",
                new Vector2(1f, 1f), new Vector2(-320f, -14f), new Vector2(440f, 66f), Ink, 22f);
            detailCost.alignment = TextAlignmentOptions.TopRight;

            var walletLabel = CreateCenteredLabel(card, "WalletLine", "",
                new Vector2(1f, 0f), new Vector2(-320f, 14f), new Vector2(440f, 38f), InkSoft, 20f);
            walletLabel.alignment = TextAlignmentOptions.BottomRight;

            Button action = InkButton(card, "Btn_Advance", "AÇ", new Vector2(1f, 0.5f),
                                      new Vector2(-28f, 0f), new Vector2(258f, 82f));
            var actionLabel = action.GetComponentInChildren<TextMeshProUGUI>();

            // ── Görünümü bağla ────────────────────────────────────────────────
            var view = panelGO.GetComponent<KamSkillTreeView>();
            if (view == null) view = panelGO.AddComponent<KamSkillTreeView>();

            var vso = new SerializedObject(view);
            SerializedProperty arr = vso.FindProperty("_nodes");
            arr.arraySize = views.Count;
            for (int i = 0; i < views.Count; i++)
            {
                SerializedProperty el = arr.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("_skillId").stringValue             = views[i].SkillId;
                el.FindPropertyRelative("_button").objectReferenceValue     = views[i].Button;
                el.FindPropertyRelative("_disc").objectReferenceValue       = views[i].Disc;
                el.FindPropertyRelative("_ring").objectReferenceValue       = views[i].Ring;
                el.FindPropertyRelative("_icon").objectReferenceValue       = views[i].Icon;
                el.FindPropertyRelative("_lockedIcon").objectReferenceValue = views[i].LockedIcon;
                el.FindPropertyRelative("_openIcon").objectReferenceValue   = views[i].OpenIcon;
                el.FindPropertyRelative("_nameLabel").objectReferenceValue  = views[i].NameLabel;
                el.FindPropertyRelative("_levelLabel").objectReferenceValue = views[i].LevelLabel;
                el.FindPropertyRelative("_levelBadge").objectReferenceValue = views[i].LevelBadge;
            }

            vso.FindProperty("_detailName").objectReferenceValue   = detailName;
            vso.FindProperty("_detailBody").objectReferenceValue   = detailBody;
            vso.FindProperty("_detailCost").objectReferenceValue   = detailCost;
            vso.FindProperty("_walletLabel").objectReferenceValue  = walletLabel;
            vso.FindProperty("_actionButton").objectReferenceValue = action;
            vso.FindProperty("_actionLabel").objectReferenceValue  = actionLabel;
            vso.FindProperty("_progress").objectReferenceValue = EnsureSkillProgress(tree);
            vso.FindProperty("_wallet").objectReferenceValue   = FindComponentAnywhere<EssenceWallet>();
            vso.ApplyModifiedProperties();

            WireSkillTreeConsumers(EnsureSkillProgress(tree));
        }

        /// <summary>Bir düğümün sahnedeki parçaları (görünüme bağlanır).</summary>
        private class SkillNodeParts
        {
            public string SkillId;
            public Button Button;
            public Image  Disc, Ring, Icon, LevelBadge;
            public Sprite LockedIcon, OpenIcon;
            public TextMeshProUGUI NameLabel, LevelLabel;
        }

        /// <summary>Tek düğüm: durum diski + mürekkep halka + ikon + ad + seviye rozeti.</summary>
        private static SkillNodeParts CreateSkillNode(Transform parent, KamSkillTreeSO.Node node,
                                                      Sprite disc, Sprite ring, Sprite lockIcon)
        {
            KamSkillCatalog.Entry entry = node.Catalog;
            string label = entry != null ? entry.Name : node.SkillId;

            var go = new GameObject($"Node_{node.SkillId}", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = node.GraphPos;
            rt.sizeDelta = new Vector2(150f, 150f);

            var parts = new SkillNodeParts { SkillId = node.SkillId, LockedIcon = lockIcon };

            parts.Disc = InkImage(go.transform, "Disc", disc, new Vector2(0.5f, 0.5f),
                                  Vector2.zero, new Vector2(92f, 92f), ParchmentHi, raycast: true);
            parts.Button = parts.Disc.gameObject.AddComponent<Button>();
            parts.Button.targetGraphic = parts.Disc;

            parts.Ring = InkImage(go.transform, "Ring", ring, new Vector2(0.5f, 0.5f),
                                  Vector2.zero, new Vector2(104f, 104f), Ink);

            parts.OpenIcon = InkArtFactory.Icon(IconFor(entry), 64);
            parts.Icon = InkImage(go.transform, "Icon", lockIcon, new Vector2(0.5f, 0.5f),
                                  Vector2.zero, new Vector2(50f, 50f), Ink);

            // Seviye rozeti — küçük disk, yalnız AÇIK düğümlerde görünür.
            parts.LevelBadge = InkImage(go.transform, "LevelBadge", disc, new Vector2(0.5f, 0.5f),
                                        new Vector2(36f, -34f), new Vector2(40f, 40f), ParchmentLo);
            parts.LevelLabel = CreateCenteredLabel(parts.LevelBadge.transform, "LevelText", "1/3",
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46f, 26f), Ink, 17f);

            // Adın ARKASINA kâğıt şerit: dallar yazının üstünden geçince ad okunmuyordu.
            InkImage(go.transform, "NamePlate", InkArtFactory.Paper("paper_soft", 96, 96, Color.white),
                     new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(196f, 30f),
                     new Color(ParchmentHi.r, ParchmentHi.g, ParchmentHi.b, 0.88f)).type = Image.Type.Sliced;

            parts.NameLabel = CreateCenteredLabel(go.transform, "Name", label,
                new Vector2(0.5f, 0f), new Vector2(0f, -4f), new Vector2(210f, 34f), Ink, 19f);

            return parts;
        }

        /// <summary>Büyünün etkisine göre mürekkep ikonu (kilitliyken asma kilit çizilir).</summary>
        private static InkIcon IconFor(KamSkillCatalog.Entry e)
            => e == null ? InkIcon.Star : e.Effect switch
            {
                KamSkillEffect.Meteor  => InkIcon.Flame,
                KamSkillEffect.Heal    => InkIcon.Drop,
                KamSkillEffect.Push    => InkIcon.Wind,
                KamSkillEffect.Petrify => InkIcon.Shield,
                KamSkillEffect.Pull    => InkIcon.Spiral,
                _                      => InkIcon.Star
            };

        /// <summary>Yerleşim parmak izi: düğüm konumları değişince dal dokusu yeniden üretilsin.</summary>
        private static string LayoutHash(KamSkillTreeSO tree)
        {
            unchecked
            {
                int h = 17;
                foreach (KamSkillTreeSO.Node n in tree.Nodes)
                {
                    if (n == null || n.SkillId == null) continue;
                    h = h * 31 + n.SkillId.GetHashCode();
                    h = h * 31 + Mathf.RoundToInt(n.GraphPos.x) * 7 + Mathf.RoundToInt(n.GraphPos.y);
                }
                return (h & 0x7FFFFF).ToString("x");
            }
        }
    }
}
