using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TacticalRPG.Prototypes.CombatLab;
using TacticalRPG.Prototypes.Expedition;

namespace TacticalRPG.Editor
{
    public static class ExpeditionSetup
    {
        public const string ScenePath="Assets/Scenes/Prototypes/KamExpedition.unity";
        public const string DataPath="Assets/Data/Prototypes/Expedition.asset";
        private const string Art="Assets/Art/Prototypes/Expedition";
        private const string LabArt="Assets/Art/Prototypes/CombatLab";
        private static ExpeditionConfig _config;
        private static Font _font;
        public static void EnsureAssets()
        {
            Directory.CreateDirectory(Art);Directory.CreateDirectory("Assets/Data/Prototypes");Directory.CreateDirectory("Assets/Scenes/Prototypes");AssetDatabase.Refresh();
            _config=AssetDatabase.LoadAssetAtPath<ExpeditionConfig>(DataPath);
            if(_config==null){_config=ScriptableObject.CreateInstance<ExpeditionConfig>();AssetDatabase.CreateAsset(_config,DataPath);}
        }
        [MenuItem("TacticalRPG/Prototipler/B - Buyuyen Seferi Ac",false,310)]
        public static void Open()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            EnsureScene();EditorSceneManager.OpenScene(ScenePath);Selection.activeObject=AssetDatabase.LoadAssetAtPath<ExpeditionConfig>(DataPath);
        }
        public static void EnsureScene()
        {
            EnsureAssets();if(File.Exists(ScenePath))return;CombatLabSetup.EnsureScene();
            _font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var prior=SceneManager.GetActiveScene();bool additive=prior.IsValid()&&!string.IsNullOrEmpty(prior.path);
            if(!additive&&prior.isDirty)throw new InvalidOperationException("Önce açık sahneni kaydet.");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,additive?NewSceneMode.Additive:NewSceneMode.Single);SceneManager.SetActiveScene(scene);
            try
            {
                var map=new ExpeditionMap(_config);BuildGround(map);
                var sun=new GameObject("Daylight",typeof(Light)).GetComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.2f;sun.color=new Color(1,.94f,.82f);sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(52,-25,0);
                RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.72f,.78f,.81f);
                var camera=new GameObject("Expedition Camera",typeof(Camera),typeof(AudioListener),typeof(UniversalAdditionalCameraData)).GetComponent<Camera>();
                camera.orthographic=true;camera.orthographicSize=_config.CameraSize;camera.nearClipPlane=.1f;camera.farClipPlane=140;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.25f,.33f,.35f);
                var start=map.Entrance(0);camera.transform.position=new Vector3(start.x,.3f,start.y)+_config.CameraOffset;camera.transform.rotation=Quaternion.LookRotation(-_config.CameraOffset);
                var props=new GameObject("Authored cover and landmarks");
                var cover=Mat("Limestone",_config.Stone);
                foreach(var p in map.Cover)
                {var column=Primitive("Broken pillar",PrimitiveType.Cylinder,props.transform,new Vector3(p.x,map.Height(p)+.83f,p.y),new Vector3(1.05f,.83f,1.05f),cover);column.transform.rotation=Quaternion.Euler(0,24,0);}
                var gates=new GameObject[2];
                for(int i=0;i<2;i++)gates[i]=Primitive("Encounter gate "+i,PrimitiveType.Cube,props.transform,new Vector3(map.Center(i).x+10,1.1f,0),new Vector3(.12f,1.5f,3.9f),Mat("Gate",new Color(.77f,.48f,.19f),true));
                for(int i=0;i<3;i++)
                {
                    var p=map.Entrance(i);Primitive("Shrine step "+i,PrimitiveType.Cylinder,props.transform,new Vector3(p.x,.35f,p.y+3),new Vector3(1.5f,.08f,1.5f),cover);
                    Primitive("Shrine light "+i,PrimitiveType.Capsule,props.transform,new Vector3(p.x,1,p.y+3),new Vector3(.22f,.5f,.22f),Mat("Shrine",new Color(.3f,.9f,.8f),true));
                    var label=new GameObject("Area name "+i,typeof(TextMesh)).GetComponent<TextMesh>();label.font=_font;label.GetComponent<MeshRenderer>().sharedMaterial=_font.material;label.fontSize=48;label.characterSize=.08f;label.anchor=TextAnchor.MiddleCenter;label.color=new Color(.9f,.96f,.9f);label.text=i==0?"KIRIK GEÇİT":i==1?"SULARIN HAFIZASI":"YÜKSEK MABET";label.transform.position=new Vector3(map.Center(i).x,3,8);label.transform.rotation=camera.transform.rotation;
                }
                var root=new GameObject("B Expedition Prototype");var controller=root.AddComponent<ExpeditionController>();var view=root.AddComponent<ExpeditionView>();var audio=root.AddComponent<AudioSource>();audio.playOnAwake=false;
                var prefabs=new CombatLabActorView[6];for(int i=0;i<6;i++)prefabs[i]=AssetDatabase.LoadAssetAtPath<CombatLabActorView>(LabArt+"/Actor_"+(LabRole)i+".prefab");
                var marker=Primitive("Exit beacon",PrimitiveType.Sphere,root.transform,Vector3.zero,Vector3.one*.5f,Mat("Exit",new Color(.96f,.79f,.32f),true));
                var route=new GameObject("Route",typeof(LineRenderer)).GetComponent<LineRenderer>();route.transform.SetParent(root.transform);route.positionCount=2;route.widthMultiplier=.06f;route.sharedMaterial=Mat("Route",new Color(.85f,.79f,.34f),true);
                var hud=BuildHud();Set(controller,"_config",_config);Set(controller,"_view",view);Set(controller,"_hud",hud);
                Set(view,"_camera",camera);Set(view,"_actors",prefabs);Set(view,"_pulsePrefab",AssetDatabase.LoadAssetAtPath<GameObject>(LabArt+"/Pulse.prefab"));
                Set(view,"_linePrefab",AssetDatabase.LoadAssetAtPath<LineRenderer>(LabArt+"/HitLine.prefab"));Set(view,"_textPrefab",AssetDatabase.LoadAssetAtPath<TextMesh>(LabArt+"/Damage.prefab"));
                Set(view,"_spawnPrefab",SpawnPrefab());Set(view,"_exitMarker",marker.transform);Set(view,"_route",route);Set(view,"_gates",gates);Set(view,"_audio",audio);
                Set(view,"_hit",AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Original/SFX/axe_1.wav"));Set(view,"_chain",AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Original/SFX/card_pick_1.wav"));
                new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
                EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
            }
            finally{if(additive){if(prior.IsValid())SceneManager.SetActiveScene(prior);EditorSceneManager.CloseScene(scene,true);}}
        }
        private static void BuildGround(ExpeditionMap map)
        {
            var root=new GameObject("Connected terrain — height and slopes");
            for(int kind=0;kind<5;kind++)
            {
                string path=Art+"/Ground_"+kind+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(mesh==null)
                {
                    var vertices=new List<Vector3>();var triangles=new List<int>();
                    foreach(var h in map.Cells)
                    {
                        var p=h.World(_config.Radius);if((int)map.Ground(p)!=kind)continue;
                        int at=vertices.Count;vertices.Add(new Vector3(p.x,map.Height(p),p.y));
                        for(int i=0;i<6;i++){float angle=(30+i*60)*Mathf.Deg2Rad;var edge=p+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*_config.Radius*.99f;vertices.Add(new Vector3(edge.x,map.Height(edge),edge.y));}
                        for(int i=0;i<6;i++){triangles.Add(at);triangles.Add(at+1+(i+1)%6);triangles.Add(at+1+i);}
                        // Vertical outer skirt gives the raised ground physical thickness.
                        for(int i=0;i<6;i++)
                        {int a=at+1+i,b=at+1+(i+1)%6;int low=vertices.Count;vertices.Add(new Vector3(vertices[a].x,0,vertices[a].z));vertices.Add(new Vector3(vertices[b].x,0,vertices[b].z));triangles.Add(a);triangles.Add(b);triangles.Add(low);triangles.Add(b);triangles.Add(low+1);triangles.Add(low);}
                    }
                    mesh=new Mesh{name="Expedition terrain "+kind};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);
                }
                var go=new GameObject(((GroundKind)kind).ToString(),typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));go.transform.SetParent(root.transform);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshCollider>().sharedMesh=mesh;
                var color=kind==1?_config.Mud:kind==2?_config.Water:kind>=3?_config.Stone:_config.Grass;go.GetComponent<MeshRenderer>().sharedMaterial=Mat("Terrain_"+kind,color);
            }
        }
        private static Material Mat(string name,Color color,bool unlit=false)
        {
            string path=Art+"/"+name+".mat";var result=AssetDatabase.LoadAssetAtPath<Material>(path);if(result!=null)return result;
            result=new Material(Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit")){name=name};result.SetColor("_BaseColor",color);if(result.HasProperty("_Smoothness"))result.SetFloat("_Smoothness",.15f);AssetDatabase.CreateAsset(result,path);return result;
        }
        private static GameObject Primitive(string name,PrimitiveType type,Transform parent,Vector3 position,Vector3 scale,Material mat)
        {var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=mat;return go;}
        private static GameObject SpawnPrefab()
        {
            string path=Art+"/SpawnWarning.prefab";var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(prefab!=null)return prefab;
            var go=Primitive("Spawn warning",PrimitiveType.Cylinder,null,Vector3.zero,new Vector3(1,.01f,1),Mat("Spawn",new Color(.98f,.64f,.22f),true));prefab=PrefabUtility.SaveAsPrefabAsset(go,path);UnityEngine.Object.DestroyImmediate(go);return prefab;
        }
        private static GameObject PulseRing()
        {
            string path=Art+"/PulseRing.prefab";var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(prefab!=null)return prefab;
            string meshPath=Art+"/PulseRingMesh.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(mesh==null)
            {
                var vertices=new Vector3[128];var triangles=new int[384];
                for(int i=0;i<64;i++)
                {
                    float angle=i*Mathf.PI*2/64;var axis=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));vertices[i*2]=axis*.5f;vertices[i*2+1]=axis*.47f;
                    int a=i*2,b=((i+1)%64)*2,t=i*6;triangles[t]=a;triangles[t+1]=a+1;triangles[t+2]=b;triangles[t+3]=b;triangles[t+4]=a+1;triangles[t+5]=b+1;
                }
                mesh=new Mesh{name="Readable pulse ring"};mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,meshPath);
            }
            var go=new GameObject("Pulse ring",typeof(MeshFilter),typeof(MeshRenderer));go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=Mat("Pulse ring",new Color(.25f,.92f,.83f),true);
            prefab=PrefabUtility.SaveAsPrefabAsset(go,path);UnityEngine.Object.DestroyImmediate(go);return prefab;
        }
        private static void UpgradePulseReference()
        {
            // Narrow migration: replace only the original prototype's filled disk, preserving other edits.
            var ring=PulseRing();var previous=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath(ScenePath);bool opened=!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
            bool changed=false;
            foreach(var root in scene.GetRootGameObjects())foreach(var view in root.GetComponentsInChildren<ExpeditionView>(true))
            {
                var so=new SerializedObject(view);var p=so.FindProperty("_pulsePrefab");
                if(AssetDatabase.GetAssetPath(p.objectReferenceValue)!=LabArt+"/Pulse.prefab")continue;
                p.objectReferenceValue=ring;so.ApplyModifiedPropertiesWithoutUndo();changed=true;
            }
            if(changed)EditorSceneManager.SaveScene(scene);
            if(opened){if(previous.IsValid())SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scene,true);}
            AssetDatabase.SaveAssets();
        }
        private static void Set(UnityEngine.Object obj,string field,UnityEngine.Object value){var so=new SerializedObject(obj);so.FindProperty(field).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
        private static void Set(UnityEngine.Object obj,string field,UnityEngine.Object[] values){var so=new SerializedObject(obj);var p=so.FindProperty(field);p.arraySize=values.Length;for(int i=0;i<values.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=values[i];so.ApplyModifiedPropertiesWithoutUndo();}
        private static RectTransform Rect(string name,Transform parent,Vector2 anchor,Vector2 at,Vector2 size)
        {var rt=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rt.SetParent(parent,false);rt.anchorMin=rt.anchorMax=anchor;rt.pivot=new Vector2(.5f,.5f);rt.anchoredPosition=at;rt.sizeDelta=size;return rt;}
        private static GameObject Panel(string name,Transform parent,Vector2 anchor,Vector2 at,Vector2 size)
        {var rt=Rect(name,parent,anchor,at,size);rt.gameObject.AddComponent<Image>().color=_config.Panel;return rt.gameObject;}
        private static Text Text(string name,Transform parent,Vector2 at,Vector2 size,string text,int font=18,TextAnchor align=TextAnchor.UpperLeft)
        {var rt=Rect(name,parent,new Vector2(.5f,.5f),at,size);var t=rt.gameObject.AddComponent<Text>();t.font=_font;t.fontSize=font;t.color=new Color(.93f,.96f,.91f);t.text=text;t.alignment=align;t.raycastTarget=false;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;return t;}
        private static Button Button(string name,Transform parent,Vector2 at,Vector2 size,string text,out Text label)
        {var rt=Rect(name,parent,new Vector2(.5f,.5f),at,size);rt.gameObject.AddComponent<Image>().color=new Color(.19f,.34f,.32f);var b=rt.gameObject.AddComponent<Button>();label=Text("Label",rt,Vector2.zero,size-new Vector2(20,12),text,18,TextAnchor.MiddleCenter);return b;}
        private static ExpeditionHud BuildHud()
        {
            var canvas=new GameObject("Expedition UI",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1440,900);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            var hud=canvas.AddComponent<ExpeditionHud>();
            var game=Rect("Game",canvas.transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(1440,900));
            var top=Panel("Top",game,new Vector2(.5f,1),new Vector2(0,-48),new Vector2(1400,76));
            var status=Text("Status",top.transform,new Vector2(-150,0),new Vector2(1040,60),"",21,TextAnchor.MiddleLeft);
            var pause=Button("Pause",top.transform,new Vector2(465,0),new Vector2(140,48),"ESC · Duraklat",out _);
            var menuButton=Button("Menu",top.transform,new Vector2(615,0),new Vector2(130,48),"Menü",out _);
            var left=Panel("Build",game,new Vector2(0,.5f),new Vector2(131,0),new Vector2(230,500));var build=Text("Build text",left.transform,Vector2.zero,new Vector2(204,470),"",16);
            var right=Panel("Objective",game,new Vector2(1,1),new Vector2(-155,-182),new Vector2(280,160));var objective=Text("Objective text",right.transform,Vector2.zero,new Vector2(250,132),"",18);
            var bottom=Panel("Bottom",game,new Vector2(.5f,0),new Vector2(0,81),new Vector2(1400,140));
            var ground=Text("Ground",bottom.transform,new Vector2(-80,50),new Vector2(1200,26),"",16,TextAnchor.MiddleLeft);
            var hint=Text("Hint",bottom.transform,new Vector2(0,22),new Vector2(1360,28),"",17,TextAnchor.MiddleLeft);
            var skills=Text("Skills",bottom.transform,new Vector2(-135,-8),new Vector2(1080,24),"",14,TextAnchor.MiddleLeft);
            Text("Controls",bottom.transform,new Vector2(-410,-42),new Vector2(520,32),"WASD · Hareket    TIK · Odak    TEKER · Yakınlık",15,TextAnchor.MiddleLeft);
            var pull=Button("Pull",bottom.transform,new Vector2(40,-42),new Vector2(160,38),"Q · Çekim",out _);
            var burst=Button("Burst",bottom.transform,new Vector2(225,-42),new Vector2(180,38),"E · Patlama",out _);
            var dash=Button("Dash",bottom.transform,new Vector2(430,-42),new Vector2(200,38),"SPACE · Kaçış",out _);
            var choices=Panel("Choices",canvas.transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(1070,410));
            var choiceTitle=Text("Title",choices.transform,new Vector2(0,135),new Vector2(990,75),"",25,TextAnchor.MiddleCenter);
            var cards=new Button[3];var labels=new Text[3];for(int i=0;i<3;i++)cards[i]=Button("Choice "+i,choices.transform,new Vector2(-345+i*345,-30),new Vector2(325,230),"",out labels[i]);
            var camp=Panel("Camp",canvas.transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(880,390));
            var campLabel=Text("Camp text",camp.transform,new Vector2(0,45),new Vector2(810,260),"",22,TextAnchor.UpperCenter);
            var next=Button("Continue",camp.transform,new Vector2(0,-130),new Vector2(430,52),"ENTER · Sonraki alana ilerle",out _);
            var result=Panel("Result",canvas.transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(950,460));
            var resultLabel=Text("Result text",result.transform,new Vector2(0,65),new Vector2(880,280),"",23,TextAnchor.UpperCenter);
            var retry=Button("Retry",result.transform,new Vector2(0,-100),new Vector2(430,48),"Mabede dön · Güçleri koru",out _);
            var same=Button("Same seed",result.transform,new Vector2(-230,-175),new Vector2(420,50),"Aynı tohum · Baştan dene",out _);
            var another=Button("New seed",result.transform,new Vector2(230,-175),new Vector2(420,50),"Yeni tohum · Yeni sefer",out _);
            var menu=Panel("Menu",canvas.transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(1080,700));
            Text("Title",menu.transform,new Vector2(0,265),new Vector2(1000,80),"KAM / B · BÜYÜYEN SEFER",34,TextAnchor.MiddleCenter);
            Text("Intro",menu.transform,new Vector2(0,125),new Vector2(980,180),"Üç bağlı alan. Seçtiklerin sonraki savaşlara taşınır.\n\nSütun: siper · Çamur: yavaşlık · Su: elektrik · Rampa: menzil\nSavaş içinde üç güçten birini seçerken zaman durur.\nYeni tohum, farklı teklifler ve düşman bileşimi getirir.",21,TextAnchor.MiddleCenter);
            var newRun=Button("New run",menu.transform,new Vector2(-240,-35),new Vector2(440,68),"YENİ SEFER",out _);
            var load=Button("Load",menu.transform,new Vector2(240,-35),new Vector2(440,68),"MABET KAYDINDAN DEVAM",out _);
            var resume=Button("Resume",menu.transform,new Vector2(-240,-120),new Vector2(440,54),"Açık sefere dön",out _);
            var quit=Button("Quit",menu.transform,new Vector2(240,-120),new Vector2(440,54),"Çıkış",out _);
            var saveLabel=Text("Save",menu.transform,new Vector2(0,-210),new Vector2(980,60),"",18,TextAnchor.MiddleCenter);
            Text("Disclaimer",menu.transform,new Vector2(0,-280),new Vector2(980,70),"Oynanış prototipi · Final sanat / ekonomi değil.\nOtomatik kayıt yalnız temizlenen alan sonundaki mabette. Ana oyun kaydını kullanmaz.",16,TextAnchor.MiddleCenter);
            Set(hud,"_menu",menu);Set(hud,"_game",game.gameObject);Set(hud,"_choices",choices);Set(hud,"_camp",camp);Set(hud,"_result",result);
            Set(hud,"_status",status);Set(hud,"_build",build);Set(hud,"_objective",objective);Set(hud,"_ground",ground);Set(hud,"_hint",hint);Set(hud,"_skills",skills);
            Set(hud,"_choiceTitle",choiceTitle);Set(hud,"_choiceLabels",labels);Set(hud,"_resultLabel",resultLabel);Set(hud,"_campLabel",campLabel);Set(hud,"_saveLabel",saveLabel);
            Set(hud,"_newRun",newRun);Set(hud,"_load",load);Set(hud,"_quit",quit);Set(hud,"_resume",resume);Set(hud,"_menuButton",menuButton);Set(hud,"_pause",pause);Set(hud,"_pull",pull);Set(hud,"_burst",burst);Set(hud,"_dash",dash);
            Set(hud,"_cards",cards);Set(hud,"_continue",next);Set(hud,"_retry",retry);Set(hud,"_sameSeed",same);Set(hud,"_anotherSeed",another);
            game.gameObject.SetActive(false);choices.SetActive(false);camp.SetActive(false);result.SetActive(false);return hud;
        }
        public static void BuildWindows()
        {
            EnsureScene();UpgradePulseReference();CombatLabValidation.RunChecks();ExpeditionValidation.RunChecks();ExpeditionTacticsValidation.RunChecks();Directory.CreateDirectory("Builds/ExpeditionV2");
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Builds/ExpeditionV2/KamExpedition.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(result.summary.result!=BuildResult.Succeeded)throw new Exception("Expedition build failed: "+result.summary.result);
            Debug.Log("EXPEDITION_BUILD_OK "+result.summary.totalSize);
        }
    }
}
