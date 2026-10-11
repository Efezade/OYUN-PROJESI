using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>Two earthy colour bands trace shared hex edges. One mesh, no colliders;
    /// only discovered region borders are drawn, and never on the combat board.</summary>
    public sealed class BiomeBoundaryVisuals : MonoBehaviour
    {
        [SerializeField] private HexGridManager _grid;
        [SerializeField] private ChapterMapGenerator _map;
        [SerializeField] private FogOfWarManager _fog;
        [SerializeField] private MapCollapseManager _collapse;
        [SerializeField] private BiomeBoundaryConfigSO _config;
        private TileMapSO _overworld;
        private Mesh _mesh;
        private MeshRenderer _renderer;
        private bool _dirty = true;
        private int _exploration = -1;
        private readonly List<Vector3> _vertices = new();
        private readonly List<Color> _colors = new();
        private readonly List<Vector2> _uv = new();
        private readonly List<int> _triangles = new();
        private readonly List<Color> _regionColors = new();
        [SerializeField] private BiomeTerrainStyleSO _terrainStyle;
        public int VisibleEdgeCount { get; private set; }
        public bool IsVisible => _renderer != null && _renderer.enabled;

        private void Awake()
        {
            var obj = new GameObject("Biyom_Gecisleri"); obj.transform.SetParent(transform, false);
            _mesh = new Mesh { name = "Discovered biome boundaries", indexFormat = IndexFormat.UInt32 };
            _mesh.MarkDynamic(); obj.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = obj.AddComponent<MeshRenderer>();
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false; _renderer.enabled = false;
        }
        private void OnEnable()
        {
            if (_map != null) _map.OnMapGenerated += NewMap;
            if (_grid != null) _grid.OnGridRegenerated += Dirty;
            if (_collapse != null) _collapse.OnTileCollapsed += Collapsed;
            _dirty = true;
        }
        private void OnDisable()
        {
            if (_map != null) _map.OnMapGenerated -= NewMap;
            if (_grid != null) _grid.OnGridRegenerated -= Dirty;
            if (_collapse != null) _collapse.OnTileCollapsed -= Collapsed;
            if (_renderer != null) _renderer.enabled = false;
        }
        private void OnDestroy() { if (_mesh != null) Destroy(_mesh); }
        private void Dirty() => _dirty = true;
        private void Collapsed(int count, int total) => Dirty();
        private void NewMap()
        {
            _overworld = _grid.TileMap; _regionColors.Clear(); _exploration = -1; Dirty();
            foreach (var region in _map.Regions)
            {
                var entry = _grid.TilePalette != null ? _grid.TilePalette.GetById(region.DepletedTileId) : null;
                var art = entry?.prefab != null ? entry.prefab.GetComponent<BiomeTileAppearance>() : null;
                var ground = art != null ? art.GroundColor : entry != null ? entry.editorColor : region.Color;
                if (_terrainStyle != null) ground = _terrainStyle.Ground(region.Id);
                _regionColors.Add(Color.Lerp(ground, region.Color, _config != null ? _config.RegionTint : 0));
            }
        }
        private void LateUpdate()
        {
            if (_config == null || _grid == null || _map == null || _renderer == null) return;
            bool world = _overworld != null && _grid.TileMap == _overworld && _map.HasRegions;
            _renderer.enabled = world;
            if (!world) return;
            int version = _fog != null ? _fog.ExplorationVersion : 0;
            if (version != _exploration) { _exploration = version; _dirty = true; }
            if (!_dirty) return;
            _dirty = false; Rebuild();
        }
        private bool Known(HexCoordinate c) => (_fog == null || _fog.IsKnown(c)) &&
            (_collapse == null || !_collapse.IsCollapsed(c));
        private void Rebuild()
        {
            _vertices.Clear(); _colors.Clear(); _uv.Clear(); _triangles.Clear(); VisibleEdgeCount = 0;
            foreach (var pair in _grid.Cells)
            {
                var a = pair.Value; int regionA = _map.RegionIndexAt(pair.Key);
                if (regionA < 0 || regionA >= _regionColors.Count || !Known(pair.Key)) continue;
                // Three of six directions visit every shared edge exactly once.
                for (int d = 0; d < 3; d++)
                {
                    var next = pair.Key.GetNeighbor(d);
                    int regionB = _map.RegionIndexAt(next);
                    if (regionB < 0 || regionB >= _regionColors.Count || regionB == regionA ||
                        !_grid.TryGetCell(next, out var b) || !Known(next) ||
                        Mathf.Abs(a.SurfaceHeight - b.SurfaceHeight) > _config.MaxHeightDifference) continue;
                    Edge(a, b, _regionColors[regionA], _regionColors[regionB]); VisibleEdgeCount++;
                }
            }
            _mesh.Clear(); _mesh.SetVertices(_vertices); _mesh.SetColors(_colors);
            _mesh.SetUVs(0, _uv); _mesh.SetTriangles(_triangles, 0); _mesh.RecalculateBounds();
            _renderer.sharedMaterial = _config.Material;
        }
        private void Edge(HexCell a, HexCell b, Color ca, Color cb)
        {
            const int STEPS = 8;
            Vector3 normal = (b.WorldPosition - a.WorldPosition).normalized;
            Vector3 tangent = Vector3.Cross(Vector3.up, normal);
            Vector3 midpoint = (a.WorldPosition + b.WorldPosition) * .5f;
            midpoint.y = Mathf.Max(a.SurfaceHeight, b.SurfaceHeight) + _config.SurfaceLift;
            int start = _vertices.Count;
            for (int i = 0; i <= STEPS; i++)
            {
                float t = (float)i / STEPS;
                Vector3 p = midpoint + tangent * ((t - .5f) * _grid.HexSize);
                float noise = (Mathf.PerlinNoise(p.x * 7, p.z * 7) - .5f) * _config.Irregularity;
                // Zero displacement at shared corners keeps adjoining boundary edges joined.
                p += normal * (noise * Mathf.Sin(t * Mathf.PI));
                for (int side = 0; side < 3; side++)
                {
                    float across = side - 1;
                    _vertices.Add(transform.InverseTransformPoint(p + normal * (across * _config.HalfWidth)));
                    var color = Color.Lerp(ca, cb, side * .5f);
                    if (QualitySettings.activeColorSpace == ColorSpace.Linear) color = color.linear;
                    color.a = _config.Opacity;
                    _colors.Add(color); _uv.Add(new Vector2(t, across));
                }
                if (i == 0) continue;
                int row = start + (i - 1) * 3;
                for (int side = 0; side < 2; side++)
                {
                    int k = row + side;
                    _triangles.Add(k); _triangles.Add(k + 3); _triangles.Add(k + 1);
                    _triangles.Add(k + 1); _triangles.Add(k + 3); _triangles.Add(k + 4);
                }
            }
        }
    }
}
