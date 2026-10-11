using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TacticalRPG.UI;
namespace TacticalRPG.Editor
{
    [InitializeOnLoad] public static class PresentationUpgradeBootstrap
    {
        static PresentationUpgradeBootstrap()
        {
            EditorApplication.delayCall+=Apply;
            EditorSceneManager.sceneOpened+=(_,_)=>EditorApplication.delayCall+=Apply;
            EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=Apply;};
        }
        private static void Apply()
        {
            if(Application.isBatchMode||EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Apply;return;}
            var scene=EditorSceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/xd.unity"||!File.Exists("Assets/Data/Presentation/SoundLibrary.asset"))return;
            var marker=Object.FindFirstObjectByType<PresentationTheme>(FindObjectsInactive.Include);if(marker!=null&&marker.Revision>=PresentationTheme.CurrentRevision)return;
            Directory.CreateDirectory("Temp/PresentationBeforeUpgrade");string backup="Temp/PresentationBeforeUpgrade/xd_"+System.DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true)){Debug.LogError("[Presentation] Scene snapshot failed; migration postponed.");return;}
            if(marker!=null&&marker.Revision>=1)PresentationSetup.InstallRootRupture();else PresentationSetup.Install();
            EditorSceneManager.SaveScene(scene);
            File.WriteAllText("Temp/PresentationInstalled.txt","Installed with unsaved-edit snapshot: "+backup);
        }
        public static void BatchInstallOnly()
        {
            if(!File.Exists(".biome-qa-copy"))throw new System.InvalidOperationException("Isolated copy required");
            EditorSceneManager.OpenScene("Assets/Scenes/xd.unity");PresentationSetup.InstallRootRupture();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
            EditorApplication.Exit(0);
        }
    }
}
