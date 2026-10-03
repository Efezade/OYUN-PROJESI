using UnityEngine;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Bir Kam mekaniğinin AYAR asset'i + runtime örneğinin fabrikası. Her mekanik bu sınıftan
    /// türeyen tek bir SO'dur (<see cref="ManaMechanicSO"/> ilk örnek); hangi bölümde hangisinin
    /// açık olduğunu <see cref="TacticalRPG.Data.ChapterRulesSO"/> söyler.
    ///
    /// Neden SO + ayrı runtime sınıfı: SO'nun alanları tasarım verisidir ve runtime'da
    /// değiştirilmez (CLAUDE.md §2). Mevcut mana gibi DEĞİŞEN durum <see cref="CreateRuntime"/>'ın
    /// ürettiği nesnede yaşar — bölüm yeniden başlayınca yeni nesne üretilir, eski durum taşınmaz.
    ///
    /// FÜZYON (madde 8) bu sınıfa EKLENMEYECEK: Efe'nin kararıyla füzyon elle yazılmış çift
    /// tarifleri (whitelist) olacak; her tarif kendi SO'su olur, buradaki tekil mekanikler değişmez.
    /// </summary>
    public abstract class KamMechanicSO : ScriptableObject
    {
        [Tooltip("Oyuncuya gösterilen kaynak adı (HUD, büyü düğmeleri).")]
        [SerializeField] private string _resourceName = "Mana";

        public string ResourceName => string.IsNullOrEmpty(_resourceName) ? name : _resourceName;

        /// <summary>Bu mekaniğin TAZE bir runtime örneği (bölüm başında çağrılır).</summary>
        public abstract IKamMechanic CreateRuntime();
    }
}
