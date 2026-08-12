using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Grid;

namespace TacticalRPG.Data
{
    /// <summary>
    /// Bir görev/encounter tanımı. Hangi savaş haritasına geçileceğini ve
    /// (Faz C'de) hangi düşmanların yükleneceğini tutar. Saf veri.
    /// </summary>
    [CreateAssetMenu(menuName = "TacticalRPG/Mission Data", fileName = "Mission")]
    public class MissionData : ScriptableObject
    {
        [SerializeField] private string    _displayName = "Görev";
        [TextArea(2, 4)]
        [SerializeField] private string    _description = "";

        [Tooltip("Arena kademesi — savaş tahtasının boyutunu ve düşman sayısını belirler.\n" +
                 "Encounter ~65 karo/4 düşman · Zindan ~80/6 · Zorunlu ~100/7 · Boss ~110/6+dalga.")]
        [SerializeField] private MapNodeType _tier = MapNodeType.Zindan;

        [Tooltip("YEDEK: prosedürel arena üretici yoksa kullanılacak elle atanmış savaş haritası. " +
                 "Normal akışta CombatMapGenerator arenayı üretir, bu alan boş kalabilir.")]
        [SerializeField] private TileMapSO _combatMap;

        [Header("Düşman Roster (Faz C — savaşa girince spawn olur)")]
        [SerializeField] private List<EnemySpawn> _enemyRoster = new();

        public string      DisplayName => _displayName;
        public string      Description => _description;
        public MapNodeType Tier        => _tier;
        public TileMapSO   CombatMap   => _combatMap;
        public IReadOnlyList<EnemySpawn> EnemyRoster => _enemyRoster;

        /// <summary>Tek bir düşman spawn tanımı: sınıf + savaş haritasındaki konum + seviye.</summary>
        [System.Serializable]
        public struct EnemySpawn
        {
            [Tooltip("Spawn edilecek düşman sınıfı (CharacterClassData).")]
            public CharacterClassData enemyClass;
            [Tooltip("Savaş haritasında doğacağı hex.")]
            public HexCoordinate      coord;
            [Tooltip("Düşmanın seviyesi (1-3).")]
            [Min(1)] public int       level;
        }
    }
}
