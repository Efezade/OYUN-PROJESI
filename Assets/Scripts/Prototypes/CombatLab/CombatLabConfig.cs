using UnityEngine;

namespace TacticalRPG.Prototypes.CombatLab
{
    [CreateAssetMenu(menuName = "TacticalRPG/Prototypes/Combat Lab")]
    public sealed class CombatLabConfig : ScriptableObject
    {
        [Header("Shared encounter — both variants")]
        [SerializeField, Range(4, 7)] private int _arenaRadius = 5;
        [SerializeField, Min(.1f)] private float _tileRadius = .8f;
        [SerializeField] private int _kamHealth = 32;
        [SerializeField] private int _guardHealth = 28;
        [SerializeField] private int _archerHealth = 20;
        [SerializeField] private int _enemyHealth = 12;
        [SerializeField] private int _bossHealth = 55;
        [SerializeField] private int _mana = 12;
        [SerializeField] private int _pullCost = 3;
        [SerializeField] private int _burstCost = 4;
        [SerializeField] private int _chainDamage = 4;
        [SerializeField] private float _chainRadius = 2.1f;
        [SerializeField] private float _pullRadius = 2.8f;
        [SerializeField] private float _burstRadius = 3.3f;
        [SerializeField] private int _burstDamage = 5;
        [Header("Turn-based pacing")]
        [SerializeField] private int _moveHexes = 3;
        [SerializeField] private float _enemyStepSeconds = .28f;
        [SerializeField] private int _manaPerRound = 3;
        [Header("Real-time pacing (not numerically equivalent to turns)")]
        [SerializeField] private float _moveSpeed = 3.6f;
        [SerializeField] private float _enemySpeed = 1.65f;
        [SerializeField] private float _allyAttackSeconds = 1.35f;
        [SerializeField] private float _enemyAttackSeconds = 2.4f;
        [SerializeField] private float _enemyWindup = .8f;
        [SerializeField] private float _manaPerSecond = .65f;
        [SerializeField] private float _pullCooldown = 5f;
        [SerializeField] private float _burstCooldown = 5f;
        [Header("Presentation")]
        [SerializeField] private Color _groundA = new Color(.27f, .34f, .35f);
        [SerializeField] private Color _groundB = new Color(.31f, .39f, .4f);
        [SerializeField] private Color _kamColor = new Color(.32f, .85f, 1f);
        [SerializeField] private Color _guardColor = new Color(.65f, .81f, .37f);
        [SerializeField] private Color _archerColor = new Color(1f, .78f, .35f);
        [SerializeField] private Color _enemyColor = new Color(.95f, .36f, .35f);
        [SerializeField] private Color _panelColor = new Color(.045f, .075f, .1f, .96f);
        [SerializeField] private Color _textColor = new Color(.91f, .95f, .95f);
        [SerializeField] private Color _accentColor = new Color(.35f, .87f, .83f);
        public int ArenaRadius => _arenaRadius;
        public float TileRadius => _tileRadius;
        public int KamHealth => _kamHealth;
        public int GuardHealth => _guardHealth;
        public int ArcherHealth => _archerHealth;
        public int EnemyHealth => _enemyHealth;
        public int BossHealth => _bossHealth;
        public int Mana => _mana;
        public int PullCost => _pullCost;
        public int BurstCost => _burstCost;
        public int ChainDamage => _chainDamage;
        public float ChainRadius => _chainRadius;
        public float PullRadius => _pullRadius;
        public float BurstRadius => _burstRadius;
        public int BurstDamage => _burstDamage;
        public int MoveHexes => _moveHexes;
        public float EnemyStepSeconds => _enemyStepSeconds;
        public int ManaPerRound => _manaPerRound;
        public float MoveSpeed => _moveSpeed;
        public float EnemySpeed => _enemySpeed;
        public float AllyAttackSeconds => _allyAttackSeconds;
        public float EnemyAttackSeconds => _enemyAttackSeconds;
        public float EnemyWindup => _enemyWindup;
        public float ManaPerSecond => _manaPerSecond;
        public float PullCooldown => _pullCooldown;
        public float BurstCooldown => _burstCooldown;
        public Color GroundA => _groundA;
        public Color GroundB => _groundB;
        public Color KamColor => _kamColor;
        public Color GuardColor => _guardColor;
        public Color ArcherColor => _archerColor;
        public Color EnemyColor => _enemyColor;
        public Color PanelColor => _panelColor;
        public Color TextColor => _textColor;
        public Color AccentColor => _accentColor;
    }
}
