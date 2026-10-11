using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TacticalRPG.Core;
using TacticalRPG.Data;
using TacticalRPG.Grid;
using Object=UnityEngine.Object;
namespace TacticalRPG.Editor
{
    [InitializeOnLoad] public static class RootRuptureQA
    {
        [Serializable] private class Report {public int checks;public List<string> failures=new();public List<string> notes=new();}
        private static Report _report=new();private static int _phase;private static double _next,_start;
        private static HexCoordinate _goal,_scar;private static int _before;private static HashSet<HexCoordinate> _originalScars;
        private static int _lightSteps;private static Light _sun;private static DayNightProfile _profile;
        private static float _lightBefore;private static Quaternion _rotationBefore;private static HexCell _fogSample;private static Color _fogBefore;
        private static float _noonLuminance;
        private static string Output=>Path.GetFullPath("RootRuptureQAResults");
        static RootRuptureQA(){if(SessionState.GetBool("RootRuptureQA.Play",false))EditorApplication.update+=Tick;}
        public static void Run()
        {
            if(!File.Exists(".biome-qa-copy"))throw new InvalidOperationException("Isolated copy required");
            Directory.CreateDirectory(Output);EditorSceneManager.OpenScene("Assets/Scenes/xd.unity");PresentationSetup.Install();
            PlayerSettings.productName="OYUN_PresentationQA";PresentationSetup.Set(Find<ChapterMapGenerator>(),"_fixedLayoutSeed",6118);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
            SessionState.SetBool("RootRuptureQA.Play",true);EditorApplication.isPlaying=true;
        }
        private static T Find<T>() where T:Component=>Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
        private static void Check(bool valid,string label){_report.checks++;if(!valid){_report.failures.Add(label);Debug.LogError("[RootRuptureQA] "+label);}}
        private static void Tick()
        {
            if(!EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.timeSinceStartup<_next)return;
            try
            {
                if(_start==0){_start=EditorApplication.timeSinceStartup;Wait(4);return;}
                if(EditorApplication.timeSinceStartup-_start>180)throw new TimeoutException("phase "+_phase);
                var roots=Find<BayterekQuestRoots>();var grid=Find<HexGridManager>();var nodes=Find<ChapterNodeManager>();var map=Find<ChapterMapGenerator>();var player=Find<PlayerController>();
                if(_phase==0)
                {
                    Economy();Find<FogOfWarManager>().SetFullReveal(true);
                    foreach(string name in new[]{"TacticalRPG/BayterekBark","TacticalRPG/RootEarth"}){var shader=Shader.Find(name);Check(shader!=null&&shader.isSupported&&!ShaderUtil.ShaderHasError(shader),"Shader invalid: "+name);}
                    Next(1);return;
                }
                if(_phase==1)
                {
                    if(nodes.Nodes.Any(n=>n.Type==MapNodeType.Mandatory&&roots.Growing(n.Coord))){Wait(.5);return;}
                    Check(roots.RupturedTileCount>5,"No fractured tile replacements");
                    var scars=Object.FindObjectsByType<RootRuptureTile>(FindObjectsSortMode.None).Where(s=>s.Amount>0).ToArray();
                    Check(scars.Length==roots.RupturedTileCount,"Ground pooling count mismatch");
                    foreach(var scar in scars)Check(grid.TryGetCell(scar.Coordinate,out var c)&&!c.Visual.activeSelf&&!c.IsWalkable,"Root did not replace/block original "+scar.Coordinate);
                    roots.TryGetTree(out var tree);Check(grid.Cells[tree].Visual.activeSelf,"Root replacement deleted Bayterek tree");
                    var segments=Object.FindObjectsByType<QuestRootSegment>(FindObjectsSortMode.None).Where(s=>s.GrowthAmount>.99f).ToArray();
                    Check(segments.Any(s=>Mathf.Min(s.CenterAt(0).y,s.CenterAt(1).y,s.CenterAt(.5f).y)<-.2f),"No submerged root sections");
                    Check(segments.Any(s=>Mathf.Max(s.CenterAt(0).y,s.CenterAt(1).y,s.CenterAt(.5f).y)>.9f),"No emerged root arches");
                    _scar=scars.OrderByDescending(s=>s.Coordinate.DistanceTo(tree)).First().Coordinate;
                    Capture("root_fracture_close",grid.Cells[_scar].WorldPosition,3.5f);Capture("root_landscape",tree.ToWorldPosition(grid.HexSize)+Vector3.up*2,14);
                    _originalScars=new HashSet<HexCoordinate>(scars.Select(s=>s.Coordinate));
                    _goal=grid.Cells.Where(kv=>kv.Value.IsWalkable&&map.IsReachable(kv.Key)&&nodes.NodeAt(kv.Key)==null&&TileCatalog.Get(map.TerrainIdAt(kv.Key))?.Family==TileFamily.Plain&&kv.Key.DistanceTo(player.CurrentCoord)>3).OrderByDescending(kv=>kv.Key.DistanceTo(tree)).First().Key;
                    nodes.SpawnMandatory(_goal,3);
                    var callback=(Action<int,HexCoordinate>)typeof(MandatoryQuestDirector).GetField("OnQuestUnlocked",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Find<MandatoryQuestDirector>());callback?.Invoke(3,_goal);
                    _before=roots.RupturedTileCount;Next(2.8);return;
                }
                if(_phase==2)
                {Check(QuestRootCinematic.IsPlaying,"New quest missing cinematic");Capture("root_eruption",roots.GrowthTip(_goal),5);Next(.5);return;}
                if(_phase==3)
                {
                    if(roots.Growing(_goal)||QuestRootCinematic.IsPlaying){Wait(.5);return;}
                    Check(roots.RupturedTileCount>=_before,"New root lost existing damage");
                    var terrain=grid.Cells.ToDictionary(kv=>kv.Key,kv=>kv.Value.TerrainWalkable);var blocked=new HashSet<HexCoordinate>(grid.Cells.Where(kv=>kv.Value.HasMovementBlock).Select(kv=>kv.Key));
                    var before=QuestRootPlanner.Flood(terrain,new HashSet<HexCoordinate>(),player.CurrentCoord);var after=QuestRootPlanner.Flood(terrain,blocked,player.CurrentCoord);
                    foreach(var node in nodes.Nodes)if(!node.Completed&&QuestRootPlanner.Accessible(before,node.Coord))Check(QuestRootPlanner.Accessible(after,node.Coord),"Rupture blocked objective "+node.Coord);
                    var collapse=Find<MapCollapseManager>();var cell=grid.Cells[_scar];
                    ((HashSet<HexCoordinate>)typeof(MapCollapseManager).GetField("_collapsed",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(collapse)).Add(_scar);
                    typeof(MapCollapseManager).GetMethod("RemoveTile",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(collapse,new object[]{cell});Next(.3);return;
                }
                if(_phase==4)
                {
                    var scar=Object.FindObjectsByType<RootRuptureTile>(FindObjectsSortMode.None).First(s=>s.Coordinate==_scar);
                    Check(!scar.GetComponent<MeshRenderer>().enabled,"Collapsed scar still visible");Check(Find<MapCollapseManager>().RestoreTile(_scar),"Rooted collapse restoration failed");
                    Check(!grid.Cells[_scar].Visual.activeSelf&&!grid.Cells[_scar].IsWalkable,"Restoration bypassed root visual ownership");
                    var mission=AssetDatabase.LoadAssetAtPath<MissionData>("Assets/Data/Missions/Pusu_UyananDev.asset");Find<GameStateManager>().RequestMission(mission);Find<GameStateManager>().ConfirmMission();Next(1);return;
                }
                if(_phase==5)
                {
                    Check(Object.FindObjectsByType<RootRuptureTile>(FindObjectsSortMode.None).Length==0,"Ground damage leaked into arena");
                    Check(grid.Cells.Values.All(c=>!c.HasMovementBlock),"Root movement blocks leaked into arena");Find<GameStateManager>().ReturnToOverworld();Next(1);return;
                }
                if(_phase==6)
                {
                    Check(_originalScars.All(c=>!grid.Cells[c].Visual.activeSelf&&grid.Cells[c].HasMovementBlock),"Ground replacements lost after arena");
                    grid.RegenerateCellVisual(_scar);Check(!grid.Cells[_scar].Visual.activeSelf,"Regenerated original bypassed suppression");
                    Find<MandatoryQuestDirector>().enabled=false;
                    var shared=nodes.Nodes.Where(n=>n.Type==MapNodeType.Mandatory&&!n.Completed).ToArray();
                    Complete(shared[0]);Next(7);return;
                }
                if(_phase==7)
                {
                    Check(roots.BranchCount>0&&roots.RupturedTileCount>0,"Completing one quest removed other roots");
                    foreach(var scar in Object.FindObjectsByType<RootRuptureTile>(FindObjectsSortMode.None).Where(s=>s.Amount>0))Check(!grid.Cells[scar.Coordinate].Visual.activeSelf,"Shared fracture restored too early");
                    foreach(var node in nodes.Nodes.Where(n=>n.Type==MapNodeType.Mandatory&&!n.Completed).ToArray())Complete(node);
                    Next(.1);return;
                }
                if(_phase==8){Check(roots.RupturedTileCount>0,"Retraction instantly removed all ground damage");Next(10);return;}
                if(_phase==9)
                {
                    Check(roots.RupturedTileCount==0&&roots.BranchCount==0,"Completed roots left ground replacements");
                    Check(_originalScars.All(c=>grid.Cells[c].Visual.activeSelf&&!grid.Cells[c].HasMovementBlock),"Retraction did not restore original tiles");
                    Capture("root_ground_healed",grid.Cells[_scar].WorldPosition,3.5f);
                    var ap=Find<ActionPointManager>();Check(ap.SlotsPerDay==6&&ap.DayTimeSlots==4,"Day/night is not four plus two slots");
                    for(int slot=0;slot<6;slot++)Check(ap.IsNightSlot(slot)==(slot>=4),"Wrong day/night slot "+slot);
                    var serial=new SerializedObject(Find<DayNightCycle>());_sun=(Light)serial.FindProperty("_sun").objectReferenceValue;_profile=(DayNightProfile)serial.FindProperty("_profile").objectReferenceValue;
                    Check(Find<DayNightCycle>().TransitionSeconds>=2,"Lighting transition too short");
                    _fogSample=grid.Cells.Values.First(c=>!Find<FogOfWarManager>().IsPermanentlyRevealed(c.Coordinate)&&c.Coordinate.DistanceTo(player.CurrentCoord)>15);
                    BeginLightStep();_phase=10;Wait(.7);return;
                }
                if(_phase==10)
                {
                    var target=_profile.GetSlot(Find<ActionPointManager>().CurrentSlot);
                    Check(Find<DayNightCycle>().IsTransitioning,"Light changed instantly");
                    Check(Mathf.Abs(_sun.intensity-_lightBefore)>.001f&&Mathf.Abs(_sun.intensity-target.sunIntensity)>.001f,"Light has no intermediate values");
                    Check((-_sun.transform.forward).y>0,"Sun went below horizon during transition");
                    if(Find<ActionPointManager>().CurrentSlot==4)
                    {
                        var tint=Tint(_fogSample);Check(tint.r<_fogBefore.r&&tint.r>_fogBefore.r*.08f,"Fog tile brightness did not fade");
                    }
                    _phase=11;Wait(2.1);return;
                }
                if(_phase==11)
                {
                    var target=_profile.GetSlot(Find<ActionPointManager>().CurrentSlot);
                    Check(!Find<DayNightCycle>().IsTransitioning&&Mathf.Abs(_sun.intensity-target.sunIntensity)<.001f,"Light did not settle");
                    Check(Quaternion.Angle(_sun.transform.rotation,Quaternion.Euler(target.sunEuler))<.1f,"Sun angle did not settle");
                    Check(Vector4.Distance(RenderSettings.ambientLight,target.ambientColor)<.001f,"Ambient did not settle");
                    Check(Find<FogOfWarManager>().IsNightMode==Find<ActionPointManager>().IsNight,"Fog and time disagree");
                    Check(RenderSettings.sun==_sun,"URP main sun changed during dimming");
                    if(Find<ActionPointManager>().CurrentSlot==1)_noonLuminance=ProbeLuminance();
                    if(Find<ActionPointManager>().CurrentSlot==5)
                    {
                        float night=ProbeLuminance();Check(night<_noonLuminance*.8f&&night>.06f,"Rendered night is not distinct/readable");
                        _report.notes.Add("Rendered neutral surface luminance: noon="+_noonLuminance.ToString("F3")+", midnight="+night.ToString("F3"));
                    }
                    Capture("lighting_slot_"+Find<ActionPointManager>().CurrentSlot,player.transform.position,11);
                    if(++_lightSteps<6){BeginLightStep();_phase=10;Wait(.7);return;}
                    Find<DayNightCycle>().ApplySlot(5,false);Next(.6);return;
                }
                if(_phase==12)
                {
                    float intensity=_sun.intensity;var rotation=_sun.transform.rotation;Find<DayNightCycle>().ApplySlot(2,false);
                    Check(Mathf.Abs(intensity-_sun.intensity)<.0001f&&Quaternion.Angle(rotation,_sun.transform.rotation)<.01f,"Interrupted transition snapped");Next(2.7);return;
                }
                if(_phase==13)
                {
                    Check(Mathf.Abs(_sun.intensity-_profile.GetSlot(2).sunIntensity)<.001f,"Retargeted transition missed target");
                    Find<DayNightCycle>().ApplySlot(5,false);Next(.5);return;
                }
                if(_phase==14){_lightBefore=_sun.intensity;Find<DayNightCycle>().enabled=false;Next(.5);return;}
                if(_phase==15)
                {
                    Check(Mathf.Abs(_sun.intensity-_lightBefore)<.0001f,"Disabled cycle kept animating");Find<DayNightCycle>().enabled=true;
                    Check(Mathf.Abs(_sun.intensity-_lightBefore)<.0001f,"Re-enabled cycle snapped");Next(2.7);return;
                }
                if(_phase==16)
                {
                    Check(Mathf.Abs(_sun.intensity-_profile.GetSlot(Find<ActionPointManager>().CurrentSlot).sunIntensity)<.001f,"Re-enabled cycle missed actual AP slot");
                    _report.notes.Add("All six actual AP slot transitions sampled before/during/after; four daytime/two nighttime; fog brightness, retarget, disable/resume checked.");Finish();
                }
            }
            catch(Exception e){_report.failures.Add(e.ToString());Debug.LogException(e);Finish();}
        }
        private static void Complete(ChapterNodeManager.MapNode node)=>typeof(ChapterNodeManager).GetMethod("Complete",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(Find<ChapterNodeManager>(),new object[]{node});
        private static Color Tint(HexCell cell){var block=new MaterialPropertyBlock();cell.MeshRenderer.GetPropertyBlock(block);return block.GetColor("_BaseColor");}
        private static float ProbeLuminance()
        {
            var go=new GameObject("Lighting probe surface",typeof(MeshFilter),typeof(MeshRenderer));go.layer=30;
            var mesh=HexMetrics.CreateHexMesh();go.GetComponent<MeshFilter>().sharedMesh=mesh;
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.SetColor("_BaseColor",Color.gray);go.GetComponent<MeshRenderer>().sharedMaterial=material;
            go.transform.position=new Vector3(0,100,0);var cameraGO=new GameObject("Lighting probe camera");var camera=cameraGO.AddComponent<Camera>();camera.cullingMask=1<<30;camera.orthographic=true;camera.orthographicSize=.5f;camera.transform.position=new Vector3(0,105,0);camera.transform.rotation=Quaternion.Euler(90,0,0);
            var rt=new RenderTexture(32,32,16);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var texture=new Texture2D(32,32,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,32,32),0,0);texture.Apply();var c=texture.GetPixel(16,16);
            float result=c.r*.2126f+c.g*.7152f+c.b*.0722f;RenderTexture.active=null;camera.targetTexture=null;
            Object.DestroyImmediate(texture);Object.DestroyImmediate(rt);Object.DestroyImmediate(cameraGO);Object.DestroyImmediate(go);Object.DestroyImmediate(mesh);Object.DestroyImmediate(material);return result;
        }
        private static void BeginLightStep()
        {
            _lightBefore=_sun.intensity;_rotationBefore=_sun.transform.rotation;_fogBefore=Tint(_fogSample);
            var ap=Find<ActionPointManager>();ap.SpendAP(ap.CurrentAP);
            Check(Mathf.Abs(_sun.intensity-_lightBefore)<.0001f&&Quaternion.Angle(_sun.transform.rotation,_rotationBefore)<.01f,"AP event snapped sun");
            Check(Vector4.Distance(_fogBefore,Tint(_fogSample))<.0001f,"AP event snapped fog brightness");
        }
        private static void Economy()
        {
            var wallet=Find<EssenceWallet>();var inventory=Find<Inventory>();var buffs=Find<PlayerBuffs>();
            Check(wallet.Unlimited&&inventory.UnlimitedConsumables&&buffs.UnlimitedTravelTokens,"Unlimited economy not wired");
            var testing=Object.Instantiate(AssetDatabase.LoadAssetAtPath<TestEconomySO>("Assets/Data/Config/TestEconomy.asset"));
            PresentationSetup.Set(wallet,"_testEconomy",testing);PresentationSetup.Set(inventory,"_testEconomy",testing);PresentationSetup.Set(buffs,"_testEconomy",testing);
            var types=(EssenceType[])Enum.GetValues(typeof(EssenceType));wallet.ClearTypes(types);int gained=wallet.TotalGained;
            var huge=types.Select(t=>new EssenceAmount(t,int.MaxValue)).ToArray();
            for(int n=0;n<50;n++)Check(wallet.TrySpend(huge),"Unlimited purchase refused");
            Check(wallet.Total==0&&wallet.TotalGained==gained,"Testing generated fake quest income");
            foreach(var type in types)Check(wallet.DisplayAmount(type)=="Sınırsız","Wrong unlimited UI for "+type);
            var store=Find<StoreManager>();Check(store.TryReroll(wallet)&&store.Stock.Count>0,"Free store reroll failed");Check(store.TryBuy(0,wallet,inventory),"Free store purchase failed");
            var recipe=AssetDatabase.FindAssets("t:UnitRecipe").Select(g=>AssetDatabase.LoadAssetAtPath<UnitRecipe>(AssetDatabase.GUIDToAssetPath(g))).First(r=>r.UnitClass!=null);
            Check(Find<PartyManager>().TryCreate(recipe),"Free production failed");var card=Find<PartyManager>().Party.Last();Check(Find<PartyManager>().TryLevelUp(card),"Free unit level failed");
            var potion=AssetDatabase.FindAssets("t:PotionSO").Select(g=>AssetDatabase.LoadAssetAtPath<PotionSO>(AssetDatabase.GUIDToAssetPath(g))).First();inventory.AddPotion(potion);int potions=inventory.PotionCount(potion);
            for(int n=0;n<30;n++)Check(inventory.TryConsumePotion(potion),"Unlimited potion refused");
            Check(inventory.PotionCount(potion)==potions,"Unlimited potion consumed inventory");Check(buffs.TrySpendStones(int.MaxValue),"Unlimited travel failed");
            PresentationSetup.Set(testing,"_unlimited",false);Check(!wallet.CanAfford(huge)&&!wallet.TrySpend(huge),"Normal economy not restored after toggle");
            wallet.Gain(EssenceType.Tas,7);Check(wallet.TotalGained==gained+7,"Real collected income no longer tracked");Check(wallet.TrySpend(new[]{new EssenceAmount(EssenceType.Tas,3)})&&wallet.Get(EssenceType.Tas)==4,"Normal payment failed");
            inventory.TryConsumePotion(potion);Check(inventory.PotionCount(potion)==potions-1,"Normal potion consumption failed");
            Check(!buffs.TrySpendStones(int.MaxValue),"Normal travel economy not restored");
            PresentationSetup.Set(testing,"_unlimited",true);_report.notes.Add("Five currencies, repeated huge purchases, stock reroll/purchase, production, level, potion/travel, real income and normal mode tested.");
        }
        private static void Capture(string name,Vector3 focus,float size)
        {
            var go=new GameObject("Root QA camera");var camera=go.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=size;camera.farClipPlane=300;camera.transform.rotation=Quaternion.Euler(53,-30,0);camera.transform.position=focus-camera.transform.forward*80;
            var rt=new RenderTexture(1600,1000,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var texture=new Texture2D(1600,1000,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1600,1000),0,0);texture.Apply();File.WriteAllBytes(Output+"/"+name+".png",texture.EncodeToPNG());
            RenderTexture.active=null;camera.targetTexture=null;Object.DestroyImmediate(texture);Object.DestroyImmediate(rt);Object.DestroyImmediate(go);
        }
        private static void Wait(double seconds)=>_next=EditorApplication.timeSinceStartup+seconds;
        private static void Next(double seconds){_phase++;Wait(seconds);}
        private static void Finish(){Directory.CreateDirectory(Output);File.WriteAllText(Output+"/report.json",JsonUtility.ToJson(_report,true));SessionState.SetBool("RootRuptureQA.Play",false);EditorApplication.update-=Tick;EditorApplication.Exit(_report.failures.Count==0?0:1);}
    }
}
