using UnityEngine;

namespace TacticalRPG.Core
{
    /// <summary>
    /// AP/Zaman motoru sabitleri — Inspector'dan tweaklenebilir ScriptableObject.
    /// 3 AP = 1 zaman dilimi, 6 dilim = 1 gün (sabah→öğle→öğleden sonra→akşam→gece→gece yarısı).
    /// </summary>
    [CreateAssetMenu(fileName = "TimeSlotConfig", menuName = "TacticalRPG/Config/TimeSlotConfig")]
    public class TimeSlotConfig : ScriptableObject
    {
        [Header("AP / Zaman Ayarları")]
        [SerializeField] private int _apPerMove      = 1;
        [SerializeField] private int _apPerTimeSlot  = 3;
        [SerializeField] private int _timeSlotsPerDay = 6;

        [Header("Zaman Dilimi Adları")]
        [SerializeField] private string[] _slotNames = {
            "Sabah", "Öğle", "Öğleden Sonra", "Akşam", "Gece", "Gece Yarısı"
        };

        public int   APPerMove       => _apPerMove;
        public int   APPerTimeSlot   => _apPerTimeSlot;
        public int   TimeSlotsPerDay => _timeSlotsPerDay;

        public string GetSlotName(int slotIndex)
        {
            if (_slotNames == null || _slotNames.Length == 0) return $"Dilim {slotIndex}";
            return _slotNames[slotIndex % _slotNames.Length];
        }
    }
}
