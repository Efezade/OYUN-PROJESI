using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using TacticalRPG.Grid;
using TacticalRPG.Core;
using TacticalRPG.Data;
using TacticalRPG.UI;
using System.Collections.Generic;

namespace TacticalRPG.Editor
{
    public static class SceneSetupTool
    {
        private const string SceneRootName   = "[TacticalRPG_Scene]";
        private const string SystemsRootName = "[TacticalRPG_Systems]";
        private const string MaterialsPath   = "Assets/Art/Materials";
        private const string PrefabsGridPath = "Assets/Prefabs/Grid";

        // TAM KURULUM zinciri sırasında alt-fazların başarı dialoglarını bastırır
        // (tek özet kutu FullSetup sonunda gösterilir). Hata dialogları bastırılmaz.
        private static bool _silentSetup;

        // ─────────────────────────────────────────────────────────────────────
        // TAM KURULUM — tek tıkla tüm fazları sırasıyla çalıştırır
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("TacticalRPG/TAM KURULUM (Tek Tikla)", false, 1)]
        public static void FullSetup()
        {
            // Tüm fazları sırayla kurar (temiz rebuild). Bağımlılık sırası:
            // 0→1→2→HUD→A→B→C→C3. Faz 3 (eski yetenek test sandbox'ı) DAHİL DEĞİL.
            _silentSetup = true;
            try
            {
                SetupPhase0();    // kamera, isik, GameManager
                SetupPhase1();    // grid, fog, oyuncu, AP, kiyamet
                SetupPhase2();    // karakterler, oz, kam mana
                SetupDebugHUD();  // debug HUD
                SetupPhaseA();    // overworld<->savas durum makinesi + gorev
                SetupPhaseB();    // deployment (oz ile birim yerlestirme)
                SetupPhaseC();    // dusman roster spawn (3 Goblin)
                SetupPhaseC3();   // tur sistemi (initiative + hareket + saldiri + AI)
                SetupPhaseC4();   // Kam komutan + savas buyusu + lose=Kam olumu
                SetupPhaseD();    // cok-tipli oz + harita toplama + tarifle birim uretme
                SetupCubeFaces(); // Bolum 1 = KUP (6 yuz) + manuel yuz cubugu
            }
            finally { _silentSetup = false; }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "TAM KURULUM Tamamlandi!",
                "Tum oyun TEK TIKLA kuruldu:\n\n" +
                "  • Faz 0 — Kamera, Isik\n" +
                "  • Faz 1 — Hex Grid, Oyuncu, AP, Kiyamet\n" +
                "  • Faz 2 — Karakter Sistemi (Kam, Savasci, Ranger)\n" +
                "  • Debug HUD\n" +
                "  • Faz A — Overworld/Savas gecisi + gorev\n" +
                "  • Faz B — Deployment (oz ile yerlestirme)\n" +
                "  • Faz C — Dusman spawn (3 Goblin)\n" +
                "  • Faz C3 — Tur sistemi (initiative + hareket + saldiri + AI)\n" +
                "  • Faz C4 — Kam komutan + savas buyusu + lose=Kam olumu\n" +
                "  • Faz D — Cok-tipli oz + harita toplama + tarifle birim uretme\n\n" +
                "Ctrl+S ile kaydet, Play'e bas:\n" +
                "Overworld'de renkli ozleri TOPLA (sag panel, 1 AP) → SavasciRanger URET →\n" +
                "Sari marker (Q5R5) → Evet → Kam + uretilen birimleri yerlestir → SAVASI BASLAT.",
                "Tamam");
        }

        // ─────────────────────────────────────────────────────────────────────
        // FAZ 0 — Kamera, Işık, GameManager
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("TacticalRPG/Faz 0 - Kamera ve Sahne", false, 11)]
        public static void SetupPhase0()
        {
            DestroyRoot(SceneRootName);

            // Sahnedeki TÜM kameraları sil. Unity'nin varsayılan "Main Camera"sı SceneRoot DIŞINDA
            // (kök seviyede) durduğu için DestroyRoot onu temizlemez → iki "Main Camera" oluşur,
            // ikisi de MainCamera tag'li olunca Camera.main YANLIŞ olanı (perspektif default) döndürür
            // ve nameplate + tıklama-raycast bozulur. Tek kamera garantisi için hepsini temizliyoruz.
            foreach (Camera existingCam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(existingCam.gameObject);

            GameObject root = new GameObject(SceneRootName);

            // İzometrik kamera — 30° eğim, 45° yatay döndürme
            // Grid merkezi ≈ (8.2, 0, 6.75) için kamera pozisyonu hesaplanmıştır.
            // Kendi assetlerini ekledikçe bu değerleri Inspector'dan ayarlayabilirsin.
            GameObject cameraGO = new GameObject("Main Camera");
            cameraGO.tag = "MainCamera";
            cameraGO.transform.SetParent(root.transform);
            cameraGO.transform.position = new Vector3(-8f, 15f, -9f);
            cameraGO.transform.rotation = Quaternion.Euler(30f, 45f, 0f);

            Camera cam = cameraGO.AddComponent<Camera>();
            cam.orthographic     = true;
            cam.orthographicSize = 10f;
            cam.nearClipPlane    = 0.1f;
            cam.farClipPlane     = 200f;
            cam.clearFlags       = CameraClearFlags.SolidColor;
            cam.backgroundColor  = new Color(0.04f, 0.03f, 0.07f);

            var urpData = cameraGO.AddComponent<UniversalAdditionalCameraData>();
            urpData.renderShadows        = true;   // gercekci grafik: golgeler acik
            urpData.renderPostProcessing = true;   // Global Volume efektleri (tonemapping/bloom) gorunsun
            urpData.antialiasing         = AntialiasingMode.SubpixelMorphologicalAntiAliasing; // SMAA
            urpData.antialiasingQuality  = AntialiasingQuality.High;
            urpData.requiresColorTexture = false;
            urpData.requiresDepthTexture = false;

            cameraGO.AddComponent<AudioListener>();

            // Işık
            GameObject lightGO = new GameObject("Directional Light");
            lightGO.transform.SetParent(root.transform);
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Light light = lightGO.AddComponent<Light>();
            light.type      = LightType.Directional;
            light.intensity = 1.6f;
            light.color     = new Color(1f, 0.96f, 0.9f); // hafif sicak gunes
            light.shadows   = LightShadows.Soft;          // gercekci yumusak golgeler

            // GameManager
            new GameObject("GameManager").transform.SetParent(root.transform);

            // Gercekci grafik preset'i — Global Volume + post-process (otomatik, her TAM KURULUM'da).
            SetupRealisticGraphics(root.transform);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[TacticalRPG] Faz 0 tamamlandi.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // FAZ 1 — Hex Grid + Navigasyon + AP + Kıyamet (eski 1.1/1.3/1.4/1.5)
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("TacticalRPG/Faz 1 - Hex Grid ve Sistemler", false, 12)]
        public static void SetupPhase1()
        {
            DestroyRoot(SystemsRootName);

            // Klasörler + Asset'ler
            EnsureFolder("Assets/Art");
            EnsureFolder(MaterialsPath);
            EnsureFolder("Assets/Art/Meshes");
            EnsureFolder("Assets/Art/Models");   // Blender FBX dosyaları buraya
            EnsureFolder("Assets/Prefabs");
            EnsureFolder(PrefabsGridPath);
            EnsureFolder("Assets/Data");
            EnsureFolder("Assets/Data/Config");
            EnsureFolder("Assets/Data/Map");     // TilePalette + TileMap

            Material hiddenMat    = GetOrCreateMaterial("FogHidden",    new Color(0.22f, 0.18f, 0.28f));
            Material exploredMat  = GetOrCreateMaterial("FogExplored",  new Color(0.25f, 0.25f, 0.25f));
            Material visibleMat   = GetOrCreateMaterial("FogVisible",   Color.white);
            Material collapsedMat = GetOrCreateMaterial("TileCollapsed",new Color(0.15f, 0.05f, 0.05f));
            GameObject hexCellPrefab = GetOrCreateHexCellPrefab(visibleMat);

            // ── TilePalette — varsayılan giriş (placeholder hex prism) ────────
            const string palettePath = "Assets/Data/Map/TilePalette.asset";
            TilePaletteSO palette = AssetDatabase.LoadAssetAtPath<TilePaletteSO>(palettePath);
            if (palette == null)
            {
                palette = ScriptableObject.CreateInstance<TilePaletteSO>();
                AssetDatabase.CreateAsset(palette, palettePath);
            }
            var palSO = new SerializedObject(palette);
            var tilesArr = palSO.FindProperty("tiles");
            if (tilesArr.arraySize == 0)
            {
                tilesArr.arraySize = 1;
                var e0 = tilesArr.GetArrayElementAtIndex(0);
                e0.FindPropertyRelative("id").stringValue          = "default";
                e0.FindPropertyRelative("displayName").stringValue = "Varsayilan";
                e0.FindPropertyRelative("prefab").objectReferenceValue = hexCellPrefab;
                e0.FindPropertyRelative("isWalkable").boolValue    = true;
                e0.FindPropertyRelative("editorColor").colorValue  = Color.gray;
            }
            palSO.ApplyModifiedProperties();
            EditorUtility.SetDirty(palette);

            // ── TileMap — boş harita, defaultTileId = "default" ───────────────
            const string mapPath = "Assets/Data/Map/TileMap.asset";
            TileMapSO tileMap = AssetDatabase.LoadAssetAtPath<TileMapSO>(mapPath);
            if (tileMap == null)
            {
                tileMap = ScriptableObject.CreateInstance<TileMapSO>();
                AssetDatabase.CreateAsset(tileMap, mapPath);
            }
            var mapSO2 = new SerializedObject(tileMap);
            mapSO2.FindProperty("defaultTileId").stringValue = "default";
            mapSO2.ApplyModifiedProperties();
            EditorUtility.SetDirty(tileMap);

            GameObject root = new GameObject(SystemsRootName);

            // ── HexGridManager ────────────────────────────────────────────────
            GameObject gridGO = new GameObject("HexGridManager");
            gridGO.transform.SetParent(root.transform);
            HexGridManager gridManager = gridGO.AddComponent<HexGridManager>();

            GameObject gridVisualsGO = new GameObject("HexGrid_Visuals");
            gridVisualsGO.transform.SetParent(gridGO.transform);

            var gridSO = new SerializedObject(gridManager);
            gridSO.FindProperty("_hexCellPrefab").objectReferenceValue = hexCellPrefab;
            gridSO.FindProperty("_gridParent").objectReferenceValue    = gridVisualsGO.transform;
            gridSO.FindProperty("_width").intValue                     = 10;
            gridSO.FindProperty("_height").intValue                    = 10;
            gridSO.FindProperty("_hexSize").floatValue                 = 1f;
            gridSO.FindProperty("_tilePalette").objectReferenceValue   = palette;
            gridSO.FindProperty("_tileMap").objectReferenceValue       = tileMap;

            // Watchtower konumları
            var wtProp = gridSO.FindProperty("_watchtowerPositions");
            wtProp.ClearArray();
            var wtCoords = new[] { (3, 5), (1, 2), (4, 8) };
            for (int i = 0; i < wtCoords.Length; i++)
            {
                wtProp.arraySize++;
                var elem = wtProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("Q").intValue = wtCoords[i].Item1;
                elem.FindPropertyRelative("R").intValue = wtCoords[i].Item2;
            }
            gridSO.ApplyModifiedProperties();
            gridManager.GenerateGrid();
            EditorUtility.SetDirty(gridVisualsGO);

            // ── FogOfWarManager ───────────────────────────────────────────────
            GameObject fogGO = new GameObject("FogOfWarManager");
            fogGO.transform.SetParent(root.transform);
            FogOfWarManager fogManager = fogGO.AddComponent<FogOfWarManager>();

            var fogSO = new SerializedObject(fogManager);
            fogSO.FindProperty("_gridManager").objectReferenceValue      = gridManager;
            fogSO.FindProperty("_hiddenMaterial").objectReferenceValue   = hiddenMat;
            fogSO.FindProperty("_exploredMaterial").objectReferenceValue = exploredMat;
            fogSO.FindProperty("_visibleMaterial").objectReferenceValue  = visibleMat;
            fogSO.ApplyModifiedProperties();

            // ── Player — kapsül (kendi karakterinle değiştirene kadar placeholder) ──
            GameObject playerGO = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            playerGO.name = "Player";
            playerGO.transform.SetParent(root.transform);
            playerGO.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
            Object.DestroyImmediate(playerGO.GetComponent<CapsuleCollider>());

            // Turuncu placeholder materyal — model atanınca gizlenir.
            EnsureFolder("Assets/Art/Materials");
            Material playerMat = GetOrCreateMaterial("PlayerPlaceholder", new Color(0.95f, 0.45f, 0.1f));
            playerGO.GetComponent<MeshRenderer>().sharedMaterial = playerMat;

            // Karakter modeli (soyguncu) — KALICI bake: kapsül görseli tamamen kaldırılır, model child
            // olarak sahneye saklanır (editörde de görünür, Play gerekmez), DİK döndürülür, karoya
            // sığacak boya auto-scale edilir, ayağı zemine oturtulur.
            GameObject charModel = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Models/Characters/soyguncu_karakteri.fbx");
            if (charModel != null)
                BakeCharacterModel(playerGO, charModel);

            PlayerController playerCtrl = playerGO.AddComponent<PlayerController>();
            var playerSO = new SerializedObject(playerCtrl);
            playerSO.FindProperty("_gridManager").objectReferenceValue = gridManager;
            playerSO.FindProperty("_fogManager").objectReferenceValue  = fogManager;
            playerSO.FindProperty("_moveSpeed").floatValue             = 8f;
            // TileHeight (0.3) + kapsül yarı-yüksekliği (0.45) + küçük boşluk
            playerSO.FindProperty("_heightOffset").floatValue          = 0.8f;
            playerSO.FindProperty("_visionRange").intValue             = 4;
            playerSO.FindProperty("_watchtowerRevealRange").intValue   = 5;
            playerSO.FindProperty("_startCoord").FindPropertyRelative("Q").intValue = 3;
            playerSO.FindProperty("_startCoord").FindPropertyRelative("R").intValue = 4;
            playerSO.ApplyModifiedProperties();

            // ── GameManager (MapInputHandler + ActionPointManager + MapCollapseManager) ──
            GameObject sceneRoot    = GameObject.Find(SceneRootName);
            GameObject gameManagerGO = sceneRoot != null
                ? sceneRoot.transform.Find("GameManager")?.gameObject
                : null;

            if (gameManagerGO == null)
            {
                gameManagerGO = new GameObject("GameManager");
                gameManagerGO.transform.SetParent(root.transform);
            }

            // MapInputHandler
            var oldInput = gameManagerGO.GetComponent<MapInputHandler>();
            if (oldInput != null) Object.DestroyImmediate(oldInput);
            MapInputHandler inputHandler = gameManagerGO.AddComponent<MapInputHandler>();
            var inputSO = new SerializedObject(inputHandler);
            inputSO.FindProperty("_gridManager").objectReferenceValue = gridManager;
            inputSO.FindProperty("_player").objectReferenceValue      = playerCtrl;
            inputSO.FindProperty("_rayDistance").floatValue           = 300f;
            inputSO.ApplyModifiedProperties();

            // TimeSlotConfig
            const string timeConfigPath = "Assets/Data/Config/TimeSlotConfig.asset";
            TimeSlotConfig timeConfig = AssetDatabase.LoadAssetAtPath<TimeSlotConfig>(timeConfigPath);
            if (timeConfig == null)
            {
                timeConfig = ScriptableObject.CreateInstance<TimeSlotConfig>();
                AssetDatabase.CreateAsset(timeConfig, timeConfigPath);
            }

            // ActionPointManager
            var oldAP = gameManagerGO.GetComponent<ActionPointManager>();
            if (oldAP != null) Object.DestroyImmediate(oldAP);
            ActionPointManager apManager = gameManagerGO.AddComponent<ActionPointManager>();
            var apSO = new SerializedObject(apManager);
            apSO.FindProperty("_player").objectReferenceValue = playerCtrl;
            apSO.FindProperty("_config").objectReferenceValue = timeConfig;
            apSO.ApplyModifiedProperties();

            // CollapseConfig
            const string collapseConfigPath = "Assets/Data/Config/CollapseConfig.asset";
            CollapseConfig collapseConfig = AssetDatabase.LoadAssetAtPath<CollapseConfig>(collapseConfigPath);
            if (collapseConfig == null)
            {
                collapseConfig = ScriptableObject.CreateInstance<CollapseConfig>();
                AssetDatabase.CreateAsset(collapseConfig, collapseConfigPath);
            }

            // MapCollapseManager
            var oldCollapse = gameManagerGO.GetComponent<MapCollapseManager>();
            if (oldCollapse != null) Object.DestroyImmediate(oldCollapse);
            MapCollapseManager collapseManager = gameManagerGO.AddComponent<MapCollapseManager>();
            var collapseSO = new SerializedObject(collapseManager);
            collapseSO.FindProperty("_gridManager").objectReferenceValue       = gridManager;
            collapseSO.FindProperty("_apManager").objectReferenceValue         = apManager;
            collapseSO.FindProperty("_player").objectReferenceValue            = playerCtrl;
            collapseSO.FindProperty("_config").objectReferenceValue            = collapseConfig;
            collapseSO.FindProperty("_collapsedMaterial").objectReferenceValue = collapsedMat;
            collapseSO.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[TacticalRPG] Faz 1 tamamlandi (Grid + Navigasyon + AP + Kiyamet).");
        }

        // ─────────────────────────────────────────────────────────────────────
        // FAZ 2 — Karakter Sistemi + Öz + Kam Mana
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("TacticalRPG/Faz 2 - Karakter Sistemi", false, 13)]
        public static void SetupPhase2()
        {
            GameObject systemsRoot = GameObject.Find(SystemsRootName);
            GameObject sceneRoot   = GameObject.Find(SceneRootName);

            if (systemsRoot == null)
            {
                EditorUtility.DisplayDialog("Hata", "Once Faz 1'i calistirin!", "Tamam");
                return;
            }

            ActionPointManager apManager = FindComponentAnywhere<ActionPointManager>();
            if (apManager == null)
            {
                EditorUtility.DisplayDialog("Hata", "ActionPointManager bulunamadi! Once Faz 1'i calistirin.", "Tamam");
                return;
            }

            EnsureFolder("Assets/Data");
            EnsureFolder("Assets/Data/Characters");

            CharacterClassData kamData = GetOrCreateCharacterSO(
                path: "Assets/Data/Characters/Kam.asset", className: "Kam",
                lore: "Kadim buyu bilgeligi ile donanmis gizemli saman.",
                maxHP: 8, attack: 4, defense: 1, moveRange: 3,
                essenceCosts: new[] { 0, 5, 12 },
                hpMult:  new[] { 1f, 1.25f, 1.6f  },
                atkMult: new[] { 1f, 1.3f,  1.7f  },
                defMult: new[] { 1f, 1.1f,  1.3f  },
                hasMana: true, maxMana: 10,
                isCommander: true, unitColor: new Color(1f, 0.80f, 0.15f)); // Kam = altın (komutan)

            // Kam'a soyguncu modelini ata → savaşta da kapsül yerine model (DeploymentManager bakar).
            // Overworld bake'iyle aynı euler/boy (tutarlı duruş).
            GameObject kamModel = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Models/Characters/soyguncu_karakteri.fbx");
            if (kamModel != null)
            {
                var kamModelSO = new SerializedObject(kamData);
                kamModelSO.FindProperty("_unitModel").objectReferenceValue = kamModel;
                kamModelSO.FindProperty("_unitModelHeight").floatValue      = CharacterModelHeight;
                kamModelSO.FindProperty("_unitModelEuler").vector3Value     = CharacterModelEuler;
                kamModelSO.ApplyModifiedProperties();
            }

            CharacterClassData warriorData = GetOrCreateCharacterSO(
                path: "Assets/Data/Characters/Savascı.asset", className: "Savasci",
                lore: "Kilic ustasi. On saflarda durur, darbeleri gogusler.",
                maxHP: 14, attack: 5, defense: 3, moveRange: 3,
                essenceCosts: new[] { 0, 6, 14 },
                hpMult:  new[] { 1f, 1.35f, 1.75f },
                atkMult: new[] { 1f, 1.2f,  1.5f  },
                defMult: new[] { 1f, 1.25f, 1.6f  },
                hasMana: false, maxMana: 0,
                unitColor: new Color(0.25f, 0.45f, 0.95f)); // Savasci = mavi

            CharacterClassData rangerData = GetOrCreateCharacterSO(
                path: "Assets/Data/Characters/Ranger.asset", className: "Ranger",
                lore: "Uzak mesafe uzmani. Gorunmez olur, ince stratejiler kurar.",
                maxHP: 10, attack: 6, defense: 1, moveRange: 4,
                essenceCosts: new[] { 0, 5, 11 },
                hpMult:  new[] { 1f, 1.2f,  1.55f },
                atkMult: new[] { 1f, 1.25f, 1.6f  },
                defMult: new[] { 1f, 1.1f,  1.25f },
                hasMana: false, maxMana: 0,
                unitColor: new Color(0.20f, 0.80f, 0.75f)); // Ranger = turkuaz

            GameObject gameManagerGO = sceneRoot != null
                ? sceneRoot.transform.Find("GameManager")?.gameObject
                : systemsRoot.transform.Find("GameManager")?.gameObject;

            if (gameManagerGO == null)
            {
                gameManagerGO = new GameObject("GameManager");
                gameManagerGO.transform.SetParent(systemsRoot.transform);
            }

            // EssenceWallet (çok-tipli öz — eski tek-havuz EssenceManager yerine)
            var oldEM = gameManagerGO.GetComponent<EssenceWallet>();
            if (oldEM != null) Object.DestroyImmediate(oldEM);
            EssenceWallet wallet = gameManagerGO.AddComponent<EssenceWallet>();
            var emSO = new SerializedObject(wallet);
            emSO.FindProperty("_startAtes").intValue   = 4; // test: bir-iki birim üretmeye yeter
            emSO.FindProperty("_startSu").intValue     = 4;
            emSO.FindProperty("_startToprak").intValue = 4;
            emSO.ApplyModifiedProperties();

            // PartyManager — başlangıçta SADECE Kam; Savaşçı/Ranger özle üretilir (Faz D tarifleri)
            var oldPM = gameManagerGO.GetComponent<PartyManager>();
            if (oldPM != null) Object.DestroyImmediate(oldPM);
            PartyManager partyManager = gameManagerGO.AddComponent<PartyManager>();
            var pmSO = new SerializedObject(partyManager);
            pmSO.FindProperty("_wallet").objectReferenceValue = wallet;
            var classList = pmSO.FindProperty("_startingClasses");
            classList.ClearArray();
            classList.arraySize = 1;
            classList.GetArrayElementAtIndex(0).objectReferenceValue = kamData;
            // warriorData/rangerData asset olarak yine üretildi (üstte) — Faz D tarifleri path'ten yükler.
            pmSO.ApplyModifiedProperties();

            // KamManaManager
            var oldKam = gameManagerGO.GetComponent<KamManaManager>();
            if (oldKam != null) Object.DestroyImmediate(oldKam);
            KamManaManager kamMana = gameManagerGO.AddComponent<KamManaManager>();
            var kamSO = new SerializedObject(kamMana);
            kamSO.FindProperty("_apManager").objectReferenceValue = apManager;
            kamSO.FindProperty("_maxMana").intValue               = 10;
            kamSO.FindProperty("_manaRegenPerSlot").intValue      = 2;
            kamSO.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[TacticalRPG] Faz 2 tamamlandi (Karakter Sistemi).");
        }

        // ─────────────────────────────────────────────────────────────────────
        // DEBUG HUD
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("TacticalRPG/Debug HUD - Ekle", false, 14)]
        public static void SetupDebugHUD()
        {
            ActionPointManager apManager       = FindComponentAnywhere<ActionPointManager>();
            MapCollapseManager collapseManager = FindComponentAnywhere<MapCollapseManager>();
            EssenceWallet      wallet          = FindComponentAnywhere<EssenceWallet>();
            KamManaManager     kamMana         = FindComponentAnywhere<KamManaManager>();
            GameStateManager   gsm             = FindComponentAnywhere<GameStateManager>(); // TAM KURULUM'da henüz null olabilir → Faz A bağlar

            if (apManager == null)
            {
                EditorUtility.DisplayDialog("Hata", "ActionPointManager bulunamadi! Once Faz 1'i calistirin.", "Tamam");
                return;
            }

            GameObject oldCanvas = GameObject.Find("DebugHUD_Canvas");
            if (oldCanvas != null) Object.DestroyImmediate(oldCanvas);

            GameObject canvasGO = new GameObject("DebugHUD_Canvas", typeof(RectTransform));
            GameObject sceneRoot = GameObject.Find(SceneRootName);
            if (sceneRoot != null) canvasGO.transform.SetParent(sceneRoot.transform);

            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            float yStart = -20f;
            float yStep  = -48f;
            int   row    = 0;

            TextMeshProUGUI timeLabel = CreateTMPLabel(
                canvasGO.transform, "Label_Time", "Gun 1  ·  Sabah",
                new Vector2(20f, yStart + yStep * row++), new Vector2(400f, 46f),
                Color.white, 28f);

            TextMeshProUGUI apLabel = CreateTMPLabel(
                canvasGO.transform, "Label_AP", "AP  ■■■  3/3",
                new Vector2(20f, yStart + yStep * row++), new Vector2(400f, 44f),
                new Color(1f, 0.85f, 0.2f), 24f);

            TextMeshProUGUI essLabel = CreateTMPLabel(
                canvasGO.transform, "Label_Essence", "Oz  0",
                new Vector2(20f, yStart + yStep * row++), new Vector2(300f, 40f),
                new Color(0.6f, 1f, 0.6f), 22f);

            TextMeshProUGUI manaLabel = null;
            if (kamMana != null)
            {
                manaLabel = CreateTMPLabel(
                    canvasGO.transform, "Label_KamMana", "Mana  10/10",
                    new Vector2(20f, yStart + yStep * row++), new Vector2(500f, 40f),
                    new Color(0.5f, 0.8f, 1f), 20f);
            }
            else row++;

            TextMeshProUGUI collapseLabel = CreateTMPLabel(
                canvasGO.transform, "Label_Collapse", "HARITA COKUYOR",
                new Vector2(20f, yStart + yStep * row), new Vector2(500f, 44f),
                new Color(1f, 0.25f, 0.15f), 22f);
            collapseLabel.gameObject.SetActive(false);

            DebugHUD hud = canvasGO.AddComponent<DebugHUD>();
            var hudSO = new SerializedObject(hud);
            hudSO.FindProperty("_state").objectReferenceValue           = gsm;
            hudSO.FindProperty("_apManager").objectReferenceValue       = apManager;
            hudSO.FindProperty("_collapseManager").objectReferenceValue = collapseManager;
            hudSO.FindProperty("_wallet").objectReferenceValue          = wallet;
            hudSO.FindProperty("_kamMana").objectReferenceValue         = kamMana;
            hudSO.FindProperty("_timeLabel").objectReferenceValue       = timeLabel;
            hudSO.FindProperty("_apLabel").objectReferenceValue         = apLabel;
            hudSO.FindProperty("_essenceLabel").objectReferenceValue    = essLabel;
            hudSO.FindProperty("_kamManaLabel").objectReferenceValue    = manaLabel;
            hudSO.FindProperty("_collapseLabel").objectReferenceValue   = collapseLabel;
            hudSO.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Debug.Log("[TacticalRPG] Debug HUD kuruldu.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // FAZ 3 (test) — Yetenek kullanımı dikey dilimi
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("TacticalRPG/Faz 3 - Yetenek Test Kurulumu", false, 15)]
        public static void SetupPhase3()
        {
            GameObject sceneRoot     = GameObject.Find(SceneRootName);
            GameObject gameManagerGO = sceneRoot != null
                ? sceneRoot.transform.Find("GameManager")?.gameObject
                : null;

            if (gameManagerGO == null)
            {
                EditorUtility.DisplayDialog("Hata",
                    "GameManager bulunamadi! Once TAM KURULUM (veya Faz 0-2) calistir.", "Tamam");
                return;
            }

            HexGridManager  gridManager = FindComponentAnywhere<HexGridManager>();
            PlayerController player      = FindComponentAnywhere<PlayerController>();
            PartyManager     party       = FindComponentAnywhere<PartyManager>();
            KamManaManager   kamMana     = FindComponentAnywhere<KamManaManager>();
            MapInputHandler  input       = FindComponentAnywhere<MapInputHandler>();

            if (gridManager == null || player == null || party == null || kamMana == null || input == null)
            {
                EditorUtility.DisplayDialog("Hata",
                    "Gerekli sistemler eksik (Grid/Player/Party/KamMana/Input).\nOnce Faz 1 ve Faz 2'yi calistir.", "Tamam");
                return;
            }

            // ── Kam'a 3 yeteneği ata ──────────────────────────────────────────
            CharacterClassData kamData = AssetDatabase.LoadAssetAtPath<CharacterClassData>(
                "Assets/Data/Characters/Kam.asset");
            KamAbilityData[] abilities =
            {
                AssetDatabase.LoadAssetAtPath<KamAbilityData>("Assets/Data/Abilities/AtesTopu.asset"),
                AssetDatabase.LoadAssetAtPath<KamAbilityData>("Assets/Data/Abilities/Sifa.asset"),
                AssetDatabase.LoadAssetAtPath<KamAbilityData>("Assets/Data/Abilities/RuhKalkani.asset"),
            };

            if (kamData != null)
            {
                var kamSO    = new SerializedObject(kamData);
                var listProp = kamSO.FindProperty("_abilities");
                listProp.ClearArray();
                int count = 0;
                foreach (var ab in abilities) if (ab != null) count++;
                listProp.arraySize = count;
                int idx = 0;
                foreach (var ab in abilities)
                    if (ab != null) listProp.GetArrayElementAtIndex(idx++).objectReferenceValue = ab;
                kamSO.ApplyModifiedProperties();
                EditorUtility.SetDirty(kamData);
            }
            else
            {
                Debug.LogWarning("[Faz3] Kam.asset bulunamadi — yetenekler atanamadi. Once Faz 2'yi calistir.");
            }

            // ── UnitManager (GameManager üstünde) ─────────────────────────────
            var oldUM = gameManagerGO.GetComponent<UnitManager>();
            if (oldUM != null) Object.DestroyImmediate(oldUM);
            UnitManager unitManager = gameManagerGO.AddComponent<UnitManager>();

            // ── Kukla düşman (kırmızı kapsül) ─────────────────────────────────
            GameObject oldEnemy = GameObject.Find("Enemy_Dummy");
            if (oldEnemy != null) Object.DestroyImmediate(oldEnemy);

            GameObject enemyGO = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemyGO.name = "Enemy_Dummy";
            enemyGO.transform.SetParent(sceneRoot.transform);
            enemyGO.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
            Object.DestroyImmediate(enemyGO.GetComponent<CapsuleCollider>());

            Material enemyMat = GetOrCreateMaterial("EnemyPlaceholder", new Color(0.85f, 0.15f, 0.15f));
            enemyGO.GetComponent<MeshRenderer>().sharedMaterial = enemyMat;

            var enemyCoord = new HexCoordinate(5, 4);
            enemyGO.transform.position = enemyCoord.ToWorldPosition(gridManager.HexSize) + Vector3.up * 0.8f;

            Unit enemyUnit = enemyGO.AddComponent<Unit>();
            var enemySO = new SerializedObject(enemyUnit);
            enemySO.FindProperty("_displayName").stringValue          = "Kukla";
            enemySO.FindProperty("_team").enumValueIndex              = (int)UnitTeam.Enemy;
            enemySO.FindProperty("_maxHP").intValue                   = 12;
            enemySO.FindProperty("_heightOffset").floatValue          = 0.8f;
            enemySO.FindProperty("_gridManager").objectReferenceValue = gridManager;
            enemySO.FindProperty("_unitManager").objectReferenceValue = unitManager;
            enemySO.FindProperty("_coord").FindPropertyRelative("Q").intValue = 5;
            enemySO.FindProperty("_coord").FindPropertyRelative("R").intValue = 4;
            enemySO.ApplyModifiedProperties();

            // ── AbilityCaster (GameManager üstünde) ───────────────────────────
            // NOT: Faz 3 eski test sandbox'ıdır; gerçek savaş büyüsü artık Faz C4'tedir
            // (turn-tabanlı, komutan birimi origin). Burada sadece referansları bağlarız;
            // tam işlev için Faz C4 + tur sistemi (Faz C3) gerekir.
            var oldCaster = gameManagerGO.GetComponent<AbilityCaster>();
            if (oldCaster != null) Object.DestroyImmediate(oldCaster);
            AbilityCaster caster = gameManagerGO.AddComponent<AbilityCaster>();
            var casterSO = new SerializedObject(caster);
            casterSO.FindProperty("_kamMana").objectReferenceValue     = kamMana;
            casterSO.FindProperty("_unitManager").objectReferenceValue = unitManager;
            casterSO.ApplyModifiedProperties();

            // ── MapInputHandler'a caster'ı bağla ──────────────────────────────
            var inputSO = new SerializedObject(input);
            inputSO.FindProperty("_caster").objectReferenceValue = caster;
            inputSO.ApplyModifiedProperties();

            // ── AbilityTestHUD (GameManager üstünde) ──────────────────────────
            var oldHud = gameManagerGO.GetComponent<AbilityTestHUD>();
            if (oldHud != null) Object.DestroyImmediate(oldHud);
            AbilityTestHUD hud = gameManagerGO.AddComponent<AbilityTestHUD>();
            var hudSO = new SerializedObject(hud);
            hudSO.FindProperty("_caster").objectReferenceValue      = caster;
            hudSO.FindProperty("_kamMana").objectReferenceValue     = kamMana;
            hudSO.FindProperty("_unitManager").objectReferenceValue = unitManager;
            hudSO.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Faz 3 — Yetenek Testi Hazir",
                "Kurulanlar:\n" +
                "  • Kukla dusman (kirmizi kapsul, Q5 R4, 12 HP)\n" +
                "  • UnitManager + AbilityCaster + AbilityTestHUD\n" +
                "  • Kam'a 3 yetenek atandi\n\n" +
                "Play'e bas, sonra:\n" +
                "  1 / 2 / 3 ile yetenek sec  →  kuklaya sol tikla.\n" +
                "  Sag ust kosedeki panelden mana ve HP'yi izle.",
                "Tamam");

            Debug.Log("[TacticalRPG] Faz 3 (yetenek testi) kuruldu.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // FAZ A — Overworld ↔ Savaş geçişi (durum makinesi + görev alanları)
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("TacticalRPG/Faz A - Overworld-Savas Gecisi", false, 16)]
        public static void SetupPhaseA()
        {
            GameObject sceneRoot     = GameObject.Find(SceneRootName);
            GameObject gameManagerGO = sceneRoot != null
                ? sceneRoot.transform.Find("GameManager")?.gameObject
                : null;

            if (gameManagerGO == null)
            {
                EditorUtility.DisplayDialog("Hata",
                    "GameManager bulunamadi! Once TAM KURULUM (veya Faz 0-2) calistir.", "Tamam");
                return;
            }

            HexGridManager  gridManager = FindComponentAnywhere<HexGridManager>();
            FogOfWarManager fogManager  = FindComponentAnywhere<FogOfWarManager>();
            PlayerController player      = FindComponentAnywhere<PlayerController>();
            MapInputHandler input        = FindComponentAnywhere<MapInputHandler>();
            ActionPointManager apManager = FindComponentAnywhere<ActionPointManager>();

            if (gridManager == null || fogManager == null || player == null || input == null)
            {
                EditorUtility.DisplayDialog("Hata",
                    "Gerekli sistemler eksik (Grid/Fog/Player/Input).\nOnce Faz 1'i calistir.", "Tamam");
                return;
            }

            // ── Savaş haritası (TileMap) — overworld'den farkli, default 'kaya' ──
            EnsureFolder("Assets/Data/Map");
            const string combatMapPath = "Assets/Data/Map/CombatTileMap.asset";
            TileMapSO combatMap = AssetDatabase.LoadAssetAtPath<TileMapSO>(combatMapPath);
            if (combatMap == null)
            {
                combatMap = ScriptableObject.CreateInstance<TileMapSO>();
                AssetDatabase.CreateAsset(combatMap, combatMapPath);
            }
            combatMap.defaultTileId = "kaya";
            combatMap.assignments.Clear();
            EditorUtility.SetDirty(combatMap);

            // ── Görev verisi (MissionData) ────────────────────────────────────
            EnsureFolder("Assets/Data/Missions");
            const string missionPath = "Assets/Data/Missions/Mission1.asset";
            MissionData mission = AssetDatabase.LoadAssetAtPath<MissionData>(missionPath);
            if (mission == null)
            {
                mission = ScriptableObject.CreateInstance<MissionData>();
                AssetDatabase.CreateAsset(mission, missionPath);
            }
            var missionSO = new SerializedObject(mission);
            missionSO.FindProperty("_displayName").stringValue          = "Goblin Pususu";
            missionSO.FindProperty("_description").stringValue           = "Ilk test gorevi.";
            missionSO.FindProperty("_combatMap").objectReferenceValue    = combatMap;
            missionSO.ApplyModifiedProperties();
            EditorUtility.SetDirty(mission);

            // ── GameStateManager ──────────────────────────────────────────────
            var oldGSM = gameManagerGO.GetComponent<GameStateManager>();
            if (oldGSM != null) Object.DestroyImmediate(oldGSM);
            GameStateManager gsm = gameManagerGO.AddComponent<GameStateManager>();
            var gsmSO = new SerializedObject(gsm);
            gsmSO.FindProperty("_grid").objectReferenceValue      = gridManager;
            gsmSO.FindProperty("_fog").objectReferenceValue       = fogManager;
            gsmSO.FindProperty("_player").objectReferenceValue    = player;
            gsmSO.FindProperty("_apManager").objectReferenceValue = apManager;
            gsmSO.ApplyModifiedProperties();

            // ── DebugHUD'a state bağla ────────────────────────────────────────
            // DebugHUD, GameStateManager'dan ÖNCE kurulur (TAM KURULUM sırası: HUD → Faz A),
            // bu yüzden _state burada bağlanır. Böylece HUD savaş/yerleştirmede gizlenir.
            DebugHUD debugHud = FindComponentAnywhere<DebugHUD>();
            if (debugHud != null)
            {
                var dhudSO = new SerializedObject(debugHud);
                dhudSO.FindProperty("_state").objectReferenceValue = gsm;
                dhudSO.ApplyModifiedProperties();
            }

            // ── MissionManager (1 görev: Q5 R5) ───────────────────────────────
            var oldMM = gameManagerGO.GetComponent<MissionManager>();
            if (oldMM != null) Object.DestroyImmediate(oldMM);
            MissionManager mm = gameManagerGO.AddComponent<MissionManager>();
            var mmSO = new SerializedObject(mm);
            mmSO.FindProperty("_grid").objectReferenceValue         = gridManager;
            mmSO.FindProperty("_stateManager").objectReferenceValue = gsm;
            var missionsProp = mmSO.FindProperty("_missions");
            missionsProp.ClearArray();
            missionsProp.arraySize = 1;
            var elem0 = missionsProp.GetArrayElementAtIndex(0);
            elem0.FindPropertyRelative("coord").FindPropertyRelative("Q").intValue = 5;
            elem0.FindPropertyRelative("coord").FindPropertyRelative("R").intValue = 5;
            elem0.FindPropertyRelative("mission").objectReferenceValue = mission;
            mmSO.ApplyModifiedProperties();

            // ── OverworldCombatHUD ────────────────────────────────────────────
            var oldOH = gameManagerGO.GetComponent<OverworldCombatHUD>();
            if (oldOH != null) Object.DestroyImmediate(oldOH);
            OverworldCombatHUD och = gameManagerGO.AddComponent<OverworldCombatHUD>();
            var ochSO = new SerializedObject(och);
            ochSO.FindProperty("_stateManager").objectReferenceValue   = gsm;
            ochSO.FindProperty("_missionManager").objectReferenceValue = mm;     // yakınlık istemi
            ochSO.FindProperty("_player").objectReferenceValue         = player; // "Savaşa Gir" mesafesi
            ochSO.ApplyModifiedProperties();

            // ── MapInputHandler'a state + mission bağla ───────────────────────
            var inputSO = new SerializedObject(input);
            inputSO.FindProperty("_stateManager").objectReferenceValue   = gsm;
            inputSO.FindProperty("_missionManager").objectReferenceValue = mm;
            inputSO.FindProperty("_caster").objectReferenceValue         = null; // test caster bağını çöz
            inputSO.ApplyModifiedProperties();

            // ── Faz 3 test iskelesini kaldır (ana haritada artık gerekmiyor) ──
            GameObject testEnemy = GameObject.Find("Enemy_Dummy");
            if (testEnemy != null) Object.DestroyImmediate(testEnemy);
            var tc = gameManagerGO.GetComponent<AbilityCaster>();  if (tc != null) Object.DestroyImmediate(tc);
            var th = gameManagerGO.GetComponent<AbilityTestHUD>(); if (th != null) Object.DestroyImmediate(th);
            var tu = gameManagerGO.GetComponent<UnitManager>();    if (tu != null) Object.DestroyImmediate(tu);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!_silentSetup) EditorUtility.DisplayDialog("Faz A — Overworld/Savas Gecisi Hazir",
                "Kurulanlar:\n" +
                "  • GameStateManager + MissionManager + OverworldCombatHUD\n" +
                "  • Savas haritasi (CombatTileMap) + 1 gorev (Goblin Pususu @ Q5 R5)\n" +
                "  • Q5 R5 hex'inde sari marker\n" +
                "  • Savasa girmek artik 1 AP harcar\n" +
                "  • Faz 3 testi kaldirildi (kukla dusman + buyu arayuzu)\n\n" +
                "Play'e bas:\n" +
                "  Sari marker'a (Q5 R5) tikla → 'Evet' → 1 AP gider, savas haritasi acilir.\n" +
                "  'Geri Don' ile overworld'e don.",
                "Tamam");

            Debug.Log("[TacticalRPG] Faz A (overworld-savas gecisi) kuruldu.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // FAZ B — Deployment (öz ile birim yerleştirme)
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("TacticalRPG/Faz B - Deployment (Birim Yerlestirme)", false, 17)]
        public static void SetupPhaseB()
        {
            GameObject sceneRoot     = GameObject.Find(SceneRootName);
            GameObject gameManagerGO = sceneRoot != null
                ? sceneRoot.transform.Find("GameManager")?.gameObject
                : null;

            if (gameManagerGO == null)
            {
                EditorUtility.DisplayDialog("Hata",
                    "GameManager bulunamadi! Once TAM KURULUM + Faz A calistir.", "Tamam");
                return;
            }

            HexGridManager   grid    = FindComponentAnywhere<HexGridManager>();
            PartyManager     party   = FindComponentAnywhere<PartyManager>();
            MapInputHandler  input   = FindComponentAnywhere<MapInputHandler>();
            GameStateManager gsm     = FindComponentAnywhere<GameStateManager>();

            if (grid == null || party == null || input == null || gsm == null)
            {
                EditorUtility.DisplayDialog("Hata",
                    "Gerekli sistemler eksik (Grid/Party/Input/GameState).\n" +
                    "Once Faz 2 ve Faz A'yi calistir.", "Tamam");
                return;
            }

            // Deployment artık bedava (öz birim üretirken harcanır) — başlangıç özü Faz D'de.

            // ── UnitManager (Faz A silmisti — geri ekle) ──
            var oldUM = gameManagerGO.GetComponent<UnitManager>();
            if (oldUM != null) Object.DestroyImmediate(oldUM);
            UnitManager unitManager = gameManagerGO.AddComponent<UnitManager>();

            // ── DeploymentManager ──
            var oldDM = gameManagerGO.GetComponent<DeploymentManager>();
            if (oldDM != null) Object.DestroyImmediate(oldDM);
            DeploymentManager dm = gameManagerGO.AddComponent<DeploymentManager>();
            var dmSO = new SerializedObject(dm);
            dmSO.FindProperty("_stateManager").objectReferenceValue = gsm;
            dmSO.FindProperty("_grid").objectReferenceValue         = grid;
            dmSO.FindProperty("_unitManager").objectReferenceValue  = unitManager;
            dmSO.FindProperty("_party").objectReferenceValue        = party;
            dmSO.FindProperty("_deployZoneRows").intValue           = 2;
            dmSO.ApplyModifiedProperties();

            // ── DeploymentHUD ──
            var oldDH = gameManagerGO.GetComponent<DeploymentHUD>();
            if (oldDH != null) Object.DestroyImmediate(oldDH);
            DeploymentHUD dh = gameManagerGO.AddComponent<DeploymentHUD>();
            var dhSO = new SerializedObject(dh);
            dhSO.FindProperty("_state").objectReferenceValue      = gsm;
            dhSO.FindProperty("_deployment").objectReferenceValue = dm;
            dhSO.FindProperty("_party").objectReferenceValue      = party;
            dhSO.ApplyModifiedProperties();

            // ── MapInputHandler'a deployment bagla ──
            var inputSO = new SerializedObject(input);
            inputSO.FindProperty("_deployment").objectReferenceValue = dm;
            inputSO.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!_silentSetup) EditorUtility.DisplayDialog("Faz B — Deployment Hazir",
                "Kurulanlar:\n" +
                "  • UnitManager (geri eklendi) + DeploymentManager + DeploymentHUD\n" +
                "  • Baslangic ozu 20 yapildi (test icin)\n" +
                "  • MapInputHandler deployment'a baglandi\n\n" +
                "Play'e bas:\n" +
                "  1) Sari marker'a (Q5 R5) tikla → 'Evet' → savas haritasi + YERLESTIRME acilir\n" +
                "  2) Alt 2 satir mavi pedlerle isaretli (yerlestirme bolgesi)\n" +
                "  3) Sol panelden kart sec → mavi pede tikla (oz harcanir, birim spawn olur)\n" +
                "  4) 'Savasi Baslat' → combat. 'Geri Don' → overworld (birimler temizlenir).",
                "Tamam");

            Debug.Log("[TacticalRPG] Faz B (deployment) kuruldu.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // FAZ C — Düşman roster spawn (savaş alanına düşman birimleri)
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("TacticalRPG/Faz C - Dusman Spawn", false, 18)]
        public static void SetupPhaseC()
        {
            GameObject sceneRoot     = GameObject.Find(SceneRootName);
            GameObject gameManagerGO = sceneRoot != null
                ? sceneRoot.transform.Find("GameManager")?.gameObject
                : null;

            if (gameManagerGO == null)
            {
                EditorUtility.DisplayDialog("Hata",
                    "GameManager bulunamadi! Once TAM KURULUM + Faz A + Faz B calistir.", "Tamam");
                return;
            }

            HexGridManager   grid        = FindComponentAnywhere<HexGridManager>();
            GameStateManager gsm         = FindComponentAnywhere<GameStateManager>();
            UnitManager      unitManager = FindComponentAnywhere<UnitManager>();

            if (grid == null || gsm == null || unitManager == null)
            {
                EditorUtility.DisplayDialog("Hata",
                    "Gerekli sistemler eksik (Grid/GameState/UnitManager).\n" +
                    "Once Faz A ve Faz B'yi calistir.", "Tamam");
                return;
            }

            // ── Düşman sınıfı: Goblin (kartlı düşman, yakın dövüş) ─────────────
            EnsureFolder("Assets/Data");
            EnsureFolder("Assets/Data/Characters");
            CharacterClassData goblinData = GetOrCreateCharacterSO(
                path: "Assets/Data/Characters/Goblin.asset", className: "Goblin",
                lore: "Erlik'in zayif ama kalabalik askerleri. Yakin dovusur.",
                maxHP: 8, attack: 3, defense: 0, moveRange: 3,
                essenceCosts: new[] { 0, 5, 12 },
                hpMult:  new[] { 1f, 1.3f,  1.7f },
                atkMult: new[] { 1f, 1.2f,  1.5f },
                defMult: new[] { 1f, 1.15f, 1.4f },
                hasMana: false, maxMana: 0,
                speed: 4, attackRange: 1);

            // ── Mission1 roster'ini 3 Goblin ile doldur (üst bölge) ───────────
            const string missionPath = "Assets/Data/Missions/Mission1.asset";
            MissionData mission = AssetDatabase.LoadAssetAtPath<MissionData>(missionPath);
            if (mission == null)
            {
                EditorUtility.DisplayDialog("Hata",
                    "Mission1.asset bulunamadi! Once Faz A'yi calistir.", "Tamam");
                return;
            }

            var missionSO  = new SerializedObject(mission);
            var rosterProp = missionSO.FindProperty("_enemyRoster");
            rosterProp.ClearArray();
            var enemyCoords = new[] { (2, 7), (4, 7), (3, 8) }; // deploy zonundan (R<2) uzak
            rosterProp.arraySize = enemyCoords.Length;
            for (int i = 0; i < enemyCoords.Length; i++)
            {
                var e = rosterProp.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("enemyClass").objectReferenceValue              = goblinData;
                e.FindPropertyRelative("coord").FindPropertyRelative("Q").intValue     = enemyCoords[i].Item1;
                e.FindPropertyRelative("coord").FindPropertyRelative("R").intValue     = enemyCoords[i].Item2;
                e.FindPropertyRelative("level").intValue                               = 1;
            }
            missionSO.ApplyModifiedProperties();
            EditorUtility.SetDirty(mission);

            // ── EnemySpawner (GameManager üstünde) ─────────────────────────────
            var oldES = gameManagerGO.GetComponent<EnemySpawner>();
            if (oldES != null) Object.DestroyImmediate(oldES);
            EnemySpawner spawner = gameManagerGO.AddComponent<EnemySpawner>();
            var esSO = new SerializedObject(spawner);
            esSO.FindProperty("_stateManager").objectReferenceValue = gsm;
            esSO.FindProperty("_grid").objectReferenceValue         = grid;
            esSO.FindProperty("_unitManager").objectReferenceValue  = unitManager;
            esSO.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!_silentSetup) EditorUtility.DisplayDialog("Faz C — Dusman Spawn Hazir",
                "Kurulanlar:\n" +
                "  • Goblin dusman sinifi (Assets/Data/Characters/Goblin.asset)\n" +
                "  • Mission1 roster'ina 3 Goblin (Q2R7, Q4R7, Q3R8)\n" +
                "  • EnemySpawner GameManager'a eklendi + wire\n\n" +
                "Play'e bas:\n" +
                "  Sari marker (Q5 R5) → 'Evet' → savas haritasi + YERLESTIRME.\n" +
                "  Ust bolgede 3 kirmizi Goblin gorunur (deployment sirasinda).\n" +
                "  'Geri Don' ile dusmanlar temizlenir.\n\n" +
                "NOT: Tur sistemi + hareket + saldiri Faz C3'te gelecek.",
                "Tamam");

            Debug.Log("[TacticalRPG] Faz C (dusman spawn) kuruldu.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // FAZ C3 — Tur sistemi (initiative + hareket + saldırı + AI + win/lose)
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("TacticalRPG/Faz C3 - Tur Sistemi (Savas)", false, 19)]
        public static void SetupPhaseC3()
        {
            GameObject sceneRoot     = GameObject.Find(SceneRootName);
            GameObject gameManagerGO = sceneRoot != null
                ? sceneRoot.transform.Find("GameManager")?.gameObject
                : null;

            if (gameManagerGO == null)
            {
                EditorUtility.DisplayDialog("Hata",
                    "GameManager bulunamadi! Once TAM KURULUM + Faz A + Faz B + Faz C calistir.", "Tamam");
                return;
            }

            HexGridManager   grid  = FindComponentAnywhere<HexGridManager>();
            GameStateManager gsm   = FindComponentAnywhere<GameStateManager>();
            UnitManager      um    = FindComponentAnywhere<UnitManager>();
            MapInputHandler  input = FindComponentAnywhere<MapInputHandler>();

            if (grid == null || gsm == null || um == null || input == null)
            {
                EditorUtility.DisplayDialog("Hata",
                    "Gerekli sistemler eksik (Grid/GameState/UnitManager/Input).\n" +
                    "Once Faz A, Faz B ve Faz C'yi calistir.", "Tamam");
                return;
            }

            // ── TurnManager ───────────────────────────────────────────────────
            var oldTM = gameManagerGO.GetComponent<TurnManager>();
            if (oldTM != null) Object.DestroyImmediate(oldTM);
            TurnManager tm = gameManagerGO.AddComponent<TurnManager>();
            var tmSO = new SerializedObject(tm);
            tmSO.FindProperty("_stateManager").objectReferenceValue = gsm;
            tmSO.FindProperty("_grid").objectReferenceValue         = grid;
            tmSO.FindProperty("_unitManager").objectReferenceValue  = um;
            tmSO.ApplyModifiedProperties();

            // ── CombatHighlighter ─────────────────────────────────────────────
            var oldCH = gameManagerGO.GetComponent<CombatHighlighter>();
            if (oldCH != null) Object.DestroyImmediate(oldCH);
            CombatHighlighter ch = gameManagerGO.AddComponent<CombatHighlighter>();
            var chSO = new SerializedObject(ch);
            chSO.FindProperty("_turnManager").objectReferenceValue = tm;
            chSO.FindProperty("_grid").objectReferenceValue        = grid;
            chSO.ApplyModifiedProperties();

            // ── CombatHUD ─────────────────────────────────────────────────────
            var oldHud = gameManagerGO.GetComponent<CombatHUD>();
            if (oldHud != null) Object.DestroyImmediate(oldHud);
            CombatHUD hud = gameManagerGO.AddComponent<CombatHUD>();
            var hudSO = new SerializedObject(hud);
            hudSO.FindProperty("_state").objectReferenceValue       = gsm;
            hudSO.FindProperty("_turnManager").objectReferenceValue = tm;
            hudSO.ApplyModifiedProperties();

            // ── CombatNameplateHUD (birim ustu isim + can bari) ───────────────
            var oldNP = gameManagerGO.GetComponent<CombatNameplateHUD>();
            if (oldNP != null) Object.DestroyImmediate(oldNP);
            CombatNameplateHUD np = gameManagerGO.AddComponent<CombatNameplateHUD>();
            var npSO = new SerializedObject(np);
            npSO.FindProperty("_state").objectReferenceValue       = gsm;
            npSO.FindProperty("_unitManager").objectReferenceValue = um;
            npSO.FindProperty("_turnManager").objectReferenceValue = tm;
            npSO.FindProperty("_camera").objectReferenceValue      = Camera.main;
            npSO.ApplyModifiedProperties();

            // ── MapInputHandler'a turnManager bagla ───────────────────────────
            var inputSO = new SerializedObject(input);
            inputSO.FindProperty("_turnManager").objectReferenceValue = tm;
            inputSO.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!_silentSetup) EditorUtility.DisplayDialog("Faz C3 — Tur Sistemi Hazir",
                "Kurulanlar:\n" +
                "  • TurnManager (hiza gore initiative) + CombatHUD + CombatHighlighter\n" +
                "  • CombatNameplateHUD: her birimin ustunde isim + can bari (dost yesil / dusman kirmizi)\n" +
                "  • MapInputHandler savas tiklamasina baglandi\n\n" +
                "Play → marker → Evet → kart yerlestir → SAVASI BASLAT:\n" +
                "  • Sol ustte sira paneli; aktif birimin ustunde sari top.\n" +
                "  • SENIN TURUN: yesil karoya tikla = git, kirmizi dusmana tikla = saldir.\n" +
                "  • 'Turu Bitir' ile siradakine gec; dusmanlar otomatik oynar.\n" +
                "  • Tum Goblin olunce ZAFER, tum birimlerin olunce YENILGI.",
                "Tamam");

            Debug.Log("[TacticalRPG] Faz C3 (tur sistemi) kuruldu.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // FAZ C4 — Kam komutan (zorunlu birim) + savaş büyüsü + lose=Kam ölümü
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("TacticalRPG/Faz C4 - Kam Komutan + Buyu", false, 20)]
        public static void SetupPhaseC4()
        {
            GameObject sceneRoot     = GameObject.Find(SceneRootName);
            GameObject gameManagerGO = sceneRoot != null
                ? sceneRoot.transform.Find("GameManager")?.gameObject
                : null;

            if (gameManagerGO == null)
            {
                EditorUtility.DisplayDialog("Hata",
                    "GameManager bulunamadi! Once TAM KURULUM (veya Faz 0-2 + A + B + C + C3) calistir.", "Tamam");
                return;
            }

            KamManaManager    kamMana   = FindComponentAnywhere<KamManaManager>();
            UnitManager       um        = FindComponentAnywhere<UnitManager>();
            PartyManager      party     = FindComponentAnywhere<PartyManager>();
            MapInputHandler   input     = FindComponentAnywhere<MapInputHandler>();
            TurnManager       tm        = FindComponentAnywhere<TurnManager>();
            DeploymentManager dm        = FindComponentAnywhere<DeploymentManager>();
            CombatHUD         combatHUD = FindComponentAnywhere<CombatHUD>();

            if (kamMana == null || um == null || party == null || input == null || tm == null || dm == null)
            {
                EditorUtility.DisplayDialog("Hata",
                    "Gerekli sistemler eksik (KamMana/UnitManager/Party/Input/TurnManager/Deployment).\n" +
                    "Once Faz 2 + Faz A + Faz B + Faz C + Faz C3'u calistir.", "Tamam");
                return;
            }

            // ── 1) Kam büyüleri (create-or-load) + Kam'a ata ───────────────────
            EnsureFolder("Assets/Data");
            EnsureFolder("Assets/Data/Abilities");
            KamAbilityData ates = GetOrCreateAbilitySO(
                "Assets/Data/Abilities/AtesTopu.asset", "ates_topu", "Ates Topu",
                "Hedef dusmana alev yagdirir.", AbilityEffectType.Damage, manaCost: 3, range: 4, power: 6);
            KamAbilityData sifa = GetOrCreateAbilitySO(
                "Assets/Data/Abilities/Sifa.asset", "sifa", "Sifa",
                "Dost birimi iyilestirir.", AbilityEffectType.Heal, manaCost: 2, range: 3, power: 5);
            KamAbilityData kalkan = GetOrCreateAbilitySO(
                "Assets/Data/Abilities/RuhKalkani.asset", "ruh_kalkani", "Ruh Kalkani",
                "Dost birime kalkan verir.", AbilityEffectType.Buff, manaCost: 4, range: 2, power: 3);

            CharacterClassData kamData = AssetDatabase.LoadAssetAtPath<CharacterClassData>(
                "Assets/Data/Characters/Kam.asset");
            if (kamData != null)
            {
                var kamSO = new SerializedObject(kamData);
                kamSO.FindProperty("_isCommander").boolValue = true; // garanti (Faz 2 tekrar calismadi ise)
                var listProp = kamSO.FindProperty("_abilities");
                listProp.ClearArray();
                listProp.arraySize = 3;
                listProp.GetArrayElementAtIndex(0).objectReferenceValue = ates;
                listProp.GetArrayElementAtIndex(1).objectReferenceValue = sifa;
                listProp.GetArrayElementAtIndex(2).objectReferenceValue = kalkan;
                kamSO.ApplyModifiedProperties();
                EditorUtility.SetDirty(kamData);
            }
            else Debug.LogWarning("[Faz C4] Kam.asset bulunamadi — once Faz 2'yi calistir.");

            // ── 2) AbilityCaster (GameManager üstünde) — origin: komutan birimi ─
            var oldCaster = gameManagerGO.GetComponent<AbilityCaster>();
            if (oldCaster != null) Object.DestroyImmediate(oldCaster);
            AbilityCaster caster = gameManagerGO.AddComponent<AbilityCaster>();
            var casterSO = new SerializedObject(caster);
            casterSO.FindProperty("_turnManager").objectReferenceValue = tm;
            casterSO.FindProperty("_kamMana").objectReferenceValue     = kamMana;
            casterSO.FindProperty("_unitManager").objectReferenceValue = um;
            casterSO.ApplyModifiedProperties();

            // ── 3) MapInputHandler'a caster'i bagla (combat'ta buyu hedefleme) ─
            var inputSO = new SerializedObject(input);
            inputSO.FindProperty("_caster").objectReferenceValue = caster;
            inputSO.ApplyModifiedProperties();

            // ── 4) DeploymentManager'a party'yi bagla (Kam otomatik iner) ──────
            var dmSO = new SerializedObject(dm);
            dmSO.FindProperty("_party").objectReferenceValue = party;
            dmSO.ApplyModifiedProperties();

            // ── 5) CombatHUD'a caster + mana bagla (Kam turunda buyu paneli) ───
            if (combatHUD != null)
            {
                var chSO = new SerializedObject(combatHUD);
                chSO.FindProperty("_caster").objectReferenceValue  = caster;
                chSO.FindProperty("_kamMana").objectReferenceValue = kamMana;
                chSO.ApplyModifiedProperties();
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!_silentSetup) EditorUtility.DisplayDialog("Faz C4 — Kam Komutan + Buyu Hazir",
                "Kurulanlar:\n" +
                "  • Kam artik KOMUTAN: savasa zorunlu + ucretsiz iner (altin renk)\n" +
                "  • Diger kahramanlar farkli renklerde (Savasci mavi, Ranger turkuaz)\n" +
                "  • Kam'a 3 buyu atandi (Ates Topu / Sifa / Ruh Kalkani)\n" +
                "  • AbilityCaster combat'a baglandi (origin: Kam birimi)\n" +
                "  • YENILGI artik = Kam'in olumu (sefer biter)\n\n" +
                "Play → marker → Evet → YERLESTIRME (Kam otomatik iner):\n" +
                "  • Kam'in turunda 1/2/3 ile buyu sec → hedefe tikla (mana harcanir).\n" +
                "  • Hasar dusmana; Sifa/Kalkan dost birime (Kam dahil).\n" +
                "  • Kam olurse YENILGI; tum Goblinler olunce ZAFER.",
                "Tamam");

            Debug.Log("[TacticalRPG] Faz C4 (Kam komutan + buyu) kuruldu.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // FAZ D — Çok-tipli öz + harita toplama + tarifle birim üretme
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("TacticalRPG/Faz D - Oz Toplama + Birim Uretme", false, 21)]
        public static void SetupPhaseD()
        {
            GameObject sceneRoot     = GameObject.Find(SceneRootName);
            GameObject gameManagerGO = sceneRoot != null
                ? sceneRoot.transform.Find("GameManager")?.gameObject
                : null;

            if (gameManagerGO == null)
            {
                EditorUtility.DisplayDialog("Hata",
                    "GameManager bulunamadi! Once TAM KURULUM (veya Faz 0-2 + A) calistir.", "Tamam");
                return;
            }

            HexGridManager     grid   = FindComponentAnywhere<HexGridManager>();
            GameStateManager   gsm    = FindComponentAnywhere<GameStateManager>();
            PlayerController    player = FindComponentAnywhere<PlayerController>();
            ActionPointManager ap     = FindComponentAnywhere<ActionPointManager>();
            EssenceWallet      wallet = FindComponentAnywhere<EssenceWallet>();
            PartyManager       party  = FindComponentAnywhere<PartyManager>();

            if (grid == null || gsm == null || player == null || wallet == null || party == null)
            {
                EditorUtility.DisplayDialog("Hata",
                    "Gerekli sistemler eksik (Grid/GameState/Player/Wallet/Party).\n" +
                    "Once Faz 0-2 + Faz A'yi calistir.", "Tamam");
                return;
            }

            // ── 1) Öz config asset (tip ad/renk + prefab) ─────────────────────
            EnsureFolder("Assets/Data");
            EnsureFolder("Assets/Data/Config");
            EssenceConfigSO config = GetOrCreateEssenceConfig("Assets/Data/Config/EssenceConfig.asset");

            // El yapımı öz haritası (rastgele DEĞİL). VARSA KORUNUR — boyamaların TAM KURULUM'da
            // silinmez; yoksa birkaç örnek yerleşimle oluşturulur (Essence Painter ile düzenle).
            EnsureFolder("Assets/Data/Map");
            EssenceMapSO essenceMap = GetOrCreateEssenceMap("Assets/Data/Map/EssenceMap.asset");

            // ── 2) Üretim tarifleri (Savaşçı/Ranger — 2 öz kombinasyonu) ──────
            EnsureFolder("Assets/Data/Recipes");
            CharacterClassData warriorData = AssetDatabase.LoadAssetAtPath<CharacterClassData>(
                "Assets/Data/Characters/Savascı.asset");
            CharacterClassData rangerData = AssetDatabase.LoadAssetAtPath<CharacterClassData>(
                "Assets/Data/Characters/Ranger.asset");

            UnitRecipe savasciRecipe = MakeRecipe(
                "Assets/Data/Recipes/SavasciRecipe.asset", "Savasci", warriorData,
                new[] { new EssenceAmount(EssenceType.Ates, 2), new EssenceAmount(EssenceType.Toprak, 1) });
            UnitRecipe rangerRecipe = MakeRecipe(
                "Assets/Data/Recipes/RangerRecipe.asset", "Ranger", rangerData,
                new[] { new EssenceAmount(EssenceType.Su, 2), new EssenceAmount(EssenceType.Toprak, 1) });

            // ── 3) EssenceNodeManager (harita öz node'ları + topla) ───────────
            var oldEN = gameManagerGO.GetComponent<EssenceNodeManager>();
            if (oldEN != null) Object.DestroyImmediate(oldEN);
            EssenceNodeManager nodes = gameManagerGO.AddComponent<EssenceNodeManager>();
            var enSO = new SerializedObject(nodes);
            enSO.FindProperty("_grid").objectReferenceValue         = grid;
            enSO.FindProperty("_stateManager").objectReferenceValue = gsm;
            enSO.FindProperty("_player").objectReferenceValue        = player;
            enSO.FindProperty("_ap").objectReferenceValue            = ap;
            enSO.FindProperty("_wallet").objectReferenceValue        = wallet;
            enSO.FindProperty("_config").objectReferenceValue        = config;
            enSO.FindProperty("_map").objectReferenceValue           = essenceMap;
            enSO.FindProperty("_nodeHeight").floatValue              = 0.12f; // yere yakın
            enSO.FindProperty("_nodeScale").floatValue               = 0.16f;
            enSO.FindProperty("_ringRadius").floatValue              = 0.34f;
            enSO.ApplyModifiedProperties();

            // ── 4) OverworldEssenceHUD (sadece cüzdan + topla + roster — ÜRETİM YOK) ─
            var oldOE = gameManagerGO.GetComponent<OverworldEssenceHUD>();
            if (oldOE != null) Object.DestroyImmediate(oldOE);
            OverworldEssenceHUD oeh = gameManagerGO.AddComponent<OverworldEssenceHUD>();
            var oeSO = new SerializedObject(oeh);
            oeSO.FindProperty("_state").objectReferenceValue  = gsm;
            oeSO.FindProperty("_wallet").objectReferenceValue = wallet;
            oeSO.FindProperty("_nodes").objectReferenceValue  = nodes;
            oeSO.FindProperty("_player").objectReferenceValue = player;
            oeSO.FindProperty("_party").objectReferenceValue  = party;
            oeSO.FindProperty("_config").objectReferenceValue = config;
            oeSO.ApplyModifiedProperties();

            // ── 5) DeploymentHUD'a üretim bağla (öz harcayarak birim üretme ARTIK BURADA) ─
            DeploymentHUD deployHud = FindComponentAnywhere<DeploymentHUD>();
            if (deployHud != null)
            {
                var dhSO = new SerializedObject(deployHud);
                dhSO.FindProperty("_wallet").objectReferenceValue = wallet;
                dhSO.FindProperty("_config").objectReferenceValue = config;
                var recipesProp = dhSO.FindProperty("_recipes");
                recipesProp.ClearArray();
                recipesProp.arraySize = 2;
                recipesProp.GetArrayElementAtIndex(0).objectReferenceValue = savasciRecipe;
                recipesProp.GetArrayElementAtIndex(1).objectReferenceValue = rangerRecipe;
                dhSO.ApplyModifiedProperties();
            }
            else
            {
                Debug.LogWarning("[TacticalRPG] Faz D: DeploymentHUD bulunamadi — once Faz B calistir " +
                                 "(uretim tarifleri yerlestirme ekranina baglanamadi).");
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!_silentSetup) EditorUtility.DisplayDialog("Faz D — Oz Toplama + Uretim Hazir",
                "Kurulanlar:\n" +
                "  • Cok-tipli oz (Ates/Su/Toprak) — cuzdan + ÖZ DEPOSU gosterimi\n" +
                "  • EL YAPIMI oz haritasi (EssenceMap) — RASTGELE DEGIL\n" +
                "  • Ozler artik karo yuzeyine YAKIN; her tur tek kure (ust uste binmez)\n" +
                "  • Birim URETME artik YERLESTIRME ekraninda (overworld'de DEGIL)\n" +
                "  • 'Savasa Gir' istemi yalniz goreve ~1 hex yaklasinca cikar\n\n" +
                "Oz yerlestirmek icin:\n" +
                "  • TacticalRPG → Essence Painter - Oz Boyama → tur sec + miktar gir → karoya tikla.\n" +
                "  • Kendi (animasyonlu) oz prefab'ini EssenceConfig'de ilgili ture ata.\n\n" +
                "Play (Overworld):\n" +
                "  • Ozlu karoya git → sag panelden 'Topla (1 AP)'.\n" +
                "  • Savas karosuna yaklas → 'Savasa Gir' → yerlestirmede 'Uret' + BEDAVA yerlestir.",
                "Tamam");

            Debug.Log("[TacticalRPG] Faz D (el yapimi oz haritasi + uretim yerlestirmede) kuruldu.");
        }

        // El yapımı öz haritası asset'i: VARSA korunur (boyamalar silinmez); yoksa örneklerle oluşturulur.
        private static EssenceMapSO GetOrCreateEssenceMap(string path)
        {
            EssenceMapSO map = AssetDatabase.LoadAssetAtPath<EssenceMapSO>(path);
            if (map != null) return map; // mevcut boyamayı koru

            map = ScriptableObject.CreateInstance<EssenceMapSO>();
            AssetDatabase.CreateAsset(map, path);

            // Sadece İLK oluşturmada birkaç örnek (oyuncu başlangıcı Q3R4 yakını) — sonra painter ile düzenle.
            map.SetAmount(new HexCoordinate(2, 4), EssenceType.Toprak, 3);
            map.SetAmount(new HexCoordinate(4, 4), EssenceType.Su,     2);
            map.SetAmount(new HexCoordinate(4, 5), EssenceType.Ates,   2);
            map.SetAmount(new HexCoordinate(4, 5), EssenceType.Toprak, 1);
            EditorUtility.SetDirty(map);
            return map;
        }

        // EssenceConfig asset'i oluşturur/günceller (3 tip: ad+renk; prefab boş = placeholder).
        private static EssenceConfigSO GetOrCreateEssenceConfig(string path)
        {
            EssenceConfigSO cfg = AssetDatabase.LoadAssetAtPath<EssenceConfigSO>(path);
            if (cfg != null) return cfg; // mevcut config'i KORU — kullanicinin prefab/renk atamalari silinmesin

            cfg = ScriptableObject.CreateInstance<EssenceConfigSO>();
            AssetDatabase.CreateAsset(cfg, path);

            var so    = new SerializedObject(cfg);
            var types = so.FindProperty("_types");
            types.ClearArray();
            types.arraySize = 3;
            SetEssenceType(types.GetArrayElementAtIndex(0), EssenceType.Ates,   "Ates",   new Color(0.90f, 0.25f, 0.20f));
            SetEssenceType(types.GetArrayElementAtIndex(1), EssenceType.Su,     "Su",     new Color(0.25f, 0.50f, 0.95f));
            SetEssenceType(types.GetArrayElementAtIndex(2), EssenceType.Toprak, "Toprak", new Color(0.35f, 0.75f, 0.35f));
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(cfg);
            return cfg;
        }

        private static void SetEssenceType(SerializedProperty e, EssenceType t, string name, Color c)
        {
            e.FindPropertyRelative("type").enumValueIndex     = (int)t;
            e.FindPropertyRelative("displayName").stringValue = name;
            e.FindPropertyRelative("color").colorValue        = c;
        }

        private static UnitRecipe MakeRecipe(string path, string name, CharacterClassData unit, EssenceAmount[] cost)
        {
            UnitRecipe r = AssetDatabase.LoadAssetAtPath<UnitRecipe>(path);
            if (r == null)
            {
                r = ScriptableObject.CreateInstance<UnitRecipe>();
                AssetDatabase.CreateAsset(r, path);
            }
            var so = new SerializedObject(r);
            so.FindProperty("_displayName").stringValue        = name;
            so.FindProperty("_unitClass").objectReferenceValue = unit;
            var costProp = so.FindProperty("_cost");
            costProp.ClearArray();
            costProp.arraySize = cost.Length;
            for (int i = 0; i < cost.Length; i++)
            {
                var e = costProp.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("type").enumValueIndex = (int)cost[i].type;
                e.FindPropertyRelative("amount").intValue     = cost[i].amount;
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(r);
            return r;
        }

        // ─────────────────────────────────────────────────────────────────────
        // TANI — Sahne durumunu logla
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("TacticalRPG/Tani - Sahne Kontrolu", false, 51)]
        public static void DiagnoseScene()
        {
            Debug.Log("===== TacticalRPG TANI =====");

            GameObject sceneRoot   = GameObject.Find(SceneRootName);
            GameObject systemsRoot = GameObject.Find(SystemsRootName);
            Debug.Log($"[TANI] SceneRoot={sceneRoot != null}  SystemsRoot={systemsRoot != null}");

            Camera cam = Camera.main;
            if (cam != null)
            {
                var urp = cam.gameObject.GetComponent<UniversalAdditionalCameraData>();
                Debug.Log($"[TANI] Kamera: ortho={cam.orthographic} size={cam.orthographicSize} URP={urp != null}");
            }
            else Debug.LogError("[TANI] Camera.main YOK!");

            if (systemsRoot == null) { Debug.LogError("[TANI] SystemsRoot yok — Faz 1 calistirilmamis!"); return; }

            HexGridManager grid = systemsRoot.GetComponentInChildren<HexGridManager>();
            if (grid != null)
            {
                int cellCount = grid.Cells?.Count ?? -1;
                Debug.Log($"[TANI] HexGridManager: {cellCount} hucre (beklenen: 100)");

                if (grid.Cells != null)
                {
                    int nullMR = 0, hidden = 0, explored = 0, visible = 0;
                    foreach (var c in grid.Cells.Values)
                    {
                        if (c.MeshRenderer == null) nullMR++;
                        switch (c.FogState)
                        {
                            case FogState.Hidden:   hidden++;   break;
                            case FogState.Explored: explored++; break;
                            case FogState.Visible:  visible++;  break;
                        }
                    }
                    Debug.Log($"[TANI] MeshRenderer null:{nullMR} | Fog H:{hidden} E:{explored} V:{visible}");
                    if (nullMR > 0) Debug.LogError($"[TANI] {nullMR} karonun MeshRenderer'i NULL!");
                    if (visible == 0) Debug.LogWarning("[TANI] Visible karo yok — RevealArea calismiyor olabilir.");
                }
            }
            else Debug.LogError("[TANI] HexGridManager YOK!");

            PlayerController player = systemsRoot.GetComponentInChildren<PlayerController>();
            Debug.Log($"[TANI] PlayerController={player != null}{(player != null ? $" konum:{player.CurrentCoord}" : "")}");

            ActionPointManager ap = FindComponentAnywhere<ActionPointManager>();
            EssenceWallet  ess    = FindComponentAnywhere<EssenceWallet>();
            KamManaManager mana   = FindComponentAnywhere<KamManaManager>();
            Debug.Log($"[TANI] AP={ap != null}  Essence={ess != null}  KamMana={mana != null}");

            Debug.Log("===== TANI BITTI =====");
            EditorUtility.DisplayDialog("Tani tamamlandi", "Console sekmesine bak.\nKirmizi = kritik sorun | Sari = uyari", "Tamam");
        }

        // ─────────────────────────────────────────────────────────────────────
        // TEMIZLE
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("TacticalRPG/0 - Sahneyi Temizle", false, 52)]
        public static void CleanupAll()
        {
            bool found = false;
            found |= DestroyRoot(SceneRootName);
            found |= DestroyRoot(SystemsRootName);
            found |= DestroyRoot("DebugHUD_Canvas");

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            EditorUtility.DisplayDialog(
                found ? "Temizlik Tamam" : "Temizlenecek Sey Yok",
                found ? "Tum TacticalRPG objeleri sahneden silindi.\nArtik TAM KURULUM ile yeniden baslatabilirsin."
                      : "Sahnede TacticalRPG objesi bulunamadi.",
                "Tamam");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Yardımcı metodlar
        // ─────────────────────────────────────────────────────────────────────

        private static bool DestroyRoot(string name)
        {
            GameObject go = GameObject.Find(name);
            if (go == null) return false;
            Object.DestroyImmediate(go);
            return true;
        }

        private static Material GetOrCreateMaterial(string assetName, Color color)
        {
            string path = $"{MaterialsPath}/{assetName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                // URP Lit → direktif ışıktan etkilenir, izometrik 3D'de derinlik görünür
                Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                             ?? Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            if (mat.HasProperty("_BaseColor"))  mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color",     color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
            if (mat.HasProperty("_Metallic"))   mat.SetFloat("_Metallic",   0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ── Karakter modeli bake (placeholder kapsül → gerçek model, KALICI) ──────
        // FBX YATIK geldiği için dik döndürülür. Hâlâ yanlışsa bu Euler'ı ayarla:
        // (90,0,0) ters yön, (0,0,90) yan, (0,180,0) arka dönük. Boy karoya sığacak auto-scale.
        private static readonly Vector3 CharacterModelEuler  = new Vector3(90f, 0f, 0f);
        private const           float   CharacterModelHeight = 1.5f;

        /// <summary>
        /// Bir karakter FBX'ini parent GO'ya KALICI bake eder: kapsül görselini kaldırır, modeli child
        /// ekler, dik döndürür, hedef boya auto-scale eder, ayağı parent orijinine (zemine) oturtur.
        /// Edit-time çalışır → sahneye saklanır, editörde de görünür (Play gerekmez).
        /// </summary>
        private static void BakeCharacterModel(GameObject parent, GameObject fbx)
        {
            var mr = parent.GetComponent<MeshRenderer>(); if (mr != null) Object.DestroyImmediate(mr);
            var mf = parent.GetComponent<MeshFilter>();   if (mf != null) Object.DestroyImmediate(mf);
            parent.transform.localScale = Vector3.one; // kapsül 0.45 ölçeği gerekmez

            var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, parent.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(CharacterModelEuler);
            model.transform.localScale    = Vector3.one;

            var rends = model.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return;

            Bounds b = rends[0].bounds;                                   // auto-scale: dünya boyu → hedef
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            if (b.size.y > 0.0001f)
                model.transform.localScale = Vector3.one * (CharacterModelHeight / b.size.y);

            b = rends[0].bounds;                                         // ayağı zemine otur
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            model.transform.position += Vector3.up * (parent.transform.position.y - b.min.y);
        }

        // ── Gerçekçi grafik preset'i (Global Volume + post-process) ──────────────
        // ACES tonemapping + bloom + vignette + renk. Kamera/ışık gölgeleri Faz 0'da açıldı.
        // Idempotent: profil asset'i (Assets/Data/PostFX_Profile.asset) yeniden kullanılır.
        private static void SetupRealisticGraphics(Transform parent)
        {
            const string profilePath = "Assets/Data/PostFX_Profile.asset";
            EnsureFolder("Assets/Data");
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }

            var tone = GetOrAddVolumeOverride<Tonemapping>(profile);
            tone.mode.overrideState = true; tone.mode.value = TonemappingMode.ACES;

            var bloom = GetOrAddVolumeOverride<Bloom>(profile);
            bloom.intensity.overrideState = true; bloom.intensity.value = 0.6f;
            bloom.threshold.overrideState = true; bloom.threshold.value = 1.05f;

            var vig = GetOrAddVolumeOverride<Vignette>(profile);
            vig.intensity.overrideState = true; vig.intensity.value = 0.28f;

            var col = GetOrAddVolumeOverride<ColorAdjustments>(profile);
            col.contrast.overrideState   = true; col.contrast.value   = 12f;
            col.saturation.overrideState = true; col.saturation.value = 6f;

            EditorUtility.SetDirty(profile);

            var volGO = new GameObject("Global Volume");
            volGO.transform.SetParent(parent, false);
            var vol = volGO.AddComponent<Volume>();
            vol.isGlobal      = true;
            vol.priority      = 1f;
            vol.sharedProfile = profile;
        }

        private static T GetOrAddVolumeOverride<T>(VolumeProfile profile) where T : VolumeComponent
            => profile.TryGet<T>(out T comp) ? comp : profile.Add<T>(true);

        // ── Bölüm 1 = KÜP (6 yüz) — CubeFaceManager + 6 yüz asset ────────────
        // Yüz 1 (Ön) = TileMap.asset (mevcut harita); 2-6 = Face_N.asset (boş, kullanıcı tasarlar).
        // Manager grid'i seçili yüzle yeniden üretir; alt çubuktan manuel geçiş. (Otomatik kenar
        // çerçeve + geçiş + küp dönüşü sonraki adımda eklenecek.)
        private static void SetupCubeFaces()
        {
            var grid   = FindComponentAnywhere<HexGridManager>();
            var player = FindComponentAnywhere<PlayerController>();
            var state  = FindComponentAnywhere<GameStateManager>();
            if (grid == null) { Debug.LogError("[Kup] HexGridManager yok — Faz 1 calistir."); return; }

            GameObject host = state != null ? state.gameObject : GameObject.Find("GameManager");
            if (host == null) { Debug.LogError("[Kup] Host GameObject bulunamadi."); return; }

            var mgr = host.GetComponent<CubeFaceManager>();
            if (mgr == null) mgr = host.AddComponent<CubeFaceManager>();

            var so = new SerializedObject(mgr);
            so.FindProperty("_grid").objectReferenceValue   = grid;
            so.FindProperty("_player").objectReferenceValue = player;
            so.FindProperty("_state").objectReferenceValue  = state;
            var faces = so.FindProperty("_faces");
            faces.arraySize = 6;
            for (int n = 1; n <= 6; n++)
                faces.GetArrayElementAtIndex(n - 1).objectReferenceValue = LoadOrCreateFaceAsset(n);
            so.ApplyModifiedProperties();

            // CubeRig — kup illuzyonu (aktif yuzun 4 komsusunu kenarlardan katlanmis panel render eder)
            var rig = host.GetComponent<CubeRig>();
            if (rig == null) rig = host.AddComponent<CubeRig>();
            var rigSO = new SerializedObject(rig);
            rigSO.FindProperty("_grid").objectReferenceValue   = grid;
            rigSO.FindProperty("_faces").objectReferenceValue  = mgr;
            rigSO.FindProperty("_player").objectReferenceValue = player;
            rigSO.FindProperty("_placeholderTile").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Grid/HexCell.prefab");
            rigSO.ApplyModifiedProperties();

            // MapInputHandler'a CubeRig'i bagla → yan yuze tiklama = Kam kenara yuruyup karsi yuze gecsin
            var mih = FindComponentAnywhere<MapInputHandler>();
            if (mih != null)
            {
                var mihSO = new SerializedObject(mih);
                mihSO.FindProperty("_cubeRig").objectReferenceValue = rig;
                mihSO.ApplyModifiedProperties();
            }

            Debug.Log("[Kup] CubeFaceManager + CubeRig + tikla-yuru-gec kuruldu (6 yuz, karinca-kup).");
        }

        private static TileMapSO LoadOrCreateFaceAsset(int n)
        {
            EnsureFolder("Assets/Data/Map");
            string path = n == 1 ? "Assets/Data/Map/TileMap.asset" : $"Assets/Data/Map/Face_{n}.asset";
            var map = AssetDatabase.LoadAssetAtPath<TileMapSO>(path);
            if (map == null)
            {
                map = ScriptableObject.CreateInstance<TileMapSO>();
                AssetDatabase.CreateAsset(map, path);
                AssetDatabase.SaveAssets();
            }
            return map;
        }

        private static Mesh GetOrCreateHexMesh()
        {
            string meshPath = "Assets/Art/Meshes/HexMesh.asset";
            EnsureFolder("Assets/Art/Meshes");
            AssetDatabase.DeleteAsset(meshPath);
            Mesh mesh = HexMetrics.CreateHexMesh(0.95f);
            AssetDatabase.CreateAsset(mesh, meshPath);
            return mesh;
        }

        private static GameObject GetOrCreateHexCellPrefab(Material defaultMat)
        {
            string path = $"{PrefabsGridPath}/HexCell.prefab";
            Mesh hexMesh = GetOrCreateHexMesh();

            GameObject go = new GameObject("HexCell");
            go.AddComponent<MeshFilter>().sharedMesh = hexMesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = defaultMat;
            go.AddComponent<MeshCollider>().sharedMesh = hexMesh;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static CharacterClassData GetOrCreateCharacterSO(
            string path, string className, string lore,
            int maxHP, int attack, int defense, int moveRange,
            int[] essenceCosts, float[] hpMult, float[] atkMult, float[] defMult,
            bool hasMana, int maxMana, int speed = 5, int attackRange = 1,
            bool isCommander = false, Color? unitColor = null)
        {
            CharacterClassData so = AssetDatabase.LoadAssetAtPath<CharacterClassData>(path);
            if (so == null)
            {
                so = ScriptableObject.CreateInstance<CharacterClassData>();
                AssetDatabase.CreateAsset(so, path);
            }
            var s = new SerializedObject(so);
            s.FindProperty("_className").stringValue  = className;
            s.FindProperty("_lore").stringValue        = lore;
            s.FindProperty("_maxHP").intValue          = maxHP;
            s.FindProperty("_attack").intValue         = attack;
            s.FindProperty("_defense").intValue        = defense;
            s.FindProperty("_moveRange").intValue      = moveRange;
            s.FindProperty("_speed").intValue          = speed;
            s.FindProperty("_attackRange").intValue    = attackRange;
            s.FindProperty("_hasManaSystem").boolValue = hasMana;
            s.FindProperty("_maxMana").intValue        = maxMana;
            s.FindProperty("_isCommander").boolValue   = isCommander;
            if (unitColor.HasValue) s.FindProperty("_unitColor").colorValue = unitColor.Value;
            SetIntArray(s,   "_essenceCostPerLevel",   essenceCosts);
            SetFloatArray(s, "_hpMultiplierPerLevel",  hpMult);
            SetFloatArray(s, "_atkMultiplierPerLevel", atkMult);
            SetFloatArray(s, "_defMultiplierPerLevel", defMult);
            s.ApplyModifiedProperties();
            EditorUtility.SetDirty(so);
            return so;
        }

        private static KamAbilityData GetOrCreateAbilitySO(
            string path, string id, string displayName, string description,
            AbilityEffectType effect, int manaCost, int range, int power)
        {
            KamAbilityData so = AssetDatabase.LoadAssetAtPath<KamAbilityData>(path);
            if (so == null)
            {
                so = ScriptableObject.CreateInstance<KamAbilityData>();
                AssetDatabase.CreateAsset(so, path);
            }
            var s = new SerializedObject(so);
            s.FindProperty("_id").stringValue          = id;
            s.FindProperty("_displayName").stringValue = displayName;
            s.FindProperty("_description").stringValue = description;
            s.FindProperty("_manaCost").intValue       = manaCost;
            s.FindProperty("_range").intValue          = range;
            s.FindProperty("_effect").enumValueIndex   = (int)effect;
            s.FindProperty("_power").intValue          = power;
            s.ApplyModifiedProperties();
            EditorUtility.SetDirty(so);
            return so;
        }

        private static void SetIntArray(SerializedObject so, string prop, int[] vals)
        {
            var p = so.FindProperty(prop);
            p.ClearArray();
            p.arraySize = vals.Length;
            for (int i = 0; i < vals.Length; i++) p.GetArrayElementAtIndex(i).intValue = vals[i];
        }

        private static void SetFloatArray(SerializedObject so, string prop, float[] vals)
        {
            var p = so.FindProperty(prop);
            p.ClearArray();
            p.arraySize = vals.Length;
            for (int i = 0; i < vals.Length; i++) p.GetArrayElementAtIndex(i).floatValue = vals[i];
        }

        private static TextMeshProUGUI CreateTMPLabel(
            Transform parent, string name, string text,
            Vector2 anchoredPos, Vector2 sizeDelta, Color color, float fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0f, 1f);
            rt.anchorMax        = new Vector2(0f, 1f);
            rt.pivot            = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta        = sizeDelta;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text               = text;
            tmp.fontSize           = fontSize;
            tmp.color              = color;
            tmp.fontStyle          = FontStyles.Bold;
            tmp.enableWordWrapping = false;
            tmp.overflowMode       = TextOverflowModes.Overflow;
            return tmp;
        }

        private static T FindComponentAnywhere<T>() where T : UnityEngine.Component
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<T>();
#else
            return Object.FindObjectOfType<T>();
#endif
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folderName = System.IO.Path.GetFileName(path);
            if (parent != null) AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
