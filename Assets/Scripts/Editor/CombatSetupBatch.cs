using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace TacticalRPG.Editor
{
    /// <summary>Savaş kurulumunu Unity'yi elle açmadan koşturur (batch doğrulaması):
    /// <c>-executeMethod TacticalRPG.Editor.CombatSetupBatch.Run</c></summary>
    public static class CombatSetupBatch
    {
        private const string ScenePath = "Assets/Scenes/xd.unity";

        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid()) { Debug.LogError($"[Savas] Sahne acilamadi: {ScenePath}"); EditorApplication.Exit(1); return; }

            SceneSetupTool.SetupCombat();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Savas] Kurulum sahneye islendi ve kaydedildi.");
        }
    }
}
