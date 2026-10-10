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

            // Bildirim akışı + pusu başlatıcı + bölge kuralları tek nesnede (harita üreticisinin çocuğu).
            GameObject rulesGo = null;
            if (gen != null)
            {
                Transform t = gen.transform.Find("BolgeKurallari");
                rulesGo = t != null ? t.gameObject : new GameObject("BolgeKurallari");
                rulesGo.transform.SetParent(gen.transform, false);
            }
            NoticeFeed     notice = rulesGo != null ? Ensure<NoticeFeed>(rulesGo) : null;
            AmbushLauncher ambush = rulesGo != null ? Ensure<AmbushLauncher>(rulesGo) : null;
            if (ambush != null)
            {
                var so = new SerializedObject(ambush);
                Set(so, "_state", state); Set(so, "_player", player); Set(so, "_notice", notice);
                if (so.FindProperty("_zoom").objectReferenceValue == null)
                    Set(so, "_zoom", Object.FindFirstObjectByType<TacticalRPG.UI.CameraZoomSettings>());
                so.ApplyModifiedProperties();
            }

            // Çürüme yöneticisi + görüntüsü kıyamet sayacının yanında (çöküşün "neden"i o).
            CorruptionManager corr = null;
            CorruptionVisuals corrFx = null;
            GameObject host = collapse != null ? collapse.gameObject : gen != null ? gen.gameObject : null;
            if (host != null)
            {
                corr = Ensure<CorruptionManager>(host);
                var so = new SerializedObject(corr);
                Set(so, "_grid", grid); Set(so, "_map", gen); Set(so, "_ap", ap); Set(so, "_state", state);
                Set(so, "_nodes", nodes); Set(so, "_progress", progress); Set(so, "_player", player);
                Set(so, "_notice", notice);
                so.ApplyModifiedProperties();

                corrFx = Ensure<CorruptionVisuals>(host);
                var vso = new SerializedObject(corrFx);
                Set(vso, "_corruption", corr); Set(vso, "_grid", grid); Set(vso, "_fog", fog);
                Set(vso, "_state", state); Set(vso, "_player", player); Set(vso, "_collapse", collapse);
                vso.ApplyModifiedProperties();
            }

            if (collapse != null)
            {
                var so = new SerializedObject(collapse);
                Set(so, "_corruption", corr);
                so.ApplyModifiedProperties();
            }

            // Kara Öz güçlendirmesi: düşman doğurucu çürüme kademesini okur.
            var spawner = FindComponentAnywhere<EnemySpawner>();
            if (spawner != null)
            {
                var so = new SerializedObject(spawner);
                Set(so, "_corruption", corr);
                so.ApplyModifiedProperties();
            }

            // Bölge izleyici harita üreticisinin yanında.
            RegionPresence presence = null;
            if (gen != null)
            {
                presence = Ensure<RegionPresence>(gen.gameObject);
                var so = new SerializedObject(presence);
                Set(so, "_player", player); Set(so, "_map", gen); Set(so, "_state", state);
                so.ApplyModifiedProperties();
            }

            // ── Bölge kuralları ──────────────────────────────────────────────
            MissionData huntAmbush = EnsureAmbushMission("Pusu_AvBirligi", "Av Birliği",
                "Gözcü Kuzgun'un haber verdiği Morvhal av birliği seni yakaladı.", MapNodeType.Encounter,
                ("Goblin", 2), ("Goblin", 1), ("GoblinSaman", 1));
            MissionData wakeAmbush = EnsureAmbushMission("Pusu_UyananDev", "Uyanan Dev",
                "Gürültün uyuyan devi uyandırdı. Acı içinde öfkeli.", MapNodeType.Zindan,
                ("Yamyam", 3), ("Goblin", 2), ("Goblin", 2));

            RegionSO R(string id) => regions.Find(r => r != null && r.Id == id);
            var wallet = FindComponentAnywhere<EssenceWallet>();
            int mechanics = 0;
            if (rulesGo != null)
            {
                var raven = Ensure<RavenWatch>(rulesGo);
                var rso = WireMechanic(raven, R(KokAhdiRegions.YirtikKoru), gen, grid, player, state, fog, ap, notice, nodes);
                Set(rso, "_huntAmbush", huntAmbush); Set(rso, "_ambush", ambush); Set(rso, "_safeRegion", R(KokAhdiRegions.HalkaKoyu));
                if (rso.FindProperty("_hunterClass").objectReferenceValue == null)
                    Set(rso, "_hunterClass", AssetDatabase.LoadAssetAtPath<CharacterClassData>("Assets/Data/Characters/Goblin.asset"));
                rso.ApplyModifiedProperties(); mechanics++;

                var wisp = Ensure<WispLights>(rulesGo);
                var wso = WireMechanic(wisp, R(KokAhdiRegions.FisiltiBatakligi), gen, grid, player, state, fog, ap, notice, nodes);
                Set(wso, "_wallet", wallet); Set(wso, "_progress", progress);
                wso.ApplyModifiedProperties(); mechanics++;

                var sleep = Ensure<SleeperWatch>(rulesGo);
                var sso = WireMechanic(sleep, R(KokAhdiRegions.UyuyanlarVadisi), gen, grid, player, state, fog, ap, notice, nodes);
                Set(sso, "_wakeAmbush", wakeAmbush); Set(sso, "_ambush", ambush);
                sso.ApplyModifiedProperties(); mechanics++;

                var hollow = Ensure<HollowTunnels>(rulesGo);
                var hso = WireMechanic(hollow, R(KokAhdiRegions.OyukTepeler), gen, grid, player, state, fog, ap, notice, nodes);
                hso.ApplyModifiedProperties(); mechanics++;
            }

            // ── Doğrulama ────────────────────────────────────────────────────
            Debug.Log($"[Bolge] DOGRULAMA — bolge:{regions.Count} set:{(set != null ? set.Count : 0)} " +
                      $"kural-bolge:{(rules != null && rules.Regions != null)} kural-curume:{(rules != null && rules.Corruption != null)} " +
                      $"uretici:{(gen != null)} curume-yonetici:{(corr != null)} curume-gorsel:{(corrFx != null)} " +
                      $"kiyamet-bagi:{(collapse != null && corr != null)} kara-oz-savas:{(spawner != null && corr != null)} " +
                      $"bolge-izleyici:{(presence != null)} bildirim:{(notice != null)} pusu:{(ambush != null)} " +
                      $"bolge-kurali:{mechanics}/4 pusu-gorev:{(huntAmbush != null)}/{(wakeAmbush != null)} " +
                      $"oyuncu:{(player != null)} sis:{(fog != null)} dugum:{(nodes != null)}");
            return 1;
        }

        private static T Ensure<T>(GameObject go) where T : Component
        {
            // "??" KULLANMA: GetComponent sahte null dönebilir (DECISION_LOG tuzaklar).
            T c = go.GetComponent<T>();
            if (c == null) c = go.AddComponent<T>();
            return c;
        }

        /// <summary>Alan adı yanlışsa sessizce NRE vermesin — uyarı yazsın.</summary>
        private static void Set(SerializedObject so, string prop, Object value)
        {
            SerializedProperty p = so.FindProperty(prop);
            if (p == null) { Debug.LogWarning($"[Bolge] {so.targetObject.GetType().Name}.{prop} alani yok."); return; }
            p.objectReferenceValue = value;
        }

        private static SerializedObject WireMechanic(RegionMechanicBase m, RegionSO region, ChapterMapGenerator gen,
                                                     HexGridManager grid, PlayerController player, GameStateManager state,
                                                     FogOfWarManager fog, ActionPointManager ap, NoticeFeed notice,
                                                     ChapterNodeManager nodes)
        {
            var so = new SerializedObject(m);
            Set(so, "_map", gen); Set(so, "_grid", grid); Set(so, "_player", player); Set(so, "_state", state);
            Set(so, "_fog", fog); Set(so, "_ap", ap); Set(so, "_notice", notice); Set(so, "_nodes", nodes);
            Set(so, "_region", region);
            return so;
        }

        /// <summary>Pusu görevi (düğümsüz savaş). VARSA DOKUNULMAZ — Efe düşmanları değiştirmiş olabilir.</summary>
        private static MissionData EnsureAmbushMission(string file, string displayName, string description,
                                                       MapNodeType tier, params (string cls, int level)[] roster)
        {
            string path = $"Assets/Data/Missions/{file}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<MissionData>(path);
            if (existing != null) return existing;

            var list = new List<MissionData.EnemySpawn>();
            foreach (var (cls, level) in roster)
            {
                var data = AssetDatabase.LoadAssetAtPath<CharacterClassData>($"Assets/Data/Characters/{cls}.asset");
                if (data == null) { Debug.LogWarning($"[Bolge] Pusu dusmani yok: {cls}"); continue; }
                list.Add(new MissionData.EnemySpawn { enemyClass = data, level = level });
            }
            MissionData m = EnsureAsset<MissionData>(path);
            m.EditorInitAmbush(displayName, description, tier, list);
            EditorUtility.SetDirty(m);
            return m;
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
