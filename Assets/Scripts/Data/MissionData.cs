using UnityEngine;

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

        // Faz C: düşman roster'ı (liste of {enemyData, coord}) buraya eklenecek.

        public string    DisplayName => _displayName;
        public string    Description => _description;
        public TileMapSO CombatMap   => _combatMap;
    }
}
