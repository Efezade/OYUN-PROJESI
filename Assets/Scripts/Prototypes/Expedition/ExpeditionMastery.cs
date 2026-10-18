using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Prototypes.CombatLab;

namespace TacticalRPG.Prototypes.Expedition
{
    public sealed partial class ExpeditionSimulation
    {
        private readonly HashSet<int> _elites=new HashSet<int>();
        public bool IsMastery=>IsJourney&&_config.MasteryJourney;
        public int MasteryRank=>IsMastery?Stage:0;
        public bool Ascended=>IsMastery&&Stage>=2;
        public int JourneySaveVersion=>IsMastery?5:3;
        public int AnchorCapacity=>Ascended?8:4;
        public float WeaponAttackSeconds=>_config.WeaponAttackSeconds/(1+MasteryRank*_config.MasterySpeedPerStage);
        public string MasterySummary=>"USTALIK "+MasteryRank+" / 2 · Vuruş "+WeaponDamage+(Ascended?(Weapon==JourneyWeapon.Crescent?" · ÜÇLÜ YARIK":" · MIZRAK YAYLIMI"):" · Sonraki mabette güçlen");
        public int MasteryDamage(int damage)=>Mathf.CeilToInt(damage*(1+MasteryRank*_config.MasteryDamagePerStage));
        public bool IsElite(LabUnit unit)=>unit!=null&&_elites.Contains(unit.Id);
        private static Vector2 Rotate(Vector2 direction,float degrees)
        {float a=degrees*Mathf.Deg2Rad;var d=direction.normalized;return new Vector2(d.x*Mathf.Cos(a)-d.y*Mathf.Sin(a),d.x*Mathf.Sin(a)+d.y*Mathf.Cos(a));}
    }
}
