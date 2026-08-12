namespace TacticalRPG.Core
{
    /// <summary>
    /// Tam-ekran bir uGUI paneli açık mı? IMGUI HUD'ları bunu <c>OnGUI</c>'nin başında kontrol edip
    /// panel açıkken KENDİNİ ÇİZMEZ.
    ///
    /// NEDEN GEREKLİ: IMGUI (<c>OnGUI</c>) her zaman ScreenSpaceOverlay Canvas'ın **ÜSTÜNE** çizer.
    /// Yani uGUI paneli "en üstte" olamaz — can barları, sıra barı, "Geri Dön" düğmesi panelin
    /// içinden geçer (2026-08-12 ekran görüntüsü: augment kartlarının üstünde isim/HP barları).
    /// Sıralama ayarıyla çözülemez; tek çözüm IMGUI tarafını susturmak.
    ///
    /// <see cref="ImguiBlocker"/> gibi hafif statik koordinasyon — mevcut IMGUI HUD'ları zaten bu
    /// deseni kullanıyor, event-driven wiring için 14 HUD'a bağımlılık eklemeye değmez.
    ///
    /// Domain reload'da varsayılan false; MenuNavigator.Awake ayrıca sıfırlar.
    /// </summary>
    public static class MenuState
    {
        /// <summary>Tam-ekran menü (KİTAP/ÇANTA/HARİTA/AYARLAR) açık mı? MenuNavigator günceller.</summary>
        public static bool IsAnyOpen { get; set; }

        /// <summary>Davul karo draftı (augment seçim kartları) açık mı? CombatDrumManager/HUD günceller.
        /// Açıkken TÜM savaş HUD'ları gizlenir — kart okunabilir kalsın.</summary>
        public static bool IsDraftOpen { get; set; }

        /// <summary>IMGUI HUD'ları şu an çizilmeli mi? HER <c>OnGUI</c>'nin İLK satırı bunu sormalı:
        /// <code>if (MenuState.HudsHidden) return;</code></summary>
        public static bool HudsHidden => IsAnyOpen || IsDraftOpen;
    }
}
