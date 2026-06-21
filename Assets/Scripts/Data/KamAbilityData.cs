using UnityEngine;

namespace TacticalRPG.Data
{
    /// <summary>
    /// Yetenek/büyü etki türü. Davranış (uygulama) kodu Faz 4 combat'ta gelir;
    /// bu enum sadece sınıflandırma içindir.
    /// </summary>
    public enum AbilityEffectType
    {
        Damage = 0, // hedefe hasar
        Heal   = 1, // hedefi iyileştir
        Buff   = 2  // geçici güçlendirme (detay sonra)
    }

    /// <summary>
    /// Tek bir Kam büyüsünün/yeteneğinin sabit verisi (mana, menzil, etki).
    /// Davranış İÇERMEZ — saf veri katmanı. Yeni büyü eklemek = yeni asset (kod gerekmez).
    /// Runtime'da hiçbir alan değiştirilmez (tıpkı CharacterClassData gibi).
    /// </summary>
    [CreateAssetMenu(menuName = "TacticalRPG/Kam Ability Data", fileName = "KamAbility")]
    public class KamAbilityData : ScriptableObject
    {
        [Header("Kimlik")]
        [SerializeField] private string _id          = "ability";
        [SerializeField] private string _displayName = "Adsız Büyü";
        [TextArea(2, 4)]
        [SerializeField] private string _description = "";
        [SerializeField] private Sprite _icon;

        [Header("Maliyet ve Menzil")]
        [Tooltip("Büyüyü kullanmak için gereken Kam manası (KamManaManager.TrySpendMana).")]
        [SerializeField, Min(0)] private int _manaCost = 1;
        [Tooltip("Hedefe en fazla kaç hex uzaktan kullanılabilir (HexCoordinate.DistanceTo).")]
        [SerializeField, Min(0)] private int _range = 1;

        [Header("Etki")]
        [SerializeField] private AbilityEffectType _effect = AbilityEffectType.Damage;
        [Tooltip("Damage = hasar miktarı, Heal = iyileşme, Buff = güçlendirme büyüklüğü.")]
        [SerializeField, Min(0)] private int _power = 1;

        // ── Kimlik ───────────────────────────────────────────────────────────
        public string Id          => _id;
        public string DisplayName => _displayName;
        public string Description => _description;
        public Sprite Icon        => _icon;

        // ── Maliyet / Menzil ──────────────────────────────────────────────────
        public int ManaCost => _manaCost;
        public int Range    => _range;

        // ── Etki ──────────────────────────────────────────────────────────────
        public AbilityEffectType Effect => _effect;
        public int               Power  => _power;
    }
}
