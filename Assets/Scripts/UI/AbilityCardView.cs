using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TacticalRPG.Data;

namespace TacticalRPG.UI
{
    /// <summary>
    /// ÇANTA'da tek bir KAM KARTI: <see cref="KamAbilityData"/>'dan ad + maliyet/menzil + açıklama
    /// gösterir. <see cref="_ability"/> null ise "BOŞ SLOT" olarak çizilir (henüz öğrenilmemiş kart).
    /// Salt görünüm — SO verisi runtime'da değişmez; kart seçme/kullanma etkileşimi savaş fazında gelir.
    ///
    /// İkon yoksa etki türüne göre renk (Damage=kızıl, Heal=yeşil, Buff=mavi) — böyle de ayırt edilir
    /// (<see cref="ClassBookEntry"/>'nin portre-yoksa-renk mantığıyla aynı desen).
    /// </summary>
    public class AbilityCardView : MonoBehaviour
    {
        [Tooltip("Bu kartın büyü verisi. BOŞSA boş slot olarak gösterilir.")]
        [SerializeField] private KamAbilityData _ability;

        [SerializeField] private Image           _icon;
        [SerializeField] private TextMeshProUGUI _nameLabel;
        [SerializeField] private TextMeshProUGUI _statLabel;
        [SerializeField] private TextMeshProUGUI _descLabel;
        [Tooltip("_ability null iken görünür olacak 'boş slot' kaplaması (opsiyonel).")]
        [SerializeField] private GameObject      _emptyOverlay;

        public bool IsEmpty => _ability == null;

        private void OnEnable() => Refresh();

        public void Refresh()
        {
            bool empty = _ability == null;
            if (_emptyOverlay != null) _emptyOverlay.SetActive(empty);

            if (empty)
            {
                if (_nameLabel != null) _nameLabel.text = "BOŞ";
                if (_statLabel != null) _statLabel.text = "";
                if (_descLabel != null) _descLabel.text = "";
                if (_icon != null) { _icon.sprite = null; _icon.color = new Color(0.18f, 0.16f, 0.14f, 1f); }
                return;
            }

            if (_nameLabel != null) _nameLabel.text = _ability.DisplayName;
            if (_statLabel != null) _statLabel.text = $"{_ability.ManaCost} mana · {_ability.Range} menzil";
            if (_descLabel != null)
                _descLabel.text = string.IsNullOrEmpty(_ability.Description)
                    ? EffectText(_ability.Effect, _ability.Power)
                    : _ability.Description;

            if (_icon != null)
            {
                if (_ability.Icon != null) { _icon.sprite = _ability.Icon; _icon.color = Color.white; }
                else                       { _icon.sprite = null; _icon.color = EffectColor(_ability.Effect); }
            }
        }

        private static string EffectText(AbilityEffectType e, int power) => e switch
        {
            AbilityEffectType.Damage => $"{power} hasar",
            AbilityEffectType.Heal   => $"{power} iyileşme",
            AbilityEffectType.Buff   => $"+{power} güçlendirme",
            _                        => ""
        };

        private static Color EffectColor(AbilityEffectType e) => e switch
        {
            AbilityEffectType.Damage => new Color(0.72f, 0.25f, 0.20f, 1f),
            AbilityEffectType.Heal   => new Color(0.30f, 0.62f, 0.35f, 1f),
            AbilityEffectType.Buff   => new Color(0.30f, 0.45f, 0.70f, 1f),
            _                        => Color.gray
        };
    }
}
