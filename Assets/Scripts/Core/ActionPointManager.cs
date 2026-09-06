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

        // BEDAVA HAMLE STOKU (Güçlü Yol Taşı, 2026-08-17). Sıfırdan büyükken hareket AP harcamaz,
        // zaman dilimi ilerlemez, gün dönmez. Sayaç hamle başına düşer → kendi kendini bitirir;
        // "şimdi kapat" demeyi unutan bir bayrak gibi sonsuza kadar açık kalamaz.
        private int _freeMoves;

        /// <summary>Kaç hamle bedava (Güçlü Yol Taşı). Var olan stok EZİLMEZ, en büyüğü kalır.</summary>
        public void GrantFreeMoves(int count)
        {
            if (count <= 0) return;
            _freeMoves = Mathf.Max(_freeMoves, count);
        }

        /// <summary>Kalan bedava hamle (HUD/teşhis).</summary>
        public int FreeMovesLeft => _freeMoves;

        /// <summary>
        /// Oyuncunun İRADESİ DIŞINDA yapılan tek hamle (çöken karodan itilme, 2026-09-03) — bir
        /// sonraki <see cref="PlayerController.OnMoved"/> AP harcamasın, zaman ilerlemesin.
        ///
        /// <see cref="GrantFreeMoves"/> gibi "en büyüğü kalsın" DEĞİL, EKLER: oyuncunun parayla
        /// aldığı Güçlü Yol Taşı stokunu, kendi seçmediği bir itilme yemeliydi yoksa.
        /// </summary>
        public void GrantForcedMove() => _freeMoves++;

        /// <summary>
        /// Bedava hamle stokunu BOŞALTIR. Güçlü Yol Taşı ile başlayan yolculuk YARIDA KESİLİNCE
        /// çağrılır: taş "şu yolculuğu" satın alıyor, kalan hak başka yöne yürümek için cebe
        /// atılmamalı — yoksa taş, iptal edilerek her yöne bedava adıma çevrilebilirdi.
        /// (Savaşa girişte de aynı temizlik yapılıyor, bkz <see cref="SetFrozen"/>.)
        /// </summary>
        public void ClearFreeMoves() => _freeMoves = 0;

        private void HandlePlayerMoved(HexCoordinate newCoord)
        {
            if (_freeMoves > 0)
            {
                _freeMoves--;
                return;                       // bedava: AP de zaman da işlemez
            }

            int cost = _config != null ? _config.APPerMove : 1;
            SpendAP(cost);
        }

        /// <summary>
        /// Savaş/yerleştirme sırasında motoru dondurur: AP harcanmaz, dilim/gün ilerlemez.
        /// Savaşın TAMAMI giriş anında ödenen sabit AP'ye sayıldığı için gerekli.
        /// </summary>
        public void SetFrozen(bool frozen)
        {
            IsFrozen = frozen;

            // Savaşa girmek overworld yürüyüşünü kesiyor (oyuncu nesnesi pasifleşiyor → yol
            // coroutine'i yarıda kalıyor). Harcanmamış BEDAVA HAMLE stoku kalırsa savaştan sonra
            // sessizce bedava yürüyüş verirdi — Güçlü Yol Taşı'nın bedeli buharlaşırdı.
            if (frozen) _freeMoves = 0;
        }

        /// <summary>Zaman motorunu 1. günün başına sarar — BÖLÜM yeniden başlarken (TASK-007).
        /// Tüm run'ı sıfırlamaz; yalnız o haritanın gün/dilim/AP sayacını sıfırlar.</summary>
        public void ResetTime()
        {
            CurrentDay  = 1;
            CurrentSlot = 0;
            TotalMoves  = 0;
            IsFrozen    = false;
            _freeMoves  = 0;              // yeni bölüm → eski bedava hamle stoku taşınmaz
            CurrentAP   = MaxAP;
            OnAPChanged?.Invoke(CurrentAP, MaxAP);
            OnTimeAdvanced?.Invoke(CurrentDay, CurrentSlot, GetCurrentSlotName());
        }

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

        /// <summary>Karo başına hareket maliyeti (TimeSlotConfig.APPerMove).</summary>
        public int APPerMove => _config != null ? _config.APPerMove : 1;

        /// <summary>
        /// Verilen sayıda hamlenin KAÇ AP tutacağını ve KAÇ ZAMAN DİLİMİ devireceğini HARCAMADAN
        /// hesaplar — harita ekranındaki "gitmek şu kadara mal olur" önizlemesi için.
        ///
        /// Döngü <see cref="SpendAP(int,bool)"/>'in aynısıdır (AP sıfıra inince dilim ilerler ve
        /// AP tazelenir). Ayrı bir formülle tahmin etmek yerine gerçek kuralı taklit ediyor;
        /// aksi halde önizleme ile gerçek harcama zamanla ayrışırdı.
        /// </summary>
        public void PreviewCost(int moves, out int apCost, out int slotsAdvanced)
        {
            int per = APPerMove;
            moves   = Mathf.Max(0, moves);
            apCost  = moves * per;
            slotsAdvanced = 0;

            if (MaxAP <= 0) return;

            int cur = CurrentAP;
            for (int i = 0; i < moves; i++)
            {
                cur -= per;
                while (cur <= 0) { slotsAdvanced++; cur += MaxAP; }
            }
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
