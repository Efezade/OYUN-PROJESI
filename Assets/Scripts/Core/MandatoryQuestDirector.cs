using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// ZORUNLU GÖREV ZİNCİRİ'nin beyni: ne zaman yeni görev düşer, ne zaman boss taşı verilir
    /// (kullanıcı kararı 2026-08-28). Kuralın tamamı için bkz <see cref="MandatoryQuestConfigSO"/>.
    ///
    /// SORUMLULUK AYRIMI: burası NE ZAMAN ve NEREYE sorusunu çözer; düğümü haritaya yazmak
    /// <see cref="ChapterNodeManager.SpawnMandatory"/>'nin, animasyonu oynatmak
    /// <see cref="MandatoryQuestFallEffect"/>'in işidir.
    ///
    /// İKİ İNCE KURAL — ikisi de oyuncuyu haksız yere cezalandırmamak için:
    ///
    ///   1. **Savaş beklemedeyken açılış ERTELENİR.** Zorunlu göreve girmek AP harcar ve o AP günü
    ///      devirebilir; görev ise ancak savaştan DÖNÜNCE tamamlanmış sayılır. Ertelenmeseydi
    ///      oyuncu tam zinciri kapatmak üzereyken tepesine yeni bir görev düşerdi. Savaş çözülünce
    ///      yeniden bakılır: kazandıysa zincir kapanır ve o görev HİÇ doğmaz.
    ///
    ///   2. **Düşecek karo mesafe bandından seçilir.** Tamamen rastgele olsaydı 11. günde açılan
    ///      görev haritanın öbür ucuna düşüp bölümü karşı hamlesiz kaybettirebilirdi. Band + kalan
    ///      AP'den türetilen tavan ikisini de (bedava yakın / imkânsız uzak) engeller.
    ///
    /// EKONOMİYE BAĞLI AÇILIŞ (Faz 1, 2026-10-03): o an açılabilecek görev sayısı
    /// = geçilen açılış GÜNÜ sayısı + geçilen ÖZ EŞİĞİ sayısı (tavan: açılış günü sayısı).
    /// Ekonomi yeni görev UYDURMAZ, yalnız takvimdeki sıradaki görevi öne çeker. Eşik listesi
    /// boşsa davranış eskisiyle birebir aynıdır.
    /// </summary>
    [DefaultExecutionOrder(-70)]   // ChapterNodeManager(-80) düğümleri kurduktan SONRA
    public class MandatoryQuestDirector : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private ChapterNodeManager      _nodes;
        [SerializeField] private ActionPointManager      _ap;
        [SerializeField] private ChapterMapGenerator     _map;
        [SerializeField] private HexGridManager          _grid;
        [SerializeField] private PlayerController        _player;
        [SerializeField] private ChapterRunManager       _run;
        [Tooltip("YEDEK zincir ayarı — bölümün kural seti (ChapterRulesSO) zincir seçmiyorsa kullanılır.")]
        [SerializeField] private MandatoryQuestConfigSO  _config;
        [Tooltip("Bölümün kural setini okumak için (Faz 2 omurgası).")]
        [SerializeField] private ChapterProgress         _progress;
        [Tooltip("EKONOMİYE BAĞLI AÇILIŞ için kazanılan öz buradan okunur. Atanmazsa yalnız takvim işler.")]
        [SerializeField] private EssenceWallet           _wallet;

        // ── Durum ────────────────────────────────────────────────────────────

        /// <summary>Haritada ŞU AN kaç zorunlu görev var (açılmış olanlar).</summary>
        public int OpenCount { get; private set; }

        /// <summary>Bunların kaçı bitti.</summary>
        public int DoneCount { get; private set; }

        /// <summary>Zincir kapandı mı? true → bir daha zorunlu görev DOĞMAZ.</summary>
        public bool ChainClosed { get; private set; }

        /// <summary>Boss taşı cepte mi? İstenildiği an bossa girmek için kullanılır.</summary>
        public bool HasBossStone { get; private set; }

        /// <summary>Sıradaki açılış savaş yüzünden ertelendi mi (teşhis / UI).</summary>
        public bool UnlockDeferred { get; private set; }

        private int  _cursor;          // şimdiye dek yapılan açılış sayısı
        private int  _earnedBaseline;  // bölüm başındaki EssenceWallet.TotalGained
        private bool _busy;            // yeniden-giriş kilidi: Spawn → OnNodesChanged → Refresh döngüsü

        /// <summary>Son açılış takvimden ÖNCE, ekonomi yüzünden mi oldu? (UI mesajı için)</summary>
        public bool LastUnlockByEconomy { get; private set; }

        /// <summary>Yeni zorunlu görev gökten düştü — (kademe, karo). UI çakması için.</summary>
        public event System.Action<int, HexCoordinate> OnQuestUnlocked;

        /// <summary>Zincir kapandı, boss taşı verildi. UI kutlaması için.</summary>
        public event System.Action OnStoneGranted;

        // ── Sorgular (IMGUI barı bunları her karede okur) ────────────────────

        /// <summary>Aktif zincir ayarı: bölümün kural seti seçtiyse o, yoksa Inspector yedeği.</summary>
        public MandatoryQuestConfigSO Config
            => _progress != null && _progress.CurrentRules != null && _progress.CurrentRules.QuestChain != null
               ? _progress.CurrentRules.QuestChain : _config;

        /// <summary>Daha açılacak bir görev var mı?</summary>
        public bool HasNextUnlock => !ChainClosed && Config != null && _cursor < Config.UnlockCount;

        /// <summary>Bölüm başından beri kazanılan öz (harcamak düşürmez).</summary>
        public int EarnedThisChapter => _wallet != null ? Mathf.Max(0, _wallet.TotalGained - _earnedBaseline) : 0;

        /// <summary>Takvimde GEÇİLMİŞ açılış günü sayısı.</summary>
        private int DaysPassed
        {
            get
            {
                if (Config == null || _ap == null) return 0;
                int n = 0;
                while (n < Config.UnlockCount && _ap.CurrentDay >= Config.UnlockDay(n)) n++;
                return n;
            }
        }

        /// <summary>Aşılmış ekonomi eşiği sayısı.</summary>
        private int ThresholdsCrossed
        {
            get
            {
                if (Config == null || _wallet == null) return 0;
                int earned = EarnedThisChapter, n = 0;
                while (n < Config.EconomyThresholdCount && earned >= Config.EconomyThreshold(n)) n++;
                return n;
            }
        }

        /// <summary>Şu ana dek açılmış olması GEREKEN görev sayısı (takvim + ekonomi, tavanlı).</summary>
        private int AllowedUnlocks
            => Config == null ? 0 : Mathf.Min(Config.UnlockCount, DaysPassed + ThresholdsCrossed);

        /// <summary>Sıradaki görev vadesinde mi (gün/eşik geldi ama savaş ya da karo yüzünden henüz düşmedi)?</summary>
        public bool UnlockDue => HasNextUnlock && _cursor < AllowedUnlocks;

        /// <summary>Sıradaki açılışın günü: vadesi geldiyse bugün, değilse takvimdeki gün (yoksa 0).</summary>
        public int NextUnlockDay => UpcomingUnlockDay(0);

        /// <summary>Sıradaki görevi EKONOMİ ile erken düşürmek için kaç öz daha kazanılmalı
        /// (0 = ekonomi yolu kapalı / kalmadı).</summary>
        public int EssenceUntilEconomyUnlock
        {
            get
            {
                if (!HasNextUnlock || UnlockDue || Config == null || _wallet == null) return 0;
                int t = ThresholdsCrossed;
                if (t >= Config.EconomyThresholdCount) return 0;
                return Mathf.Max(0, Config.EconomyThreshold(t) - EarnedThisChapter);
            }
        }

        /// <summary>Sıradaki açılışa kaç GÜN kaldı (0 = bugün / vadesi geldi).</summary>
        public int DaysUntilNextUnlock
            => HasNextUnlock && _ap != null ? Mathf.Max(0, NextUnlockDay - _ap.CurrentDay) : 0;

        /// <summary>Daha kaç görev açılabilir (takvim + ekonomi aynı tavanı paylaşır).</summary>
        public int RemainingUnlocks => HasNextUnlock ? Config.UnlockCount - _cursor : 0;

        /// <summary>
        /// Kalan açılışların günleri, sıradakinden başlayarak (UI takvimi için). Vadesi gelmiş
        /// olanlar BUGÜN; kalanlar takvimin en yakın günlerinden. Ekonominin öne çektiği her görev
        /// takvimin SONUNDAN bir gün siler — tavan sabit olduğu için. Aralık dışında 0.
        /// </summary>
        public int UpcomingUnlockDay(int offset)
        {
            if (!HasNextUnlock || _ap == null || offset < 0 || offset >= RemainingUnlocks) return 0;
            int due = Mathf.Max(0, AllowedUnlocks - _cursor);
            if (offset < due) return _ap.CurrentDay;
            int idx = DaysPassed + offset - due;
            return idx < Config.UnlockCount ? Config.UnlockDay(idx) : 0;
        }

        /// <summary>Sıradaki açılışa kaç AP kaldı. Gün/dilim DEĞİL — oyuncunun harcadığı birim AP.</summary>
        public int APUntilNextUnlock
        {
            get
            {
                if (!HasNextUnlock || _ap == null) return 0;
                int target = NextUnlockDay;
                if (_ap.CurrentDay >= target) return 0;
                int perDay = Mathf.Max(1, _ap.SlotsPerDay * _ap.MaxAP);
                return _ap.APRemainingToday + (target - _ap.CurrentDay - 1) * perDay;
            }
        }

        /// <summary>Uyarı penceresi açık mı? (barda hayalet çizgi + geri sayım gösterilir)</summary>
        public bool WarningActive
            => HasNextUnlock && Config != null && APUntilNextUnlock <= Config.WarningAP;

        // ── Bağlantı ─────────────────────────────────────────────────────────

        private void OnEnable()
        {
            if (_nodes != null) _nodes.OnNodesChanged += Refresh;
            if (_ap    != null) _ap.OnTimeAdvanced    += HandleTimeAdvanced;
            if (_map   != null) _map.OnMapGenerated   += ResetChain;
            if (_wallet != null) _wallet.OnChanged    += Refresh;
        }

        private void OnDisable()
        {
            if (_nodes != null) _nodes.OnNodesChanged -= Refresh;
            if (_ap    != null) _ap.OnTimeAdvanced    -= HandleTimeAdvanced;
            if (_map   != null) _map.OnMapGenerated   -= ResetChain;
            if (_wallet != null) _wallet.OnChanged    -= Refresh;
        }

        private void Start()
        {
            // Boss kapısını AKTİF ET. Bu çağrı yapılmazsa ChapterNodeManager eski davranışta kalır
            // (taşsız da bossa girilir) — yönetici sahnede yoksa bölüm bitirilemez hâle GELMESİN.
            if (_nodes != null) _nodes.SetBossStone(false);
            Refresh();
        }

        /// <summary>Yeni harita üretildi → zincir baştan kurulur (bölüm yeniden başlarken de).</summary>
        private void ResetChain()
        {
            _cursor        = 0;
            // Ekonomi BÖLÜM İÇİ büyümeyi ölçer: önceki denemede kazanılan öz yeni haritayı hızlandırmasın.
            _earnedBaseline     = _wallet != null ? _wallet.TotalGained : 0;
            LastUnlockByEconomy = false;
            ChainClosed    = false;
            HasBossStone   = false;
            UnlockDeferred = false;
            OpenCount      = 0;
            DoneCount      = 0;
            if (_nodes != null) _nodes.SetBossStone(false);
        }

        private void HandleTimeAdvanced(int day, int slot, string slotName) => Refresh();

        // ── Çekirdek ─────────────────────────────────────────────────────────

        /// <summary>
        /// Tek değerlendirme adımı: sayıları oku → zincir kapanabiliyor mu → kapanmadıysa açılış
        /// zamanı geldi mi. SIRA ÖNEMLİ: kapanış önce bakılır, yoksa son görevini bitiren oyuncuya
        /// aynı karede yeni görev düşerdi.
        /// </summary>
        private void Refresh()
        {
            if (_busy || _nodes == null) return;
            _busy = true;
            try
            {
                ReadCounts();
                if (!TryCloseChain() && TryUnlock()) ReadCounts();
            }
            finally { _busy = false; }
        }

        private void ReadCounts()
        {
            _nodes.MandatoryProgress(out int done, out int total);
            DoneCount = done;
            OpenCount = total;
        }

        /// <summary>Açık zorunlu görevlerin HEPSİ bittiyse taşı ver ve zinciri kapat.</summary>
        private bool TryCloseChain()
        {
            if (ChainClosed || OpenCount <= 0 || DoneCount < OpenCount) return false;

            ChainClosed    = true;
            HasBossStone   = true;
            UnlockDeferred = false;
            _nodes.SetBossStone(true);
            OnStoneGranted?.Invoke();

            int forfeited = Config != null ? Config.UnlockCount - _cursor : 0;
            Debug.Log($"[Gorev] ZINCIR KAPANDI — {DoneCount} zorunlu gorev bitti, BOSS TASI verildi. " +
                      $"Acilmayan {forfeited} gorev ve ustel odulleri kalici olarak kaybedildi.");
            return true;
        }

        /// <summary>Açılış günü geldiyse yeni zorunlu görevi gökten düşür.</summary>
        private bool TryUnlock()
        {
            if (!HasNextUnlock || _ap == null) return false;
            if (_cursor >= AllowedUnlocks) return false;   // ne takvim ne ekonomi henüz izin veriyor

            // KURAL 1: zorunlu savaş sürüyor → ertele, dönüşte yeniden bakılır.
            if (_nodes.MandatoryCombatPending) { UnlockDeferred = true; return false; }

            if (!TryPickFallCoord(out HexCoordinate coord))
            {
                // İmleç İLERLETİLMEZ: uygun karo sonra açılabilir (oyuncu yer değiştirir, çöküş
                // düğümleri siler). Sessizce atlanırsa görev bir daha hiç doğmazdı.
                Debug.LogWarning("[Gorev] Gokten dusecek uygun karo bulunamadi — sonraki adimda tekrar denenecek.");
                return false;
            }

            int tier = OpenCount + 1;
            if (!_nodes.SpawnMandatory(coord, tier)) return false;

            // Takvim tek başına bu açılışa izin VERMİYORDUYSA görevi ekonomi öne çekmiştir.
            LastUnlockByEconomy = _cursor >= DaysPassed;
            _cursor++;
            UnlockDeferred = false;
            Debug.Log($"[Gorev] {tier}. zorunlu gorev gun {_ap.CurrentDay} icinde dustu" +
                      (LastUnlockByEconomy ? $" (EKONOMI: {EarnedThisChapter} oz kazanildi)" : "") +
                      $" -> {coord} " +
                      $"(odul {(Config != null ? Config.RewardForTier(tier) : 0)} oz). " +
                      $"Boss tasi icin artik {tier} gorev gerekiyor.");
            OnQuestUnlocked?.Invoke(tier, coord);
            return true;
        }

        // ── Hedef karo seçimi ────────────────────────────────────────────────

        /// <summary>
        /// Gökten düşecek karoyu seçer: erişilebilir + DÜZLÜK + üstünde düğüm yok + oyuncuya
        /// mesafesi banda uyan karolar arasından.
        ///
        /// Neden yalnız DÜZLÜK: taş/doğa aileleri yürünür ama ÖZ taşır — üstlerine görev düşseydi
        /// o öz yatağı sessizce yok olurdu (<see cref="ChapterNodeManager"/> ilk yerleşimde de
        /// aynı kuralı uyguluyor). Su/dağ/nehir zaten yürünemez.
        /// </summary>
        private bool TryPickFallCoord(out HexCoordinate result)
        {
            result = default;
            if (_map == null || _grid == null) return false;

            HexCoordinate from = _player != null ? _player.CurrentCoord : new HexCoordinate(0, 0);

            var pool = new List<HexCoordinate>();
            for (int col = 0; col < _grid.Width; col++)
                for (int row = 0; row < _grid.Height; row++)
                {
                    // Tahta odd-r offset: ham dizi indisi KOORDİNAT DEĞİLDİR (2026-08-05 hatası).
                    var c = HexCoordinate.FromOffset(col, row);
                    if (c.Equals(from)) continue;
                    if (!_map.IsReachable(c)) continue;
                    var entry = TileCatalog.Get(_map.TerrainIdAt(c));
                    if (entry == null || entry.Family != TileFamily.Plain) continue;
                    if (_nodes.NodeAt(c) != null) continue;
                    pool.Add(c);
                }

            if (pool.Count == 0) return false;

            // Aynı seed + aynı oyuncu konumu → aynı sonuç (teşhis edilebilirlik).
            var rnd = new PythonRandom(_map.CurrentSeed + 3000 + OpenCount);
            rnd.Shuffle(pool);

            int hardMax = MaxTravelDistance();
            int max = Config != null ? Mathf.Min(Config.SpawnDistance.y, hardMax) : hardMax;
            int min = Config != null ? Mathf.Min(Config.SpawnDistance.x, max)     : 0;

            // Band → bandın altını serbest bırak → tamamen serbest. İlk tutan kazanır.
            if (TryFirstInBand(pool, from, min, max, out result)) return true;
            if (TryFirstInBand(pool, from, 0,   max, out result)) return true;
            return TryFirstInBand(pool, from, 0, int.MaxValue, out result);
        }

        private static bool TryFirstInBand(List<HexCoordinate> pool, HexCoordinate from,
                                           int min, int max, out HexCoordinate result)
        {
            foreach (var c in pool)
            {
                int d = from.DistanceTo(c);
                if (d < min || d > max) continue;
                result = c;
                return true;
            }
            result = default;
            return false;
        }

        /// <summary>
        /// Bölüm bitene dek kalan AP'nin yalnız <see cref="MandatoryQuestConfigSO.ReachSafety"/>
        /// kadarı yola ayrılabilir — oyuncunun görevi yapmak, bossa girmek ve gerekirse markete
        /// uğramak için de AP'si kalmalı.
        /// </summary>
        private int MaxTravelDistance()
        {
            if (_ap == null || Config == null) return int.MaxValue;

            int hardCut = _run != null ? _run.HardCutDay : 14;
            int perDay  = Mathf.Max(1, _ap.SlotsPerDay * _ap.MaxAP);
            int apLeft  = _ap.APRemainingToday + Mathf.Max(0, hardCut - _ap.CurrentDay) * perDay;
            int move    = Mathf.Max(1, _ap.APPerMove);

            return Mathf.Max(1, Mathf.FloorToInt((apLeft - Config.QuestAP) * Config.ReachSafety / move));
        }
    }
}
