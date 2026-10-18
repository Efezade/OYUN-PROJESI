using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using TacticalRPG.Prototypes.Expedition;
using TacticalRPG.Prototypes.CombatLab;

namespace TacticalRPG.Editor
{
    public static class PressureBattleValidation
    {
        private static int _checks;
        private static void Check(bool ok,string text){_checks++;if(!ok)throw new Exception("Pressure: "+text);}
        private static object Call(ExpeditionSimulation s,string method,params object[] args)=>typeof(ExpeditionSimulation).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(s,args);
        private static void Set(LabUnit u,string name,object value)=>typeof(LabUnit).GetProperty(name).SetValue(u,value);
        private static ExpeditionSimulation Boss(ExpeditionConfig c,int seed=12345,int phase=3)
        {
            var s=new ExpeditionSimulation(c);Check(s.Load(new ExpeditionSave{version=5,seed=seed,nextStage=2,weapon=1,powers=new int[9],credited=Array.Empty<int>(),offerState=123}),"load fixture");
            Call(s,"PrepareWardenCapture",phase);
            foreach(var a in s.Units)if(a.Ally)Set(a,"Cooldown",1000f);
            for(int i=0;i<40;i++)s.Tick(.05f,Vector2.zero);
            Set(s.Kam,"Position",s.Map.Center(2));Set(s.Warden,"Position",s.Map.Center(2)+Vector2.right*4);return s;
        }
        public static void RunChecks()
        {
            _checks=0;var c=AssetDatabase.LoadAssetAtPath<ExpeditionConfig>(PressureBattleSetup.DataPath);var old=AssetDatabase.LoadAssetAtPath<ExpeditionConfig>(HybridBattleSetup.DataPath);var p=c.Pressure;
            Check(p!=null&&old.Pressure==null,"profile isolated from v8");
            Check(c.EnemyHealth==old.EnemyHealth&&c.WardenHealth==old.WardenHealth&&c.EliteHealthMultiplier==old.EliteHealthMultiplier,"no enemy health inflation");
            Check(p.AttackSlots(0)==2&&p.AttackSlots(1)==3&&p.AttackSlots(2)==3,"bounded simultaneous threat progression");
            Check(p.DamageBonus(0)==0&&p.Pursuit(0)==1&&p.Cooldown(0)==1&&p.PackInterval(0)==old.PackInterval,"tutorial pressure unchanged");
            Check(p.Pursuit(2)>p.Pursuit(1)&&p.Cooldown(2)>p.Cooldown(1)&&p.PackInterval(2)<p.PackInterval(1),"pressure progresses by mission");
            Check(c.WardenRushSpeed>=c.WardenWalkSpeed.z*4&&c.WardenWarning*.8f>=.7f,"fast burst retains second warning");
            Check(c.WardenExposure<old.WardenExposure&&c.WardenRecovery<old.WardenRecovery,"shorter boss openings");
            for(int stage=1;stage<=2;stage++)
            {
                var encounter=new ExpeditionSimulation(c);encounter.Load(new ExpeditionSave{version=5,seed=12345,nextStage=stage,weapon=1,powers=new int[9],credited=Array.Empty<int>(),offerState=123});
                bool bomber=false,protector=false;foreach(var spawn in encounter.Pending){bomber|=encounter.TacticOf(spawn.Unit)==EnemyTactic.Bomber;protector|=encounter.TacticOf(spawn.Unit)==EnemyTactic.Protector;Check(spawn.Unit.Health==18,"opening fodder remains fragile");}
                Check(bomber&&(stage==1||protector),"mixed roles on later opening wave");
            }
            var s=Boss(c);Call(s,"PrepareMeteorCapture");
            Check(s.Meteors.Count>=3&&s.Meteors.Count<=5,"bounded meteor field");
            Check(s.Map.CanStep(s.Kam.Position,s.MeteorEscape)&&Vector2.Distance(s.Kam.Position,s.MeteorEscape)>=2.9f,"reachable meteor escape");
            var first=s.Meteors[0].Position;float time=s.Meteors[0].Remaining;int hp=s.Kam.Health;
            var same=Boss(c);Call(same,"PrepareMeteorCapture");bool deterministic=s.Meteors.Count==same.Meteors.Count;
            for(int i=0;i<s.Meteors.Count&&deterministic;i++)deterministic&=s.Meteors[i].Position==same.Meteors[i].Position;
            Check(deterministic,"same seed reproduces meteor targets");
            var different=Boss(c,9876);Call(different,"PrepareMeteorCapture");bool varied=s.Meteors.Count!=different.Meteors.Count;
            for(int i=1;i<Math.Min(s.Meteors.Count,different.Meteors.Count);i++)varied|=s.Meteors[i].Position!=different.Meteors[i].Position;
            Check(varied,"different seed varies secondary targets");
            s.TogglePause();s.Tick(.05f,Vector2.zero);Check(s.Meteors[0].Remaining==time,"pause freezes meteors");s.TogglePause();
            Set(s.Kam,"Position",s.MeteorEscape);for(int i=0;i<35;i++)Call(s,"TickWarden",.05f);
            Check(s.Kam.Health==hp&&s.Meteors[0].Position==first,"movement escapes locked targets");
            Set(same.Kam,"Position",same.Meteors[0].Position);hp=same.Kam.Health;
            for(int i=0;i<37;i++)Call(same,"TickWarden",.05f);
            Check(same.Kam.Health==hp-p.MeteorDamage,"standing on one impact takes one hit");
            for(int i=0;i<40&&s.WardenAction==WardenMove.Meteor;i++)Call(s,"TickWarden",.05f);
            Check(s.WardenAction==WardenMove.Rush&&s.Warden.Windup>0,"late meteor combo has warned rush followup");
            for(int i=0;i<150&&!s.WardenExposed;i++)Call(s,"TickWarden",.05f);
            Check(s.WardenExposed,"combo terminates in punish window");
            Call(s,"PrepareMeteorCapture");Call(s,"Hit",s.Warden,10000,s.Warden.Position,false);Check(s.Meteors.Count==0&&!s.Warden.Alive,"boss death cancels meteors");
            var transition=Boss(c,12345,2);Call(transition,"PrepareMeteorCapture");Call(transition,"Hit",transition.Warden,10000,transition.Warden.Position,false);
            Check(transition.WardenStage==3&&transition.Meteors.Count==0,"phase transition clears old targets");
            var adds=Boss(c,12345,2);Call(adds,"SummonWardenAdds");
            for(int i=0;i<40;i++)adds.Tick(.05f,Vector2.zero);
            Call(adds,"PrepareMeteorCapture");var guards=new List<LabUnit>();foreach(var u in adds.Units)if(adds.IsWardenAdd(u)&&u.Alive)guards.Add(u);
            Check(guards.Count==2,"pressure summons capped guards");
            foreach(var guard in guards)Call(adds,"Hit",guard,10000,guard.Position,false);
            Check(adds.WardenAction==WardenMove.Meteor&&adds.Meteors.Count>0,"clearing guards cannot cancel telegraphed boss sequence");
            for(int i=0;i<220&&!adds.WardenExposed;i++)Call(adds,"TickWarden",.05f);
            Check(adds.WardenExposed,"clearing guards still rewards completed sequence");
            Call(transition,"PrepareMeteorCapture");Set(transition.Kam,"Health",0);transition.Tick(.05f,Vector2.zero);Check(transition.Retry()&&transition.Meteors.Count==0,"retry clears old effects");
            string scene=File.ReadAllText(PressureBattleSetup.ScenePath),config=File.ReadAllText(PressureBattleSetup.DataPath);PressureBattleSetup.EnsureScene();
            Check(scene==File.ReadAllText(PressureBattleSetup.ScenePath)&&config==File.ReadAllText(PressureBattleSetup.DataPath),"setup preserves authored assets");
            HybridBattleValidation.RunChecks(PressureBattleSetup.DataPath);
            File.WriteAllText("Logs/pressure-validation.json","{\"passed\":true,\"checks\":"+_checks+",\"journeyChecks\":36}");
            ProbePolicies(old,c);Debug.Log("PRESSURE_VALIDATION_OK "+_checks);
        }
        // Normal-health behavioural probes, not a model of human skill or a win-rate estimate.
        private static void ProbePolicies(ExpeditionConfig old,ExpeditionConfig current)
        {
            var csv=new StringBuilder("version,weapon,seed,policy,seconds,outcome,kamHp,bossHp,bossPhase\n");
            foreach(var c in new[]{old,current})foreach(int weapon in new[]{0,1})foreach(int seed in new[]{12345,9831})foreach(bool active in new[]{false,true})
            {
                var s=new ExpeditionSimulation(c);s.Load(new ExpeditionSave{version=5,seed=seed,nextStage=2,xp=100,picks=6,weapon=weapon,powers=new[]{1,1,1,1,1,1,0,0,0},credited=Array.Empty<int>(),offerState=123});
                Call(s,"PrepareWardenCapture",1);Set(s.Warden,"Health",c.WardenHealth);float started=s.Elapsed;
                for(int tick=0;tick<3600&&s.Phase==ExpeditionPhase.Fighting;tick++)
                {
                    Vector2 move=Vector2.zero;LabUnit near=null;float best=float.MaxValue;
                    foreach(var u in s.Units)if(!u.Ally&&u.Alive){float d=Vector2.Distance(u.Position,s.Kam.Position);if(d<best){best=d;near=u;}}
                    if(active&&near!=null)
                    {
                        bool evade=false;
                        foreach(var meteor in s.Meteors)if(!meteor.Impacted&&Vector2.Distance(s.Kam.Position,meteor.Position)<c.Pressure.MeteorRadius+.65f){move=s.MeteorEscape-s.Kam.Position;evade=true;break;}
                        if(!evade&&s.WardenVisible&&s.WardenBusy){var radial=s.Kam.Position-s.Warden.Position;move=new Vector2(-radial.y,radial.x).normalized;}
                        else if(!evade&&(best>s.WeaponRange*.75f||!s.Map.HasLine(s.Kam.Position,near.Position)))move=s.Map.NextStep(s.Kam.Position,near.Position)-s.Kam.Position;
                        s.Cast(LabSkill.Pull,near.Position);s.Cast(LabSkill.Burst,s.Kam.Position);
                        if(evade)s.Dash(move);
                    }
                    s.Tick(.05f,move);
                }
                csv.AppendLine(string.Join(",",c==old?"v8":"v9",weapon,seed,active?"move+skills":"stationary-auto",(s.Elapsed-started).ToString("F2",System.Globalization.CultureInfo.InvariantCulture),s.Phase,s.Kam.Health,s.Warden.Health,s.WardenStage));
            }
            File.WriteAllText("Logs/pressure-policy-probes.csv",csv.ToString());
        }
    }
}
