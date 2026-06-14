using UnityEngine;

namespace TacticalRPG.Grid
{
    public enum FogState { Hidden, Explored, Visible }

    /// <summary>
    /// Hex haritasındaki tek bir karonun veri modeli.
    /// FogState doğrudan burada tutulur; FogOfWarManager ayrı Dictionary kullanmaz.
    /// </summary>
    public class HexCell
    {
        public HexCoordinate Coordinate    { get; }
        public Vector3       WorldPosition { get; }
        public bool          IsWalkable    { get; set; } = true;

        // Fog durumu — başlangıç değeri Hidden
        public FogState FogState { get; set; } = FogState.Hidden;

        // Görsel bileşen referansları — SpawnVisual tarafından doldurulur
        public GameObject   Visual       { get; set; }
        public MeshRenderer MeshRenderer { get; set; }

        public HexCell(HexCoordinate coordinate, float hexSize)
        {
            Coordinate    = coordinate;
            WorldPosition = coordinate.ToWorldPosition(hexSize);
        }
    }
}
