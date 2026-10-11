using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    [DefaultExecutionOrder(-65)]
    public sealed class BayterekQuestRoots : MonoBehaviour
    {
        [SerializeField] private HexGridManager _grid;
        [SerializeField] private ChapterMapGenerator _map;
        [SerializeField] private ChapterNodeManager _nodes;
        [SerializeField] private PlayerController _player;
        [SerializeField] private GameStateManager _state;
        [SerializeField] private FogOfWarManager _fog;
        [SerializeField] private MapCollapseManager _collapse;
        [SerializeField] private QuestRootConfigSO _config;
        [SerializeField] private BiomeTerrainStyling _styling;

        private sealed class Piece
        {
            public HexCoordinate Coord;
            public QuestRootSegment Visual, SideVisual;
            public HexCoordinate? Side;
            public float Amount;
            public bool Blocked;
            public Vector3 Direction, SideDirection;
        }
        private sealed class Scar { public RootRuptureTile Visual; public float Amount; }
        private readonly Dictionary<HexCoordinate, Scar> _scars = new();
        private readonly Stack<RootRuptureTile> _scarPool = new();
        private readonly List<HexCoordinate> _releasedScars = new();
        public int RupturedTileCount => _scars.Count;
        private sealed class Branch
        {
            public ChapterNodeManager.MapNode Quest;
            public readonly List<Piece> Pieces = new();
            public int Cursor;
            public bool Retracting, Paused;
        }
        private readonly List<Branch> _branches = new();
        private readonly Stack<QuestRootSegment> _pool = new();
        private readonly HashSet<ChapterNodeManager.MapNode> _warned = new();
        private Transform _visualRoot;
        private TileMapSO _overworld;
        private bool _dirty;
        private bool OnWorld => _grid != null && _grid.TileMap == _overworld;
        private bool CanAnimate => OnWorld && (_state == null || _state.State == GameState.Overworld);
        public event System.Action<HexCoordinate> OnBranchCreated;
        public bool TryGetTree(out HexCoordinate coord) => FindTree(out coord);
        public void PauseGrowth(HexCoordinate goal,bool pause) { foreach(var b in _branches) if(b.Quest.Coord==goal)b.Paused=pause; }
        public bool Growing(HexCoordinate goal) { foreach(var b in _branches)if(b.Quest.Coord==goal)return !b.Retracting && b.Cursor<b.Pieces.Count;return false; }
        public float GrowthSecondsRemaining(HexCoordinate goal) { foreach(var b in _branches)if(b.Quest.Coord==goal)return Mathf.Max(0,b.Pieces.Count-b.Cursor)*_config.GrowSeconds;return 0; }
        public Vector3 GrowthTip(HexCoordinate goal) { foreach(var b in _branches)if(b.Quest.Coord==goal && b.Pieces.Count>0)return b.Pieces[Mathf.Min(b.Cursor,b.Pieces.Count-1)].Visual.Tip;return Surface(goal); }
        public IEnumerable<HexCoordinate> BranchCells(HexCoordinate goal) { foreach(var b in _branches)if(b.Quest.Coord==goal)foreach(var p in b.Pieces){yield return p.Coord;if(p.Side.HasValue)yield return p.Side.Value;} }
        public int BranchCount => _branches.Count;
        public int BlockedTileCount
        {
            get
            {
                if (!OnWorld || !_grid.HasCells) return 0;
                int count = 0;
                foreach (var cell in _grid.Cells.Values) if (cell.HasMovementBlock) count++;
                return count;
            }
        }
        public QuestRootConfigSO Config => _config;

        private void Awake()
        {
            _visualRoot = new GameObject("Bayterek_Gorev_Kokleri").transform;
            _visualRoot.SetParent(transform, false);
        }
        private void OnEnable()
        {
            if (_map != null) _map.OnMapGenerated += ResetForMap;
            if (_nodes != null) _nodes.OnNodesChanged += MarkDirty;
            if (_grid != null) _grid.OnGridRegenerated += RestoreBlocks;
            if (_player != null) _player.OnMoved += PlayerMoved;
            if (_collapse != null) _collapse.OnTileCollapsed += Collapsed;
            _dirty = true;
        }
        private void OnDisable()
        {
            if (_map != null) _map.OnMapGenerated -= ResetForMap;
            if (_nodes != null) _nodes.OnNodesChanged -= MarkDirty;
            if (_grid != null) _grid.OnGridRegenerated -= RestoreBlocks;
            if (_player != null) _player.OnMoved -= PlayerMoved;
            if (_collapse != null) _collapse.OnTileCollapsed -= Collapsed;
            ClearBranches();
        }
        private void MarkDirty() => _dirty = true;
        private void PlayerMoved(HexCoordinate coord) => _dirty = true;
        private void Collapsed(int count, int total) => _dirty = true;
        private void ResetForMap()
        {
            ClearBranches(); _overworld = _grid != null ? _grid.TileMap : null;
            _warned.Clear(); _dirty = true;
        }
        private void RestoreBlocks()
        {
            // Called before the returning player is initialized: blockers must already exist.
            if (!OnWorld) return;
            foreach (var branch in _branches)
                foreach (var piece in branch.Pieces)
                    if (piece.Blocked) SetBlock(branch,piece,true);
            _grid.NotifyWalkabilityChanged(); _dirty = true;
        }
        private void ClearBranches()
        {
            foreach (var branch in _branches)
                foreach (var piece in branch.Pieces)
                {
                    SetBlock(branch,piece,false);
                    Return(piece.Visual); Return(piece.SideVisual);
                }
            _branches.Clear();
            foreach (var scar in _scars.Values) ReturnScar(scar.Visual);
            _scars.Clear();
            if (_grid != null) _grid.NotifyWalkabilityChanged();
        }
        private void LateUpdate()
        {
            if (_config == null || _config.BarkMaterial == null || _grid == null || _nodes == null || _player == null) return;
            if (_overworld == null && _map != null && _map.HasRegions) _overworld = _grid.TileMap;
            bool shown = OnWorld && (_state == null || _state.State == GameState.Overworld || _state.State == GameState.ConfirmMission);
            if (_visualRoot != null && _visualRoot.gameObject.activeSelf != shown) _visualRoot.gameObject.SetActive(shown);
            if (!shown) return;
            if (_dirty && CanAnimate) { _dirty = false; Synchronize(); }
            bool changed = false;
            foreach (var scar in _scars.Values) scar.Amount = 0;
            for (int n = _branches.Count-1; n >= 0; n--)
            {
                var branch = _branches[n];
                if (CanAnimate) changed |= Advance(branch, Time.deltaTime);
                foreach (var piece in branch.Pieces)
                {
                    bool visible = (_fog == null || _fog.VolumeRendering || _fog.IsVisuallyRevealed(piece.Coord)) &&
                        (_collapse == null || !_collapse.IsCollapsed(piece.Coord));
                    if (piece.Visual != null) piece.Visual.Show(piece.Amount, visible);
                    if(piece.SideVisual!=null)piece.SideVisual.Show(piece.Amount,piece.Side.HasValue && Revealed(piece.Side.Value));
                    AccumulateScar(piece.Coord, piece.Amount, piece.Direction);
                    if (piece.Side.HasValue) AccumulateScar(piece.Side.Value, piece.Amount, piece.SideDirection);
                }
                if (branch.Retracting && branch.Cursor < 0)
                {
                    foreach (var piece in branch.Pieces) { Return(piece.Visual);Return(piece.SideVisual); }
                    _branches.RemoveAt(n); _dirty = true;
                }
            }
            _releasedScars.Clear();
            foreach (var kv in _scars)
            {
                if (_grid.TryGetCell(kv.Key, out var cell)) kv.Value.Visual.Bind(cell);
                kv.Value.Visual.Show(kv.Value.Amount, Revealed(kv.Key), CanAnimate ? Time.deltaTime : 0);
                if (kv.Value.Amount <= 0) { ReturnScar(kv.Value.Visual); _releasedScars.Add(kv.Key); }
            }
            foreach (var coord in _releasedScars) _scars.Remove(coord);
            if (changed) _grid.NotifyWalkabilityChanged();
        }
        private bool Revealed(HexCoordinate coord) => (_fog == null || _fog.VolumeRendering || _fog.IsVisuallyRevealed(coord)) &&
            (_collapse == null || !_collapse.IsCollapsed(coord));
        private void AccumulateScar(HexCoordinate coord, float amount, Vector3 direction)
        {
            if (amount <= 0 || _config.EarthMaterial == null || _map.TerrainIdAt(coord) == TileCatalog.Bayterek) return;
            if (!_scars.TryGetValue(coord, out var scar))
            {
                if (!_grid.TryGetCell(coord, out var cell)) return;
                RootRuptureTile visual;
                if (_scarPool.Count > 0) visual = _scarPool.Pop();
                else { var go = new GameObject("Kokun_Yardigi_Karo"); go.transform.SetParent(_visualRoot, false); visual = go.AddComponent<RootRuptureTile>(); }
                visual.gameObject.SetActive(true);
                Color ground = _styling != null && _styling.Style != null ? _styling.Style.Ground(_map.RegionAt(coord)?.Id) : _config.EarthTint;
                visual.Configure(cell, direction, _grid.HexSize, _config, ground);
                scar = new Scar { Visual = visual }; _scars.Add(coord, scar);
            }
            scar.Amount = Mathf.Max(scar.Amount, amount);
        }
        private void ReturnScar(RootRuptureTile scar)
        { if (scar != null) { scar.Release(); scar.gameObject.SetActive(false); _scarPool.Push(scar); } }
        private bool Advance(Branch branch, float delta)
        {
            if (branch.Retracting)
            {
                if (branch.Cursor < 0) return false;
                var piece = branch.Pieces[branch.Cursor];
                piece.Amount = Mathf.Max(0, piece.Amount - delta/_config.RetractSeconds);
                if (piece.Amount > 0) return false;
                SetBlock(branch, piece, false); SoundSignals.At("root_retract",Surface(piece.Coord),.45f); branch.Cursor--; return true;
            }
            if (branch.Paused || branch.Cursor >= branch.Pieces.Count) return false;
            var next = branch.Pieces[branch.Cursor];
            // Never grow into the current step, even if the AP event unlocked a quest mid-walk.
            if (_player.CurrentCoord == next.Coord || _player.StepTarget == next.Coord || (next.Side.HasValue && (_player.CurrentCoord==next.Side.Value || _player.StepTarget==next.Side.Value))) return false;
            bool changed = !next.Blocked;
            if (changed)
            {
                SetBlock(branch, next, true); SoundSignals.At("root_grow",Surface(next.Coord),.6f);
                if (_scars.TryGetValue(next.Coord, out var scar)) scar.Visual.Agitate(Revealed(next.Coord));
                if (next.Side.HasValue && _scars.TryGetValue(next.Side.Value, out var sideScar)) sideScar.Visual.Agitate(Revealed(next.Side.Value));
            }
            next.Amount = Mathf.Min(1, next.Amount + delta/_config.GrowSeconds);
            if (next.Amount >= 1) branch.Cursor++;
            return changed;
        }
        private void SetBlock(Branch owner, Piece piece, bool blocked)
        {
            piece.Blocked = blocked;
            if (OnWorld && _grid.TryGetCell(piece.Coord, out var cell)) cell.SetMovementBlock(owner, blocked);
            if(OnWorld && piece.Side.HasValue && _grid.TryGetCell(piece.Side.Value,out var side))side.SetMovementBlock(owner,blocked);
        }
        private void Synchronize()
        {
            foreach (var branch in _branches)
                if (!branch.Retracting && (branch.Quest.Completed || !ContainsNode(branch.Quest)))
                {
                    branch.Retracting = true;
                    branch.Cursor = Mathf.Min(branch.Cursor, branch.Pieces.Count-1);
                    while (branch.Cursor >= 0 && branch.Pieces[branch.Cursor].Amount <= 0) branch.Cursor--;
                }
            if (!FindTree(out var tree)) return; // Other chapters without Bayterek do not get roots.
            foreach (var quest in _nodes.Nodes)
            {
                if (quest.Type != MapNodeType.Mandatory || quest.Completed || HasBranch(quest)) continue;
                var terrain = new Dictionary<HexCoordinate, bool>();
                foreach (var kv in _grid.Cells)
                    if (_collapse == null || !_collapse.IsCollapsed(kv.Key)) terrain[kv.Key] = kv.Value.TerrainWalkable;
                var reserved = new HashSet<HexCoordinate>();
                foreach (var branch in _branches)
                    foreach (var piece in branch.Pieces)
                        if (!branch.Retracting || piece.Blocked) { reserved.Add(piece.Coord);if(piece.Side.HasValue)reserved.Add(piece.Side.Value); }
                var protect = new HashSet<HexCoordinate> { _player.CurrentCoord };
                if (_player.StepTarget.HasValue) protect.Add(_player.StepTarget.Value);
                var objectives = new List<HexCoordinate>();
                foreach (var node in _nodes.Nodes)
                {
                    protect.Add(node.Coord);
                    if (!node.Completed) objectives.Add(node.Coord);
                }
                foreach (var core in _map.RegionCores) objectives.Add(core.coord);
                foreach (var kv in _grid.Cells)
                {
                    string id = _map.TerrainIdAt(kv.Key);
                    if (id == TileCatalog.AgacKovugu || id == TileCatalog.HalkaMeclisi || id == TileCatalog.RuhTapinagi)
                    { protect.Add(kv.Key); objectives.Add(kv.Key); }
                }
                var route = QuestRootPlanner.Plan(terrain, reserved, protect, objectives, _player.CurrentCoord, tree, quest.Coord);
                if (route == null)
                {
                    if (_warned.Add(quest)) Debug.LogWarning($"[Bayterek] Guvenli kok rotasi ertelendi: {quest.Coord}");
                    continue;
                }
                var required=new List<HexCoordinate>();
                var reachable=QuestRootPlanner.Flood(terrain,reserved,_player.CurrentCoord);
                foreach(var objective in objectives)if(QuestRootPlanner.Accessible(reachable,objective))required.Add(objective);
                foreach(var coordinate in route)reserved.Add(coordinate);
                var created = new Branch { Quest = quest };
                for (int i = 0; i < route.Count; i++)
                {
                    var c = route[i]; var center = Surface(c);
                    var from = i == 0 ? center : (Surface(route[i-1]) + center)*.5f;
                    var to = i == route.Count-1 ? (center+Surface(quest.Coord))*.5f : (center+Surface(route[i+1]))*.5f;
                    var visual = Rent();
                    var incoming = i == 0 ? to-center : center-Surface(route[i-1]);
                    var outgoing = i == route.Count-1 ? Surface(quest.Coord)-center : Surface(route[i+1])-center;
                    float phaseStep = Mathf.PI * 2 / _config.WaveLengthTiles;
                    visual.Build(from, to, incoming, outgoing, _config.Radius*(1+.16f*Mathf.Sin(i*.7f)), _config.BarkMaterial, c.VariantHash(),i==route.Count-1?.12f:_config.Radius*(1+.16f*Mathf.Sin((i+1)*.7f)),
                        i*phaseStep, phaseStep, _config.BurrowAmplitude, _config.BurrowOffset);
                    visual.Show(0, false);
                    var piece=new Piece { Coord = c, Visual = visual, Direction = to-from };
                    // Sparse lateral buttresses use a real second blocked hex, never an objective corridor.
                    if(i>1 && i<route.Count-2 && i%4==0) for(int d=0;d<6;d++)
                    {
                        var side=c.GetNeighbor((d+(c.VariantHash()&7))%6);
                        if(protect.Contains(side)||reserved.Contains(side)||!terrain.TryGetValue(side,out bool land)||!land)continue;
                        reserved.Add(side);var after=QuestRootPlanner.Flood(terrain,reserved,_player.CurrentCoord);
                        bool safe=after.Count>1;foreach(var objective in required)if(!QuestRootPlanner.Accessible(after,objective)){safe=false;break;}
                        if(!safe){reserved.Remove(side);continue;}
                        piece.Side=side;piece.SideVisual=Rent();var tip=Surface(side);
                        piece.SideDirection = tip-center;
                        piece.SideVisual.Build(center,tip,to-from,tip-center,_config.Radius*.8f,_config.BarkMaterial,side.VariantHash(),.06f,
                            (i+.5f)*phaseStep, phaseStep*.75f, _config.BurrowAmplitude, _config.BurrowOffset);
                        piece.SideVisual.Show(0,false);break;
                    }
                    created.Pieces.Add(piece);
                }
                _branches.Add(created);
                OnBranchCreated?.Invoke(quest.Coord);
            }
        }
        private Vector3 Surface(HexCoordinate c)
        {
            _grid.TryGetCell(c, out var cell);
            return c.ToWorldPosition(_grid.HexSize) + Vector3.up*((cell != null ? cell.SurfaceHeight : HexMetrics.TileHeight)+_config.SurfaceLift);
        }
        private bool FindTree(out HexCoordinate coord)
        {
            foreach (var kv in _grid.Cells)
                if (_map.TerrainIdAt(kv.Key) == TileCatalog.Bayterek) { coord = kv.Key; return true; }
            coord = default; return false;
        }
        private bool ContainsNode(ChapterNodeManager.MapNode node)
        { foreach (var n in _nodes.Nodes) if (n == node) return true; return false; }
        private bool HasBranch(ChapterNodeManager.MapNode node)
        { foreach (var b in _branches) if (b.Quest == node) return true; return false; }
        private QuestRootSegment Rent()
        {
            QuestRootSegment segment;
            if (_pool.Count > 0) segment = _pool.Pop();
            else { var go = new GameObject("Gorev_Koku"); go.transform.SetParent(_visualRoot, false); segment = go.AddComponent<QuestRootSegment>(); }
            segment.gameObject.SetActive(true); return segment;
        }
        private void Return(QuestRootSegment segment)
        { if (segment != null) { segment.gameObject.SetActive(false); _pool.Push(segment); } }
    }
}
