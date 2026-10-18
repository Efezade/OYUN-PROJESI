using UnityEngine;

namespace TacticalRPG.Prototypes.Expedition
{
    [CreateAssetMenu(menuName="TacticalRPG/Prototypes/Combat Pressure")]
    public sealed class CombatPressureProfile : ScriptableObject
    {
        [SerializeField] private Vector3 _pursuit=new Vector3(1,1.2f,1.4f);
        [SerializeField] private Vector3 _cooldown=new Vector3(1,1.2f,1.45f);
        [SerializeField] private Vector3 _packInterval=new Vector3(16,12,10);
        [SerializeField] private Vector3Int _attackSlots=new Vector3Int(2,3,3);
        [SerializeField] private Vector3Int _damageBonus=new Vector3Int(0,1,2);
        [SerializeField] private float _meteorWarning=1.35f;
        [SerializeField] private float _meteorStagger=.32f;
        [SerializeField] private float _meteorRadius=1.25f;
        [SerializeField] private float _meteorFall=.55f;
        [SerializeField] private float _meteorHeight=8;
        [SerializeField] private int _meteorDamage=16;
        public float Pursuit(int stage)=>_pursuit[Mathf.Clamp(stage,0,2)];
        public float Cooldown(int stage)=>_cooldown[Mathf.Clamp(stage,0,2)];
        public float PackInterval(int stage)=>_packInterval[Mathf.Clamp(stage,0,2)];
        public int AttackSlots(int stage)=>_attackSlots[Mathf.Clamp(stage,0,2)];
        public int DamageBonus(int stage)=>_damageBonus[Mathf.Clamp(stage,0,2)];
        public float MeteorWarning=>_meteorWarning;
        public float MeteorStagger=>_meteorStagger;
        public float MeteorRadius=>_meteorRadius;
        public float MeteorFall=>_meteorFall;
        public float MeteorHeight=>_meteorHeight;
        public int MeteorDamage=>_meteorDamage;
    }
}
