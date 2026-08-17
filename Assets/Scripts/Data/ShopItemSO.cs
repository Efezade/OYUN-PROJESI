using System.Collections.Generic;
using UnityEngine;

namespace TacticalRPG.Data
{
    /// <summary>
    /// Bir mağaza etkisinin türü. Davranış (uygulama) <c>PlayerBuffs</c>'ta; bu enum sadece sınıflandırma.
    /// </summary>
    public enum ShopEffectKind
    {
        BonusAPNow = 0, // ANINDA +N AP (tek seferlik)
        MoveSpeed  = 1, // yürüme hızı çarpanı (+magnitude%) — geçici (durationMoves) ya da kalıcı
        MoveRange  = 2, // tek tık hareket menzili +magnitude karo — geçici ya da kalıcı
        /// <summary>+magnitude YOL TAŞI. Harita ekranından seyahat etmenin ANAHTARI: bir taş
        /// harcanır, yol koşarak kat edilir. AP ve zaman NORMAL işler — taş yalnız bekleme süresini
        /// kısaltır ve haritadan gitme hakkını verir.</summary>
        FastTravelToken = 3,

        /// <summary>+magnitude GÜÇLÜ YOL TAŞI. Mesafeye göre BİRDEN ÇOK harcanır (her taş haritanın
        /// dörtte biri kadar yol) ama karşılığında yolculuk BEDAVADIR: AP düşmez, zaman dilimi
        /// ilerlemez, gün dönmez.</summary>
        PowerTravelToken = 4
    }

    /// <summary>
    /// Mağazada öz karşılığı satın alınabilir bir öğe: KALICI item ya da GEÇİCİ pot / "basic geliştirme".
    /// Saf veri — davranış içermez (satın alım <c>StoreManager</c>/<c>PlayerBuffs</c>). Yeni öğe = yeni asset.
    ///
    /// Örnekler: "Yel Ayağı İksiri" (MoveSpeed +100%, 6 adım) · "Zaman Kumu" (BonusAPNow +5) ·
    /// "Sağlam Çizmeler" (MoveSpeed +25%, kalıcı).
    /// </summary>
    [CreateAssetMenu(menuName = "TacticalRPG/Shop Item", fileName = "ShopItem")]
    public class ShopItemSO : ScriptableObject
    {
        [Header("Kimlik")]
        [SerializeField] private string _id          = "item";
        [SerializeField] private string _displayName = "Öğe";
        [TextArea(2, 4)]
        [SerializeField] private string _description = "";
        [SerializeField] private Sprite _icon;

        [Header("Maliyet (öz)")]
        [Tooltip("Satın alma bedeli — birden çok öz türü olabilir. Boşsa bedava.")]
        [SerializeField] private List<EssenceAmount> _cost = new();

        [Header("Etki")]
        [SerializeField] private ShopEffectKind _effect = ShopEffectKind.BonusAPNow;
        [Tooltip("Etkinin büyüklüğü. BonusAPNow=AP · MoveSpeed=yüzde (100=+%100=x2) · MoveRange=karo.")]
        [SerializeField, Min(0)] private int _magnitude = 1;

        [Header("Süre")]
        [Tooltip("KALICI item mı? İşaretliyse süre yok sayılır (kalıcı upgrade). BonusAPNow zaten tek seferlik.")]
        [SerializeField] private bool _permanent = false;
        [Tooltip("GEÇİCİ etki kaç ADIM (oyuncu hareketi) sürer. _permanent iken yok sayılır.")]
        [SerializeField, Min(0)] private int _durationMoves = 6;

        public string Id          => _id;
        public string DisplayName => _displayName;
        public string Description => _description;
        public Sprite Icon        => _icon;
        public IReadOnlyList<EssenceAmount> Cost => _cost;

        public ShopEffectKind Effect       => _effect;
        public int            Magnitude    => _magnitude;
        public bool           IsPermanent  => _permanent;
        public int            DurationMoves => _durationMoves;

        /// <summary>Etkisi süreli mi (geçici pot)? BonusAPNow ve kalıcı item süreli değildir.</summary>
        public bool IsTimed => !_permanent && _effect != ShopEffectKind.BonusAPNow && _durationMoves > 0;

        /// <summary>UI için kısa maliyet metni, örn "3 Ateş · 2 Toprak" (config verilirse ad kullanılır).</summary>
        public string CostText(EssenceConfigSO config)
        {
            if (_cost == null || _cost.Count == 0) return "Bedava";
            var parts = new List<string>(_cost.Count);
            foreach (var c in _cost)
            {
                string n = config != null ? config.NameOf(c.type) : c.type.ToString();
                parts.Add($"{c.amount} {n}");
            }
            return string.Join(" · ", parts);
        }
    }
}
