using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Grid;

namespace TacticalRPG.Data
{
    /// <summary>
    /// Bölüm haritasının ORGANİK üretim ayarları. Sayılar koda gömülmez — hepsi buradan gelir
    /// (CLAUDE.md §3 Whiteboxing).
    ///
    /// Oranlar kullanıcının (Efe) kendi araştırmasından gelen hedeflerdir ve haritanın TAMAMINA
    /// değil, KITAYA (kara karolarına) uygulanır — kıyının dışındaki sis/deniz dekoru istatistiğe
    /// girmez, çünkü o oynanabilir alan değil, sınırı gizleyen görsel bant.
    ///
    /// **UYARI:** Boyutu / oranları / gürültü ayarlarını değiştirirsen 30-seed havuzunun doğrulaması
    /// GEÇERSİZ olur (haritalar bambaşka çıkar). Değiştirdikten sonra seed taramasını yeniden koştur:
    /// `Docs/Balance/tools/seed_taramasi/tara.ps1`
    /// </summary>
    [CreateAssetMenu(fileName = "TerrainConfig", menuName = "TacticalRPG/Config/TerrainConfig")]
    public class TerrainConfigSO : ScriptableObject
    {
        [Header("Tahta (sınırlayıcı kutu)")]
        [Tooltip("Kıta bu kutunun İÇİNE oturur; kutunun tamamı dolmaz. Kutu ne kadar büyükse " +
                 "kıyı o kadar serbest kıvrılabilir — ama boş hücreler de artar.")]
        [SerializeField, Min(8)] private int _width  = 36;
        [SerializeField, Min(8)] private int _height = 34;

        [Header("Kıta büyüklüğü (KARA karo sayısı)")]
        [SerializeField, Min(50)] private int _targetLandMin = 500;
        [SerializeField, Min(50)] private int _targetLandMax = 600;

        [Header("Karo dağılımı (KITAYA oranla)")]
        [Tooltip("Yürünemez nehir karoları. Hedef: %4.9")]
        [SerializeField, Range(0f, 0.3f)] private float _riverPct    = 0.049f;
        [Tooltip("Dağ / kaya (yürünemez). Hedef: %7.5")]
        [SerializeField, Range(0f, 0.3f)] private float _mountainPct = 0.075f;
        [Tooltip("Sık orman + göl blobları (yürünemez). Hedef: %8.9")]
        [SerializeField, Range(0f, 0.3f)] private float _blobPct     = 0.089f;
        [Tooltip("Köprü / sığ geçit (YÜRÜNÜR, nehrin üstünde). Hedef: %0.4 — nehri gerçekten " +
                 "kesen noktalara konur, rastgele yere ASLA.")]
        [SerializeField, Range(0f, 0.05f)] private float _bridgePct  = 0.004f;

        [Header("Kıyı şekli (organiklik)")]
        [Tooltip("Kıyının dışındaki dekor bandı kalınlığı (sığ su → derin su → sis). 0 = bant yok.")]
        [SerializeField, Range(0, 5)] private int _fringeWidth = 2;
        [Tooltip("Kıyı çizgisinin gürültü katkısı. 0 = elips (kötü), 0.5 = çok parçalı.")]
        [SerializeField, Range(0f, 0.7f)] private float _coastRoughness = 0.42f;
        [Tooltip("Kıta gürültüsünün frekansı. Düşük = az sayıda büyük burun, yüksek = çok girinti.")]
        [SerializeField, Range(0.6f, 3f)] private float _shapeFrequency = 1.55f;
        [Tooltip("Domain warp şiddeti — şeklin 'gürültü' değil 'coğrafya' gibi görünmesini sağlar.")]
        [SerializeField, Range(0f, 0.8f)] private float _warpAmount = 0.34f;

        [Header("Landmark (yön bulma / gizem)")]
        [Tooltip("Haritaya serpilecek göze çarpan nadir karo sayısı (dikilitaş, harabe, dev ağaç…).")]
        [SerializeField, Min(0)] private int _landmarkCount = 12;
        [Tooltip("İki landmark arasındaki en az hex mesafesi — hepsi bir köşede toplanmasın.")]
        [SerializeField, Min(1)] private int _landmarkSpacing = 4;

        [Header("Sabit seed havuzu")]
        [Tooltip("Oyun her başladığında bu havuzdan RASTGELE bir seed seçilir (son oynanandan farklı). " +
                 "Havuz elle değil, `Docs/Balance/tools/seed_taramasi` ile binlerce aday taranarak " +
                 "seçildi: oran uyumu + bağlantı + 14 günlük AP baskısı + kıyı organikliği puanı.")]
        [SerializeField] private List<int> _seedPool = new();

        public int   Width          => _width;
        public int   Height         => _height;
        public float RiverPct       => _riverPct;
        public float MountainPct    => _mountainPct;
        public float BlobPct        => _blobPct;
        public float BridgePct      => _bridgePct;
        public int   FringeWidth    => _fringeWidth;

        public IReadOnlyList<int> SeedPool => _seedPool;

        /// <summary>Üreticinin beklediği parametre paketi.</summary>
        public TerrainParams ToParams() => new TerrainParams
        {
            Width          = _width,
            Height         = _height,
            TargetLandMin  = Mathf.Min(_targetLandMin, _targetLandMax),
            TargetLandMax  = Mathf.Max(_targetLandMin, _targetLandMax),
            RiverPct       = _riverPct,
            MountainPct    = _mountainPct,
            BlobPct        = _blobPct,
            BridgePct      = _bridgePct,
            FringeWidth    = _fringeWidth,
            CoastRoughness = _coastRoughness,
            ShapeFrequency = _shapeFrequency,
            WarpAmount     = _warpAmount,
            LandmarkCount  = _landmarkCount,
            LandmarkSpacing= _landmarkSpacing
        };
    }
}
