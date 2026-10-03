using UnityEngine;

namespace TacticalRPG.Data
{
    /// <summary>SAVAŞ görevi — bugüne kadarki tek zorunlu görev tipi. Yerleşik savaş akışını
    /// kullanır (prosedürel arena, davul, Kam ölürse bölüm kaybedilir).</summary>
    [CreateAssetMenu(fileName = "GorevTipi_Savas", menuName = "TacticalRPG/Gorev Tipi/Savas")]
    public class CombatQuestKindSO : QuestKindSO
    {
        public override bool IsCombat => true;
    }
}
