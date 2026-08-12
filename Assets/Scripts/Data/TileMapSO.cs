using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Grid;

namespace TacticalRPG.Data
{
    /// <summary>
    /// Her HexCoordinate'e hangi karo türünün (tileId) atandığını saklar.
    /// TilePainterWindow bu asset'i günceller — elle de düzenlenebilir.
    /// </summary>
    [CreateAssetMenu(fileName = "TileMap", menuName = "TacticalRPG/Tile Map")]
    public class TileMapSO : ScriptableObject
    {
        [System.Serializable]
        public struct TileAssignment
        {
            public HexCoordinate coord;
            public string        tileId;
        }

        [Tooltip("Atama yapılmamış karolar bu türü kullanır.")]
        public string               defaultTileId = "default";
        public List<TileAssignment> assignments   = new();

        [Header("Tahta boyutu")]
        [Tooltip("Bu haritanın kaç sütun × satır kapladığı. (0,0) = grid'in kendi ayarı kullanılır.\n\n" +
                 "NEDEN VAR: overworld ile savaş haritası AYNI HexGridManager'ı paylaşıyor. Boyut " +
                 "yalnız grid'de dursaydı, overworld tahtası büyüyünce savaş arenası da büyürdü " +
                 "(1 bölüm haritası 36×34 iken savaş da 1224 karo olurdu). Boyut artık haritanın " +
                 "kendi verisi.")]
        [SerializeField] private Vector2Int _gridSize = Vector2Int.zero;

        /// <summary>Haritanın kendi tahta boyutu; (0,0) ise grid'in ayarı geçerlidir.</summary>
        public Vector2Int GridSize => _gridSize;

        /// <summary>Runtime üretimde tahta boyutunu bildirir (üretici çağırır).</summary>
        public void SetGridSize(int width, int height) => _gridSize = new Vector2Int(width, height);

        public string GetTileId(HexCoordinate coord)
        {
            foreach (var a in assignments)
                if (a.coord == coord) return a.tileId;
            return defaultTileId;
        }

        public void SetTileId(HexCoordinate coord, string tileId)
        {
            for (int i = 0; i < assignments.Count; i++)
            {
                if (assignments[i].coord == coord)
                {
                    assignments[i] = new TileAssignment { coord = coord, tileId = tileId };
                    return;
                }
            }
            assignments.Add(new TileAssignment { coord = coord, tileId = tileId });
        }

        public void RemoveAssignment(HexCoordinate coord)
        {
            assignments.RemoveAll(a => a.coord == coord);
        }
    }
}
