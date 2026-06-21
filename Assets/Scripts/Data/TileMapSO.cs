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
