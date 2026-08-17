using UnityEngine;

namespace TacticalRPG.Data
{
    /// <summary>
    /// Bir öz türünün haritadaki küresinin BİÇİMİ. Renk ayrı ayarlanır (bkz
    /// <see cref="EssenceConfigSO.TypeStyle.color"/>) — biçim "bu ne özü" der, renk pekiştirir.
    /// Küreleri <c>EssenceOrbFactory</c> bu biçime göre üretir.
    /// </summary>
    public enum EssenceOrbShape
    {
        Alev    = 0, // yukarı doğru incelen, titreşen diller — ateş
        Su      = 1, // içinde ağır ağır dönen damla/dalga kabarcıkları
        Toz     = 2, // hızlı ve düzensiz dönen ince toz zerreleri — toprak
        Kristal = 3, // yavaş dönen köşeli kırıklar — taş
        Yaprak  = 4  // savrulan yassı yapraklar/sporlar — doğa
    }

    /// <summary>
    /// Öz sisteminin TEK ayar dosyası: her türün adı + rengi + küre biçimi/prefabı, ve haritaya
    /// KAÇ öz saçılacağı. Renkler/prefablar/sayılar koda gömülmez — buradan ayarlanır (Whiteboxing).
    ///
    /// YERLEŞİM (2026-08-17, kullanıcı isteği): öz artık "her taşlık/orman karosu öz verir"
    /// değil; haritaya <see cref="TotalRange"/> kadar öz SAÇILIR (rastgele ama seed'e bağlı, hep
    /// YÜRÜNÜR karolara). Bir öz karosu uzaktan tanınsın diye üç görsel katman taşır:
    /// karo rengi türe boyanır · kenarına o renkte kontur çizilir · üstünde hareketli bir küre durur.
    /// Yerleştiren: <c>EssenceFieldManager</c>, çizen: <c>EssenceFieldVisuals</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "TacticalRPG/Essence Config", fileName = "EssenceConfig")]
    public class EssenceConfigSO : ScriptableObject
    {
        [System.Serializable]
        public struct TypeStyle
        {
            public EssenceType type;
            public string      displayName;
            public Color       color;
            [Tooltip("Haritadaki kürenin biçimi. Prefab BOŞSA bu biçimde bir küre üretilir.")]
            public EssenceOrbShape orbShape;
            [Tooltip("Bu türün harita görseli. BOŞSA renkli placeholder küre üretilir — " +
                     "buraya kendi (animasyonlu) öz prefab'ını atayınca otomatik o kullanılır.")]
            public GameObject  prefab;
        }

        [Header("Tür stilleri (ad + renk + küre)")]
        [SerializeField] private TypeStyle[] _types;

        [Header("Harita yerleşimi")]
        [Tooltip("Bir bölümde haritadan toplanabilecek TOPLAM öz (x = en az, y = en çok). " +
                 "Kullanıcı kararı 2026-08-17: ilk harita 60–80.")]
        [SerializeField] private Vector2Int _totalRange = new(60, 80);

        [Tooltip("Haritaya SAÇILACAK türler. Bölüm 1 = Taş + Doğa (GAME_DESIGN §3). Bir tür " +
                 "buradan çıkarılırsa o türün karoları haritada hiç oluşmaz.")]
        [SerializeField] private EssenceType[] _mapTypes = { EssenceType.Tas, EssenceType.Doga };

        [Tooltip("Tek bir öz karosunun verebileceği miktar (x = en az, y = en çok).")]
        [SerializeField] private Vector2Int _amountPerTile = new(1, 3);

        [Header("Öz karosunun görünüşü")]
        [Tooltip("Karonun kendi rengi öz rengine ne kadar çekilsin (0 = hiç, 1 = tamamen).")]
        [SerializeField, Range(0f, 1f)] private float _tileTint = 0.42f;

        [Tooltip("Karonun kenarını saran KALIN kontur bandının genişliği (dünya birimi). Karo dış " +
                 "yarıçapı 1.0 → 0.18 gözle seçilir kalınlıkta bir çerçevedir. 0 = kontur yok.")]
        [SerializeField, Min(0f)] private float _outlineWidth = 0.18f;

        [Tooltip("Kalın bandın DIŞINA çizilen koyu ince şerit. Kontur her arazi renginin üstünde " +
                 "okunsun diye var (açık kumda sarı bir band kaybolurdu). 0 = şerit yok.")]
        [SerializeField, Min(0f)] private float _outlineEdgeWidth = 0.045f;

        [SerializeField] private Color _outlineEdgeColor = new(0.04f, 0.04f, 0.05f);

        [Tooltip("Kontur/küre renginin emisyon çarpanı — karanlıkta da seçilsin.")]
        [SerializeField, Min(0f)] private float _glow = 3.0f;

        [Tooltip("Kürenin karo YÜZEYİNDEN yüksekliği (dünya birimi) — 'karodan çok az yukarıda'.")]
        [SerializeField, Min(0f)] private float _orbHeight = 0.34f;

        [Tooltip("Kürenin çapı (dünya birimi). Karo çapı ~1.73 → 0.34 karonun beşte biri kadar.")]
        [SerializeField, Min(0.01f)] private float _orbScale = 0.36f;

        [Tooltip("Kontur çizgisinin materyali (kendinden ışıklı). Boşsa çalışma zamanında " +
                 "basit bir materyal üretilir.")]
        [SerializeField] private Material _outlineMaterial;

        [Header("Öz sökülünce (karo ruhsuz kalır)")]
        [Tooltip("Özü alınmış karonun ve üstündeki süslerin döneceği renk. Kullanıcı isteği " +
                 "2026-08-17: karo 'ruhu çekilmiş' gibi kararsın — ama çatlaklar seçilebilsin diye " +
                 "koyu değil, SOLGUN olsun.")]
        [SerializeField] private Color _drainedColor = new(0.50f, 0.47f, 0.43f);

        [Tooltip("Karonun kendi rengi bu orana kadar griye çekilir (1 = tamamen gri).")]
        [SerializeField, Range(0f, 1f)] private float _drainStrength = 0.88f;

        [Tooltip("Karonun ÜSTÜNE serilen kurak yüzeyin rengi. Karonun dokusunu gerçekten örttüğü " +
                 "için renk buradan gelir — sadece _BaseColor boyamak yeşil çimi griye çeviremiyordu. " +
                 "ÇOK KOYU YAPMA: karo kararınca çatlaklar da kayboluyor (kullanıcı geri bildirimi " +
                 "2026-08-17) — detayın okunması için kurumuş toprak tonunda kalmalı.")]
        [SerializeField] private Color _drainCapColor = new(0.42f, 0.38f, 0.32f);

        [Tooltip("Kurak yüzeyin örtücülüğü. 1 = karo tamamen kaybolur, 0.8 civarı altındaki " +
                 "zemini hafifçe gösterir (daha inandırıcı).")]
        [SerializeField, Range(0f, 1f)] private float _drainCapOpacity = 0.82f;

        [Tooltip("Çatlak çizgilerinin rengi — kurumuş toprak yarığı. Kurak yüzeyden belirgin " +
                 "ölçüde KOYU olmalı, yoksa çatlaklar zeminde kaybolur.")]
        [SerializeField] private Color _crackColor = new(0.09f, 0.075f, 0.06f);

        [Tooltip("Kararma + çatlama animasyonunun süresi (sn). Işık hüzmesiyle aynı tempoda olsun.")]
        [SerializeField, Min(0.05f)] private float _drainDuration = 1.15f;

        // ── Tür sorguları ────────────────────────────────────────────────────

        public string NameOf(EssenceType t)
        {
            if (_types != null)
                foreach (var s in _types)
                    if (s.type == t && !string.IsNullOrEmpty(s.displayName)) return s.displayName;
            return t.ToString();
        }

        public Color ColorOf(EssenceType t)
        {
            if (_types != null)
                foreach (var s in _types)
                    if (s.type == t) return s.color;
            return Color.white;
        }

        /// <summary>Bu türün görsel prefab'ı (atanmamışsa null → placeholder küre).</summary>
        public GameObject PrefabOf(EssenceType t)
        {
            if (_types != null)
                foreach (var s in _types)
                    if (s.type == t) return s.prefab;
            return null;
        }

        public EssenceOrbShape ShapeOf(EssenceType t)
        {
            if (_types != null)
                foreach (var s in _types)
                    if (s.type == t) return s.orbShape;
            return EssenceOrbShape.Kristal;
        }

        // ── Yerleşim sorguları ───────────────────────────────────────────────

        /// <summary>Haritaya saçılacak toplam öz aralığı (x = en az, y = en çok).</summary>
        public Vector2Int TotalRange => new(Mathf.Max(0, _totalRange.x),
                                            Mathf.Max(_totalRange.x, _totalRange.y));

        /// <summary>Haritaya saçılacak türler (boşsa Taş + Doğa).</summary>
        public EssenceType[] MapTypes => (_mapTypes != null && _mapTypes.Length > 0)
            ? _mapTypes
            : new[] { EssenceType.Tas, EssenceType.Doga };

        public Vector2Int AmountPerTile => new(Mathf.Max(1, _amountPerTile.x),
                                               Mathf.Max(_amountPerTile.x, _amountPerTile.y));

        public float    TileTint         => _tileTint;
        public float    OutlineWidth     => _outlineWidth;
        public float    OutlineEdgeWidth => _outlineEdgeWidth;
        public Color    OutlineEdgeColor => _outlineEdgeColor;
        public float    Glow             => _glow;
        public float    OrbHeight        => _orbHeight;
        public float    OrbScale         => _orbScale;
        public Material OutlineMaterial  => _outlineMaterial;

        /// <summary>Özü alınmış karonun rengi — "ruhu çekilmiş" gri.</summary>
        public Color DrainedColorOf(Color natural)
            => Color.Lerp(natural, _drainedColor, Mathf.Clamp01(_drainStrength));

        /// <summary>Kürenin/hüzmenin göğe giderken çekildiği son renk.</summary>
        public Color DrainedColor => _drainedColor;

        /// <summary>Karonun üstüne serilen kurak yüzeyin rengi (alfası örtücülüğü taşır).</summary>
        public Color DrainCapColor
        {
            get { Color c = _drainCapColor; c.a = Mathf.Clamp01(_drainCapOpacity); return c; }
        }

        public Color CrackColor    => _crackColor;
        public float DrainDuration => _drainDuration;

        /// <summary>Bu tür haritada saçılan türlerden biri mi?</summary>
        public bool IsMapType(EssenceType t)
        {
            foreach (var m in MapTypes) if (m == t) return true;
            return false;
        }
    }
}
