using System;
using UnityEngine;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Öz (Essence) para birimini yönetir.
    /// Kazanma/harcama işlemleri TrySpend / Gain üzerinden geçer; UI bu event'i dinler.
    /// </summary>
    public class EssenceManager : MonoBehaviour
    {
        [SerializeField] private int _startingEssence = 0;
        [SerializeField] private int _maxEssence      = 99;

        public int CurrentEssence { get; private set; }
        public int MaxEssence     => _maxEssence;

        /// <summary>current öz, delta (+ kazanma, - harcama)</summary>
        public event Action<int, int> OnEssenceChanged;

        private void Start()
        {
            CurrentEssence = Mathf.Clamp(_startingEssence, 0, _maxEssence);
            OnEssenceChanged?.Invoke(CurrentEssence, 0);
        }

        public bool CanAfford(int cost) => cost >= 0 && CurrentEssence >= cost;

        public bool TrySpend(int cost)
        {
            if (!CanAfford(cost)) return false;
            CurrentEssence -= cost;
            OnEssenceChanged?.Invoke(CurrentEssence, -cost);
            return true;
        }

        public void Gain(int amount)
        {
            if (amount <= 0) return;
            int gained = Mathf.Min(amount, _maxEssence - CurrentEssence);
            CurrentEssence += gained;
            OnEssenceChanged?.Invoke(CurrentEssence, gained);
        }
    }
}
