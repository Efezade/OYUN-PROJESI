using UnityEngine;

namespace TacticalRPG.Prototypes.Expedition
{
    [CreateAssetMenu(menuName = "TacticalRPG/Prototypes/Expedition")]
    public sealed class ExpeditionConfig : ScriptableObject
    {
        [SerializeField] private float _tileRadius = .8f;
        [SerializeField] private float _moveSpeed = 4.2f;
        [SerializeField] private float _enemySpeed = 1.8f;
        [SerializeField] private int _kamHealth = 65;
        [SerializeField] private int _allyHealth = 48;
        [SerializeField] private int _enemyHealth = 18;
        [SerializeField] private int _bossHealth = 150;
        [SerializeField] private float _mana = 16;
        [SerializeField] private float _manaRegen = 1.3f;
        [SerializeField] private float _attackSeconds = 1.05f;
        [SerializeField] private float _enemyWindup = 1.1f;
        [SerializeField] private float _enemyAttackSeconds = 2.7f;
        [SerializeField] private float _spawnWarningSeconds = 1.8f;
        [SerializeField] private float _packInterval = 16f;
        [SerializeField] private float _height = 1.7f;
        [SerializeField] private float _mudSpeed = .65f;
        [SerializeField] private float _highGroundRange = 1.4f;
        [Header("Encounter variety")]
        [SerializeField] private float _chargeRange = 6f;
        [SerializeField] private float _chargeSpeed = 9f;
        [SerializeField] private float _chargeWarning = 1.4f;
        [SerializeField] private float _chargeRadius = .65f;
        [SerializeField] private float _protectorRadius = 3f;
        [SerializeField] private float _protectorDamageMultiplier = .55f;
        [SerializeField] private float _vortexDuration = 2.4f;
        [SerializeField] private int[] _powerThresholds = { 3, 8, 15, 24, 36, 50, 65 };
        [SerializeField] private Vector3 _cameraOffset = new Vector3(0, 16, -12);
        [SerializeField] private float _cameraSize = 8.8f;
        [Header("Weapon journey prototype")]
        [SerializeField] private bool _weaponJourney;
        [SerializeField] private float _crescentRange=2.7f;
        [SerializeField] private float _spearRange=7.5f;
        [SerializeField] private int _crescentDamage=9;
        [SerializeField] private int _spearDamage=6;
        [SerializeField] private float _weaponAttackSeconds=.85f;
        [SerializeField] private float _spearWidth=.5f;
        [SerializeField] private int _finisherCharge=3;
        [SerializeField] private float _lungeDistance=3.5f;
        [SerializeField] private float _lungeCooldown=4;
        [SerializeField] private float _anchorLife=9;
        [Header("Enemy encounter tactics")]
        [SerializeField] private float _flankSpeed=2.8f;
        [SerializeField] private float _sniperRange=8;
        [SerializeField] private float _sniperWarning=1.35f;
        [SerializeField] private float _sniperCooldown=3.2f;
        [SerializeField] private float _bombWarning=1.6f;
        [SerializeField] private float _bombRadius=1.6f;
        [SerializeField] private float _bombDuration=2.6f;
        [SerializeField] private float _separationRadius=1.1f;
        public float FlankSpeed=>_flankSpeed;
        public float SniperRange=>_sniperRange;
        public float SniperWarning=>_sniperWarning;
        public float SniperCooldown=>_sniperCooldown;
        public float BombWarning=>_bombWarning;
        public float BombRadius=>_bombRadius;
        public float BombDuration=>_bombDuration;
        public float SeparationRadius=>_separationRadius;
        public bool WeaponJourney=>_weaponJourney;
        public float CrescentRange=>_crescentRange;
        public float SpearRange=>_spearRange;
        public int CrescentDamage=>_crescentDamage;
        public int SpearDamage=>_spearDamage;
        public float WeaponAttackSeconds=>_weaponAttackSeconds;
        public float SpearWidth=>_spearWidth;
        public int FinisherCharge=>_finisherCharge;
        public float LungeDistance=>_lungeDistance;
        public float LungeCooldown=>_lungeCooldown;
        public float AnchorLife=>_anchorLife;
        [SerializeField] private Color _grass = new Color(.45f, .53f, .37f);
        [SerializeField] private Color _stone = new Color(.57f, .57f, .47f);
        [SerializeField] private Color _water = new Color(.27f, .54f, .57f);
        [SerializeField] private Color _mud = new Color(.45f, .39f, .29f);
        [SerializeField] private Color _panel = new Color(.055f, .1f, .12f, .94f);
        public float Radius => _tileRadius;
        public float MoveSpeed => _moveSpeed;
        public float EnemySpeed => _enemySpeed;
        public int KamHealth => _kamHealth;
        public int AllyHealth => _allyHealth;
        public int EnemyHealth => _enemyHealth;
        public int BossHealth => _bossHealth;
        public float Mana => _mana;
        public float ManaRegen => _manaRegen;
        public float AttackSeconds => _attackSeconds;
        public float Windup => _enemyWindup;
        public float EnemyAttackSeconds => _enemyAttackSeconds;
        public float SpawnWarning => _spawnWarningSeconds;
        public float PackInterval => _packInterval;
        public float Height => _height;
        public float MudSpeed => _mudSpeed;
        public float HighRange => _highGroundRange;
        public float ChargeRange => _chargeRange;
        public float ChargeSpeed => _chargeSpeed;
        public float ChargeWarning => _chargeWarning;
        public float ChargeRadius => _chargeRadius;
        public float ProtectorRadius => _protectorRadius;
        public float ProtectorDamageMultiplier => _protectorDamageMultiplier;
        public float VortexDuration => _vortexDuration;
        public int[] Thresholds => _powerThresholds;
        public Vector3 CameraOffset => _cameraOffset;
        public float CameraSize => _cameraSize;
        public Color Grass => _grass;
        public Color Stone => _stone;
        public Color Water => _water;
        public Color Mud => _mud;
        public Color Panel => _panel;
    }
}
