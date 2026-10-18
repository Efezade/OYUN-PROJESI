using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using TacticalRPG.Prototypes.CombatLab;
using TacticalRPG.Prototypes.Expedition;

namespace TacticalRPG.Editor
{
    public static class WeaponJourneyValidation
    {
        private static int _checks;
        private static void Check(bool value,string message){_checks++;if(!value)throw new Exception("Weapon journey validation: "+message);}
        private static void Set(LabUnit u,string field,object value)=>typeof(LabUnit).GetProperty(field).SetValue(u,value);
        private static void Call(ExpeditionSimulation s,string method,params object[] args)=>typeof(ExpeditionSimulation).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(s,args);
        private static void Own(ExpeditionSimulation s,int id)=>((bool[])typeof(ExpeditionSimulation).GetField("_powers",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(s))[id]=true;
        private static string Build(ExpeditionSimulation s){string result=((int)s.Weapon)+":";for(int i=0;i<9;i++)result+=s.HasPower(i)?"1":"0";return result;}
        private static void PickWeapon(ExpeditionSimulation s,JourneyWeapon weapon){for(int i=0;i<2;i++)if(s.Offers[i]==(int)weapon){Check(s.Choose(i),"weapon choice accepted");return;}throw new Exception("weapon missing");}
        private static void Drive(ExpeditionSimulation s)
        {
            LabUnit nearest=null;float best=float.MaxValue;
            foreach(var u in s.Units)if(!u.Ally&&u.Alive){float d=Vector2.Distance(u.Position,s.Kam.Position);if(d<best){best=d;nearest=u;}}
            var input=Vector2.zero;
            if(nearest!=null)
            {
                if(best>s.WeaponRange*.8f||!s.Map.HasLine(s.Kam.Position,nearest.Position))input=s.Map.NextStep(s.Kam.Position,nearest.Position)-s.Kam.Position;
                if(s.Weapon==JourneyWeapon.Spear||best>2)s.Cast(LabSkill.Pull,nearest.Position);
                s.Cast(LabSkill.Burst,s.Kam.Position);
            }
            s.Tick(.05f,input);
        }
        private static ExpeditionSimulation CombatFixture(ExpeditionConfig config,JourneyWeapon weapon)
        {
            var s=new ExpeditionSimulation(config);s.Start(12345);PickWeapon(s,weapon);
            for(int i=0;i<45;i++)s.Tick(.05f,Vector2.zero);
            foreach(var u in s.Units){Set(u,"Position",new Vector2(7,-5));Set(u,"Cooldown",100f);Set(u,"Windup",0f);Set(u,"Health",500);Set(u,"MaxHealth",500);}
            Set(s.Kam,"Position",new Vector2(-3,0));Call(s,"ResetJourneyCombat");return s;
        }
        public static void RunChecks()
        {
            _checks=0;var config=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ExpeditionConfig>(WeaponJourneySetup.DataPath));
            var so=new SerializedObject(config);so.FindProperty("_kamHealth").intValue=5000;so.FindProperty("_allyHealth").intValue=5000;so.ApplyModifiedPropertiesWithoutUndo();
            try
            {
                var menu=new ExpeditionSimulation(config);menu.Start(12345);Check(menu.Phase==ExpeditionPhase.WeaponChoice,"initial weapon draft");
                var repeat=new ExpeditionSimulation(config);repeat.Start(12345);Check(menu.Offers[0]==repeat.Offers[0]&&menu.Offers[1]==repeat.Offers[1],"seed repeats start offers");
                Check(menu.Offers[0]!=menu.Offers[1]&&menu.Offers[2]==-1,"two real weapons, no fake third option");
                float elapsed=menu.Elapsed;menu.Tick(.05f,Vector2.one);Check(menu.Elapsed==elapsed&&!menu.Choose(2),"starting draft freezes world and rejects absent card");
                var melee=CombatFixture(config,JourneyWeapon.Crescent);var enemies=new List<LabUnit>();foreach(var u in melee.Units)if(!u.Ally)enemies.Add(u);
                var center=melee.Kam.Position;
                Set(enemies[0],"Position",center+Vector2.right*1.8f);Set(enemies[1],"Position",center+new Vector2(1.7f,.5f));Set(enemies[2],"Position",center-Vector2.right*1.5f);
                Call(melee,"WeaponStrike",enemies[0]);
                Check(enemies[0].Health<500&&enemies[1].Health<500&&enemies[2].Health==500,"crescent cleaves front arc, not behind");
                Check(melee.Charge==1&&!melee.Cast(LabSkill.Burst,center),"finisher requires landed attacks");
                Call(melee,"WeaponStrike",enemies[0]);Call(melee,"WeaponStrike",enemies[0]);Check(melee.CanWeaponE&&melee.Cast(LabSkill.Burst,center)&&melee.Charge==0,"three attacks unlock and spend finisher");
                var spear=CombatFixture(config,JourneyWeapon.Spear);enemies.Clear();foreach(var u in spear.Units)if(!u.Ally)enemies.Add(u);center=spear.Kam.Position;
                Set(enemies[0],"Position",center+Vector2.right*2);Set(enemies[1],"Position",center+Vector2.right*4);Set(enemies[2],"Position",center+new Vector2(2,1.7f));
                Check(!spear.CanWeaponQ&&!spear.Cast(LabSkill.Pull,center),"empty recall cannot be spammed");
                Call(spear,"WeaponStrike",enemies[0]);Check(enemies[0].Health<500&&enemies[1].Health<500&&enemies[2].Health==500,"spear pierces aligned targets only");
                Check(spear.Anchors.Count==1&&spear.CanWeaponQ,"spear leaves recallable anchor");int hp=enemies[0].Health;
                Check(spear.Cast(LabSkill.Pull,center)&&spear.Anchors.Count==0&&enemies[0].Health<hp,"recall damages along returning path and consumes anchors");
                var wall=spear.Map.Cover[0];int blockedHp=enemies[0].Health;Set(enemies[0],"Position",wall+Vector2.right*1.4f);
                Call(spear,"WeaponLine",wall-Vector2.right*1.4f,wall+Vector2.right*2,10,.6f,11,false);
                Check(enemies[0].Health==blockedHp,"weapon line cannot hit through cover");
                var evolved=CombatFixture(config,JourneyWeapon.Crescent);enemies.Clear();foreach(var u in evolved.Units)if(!u.Ally)enemies.Add(u);center=evolved.Kam.Position;
                Set(enemies[0],"Position",center+Vector2.right*2);Own(evolved,0);int waves=0;int blasts=0;evolved.Effect+=fx=>{if(fx.Kind==12)waves++;if(fx.Kind==13)blasts++;};
                for(int i=0;i<3;i++)Call(evolved,"WeaponStrike",enemies[0]);Check(waves==1,"third crescent attack adds a ranged wave");
                Own(evolved,3);Check(evolved.Cast(LabSkill.Burst,center),"charged evolved finisher fires");int firstBlasts=blasts;
                Call(evolved,"TickJourney",.65f);Check(blasts==firstBlasts+1&&evolved.Charge==0,"echo repeats without charging or spending twice");
                Own(evolved,1);Set(enemies[0],"Position",center+Vector2.right*2);int before=enemies[0].Health;Call(evolved,"TickJourney",.01f);
                // Reset only orbit timing/angle to test its actual contact radius independently.
                typeof(ExpeditionSimulation).GetProperty("BladeAngle").SetValue(evolved,0f);typeof(ExpeditionSimulation).GetField("_bladeClock",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(evolved,0f);
                Call(evolved,"TickJourney",.01f);Check(enemies[0].Health<before,"new orbit skill damages on blade contact");
                Own(evolved,4);int discs=0;evolved.Effect+=fx=>{if(fx.Kind==14)discs++;};Call(evolved,"TickJourney",3f);Check(discs>0,"new disc skill fires automatically");
                var twin=CombatFixture(config,JourneyWeapon.Spear);enemies.Clear();foreach(var u in twin.Units)if(!u.Ally)enemies.Add(u);center=twin.Kam.Position;Set(enemies[0],"Position",center+Vector2.right*2);Own(twin,0);Own(twin,2);
                Call(twin,"WeaponStrike",enemies[0]);Check(twin.Anchors.Count==2,"spear transformation leaves two anchors");
                Check(twin.Cast(LabSkill.Pull,center)&&twin.VortexActive&&twin.Anchors.Count==0,"recall combines with persistent pull field");
                var trail=CombatFixture(config,JourneyWeapon.Crescent);enemies.Clear();foreach(var u in trail.Units)if(!u.Ally)enemies.Add(u);center=trail.Kam.Position;Set(enemies[0],"Position",center+Vector2.right);Own(trail,5);before=enemies[0].Health;
                Check(trail.Dash(Vector2.right)&&enemies[0].Health<before,"dash transformation cuts crossed enemies");
                foreach(JourneyWeapon weapon in Enum.GetValues(typeof(JourneyWeapon)))
                {
                    var run=new ExpeditionSimulation(config);run.Start(12345);PickWeapon(run,weapon);int camps=0;var gained=new HashSet<int>();var stagePicks=new List<int>();
                    for(int tick=0;tick<30000&&run.Phase!=ExpeditionPhase.Won;tick++)
                    {
                        CheckPhase(run);
                        if(run.Phase==ExpeditionPhase.Choosing)
                        {
                            int a=run.Offers[0],b=run.Offers[1],c=run.Offers[2];Check(a>=0&&b>=0&&c>=0&&a!=b&&a!=c&&b!=c,"distinct available new powers");
                            foreach(int id in run.Offers)Check(!run.HasPower(id)&&(id!=7||run.HasPower(0)),"offer new with valid prerequisites");
                            float clock=run.Elapsed;run.Tick(.05f,Vector2.one);Check(run.Elapsed==clock&&!run.Cast(LabSkill.Burst,run.Kam.Position),"power draft freezes actions");
                            int index=0;for(int i=0;i<3;i++)if(run.Offers[i]==0)index=i;
                            int chosen=run.Offers[index];run.Choose(index);Check(gained.Add(chosen)&&run.HasPower(chosen),"choice adds new behavior once");continue;
                        }
                        if(run.Phase==ExpeditionPhase.Camp)
                        {
                            camps++;stagePicks.Add(run.Picks);Check(run.Picks==camps*2,"two behavior choices per completed mission");
                            string build=Build(run);int xp=run.XP;var saved=run.Checkpoint();
                            var loaded=new ExpeditionSimulation(config);Check(loaded.Load(JsonUtility.FromJson<ExpeditionSave>(JsonUtility.ToJson(saved))),"v3 checkpoint JSON loads");
                            Check(Build(loaded)==build&&loaded.XP==xp&&loaded.Stage==run.Stage+1,"weapon, powers, XP survive disk roundtrip");
                            Check(loaded.Phase==ExpeditionPhase.Fighting,"no weapon reselection on mission load");
                            var malformed=JsonUtility.FromJson<ExpeditionSave>(JsonUtility.ToJson(saved));malformed.version=1;Check(!loaded.Load(malformed),"old save cannot be mistaken for v3");
                            Set(loaded.Kam,"Health",0);loaded.Tick(.05f,Vector2.zero);Check(loaded.Phase==ExpeditionPhase.Lost&&loaded.Retry()&&Build(loaded)==build&&loaded.XP==xp,"death retry preserves weapon and choices");
                            int stage=run.Stage;run.Continue();for(int i=0;i<1500&&run.Stage==stage;i++)run.Tick(.05f,run.Map.NextStep(run.Kam.Position,run.Map.Center(stage+1))-run.Kam.Position);
                            Check(run.Stage==stage+1&&Build(run)==build&&run.XP==xp,"physical mission transition preserves combination");continue;
                        }
                        Drive(run);
                    }
                    Check(run.Phase==ExpeditionPhase.Won&&camps==2&&run.Picks==6&&gained.Count==6,"both weapons can complete all three missions with six unique behaviors");
                    run.Start(999);Check(run.Phase==ExpeditionPhase.WeaponChoice&&run.Picks==0&&run.XP==0&&!run.HasPower(0),"new journey resets only after explicit new run");
                }
                Directory.CreateDirectory("Logs");File.WriteAllText("Logs/weapon-journey-validation.json","{\"passed\":true,\"checks\":"+_checks+",\"weapons\":2,\"missionsPerWeapon\":3,\"choicesPerWeapon\":6}");
                Debug.Log("WEAPON_JOURNEY_VALIDATION_OK "+_checks);
            }
            finally{UnityEngine.Object.DestroyImmediate(config);}
        }
        private static void CheckPhase(ExpeditionSimulation s){if(s.Phase==ExpeditionPhase.Lost)throw new Exception("Functional durability fixture died");}
    }
}
