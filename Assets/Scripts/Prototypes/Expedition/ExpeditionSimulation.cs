using System;
using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Prototypes.CombatLab;

namespace TacticalRPG.Prototypes.Expedition
{
    public enum ExpeditionPhase { Ready, Fighting, Choosing, Camp, Travel, Won, Lost, WeaponChoice }
    public enum ExpeditionPower { Arc, Conduction, Vortex, Echo, Siphon, Orbit, Ward }
    public static class ExpeditionPowers
    {
        private static readonly string[] Names={"SIÇRAYAN KIVILCIM","SU İLETİMİ","GİRDAP","YANKI PATLAMASI","AKIŞ","YÖRÜNGE","MÜHÜR KALKANI"};
        public static string Name(int id) => Names[id];
        public static string Description(int id) => id switch {
            0=>"Kam'ın normal atışı ek hedefe sıçrar ve işaretler. Üst seviye: iki ek hedef.",
            1=>"Islak hedeflerde zincir daha uzağa yayılır ve +2 hasar verir. Suya çekimle birleşir.",
            2=>"Çekim büyür ve hızlanır. Seviye 2: 2,4 sn kalan girdap düşmanları çeker ve yavaşlatır.",
            3=>"E patlaması kısa gecikmeyle tekrar eder. Üst seviye: iki yankı; ek mana harcamaz.",
            4=>"Zincir tetiklemek mana kazandırır. Aynı anda çok zincirde kazanç sınırı vardır.",
            5=>"Yakına vuran halka kazan. Seviye 2: kaçışın sonunda ek halka tetiklenir.",
            _=>"Büyü kullanınca kalkan kazan. Seviye 2: hasarla kırılınca düşmanları geri iter."};
    }
    public sealed class ExpeditionSpawn
    {
        public LabUnit Unit { get; }
        public float Remaining { get; internal set; }
        public ExpeditionSpawn(LabUnit unit,float delay){Unit=unit;Remaining=delay;}
    }
    [Serializable]
    public sealed class ExpeditionSave
    {
        public int version=1;
        public int seed;
        public int nextStage;
        public int xp;
        public int picks;
        public int[] levels;
        public int[] credited;
        public uint offerState;
        public float elapsed;
        public int weapon;
        public int[] powers;
    }
    public sealed partial class ExpeditionSimulation
    {
        private readonly ExpeditionConfig _config;
        private readonly List<LabUnit> _units=new List<LabUnit>();
        private readonly List<ExpeditionSpawn> _pending=new List<ExpeditionSpawn>();
        private readonly HashSet<int> _credited=new HashSet<int>();
        private readonly int[] _levels=new int[7];
        private readonly int[] _offers=new int[3];
        private readonly Dictionary<int,Vector2> _waypoints=new Dictionary<int,Vector2>();
        private readonly Dictionary<int,float> _routeTimers=new Dictionary<int,float>();
        private uint _offerState;
        private int _focus=-1;
        private int _wave;
        private int _picks;
        private float _packClock;
        private float _orbitClock;
        private float _echoClock;
        private int _echoRemaining;
        private float _siphonClock;
        private float _shieldClock;
        private float _dashClock;
        public ExpeditionMap Map { get; }
        public event Action Changed;
        public event Action<LabFx> Effect;
        public ExpeditionPhase Phase {get;private set;}=ExpeditionPhase.Ready;
        public IReadOnlyList<LabUnit> Units=>_units;
        public IReadOnlyList<ExpeditionSpawn> Pending=>_pending;
        public IReadOnlyList<int> Offers=>_offers;
        public IReadOnlyList<int> Levels=>_levels;
        public LabUnit Kam=>_units[0];
        public int Seed {get;private set;}
        public int Stage {get;private set;}
        public int Wave=>_wave;
        public int XP {get;private set;}
        public int Picks=>_picks;
        public int Chains {get;private set;}
        public int Deaths {get;private set;}
        public float Elapsed {get;private set;}
        public float Mana {get;private set;}
        public float Shield {get;private set;}
        public float PullCooldown {get;private set;}
        public float BurstCooldown {get;private set;}
        public float DashCooldown=>_dashClock;
        public bool Paused {get;private set;}
        public bool MovingAllowed=>!Paused&&(Phase==ExpeditionPhase.Fighting||Phase==ExpeditionPhase.Travel);
        public int NextThreshold=>_picks<_config.Thresholds.Length?_config.Thresholds[_picks]:0;
        public string Hint {get;private set;}="Yeni sefer başlat veya mabet kaydından devam et.";
        public string StageName=>Stage switch {0=>"KIRIK GEÇİT",1=>"SULARIN HAFIZASI",_=>"YÜKSEK MABET"};
        public int Modifier=>Math.Abs((Seed/7+Stage*5)%3);
        public string ModifierName=>Modifier switch {0=>"Akın: fazladan yakın dövüşçü",1=>"Avcılar: daha çok elektrikli nişancı",_=>"Zırhlılar: daha dayanıklı, daha yavaş düşmanlar"};
        public int LivingEnemies {get{int n=0;foreach(var u in _units)if(u.Alive&&!u.Ally)n++;return n;}}
        public ExpeditionSimulation(ExpeditionConfig config){_config=config;Map=new ExpeditionMap(config);}
        public void Start(int seed)
        {
            Seed=Math.Max(1,seed);Stage=0;XP=_picks=Chains=Deaths=0;Elapsed=0;Array.Clear(_levels,0,_levels.Length);_credited.Clear();_offerState=(uint)Seed;
            ResetJourney();
            BeginStage();
            if(IsJourney){Phase=ExpeditionPhase.WeaponChoice;int first=NextRandom(2);_offers[0]=first;_offers[1]=1-first;_offers[2]=-1;Changed?.Invoke();}
        }
        private void BeginStage()
        {
            _units.Clear();_pending.Clear();_waypoints.Clear();_routeTimers.Clear();_focus=-1;
            ResetTactics();
            ResetJourneyCombat();
            _wave=0;_packClock=0;_orbitClock=1;_echoRemaining=0;_echoClock=_siphonClock=_shieldClock=_dashClock=0;
            Shield=PullCooldown=BurstCooldown=0;Mana=_config.Mana;Paused=false;
            var p=Map.Entrance(Stage);
            _units.Add(Unit(0,LabRole.Kam,p,_config.KamHealth,5,5.4f));
            _units.Add(Unit(1,LabRole.Guard,p+new Vector2(.8f,-1),_config.AllyHealth,7,1.8f));
            _units.Add(Unit(2,LabRole.Archer,p+new Vector2(-.6f,1),_config.AllyHealth,3,6));
            if(IsJourney){Kam.Range=WeaponRange;Kam.Damage=WeaponDamage;_units[1].Damage=3;_units[2].Damage=2;}
            Phase=ExpeditionPhase.Fighting;QueuePack();
            Hint="Q ile topla ve işaretle, E ile zincirle. SPACE: kısa kaçış. Güçler alanlar arasında korunur.";Changed?.Invoke();
            if(IsJourney)Hint=(IsMastery?MasterySummary+" · ":"")+"Görev "+(Stage+1)+" · "+WeaponName+". Otomatik saldır; Q silah becerisi, E vuruşlarla dolar.";
        }
        private static LabUnit Unit(int id,LabRole role,Vector2 p,int hp,int damage,float range)=>new LabUnit {Id=id,Role=role,Position=p,Health=hp,MaxHealth=hp,Damage=damage,Range=range,Cooldown=.7f};
        private void QueuePack()
        {
            _wave++;_packClock=0;
            var rng=new System.Random(Seed+Stage*1009+_wave*137);
            int count=4+_wave+(Modifier==0?1:0);if(Stage==2&&_wave==3)count=6;if(IsMastery)count+=Stage*2;
            for(int i=0;i<count;i++)
            {
                bool boss=Stage==2&&_wave==3&&i==0;
                var role=boss?LabRole.Boss:((i+_wave)% (Modifier==1?2:4)==0?LabRole.Gunner:LabRole.Raider);
                var tactic=boss?EnemyTactic.Boss:i==0?EnemyTactic.Charger:Stage>0&&i==2?EnemyTactic.Protector:EnemyTactic.Normal;
                if(IsJourney&&!boss){if(i==1)tactic=EnemyTactic.Flanker;else if(Stage>0&&i==3)tactic=EnemyTactic.Bomber;else if(i>=3&&tactic==EnemyTactic.Normal)tactic=EnemyTactic.Sniper;}
                if(IsMastery&&!boss)
                {
                    // Opening wave showcases power; elites join later waves.
                    tactic=i==0?EnemyTactic.Charger:i==1?EnemyTactic.Flanker:i==4?EnemyTactic.Sniper:EnemyTactic.Normal;
                    if(Stage>0&&_wave>1){if(i==2)tactic=EnemyTactic.Protector;if(i==3)tactic=EnemyTactic.Bomber;}
                    role=tactic==EnemyTactic.Sniper?LabRole.Gunner:LabRole.Raider;
                }
                if(tactic==EnemyTactic.Charger)role=LabRole.Raider;
                if(tactic==EnemyTactic.Protector)role=LabRole.Gunner;
                if(tactic==EnemyTactic.Sniper||tactic==EnemyTactic.Bomber)role=LabRole.Gunner;
                Vector2 p=Map.Center(Stage)+new Vector2(5,0);float best=-1;
                for(int attempt=0;attempt<50;attempt++)
                {
                    double angle=rng.NextDouble()*Math.PI*2;float radius=5.5f+(float)rng.NextDouble()*2;
                    var candidate=Map.Center(Stage)+new Vector2((float)Math.Cos(angle),(float)Math.Sin(angle))*radius;
                    if(!Map.Walkable(candidate)||Map.Ground(candidate)==GroundKind.Ramp)continue;
                    float d=Vector2.Distance(candidate,Kam.Position);if(d>best){best=d;p=candidate;}if(d>6)break;
                }
                int hp=boss?_config.BossHealth:_config.EnemyHealth+Stage*4+(Modifier==2?7:0);
                bool elite=IsMastery&&!boss&&Stage>0&&_wave>1&&(i==0||Stage==2&&i==3);
                if(IsMastery&&!boss)hp=elite?Mathf.CeilToInt(_config.EnemyHealth*_config.EliteHealthMultiplier):_config.EnemyHealth;
                var u=Unit(100+Stage*100+(_wave-1)*20+i,role,p,hp,boss?8:IsMastery?(elite?5+Stage:3):3+Stage,role==LabRole.Gunner?5.5f:boss?2.2f:1.6f);
                RegisterTactic(u,tactic);if(elite)_elites.Add(u.Id);
                _pending.Add(new ExpeditionSpawn(u,_config.SpawnWarning+i*.08f));
            }
            Changed?.Invoke();
        }
        public LabUnit Find(int id){foreach(var u in _units)if(u.Id==id)return u;return null;}
        public bool SetFocus(int id){var u=Find(id);if(!MovingAllowed||u==null||u.Ally||!u.Alive)return false;_focus=id;Hint="Ekip hedefi: "+DisplayName(u);Changed?.Invoke();return true;}
        public float Range(LabUnit u)=>u.Range+(Map.Ground(u.Position)==GroundKind.High&&(!IsJourney||u.Role!=LabRole.Kam||Weapon==JourneyWeapon.Spear)?_config.HighRange:0);
        public bool CanHit(LabUnit a,LabUnit b)=>a.Alive&&b.Alive&&Vector2.Distance(a.Position,b.Position)<=Range(a)&&Map.HasLine(a.Position,b.Position);
        public void TogglePause(){if(Phase==ExpeditionPhase.Fighting||Phase==ExpeditionPhase.Travel){Paused=!Paused;Changed?.Invoke();}}
        public bool Cast(LabSkill skill,Vector2 target)
        {
            if(IsJourney)return CastWeapon(skill,target);
            if(!MovingAllowed||Phase!=ExpeditionPhase.Fighting)return false;
            float cost=skill==LabSkill.Pull?3:4;
            if(Mana<cost||(skill==LabSkill.Pull?PullCooldown:BurstCooldown)>0)return false;
            if(skill==LabSkill.Pull&&(!Map.Contains(target)||Vector2.Distance(Kam.Position,target)>7||!Map.HasLine(Kam.Position,target)))return false;
            Mana-=cost;GrantShield();
            if(skill==LabSkill.Pull)
            {
                PullCooldown=5-_levels[2]*.8f;
                foreach(var u in _units)
                {
                    if(u.Ally||!u.Alive||Vector2.Distance(u.Position,target)>3.2f+_levels[2]*.8f||!Map.HasLine(target,u.Position))continue;
                    var old=u.Position;float length=Mathf.Min(Vector2.Distance(old,target),2+_levels[2]*.6f);
                    for(float step=0;step<length;step+=.1f)Move(u,target-u.Position,Mathf.Min(.1f,length-step));
                    u.Mark=8;Effect?.Invoke(new LabFx(old,u.Position,0,2));
                }
                if(_levels[2]>=2)CreateVortex(target);
                Hint="ÇEKİM · işaretler hazır. Patlama veya sıçrayan atışla birleştir.";
            }
            else {BurstCooldown=5;Burst();_echoRemaining=_levels[3];_echoClock=.6f;Hint="PATLAMA · seçtiğin yankılar ek mana harcamadan sürer.";}
            CheckProgress();Changed?.Invoke();return true;
        }
        private void GrantShield(){if(_levels[6]==0)return;Shield=Mathf.Min(10,Shield+3+_levels[6]*2);_shieldClock=7;}
        private void Burst()
        {
            Effect?.Invoke(new LabFx(Kam.Position,Kam.Position,0,3));
            foreach(var u in _units)if(!u.Ally&&u.Alive&&Vector2.Distance(u.Position,Kam.Position)<3.6f&&Map.HasLine(Kam.Position,u.Position))Hit(u,7,Kam.Position,true);
        }
        public bool Dash(Vector2 direction)
        {
            if(!MovingAllowed||_dashClock>0||direction.sqrMagnitude<.01f)return false;
            for(int i=0;i<18;i++)Move(Kam,direction,.13f);
            if(IsJourney)JourneyDash(direction);
            if(_levels[5]>=2)OrbitPulse();
            _dashClock=3.2f;Effect?.Invoke(new LabFx(Kam.Position,Kam.Position,0,4));return true;
        }
        private void Strike(LabUnit source,LabUnit target)
        {
            if(IsJourney&&source.Role==LabRole.Kam){WeaponStrike(target);return;}
            if(source.Role==LabRole.Archer)target.Mark=8;
            Hit(target,source.Damage,source.Position,source.Role!=LabRole.Archer);
            if(source.Role!=LabRole.Kam||_levels[0]==0)return;
            var last=target;var used=new HashSet<int>{target.Id};
            for(int i=0;i<_levels[0];i++)
            {
                LabUnit next=null;float best=4;
                foreach(var u in _units){if(u.Ally||!u.Alive||used.Contains(u.Id))continue;float d=Vector2.Distance(last.Position,u.Position);if(d<best&&Map.HasLine(last.Position,u.Position)){best=d;next=u;}}
                if(next==null)break;used.Add(next.Id);next.Mark=8;Hit(next,3,last.Position,true);last=next;
            }
        }
        private void Hit(LabUnit target,int damage,Vector2 source,bool trigger)
        {
            if(!target.Alive)return;
            if(target.Role==LabRole.Kam&&Shield>0){int absorb=Mathf.Min(Mathf.CeilToInt(Shield),damage);Shield=Mathf.Max(0,Shield-absorb);damage-=absorb;if(Shield<=0&&(_levels[6]>=2||(IsJourney&&HasPower(6))))ShieldBreak();}
            if(!target.Ally&&IsProtected(target))damage=Mathf.Max(1,Mathf.CeilToInt(damage*_config.ProtectorDamageMultiplier));
            bool chain=trigger&&!target.Ally&&target.Mark>0;if(chain)target.Mark=0;
            int amount=Mathf.Min(damage,target.Health);target.Health-=amount;
            Effect?.Invoke(new LabFx(source,target.Position,amount,0));
            if(!target.Alive&&!target.Ally&&_credited.Add(target.Id))XP+=target.Role==LabRole.Boss?4:1;
            if(!chain)return;
            Chains++;
            if(_levels[4]>0&&_siphonClock<=0){Mana=Mathf.Min(_config.Mana,Mana+_levels[4]);_siphonClock=.4f;}
            bool water=_levels[1]>0&&Map.Ground(target.Position)==GroundKind.Water;
            float radius=2.3f+(water?_levels[1]*1.2f:0);
            Effect?.Invoke(new LabFx(target.Position,target.Position,0,1));
            foreach(var other in _units)
                if(other.Alive&&!other.Ally&&other.Id!=target.Id&&Vector2.Distance(target.Position,other.Position)<radius&&Map.HasLine(target.Position,other.Position))
                    Hit(other,4+(water?_levels[1]*2:0),target.Position,true);
        }
        public void Tick(float dt,Vector2 input)
        {
            if(!MovingAllowed)return;dt=Mathf.Clamp(dt,0,.05f);Elapsed+=dt;
            _dashClock=Mathf.Max(0,_dashClock-dt);_siphonClock-=dt;_shieldClock-=dt;if(_shieldClock<=0)Shield=0;
            Mana=Mathf.Min(_config.Mana,Mana+_config.ManaRegen*dt);PullCooldown=Mathf.Max(0,PullCooldown-dt);BurstCooldown=Mathf.Max(0,BurstCooldown-dt);
            Move(Kam,input,_config.MoveSpeed*dt);
            if(Phase==ExpeditionPhase.Travel)
            {
                for(int i=1;i<3;i++)if(_units[i].Alive)Navigate(_units[i],Kam.Position+new Vector2(-i,.5f*i),_config.MoveSpeed*dt,dt);
                if(Kam.Position.x>Map.Center(Stage+1).x-6){Stage++;BeginStage();}
                return;
            }
            for(int i=_pending.Count-1;i>=0;i--){_pending[i].Remaining-=dt;if(_pending[i].Remaining<=0){_units.Add(_pending[i].Unit);_pending.RemoveAt(i);Changed?.Invoke();}}
            _packClock+=dt;
            TickVortex(dt);
            if(IsJourney)TickJourney(dt);
            if(IsJourney)TickHazards(dt);
            if(_wave<3&&((_packClock>=_config.PackInterval&&LivingEnemies<9)||(LivingEnemies==0&&_pending.Count==0&&_packClock>2)))QueuePack();
            _echoClock-=dt;if(_echoRemaining>0&&_echoClock<=0){_echoRemaining--;_echoClock=.6f;Burst();}
            _orbitClock-=dt;
            if(_levels[5]>0&&_orbitClock<=0)
            {
                _orbitClock=2.6f;OrbitPulse();
            }
            foreach(var u in _units)
            {
                if(!u.Alive)continue;u.Cooldown=Mathf.Max(0,u.Cooldown-dt);u.Mark=Mathf.Max(0,u.Mark-dt);
                if(u.Ally)AllyTick(u,dt);else EnemyTick(u,dt);if(!Kam.Alive)break;
            }
            CheckProgress();
        }
        private LabUnit Target(LabUnit source)
        {
            var f=Find(_focus);if(source.Ally&&f!=null&&f.Alive&&Vector2.Distance(f.Position,Kam.Position)<9)return f;
            LabUnit best=null;float distance=float.MaxValue;
            foreach(var u in _units){if(!u.Alive||u.Ally==source.Ally)continue;float d=(u.Position-source.Position).sqrMagnitude;if(d<distance){distance=d;best=u;}}
            return best;
        }
        private void AllyTick(LabUnit u,float dt)
        {
            var target=Target(u);
            if(u.Role!=LabRole.Kam)
            {
                bool escaping=false;
                foreach(var e in _units)if(!e.Ally&&e.Alive&&e.Windup>0&&Vector2.Distance(u.Position,e.Aim)<(e.Role==LabRole.Boss?2.2f:1.25f))
                {var away=u.Position-e.Aim;if(away.sqrMagnitude<.01f)away=Vector2.up;Move(u,away,_config.MoveSpeed*dt);escaping=true;break;}
                if(!escaping)
                {
                    var follow=Kam.Position+new Vector2(u.Role==LabRole.Guard?1:-1,.7f);
                    bool engage=target!=null&&Vector2.Distance(target.Position,Kam.Position)<7&&Vector2.Distance(u.Position,Kam.Position)<9;
                    var goal=engage?target.Position:follow;float stop=engage?Range(u)*.78f:.7f;
                    if(Vector2.Distance(u.Position,goal)>stop||(engage&&!CanHit(u,target)))Navigate(u,goal,_config.MoveSpeed*.92f*dt,dt);
                }
            }
            if(target!=null&&u.Cooldown<=0&&CanHit(u,target)){Strike(u,target);u.Cooldown=IsJourney&&u.Role==LabRole.Kam?WeaponAttackSeconds:_config.AttackSeconds;}
        }
        private void EnemyTick(LabUnit u,float dt)
        {
            if(IsJourney&&TickEncounter(u,dt))return;
            if(TickTactic(u,dt))return;
            if(u.Windup>0)
            {
                u.Windup-=dt;
                if(u.Windup<=0)
                {
                    Effect?.Invoke(new LabFx(u.Position,u.Aim,0,3));float radius=u.Role==LabRole.Boss?1.7f:.85f;
                    foreach(var a in _units)if(a.Ally&&a.Alive&&Vector2.Distance(a.Position,u.Aim)<radius&&Map.HasLine(u.Position,a.Position))
                        Hit(a,u.Damage+(u.Role==LabRole.Gunner&&Map.Ground(a.Position)==GroundKind.Water?2:0),u.Position,false);
                    u.Cooldown=_config.EnemyAttackSeconds;
                }return;
            }
            var target=Target(u);if(target==null)return;
            if(u.Role==LabRole.Gunner&&u.Cooldown>0&&Vector2.Distance(u.Position,target.Position)<3.2f)
            {Move(u,u.Position-target.Position,_config.EnemySpeed*dt);return;}
            if(!CanHit(u,target))Navigate(u,target.Position,_config.EnemySpeed*(Modifier==2?.8f:1)*dt,dt);
            else if(u.Cooldown<=0&&(!IsJourney||AttackSlotAvailable(u))){u.Aim=target.Position;u.Windup=_config.Windup;}
            if(IsJourney&&u.Windup<=0)SeparateEnemy(u,dt);
        }
        private void Navigate(LabUnit u,Vector2 goal,float distance,float dt)
        {
            _routeTimers.TryGetValue(u.Id,out float time);time-=dt;
            if(time<=0||!_waypoints.ContainsKey(u.Id)){_waypoints[u.Id]=Map.NextStep(u.Position,goal);time=.35f;}
            _routeTimers[u.Id]=time;Move(u,_waypoints[u.Id]-u.Position,distance);
        }
        private void Move(LabUnit u,Vector2 direction,float distance)
        {
            if(direction.sqrMagnitude<.0001f)return;
            if(Map.Ground(u.Position)==GroundKind.Mud)distance*=_config.MudSpeed;
            if(!u.Ally&&InVortex(u.Position))distance*=.5f;
            var delta=direction.normalized*Mathf.Min(distance,direction.magnitude);
            var candidate=u.Position+delta;
            // Closed encounter perimeter; an unfinished fight cannot be bypassed into the next room.
            float right=Phase==ExpeditionPhase.Travel?Map.Center(Mathf.Min(2,Stage+1)).x+10:Map.Center(Stage).x+10;
            if(candidate.x>right)return;
            if(Map.CanStep(u.Position,candidate)){u.Position=candidate;return;}
            candidate=u.Position+new Vector2(delta.x,0);if(Map.CanStep(u.Position,candidate)){u.Position=candidate;return;}
            candidate=u.Position+new Vector2(0,delta.y);if(Map.CanStep(u.Position,candidate))u.Position=candidate;
        }
        private void CheckProgress()
        {
            if(!Kam.Alive){Phase=ExpeditionPhase.Lost;Hint="Kam düştü. Mabetten dönünce güçlerin korunur; aynı düşman ikinci kez gelişim vermez.";Changed?.Invoke();return;}
            if(Phase!=ExpeditionPhase.Fighting)return;
            if(NextThreshold>0&&XP>=NextThreshold){MakeOffers();Phase=ExpeditionPhase.Choosing;Changed?.Invoke();return;}
            if(_wave==3&&LivingEnemies==0&&_pending.Count==0)
            {
                Phase=Stage==2?ExpeditionPhase.Won:ExpeditionPhase.Camp;_echoRemaining=0;
                foreach(var u in _units)if(u.Ally)u.Health=u.MaxHealth;
                Mana=_config.Mana;Hint=Stage==2?"Üç alan tamamlandı. Yeni tohumda farklı bir kombinasyon dene.":"Mabet güvenli. Ekip iyileşti; güçlerin korundu. Devam ederek doğu geçidine yürü.";Changed?.Invoke();
            }
        }
        private int NextRandom(int max){_offerState^=_offerState<<13;_offerState^=_offerState>>17;_offerState^=_offerState<<5;return (int)(_offerState%(uint)max);}
        private void MakeOffers()
        {
            if(IsJourney){MakeJourneyOffers();return;}
            var pool=new List<int>();for(int i=0;i<_levels.Length;i++)if(_levels[i]<2)pool.Add(i);
            // One continuation when available, plus distinct alternatives. No dead max-level offers.
            int continuation=-1;foreach(int p in pool)if(_levels[p]==1){continuation=p;break;}
            for(int i=0;i<3;i++){int at=i==0&&continuation>=0?pool.IndexOf(continuation):NextRandom(pool.Count);_offers[i]=pool[at];pool.RemoveAt(at);}
        }
        public bool Choose(int index)
        {
            if(IsJourney)return ChooseJourney(index);
            if(Phase!=ExpeditionPhase.Choosing||index<0||index>=3)return false;
            _levels[_offers[index]]++;_picks++;Phase=ExpeditionPhase.Fighting;Hint=ExpeditionPowers.Name(_offers[index])+" seçildi; sonraki alanlara taşınır.";CheckProgress();Changed?.Invoke();return true;
        }
        public bool Continue()
        {
            if(Phase!=ExpeditionPhase.Camp)return false;
            Phase=ExpeditionPhase.Travel;Hint="DOĞU GEÇİDİ → Sonraki alana yürü. Kombinasyonun korunuyor.";Changed?.Invoke();return true;
        }
        public bool Retry(){if(Phase!=ExpeditionPhase.Lost)return false;Deaths++;BeginStage();return true;}
        public ExpeditionSave Checkpoint()
        {
            if(Phase!=ExpeditionPhase.Camp)return null;
            if(IsJourney)return JourneyCheckpoint();
            var credited=new int[_credited.Count];_credited.CopyTo(credited);
            return new ExpeditionSave{seed=Seed,nextStage=Stage+1,xp=XP,picks=_picks,levels=(int[])_levels.Clone(),credited=credited,offerState=_offerState,elapsed=Elapsed};
        }
        public bool Load(ExpeditionSave save)
        {
            if(IsJourney)return LoadJourney(save);
            if(save==null||save.version!=1||save.seed<1||save.nextStage<1||save.nextStage>2||save.levels==null||save.levels.Length!=7||save.picks<0||save.picks>_config.Thresholds.Length||save.offerState==0||save.credited==null)return false;
            int total=0;foreach(int v in save.levels){if(v<0||v>2)return false;total+=v;}if(total!=save.picks||save.xp<0)return false;
            Seed=save.seed;Stage=save.nextStage;XP=save.xp;_picks=save.picks;_offerState=save.offerState;Elapsed=save.elapsed;Chains=Deaths=0;
            Array.Copy(save.levels,_levels,7);_credited.Clear();foreach(int id in save.credited)_credited.Add(id);BeginStage();return true;
        }
    }
}
