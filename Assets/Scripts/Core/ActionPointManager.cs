using System;
using UnityEngine;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// AP (Aksiyon Puanı) ve Zaman motorunu yönetir.
    /// PlayerController.OnMoved'e abone olur; AP biter → zaman dilimi ilerler.
    ///
    /// KURALLAR (TimeSlotConfig'ten okunur):
    ///   • 1 karo ilerleme = 1 AP → OnMoved KARO BAŞINA tetiklendiği için 2 karoluk yol 2 AP eder.
    ///   • Öz toplama = 1 AP (EssenceNodeManager kendi maliyetini harcar).
    ///   • Savaşa girme = 3 AP; TÜM savaş bu 3 AP'ye sayılır. Savaş/yerleştirme sırasında motor
    ///     DONDURULUR (<see cref="SetFrozen"/>) → zaman ilerlemez, gün geçmez.
    ///   • 9 AP = 1 dilim, 6 dilim = 1 gün (54 AP). İlk 4 dilim gündüz, son 2 dilim gece.
    /// </summary>
    public class ActionPointManager : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private PlayerController _player;
        [SerializeField] private TimeSlotConfig   _config;

        // AP durumu
        public int CurrentAP      { get; private set; }
        public int MaxAP          => _config != null ? _config.APPerTimeSlot : 9;

        // Zaman durumu
        public int CurrentDay     { get; private set; } = 1;
        public int CurrentSlot    { get; private set; } = 0;
        public int TotalMoves     { get; private set; } = 0;
        public int SlotsPerDay    => _config != null ? _config.TimeSlotsPerDay : 6;

        /// <summary>Savaşa girmenin AP maliyeti (tüm savaş bu kadar sayılır).</summary>
        public int CombatAPCost   => _config != null ? _config.APPerCombat : 3;

        /// <summary>Şu anki dilim gece mi? (varsayılan: dilim 4-5)</summary>
        public bool IsNight       => _config != null && _config.IsNight(CurrentSlot);

        /// <summary>Günün ilk kaç dilimi gündüz? (varsayılan 4 → kalan 2 dilim gece)</summary>
        public int  DayTimeSlots  => _config != null ? _config.DayTimeSlots : 4;

        /// <summary>Verilen dilim gece mi? (zaman sayacı UI'ı dilimleri boyarken kullanır)</summary>
        public bool IsNightSlot(int slot) => _config != null ? _config.IsNight(slot) : slot >= 4;

        /// <summary>Savaş/yerleştirme sırasında true — AP harcanmaz, zaman ilerlemez.</summary>
        public bool IsFrozen      { get; private set; }

        /// <summary>Bu günün SONUNA (yeni gün) kadar kalan toplam AP — collapse geri sayımı için.</summary>
        public int APRemainingToday => CurrentAP + Mathf.Max(0, SlotsPerDay - 1 - CurrentSlot) * MaxAP;

        // Event'ler — UI bu event'leri dinler
        public event Action<int, int>    OnAPChanged;        // (currentAP, maxAP)
        public event Action<int, int, string> OnTimeAdvanced; // (day, slot, slotName)
        /// <summary>Gündüz↔gece SINIRI geçildiğinde tetiklenir (her dilimde DEĞİL). true = gece oldu.
        /// DayNightCycle bunu dinleyip karo değiş-tokuş animasyonunu + sert ışık geçişini oynatır.</summary>
        public event Action<bool>        OnDayNightChanged;

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

        /// <summary>
        /// Savaş/yerleştirme sırasında motoru dondurur: AP harcanmaz, dilim/gün ilerlemez.
        /// Savaşın TAMAMI giriş anında ödenen sabit AP'ye sayıldığı için gerekli.
        /// </summary>
        public void SetFrozen(bool frozen) => IsFrozen = frozen;

        /// <summary>Savaşa giriş bedelini (varsayılan 3 AP) harcar. GameStateManager çağırır.</summary>
        public void SpendCombatEntryCost() => SpendAP(CombatAPCost, force: true);

        public void SpendAP(int amount) => SpendAP(amount, force: false);

        /// <param name="force">Dondurulmuş olsa bile harcar (savaş giriş bedeli için).</param>
        public void SpendAP(int amount, bool force)
        {
            if (IsFrozen && !force) return;   // savaş sırasında zaman akmaz

            TotalMoves++;
            CurrentAP -= amount;

            // Bir hamle birden fazla dilimi devirebilir (örn. 1 AP kalmışken 3 AP'lik savaş girişi)
            // → AP pozitife dönene kadar dilim ilerlet, aksi halde AP negatifte takılırdı.
            while (CurrentAP <= 0)
            {
                AdvanceTime();
                CurrentAP += MaxAP;
                if (MaxAP <= 0) { CurrentAP = 0; break; }   // hatalı config'te sonsuz döngü olmasın
            }

            OnAPChanged?.Invoke(CurrentAP, MaxAP);
            Debug.Log($"[Time] Gün {CurrentDay} | {GetCurrentSlotName()} | AP: {CurrentAP}/{MaxAP}");
        }

        public void RefillAP()
        {
            CurrentAP = MaxAP;
            OnAPChanged?.Invoke(CurrentAP, MaxAP);
        }

        /// <summary>Mevcut dilime BONUS AP ekler (mağaza "Zaman Kumu" iksiri gibi). MaxAP'yi aşabilir —
        /// harcayınca dilim ilerlemesi normal SpendAP mantığıyla işler. Zaman İLERLETMEZ.</summary>
        public void GrantAP(int amount)
        {
            if (amount <= 0) return;
            CurrentAP += amount;
            OnAPChanged?.Invoke(CurrentAP, MaxAP);
        }

        private void AdvanceTime()
        {
            int  slotsPerDay = _config != null ? _config.TimeSlotsPerDay : 6;
            bool wasNight    = IsNight;

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

            // Gündüz↔gece sınırı: yalnız DEĞİŞTİĞİNDE haber ver (her dilimde değil).
            if (IsNight != wasNight)
                OnDayNightChanged?.Invoke(IsNight);
        }

        public string GetCurrentSlotName() =>
            _config != null ? _config.GetSlotName(CurrentSlot) : $"Dilim {CurrentSlot}";

        public string GetTimeString() =>
            $"Gün {CurrentDay} — {GetCurrentSlotName()} ({CurrentAP}/{MaxAP} AP)";
    }
}
