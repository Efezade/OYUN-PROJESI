using System.Collections.Generic;
using UnityEngine;

namespace TacticalRPG.Data
{
    /// <summary>
    /// EVRİMİN getirdiği kalıcı özellik (Efe'nin isteği 2026-10-03): evrim KİTAP'ta özle açılır,
    /// o sınıftan savaşa inen HER birime işler. Örnek: Okçu II. evrim → saldırısı iki ok atar.
    /// Çözümlemesi <c>Unit</c>'te (saldırı) ve <c>TurnManager</c>'da (tur başı).
    /// </summary>
    public enum EvolutionTrait
    {
        None,
        BonusAttack,        // +N saldırı
        BonusDefense,       // +N savunma
        BonusMove,          // +N hareket
        BonusRange,         // +N saldırı menzili
        BonusSpeed,         // +N hız (sırada öne geçer)
        DoubleStrike,       // normal saldırı İKİNCİ kez vurur (%N gücünde) — "çift ok"
        Lifesteal,          // verdiği hasarın %N'i kadar can kazanır
        Thorns,             // yakından vuran düşman N hasar geri alır
        Cleave,             // hedefin yanındaki düşmanlar da %N hasar alır
        StartShield,        // savaşa N kalkanla iner
        Regen,              // kendi turunun başında N can yeniler
        CooldownReduction   // sınıf yeteneklerinin bekleme süresi N tur kısalır
    }

    /// <summary>Bir sınıfın tek bir evrim basamağı (I · II · III).</summary>
    [System.Serializable]
    public class ClassEvolution
    {
        [SerializeField] private string _name = "Evrim";
        [Tooltip("Kitapta slotun içinde yazan KISA açıklama (= davranışın sözleşmesi).")]
        [SerializeField] private string _description = "";
        [Tooltip("Açma bedeli (öz). Şimdilik yer tutucu sayılar.")]
        [SerializeField] private EssenceAmount[] _cost;
        [SerializeField] private EvolutionTrait _trait;
        [Tooltip("Özelliğin büyüklüğü (trait'e göre: düz sayı ya da yüzde).")]
        [SerializeField] private int _amount = 1;

        public string         Name        => _name;
        public string         Description => _description;
        public IReadOnlyList<EssenceAmount> Cost => _cost ?? (IReadOnlyList<EssenceAmount>)System.Array.Empty<EssenceAmount>();
        public EvolutionTrait Trait       => _trait;
        public int            Amount      => _amount;
    }

    /// <summary>
    /// Bir birimin savaşta açık olan evrimlerinin TOPLAMI. Savaşa inerken bir kez hesaplanır ve
    /// birime verilir — her saldırıda evrim listesi taranmaz.
    /// </summary>
    public struct EvolutionTraits
    {
        public int Attack, Defense, Move, Range, Speed;
        public int DoubleStrikePct, LifestealPct, Thorns, CleavePct, StartShield, Regen, CooldownReduction;

        public void Add(EvolutionTrait trait, int amount)
        {
            switch (trait)
            {
                case EvolutionTrait.BonusAttack:       Attack            += amount; break;
                case EvolutionTrait.BonusDefense:      Defense           += amount; break;
                case EvolutionTrait.BonusMove:         Move              += amount; break;
                case EvolutionTrait.BonusRange:        Range             += amount; break;
                case EvolutionTrait.BonusSpeed:        Speed             += amount; break;
                case EvolutionTrait.DoubleStrike:      DoubleStrikePct   += amount; break;
                case EvolutionTrait.Lifesteal:         LifestealPct      += amount; break;
                case EvolutionTrait.Thorns:            Thorns            += amount; break;
                case EvolutionTrait.Cleave:            CleavePct         += amount; break;
                case EvolutionTrait.StartShield:       StartShield       += amount; break;
                case EvolutionTrait.Regen:             Regen             += amount; break;
                case EvolutionTrait.CooldownReduction: CooldownReduction += amount; break;
            }
        }

        /// <summary>Başka bir toplamı bu toplama ekler (evrim + eşya + pot birleşir).</summary>
        public void Merge(in EvolutionTraits o)
        {
            Attack += o.Attack; Defense += o.Defense; Move += o.Move; Range += o.Range; Speed += o.Speed;
            DoubleStrikePct += o.DoubleStrikePct; LifestealPct += o.LifestealPct; Thorns += o.Thorns;
            CleavePct += o.CleavePct; StartShield += o.StartShield; Regen += o.Regen;
            CooldownReduction += o.CooldownReduction;
        }

        /// <summary>HUD satırı: "+1 menzil · çift vuruş %70". Boşsa boş metin.</summary>
        public string Describe()
        {
            var parts = new List<string>();
            if (Attack  != 0) parts.Add($"+{Attack} saldırı");
            if (Defense != 0) parts.Add($"+{Defense} savunma");
            if (Move    != 0) parts.Add($"+{Move} hareket");
            if (Range   != 0) parts.Add($"+{Range} menzil");
            if (Speed   != 0) parts.Add($"+{Speed} hız");
            if (DoubleStrikePct   > 0) parts.Add($"çift vuruş %{DoubleStrikePct}");
            if (LifestealPct      > 0) parts.Add($"can çalma %{LifestealPct}");
            if (Thorns            > 0) parts.Add($"diken {Thorns}");
            if (CleavePct         > 0) parts.Add($"yarma %{CleavePct}");
            if (StartShield       > 0) parts.Add($"kalkan {StartShield}");
            if (Regen             > 0) parts.Add($"yenilenme {Regen}");
            if (CooldownReduction > 0) parts.Add($"bekleme -{CooldownReduction}");
            return string.Join(" · ", parts);
        }
    }

    /// <summary>
    /// SINIF YETENEĞİNİN türü (Efe'nin isteği 2026-10-03: "sadece düz vuruş olmasın"). Her sınıfın
    /// üç yeteneği var; savaşta normal saldırının YERİNE kullanılır ve bekleme süresine girer.
    /// Çözümlemesi <c>UnitAbilityCaster</c>'da, yer tutucu animasyonu <c>UnitAbilityFx</c>'te.
    /// </summary>
    public enum UnitAbilityKind
    {
        PowerStrike,    // tek hedefe %Güç kadar sert vuruş
        MultiStrike,    // tek hedefe Adet kez %Güç vuruş (çift ok, hançer yağmuru)
        AreaBlast,      // hedef karonun çevresine (Yarıçap) %Güç alan hasarı — Menzil 0 = kendi çevresi
        StunStrike,     // %Güç vuruş + 1 tur sersemletme
        HealAlly,       // dosta Güç kadar can
        ShieldAlly      // dosta Güç kadar kalkan
    }

    /// <summary>Bir sınıfın tek bir aktif yeteneği.</summary>
    [System.Serializable]
    public class ClassAbility
    {
        [SerializeField] private string _name = "Yetenek";
        [SerializeField] private string _description = "";
        [SerializeField] private UnitAbilityKind _kind;
        [Tooltip("Hasar yeteneklerinde SALDIRININ yüzdesi (160 = 1.6 kat); can/kalkanda düz sayı.")]
        [SerializeField] private int _power = 100;
        [Tooltip("MultiStrike: kaç vuruş.")]
        [SerializeField, Min(1)] private int _hits = 1;
        [Tooltip("Hedefe en fazla kaç karo. 0 = yalnız kendi karosu (Barbar'ın döner baltası gibi). " +
                 "-1 = birimin saldırı menzili.")]
        [SerializeField] private int _range = -1;
        [Tooltip("AreaBlast: etki yarıçapı (1 = 7 karo).")]
        [SerializeField, Min(0)] private int _radius;
        [Tooltip("Kullanıldıktan sonra kaç tur bekler.")]
        [SerializeField, Min(0)] private int _cooldown = 2;
        [Tooltip("Yer tutucu animasyonun rengi.")]
        [SerializeField] private Color _color = Color.white;

        public string          Name        => _name;
        public string          Description => _description;
        public UnitAbilityKind Kind        => _kind;
        public int             Power       => _power;
        public int             Hits        => Mathf.Max(1, _hits);
        public int             Range       => _range;
        public int             Radius      => _radius;
        public int             Cooldown    => _cooldown;
        public Color           Color       => _color;

        /// <summary>Dosta mı kullanılır (can/kalkan)?</summary>
        public bool TargetsAlly => _kind == UnitAbilityKind.HealAlly || _kind == UnitAbilityKind.ShieldAlly;
    }
}
