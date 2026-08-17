using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Bölümün haritasını PROSEDÜREL üretip grid'e uygular (GAME_DESIGN.md §3).
    /// Üretimin kendisi <see cref="TerrainGenerator"/>'da (organik kıta boru hattı);
    /// bu bileşen yalnız "seed seç → üret → TileMap'e çevir → grid'e ver → özü yönet" işini yapar.
    ///
    /// **Seed havuzu SABİT** (<see cref="TerrainConfigSO.SeedPool"/>, 30 adet). Sonsuz/tam rastgele
    /// üretim YOK — bu 30 harita otomatik taramayla oran/bağlantı/AP-baskısı filtrelerinden geçirildi.
    /// Havuzdan seçim rastgele ama SON OYNANANDAN FARKLI olacak şekilde yapılır (retry'de aynı
    /// haritayı tekrar vermemek için; son seed PlayerPrefs'te tutulur).
    ///
    /// ÖZ ARTIK BURADA DEĞİL (2026-08-17): "her taşlık/orman karosu öz verir" kuralı kalktı,
    /// yerine haritaya 60–80 öz SAÇAN <see cref="EssenceFieldManager"/> geldi. Bu bileşen özün
    /// yalnız ALTYAPISINI sağlar: karo tipi sorgusu (<see cref="TerrainIdAt"/>), karo değiştirme
    /// (<see cref="SetTile"/>) ve erişilebilirlik (<see cref="IsReachable"/>).
    /// </summary>
    [DefaultExecutionOrder(-90)]   // HexGridManager(-100) kurulduktan SONRA, tüketicilerden ÖNCE
    public class ChapterMapGenerator : MonoBehaviour
    {
        [SerializeField] private HexGridManager     _grid;
        [SerializeField] private TerrainConfigSO    _config;
        [SerializeField] private PlayerController   _player;

        [Tooltip("Kapalıysa üretim yapılmaz — grid'e elle atanmış TileMap kullanılır (elle boyanmış " +
                 "haritayla test etmek için).")]
        [SerializeField] private bool _generateOnStart = true;

        private const string LastSeedKey = "TacticalRPG.LastChapterSeed";

        private string[,] _terrain;      // [sütun, satır] — üretilmiş karo tipleri (öz tükenince güncellenir)
        private TileMapSO _runtimeMap;   // RUNTIME kopya — asset'e YAZILMAZ (CLAUDE.md §2)

        // Oyuncunun başlangıcından YÜRÜYEREK erişebildiği karolar (tahta koordinatında).
        // Dağ/göl arkasında kalan cepler buraya girmez; düğüm yerleşimi bunu kullanır
        // (referans: harita_map1_sim.build_nodes → havuz `walkable_comp`'tan seçilir).
        private readonly HashSet<HexCoordinate> _reachable = new();
        private HexCoordinate _startCoord;

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
            MapResult result = TerrainGenerator.Generate(_config.ToParams(), seed);
            _terrain = result.Tiles;

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
            // (sütun, satır) → tahtanın AXIAL koordinatı. Bu dönüşüm 2026-08-05'e kadar YOKTU:
            // harita tahtaya kayık oturuyordu (sol altta giderek büyüyen boş "ova" kaması, sağdaki
            // üretim tahta dışına düşüp çöpe gidiyordu — 550 karonun 144'ü, %26).
            //
            // VARSAYILAN ARTIK "BOŞ": atanmamış her koordinatta hücre ÜRETİLMEZ. Organik sınır
            // buradan geliyor — kıtanın dışı gerçekten yok, sadece görünmeyen değil.
            _runtimeMap.defaultTileId = TerrainGenerator.VoidId;
            // Tahta boyutunu HARİTA taşısın: grid'in Inspector değeri config'le uyuşmazsa
            // (ör. TAM KURULUM koşmadıysa) harita kırpılırdı — sessiz ve teşhisi zor bir hata.
            _runtimeMap.SetGridSize(_config.Width, _config.Height);
            for (int col = 0; col < _config.Width; col++)
                for (int row = 0; row < _config.Height; row++)
                    _runtimeMap.SetTileId(HexCoordinate.FromOffset(col, row), _terrain[col, row]);

            BuildReachable(result);

            _grid.SetTileMap(_runtimeMap);   // grid yeniden üretilir → sis/çöküş kendini yeniler

            // Oyuncuyu YÜRÜNÜR bir karoya koy — sabit başlangıç koordinatı organik haritada
            // denizin/dağın içine denk gelirdi.
            if (_player != null) _player.Initialize(_startCoord);

            PlayerPrefs.SetInt(LastSeedKey, seed);
            Debug.Log($"[Bolum] Harita uretildi — seed {seed} | kara {result.Land} karo " +
                      $"(yurunur %{result.WalkablePct:F1} · nehir %{result.RiverPct:F1} · " +
                      $"dag %{result.MountainPct:F1} · orman/gol %{result.BlobPct:F1} · " +
                      $"gecit %{result.CrossingPct:F2}) | erisilebilir %{result.ReachablePct:F1} " +
                      $"({result.MainComponent} karo) | oz arzi {result.EssenceSupply} | " +
                      $"landmark {result.Landmark} | sinir dekoru {result.Fringe}");
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

        // ── Karo sorguları (öz yerleşimi ve düğümler bunları kullanır) ──────

        // Dışarıdan gelen koordinatlar TAHTANIN axial'i; iç dizi (sütun, satır) indisli.
        private bool InRange(HexCoordinate c)
        {
            if (_terrain == null) return false;
            c.ToOffset(out int col, out int row);
            return col >= 0 && col < _terrain.GetLength(0)
                && row >= 0 && row < _terrain.GetLength(1);
        }

        /// <summary>Axial koordinatın dizideki karşılığı (sınır kontrolü çağıranın işi).</summary>
        private string TerrainRef(HexCoordinate c)
        {
            c.ToOffset(out int col, out int row);
            return _terrain[col, row];
        }

        private void SetTerrain(HexCoordinate c, string id)
        {
            c.ToOffset(out int col, out int row);
            _terrain[col, row] = id;
        }

        /// <summary>Bu karonun terrain tipi (yoksa null).</summary>
        public string TerrainIdAt(HexCoordinate c) => InRange(c) ? TerrainRef(c) : null;

        /// <summary>Karonun tipini değiştir (hem terrain hem runtime TileMap hem görsel).
        /// Düğüm yerleşimi kullanır: ör. gözetleme kulesi karosunu gerçek "kule" karosuna çevirmek —
        /// böylece palette'teki kule modeli render edilir, ayrı bir işaret nesnesi gerekmez.</summary>
        public void SetTile(HexCoordinate c, string tileId)
        {
            if (!InRange(c) || string.IsNullOrEmpty(tileId)) return;
            SetTerrain(c, tileId);
            if (_runtimeMap != null) _runtimeMap.SetTileId(c, tileId);
            if (_grid != null) _grid.RegenerateCellVisual(c);   // IsWalkable de palete göre senkronlanır
        }

        /// <summary>Oyuncunun başlayacağı karo: config'teki ipucundan en yakın YÜRÜNÜR karo,
        /// ana bağlantılı bileşen içinde (harita parçalıysa küçük bir cepte doğmasın).</summary>
        public HexCoordinate ResolveStartCoord() => _startCoord;

        /// <summary>Bu karoya başlangıçtan YÜRÜYEREK gidilebilir mi? (dağ/göl arkasındaki
        /// cepler false döner). Düğüm yerleşimi bunu kullanır — erişilemeyen bir karoya konan
        /// zorunlu görev bölümü bitirilemez yapardı.</summary>
        public bool IsReachable(HexCoordinate c) => _reachable.Contains(c);

        /// <summary>Erişilebilir karo sayısı (HUD/log/teşhis).</summary>
        public int ReachableCount => _reachable.Count;

        /// <summary>Başlangıç karosunu ve ana bağlantılı bileşeni kaydeder. Başlangıcı ÜRETİCİ
        /// seçer (kıtanın ağırlık merkezine yakın, alçak, yürünür karo) — organik haritada sabit
        /// bir "ipucu koordinatı" denize düşerdi.</summary>
        private void BuildReachable(MapResult result)
        {
            _reachable.Clear();
            if (_terrain == null) { _startCoord = new HexCoordinate(0, 0); return; }

            var comp = TerrainGenerator.ConnectedComponent(_terrain, result.Start.q, result.Start.r,
                                                           out (int q, int r) start);
            _startCoord = HexCoordinate.FromOffset(start.q, start.r);   // dizi indisi → tahta koordinatı
            foreach (var t in comp) _reachable.Add(HexCoordinate.FromOffset(t.q, t.r));
        }
    }
}
