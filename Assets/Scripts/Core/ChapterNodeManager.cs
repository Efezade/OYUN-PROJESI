using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Bölüm haritasındaki DÜĞÜMLER: 3 zorunlu görev · 6 zindan · 8 encounter · gündüz marketi ·
    /// gözetleme kulesi · 1 ana boss (TASK-006 · GAME_DESIGN.md §3).
    /// Referans yerleşim: `Docs/Balance/tools/harita_map1_sim.py` → `build_nodes`.
    ///
    /// İLKELER (görev metninden):
    ///   • Zorunlu 3 görev SİS'TEN BAĞIMSIZ hep görünür — üçü bitmeden bölüm bitmez.
    ///   • Zindan/encounter: **zorluk girmeden ÖNCE görünür, ödül GİRENE KADAR gizli**
    ///     ("riski bil, ödülü bilme"). Ödül yüksek varyanslı.
    ///   • Market yalnız GÜNDÜZ dilimlerinde açık.
    ///   • Kule: çevresini KALICI açar (sis zaten geri kapanmıyor; bu sadece erken açma).
    ///   • Boss KONUMDAN BAĞIMSIZ — haritanın her yerinden girilir, rota ile ilgisi yok.
    ///   • Bu bölümde portal ve gece mistik marketi YOK.
    ///
    /// NOT (Python referansından SAPMA): `build_nodes`'un "ova havuzu" sırası Python'un KÜME
    /// sıralamasından geliyordu — dile bağlı, taşınabilir değil. Burada havuz (q, r) sırasına göre
    /// kurulup öyle karılıyor: aynı seed'de HER ZAMAN aynı yerleşim, ama Python sim'iyle birebir
    /// aynı koordinatlar değil. Sayılar ve değer/maliyet aralıkları aynı (denge etkilenmez).
    /// </summary>
    [DefaultExecutionOrder(-80)]   // ChapterMapGenerator(-90) haritayı kurduktan SONRA
    public class ChapterNodeManager : MonoBehaviour
    {
        /// <summary>Haritadaki tek bir düğüm. Ödül alanı GİZLİ tutulur (bkz <see cref="RewardKnown"/>).</summary>
        public class MapNode
        {
            public HexCoordinate Coord;
            public MapNodeType   Type;
            public int           Value;        // ödül (öz) — gizli olabilir
            public int           APCost;
            public bool          Completed;
            public bool          RewardKnown;  // girildikten SONRA true olur
        }

        [SerializeField] private HexGridManager       _grid;
        [SerializeField] private ChapterMapGenerator  _map;
        [SerializeField] private NodeConfigSO         _config;
        [SerializeField] private ActionPointManager   _ap;
        [SerializeField] private EssenceWallet        _wallet;
        [SerializeField] private PlayerController     _player;
        [SerializeField] private FogOfWarManager      _fog;
        [SerializeField] private GameStateManager     _state;
        [SerializeField] private MissionManager       _missions;
        [Tooltip("Market düğümü BU dükkânı açar (kullanıcı kararı 2026-07-28: market düğümü ile " +
                 "boyalı 'magaza' karosu aynı dükkândır).")]
        [SerializeField] private StoreManager         _store;
        [Tooltip("Zorunlu görev karolarının çöküşten muaf tutulması için (TASK-007).")]
        [SerializeField] private MapCollapseManager   _collapse;
        [Tooltip("Kule sisi açarken oynayan epik ışık efekti (eski oyundaki açılış animasyonu).")]
        [SerializeField] private TowerRevealEffect    _towerFx;

        [Header("İşaretler (whitebox — gerçek görsel gelince prefab atanır)")]
        [Tooltip("Zorunlu görev işareti bu yüksekliğe konur — sis bulutunun ÜSTÜNDE kalsın diye " +
                 "(sisten bağımsız görünürlük).")]
        [SerializeField] private float _mandatoryMarkerHeight = 1.6f;
        [SerializeField] private float _markerHeight          = 0.9f;
        [SerializeField] private float _markerScale           = 0.35f;

        private readonly List<MapNode> _nodes = new();
        private Transform _markersRoot;
        private readonly Dictionary<MapNode, GameObject> _markers = new();

        /// <summary>Konumdan bağımsız ana boss (haritaya yerleştirilmez).</summary>
        public MapNode Boss { get; private set; }

        /// <summary>Düğüm durumu değişti (tamamlandı / harita yenilendi) — UI dinler.</summary>
        public event System.Action OnNodesChanged;

        public IReadOnlyList<MapNode> Nodes => _nodes;

        /// <summary>Zorunlu görevlerin kaçı bitti / toplam.</summary>
        public void MandatoryProgress(out int done, out int total)
        {
            done = 0; total = 0;
            foreach (var n in _nodes)
                if (n.Type == MapNodeType.Mandatory) { total++; if (n.Completed) done++; }
        }

        /// <summary>Bölüm bitirilebilir mi? (3 zorunlu görev tamam + boss yenildi)</summary>
        public bool ChapterClearable()
        {
            MandatoryProgress(out int done, out int total);
            return done >= total && Boss != null && Boss.Completed;
        }

        private void OnEnable()
        {
            if (_map    != null) _map.OnMapGenerated   += Rebuild;
            if (_state  != null) _state.OnStateChanged += HandleStateChanged;
            if (_player != null) _player.OnMoved       += RefreshMarkerVisibility;
            if (_ap     != null) _ap.OnTimeAdvanced    += HandleTimeAdvanced;
        }

        private void OnDisable()
        {
            if (_map    != null) _map.OnMapGenerated   -= Rebuild;
            if (_state  != null) _state.OnStateChanged -= HandleStateChanged;
            if (_player != null) _player.OnMoved       -= RefreshMarkerVisibility;
            if (_ap     != null) _ap.OnTimeAdvanced    -= HandleTimeAdvanced;
        }

        // ── Yerleşim ─────────────────────────────────────────────────────────

        /// <summary>Harita üretilince düğümleri yeniden yerleştirir.</summary>
        public void Rebuild()
        {
            _nodes.Clear();
            Boss = null;
            ClearMarkers();
            if (_fog != null) _fog.ClearPermanentReveals();

            if (_map == null || _config == null || _grid == null) return;

            // Yerleşim yalnız "ova" karolarına (GAME_DESIGN §3) ve oyuncunun başladığı karo hariç.
            // Havuz TAHTANIN gerçek koordinatlarında kurulur (HexCoordinate.FromOffset). 2026-08-05'e
            // kadar ham dizi indisleri kullanılıyordu → alt satırlardaki düğümler var olmayan
            // hücrelere düşüp SESSİZCE kayboluyordu (zorunlu görev/market dahil).
            HexCoordinate start = _player != null ? _player.CurrentCoord : new HexCoordinate(0, 0);
            var pool = new List<HexCoordinate>();
            for (int col = 0; col < _grid.Width; col++)
                for (int row = 0; row < _grid.Height; row++)
                {
                    var c = HexCoordinate.FromOffset(col, row);
                    if (c.Equals(start)) continue;
                    // SADECE erişilebilir bölge (referans: build_nodes → `walkable_comp`).
                    // Aksi halde bir zorunlu görev dağ ardındaki cebe düşüp bölümü bitirilemez yapardı.
                    if (!_map.IsReachable(c)) continue;
                    if (_map.TerrainIdAt(c) == TerrainGenerator.OvaId) pool.Add(c);
                }

            // Aynı seed → aynı yerleşim. (+1000: Python referansıyla aynı ofset.)
            var rnd = new PythonRandom(_map.CurrentSeed + 1000);
            rnd.Shuffle(pool);

            int idx = 0;
            Take(pool, ref idx, _config.MandatoryCount, MapNodeType.Mandatory,
                 () => _config.MandatoryValue, () => _config.MandatoryAP, rnd);
            Take(pool, ref idx, _config.ZindanCount, MapNodeType.Zindan,
                 () => rnd.RandInt(_config.ZindanValue.x, _config.ZindanValue.y),
                 () => rnd.RandInt(_config.ZindanAP.x,    _config.ZindanAP.y), rnd);
            Take(pool, ref idx, _config.EncounterCount, MapNodeType.Encounter,
                 () => rnd.RandInt(_config.EncounterValue.x, _config.EncounterValue.y),
                 () => rnd.RandInt(_config.EncounterAP.x,    _config.EncounterAP.y), rnd);
            Take(pool, ref idx, _config.MarketCount, MapNodeType.Market,
                 () => 0, () => 0, rnd);
            Take(pool, ref idx, _config.WatchtowerCount, MapNodeType.Watchtower,
                 () => 0, () => _config.WatchtowerAP, rnd);

            // Her düğümün karosunu KENDİ karo tipine çevir → palette'teki model render edilir
            // (mağara / kamp / görev alanı / ticaret hanı / kule) ve savaşlı olanlar canEnterCombat
            // taşıdığı için eski oyundaki "yaklaş → Savaşa Gir" akışı çalışır. Ayrı işaret küresine
            // gerek kalmaz. Yürünemez olanlar (kule, mağaza) KOMŞU karodan kullanılır (NodeForPlayer).
            foreach (var n in _nodes)
            {
                string tileId = TileIdOf(n.Type);
                if (!string.IsNullOrEmpty(tileId)) _map.SetTile(n.Coord, tileId);
            }

            // Boss: KONUMDAN BAĞIMSIZ — havuzdan karo almaz, haritada işareti yoktur.
            Boss = new MapNode { Type = MapNodeType.Boss, APCost = _config.BossAP, Value = 0 };

            BuildMarkers();
            PublishMarketNodes();
            PublishProtectedTiles();
            OnNodesChanged?.Invoke();
            Debug.Log($"[Node] Yerlesim tamam — zorunlu {_config.MandatoryCount}, zindan {_config.ZindanCount}, " +
                      $"encounter {_config.EncounterCount}, market {_config.MarketCount}, " +
                      $"kule {_config.WatchtowerCount}, boss 1 (konumsuz).");
        }

        private void Take(List<HexCoordinate> pool, ref int idx, int count, MapNodeType type,
                          System.Func<int> value, System.Func<int> apCost, PythonRandom rnd)
        {
            for (int i = 0; i < count && idx < pool.Count; i++, idx++)
                _nodes.Add(new MapNode
                {
                    Coord  = pool[idx],
                    Type   = type,
                    Value  = value(),
                    APCost = apCost(),
                    // Zorunlu görevin ödülü zaten biliniyor; zindan/encounter'ınki GİZLİ.
                    RewardKnown = type != MapNodeType.Zindan && type != MapNodeType.Encounter
                });
        }

        // ── Sorgular ─────────────────────────────────────────────────────────

        // ── Düğüm KARO id'leri (palette'teki modellere bağlı) ───────────────
        // Düğümler artık haritada GERÇEK KARO olarak duruyor: kendi modeliyle görünür ve savaşlı
        // olanlar `canEnterCombat` sayesinde eski oyundaki gibi "yaklaş → Savaşa Gir" akışını açar
        // (MissionManager.GetEnterableMission bu bayrağa bakıyor).
        public const string WatchtowerTileId = "kule";          // gözetleme kulesi
        public const string DungeonTileId    = "magara";        // zindan girişi
        public const string EncounterTileId  = "kamp";          // karşılaşma (baskın kampı)
        public const string MandatoryTileId  = "gorev_alani";   // zorunlu harita-kurtarma görevi
        public const string MarketTileId     = "magaza";        // ticaret hanı (StoreManager açar)

        /// <summary>Düğüm tipinin haritadaki karo id'si (yoksa null → karo boyanmaz).</summary>
        public static string TileIdOf(MapNodeType t) => t switch
        {
            MapNodeType.Watchtower => WatchtowerTileId,
            MapNodeType.Zindan     => DungeonTileId,
            MapNodeType.Encounter  => EncounterTileId,
            MapNodeType.Mandatory  => MandatoryTileId,
            MapNodeType.Market     => MarketTileId,
            _                      => null   // boss konumsuz
        };

        public MapNode NodeAt(HexCoordinate c)
        {
            foreach (var n in _nodes) if (n.Coord.Equals(c)) return n;
            return null;
        }

        /// <summary>Oyuncunun ŞU AN kullanabileceği düğüm: bastığı karodaki düğüm; yoksa 1 karo
        /// yanındaki YÜRÜNEMEZ yapı (kule / ticaret hanı). Bunların karosuna basılamaz — üstlerinde
        /// bir yapı var — bu yüzden komşu karodan kullanılırlar (eski WatchtowerManager deseni).</summary>
        public MapNode NodeForPlayer(HexCoordinate c)
        {
            MapNode here = NodeAt(c);
            if (here != null) return here;

            foreach (var n in _nodes)
            {
                bool adjacentUsable = n.Type == MapNodeType.Watchtower || n.Type == MapNodeType.Market;
                if (adjacentUsable && !n.Completed && c.DistanceTo(n.Coord) <= 1) return n;
            }
            return null;
        }

        /// <summary>Zorunlu görev işareti sis'ten BAĞIMSIZ görünür; diğerleri karo bilinene kadar gizli.</summary>
        public bool IsMarkerVisible(MapNode n)
            => n.Type == MapNodeType.Mandatory || _fog == null || _fog.IsKnown(n.Coord);

        /// <summary>Girmeden ÖNCE gösterilen zorluk — AP maliyetinden türetilir (ödül DEĞİL).</summary>
        public string DifficultyLabel(MapNode n)
        {
            if (n.Type != MapNodeType.Zindan && n.Type != MapNodeType.Encounter && n.Type != MapNodeType.Boss)
                return "";
            int lv = Mathf.Clamp(n.APCost, 1, 6);
            return new string('★', lv) + new string('·', 6 - lv);
        }

        /// <summary>Ödül metni — GİZLİYSE açıkça "?" döner (oyuncu bilmemeli).</summary>
        public string RewardLabel(MapNode n) => n.RewardKnown ? $"{n.Value} öz" : "? (girmeden bilinmez)";

        /// <summary>Market yalnız GÜNDÜZ dilimlerinde açık (gece mistik marketi bu bölümde YOK).</summary>
        public bool IsMarketOpen() => _ap == null || !_ap.IsNight;

        /// <summary>Düğümün ŞU ANKİ AP maliyeti. Gün <see cref="NodeConfigSO.LateCostFromDay"/>'den
        /// itibaren zindan/encounter maliyeti çarpanla artar (TASK-007: zaman baskısı).</summary>
        public int EffectiveAPCost(MapNode n)
        {
            if (n == null) return 0;
            bool risky = n.Type == MapNodeType.Zindan || n.Type == MapNodeType.Encounter;
            if (!risky || _config == null || _ap == null) return n.APCost;
            return _ap.CurrentDay >= _config.LateCostFromDay
                ? n.APCost * _config.LateCostMultiplier
                : n.APCost;
        }

        /// <summary>Maliyet artışı yürürlükte mi? (HUD "pahalılaştı" uyarısı için)</summary>
        public bool LateCostActive => _config != null && _ap != null && _ap.CurrentDay >= _config.LateCostFromDay;

        public bool CanEnter(MapNode n)
        {
            if (n == null || n.Completed) return false;
            if (n.Type == MapNodeType.Market) return IsMarketOpen();
            return _ap == null || _ap.CurrentAP >= EffectiveAPCost(n);
        }

        // ── Eylemler ─────────────────────────────────────────────────────────

        private MapNode _pendingCombatNode;   // savaşa girildi, dönüşte ödül verilecek

        /// <summary>Düğüme gir: AP harca; savaşlı türlerde savaşa yönlendir, diğerlerini anında çöz.</summary>
        public bool Enter(MapNode n)
        {
            if (!CanEnter(n)) return false;
            int cost = EffectiveAPCost(n);
            if (_ap != null && cost > 0) _ap.SpendAP(cost);

            switch (n.Type)
            {
                case MapNodeType.Watchtower:
                    if (_fog != null) _fog.RevealAreaPermanent(n.Coord, _config.WatchtowerRadius);
                    // Eski oyundaki EPİK AÇILIŞ efekti (ışık hüzmesi + genişleyen halka + patlama).
                    if (_towerFx != null && _grid != null && _grid.TryGetCell(n.Coord, out HexCell tCell))
                        _towerFx.Play(tCell.WorldPosition + Vector3.up * tCell.SurfaceHeight);
                    Complete(n);
                    return true;

                case MapNodeType.Market:
                    // Dükkân zaten StoreManager/StoreHUD'da; market düğümü onu açar (karo ile AYNI
                    // dükkân). Düğüm TÜKENMEZ — markete tekrar tekrar girilebilir.
                    PublishMarketNodes();
                    OnNodesChanged?.Invoke();
                    return true;

                case MapNodeType.Mandatory:
                case MapNodeType.Zindan:
                case MapNodeType.Encounter:
                case MapNodeType.Boss:
                    // Savaşa yönlendir; ödül DÖNÜŞTE verilir (girmeden bilinmesin).
                    MissionData mission = _missions != null ? _missions.GetEnterableMission(n.Coord) : null;
                    if (mission != null && _state != null)
                    {
                        _pendingCombatNode = n;
                        _state.RequestMission(mission);
                    }
                    else
                    {
                        // Savaş içeriği bağlı değilse düğüm yine de çözülür (akış tıkanmasın).
                        Complete(n);
                    }
                    return true;
            }
            return false;
        }

        /// <summary>Market düğümlerinin karolarını dükkâna bildir + gündüz/gece durumunu geçir.</summary>
        private void PublishMarketNodes()
        {
            if (_store == null) return;
            var coords = new List<HexCoordinate>();
            foreach (var n in _nodes)
                if (n.Type == MapNodeType.Market) coords.Add(n.Coord);
            _store.SetNodeStores(coords, IsMarketOpen());
        }

        /// <summary>ZORUNLU görev karolarını çöküşten muaf tut (TASK-007) — silinirlerse bölüm
        /// bitirilemez hale gelirdi.</summary>
        private void PublishProtectedTiles()
        {
            if (_collapse == null) return;
            var coords = new List<HexCoordinate>();
            foreach (var n in _nodes)
                if (n.Type == MapNodeType.Mandatory) coords.Add(n.Coord);
            _collapse.SetProtectedTiles(coords);
        }

        /// <summary>Zaman ilerleyince (gündüz↔gece) market düğümlerinin açıklığı güncellenir.</summary>
        private void HandleTimeAdvanced(int day, int slot, string slotName)
        {
            if (_store != null) _store.SetNodeStoresOpen(IsMarketOpen());
            OnNodesChanged?.Invoke();
        }

        private void HandleStateChanged(GameState s)
        {
            // Savaşa KARO ÜZERİNDEN girildiyse (eski akış: yaklaş → "Savaşa Gir" istemi) düğüm
            // sistemi devrede değildi. İki yolu birleştir: o an oyuncunun üstünde/yanında savaşlı
            // bir düğüm varsa onu bekleyen say → dönüşte tamamlanır ve ÖDÜLÜ verilir.
            if ((s == GameState.ConfirmMission || s == GameState.Combat) && _pendingCombatNode == null && _player != null)
            {
                MapNode n = NodeForPlayer(_player.CurrentCoord);
                if (n != null && !n.Completed && IsCombatNode(n.Type)) _pendingCombatNode = n;
            }

            // Savaştan overworld'e dönüldü → bekleyen düğümü tamamla + ödülü AÇIKLA.
            if (s == GameState.Overworld && _pendingCombatNode != null)
            {
                Complete(_pendingCombatNode);
                _pendingCombatNode = null;
            }
        }

        private static bool IsCombatNode(MapNodeType t)
            => t == MapNodeType.Zindan || t == MapNodeType.Encounter
            || t == MapNodeType.Mandatory || t == MapNodeType.Boss;

        private void Complete(MapNode n)
        {
            n.Completed   = true;
            n.RewardKnown = true;                       // ödül ARTIK görünür
            if (n.Value > 0 && _wallet != null)
                _wallet.Gain(EssenceType.Doga, n.Value); // TODO(Sherlock): ödül öz TÜRÜ kararlaştırılmadı

            var marker = MarkerOf(n);
            if (marker != null) marker.SetActive(false);
            OnNodesChanged?.Invoke();
        }

        // ── İşaretler (whitebox küreler) ─────────────────────────────────────

        private static readonly Dictionary<MapNodeType, Color> MarkerColors = new()
        {
            { MapNodeType.Mandatory,  new Color(1.00f, 0.85f, 0.20f) }, // altın
            { MapNodeType.Zindan,     new Color(0.65f, 0.25f, 0.75f) }, // mor
            { MapNodeType.Encounter,  new Color(0.85f, 0.35f, 0.25f) }, // kızıl
            { MapNodeType.Market,     new Color(0.25f, 0.70f, 0.85f) }, // camgöbeği
            { MapNodeType.Watchtower, new Color(0.95f, 0.95f, 0.95f) }, // beyaz
        };

        private GameObject MarkerOf(MapNode n) => _markers.TryGetValue(n, out var go) ? go : null;

        private void ClearMarkers()
        {
            _markers.Clear();
            // Editörde (Play'e basılmadan önizleme üretilirken) Destroy çalışmaz → DestroyImmediate.
            if (_markersRoot != null)
            {
                if (Application.isPlaying) Destroy(_markersRoot.gameObject);
                else                       DestroyImmediate(_markersRoot.gameObject);
            }
            _markersRoot = null;
        }

        private void BuildMarkers()
        {
            _markersRoot = new GameObject("NodeMarkers").transform;
            _markersRoot.SetParent(transform, false);

            // Artık HER düğümün kendi karo modeli var (mağara/kamp/görev alanı/han/kule), bu yüzden
            // işaret küresi YALNIZ ZORUNLU GÖREVLERDE kalıyor: onlar sis'ten BAĞIMSIZ görünmek
            // zorunda (TASK-006), karo modeli ise sisin altında kalır. Diğerleri normal keşifle bulunur.
            foreach (var n in _nodes)
            {
                if (n.Type != MapNodeType.Mandatory) continue;
                if (!_grid.TryGetCell(n.Coord, out HexCell cell)) continue;

                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = $"Node_{n.Type}_{n.Coord.Q}_{n.Coord.R}";
                go.transform.SetParent(_markersRoot, false);
                Collider col = go.GetComponent<Collider>();          // tıklamayı engellemesin
                if (col != null) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }

                float y = n.Type == MapNodeType.Mandatory ? _mandatoryMarkerHeight : _markerHeight;
                go.transform.position   = cell.WorldPosition + Vector3.up * y;
                go.transform.localScale = Vector3.one * _markerScale;

                var rend = go.GetComponent<Renderer>();
                var mpb  = new MaterialPropertyBlock();
                mpb.SetColor("_BaseColor", MarkerColors.TryGetValue(n.Type, out Color c) ? c : Color.white);
                rend.SetPropertyBlock(mpb);

                _markers[n] = go;
                go.SetActive(IsMarkerVisible(n));
            }
        }

        /// <summary>Sis açıldıkça işaretler görünür olur (zorunlu olanlar zaten hep açık).
        /// Update'te DEĞİL, yalnız oyuncu hareket edince çalışır (CLAUDE.md §6).</summary>
        private void RefreshMarkerVisibility(HexCoordinate _)
        {
            foreach (var kv in _markers)
                if (!kv.Key.Completed && kv.Value != null)
                {
                    bool vis = IsMarkerVisible(kv.Key);
                    if (kv.Value.activeSelf != vis) kv.Value.SetActive(vis);
                }
        }
    }
}
