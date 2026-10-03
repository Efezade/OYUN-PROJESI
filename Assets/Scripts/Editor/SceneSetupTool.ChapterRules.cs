using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TacticalRPG.Core;
using TacticalRPG.Data;

namespace TacticalRPG.Editor
{
    /// <summary>
    /// FAZ 2 OMURGASI + FAZ 1 + YÜRÜYÜŞ (2026-10-03) kurulumu. Unity KAPALIYKEN:
    /// <code>
    /// Unity.exe -batchmode -quit -projectPath "C:\3D OYUN\OYUN" ^
    ///           -executeMethod TacticalRPG.Editor.SceneSetupTool.SetupChapterRulesBatch -logFile log.txt
    /// </code>
    ///
    /// Üretir (VARSA DOKUNMAZ, CLAUDE.md §9.1):
    ///   • <c>Assets/Data/Rules/Mekanik_Mana.asset</c>   — ilk Kam mekaniği (10 mana, dilim başı +2)
    ///   • <c>Assets/Data/Rules/Bolum1_Kurallar.asset</c> — 1. bölümün kural seti
    ///   • <c>Assets/Data/Quests/GorevTipi_Savas.asset</c> + <c>GorevTipi_Adak.asset</c>
    /// Bağlar: bölüm listesinin 1. girişine kural seti (boşsa), sahnedeki tüketicilere
    /// <see cref="ChapterProgress"/>, adak runner'ı, otomatik yürüyüş durdurucu.
    ///
    /// TAM KURULUM zincirinde SetupStore'dan sonra koşar (ChapterProgress ve yetenek ağacı o
    /// noktada sahnede). Ayrı menü/batch yalnız "tam kurulum koşturmadan sahne güncel olsun" içindir.
    /// </summary>
    public static partial class SceneSetupTool
    {
        private const string RulesFolder       = "Assets/Data/Rules";
        private const string QuestKindFolder   = "Assets/Data/Quests";
        private const string ManaMechanicPath  = RulesFolder + "/Mekanik_Mana.asset";
        private const string Chapter1RulesPath = RulesFolder + "/Bolum1_Kurallar.asset";

        [MenuItem("TacticalRPG/Bolum - Kural Seti Omurgasini Kur", false, 30)]
        public static void SetupChapterRulesMenu()
        {
            int n = ApplyChapterRules();
            EditorUtility.DisplayDialog("Kural Seti Omurgasi",
                n > 0 ? "Kuruldu: Mekanik_Mana, Bolum1_Kurallar, gorev tipleri (Savas/Adak), " +
                        "adak runner'i ve otomatik yuruyus durdurucu.\n\nSAHNEYI KAYDET (Ctrl+S)."
                      : "Kurulamadi: sahnede ChapterProgress yok. Once TAM KURULUM.",
                "Tamam");
        }

        public static void SetupChapterRulesBatch()
        {
            var scene = EditorSceneManager.OpenScene(BatchScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[Kural] Sahne acilamadi: {BatchScenePath}");
                EditorApplication.Exit(1);
                return;
            }

            if (ApplyChapterRules() == 0) { EditorApplication.Exit(1); return; }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        /// <summary>TAM KURULUM zincirindeki adım (diyalogsuz).</summary>
        public static void SetupChapterRules() => ApplyChapterRules();

        /// <summary>Ortak gövde. 0 = kurulamadı.</summary>
        private static int ApplyChapterRules()
        {
            var progress = FindComponentAnywhere<ChapterProgress>();
            if (progress == null)
            {
                Debug.LogError("[Kural] Sahnede ChapterProgress yok — once 'Bolum - 8 Bolum Ilerlemesi Kur'.");
                return 0;
            }

            // ── Asset'ler ────────────────────────────────────────────────────
            ManaMechanicSO     mana   = EnsureManaMechanic();
            CombatQuestKindSO  combat = EnsureQuestKind<CombatQuestKindSO>("GorevTipi_Savas", "Savaş",
                "Görev alanındaki düşmanları yen. Kam düşerse bölüm kaybedilir.");
            OfferingQuestKindSO offering = EnsureQuestKind<OfferingQuestKindSO>("GorevTipi_Adak", "Adak",
                "Savaş yok: görev karosunda istenen özü ada, harita kurtulsun.");
            MandatoryQuestConfigSO questCfg = EnsureMandatoryQuestConfig();
            SeedQuestKinds(questCfg, combat);

            KamSkillTreeSO tree = AssetDatabase.LoadAssetAtPath<KamSkillTreeSO>(SkillTreeAssetPath);
            ChapterRulesSO rules = EnsureChapter1Rules(mana, questCfg, tree);

            ChapterConfigSO chapters = progress.Config != null ? progress.Config : EnsureChapterConfig();
            ChapterConfigSO.ChapterEntry first = chapters.Get(1);
            if (first != null && first.rules == null)
            {
                first.rules = rules;
                EditorUtility.SetDirty(chapters);
            }

            // ── Sahne ────────────────────────────────────────────────────────
            var ap      = FindComponentAnywhere<ActionPointManager>();
            var gen     = FindComponentAnywhere<ChapterMapGenerator>();
            var wallet  = FindComponentAnywhere<EssenceWallet>();
            var player  = FindComponentAnywhere<PlayerController>();
            var host    = FindComponentAnywhere<KamMechanicHost>();
            var nodes   = FindComponentAnywhere<ChapterNodeManager>();
            var quests  = FindComponentAnywhere<MandatoryQuestDirector>();
            var run     = FindComponentAnywhere<ChapterRunManager>();
            var skills  = FindComponentAnywhere<KamSkillProgress>();

            if (host != null)
            {
                var so = new SerializedObject(host);
                so.FindProperty("_progress").objectReferenceValue = progress;
                so.FindProperty("_map").objectReferenceValue      = gen;
                if (so.FindProperty("_apManager").objectReferenceValue == null)
                    so.FindProperty("_apManager").objectReferenceValue = ap;
                if (so.FindProperty("_fallbackMechanic").objectReferenceValue == null)
                    so.FindProperty("_fallbackMechanic").objectReferenceValue = mana;
                so.ApplyModifiedProperties();
            }

            if (quests != null)
            {
                var so = new SerializedObject(quests);
                so.FindProperty("_progress").objectReferenceValue = progress;
                so.FindProperty("_wallet").objectReferenceValue   = wallet;
                so.ApplyModifiedProperties();
            }

            OfferingQuestRunner offeringRunner = null;
            if (nodes != null)
            {
                offeringRunner = nodes.GetComponent<OfferingQuestRunner>();
                if (offeringRunner == null) offeringRunner = nodes.gameObject.AddComponent<OfferingQuestRunner>();
                var rso = new SerializedObject(offeringRunner);
                rso.FindProperty("_wallet").objectReferenceValue = wallet;
                rso.ApplyModifiedProperties();

                var so = new SerializedObject(nodes);
                so.FindProperty("_progress").objectReferenceValue = progress;
                AddUnique(so.FindProperty("_questRunners"), offeringRunner);
                so.ApplyModifiedProperties();
            }

            if (run != null)
            {
                var so = new SerializedObject(run);
                so.FindProperty("_progress").objectReferenceValue = progress;
                so.ApplyModifiedProperties();
            }

            if (skills != null)
            {
                var so = new SerializedObject(skills);
                so.FindProperty("_progress").objectReferenceValue = progress;
                so.ApplyModifiedProperties();
            }

            // Otomatik yürüyüş durdurucu — zincir yöneticisiyle aynı nesnede (olaylarının kaynağı).
            WalkInterruptor interruptor = null;
            if (quests != null)
            {
                interruptor = quests.GetComponent<WalkInterruptor>();
                if (interruptor == null) interruptor = quests.gameObject.AddComponent<WalkInterruptor>();
                var so = new SerializedObject(interruptor);
                so.FindProperty("_player").objectReferenceValue = player;
                so.FindProperty("_quests").objectReferenceValue = quests;
                so.FindProperty("_ap").objectReferenceValue     = ap;
                so.ApplyModifiedProperties();
            }

            // ── Doğrulama: sessizce yarım kalıp "tamam" demesin ──────────────
            Debug.Log($"[Kural] DOGRULAMA — bolum1-kural:{(chapters.RulesOf(1) != null)} " +
                      $"mekanik:{(rules.KamMechanic != null)} zincir:{(rules.QuestChain != null)} " +
                      $"agac:{(rules.SkillTree != null)} gorev-tipi[1]:{(questCfg.KindForTier(1) != null ? questCfg.KindForTier(1).name : "savas(bos)")}");
            Debug.Log($"[Kural] DOGRULAMA — host:{(host != null)} yonetici:{(quests != null)} " +
                      $"dugum:{(nodes != null)} adak-runner:{(offeringRunner != null)} bolum:{(run != null)} " +
                      $"agac-ilerleme:{(skills != null)} yuruyus-durdurucu:{(interruptor != null)} " +
                      $"cuzdan:{(wallet != null)} oyuncu:{(player != null)}");
            return 1;
        }

        // ── Asset yardımcıları (hepsi idempotent) ────────────────────────────

        /// <summary>Mana mekaniği asset'i — yoksa sınıf varsayılanlarıyla (10 / +2) üretilir.
        /// Faz 2 kurulumu da (yedek mekanik olarak) bunu kullanır.</summary>
        private static ManaMechanicSO EnsureManaMechanic() => EnsureAsset<ManaMechanicSO>(ManaMechanicPath);

        private static T EnsureQuestKind<T>(string file, string displayName, string description)
            where T : QuestKindSO
        {
            string path = $"{QuestKindFolder}/{file}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            T kind = EnsureAsset<T>(path);
            var so = new SerializedObject(kind);
            so.FindProperty("_displayName").stringValue = displayName;
            so.FindProperty("_description").stringValue = description;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(kind);
            return kind;
        }

        /// <summary>Zincirin görev tipi listesi BOŞSA tek elemanlı [Savaş] yapılır — davranış
        /// aynı kalır ama Efe Inspector'da slotu görür ve Adak'ı sürükleyebilir. Doluysa dokunulmaz.</summary>
        private static void SeedQuestKinds(MandatoryQuestConfigSO cfg, QuestKindSO combat)
        {
            var so = new SerializedObject(cfg);
            var kinds = so.FindProperty("_questKinds");
            if (kinds == null || kinds.arraySize > 0) return;
            kinds.arraySize = 1;
            kinds.GetArrayElementAtIndex(0).objectReferenceValue = combat;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(cfg);
        }

        private static ChapterRulesSO EnsureChapter1Rules(KamMechanicSO mechanic, MandatoryQuestConfigSO chain,
                                                          KamSkillTreeSO tree)
        {
            var existing = AssetDatabase.LoadAssetAtPath<ChapterRulesSO>(Chapter1RulesPath);
            if (existing != null) return existing;

            ChapterRulesSO rules = EnsureAsset<ChapterRulesSO>(Chapter1RulesPath);
            var so = new SerializedObject(rules);
            so.FindProperty("_kamMechanic").objectReferenceValue = mechanic;
            so.FindProperty("_questChain").objectReferenceValue  = chain;
            so.FindProperty("_skillTree").objectReferenceValue   = tree;
            so.FindProperty("_designNote").stringValue =
                "Bölüm 1 — Taş & Doğa. Öğretici map: Kam klasik mana kullanır, zincir 2 görevle " +
                "açılır (gün 5/8/11 + ekonomi eşikleri). Bugüne kadarki davranışın birebir aynısı.";
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rules);
            return rules;
        }

        private static void AddUnique(SerializedProperty list, Object item)
        {
            if (list == null || item == null) return;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == item) return;
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = item;
        }
    }
}
