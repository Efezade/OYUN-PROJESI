using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using TacticalRPG.Prototypes.CombatLab;
using TacticalRPG.Prototypes.Expedition;

namespace TacticalRPG.Editor
{
    public static class EncounterValidation
    {
        private static int _checks;
        private static void Check(bool value,string message){_checks++;if(!value)throw new Exception("Encounter validation: "+message);}
        private static void Set(LabUnit u,string field,object value)=>typeof(LabUnit).GetProperty(field).SetValue(u,value);
        private static object Call(ExpeditionSimulation s,string method,params object[] args)=>typeof(ExpeditionSimulation).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(s,args);
        private static ExpeditionSimulation Fixture(ExpeditionConfig config)
        {
            var s=new ExpeditionSimulation(config);s.Load(new ExpeditionSave{version=3,seed=12345,nextStage=1,xp=9,picks=2,weapon=1,powers=new[]{1,1,0,0,0,0,0,0,0},credited=Array.Empty<int>(),offerState=123});
            for(int i=0;i<48;i++)s.Tick(.05f,Vector2.zero);
            foreach(var u in s.Units){Set(u,"Position",s.Map.Center(1)+new Vector2(7,-5));Set(u,"Cooldown",100f);Set(u,"Windup",0f);}
            Set(s.Kam,"Position",s.Map.Center(1)+new Vector2(-3,0));return s;
        }
        private static LabUnit Role(ExpeditionSimulation s,EnemyTactic role){foreach(var u in s.Units)if(s.TacticOf(u)==role)return u;throw new Exception("Missing encounter role: "+role);}
        public static void RunChecks()
        {
            _checks=0;var config=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ExpeditionConfig>(WeaponJourneySetup.DataPath));
            try
            {
                var s=Fixture(config);var sniper=Role(s,EnemyTactic.Sniper);var flanker=Role(s,EnemyTactic.Flanker);var bomber=Role(s,EnemyTactic.Bomber);var protector=Role(s,EnemyTactic.Protector);
                Check(sniper!=null&&flanker!=null&&bomber!=null&&protector!=null,"mixed second-mission roster");
                var center=s.Kam.Position;Set(sniper,"Position",center+Vector2.right*2);Set(sniper,"Cooldown",1f);
                float before=Vector2.Distance(sniper.Position,center);Call(s,"TickEncounter",sniper,.05f);Check(Vector2.Distance(sniper.Position,center)>before,"sniper retreats when approached");
                Set(sniper,"Position",center+Vector2.right*6);Set(sniper,"Cooldown",1f);var start=sniper.Position;Call(s,"TickEncounter",sniper,.05f);
                Check(Vector2.Distance(sniper.Position,start)>.01f&&Mathf.Abs(sniper.Position.y-start.y)>.01f,"sniper strafes rather than marching straight");
                Set(sniper,"Position",center+Vector2.right*6);Set(sniper,"Cooldown",0f);Call(s,"TickEncounter",sniper,.05f);Check(s.SniperAiming(sniper),"sniper announces shot");
                var aim=sniper.Aim;int hp=s.Kam.Health;Set(s.Kam,"Position",center+Vector2.up*2);
                for(int i=0;i<30;i++)Call(s,"TickEncounter",sniper,.05f);
                Check(sniper.Aim==aim&&s.Kam.Health==hp,"locked shot misses a sidestep without tracking");
                Set(s.Kam,"Position",center);Set(sniper,"Position",center+Vector2.right*6);Set(sniper,"Cooldown",0f);Call(s,"TickEncounter",sniper,.05f);
                for(int i=0;i<30;i++)Call(s,"TickEncounter",sniper,.05f);
                Check(s.Kam.Health<hp,"standing in announced firing lane causes damage");
                Set(flanker,"Position",center+Vector2.right*4);Set(flanker,"Cooldown",0f);start=flanker.Position;
                for(int i=0;i<8;i++)Call(s,"TickEncounter",flanker,.05f);
                Check(Mathf.Abs(flanker.Position.y-start.y)>.1f,"flanker approaches from the side");
                Set(flanker,"Position",center+Vector2.right);Set(protector,"Position",center+Vector2.right*6);start=protector.Position;
                for(int i=0;i<10;i++)Call(s,"TickEncounter",protector,.05f);
                Check(Vector2.Distance(protector.Position,flanker.Position)<Vector2.Distance(start,flanker.Position),"protector follows its front ally");
                Set(sniper,"Windup",1f);Set(flanker,"Windup",1f);Check(!(bool)Call(s,"AttackSlotAvailable",bomber),"simultaneous telegraphs capped at two");Set(sniper,"Windup",0f);Set(flanker,"Windup",0f);
                Set(bomber,"Position",center+Vector2.right*5);Set(bomber,"Cooldown",0f);hp=s.Kam.Health;Call(s,"TickEncounter",bomber,.05f);
                Check(bomber.Windup>1&&s.Hazards.Count==0,"bomb announces full radius before damage");
                s.TogglePause();float life=bomber.Windup;s.Tick(.05f,Vector2.zero);Check(bomber.Windup==life&&s.Kam.Health==hp,"pause freezes preparation and damage");s.TogglePause();
                for(int i=0;i<34;i++)Call(s,"TickEncounter",bomber,.05f);
                Check(s.Hazards.Count==1&&Vector2.Distance(s.Hazards[0].Position,center)<.01f,"bomb lands at the locked location");
                Call(s,"TickHazards",.05f);Check(s.Kam.Health<hp,"remaining in hazard damages player");
                Set(s.Kam,"Position",center+Vector2.up*3);hp=s.Kam.Health;Call(s,"TickHazards",.7f);Check(s.Kam.Health==hp,"escaping the marked ground avoids damage");
                Call(s,"TickHazards",3f);Check(s.Hazards.Count==0,"hazards expire");
                Set(flanker,"Position",center);Set(sniper,"Position",center);Call(s,"SeparateEnemy",flanker,.05f);Check(Vector2.Distance(flanker.Position,sniper.Position)>.01f&&s.Map.Walkable(flanker.Position),"stacked enemies separate on valid terrain");
                Directory.CreateDirectory("Logs");File.WriteAllText("Logs/encounter-validation.json","{\"passed\":true,\"checks\":"+_checks+"}");Debug.Log("ENCOUNTER_VALIDATION_OK "+_checks);
            }
            finally{UnityEngine.Object.DestroyImmediate(config);}
        }
    }
}
