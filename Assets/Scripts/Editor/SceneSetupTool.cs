using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
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

        // ─────────────────────────────────────────────────────────────────────
        // TAM KURULUM — tek tıkla tüm fazları sırasıyla çalıştırır
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("TacticalRPG/TAM KURULUM (Tek Tikla)", false, 1)]
        public static void FullSetup()
        {
            SetupPhase0();
            SetupPhase1();
            SetupPhase2();
            SetupDebugHUD();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "TAM KURULUM Tamamlandi!",
                "Tum fazlar basariyla kuruldu:\n\n" +
                "  • Faz 0 — Kamera, Isik\n" +
                "  • Faz 1 — Hex Grid, Oyuncu, AP, Kiyamet\n" +
                "  • Faz 2 — Karakter Sistemi (Kam, Savasci, Ranger)\n" +
                "  • Debug HUD\n\n" +
                "Ctrl+S ile kaydet, sonra Play'e bas!",
                "Tamam");
        }

        // ─────────────────────────────────────────────────────────────────────
        // FAZ 0 — Kamera, Işık, GameManager
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("TacticalRPG/Faz 0 - Kamera ve Sahne", false, 11)]
        public static void SetupPhase0()
        {
            DestroyRoot(SceneRootName);

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
            urpData.renderShadows        = false;
            urpData.requiresColorTexture = false;
            urpData.requiresDepthTexture = false;

            cameraGO.AddComponent<AudioListener>();

            // Işık
            GameObject lightGO = new GameObject("Directional Light");
            lightGO.transform.SetParent(root.transform);
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Light light = lightGO.AddComponent<Light>();
            light.type      = LightType.Directional;
            light.intensity = 2f;
            light.color     = Color.white;

            // GameManager
            new GameObject("GameManager").transform.SetParent(root.transform);

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

            // Turuncu placeholder materyal — kendi karakterinle değiştirilecek
            EnsureFolder("Assets/Art/Materials");
            Material playerMat = GetOrCreateMaterial("PlayerPlaceholder", new Color(0.95f, 0.45f, 0.1f));
            playerGO.GetComponent<MeshRenderer>().sharedMaterial = playerMat;

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
                hasMana: true, maxMana: 10);

            CharacterClassData warriorData = GetOrCreateCharacterSO(
                path: "Assets/Data/Characters/Savascı.asset", className: "Savasci",
                lore: "Kilic ustasi. On saflarda durur, darbeleri gogusler.",
                maxHP: 14, attack: 5, defense: 3, moveRange: 3,
                essenceCosts: new[] { 0, 6, 14 },
                hpMult:  new[] { 1f, 1.35f, 1.75f },
                atkMult: new[] { 1f, 1.2f,  1.5f  },
                defMult: new[] { 1f, 1.25f, 1.6f  },
                hasMana: false, maxMana: 0);

            CharacterClassData rangerData = GetOrCreateCharacterSO(
                path: "Assets/Data/Characters/Ranger.asset", className: "Ranger",
                lore: "Uzak mesafe uzmani. Gorunmez olur, ince stratejiler kurar.",
                maxHP: 10, attack: 6, defense: 1, moveRange: 4,
                essenceCosts: new[] { 0, 5, 11 },
                hpMult:  new[] { 1f, 1.2f,  1.55f },
                atkMult: new[] { 1f, 1.25f, 1.6f  },
                defMult: new[] { 1f, 1.1f,  1.25f },
                hasMana: false, maxMana: 0);

            GameObject gameManagerGO = sceneRoot != null
                ? sceneRoot.transform.Find("GameManager")?.gameObject
                : systemsRoot.transform.Find("GameManager")?.gameObject;

            if (gameManagerGO == null)
            {
                gameManagerGO = new GameObject("GameManager");
                gameManagerGO.transform.SetParent(systemsRoot.transform);
            }

            // EssenceManager
            var oldEM = gameManagerGO.GetComponent<EssenceManager>();
            if (oldEM != null) Object.DestroyImmediate(oldEM);
            EssenceManager essManager = gameManagerGO.AddComponent<EssenceManager>();
            var emSO = new SerializedObject(essManager);
            emSO.FindProperty("_startingEssence").intValue = 0;
            emSO.FindProperty("_maxEssence").intValue      = 99;
            emSO.ApplyModifiedProperties();

            // PartyManager
            var oldPM = gameManagerGO.GetComponent<PartyManager>();
            if (oldPM != null) Object.DestroyImmediate(oldPM);
            PartyManager partyManager = gameManagerGO.AddComponent<PartyManager>();
            var pmSO = new SerializedObject(partyManager);
            pmSO.FindProperty("_essenceManager").objectReferenceValue = essManager;
            var classList = pmSO.FindProperty("_startingClasses");
            classList.ClearArray();
            classList.arraySize = 3;
            classList.GetArrayElementAtIndex(0).objectReferenceValue = kamData;
            classList.GetArrayElementAtIndex(1).objectReferenceValue = warriorData;
            classList.GetArrayElementAtIndex(2).objectReferenceValue = rangerData;
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
            EssenceManager     essManager      = FindComponentAnywhere<EssenceManager>();
            KamManaManager     kamMana         = FindComponentAnywhere<KamManaManager>();

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
            hudSO.FindProperty("_apManager").objectReferenceValue       = apManager;
            hudSO.FindProperty("_collapseManager").objectReferenceValue = collapseManager;
            hudSO.FindProperty("_essenceManager").objectReferenceValue  = essManager;
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
            var oldCaster = gameManagerGO.GetComponent<AbilityCaster>();
            if (oldCaster != null) Object.DestroyImmediate(oldCaster);
            AbilityCaster caster = gameManagerGO.AddComponent<AbilityCaster>();
            var casterSO = new SerializedObject(caster);
            casterSO.FindProperty("_player").objectReferenceValue       = player;
            casterSO.FindProperty("_kamMana").objectReferenceValue      = kamMana;
            casterSO.FindProperty("_partyManager").objectReferenceValue = party;
            casterSO.FindProperty("_unitManager").objectReferenceValue  = unitManager;
            casterSO.FindProperty("_casterClassName").stringValue       = "Kam";
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
            ochSO.FindProperty("_stateManager").objectReferenceValue = gsm;
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

            EditorUtility.DisplayDialog("Faz A — Overworld/Savas Gecisi Hazir",
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
            EssenceManager ess    = FindComponentAnywhere<EssenceManager>();
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
            bool hasMana, int maxMana)
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
            s.FindProperty("_hasManaSystem").boolValue = hasMana;
            s.FindProperty("_maxMana").intValue        = maxMana;
            SetIntArray(s,   "_essenceCostPerLevel",   essenceCosts);
            SetFloatArray(s, "_hpMultiplierPerLevel",  hpMult);
            SetFloatArray(s, "_atkMultiplierPerLevel", atkMult);
            SetFloatArray(s, "_defMultiplierPerLevel", defMult);
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
