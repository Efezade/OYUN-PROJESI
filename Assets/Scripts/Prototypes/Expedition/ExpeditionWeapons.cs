using System;
using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Prototypes.CombatLab;

namespace TacticalRPG.Prototypes.Expedition
{
    public enum JourneyWeapon { Crescent, Spear }
    public sealed class SpearAnchor
    {
        public Vector2 Position {get;internal set;}
        public float Life {get;internal set;}
    }
    public sealed partial class ExpeditionSimulation
    {
        private readonly bool[] _powers=new bool[9];
        private readonly List<SpearAnchor> _anchors=new List<SpearAnchor>(4);
        private int _weaponHits;
        private float _bladeClock,_discClock,_weaponEcho;
        private Vector2 _echoDirection;
        public bool IsJourney=>_config.WeaponJourney;
        public JourneyWeapon Weapon {get;private set;}
        public int Charge {get;private set;}
        public float BladeAngle {get;private set;}
        public Vector2 Facing {get;private set;}=Vector2.right;
        public int ChargeRequired=>_config.FinisherCharge;
        public IReadOnlyList<SpearAnchor> Anchors=>_anchors;
        public bool HasPower(int id)=>id>=0&&id<_powers.Length&&_powers[id];
        public string WeaponName=>Weapon==JourneyWeapon.Crescent?"HİLAL DARBESİ":"DELİCİ MIZRAK";
        public float WeaponRange=>Weapon==JourneyWeapon.Crescent?_config.CrescentRange:_config.SpearRange;
        public int WeaponDamage=>Weapon==JourneyWeapon.Crescent?_config.CrescentDamage:_config.SpearDamage;
        public bool CanWeaponQ=>MovingAllowed&&Phase==ExpeditionPhase.Fighting&&PullCooldown<=0&&(Weapon==JourneyWeapon.Crescent||_anchors.Count>0);
        public bool CanWeaponE=>MovingAllowed&&Phase==ExpeditionPhase.Fighting&&Charge>=ChargeRequired&&BurstCooldown<=0;
        public string QName=>Weapon==JourneyWeapon.Crescent?"ATIL VE BİÇ":"GERİ ÇAĞIR";
        public string EName=>Weapon==JourneyWeapon.Crescent?"HİLAL BİTİRİŞİ":"MESAFE AÇ";
        public string PowerName(int id)=>id switch {0=>Weapon==JourneyWeapon.Crescent?"ÜÇÜNCÜ DARBE":"ÇİFTE MIZRAK",1=>"DÖNEN BIÇAKLAR",2=>"ÇEKİM ALANI",3=>"BİTİRİCİ YANKISI",4=>"SEKEN DİSK",5=>"KAÇIŞ İZİ",6=>"KIRILAN MÜHÜR",7=>"KIRILMA",_=>"KARŞI HAMLE"};
        public string PowerDescription(int id)=>id switch {
            0=>Weapon==JourneyWeapon.Crescent?"Her üçüncü temel darbe uzakta da kesen bir yarık açar. Düşmanları önünde hizala.":"Atışın iki kola ayrılır. Daha geniş hattı deler ve iki mızrak bırakır; Q ile geri çağır.",
            1=>"Yeni otomatik yetenek: çevrende iki bıçak döner. Düşmanı bıçakların temas mesafesinde tut.",
            2=>"Q, bittiği noktada 2,4 sn çeken ve yavaşlatan alan bırakır. Diğer saldırıların için düşmanları toplar.",
            3=>"E bitirişi kısa gecikmeyle tekrar eder. Yeni vuruş gücü harcamaz; hareket ederek ikinci etkiyi konumlandır.",
            4=>"Yeni otomatik yetenek: yakındaki düşmana bir disk atılır ve ikinci düşmana seker. Yeni bir uzak destek saldırısı.",
            5=>"SPACE kaçışı geçtiğin hattaki düşmanları keser. Peşindeki düşmanları kaçış yoluna hizala.",
            6=>"E ayrıca kalkan verir. Kalkan hasarla kırılınca yakın düşmanları geri iter. Süresi dolması tetiklemez.",
            7=>Weapon==JourneyWeapon.Crescent?"Üçüncü darbenin yarığı, çarptığı hedeflerde ek patlama açar. Üçüncü Darbe ile birleşir.":"Geri çağrılan mızrakların başlangıcında patlama oluşur. Çifte Mızrak ile birleşir.",
            _=>"E kaçışını tekrar hazırlar ve önündeki düşmanları geri iter. Yakın bitirişten çıkmak veya uzak mesafeyi korumak için."};
        private void ResetJourney(){Array.Clear(_powers,0,_powers.Length);Weapon=JourneyWeapon.Crescent;ResetJourneyCombat();}
        private void ResetJourneyCombat(){_anchors.Clear();Charge=0;_weaponHits=0;_bladeClock=0;_discClock=1;_weaponEcho=0;BladeAngle=0;Facing=Vector2.right;}
        private bool ChooseJourney(int index)
        {
            if(index<0||index>=3||_offers[index]<0)return false;
            if(Phase==ExpeditionPhase.WeaponChoice){Weapon=(JourneyWeapon)_offers[index];BeginStage();Changed?.Invoke();return true;}
            if(Phase!=ExpeditionPhase.Choosing||HasPower(_offers[index]))return false;
            int id=_offers[index];_powers[id]=true;_picks++;Phase=ExpeditionPhase.Fighting;
            Hint=PowerName(id)+" KAZANILDI · Sonraki görevlerde de korunur.";CheckProgress();Changed?.Invoke();return true;
        }
        private void MakeJourneyOffers()
        {
            var pool=new List<int>();for(int i=0;i<_powers.Length;i++)if(!_powers[i]&&(i!=7||HasPower(0)))pool.Add(i);
            for(int i=0;i<3;i++)_offers[i]=-1;
            // The first reward always contains a new automatic skill and the weapon transformation.
            if(_picks==0){_offers[0]=NextRandom(2)==0?1:4;_offers[1]=0;pool.Remove(_offers[0]);pool.Remove(0);_offers[2]=pool[NextRandom(pool.Count)];return;}
            for(int i=0;i<3&&pool.Count>0;i++){int at=NextRandom(pool.Count);_offers[i]=pool[at];pool.RemoveAt(at);}
        }
        private static float SegmentDistance(Vector2 p,Vector2 a,Vector2 b)
        {var d=b-a;float t=d.sqrMagnitude<.0001f?0:Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude);return Vector2.Distance(p,a+d*t);}
        private bool WeaponLine(Vector2 from,Vector2 to,int damage,float width,int kind,bool shatter=false)
        {
            Effect?.Invoke(new LabFx(from,to,0,kind));bool hit=false;
            // Damage feedback is separate from the line/arc silhouette.
            foreach(var u in _units)if(!u.Ally&&u.Alive&&SegmentDistance(u.Position,from,to)<=width&&Map.HasLine(from,u.Position))
            {Hit(u,damage,from,false);hit=true;if(shatter)WeaponBlast(u.Position,2,4);}
            return hit;
        }
        private void WeaponBlast(Vector2 point,float radius,int damage)
        {
            Effect?.Invoke(new LabFx(point,point,0,13));
            foreach(var u in _units)if(!u.Ally&&u.Alive&&Vector2.Distance(point,u.Position)<radius&&Map.HasLine(point,u.Position))Hit(u,damage,point,false);
        }
        private bool CrescentSweep(Vector2 direction,float radius,int damage)
        {
            Facing=direction.normalized;Effect?.Invoke(new LabFx(Kam.Position,Kam.Position+Facing*radius,0,10));bool hit=false;
            foreach(var u in _units)
            {
                var delta=u.Position-Kam.Position;
                if(u.Ally||!u.Alive||delta.magnitude>radius||Vector2.Dot(delta.normalized,Facing)<.35f||!Map.HasLine(Kam.Position,u.Position))continue;
                Hit(u,damage,Kam.Position,false);hit=true;
            }
            return hit;
        }
        private void FireSpear(Vector2 direction)
        {
            Facing=direction.normalized;Vector2 end=Kam.Position;
            for(float d=.15f;d<=Range(Kam);d+=.15f){var p=Kam.Position+Facing*d;if(!Map.Contains(p)||!Map.HasLine(Kam.Position,p))break;end=p;}
            WeaponLine(Kam.Position,end,WeaponDamage,_config.SpearWidth,11);
            if(_anchors.Count==4)_anchors.RemoveAt(0);
            _anchors.Add(new SpearAnchor{Position=end,Life=_config.AnchorLife});
        }
        private void WeaponStrike(LabUnit target)
        {
            var direction=target.Position-Kam.Position;if(direction.sqrMagnitude<.001f)direction=Facing;
            if(Weapon==JourneyWeapon.Crescent)
            {
                if(!CrescentSweep(direction,WeaponRange,WeaponDamage))return;
                _weaponHits++;
                if(HasPower(0)&&_weaponHits%3==0)WeaponLine(Kam.Position,Kam.Position+Facing*6,WeaponDamage,.7f,12,HasPower(7));
            }
            else
            {
                FireSpear(direction);
                if(HasPower(0)){var d=direction.normalized;FireSpear(new Vector2(d.x*.96f-d.y*.28f,d.x*.28f+d.y*.96f));}
                _weaponHits++;
            }
            Charge=Mathf.Min(ChargeRequired,Charge+1);
        }
        private bool CastWeapon(LabSkill skill,Vector2 target)
        {
            if(skill==LabSkill.Pull)
            {
                if(!CanWeaponQ)return false;
                if(Weapon==JourneyWeapon.Crescent)
                {
                    var direction=target-Kam.Position;if(direction.sqrMagnitude<.01f)direction=Facing;
                    for(float moved=0;moved<_config.LungeDistance;moved+=.1f)Move(Kam,direction,.1f);
                    CrescentSweep(direction,WeaponRange,WeaponDamage+3);PullCooldown=_config.LungeCooldown;
                    if(HasPower(2))CreateVortex(Kam.Position+direction.normalized);
                }
                else
                {
                    var well=_anchors[_anchors.Count-1].Position;
                    foreach(var anchor in _anchors){WeaponLine(anchor.Position,Kam.Position,WeaponDamage+2,_config.SpearWidth,12);if(HasPower(7))WeaponBlast(anchor.Position,2,4);}
                    _anchors.Clear();PullCooldown=.8f;if(HasPower(2))CreateVortex(well);
                }
            }
            else
            {
                if(!CanWeaponE)return false;Charge=0;BurstCooldown=.8f;WeaponFinisher();
                if(HasPower(3)){_weaponEcho=.6f;_echoDirection=Facing;}
            }
            CheckProgress();Changed?.Invoke();return true;
        }
        private void WeaponFinisher()
        {
            if(Weapon==JourneyWeapon.Crescent)WeaponBlast(Kam.Position,3.8f,WeaponDamage*2);
            else {WeaponBlast(Kam.Position,3.2f,5);ShieldBreak();Shield=Mathf.Max(Shield,4);_shieldClock=4;}
            if(HasPower(6)){Shield=Mathf.Max(Shield,8);_shieldClock=6;}
            if(HasPower(8)){_dashClock=0;ShieldBreak();}
        }
        private void JourneyDash(Vector2 direction)
        {if(HasPower(5))WeaponLine(Kam.Position-direction.normalized*2.3f,Kam.Position,10,.8f,12);}
        private void TickJourney(float dt)
        {
            for(int i=_anchors.Count-1;i>=0;i--){_anchors[i].Life-=dt;if(_anchors[i].Life<=0)_anchors.RemoveAt(i);}
            if(_weaponEcho>0){_weaponEcho-=dt;if(_weaponEcho<=0){Facing=_echoDirection;WeaponFinisher();}}
            BladeAngle+=dt*2.4f;_bladeClock-=dt;_discClock-=dt;
            if(HasPower(1)&&_bladeClock<=0)
            {
                _bladeClock=.35f;var axis=new Vector2(Mathf.Cos(BladeAngle),Mathf.Sin(BladeAngle))*2;
                foreach(var u in _units)if(!u.Ally&&u.Alive&&Map.HasLine(Kam.Position,u.Position)&&(Vector2.Distance(u.Position,Kam.Position+axis)<.85f||Vector2.Distance(u.Position,Kam.Position-axis)<.85f))Hit(u,5,Kam.Position,false);
            }
            if(HasPower(4)&&_discClock<=0)
            {
                _discClock=2.8f;var target=Target(Kam);if(target==null||Vector2.Distance(target.Position,Kam.Position)>7||!Map.HasLine(Kam.Position,target.Position))return;
                Hit(target,6,Kam.Position,false);Effect?.Invoke(new LabFx(Kam.Position,target.Position,0,14));
                foreach(var u in _units)if(!u.Ally&&u.Alive&&u.Id!=target.Id&&Vector2.Distance(u.Position,target.Position)<4&&Map.HasLine(target.Position,u.Position)){Hit(u,6,target.Position,false);Effect?.Invoke(new LabFx(target.Position,u.Position,0,14));break;}
            }
        }
        private ExpeditionSave JourneyCheckpoint()
        {
            var credited=new int[_credited.Count];_credited.CopyTo(credited);var powers=new int[9];for(int i=0;i<9;i++)powers[i]=_powers[i]?1:0;
            return new ExpeditionSave{version=3,seed=Seed,nextStage=Stage+1,xp=XP,picks=_picks,levels=new int[7],powers=powers,weapon=(int)Weapon,credited=credited,offerState=_offerState,elapsed=Elapsed};
        }
        private bool LoadJourney(ExpeditionSave save)
        {
            if(save==null||save.version!=3||save.seed<1||save.nextStage<1||save.nextStage>2||save.weapon<0||save.weapon>1||save.powers==null||save.powers.Length!=9||save.credited==null||save.offerState==0||save.xp<0||save.picks<0||save.picks>_config.Thresholds.Length||float.IsNaN(save.elapsed)||float.IsInfinity(save.elapsed)||save.elapsed<0)return false;
            int total=0;foreach(int n in save.powers){if(n<0||n>1)return false;total+=n;}if(total!=save.picks||(save.powers[7]>0&&save.powers[0]==0))return false;
            ResetJourney();Array.Clear(_levels,0,_levels.Length);Weapon=(JourneyWeapon)save.weapon;Seed=save.seed;Stage=save.nextStage;XP=save.xp;_picks=save.picks;_offerState=save.offerState;Elapsed=save.elapsed;Chains=Deaths=0;
            for(int i=0;i<9;i++)_powers[i]=save.powers[i]>0;_credited.Clear();foreach(int id in save.credited)_credited.Add(id);BeginStage();return true;
        }
    }
}
