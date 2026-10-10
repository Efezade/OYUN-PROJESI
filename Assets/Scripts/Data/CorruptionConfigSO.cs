using UnityEngine;

namespace TacticalRPG.Data
{
    /// <summary>
    /// KARA AŞI ÇÜRÜMESİ ayarları (2026-10-10) — map çapında yayılan baskı mekaniği.
    ///
    /// Hikâye (hikaye/Dokuz Hücre…md §4): Morvhal, Bayterek'in köklerine kara bir aşı yapmış;
    /// orman içten içe çürüyor. Oyunda: çürüme her gün kaynaklarından (Zar yırtığı, üç aşı noktası,
    /// Bayterek'in kalbi) dışarı yayılır ve derinleşir. Kararmış karolar KIYAMET SAYACININ
    /// (çöküş) ilk adayıdır. Bir aşı bölgesindeki zorunlu görev bitince o bölgenin kaynağı
    /// ARINIR: yayılma durur, çürüme gün gün geri çekilir.
    ///
    /// Sayılar ÖRNEKTİR (denge hesapları durdurulmuş — CLAUDE.md §9); Play'de ayarlanır.
    /// </summary>
    [CreateAssetMenu(fileName = "CorruptionConfig", menuName = "TacticalRPG/Config/Corruption Config")]
    public class CorruptionConfigSO : ScriptableObject
    {
        [Header("Başlangıç")]
        [Tooltip("Kaynağın çevresinde ilk gün kaç halka çürük başlar (1 = kaynak + 6 komşu).")]
        [SerializeField, Range(0, 3)] private int _initialRadius = 1;

        [Header("Yayılma (gün başında)")]
        [Tooltip("ARINABİLİR kaynak (aşı noktası) günde kaç YENİ karoya yayılır.")]
        [SerializeField, Min(0)] private int _graftSpreadPerDay = 3;
        [Tooltip("KALICI kaynak (Zar yırtığı, Kalp) günde kaç yeni karoya yayılır.")]
        [SerializeField, Min(0)] private int _permanentSpreadPerDay = 2;
        [Tooltip("Her geçen gün yayılmaya eklenen pay (gün × bu, aşağı yuvarlanır). Baskı zamanla artar.")]
        [SerializeField, Min(0f)] private float _spreadGrowthPerDay = 0.25f;
        [Tooltip("Çürük bir karonun her gün bir kademe DERİNLEŞME olasılığı.")]
        [SerializeField, Range(0f, 1f)] private float _deepenChance = 0.35f;
        [Tooltip("Yayılma bu günden önce başlamaz (ilk günler nefes payı).")]
        [SerializeField, Min(1)] private int _spreadStartDay = 2;

        [Header("Arınma")]
        [Tooltip("Arınmış kaynağın çürüğü her gün kaç kademe geri çekilir.")]
        [SerializeField, Range(1, 3)] private int _recedePerDay = 1;
        [Tooltip("Zorunlu görev bu kadar karo yakınındaki aşı kaynağını arındırır (bölgesi aynıysa mesafe bakılmaz).")]
        [SerializeField, Min(1)] private int _purifyRadius = 6;

        [Header("Kıyamet bağı")]
        [Tooltip("Çöküş seçiminde kademe başına ağırlık (0 = çürüme çöküşü etkilemez). " +
                 "Kademe 3 karonun seçilme ağırlığı 1 + 3×bu.")]
        [SerializeField, Min(0f)] private float _collapseWeightPerLevel = 1.5f;

        [Header("Görsel")]
        [Tooltip("Kademe 1-2-3 karo rengi (dokuyla ÇARPILIR — beyaz = etkisiz).")]
        [SerializeField] private Color[] _levelTint =
        {
            new(0.90f, 0.84f, 1.00f),
            new(0.74f, 0.60f, 0.92f),
            new(0.52f, 0.36f, 0.72f),
        };
        [Tooltip("Sisli karonun BULUTU bu renge kayar (mor pus) — çürüme uzaktan da görünsün.")]
        [SerializeField] private Color _cloudTint = new(0.45f, 0.28f, 0.62f);
        [SerializeField, Range(0f, 1f)] private float _cloudStrength = 0.55f;

        public const int MAX_LEVEL = 3;

        public int   InitialRadius       => _initialRadius;
        public float DeepenChance        => _deepenChance;
        public int   SpreadStartDay      => _spreadStartDay;
        public int   RecedePerDay        => _recedePerDay;
        public int   PurifyRadius        => _purifyRadius;
        public float CollapseWeightPerLevel => _collapseWeightPerLevel;
        public Color CloudTint           => _cloudTint;
        public float CloudStrength       => _cloudStrength;

        /// <summary>Bir kaynağın bu gün kaç yeni karoya yayılacağı.</summary>
        public int SpreadFor(int day, bool permanent)
        {
            if (day < _spreadStartDay) return 0;
            int baseCount = permanent ? _permanentSpreadPerDay : _graftSpreadPerDay;
            return baseCount + Mathf.FloorToInt((day - _spreadStartDay) * _spreadGrowthPerDay);
        }

        /// <summary>Kademenin karo rengi (0 = beyaz/etkisiz).</summary>
        public Color TintFor(int level)
        {
            if (level <= 0 || _levelTint == null || _levelTint.Length == 0) return Color.white;
            return _levelTint[Mathf.Clamp(level - 1, 0, _levelTint.Length - 1)];
        }
    }
}
