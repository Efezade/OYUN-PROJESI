namespace TacticalRPG.UI
{
    /// <summary>
    /// IMGUI HUD'ları için ORTAK YERLEŞİM ŞERİTLERİ (1920×1080 sanal ekranda).
    ///
    /// Neden var: her HUD kendi Rect'ini elle yazıyordu ve birkaçı aynı noktaya denk geliyordu —
    /// gün barı (üst-orta y=8) ile "Savaşa Gir" istemi (üst-orta y=12) BİREBİR üst üste biniyordu,
    /// öz deposu ile yetenek test paneli de sağ-üstte çakışıyordu (2026-08-12 hata raporu).
    ///
    /// Yeni bir üst/sağ HUD eklerken koordinat UYDURMA — buradaki şeritlerden birini kullan ya da
    /// yeni bir şerit EKLE. Böylece "hangi panel nereye düşüyor" sorusu tek dosyadan yanıtlanır ve
    /// çakışmalar gözle görülür kalır.
    /// </summary>
    public static class HudLayout
    {
        // ── ÜST-ORTA yığını (yukarıdan aşağı) ────────────────────────────────
        /// <summary>ZORUNLU GÖREV ZİNCİRİ barı — üst-ortanın EN ÜSTÜ. Oyunun ana kararı
        /// ("zinciri şimdi mi kapatayım") buradan okunduğu için gün barının da üstünde durur.</summary>
        public const float QuestBarY      = 8f;
        public const float QuestBarHeight = 38f;

        /// <summary>Bölüm durum barı (Gün X/14 · çöküş bilgisi).</summary>
        public const float RunBarY      = QuestBarY + QuestBarHeight + 6f;   // 52
        public const float RunBarHeight = 30f;

        /// <summary>Gün barının hemen ALTI — üst-orta ikinci sıra (görev istemi vb.).</summary>
        public const float SecondRowY = RunBarY + RunBarHeight + 10f;   // 48

        /// <summary>Üst-orta üçüncü sıra (dükkân paneli gibi büyük kutular).</summary>
        public const float ThirdRowY = SecondRowY + 86f;                // 134

        // ── SAĞ-ÜST yığını ───────────────────────────────────────────────────
        public const float RightMargin  = 12f;
        /// <summary>Öz deposu paneli.</summary>
        public const float RightFirstY  = 12f;
        /// <summary>Öz deposunun altı (yetenek test paneli gibi ikincil paneller).</summary>
        public const float RightSecondY = RightFirstY + 242f;           // 254

        // ── SOL-ÜST ──────────────────────────────────────────────────────────
        /// <summary>Zaman kadranı burada (TimeDialHUD kendi marjlarını kullanır).
        /// Sol-üstte 16..200 arası DOLU — yeni panel koyacaksan altına in.</summary>
        public const float LeftDialBottom = 200f;
    }
}
