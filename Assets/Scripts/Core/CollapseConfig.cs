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
        [Tooltip("Kaçıncı günün başında çökecek karolar İŞARETLENMEYE başlar? İşaretlenen karolar " +
                 "1 gün sonra silinir (uyarı süresi). 3 → uyarılar 3. günde çıkar, İLK SİLME 4. günün " +
                 "sabahında olur (3. günün gecesi bittiği anda).")]
        [SerializeField] private int _collapseStartDay = 3;

        [Header("Çöküş Hızı")]
        [Tooltip("Her gün sonu kaç karo silinir?")]
        [SerializeField] private int _tilesRemovedPerDay = 2;

        [Tooltip("Her gün bu sayı kadar artar (ivme)")]
        [SerializeField] private int _removalAcceleration = 1;

        [Header("Sınırlamalar")]
        [Tooltip("Bir günde silinebilecek maksimum karo sayısı")]
        [SerializeField] private int _maxRemovalPerDay = 10;

        [Header("Sert Kesim (TASK-007)")]
        [Tooltip("Bölümün SON oynanabilir günü. Bu gün bittiğinde harita ilerlenemez hale gelir ve " +
                 "BÖLÜM KAYBEDİLİR (tüm run değil — sadece o harita baştan başlar). GAME_DESIGN §3: gün 14.")]
        [SerializeField] private int _hardCutDay = 14;

        public int HardCutDay            => _hardCutDay;
        public int CollapseStartDay      => _collapseStartDay;
        public int TilesRemovedPerDay    => _tilesRemovedPerDay;
        public int RemovalAcceleration   => _removalAcceleration;
        public int MaxRemovalPerDay      => _maxRemovalPerDay;

        public int GetRemovalCount(int currentDay)
        {
            if (currentDay < _collapseStartDay) return 0;
            int daysPastThreshold = currentDay - _collapseStartDay;
            int count = _tilesRemovedPerDay + daysPastThreshold * _removalAcceleration;
            return Mathf.Clamp(count, 0, _maxRemovalPerDay);
        }
    }
}
