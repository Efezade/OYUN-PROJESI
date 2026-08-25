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
    /// </summary>
    public class PlayerBuffs : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private PlayerController    _player;
        [SerializeField] private MapInputHandler     _input;
        [SerializeField] private ActionPointManager  _apManager;

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
            if (_player != null) _player.OnMoved += HandleMoved;
        }

        private void OnDisable()
        {
            if (_player != null) _player.OnMoved -= HandleMoved;
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
