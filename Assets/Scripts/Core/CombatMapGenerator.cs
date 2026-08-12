using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Savaşa girerken ARENAYI ÜRETİR ve grid'e uygular — overworld haritasından tamamen ayrı
    /// (kullanıcı isteği 2026-08-12: "combat map main map'ten ayrıştırılacak").
    ///
    /// Eskiden savaş, overworld ile AYNI 22×25'lik tahtayı `CombatTileMap.asset` üzerinden
    /// kullanıyordu: 550 karo düz "kaya", sıfır engel, iki tarafın buluşması 7 tur. Artık düğüm
    /// tipine göre 4 kademede (encounter/zindan/zorunlu/boss) 65-120 karoluk, engelli, tırtıklı
    /// kenarlı bir taktik tahta üretiliyor.
    ///
    /// Sorumluluğu dar: seed seç → üret → runtime TileMap'e çevir → grid'e ver → deploy bölgesi ve
    /// düşman doğma noktalarını yayınla. Savaş kuralları (sıra, hasar, yetenek) buraya girmez.
    /// </summary>
    [DefaultExecutionOrder(-95)]   // HexGridManager(-100) sonrası, tüketicilerden önce
    public class CombatMapGenerator : MonoBehaviour
    {
        [SerializeField] private HexGridManager       _grid;
        [SerializeField] private CombatArenaConfigSO  _config;

        private const string LastSeedKey = "TacticalRPG.LastArenaSeed";

        private TileMapSO _runtimeMap;
        private ArenaResult _arena;

        /// <summary>Üretilmiş arenanın karo rolleri (siper/yükselti/tehlike). Görüş hattı ve
        /// hareket maliyeti bunu okuyacak.</summary>
        public ArenaResult Arena => _arena;

        /// <summary>Düşmanların doğacağı karolar (tahta koordinatında). Görevdeki sabit koordinatlar
        /// prosedürel arenada anlamsız — <c>EnemySpawner</c> bunu kullanır.</summary>
        public IReadOnlyList<HexCoordinate> EnemySpawns => _enemySpawns;
        private readonly List<HexCoordinate> _enemySpawns = new();

        /// <summary>Bu koşumda kullanılan arena seed'i (log/HUD).</summary>
        public int CurrentSeed { get; private set; } = -1;

        /// <summary>Arena üretildi.</summary>
        public event System.Action OnArenaGenerated;

        /// <summary>Düğüm tipinin arena kademesi. Boss ve zorunlu görev daha büyük tahta ister.</summary>
        public static ArenaTier TierFor(MapNodeType type) => type switch
        {
            MapNodeType.Encounter => ArenaTier.Encounter,
            MapNodeType.Zindan    => ArenaTier.Dungeon,
            MapNodeType.Mandatory => ArenaTier.Mandatory,
            MapNodeType.Boss      => ArenaTier.Boss,
            _                     => ArenaTier.Dungeon,
        };

        /// <summary>Arenayı üretip grid'e uygular. Savaşa giriş akışı çağırır.</summary>
        public bool Build(ArenaTier tier)
        {
            if (_grid == null)
            {
                Debug.LogError("[Arena] CombatMapGenerator: grid atanmamis.");
                return false;
            }

            ArenaParams pars = _config != null ? _config.ParamsFor(tier) : ArenaParams.ForTier(tier);
            CurrentSeed = PickSeed();
            _arena = CombatArenaGenerator.Generate(pars, CurrentSeed);

            _runtimeMap = ScriptableObject.CreateInstance<TileMapSO>();
            _runtimeMap.name          = $"Arena_{tier}_{CurrentSeed}";
            _runtimeMap.defaultTileId = TileCatalog.Void;
            // Tahta boyutunu HARİTA taşır — overworld 36×34 iken arena 11×9 kalabilsin diye.
            _runtimeMap.SetGridSize(pars.Width, pars.Height);

            for (int col = 0; col < pars.Width; col++)
                for (int row = 0; row < pars.Height; row++)
                    _runtimeMap.SetTileId(HexCoordinate.FromOffset(col, row), _arena.Tiles[col, row]);

            _enemySpawns.Clear();
            foreach (var s in _arena.EnemySpawns)
                _enemySpawns.Add(HexCoordinate.FromOffset(s.q, s.r));

            _grid.SetTileMap(_runtimeMap);
            PlayerPrefs.SetInt(LastSeedKey, CurrentSeed);

            Debug.Log($"[Arena] {tier} uretildi — seed {CurrentSeed} | {pars.Width}x{pars.Height} kutu, " +
                      $"{_arena.Playable} oynanabilir karo | engel %{_arena.BlockedPct:F1} " +
                      $"(duvar {_arena.Walls}, siper {_arena.Covers}) | etkilesim %{_arena.InteractivePct:F1} " +
                      $"(yukselti {_arena.Highs}, tehlike {_arena.Hazards}, zor {_arena.Difficults}) | " +
                      $"deploy {_arena.DeployZone.Count} karo | dusman {_enemySpawns.Count}");

            OnArenaGenerated?.Invoke();
            return true;
        }

        /// <summary>Havuzdan seed seç: mümkünse son savaştakinden farklı (arka arkaya aynı arena olmasın).</summary>
        private int PickSeed()
        {
            var pool = _config != null ? _config.SeedPool : null;
            int last = PlayerPrefs.GetInt(LastSeedKey, -1);

            if (pool == null || pool.Count == 0)
                return Random.Range(1, int.MaxValue);        // havuz yoksa serbest üretim

            if (pool.Count == 1) return pool[0];

            int pick = pool[Random.Range(0, pool.Count)];
            for (int guard = 0; pick == last && guard < 16; guard++)
                pick = pool[Random.Range(0, pool.Count)];
            return pick;
        }

        /// <summary>Bu karonun taktik rolü (siper/yükselti/tehlike). Arena yoksa <c>Floor</c>.</summary>
        public CombatRole RoleAt(HexCoordinate coord)
        {
            if (_arena == null) return CombatRole.Floor;
            coord.ToOffset(out int col, out int row);
            if (col < 0 || row < 0 || col >= _arena.Roles.GetLength(0) || row >= _arena.Roles.GetLength(1))
                return CombatRole.Floor;
            return _arena.Roles[col, row];
        }
    }
}
