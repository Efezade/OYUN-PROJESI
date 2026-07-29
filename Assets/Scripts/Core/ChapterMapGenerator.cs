using UnityEngine;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Bölümün haritasını PROSEDÜREL üretip grid'e uygular (TASK-005 · GAME_DESIGN.md §3).
    /// Üretimin kendisi <see cref="TerrainGenerator"/>'da (Python referansının birebir portu);
    /// bu bileşen yalnız "seed seç → üret → TileMap'e çevir → grid'e ver → özü yönet" işini yapar.
    ///
    /// **Seed havuzu SABİT** (<see cref="TerrainConfigSO.SeedPool"/>, 10 adet). Sonsuz/tam rastgele
    /// üretim YOK — bu 10 harita adalet/oynanabilirlik için elle doğrulandı. Havuzdan seçim rastgele
    /// ama SON OYNANANDAN FARKLI olacak şekilde yapılır (retry'de aynı haritayı tekrar vermemek için;
    /// son seed PlayerPrefs'te tutulur).
    ///
    /// ÖZ TEK SEFERLİK: öz ayrı bir node değil, karonun kendisidir. Toplanınca karo
    /// <see cref="TerrainGenerator.DepletedId"/> (ova) olur ve görseli yenilenir — bir daha vermez.
    /// </summary>
    [DefaultExecutionOrder(-90)]   // HexGridManager(-100) kurulduktan SONRA, tüketicilerden ÖNCE
    public class ChapterMapGenerator : MonoBehaviour
    {
        [SerializeField] private HexGridManager     _grid;
        [SerializeField] private TerrainConfigSO    _config;
        [SerializeField] private EssenceWallet      _wallet;
        [SerializeField] private ActionPointManager _ap;
        [SerializeField] private PlayerController   _player;

        [Tooltip("Öz toplamanın AP maliyeti (GAME_DESIGN §0: 1 AP).")]
        [SerializeField, Min(0)] private int _collectAPCost = 1;

        [Tooltip("Kapalıysa üretim yapılmaz — grid'e elle atanmış TileMap kullanılır (elle boyanmış " +
                 "haritayla test etmek için).")]
        [SerializeField] private bool _generateOnStart = true;

        private const string LastSeedKey = "TacticalRPG.LastChapterSeed";

        private string[,] _terrain;      // [q, r] — üretilmiş karo tipleri (öz tükenince güncellenir)
        private TileMapSO _runtimeMap;   // RUNTIME kopya — asset'e YAZILMAZ (CLAUDE.md §2)

        /// <summary>Bu koşumda kullanılan seed (HUD/log için).</summary>
        public int CurrentSeed { get; private set; } = -1;

        /// <summary>Harita üretildi/yeniden üretildi.</summary>
        public event System.Action OnMapGenerated;

        private void Start()
        {
            if (_generateOnStart) GenerateNew();
        }

        /// <summary>Havuzdan (son oynanandan farklı) bir seed seçip haritayı üretir.</summary>
        public void GenerateNew() => Generate(PickSeed());

        /// <summary>Belirli bir seed ile üretir — 10'luk havuzun test/doğrulaması için.</summary>
        public void Generate(int seed) => GenerateInto(null, seed);

        /// <summary>Haritayı üretir ve grid'e uygular.
        /// <paramref name="target"/> null ise RUNTIME kopyaya yazar (oyun; asset'e dokunulmaz —
        /// CLAUDE.md §2). Bir TileMapSO ASSET'i verilirse ONA yazar: editörde Play'e basmadan da
        /// üretilen harita sahnede kalıcı görünsün diye (kullanıcı isteği 2026-07-29).</summary>
        public void GenerateInto(TileMapSO target, int seed)
        {
            if (_grid == null || _config == null)
            {
                Debug.LogError("[Bolum] ChapterMapGenerator: grid ya da TerrainConfig atanmamis.");
                return;
            }

            CurrentSeed = seed;
            _terrain = TerrainGenerator.Generate(
                _config.Width, _config.Height, seed,
                _config.ObstaclePct, _config.RiverCount, _config.BridgesPerRiver,
                _config.BlobSizeMin, _config.BlobSizeMax, _config.SubtypeWeights());

            // Hedef verilmediyse RUNTIME kopya (asset'e yazılmaz); verildiyse o asset'e yazılır.
            if (target != null)
            {
                _runtimeMap = target;
                _runtimeMap.assignments.Clear();
            }
            else
            {
                _runtimeMap      = ScriptableObject.CreateInstance<TileMapSO>();
                _runtimeMap.name = $"ChapterMap_Seed{seed}";
            }
            _runtimeMap.defaultTileId = TerrainGenerator.OvaId;
            for (int q = 0; q < _config.Width; q++)
                for (int r = 0; r < _config.Height; r++)
                    _runtimeMap.SetTileId(new HexCoordinate(q, r), _terrain[q, r]);

            _grid.SetTileMap(_runtimeMap);   // grid yeniden üretilir → sis/çöküş kendini yeniler

            // Oyuncuyu YÜRÜNÜR bir karoya koy — sabit başlangıç koordinatı prosedürel haritada
            // dağın/gölün içine denk gelebilirdi.
            if (_player != null) _player.Initialize(ResolveStartCoord());

            PlayerPrefs.SetInt(LastSeedKey, seed);
            Debug.Log($"[Bolum] Harita uretildi — seed {seed} ({_config.Width}x{_config.Height}).");
            OnMapGenerated?.Invoke();
        }

        /// <summary>Havuzdan seed seç: mümkünse son oynanandan farklı.</summary>
        private int PickSeed()
        {
            var pool = _config.SeedPool;
            if (pool == null || pool.Count == 0) return 1;
            if (pool.Count == 1) return pool[0];

            int last = PlayerPrefs.GetInt(LastSeedKey, -1);
            int pick = pool[UnityEngine.Random.Range(0, pool.Count)];
            for (int guard = 0; pick == last && guard < 16; guard++)
                pick = pool[UnityEngine.Random.Range(0, pool.Count)];
            return pick;
        }

        // ── Öz (terrain'in kendisi) ──────────────────────────────────────────

        private bool InRange(HexCoordinate c)
            => _terrain != null && c.Q >= 0 && c.Q < _terrain.GetLength(0)
                                && c.R >= 0 && c.R < _terrain.GetLength(1);

        /// <summary>Bu karonun terrain tipi (yoksa null).</summary>
        public string TerrainIdAt(HexCoordinate c) => InRange(c) ? _terrain[c.Q, c.R] : null;

        /// <summary>Karonun tipini değiştir (hem terrain hem runtime TileMap hem görsel).
        /// Düğüm yerleşimi kullanır: ör. gözetleme kulesi karosunu gerçek "kule" karosuna çevirmek —
        /// böylece palette'teki kule modeli render edilir, ayrı bir işaret nesnesi gerekmez.</summary>
        public void SetTile(HexCoordinate c, string tileId)
        {
            if (!InRange(c) || string.IsNullOrEmpty(tileId)) return;
            _terrain[c.Q, c.R] = tileId;
            if (_runtimeMap != null) _runtimeMap.SetTileId(c, tileId);
            if (_grid != null) _grid.RegenerateCellVisual(c);   // IsWalkable de palete göre senkronlanır
        }

        /// <summary>Bu karoda toplanabilir öz var mı?</summary>
        public bool HasEssenceAt(HexCoordinate c)
        {
            if (!InRange(c)) return false;
            TerrainGenerator.EssenceOf(_terrain[c.Q, c.R], out int amount, out _);
            return amount > 0;
        }

        /// <summary>UI metni, örn "2 Doğa (orman)".</summary>
        public string Describe(HexCoordinate c)
        {
            if (!InRange(c)) return "";
            string id = _terrain[c.Q, c.R];
            TerrainGenerator.EssenceOf(id, out int amount, out TerrainGenerator.EssenceKind kind);
            if (amount <= 0) return "";
            return $"{amount} {(kind == TerrainGenerator.EssenceKind.Tas ? "Taş" : "Doğa")} ({id})";
        }

        public bool CanCollect(HexCoordinate c)
            => HasEssenceAt(c) && (_ap == null || _ap.CurrentAP >= _collectAPCost);

        /// <summary>Karonun özünü topla: AP harca, cüzdana ekle, karoyu TÜKET (ovaya çevir).</summary>
        public bool CollectAt(HexCoordinate c)
        {
            if (!CanCollect(c)) return false;

            TerrainGenerator.EssenceOf(_terrain[c.Q, c.R], out int amount, out TerrainGenerator.EssenceKind kind);
            if (amount <= 0) return false;

            if (_ap != null) _ap.SpendAP(_collectAPCost);

            if (_wallet != null)
                _wallet.Gain(kind == TerrainGenerator.EssenceKind.Tas ? EssenceType.Tas : EssenceType.Doga, amount);

            // TEK SEFERLİK: karo tükenir → ova. Hem terrain'de hem runtime TileMap'te.
            _terrain[c.Q, c.R] = TerrainGenerator.DepletedId;
            if (_runtimeMap != null) _runtimeMap.SetTileId(c, TerrainGenerator.DepletedId);
            if (_grid != null) _grid.RegenerateCellVisual(c);

            return true;
        }

        /// <summary>Oyuncunun başlayacağı karo: config'teki ipucundan en yakın YÜRÜNÜR karo,
        /// ana bağlantılı bileşen içinde (harita parçalıysa küçük bir adada doğmasın).</summary>
        public HexCoordinate ResolveStartCoord()
        {
            if (_terrain == null) return new HexCoordinate(0, 0);
            Vector2Int hint = _config != null ? _config.StartHint : new Vector2Int(0, 0);
            TerrainGenerator.ConnectedComponent(_terrain, hint.x, hint.y, out (int q, int r) start);
            return new HexCoordinate(start.q, start.r);
        }
    }
}
