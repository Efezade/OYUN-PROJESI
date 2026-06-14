using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TacticalRPG.Grid;

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
