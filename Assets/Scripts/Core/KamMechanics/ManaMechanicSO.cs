using System;
using UnityEngine;

namespace TacticalRPG.Core
{
    /// <summary>
    /// İLK KAM MEKANİĞİ: klasik mana havuzu — tavan 10, büyü ve davul kartlarının bedeli
    /// manadan düşer. Eşik etkisi YOK (güç değişmez); 1. bölümün kuralı bu.
    ///
    /// SAVAŞ BÜTÇESİ (Efe, 2026-10-03): Kam HER SAVAŞA 10 mana ile girer; savaş içinde mana
    /// yenilenmez (yalnız Davul Taşı kartı). Kart bedelleri bu bütçeye göre türetildi — bkz
    /// <c>AugmentCatalog.ManaRules</c>: savaş başına 2-3 kart, en çok 2 büyü.
    /// </summary>
    [CreateAssetMenu(fileName = "Mekanik_Mana", menuName = "TacticalRPG/Kam Mekanigi/Mana")]
    public class ManaMechanicSO : KamMechanicSO
    {
        [SerializeField, Min(1)] private int _maxMana          = 10;
        [SerializeField, Min(0)] private int _manaRegenPerSlot = 2;

        [Tooltip("Bölüm başında tam dolu mu başlar? Kapalıysa boş başlar.")]
        [SerializeField] private bool _startFull = true;

        [Tooltip("Her savaşın başında mana TAVANA doldurulsun mu? (Efe: her savaş 10 mana.)")]
        [SerializeField] private bool _refillEachCombat = true;

        public override IKamMechanic CreateRuntime()
            => new ManaMechanic(ResourceName, _maxMana, _manaRegenPerSlot, _startFull, _refillEachCombat);

        /// <summary>Mana mekaniğinin runtime durumu.</summary>
        private sealed class ManaMechanic : IKamMechanic
        {
            private readonly int  _regen;
            private readonly bool _refillEachCombat;

            public string ResourceName { get; }
            public int    Current      { get; private set; }
            public int    Max          { get; }

            public event Action<int, int> OnResourceChanged;

            public ManaMechanic(string name, int max, int regen, bool startFull, bool refillEachCombat)
            {
                ResourceName      = name;
                Max               = Mathf.Max(1, max);
                _regen            = Mathf.Max(0, regen);
                _refillEachCombat = refillEachCombat;
                Current           = startFull ? Max : 0;
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

            public void OnCombatStarted()
            {
                if (!_refillEachCombat || Current == Max) return;
                Current = Max;
                OnResourceChanged?.Invoke(Current, Max);
            }
        }
    }
}
