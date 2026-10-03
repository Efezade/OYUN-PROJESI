using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Mağazadan satın alınan etkileri UYGULAYAN merkez (öz zaten <c>EssenceWallet.TrySpend</c> ile
    /// düşülmüş olarak gelir — burası sadece etkiyi tatbik eder). Etki türleri <see cref="ShopEffectKind"/>:
    ///   • BonusAPNow → anında <see cref="ActionPointManager.GrantAP"/> (tek seferlik).
    ///   • MoveSpeed  → <see cref="PlayerController.SpeedMultiplier"/> artışı (geçici ya da kalıcı).
    ///   • MoveRange  → <see cref="MapInputHandler.BonusMoveRange"/> artışı (geçici ya da kalıcı).
    ///
    /// GEÇİCİ etkiler ADIM (oyuncu hareketi) ile ölçülür: <see cref="PlayerController.OnMoved"/>'de
    /// geri sayılır, biten etki geri alınır. Sistemlere tek yönlü dokunur (event-driven, CLAUDE.md).
    ///
    /// POTLAR (2026-10-03): ÇANTA'da içilen pot <see cref="ApplyPotion"/> ile buraya gelir. Ana
    /// harita potları adımla, SAVAŞ potları savaş sayısıyla biter; savaş potlarının toplamı
    /// <see cref="CombatTraits"/> ile yerleştirmede her birime eklenir.
    /// </summary>
    public class PlayerBuffs : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private PlayerController    _player;
        [SerializeField] private MapInputHandler     _input;
        [SerializeField] private ActionPointManager  _apManager;
        [Tooltip("Kâhin Tütsüsü (sisi kalıcı açar) için.")]
        [SerializeField] private FogOfWarManager     _fog;
        [Tooltip("Savaş potlarının süresi savaş bitince düşer.")]
        [SerializeField] private TurnManager         _turns;

        private sealed class TimedBuff
        {
            public ShopEffectKind kind;
            public float speedDelta;  // MoveSpeed için
            public int   rangeDelta;  // MoveRange için
            public int   movesLeft;
        }

        [Header("Güçlü yol taşı (harita ekranından seyahat)")]
        [Tooltip("TEST KOLAYLIĞI (kullanıcı isteği 2026-08-17): açıkken taşlar TÜKENMEZ ve sayaç " +
                 "'sınırsız' gösterir. Gerçek ekonomi kurulurken KAPATILACAK.")]
        [SerializeField] private bool _unlimitedTravelTokens = true;
        [Tooltip("Oyuna kaç GÜÇLÜ YOL TAŞI ile başlanır (sınırsız kapalıyken anlamlı).")]
        [SerializeField, Min(0)] private int _startingPowerStones = 0;

        private readonly List<TimedBuff> _timed = new();

        // ── Pot durumu ───────────────────────────────────────────────────────
        private sealed class PotionTimed  { public PotionSO Potion; public int MovesLeft; }
        private sealed class PotionCombat { public PotionSO Potion; public EvolutionTraits Traits; public int BattlesLeft; }

        private readonly List<PotionTimed>  _potionTimed  = new();
        private readonly List<PotionCombat> _potionCombat = new();
        private int _freeCollect;   // açık "Toplayıcı Özü" sayısı

        /// <summary>Etkin pot listesi değişti (ÇANTA → POTLAR dinler).</summary>
        public event System.Action OnPotionsChanged;

        /// <summary>Öz toplamak şu an AP harcamıyor mu? (Toplayıcı Özü)</summary>
        public bool FreeCollectActive => _freeCollect > 0;

        /// <summary>Sonraki savaşta birliğe işleyecek pot etkilerinin toplamı.</summary>
        public EvolutionTraits CombatTraits
        {
            get
            {
                var t = new EvolutionTraits();
                foreach (var c in _potionCombat) t.Merge(c.Traits);
                return t;
            }
        }

        /// <summary>Etkin potların kısa listesi ("Yel İksiri — 7 adım").</summary>
        public void FillActivePotions(List<string> into)
        {
            into.Clear();
            foreach (var t in _potionTimed)  into.Add($"{t.Potion.DisplayName} — {t.MovesLeft} adım kaldı");
            foreach (var c in _potionCombat) into.Add($"{c.Potion.DisplayName} — sonraki {c.BattlesLeft} savaş");
        }

        /// <summary>Şu an aktif geçici etki sayısı (HUD göstergesi için).</summary>
        public int ActiveTimedCount => _timed.Count;

        // ── Güçlü yol taşı ───────────────────────────────────────────────────
        // TEK taş türü var. Eskiden ikiydi (ucuz "Yol Taşı" + "Güçlü Yol Taşı"); ucuz olan
        // 2026-08-19'da kullanıcı isteğiyle kaldırıldı, onunla birlikte tür ayrımı da gitti.

        private int _stones;

        /// <summary>Taşlar tükenmiyor mu? (test ayarı)</summary>
        public bool UnlimitedTravelTokens => _unlimitedTravelTokens;

        /// <summary>Taş sayısı değişti — harita ekranındaki sayaç dinler.</summary>
        public event System.Action OnTravelStonesChanged;

        public int Stones() => _stones;

        /// <summary>Bu kadar taş var mı? (sınırsız modda hep true)</summary>
        public bool HasStones(int count = 1) => _unlimitedTravelTokens || _stones >= count;

        public void GrantStones(int count)
        {
            if (count <= 0) return;
            _stones += count;
            OnTravelStonesChanged?.Invoke();
        }

        /// <summary>Taş harcar. Sınırsız modda düşmez ama yine true döner.</summary>
        public bool TrySpendStones(int count)
        {
            if (count <= 0) return true;
            if (_unlimitedTravelTokens) return true;
            if (_stones < count) return false;

            _stones -= count;
            OnTravelStonesChanged?.Invoke();
            return true;
        }

        private void Start()
        {
            _stones = _startingPowerStones;
            OnTravelStonesChanged?.Invoke();
        }

        private void OnEnable()
        {
            if (_player != null) _player.OnMoved       += HandleMoved;
            if (_turns  != null) _turns.OnCombatEnded  += HandleCombatEnded;
        }

        private void OnDisable()
        {
            if (_player != null) _player.OnMoved       -= HandleMoved;
            if (_turns  != null) _turns.OnCombatEnded  -= HandleCombatEnded;
        }

        // ── Potlar ───────────────────────────────────────────────────────────

        /// <summary>İçilen potu uygular (pot envanterden ÇAĞIRAN tarafından düşülmüş olmalı).</summary>
        public void ApplyPotion(PotionSO potion)
        {
            if (potion == null) return;
            switch (potion.Kind)
            {
                case PotionEffectKind.MoveRange:
                    if (_input != null) _input.BonusMoveRange += potion.Magnitude;
                    _potionTimed.Add(new PotionTimed { Potion = potion, MovesLeft = Mathf.Max(1, potion.Duration) });
                    break;

                case PotionEffectKind.MoveSpeed:
                    if (_player != null) _player.SpeedMultiplier += potion.Magnitude / 100f;
                    _potionTimed.Add(new PotionTimed { Potion = potion, MovesLeft = Mathf.Max(1, potion.Duration) });
                    break;

                case PotionEffectKind.Vision:
                    if (_player != null) { _player.VisionBonus += potion.Magnitude; _player.RefreshVision(); }
                    _potionTimed.Add(new PotionTimed { Potion = potion, MovesLeft = Mathf.Max(1, potion.Duration) });
                    break;

                case PotionEffectKind.FreeCollect:
                    _freeCollect++;
                    _potionTimed.Add(new PotionTimed { Potion = potion, MovesLeft = Mathf.Max(1, potion.Duration) });
                    break;

                case PotionEffectKind.BonusAP:
                    _apManager?.GrantAP(potion.Magnitude);
                    break;

                case PotionEffectKind.RevealArea:
                    if (_fog != null && _player != null) _fog.RevealAreaPermanent(_player.CurrentCoord, potion.Magnitude);
                    break;

                case PotionEffectKind.TravelStones:
                    GrantStones(potion.Magnitude);
                    break;

                case PotionEffectKind.CombatTrait:
                    _potionCombat.Add(new PotionCombat
                    {
                        Potion = potion, Traits = potion.CombatTraits(), BattlesLeft = Mathf.Max(1, potion.Duration)
                    });
                    break;
            }
            Debug.Log($"[Pot] {potion.DisplayName} icildi ({potion.DurationText}).");
            OnPotionsChanged?.Invoke();
        }

        /// <summary>Süresi biten ana harita potunun etkisini geri alır.</summary>
        private void RevertPotion(PotionSO potion)
        {
            switch (potion.Kind)
            {
                case PotionEffectKind.MoveRange:   if (_input  != null) _input.BonusMoveRange   -= potion.Magnitude; break;
                case PotionEffectKind.MoveSpeed:   if (_player != null) _player.SpeedMultiplier -= potion.Magnitude / 100f; break;
                case PotionEffectKind.Vision:      if (_player != null) _player.VisionBonus     -= potion.Magnitude; break;
                case PotionEffectKind.FreeCollect: _freeCollect = Mathf.Max(0, _freeCollect - 1); break;
            }
        }

        private void HandleCombatEnded(CombatResult _)
        {
            if (_potionCombat.Count == 0) return;
            for (int i = _potionCombat.Count - 1; i >= 0; i--)
                if (--_potionCombat[i].BattlesLeft <= 0) _potionCombat.RemoveAt(i);
            OnPotionsChanged?.Invoke();
        }

        /// <summary>Bir satın alımı uygular. Öz bedeli çağırandan ÖNCE düşülmüş olmalıdır.</summary>
        public void ApplyPurchase(ShopItemSO item)
        {
            if (item == null) return;

            switch (item.Effect)
            {
                case ShopEffectKind.BonusAPNow:
                    _apManager?.GrantAP(item.Magnitude);
                    break;

                case ShopEffectKind.MoveSpeed:
                {
                    float delta = item.Magnitude / 100f; // 100 = +%100
                    if (_player != null) _player.SpeedMultiplier += delta;
                    if (!item.IsPermanent && item.DurationMoves > 0)
                        _timed.Add(new TimedBuff { kind = ShopEffectKind.MoveSpeed, speedDelta = delta, movesLeft = item.DurationMoves });
                    break;
                }

                case ShopEffectKind.MoveRange:
                {
                    if (_input != null) _input.BonusMoveRange += item.Magnitude;
                    if (!item.IsPermanent && item.DurationMoves > 0)
                        _timed.Add(new TimedBuff { kind = ShopEffectKind.MoveRange, rangeDelta = item.Magnitude, movesLeft = item.DurationMoves });
                    break;
                }

                // Süreli DEĞİL: envanterde durur, harita ekranından seyahat ederken harcanır.
                case ShopEffectKind.PowerTravelToken:
                    GrantStones(item.Magnitude);
                    break;

                // ShopEffectKind.FastTravelToken (ucuz "Yol Taşı") 2026-08-19'da EMEKLİ EDİLDİ:
                // dükkân kataloğundan çıkarıldı, karşılığı da kalmadı. Enum girişi DURUYOR —
                // ShopItemSO asset'leri etkiyi INDEKS olarak saklıyor, üyeyi silmek diğer
                // etkilerin indeksini kaydırıp mevcut asset'leri bozardı.
            }
        }

        private void HandleMoved(HexCoordinate _)
        {
            // Pot süreleri (adım).
            if (_potionTimed.Count > 0)
            {
                for (int i = _potionTimed.Count - 1; i >= 0; i--)
                {
                    if (--_potionTimed[i].MovesLeft > 0) continue;
                    RevertPotion(_potionTimed[i].Potion);
                    _potionTimed.RemoveAt(i);
                }
                OnPotionsChanged?.Invoke();
            }

            if (_timed.Count == 0) return;

            for (int i = _timed.Count - 1; i >= 0; i--)
            {
                var b = _timed[i];
                b.movesLeft--;
                if (b.movesLeft > 0) continue;

                // Süre bitti → etkiyi geri al.
                if (b.kind == ShopEffectKind.MoveSpeed && _player != null) _player.SpeedMultiplier -= b.speedDelta;
                if (b.kind == ShopEffectKind.MoveRange && _input  != null) _input.BonusMoveRange   -= b.rangeDelta;
                _timed.RemoveAt(i);
            }
        }
    }
}
