using System.Collections.Generic;
using UnityEngine;

namespace TacticalRPG.Data
{
    /// <summary>
    /// Bir asker kartını üretmenin öz tarifi: hangi sınıf + hangi öz kombinasyonu maliyet.
    /// Tasarım: "öz harcayarak asker sınıflarını savaşa hazırlarsın." Maliyet 2+ öz türü
    /// kombinasyonu olabilir. Saf veri — miktarlar Inspector'dan ayarlanır.
    /// </summary>
    [CreateAssetMenu(menuName = "TacticalRPG/Unit Recipe", fileName = "UnitRecipe")]
    public class UnitRecipe : ScriptableObject
    {
        [SerializeField] private string             _displayName = "Birim";
        [Tooltip("Üretilecek sınıf (CharacterClassData).")]
        [SerializeField] private CharacterClassData _unitClass;
        [Tooltip("Üretim maliyeti — öz türü + miktar kombinasyonu (örn. 2 Ateş + 1 Toprak).")]
        [SerializeField] private EssenceAmount[]     _cost;

        public string                       DisplayName => _displayName;
        public CharacterClassData           UnitClass   => _unitClass;
        public IReadOnlyList<EssenceAmount> Cost        => _cost;

        /// <summary>Maliyeti "2 Ates + 1 Toprak" gibi okunur metne çevirir (HUD için).</summary>
        public string CostString(EssenceConfigSO config)
        {
            if (_cost == null || _cost.Length == 0) return "bedava";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _cost.Length; i++)
            {
                if (i > 0) sb.Append(" + ");
                string name = config != null ? config.NameOf(_cost[i].type) : _cost[i].type.ToString();
                sb.Append($"{_cost[i].amount} {name}");
            }
            return sb.ToString();
        }
    }
}
