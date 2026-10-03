using System.Collections.Generic;
using UnityEngine;

namespace TacticalRPG.Data
{
    /// <summary>
    /// Potun ne yaptığı. Üç aile:
    ///   • ANA HARİTA, SÜRELİ (adım): <see cref="MoveRange"/>, <see cref="MoveSpeed"/>,
    ///     <see cref="Vision"/>, <see cref="FreeCollect"/> — Süre = kaç ADIM.
    ///   • ANINDA: <see cref="BonusAP"/>, <see cref="RevealArea"/>, <see cref="TravelStones"/>.
    ///   • SAVAŞ: <see cref="CombatTrait"/> — sonraki Süre kadar SAVAŞTA tüm birliğe işler.
    /// Uygulaması <c>PlayerBuffs.ApplyPotion</c>'da.
    /// </summary>
    public enum PotionEffectKind
    {
        MoveRange,      // tek tıkla yürüme menzili +N karo (sis içine doğru)
        MoveSpeed,      // yürüme hızı +%N (yalnız görsel — AP aynı)
        Vision,         // görüş yarıçapı +N karo
        FreeCollect,    // öz toplamak AP harcamaz
        BonusAP,        // anında +N AP
        RevealArea,     // bulunduğun yerin N karo çevresinde sisi KALICI açar
        TravelStones,   // +N güçlü yol taşı
        CombatTrait     // sonraki savaş(lar)da birliğe özellik (evrim kümesinden)
    }

    /// <summary>
    /// GEÇİCİ İKSİR (Efe'nin isteği 2026-10-03): markette alınır, ÇANTA → POTLAR'da birikir,
    /// "İÇ" ile kullanılır. Etkisi süreli ya da anlıktır; kalıcı güç EŞYA'nın işidir.
    /// </summary>
    [CreateAssetMenu(fileName = "Pot", menuName = "TacticalRPG/Pot")]
    public class PotionSO : ScriptableObject
    {
        [SerializeField] private string _id = "pot";
        [SerializeField] private string _displayName = "Pot";
        [TextArea(2, 3)]
        [SerializeField] private string _description = "";
        [SerializeField] private Sprite _icon;
        [SerializeField] private Color _color = Color.white;
        [SerializeField] private ItemRarity _rarity;
        [SerializeField] private EssenceAmount[] _price;

        [Header("Etki")]
        [SerializeField] private PotionEffectKind _kind;
        [Tooltip("Büyüklük (karo / yüzde / AP / taş sayısı). CombatTrait'te birinci özelliğin miktarı.")]
        [SerializeField] private int _magnitude = 1;
        [Tooltip("Süre: süreli ana harita potlarında ADIM, savaş potlarında SAVAŞ sayısı.")]
        [SerializeField, Min(0)] private int _duration;

        [Header("Savaş potu (CombatTrait)")]
        [SerializeField] private EvolutionTrait _trait;
        [SerializeField] private EvolutionTrait _trait2;
        [SerializeField] private int _amount2;

        public string           Id          => _id;
        public string           DisplayName => _displayName;
        public string           Description => _description;
        public Sprite           Icon        => _icon;
        public Color            Color       => _color;
        public ItemRarity       Rarity      => _rarity;
        public IReadOnlyList<EssenceAmount> Price => _price ?? (IReadOnlyList<EssenceAmount>)System.Array.Empty<EssenceAmount>();
        public PotionEffectKind Kind        => _kind;
        public int              Magnitude   => _magnitude;
        public int              Duration    => _duration;

        /// <summary>Savaş potunun birliğe verdiği özellikler.</summary>
        public EvolutionTraits CombatTraits()
        {
            var t = new EvolutionTraits();
            if (_kind != PotionEffectKind.CombatTrait) return t;
            if (_trait  != EvolutionTrait.None) t.Add(_trait,  _magnitude);
            if (_trait2 != EvolutionTrait.None) t.Add(_trait2, _amount2);
            return t;
        }

        /// <summary>Süre etiketi: "12 adım" / "2 savaş" / "anında".</summary>
        public string DurationText => _kind switch
        {
            PotionEffectKind.MoveRange or PotionEffectKind.MoveSpeed or
            PotionEffectKind.Vision    or PotionEffectKind.FreeCollect => $"{_duration} adım",
            PotionEffectKind.CombatTrait => $"{Mathf.Max(1, _duration)} savaş",
            _ => "anında"
        };
    }
}
