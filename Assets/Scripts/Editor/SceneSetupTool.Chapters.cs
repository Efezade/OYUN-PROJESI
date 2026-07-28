using UnityEngine;
using UnityEditor;
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
            genSO.FindProperty("_wallet").objectReferenceValue = FindComponentAnywhere<EssenceWallet>();
            genSO.FindProperty("_ap").objectReferenceValue     = FindComponentAnywhere<ActionPointManager>();
            genSO.FindProperty("_player").objectReferenceValue = player;
            genSO.ApplyModifiedProperties();

            // Oz toplama artik KARONUN KENDISINDEN → HUD'i uretici ile besle + OZ DEPOSU'nda
            // bolum 1'in gercek ozlerini (Tas + Doga) goster.
            var essHud = FindComponentAnywhere<TacticalRPG.UI.OverworldEssenceHUD>();
            if (essHud != null)
            {
                var hudSO = new SerializedObject(essHud);
                var prop  = hudSO.FindProperty("_terrain");
                if (prop != null) prop.objectReferenceValue = gen;
                var shown = hudSO.FindProperty("_shownTypes");
                if (shown != null)
                {
                    shown.arraySize = 2;
                    shown.GetArrayElementAtIndex(0).enumValueIndex = (int)EssenceType.Tas;
                    shown.GetArrayElementAtIndex(1).enumValueIndex = (int)EssenceType.Doga;
                }
                hudSO.ApplyModifiedProperties();
            }

            EnsureEssenceStyles();

            // ── WatchtowerManager — kule ile haritanin sisini KALICI kaldirma
            var fog = FindComponentAnywhere<FogOfWarManager>();
            var wt  = host.GetComponent<WatchtowerManager>();
            if (wt == null) wt = host.AddComponent<WatchtowerManager>();
            var wtSO = new SerializedObject(wt);
            wtSO.FindProperty("_grid").objectReferenceValue   = grid;
            wtSO.FindProperty("_player").objectReferenceValue = player;
            wtSO.FindProperty("_fog").objectReferenceValue    = fog;
            wtSO.FindProperty("_world").objectReferenceValue  = null;   // ada yoneticisi YOK (tek harita)
            wtSO.FindProperty("_state").objectReferenceValue  = state;
            wtSO.ApplyModifiedProperties();

            // ── MapCollapseManager — durum baglantisi (_world null: tek harita = "Ada 1")
            var cm = host.GetComponent<MapCollapseManager>();
            if (cm != null)
            {
                var cmSO = new SerializedObject(cm);
                cmSO.FindProperty("_world").objectReferenceValue = null;
                cmSO.FindProperty("_state").objectReferenceValue = state;
                cmSO.ApplyModifiedProperties();
            }

            EditorUtility.SetDirty(host);
            Debug.Log("[Bolum] Tek haritali dunya kuruldu (1 bolum = 1 harita). " +
                      "9 adali dunya ALTERNATIF menusunde duruyor, silinmedi.");
        }

        /// <summary>Prosedürel terrain'in 11 karo tipini palete ekler — YALNIZ YOKSA
        /// (kullanıcının atadığı prefab/renk TAM KURULUM'da EZİLMEZ).
        /// "kopru" zaten palette var (köprü FBX'i) → dokunulmaz, nehrin geçidi olarak kullanılır.</summary>
        private static void EnsureTerrainPaletteEntries(HexGridManager grid)
        {
            TilePaletteSO palette = grid != null ? grid.TilePalette : null;
            if (palette == null) return;

            // id, görünen ad, yürünür mü, editör rengi, yüzey yüksekliği
            var defs = new (string id, string name, bool walkable, Color color, float height)[]
            {
                (TerrainGenerator.OvaId,              "Ova",                 true,  new Color(0.62f, 0.70f, 0.42f), 0f),
                (TerrainGenerator.TaslikOvaId,        "Taşlık Ova (1 taş)",  true,  new Color(0.66f, 0.64f, 0.58f), 0f),
                (TerrainGenerator.BolTaslikOvaId,     "Bol Taşlık (2 taş)",  true,  new Color(0.55f, 0.53f, 0.50f), 0f),
                (TerrainGenerator.AzAgacliOvaId,      "Az Ağaçlı (1 doğa)",  true,  new Color(0.45f, 0.62f, 0.35f), 0f),
                (TerrainGenerator.OrmanId,            "Orman (2 doğa)",      true,  new Color(0.28f, 0.50f, 0.26f), 0f),
                (TerrainGenerator.NadirYuksekOrmanId, "Yüksek Orman (3)",    true,  new Color(0.18f, 0.38f, 0.20f), 0f),
                (TerrainGenerator.SikOrmanId,         "Sık Orman (engel)",   false, new Color(0.10f, 0.24f, 0.13f), 0f),
                (TerrainGenerator.DagId,              "Dağ (engel)",         false, new Color(0.45f, 0.42f, 0.40f), 0f),
                (TerrainGenerator.GolId,             "Göl (engel)",          false, new Color(0.20f, 0.42f, 0.66f), 0f),
                (TerrainGenerator.NehirId,            "Nehir (engel)",       false, new Color(0.26f, 0.54f, 0.78f), 0f),
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

                tilesArr.arraySize++;
                var e = tilesArr.GetArrayElementAtIndex(tilesArr.arraySize - 1);
                e.FindPropertyRelative("id").stringValue                   = d.id;
                e.FindPropertyRelative("displayName").stringValue          = d.name;
                e.FindPropertyRelative("prefab").objectReferenceValue      = null;  // placeholder tint
                e.FindPropertyRelative("isWalkable").boolValue             = d.walkable;
                e.FindPropertyRelative("canEnterCombat").boolValue         = false;
                e.FindPropertyRelative("surfaceHeightOverride").floatValue = d.height;
                e.FindPropertyRelative("editorColor").colorValue           = d.color;
                added++;
            }
            palSO.ApplyModifiedProperties();
            EditorUtility.SetDirty(palette);
            if (added > 0) Debug.Log($"[Bolum] Palete {added} terrain karosu eklendi.");
        }

        /// <summary>EssenceConfig'e bölüm 1'in öz türlerini (Taş, Doğa) ekler — YALNIZ YOKSA.
        /// Eski Ateş/Su/Toprak girişlerine DOKUNULMAZ (silinmez).</summary>
        private static void EnsureEssenceStyles()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<EssenceConfigSO>("Assets/Data/Config/EssenceConfig.asset");
            if (cfg == null) return;

            var so  = new SerializedObject(cfg);
            var arr = so.FindProperty("_types");
            if (arr == null) return;

            var wanted = new (EssenceType type, string name, Color color)[]
            {
                (EssenceType.Tas,  "Taş",  new Color(0.62f, 0.60f, 0.56f)),
                (EssenceType.Doga, "Doğa", new Color(0.36f, 0.62f, 0.32f)),
            };

            foreach (var w in wanted)
            {
                bool exists = false;
                for (int i = 0; i < arr.arraySize; i++)
                    if (arr.GetArrayElementAtIndex(i).FindPropertyRelative("type").enumValueIndex == (int)w.type)
                    { exists = true; break; }
                if (exists) continue;

                arr.arraySize++;
                var e = arr.GetArrayElementAtIndex(arr.arraySize - 1);
                e.FindPropertyRelative("type").enumValueIndex     = (int)w.type;
                e.FindPropertyRelative("displayName").stringValue = w.name;
                e.FindPropertyRelative("color").colorValue        = w.color;
                e.FindPropertyRelative("prefab").objectReferenceValue = null;
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(cfg);
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
