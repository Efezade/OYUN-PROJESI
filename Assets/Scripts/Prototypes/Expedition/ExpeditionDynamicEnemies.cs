using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Prototypes.CombatLab;

namespace TacticalRPG.Prototypes.Expedition
{
    public sealed partial class ExpeditionSimulation
    {
        private sealed class DynamicEnemyState { public int Phase;public float Clock;public Vector2 From; }
        private sealed class CloneState { public int Owner;public float Life; }
        private readonly Dictionary<int,DynamicEnemyState> _dynamicEnemies=new Dictionary<int,DynamicEnemyState>();
        private readonly Dictionary<int,CloneState> _clones=new Dictionary<int,CloneState>();
        private int _cloneId=10000;
        public bool IsClone(LabUnit u)=>u!=null&&_clones.ContainsKey(u.Id);
        public bool Leaping(LabUnit u)=>u!=null&&u.Alive&&TacticOf(u)==EnemyTactic.Leaper&&_dynamicEnemies.TryGetValue(u.Id,out var state)&&state.Phase==2;
        public bool SelfPreparation(LabUnit u)=>u!=null&&(TacticOf(u)==EnemyTactic.Cloner||TacticOf(u)==EnemyTactic.Frenzied&&_dynamicEnemies.TryGetValue(u.Id,out var state)&&state.Phase==1);
        public float LeapRadius=>_config.LeapRadius;
        public bool LeapWarning(LabUnit u)=>u!=null&&u.Alive&&TacticOf(u)==EnemyTactic.Leaper&&(u.Windup>0||Leaping(u));
        public bool Enraged(LabUnit u)=>u!=null&&u.Alive&&TacticOf(u)==EnemyTactic.Frenzied&&_dynamicEnemies.TryGetValue(u.Id,out var state)&&state.Phase==2;
        public float EnemyAirHeight(LabUnit u)
        {
            if(u==null||!u.Alive||TacticOf(u)!=EnemyTactic.Leaper||!_dynamicEnemies.TryGetValue(u.Id,out var state)||state.Phase!=2)return 0;
            return Mathf.Sin(Mathf.Clamp01(state.Clock/_config.LeapTime)*Mathf.PI)*_config.LeapHeight;
        }
        private void ResetDynamicEnemies(){_dynamicEnemies.Clear();_clones.Clear();_cloneId=10000;}
        private void DismissClones(int owner)
        {
            foreach(var pair in _clones)if(pair.Value.Owner==owner){var clone=Find(pair.Key);if(clone!=null){clone.Health=0;clone.Windup=0;}}
            for(int i=_pending.Count-1;i>=0;i--)if(_clones.TryGetValue(_pending[i].Unit.Id,out var state)&&state.Owner==owner)_pending.RemoveAt(i);
        }
        private void MaintainClones(float dt)
        {
            foreach(var pair in _clones)
            {
                pair.Value.Life-=dt;var owner=Find(pair.Value.Owner);
                if(pair.Value.Life>0&&owner!=null&&owner.Alive)continue;
                var unit=Find(pair.Key);if(unit!=null){unit.Health=0;unit.Windup=0;}
                for(int i=_pending.Count-1;i>=0;i--)if(_pending[i].Unit.Id==pair.Key)_pending.RemoveAt(i);
            }
        }
        private int CloneCount(int owner=-1)
        {
            int count=0;foreach(var u in _units)if(u.Alive&&_clones.TryGetValue(u.Id,out var state)&&(owner<0||state.Owner==owner))count++;
            foreach(var p in _pending)if(_clones.TryGetValue(p.Unit.Id,out var state)&&(owner<0||state.Owner==owner))count++;return count;
        }
        private void CreateClones(LabUnit owner)
        {
            int created=0;
            for(int n=0;n<8&&created<2&&CloneCount()<4&&CloneCount(owner.Id)<2;n++)
            {
                var point=owner.Position+Rotate(Vector2.right,n*45)*2;
                if(!Map.Walkable(point)||!Map.HasLine(owner.Position,point))continue;
                var clone=Unit(_cloneId++,LabRole.Raider,point,_config.CloneHealth,2,1.6f);RegisterTactic(clone,EnemyTactic.Clone);
                _clones.Add(clone.Id,new CloneState{Owner=owner.Id,Life=_config.CloneLife});_pending.Add(new ExpeditionSpawn(clone,_config.SpawnWarning));created++;
            }
        }
        private void DynamicImpact(LabUnit u,float radius,int bonus=0)
        {
            Effect?.Invoke(new LabFx(u.Position,u.Aim,0,3));
            foreach(var a in _units)if(a.Ally&&a.Alive&&Vector2.Distance(a.Position,u.Aim)<radius&&Map.HasLine(u.Aim,a.Position))Hit(a,u.Damage+bonus,u.Position,false);
        }
        private bool TickDynamicEnemy(LabUnit u,float dt)
        {
            var kind=TacticOf(u);if(kind!=EnemyTactic.Leaper&&kind!=EnemyTactic.Frenzied&&kind!=EnemyTactic.Cloner)return false;
            if(!_dynamicEnemies.TryGetValue(u.Id,out var state)){state=new DynamicEnemyState();_dynamicEnemies.Add(u.Id,state);}
            if(kind==EnemyTactic.Leaper&&state.Phase==2)
            {
                state.Clock+=dt;u.Position=Vector2.Lerp(state.From,u.Aim,Mathf.Clamp01(state.Clock/_config.LeapTime));
                if(state.Clock>=_config.LeapTime){u.Position=u.Aim;DynamicImpact(u,_config.LeapRadius,2);state.Phase=4;state.Clock=.9f;u.Cooldown=3.2f;}return true;
            }
            if(state.Phase==4){state.Clock-=dt;if(state.Clock<=0)state.Phase=0;return true;}
            if(u.Windup>0)
            {
                u.Windup=Mathf.Max(0,u.Windup-dt);if(u.Windup>0)return true;
                if(kind==EnemyTactic.Leaper){state.Phase=2;state.Clock=0;state.From=u.Position;}
                else if(kind==EnemyTactic.Cloner){CreateClones(u);u.Cooldown=6;state.Phase=4;state.Clock=1;}
                else if(state.Phase==1){state.Phase=2;state.Clock=2;}
                else{DynamicImpact(u,1.1f,1);state.Phase=4;state.Clock=1.3f;u.Cooldown=3;}return true;
            }
            float distance=Vector2.Distance(u.Position,Kam.Position);
            if(kind==EnemyTactic.Frenzied&&state.Phase==2)
            {
                state.Clock-=dt;
                if(state.Clock<=0){state.Phase=4;state.Clock=1.3f;u.Cooldown=3;return true;}
                if(distance<1.8f&&Map.HasLine(u.Position,Kam.Position)&&AttackSlotAvailable(u)){u.Aim=Kam.Position;u.Windup=.65f;state.Phase=3;return true;}
                Navigate(u,Kam.Position,_config.EnemySpeed*_config.FrenzyMultiplier*dt,dt);SeparateEnemy(u,dt);return true;
            }
            if(kind==EnemyTactic.Cloner)
            {
                RangePosition(u,5,dt);
                if(u.Cooldown<=0&&distance<8&&CloneCount()<4&&CloneCount(u.Id)<2&&AttackSlotAvailable(u)){u.Aim=u.Position;u.Windup=1.4f;state.Phase=1;}
            }
            else if(kind==EnemyTactic.Leaper)
            {
                if(u.Cooldown<=0&&distance<7&&Map.Walkable(Kam.Position)&&AttackSlotAvailable(u)){u.Aim=Kam.Position;u.Windup=_config.LeapWarning;state.Phase=1;}
                else if(distance>4)Navigate(u,Kam.Position,_config.EnemySpeed*dt,dt);else Strafe(u,Kam.Position,dt);
            }
            else
            {
                if(u.Cooldown<=0&&distance<7&&AttackSlotAvailable(u)){u.Aim=u.Position;u.Windup=.8f;state.Phase=1;}
                else Navigate(u,Kam.Position,_config.EnemySpeed*dt,dt);
            }
            if(u.Windup<=0)SeparateEnemy(u,dt);return true;
        }
    }
}
