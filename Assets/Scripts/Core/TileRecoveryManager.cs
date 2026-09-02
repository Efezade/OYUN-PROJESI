using System;
using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// KARO GERİ GETİRME (kullanıcı isteği 2026-09-02, madde 10) — çöküşün karşı mekaniği.
    ///
    /// İKİ AYRI ADIM, bilinçli olarak ayrıldı:
    ///   1) HAK KAZANMA (arazide, çukurun kenarında): çökmüş bir karonun YANINDAKİ yürünebilir
    ///      karoda dururken oyuncu ya RİSKE girer (bedava, rastgele) ya ÖZ ÖDER (kesin). Kazanılan
    ///      şey karo değil <see cref="Credits"/> — "kaç karo geri getirebilirim" hakkı.
    ///   2) YERLEŞTİRME (harita ekranında): kazanılan hak, TANRISAL BAKIŞLA harcanır — oyuncu tüm
    ///      haritadan, sisin DIŞINDA kalan çökmüş karolardan istediğini seçer.
    ///
    /// Neden ayrı: kullanıcının anlattığı akış tam olarak bu ("+5 karo geri getirme kazandın →
    /// sonra istediğin yerde kullanırsın"). Hak kazandığın çukuru geri getirmek zorunda değilsin;
    /// yolunu açmak istediğin BAŞKA bir çukuru geri getirebilirsin.
    ///
    /// AYNI ÇUKUR BİR KEZ DENENİR (<see cref="_attempted"/>): yoksa oyuncu tek bir çukurun
    /// kenarında durup risk mekaniğini sonsuz çevirir, ekonomi anlamsızlaşır.
    /// </summary>
    public class TileRecoveryManager : MonoBehaviour
    {
        [Header("Bağımlılıklar (boşsa sahnede aranır)")]
        [SerializeField] private HexGridManager      _grid;
        [SerializeField] private MapCollapseManager  _collapse;
        [SerializeField] private PlayerController    _player;
        [Tooltip("Sisin DIŞINDA kalma şartı için gerekir. Boşsa şart uygulanmaz (her çukur seçilebilir).")]
        [SerializeField] private FogOfWarManager     _fog;
        [Tooltip("Ödemeli deneme için. Boşsa yalnız riskli deneme kullanılabilir.")]
        [SerializeField] private EssenceWallet       _wallet;
        [Tooltip("Riskli denemenin başarısızlık bedeli (AP) buradan alınır. Boşsa bedel uygulanmaz.")]
        [SerializeField] private ActionPointManager  _ap;

        [Header("Riskli deneme (bedava, rastgele)")]
        [Tooltip("Çukura inmenin işe yarama şansı.")]
        [SerializeField, Range(0f, 1f)] private float _riskSuccessChance = 0.55f;
        [Tooltip("Başarılı riskin verdiği hak — EN AZ.")]
        [SerializeField, Min(1)] private int _riskRewardMin = 1;
        [Tooltip("Başarılı riskin verdiği hak — EN FAZLA. Riskin ödülü ödemeden yüksek olmalı, " +
                 "yoksa riske girmenin bir anlamı kalmaz.")]
        [SerializeField, Min(1)] private int _riskRewardMax = 3;
        [Tooltip("Risk BAŞARISIZ olursa kaybedilen AP (toprak altında vakit kaybı). 0 = bedelsiz.")]
        [SerializeField, Min(0)] private int _riskFailureAPCost = 2;

        [Header("Ödemeli deneme (kesin)")]
        [Tooltip("Ödemenin verdiği hak. Kesin olduğu için riskin ortalamasından düşük tutulur.")]
        [SerializeField, Min(1)] private int _paidReward = 1;
        [Tooltip("Ödemeli denemenin bedeli. TASLAK fiyat — gerçek ekonomide ayarlanacak.")]
        [SerializeField] private EssenceAmount[] _paidCost =
        {
            new EssenceAmount(EssenceType.Tas,  4),
            new EssenceAmount(EssenceType.Doga, 3),
        };

        [Header("Kural")]
        [Tooltip("Çukura kaç karo mesafeden inilebilir. 1 = yalnız bitişik karodan (kullanıcı: " +
                 "'yakın yürünebilir bir karo varsa').")]
        [SerializeField, Range(1, 3)] private int _reachRange = 1;
        [Tooltip("AÇIK: geri getirilecek karo sisin DIŞINDA olmalı (kullanıcı isteği). " +
                 "KAPALI: haritadaki her çukur seçilebilir (test kolaylığı).")]
        [SerializeField] private bool _requireKnownTile = true;

        // ── Durum ────────────────────────────────────────────────────────────

        /// <summary>Elde duran "karo geri getirme" hakkı.</summary>
        public int Credits { get; private set; }

        /// <summary>Hak sayısı ya da geri getirilebilir çukur listesi değişti.</summary>
        public event Action OnChanged;

        /// <summary>Denenmiş çukurlar — aynı çukur ikinci kez hak vermez.</summary>
        private readonly HashSet<HexCoordinate> _attempted = new();

        private readonly List<HexCoordinate> _restorable = new();

        private void Awake()
        {
            // Kritik bağlar koddan da kurulur (CLAUDE.md: kurulum editöre bırakılmaz).
            if (_grid     == null) _grid     = FindFirstObjectByType<HexGridManager>();
            if (_collapse == null) _collapse = FindFirstObjectByType<MapCollapseManager>();
            if (_player   == null) _player   = FindFirstObjectByType<PlayerController>();
            if (_fog      == null) _fog      = FindFirstObjectByType<FogOfWarManager>();
            if (_wallet   == null) _wallet   = FindFirstObjectByType<EssenceWallet>();
            if (_ap       == null) _ap       = FindFirstObjectByType<ActionPointManager>();
        }

        private void OnEnable()
        {
            if (_collapse != null)
            {
                _collapse.OnTileCollapsed += HandleCollapsed;
                _collapse.OnTileRestored  += HandleRestored;
            }
        }

        private void OnDisable()
        {
            if (_collapse != null)
            {
                _collapse.OnTileCollapsed -= HandleCollapsed;
                _collapse.OnTileRestored  -= HandleRestored;
            }
        }

        private void HandleCollapsed(int _, int __) => OnChanged?.Invoke();
        private void HandleRestored(HexCoordinate _) => OnChanged?.Invoke();

        /// <summary>Yeni bölüm: haklar ve deneme geçmişi sıfırlanır (çöküş de sıfırlanıyor).</summary>
        public void ResetRecovery()
        {
            Credits = 0;
            _attempted.Clear();
            OnChanged?.Invoke();
        }

        // ── 1) HAK KAZANMA (arazide) ─────────────────────────────────────────

        /// <summary>
        /// Oyuncunun yanında DENENEBİLİR bir çukur var mı? Şartlar: menzilde, kalıcı çökmüş, daha
        /// önce denenmemiş ve oyuncunun bastığı karo yürünebilir (yani çukurun kenarındayız).
        /// </summary>
        public bool TryGetAttemptTarget(out HexCoordinate target)
        {
            target = default;
            if (_grid == null || _collapse == null || _player == null) return false;

            HexCoordinate from = _player.CurrentCoord;
            int best = int.MaxValue;

            foreach (HexCoordinate coord in _collapse.CollapsedTiles)
            {
                if (_attempted.Contains(coord)) continue;
                int dist = from.DistanceTo(coord);
                if (dist == 0 || dist > _reachRange) continue;
                if (dist >= best) continue;
                best   = dist;
                target = coord;
            }
            return best != int.MaxValue;
        }

        /// <summary>Bu çukur daha önce denendi mi?</summary>
        public bool WasAttempted(HexCoordinate coord) => _attempted.Contains(coord);

        /// <summary>Ödemeli denemenin bedeli (UI yazısı için).</summary>
        public IReadOnlyList<EssenceAmount> PaidCost => _paidCost;

        public bool CanAffordPaid() => _wallet != null && _paidCost != null && _wallet.CanAfford(_paidCost);

        /// <summary>
        /// RİSKLİ deneme: bedava, sonuç rastgele. Başarı → <see cref="_riskRewardMin"/>..
        /// <see cref="_riskRewardMax"/> hak. Başarısızlık → hak yok + AP bedeli.
        /// </summary>
        /// <returns>Kazanılan hak (0 = başarısız). Çukur her iki durumda da "denenmiş" olur.</returns>
        public int AttemptRisk(HexCoordinate target, out string message)
        {
            if (!ValidateTarget(target, out message)) return 0;

            _attempted.Add(target);

            if (UnityEngine.Random.value > _riskSuccessChance)
            {
                if (_ap != null && _riskFailureAPCost > 0) _ap.SpendAP(_riskFailureAPCost);
                message = _riskFailureAPCost > 0
                    ? $"Çukur çöktü — eli boş çıktın ({_riskFailureAPCost} AP kayıp)."
                    : "Çukur çöktü — eli boş çıktın.";
                OnChanged?.Invoke();
                return 0;
            }

            int gained = UnityEngine.Random.Range(_riskRewardMin, _riskRewardMax + 1);
            Grant(gained);
            message = $"Zeminden {gained} karo geri getirme hakkı çıkardın.";
            return gained;
        }

        /// <summary>ÖDEMELİ deneme: öz harcanır, sonuç kesin.</summary>
        /// <returns>Kazanılan hak (0 = öz yetmedi ya da çukur geçersiz).</returns>
        public int AttemptPaid(HexCoordinate target, out string message)
        {
            if (!ValidateTarget(target, out message)) return 0;

            if (_wallet == null || _paidCost == null || _paidCost.Length == 0)
            { message = "Ödeme yapılamıyor: öz cüzdanı bağlı değil."; return 0; }

            if (!_wallet.TrySpend(_paidCost))
            { message = "Öz yetmiyor."; return 0; }

            _attempted.Add(target);
            Grant(_paidReward);
            message = $"Öz karşılığı {_paidReward} karo geri getirme hakkı aldın.";
            return _paidReward;
        }

        private bool ValidateTarget(HexCoordinate target, out string message)
        {
            if (_collapse == null || !_collapse.IsCollapsed(target))
            { message = "Burada çökmüş karo yok."; return false; }

            if (_attempted.Contains(target))
            { message = "Bu çukur zaten denendi."; return false; }

            if (_player != null && _player.CurrentCoord.DistanceTo(target) > _reachRange)
            { message = "Çukur çok uzak — kenarına gelmelisin."; return false; }

            message = string.Empty;
            return true;
        }

        /// <summary>Hak ekler (görev/ödül sistemleri de buradan verebilir: "+5 karo geri getirme").</summary>
        public void Grant(int amount)
        {
            if (amount <= 0) return;
            Credits += amount;
            OnChanged?.Invoke();
        }

        // ── 2) YERLEŞTİRME (harita ekranı) ───────────────────────────────────

        /// <summary>
        /// Geri getirilebilir çukurlar: kalıcı çökmüş + (şart açıksa) sisin DIŞINDA + henüz düşmek
        /// üzere İŞARETLİ olmayan. Liste her çağrıda tazelenir; harita ekranı bunu çizer.
        /// </summary>
        public IReadOnlyList<HexCoordinate> RestorableTiles()
        {
            _restorable.Clear();
            if (_collapse == null) return _restorable;

            foreach (HexCoordinate coord in _collapse.CollapsedTiles)
                if (CanRestore(coord)) _restorable.Add(coord);

            return _restorable;
        }

        /// <summary>Bu çukur şu an geri getirilebilir mi? (Hak sayısına BAKMAZ — onu çağıran sorar.)</summary>
        public bool CanRestore(HexCoordinate coord)
        {
            if (_collapse == null || !_collapse.IsCollapsed(coord)) return false;
            if (_grid == null || !_grid.TryGetCell(coord, out _))   return false;
            if (_requireKnownTile && _fog != null && !_fog.IsKnown(coord)) return false;
            return true;
        }

        /// <summary>
        /// Hak harcayarak çukuru geri getirir. Karo çöküş ÖNCESİ hâline döner
        /// (<see cref="MapCollapseManager.RestoreTile"/>).
        /// </summary>
        public bool TryRestore(HexCoordinate coord, out string message)
        {
            if (Credits <= 0)
            { message = "Karo geri getirme hakkın yok."; return false; }

            if (!CanRestore(coord))
            {
                message = _requireKnownTile && _fog != null && !_fog.IsKnown(coord)
                    ? "Orası sisin içinde — göremediğin yeri onaramazsın."
                    : "Burada geri getirilecek karo yok.";
                return false;
            }

            if (!_collapse.RestoreTile(coord))
            { message = "Karo geri getirilemedi (işaretli ya da haritada yok)."; return false; }

            Credits--;
            OnChanged?.Invoke();
            message = Credits > 0
                ? $"Karo geri geldi. Kalan hak: {Credits}."
                : "Karo geri geldi. Hakkın kalmadı.";
            return true;
        }
    }
}
