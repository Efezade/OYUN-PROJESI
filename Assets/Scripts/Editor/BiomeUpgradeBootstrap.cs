using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TacticalRPG.Core;

namespace TacticalRPG.Editor
{
    /// <summary>One-time migration of the open scene, preserving a copy including unsaved edits.
    /// No scene reload or user setup sequence is required after scripts finish importing.</summary>
    [InitializeOnLoad]
    public static class BiomeUpgradeBootstrap
    {
        static BiomeUpgradeBootstrap()
        {
            EditorApplication.delayCall += ApplyOnce;
            EditorSceneManager.sceneOpened += (_, _) => EditorApplication.delayCall += ApplyOnce;
            EditorApplication.playModeStateChanged += state =>
            { if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += ApplyOnce; };
        }
        private static void ApplyOnce()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            { EditorApplication.delayCall += ApplyOnce; return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/xd.unity" || !File.Exists(BiomeArtSetup.Models + "/manifest.json")) return;
            // v3 carries the semantic vertex masks used by the complete terrain shader.
            if (!File.ReadAllText(BiomeArtSetup.Models + "/manifest.json").Contains("\"version\": 3")) return;
            if (Object.FindFirstObjectByType<BayterekQuestRoots>(FindObjectsInactive.Include) != null &&
                Object.FindFirstObjectByType<BiomeBoundaryVisuals>(FindObjectsInactive.Include) != null &&
                Object.FindFirstObjectByType<BiomeTerrainStyling>(FindObjectsInactive.Include) != null) return;
            Directory.CreateDirectory("Temp/BiomeBeforeUpgrade");
            string backup = "Temp/BiomeBeforeUpgrade/xd_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity";
            if (!EditorSceneManager.SaveScene(scene, backup, true))
            { Debug.LogError("[Biome] Scene backup failed; automatic scene migration postponed."); return; }
            BiomeArtSetup.Install();
            EditorSceneManager.SaveScene(scene);
            File.WriteAllText("Temp/BiomeInstalled.txt", "Installed; scene snapshot: " + backup);
            Debug.Log("[Biome] Biyomlar ve Bayterek kokleri kuruldu. Kayit oncesi kopya: " + backup);
        }
    }
}
