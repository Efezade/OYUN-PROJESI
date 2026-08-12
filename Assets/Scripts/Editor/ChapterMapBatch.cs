using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace TacticalRPG.Editor
{
    /// <summary>
    /// Haritayı Unity'yi ELLE AÇMADAN üretip sahneye işleyen batch girişi:
    /// <code>
    /// Unity.exe -batchmode -quit -projectPath "C:\3D OYUN\OYUN" ^
    ///           -executeMethod TacticalRPG.Editor.ChapterMapBatch.RegenerateSceneMap -logFile log.txt
    /// </code>
    ///
    /// Neden gerekli: sahnede üretilmiş karolar GERÇEK GameObject olarak serileşiyor. Üretici
    /// değiştiğinde sahne dosyasındaki karolar ESKİ haritanınki olarak kalır ve Unity açılınca
    /// (Play'e basmadan) eski harita görünür — "değişiklik olmamış" izlenimi verir. Bu giriş
    /// sahneyi açar, mevcut "Haritayi Simdi Uret" akışını koşturur ve sahneyi kaydeder.
    ///
    /// Menüden aynı işi <c>TacticalRPG ▸ Bolum - Haritayi Simdi Uret</c> yapar (dialog gösterir).
    /// </summary>
    public static class ChapterMapBatch
    {
        private const string ScenePath = "Assets/Scenes/xd.unity";

        public static void RegenerateSceneMap()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[Bolum] Sahne acilamadi: {ScenePath}");
                EditorApplication.Exit(1);
                return;
            }

            // Karo görselleri hazır olmalı (yeni id'ler palette'te yoksa harita gri çıkar).
            TileVisualFactory.BuildAll(force: false);

            SceneSetupTool.GenerateChapterMapInEditor();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Bolum] Sahne haritasi yenilendi ve kaydedildi.");
        }
    }
}
