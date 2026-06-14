using UnityEngine;

namespace TacticalRPG.Grid
{
    /// <summary>
    /// Hex haritasındaki tek bir karonun veri modeli.
    /// MonoBehaviour değil; HexGridManager tarafından yönetilir.
    /// </summary>
    public class HexCell
    {
        public HexCoordinate Coordinate { get; }
        public Vector3 WorldPosition { get; }
        public bool IsWalkable { get; set; } = true;

        // Inspector'dan [SerializeField] ile atanan prefab'ın runtime kopyası
        public GameObject Visual { get; set; }

        public HexCell(HexCoordinate coordinate, float hexSize)
        {
            Coordinate = coordinate;
            WorldPosition = coordinate.ToWorldPosition(hexSize);
        }
    }
}
