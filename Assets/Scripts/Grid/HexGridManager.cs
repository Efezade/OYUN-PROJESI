using System.Collections.Generic;
using UnityEngine;

namespace TacticalRPG.Grid
{
    /// <summary>
    /// Offset-r (odd-r) düzeninde dikdörtgen hex haritası oluşturur ve yönetir.
    /// Axial koordinat (q,r) birincil anahtar. GenerateGrid() Edit modunda da çalışır.
    /// </summary>
    [DefaultExecutionOrder(-100)] // FogOfWarManager ve PlayerController'dan önce çalışır
    public class HexGridManager : MonoBehaviour
    {
        [Header("Grid Boyutu")]
        [SerializeField] private int _width  = 10;
        [SerializeField] private int _height = 10;

        [Header("Hex Geometrisi")]
        [SerializeField] private float _hexSize = 1f;

        [Header("Görsel (Placeholder)")]
        [SerializeField] private GameObject _hexCellPrefab;
        [SerializeField] private Transform  _gridParent;

        [Header("Harita Özellikleri")]
        [SerializeField] private List<HexCoordinate> _watchtowerPositions = new();

        private Dictionary<HexCoordinate, HexCell> _cells;

        public IReadOnlyDictionary<HexCoordinate, HexCell> Cells    => _cells;
        public float HexSize => _hexSize;
        public int   Width   => _width;
        public int   Height  => _height;

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

        // ── Grid üretimi ──────────────────────────────────────────────────

        public void GenerateGrid()
        {
            ClearVisuals();
            _cells = new Dictionary<HexCoordinate, HexCell>(_width * _height);

            if (_hexCellPrefab == null)
                Debug.LogWarning("[HexGridManager] _hexCellPrefab NULL — fallback primitive kullanılıyor. Faz 1.1'i yeniden çalıştır.");

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

        private void SpawnVisual(HexCell cell)
        {
            Transform  parent = _gridParent != null ? _gridParent : transform;
            GameObject go;

            if (_hexCellPrefab != null)
            {
                go = Instantiate(_hexCellPrefab, cell.WorldPosition, Quaternion.identity, parent);
            }
            else
            {
                // Fallback: düz silindir (görülebilir placeholder)
                go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.transform.SetParent(parent);
                go.transform.position   = cell.WorldPosition;
                go.transform.localScale = new Vector3(0.9f, 0.05f, 0.9f);
                // Collider kalır — raycast çalışır
            }

            go.name           = $"Hex_{cell.Coordinate}";
            cell.Visual       = go;
            cell.MeshRenderer = go.GetComponent<MeshRenderer>();
        }

        // ── Sorgulama API'si ──────────────────────────────────────────────

        public bool TryGetCell(HexCoordinate coord, out HexCell cell) =>
            _cells.TryGetValue(coord, out cell);

        public bool IsInBounds(HexCoordinate coord) =>
            _cells.ContainsKey(coord);

        public List<HexCell> GetNeighbors(HexCoordinate coord)
        {
            var neighbors = new List<HexCell>(6);
            for (int i = 0; i < 6; i++)
            {
                HexCoordinate n = coord.GetNeighbor(i);
                if (_cells.TryGetValue(n, out HexCell neighbor))
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
