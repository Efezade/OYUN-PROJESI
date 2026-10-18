using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TacticalRPG.Prototypes.Expedition;

namespace TacticalRPG.Editor
{
    public static class WeaponJourneySetup
    {
        public const string ScenePath="Assets/Scenes/Prototypes/WeaponJourney.unity";
        public const string DataPath="Assets/Data/Prototypes/WeaponJourney.asset";
        public static void EnsureAssets()
        {
            ExpeditionSetup.EnsureAssets();if(AssetDatabase.LoadAssetAtPath<ExpeditionConfig>(DataPath)!=null)return;
            AssetDatabase.CopyAsset(ExpeditionSetup.DataPath,DataPath);
            var config=AssetDatabase.LoadAssetAtPath<ExpeditionConfig>(DataPath);var so=new SerializedObject(config);
            so.FindProperty("_weaponJourney").boolValue=true;
            var thresholds=so.FindProperty("_powerThresholds");int[] values={3,9,22,29,41,49};thresholds.arraySize=values.Length;
            for(int i=0;i<values.Length;i++)thresholds.GetArrayElementAtIndex(i).intValue=values[i];
            so.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssets();
        }
        public static void EnsureScene()
        {
            EnsureAssets();if(File.Exists(ScenePath))return;ExpeditionSetup.EnsureScene();AssetDatabase.CopyAsset(ExpeditionSetup.ScenePath,ScenePath);
            var previous=SceneManager.GetActiveScene();var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
            try
            {
                foreach(var root in scene.GetRootGameObjects())
                {
                    foreach(var controller in root.GetComponentsInChildren<ExpeditionController>(true))
                    {var so=new SerializedObject(controller);so.FindProperty("_config").objectReferenceValue=AssetDatabase.LoadAssetAtPath<ExpeditionConfig>(DataPath);so.ApplyModifiedPropertiesWithoutUndo();}
                    foreach(var text in root.GetComponentsInChildren<Text>(true))
                    {
                        if(text.name=="Title"&&text.transform.parent.name=="Menu")text.text="KAM / İKİ SİLAH · ÜÇ GÖREV";
                        if(text.name=="Intro")text.text="Yakından hilal darbesi veya uzaktan delici mızrak.\nSilahın ve yeteneklerin üç görev boyunca korunur.\n\nTemel saldırı otomatik · Q silaha özel · E isabetlerle dolar\nHer seçim yeni bir yetenek ya da davranış açar.\nBaşlangıçta iki seçenek vardır; sıraları tohuma göre değişir.";
                        if(text.name=="Disclaimer")text.text="Ayrı oynanış prototipi · İlk haritanın görev menüsü değildir.\nMabet kaydı v3'e ayrıdır; eski B kayıtlarını değiştirmez.";
                    }
                }
                EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            }
            finally{if(previous.IsValid())SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scene,true);}
        }
        [MenuItem("TacticalRPG/Prototipler/B - Iki Silah Uc Gorev",false,312)]
        public static void Open(){if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;EnsureScene();EditorSceneManager.OpenScene(ScenePath);}
        public static void BuildWindows()
        {
            EnsureScene();CombatLabValidation.RunChecks();ExpeditionValidation.RunChecks();ExpeditionTacticsValidation.RunChecks();WeaponJourneyValidation.RunChecks();
            EncounterValidation.RunChecks();Directory.CreateDirectory("Builds/WeaponJourneyV4");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Builds/WeaponJourneyV4/KamWeaponJourney.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Weapon journey build failed: "+report.summary.result);
            Debug.Log("WEAPON_JOURNEY_BUILD_OK "+report.summary.totalSize);
        }
    }
}
