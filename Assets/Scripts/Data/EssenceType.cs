using UnityEngine;

namespace TacticalRPG.Data
{
    /// <summary>
    /// Çok-tipli öz (Töz) türleri. Tasarım kanonu: Kırmızı=Ateş, Mavi=Su; 3.sü Toprak.
    /// "En azından şimdilik 3 tür" — yeni tür eklemek = enum'a değer + EssenceConfigSO girişi.
    /// </summary>
    public enum EssenceType
    {
        // ── Bölüm 1 ÖNCESİ deneme türleri (GAME_DESIGN.md §0'da "sadece bir denemeydi, iptal").
        //    SİLİNMEDİ: mevcut tarifler/mağaza fiyatları bunları kullanıyor; kaldırmak onları bozar.
        Ates   = 0, // Kırmızı
        Su     = 1, // Mavi
        Toprak = 2, // Yeşil

        // ── BÖLÜM 1'İN GERÇEK ÖZLERİ (GAME_DESIGN.md §3): terrain'in KENDİSİNDEN türer,
        //    ayrı node yok, TEK SEFERLİK (toplanınca karo ovaya döner). Bkz TerrainGenerator.
        Tas    = 3, // Gri  — taşlık ova (1) / bol taşlık ova (2)
        Doga   = 4  // Yeşil — az ağaçlı ova (1) / orman (2) / nadir yüksek orman (3)
    }

    /// <summary>
    /// Tek bir öz türü + miktar (tarif maliyeti / kazanım için). Inspector'da düzenlenebilir.
    /// </summary>
    [System.Serializable]
    public struct EssenceAmount
    {
        public EssenceType type;
        [Min(0)] public int amount;

        public EssenceAmount(EssenceType type, int amount)
        {
            this.type   = type;
            this.amount = amount;
        }
    }
}
