using System.Collections.Generic;
using UnityEngine;

namespace TacticalRPG.Grid
{
    /// <summary>
    /// Offset-r (odd-r) düzeninde dikdörtgen hex haritası oluşturur ve yönetir.
    /// Axial koordinat (q,r) birincil anahtar olarak kullanılır.
    /// GenerateGrid() public'tir; Editor araçları Edit modunda doğrudan çağırabilir.
    /// </summary>
    public class HexGridManager : MonoBehaviour
    {
        [Header("Grid Boyutu")]
        [SerializeField] private int _width = 10;
        [SerializeField] private int _height = 10;

        [Header("Hex Geometrisi")]
        [SerializeField] private float _hexSize = 1f;

        [Header("Görsel (Placeholder)")]
        [SerializeField] private GameObject _hexCellPrefab;
        [SerializeField] private Transform _gridParent;

        private Dictionary<HexCoordinate, HexCell> _cells;

        public IReadOnlyDictionary<HexCoordinate, HexCell> Cells => _cells;
        public float HexSize => _hexSize;
        public int Width => _width;
        public int Height => _height;

        // Grid merkezi — kamera konumlandırması için kullanılır
        public Vector3 GridCenter
        {
            get
            {
                float x = _hexSize * (Mathf.Sqrt(3f) * (_width - 1) * 0.5f
                                    + Mathf.Sqrt(3f) * 0.5f * (_height - 1) * 0.5f);
                float z = _hexSize * 1.5f * (_height - 1) * 0.5f;
                return new Vector3(x, 0f, z);
            }
        }

        private void Awake()
        {
            // Play modunda: Editor'da yaratılmış görsel objeleri temizle, yeniden üret
            ClearVisuals();
            GenerateGrid();
        }

        // ── Grid üretimi (Editor araçlarından da çağrılabilir) ────────────

        public void GenerateGrid()
        {
            _cells = new Dictionary<HexCoordinate, HexCell>(_width * _height);

            for (int r = 0; r < _height; r++)
            {
                int rOffset = r >> 1;
                for (int col = 0; col < _width; col++)
                {
                    int q = col - rOffset;
                    var coord = new HexCoordinate(q, r);
                    var cell = new HexCell(coord, _hexSize);
                    _cells[coord] = cell;

                    if (_hexCellPrefab != null)
                        SpawnVisual(cell);
                }
            }
        }

        public void ClearVisuals()
        {
            Transform parent = _gridParent != null ? _gridParent : transform;
            // Mevcut tüm child görsel objelerini sil
            for (int i = parent.childCount - 1; i >= 0; i--)
                DestroyImmediate(parent.GetChild(i).gameObject);

            if (_cells != null)
                _cells.Clear();
        }

        private void SpawnVisual(HexCell cell)
        {
            Transform parent = _gridParent != null ? _gridParent : transform;
            GameObject go = Instantiate(_hexCellPrefab, cell.WorldPosition, Quaternion.identity, parent);
            go.name = $"Hex_{cell.Coordinate}";
            cell.Visual = go;
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
                HexCoordinate neighborCoord = coord.GetNeighbor(i);
                if (_cells.TryGetValue(neighborCoord, out HexCell neighbor))
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
            float s = -q - r;
            int rq = Mathf.RoundToInt(q);
            int rr = Mathf.RoundToInt(r);
            int rs = Mathf.RoundToInt(s);

            float dq = Mathf.Abs(rq - q);
            float dr = Mathf.Abs(rr - r);
            float ds = Mathf.Abs(rs - s);

            if (dq > dr && dq > ds) rq = -rr - rs;
            else if (dr > ds) rr = -rq - rs;

            return new HexCoordinate(rq, rr);
        }
    }
}
