using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TacticalRPG.Core;
using TacticalRPG.Data;
using TacticalRPG.Grid;
using TacticalRPG.UI;
using Object=UnityEngine.Object;
namespace TacticalRPG.Editor
{
    [InitializeOnLoad] public static class PresentationQA
    {
        [Serializable] private class Report{public int checks;public List<string> failures=new();public List<string> notes=new();}
        private static Report _report=new();private static int _phase;private static double _next,_start;
        private static string Output=>Path.GetFullPath("PresentationQAResults");
        private static HexCoordinate _quest,_collapsed;private static Vector3 _cameraBefore;private static float _sizeBefore;
        private static int _explored;private static bool _lockedBefore;
        static PresentationQA(){if(SessionState.GetBool("PresentationQA.Play",false))EditorApplication.update+=Tick;}
        public static void Run()
        {
            if(!File.Exists(".biome-qa-copy"))throw new InvalidOperationException("Isolated copy required");
            Directory.CreateDirectory(Output);EditorSceneManager.OpenScene("Assets/Scenes/xd.unity");
            PresentationSetup.Install();
            PlayerSettings.productName="OYUN_PresentationQA";
            PresentationSetup.Set(Find<ChapterMapGenerator>(),"_fixedLayoutSeed",6118);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
            // Export only presentation assets and a reviewable scene; never overwrite source gameplay data.
            File.WriteAllText(Path.Combine(Output,"installed.txt"),"Presentation installed");
            SessionState.SetBool("PresentationQA.Play",true);EditorApplication.isPlaying=true;
        }
        private static T Find<T>() where T:Component=>Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
        private static void Check(bool value,string message){_report.checks++;if(!value){_report.failures.Add(message);Debug.LogError("[PresentationQA] "+message);}}
        private static void Tick()
        {
            if(!EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.timeSinceStartup<_next)return;
            try
            {
                if(_start==0){_start=EditorApplication.timeSinceStartup;Wait(3);return;}
                if(EditorApplication.timeSinceStartup-_start>150)throw new TimeoutException("phase "+_phase);
                var roots=Find<BayterekQuestRoots>();var grid=Find<HexGridManager>();var map=Find<ChapterMapGenerator>();var nodes=Find<ChapterNodeManager>();var fog=Find<FogOfWarManager>();var player=Find<PlayerController>();var nav=Find<MenuNavigator>();var audio=Find<GameAudio>();
                if(_phase==0)
                {
                    Check(audio.Library!=null&&audio.Library.Cues.Length>=71,"Missing sound library");
                    foreach(var cue in audio.Library.Cues)Check(cue.variants.Length==3&&cue.variants.All(c=>c!=null),"Missing cue variants: "+cue.id);
                    Check(audio.Library.Themes.Length==10&&audio.Library.Themes.All(t=>t.music!=null&&t.ambience!=null),"Music/ambience theme missing");
                    foreach(string shader in new[]{"TacticalRPG/WorldAtmosphere","TacticalRPG/BayterekBark"}){var sh=Shader.Find(shader);Check(sh!=null&&sh.isSupported&&!ShaderUtil.ShaderHasError(sh),"Shader error: "+shader);}
                    int count=audio.PlayedCount;SoundSignals.Play("ui_click");Check(audio.PlayedCount>count,"Sound event did not reach audio");
                    audio.SetMusic(.23f);audio.SetAmbience(.31f);audio.SetInterface(.39f);Check(Mathf.Approximately(audio.Music,.23f)&&Mathf.Approximately(audio.Ambience,.31f),"Live audio settings failed");
                    ScreenCapture.CaptureScreenshot(Path.Combine(Output,"hud_actual.png"));Capture("world_fog",false);nav.OpenScreen(MenuScreen.Settings);Next(1);return;
                }
                if(_phase==1)
                {
                    var settings=Find<SettingsController>();var sliders=settings.GetComponentsInChildren<Slider>(true);Check(sliders.Length==8,"Settings should have eight sliders");
                    foreach(var slider in sliders)
                    {
                        var rt=(RectTransform)slider.transform;var settingsScroll=slider.GetComponentInParent<ScrollRect>();
                        if(settingsScroll!=null){float max=Mathf.Max(0,settingsScroll.content.rect.height-settingsScroll.viewport.rect.height);settingsScroll.content.anchoredPosition=new Vector2(0,Mathf.Clamp(-rt.anchoredPosition.y-settingsScroll.viewport.rect.height*.5f,0,max));Canvas.ForceUpdateCanvases();}
                        var canvas=slider.GetComponentInParent<Canvas>();
                        Vector3 world=rt.TransformPoint(new Vector3(rt.rect.xMin+rt.rect.width*.72f,rt.rect.center.y,0));
                        Vector2 screen=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,world);
                        var e=new PointerEventData(EventSystem.current){position=screen,button=PointerEventData.InputButton.Left};
                        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&(hits[0].gameObject==slider.gameObject||hits[0].gameObject.transform.IsChildOf(slider.transform)),"Slider raycast blocked: "+slider.name);
                        ExecuteEvents.Execute(slider.gameObject,e,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(slider.gameObject,e,ExecuteEvents.dragHandler);ExecuteEvents.Execute(slider.gameObject,e,ExecuteEvents.pointerUpHandler);
                        Check(slider.normalizedValue>.5f&&slider.normalizedValue<.95f,"Pointer drag did not set slider: "+slider.name);
                    }
                    Check(Mathf.Abs(audio.Master-.72f)<.06f&&Mathf.Abs(audio.Interface-.72f)<.06f,"Slider did not update audio model");
                    Find<DisplaySettings>().SetBrightness(1);nav.CloseScreen();nav.OpenScreen(MenuScreen.Settings);settings.GetComponentInChildren<ScrollRect>().verticalNormalizedPosition=1;
                    Capture("settings_top",true);var scroll=settings.GetComponentInChildren<ScrollRect>();scroll.verticalNormalizedPosition=0;Next(.5);return;
                }
                if(_phase==2){Capture("settings_bottom",true);nav.OpenScreen(MenuScreen.Book);Next(.5);return;}
                if(_phase==3){Capture("book",true);var pager=Find<CharacterBookView>().GetComponentInParent<BookmarkPager>();if(pager!=null){pager.Show(1);Capture("book_skills",true);pager.Show(2);Capture("book_tiles",true);pager.Show(0);}nav.OpenScreen(MenuScreen.Bag);Next(.5);return;}
                if(_phase==4){Capture("bag",true);var pager=Find<BagInventoryView>().GetComponentInParent<BookmarkPager>();if(pager!=null){pager.Show(1);Capture("bag_kam",true);pager.Show(2);Capture("bag_potions",true);pager.Show(0);}nav.OpenScreen(MenuScreen.Map);Next(.5);return;}
                if(_phase==5)
                {
                    Capture("map",true);nav.CloseScreen();
                    roots.TryGetTree(out var tree);
                    _quest=grid.Cells.Where(kv=>kv.Value.IsWalkable&&map.IsReachable(kv.Key)&&nodes.NodeAt(kv.Key)==null&&TileCatalog.Get(map.TerrainIdAt(kv.Key))?.Family==TileFamily.Plain&&kv.Key.DistanceTo(player.CurrentCoord)>3).OrderByDescending(kv=>kv.Key.DistanceTo(tree)).First().Key;
                    _cameraBefore=Camera.main.transform.position;_sizeBefore=Camera.main.orthographicSize;_lockedBefore=player.IsMovementLocked;_explored=fog.ExplorationVersion;
                    // Same public spawn + director event ordering as a newly unlocked mandatory quest.
                    nodes.SpawnMandatory(_quest,3);
                    var callback=(Action<int,HexCoordinate>)typeof(MandatoryQuestDirector).GetField("OnQuestUnlocked",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Find<MandatoryQuestDirector>());callback?.Invoke(3,_quest);
                    Next(2.2);return;
                }
                if(_phase==6)
                {
                    Check(QuestRootCinematic.IsPlaying,"New quest did not start cinematic");Check(player.IsMovementLocked,"Cinematic did not own movement");
                    var miniature=Find<MiniatureEffect>();Check(miniature==null||(float)typeof(MiniatureEffect).GetField("_weight",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(miniature)<.01f,"Cinematic obscured by miniature blur");
                    var beaconRoot=(Transform)typeof(MandatoryQuestBeacon).GetField("_root",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Find<MandatoryQuestBeacon>());Check(beaconRoot!=null&&!beaconRoot.gameObject.activeInHierarchy,"Beacon obscures cinematic");
                    var cinematicSO=new SerializedObject(Find<QuestRootCinematic>());Check(EditorUtility.IsPersistent(cinematicSO.FindProperty("_dustPrefab").objectReferenceValue),"Cinematic dust must reference editable prefab asset");
                    Capture("root_establish",false);Next(2.5);return;
                }
                if(_phase==7){Capture("root_growth",false);Next(17);return;}
                if(_phase==8)
                {
                    Check(!QuestRootCinematic.IsPlaying&&Find<QuestRootCinematic>().CompletedCount>0,"Cinematic did not finish");Check(player.IsMovementLocked==_lockedBefore,"Cinematic left movement locked");
                    Check(Find<CameraFollow>().enabled&&Find<CameraZoomSettings>().enabled,"Camera controllers not restored");Check(Mathf.Abs(Camera.main.orthographicSize-_sizeBefore)<.05f,"Zoom not restored");Check(fog.ExplorationVersion==_explored,"Cinematic permanently explored the map");
                    fog.SetFullReveal(true);Next(1);return;
                }
                if(_phase==9)
                {
                    var terrain=grid.Cells.ToDictionary(kv=>kv.Key,kv=>kv.Value.TerrainWalkable);var blocked=new HashSet<HexCoordinate>(grid.Cells.Where(kv=>kv.Value.HasMovementBlock).Select(kv=>kv.Key));
                    var before=QuestRootPlanner.Flood(terrain,new HashSet<HexCoordinate>(),player.CurrentCoord);var after=QuestRootPlanner.Flood(terrain,blocked,player.CurrentCoord);
                    foreach(var node in nodes.Nodes)if(!node.Completed&&QuestRootPlanner.Accessible(before,node.Coord))Check(QuestRootPlanner.Accessible(after,node.Coord),"Wide roots blocked objective "+node.Coord);
                    roots.TryGetTree(out var tree);CaptureWorldAt("tree_roots",tree.ToWorldPosition(grid.HexSize)+Vector3.up*3,13);
                    var cell=grid.Cells.Values.First(c=>c.TerrainWalkable&&!c.HasMovementBlock&&nodes.NodeAt(c.Coordinate)==null&&c.Coordinate.DistanceTo(player.CurrentCoord)>3);_collapsed=cell.Coordinate;
                    var collapse=Find<MapCollapseManager>();((HashSet<HexCoordinate>)typeof(MapCollapseManager).GetField("_collapsed",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(collapse)).Add(_collapsed);
                    typeof(MapCollapseManager).GetMethod("RemoveTile",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(collapse,new object[]{cell});Next(.5);return;
                }
                if(_phase==10){Check(grid.TryGetCell(_collapsed,out var cell)&&!cell.IsWalkable&&!cell.Visual.activeSelf,"Collapsed terrain remains visible/walkable");CaptureWorldAt("void_open",_collapsed.ToWorldPosition(grid.HexSize),3);Find<CollapseWaveEffect>().Play(_collapsed.ToWorldPosition(grid.HexSize),0);Next(.8);return;}
                if(_phase==11){CaptureWorldAt("earthquake",_collapsed.ToWorldPosition(grid.HexSize),12);Check(Find<MapCollapseManager>().RestoreTile(_collapsed),"Void restoration failed");Next(.5);return;}
                if(_phase==12)
                {
                    Check(grid.TryGetCell(_collapsed,out var cell)&&cell.IsWalkable&&cell.Visual.activeSelf,"Restored void did not return terrain");
                    var mission=AssetDatabase.LoadAssetAtPath<MissionData>("Assets/Data/Missions/Pusu_UyananDev.asset");Find<GameStateManager>().RequestMission(mission);Find<GameStateManager>().ConfirmMission();Next(2);return;
                }
                if(_phase==13){Check(!fog.FogEnabled,"Fog leaked into combat arena");Check(audio.CurrentTheme=="combat","Combat score not selected");Capture("deployment",true);Find<GameStateManager>().ReturnToOverworld();Next(2);return;}
                if(_phase==14)
                {
                    Check(audio.CurrentTheme!="combat","Exploration music did not return");Check(grid.Cells.Values.Any(c=>c.HasMovementBlock),"Roots not restored after arena");
                    _quest=grid.Cells.First(kv=>kv.Value.IsWalkable&&map.IsReachable(kv.Key)&&nodes.NodeAt(kv.Key)==null&&TileCatalog.Get(map.TerrainIdAt(kv.Key))?.Family==TileFamily.Plain&&kv.Key.DistanceTo(player.CurrentCoord)>3).Key;
                    nodes.SpawnMandatory(_quest,4);var callback=(Action<int,HexCoordinate>)typeof(MandatoryQuestDirector).GetField("OnQuestUnlocked",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Find<MandatoryQuestDirector>());callback?.Invoke(4,_quest);Next(.6);return;
                }
                if(_phase==15)
                {
                    var cinematic=Find<QuestRootCinematic>();Check(QuestRootCinematic.IsPlaying,"Interruption test did not start cinematic");cinematic.enabled=false;
                    Check(!QuestRootCinematic.IsPlaying&&!player.IsMovementLocked,"Disabled cinematic kept movement lease");Check(Find<CameraFollow>().enabled&&Find<CameraZoomSettings>().enabled,"Disabled cinematic kept camera lease");Check(nav.GetComponent<Canvas>().enabled,"Disabled cinematic kept menu hidden");
                    cinematic.enabled=true;Next(.5);return;
                }
                if(_phase==16){var beaconRoot=(Transform)typeof(MandatoryQuestBeacon).GetField("_root",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Find<MandatoryQuestBeacon>());Check(beaconRoot!=null&&beaconRoot.gameObject.activeInHierarchy,"Beacons not restored after cinematic interruption");Finish();}
            }
            catch(Exception e){_report.failures.Add(e.ToString());Debug.LogException(e);Finish();}
        }
        private static void Wait(double seconds)=>_next=EditorApplication.timeSinceStartup+seconds;
        private static void Next(double seconds){_phase++;Wait(seconds);}
        private static void CaptureWorldAt(string name,Vector3 target,float size)
        {var cam=Camera.main;var pos=cam.transform.position;var rot=cam.transform.rotation;float old=cam.orthographicSize;cam.transform.rotation=Quaternion.Euler(58,-30,0);cam.transform.position=target-cam.transform.forward*90;cam.orthographicSize=size;Capture(name,false);cam.transform.SetPositionAndRotation(pos,rot);cam.orthographicSize=old;}
        private static void Capture(string name,bool ui)
        {
            var cam=Camera.main;var miniature=Find<MiniatureEffect>();bool mini=miniature!=null&&miniature.enabled;if(mini)miniature.enabled=false;
            var canvases=Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);var modes=new RenderMode[canvases.Length];var cameras=new Camera[canvases.Length];var distances=new float[canvases.Length];
            for(int i=0;i<canvases.Length;i++){modes[i]=canvases[i].renderMode;cameras[i]=canvases[i].worldCamera;distances[i]=canvases[i].planeDistance;if(ui){canvases[i].renderMode=RenderMode.ScreenSpaceCamera;canvases[i].worldCamera=cam;canvases[i].planeDistance=1;}}
            var layerRestore=new Dictionary<GameObject,int>();int mask=cam.cullingMask;CameraClearFlags clear=cam.clearFlags;Color background=cam.backgroundColor;
            if(ui){foreach(var canvas in canvases)foreach(var t in canvas.GetComponentsInChildren<Transform>(true)){if(!layerRestore.ContainsKey(t.gameObject))layerRestore[t.gameObject]=t.gameObject.layer;t.gameObject.layer=31;}cam.cullingMask=1<<31;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.035f,.052f,.063f);}
            var rt=new RenderTexture(1920,1080,24);cam.targetTexture=rt;Canvas.ForceUpdateCanvases();cam.Render();RenderTexture.active=rt;
            var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(Output,name+".png"),tex.EncodeToPNG());RenderTexture.active=null;cam.targetTexture=null;Object.DestroyImmediate(tex);Object.DestroyImmediate(rt);
            for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=modes[i];canvases[i].worldCamera=cameras[i];canvases[i].planeDistance=distances[i];}
            foreach(var kv in layerRestore)kv.Key.layer=kv.Value;cam.cullingMask=mask;cam.clearFlags=clear;cam.backgroundColor=background;
            if(mini)miniature.enabled=true;
        }
        private static void Finish(){Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"report.json"),JsonUtility.ToJson(_report,true));SessionState.SetBool("PresentationQA.Play",false);EditorApplication.update-=Tick;EditorApplication.Exit(_report.failures.Count==0?0:1);}
    }
}
