using System.Collections.Generic;
using UnityEngine;

namespace TacticalRPG.Grid
{
    /// <summary>
    /// Offset-r (odd-r) düzeninde dikdörtgen hex haritası oluşturur ve yönetir.
    /// Axial koordinat (q,r) birincil anahtar olarak kullanılır.
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

        private void Awake()
        {
            GenerateGrid();
        }

        private void GenerateGrid()
        {
            _cells = new Dictionary<HexCoordinate, HexCell>(_width * _height);

            // Odd-r offset → axial dönüşümü ile dikdörtgen grid
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

        /// <summary>
        /// Dünya koordinatından en yakın hex koordinatını döner.
        /// Fare tıklaması veya Raycast sonucu için kullanılır.
        /// </summary>
        public HexCoordinate WorldToHex(Vector3 worldPos)
        {
            float q = (Mathf.Sqrt(3f) / 3f * worldPos.x - 1f / 3f * worldPos.z) / _hexSize;
            float r = (2f / 3f * worldPos.z) / _hexSize;
            return RoundHex(q, r);
        }

        // Kayan noktalı axial koordinatı en yakın tam sayıya yuvarlar (cube rounding)
        private static HexCoordinate RoundHex(float q, float r)
        {
            float s = -q - r;
            int rq = Mathf.RoundToInt(q);
            int rr = Mathf.RoundToInt(r);
            int rs = Mathf.RoundToInt(s);

            float dq = Mathf.Abs(rq - q);
            float dr = Mathf.Abs(rr - r);
            float ds = Mathf.Abs(rs - s);

            if (dq > dr && dq > ds)
                rq = -rr - rs;
            else if (dr > ds)
                rr = -rq - rs;

            return new HexCoordinate(rq, rr);
        }
    }
}
