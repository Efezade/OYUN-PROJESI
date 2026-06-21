using UnityEngine;

namespace TacticalRPG.Grid
{
    public enum FogState  { Hidden, Explored, Visible }
    public enum CellType  { Normal, Watchtower, Obstacle }

    /// <summary>
    /// Hex haritasındaki tek bir karonun veri modeli.
    /// FogState ve CellType doğrudan burada tutulur.
    /// </summary>
    public class HexCell
    {
        public HexCoordinate Coordinate    { get; }
        public Vector3       WorldPosition { get; }
        public bool          IsWalkable    { get; set; } = true;
        public CellType      CellType      { get; set; } = CellType.Normal;

        // Fog durumu — başlangıç değeri Hidden
        public FogState FogState { get; set; } = FogState.Hidden;

        // Görsel bileşen referansları — SpawnVisual tarafından doldurulur
        public GameObject   Visual       { get; set; }
        public MeshRenderer MeshRenderer { get; set; }

        // Birimlerin üstünde duracağı yüzey yüksekliği (taban üstü, dünya birimi).
        // SpawnVisual karoyu ürettiğinde ölçer; düz placeholder = TileHeight (0.3),
        // köprü gibi yüksek karolar daha büyük. Engebe/yükseklik desteği bundan gelir.
        public float SurfaceHeight { get; set; } = HexMetrics.TileHeight;

        public HexCell(HexCoordinate coordinate, float hexSize)
        {
            Coordinate    = coordinate;
            WorldPosition = coordinate.ToWorldPosition(hexSize);
        }
    }
}
