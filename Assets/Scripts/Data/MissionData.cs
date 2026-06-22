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
        [Tooltip("Bu göreve girilince yüklenecek savaş haritası (TileMap).")]
        [SerializeField] private TileMapSO _combatMap;

        [Header("Düşman Roster (Faz C — savaşa girince spawn olur)")]
        [SerializeField] private List<EnemySpawn> _enemyRoster = new();

        public string    DisplayName => _displayName;
        public string    Description => _description;
        public TileMapSO CombatMap   => _combatMap;
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
