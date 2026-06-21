using System;
using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Kam'ın yeteneklerini kullanan kaster.
    /// 1/2/3 tuşlarıyla yetenek "hazırlar" (arm), hedefe tıklanınca uygular.
    /// Menzil = oyuncu konumundan (PlayerController.CurrentCoord) hedefe hex mesafesi.
    /// Mana KamManaManager'dan harcanır. Etki uygulaması Unit üzerine.
    /// (Tam combat/initiative Faz 3-4; bu, dikey dilim için kaster çekirdeği.)
    /// </summary>
    public class AbilityCaster : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private PlayerController _player;
        [SerializeField] private KamManaManager   _kamMana;
        [SerializeField] private PartyManager     _partyManager;
        [SerializeField] private UnitManager      _unitManager;

        [Header("Ayar")]
        [Tooltip("Yetenekleri hangi parti sınıfından okuyacağı.")]
        [SerializeField] private string _casterClassName = "Kam";

        private IReadOnlyList<KamAbilityData> _abilities;

        public IReadOnlyList<KamAbilityData> Abilities    => _abilities;
        public KamAbilityData                ArmedAbility { get; private set; }
        public bool                          HasArmedAbility => ArmedAbility != null;

        /// <summary>Hazırlanan yetenek / durum değişti (HUD yenilensin).</summary>
        public event Action         OnStateChanged;
        /// <summary>Kullanıcıya gösterilecek kısa geri bildirim metni.</summary>
        public event Action<string> OnCastMessage;

        private void Start()
        {
            if (_partyManager != null)
            {
                CharacterCard card = _partyManager.FindByClass(_casterClassName);
                if (card != null) _abilities = card.Data.Abilities;
            }

            if (_abilities == null || _abilities.Count == 0)
                Debug.LogWarning($"[AbilityCaster] '{_casterClassName}' için yetenek bulunamadı. " +
                                 "Faz 3 kurulumunu çalıştırdın mı / Kam'a yetenek atandı mı?");
        }

        private void Update()
        {
            if      (Input.GetKeyDown(KeyCode.Alpha1)) ArmIndex(0);
            else if (Input.GetKeyDown(KeyCode.Alpha2)) ArmIndex(1);
            else if (Input.GetKeyDown(KeyCode.Alpha3)) ArmIndex(2);
            else if (Input.GetKeyDown(KeyCode.Escape) && HasArmedAbility) Disarm();
        }

        private void ArmIndex(int index)
        {
            if (_abilities == null || index < 0 || index >= _abilities.Count) return;
            ArmedAbility = _abilities[index];
            OnStateChanged?.Invoke();
        }

        private void Disarm()
        {
            ArmedAbility = null;
            OnStateChanged?.Invoke();
        }

        /// <summary>Hazırlanmış yeteneği hedef koordinata uygular. Başarı durumunu döndürür.</summary>
        public bool TryCastAt(HexCoordinate targetCoord)
        {
            if (!HasArmedAbility)               return false;
            if (_unitManager == null || _player == null) return false;

            KamAbilityData ability = ArmedAbility;

            Unit target = _unitManager.GetUnitAt(targetCoord);
            if (target == null) { Message("Hedefte birim yok.");                       return false; }

            int dist = _player.CurrentCoord.DistanceTo(targetCoord);
            if (dist > ability.Range) { Message($"Menzil disi ({dist} > {ability.Range})."); return false; }

            if (_kamMana == null || !_kamMana.CanCast(ability.ManaCost))
            { Message($"Yetersiz mana ({ability.ManaCost} gerek)."); return false; }

            _kamMana.TrySpendMana(ability.ManaCost);
            ApplyEffect(ability, target);

            Message($"{ability.DisplayName} -> {target.DisplayName}  ({ability.Effect} {ability.Power})");
            Disarm(); // tek kullanım sonrası bırak
            return true;
        }

        private static void ApplyEffect(KamAbilityData ability, Unit target)
        {
            switch (ability.Effect)
            {
                case AbilityEffectType.Damage: target.TakeDamage(ability.Power); break;
                case AbilityEffectType.Heal:   target.Heal(ability.Power);       break;
                case AbilityEffectType.Buff:   target.AddShield(ability.Power);  break;
            }
        }

        private void Message(string text)
        {
            Debug.Log($"[Cast] {text}");
            OnCastMessage?.Invoke(text);
        }
    }
}
