using System;
using UnityEngine;

namespace TacticalRPG.Core
{
    /// <summary>
    /// İLK KAM MEKANİĞİ: klasik mana havuzu. Bugüne kadar <c>KamManaManager</c>'ın yaptığı işin
    /// birebir aynısı — tavan 10, her zaman diliminde +2, büyü bedeli manadan düşer. Eşik etkisi
    /// YOK (güç değişmez); 1. bölümün kuralı bu.
    /// </summary>
    [CreateAssetMenu(fileName = "Mekanik_Mana", menuName = "TacticalRPG/Kam Mekanigi/Mana")]
    public class ManaMechanicSO : KamMechanicSO
    {
        [SerializeField, Min(1)] private int _maxMana          = 10;
        [SerializeField, Min(0)] private int _manaRegenPerSlot = 2;

        [Tooltip("Bölüm başında tam dolu mu başlar? Kapalıysa boş başlar.")]
        [SerializeField] private bool _startFull = true;

        public override IKamMechanic CreateRuntime()
            => new ManaMechanic(ResourceName, _maxMana, _manaRegenPerSlot, _startFull);

        /// <summary>Mana mekaniğinin runtime durumu.</summary>
        private sealed class ManaMechanic : IKamMechanic
        {
            private readonly int _regen;

            public string ResourceName { get; }
            public int    Current      { get; private set; }
            public int    Max          { get; }

            public event Action<int, int> OnResourceChanged;

            public ManaMechanic(string name, int max, int regen, bool startFull)
            {
                ResourceName = name;
                Max          = Mathf.Max(1, max);
                _regen       = Mathf.Max(0, regen);
                Current      = startFull ? Max : 0;
            }

            public bool CanPay(int cost) => cost >= 0 && Current >= cost;

            public bool TryPay(int cost)
            {
                if (!CanPay(cost)) return false;
                Current -= cost;
                OnResourceChanged?.Invoke(Current, Max);
                return true;
            }

            public void Restore(int amount)
            {
                if (amount <= 0) return;
                int prev = Current;
                Current = Mathf.Min(Max, Current + amount);
                if (Current != prev) OnResourceChanged?.Invoke(Current, Max);
            }

            public int ModifyPower(int basePower) => basePower;

            public void OnTimeSlotAdvanced(int day, int slot) => Restore(_regen);
        }
    }
}
