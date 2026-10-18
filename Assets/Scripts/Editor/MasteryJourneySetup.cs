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
    public static class MasteryJourneySetup
    {
        public const string DataPath="Assets/Data/Prototypes/MasteryJourney.asset";
        public const string ScenePath="Assets/Scenes/Prototypes/MasteryJourney.unity";
        public static void EnsureAssets()
        {
            WeaponJourneySetup.EnsureAssets();if(AssetDatabase.LoadAssetAtPath<ExpeditionConfig>(DataPath)!=null)return;
            AssetDatabase.CopyAsset(WeaponJourneySetup.DataPath,DataPath);
            var so=new SerializedObject(AssetDatabase.LoadAssetAtPath<ExpeditionConfig>(DataPath));so.FindProperty("_masteryJourney").boolValue=true;
            var thresholds=so.FindProperty("_powerThresholds");int[] values={3,9,25,34,55,62};thresholds.arraySize=values.Length;
            for(int i=0;i<values.Length;i++)thresholds.GetArrayElementAtIndex(i).intValue=values[i];
            so.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssets();
        }
        public static void EnsureScene()
        {
            EnsureAssets();if(File.Exists(ScenePath))return;WeaponJourneySetup.EnsureScene();AssetDatabase.CopyAsset(WeaponJourneySetup.ScenePath,ScenePath);
            var previous=SceneManager.GetActiveScene();var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
            try
            {
                foreach(var root in scene.GetRootGameObjects())
                {
                    foreach(var c in root.GetComponentsInChildren<ExpeditionController>(true))
                    {var so=new SerializedObject(c);so.FindProperty("_config").objectReferenceValue=AssetDatabase.LoadAssetAtPath<ExpeditionConfig>(DataPath);so.ApplyModifiedPropertiesWithoutUndo();}
                    foreach(var t in root.GetComponentsInChildren<Text>(true))
                    {
                        if(t.name=="Title"&&t.transform.parent.name=="Menu")t.text="KAM / USTALIK VE DÖNÜŞÜM";
                        if(t.name=="Intro")t.text="Üç görev boyunca silah ustalığın artar.\nÜçüncü görev: Üçlü Yarık veya Mızrak Yaylımı.\n\nEski düşmanları dağıt; altın işaretli seçkinlere dikkat et.\nTemel saldırı otomatik · Q silaha özel · E isabetlerle dolar\nAltı kart seçimi ve silahın görevler boyunca korunur.";
                        if(t.name=="Disclaimer")t.text="Ayrı üç görevlik prototip · v5 mabet kaydı\nÖnceki prototip kayıtlarını kullanmaz veya değiştirmez.";
                    }
                }
                EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            }
            finally{if(previous.IsValid())SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scene,true);}
        }
        [MenuItem("TacticalRPG/Prototipler/B - Ustalik ve Donusum",false,313)]
        public static void Open(){if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;WardenSetup.EnsureScene();EditorSceneManager.OpenScene(ScenePath);}
        public static void BuildWindows()
        {
            WardenSetup.EnsureScene();CombatLabValidation.RunChecks();ExpeditionValidation.RunChecks();ExpeditionTacticsValidation.RunChecks();WeaponJourneyValidation.RunChecks();EncounterValidation.RunChecks();MasteryValidation.RunChecks();DynamicEnemyValidation.RunChecks();WardenValidation.RunChecks();
            Directory.CreateDirectory("Builds/MasteryJourneyV7");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Builds/MasteryJourneyV7/KamMastery.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Mastery build failed: "+report.summary.result);
            Debug.Log("MASTERY_BUILD_OK");
        }
    }
}
