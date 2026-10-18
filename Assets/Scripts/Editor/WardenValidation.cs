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
    public static class WardenValidation
    {
        private static int _checks;
        private static void Check(bool ok,string text){_checks++;if(!ok)throw new Exception("Warden: "+text);}
        private static object Call(ExpeditionSimulation s,string name,params object[] args)=>typeof(ExpeditionSimulation).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(s,args);
        private static void Set(LabUnit u,string name,object v)=>typeof(LabUnit).GetProperty(name).SetValue(u,v);
        private static void Advance(ExpeditionSimulation s,int ticks){for(int i=0;i<ticks;i++)Call(s,"TickWarden",.05f);}
        private static Vector2 Patch(ExpeditionSimulation s)
        {for(int x=-6;x<4;x++)for(int y=-4;y<5;y++){var p=s.Map.Center(2)+new Vector2(x,y);if(s.Map.Walkable(p)&&s.Map.Walkable(p+Vector2.right*5)&&s.Map.HasLine(p,p+Vector2.right*5)&&s.Map.Walkable(p-Vector2.right*2))return p;}throw new Exception("No boss test patch");}
        public static void RunChecks()
        {
            _checks=0;var c=AssetDatabase.LoadAssetAtPath<ExpeditionConfig>(MasteryJourneySetup.DataPath);var s=new ExpeditionSimulation(c);
            Check(s.Load(new ExpeditionSave{version=5,seed=12345,nextStage=2,weapon=1,powers=new int[9],credited=Array.Empty<int>(),offerState=123}),"third mission checkpoint valid");
            var pending=(List<ExpeditionSpawn>)typeof(ExpeditionSimulation).GetField("_pending",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(s);pending.Clear();Call(s,"QueueWarden");
            Check(pending.Count==1&&s.WardenStage==1,"boss has isolated announced entrance");
            foreach(var a in s.Units){Set(a,"Cooldown",100f);Set(a,"Health",10000);}
            for(int i=0;i<40;i++)s.Tick(.05f,Vector2.zero);
            var boss=s.Warden;Check(s.WardenVisible,"boss spawn visible");var p=Patch(s);Set(boss,"Position",p);Set(s.Kam,"Position",p+Vector2.right*2);Advance(s,40);
            Call(s,"PrepareWarden",WardenMove.Cleave);var facing=s.WardenFacing;int hp=s.Kam.Health;Set(s.Kam,"Position",p-Vector2.right*2);Advance(s,27);
            Check(s.WardenFacing==facing&&s.Kam.Health==hp,"cleave locks direction and can be avoided behind");
            Set(s.Kam,"Position",p+Vector2.right*2);Call(s,"PrepareWarden",WardenMove.Cleave);Advance(s,27);Check(s.Kam.Health<hp,"cleave damages inside actual sector");
            Call(s,"Hit",boss,10000,p,false);Check(s.WardenStage==2&&boss.Health==Mathf.RoundToInt(c.WardenHealth*c.WardenThresholds.x),"burst cannot skip phase two");
            int health=boss.Health;Call(s,"Hit",boss,10000,p,false);Check(boss.Health==health&&s.WardenAction==WardenMove.Transition,"transition protects phase change");
            float clock=s.Elapsed;s.TogglePause();s.Tick(.05f,Vector2.zero);Check(s.Elapsed==clock&&s.WardenAction==WardenMove.Transition,"pause freezes transition");s.TogglePause();Advance(s,34);
            Check(s.WardenAddCountForView==2,"phase two summons two adds");Call(s,"SummonWardenAdds");Check(s.WardenAddCountForView==2,"add cap counts pending spawns");
            Check((int)Call(s,"WardenIncomingDamage",20)<20,"living or pending adds protect boss");
            foreach(var a in s.Units)if(a.Ally)Set(a,"Cooldown",100f);for(int i=0;i<40;i++)s.Tick(.05f,Vector2.zero);
            int xp=s.XP;var adds=new List<LabUnit>();foreach(var u in s.Units)if(s.IsWardenAdd(u)&&u.Alive)adds.Add(u);
            Check(adds.Count==2,"announced adds spawn");foreach(var add in adds)Call(s,"Hit",add,10000,add.Position,false);
            Check(s.XP==xp&&s.WardenExposed,"clearing adds grants opening without farmable XP");Check((int)Call(s,"WardenIncomingDamage",20)>20,"combination damage rewarded during opening");
            Set(boss,"Position",p);Set(s.Kam,"Position",p+Vector2.right*4);Call(s,"PrepareWarden",WardenMove.Rush);var aim=boss.Aim;Set(s.Kam,"Position",p-Vector2.right*2);Advance(s,26);Check(boss.Aim==aim,"first rush target locks");
            int prepared=0;bool wasWindup=false;for(int i=0;i<200&&!s.WardenExposed;i++){Call(s,"TickWarden",.05f);bool w=boss.Windup>0;if(w&&!wasWindup)prepared++;wasWindup=w;}
            Check(prepared>=1&&s.WardenExposed,"second rush has its own warning and ends exposed");
            Call(s,"Hit",boss,10000,p,false);Check(s.WardenStage==3&&boss.Health==Mathf.RoundToInt(c.WardenHealth*c.WardenThresholds.y),"burst cannot skip final phase");Advance(s,34);Check(s.WardenAddCountForView==3,"phase three summons three adds");Call(s,"DismissWardenAdds");
            Set(boss,"Position",p);Set(s.Kam,"Position",p+Vector2.right*3);hp=s.Kam.Health;Call(s,"PrepareWarden",WardenMove.Ring);Advance(s,75);
            Check(s.Kam.Health==hp-c.WardenDamage,"expanding wave hits once per target");
            Set(boss,"Position",p);Set(s.Kam,"Position",p+Vector2.right*3);Call(s,"PrepareWarden",WardenMove.Slam);var first=s.WardenSlams[0];Set(s.Kam,"Position",p-Vector2.right*2);hp=s.Kam.Health;Advance(s,70);
            Check(first==p+Vector2.right*3&&s.Kam.Health==hp&&s.WardenExposed,"marked slams stay locked and reward escape");
            Call(s,"SummonWardenAdds");Call(s,"Hit",boss,10000,p,false);Check(!boss.Alive&&s.WardenAddCountForView==0,"boss death removes pending and living adds");
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/warden-validation.json","{\"passed\":true,\"checks\":"+_checks+"}");Debug.Log("WARDEN_VALIDATION_OK "+_checks);
        }
    }
}
