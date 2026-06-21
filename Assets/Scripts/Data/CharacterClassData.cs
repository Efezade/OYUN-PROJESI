using System.Collections.Generic;
using UnityEngine;

namespace TacticalRPG.Data
{
    /// <summary>
    /// Bir karakter sınıfının sabit verisi. Tüm seviye ilerlemesi buradan gelir.
    /// Runtime'da hiçbir alan değiştirilmez — CharacterCard kopyaları kullanır.
    /// </summary>
    [CreateAssetMenu(menuName = "TacticalRPG/Character Class Data", fileName = "CharacterClassData")]
    public class CharacterClassData : ScriptableObject
    {
        [Header("Kimlik")]
        [SerializeField] private string _className = "Adsız";
        [TextArea(2, 4)]
        [SerializeField] private string _lore = "";

        [Header("Temel İstatistikler (Seviye 1)")]
        [SerializeField] private int _maxHP      = 10;
        [SerializeField] private int _attack     = 3;
        [SerializeField] private int _defense    = 1;
        [SerializeField] private int _moveRange  = 3;

        [Header("Seviye İlerlemesi (index = hedef seviye, 0=Sv1 1=Sv2 2=Sv3)")]
        [SerializeField] private int[]   _essenceCostPerLevel    = { 0, 5, 12 };
        [SerializeField] private float[] _hpMultiplierPerLevel   = { 1f, 1.3f, 1.7f };
        [SerializeField] private float[] _atkMultiplierPerLevel  = { 1f, 1.2f, 1.5f };
        [SerializeField] private float[] _defMultiplierPerLevel  = { 1f, 1.15f, 1.4f };

        [Header("Deployment (savaşa sürme öz maliyeti)")]
        [SerializeField, Min(0)] private int _deployCost = 3;

        [Header("Özel Sistem")]
        [SerializeField] private bool _hasManaSystem;
        [SerializeField] private int  _maxMana;

        [Header("Yetenekler (Kam vb. — boş olabilir)")]
        [SerializeField] private List<KamAbilityData> _abilities = new();

        // ── Kimlik ───────────────────────────────────────────────────────────
        public string ClassName => _className;
        public string Lore      => _lore;

        // ── Temel değerler ────────────────────────────────────────────────────
        public int BaseMaxHP    => _maxHP;
        public int BaseAttack   => _attack;
        public int BaseDefense  => _defense;
        public int MoveRange    => _moveRange;
        public int DeployCost   => _deployCost;

        // ── Mana ─────────────────────────────────────────────────────────────
        public bool HasManaSystem => _hasManaSystem;
        public int  MaxMana       => _maxMana;

        // ── Yetenekler ────────────────────────────────────────────────────────
        public IReadOnlyList<KamAbilityData> Abilities => _abilities;

        // ── Seviye sorgulama ──────────────────────────────────────────────────
        public const int MaxLevel = 3; // 1, 2, 3

        /// <summary>level: 1-indexed (1=başlangıç, 3=max)</summary>
        public int GetMaxHP(int level)    => ScaledStat(_maxHP,    _hpMultiplierPerLevel,  level);
        public int GetAttack(int level)   => ScaledStat(_attack,   _atkMultiplierPerLevel, level);
        public int GetDefense(int level)  => ScaledStat(_defense,  _defMultiplierPerLevel, level);

        /// <summary>Seviye 1'den targetLevel'a yükseltme öz maliyeti.</summary>
        public int GetEssenceCost(int targetLevel)
        {
            int idx = targetLevel - 1;
            return (_essenceCostPerLevel != null && idx >= 0 && idx < _essenceCostPerLevel.Length)
                ? _essenceCostPerLevel[idx]
                : 999;
        }

        private int ScaledStat(int baseStat, float[] multipliers, int level)
        {
            int idx = Mathf.Clamp(level - 1, 0, MaxLevel - 1);
            float m = (multipliers != null && idx < multipliers.Length) ? multipliers[idx] : 1f;
            return Mathf.RoundToInt(baseStat * m);
        }
    }
}
