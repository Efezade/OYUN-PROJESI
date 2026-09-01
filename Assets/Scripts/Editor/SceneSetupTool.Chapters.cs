using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TacticalRPG.Core;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Editor
{
    /// <summary>
    /// SceneSetupTool'un BÖLÜM (chapter) parçası — **1 bölüm = 1 harita, toplam 8 bölüm**
    /// (Docs/GAME_DESIGN.md §0/§3, TASK-004 · 2026-07-28).
    ///
    ///   • <c>Assets/Data/Chapters/ChapterConfig.asset</c> üretir (8 giriş; adı/teması netleşmemiş
    ///     bölümler "?" ile PLACEHOLDER işaretli — uydurma içerik gerçek karar sanılmasın).
    ///   • GameManager'a <see cref="ChapterProgress"/> (ilerlemenin tek kaynağı) + TAB ile açılan
    ///     8 bölümlük ilerleme şeridini (<see cref="TacticalRPG.UI.MinimapHUD"/>) ekler ve bağlar.
    ///
    /// TAM KURULUM zincirinde **SetupUIShell'den ÖNCE** çalışmalı — HARİTA ekranı
    /// (<c>PopulateMapScreen</c>) kurulurken sahnede hazır bir ChapterProgress arar.
    ///
    /// KULLANICI VERİSİNİ KORUR: ChapterConfig.asset varsa ÜZERİNE YAZILMAZ (elle düzenlenen
    /// bölüm adları/temaları TAM KURULUM'da silinmesin — CLAUDE.md tuzak notu).
    /// </summary>
    public static partial class SceneSetupTool
    {
        private const string ChapterFolder = "Assets/Data/Chapters";
        private const string ChapterConfigPath = ChapterFolder + "/ChapterConfig.asset";

        /// <summary>8 bölümün başlangıç tanımı. Bölüm 1-3 GAME_DESIGN.md §3'ten (2-3 "~" ile
        /// yaklaşık verilmiş); 4-8 HENÜZ TASARLANMADI → placeholder.</summary>
        private static readonly (string name, string theme, bool placeholder)[] DefaultChapters =
        {
            ("Bölüm 1", "Taş & Doğa",        false),
            ("Bölüm 2", "~ Ateş / Volkanik", true),
            ("Bölüm 3", "~ Teknoloji",       true),
            ("Bölüm 4", "?",                 true),
            ("Bölüm 5", "?",                 true),
            ("Bölüm 6", "?",                 true),
            ("Bölüm 7", "?",                 true),
            ("Bölüm 8", "?",                 true),
        };

        [MenuItem("TacticalRPG/Bolum - 8 Bolum Ilerlemesi Kur", false, 24)]
        public static void SetupChapters()
        {
            GameObject host = FindComponentAnywhere<GameStateManager>()?.gameObject
                              ?? GameObject.Find("GameManager");
            if (host == null)
            {
                if (!_silentSetup)
                    EditorUtility.DisplayDialog("Bolum",
                        "GameManager bulunamadi! Once TAM KURULUM (Faz 0-2) calistir.", "Tamam");
                return;
            }

            ChapterConfigSO config = EnsureChapterConfig();

            var progress = host.GetComponent<ChapterProgress>();
            if (progress == null) progress = host.AddComponent<ChapterProgress>();
            var pso = new SerializedObject(progress);
            pso.FindProperty("_config").objectReferenceValue = config;
            pso.ApplyModifiedProperties();

            // TAB ile 8 bolumluk ilerleme seridi (eski 3x3 ada minimap'inin yerine).
            var mm = host.GetComponent<TacticalRPG.UI.MinimapHUD>();
            if (mm == null) mm = host.AddComponent<TacticalRPG.UI.MinimapHUD>();
            var mmSO = new SerializedObject(mm);
            mmSO.FindProperty("_progress").objectReferenceValue = progress;
            mmSO.ApplyModifiedProperties();

            EditorUtility.SetDirty(host);
            Debug.Log($"[Bolum] {config.Count} bolumluk ilerleme kuruldu (ChapterProgress + TAB seridi).");

            if (!_silentSetup)
                EditorUtility.DisplayDialog("Bolum",
                    $"{config.Count} bolum kuruldu.\n\n" +
                    "• ChapterConfig.asset: Assets/Data/Chapters\n" +
                    "• GameManager -> ChapterProgress (ilerlemenin tek kaynagi)\n" +
                    "• TAB: 8 bolumluk ilerleme seridi\n\n" +
                    "HARITA ekranindaki 8'lik yol icin 'UI - Menu Iskeleti Kur' calistir.", "Tamam");
        }

        /// <summary>
        /// **Bölüm dünyası (yürürlükteki tasarım): 1 bölüm = 1 harita.** TAM KURULUM zincirinde
        /// eski <c>SetupWorld3x3</c>'ün YERİNE geçer.
        ///
        /// Kurar: gözetleme kulesi (kule karosu), çöküş yöneticisinin durum bağlantısı, savaş karoları
        /// (deneme11-20). KURMAZ: <c>WorldGridManager</c> (9 ada), portal karoları, <c>TeleportManager</c>
        /// — bunlar ALTERNATİF dünyaya ait (menü: "ALTERNATIF - 9 Harita 3x3 Dunyayi Geri Yukle").
        ///
        /// Tüketiciler (<c>MapInputHandler</c>, <c>StoreManager</c>, <c>WatchtowerManager</c>,
        /// <c>MapCollapseManager</c>) <c>_world</c> alanını OPSİYONEL tutar (null-guard'lı) → ada
        /// yöneticisi olmadan sorunsuz çalışır, tek harita "Ada 1" sayılır.
        /// </summary>
        [MenuItem("TacticalRPG/Bolum - Tek Haritali Dunya Kur (1 bolum = 1 harita)", false, 25)]
        public static void SetupChapterWorld()
        {
            var grid   = FindComponentAnywhere<HexGridManager>();
            var player = FindComponentAnywhere<PlayerController>();
            var state  = FindComponentAnywhere<GameStateManager>();
            GameObject host = state != null ? state.gameObject : GameObject.Find("GameManager");
            if (grid == null || host == null)
            {
                if (!_silentSetup)
                    EditorUtility.DisplayDialog("Bolum Dunyasi",
                        "Grid ya da GameManager bulunamadi! Once TAM KURULUM (Faz 0-2) calistir.", "Tamam");
                return;
            }

            if (grid.GridRoot != null) grid.GridRoot.gameObject.SetActive(true); // grid gizli kalmasin

            // Savas karolari (deneme11-20) — ada yapisindan bagimsiz, cekirdek oynanis.
            EnsureCombatTestTiles(grid);

            // ── TASK-005: prosedurel terrain (22x25, 10-seed havuzu) ─────────
            TerrainConfigSO terrainConfig = EnsureTerrainConfig();
            EnsureTerrainPaletteEntries(grid);
            AssignTerrainTileModels(force: false);   // her terrain karosunun 3B modeli olsun

            // Grid boyutunu terrain config'e esitle (22x25) — uretilen harita tam otursun.
            var gridSO = new SerializedObject(grid);
            gridSO.FindProperty("_width").intValue  = terrainConfig.Width;
            gridSO.FindProperty("_height").intValue = terrainConfig.Height;
            gridSO.ApplyModifiedProperties();

            var gen = host.GetComponent<ChapterMapGenerator>();
            if (gen == null) gen = host.AddComponent<ChapterMapGenerator>();
            var genSO = new SerializedObject(gen);
            genSO.FindProperty("_grid").objectReferenceValue   = grid;
            genSO.FindProperty("_config").objectReferenceValue = terrainConfig;
            genSO.FindProperty("_player").objectReferenceValue = player;
            genSO.ApplyModifiedProperties();

            // ── Oz yataklari (2026-08-17): haritaya 60-80 oz SACILIR + karo boyanir/konturlanir
            //    + ustune hareketli kure konur. Once kure prefablari uretilir, sonra config'e yazilir.
            EssenceOrbFactory.BuildAll(force: false);
            EnsureEssenceStyles();
            EssenceConfigSO essenceConfig =
                AssetDatabase.LoadAssetAtPath<EssenceConfigSO>("Assets/Data/Config/EssenceConfig.asset");

            var field = host.GetComponent<EssenceFieldManager>();
            if (field == null) field = host.AddComponent<EssenceFieldManager>();
            var fSO = new SerializedObject(field);
            fSO.FindProperty("_grid").objectReferenceValue   = grid;
            fSO.FindProperty("_map").objectReferenceValue    = gen;
            fSO.FindProperty("_config").objectReferenceValue = essenceConfig;
            fSO.FindProperty("_wallet").objectReferenceValue = FindComponentAnywhere<EssenceWallet>();
            fSO.FindProperty("_ap").objectReferenceValue     = FindComponentAnywhere<ActionPointManager>();
            fSO.FindProperty("_player").objectReferenceValue = player;
            fSO.ApplyModifiedProperties();

            // Oz sokulme gosterisi (goge solan isik huzmesi) — karodan oz alininca oynar.
            var harvestFx = host.GetComponent<EssenceHarvestEffect>();
            if (harvestFx == null) harvestFx = host.AddComponent<EssenceHarvestEffect>();

            var fieldVis = host.GetComponent<EssenceFieldVisuals>();
            if (fieldVis == null) fieldVis = host.AddComponent<EssenceFieldVisuals>();
            var fvSO = new SerializedObject(fieldVis);
            fvSO.FindProperty("_harvest").objectReferenceValue = harvestFx;
            fvSO.FindProperty("_field").objectReferenceValue    = field;
            fvSO.FindProperty("_grid").objectReferenceValue     = grid;
            fvSO.FindProperty("_config").objectReferenceValue   = essenceConfig;
            fvSO.FindProperty("_fog").objectReferenceValue      = FindComponentAnywhere<FogOfWarManager>();
            fvSO.FindProperty("_state").objectReferenceValue    = state;
            fvSO.FindProperty("_player").objectReferenceValue   = player;
            fvSO.FindProperty("_collapse").objectReferenceValue = host.GetComponent<MapCollapseManager>();
            fvSO.FindProperty("_ringMaterial").objectReferenceValue  = EssenceOrbFactory.RingMaterial();
            fvSO.FindProperty("_drainMaterial").objectReferenceValue = EssenceOrbFactory.DrainMaterial();
            fvSO.ApplyModifiedProperties();

            // ── MINIHARITA (2026-08-17): HARITA ekranindaki gercek harita. Dokuyu VERIDEN boyar
            //    (kamera + RenderTexture DEGIL) → sis bilgisini ve karo tiplerini dogrudan kullanir.
            var minimap = host.GetComponent<MinimapRenderer>();
            if (minimap == null) minimap = host.AddComponent<MinimapRenderer>();
            var mmSO = new SerializedObject(minimap);
            mmSO.FindProperty("_grid").objectReferenceValue  = grid;
            mmSO.FindProperty("_fog").objectReferenceValue   = FindComponentAnywhere<FogOfWarManager>();
            mmSO.FindProperty("_state").objectReferenceValue = state;
            mmSO.FindProperty("_style").objectReferenceValue = EnsureMinimapStyle();
            mmSO.ApplyModifiedProperties();

            // HUD'i yataklarla besle + OZ DEPOSU'nda bolum 1'in gercek ozlerini (Tas + Doga) goster.
            var essHud = FindComponentAnywhere<TacticalRPG.UI.OverworldEssenceHUD>();
            if (essHud != null)
            {
                var hudSO = new SerializedObject(essHud);
                var prop  = hudSO.FindProperty("_field");
                if (prop != null) prop.objectReferenceValue = field;
                var shown = hudSO.FindProperty("_shownTypes");
                if (shown != null)
                {
                    shown.arraySize = 2;
                    shown.GetArrayElementAtIndex(0).enumValueIndex = (int)EssenceType.Tas;
                    shown.GetArrayElementAtIndex(1).enumValueIndex = (int)EssenceType.Doga;
                }
                hudSO.ApplyModifiedProperties();
            }

            // ── TASK-006: harita dugumleri (zorunlu gorev / zindan / encounter / market / kule / boss)
            NodeConfigSO nodeConfig = EnsureNodeConfig();
            var nodes = host.GetComponent<ChapterNodeManager>();
            if (nodes == null) nodes = host.AddComponent<ChapterNodeManager>();
            var nSO = new SerializedObject(nodes);
            nSO.FindProperty("_grid").objectReferenceValue     = grid;
            nSO.FindProperty("_map").objectReferenceValue      = gen;
            nSO.FindProperty("_config").objectReferenceValue   = nodeConfig;
            nSO.FindProperty("_ap").objectReferenceValue       = FindComponentAnywhere<ActionPointManager>();
            nSO.FindProperty("_wallet").objectReferenceValue   = FindComponentAnywhere<EssenceWallet>();
            nSO.FindProperty("_player").objectReferenceValue   = player;
            nSO.FindProperty("_fog").objectReferenceValue      = FindComponentAnywhere<FogOfWarManager>();
            nSO.FindProperty("_state").objectReferenceValue    = state;
            nSO.FindProperty("_missions").objectReferenceValue = FindComponentAnywhere<MissionManager>();
            nSO.FindProperty("_store").objectReferenceValue    = FindComponentAnywhere<StoreManager>();
            // Kule acilis efekti (eski oyundaki isik huzmesi + halka) — dugum kulesi de oynatsin.
            var towerFx = host.GetComponent<TowerRevealEffect>();
            if (towerFx == null) towerFx = host.AddComponent<TowerRevealEffect>();
            nSO.FindProperty("_towerFx").objectReferenceValue  = towerFx;
            nSO.ApplyModifiedProperties();

            // ── TASK-007: zaman baskisi + bolum kaybi/retry ──────────────────
            var collapseMgr = host.GetComponent<MapCollapseManager>();
            nSO = new SerializedObject(nodes);
            nSO.FindProperty("_collapse").objectReferenceValue = collapseMgr;
            nSO.ApplyModifiedProperties();

            CollapseConfig collapseCfg =
                AssetDatabase.LoadAssetAtPath<CollapseConfig>("Assets/Data/Config/CollapseConfig.asset");

            var run = host.GetComponent<ChapterRunManager>();
            if (run == null) run = host.AddComponent<ChapterRunManager>();
            var rSO = new SerializedObject(run);
            rSO.FindProperty("_ap").objectReferenceValue             = FindComponentAnywhere<ActionPointManager>();
            rSO.FindProperty("_collapseConfig").objectReferenceValue = collapseCfg;
            rSO.FindProperty("_map").objectReferenceValue            = gen;
            rSO.FindProperty("_wallet").objectReferenceValue         = FindComponentAnywhere<EssenceWallet>();
            rSO.FindProperty("_turns").objectReferenceValue          = FindComponentAnywhere<TurnManager>();
            rSO.FindProperty("_state").objectReferenceValue          = state;
            rSO.FindProperty("_collapse").objectReferenceValue       = collapseMgr;
            rSO.ApplyModifiedProperties();

            // Sert kesimde harita tiklamalarini kilitle.
            var mapInput = FindComponentAnywhere<MapInputHandler>();
            if (mapInput != null)
            {
                var miSO = new SerializedObject(mapInput);
                var runProp = miSO.FindProperty("_run");
                if (runProp != null) { runProp.objectReferenceValue = run; miSO.ApplyModifiedProperties(); }
            }

            var runHud = host.GetComponent<TacticalRPG.UI.ChapterRunHUD>();
            if (runHud == null) runHud = host.AddComponent<TacticalRPG.UI.ChapterRunHUD>();
            var rhSO = new SerializedObject(runHud);
            rhSO.FindProperty("_run").objectReferenceValue            = run;
            rhSO.FindProperty("_nodes").objectReferenceValue          = nodes;
            rhSO.FindProperty("_ap").objectReferenceValue             = FindComponentAnywhere<ActionPointManager>();
            rhSO.FindProperty("_collapseConfig").objectReferenceValue = collapseCfg;
            rhSO.FindProperty("_state").objectReferenceValue          = state;
            rhSO.ApplyModifiedProperties();

            var nodeHud = host.GetComponent<TacticalRPG.UI.ChapterNodeHUD>();
            if (nodeHud == null) nodeHud = host.AddComponent<TacticalRPG.UI.ChapterNodeHUD>();
            var nhSO = new SerializedObject(nodeHud);
            nhSO.FindProperty("_state").objectReferenceValue  = state;
            nhSO.FindProperty("_nodes").objectReferenceValue  = nodes;
            nhSO.FindProperty("_player").objectReferenceValue = player;
            nhSO.FindProperty("_ap").objectReferenceValue     = FindComponentAnywhere<ActionPointManager>();
            nhSO.ApplyModifiedProperties();

            SetupMandatoryQuestChain(host, grid, gen, player, state, nodes, run);

            var fog = FindComponentAnywhere<FogOfWarManager>();

            // ── MapCollapseManager — durum baglantisi
            var cm = host.GetComponent<MapCollapseManager>();
            if (cm != null)
            {
                var cmSO = new SerializedObject(cm);
                cmSO.FindProperty("_state").objectReferenceValue = state;
                cmSO.ApplyModifiedProperties();
            }

            // Savas tarafi: hasar formulu + prosedurel arena ureticisi (2026-08-12).
            SetupCombat();

            EditorUtility.SetDirty(host);
            Debug.Log("[Bolum] Tek haritali dunya kuruldu (1 bolum = 1 harita).");
        }

        /// <summary>Terrain + düğüm karolarının palet girişlerini kurar.
        ///
        /// Terrain karolarının TAMAMI (70+ tip, renkli/dokulu prefablarıyla) artık
        /// <see cref="TileVisualFactory"/> tarafından üretiliyor — burada elle tutulan kısa bir
        /// liste vardı, katalog büyüyünce ikisi kaçınılmaz olarak ayrışırdı. Düğüm karoları
        /// (mağara/kamp/görev alanı) prosedürel dağıtıma girmediği için burada kalıyor:
        /// savaşa giriş bayrağı (`canEnterCombat`) onlara özgü.</summary>
        private static void EnsureTerrainPaletteEntries(HexGridManager grid)
        {
            TilePaletteSO palette = grid != null ? grid.TilePalette : null;
            if (palette == null) return;

            TileVisualFactory.BuildAll(force: false);

            // combat=false OLAN GİRİŞ ÖNEMLİ: "gorev_tamam" bitmiş zorunlu görevin karosu ve savaşa
            // giris bayragi TASIMAZ. MissionManager menzildeki her canEnterCombat karosundan savas
            // aciyor, dugumun "tamamlandi" olmasina BAKMIYOR — bayrak kalsaydi bitmis goreve
            // tekrar tekrar girilebilirdi.
            var defs = new (string id, string name, bool walkable, bool combat, Color color)[]
            {
                (ChapterNodeManager.DungeonTileId,   "Mağara (zindan)",   true, true,  new Color(0.34f, 0.30f, 0.34f)),
                (ChapterNodeManager.EncounterTileId, "Kamp (karşılaşma)", true, true,  new Color(0.72f, 0.45f, 0.24f)),
                (ChapterNodeManager.MandatoryTileId, "Görev Alanı",       true, true,  new Color(0.90f, 0.76f, 0.28f)),
                (ChapterNodeManager.ClearedMandatoryTileId,
                                                     "Görev Alanı (bitti)", true, false, new Color(1.00f, 0.80f, 0.24f)),
            };

            var palSO    = new SerializedObject(palette);
            var tilesArr = palSO.FindProperty("tiles");
            int added = 0;
            foreach (var d in defs)
            {
                bool exists = false;
                for (int j = 0; j < tilesArr.arraySize; j++)
                    if (tilesArr.GetArrayElementAtIndex(j).FindPropertyRelative("id").stringValue == d.id) { exists = true; break; }
                if (exists) continue;

                // DIKKAT: arraySize++ Unity'de SON ELEMANI KOPYALAR. Bu yuzden HER alan acikca
                // yazilmali — ozellikle isStore. (Bir kez atlandi: yeni terrain karolari "magaza"
                // girisinden isStore=1 miras aldi, harita boyunca her karoda dukkan aciliyordu.)
                tilesArr.arraySize++;
                var e = tilesArr.GetArrayElementAtIndex(tilesArr.arraySize - 1);
                e.FindPropertyRelative("id").stringValue                   = d.id;
                e.FindPropertyRelative("displayName").stringValue          = d.name;
                e.FindPropertyRelative("prefab").objectReferenceValue      = null;  // model asagida atanir
                e.FindPropertyRelative("isWalkable").boolValue             = d.walkable;
                e.FindPropertyRelative("canEnterCombat").boolValue         = d.combat;
                e.FindPropertyRelative("isStore").boolValue                = false;
                e.FindPropertyRelative("surfaceHeightOverride").floatValue = 0f;
                e.FindPropertyRelative("editorColor").colorValue           = d.color;
                added++;
            }
            palSO.ApplyModifiedProperties();
            EditorUtility.SetDirty(palette);
            if (added > 0) Debug.Log($"[Bolum] Palete {added} dugum karosu eklendi.");
        }

        /// <summary>
        /// **Haritayı EDİTÖRDE üretir** — Play'e basmadan sahnede yeni (prosedürel) harita görünsün
        /// (kullanıcı isteği 2026-07-29: "oyunu başlatmasam da yeni map gözüksün, eski maple işimiz
        /// kalmadı"). Üretilen harita kalıcı bir asset'e (`Bolum1_Uretilen.asset`) yazılır ve grid'e
        /// atanır → sahne kaydedilince/yeniden açılınca da yeni harita durur.
        ///
        /// ESKİ ELLE BOYANMIŞ HARİTA SİLİNMEZ: `TileMap.asset` + `Face_2..9` yerinde duruyor
        /// (ayrıca `Docs/Alternatif_Tasarimlar/3x3_Dunya_Haritasi/` yedeği var). Sadece grid artık
        /// ona değil, üretilen haritaya bakıyor.
        /// </summary>
        [MenuItem("TacticalRPG/Bolum - Haritayi Simdi Uret (Play'siz gorun)", false, 26)]
        public static void GenerateChapterMapInEditor()
        {
            var gen  = FindComponentAnywhere<ChapterMapGenerator>();
            var grid = FindComponentAnywhere<HexGridManager>();
            if (gen == null || grid == null)
            {
                if (!_silentSetup)
                    EditorUtility.DisplayDialog("Harita Uret",
                        "ChapterMapGenerator ya da grid yok. Once TAM KURULUM calistir.", "Tamam");
                return;
            }

            TerrainConfigSO cfg = EnsureTerrainConfig();
            int seed = (cfg != null && cfg.SeedPool != null && cfg.SeedPool.Count > 0)
                ? cfg.SeedPool[Random.Range(0, cfg.SeedPool.Count)]
                : 1;

            // Kalici cikti asset'i — runtime kopya olsa editorde derleme sonrasi kaybolurdu.
            EnsureFolder("Assets/Data/Map");
            const string outPath = "Assets/Data/Map/Bolum1_Uretilen.asset";
            var outMap = AssetDatabase.LoadAssetAtPath<TileMapSO>(outPath);
            if (outMap == null)
            {
                outMap = ScriptableObject.CreateInstance<TileMapSO>();
                AssetDatabase.CreateAsset(outMap, outPath);
            }

            gen.GenerateInto(outMap, seed);

            // Dugumleri de yerlestir ki magara/kamp/gorev/han/kule karolari editorde gorunsun.
            var nodes = FindComponentAnywhere<ChapterNodeManager>();
            if (nodes != null) nodes.Rebuild();

            // Grid KALICI olarak uretilen haritaya baksin (sahne yeniden acilinca eski harita gelmesin).
            var gridSO = new SerializedObject(grid);
            gridSO.FindProperty("_tileMap").objectReferenceValue = outMap;
            gridSO.ApplyModifiedProperties();

            EditorUtility.SetDirty(outMap);
            EditorUtility.SetDirty(grid);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log($"[Bolum] Harita EDITORDE uretildi — seed {seed}. Eski elle boyanmis harita silinmedi.");
            if (!_silentSetup)
                EditorUtility.DisplayDialog("Harita Uretildi",
                    $"Seed {seed} ile uretildi ve sahneye uygulandi.\n\n" +
                    "Play'e basmadan da yeni harita gorunur. Farkli bir harita icin bu menuyu\n" +
                    "tekrar calistir (havuzdan baska bir seed secilir).", "Tamam");
        }

        /// <summary>
        /// ZORUNLU GÖREV ZİNCİRİ'ni kurar (2026-08-28): ayar asset'i + gökten düşüş efekti +
        /// yönetici + üstteki çizgi barı, hepsi birbirine bağlı.
        ///
        /// Yönetici sahneye girdiği anda <c>ChapterNodeManager</c>'ın BOSS TAŞI KAPISI da devreye
        /// girer (<c>SetBossStone</c> ilk çağrıda kapıyı açar) — yani bu adım koşmazsa boss eski
        /// hâliyle taşsız girilebilir kalır, bölüm sessizce bitirilemez hâle GELMEZ.
        /// </summary>
        private static void SetupMandatoryQuestChain(GameObject host, HexGridManager grid,
                                                     ChapterMapGenerator gen, PlayerController player,
                                                     GameStateManager state, ChapterNodeManager nodes,
                                                     ChapterRunManager run)
        {
            MandatoryQuestConfigSO questCfg = EnsureMandatoryQuestConfig();

            // Iki animasyon: gorev DUSER (acilis) / gorev MUHURLENIR (bitis). Ikisi de proseduel.
            var fallFx = host.GetComponent<MandatoryQuestFallEffect>();
            if (fallFx == null) fallFx = host.AddComponent<MandatoryQuestFallEffect>();
            var clearFx = host.GetComponent<MandatoryQuestClearEffect>();
            if (clearFx == null) clearFx = host.AddComponent<MandatoryQuestClearEffect>();

            // Dugum yoneticisine zincir ayarini + efektleri tanit.
            var nSO = new SerializedObject(nodes);
            nSO.FindProperty("_questConfig").objectReferenceValue  = questCfg;
            nSO.FindProperty("_questFallFx").objectReferenceValue  = fallFx;
            nSO.FindProperty("_questClearFx").objectReferenceValue = clearFx;
            nSO.ApplyModifiedProperties();

            // Zincirin beyni.
            var director = host.GetComponent<MandatoryQuestDirector>();
            if (director == null) director = host.AddComponent<MandatoryQuestDirector>();
            var dSO = new SerializedObject(director);
            dSO.FindProperty("_nodes").objectReferenceValue  = nodes;
            dSO.FindProperty("_ap").objectReferenceValue     = FindComponentAnywhere<ActionPointManager>();
            dSO.FindProperty("_map").objectReferenceValue    = gen;
            dSO.FindProperty("_grid").objectReferenceValue   = grid;
            dSO.FindProperty("_player").objectReferenceValue = player;
            dSO.FindProperty("_run").objectReferenceValue    = run;
            dSO.FindProperty("_config").objectReferenceValue = questCfg;
            dSO.ApplyModifiedProperties();

            // Ustteki cizgi bari.
            var bar = host.GetComponent<TacticalRPG.UI.MandatoryQuestBarHUD>();
            if (bar == null) bar = host.AddComponent<TacticalRPG.UI.MandatoryQuestBarHUD>();
            var bSO = new SerializedObject(bar);
            bSO.FindProperty("_director").objectReferenceValue = director;
            bSO.FindProperty("_state").objectReferenceValue    = state;
            bSO.ApplyModifiedProperties();

            Debug.Log($"[Kurulum] Zorunlu gorev zinciri hazir — baslangic {questCfg.InitialCount}, " +
                      $"acilis gunleri {string.Join("/", AcilisGunleri(questCfg))}, " +
                      $"en fazla {questCfg.MaxCount} gorev.");
        }

        private static string[] AcilisGunleri(MandatoryQuestConfigSO cfg)
        {
            var days = new string[cfg.UnlockCount];
            for (int i = 0; i < days.Length; i++) days[i] = cfg.UnlockDay(i).ToString();
            return days;
        }

        /// <summary>MandatoryQuestConfig.asset'i yükler; YOKSA varsayılanlarla oluşturur
        /// (varsa DOKUNMAZ — playtest'te ayarlanan günler/ödüller TAM KURULUM'da ezilmesin).</summary>
        private static MandatoryQuestConfigSO EnsureMandatoryQuestConfig()
        {
            const string path = "Assets/Data/Config/MandatoryQuestConfig.asset";
            var cfg = AssetDatabase.LoadAssetAtPath<MandatoryQuestConfigSO>(path);
            if (cfg != null) return cfg;

            EnsureFolder("Assets/Data/Config");
            cfg = ScriptableObject.CreateInstance<MandatoryQuestConfigSO>();
            AssetDatabase.CreateAsset(cfg, path);   // alan varsayilanlari = 2 baslangic + gun 5/8/11
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            return cfg;
        }

        /// <summary>NodeConfig.asset'i yükler; YOKSA varsayılanlarla oluşturur (varsa DOKUNMAZ —
        /// playtest'te elle ayarlanan sayılar TAM KURULUM'da ezilmesin).</summary>
        private static NodeConfigSO EnsureNodeConfig()
        {
            const string path = "Assets/Data/Config/NodeConfig.asset";
            var cfg = AssetDatabase.LoadAssetAtPath<NodeConfigSO>(path);
            if (cfg != null) return cfg;

            EnsureFolder("Assets/Data/Config");
            cfg = ScriptableObject.CreateInstance<NodeConfigSO>();
            AssetDatabase.CreateAsset(cfg, path);   // alan varsayilanlari = INBOX TASK-006 taslak sayilari
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            return cfg;
        }

        /// <summary>EssenceConfig'te BEŞ öz türünün de stili bulunsun: eksik tür EKLENİR, var olanın
        /// adı/rengi KORUNUR (kullanıcı tweak'i silinmesin) ama küre biçimi ve prefabı BOŞSA doldurulur.
        ///
        /// DİKKAT — <c>arraySize++</c> SON ELEMANI KOPYALAR: yeni girişte HER alan açıkça yazılmalı,
        /// yoksa komşu girişten biçim/prefab miras alınır (CLAUDE.md tuzak notu).</summary>
        private static void EnsureEssenceStyles()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<EssenceConfigSO>("Assets/Data/Config/EssenceConfig.asset");
            if (cfg == null) return;

            var so  = new SerializedObject(cfg);
            var arr = so.FindProperty("_types");
            if (arr == null) return;

            var wanted = new (EssenceType type, string name, Color color, EssenceOrbShape shape)[]
            {
                (EssenceType.Ates,   "Ateş",  new Color(0.95f, 0.35f, 0.14f), EssenceOrbShape.Alev),
                (EssenceType.Su,     "Su",    new Color(0.24f, 0.58f, 0.95f), EssenceOrbShape.Su),
                (EssenceType.Toprak, "Toprak",new Color(0.70f, 0.52f, 0.28f), EssenceOrbShape.Toz),
                (EssenceType.Tas,    "Taş",   new Color(0.66f, 0.66f, 0.62f), EssenceOrbShape.Kristal),
                (EssenceType.Doga,   "Doğa",  new Color(0.36f, 0.78f, 0.34f), EssenceOrbShape.Yaprak),
            };

            foreach (var w in wanted)
            {
                SerializedProperty e = null;
                for (int i = 0; i < arr.arraySize; i++)
                    if (arr.GetArrayElementAtIndex(i).FindPropertyRelative("type").enumValueIndex == (int)w.type)
                    { e = arr.GetArrayElementAtIndex(i); break; }

                if (e == null)
                {
                    arr.arraySize++;
                    e = arr.GetArrayElementAtIndex(arr.arraySize - 1);
                    e.FindPropertyRelative("type").enumValueIndex     = (int)w.type;
                    e.FindPropertyRelative("displayName").stringValue = w.name;
                    e.FindPropertyRelative("color").colorValue        = w.color;
                }

                // Biçim: enum'un "boş" hâli olmadığı için her seferinde kanonik değere çekilir.
                // Prefab: yalnız BOŞSA doldurulur — kullanıcı kendi modelini atadıysa ona dokunulmaz.
                var shapeProp = e.FindPropertyRelative("orbShape");
                if (shapeProp != null) shapeProp.enumValueIndex = (int)w.shape;

                var prefabProp = e.FindPropertyRelative("prefab");
                if (prefabProp != null && prefabProp.objectReferenceValue == null)
                    prefabProp.objectReferenceValue = EssenceOrbFactory.PrefabFor(w.shape);
            }

            // ── ESKİ VARSAYILANDAN GEÇİŞ (2026-08-17: "kontur bold olsun") ──────────────
            // Asset bir kez üretildikten sonra C#'taki alan varsayılanı ARTIK OKUNMAZ — asset'te
            // yazılı eski değer kazanır. Bu yüzden yalnız DEĞER HÂLÂ ESKİ VARSAYILANSA yükseltilir;
            // kullanıcı elle başka bir değer verdiyse dokunulmaz.
            MigrateDefault(so, "_outlineWidth", 0.085f, 0.18f);
            MigrateDefault(so, "_glow",         2.4f,   3.0f);

            // Kararmis karo COK KOYU idi, catlaklar icinde kayboluyordu (kullanici, 2026-08-17)
            // → kurumus toprak tonuna acildi.
            MigrateColor(so, "_drainCapColor", new Color(0.20f, 0.18f, 0.155f),  new Color(0.42f, 0.38f, 0.32f));
            MigrateColor(so, "_drainedColor",  new Color(0.33f, 0.32f, 0.31f),   new Color(0.50f, 0.47f, 0.43f));
            MigrateColor(so, "_crackColor",    new Color(0.045f, 0.038f, 0.032f),new Color(0.09f, 0.075f, 0.06f));

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(cfg);
        }

        /// <summary>Alan hâlâ ESKİ varsayılandaysa yenisine yükseltir (kullanıcı tweak'i korunur).</summary>
        private static void MigrateDefault(SerializedObject so, string field, float oldDefault, float newDefault)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null || p.propertyType != SerializedPropertyType.Float) return;
            if (Mathf.Abs(p.floatValue - oldDefault) > 0.0001f) return;

            p.floatValue = newDefault;
            // Yardımcı artık EssenceConfig dışında da kullanılıyor (RouteMarker) → log nesneyi söylesin.
            Debug.Log($"[Kurulum] {so.targetObject.GetType().Name}.{field}: {oldDefault} → {newDefault} " +
                      "(eski varsayilandan yukseltildi).");
        }

        /// <summary>Renk alanının float karşılığı (kullanıcı elle değiştirdiyse dokunulmaz).</summary>
        private static void MigrateColor(SerializedObject so, string field, Color oldDefault, Color newDefault)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null || p.propertyType != SerializedPropertyType.Color) return;

            Color c = p.colorValue;
            if (Mathf.Abs(c.r - oldDefault.r) > 0.002f ||
                Mathf.Abs(c.g - oldDefault.g) > 0.002f ||
                Mathf.Abs(c.b - oldDefault.b) > 0.002f) return;

            p.colorValue = newDefault;
            Debug.Log($"[Oz] EssenceConfig.{field}: {oldDefault} → {newDefault} (eski varsayilandan acildi).");
        }

        /// <summary>MinimapStyle.asset'i yükler; YOKSA varsayılanlarla oluşturur (varsa DOKUNMAZ —
        /// kullanıcının çözünürlük/renk tweak'i TAM KURULUM'da silinmesin).</summary>
        private static MinimapStyleSO EnsureMinimapStyle()
        {
            const string path = "Assets/Data/Config/MinimapStyle.asset";
            var style = AssetDatabase.LoadAssetAtPath<MinimapStyleSO>(path);
            if (style != null) return style;

            style = ScriptableObject.CreateInstance<MinimapStyleSO>();
            AssetDatabase.CreateAsset(style, path);   // alan varsayilanlari = MinimapStyleSO'daki degerler
            EditorUtility.SetDirty(style);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Minimap] Stil asset'i uretildi: {path}");
            return style;
        }

        /// <summary>TerrainConfig.asset'i yükler; YOKSA varsayılanlarla oluşturur (varsa DOKUNMAZ).</summary>
        private static TerrainConfigSO EnsureTerrainConfig()
        {
            const string path = "Assets/Data/Config/TerrainConfig.asset";
            var cfg = AssetDatabase.LoadAssetAtPath<TerrainConfigSO>(path);
            if (cfg != null) return cfg;

            EnsureFolder("Assets/Data/Config");
            cfg = ScriptableObject.CreateInstance<TerrainConfigSO>();
            AssetDatabase.CreateAsset(cfg, path);   // alan varsayilanlari zaten GAME_DESIGN §3 degerleri
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            return cfg;
        }

        /// <summary>ChapterConfig.asset'i yükler; YOKSA varsayılan 8 bölümle oluşturur.
        /// VARSA dokunmaz — kullanıcının Inspector'da düzenlediği ad/tema ezilmez.</summary>
        private static ChapterConfigSO EnsureChapterConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<ChapterConfigSO>(ChapterConfigPath);
            if (config != null) return config;

            EnsureFolder(ChapterFolder);
            config = ScriptableObject.CreateInstance<ChapterConfigSO>();
            AssetDatabase.CreateAsset(config, ChapterConfigPath);

            var so   = new SerializedObject(config);
            var list = so.FindProperty("chapters");
            list.arraySize = DefaultChapters.Length;
            for (int i = 0; i < DefaultChapters.Length; i++)
            {
                var e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("displayName").stringValue = DefaultChapters[i].name;
                e.FindPropertyRelative("theme").stringValue       = DefaultChapters[i].theme;
                e.FindPropertyRelative("isPlaceholder").boolValue = DefaultChapters[i].placeholder;
            }
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            return config;
        }
    }
}
