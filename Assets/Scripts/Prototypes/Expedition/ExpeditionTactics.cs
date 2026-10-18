using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Prototypes.CombatLab;

namespace TacticalRPG.Prototypes.Expedition
{
    public enum EnemyTactic { Normal, Charger, Protector, Boss, Flanker, Sniper, Bomber }
    public sealed partial class ExpeditionSimulation
    {
        private sealed class TacticState
        {
            public EnemyTactic Kind;
            public bool Rushing;
            public bool SmashNext;
            public readonly HashSet<int> Hit = new HashSet<int>();
        }
        private readonly Dictionary<int,TacticState> _tactics=new Dictionary<int,TacticState>();
        private float _vortexClock;
        public Vector2 VortexPosition {get;private set;}
        public bool VortexActive=>_vortexClock>0&&Phase==ExpeditionPhase.Fighting;
        public float VortexRadius=>2.4f;
        public float ProtectorRadius=>_config.ProtectorRadius;
        public EnemyTactic TacticOf(LabUnit u)=>_tactics.TryGetValue(u.Id,out var t)?t.Kind:EnemyTactic.Normal;
        public bool ChargeActive(LabUnit u)=>u.Alive&&_tactics.TryGetValue(u.Id,out var t)&&(t.Kind==EnemyTactic.Charger||t.Kind==EnemyTactic.Boss)&&!t.SmashNext&&(u.Windup>0||t.Rushing);
        public string DisplayName(LabUnit u)=>(IsElite(u)?"SEÇKİN ":"")+TacticName(u);
        private string TacticName(LabUnit u)=>TacticOf(u) switch {EnemyTactic.Charger=>"YARICI",EnemyTactic.Protector=>"KALKANCI",EnemyTactic.Boss=>"MABET BEKÇİSİ",EnemyTactic.Flanker=>"YANCI",EnemyTactic.Sniper=>"AVCI",EnemyTactic.Bomber=>"KUŞATMACI",_=>u.Name};
        private void ResetTactics(){_elites.Clear();_tactics.Clear();_vortexClock=0;ResetEncounters();}
        private void RegisterTactic(LabUnit u,EnemyTactic kind){_tactics[u.Id]=new TacticState{Kind=kind};}
        public bool IsProtected(LabUnit u)
        {
            if(u.Ally||!u.Alive||TacticOf(u)==EnemyTactic.Protector)return false;
            foreach(var other in _units)
                if(other.Alive&&TacticOf(other)==EnemyTactic.Protector&&Vector2.Distance(u.Position,other.Position)<=ProtectorRadius&&Map.HasLine(other.Position,u.Position))return true;
            return false;
        }
        private bool TickTactic(LabUnit u,float dt)
        {
            if(!_tactics.TryGetValue(u.Id,out var t)||(t.Kind!=EnemyTactic.Charger&&t.Kind!=EnemyTactic.Boss))return false;
            if(t.SmashNext)
            {
                // Let the normal boss slam finish before scheduling another rush.
                if(u.Windup>0&&u.Windup<=dt)t.SmashNext=false;
                return false;
            }
            if(u.Windup>0){u.Windup=Mathf.Max(0,u.Windup-dt);if(u.Windup==0)t.Rushing=true;return true;}
            if(t.Rushing)
            {
                float remaining=_config.ChargeSpeed*dt;
                while(remaining>0)
                {
                    var delta=u.Aim-u.Position;
                    float step=Mathf.Min(.1f,remaining,delta.magnitude);
                    if(step<.001f){EndRush(u,t);break;}
                    var destination=u.Position+delta.normalized*step;
                    // No sliding around cover or tunnelling across height transitions.
                    if(!Map.CanStep(u.Position,destination)){EndRush(u,t);break;}
                    u.Position=destination;remaining-=step;
                    foreach(var ally in _units)
                        if(ally.Ally&&ally.Alive&&!t.Hit.Contains(ally.Id)&&Vector2.Distance(ally.Position,u.Position)<_config.ChargeRadius&&Map.HasLine(u.Position,ally.Position))
                        {t.Hit.Add(ally.Id);Hit(ally,u.Damage+2,u.Position,false);}
                }
                return true;
            }
            var target=IsJourney?Kam:Target(u);if(target==null)return true;
            if(u.Cooldown>0){if(IsJourney)Strafe(u,Kam.Position,dt,.6f);return true;}
            if(Vector2.Distance(u.Position,target.Position)>_config.ChargeRange||!Map.HasLine(u.Position,target.Position))
                Navigate(u,target.Position,_config.EnemySpeed*dt,dt);
            else
            {
                if(IsJourney&&!AttackSlotAvailable(u))return true;
                var direction=target.Position-u.Position;if(direction.sqrMagnitude<.001f)direction=Vector2.right;
                u.Aim=u.Position+direction.normalized*_config.ChargeRange;
                u.Windup=_config.ChargeWarning;t.Hit.Clear();
            }
            return true;
        }
        private void EndRush(LabUnit u,TacticState t){t.Rushing=false;u.Cooldown=_config.EnemyAttackSeconds;t.SmashNext=t.Kind==EnemyTactic.Boss;}
        private void CreateVortex(Vector2 target){VortexPosition=target;_vortexClock=_config.VortexDuration;}
        private bool InVortex(Vector2 p)=>VortexActive&&Vector2.Distance(p,VortexPosition)<VortexRadius&&Map.HasLine(VortexPosition,p);
        private void TickVortex(float dt)
        {
            if(!VortexActive)return;
            foreach(var u in _units)if(!u.Ally&&u.Alive&&InVortex(u.Position))Move(u,VortexPosition-u.Position,dt*1.2f);
            _vortexClock=Mathf.Max(0,_vortexClock-dt);
        }
        private void OrbitPulse()
        {
            Effect?.Invoke(new LabFx(Kam.Position,Kam.Position,0,1));
            foreach(var u in _units)if(!u.Ally&&u.Alive&&Vector2.Distance(u.Position,Kam.Position)<2.3f&&Map.HasLine(Kam.Position,u.Position))Hit(u,2+_levels[5]*2,Kam.Position,true);
        }
        private void ShieldBreak()
        {
            Effect?.Invoke(new LabFx(Kam.Position,Kam.Position,0,4));
            foreach(var u in _units)if(!u.Ally&&u.Alive&&Vector2.Distance(u.Position,Kam.Position)<3&&Map.HasLine(Kam.Position,u.Position))
            {
                var away=u.Position-Kam.Position;if(away.sqrMagnitude<.001f)away=Vector2.right;
                for(int i=0;i<12;i++)Move(u,away,.1f);
            }
        }
    }
}
