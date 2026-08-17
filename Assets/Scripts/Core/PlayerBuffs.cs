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

        [Header("Yol taşları (harita ekranından seyahat)")]
        [Tooltip("TEST KOLAYLIĞI (kullanıcı isteği 2026-08-17): açıkken taşlar TÜKENMEZ ve sayaç " +
                 "'sınırsız' gösterir. Gerçek ekonomi kurulurken KAPATILACAK.")]
        [SerializeField] private bool _unlimitedTravelTokens = true;
        [Tooltip("Oyuna kaç YOL TAŞI ile başlanır (sınırsız kapalıyken anlamlı).")]
        [SerializeField, Min(0)] private int _startingRoadStones = 0;
        [Tooltip("Oyuna kaç GÜÇLÜ YOL TAŞI ile başlanır.")]
        [SerializeField, Min(0)] private int _startingPowerStones = 0;

        private readonly List<TimedBuff> _timed = new();

        /// <summary>Şu an aktif geçici etki sayısı (HUD göstergesi için).</summary>
        public int ActiveTimedCount => _timed.Count;

        // ── Yol taşları ──────────────────────────────────────────────────────

        /// <summary>İki taş türü. Tek bir sayaç dizisiyle tutuluyor: davranışları aynı
        /// (kazan / harca / say), FARKI harcandıklarında ne yaptıklarında.</summary>
        public enum TravelStone
        {
            Road  = 0,   // Yol Taşı — koşarak git, AP ve zaman NORMAL işler
            Power = 1    // Güçlü Yol Taşı — mesafeye göre birkaç tane, ama AP/zaman HARCANMAZ
        }

        private readonly int[] _stones = new int[2];

        /// <summary>Taşlar tükenmiyor mu? (test ayarı)</summary>
        public bool UnlimitedTravelTokens => _unlimitedTravelTokens;

        /// <summary>Taş sayısı değişti — harita ekranındaki sayaçlar dinler.</summary>
        public event System.Action OnTravelStonesChanged;

        public int Stones(TravelStone kind) => _stones[(int)kind];

        /// <summary>Bu kadar taş var mı? (sınırsız modda hep true)</summary>
        public bool HasStones(TravelStone kind, int count = 1)
            => _unlimitedTravelTokens || _stones[(int)kind] >= count;

        public void GrantStones(TravelStone kind, int count)
        {
            if (count <= 0) return;
            _stones[(int)kind] += count;
            OnTravelStonesChanged?.Invoke();
        }

        /// <summary>Taş harcar. Sınırsız modda düşmez ama yine true döner.</summary>
        public bool TrySpendStones(TravelStone kind, int count)
        {
            if (count <= 0) return true;
            if (_unlimitedTravelTokens) return true;
            if (_stones[(int)kind] < count) return false;

            _stones[(int)kind] -= count;
            OnTravelStonesChanged?.Invoke();
            return true;
        }

        private void Start()
        {
            _stones[(int)TravelStone.Road]  = _startingRoadStones;
            _stones[(int)TravelStone.Power] = _startingPowerStones;
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

                // Süreli DEĞİL: envanterde dururlar, harita ekranından seyahat ederken harcanırlar.
                case ShopEffectKind.FastTravelToken:
                    GrantStones(TravelStone.Road, item.Magnitude);
                    break;

                case ShopEffectKind.PowerTravelToken:
                    GrantStones(TravelStone.Power, item.Magnitude);
                    break;
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
