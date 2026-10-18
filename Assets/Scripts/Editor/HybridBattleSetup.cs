using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using BattleTileWorkshop;
using TacticalRPG.Prototypes.Expedition;
using Object=UnityEngine.Object;

namespace TacticalRPG.Editor
{
    public static class HybridBattleSetup
    {
        public const string DataPath="Assets/Data/Prototypes/HybridBattle.asset";
        public const string LayoutPath="Assets/Data/Prototypes/HybridBattleLayout.asset";
        public const string ScenePath="Assets/Scenes/Prototypes/HybridBattle.unity";
        private const string Art="Assets/Art/Prototypes/HybridBattle";
        private static void Set(Object obj,string field,Object value){var so=new SerializedObject(obj);so.FindProperty(field).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
        public static void EnsureAssets()
        {
            MasteryJourneySetup.EnsureAssets();Directory.CreateDirectory(Art);AssetDatabase.Refresh();
            var layout=AssetDatabase.LoadAssetAtPath<HybridBattleLayout>(LayoutPath);
            if(layout==null)
            {
                layout=ScriptableObject.CreateInstance<HybridBattleLayout>();AssetDatabase.CreateAsset(layout,LayoutPath);
                Set(layout,"_groundPrefab",AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BattleTilePack/Prefabs/SeamlessHexGround.prefab"));
                Set(layout,"_gatePrefab",AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BattleMapPack/EntranceGate/Prefabs/EntranceGate.prefab"));
                Set(layout,"_bridgeMaterial",AssetDatabase.LoadAssetAtPath<Material>("Assets/BattleTilePack/Materials/SeamlessGround_Top.mat"));
                Set(layout,"_trimMaterial",AssetDatabase.LoadAssetAtPath<Material>("Assets/BattleTilePack/Materials/SeamlessGround_Sides.mat"));
            }
            if(AssetDatabase.LoadAssetAtPath<ExpeditionConfig>(DataPath)==null)
            {
                AssetDatabase.CopyAsset(MasteryJourneySetup.DataPath,DataPath);var c=AssetDatabase.LoadAssetAtPath<ExpeditionConfig>(DataPath);Set(c,"_hybridLayout",layout);
                var so=new SerializedObject(c);so.FindProperty("_tileRadius").floatValue=1;so.FindProperty("_cameraOffset").vector3Value=new Vector3(-10,16,-12);so.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.SaveAssets();
        }
        private static GameObject Box(string name,Transform parent,Vector3 position,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;return go;
        }
        private static GameObject Bridge(HybridBattleLayout layout,int stage)
        {
            string path=Art+"/Bridge_"+stage+".prefab";var found=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(found!=null)return found;
            var root=new GameObject("Geçici köprü "+stage);float start=layout.BridgeStart(stage),end=layout.BridgeEnd(stage),width=layout.HalfWidth;
            var v=new List<Vector3>();var t=new List<int>();int count=Mathf.CeilToInt((end-start)/.2f);
            for(int i=0;i<=count;i++)
            {float x=Mathf.Lerp(start,end,(float)i/count);float y=layout.Height(new Vector2(x,0));v.Add(new Vector3(x-start,y,-width));v.Add(new Vector3(x-start,y,width));}
            for(int i=0;i<count;i++){int n=i*2;t.Add(n);t.Add(n+1);t.Add(n+2);t.Add(n+1);t.Add(n+3);t.Add(n+2);}
            var mesh=new Mesh{name="Bridge walkable deck"};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            string meshPath=Art+"/BridgeDeck_"+stage+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);if(existing==null)AssetDatabase.CreateAsset(mesh,meshPath);else{Object.DestroyImmediate(mesh);mesh=existing;}
            var deck=new GameObject("Yürünebilir yüzey",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));deck.transform.SetParent(root.transform,false);deck.GetComponent<MeshFilter>().sharedMesh=mesh;deck.GetComponent<MeshRenderer>().sharedMaterial=layout.BridgeMaterial;deck.GetComponent<MeshCollider>().sharedMesh=mesh;
            // Low parapets leave telegraphs visible. Collision is handled by the shared layout boundary.
            int spans=Mathf.CeilToInt((end-start)/1.4f);
            for(int i=0;i<spans;i++)
            {
                float a=Mathf.Lerp(start,end,(float)i/spans),b=Mathf.Lerp(start,end,(float)(i+1)/spans);float ya=layout.Height(new Vector2(a,0)),yb=layout.Height(new Vector2(b,0));float x=(a+b)*.5f-start;
                for(int side=-1;side<=1;side+=2)
                {
                    var rail=Box("Alçak korkuluk",root.transform,new Vector3(x,(ya+yb)*.5f+.22f,side*(width+.12f)),new Vector3(b-a+.015f,.44f,.24f),layout.TrimMaterial);rail.transform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(yb-ya,b-a)*Mathf.Rad2Deg);
                    if(i%2==0)Box("Köprü ayağı",root.transform,new Vector3(x,(ya+yb)*.25f-1.1f,side*(width-.2f)),new Vector3(.45f,(ya+yb)*.5f+2.2f,.45f),layout.TrimMaterial);
                }
            }
            found=PrefabUtility.SaveAsPrefabAsset(root,path);Object.DestroyImmediate(root);return found;
        }
        public static void EnsureScene()
        {
            EnsureAssets();if(File.Exists(ScenePath))return;WardenSetup.EnsureScene();AssetDatabase.CopyAsset(MasteryJourneySetup.ScenePath,ScenePath);
            var prior=SceneManager.GetActiveScene();var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);SceneManager.SetActiveScene(scene);
            try
            {
                var c=AssetDatabase.LoadAssetAtPath<ExpeditionConfig>(DataPath);var map=new ExpeditionMap(c);var layout=c.HybridLayout;ExpeditionView view=null;ExpeditionHud hud=null;
                foreach(var root in scene.GetRootGameObjects())
                {
                    if(root.name=="Connected terrain — height and slopes"||root.name=="Authored cover and landmarks"||root.name.StartsWith("Area name ")){Object.DestroyImmediate(root);continue;}
                    foreach(var controller in root.GetComponentsInChildren<ExpeditionController>(true))Set(controller,"_config",c);
                    foreach(var candidate in root.GetComponentsInChildren<ExpeditionView>(true))view=candidate;
                    foreach(var candidate in root.GetComponentsInChildren<ExpeditionHud>(true))hud=candidate;
                }
                var ground=new GameObject("HIBRIT · Düzenlenebilir zeminler");
                foreach(var h in map.Platforms)
                {
                    var go=(GameObject)PrefabUtility.InstantiatePrefab(layout.GroundPrefab,scene);go.transform.SetParent(ground.transform,false);go.name="Hex "+h.Q+","+h.R;
                    var pos=h.World(c.Radius);go.GetComponent<HexGroundPlacement>().Configure(h.Q,h.R,layout.Region(pos));
                }
                var structures=new GameObject("HIBRIT · Bağımsız yapılar");
                var gate=(GameObject)PrefabUtility.InstantiatePrefab(layout.GatePrefab,scene);gate.transform.SetParent(structures.transform,false);gate.transform.position=new Vector3(layout.GatePosition.x,layout.Level(0),layout.GatePosition.y);gate.transform.rotation=Quaternion.Euler(0,90,0);
                for(int i=0;i<2;i++){var bridge=(GameObject)PrefabUtility.InstantiatePrefab(Bridge(layout,i),scene);bridge.transform.SetParent(structures.transform,false);bridge.transform.position=new Vector3(layout.BridgeStart(i),0,0);}
                foreach(var p in map.Cover)
                {
                    Box("Geçici siper",structures.transform,new Vector3(p.x,map.Height(p)+.825f,p.y),new Vector3(.96f,1.65f,.96f),layout.TrimMaterial);
                    Box("Siper başlığı",structures.transform,new Vector3(p.x,map.Height(p)+1.62f,p.y),new Vector3(1.06f,.18f,1.06f),layout.BridgeMaterial);
                }
                var gates=new GameObject[2];
                for(int i=0;i<2;i++)
                {gates[i]=Box("Savaş kapanı "+i,structures.transform,new Vector3(map.EncounterEdge(i),layout.Level(i)+.5f,0),new Vector3(.12f,1,layout.HalfWidth*2),AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Prototypes/Expedition/Gate.mat"));}
                var vs=new SerializedObject(view);var arr=vs.FindProperty("_gates");arr.arraySize=2;for(int i=0;i<2;i++)arr.GetArrayElementAtIndex(i).objectReferenceValue=gates[i];vs.ApplyModifiedPropertiesWithoutUndo();
                foreach(var text in hud.GetComponentsInChildren<Text>(true))
                {
                    if(text.name=="Title"&&text.transform.parent.name=="Menu")text.text="KAM / HİBRİT SAVAŞ ATÖLYESİ";
                    if(text.name=="Intro")text.text="Sarmaşıklı kapı → taş avlu → köprü → yüksek teras.\nÜç görev boyunca silahın ve güçlerin korunur.\n\nWASD hareket · Q / E beceri · SPACE kaçış\nHaritayı gez: savaşsız ölçü ve geçiş denemesi.";
                    if(text.name=="Disclaimer")text.text="Zemin ve kapı: Zeynep'in atölye paketi. Köprü / siperler geçici.\nAyrı hibrit kayıt; önceki boss prototipi korunur.";
                }
                var hs=new SerializedObject(hud);var template=(Button)hs.FindProperty("_newRun").objectReferenceValue;var explore=Object.Instantiate(template,template.transform.parent);explore.name="Explore";((RectTransform)explore.transform).anchoredPosition=new Vector2(0,-181);((RectTransform)explore.transform).sizeDelta=new Vector2(460,46);explore.GetComponentInChildren<Text>().text="HARİTAYI GEZ · SAVAŞSIZ";Set(hud,"_explore",explore);
                var save=(Text)hs.FindProperty("_saveLabel").objectReferenceValue;save.rectTransform.anchoredPosition=new Vector2(0,-230);
                var p0=map.Entrance(0);view.Camera.transform.position=new Vector3(p0.x,map.Height(p0),p0.y)+c.CameraOffset;
                EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            }
            finally{if(prior.IsValid())SceneManager.SetActiveScene(prior);EditorSceneManager.CloseScene(scene,true);}
        }
        [MenuItem("TacticalRPG/Prototipler/B - Hibrit Savas Atolyesi",false,314)]
        public static void Open(){if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;EnsureScene();EditorSceneManager.OpenScene(ScenePath);}
        // Explicit first-review polish; normal setup never reapplies these authoring changes.
        public static void RefinePreviewAndBuild()
        {
            EnsureScene();var c=AssetDatabase.LoadAssetAtPath<ExpeditionConfig>(DataPath);var so=new SerializedObject(c);so.FindProperty("_cameraOffset").vector3Value=new Vector3(-10,16,-12);so.ApplyModifiedPropertiesWithoutUndo();
            var scene=EditorSceneManager.OpenScene(ScenePath);
            foreach(var root in scene.GetRootGameObjects())
            {
                if(root.name.StartsWith("Area name ")){Object.DestroyImmediate(root);continue;}
                foreach(var light in root.GetComponentsInChildren<Light>())if(light.type==LightType.Directional)light.intensity=1.05f;
            }
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            HybridBattleValidation.ValidateScene();BuildWindows();
        }
        public static void BuildWindows()
        {
            EnsureScene();CombatLabValidation.RunChecks();ExpeditionValidation.RunChecks();ExpeditionTacticsValidation.RunChecks();WeaponJourneyValidation.RunChecks();EncounterValidation.RunChecks();MasteryValidation.RunChecks();DynamicEnemyValidation.RunChecks();WardenValidation.RunChecks();HybridBattleValidation.RunChecks();
            Directory.CreateDirectory("Builds/HybridBattleV8");var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Builds/HybridBattleV8/KamHybrid.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Hybrid build failed: "+report.summary.result);Debug.Log("HYBRID_BUILD_OK");
        }
    }
}
