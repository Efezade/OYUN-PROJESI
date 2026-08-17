using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// HARİTADAKİ ÖZ YATAKLARI — kaç tane, nerede, hangi türden (kullanıcı kararı 2026-08-17).
    ///
    /// ESKİ DAVRANIŞ: öz "karonun kendisiydi" — her taşlık/orman karosu otomatik öz verirdi.
    /// Haritada 500+ öz birikiyordu, hangi karonun öz verdiği görünmüyordu ve toplam denetlenemiyordu.
    ///
    /// YENİ DAVRANIŞ: haritaya <see cref="EssenceConfigSO.TotalRange"/> kadar (60–80) öz SAÇILIR.
    ///   • Yalnız YÜRÜNÜR ve oyuncunun yürüyerek ERİŞEBİLDİĞİ karolara (dağ ardındaki cebe düşmez).
    ///   • Tür karonun ailesinden gelir: taşlık aile → Taş, ormanlık aile → Doğa. Böylece öz hâlâ
    ///     araziyle uyumlu görünür; ama artık her orman karosu öz vermez, yalnız SEÇİLENLER verir.
    ///   • Seçim SEED'E BAĞLI rastgeledir (<see cref="PythonRandom"/>) → aynı harita hep aynı yataklar.
    ///   • TEK SEFERLİK: toplanınca karo ovaya döner, yatak silinir.
    ///
    /// Bu bileşen yalnız VERİYİ tutar. Karonun boyanması, konturu ve üstündeki hareketli küre
    /// <see cref="EssenceFieldVisuals"/>'in işidir (tek sorumluluk).
    /// </summary>
    [DefaultExecutionOrder(-85)]   // ChapterMapGenerator(-90) haritayı kurduktan SONRA, düğümlerden(-80) ÖNCE
    public class EssenceFieldManager : MonoBehaviour
    {
        /// <summary>Haritadaki tek bir öz yatağı.</summary>
        public class Deposit
        {
            public HexCoordinate Coord;
            public EssenceType   Type;
            public int           Amount;
        }

        [Header("Bağımlılıklar")]
        [SerializeField] private HexGridManager      _grid;
        [SerializeField] private ChapterMapGenerator _map;
        [SerializeField] private EssenceConfigSO     _config;
        [SerializeField] private EssenceWallet       _wallet;
        [SerializeField] private ActionPointManager  _ap;
        [SerializeField] private PlayerController    _player;

        [Header("Toplama")]
        [Tooltip("Öz toplamanın AP maliyeti (GAME_DESIGN §0: 1 AP).")]
        [SerializeField, Min(0)] private int _collectAPCost = 1;

        private readonly Dictionary<HexCoordinate, Deposit> _deposits = new();

        // ÖZÜ ALINMIŞ karolar. Kalıcı bir DURUM (efekt değil): karo "ruhu çekilmiş" gri kalır ve
        // bu grilik savaştan dönünce de sürmeli — grid yeniden kurulunca görsel katman bu kümeden
        // rengi geri boyar. Yalnız yeni harita üretilince temizlenir.
        private readonly HashSet<HexCoordinate> _drained = new();

        /// <summary>Yataklar yeniden saçıldı (harita üretildi) — görsel katman dinler.</summary>
        public event System.Action OnFieldRebuilt;

        /// <summary>Bir yatak haritadan kalktı. <c>collected</c> true ise OYUNCU TOPLADI
        /// (sökülme gösterisi + karo grileşir); false ise karo çöküşle yok oldu (gösteri yok).</summary>
        public event System.Action<HexCoordinate, bool> OnDepositRemoved;

        /// <summary>Haritada duran (henüz toplanmamış) yataklar.</summary>
        public IEnumerable<Deposit> Deposits => _deposits.Values;

        /// <summary>Özü alınmış (gri kalan) karolar.</summary>
        public IEnumerable<HexCoordinate> DrainedTiles => _drained;

        public int DepositCount => _deposits.Count;

        /// <summary>Haritada duran toplam öz (HUD/log).</summary>
        public int RemainingTotal
        {
            get { int s = 0; foreach (var d in _deposits.Values) s += d.Amount; return s; }
        }

        public EssenceConfigSO Config => _config;

        private void OnEnable()
        {
            if (_map != null) _map.OnMapGenerated += Rebuild;
        }

        private void OnDisable()
        {
            if (_map != null) _map.OnMapGenerated -= Rebuild;
        }

        // ── Yerleşim ─────────────────────────────────────────────────────────

        /// <summary>Harita üretilince yatakları yeniden saçar.</summary>
        public void Rebuild()
        {
            _deposits.Clear();
            _drained.Clear();                 // yeni harita → eski gri karolar geçersiz
            if (_map == null || _grid == null || _config == null)
            {
                Debug.LogWarning("[Oz] EssenceFieldManager: grid/harita/config atanmamis — oz sacilmadi.");
                OnFieldRebuilt?.Invoke();
                return;
            }

            List<HexCoordinate> pool = BuildPool();

            // Aynı seed → aynı yataklar. (+2000: düğüm yerleşiminin (+1000) ofsetiyle çakışmasın,
            // yoksa iki sistem aynı karışım sırasını kullanır ve yataklar hep düğümlere yapışırdı.)
            var rnd = new PythonRandom(_map.CurrentSeed + 2000);
            rnd.Shuffle(pool);

            Vector2Int range  = _config.TotalRange;
            Vector2Int perTile = _config.AmountPerTile;
            int target = range.x >= range.y ? range.x : rnd.RandInt(range.x, range.y);
            int total  = 0;

            foreach (HexCoordinate c in pool)
            {
                if (total >= target) break;

                EssenceType type = TypeOf(c);
                int amount = Mathf.Clamp(TileAmount(c), perTile.x, perTile.y);

                // Üst sınırı AŞMA: kalan boşluğa sığmayan yatağı küçült, sığmıyorsa atla.
                int room = range.y - total;
                if (room <= 0) break;
                if (amount > room) amount = room;

                _deposits[c] = new Deposit { Coord = c, Type = type, Amount = amount };
                total += amount;
            }

            LogPlacement(pool.Count, target, total);
            OnFieldRebuilt?.Invoke();
        }

        /// <summary>Aday karolar: erişilebilir + yürünür + taşlık/ormanlık aile + oyuncunun karosu değil.
        /// Aile ile öz türü eşleşmezse (config o türü saçmıyorsa) karo havuza girmez.</summary>
        private List<HexCoordinate> BuildPool()
        {
            var pool = new List<HexCoordinate>();
            HexCoordinate start = _player != null ? _player.CurrentCoord : new HexCoordinate(0, 0);

            for (int col = 0; col < _grid.Width; col++)
                for (int row = 0; row < _grid.Height; row++)
                {
                    var c = HexCoordinate.FromOffset(col, row);
                    if (c.Equals(start)) continue;
                    if (!_map.IsReachable(c)) continue;

                    // Karo gerçekten YÜRÜNÜR mü — hem katalog hem grid hücresi sorulur. Katalog
                    // "yürünür" dese de düğüm/çöküş hücreyi kapatmış olabilir.
                    var entry = TileCatalog.Get(_map.TerrainIdAt(c));
                    if (entry == null || !entry.Walkable) continue;
                    if (!_grid.TryGetCell(c, out HexCell cell) || !cell.IsWalkable) continue;

                    if (!FamilyToType(entry.Family, out EssenceType type)) continue;
                    if (!_config.IsMapType(type)) continue;

                    pool.Add(c);
                }
            return pool;
        }

        /// <summary>Karo ailesi → öz türü. Taşlık aile taş, ormanlık aile doğa verir; diğerleri öz vermez.</summary>
        private static bool FamilyToType(TileFamily family, out EssenceType type)
        {
            switch (family)
            {
                case TileFamily.Stone:  type = EssenceType.Tas;  return true;
                case TileFamily.Nature: type = EssenceType.Doga; return true;
                default:                type = EssenceType.Tas;  return false;
            }
        }

        private EssenceType TypeOf(HexCoordinate c)
        {
            var entry = TileCatalog.Get(_map.TerrainIdAt(c));
            FamilyToType(entry != null ? entry.Family : TileFamily.Plain, out EssenceType t);
            return t;
        }

        /// <summary>Karonun zenginliği: katalogdaki öz değeri (orman 2, yüksek orman 3…). Katalogda
        /// değer yoksa 1 — yatak yine de bir şey versin.</summary>
        private int TileAmount(HexCoordinate c)
        {
            TileCatalog.EssenceOf(_map.TerrainIdAt(c), out int amount, out _);
            return amount > 0 ? amount : 1;
        }

        private void LogPlacement(int poolSize, int target, int total)
        {
            int tas = 0, doga = 0;
            foreach (var d in _deposits.Values)
                if (d.Type == EssenceType.Tas) tas += d.Amount; else doga += d.Amount;

            Debug.Log($"[Oz] {_deposits.Count} yatak sacildi — toplam {total} oz " +
                      $"(hedef {target}, aralik {_config.TotalRange.x}-{_config.TotalRange.y}) | " +
                      $"tas {tas} · doga {doga} | aday havuz {poolSize} karo");

            if (total < _config.TotalRange.x)
                Debug.LogWarning($"[Oz] Hedefin ALTINDA kalindi ({total} < {_config.TotalRange.x}) — " +
                                 "haritada yeterli taslik/ormanlik karo yok. Seed havuzu ya da " +
                                 "EssenceConfig.AmountPerTile gozden gecirilmeli.");
        }

        // ── Sorgular ─────────────────────────────────────────────────────────

        public bool HasEssenceAt(HexCoordinate c) => _deposits.ContainsKey(c);

        public Deposit DepositAt(HexCoordinate c)
            => _deposits.TryGetValue(c, out Deposit d) ? d : null;

        /// <summary>UI metni, örn "2 Doğa".</summary>
        public string Describe(HexCoordinate c)
        {
            Deposit d = DepositAt(c);
            if (d == null) return "";
            string name = _config != null ? _config.NameOf(d.Type) : d.Type.ToString();
            return $"{d.Amount} {name}";
        }

        public int CollectAPCost => _collectAPCost;

        public bool CanCollect(HexCoordinate c)
            => HasEssenceAt(c) && (_ap == null || _ap.CurrentAP >= _collectAPCost);

        // ── Eylemler ─────────────────────────────────────────────────────────

        /// <summary>Yatağı topla: AP harca, cüzdana ekle, karoyu TÜKET (ovaya çevir), süsü kaldır.
        ///
        /// SIRA ÖNEMLİ: karo ÖNCE ovaya çevrilir (SetTile görseli yeniden üretir ve rengini
        /// sıfırlar), olay SONRA yayılır. Ters olsaydı görsel katmanın bastığı "ruhu çekilmiş gri"
        /// hemen ardından gelen yeniden üretimle silinirdi.</summary>
        public bool CollectAt(HexCoordinate c)
        {
            if (!CanCollect(c)) return false;
            Deposit d = _deposits[c];

            if (_ap != null) _ap.SpendAP(_collectAPCost);
            if (_wallet != null) _wallet.Gain(d.Type, d.Amount);

            _deposits.Remove(c);
            _drained.Add(c);

            // TEK SEFERLİK: karo tükenir → ova.
            _map.SetTile(c, TerrainGenerator.DepletedId);

            OnDepositRemoved?.Invoke(c, true);
            return true;
        }

        /// <summary>Karo artık yürünemez oldu (çöküş sildi) → üstündeki yatak da kaybolur.
        /// <see cref="MapCollapseManager.OnTileCollapsed"/> koordinat taşımadığı için tarama yapılır;
        /// çöküş günde birkaç kez olur, maliyeti önemsiz.</summary>
        public void PruneUnwalkable()
        {
            if (_grid == null || _deposits.Count == 0) return;

            List<HexCoordinate> gone = null;
            foreach (var kv in _deposits)
                if (!_grid.TryGetCell(kv.Key, out HexCell cell) || !cell.IsWalkable)
                    (gone ??= new List<HexCoordinate>()).Add(kv.Key);

            if (gone == null) return;
            foreach (var c in gone)
            {
                _deposits.Remove(c);
                OnDepositRemoved?.Invoke(c, false);   // toplanmadı — sökülme gösterisi yok
            }
            Debug.Log($"[Oz] {gone.Count} yatak cokusle kayboldu — kalan {RemainingTotal} oz.");
        }
    }
}
