using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TacticalRPG.Prototypes.Expedition;

namespace TacticalRPG.Editor
{
    public static class WardenSetup
    {
        private const string Folder="Assets/Data/Prototypes/Warden";
        private const string Prefab=Folder+"/AshWarden.prefab";
        private static Material Material(string name,Color color)
        {
            var found=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/"+name+".mat");if(found!=null)return found;
            var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.25f);AssetDatabase.CreateAsset(m,Folder+"/"+name+".mat");return m;
        }
        private static Transform Part(string name,Transform parent,PrimitiveType type,Vector3 p,Vector3 scale,Material material)
        {var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=scale;Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=material;return go.transform;}
        public static void EnsureAssets()
        {
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Data/Prototypes","Warden");
            if(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab)!=null)
            {
                var loaded=PrefabUtility.LoadPrefabContents(Prefab);try{var rigSo=new SerializedObject(loaded.GetComponent<WardenVisualRig>());var legs=loaded.GetComponentsInChildren<Transform>();int n=0;foreach(var leg in legs)if(leg.name=="Greave"){var field=rigSo.FindProperty(n++==0?"_leftLeg":"_rightLeg");if(field.objectReferenceValue==null)field.objectReferenceValue=leg;}rigSo.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.SaveAsPrefabAsset(loaded,Prefab);}finally{PrefabUtility.UnloadPrefabContents(loaded);}return;
            }
            var armor=Material("Basalt",new Color(.14f,.16f,.20f));var trim=Material("OldBrass",new Color(.55f,.39f,.16f));var ember=Material("Ember",new Color(1,.25f,.06f));
            var root=new GameObject("Ash Warden - replaceable visual");var rig=root.AddComponent<WardenVisualRig>();
            Transform leftLeg=null,rightLeg=null;
            var torso=new GameObject("Torso").transform;torso.SetParent(root.transform,false);
            Part("Armored body",torso,PrimitiveType.Capsule,new Vector3(0,2,0),new Vector3(1.2f,1.3f,.85f),armor);
            Part("Helm",torso,PrimitiveType.Cube,new Vector3(0,3.35f,0),new Vector3(.85f,.85f,.8f),armor);
            Part("Visor",torso,PrimitiveType.Cube,new Vector3(0,3.4f,.43f),new Vector3(.6f,.1f,.06f),ember);
            var core=Part("Exposed heart",torso,PrimitiveType.Sphere,new Vector3(0,2.3f,.5f),new Vector3(.42f,.55f,.2f),ember);
            for(int side=-1;side<=1;side+=2)
            {
                Part("Shoulder",torso,PrimitiveType.Cube,new Vector3(side*.85f,2.9f,0),new Vector3(.8f,.45f,.9f),armor).localRotation=Quaternion.Euler(0,0,side*18);
                var leg=Part("Greave",root.transform,PrimitiveType.Cube,new Vector3(side*.4f,.6f,0),new Vector3(.5f,1.2f,.6f),armor);if(side<0)leftLeg=leg;else rightLeg=leg;
                Part("Broken crown",torso,PrimitiveType.Cube,new Vector3(side*.4f,4,0),new Vector3(.17f,.9f,.2f),trim).localRotation=Quaternion.Euler(0,0,-side*18);
            }
            Part("Crown crest",torso,PrimitiveType.Cube,new Vector3(0,4.1f,0),new Vector3(.22f,1.1f,.22f),trim);
            var blade=new GameObject("Blade pivot").transform;blade.SetParent(torso,false);blade.localPosition=new Vector3(1.1f,2.1f,.1f);
            Part("Great blade",blade,PrimitiveType.Cube,new Vector3(0,0,1.2f),new Vector3(.48f,.16f,3.2f),armor);
            Part("Blade seam",blade,PrimitiveType.Cube,new Vector3(0,.1f,1.2f),new Vector3(.07f,.05f,3),ember);
            Part("Guard",blade,PrimitiveType.Cube,Vector3.zero,new Vector3(1,.25f,.2f),trim);
            var so=new SerializedObject(rig);so.FindProperty("_leftLeg").objectReferenceValue=leftLeg;so.FindProperty("_rightLeg").objectReferenceValue=rightLeg;so.FindProperty("_bladePivot").objectReferenceValue=blade;so.FindProperty("_torso").objectReferenceValue=torso;so.FindProperty("_core").objectReferenceValue=core.GetComponent<Renderer>();so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root,Prefab);Object.DestroyImmediate(root);AssetDatabase.SaveAssets();
        }
        private static RectTransform Rect(string name,Transform parent,Vector2 at,Vector2 size)
        {var rt=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rt.SetParent(parent,false);rt.anchorMin=rt.anchorMax=new Vector2(.5f,1);rt.anchoredPosition=at;rt.sizeDelta=size;return rt;}
        public static void EnsureScene()
        {
            MasteryJourneySetup.EnsureScene();EnsureAssets();var previous=SceneManager.GetActiveScene();var scene=EditorSceneManager.OpenScene(MasteryJourneySetup.ScenePath,OpenSceneMode.Additive);
            try
            {
                foreach(var root in scene.GetRootGameObjects())
                {
                    foreach(var view in root.GetComponentsInChildren<ExpeditionView>(true))
                    {var so=new SerializedObject(view);if(so.FindProperty("_wardenPrefab").objectReferenceValue==null){so.FindProperty("_wardenPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(Prefab).GetComponent<WardenVisualRig>();so.ApplyModifiedPropertiesWithoutUndo();}}
                    foreach(var hud in root.GetComponentsInChildren<ExpeditionHud>(true))
                    {
                        var so=new SerializedObject(hud);if(so.FindProperty("_wardenPanel").objectReferenceValue!=null)continue;
                        var parent=((GameObject)so.FindProperty("_game").objectReferenceValue).transform;
                        var panel=Rect("Warden status",parent,new Vector2(0,-135),new Vector2(760,78));panel.gameObject.AddComponent<Image>().color=new Color(.06f,.06f,.09f,.96f);
                        var label=Rect("Warden label",panel,new Vector2(0,-27),new Vector2(730,48)).gameObject.AddComponent<Text>();label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.fontSize=18;label.color=new Color(1,.86f,.65f);label.alignment=TextAnchor.MiddleCenter;label.raycastTarget=false;
                        var back=Rect("Health track",panel,new Vector2(0,-65),new Vector2(710,10));back.gameObject.AddComponent<Image>().color=new Color(.2f,.14f,.14f);
                        var fill=Rect("Health fill",back,Vector2.zero,new Vector2(710,10)).gameObject.AddComponent<Image>();fill.rectTransform.anchorMin=fill.rectTransform.anchorMax=new Vector2(.5f,.5f);fill.type=Image.Type.Filled;fill.fillMethod=Image.FillMethod.Horizontal;fill.color=new Color(.85f,.25f,.12f);fill.raycastTarget=false;
                        so.FindProperty("_wardenPanel").objectReferenceValue=panel.gameObject;so.FindProperty("_wardenLabel").objectReferenceValue=label;so.FindProperty("_wardenFill").objectReferenceValue=fill;so.ApplyModifiedPropertiesWithoutUndo();panel.gameObject.SetActive(false);
                    }
                }
                EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            }
            finally{if(previous.IsValid())SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scene,true);}
        }
    }
}
