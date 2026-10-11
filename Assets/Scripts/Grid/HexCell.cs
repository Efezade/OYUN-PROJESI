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
        private bool _terrainWalkable = true;
        private System.Collections.Generic.HashSet<object> _movementBlocks;
        // Temporary roots never overwrite terrain/collapse state. Multiple quest branches
        // can share a cell; removing one owner must not open the other branch's cell.
        public bool IsWalkable
        {
            get => _terrainWalkable && !HasMovementBlock;
            set => _terrainWalkable = value;
        }
        public bool TerrainWalkable => _terrainWalkable;
        public bool HasMovementBlock => _movementBlocks != null && _movementBlocks.Count > 0;
        public void SetMovementBlock(object owner, bool blocked)
        {
            if (owner == null) return;
            if (blocked) { _movementBlocks ??= new(); _movementBlocks.Add(owner); }
            else _movementBlocks?.Remove(owner);
        }
        public CellType      CellType      { get; set; } = CellType.Normal;

        // Boyalı SAVAŞ karosu (palet canEnterCombat) — MissionManager buradan görev üretir.
        public bool CanEnterCombat { get; set; }

        // Boyalı MAĞAZA karosu (palet isStore) — StoreManager yakınlık dükkânını buradan açar.
        public bool IsStore { get; set; }

        // Fog durumu — başlangıç değeri Hidden
        public FogState FogState { get; set; } = FogState.Hidden;

        // Görsel bileşen referansları — SpawnVisual tarafından doldurulur
        private GameObject _visual;
        private System.Collections.Generic.HashSet<object> _visualSuppressors;
        public GameObject Visual
        {
            get => _visual;
            set { _visual = value; RefreshVisualVisibility(); }
        }
        // A restored collapse must not reveal terrain still torn up by another root.
        public void SetVisualSuppressed(object owner, bool suppressed)
        {
            if (suppressed) { _visualSuppressors ??= new(); _visualSuppressors.Add(owner); }
            else _visualSuppressors?.Remove(owner);
            RefreshVisualVisibility();
        }
        public void RefreshVisualVisibility()
        {
            bool shown = _visualSuppressors == null || _visualSuppressors.Count == 0;
            if (_visual != null && _visual.activeSelf != shown) _visual.SetActive(shown);
        }
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
