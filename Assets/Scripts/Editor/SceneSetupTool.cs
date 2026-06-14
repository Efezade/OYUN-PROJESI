using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TacticalRPG.Grid;
using TacticalRPG.Core;

namespace TacticalRPG.Editor
{
    /// <summary>
    /// TacticalRPG sahne kurulum araçları.
    ///
    /// Önerilen çalıştırma sırası:
    ///   1. "0 — Sahneyi Tamamen Temizle"
    ///   2. "Faz 0 — Temel Sahne Ogeleri"
    ///   3. "Faz 1.1 — HexGrid ve FogOfWar"
    /// </summary>
    public static class SceneSetupTool
    {
        // Her fazın sahiplendiği kök obje adı
        private const string SceneRootName   = "[TacticalRPG_Scene]";
        private const string SystemsRootName = "[TacticalRPG_Systems]";

        private const string MaterialsPath   = "Assets/Art/Materials";
        private const string PrefabsGridPath = "Assets/Prefabs/Grid";

        // ─────────────────────────────────────────────────────────────────────
        // MENÜ: 0 — Tüm sahneyi temizle
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("TacticalRPG/0 — Sahneyi Tamamen Temizle")]
        public static void CleanupAll()
        {
            bool foundAny = false;
            foundAny |= DestroyRoot(SceneRootName);
            foundAny |= DestroyRoot(SystemsRootName);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            if (foundAny)
                EditorUtility.DisplayDialog("Temizlik Tamam",
                    "Tum TacticalRPG objeleri sahneden silindi.\n\nArtik Faz 0'dan baslayabilirsin.",
                    "Tamam");
            else
                EditorUtility.DisplayDialog("Temizlenecek Sey Yok",
                    "Sahnede TacticalRPG objesi bulunamadi.",
                    "Tamam");
        }

        // ─────────────────────────────────────────────────────────────────────
        // MENÜ: Faz 0 — Temel sahne ögeleri
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("TacticalRPG/Faz 0 — Temel Sahne Ogeleri (Kamera, Isik, GameManager)")]
        public static void SetupPhase0()
        {
            // Önceki Faz 0 kurulumunu temizle (Faz 1 dokunulmaz)
            DestroyRoot(SceneRootName);

            GameObject root = new GameObject(SceneRootName);

            // ── Ana Kamera (top-down, ortografik) ────────────────────────────
            // Grid: 10x10, hexSize=1 → yaklaşık x:[0,16.5] z:[0,13.5]
            // Merkez: (7.8f, 0, 6.75f) → kamera tam ortaya bakar
            GameObject cameraGO = new GameObject("Main Camera");
            cameraGO.tag = "MainCamera";
            cameraGO.transform.SetParent(root.transform);
            cameraGO.transform.position = new Vector3(7.8f, 50f, 6.75f);
            cameraGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            Camera cam = cameraGO.AddComponent<Camera>();
            cam.orthographic     = true;
            cam.orthographicSize = 10f;      // 10x10 grid tamamen sığar
            cam.nearClipPlane    = 0.1f;
            cam.farClipPlane     = 150f;
            cam.clearFlags       = CameraClearFlags.SolidColor;
            cam.backgroundColor  = new Color(0.08f, 0.08f, 0.08f);

            cameraGO.AddComponent<AudioListener>();

            // ── Directional Light ─────────────────────────────────────────────
            GameObject lightGO = new GameObject("Directional Light");
            lightGO.transform.SetParent(root.transform);
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            Light light = lightGO.AddComponent<Light>();
            light.type      = LightType.Directional;
            light.intensity = 2f;
            light.color     = Color.white;

            // ── GameManager ───────────────────────────────────────────────────
            GameObject gmGO = new GameObject("GameManager");
            gmGO.transform.SetParent(root.transform);
            gmGO.transform.position = Vector3.zero;

            // ── Kaydet ────────────────────────────────────────────────────────
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log($"[TacticalRPG] Faz 0 tamamlandi. '{SceneRootName}' olusturuldu.");
            EditorUtility.DisplayDialog(
                "Faz 0 Tamamlandi!",
                $"'{SceneRootName}' altinda olusturuldu:\n\n" +
                "  • Main Camera  — ortografik, Y=100, X rot=90°\n" +
                "  • Directional Light  — intensity=2\n" +
                "  • GameManager  — bos, hazir\n\n" +
                "Simdi Faz 1.1'i calistirabilirsin.",
                "Tamam");
        }

        // ─────────────────────────────────────────────────────────────────────
        // MENÜ: Faz 1.1 — HexGrid + FogOfWar
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("TacticalRPG/Faz 1.1 - Hex Haritayi Kur")]
        public static void SetupPhase1()
        {
            // Yalnızca sistem kökünü temizle; Faz 0 kamerasına dokunma
            DestroyRoot(SystemsRootName);

            // Klasörler
            EnsureFolder("Assets/Art");
            EnsureFolder(MaterialsPath);
            EnsureFolder("Assets/Prefabs");
            EnsureFolder(PrefabsGridPath);

            // Asset'ler
            Material hiddenMat   = GetOrCreateUnlitMaterial("FogHidden",   Color.black);
            Material exploredMat = GetOrCreateUnlitMaterial("FogExplored", new Color(0.25f, 0.25f, 0.25f));
            Material visibleMat  = GetOrCreateUnlitMaterial("FogVisible",  Color.white);
            GameObject hexCellPrefab = GetOrCreateHexCellPrefab(visibleMat);

            // Kök
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
            gridSO.ApplyModifiedProperties();

            // Edit modunda hücreleri hemen üret — Play beklemeye gerek yok
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

            // Kaydet
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[TacticalRPG] Faz 1.1 tamamlandi. '{SystemsRootName}' hazir.");
            EditorUtility.DisplayDialog(
                "Faz 1.1 Tamamlandi!",
                $"'{SystemsRootName}' altinda olusturuldu:\n\n" +
                "  • HexGridManager  — 10x10, hexSize=1\n" +
                "  • FogOfWarManager  — Hidden / Explored / Visible\n" +
                "  • Prefabs/Grid/HexCell.prefab\n" +
                "  • Art/Materials/ (3 materyal)\n\n" +
                "Play'e bas — tum karolar siyah baslar.",
                "Tamam");
        }

        // ─────────────────────────────────────────────────────────────────────
        // MENÜ: Faz 1.3 — Pathfinding + Oyuncu + Kuleler
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("TacticalRPG/Faz 1.3 - Pathfinding ve Oyuncu")]
        public static void SetupPhase13()
        {
            // Gerekli sistemleri sahnede bul
            GameObject systemsRoot = GameObject.Find(SystemsRootName);
            if (systemsRoot == null)
            {
                EditorUtility.DisplayDialog("Hata", "Once 'Faz 1.1 - Hex Haritayi Kur' calistirin!", "Tamam");
                return;
            }

            GameObject sceneRoot = GameObject.Find(SceneRootName);
            HexGridManager  gridManager = systemsRoot.GetComponentInChildren<HexGridManager>();
            FogOfWarManager fogManager  = systemsRoot.GetComponentInChildren<FogOfWarManager>();

            if (gridManager == null || fogManager == null)
            {
                EditorUtility.DisplayDialog("Hata", "HexGridManager veya FogOfWarManager bulunamadi!", "Tamam");
                return;
            }

            // ── 1. Prefab'a MeshCollider ekle, grid'i yeniden üret ────────────
            GetOrCreateHexCellPrefab(AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsPath}/FogVisible.mat"));
            var gridSO = new SerializedObject(gridManager);

            // Kule (Watchtower) konumları — 10x10 grid içinde 3 nokta
            var watchtowerProp = gridSO.FindProperty("_watchtowerPositions");
            watchtowerProp.ClearArray();
            var wtCoords = new[] { (3, 5), (1, 2), (4, 8) };
            for (int i = 0; i < wtCoords.Length; i++)
            {
                watchtowerProp.arraySize++;
                var elem = watchtowerProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("Q").intValue = wtCoords[i].Item1;
                elem.FindPropertyRelative("R").intValue = wtCoords[i].Item2;
            }
            gridSO.ApplyModifiedProperties();
            gridManager.GenerateGrid(); // MeshCollider'lı yeni prefab ile yeniden üret

            // ── 2. Oyuncu (Player) — basit küp, [TacticalRPG_Systems] altında ─
            Transform existingPlayer = systemsRoot.transform.Find("Player");
            if (existingPlayer != null) Object.DestroyImmediate(existingPlayer.gameObject);

            GameObject playerGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            playerGO.name = "Player";
            playerGO.transform.SetParent(systemsRoot.transform);
            playerGO.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            Object.DestroyImmediate(playerGO.GetComponent<BoxCollider>()); // grid collider ile çakışmasın

            PlayerController playerCtrl = playerGO.AddComponent<PlayerController>();
            var playerSO = new SerializedObject(playerCtrl);
            playerSO.FindProperty("_gridManager").objectReferenceValue  = gridManager;
            playerSO.FindProperty("_fogManager").objectReferenceValue   = fogManager;
            playerSO.FindProperty("_moveSpeed").floatValue              = 8f;
            playerSO.FindProperty("_heightOffset").floatValue           = 0.15f;
            playerSO.FindProperty("_visionRange").intValue              = 3;
            playerSO.FindProperty("_watchtowerRevealRange").intValue    = 5;
            // Başlangıç: grid sol alt köşesi (0, 0) axial
            playerSO.FindProperty("_startCoord").FindPropertyRelative("Q").intValue = 0;
            playerSO.FindProperty("_startCoord").FindPropertyRelative("R").intValue = 0;
            playerSO.ApplyModifiedProperties();

            // ── 3. MapInputHandler — GameManager objesine ekle ────────────────
            GameObject gameManagerGO = sceneRoot != null
                ? sceneRoot.transform.Find("GameManager")?.gameObject
                : null;

            if (gameManagerGO == null)
            {
                gameManagerGO = new GameObject("GameManager");
                gameManagerGO.transform.SetParent(systemsRoot.transform);
            }

            // Varsa eski MapInputHandler temizle
            var existingHandler = gameManagerGO.GetComponent<MapInputHandler>();
            if (existingHandler != null) Object.DestroyImmediate(existingHandler);

            MapInputHandler inputHandler = gameManagerGO.AddComponent<MapInputHandler>();
            var inputSO = new SerializedObject(inputHandler);
            inputSO.FindProperty("_gridManager").objectReferenceValue = gridManager;
            inputSO.FindProperty("_player").objectReferenceValue      = playerCtrl;
            inputSO.FindProperty("_rayDistance").floatValue           = 300f;
            inputSO.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[TacticalRPG] Faz 1.3 tamamlandi.");
            EditorUtility.DisplayDialog(
                "Faz 1.3 Tamamlandi!",
                "Eklenenler:\n\n" +
                "  • HexPathfinder (A* algoritması)\n" +
                "  • Player (küp) — (0,0) konumunda\n" +
                "  • MapInputHandler — sol tık ile hareket\n" +
                "  • 3 Watchtower konumu: (3,5) (1,2) (4,8)\n" +
                "  • MeshCollider — tüm hex karolarda\n\n" +
                "Play'e bas, haritaya sol tıkla — karakter A* ile yürür!\n" +
                "Watchtower karosuna gidince geniş alan açılır.",
                "Tamam");
        }

        // ─────────────────────────────────────────────────────────────────────
        // MENÜ: Faz 1.4 — AP + Zaman Motoru
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("TacticalRPG/Faz 1.4 - AP ve Zaman Motoru")]
        public static void SetupPhase14()
        {
            GameObject systemsRoot = GameObject.Find(SystemsRootName);
            if (systemsRoot == null)
            {
                EditorUtility.DisplayDialog("Hata", "Once 'Faz 1.1' ve 'Faz 1.3' calistirin!", "Tamam");
                return;
            }

            PlayerController player = systemsRoot.GetComponentInChildren<PlayerController>();
            if (player == null)
            {
                EditorUtility.DisplayDialog("Hata", "PlayerController bulunamadi! Once Faz 1.3 calistirin.", "Tamam");
                return;
            }

            // ── TimeSlotConfig ScriptableObject asset ──────────────────────
            EnsureFolder("Assets/Data");
            EnsureFolder("Assets/Data/Config");
            const string configPath = "Assets/Data/Config/TimeSlotConfig.asset";
            TimeSlotConfig config = AssetDatabase.LoadAssetAtPath<TimeSlotConfig>(configPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<TimeSlotConfig>();
                AssetDatabase.CreateAsset(config, configPath);
            }

            // ── ActionPointManager — GameManager objesine ekle ─────────────
            GameObject sceneRoot    = GameObject.Find(SceneRootName);
            GameObject gameManagerGO = sceneRoot != null
                ? sceneRoot.transform.Find("GameManager")?.gameObject
                : systemsRoot.transform.Find("GameManager")?.gameObject;

            if (gameManagerGO == null)
            {
                gameManagerGO = new GameObject("GameManager");
                gameManagerGO.transform.SetParent(systemsRoot.transform);
            }

            var existingAP = gameManagerGO.GetComponent<ActionPointManager>();
            if (existingAP != null) Object.DestroyImmediate(existingAP);

            ActionPointManager apManager = gameManagerGO.AddComponent<ActionPointManager>();
            var apSO = new SerializedObject(apManager);
            apSO.FindProperty("_player").objectReferenceValue  = player;
            apSO.FindProperty("_config").objectReferenceValue  = config;
            apSO.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[TacticalRPG] Faz 1.4 tamamlandi.");
            EditorUtility.DisplayDialog(
                "Faz 1.4 Tamamlandi!",
                "AP + Zaman Motoru aktif:\n\n" +
                "  • ActionPointManager — GameManager'a eklendi\n" +
                "  • TimeSlotConfig SO — Assets/Data/Config/\n" +
                "  • 3 AP = 1 zaman dilimi\n" +
                "  • 6 dilim = 1 gün\n\n" +
                "Console'da 'Gün X | Dilim' loglarını izle.\n" +
                "Faz 1.5 ile Kıyamet Sayacı eklenecek.",
                "Tamam");
        }

        // ─────────────────────────────────────────────────────────────────────
        // MENÜ: Faz 1.5 — Map Collapse / Kıyamet Sayacı
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("TacticalRPG/Faz 1.5 - Kiyamet Sayaci (Map Collapse)")]
        public static void SetupPhase15()
        {
            GameObject systemsRoot = GameObject.Find(SystemsRootName);
            if (systemsRoot == null)
            {
                EditorUtility.DisplayDialog("Hata", "Once Faz 1.1, 1.3 ve 1.4 calistirin!", "Tamam");
                return;
            }

            HexGridManager     gridManager = systemsRoot.GetComponentInChildren<HexGridManager>();
            PlayerController   player      = systemsRoot.GetComponentInChildren<PlayerController>();
            ActionPointManager apManager   = FindComponentAnywhere<ActionPointManager>();

            if (gridManager == null || player == null || apManager == null)
            {
                EditorUtility.DisplayDialog("Hata", "HexGridManager, PlayerController veya ActionPointManager bulunamadi!", "Tamam");
                return;
            }

            // ── CollapseConfig ScriptableObject ───────────────────────────
            EnsureFolder("Assets/Data");
            EnsureFolder("Assets/Data/Config");
            const string configPath = "Assets/Data/Config/CollapseConfig.asset";
            CollapseConfig config = AssetDatabase.LoadAssetAtPath<CollapseConfig>(configPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<CollapseConfig>();
                AssetDatabase.CreateAsset(config, configPath);
            }

            // ── Collapse materyali ────────────────────────────────────────
            Material collapsedMat = GetOrCreateUnlitMaterial("TileCollapsed", new Color(0.15f, 0.05f, 0.05f));

            // ── MapCollapseManager — GameManager'a ekle ───────────────────
            GameObject sceneRoot    = GameObject.Find(SceneRootName);
            GameObject gameManagerGO = sceneRoot != null
                ? sceneRoot.transform.Find("GameManager")?.gameObject
                : systemsRoot.transform.Find("GameManager")?.gameObject;

            if (gameManagerGO == null)
            {
                gameManagerGO = new GameObject("GameManager");
                gameManagerGO.transform.SetParent(systemsRoot.transform);
            }

            var existing = gameManagerGO.GetComponent<MapCollapseManager>();
            if (existing != null) Object.DestroyImmediate(existing);

            MapCollapseManager collapseManager = gameManagerGO.AddComponent<MapCollapseManager>();
            var collapseSO = new SerializedObject(collapseManager);
            collapseSO.FindProperty("_gridManager").objectReferenceValue      = gridManager;
            collapseSO.FindProperty("_apManager").objectReferenceValue        = apManager;
            collapseSO.FindProperty("_player").objectReferenceValue           = player;
            collapseSO.FindProperty("_config").objectReferenceValue           = config;
            collapseSO.FindProperty("_collapsedMaterial").objectReferenceValue = collapsedMat;
            collapseSO.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[TacticalRPG] Faz 1.5 tamamlandi.");
            EditorUtility.DisplayDialog(
                "Faz 1.5 Tamamlandi! — Kiyamet Sayaci",
                "Map Collapse aktif:\n\n" +
                "  • MapCollapseManager — GameManager'a eklendi\n" +
                "  • CollapseConfig SO — Assets/Data/Config/\n" +
                "  • Gün 4'ten itibaren her gün sonu karo silinir\n" +
                "  • Hız: 2 karo/gün, +1 ivme (max 10)\n" +
                "  • Oyuncu konumu ve Watchtower'lar korunur\n\n" +
                "Hafta 1 TAMAMLANDI! Hafta 2'ye gecmek icin hazir.",
                "Tamam");
        }

        private static T FindComponentAnywhere<T>() where T : UnityEngine.Component
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<T>();
#else
            return Object.FindObjectOfType<T>();
#endif
        }

        // ─────────────────────────────────────────────────────────────────────
        // Yardımcı metodlar
        // ─────────────────────────────────────────────────────────────────────

        private static bool DestroyRoot(string rootName)
        {
            GameObject go = GameObject.Find(rootName);
            if (go == null) return false;
            Object.DestroyImmediate(go);
            Debug.Log($"[TacticalRPG] '{rootName}' sahneden silindi.");
            return true;
        }

        private static Material GetOrCreateUnlitMaterial(string assetName, Color color)
        {
            string path = $"{MaterialsPath}/{assetName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
            {
                mat.color = color;
                EditorUtility.SetDirty(mat);
                return mat;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color");
            mat = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static Mesh GetOrCreateHexMesh()
        {
            string meshPath = "Assets/Art/Meshes/HexMesh.asset";
            EnsureFolder("Assets/Art/Meshes");

            // Her seferinde yeniden üret — HexMetrics değişirse güncel kalsın
            AssetDatabase.DeleteAsset(meshPath);
            Mesh mesh = HexMetrics.CreateHexMesh(0.95f);
            AssetDatabase.CreateAsset(mesh, meshPath);
            return mesh;
        }

        private static GameObject GetOrCreateHexCellPrefab(Material defaultMat)
        {
            string path = $"{PrefabsGridPath}/HexCell.prefab";

            Mesh hexMesh = GetOrCreateHexMesh();

            // Her seferinde prosedürel hex mesh ile yeniden oluştur
            GameObject go = new GameObject("HexCell");
            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = hexMesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = defaultMat;
            // MeshCollider — Raycast tıklama algılaması için gerekli
            MeshCollider mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = hexMesh;

            // PrefabUtility.SaveAsPrefabAsset varsa üzerine yazar
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folderName = System.IO.Path.GetFileName(path);
            if (parent != null)
                AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
