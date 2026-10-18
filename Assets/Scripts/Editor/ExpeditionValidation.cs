using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using TacticalRPG.Prototypes.CombatLab;
using TacticalRPG.Prototypes.Expedition;

namespace TacticalRPG.Editor
{
    public static class ExpeditionValidation
    {
        private static int _checks;
        private static void Check(bool value,string text){_checks++;if(!value)throw new Exception("Expedition validation: "+text);}
        [MenuItem("TacticalRPG/Prototipler/B - Sefer Mantik Kontrolleri",false,311)]
        public static void RunChecks()
        {
            _checks=0;var config=ScriptableObject.CreateInstance<ExpeditionConfig>();var map=new ExpeditionMap(config);
            Check(map.Cells.Count>500,"larger three-area connected board");
            Check(map.Ground(new Vector2(3,-1))==GroundKind.Water,"water surface");
            Check(map.Ground(new Vector2(-3,2))==GroundKind.Mud,"mud surface");
            Check(map.Height(new Vector2(5,3))>1.8f,"real raised ground");
            Check(map.CanStep(new Vector2(1,3),new Vector2(5,3)),"ramp traversable");
            Check(!map.CanStep(new Vector2(5,.5f),new Vector2(5,3)),"cliff not traversable");
            var wall=map.Cover[0];Check(!map.HasLine(wall-Vector2.right*2,wall+Vector2.right*2),"pillar blocks shots");
            Check(!map.Walkable(wall),"pillar blocks walking");
            Check(!map.Contains(new Vector2(-100,0)),"off-board rejected");
            bool allRoundtrip=true;foreach(var cell in map.Cells)allRoundtrip&=map.Hex(cell.World(config.Radius)).Equals(cell);
            Check(allRoundtrip,"all axial coordinates roundtrip");
            var a=new ExpeditionSimulation(config);var b=new ExpeditionSimulation(config);a.Start(12345);b.Start(12345);
            Check(a.Pending.Count==b.Pending.Count&&a.Pending.Count>=5,"same seed pack size");
            for(int i=0;i<a.Pending.Count;i++)Check(a.Pending[i].Unit.Position==b.Pending[i].Unit.Position&&a.Pending[i].Unit.Role==b.Pending[i].Unit.Role,"seed reproduces spawn "+i);
            b.Start(67890);Check(b.Pending[0].Unit.Position!=a.Pending[0].Unit.Position,"new seed changes encounter");
            float mana=a.Mana;Check(!a.Cast(LabSkill.Pull,new Vector2(100,100))&&a.Mana==mana,"invalid aim spends no mana");
            a.TogglePause();float clock=a.Elapsed;var position=a.Kam.Position;a.Tick(.05f,Vector2.right);
            Check(a.Elapsed==clock&&a.Kam.Position==position&&!a.Dash(Vector2.right),"pause locks movement and actions");a.TogglePause();
            Check(a.Dash(Vector2.left)&&!a.Dash(Vector2.left),"dash cooldown");
            // Controlled durability fixture validates progression/pathing, not player balance.
            var so=new SerializedObject(config);so.FindProperty("_kamHealth").intValue=5000;so.FindProperty("_allyHealth").intValue=5000;so.ApplyModifiedPropertiesWithoutUndo();
            var run=new ExpeditionSimulation(config);run.Start(12345);int camps=0,picks=0;bool frozenChecked=false;var offers=new List<string>();
            for(int tick=0;tick<24000&&run.Phase!=ExpeditionPhase.Won;tick++)
            {
                if(run.Phase==ExpeditionPhase.Lost)throw new Exception("Durability fixture unexpectedly lost at stage "+run.Stage);
                if(run.Phase==ExpeditionPhase.Choosing)
                {
                    int x=run.Offers[0],y=run.Offers[1],z=run.Offers[2];Check(x!=y&&x!=z&&y!=z,"three distinct offers");
                    Check(run.Levels[x]<2&&run.Levels[y]<2&&run.Levels[z]<2,"offers below cap");offers.Add(x+","+y+","+z);
                    if(!frozenChecked)
                    {
                        float elapsed=run.Elapsed;float savedMana=run.Mana;int hp=run.Kam.Health;var savedPosition=run.Kam.Position;
                        for(int i=0;i<20;i++)run.Tick(.05f,Vector2.one);
                        Check(run.Elapsed==elapsed&&run.Mana==savedMana&&run.Kam.Health==hp&&run.Kam.Position==savedPosition,"draft freezes entire encounter");
                        Check(!run.Cast(LabSkill.Burst,Vector2.zero)&&!run.Dash(Vector2.right),"no abilities during draft");frozenChecked=true;
                    }
                    Check(!run.Choose(9),"invalid offer rejected");int chosen=run.Offers[picks%3],before=run.Levels[chosen];
                    Check(run.Choose(picks%3)&&run.Levels[chosen]==before+1,"choice applies exactly once");picks++;continue;
                }
                if(run.Phase==ExpeditionPhase.Camp)
                {
                    camps++;var save=run.Checkpoint();Check(save!=null&&save.nextStage==camps,"checkpoint advances stage");
                    var restored=new ExpeditionSimulation(config);Check(restored.Load(JsonUtility.FromJson<ExpeditionSave>(JsonUtility.ToJson(save))),"checkpoint JSON restores");
                    Check(restored.Stage==run.Stage+1&&restored.XP==run.XP,"checkpoint keeps progression");
                    for(int i=0;i<7;i++)Check(restored.Levels[i]==run.Levels[i],"power persists across save "+i);
                    var oldLevels=Levels(run);run.Continue();int oldStage=run.Stage;
                    if(camps==1)
                    {
                        WalkTo(run,new Vector2(5,3));Check(run.Map.Ground(run.Kam.Position)==GroundKind.High&&Mathf.Abs(run.Range(run.Kam)-run.Kam.Range-config.HighRange)<.001f,"walking up ramp grants actual range bonus");
                        WalkTo(run,new Vector2(-4,-2));var grassStart=run.Kam.Position;run.Tick(.05f,Vector2.right);float grassDistance=Vector2.Distance(grassStart,run.Kam.Position);
                        WalkTo(run,new Vector2(-4,3));var mudStart=run.Kam.Position;run.Tick(.05f,Vector2.up);float mudDistance=Vector2.Distance(mudStart,run.Kam.Position);
                        Check(Mathf.Abs(mudDistance/grassDistance-config.MudSpeed)<.01f,"mud reduces actual movement distance");
                    }
                    for(int travel=0;travel<1200&&run.Stage==oldStage;travel++)
                    {var goal=run.Map.Center(oldStage+1);run.Tick(.05f,run.Map.NextStep(run.Kam.Position,goal)-run.Kam.Position);}
                    Check(run.Stage==oldStage+1,"connected passage physically traversed");Check(Levels(run)==oldLevels,"physical transition keeps all powers");continue;
                }
                Drive(run,tick);
            }
            Check(run.Phase==ExpeditionPhase.Won&&camps==2,"all three fights, camps and final boss complete");
            Check(picks>=6&&run.Chains>0,"multiple power picks and real chains");
            Check(config.KamHealth==5000&&config.Mana==16,"runtime never mutates configuration");
            // Replayed enemies at a checkpoint cannot mint new growth.
            var credited=new int[61];for(int i=0;i<credited.Length;i++)credited[i]=200+i;
            var replay=new ExpeditionSimulation(config);
            Check(replay.Load(new ExpeditionSave{seed=12345,nextStage=1,xp=8,picks=2,levels=new[]{2,0,0,0,0,0,0},credited=credited,offerState=12345}),"anti-farm fixture loads");
            for(int i=0;i<14000&&replay.Phase==ExpeditionPhase.Fighting;i++)Drive(replay,i);
            Check(replay.Phase==ExpeditionPhase.Camp&&replay.XP==8,"repeat enemies award no duplicate progression");
            var malformed=new ExpeditionSave{version=99};Check(!replay.Load(malformed),"unsupported save rejected");
            var spells=new ExpeditionSimulation(config);spells.Load(new ExpeditionSave{seed=12,nextStage=1,xp=8,picks=2,levels=new[]{0,0,0,1,0,0,1},credited=Array.Empty<int>(),offerState=12});
            int bursts=0;spells.Effect+=fx=>{if(fx.Kind==3)bursts++;};
            Check(spells.Cast(LabSkill.Burst,spells.Kam.Position)&&spells.Shield==5&&spells.Mana==12,"ward and mana cost apply on cast");
            for(int i=0;i<16;i++)spells.Tick(.05f,Vector2.zero);
            Check(bursts==2&&Mathf.Abs(spells.Mana-(12+.8f*config.ManaRegen))<.01f,"echo repeats once without extra mana");
            run.Start(55);Check(run.Picks==0&&run.XP==0&&Levels(run)=="0,0,0,0,0,0,0","new run resets build");
            so=new SerializedObject(config);so.FindProperty("_kamHealth").intValue=1;so.FindProperty("_allyHealth").intValue=1;so.ApplyModifiedPropertiesWithoutUndo();
            var loss=new ExpeditionSimulation(config);loss.Start(22);
            for(int i=0;i<5000&&loss.Phase!=ExpeditionPhase.Lost;i++)loss.Tick(.05f,Vector2.zero);
            Check(loss.Phase==ExpeditionPhase.Lost,"death enters loss state");Check(loss.Retry()&&loss.Phase==ExpeditionPhase.Fighting&&loss.Deaths==1&&loss.Seed==22,"shrine retry keeps run seed");
            UnityEngine.Object.DestroyImmediate(config);
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/expedition-validation.json",JsonUtility.ToJson(new Report{checks=_checks,passed=true,camps=camps,picks=picks,offers=offers.ToArray()},true));
            Debug.Log("EXPEDITION_VALIDATION_OK "+_checks+" checks; "+picks+" picks; 2 camps");
        }
        private static string Levels(ExpeditionSimulation s){var values=new string[7];for(int i=0;i<7;i++)values[i]=s.Levels[i].ToString();return string.Join(",",values);}
        private static void WalkTo(ExpeditionSimulation s,Vector2 destination)
        {
            for(int i=0;i<900&&Vector2.Distance(s.Kam.Position,destination)>.1f;i++)s.Tick(.05f,s.Map.NextStep(s.Kam.Position,destination)-s.Kam.Position);
            Check(Vector2.Distance(s.Kam.Position,destination)<.15f,"reachable terrain location "+destination);
        }
        private static void Drive(ExpeditionSimulation s,int tick)
        {
            LabUnit nearest=null;float best=float.MaxValue;
            foreach(var u in s.Units){if(u.Ally||!u.Alive)continue;float d=Vector2.Distance(s.Kam.Position,u.Position);if(d<best){best=d;nearest=u;}}
            Vector2 direction=Vector2.zero;
            if(nearest!=null)
            {
                if(best>3||!s.Map.HasLine(s.Kam.Position,nearest.Position))direction=s.Map.NextStep(s.Kam.Position,nearest.Position)-s.Kam.Position;
                s.Cast(LabSkill.Pull,nearest.Position);s.Cast(LabSkill.Burst,s.Kam.Position);
            }
            s.Tick(.05f,direction);
        }
        [Serializable] private sealed class Report{public int checks;public bool passed;public int camps;public int picks;public string[] offers;}
    }
}
