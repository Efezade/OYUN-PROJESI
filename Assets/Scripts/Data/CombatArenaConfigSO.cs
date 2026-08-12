using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Grid;

namespace TacticalRPG.Data
{
    /// <summary>
    /// Savaş arenalarının kademe ayarları. Sayılar koda gömülü değil (CLAUDE.md §3) —
    /// <see cref="ArenaParams.ForTier"/> yalnızca bu asset yoksa devreye giren yedektir.
    ///
    /// Kademeler 2026-08-12 hesabından: sahadaki birim × ~10 karo, savaş 4-12 tur,
    /// engel %8-10, etkileşimli karo %10-12.
    /// </summary>
    [CreateAssetMenu(fileName = "CombatArenaConfig", menuName = "TacticalRPG/Config/CombatArenaConfig")]
    public class CombatArenaConfigSO : ScriptableObject
    {
        [System.Serializable]
        public class Tier
        {
            public ArenaTier tier = ArenaTier.Dungeon;
            [Min(6)] public int width  = 11;
            [Min(6)] public int height = 9;
            [Tooltip("Oyuncu bölgesinin satır derinliği (alt uç).")]
            [Min(1)] public int deployDepth = 2;
            [Tooltip("Düşman bölgesinin satır derinliği (üst uç).")]
            [Min(1)] public int enemyDepth  = 2;
            [Min(1)] public int enemyCount  = 6;
            [Tooltip("Duvar + siper hedef oranı. %8-10 önerilir; üstü labirent olur, altı boş tarla.")]
            [Range(0f, 0.25f)] public float blockedPct = 0.09f;
            [Tooltip("Yükselti + tehlike + zor arazi oranı. %10-12 önerilir (davul karoları üstüne gelir).")]
            [Range(0f, 0.30f)] public float interactivePct = 0.11f;
            [Tooltip("Kenar tırtıklılığı. 0 = düz dikdörtgen (istenmiyor), 0.5 = organik kenar.")]
            [Range(0f, 0.8f)] public float edgeRoughness = 0.50f;
        }

        [SerializeField] private List<Tier> _tiers = new();

        [Header("Seed havuzu")]
        [Tooltip("Her savaşta buradan RASTGELE (son oynanandan farklı) bir seed seçilir. " +
                 "Boşsa tamamen rastgele üretilir.")]
        [SerializeField] private List<int> _seedPool = new();

        public IReadOnlyList<int> SeedPool => _seedPool;

        /// <summary>Kademenin parametreleri; asset'te tanımlı değilse koddaki varsayılan.</summary>
        public ArenaParams ParamsFor(ArenaTier tier)
        {
            foreach (var t in _tiers)
            {
                if (t.tier != tier) continue;
                return new ArenaParams
                {
                    Width = t.width, Height = t.height,
                    DeployDepth = t.deployDepth, EnemyDepth = t.enemyDepth,
                    EnemyCount = t.enemyCount,
                    BlockedPct = t.blockedPct, InteractivePct = t.interactivePct,
                    EdgeRoughness = t.edgeRoughness
                };
            }
            return ArenaParams.ForTier(tier);
        }
    }
}
