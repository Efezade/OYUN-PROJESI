using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TacticalRPG.Grid;

namespace TacticalRPG.Editor
{
    /// <summary>
    /// Faz 1.1 sahne kurulumunu tek tıklamayla otomatikleştirir.
    /// Menü: TacticalRPG > Faz 1.1 — Sahne Kurulumunu Yap
    /// </summary>
    public static class SceneSetupTool
    {
        private const string MaterialsPath   = "Assets/Art/Materials";
        private const string PrefabsGridPath = "Assets/Prefabs/Grid";
        private const string RootName        = "[TacticalRPG_Systems]";

        [MenuItem("TacticalRPG/0 — Sahneyi Temizle (Once Calistir)")]
        public static void CleanupScene()
        {
            GameObject existing = GameObject.Find(RootName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                Debug.Log($"[TacticalRPG] '{RootName}' sahneden silindi.");
                EditorUtility.DisplayDialog("Temizlik Tamamlandi", $"'{RootName}' ve tum alt objeleri sahneden kaldirildi.\n\nSimdi Faz 1.1 kurulumunu calistiabilirsin.", "Tamam");
            }
            else
            {
                EditorUtility.DisplayDialog("Temizlenecek Sey Yok", $"Sahnede '{RootName}' bulunamadi.\nDogrudan Faz 1.1 kurulumunu calistirabilirsin.", "Tamam");
            }
        }

        [MenuItem("TacticalRPG/Faz 1.1 — Sahne Kurulumunu Yap")]
        public static void SetupPhase1Scene()
        {
            // ── 1. Temizlik: önceki kurulumu sıfırla ─────────────────────
            CleanupExistingSetup();

            // ── 2. Klasör yapısını hazırla ────────────────────────────────
            EnsureFolder("Assets/Art");
            EnsureFolder(MaterialsPath);
            EnsureFolder("Assets/Prefabs");
            EnsureFolder(PrefabsGridPath);

            // ── 3. Asset'leri oluştur ─────────────────────────────────────
            Material hiddenMat   = GetOrCreateUnlitMaterial("FogHidden",   Color.black);
            Material exploredMat = GetOrCreateUnlitMaterial("FogExplored", new Color(0.25f, 0.25f, 0.25f));
            Material visibleMat  = GetOrCreateUnlitMaterial("FogVisible",  Color.white);
            GameObject hexCellPrefab = GetOrCreateHexCellPrefab(visibleMat);

            // ── 4. Ana parent ─────────────────────────────────────────────
            GameObject rootGO = new GameObject(RootName);

            // ── 5. HexGridManager ─────────────────────────────────────────
            GameObject gridGO = new GameObject("HexGridManager");
            gridGO.transform.SetParent(rootGO.transform);
            HexGridManager gridManager = gridGO.AddComponent<HexGridManager>();

            GameObject gridParentGO = new GameObject("HexGrid_Visuals");
            gridParentGO.transform.SetParent(gridGO.transform);

            var gridSO = new SerializedObject(gridManager);
            gridSO.FindProperty("_hexCellPrefab").objectReferenceValue = hexCellPrefab;
            gridSO.FindProperty("_gridParent").objectReferenceValue    = gridParentGO.transform;
            gridSO.FindProperty("_width").intValue                     = 10;
            gridSO.FindProperty("_height").intValue                    = 10;
            gridSO.FindProperty("_hexSize").floatValue                 = 1f;
            gridSO.ApplyModifiedProperties();

            // ── 6. FogOfWarManager ────────────────────────────────────────
            GameObject fogGO = new GameObject("FogOfWarManager");
            fogGO.transform.SetParent(rootGO.transform);
            FogOfWarManager fogManager = fogGO.AddComponent<FogOfWarManager>();

            var fogSO = new SerializedObject(fogManager);
            fogSO.FindProperty("_gridManager").objectReferenceValue      = gridManager;
            fogSO.FindProperty("_hiddenMaterial").objectReferenceValue   = hiddenMat;
            fogSO.FindProperty("_exploredMaterial").objectReferenceValue = exploredMat;
            fogSO.FindProperty("_visibleMaterial").objectReferenceValue  = visibleMat;
            fogSO.ApplyModifiedProperties();

            // ── 7. Kaydet ─────────────────────────────────────────────────
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[TacticalRPG] Faz 1.1 kurulumu tamamlandi. '{RootName}' altinda hazir.");
            EditorUtility.DisplayDialog(
                "Kurulum Tamamlandi!",
                $"'{RootName}' altinda olusturuldu:\n" +
                "  • HexGridManager (10x10 grid, hexSize=1)\n" +
                "  • FogOfWarManager (Hidden/Explored/Visible)\n" +
                "  • Prefabs/Grid/HexCell.prefab\n" +
                "  • Art/Materials/ (3 fog materyali)\n\n" +
                "Play'e bas — tum karolar siyah baslar.\n" +
                "Araci tekrar calistirirsan onceki kurulum otomatik silinir.",
                "Tamam"
            );
        }

        private static void CleanupExistingSetup()
        {
            GameObject existing = GameObject.Find(RootName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
                Debug.Log($"[TacticalRPG] Eski '{RootName}' temizlendi.");
            }
        }

        // ── Yardımcı Metodlar ─────────────────────────────────────────────

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

            // URP projelerde Unlit/Color yoktur; Universal Render Pipeline/Unlit kullan
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            mat = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static GameObject GetOrCreateHexCellPrefab(Material defaultMat)
        {
            string path = $"{PrefabsGridPath}/HexCell.prefab";

            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            // Cylinder: ince disk — hexSize=1 için komşu merkez mesafesi sqrt(3)≈1.73
            // Çap 1.5 → her yanda ~0.12 birim boşluk bırakır
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = "HexCell";
            cylinder.transform.localScale = new Vector3(1.5f, 0.05f, 1.5f);

            // Placeholder materyal ata
            cylinder.GetComponent<Renderer>().sharedMaterial = defaultMat;

            // Capsule Collider kaldır (grid tıklama için ayrı raycast katmanı kullanılacak)
            Object.DestroyImmediate(cylinder.GetComponent<CapsuleCollider>());

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(cylinder, path);
            Object.DestroyImmediate(cylinder);
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
