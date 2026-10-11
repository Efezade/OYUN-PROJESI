using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TacticalRPG.Core;
using TacticalRPG.Data;
using TacticalRPG.Grid;
using Object = UnityEngine.Object;

namespace TacticalRPG.Editor
{
    /// <summary>Opt-in integration checks. Run only in an isolated project copy.</summary>
    [InitializeOnLoad]
    public static class BiomeWorldQA
    {
        [Serializable] private class Report
        {
            public int maps, routes, assets, assertions;
            public List<string> failures = new();
            public List<string> notes = new();
        }
        private static Report _report = new();
        private static string Output => Path.GetFullPath("BiomeQAResults");
        private static double _next;
        private static int _phase;
        private static double _playStarted;
        private static BayterekQuestRoots _roots;
        private static ChapterNodeManager _nodes;
        private static HexGridManager _grid;
        private static ChapterMapGenerator _map;
        private static PlayerController _player;
        private static GameStateManager _state;
        private static ChapterNodeManager.MapNode _first;
        private static HashSet<HexCoordinate> _initialBlocks;
        private static int _growthStartBlocks, _fogEdges;
        private static float _newGrowthProgress;
        static BiomeWorldQA()
        {
            if (SessionState.GetBool("BiomeQA.Play", false)) EditorApplication.update += Tick;
        }
        public static void Run()
        {
            if (!File.Exists(".biome-qa-copy")) throw new InvalidOperationException("Biome QA runs only in an explicitly marked isolated copy.");
            Directory.CreateDirectory(Output); _report = new Report();
            try
            {
                BiomeArtSetup.BatchInstall();
                PresentationSetup.Install();
                PlayerSettings.productName = "OYUN_BiomeQA";
                ValidateAssets(); ValidateRoutes();
                File.WriteAllText(Path.Combine(Output,"data.json"),JsonUtility.ToJson(_report,true));
                if (_report.failures.Count > 0) { EditorApplication.Exit(1); return; }
                var config = AssetDatabase.LoadAssetAtPath<QuestRootConfigSO>(BiomeArtSetup.ConfigPath);
                var so = new SerializedObject(config);
                so.FindProperty("_growSecondsPerTile").floatValue = .04f;
                so.FindProperty("_retractSecondsPerTile").floatValue = .04f;
                so.ApplyModifiedPropertiesWithoutUndo(); // test copy only, never copied to source project
                var generator = Object.FindFirstObjectByType<ChapterMapGenerator>();
                var gs = new SerializedObject(generator); gs.FindProperty("_fixedLayoutSeed").intValue = 6118;
                gs.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                SessionState.SetBool("BiomeQA.Play",true);
                EditorApplication.isPlaying = true;
            }
            catch(Exception e) { Debug.LogException(e); _report.failures.Add(e.ToString()); Finish(false); }
        }
        private static void Check(bool condition, string message)
        { _report.assertions++; if (!condition) { _report.failures.Add(message); Debug.LogError("[BiomeQA] " + message); } }
        private static void ValidateAssets()
        {
            var palette = AssetDatabase.LoadAssetAtPath<TilePaletteSO>("Assets/Data/Map/TilePalette.asset");
            foreach(var tile in TileCatalog.All)
            {
                if(tile.Family==TileFamily.Void)continue;
                var entry=palette.GetById(tile.Id);
                Check(entry?.prefab!=null,"Catalogue tile still has no model: "+tile.Id);
                if(entry?.prefab!=null)
                    Check(!AssetDatabase.GetAssetPath(entry.prefab).StartsWith("Assets/Prefabs/Grid/Terrain/"),"Old placeholder remains: "+tile.Id);
            }
            foreach(string id in new[]{"kule","magaza","magara","kamp","gorev_alani","gorev_tamam"})
            {
                var entry=palette.GetById(id);
                Check(entry?.prefab!=null && AssetDatabase.GetAssetPath(entry.prefab).StartsWith(BiomeArtSetup.Prefabs+"/"),"Node model missing: "+id);
                if(entry?.prefab==null)continue;
                Check(entry.prefab.GetComponentsInChildren<Renderer>().Length>0,"Empty node model: "+id);
            }
            var style=AssetDatabase.LoadAssetAtPath<BiomeTerrainStyleSO>(BiomeArtSetup.TerrainStylePath);
            Check(style!=null && style.Shader!=null && style.Shader.isSupported,"Biome blend shader unavailable");
            foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{BiomeArtSetup.Prefabs}))
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                _report.assets++;
                var meshes=prefab.GetComponentsInChildren<MeshFilter>();
                Check(meshes.Length>0,"Empty model: "+prefab.name);
                foreach(var filter in meshes)
                {
                    var mesh=filter.sharedMesh;
                    Check(mesh!=null && mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Color),"Biome colour mask missing: "+prefab.name);
                    var renderer=filter.GetComponent<MeshRenderer>();
                    Check(renderer!=null && renderer.sharedMaterials.All(m=>m!=null && m.mainTexture!=null),"Model texture missing: "+prefab.name);
                }
            }
            Check(palette.GetById("magaza").isStore && !palette.GetById("magaza").isWalkable,"Market interaction flags changed");
            Check(palette.GetById("gorev_alani").canEnterCombat && !palette.GetById("gorev_tamam").canEnterCombat,"Mission interaction flags changed");
            Check(palette.GetById("kopru").surfaceHeightOverride>.5f,"Bridge standing height incorrect");
            foreach (string id in new[] { "solgun_cayir","bataklik","kok_agi","halka_zemini","halka_meclisi",TileCatalog.YosunTarla,"kok_sirti","orman","bayterek","dusmus_dev","agac_kovugu","ruh_tapinagi" })
            {
                var e = palette.GetById(id); Check(e?.prefab != null, "Missing prefab: "+id);
                if (e?.prefab == null) continue;
                _report.assets++;
                var obj = Object.Instantiate(e.prefab);
                var rs = obj.GetComponentsInChildren<Renderer>();
                Check(rs.Length>0,"Prefab has no renderers: "+id);
                if(rs.Length==0){Object.DestroyImmediate(obj);continue;}
                Bounds bounds=rs[0].bounds;
                foreach(var r in rs) { bounds.Encapsulate(r.bounds); foreach(var m in r.sharedMaterials) Check(m != null && m.mainTexture != null,"Missing material/texture: "+id); }
                if(id=="bayterek") Check(bounds.size.y > 10 && bounds.size.x > 6,"Bayterek was fitted to one tile");
                else if(id=="halka_meclisi") Check(bounds.size.x>6 && rs.Length>=2,"Village foundation missing or incorrectly scaled");
                else Check(bounds.size.z > 1.95f && bounds.size.z < 2.05f && bounds.min.y > -.02f,"Tile orientation/size: "+id+" "+bounds);
                Object.DestroyImmediate(obj);
            }
            var cell=new HexCell(HexCoordinate.Zero,1);object a=new(),b=new();
            cell.SetMovementBlock(a,true);cell.SetMovementBlock(b,true);cell.SetMovementBlock(a,false);
            Check(!cell.IsWalkable,"Shared root released another owner");
            cell.IsWalkable=false;cell.SetMovementBlock(b,false);Check(!cell.IsWalkable,"Root removal restored collapsed terrain");
            cell.IsWalkable=true;Check(cell.IsWalkable,"Terrain did not reopen after all roots cleared");
        }
        private static void ValidateRoutes()
        {
            var config=AssetDatabase.LoadAssetAtPath<TerrainConfigSO>("Assets/Data/Config/TerrainConfig.asset");
            var regions=AssetDatabase.LoadAssetAtPath<RegionSetSO>("Assets/Data/Regions/Bolum1_BolgeSeti.asset");
            foreach(int seed in config.SeedPool)
                for(int layout=1;layout<=4;layout++)
                {
                    var prm=config.ToParams();prm.Regions=regions.ToPlan(seed*31+layout*7919,null);
                    var result=TerrainGenerator.Generate(prm,seed);_report.maps++;
                    var terrain=new Dictionary<HexCoordinate,bool>(); HexCoordinate tree=default; int trees=0;
                    var candidates=new List<HexCoordinate>();
                    var landmarks=new List<HexCoordinate>();
                    for(int row=0;row<prm.Height;row++) for(int col=0;col<prm.Width;col++)
                    {
                        string id=result.Tiles[col,row];var cat=TileCatalog.Get(id);if(cat==null || TileCatalog.IsVoid(id))continue;
                        var c=HexCoordinate.FromOffset(col,row);terrain[c]=cat.Walkable;
                        if(id==TileCatalog.Bayterek){tree=c;trees++;}
                        if(cat.Family==TileFamily.Plain)candidates.Add(c);
                        if(id==TileCatalog.AgacKovugu || id==TileCatalog.HalkaMeclisi || id==TileCatalog.RuhTapinagi)landmarks.Add(c);
                    }
                    Check(trees==1,$"Tree count {seed}/{layout}: {trees}");if(trees!=1)continue;
                    var player=HexCoordinate.FromOffset(result.Start.q,result.Start.r);
                    var blocks=new HashSet<HexCoordinate>();var reachable=QuestRootPlanner.Flood(terrain,blocks,player);
                    candidates.RemoveAll(c=>!reachable.Contains(c) || c.DistanceTo(player)<2 || c.DistanceTo(tree)<4);
                    var rnd=new PythonRandom(seed+layout);rnd.Shuffle(candidates);
                    var goals=new List<HexCoordinate>();
                    foreach(var source in result.Sources)
                    {
                        if(source.Permanent)continue;
                        var sc=HexCoordinate.FromOffset(source.Q,source.R);
                        var near=candidates.Where(c=>!goals.Contains(c)).OrderBy(c=>c.DistanceTo(sc)).FirstOrDefault();goals.Add(near);
                    }
                    foreach(var c in candidates) { if(goals.Count>=5)break; if(!goals.Contains(c))goals.Add(c); }
                    var protect=new HashSet<HexCoordinate>(goals){player};
                    foreach(var c in landmarks)protect.Add(c);
                    var objectives=new List<HexCoordinate>(goals);objectives.AddRange(landmarks);
                    foreach(var core in result.Cores)objectives.Add(HexCoordinate.FromOffset(core.q,core.r));
                    var required=objectives.Where(c=>QuestRootPlanner.Accessible(reachable,c)).ToList();
                    Check(result.RegionSize.Sum()==result.Land,"Clearing region area count mismatch");
                    foreach(var goal in goals)
                    {
                        var path=QuestRootPlanner.Plan(terrain,blocks,protect,objectives,player,tree,goal);
                        if(path==null){_report.failures.Add($"No root {seed}/{layout} -> {goal}");continue;}
                        _report.routes++;
                        Check(path[0]==tree && path[path.Count-1].DistanceTo(goal)==1,"Root endpoints invalid");
                        for(int i=1;i<path.Count;i++)Check(path[i-1].DistanceTo(path[i])==1,"Non-contiguous root");
                        foreach(var c in path)blocks.Add(c);
                        var after=QuestRootPlanner.Flood(terrain,blocks,player);
                        foreach(var g in required)Check(QuestRootPlanner.Accessible(after,g),$"Objective inaccessible after roots: {seed}/{layout}/{g}");
                    }
                }
            Debug.Log($"[BiomeQA] { _report.maps} maps, {_report.routes} roots, {_report.failures.Count} failures");
        }
        private static void Tick()
        {
            if(!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup<_next)return;
            try
            {
                if(_playStarted==0)_playStarted=EditorApplication.timeSinceStartup;
                if(EditorApplication.timeSinceStartup-_playStarted>180)throw new TimeoutException("Biome Play checks timed out at phase "+_phase);
                if(_phase==0)
                {
                    _roots=Object.FindFirstObjectByType<BayterekQuestRoots>();_nodes=Object.FindFirstObjectByType<ChapterNodeManager>();
                    _grid=Object.FindFirstObjectByType<HexGridManager>();_map=Object.FindFirstObjectByType<ChapterMapGenerator>();
                    _player=Object.FindFirstObjectByType<PlayerController>();_state=Object.FindFirstObjectByType<GameStateManager>();
                    if(_roots==null || _nodes==null || _nodes.Nodes.Count==0)return;
                    if(_roots.BranchCount==0)return;
                    _growthStartBlocks=Blocks().Count;
                    var boundary = Object.FindFirstObjectByType<BiomeBoundaryVisuals>();
                    Check(boundary != null,"Missing biome boundaries");
                    Check(Object.FindFirstObjectByType<BiomeTerrainStyling>()!=null,"Missing biome ground blending");
                    _fogEdges=boundary != null ? boundary.VisibleEdgeCount : 0;
                    Capture("world_fog",null,38);
                    Object.FindFirstObjectByType<FogOfWarManager>().SetFullReveal(true);
                    _phase=1;_next=EditorApplication.timeSinceStartup+6;return;
                }
                if(_phase==1)
                {
                    _initialBlocks=Blocks();Check(_initialBlocks.Count>5,"No impassable root tiles");
                    Check(_initialBlocks.Count>_growthStartBlocks,"Initial growth was not progressive");
                    _first=_nodes.Nodes.First(n=>n.Type==MapNodeType.Mandatory);
                    Check(_roots.BranchCount==_nodes.Nodes.Count(n=>n.Type==MapNodeType.Mandatory && !n.Completed),"Initial branch count mismatch");
                    Capture("world",null,38);CaptureRegions();
                    var block=new MaterialPropertyBlock();
                    Check(_grid.Cells.Values.All(c=>c.MeshRenderer!=null),"Initial hidden-grid renderers were not cached");
                    var styled=_grid.Cells.Values.First(c=>c.MeshRenderer!=null && _map.RegionAt(c.Coordinate)!=null);
                    styled.MeshRenderer.GetPropertyBlock(block);
                    _report.notes.Add($"Biome shader={styled.MeshRenderer.sharedMaterial.shader.name}, strength={block.GetVector("_BiomeStrength")}, ground={block.GetVector("_BiomeGround")}");
                    Check(block.GetVector("_BiomeStrength").x>.9f,"Biome grading lost after fog reveal");
                    Check(block.GetVector("_BiomeCenter").w>0,"Biome blend centre missing");
                    foreach(var node in _nodes.Nodes.GroupBy(n=>n.Type).Select(g=>g.First()))
                        Capture("yapi_"+ChapterNodeManager.TileIdOf(node.Type),node.Coord.ToWorldPosition(_grid.HexSize),3.2f);
                    var boundary = Object.FindFirstObjectByType<BiomeBoundaryVisuals>();
                    Check(boundary.IsVisible && boundary.VisibleEdgeCount>20,"Discovered biome transitions missing");
                    Check(boundary.VisibleEdgeCount>_fogEdges,"Hidden biome edges were exposed before exploration");
                    _report.notes.Add("Visible biome edges: "+boundary.VisibleEdgeCount);
                    foreach(var pair in _grid.Cells)
                    {
                        var neighbor=pair.Key.GetNeighbor(0);
                        if(pair.Value.TerrainWalkable && _grid.TryGetCell(neighbor,out var other) && other.TerrainWalkable &&
                            _map.RegionIndexAt(pair.Key)>=0 && _map.RegionIndexAt(neighbor)>=0 &&
                            _map.RegionIndexAt(pair.Key)!=_map.RegionIndexAt(neighbor))
                        {Capture("biyom_gecisi",(pair.Value.WorldPosition+other.WorldPosition)*.5f,5);break;}
                    }
                    typeof(ChapterNodeManager).GetMethod("Complete",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(_nodes,new object[]{_first});
                    _phase=2;_next=EditorApplication.timeSinceStartup+.2;return;
                }
                if(_phase==2)
                {
                    int now=Blocks().Count;Check(now>0,"Completion instantly removed all roots");
                    _report.notes.Add("Retraction running, blocked "+_initialBlocks.Count+" -> "+now);
                    _phase=3;_next=EditorApplication.timeSinceStartup+6;return;
                }
                if(_phase==3)
                {
                    Check(_roots.BranchCount==_nodes.Nodes.Count(n=>n.Type==MapNodeType.Mandatory && !n.Completed),"Completed quest root remains");
                    Check(Blocks().Count<_initialBlocks.Count,"Retraction did not release tiles");
                    var tree=_grid.Cells.Keys.First(c=>_map.TerrainIdAt(c)==TileCatalog.Bayterek);
                    // A two-tile branch can finish before the first sampling tick. Choose
                    // a distant goal so this test can observe an intermediate state.
                    var pick=_grid.Cells.Where(kv=>kv.Value.IsWalkable && _map.IsReachable(kv.Key) && _nodes.NodeAt(kv.Key)==null &&
                        TileCatalog.Get(_map.TerrainIdAt(kv.Key))?.Family==TileFamily.Plain && kv.Key.DistanceTo(_player.CurrentCoord)>3)
                        .OrderByDescending(kv=>kv.Key.DistanceTo(tree)).First().Key;
                    _nodes.SpawnMandatory(pick,3);_phase=4;_next=EditorApplication.timeSinceStartup+.15;return;
                }
                if(_phase==4)
                {
                    Check(_roots.BranchCount==2,"New quest did not start a new branch");
                    _newGrowthProgress=RootGrowthProgress();
                    _report.notes.Add("New-quest growth running");_phase=5;_next=EditorApplication.timeSinceStartup+6;return;
                }
                if(_phase==5)
                {
                    _initialBlocks=Blocks();
                    // A new branch may entirely share already-blocked tiles. Its visible
                    // growth still advances even though the union of blocked cells does not.
                    Check(RootGrowthProgress()>_newGrowthProgress,"New quest growth was not progressive");
                    var mission=AssetDatabase.LoadAssetAtPath<MissionData>("Assets/Data/Missions/Pusu_UyananDev.asset");
                    _state.RequestMission(mission);_state.ConfirmMission();_phase=6;_next=EditorApplication.timeSinceStartup+1;return;
                }
                if(_phase==6)
                {
                    Check(_grid.Cells.Values.All(c=>!c.HasMovementBlock),"Overworld roots leaked into combat grid");
                    Check(!Object.FindFirstObjectByType<BiomeBoundaryVisuals>().IsVisible,"Biome borders leaked into combat");
                    Capture("savas_arenasi",null,12);
                    _state.ReturnToOverworld();_phase=7;_next=EditorApplication.timeSinceStartup+1;return;
                }
                if(_phase==7)
                {
                    Check(_initialBlocks.SetEquals(Blocks()),"Root state lost on combat return");
                    // Collapse a rooted terrain tile, restore it while the root still owns it,
                    // and make sure restoration cannot accidentally open the path.
                    var rooted=_grid.Cells.Values.First(c=>c.HasMovementBlock && c.TerrainWalkable);
                    var collapse=Object.FindFirstObjectByType<MapCollapseManager>();
                    var collapsed=(HashSet<HexCoordinate>)typeof(MapCollapseManager).GetField("_collapsed",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(collapse);
                    collapsed.Add(rooted.Coordinate);
                    typeof(MapCollapseManager).GetMethod("RemoveTile",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(collapse,new object[]{rooted});
                    Check(!rooted.TerrainWalkable,"Collapse did not disable rooted terrain");
                    Check(collapse.RestoreTile(rooted.Coordinate) && rooted.TerrainWalkable && !rooted.IsWalkable,"Restoring terrain bypassed active root");
                    foreach(var n in _nodes.Nodes.Where(n=>n.Type==MapNodeType.Mandatory && !n.Completed).ToArray())
                        typeof(ChapterNodeManager).GetMethod("Complete",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(_nodes,new object[]{n});
                    _phase=8;_next=EditorApplication.timeSinceStartup+7;return;
                }
                if(_phase==8)
                {
                    Check(Blocks().Count==0 && _roots.BranchCount==0,"All completed quests left root blockers");
                    _map.Generate(6118);_phase=9;_next=EditorApplication.timeSinceStartup+6;return;
                }
                if(_phase==9)
                {
                    Check(_roots.BranchCount>0 && Blocks().Count>0,"New map did not rebuild roots");
                    Finish(_report.failures.Count==0);
                }
            }
            catch(Exception e){_report.failures.Add(e.ToString());Debug.LogException(e);Finish(false);}
        }
        private static HashSet<HexCoordinate> Blocks()=>new(_grid.Cells.Where(kv=>kv.Value.HasMovementBlock).Select(kv=>kv.Key));
        private static float RootGrowthProgress() => Object.FindObjectsByType<QuestRootSegment>(FindObjectsSortMode.None)
            .Where(s=>s.gameObject.activeInHierarchy).Sum(s=>s.GrowthAmount);
        private static void CaptureRegions()
        {
            var fog=Object.FindFirstObjectByType<FogOfWarManager>();fog.SetFullReveal(true);
            foreach(var core in _map.RegionCores)Capture(_map.Regions[core.region].Id,core.coord.ToWorldPosition(_grid.HexSize),9);
            foreach(var kv in _grid.Cells)if(_map.TerrainIdAt(kv.Key)==TileCatalog.Bayterek)Capture("bayterek",kv.Value.WorldPosition+Vector3.up*4,12);
        }
        private static void Capture(string name, Vector3? center,float size)
        {
            var go=new GameObject("QA Camera");var cam=go.AddComponent<Camera>();
            cam.orthographic=true;cam.orthographicSize=size;cam.farClipPlane=300;
            cam.transform.rotation=Quaternion.Euler(50,-30,0);
            var target=center ?? _grid.Cells.Values.Aggregate(Vector3.zero,(a,c)=>a+c.WorldPosition)/_grid.Cells.Count;
            cam.transform.position=target-cam.transform.forward*100;
            var rt=new RenderTexture(1600,1100,24);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;
            var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();
            File.WriteAllBytes(Path.Combine(Output,name+".png"),tex.EncodeToPNG());RenderTexture.active=null;cam.targetTexture=null;
            Object.DestroyImmediate(tex);Object.DestroyImmediate(rt);Object.DestroyImmediate(go);
        }
        private static void Finish(bool passed)
        {
            Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"play.json"),JsonUtility.ToJson(_report,true));
            SessionState.SetBool("BiomeQA.Play",false);EditorApplication.update-=Tick;
            Debug.Log("[BiomeQA] "+(passed?"PASS":"FAIL"));EditorApplication.Exit(passed?0:1);
        }
    }
}
