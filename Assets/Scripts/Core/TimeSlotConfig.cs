using UnityEngine;

namespace TacticalRPG.Core
{
    /// <summary>
    /// AP/Zaman motoru sabitleri — Inspector'dan tweaklenebilir ScriptableObject.
    ///
    /// KURAL: 1 karo ilerleme = 1 AP (2 karo yürüyen 2 AP harcar), öz toplama = 1 AP,
    /// savaşa girme = 3 AP (TÜM savaş bu 3 AP'ye sayılır; savaş sırasında zaman AKMAZ).
    /// **4 AP = 1 zaman dilimi, 6 dilim = 1 gün → 24 AP = 1 gün** (TASK-005, 2026-07-28).
    /// Eskiden 9/dilim = 54 AP/gün'dü; bölüm 1'in TÜM denge simülasyonu (öz hedefi, collapse
    /// zamanlaması, 10-seed havuzu) 24 AP/gün varsayımıyla yapıldı — bkz Docs/GAME_DESIGN.md §0.
    /// İlk 4 dilim GÜNDÜZ, son 2 dilim GECE.
    /// </summary>
    [CreateAssetMenu(fileName = "TimeSlotConfig", menuName = "TacticalRPG/Config/TimeSlotConfig")]
    public class TimeSlotConfig : ScriptableObject
    {
        [Header("AP / Zaman Ayarları")]
        [Tooltip("Bir karo ilerlemenin maliyeti. Oyuncu her karoya vardığında ayrı ayrı harcanır " +
                 "→ 2 karoluk yol 2 AP eder.")]
        [SerializeField] private int _apPerMove      = 1;
        [Tooltip("Bir zaman dilimini ilerletmek için harcanması gereken AP.")]
        [SerializeField] private int _apPerTimeSlot  = 4;
        [Tooltip("Bir gündeki zaman dilimi sayısı. 6 dilim x 4 AP = 24 AP = 1 gün (GAME_DESIGN §0).")]
        [SerializeField] private int _timeSlotsPerDay = 6;
        [Tooltip("Savaşa girmenin maliyeti. TÜM savaş bu kadar AP sayılır — savaş sırasında " +
                 "ayrıca AP harcanmaz ve zaman ilerlemez.")]
        [SerializeField] private int _apPerCombat    = 3;

        [Header("Gündüz / Gece")]
        [Tooltip("Günün ilk kaç dilimi GÜNDÜZ sayılır? Kalanı gecedir (varsayılan 4 gündüz + 2 gece).")]
        [SerializeField] private int _dayTimeSlots = 4;

        [Header("Zaman Dilimi Adları")]
        [SerializeField] private string[] _slotNames = {
            "Sabah", "Öğle", "Öğleden Sonra", "Akşam", "Gece", "Gece Yarısı"
        };

        public int   APPerMove       => _apPerMove;
        public int   APPerTimeSlot   => _apPerTimeSlot;
        public int   TimeSlotsPerDay => _timeSlotsPerDay;
        public int   APPerCombat     => _apPerCombat;
        public int   DayTimeSlots    => _dayTimeSlots;

        /// <summary>Bu dilim GECE mi? (dilim indeksi >= gündüz dilim sayısı)</summary>
        public bool IsNight(int slotIndex) => slotIndex >= _dayTimeSlots;

        public string GetSlotName(int slotIndex)
        {
            if (_slotNames == null || _slotNames.Length == 0) return $"Dilim {slotIndex}";
            return _slotNames[slotIndex % _slotNames.Length];
        }
    }
}
