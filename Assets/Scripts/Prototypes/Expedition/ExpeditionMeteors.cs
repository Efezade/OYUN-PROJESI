using System.Collections.Generic;
using UnityEngine;

namespace TacticalRPG.Prototypes.Expedition
{
    public sealed class WardenMeteor
    {
        public Vector2 Position {get;internal set;}
        public float Remaining {get;internal set;}
        public bool Impacted {get;internal set;}
    }
    public sealed partial class ExpeditionSimulation
    {
        private readonly List<WardenMeteor> _meteors=new List<WardenMeteor>(5);
        private int _meteorSequence;
        private bool _wardenFollowup;
        private bool _wardenAddOpeningQueued;
        public IReadOnlyList<WardenMeteor> Meteors=>_meteors;
        public Vector2 MeteorEscape {get;private set;}
        private void ResetMeteors(){_meteors.Clear();_meteorSequence=0;_wardenFollowup=false;_wardenAddOpeningQueued=false;}
        private void PrepareMeteors()
        {
            _meteors.Clear();var pressure=_config.Pressure;
            if(pressure==null){RecoverWarden();return;}
            MeteorEscape=Kam.Position;
            for(int i=0;i<16;i++)
            {
                var p=Kam.Position+Rotate(Vector2.right,i*22.5f)*3;
                if(Map.Region(p)==Stage&&Map.CanStep(Kam.Position,p)){MeteorEscape=p;break;}
            }
            // Do not schedule a strike on a trapped player; a reachable exit is mandatory.
            if(Vector2.Distance(MeteorEscape,Kam.Position)<2){RecoverWarden();return;}
            var rng=new System.Random(Seed+17011+(++_meteorSequence)*331);
            _meteors.Add(new WardenMeteor{Position=Kam.Position,Remaining=pressure.MeteorWarning});
            for(int attempt=0;attempt<100&&_meteors.Count<WardenStage+2;attempt++)
            {
                float angle=(float)rng.NextDouble()*360;
                var p=Kam.Position+Rotate(Vector2.right,angle)*(2+(float)rng.NextDouble()*4);
                if(!Map.Walkable(p)||Map.Region(p)!=Stage||SegmentDistance(p,Kam.Position,MeteorEscape)<pressure.MeteorRadius+.5f)continue;
                bool near=false;foreach(var m in _meteors)if(Vector2.Distance(p,m.Position)<pressure.MeteorRadius*2+.25f)near=true;
                if(near)continue;
                _meteors.Add(new WardenMeteor{Position=p,Remaining=pressure.MeteorWarning+_meteors.Count*pressure.MeteorStagger});
            }
            _warden.Windup=0;_wardenActive=true;
        }
        private void TickMeteors(float dt)
        {
            bool done=true;var pressure=_config.Pressure;
            foreach(var meteor in _meteors)
            {
                meteor.Remaining-=dt;
                if(!meteor.Impacted&&meteor.Remaining<=0)
                {
                    meteor.Impacted=true;
                    foreach(var a in _units)if(a.Ally&&a.Alive&&Vector2.Distance(a.Position,meteor.Position)<pressure.MeteorRadius)
                        Hit(a,pressure.MeteorDamage,meteor.Position,false);
                    Effect?.Invoke(new TacticalRPG.Prototypes.CombatLab.LabFx(meteor.Position,meteor.Position,0,3));
                }
                if(meteor.Remaining>-.3f)done=false;
            }
            if(!done)return;
            _meteors.Clear();
            if(WardenStage>1){_wardenFollowup=true;PrepareWarden(WardenMove.Rush);}else OpenWarden();
        }
        private void CompleteWardenHeavy()
        {
            if(_config.Pressure!=null&&WardenStage>1&&!_wardenFollowup)
            {_wardenFollowup=true;PrepareWarden(WardenMove.Slam);}
            else OpenWarden();
        }
        internal void PrepareMeteorCapture(){_wardenFollowup=false;PrepareWarden(WardenMove.Meteor);}
    }
}
