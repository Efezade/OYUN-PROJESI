namespace TacticalRPG.Core
{
    /// <summary>
    /// Herhangi bir tam-ekran uGUI menüsü (KİTAP/ÇANTA/HARİTA/AYARLAR) açık mı?
    /// Overworld IMGUI HUD'ları (öz deposu vb.) bunu OnGUI'de kontrol edip menü açıkken kendini
    /// ÇİZMEZ — çünkü IMGUI (OnGUI) her zaman ScreenSpaceOverlay Canvas'ın ÜSTÜNE çizer, aksi halde
    /// menü panelinin üstüne taşarlar. <see cref="ImguiBlocker"/> gibi hafif statik koordinasyon
    /// (event-driven wiring yerine — mevcut IMGUI HUD'ları zaten bu deseni kullanıyor).
    ///
    /// MenuNavigator günceller. Domain reload'da varsayılan false; MenuNavigator.Awake da sıfırlar.
    /// </summary>
    public static class MenuState
    {
        /// <summary>Şu an bir tam-ekran menü açık mı?</summary>
        public static bool IsAnyOpen { get; set; }
    }
}
