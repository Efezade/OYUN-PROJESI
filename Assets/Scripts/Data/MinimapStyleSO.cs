using UnityEngine;

namespace TacticalRPG.Data
{
    /// <summary>
    /// MİNİHARİTANIN GÖRSEL AYARI — çözünürlük, sis renkleri, gölgeleme, ikon renkleri.
    /// Hiçbiri koda gömülmez (Whiteboxing, CLAUDE.md §3): haritanın "ne kadar pikselli",
    /// "ne kadar kontrastlı" göründüğü buradan ayarlanır.
    ///
    /// GÖLGELEME MINECRAFT HARİTASININ KURALI: her karo, KUZEYİNDEKİ komşusuyla yüksekliği
    /// karşılaştırılarak üç tondan birine boyanır (alçaksa koyu, eşitse orta, yüksekse parlak).
    /// Bu, tek bir renk katmanından kabartma hissi çıkarmanın en ucuz yolu — gerçek ışık/gölge
    /// hesabı yok, sadece komşu karşılaştırması.
    /// </summary>
    [CreateAssetMenu(menuName = "TacticalRPG/Minimap Style", fileName = "MinimapStyle")]
    public class MinimapStyleSO : ScriptableObject
    {
        [Header("Çözünürlük")]
        [Tooltip("Dünya birimi başına piksel. DÜŞÜK = daha iri piksel, daha 'harita' hissi. " +
                 "Bir karo ~2 birim geniştir → 8 ile karo ≈ 16 piksel olur.")]
        [SerializeField, Range(2f, 24f)] private float _pixelsPerUnit = 8f;

        [Header("Sis")]
        [Tooltip("Hiç keşfedilmemiş karo. ALFASI 0 = hiç çizilmez, harita orada BOMBOŞ kalır " +
                 "(kullanıcı isteği: sisli yerler gözükmesin). Alfa yükseltilirse karo bir leke " +
                 "olarak belirir — ama DİKKAT: o zaman kıtanın SİLUETİ keşfedilmeden sızar, " +
                 "oyuncu gitmediği kıyının şeklini haritadan okur.")]
        [SerializeField] private Color _unexploredColor = new(0.24f, 0.20f, 0.15f, 0f);

        [Tooltip("Harita dışı / karo olmayan koordinat (kıtanın dışı).")]
        [SerializeField] private Color _voidColor = new(0f, 0f, 0f, 0f);

        [Tooltip("Keşfedilmiş ama ŞU AN görüş alanında olmayan karonun karartma çarpanı. " +
                 "Klasik savaş sisi: bildiğin ama görmediğin yer soluk kalır.")]
        [SerializeField, Range(0.1f, 1f)] private float _exploredDim = 0.62f;

        [Header("Kabartma (Minecraft harita kuralı)")]
        [Tooltip("Kuzeydeki komşudan ALÇAK karo (Minecraft: 180/255).")]
        [SerializeField, Range(0.3f, 1.2f)] private float _shadeLower = 0.706f;
        [Tooltip("Kuzeydeki komşuyla AYNI yükseklikte (Minecraft: 220/255).")]
        [SerializeField, Range(0.3f, 1.2f)] private float _shadeEqual = 0.863f;
        [Tooltip("Kuzeydeki komşudan YÜKSEK karo (Minecraft: 255/255).")]
        [SerializeField, Range(0.3f, 1.2f)] private float _shadeHigher = 1.0f;

        [Header("Doku")]
        [Tooltip("Karo başına küçük rastgele parlaklık oynaması — düz renk alanları cansız durmasın. " +
                 "0 = kapalı. Değer KOORDİNATTAN türer, her açılışta aynıdır.")]
        [SerializeField, Range(0f, 0.2f)] private float _dither = 0.045f;

        [Tooltip("Karo kenarındaki piksellerin karartılması — altıgenler birbirinden ayrılsın. " +
                 "0 = kenarlık yok (tamamen düz, daha 'boyanmış' görünür).")]
        [SerializeField, Range(0f, 0.6f)] private float _edgeDarken = 0.16f;

        [Header("İkon renkleri")]
        [SerializeField] private Color _marketColor     = new(0.98f, 0.80f, 0.30f);
        [SerializeField] private Color _dungeonColor    = new(0.72f, 0.40f, 0.90f);
        [SerializeField] private Color _encounterColor  = new(0.92f, 0.36f, 0.28f);
        [SerializeField] private Color _mandatoryColor  = new(1.00f, 0.90f, 0.42f);
        [SerializeField] private Color _watchtowerColor = new(0.86f, 0.93f, 1.00f);
        [SerializeField] private Color _playerColor     = new(0.30f, 0.95f, 1.00f);

        [Tooltip("İkonların ekrandaki boyutu (piksel).")]
        [SerializeField, Range(8f, 64f)] private float _iconSize = 26f;

        [Tooltip("Tamamlanmış düğümlerin ikon saydamlığı — 'buraya gittim' izi kalsın ama öne çıkmasın.")]
        [SerializeField, Range(0f, 1f)] private float _completedIconAlpha = 0.38f;

        [Header("Hikaye zinciri çizgisi (2026-10-03)")]
        [Tooltip("Palet boşsa kullanılan tek zincir rengi.")]
        [SerializeField] private Color _chainColor          = new(0.96f, 0.90f, 0.78f, 0.55f);
        [Tooltip("HER ZİNCİR FARKLI RENK (Efe, 2026-10-03): zincir N bu listenin N. rengini alır, " +
                 "liste bitince başa döner. Altın bilerek YOK — o zorunlu zincirin rengi. " +
                 "Alfa düşük tutulur: zincir dururken HAFİF belirgin olmalı.")]
        [SerializeField] private Color[] _chainPalette =
        {
            new(0.55f, 0.80f, 1.00f, 0.65f),   // gök mavisi
            new(0.60f, 0.95f, 0.60f, 0.65f),   // yeşil
            new(1.00f, 0.55f, 0.55f, 0.65f),   // mercan
            new(0.82f, 0.62f, 1.00f, 0.65f),   // menekşe
            new(0.40f, 0.95f, 0.90f, 0.65f),   // turkuaz
            new(1.00f, 0.68f, 0.38f, 0.65f),   // turuncu
            new(0.98f, 0.62f, 0.88f, 0.65f),   // pembe
            new(0.80f, 0.88f, 0.52f, 0.65f),   // fıstık
        };
        [Tooltip("Zorunlu görev zincirinin rengi (altın).")]
        [SerializeField] private Color _mandatoryChainColor = new(1.00f, 0.84f, 0.30f, 0.75f);
        [Tooltip("Çizgi kalınlığı (ekran pikseli, yakınlaştırmadan bağımsız).")]
        [SerializeField, Range(1f, 8f)]  private float _chainThickness = 2.5f;
        [Tooltip("Zincir halkası uzunluğu (piksel).")]
        [SerializeField, Range(2f, 30f)] private float _chainLink = 8f;
        [Tooltip("Halkalar arası boşluk (piksel).")]
        [SerializeField, Range(1f, 20f)] private float _chainGap  = 5f;

        [Header("Zincir vurgusu (fare üstüne gelince)")]
        [Tooltip("Vurgulanan zincirin kalınlık çarpanı.")]
        [SerializeField, Range(1f, 5f)]   private float _chainHoverThickness = 2.2f;
        [Tooltip("Parıltı katmanının genişliği (çizgi kalınlığının katı).")]
        [SerializeField, Range(1f, 12f)]  private float _chainGlowWidth = 5f;
        [Tooltip("Parıltının nabız hızı.")]
        [SerializeField, Range(0f, 12f)]  private float _chainPulseSpeed = 4f;
        [Tooltip("Fare çizgiye bu kadar piksel yakınsa zincirin üstünde sayılır.")]
        [SerializeField, Range(2f, 24f)]  private float _chainHoverRadius = 8f;

        public float ChainHoverThickness => _chainHoverThickness;
        public float ChainGlowWidth      => _chainGlowWidth;
        public float ChainPulseSpeed     => _chainPulseSpeed;
        public float ChainHoverRadius    => _chainHoverRadius;

        /// <summary>index'inci zincirin rengi (palet döner; palet boşsa tek renk).</summary>
        public Color ChainColorAt(int index)
            => _chainPalette != null && _chainPalette.Length > 0
               ? _chainPalette[Mathf.Abs(index) % _chainPalette.Length] : _chainColor;

        public Color ChainColor          => _chainColor;
        public Color MandatoryChainColor => _mandatoryChainColor;
        public float ChainThickness      => _chainThickness;
        public float ChainLink           => _chainLink;
        public float ChainGap            => _chainGap;

        public float PixelsPerUnit => _pixelsPerUnit;
        public Color UnexploredColor => _unexploredColor;
        public Color VoidColor => _voidColor;
        public float ExploredDim => _exploredDim;
        public float ShadeLower => _shadeLower;
        public float ShadeEqual => _shadeEqual;
        public float ShadeHigher => _shadeHigher;
        public float Dither => _dither;
        public float EdgeDarken => _edgeDarken;

        public Color MarketColor => _marketColor;
        public Color DungeonColor => _dungeonColor;
        public Color EncounterColor => _encounterColor;
        public Color MandatoryColor => _mandatoryColor;
        public Color WatchtowerColor => _watchtowerColor;
        public Color PlayerColor => _playerColor;
        public float IconSize => _iconSize;
        public float CompletedIconAlpha => _completedIconAlpha;
    }
}
