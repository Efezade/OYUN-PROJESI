using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Prototypes.CombatLab;

namespace TacticalRPG.Prototypes.Expedition
{
    public sealed class EncounterHazard
    {
        public Vector2 Position {get;internal set;}
        public float Life {get;internal set;}
        internal float Pulse;
        internal int Damage;
    }
    public sealed partial class ExpeditionSimulation
    {
        private readonly List<EncounterHazard> _hazards=new List<EncounterHazard>(2);
        public IReadOnlyList<EncounterHazard> Hazards=>_hazards;
        public float HazardRadius=>_config.BombRadius;
        public bool SniperAiming(LabUnit u)=>u.Alive&&TacticOf(u)==EnemyTactic.Sniper&&u.Windup>0;
        private void ResetEncounters(){_hazards.Clear();}
        private bool AttackSlotAvailable(LabUnit source)
        {
            int attacks=0;foreach(var u in _units)if(!u.Ally&&u.Alive&&u.Id!=source.Id&&(u.Windup>0||ChargeActive(u)||Leaping(u)||u==_warden&&WardenBusy))attacks++;
            // At most two enemy preparations/rushes at once, including the boss.
            return attacks<2;
        }
        private void SeparateEnemy(LabUnit u,float dt)
        {
            Vector2 push=Vector2.zero;
            foreach(var other in _units)
            {
                if(other.Ally||!other.Alive||other.Id==u.Id)continue;
                var delta=u.Position-other.Position;float d=delta.magnitude;
                if(d>=_config.SeparationRadius)continue;
                if(d<.01f)delta=u.Id<other.Id?Vector2.left:Vector2.right;
                push+=delta.normalized*(_config.SeparationRadius-d);
            }
            if(push.sqrMagnitude>.001f)Move(u,push,dt*1.3f);
        }
        private void Strafe(LabUnit u,Vector2 target,float dt,float speed=1)
        {
            var radial=u.Position-target;if(radial.sqrMagnitude<.001f)radial=Vector2.right;
            var side=new Vector2(-radial.y,radial.x).normalized*(u.Id%2==0?1:-1);
            var before=u.Position;Move(u,side,_config.EnemySpeed*speed*dt);
            if(Vector2.Distance(before,u.Position)<.001f)Move(u,-side,_config.EnemySpeed*speed*dt);
        }
        private void RangePosition(LabUnit u,float desired,float dt)
        {
            float distance=Vector2.Distance(u.Position,Kam.Position);
            if(distance<desired-1)
            {
                var before=u.Position;Move(u,u.Position-Kam.Position,_config.EnemySpeed*dt);
                if(Vector2.Distance(before,u.Position)<.001f)Strafe(u,Kam.Position,dt);
            }
            else if(distance>desired+1||!Map.HasLine(u.Position,Kam.Position))Navigate(u,Kam.Position,_config.EnemySpeed*dt,dt);
            else Strafe(u,Kam.Position,dt,.65f);
        }
        private bool TickEncounter(LabUnit u,float dt)
        {
            var tactic=TacticOf(u);if(tactic==EnemyTactic.Clone)tactic=EnemyTactic.Flanker;
            if(tactic!=EnemyTactic.Flanker&&tactic!=EnemyTactic.Sniper&&tactic!=EnemyTactic.Bomber&&tactic!=EnemyTactic.Protector)return false;
            if(u.Windup>0)
            {
                u.Windup=Mathf.Max(0,u.Windup-dt);if(u.Windup>0)return true;
                if(tactic==EnemyTactic.Sniper)
                {
                    Effect?.Invoke(new LabFx(u.Position,u.Aim,0,15));
                    foreach(var a in _units)if(a.Ally&&a.Alive&&SegmentDistance(a.Position,u.Position,u.Aim)<.55f&&Map.HasLine(u.Position,a.Position))Hit(a,u.Damage+2,u.Position,false);
                }
                else if(tactic==EnemyTactic.Bomber)
                {
                    if(_hazards.Count<2)_hazards.Add(new EncounterHazard{Position=u.Aim,Life=_config.BombDuration,Damage=u.Damage,Pulse=0});
                }
                else
                {
                    Effect?.Invoke(new LabFx(u.Position,u.Aim,0,3));
                    foreach(var a in _units)if(a.Ally&&a.Alive&&Vector2.Distance(a.Position,u.Aim)<1.1f&&Map.HasLine(u.Position,a.Position))Hit(a,u.Damage,u.Position,false);
                }
                u.Cooldown=tactic==EnemyTactic.Bomber?4:_config.SniperCooldown;return true;
            }
            if(tactic==EnemyTactic.Protector)
            {
                LabUnit front=null;float closest=float.MaxValue;
                foreach(var other in _units)if(!other.Ally&&other.Alive&&other.Id!=u.Id&&TacticOf(other)!=EnemyTactic.Protector)
                {float d=Vector2.Distance(other.Position,Kam.Position);if(d<closest){closest=d;front=other;}}
                if(front!=null)
                {
                    var away=(front.Position-Kam.Position).normalized;var goal=front.Position+away*1.5f;
                    if(Vector2.Distance(u.Position,goal)>1)Navigate(u,goal,_config.EnemySpeed*dt,dt);
                }
                else RangePosition(u,3,dt);
                if(u.Cooldown<=0&&Vector2.Distance(u.Position,Kam.Position)<3&&Map.HasLine(u.Position,Kam.Position)&&AttackSlotAvailable(u)){u.Aim=Kam.Position;u.Windup=_config.Windup;}
            }
            else if(tactic==EnemyTactic.Flanker)
            {
                float distance=Vector2.Distance(u.Position,Kam.Position);
                if(distance<2.4f&&u.Cooldown<=0&&Map.HasLine(u.Position,Kam.Position)&&AttackSlotAvailable(u)){u.Aim=Kam.Position;u.Windup=.8f;return true;}
                if(distance<3.1f&&u.Cooldown>0)Strafe(u,Kam.Position,dt,1.2f);
                else
                {
                    var radial=(u.Position-Kam.Position).normalized;var side=new Vector2(-radial.y,radial.x)*(u.Id%2==0?1:-1);
                    var goal=Kam.Position+side*(distance>4?2.2f:.9f);
                    Navigate(u,goal,_config.FlankSpeed*dt,dt);
                }
            }
            else
            {
                float desired=tactic==EnemyTactic.Sniper?6:5;
                // Recover visibly after shooting. Chasing a shooter always gains distance.
                if(u.Cooldown<_config.SniperCooldown-.8f)RangePosition(u,desired,dt);
                if(u.Cooldown<=0&&Vector2.Distance(u.Position,Kam.Position)<=_config.SniperRange&&Map.HasLine(u.Position,Kam.Position)&&AttackSlotAvailable(u))
                {
                    if(tactic==EnemyTactic.Bomber&&_hazards.Count>=2)return true;
                    var delta=Kam.Position-u.Position;
                    u.Aim=tactic==EnemyTactic.Sniper?u.Position+delta.normalized*_config.SniperRange:Kam.Position;
                    u.Windup=tactic==EnemyTactic.Sniper?_config.SniperWarning:_config.BombWarning;
                }
            }
            if(u.Windup<=0)SeparateEnemy(u,dt);return true;
        }
        private void TickHazards(float dt)
        {
            for(int i=_hazards.Count-1;i>=0;i--)
            {
                var h=_hazards[i];h.Life-=dt;if(h.Life<=0){_hazards.RemoveAt(i);continue;}
                h.Pulse-=dt;if(h.Pulse>0)continue;h.Pulse=.65f;
                foreach(var a in _units)if(a.Ally&&a.Alive&&Vector2.Distance(a.Position,h.Position)<HazardRadius&&Map.HasLine(h.Position,a.Position))Hit(a,h.Damage,h.Position,false);
            }
        }
    }
}
