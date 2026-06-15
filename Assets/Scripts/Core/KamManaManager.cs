using System;
using UnityEngine;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Kam'a özgü mana havuzu (0-10).
    /// Zaman dilimi geçişinde regen yapar, büyüler TrySpendMana ile tüketir.
    /// Regen miktarı Inspector'dan ayarlanır.
    /// </summary>
    public class KamManaManager : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private ActionPointManager _apManager;

        [Header("Mana Ayarları")]
        [SerializeField] private int _maxMana          = 10;
        [SerializeField] private int _manaRegenPerSlot = 2;

        public int CurrentMana { get; private set; }
        public int MaxMana     => _maxMana;

        /// <summary>current mana, max mana</summary>
        public event Action<int, int> OnManaChanged;

        private void OnEnable()
        {
            if (_apManager != null)
                _apManager.OnTimeAdvanced += HandleTimeAdvanced;
        }

        private void OnDisable()
        {
            if (_apManager != null)
                _apManager.OnTimeAdvanced -= HandleTimeAdvanced;
        }

        private void Start()
        {
            if (_apManager == null)
                Debug.LogError("[KamMana] _apManager NULL! Faz 1.4'ü kurmadan bu sistemi çalıştıramazsın.");

            CurrentMana = _maxMana; // tam dolarak başla
            OnManaChanged?.Invoke(CurrentMana, _maxMana);
        }

        // ── Zaman dilimi geçişinde regen ─────────────────────────────────────

        private void HandleTimeAdvanced(int day, int slot, string slotName)
        {
            int prev = CurrentMana;
            CurrentMana = Mathf.Min(_maxMana, CurrentMana + _manaRegenPerSlot);
            if (CurrentMana != prev)
                OnManaChanged?.Invoke(CurrentMana, _maxMana);
        }

        // ── Büyü maliyeti ─────────────────────────────────────────────────────

        public bool CanCast(int cost)     => cost >= 0 && CurrentMana >= cost;

        public bool TrySpendMana(int cost)
        {
            if (!CanCast(cost)) return false;
            CurrentMana -= cost;
            OnManaChanged?.Invoke(CurrentMana, _maxMana);
            return true;
        }

        public void RestoreMana(int amount)
        {
            if (amount <= 0) return;
            CurrentMana = Mathf.Min(_maxMana, CurrentMana + amount);
            OnManaChanged?.Invoke(CurrentMana, _maxMana);
        }
    }
}
