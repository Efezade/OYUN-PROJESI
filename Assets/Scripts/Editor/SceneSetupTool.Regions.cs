using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TacticalRPG.Core;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Editor
{
    /// <summary>
    /// BÖLGELER + KARA AŞI ÇÜRÜMESİ kurulumu (2026-10-10). Unity KAPALIYKEN:
    /// <code>
    /// Unity.exe -batchmode -quit -projectPath "C:\3D OYUN\OYUN" ^
    ///           -executeMethod TacticalRPG.Editor.SceneSetupTool.SetupRegionsBatch -logFile log.txt
    /// </code>
    ///
    /// Üretir (VARSA DOKUNMAZ, CLAUDE.md §9.1):
    ///   • <c>Assets/Data/Regions/Bolge_*.asset</c> — 7 bölge (varsayılanlar: KokAhdiRegions)
    ///   • <c>Assets/Data/Regions/Bolum1_BolgeSeti.asset</c>
    ///   • <c>Assets/Data/Config/CorruptionConfig.asset</c>
    /// Bağlar: Bölüm 1 kural setinin BOŞ bölge/çürüme alanlarına; sahneye çürüme yöneticisi +
    /// bölge izleyici; harita üreticisine kural seti kaynağı; kıyamet sayacına çürüme ağırlığı.
    ///
    /// TAM KURULUM'da SetupChapterRules'tan SONRA, haritanın editörde üretilmesinden ÖNCE koşar
    /// (önizleme haritası da bölgeli çıksın).
    /// </summary>
    public static partial class SceneSetupTool
    {
        private const string RegionFolder       = "Assets/Data/Regions";
        private const string Chapter1RegionSet  = RegionFolder + "/Bolum1_BolgeSeti.asset";
        private const string CorruptionCfgPath  = "Assets/Data/Config/CorruptionConfig.asset";

        // Bölge başına atmosfer (hikaye/Dokuz Hücre…md §5), giriş yazısı rengi ve görüş kuralı.
        private static readonly Dictionary<string, (string flavor, Color color, int vision)> RegionMood = new()
        {
            [KokAhdiRegions.YirtikKoru] = ("Zar'ın yırtıldığı sınır. Ağaçlar yarı kurumuş, havada hafif mor bir pus.",
                                            new Color(0.72f, 0.55f, 0.92f), 0),
            [KokAhdiRegions.HalkaKoyu] = ("Dev bir kütüğün halkaları üzerine kurulmuş köy. Kehribar fenerler hiç sönmez.",
                                            new Color(0.98f, 0.76f, 0.36f), 0),
            [KokAhdiRegions.FisiltiBatakligi] = ("Sis, Alaz ışıkları ve çığlık atan Mandragora kökleri. Burada göz yanıltır.",
                                            new Color(0.48f, 0.80f, 0.72f), -1),
            [KokAhdiRegions.UyuyanlarVadisi] = ("Dev ağaçlar ve yosun kaplı uyuyan dev heykelleri. Kodamalar fısıldar.",
                                            new Color(0.62f, 0.86f, 0.50f), 0),
            [KokAhdiRegions.OyukTepeler] = ("Ağaç kovukları ve Eski Ruhların tapınakları. Huldra'nın yurdu.",
                                            new Color(0.86f, 0.70f, 0.48f), 0),
            [KokAhdiRegions.BayterekKalbi] = ("Köklerin Zar'a değdiği yer. Gökyüzünde çatlaklar görünüyor.",
                                            new Color(0.95f, 0.50f, 0.42f), 0),
            [KokAhdiRegions.KokAhdiKorulari] = ("Ahd'e sadık korular. Ateşte dövülen, çarkla dönen hiçbir şey toprağa değmez.",
                                            new Color(0.78f, 0.86f, 0.62f), 0),
        };

        [MenuItem("TacticalRPG/Bolum - Bolgeler ve Curume Kur", false, 31)]
        public static void SetupRegionsMenu()
        {
            int n = ApplyRegions();
            EditorUtility.DisplayDialog("Bolgeler ve Kara Asi Curumesi",
                n > 0 ? "Kuruldu: 7 bolge + bolge seti + curume ayari, Bolum 1 kural setine baglandi; " +
                        "sahneye CorruptionManager + RegionPresence eklendi.\n\n" +
                        "Haritayi yeniden uretmek icin TAM KURULUM ya da Play.\nSAHNEYI KAYDET (Ctrl+S)."
                      : "Kurulamadi: sahnede ChapterProgress yok. Once TAM KURULUM.",
                "Tamam");
        }

        public static void SetupRegionsBatch()
        {
            var scene = EditorSceneManager.OpenScene(BatchScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[Bolge] Sahne acilamadi: {BatchScenePath}");
                EditorApplication.Exit(1);
                return;
            }
            TileVisualFactory.BuildAll(force: false);   // yeni bölge karolarına yer tutucu + palet girişi
            if (ApplyRegions() == 0) { EditorApplication.Exit(1); return; }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        /// <summary>TAM KURULUM zincirindeki adım (diyalogsuz).</summary>
        public static void SetupRegions() => ApplyRegions();

        private static int ApplyRegions()
        {
            var progress = FindComponentAnywhere<ChapterProgress>();
            if (progress == null)
            {
                Debug.LogError("[Bolge] Sahnede ChapterProgress yok — once 'Bolum - 8 Bolum Ilerlemesi Kur'.");
                return 0;
            }

            // ── Asset'ler ────────────────────────────────────────────────────
            var regions = new List<RegionSO>();
            foreach (RegionDef def in KokAhdiRegions.CreateDefs())
                regions.Add(EnsureRegion(def));

            RegionSetSO set = EnsureRegionSet(regions);
            CorruptionConfigSO corruption = EnsureAsset<CorruptionConfigSO>(CorruptionCfgPath);

            ChapterConfigSO chapters = progress.Config;
            ChapterRulesSO rules = chapters != null ? chapters.RulesOf(1) : null;
            if (rules == null) rules = AssetDatabase.LoadAssetAtPath<ChapterRulesSO>(Chapter1RulesPath);
            if (rules != null)
            {
                var rso = new SerializedObject(rules);
                bool dirty = false;
                if (rso.FindProperty("_regions").objectReferenceValue == null)
                { rso.FindProperty("_regions").objectReferenceValue = set; dirty = true; }
                if (rso.FindProperty("_corruption").objectReferenceValue == null)
                { rso.FindProperty("_corruption").objectReferenceValue = corruption; dirty = true; }
                if (dirty) { rso.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(rules); }
            }
            else Debug.LogWarning("[Bolge] Bolum 1 kural seti yok — once 'Bolum - Kural Seti Omurgasini Kur'.");

            // ── Sahne ────────────────────────────────────────────────────────
            var gen      = FindComponentAnywhere<ChapterMapGenerator>();
            var grid     = FindComponentAnywhere<HexGridManager>();
            var ap       = FindComponentAnywhere<ActionPointManager>();
            var state    = FindComponentAnywhere<GameStateManager>();
            var fog      = FindComponentAnywhere<FogOfWarManager>();
            var nodes    = FindComponentAnywhere<ChapterNodeManager>();
            var collapse = FindComponentAnywhere<MapCollapseManager>();
            var player   = FindComponentAnywhere<PlayerController>();

            if (gen != null)
            {
                var so = new SerializedObject(gen);
                so.FindProperty("_progress").objectReferenceValue = progress;
                so.ApplyModifiedProperties();
            }

            // Çürüme yöneticisi kıyamet sayacının yanında (çöküşün "neden"i o).
            CorruptionManager corr = null;
            GameObject host = collapse != null ? collapse.gameObject : gen != null ? gen.gameObject : null;
            if (host != null)
            {
                corr = host.GetComponent<CorruptionManager>();
                if (corr == null) corr = host.AddComponent<CorruptionManager>();
                var so = new SerializedObject(corr);
                so.FindProperty("_grid").objectReferenceValue     = grid;
                so.FindProperty("_map").objectReferenceValue      = gen;
                so.FindProperty("_ap").objectReferenceValue       = ap;
                so.FindProperty("_state").objectReferenceValue    = state;
                so.FindProperty("_fog").objectReferenceValue      = fog;
                so.FindProperty("_nodes").objectReferenceValue    = nodes;
                so.FindProperty("_progress").objectReferenceValue = progress;
                so.ApplyModifiedProperties();
            }

            if (collapse != null)
            {
                var so = new SerializedObject(collapse);
                so.FindProperty("_corruption").objectReferenceValue = corr;
                so.ApplyModifiedProperties();
            }

            // Bölge izleyici harita üreticisinin yanında.
            RegionPresence presence = null;
            if (gen != null)
            {
                presence = gen.GetComponent<RegionPresence>();
                if (presence == null) presence = gen.gameObject.AddComponent<RegionPresence>();
                var so = new SerializedObject(presence);
                so.FindProperty("_player").objectReferenceValue = player;
                so.FindProperty("_map").objectReferenceValue    = gen;
                so.FindProperty("_state").objectReferenceValue  = state;
                so.ApplyModifiedProperties();
            }

            // ── Doğrulama ────────────────────────────────────────────────────
            Debug.Log($"[Bolge] DOGRULAMA — bolge:{regions.Count} set:{(set != null ? set.Count : 0)} " +
                      $"kural-bolge:{(rules != null && rules.Regions != null)} kural-curume:{(rules != null && rules.Corruption != null)} " +
                      $"uretici:{(gen != null)} curume-yonetici:{(corr != null)} kiyamet-bagi:{(collapse != null && corr != null)} " +
                      $"bolge-izleyici:{(presence != null)} oyuncu:{(player != null)} sis:{(fog != null)} dugum:{(nodes != null)}");
            return 1;
        }

        private static RegionSO EnsureRegion(RegionDef def)
        {
            string path = $"{RegionFolder}/Bolge_{def.Id}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<RegionSO>(path);
            if (existing != null) return existing;

            RegionSO r = EnsureAsset<RegionSO>(path);
            var mood = RegionMood.TryGetValue(def.Id, out var m) ? m : ("", Color.white, 0);
            r.EditorInitFrom(def, mood.Item1, mood.Item2, mood.Item3);
            EditorUtility.SetDirty(r);
            return r;
        }

        /// <summary>Bölge seti: yoksa üretilir; listesi BOŞSA sırayla doldurulur. Doluysa dokunulmaz
        /// (Efe bölge ekleyip çıkarmış olabilir).</summary>
        private static RegionSetSO EnsureRegionSet(List<RegionSO> regions)
        {
            RegionSetSO set = EnsureAsset<RegionSetSO>(Chapter1RegionSet);
            var so = new SerializedObject(set);
            var list = so.FindProperty("_regions");
            if (list.arraySize == 0)
            {
                foreach (var r in regions) AddUnique(list, r);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(set);
            }
            return set;
        }
    }
}
