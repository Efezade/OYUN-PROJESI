using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Prototypes.CombatLab;

namespace TacticalRPG.Prototypes.Expedition
{
    public enum WardenMove { Approach, Transition, Cleave, Rush, Slam, Ring, Summon, Recovery, Exposed, Meteor }
    public sealed partial class ExpeditionSimulation
    {
        private LabUnit _warden;
        private bool _wardenSpawned,_wardenActive;
        private float _wardenClock,_ringRadius;
        private int _wardenCycle,_rushes,_slamIndex,_addId;
        private Vector2 _wardenOrigin,_wardenFacing=Vector2.right;
        private readonly List<Vector2> _wardenSlams=new List<Vector2>(3);
        private readonly HashSet<int> _wardenHits=new HashSet<int>();
        private readonly HashSet<int> _wardenAdds=new HashSet<int>();
        public LabUnit Warden=>_warden;
        public int WardenStage {get;private set;}
        public WardenMove WardenAction {get;private set;}
        public bool WardenVisible=>_warden!=null&&_warden.Alive&&_units.Contains(_warden);
        public bool WardenBusy=>WardenVisible&&WardenAction!=WardenMove.Approach&&WardenAction!=WardenMove.Recovery&&WardenAction!=WardenMove.Exposed;
        public bool WardenExposed=>WardenVisible&&WardenAction==WardenMove.Exposed;
        public Vector2 WardenFacing=>_wardenFacing;
        public Vector2 WardenOrigin=>_wardenOrigin;
        public float WardenRingRadius=>_wardenActive?_ringRadius:_config.WardenRingRange;
        public IReadOnlyList<Vector2> WardenSlams=>_wardenSlams;
        public int WardenSlamIndex=>_slamIndex;
        public bool WardenExecuting=>_wardenActive;
        public int WardenAddCountForView=>WardenAddCount();
        public bool IsWardenAdd(LabUnit u)=>u!=null&&_wardenAdds.Contains(u.Id);
        public string WardenInstruction=>WardenAction==WardenMove.Transition?"AŞAMA DEĞİŞİYOR · Konum al":WardenExposed?"ÇEKİRDEK AÇIK · Q / E ve kombinasyonunu boşalt":WardenAction switch {
            WardenMove.Cleave=>"GENİŞ BİÇİŞ · Arkasına geç veya menzilden çık",
            WardenMove.Rush=>WardenStage>1?"ÇİFTE HÜCUM · İkinci hazırlığı da oku":"HÜCUM · Çizgiden yana çık",
            WardenMove.Slam=>WardenStage==1?"YER DARBESİ · İşaretli zeminden çık":"ÜÇLÜ YIKIM · İşaretli zeminlerden çık",
            WardenMove.Ring=>"KÜL DALGASI · Dışa kaç veya sütunu siper et",
            WardenMove.Summon=>"MUHAFIZ ÇAĞRISI · Alan saldırılarını hazırla",
            WardenMove.Meteor=>WardenStage>1?"GÖK YIKIMI → HÜCUM · İşaretlerden çık, ardından yana kaç":"GÖK YIKIMI · Kilitli işaretlerden uzaklaş",
            _=>WardenAddCount()>0?"BAĞLI ZIRH · Küçük muhafızları temizle":"TOPARLANIYOR · Güvenli vuruş fırsatı"};
        private void ResetWarden(){ResetMeteors();_warden=null;_wardenSpawned=false;_wardenActive=false;_wardenAdds.Clear();_wardenSlams.Clear();_wardenHits.Clear();_wardenCycle=0;_addId=20000;WardenStage=0;WardenAction=WardenMove.Approach;}
        internal void PrepareWardenCapture(int phase)
        {
            // Explicit visual fixture; never reached through the normal play loop.
            _pending.Clear();_units.RemoveAll(u=>!u.Ally);_wave=3;QueueWarden();
            WardenStage=phase;_warden.Health=phase==2?Mathf.RoundToInt(_warden.MaxHealth*_config.WardenThresholds.x):Mathf.RoundToInt(_warden.MaxHealth*_config.WardenThresholds.y);
            Kam.Position=_warden.Position-Vector2.right*3;
            if(!Map.Walkable(Kam.Position))Kam.Position=Map.Entrance(2);
        }
        private void QueueWarden()
        {
            _wardenSpawned=true;_hazards.Clear();var p=Map.Center(2)+Vector2.right*4;
            if(!Map.Walkable(p))p=Map.Center(2);
            _warden=Unit(9000,LabRole.Boss,p,_config.WardenHealth,_config.WardenDamage,_config.WardenCleaveRange);
            RegisterTactic(_warden,EnemyTactic.Boss);_pending.Add(new ExpeditionSpawn(_warden,_config.SpawnWarning));
            WardenStage=1;WardenAction=WardenMove.Transition;_wardenClock=_config.WardenTransition;_wardenFacing=(Kam.Position-p).normalized;
            Hint="KÜL YEMİNLİ MUHAFIZ · Üç aşamalı son karşılaşma";Changed?.Invoke();
        }
        private int WardenAddCount()
        {int n=0;foreach(var u in _units)if(u.Alive&&IsWardenAdd(u))n++;foreach(var p in _pending)if(IsWardenAdd(p.Unit))n++;return n;}
        private void SummonWardenAdds()
        {
            int target=WardenStage==2?2:3;
            for(int i=0;i<12&&WardenAddCount()<target;i++)
            {
                var p=_warden.Position+Rotate(Vector2.right,i*30)*3;
                if(!Map.Walkable(p))continue;
                bool near=false;foreach(var pending in _pending)if(Vector2.Distance(pending.Unit.Position,p)<1.5f)near=true;if(near)continue;
                int index=WardenAddCount();var tactic=_config.Pressure==null?EnemyTactic.Flanker:index==0?EnemyTactic.Sniper:index==1?EnemyTactic.Leaper:EnemyTactic.Flanker;
                var add=Unit(_addId++,tactic==EnemyTactic.Sniper?LabRole.Gunner:LabRole.Raider,p,_config.EnemyHealth,_config.Pressure==null?3:5,tactic==EnemyTactic.Sniper?5.5f:1.6f);_wardenAdds.Add(add.Id);RegisterTactic(add,tactic);_pending.Add(new ExpeditionSpawn(add,_config.SpawnWarning));
            }
        }
        private void DismissWardenAdds(){foreach(var u in _units)if(IsWardenAdd(u)){u.Health=0;u.Windup=0;}for(int i=_pending.Count-1;i>=0;i--)if(IsWardenAdd(_pending[i].Unit))_pending.RemoveAt(i);}
        private void WardenAddDied()
        {
            if(!WardenVisible||WardenAddCount()!=0||WardenAction==WardenMove.Transition)return;
            if(_config.Pressure!=null&&WardenBusy)_wardenAddOpeningQueued=true;
            else OpenWarden();
        }
        private int WardenIncomingDamage(int damage)
        {if(WardenAction==WardenMove.Transition)return 0;return Mathf.CeilToInt(damage*(WardenExposed?_config.WardenExposureDamage:WardenAddCount()>0?_config.WardenShieldDamage:1));}
        private int ClampWardenThreshold(int damage)
        {int floor=WardenStage==1?Mathf.RoundToInt(_warden.MaxHealth*_config.WardenThresholds.x):WardenStage==2?Mathf.RoundToInt(_warden.MaxHealth*_config.WardenThresholds.y):0;return Mathf.Min(damage,Mathf.Max(0,_warden.Health-floor));}
        private void WardenDamaged()
        {
            if(!_warden.Alive){_meteors.Clear();return;}
            float ratio=(float)_warden.Health/_warden.MaxHealth;
            if(WardenStage==1&&_warden.Health<=Mathf.RoundToInt(_warden.MaxHealth*_config.WardenThresholds.x)||WardenStage==2&&_warden.Health<=Mathf.RoundToInt(_warden.MaxHealth*_config.WardenThresholds.y))
            {WardenStage++;WardenAction=WardenMove.Transition;_wardenClock=_config.WardenTransition;_warden.Windup=0;_wardenActive=false;_wardenSlams.Clear();_meteors.Clear();_wardenFollowup=false;_wardenAddOpeningQueued=false;_wardenCycle=0;DismissWardenAdds();Hint="MUHAFIZ · AŞAMA "+WardenStage;}
        }
        private void OpenWarden(){_wardenAddOpeningQueued=false;WardenAction=WardenMove.Exposed;_wardenClock=_config.WardenExposure;_warden.Windup=0;_wardenActive=false;_wardenSlams.Clear();_meteors.Clear();}
        private void RecoverWarden(){if(_wardenAddOpeningQueued){OpenWarden();return;}WardenAction=WardenMove.Recovery;_wardenClock=_config.WardenRecovery;_warden.Windup=0;_wardenActive=false;_wardenSlams.Clear();_meteors.Clear();}
        private void PrepareWarden(WardenMove move)
        {
            WardenAction=move;_wardenActive=false;_wardenOrigin=_warden.Position;_wardenFacing=(Kam.Position-_warden.Position).normalized;if(_wardenFacing.sqrMagnitude<.1f)_wardenFacing=Vector2.right;
            _warden.Aim=_warden.Position+_wardenFacing*_config.WardenRushRange;_warden.Windup=_config.WardenWarning;_wardenHits.Clear();_wardenSlams.Clear();
            if(move==WardenMove.Rush)_rushes=WardenStage>1?2:1;
            if(move==WardenMove.Meteor)PrepareMeteors();
            if(move==WardenMove.Slam){_slamIndex=0;_wardenSlams.Add(Kam.Position);if(WardenStage>=2)for(int i=-1;i<=1;i+=2){var p=Kam.Position+Rotate(_wardenFacing,90)*i*2.5f;if(Map.Walkable(p))_wardenSlams.Add(p);}}
        }
        private void WardenArea(Vector2 center,float radius)
        {foreach(var a in _units)if(a.Ally&&a.Alive&&Vector2.Distance(a.Position,center)<radius&&Map.HasLine(center,a.Position))Hit(a,_config.WardenDamage,center,false);}
        private void TickWarden(float dt)
        {
            if(!_warden.Alive)return;
            if(WardenAction==WardenMove.Meteor){TickMeteors(dt);return;}
            if(WardenAction==WardenMove.Transition)
            {if((_wardenClock-=dt)<=0){if(WardenStage>1)SummonWardenAdds();if(_config.Pressure!=null){_wardenFollowup=false;PrepareWarden(WardenMove.Meteor);}else RecoverWarden();}return;}
            if(WardenAction==WardenMove.Recovery||WardenAction==WardenMove.Exposed)
            {if((_wardenClock-=dt)<=0)WardenAction=WardenMove.Approach;return;}
            if(WardenAction==WardenMove.Approach)
            {
                var move=WardenStage==1?(_wardenCycle%3==0?WardenMove.Cleave:_wardenCycle%3==1?WardenMove.Rush:WardenMove.Slam):WardenStage==2?(_wardenCycle%3==0?WardenMove.Rush:_wardenCycle%3==1?WardenMove.Cleave:WardenMove.Summon):(_wardenCycle%4==0?WardenMove.Ring:_wardenCycle%4==1?WardenMove.Slam:_wardenCycle%4==2?WardenMove.Rush:WardenMove.Summon);
                if(move==WardenMove.Summon&&WardenAddCount()>0)move=WardenMove.Slam;
                if(_config.Pressure!=null)
                {
                    move=WardenStage==1?((_wardenCycle%4) switch{0=>WardenMove.Cleave,1=>WardenMove.Rush,2=>WardenMove.Meteor,_=>WardenMove.Slam}):
                        ((_wardenCycle%5) switch{0=>WardenMove.Rush,1=>WardenMove.Cleave,2=>WardenMove.Meteor,3=>WardenStage==3?WardenMove.Ring:WardenMove.Slam,_=>WardenMove.Summon});
                    if(move==WardenMove.Summon&&WardenAddCount()>0)move=WardenMove.Slam;
                }
                float distance=Vector2.Distance(_warden.Position,Kam.Position);float range=move==WardenMove.Cleave?_config.WardenCleaveRange*.9f:_config.WardenRushRange;
                _wardenFacing=(Kam.Position-_warden.Position).normalized;
                if(distance>range||!Map.HasLine(_warden.Position,Kam.Position))Navigate(_warden,Kam.Position,_config.WardenWalkSpeed[WardenStage-1]*dt,dt);
                else if(AttackSlotAvailable(_warden)){_wardenCycle++;_wardenFollowup=false;PrepareWarden(move);}return;
            }
            if(_warden.Windup>0)
            {
                _warden.Windup=Mathf.Max(0,_warden.Windup-dt);if(_warden.Windup>0)return;
                _wardenActive=true;
                if(WardenAction==WardenMove.Cleave)
                {foreach(var a in _units){var d=a.Position-_warden.Position;if(a.Ally&&a.Alive&&d.magnitude<=_config.WardenCleaveRange&&Vector2.Dot(d.normalized,_wardenFacing)>=Mathf.Cos(_config.WardenCleaveAngle*Mathf.Deg2Rad)&&Map.HasLine(_warden.Position,a.Position))Hit(a,_config.WardenDamage+2,_warden.Position,false);}RecoverWarden();}
                else if(WardenAction==WardenMove.Summon){SummonWardenAdds();RecoverWarden();}
                else if(WardenAction==WardenMove.Ring){_ringRadius=0;_wardenOrigin=_warden.Position;}
                else if(WardenAction==WardenMove.Slam)_wardenClock=0;
                return;
            }
            if(WardenAction==WardenMove.Rush)
            {
                bool ended=false;float remaining=_config.WardenRushSpeed*dt;
                while(remaining>0)
                {var d=_warden.Aim-_warden.Position;float step=Mathf.Min(.1f,remaining,d.magnitude);if(step<.001f||!Map.CanStep(_warden.Position,_warden.Position+d.normalized*step)){ended=true;break;}_warden.Position+=d.normalized*step;remaining-=step;
                    foreach(var a in _units)if(a.Ally&&a.Alive&&!_wardenHits.Contains(a.Id)&&Vector2.Distance(a.Position,_warden.Position)<_config.ChargeRadius&&Map.HasLine(_warden.Position,a.Position)){_wardenHits.Add(a.Id);Hit(a,_config.WardenDamage,_warden.Position,false);}}
                if(ended){if(--_rushes>0){_wardenOrigin=_warden.Position;_wardenFacing=(Kam.Position-_warden.Position).normalized;_warden.Aim=_warden.Position+_wardenFacing*_config.WardenRushRange;_warden.Windup=_config.WardenWarning*.8f;_wardenActive=false;_wardenHits.Clear();}else CompleteWardenHeavy();}
            }
            else if(WardenAction==WardenMove.Slam)
            {if((_wardenClock-=dt)<=0){WardenArea(_wardenSlams[_slamIndex],_config.WardenSlamRadius);_slamIndex++;_wardenClock=_config.WardenSlamInterval;if(_slamIndex>=_wardenSlams.Count)OpenWarden();}}
            else if(WardenAction==WardenMove.Ring)
            {
                _ringRadius+=_config.WardenRingSpeed*dt;
                foreach(var a in _units)if(a.Ally&&a.Alive&&!_wardenHits.Contains(a.Id)&&Mathf.Abs(Vector2.Distance(a.Position,_wardenOrigin)-_ringRadius)<=_config.WardenRingWidth&&Map.HasLine(_wardenOrigin,a.Position)){_wardenHits.Add(a.Id);Hit(a,_config.WardenDamage,_wardenOrigin,false);}
                if(_ringRadius>_config.WardenRingRange)CompleteWardenHeavy();
            }
        }
    }
}
