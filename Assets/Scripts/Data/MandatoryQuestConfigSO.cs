using UnityEngine;

namespace TacticalRPG.Data
{
    /// <summary>
    /// ZORUNLU GÖREV ZİNCİRİ'nin tek doğruluk kaynağı (kullanıcı kararı 2026-08-28).
    ///
    /// KURAL — zincir oyuncunun kapattığı anda DURUR:
    ///   • Harita <see cref="InitialCount"/> zorunlu görevle açılır (varsayılan 2).
    ///   • <see cref="UnlockDays"/> günlerinden biri gelirse ve zincir HÂLÂ açıksa yeni bir zorunlu
    ///     görev gökten düşer → boss taşının bedeli bir görev daha artar.
    ///   • O ANDA AÇIK olan zorunlu görevlerin HEPSİ bitince boss taşı verilir ve **zincir kapanır**:
    ///     kalan görevler bir daha HİÇ doğmaz, üstel ödülleri kalıcı olarak kaybedilir.
    ///
    /// Tasarımın kalbi bu: erken bitiren güvenliği alır ama büyük ödülleri görmez; bekleyen
    /// ödülleri büyütür ama taşı her gün daha pahalıya alır. Baskın strateji yoktur.
    ///
    /// Not: <see cref="NodeConfigSO.MandatoryCount"/> BU ASSET atandığında yok sayılır — sayı
    /// buradan okunur (iki kaynak olmasın).
    /// </summary>
    [CreateAssetMenu(fileName = "MandatoryQuestConfig",
                     menuName  = "TacticalRPG/Config/MandatoryQuestConfig")]
    public class MandatoryQuestConfigSO : ScriptableObject
    {
        [Header("Zincir")]
        [Tooltip("Harita açılırken kaç zorunlu görev hazır bulunur.")]
        [SerializeField, Min(1)] private int _initialCount = 2;

        [Tooltip("Bu GÜNLERE gelindiğinde (gün >= değer) zincir hâlâ açıksa YENİ bir zorunlu görev " +
                 "gökten düşer. Artan sırada olmalı — OnValidate sıralar.\n\n" +
                 "Varsayılan 5/8/11: son açılış sert kesimden (gün 14) EN AZ 3 tam gün önce " +
                 "olmalı, yoksa görev haritanın öbür ucuna düştüğünde oyuncunun hiçbir karşı " +
                 "hamlesi kalmaz ve karar 'zar atışı'na döner. Çöküşün başladığı gün 10'a da " +
                 "denk getirilmez: iki baskı aynı güne yığılırsa oyuncu tepki verecek yer bulamaz.")]
        [SerializeField] private int[] _unlockDays = { 5, 8, 11 };

        [Header("Ödül eğrisi — ÜSTEL")]
        [Tooltip("1. kademe zorunlu görevin ödülü (öz).")]
        [SerializeField, Min(1)] private int _baseReward = 20;

        [Tooltip("Her kademede ödül bu çarpanla büyür. ÜSTEL — logaritmik DEĞİL: riski taşıyan tek " +
                 "şey ödülün büyüklüğü olduğu için geç kademeler çarpıcı biçimde iyi olmalı.\n" +
                 "1.6 ile: 20 · 32 · 51 · 82 · 131 (beşini de yapan, ikide duranın ~6 katını alır).")]
        [SerializeField, Min(1f)] private float _rewardRatio = 1.6f;

        [Header("Maliyet")]
        [Tooltip("Bir zorunlu göreve girmenin AP maliyeti (kademeden bağımsız).")]
        [SerializeField, Min(0)] private int _questAP = 5;

        [Header("Görev tipleri (Faz 1)")]
        [Tooltip("Kademe → görev tipi. 1. kademe listenin 1. elemanını, 2. kademe 2.'sini alır; " +
                 "liste kısaysa SON eleman tekrar eder. Boş liste ya da boş eleman = SAVAŞ (eski " +
                 "davranış).\n\nÖrnek: [Savaş, Savaş, Adak] → ilk iki görev savaş, sonrakiler adak.")]
        [SerializeField] private QuestKindSO[] _questKinds;

        [Header("Ekonomiye bağlı açılış (Faz 1)")]
        [Tooltip("Bölüm başından beri KAZANILAN öz (harcamak düşürmez) bu eşiklerden birini geçince, " +
                 "zincir hâlâ açıksa SIRADAKİ görev gününü beklemeden HEMEN düşer. Görev sayısının " +
                 "tavanı değişmez (başlangıç + açılış günleri) — ekonomi yalnız takvimi öne çeker: " +
                 "hızlı büyüyen oyuncu büyük ödüllü görevleri erken görür ama boss taşı da pahalanır.\n\n" +
                 "Artan sırada olmalı — OnValidate sıralar. Boş = kapalı (yalnız takvim). " +
                 "Haritada ~60-80 öz yatağı × 1-3 öz var; 80/160 ≈ hasadın yarısı / tamamı. " +
                 "SAYILAR ÖRNEK (CLAUDE.md §9: denge işi durduruldu).")]
        [SerializeField] private int[] _economyThresholds = { 80, 160 };

        [Header("Uyarı (UI barı)")]
        [Tooltip("Sıradaki açılışa BU KADAR AP kala barda soluk hayalet çizgi + geri sayım belirir. " +
                 "24 = tam bir gün. Uyarı şart: oyuncu 'zinciri şimdi kapatayım mı' kararını " +
                 "körlemesine veremez.")]
        [SerializeField, Min(1)] private int _warningAP = 24;

        [Header("Gökten düşüş — hedef karo")]
        [Tooltip("Yeni görevin oyuncuya HEX MESAFESİ bandı (min, max). Yakına düşerse bedavaya " +
                 "gelir, uzağa düşerse ulaşılamaz. Band ikisini de engeller.")]
        [SerializeField] private Vector2Int _spawnDistance = new(6, 14);

        [Tooltip("Kalan sürenin en fazla bu kadarı YOLA ayrılabilir. Bölüm bitene dek kalan AP ile " +
                 "hesaplanan üst sınırı kısar; oyuncuya boss/market/dönüş için de AP kalmalı. " +
                 "0.5 = kalan AP'nin yarısı yola gidebilir.")]
        [SerializeField, Range(0.1f, 1f)] private float _reachSafety = 0.5f;

        public int   InitialCount  => Mathf.Max(1, _initialCount);
        public int   UnlockCount   => _unlockDays != null ? _unlockDays.Length : 0;
        public int   QuestAP       => _questAP;
        public int   WarningAP     => Mathf.Max(1, _warningAP);
        public Vector2Int SpawnDistance => _spawnDistance;
        public float ReachSafety   => _reachSafety;

        /// <summary>Ekonomi eşiği sayısı (0 = ekonomiye bağlı açılış kapalı).</summary>
        public int EconomyThresholdCount => _economyThresholds != null ? _economyThresholds.Length : 0;

        /// <summary>index'inci ekonomi eşiği (kazanılan öz). Aralık dışında int.MaxValue.</summary>
        public int EconomyThreshold(int index)
            => (_economyThresholds != null && index >= 0 && index < _economyThresholds.Length)
               ? _economyThresholds[index] : int.MaxValue;

        /// <summary>Kademenin görev tipi. null = SAVAŞ (yerleşik akış).</summary>
        public QuestKindSO KindForTier(int tier)
        {
            if (_questKinds == null || _questKinds.Length == 0) return null;
            return _questKinds[Mathf.Clamp(tier - 1, 0, _questKinds.Length - 1)];
        }

        /// <summary>Zincirin ULAŞABİLECEĞİ en yüksek görev sayısı (başlangıç + tüm açılışlar).</summary>
        public int MaxCount => InitialCount + UnlockCount;

        /// <summary>index'inci açılışın günü (0-tabanlı). Aralık dışında int.MaxValue → hiç açılmaz.</summary>
        public int UnlockDay(int index)
            => (_unlockDays != null && index >= 0 && index < _unlockDays.Length)
               ? _unlockDays[index] : int.MaxValue;

        /// <summary>Kademe (1-tabanlı) ödülü — ÜSTEL eğri.</summary>
        public int RewardForTier(int tier)
            => Mathf.RoundToInt(_baseReward * Mathf.Pow(Mathf.Max(1f, _rewardRatio), Mathf.Max(0, tier - 1)));

        private void OnValidate()
        {
            // Açılış ve ekonomi imleçleri sırayla ilerliyor; sırasız bir dizi sessizce yanlış
            // gün/eşik açardı.
            SortAscending(_unlockDays);
            SortAscending(_economyThresholds);
        }

        private static void SortAscending(int[] values)
        {
            if (values == null || values.Length < 2) return;
            for (int i = 1; i < values.Length; i++)
                if (values[i] < values[i - 1]) { System.Array.Sort(values); return; }
        }
    }
}
