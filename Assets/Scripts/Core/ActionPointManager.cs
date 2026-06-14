using System;
using UnityEngine;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// AP (Aksiyon Puanı) ve Zaman motorunu yönetir.
    /// PlayerController.OnMoved'e abone olur; AP biter → zaman dilimi ilerler.
    /// 3 AP = 1 dilim, 6 dilim = 1 gün.
    /// </summary>
    public class ActionPointManager : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private PlayerController _player;
        [SerializeField] private TimeSlotConfig   _config;

        // AP durumu
        public int CurrentAP      { get; private set; }
        public int MaxAP          => _config != null ? _config.APPerTimeSlot : 3;

        // Zaman durumu
        public int CurrentDay     { get; private set; } = 1;
        public int CurrentSlot    { get; private set; } = 0;
        public int TotalMoves     { get; private set; } = 0;

        // Event'ler — UI bu event'leri dinler
        public event Action<int, int>    OnAPChanged;        // (currentAP, maxAP)
        public event Action<int, int, string> OnTimeAdvanced; // (day, slot, slotName)

        private void Awake()
        {
            if (_config == null)
                Debug.LogWarning("[ActionPointManager] TimeSlotConfig atanmamis! Default degerler kullanilir.");
        }

        private void OnEnable()
        {
            if (_player != null)
                _player.OnMoved += HandlePlayerMoved;
        }

        private void OnDisable()
        {
            if (_player != null)
                _player.OnMoved -= HandlePlayerMoved;
        }

        private void Start()
        {
            CurrentAP = MaxAP;
            OnAPChanged?.Invoke(CurrentAP, MaxAP);
        }

        private void HandlePlayerMoved(HexCoordinate newCoord)
        {
            int cost = _config != null ? _config.APPerMove : 1;
            SpendAP(cost);
        }

        public void SpendAP(int amount)
        {
            TotalMoves++;
            CurrentAP -= amount;

            if (CurrentAP <= 0)
            {
                AdvanceTime();
                CurrentAP = MaxAP + CurrentAP; // taşan negatif AP bir sonraki dilimine geçer
                if (CurrentAP < 0) CurrentAP = 0;
            }

            OnAPChanged?.Invoke(CurrentAP, MaxAP);
            Debug.Log($"[Time] Gün {CurrentDay} | {GetCurrentSlotName()} | AP: {CurrentAP}/{MaxAP}");
        }

        public void RefillAP()
        {
            CurrentAP = MaxAP;
            OnAPChanged?.Invoke(CurrentAP, MaxAP);
        }

        private void AdvanceTime()
        {
            int slotsPerDay = _config != null ? _config.TimeSlotsPerDay : 6;
            CurrentSlot++;

            if (CurrentSlot >= slotsPerDay)
            {
                CurrentSlot = 0;
                CurrentDay++;
                Debug.Log($"[Time] === Yeni Gun: {CurrentDay} ===");
            }

            string slotName = GetCurrentSlotName();
            OnTimeAdvanced?.Invoke(CurrentDay, CurrentSlot, slotName);
            Debug.Log($"[Time] Zaman dilimi ilerledi → {slotName}");
        }

        public string GetCurrentSlotName() =>
            _config != null ? _config.GetSlotName(CurrentSlot) : $"Dilim {CurrentSlot}";

        public string GetTimeString() =>
            $"Gün {CurrentDay} — {GetCurrentSlotName()} ({CurrentAP}/{MaxAP} AP)";
    }
}
