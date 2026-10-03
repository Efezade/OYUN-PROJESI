using System.Collections.Generic;
using UnityEngine;

namespace TacticalRPG.Data
{
    /// <summary>Eşya/pot nadirliği — markette çıkma olasılığını ve fiyat bandını belirler.</summary>
    public enum ItemRarity { Siradan, Nadir, Destansi }

    /// <summary>
    /// Eşyanın takıldığı VÜCUT BÖLGESİ (Efe'nin isteği 2026-10-03: "kol, gövde, kafa gibi
    /// bölgelere gerekli eşyaları sürükleyip takabileyim"). Eşya YALNIZ kendi bölgesine takılır.
    /// <see cref="Yok"/> = atanmamış (kurulum doldurur) — sıra değişmesin, asset'ler indeks saklıyor.
    /// </summary>
    public enum EquipSlot { Yok, Kafa, Boyun, Govde, SagKol, SolKol, Ayak }

    public static class EquipSlots
    {
        /// <summary>Yuva sayısı (Yok hariç) — karakter başına bu kadar yuva.</summary>
        public const int Count = 6;

        /// <summary>Bölge → yuva indeksi (0..Count-1). Yok → -1.</summary>
        public static int IndexOf(EquipSlot s) => s == EquipSlot.Yok ? -1 : (int)s - 1;

        public static EquipSlot FromIndex(int i) => (EquipSlot)(i + 1);

        public static string Label(EquipSlot s) => s switch
        {
            EquipSlot.Kafa   => "KAFA",
            EquipSlot.Boyun  => "BOYUN",
            EquipSlot.Govde  => "GÖVDE",
            EquipSlot.SagKol => "SAĞ KOL",
            EquipSlot.SolKol => "SOL KOL",
            EquipSlot.Ayak   => "AYAK",
            _                => "—"
        };
    }

    /// <summary>
    /// KARAKTERE TAKILAN EŞYA (Efe'nin isteği 2026-10-03): ÇANTA'da sürükleyip bir karakterin
    /// yuvasına bırakılır; o karakter savaşa inince eşyanın etkilerini taşır. Etkiler evrimlerle
    /// AYNI özellik kümesinden gelir (<see cref="EvolutionTrait"/>) — çift vuruş, kalkan, diken …
    /// böylece savaş kodu tek bir toplamı (<see cref="EvolutionTraits"/>) okur.
    ///
    /// Kalıcıdır: takılı kaldığı sürece her savaşta işler, çıkarılınca çantaya döner.
    /// </summary>
    [CreateAssetMenu(fileName = "Esya", menuName = "TacticalRPG/Esya")]
    public class ItemSO : ScriptableObject
    {
        [SerializeField] private string _id = "esya";
        [SerializeField] private string _displayName = "Eşya";
        [TextArea(2, 3)]
        [SerializeField] private string _description = "";
        [SerializeField] private Sprite _icon;
        [SerializeField] private ItemRarity _rarity;
        [SerializeField] private EssenceAmount[] _price;
        [Tooltip("Takıldığı vücut bölgesi — yalnız bu yuvaya takılabilir.")]
        [SerializeField] private EquipSlot _slot;

        [Header("Etkiler (en fazla iki)")]
        [SerializeField] private EvolutionTrait _trait;
        [SerializeField] private int _amount = 1;
        [SerializeField] private EvolutionTrait _trait2;
        [SerializeField] private int _amount2;

        public string     Id          => _id;
        public string     DisplayName => _displayName;
        public string     Description => _description;
        public Sprite     Icon        => _icon;
        public ItemRarity Rarity      => _rarity;
        public EquipSlot  Slot        => _slot;
        public IReadOnlyList<EssenceAmount> Price => _price ?? (IReadOnlyList<EssenceAmount>)System.Array.Empty<EssenceAmount>();

        /// <summary>Eşyanın etkilerini verilen toplama ekler.</summary>
        public void AddTo(ref EvolutionTraits traits)
        {
            if (_trait  != EvolutionTrait.None) traits.Add(_trait,  _amount);
            if (_trait2 != EvolutionTrait.None) traits.Add(_trait2, _amount2);
        }

        /// <summary>Etkilerin kısa metni ("+2 saldırı · yarma %25").</summary>
        public string EffectText()
        {
            var t = new EvolutionTraits();
            AddTo(ref t);
            return t.Describe();
        }
    }
}
