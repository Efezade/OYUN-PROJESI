using UnityEngine;

namespace TacticalRPG.Data
{
    /// <summary>
    /// HARİTANIN DIŞINDA NE VAR — "haritanın bittiği yer görünmesin" ayarları.
    ///
    /// NEDEN AYRI VERİ (CLAUDE.md §2/§3): overworld ile savaş arenası aynı mekanizmayı kullanır
    /// ama BAŞKA bir dünya ister — kıtanın dışı sonsuz okyanus, arenanın dışı sonsuz orman.
    /// İki ayrı kod yazmak yerine tek üretici + iki profil asset'i; renkler/yoğunluklar koda
    /// gömülmez, Inspector'dan ayarlanır.
    ///
    /// ÜÇ KATMAN (sıralaması önemli, üçü birlikte "boşluk yok" garantisi verir):
    ///   1. DÜZLEM   — ufka kadar giden tek büyük yüzey. Kamera nereye bakarsa baksın altta bu var.
    ///   2. BANT     — tahtanın dışına doğru birkaç halka GERÇEK hex karosu (tek birleştirilmiş
    ///                 mesh). Karo kenarının düzlemle buluştuğu keskin çizgiyi yumuşatır.
    ///   3. SÜSLEME  — bandın ötesine serpilen ağaç/kaya/sis öbekleri. Düz bir renk yerine
    ///                 "devam eden bir dünya" hissi veren şey budur.
    ///
    /// Hiçbiri COLLIDER taşımaz ve hiçbiri <c>HexGridManager</c>'a hücre olarak kaydedilmez →
    /// tıklanamaz, yürünemez, sis sistemine girmez. Sınır "görünmez duvar" değil, DOĞAL sınırdır:
    /// kıta zaten kenara değmiyor (TerrainGenerator margin kuralı), dışı da su/orman.
    /// </summary>
    [CreateAssetMenu(fileName = "MapSurroundProfile", menuName = "TacticalRPG/Map Surround Profile")]
    public class MapSurroundProfileSO : ScriptableObject
    {
        [Header("Kimlik")]
        public string displayName = "Okyanus";

        // ── 1) Sonsuz düzlem ─────────────────────────────────────────────────
        [Header("1) Sonsuz düzlem (ufka kadar)")]
        [Tooltip("Düzlemin rengi. Bandın EN DIŞ rengiyle yakın olmalı, yoksa birleşme yeri çizgi olur.")]
        public Color planeColor = new(0.10f, 0.22f, 0.38f);

        [Tooltip("Tahta kenarından itibaren kaç dünya birimi uzağa gitsin. İzometrik kamerada " +
                 "300 birim her açıdan ufku doldurur.")]
        [Min(20f)] public float planeMargin = 320f;

        [Tooltip("Düzlemin yüksekliği. 0 = karo TABANI seviyesi (karolar bunun üstünde durur; " +
                 "karolar arası ince boşluklar da bununla dolar). Karo üstü 0.3 — düzlemi oraya " +
                 "çıkarırsan çevre tahtayla aynı hizada olur. Bant zaten bu yüksekliğe doğru iner.")]
        public float planeHeight = 0f;

        [Range(0f, 1f)] public float planeSmoothness = 0.55f;

        // ── 2) Geçiş bandı ───────────────────────────────────────────────────
        [Header("2) Geçiş bandı (gerçek hex karoları, tek mesh)")]
        [Tooltip("Tahtanın dışına kaç halka karo döşensin. 0 = bant yok (yalnız düzlem).")]
        [Range(0, 12)] public int bandRings = 5;

        [Tooltip("Banda en YAKIN halkanın rengi (haritanın kendi kenar karolarına benzemeli).")]
        public Color bandColorNear = new(0.14f, 0.30f, 0.50f);
        [Tooltip("Bandın DIŞ halkalarının rengi — düzlem rengine doğru gider.")]
        public Color bandColorFar  = new(0.10f, 0.22f, 0.38f);

        [Tooltip("Karo yüksekliğindeki rastgele oynama (dünya birimi). Küçük tut: büyük değer " +
                 "düz bir yüzeyde testere dişi gibi görünür.")]
        [Range(0f, 0.3f)] public float heightJitter = 0.03f;

        [Tooltip("Karo ölçeği. 1.0 = karolar birbirine değer (kütle hissi). Oynanan tahta 0.95 " +
                 "kullanıyor (aralarında çizgi var) — bant için kapalı olması daha iyi.")]
        [Range(0.8f, 1.05f)] public float tileScale = 1f;

        // ── 3) Süsleme ───────────────────────────────────────────────────────
        [Header("3) Süsleme (ağaç / kaya / sis öbeği)")]
        [Tooltip("Süslerin tahta kenarından ne kadar uzağa serpileceği (dünya birimi). " +
                 "Bunun ötesi düz düzlem olarak kalır.")]
        [Min(0f)] public float propMargin = 55f;

        [Tooltip("Süsler arası ortalama mesafe (dünya birimi). KÜÇÜK = sık orman.")]
        [Min(0.5f)] public float propSpacing = 2.6f;

        [Tooltip("Bir yuvanın dolu çıkma olasılığı. 1 = her yuvada süs (kapalı kanopi).")]
        [Range(0f, 1f)] public float propChance = 0.55f;

        [Tooltip("Güvenlik tavanı — tek mesh'e girecek en fazla süs sayısı.")]
        [Range(0, 4000)] public int propLimit = 1600;

        public Color   propColor  = new(0.16f, 0.30f, 0.20f);
        [Tooltip("Süsün taban yarıçapı (min/max).")]
        public Vector2 propWidth  = new(0.7f, 1.5f);
        [Tooltip("Süsün yüksekliği (min/max). Alçak+geniş = kaya/sis, yüksek+dar = ağaç.")]
        public Vector2 propHeight = new(1.4f, 3.2f);

        [Tooltip("Süsün oynanabilir alandan en az kaç HEX uzakta durması gerektiği. 1 = karonun " +
                 "hemen yanına ağaç dikilmez (kıyı/açıklık nefes alsın).")]
        [Range(0, 4)] public int propKeepOut = 1;

        // ── Işık ─────────────────────────────────────────────────────────────
        [Header("Genel")]
        [Tooltip("Tüm katmanların renk çarpanı. 1'in altı = uzak/loş durur, oynanan tahta öne çıkar. " +
                 "Overworld'de sisli bölgeyle uyum için 0.6-0.8 iyi çalışır.")]
        [Range(0.1f, 1.5f)] public float brightness = 0.8f;
    }
}
