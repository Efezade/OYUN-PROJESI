using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Data;

namespace TacticalRPG.Grid
{
    [DefaultExecutionOrder(-100)]
    public class HexGridManager : MonoBehaviour
    {
        [Header("Grid Boyutu")]
        [SerializeField] private int   _width   = 10;
        [SerializeField] private int   _height  = 10;
        [SerializeField] private float _hexSize = 1f;

        [Header("Varsayılan Görsel (Palette boşsa kullanılır)")]
        [SerializeField] private GameObject _hexCellPrefab;
        [SerializeField] private Transform  _gridParent;

        [Header("Karo Sistemi")]
        [SerializeField] private TilePaletteSO _tilePalette;
        [SerializeField] private TileMapSO     _tileMap;

        [Header("Harita Özellikleri")]
        [SerializeField] private List<HexCoordinate> _watchtowerPositions = new();

        private Dictionary<HexCoordinate, HexCell> _cells;

        public IReadOnlyDictionary<HexCoordinate, HexCell> Cells      => _cells;
        public float                                        HexSize    => _hexSize;
        public int                                          Width      => _width;
        public int                                          Height     => _height;
        public bool                                         HasCells   => _cells != null && _cells.Count > 0;
        public TilePaletteSO                               TilePalette => _tilePalette;
        public TileMapSO                                   TileMap     => _tileMap;

        public Vector3 GridCenter
        {
            get
            {
                float x = _hexSize * (Mathf.Sqrt(3f) * (_width  - 1) * 0.5f
                                    + Mathf.Sqrt(3f) * 0.5f * (_height - 1) * 0.5f);
                float z = _hexSize * 1.5f * (_height - 1) * 0.5f;
                return new Vector3(x, 0f, z);
            }
        }

        private void Awake() => GenerateGrid();

        // ── Grid üretimi ──────────────────────────────────────────────────────

        public void GenerateGrid()
        {
            ClearVisuals();
            _cells = new Dictionary<HexCoordinate, HexCell>(_width * _height);

            for (int r = 0; r < _height; r++)
            {
                int rOffset = r >> 1;
                for (int col = 0; col < _width; col++)
                {
                    int q     = col - rOffset;
                    var coord = new HexCoordinate(q, r);
                    var cell  = new HexCell(coord, _hexSize);

                    if (_watchtowerPositions.Contains(coord))
                        cell.CellType = CellType.Watchtower;

                    _cells[coord] = cell;
                    SpawnVisual(cell);
                }
            }
        }

        public void ClearVisuals()
        {
            Transform parent = _gridParent != null ? _gridParent : transform;
            for (int i = parent.childCount - 1; i >= 0; i--)
                DestroyImmediate(parent.GetChild(i).gameObject);

            _cells?.Clear();
        }

        /// <summary>
        /// Tek bir hücrenin görselini yeniden üretir (TilePainter boyamadan sonra çağrılır).
        /// </summary>
        public void RegenerateCellVisual(HexCoordinate coord)
        {
            if (_cells == null || !_cells.TryGetValue(coord, out HexCell cell)) return;

            if (cell.Visual != null)
                DestroyImmediate(cell.Visual);

            SpawnVisual(cell);
        }

        // ── Görsel üretimi ────────────────────────────────────────────────────

        private void SpawnVisual(HexCell cell)
        {
            Transform  parent = _gridParent != null ? _gridParent : transform;
            GameObject prefab = ResolvePrefab(cell.Coordinate);
            GameObject go;

            if (prefab != null)
            {
                go = Instantiate(prefab, cell.WorldPosition, Quaternion.identity, parent);

                // Kırık mesh GUID fallback (yalnızca placeholder HexCell prefabı için)
                var mf = go.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh == null)
                {
                    Mesh fresh = HexMetrics.CreateHexMesh(0.95f);
                    mf.sharedMesh = fresh;
                    var mc = go.GetComponent<MeshCollider>();
                    if (mc != null) mc.sharedMesh = fresh;
                }
            }
            else
            {
                // Tamamen prefabsız acil fallback
                go = new GameObject();
                go.transform.SetParent(parent);
                go.transform.position = cell.WorldPosition;

                var mf2 = go.AddComponent<MeshFilter>();
                mf2.sharedMesh = HexMetrics.CreateHexMesh(0.95f);

                go.AddComponent<MeshRenderer>().sharedMaterial =
                    new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));

                var mc2 = go.AddComponent<MeshCollider>();
                mc2.sharedMesh = mf2.sharedMesh;
            }

            go.name           = $"Hex_{cell.Coordinate}";
            cell.Visual       = go;
            // Kompleks prefablarda (child MeshRenderer) kök önce denenir
            cell.MeshRenderer = go.GetComponent<MeshRenderer>()
                             ?? go.GetComponentInChildren<MeshRenderer>();
        }

        private GameObject ResolvePrefab(HexCoordinate coord)
        {
            if (_tilePalette != null && _tileMap != null)
            {
                string id    = _tileMap.GetTileId(coord);
                var    entry = _tilePalette.GetById(id);
                if (entry?.prefab != null)
                    return entry.prefab;
            }
            return _hexCellPrefab;
        }

        // ── Sorgulama API'si ──────────────────────────────────────────────────

        public bool TryGetCell(HexCoordinate coord, out HexCell cell)
        {
            if (_cells == null) { cell = null; return false; }
            return _cells.TryGetValue(coord, out cell);
        }

        public bool IsInBounds(HexCoordinate coord) =>
            _cells != null && _cells.ContainsKey(coord);

        public List<HexCell> GetNeighbors(HexCoordinate coord)
        {
            var neighbors = new List<HexCell>(6);
            for (int i = 0; i < 6; i++)
            {
                HexCoordinate n = coord.GetNeighbor(i);
                if (_cells != null && _cells.TryGetValue(n, out HexCell neighbor))
                    neighbors.Add(neighbor);
            }
            return neighbors;
        }

        public HexCoordinate WorldToHex(Vector3 worldPos)
        {
            float q = (Mathf.Sqrt(3f) / 3f * worldPos.x - 1f / 3f * worldPos.z) / _hexSize;
            float r = (2f / 3f * worldPos.z) / _hexSize;
            return RoundHex(q, r);
        }

        private static HexCoordinate RoundHex(float q, float r)
        {
            float s  = -q - r;
            int   rq = Mathf.RoundToInt(q);
            int   rr = Mathf.RoundToInt(r);
            int   rs = Mathf.RoundToInt(s);

            float dq = Mathf.Abs(rq - q);
            float dr = Mathf.Abs(rr - r);
            float ds = Mathf.Abs(rs - s);

            if (dq > dr && dq > ds) rq = -rr - rs;
            else if (dr > ds)       rr = -rq - rs;

            return new HexCoordinate(rq, rr);
        }
    }
}
