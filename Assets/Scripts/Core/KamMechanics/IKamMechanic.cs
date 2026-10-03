using System;

namespace TacticalRPG.Core
{
    /// <summary>
    /// KAM'IN ANA MEKANİĞİ (Faz 2 omurgası, YOL_HARITASI.md madde 7): her map'te Kam'ın büyü
    /// kaynağı değişebilir — bugün MANA, yarın "can = o map'in özü" gibi bir şey. Çekirdek kod
    /// hangi mekaniğin açık olduğunu BİLMEZ; yalnız bu arayüzle konuşur.
    ///
    /// Üç soruya cevap verir (yol haritasındaki tanım):
    ///   • KAYNAK NEDİR      → <see cref="ResourceName"/>, <see cref="Current"/>, <see cref="Max"/>
    ///   • BEDELİ KİM ÖDER   → <see cref="CanPay"/> / <see cref="TryPay"/> / <see cref="Restore"/>
    ///   • EŞİKTE NE DEĞİŞİR → <see cref="ModifyPower"/> (örn. kaynak azaldıkça büyü güçlenir)
    ///
    /// Yetenek maliyetleri "can mı, mana mı, öz mü" diye SORMAZ: maliyet bir sayıdır, onu aktif
    /// mekanik yorumlar. Runtime nesnesidir (düz C# sınıfı) — ayarları <see cref="KamMechanicSO"/>
    /// taşır ve her bölüm başında yeni bir örnek üretir (SO runtime'da değiştirilmez, CLAUDE.md §2).
    /// </summary>
    public interface IKamMechanic
    {
        /// <summary>Oyuncuya gösterilen kaynak adı ("Mana", "Kan" ...).</summary>
        string ResourceName { get; }

        int Current { get; }
        int Max     { get; }

        /// <summary>Bu bedel şu an ödenebilir mi?</summary>
        bool CanPay(int cost);

        /// <summary>Bedeli öder. false = ödenemedi, hiçbir şey değişmedi.</summary>
        bool TryPay(int cost);

        /// <summary>Kaynağı geri doldurur (davul "mana" karosu, iksir ...). Tavanı geçmez.</summary>
        void Restore(int amount);

        /// <summary>
        /// EŞİK ETKİSİ: büyünün taban gücünü mekaniğin o anki durumuna göre değiştirir.
        /// Eşiği olmayan mekanik gücü aynen döndürür.
        /// </summary>
        int ModifyPower(int basePower);

        /// <summary>Overworld'de zaman dilimi ilerledi (yenilenme burada olur).</summary>
        void OnTimeSlotAdvanced(int day, int slot);

        /// <summary>(mevcut, tavan) — HUD dinler.</summary>
        event Action<int, int> OnResourceChanged;
    }
}
