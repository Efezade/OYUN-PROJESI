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

        private readonly List<TimedBuff> _timed = new();

        /// <summary>Şu an aktif geçici etki sayısı (HUD göstergesi için).</summary>
        public int ActiveTimedCount => _timed.Count;

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
