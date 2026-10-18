using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using TacticalRPG.Prototypes.Expedition;
using TacticalRPG.Prototypes.CombatLab;

namespace TacticalRPG.Editor
{
    public static class HybridBattleValidation
    {
        private static int _checks;
        private static void Check(bool ok,string message){_checks++;if(!ok)throw new Exception("Hybrid: "+message);}
        public static void ValidateScene()
        {
            _checks=0;HybridBattleSetup.EnsureScene();
            var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(HybridBattleSetup.ScenePath);
            var c=AssetDatabase.LoadAssetAtPath<ExpeditionConfig>(HybridBattleSetup.DataPath);var map=new ExpeditionMap(c);
            Physics.SyncTransforms();int missing=0,renderers=0;LODGroup gate=null;
            foreach(var root in scene.GetRootGameObjects())
            {
                foreach(var tr in root.GetComponentsInChildren<Transform>(true))missing+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(tr.gameObject);
                foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {renderers++;foreach(var mat in renderer.sharedMaterials)Check(mat!=null&&mat.shader!=null,"renderer material "+renderer.name);}
                foreach(var lod in root.GetComponentsInChildren<LODGroup>(true))gate=lod;
            }
            Check(missing==0,"no missing scripts");Check(gate!=null&&gate.lodCount==3,"three source gate LODs");
            int samples=0;
            for(float x=-7;x<map.Center(2).x+2;x+=.23f)
            {
                var p=new Vector2(x,0);if(!map.Walkable(p))continue;
                Check(Physics.Raycast(new Vector3(x,8,0),Vector3.down,out var hit,12),"ray hits floor "+x);
                // The gate arch and encounter barriers are intentionally above the floor.
                if(Mathf.Abs(x-c.HybridLayout.GatePosition.x)<.8f||Mathf.Abs(x-map.EncounterEdge(0))<.15f||Mathf.Abs(x-map.EncounterEdge(1))<.15f)continue;
                Check(Mathf.Abs(hit.point.y-map.Height(p))<.025f,"render collider / movement height agree "+x+" actual="+hit.point.y+" expected="+map.Height(p));samples++;
            }
            string before=File.ReadAllText(HybridBattleSetup.ScenePath);HybridBattleSetup.EnsureScene();Check(before==File.ReadAllText(HybridBattleSetup.ScenePath),"setup preserves authored scene");
            File.WriteAllText("Logs/hybrid-scene-validation.json","{\"passed\":true,\"checks\":"+_checks+",\"floorSamples\":"+samples+",\"renderers\":"+renderers+"}");Debug.Log("HYBRID_SCENE_VALIDATION_OK "+_checks);
        }
        public static void RunChecks()
        {
            _checks=0;var c=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ExpeditionConfig>(HybridBattleSetup.DataPath));
            try
            {
                var m=new ExpeditionMap(c);var l=c.HybridLayout;
                Check(c.Radius==1&&l.GroundPrefab!=null&&l.GatePrefab!=null,"source prefab references and 2m metric");
                Check(m.Platforms.Count==309,"91 + 91 + 127 authored ground tiles");
                Check(m.CanStep(m.Entrance(0),Vector2.zero),"gate centre passage clear");
                Check(!m.Walkable(l.GatePosition+new Vector2(0,1.12f)),"gate left foot blocks");
                Check(!m.Walkable(l.GatePosition+new Vector2(0,-1.25f)),"gate right foot blocks");
                Check(!m.HasLine(l.GatePosition+new Vector2(-2,1.12f),l.GatePosition+new Vector2(2,1.12f)),"gate blocks shots");
                for(int i=0;i<3;i++)
                {
                    Check(Mathf.Abs(m.Height(m.Center(i))-l.Level(i))<.001f,"platform elevation "+i);
                    Check(m.Walkable(m.Entrance(i)),"stage entry "+i);
                    Check(!m.Walkable(m.Center(i)+l.CoverOffsets[0]),"cover blocks "+i);
                }
                for(int i=0;i<2;i++)
                {
                    var a=new Vector2(l.BridgeStart(i),0);var b=new Vector2(l.BridgeEnd(i),0);var middle=(a+b)*.5f;
                    Check(m.CanStep(a,b)&&m.CanStep(b,a),"bridge continuous both directions "+i);
                    Check(!m.Walkable(middle+Vector2.up*(l.HalfWidth+.1f)),"bridge edge blocks "+i);
                    Check(m.Ground(middle)==GroundKind.Ramp,"ramp classified "+i);
                }
                var tour=new ExpeditionSimulation(c);tour.StartExploration();
                foreach(var target in new[]{m.Center(0),m.Center(1),m.Center(2),m.Entrance(0)})
                {
                    int ticks=0;while(Vector2.Distance(tour.Kam.Position,target)>.2f&&ticks++<5000)tour.Tick(.05f,m.NextStep(tour.Kam.Position,target)-tour.Kam.Position);
                    Check(Vector2.Distance(tour.Kam.Position,target)<.25f,"tour route "+target);
                }
                Check(tour.Exploring&&tour.LivingEnemies==0&&tour.Pending.Count==0&&tour.XP==0,"exploration has no enemies or rewards");
                // Functional driver, not a normal-difficulty balance test.
                var so=new SerializedObject(c);so.FindProperty("_kamHealth").intValue=100000;so.FindProperty("_allyHealth").intValue=100000;so.ApplyModifiedPropertiesWithoutUndo();
                foreach(int weapon in new[]{0,1})
                {
                    var s=new ExpeditionSimulation(c);s.Start(12345);for(int i=0;i<2;i++)if(s.Offers[i]==weapon){s.Choose(i);break;}int camps=0;
                    for(int tick=0;tick<40000&&s.Phase!=ExpeditionPhase.Won;tick++)
                    {
                        if(s.Phase==ExpeditionPhase.Lost)throw new Exception("Hybrid functional driver died");
                        if(s.Phase==ExpeditionPhase.Choosing){int pick=0;for(int i=0;i<3;i++)if(s.Offers[i]==0||s.Offers[i]==7)pick=i;s.Choose(pick);continue;}
                        if(s.Phase==ExpeditionPhase.Camp)
                        {
                            camps++;var saved=s.Checkpoint();var loaded=new ExpeditionSimulation(c);Check(loaded.Load(saved)&&!loaded.Exploring&&loaded.Picks==s.Picks,"checkpoint preserves build "+weapon+"/"+camps);
                            int stage=s.Stage;s.Continue();for(int n=0;n<2000&&s.Stage==stage;n++)s.Tick(.05f,s.Map.NextStep(s.Kam.Position,s.Map.Center(stage+1))-s.Kam.Position);
                            Check(s.Stage==stage+1,"physical bridge transition "+weapon+"/"+camps);continue;
                        }
                        LabUnit near=null;float distance=float.MaxValue;foreach(var u in s.Units)if(!u.Ally&&u.Alive){float d=Vector2.Distance(s.Kam.Position,u.Position);if(d<distance){distance=d;near=u;}}
                        Vector2 move=Vector2.zero;if(near!=null){if(distance>s.WeaponRange*.8f||!s.Map.HasLine(s.Kam.Position,near.Position))move=s.Map.NextStep(s.Kam.Position,near.Position)-s.Kam.Position;s.Cast(LabSkill.Pull,near.Position);s.Cast(LabSkill.Burst,s.Kam.Position);}s.Tick(.05f,move);
                    }
                    Check(s.Phase==ExpeditionPhase.Won&&camps==2&&s.Picks==6,"three missions and boss completed weapon "+weapon+" phase="+s.Phase+" stage="+s.Stage+" enemies="+s.LivingEnemies);
                }
                File.WriteAllText("Logs/hybrid-validation.json","{\"passed\":true,\"checks\":"+_checks+"}");Debug.Log("HYBRID_VALIDATION_OK "+_checks);
            }
            finally{UnityEngine.Object.DestroyImmediate(c);}
        }
    }
}
