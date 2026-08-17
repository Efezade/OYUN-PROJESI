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
