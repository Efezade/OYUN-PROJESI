using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using TacticalRPG.Prototypes.Expedition;
using TacticalRPG.Prototypes.CombatLab;

namespace TacticalRPG.Editor
{
    public static class MasteryValidation
    {
        private static int _checks;
        private static void Check(bool value,string message){_checks++;if(!value)throw new Exception("Mastery: "+message);}
        private static void Call(ExpeditionSimulation s,string method,params object[] args)=>typeof(ExpeditionSimulation).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(s,args);
        private static ExpeditionSimulation Fixture(ExpeditionConfig c,int weapon,int stage)
        {
            var s=new ExpeditionSimulation(c);
            if(stage==0){s.Start(12345);for(int i=0;i<2;i++)if(s.Offers[i]==weapon){s.Choose(i);break;}}
            else Check(s.Load(new ExpeditionSave{version=5,seed=12345,nextStage=stage,xp=0,picks=0,weapon=weapon,powers=new int[9],credited=Array.Empty<int>(),offerState=123}),"fixture loads");
            return s;
        }
        public static void RunChecks()
        {
            _checks=0;var c=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ExpeditionConfig>(MasteryJourneySetup.DataPath));
            try
            {
                foreach(int weapon in new[]{0,1})
                {
                    var first=Fixture(c,weapon,0);var last=Fixture(c,weapon,2);
                    Check(last.WeaponDamage==first.WeaponDamage*2&&last.WeaponAttackSeconds<first.WeaponAttackSeconds,"guaranteed mastery damage and speed");
                    Check(last.Ascended&&last.AnchorCapacity==8,"transformation available on third mission entry");
                    foreach(var p in last.Pending)Check(p.Unit.Health==c.EnemyHealth&&!last.IsElite(p.Unit),"opening wave preserves weak old enemies");
                    Call(last,"QueuePack");int elites=0;
                    foreach(var p in last.Pending)if(last.IsElite(p.Unit)){elites++;Check(p.Unit.Health>c.EnemyHealth*2&&p.Unit.Damage>3,"elites retain threat");}
                    Check(elites==2,"third mission second wave contains two elites");
                    // Isolated attack geometry fixture, no balance claims.
                    var target=last.Pending[0].Unit;typeof(LabUnit).GetProperty("Position").SetValue(target,last.Kam.Position+Vector2.right*2);
                    if(weapon==1){Call(last,"WeaponStrike",target);Check(last.Anchors.Count==3,"ascended spear creates three projectiles");Call(last,"WeaponStrike",target);Call(last,"WeaponStrike",target);Check(last.Anchors.Count==8,"anchors capped at eight");}
                    else
                    {
                        for(int i=0;i<45;i++)last.Tick(.05f,Vector2.zero);
                        LabUnit enemy=null;foreach(var u in last.Units)if(!u.Ally&&u.Alive){enemy=u;break;}
                        Check(enemy!=null,"melee target exists");typeof(LabUnit).GetProperty("Position").SetValue(enemy,last.Kam.Position+Vector2.right*1.5f);typeof(LabUnit).GetProperty("Health").SetValue(enemy,500);
                        int rays=0;last.Effect+=fx=>{if(fx.Kind==12)rays++;};for(int i=0;i<3;i++)Call(last,"WeaponStrike",enemy);Check(rays==3,"ascended crescent emits three rays");
                    }
                }
                // Functional full journey driver uses high health; normal difficulty requires human play.
                var so=new SerializedObject(c);so.FindProperty("_kamHealth").intValue=100000;so.FindProperty("_allyHealth").intValue=100000;so.ApplyModifiedPropertiesWithoutUndo();
                foreach(int weapon in new[]{0,1})
                {
                    var s=Fixture(c,weapon,0);int camps=0;
                    for(int tick=0;tick<40000&&s.Phase!=ExpeditionPhase.Won;tick++)
                    {
                        CheckAlive(s);
                        if(s.Phase==ExpeditionPhase.Choosing)
                        {int index=0;for(int i=0;i<3;i++)if(s.Offers[i]==0||s.Offers[i]==7)index=i;Check(s.Choose(index),"power accepted");continue;}
                        if(s.Phase==ExpeditionPhase.Camp)
                        {
                            camps++;Check(s.Picks==camps*2,"two cards per completed mission");var save=s.Checkpoint();var loaded=new ExpeditionSimulation(c);
                            Check(save.version==5&&loaded.Load(JsonUtility.FromJson<ExpeditionSave>(JsonUtility.ToJson(save))),"v5 disk roundtrip");
                            Check(loaded.MasteryRank==camps&&loaded.Picks==s.Picks&&loaded.Weapon==s.Weapon,"mastery and build persist");
                            var old=new ExpeditionSimulation(AssetDatabase.LoadAssetAtPath<ExpeditionConfig>(WeaponJourneySetup.DataPath));Check(!old.Load(save),"v5 rejected by old journey");
                            typeof(LabUnit).GetProperty("Health").SetValue(loaded.Kam,0);loaded.Tick(.05f,Vector2.zero);Check(loaded.Retry()&&loaded.MasteryRank==camps&&loaded.Picks==s.Picks,"retry keeps mastery without stacking");
                            int stage=s.Stage;s.Continue();for(int n=0;n<1500&&s.Stage==stage;n++)s.Tick(.05f,s.Map.NextStep(s.Kam.Position,s.Map.Center(stage+1))-s.Kam.Position);
                            Check(s.Stage==stage+1&&s.MasteryRank==camps,"physical transition grants mastery");continue;
                        }
                        LabUnit near=null;float distance=float.MaxValue;foreach(var u in s.Units)if(!u.Ally&&u.Alive){float d=Vector2.Distance(s.Kam.Position,u.Position);if(d<distance){near=u;distance=d;}}
                        var move=Vector2.zero;if(near!=null){if(distance>s.WeaponRange*.8f||!s.Map.HasLine(s.Kam.Position,near.Position))move=s.Map.NextStep(s.Kam.Position,near.Position)-s.Kam.Position;s.Cast(LabSkill.Pull,near.Position);s.Cast(LabSkill.Burst,s.Kam.Position);}s.Tick(.05f,move);
                    }
                    Check(s.Phase==ExpeditionPhase.Won&&s.Picks==6&&camps==2,"three missions six cards complete");s.Start(111);Check(s.MasteryRank==0&&!s.Ascended&&s.Picks==0,"new run resets mastery");
                }
                Directory.CreateDirectory("Logs");File.WriteAllText("Logs/mastery-validation.json","{\"passed\":true,\"checks\":"+_checks+"}");Debug.Log("MASTERY_VALIDATION_OK "+_checks);
            }
            finally{UnityEngine.Object.DestroyImmediate(c);}
        }
        private static void CheckAlive(ExpeditionSimulation s){if(s.Phase==ExpeditionPhase.Lost)throw new Exception("Mastery functional fixture died");}
    }
}
