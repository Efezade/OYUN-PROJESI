using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using TacticalRPG.Prototypes.CombatLab;
using TacticalRPG.Prototypes.Expedition;

namespace TacticalRPG.Editor
{
    public static class ExpeditionTacticsValidation
    {
        private static int _checks;
        private static void Check(bool value,string message){_checks++;if(!value)throw new Exception("Tactics validation: "+message);}
        private static void Set(LabUnit u,string name,object value)=>typeof(LabUnit).GetProperty(name).SetValue(u,value);
        private static void Call(ExpeditionSimulation sim,string method,params object[] args)=>typeof(ExpeditionSimulation).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sim,args);
        private static ExpeditionSimulation Fixture(ExpeditionConfig config,int[] levels=null)
        {
            var sim=new ExpeditionSimulation(config);levels=levels??new int[7];int picks=0;foreach(var level in levels)picks+=level;
            sim.Load(new ExpeditionSave{seed=12345,nextStage=1,levels=levels,picks=picks,xp=0,credited=Array.Empty<int>(),offerState=12345});
            for(int i=0;i<50;i++)sim.Tick(.05f,Vector2.zero);
            foreach(var u in sim.Units){Set(u,"Cooldown",100f);Set(u,"Windup",0f);Set(u,"Position",sim.Map.Center(1)+new Vector2(7,-3));}
            Set(sim.Kam,"Position",sim.Map.Center(1));
            return sim;
        }
        public static void RunChecks()
        {
            _checks=0;var config=ScriptableObject.CreateInstance<ExpeditionConfig>();
            var so=new SerializedObject(config);so.FindProperty("_kamHealth").intValue=5000;so.FindProperty("_allyHealth").intValue=5000;so.ApplyModifiedPropertiesWithoutUndo();
            try
            {
                var sim=Fixture(config);LabUnit charger=null,protector=null,normal=null;
                foreach(var u in sim.Units){if(sim.TacticOf(u)==EnemyTactic.Charger)charger=u;else if(sim.TacticOf(u)==EnemyTactic.Protector)protector=u;else if(!u.Ally)normal=u;}
                Check(charger!=null&&protector!=null&&normal!=null,"stage two has distinct enemy roles");
                var center=sim.Map.Center(1);Set(charger,"Position",center-Vector2.right*3);Set(charger,"Cooldown",0f);
                sim.Tick(.05f,Vector2.zero);Check(sim.ChargeActive(charger)&&charger.Windup>0,"rush telegraphs before movement");
                var start=charger.Position;var aim=charger.Aim;int hp=sim.Kam.Health;
                sim.TogglePause();sim.Tick(.05f,Vector2.zero);Check(charger.Position==start&&charger.Aim==aim,"pause freezes rush");sim.TogglePause();
                for(int i=0;i<10;i++)sim.Tick(.05f,Vector2.up);
                Check(charger.Position==start&&charger.Aim==aim&&sim.Kam.Health==hp,"warning has no damage and aim is locked");
                Set(sim.Kam,"Position",center);for(int i=0;i<40;i++)sim.Tick(.05f,Vector2.zero);
                Check(sim.Kam.Health==hp-charger.Damage-2,"rush hits a crossed target exactly once");
                Check(!sim.ChargeActive(charger),"rush ends and enters cooldown");
                Set(normal,"Position",center);Set(protector,"Position",center+Vector2.up);
                Check(sim.IsProtected(normal)&&!sim.IsProtected(protector),"protector covers allies but not itself");
                int before=normal.Health;Call(sim,"Hit",normal,10,center,false);
                Check(before-normal.Health==Mathf.CeilToInt(10*config.ProtectorDamageMultiplier),"aura reduces damage");
                Set(protector,"Health",0);Check(!sim.IsProtected(normal),"killing protector immediately removes protection");
                var vortex=Fixture(config,new[]{0,0,2,0,0,0,0});
                Check(vortex.Cast(LabSkill.Pull,vortex.Kam.Position)&&vortex.VortexActive,"tier two pull leaves a persistent well");
                vortex.TogglePause();for(int i=0;i<80;i++)vortex.Tick(.05f,Vector2.zero);Check(vortex.VortexActive,"paused well does not expire");vortex.TogglePause();
                for(int i=0;i<60;i++)vortex.Tick(.05f,Vector2.zero);Check(!vortex.VortexActive,"well expires in simulation time");
                var orbit=Fixture(config,new[]{0,0,0,0,0,2,0});LabUnit victim=null;
                foreach(var u in orbit.Units)if(!u.Ally&&orbit.TacticOf(u)!=EnemyTactic.Protector){victim=u;break;}
                Set(victim,"Position",orbit.Kam.Position+Vector2.right*3);before=victim.Health;orbit.Dash(Vector2.right);
                Check(victim.Health<before,"tier two dash emits damaging orbit pulse");
                var ward=Fixture(config,new[]{0,0,0,0,0,0,2});victim=null;
                foreach(var u in ward.Units)if(!u.Ally){victim=u;break;}
                Set(victim,"Position",ward.Kam.Position+Vector2.right);ward.Cast(LabSkill.Burst,ward.Kam.Position);start=victim.Position;
                Call(ward,"Hit",ward.Kam,20,victim.Position,false);
                Check(ward.Shield==0&&Vector2.Distance(victim.Position,ward.Kam.Position)>Vector2.Distance(start,ward.Kam.Position),"broken tier two shield pushes nearby enemies");
                Check(ward.Map.Walkable(victim.Position),"knockback stays on valid ground");
                Directory.CreateDirectory("Logs");File.WriteAllText("Logs/expedition-tactics-validation.json","{\"checks\":"+_checks+",\"passed\":true}");
                Debug.Log("EXPEDITION_TACTICS_VALIDATION_OK checks="+_checks);
            }
            finally{UnityEngine.Object.DestroyImmediate(config);}
        }
    }
}
