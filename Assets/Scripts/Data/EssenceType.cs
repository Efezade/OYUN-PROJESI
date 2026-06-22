using UnityEngine;

namespace TacticalRPG.Data
{
    /// <summary>
    /// Çok-tipli öz (Töz) türleri. Tasarım kanonu: Kırmızı=Ateş, Mavi=Su; 3.sü Toprak.
    /// "En azından şimdilik 3 tür" — yeni tür eklemek = enum'a değer + EssenceConfigSO girişi.
    /// </summary>
    public enum EssenceType
    {
        Ates   = 0, // Kırmızı
        Su     = 1, // Mavi
        Toprak = 2  // Yeşil
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
