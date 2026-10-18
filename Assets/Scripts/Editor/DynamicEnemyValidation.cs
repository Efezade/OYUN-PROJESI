using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using TacticalRPG.Prototypes.Expedition;
using TacticalRPG.Prototypes.CombatLab;

namespace TacticalRPG.Editor
{
    public static class DynamicEnemyValidation
    {
        private static int _checks;
        private static void Check(bool value,string message){_checks++;if(!value)throw new Exception("Dynamic enemy: "+message);}
        private static object Call(ExpeditionSimulation s,string method,params object[] args)=>typeof(ExpeditionSimulation).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(s,args);
        private static void Set(LabUnit u,string name,object value)=>typeof(LabUnit).GetProperty(name).SetValue(u,value);
        private static LabUnit Role(ExpeditionSimulation s,EnemyTactic kind){foreach(var u in s.Units)if(s.TacticOf(u)==kind)return u;throw new Exception("Role missing: "+kind);}
        private static ExpeditionSimulation Fixture(ExpeditionConfig c)
        {
            var s=new ExpeditionSimulation(c);s.Load(new ExpeditionSave{version=5,seed=12345,nextStage=2,weapon=1,powers=new int[9],credited=Array.Empty<int>(),offerState=123});Call(s,"QueuePack");
            for(int i=0;i<65;i++)s.Tick(.05f,Vector2.zero);
            foreach(var u in s.Units){Set(u,"Health",10000);Set(u,"Cooldown",100f);Set(u,"Windup",0f);Set(u,"Position",s.Map.Center(2)+new Vector2(7,-5));}
            Set(s.Kam,"Position",s.Map.Center(2)+new Vector2(-3,0));return s;
        }
        public static void RunChecks()
        {
            _checks=0;var c=AssetDatabase.LoadAssetAtPath<ExpeditionConfig>(MasteryJourneySetup.DataPath);
            var s=Fixture(c);var leap=Role(s,EnemyTactic.Leaper);var rage=Role(s,EnemyTactic.Frenzied);var cloner=Role(s,EnemyTactic.Cloner);
            Check(leap!=null&&rage!=null&&cloner!=null,"three dynamic roles present");
            // Clear any state created during spawning, preserving roster/tactics.
            Call(s,"ResetDynamicEnemies");var center=s.Kam.Position;Set(leap,"Position",center+Vector2.right*5);Set(leap,"Cooldown",0f);
            int hp=s.Kam.Health;Call(s,"TickDynamicEnemy",leap,.05f);var aim=leap.Aim;
            Check(leap.Windup>1&&s.LeapWarning(leap)&&s.EnemyAirHeight(leap)==0,"landing warning precedes takeoff");
            Set(s.Kam,"Position",center+Vector2.up*3);
            for(int i=0;i<24;i++)Call(s,"TickDynamicEnemy",leap,.05f);
            Check(s.Leaping(leap)&&s.EnemyAirHeight(leap)>0,"leap has airborne arc");
            float height=s.EnemyAirHeight(leap);s.TogglePause();s.Tick(.05f,Vector2.zero);Check(s.EnemyAirHeight(leap)==height,"pause freezes jump");s.TogglePause();
            Set(rage,"Windup",1f);Check(!(bool)Call(s,"AttackSlotAvailable",cloner),"airborne leap occupies attack budget");Set(rage,"Windup",0f);
            for(int i=0;i<20;i++)Call(s,"TickDynamicEnemy",leap,.05f);
            Check(leap.Aim==aim&&Vector2.Distance(leap.Position,aim)<.01f&&s.Kam.Health==hp,"locked landing is avoidable");
            var landed=leap.Position;Call(s,"TickDynamicEnemy",leap,.1f);Check(leap.Position==landed,"landing recovery creates opening");
            Call(s,"ResetDynamicEnemies");Set(s.Kam,"Position",center);Set(leap,"Position",center+Vector2.right*5);Set(leap,"Cooldown",0f);Call(s,"TickDynamicEnemy",leap,.05f);
            for(int i=0;i<40;i++)Call(s,"TickDynamicEnemy",leap,.05f);
            Check(s.Kam.Health<hp,"ignoring landing warning takes damage");
            Call(s,"ResetDynamicEnemies");Set(rage,"Position",center+Vector2.right*5);Set(rage,"Cooldown",0f);Call(s,"TickDynamicEnemy",rage,.05f);
            Check(rage.Windup>0&&!s.Enraged(rage)&&s.SelfPreparation(rage),"frenzy announces before speeding up");
            for(int i=0;i<17;i++)Call(s,"TickDynamicEnemy",rage,.05f);
            Check(s.Enraged(rage),"frenzy activates");var before=rage.Position;Call(s,"TickDynamicEnemy",rage,.05f);Check(Vector2.Distance(before,rage.Position)>c.EnemySpeed*.05f,"frenzy moves faster");
            for(int i=0;i<70;i++)Call(s,"TickDynamicEnemy",rage,.05f);
            Check(!s.Enraged(rage)&&rage.Cooldown>0,"frenzy ends with exhaustion");
            Call(s,"ResetDynamicEnemies");foreach(var u in s.Units){Set(u,"Cooldown",100f);Set(u,"Windup",0f);}
            Set(cloner,"Position",center+Vector2.right*5);Set(cloner,"Cooldown",0f);Call(s,"TickDynamicEnemy",cloner,.05f);
            Check(cloner.Windup>1&&(int)Call(s,"CloneCount",-1)==0,"cloning has preparation");
            for(int i=0;i<30;i++)Call(s,"TickDynamicEnemy",cloner,.05f);
            Check((int)Call(s,"CloneCount",cloner.Id)==2,"two clones queued");Call(s,"CreateClones",cloner);Check((int)Call(s,"CloneCount",cloner.Id)==2,"per-owner clone cap");
            Call(s,"CreateClones",rage);Call(s,"CreateClones",leap);Check((int)Call(s,"CloneCount",-1)<=4,"global clone cap");
            // Release queued clones through the real spawn path with attacks disabled.
            for(int i=0;i<40;i++)s.Tick(.05f,Vector2.zero);
            LabUnit clone=null;foreach(var u in s.Units)if(s.IsClone(u)&&u.Alive){clone=u;break;}
            Check(clone!=null&&s.TacticOf(clone)==EnemyTactic.Clone&&clone.MaxHealth==c.CloneHealth,"spawned copy is fragile and cannot clone");
            int xp=s.XP;Call(s,"Hit",clone,10000,clone.Position,false);Check(s.XP==xp,"clone kill never grants progression");
            Call(s,"Hit",cloner,100000,cloner.Position,false);Check((int)Call(s,"CloneCount",cloner.Id)==0,"owner death immediately removes copies");
            Call(s,"MaintainClones",20f);Check((int)Call(s,"CloneCount",-1)==0,"all clones expire");
            Call(s,"CreateClones",rage);Call(s,"Hit",rage,100000,rage.Position,false);Check((int)Call(s,"CloneCount",rage.Id)==0,"owner death cancels pending copies");
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/dynamic-enemy-validation.json","{\"passed\":true,\"checks\":"+_checks+"}");Debug.Log("DYNAMIC_ENEMY_VALIDATION_OK "+_checks);
        }
    }
}
