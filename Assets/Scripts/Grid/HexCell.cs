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

        // Boyalı SAVAŞ karosu (palet canEnterCombat) — MissionManager buradan görev üretir.
        public bool CanEnterCombat { get; set; }

        // Boyalı MAĞAZA karosu (palet isStore) — StoreManager yakınlık dükkânını buradan açar.
        public bool IsStore { get; set; }

        // Fog durumu — başlangıç değeri Hidden
        public FogState FogState { get; set; } = FogState.Hidden;

        // Görsel bileşen referansları — SpawnVisual tarafından doldurulur
        public GameObject   Visual       { get; set; }
        public MeshRenderer MeshRenderer { get; set; }

        // Sisten bağımsız TEMEL renk (placeholder tint için editorColor, dokulu/authored
        // karo için beyaz). FogOfWarManager bunu parlaklıkla çarpıp _BaseColor'a yazar,
        // böylece materyali değiştirmeden (boyanmış karoyu bozmadan) sis efekti verir.
        public Color BaseColor { get; set; } = Color.white;

        // ÜST KATMAN rengi (2026-10-10): BaseColor'ın ÜSTÜNE çarpılır. Kara Aşı çürümesi karoyu
        // buradan morartır. Ayrı tutulur çünkü BaseColor'ı öz görseli (EssenceFieldVisuals) de
        // yazıyor — ikisi aynı alana yazsaydı biri ötekinin rengini silerdi.
        public Color OverlayTint { get; set; } = Color.white;

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
