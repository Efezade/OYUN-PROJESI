using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TacticalRPG.Prototypes.Expedition;
using Object=UnityEngine.Object;

namespace TacticalRPG.Editor
{
    public static class PressureBattleSetup
    {
        public const string DataPath="Assets/Data/Prototypes/PressureBattle.asset";
        public const string ScenePath="Assets/Scenes/Prototypes/PressureBattle.unity";
        private const string ProfilePath="Assets/Data/Prototypes/CombatPressure.asset";
        private const string MeteorPath="Assets/Data/Prototypes/Warden/Meteor.prefab";
        private static void Reference(Object obj,string name,Object value){var so=new SerializedObject(obj);so.FindProperty(name).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
        public static void EnsureAssets()
        {
            HybridBattleSetup.EnsureAssets();
            var profile=AssetDatabase.LoadAssetAtPath<CombatPressureProfile>(ProfilePath);
            if(profile==null){profile=ScriptableObject.CreateInstance<CombatPressureProfile>();AssetDatabase.CreateAsset(profile,ProfilePath);}
            if(AssetDatabase.LoadAssetAtPath<ExpeditionConfig>(DataPath)==null)
            {
                AssetDatabase.CopyAsset(HybridBattleSetup.DataPath,DataPath);var c=AssetDatabase.LoadAssetAtPath<ExpeditionConfig>(DataPath);
                Reference(c,"_pressure",profile);var so=new SerializedObject(c);
                so.FindProperty("_wardenRushSpeed").floatValue=16;
                so.FindProperty("_wardenWarning").floatValue=.95f;
                so.FindProperty("_wardenRecovery").floatValue=1;
                so.FindProperty("_wardenExposure").floatValue=2.1f;
                so.FindProperty("_wardenDamage").intValue=14;
                so.FindProperty("_wardenSlamInterval").floatValue=.48f;
                so.FindProperty("_wardenRingSpeed").floatValue=4;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            if(AssetDatabase.LoadAssetAtPath<GameObject>(MeteorPath)==null)
            {
                const string materialPath="Assets/Data/Prototypes/Warden/Meteor.mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.SetColor("_BaseColor",new Color(.32f,.12f,.035f));material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",new Color(1,.24f,.025f)*2);AssetDatabase.CreateAsset(material,materialPath);}
                var root=new GameObject("Gökten düşen kırık taş");
                for(int i=0;i<3;i++){var rock=GameObject.CreatePrimitive(PrimitiveType.Cube);rock.transform.SetParent(root.transform,false);rock.transform.localScale=new Vector3(.7f,.5f,.65f)/(1+i*.28f);rock.transform.localRotation=Quaternion.Euler(i*37,23+i*61,i*29);Object.DestroyImmediate(rock.GetComponent<Collider>());rock.GetComponent<Renderer>().sharedMaterial=material;}
                PrefabUtility.SaveAsPrefabAsset(root,MeteorPath);Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets();
        }
        public static void EnsureScene()
        {
            EnsureAssets();if(File.Exists(ScenePath))return;HybridBattleSetup.EnsureScene();AssetDatabase.CopyAsset(HybridBattleSetup.ScenePath,ScenePath);
            var prior=SceneManager.GetActiveScene();var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
            try
            {
                var c=AssetDatabase.LoadAssetAtPath<ExpeditionConfig>(DataPath);
                foreach(var root in scene.GetRootGameObjects())
                {
                    foreach(var controller in root.GetComponentsInChildren<ExpeditionController>(true))Reference(controller,"_config",c);
                    foreach(var view in root.GetComponentsInChildren<ExpeditionView>(true))Reference(view,"_meteorPrefab",AssetDatabase.LoadAssetAtPath<GameObject>(MeteorPath));
                    foreach(var text in root.GetComponentsInChildren<Text>(true))
                    {
                        if(text.name=="Title"&&text.transform.parent.name=="Menu")text.text="KAM / BASKI VE BOSS · v9";
                        if(text.name=="Intro")text.text="İlk görev: silahını öğren. İkinci: hedef önceliği.\nÜçüncü: kalabalığı ez, seçkinleri ve gökyüzünü oku.\n\nWASD hareket · Q / E beceri · SPACE kaçış\nBoss: kilitli gök saldırıları ve ani çift hücum.";
                        if(text.name=="Disclaimer")text.text="v9 denge deneyi · Can şişirmeden saldırı baskısı.\nHarita şu an v8 düzeninde; yeni koridor planı konsepttir.";
                    }
                }
                EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            }
            finally{if(prior.IsValid())SceneManager.SetActiveScene(prior);EditorSceneManager.CloseScene(scene,true);}
        }
        [MenuItem("TacticalRPG/Prototipler/B - Zorluk ve Boss v9",false,315)]
        public static void Open(){if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;EnsureScene();EditorSceneManager.OpenScene(ScenePath);}
        public static void BuildWindows()
        {
            EnsureScene();CombatLabValidation.RunChecks();ExpeditionValidation.RunChecks();ExpeditionTacticsValidation.RunChecks();WeaponJourneyValidation.RunChecks();EncounterValidation.RunChecks();MasteryValidation.RunChecks();DynamicEnemyValidation.RunChecks();WardenValidation.RunChecks();HybridBattleValidation.RunChecks();PressureBattleValidation.RunChecks();
            Directory.CreateDirectory("Builds/PressureBattleV9");var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Builds/PressureBattleV9/KamPressure.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Pressure build failed: "+report.summary.result);Debug.Log("PRESSURE_BUILD_OK");
        }
    }
}
