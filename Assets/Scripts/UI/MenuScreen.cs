namespace TacticalRPG.UI
{
    /// <summary>
    /// Ana gezinme kabuğundaki tam-ekran menü ekranları.
    /// <see cref="None"/> = hiçbir kaplama yok (overworld doğrudan görünür).
    /// </summary>
    public enum MenuScreen
    {
        None     = 0,
        Book     = 1, // KİTAP  — asker kartı / sınıf evrimi (öz ekonomisi)
        Bag      = 2, // ÇANTA  — eşya + potlar + Kam skill tree
        Map      = 3, // HARİTA — parşömen harita + hizmet pinleri
        Settings = 4  // AYARLAR — sağ üst dişli
    }
}
