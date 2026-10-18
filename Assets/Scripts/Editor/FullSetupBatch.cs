using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TacticalRPG.Editor
{
    /// <summary>Runs the same TacticalRPG/TAM KURULUM menu action on the game scene in batch mode.</summary>
    public static class FullSetupBatch
    {
        private const string ScenePath = "Assets/Scenes/xd.unity";

        public static void OpenAndPlay()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameViewResolutionTool.Configure();
            EditorApplication.delayCall += () =>
            {
                EditorApplication.isPlaying = true;
                Debug.Log($"[TacticalRPG] Oyun Play modunda acildi: {ScenePath}");
            };
        }

        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
                throw new System.InvalidOperationException($"Sahne acilamadi: {ScenePath}");

            SceneSetupTool.FullSetup();
            if (!EditorSceneManager.SaveScene(scene))
                throw new System.InvalidOperationException($"Sahne kaydedilemedi: {ScenePath}");

            AssetDatabase.SaveAssets();
            Debug.Log($"[TacticalRPG] TAM KURULUM sahneye kaydedildi: {ScenePath}");
        }
    }
}
