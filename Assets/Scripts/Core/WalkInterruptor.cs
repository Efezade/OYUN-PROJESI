using UnityEngine;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// YÜRÜYÜŞÜ OLAYDA OTOMATİK DURDURUR (2026-10-03, Efe'nin cevapsız kalan sorusu: "bir olay
    /// olunca yürüyüş kendiliğinden dursun mu?" → evet).
    ///
    /// Keşfedilmiş bölgede menzil sınırsız; tek tıkla 20+ karoluk yürüyüş olağan. Bu sırada oyunun
    /// kararını değiştiren bir şey olursa (yeni zorunlu görev düştü, gece çöktü) oyuncu yürümeye
    /// devam ederken bunu kaçırmamalı. Kam sıradaki karoda durur, HUD sebebi yazar, oyuncu yeniden
    /// karar verir. Bedel sorunu yok: AP karo başına ödeniyor (bkz <see cref="PlayerController"/>).
    ///
    /// YOL KOPMASI burada DEĞİL: önündeki karo çökerse <see cref="PlayerController"/> kendisi durur
    /// (güvenlik kuralı, kapatılamaz). Burası yalnız "haber ver" olaylarını taşır.
    ///
    /// HIZLI SEYAHATE KARIŞMAZ: yol taşı yolculuğu bedava hamlelerle yürüyor; yarıda kesmek
    /// oyuncunun ödediği hakkı yakardı.
    /// </summary>
    public class WalkInterruptor : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private PlayerController       _player;
        [SerializeField] private MandatoryQuestDirector _quests;
        [SerializeField] private ActionPointManager     _ap;

        [Header("Hangi olaylar durdursun")]
        [Tooltip("Yeni zorunlu görev gökten düştü.")]
        [SerializeField] private bool _onQuestUnlocked = true;
        [Tooltip("Gece çöktü (görüş daralır, market kapanır).")]
        [SerializeField] private bool _onNightfall     = true;
        [Tooltip("Gün doğdu. Varsayılan KAPALI: günde iki kez durmak uzun yürüyüşü bölük pörçük eder.")]
        [SerializeField] private bool _onDaybreak      = false;

        private void OnEnable()
        {
            if (_quests != null) _quests.OnQuestUnlocked += HandleQuestUnlocked;
            if (_ap     != null) _ap.OnDayNightChanged   += HandleDayNight;
        }

        private void OnDisable()
        {
            if (_quests != null) _quests.OnQuestUnlocked -= HandleQuestUnlocked;
            if (_ap     != null) _ap.OnDayNightChanged   -= HandleDayNight;
        }

        private void HandleQuestUnlocked(int tier, HexCoordinate coord)
        {
            if (_onQuestUnlocked) Interrupt($"{tier}. zorunlu görev düştü");
        }

        private void HandleDayNight(bool isNight)
        {
            if (isNight && _onNightfall)  Interrupt("gece çöktü");
            if (!isNight && _onDaybreak)  Interrupt("gün doğdu");
        }

        private void Interrupt(string reason)
        {
            if (_player == null || !_player.IsMoving || _player.IsFastTravel) return;
            _player.RequestStop(reason);
        }
    }
}
