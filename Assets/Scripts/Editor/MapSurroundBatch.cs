using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace TacticalRPG.Editor
{
    /// <summary>Harita çevresi kurulumunu Unity'yi elle açmadan koşturur (batch doğrulaması):
    /// <c>-executeMethod TacticalRPG.Editor.MapSurroundBatch.Run</c>.
    /// Haritayı YENİDEN ÜRETMEZ — yalnız çevre bileşenini/profilleri kurar ve sahneyi kaydeder.</summary>
    public static class MapSurroundBatch
    {
        private const string ScenePath = "Assets/Scenes/xd.unity";

        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[Cevre] Sahne acilamadi: {ScenePath}");
                EditorApplication.Exit(1);
                return;
            }

            SceneSetupTool.SetupMapSurround();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Cevre] Kurulum sahneye islendi ve kaydedildi.");
        }

        /// <summary>
        /// DOĞRULAMA koşumu: grid'i editörde kurar, çevreyi üretir ve sayıları loglar.
        /// <c>-executeMethod TacticalRPG.Editor.MapSurroundBatch.Verify</c>
        ///
        /// Sahneyi KAYDETMEZ — amacı yalnız "çevre gerçekten üretiliyor mu, kaç karo/süs çıkıyor"
        /// sorusunu Play'e basmadan cevaplamak. (Play mode'da ölçüm yapamıyorum; ölçülebilir olan
        /// şey üretimin kendisi — bkz. Docs'taki dürüstlük sınırı.)
        /// </summary>
        public static void Verify()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid()) { Debug.LogError("[Cevre] Sahne acilamadi."); EditorApplication.Exit(1); return; }

            var grid    = SceneSetupTool.FindComponentAnywhere<TacticalRPG.Grid.HexGridManager>();
            var builder = SceneSetupTool.FindComponentAnywhere<TacticalRPG.Core.MapSurroundBuilder>();
            if (grid == null || builder == null)
            {
                Debug.LogError("[Cevre] Grid ya da MapSurroundBuilder sahnede yok — once Run.");
                EditorApplication.Exit(1);
                return;
            }

            grid.GenerateGrid();                       // hücre sözlüğü editörde de dolsun
            Debug.Log($"[Cevre] Dogrulama: grid {grid.Cells.Count} hucre.");
            builder.Rebuild(force: true);              // sayılar Rebuild içinde loglanır
        }
    }
}
