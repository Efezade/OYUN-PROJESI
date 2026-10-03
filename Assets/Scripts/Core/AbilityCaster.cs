using System;
using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Kam'ın (komutan) savaştaki büyü kasteri.
    /// Origin = komutan BİRİMİNİN hex konumu (PlayerController değil); yetenekler komutanın
    /// kartından okunur. Büyü, Kam'ın TUR EYLEMİDİR: yalnızca Kam'ın sırasında ve eylemi
    /// harcanmamışken 1/2/3 ile hazırlanır, hedefe tıklanınca uygulanır. Bedel Kam'ın AKTİF MEKANİĞİNDEN
    /// (<see cref="KamMechanicHost"/>) düşer — mana mı, can mı bilmez; gücü de eşik etkisinden geçer.
    /// Başarı sonrası TurnManager'a eylem bildirilir (win/lose + otomatik tur sonu).
    /// (Event-driven; bağımlılık tek yönlü: AbilityCaster → TurnManager/UnitManager/KamMechanicHost.)
    /// </summary>
    public class AbilityCaster : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private TurnManager    _turnManager;
        [UnityEngine.Serialization.FormerlySerializedAs("_kamMana")]
        [SerializeField] private KamMechanicHost _kam;
        [SerializeField] private UnitManager    _unitManager;

        public KamAbilityData ArmedAbility    { get; private set; }
        public bool           HasArmedAbility => ArmedAbility != null;

        /// <summary>Aktif komutanın (Kam) yetenek listesi — yoksa null.</summary>
        public IReadOnlyList<KamAbilityData> Abilities
        {
            get
            {
                Unit cmd = _unitManager != null ? _unitManager.GetCommander() : null;
                return cmd != null && cmd.Card != null ? cmd.Card.Data.Abilities : null;
            }
        }

        /// <summary>Hazırlanan yetenek / durum değişti (HUD yenilensin).</summary>
        public event Action         OnStateChanged;
        /// <summary>Kullanıcıya gösterilecek kısa geri bildirim metni.</summary>
        public event Action<string> OnCastMessage;

        private void Update()
        {
            // Yalnızca Kam'ın oyuncu turunda büyü hazırlanabilir.
            if (ActiveCommander() == null)
            {
                if (HasArmedAbility) Disarm();
                return;
            }

            if      (Input.GetKeyDown(KeyCode.Alpha1)) ArmAbility(0);
            else if (Input.GetKeyDown(KeyCode.Alpha2)) ArmAbility(1);
            else if (Input.GetKeyDown(KeyCode.Alpha3)) ArmAbility(2);
            else if (Input.GetKeyDown(KeyCode.Escape) && HasArmedAbility) Disarm();
        }

        // ── Hazırlama ─────────────────────────────────────────────────────────

        /// <summary>Belirtilen yeteneği hazırlar (yalnızca Kam'ın turunda, eylemi harcanmamışken).</summary>
        public void ArmAbility(int index)
        {
            Unit cmd = ActiveCommander();
            if (cmd == null) return;
            if (_turnManager.CurrentHasActed) { Message("Bu tur eylem zaten yapildi."); return; }

            var list = cmd.Card.Data.Abilities;
            if (list == null || index < 0 || index >= list.Count) return;

            ArmedAbility = list[index];
            OnStateChanged?.Invoke();
        }

        public void Disarm()
        {
            if (!HasArmedAbility) return;
            ArmedAbility = null;
            OnStateChanged?.Invoke();
        }

        // ── Uygulama ──────────────────────────────────────────────────────────

        /// <summary>Hazırlanmış yeteneği hedef koordinata uygular. Başarı durumunu döndürür.</summary>
        public bool TryCastAt(HexCoordinate targetCoord)
        {
            if (!HasArmedAbility) return false;

            Unit cmd = ActiveCommander();
            if (cmd == null) { Disarm(); return false; }
            if (_turnManager.CurrentHasActed) { Message("Bu tur eylem zaten yapildi."); return false; }

            KamAbilityData ability = ArmedAbility;

            Unit target = _unitManager != null ? _unitManager.GetUnitAt(targetCoord) : null;
            if (target == null) { Message("Hedefte birim yok."); return false; }

            // Hedef türü: hasar düşmana, iyileştirme/güçlendirme dosta (Kam dahil).
            bool validTarget = ability.Effect == AbilityEffectType.Damage
                ? target.Team == UnitTeam.Enemy
                : target.Team == UnitTeam.Player;
            if (!validTarget)
            {
                Message(ability.Effect == AbilityEffectType.Damage
                    ? "Hasar buyusu dusmana kullanilir."
                    : "Bu buyu dost birime kullanilir.");
                return false;
            }

            int dist = cmd.Coordinate.DistanceTo(targetCoord);
            if (dist > ability.Range) { Message($"Menzil disi ({dist} > {ability.Range})."); return false; }

            if (_kam == null || !_kam.CanPay(ability.ManaCost))
            {
                string res = _kam != null ? _kam.ResourceName : "mana";
                Message($"Yetersiz {res} ({ability.ManaCost} gerek)."); return false;
            }

            _kam.TryPay(ability.ManaCost);
            // EŞİK ETKİSİ: güç, bedel ÖDENDİKTEN sonra okunur — "kaynağın azaldıkça güçlenirsin"
            // türü bir mekanikte son ödenen bedel de eşiğe sayılmalı.
            int power = _kam.ModifyPower(ability.Power);
            ApplyEffect(ability, target, power);
            Message($"{ability.DisplayName} -> {target.DisplayName}  ({ability.Effect} {power})");

            Disarm();
            _turnManager.RegisterCommanderAction(); // eylemi tüket + win/lose + otomatik tur sonu
            return true;
        }

        private static void ApplyEffect(KamAbilityData ability, Unit target, int power)
        {
            switch (ability.Effect)
            {
                case AbilityEffectType.Damage: target.TakeDamage(power); break;
                case AbilityEffectType.Heal:   target.Heal(power);       break;
                case AbilityEffectType.Buff:   target.AddShield(power);  break;
            }
        }

        // ── Yardımcılar ───────────────────────────────────────────────────────

        // Şu an oynayabilir durumdaki komutan birimi (Kam'ın oyuncu turu) — değilse null.
        private Unit ActiveCommander()
        {
            if (_turnManager == null || !_turnManager.IsPlayerTurn) return null;
            Unit cur = _turnManager.CurrentUnit;
            return (cur != null && cur.IsCommander && cur.Card != null) ? cur : null;
        }

        private void Message(string text)
        {
            Debug.Log($"[Cast] {text}");
            OnCastMessage?.Invoke(text);
        }
    }
}
