using UnityEngine;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Map Collapse (Kıyamet Sayacı) parametreleri — Inspector'dan tweaklenebilir.
    /// </summary>
    [CreateAssetMenu(fileName = "CollapseConfig", menuName = "TacticalRPG/Config/CollapseConfig")]
    public class CollapseConfig : ScriptableObject
    {
        [Header("Kıyamet Eşiği")]
        [Tooltip("İLK SİLMENİN olduğu gün. Karolar bu günden 'Uyarı Günü' kadar ÖNCE işaretlenir " +
                 "(kırmızı çerçeve + sayaç), sonra silinir.")]
        [SerializeField] private int _collapseStartDay = 3;

        [Tooltip("Bir karo silinmeden KAÇ GÜN önce görsel olarak uyarılır? (TASK-007 kabul kriteri: " +
                 "en az 1-2 gün). 2 = karo iki gün boyunca çatlak/kırmızı çerçeveli durur, sonra silinir.")]
        [SerializeField, Min(1)] private int _telegraphDays = 2;

        [Header("Çöküş Hızı")]
        [Tooltip("Her gün sonu kaç karo silinir?")]
        [SerializeField] private int _tilesRemovedPerDay = 2;

        [Tooltip("Her gün bu sayı kadar artar (ivme)")]
        [SerializeField] private int _removalAcceleration = 1;

        [Header("Sınırlamalar")]
        [Tooltip("Bir günde silinebilecek maksimum karo sayısı")]
        [SerializeField] private int _maxRemovalPerDay = 10;

        [Header("Baskı — çöküş oyuncunun ÇEVRESİNE ağırlıklanır (kullanıcı isteği 2026-09-02)")]
        [Tooltip("Her gün işaretlenen karoların NE KADARI oyuncunun yakınından seçilsin. " +
                 "0 = eski davranış (tamamen rastgele, çöküş uzakta kalır ve hissedilmez), " +
                 "1 = hepsi dipte. Seçim yine RASTGELEDİR — yalnız havuz daraltılır.")]
        [SerializeField, Range(0f, 1f)] private float _nearPlayerShare = 0.6f;

        [Tooltip("'Yakın' kaç karo demek. Görüş menzili civarı iyi bir değer: çöken karo " +
                 "gerçekten görülsün ama her seferinde ayağının dibinde olmasın.")]
        [SerializeField, Min(1)] private int _nearPlayerRadius = 7;

        [Tooltip("Oyuncuya BU KADAR yakın karolar hiç işaretlenmez. 0 yaparsan oyuncu çökmekte " +
                 "olan karolarla çevrilip kapana kısılabilir; 2 = dibindeki halka güvenli kalır.")]
        [SerializeField, Min(0)] private int _minPlayerDistance = 2;

        [Header("Sert Kesim (TASK-007)")]
        [Tooltip("Bölümün SON oynanabilir günü. Bu gün bittiğinde harita ilerlenemez hale gelir ve " +
                 "BÖLÜM KAYBEDİLİR (tüm run değil — sadece o harita baştan başlar). GAME_DESIGN §3: gün 14.")]
        [SerializeField] private int _hardCutDay = 14;

        public int HardCutDay            => _hardCutDay;
        public int CollapseStartDay      => _collapseStartDay;
        public int TelegraphDays         => Mathf.Max(1, _telegraphDays);
        public int TilesRemovedPerDay    => _tilesRemovedPerDay;
        public int RemovalAcceleration   => _removalAcceleration;
        public int MaxRemovalPerDay      => _maxRemovalPerDay;
        public float NearPlayerShare     => Mathf.Clamp01(_nearPlayerShare);
        public int   NearPlayerRadius    => Mathf.Max(1, _nearPlayerRadius);
        public int   MinPlayerDistance   => Mathf.Max(0, _minPlayerDistance);

        public int GetRemovalCount(int currentDay)
        {
            if (currentDay < _collapseStartDay) return 0;
            int daysPastThreshold = currentDay - _collapseStartDay;
            int count = _tilesRemovedPerDay + daysPastThreshold * _removalAcceleration;
            return Mathf.Clamp(count, 0, _maxRemovalPerDay);
        }
    }
}
