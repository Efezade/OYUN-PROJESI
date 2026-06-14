using System.Collections.Generic;

namespace TacticalRPG.Grid
{
    /// <summary>
    /// Hex grid üzerinde A* yol bulma algoritması.
    /// MonoBehaviour değil; PlayerController tarafından new() ile kullanılır.
    /// LINQ kullanılmaz — kritik yol, elle min arama yapılır.
    /// </summary>
    public class HexPathfinder
    {
        private class Node
        {
            public HexCell Cell;
            public Node    Parent;
            public int     G;
            public int     H;
            public int     F => G + H;
        }

        /// <summary>
        /// start'tan end'e en kısa yürünebilir yolu döner.
        /// Yol yoksa null döner.
        /// </summary>
        public List<HexCell> FindPath(HexCell start, HexCell end, HexGridManager grid)
        {
            if (start == null || end == null || !end.IsWalkable) return null;

            var open   = new List<Node>();
            var closed = new HashSet<HexCoordinate>();
            var bestG  = new Dictionary<HexCoordinate, int>();

            open.Add(new Node { Cell = start, G = 0, H = start.Coordinate.DistanceTo(end.Coordinate) });
            bestG[start.Coordinate] = 0;

            while (open.Count > 0)
            {
                Node current = LowestF(open);
                open.Remove(current);

                if (current.Cell.Coordinate == end.Coordinate)
                    return BuildPath(current);

                closed.Add(current.Cell.Coordinate);

                foreach (HexCell neighbor in grid.GetNeighbors(current.Cell.Coordinate))
                {
                    if (!neighbor.IsWalkable)                      continue;
                    if (closed.Contains(neighbor.Coordinate))      continue;

                    int tentativeG = current.G + 1;

                    if (bestG.TryGetValue(neighbor.Coordinate, out int existing) && tentativeG >= existing)
                        continue;

                    bestG[neighbor.Coordinate] = tentativeG;
                    RemoveFromOpen(open, neighbor.Coordinate);
                    open.Add(new Node
                    {
                        Cell   = neighbor,
                        Parent = current,
                        G      = tentativeG,
                        H      = neighbor.Coordinate.DistanceTo(end.Coordinate)
                    });
                }
            }

            return null;
        }

        private static Node LowestF(List<Node> nodes)
        {
            Node best = nodes[0];
            for (int i = 1; i < nodes.Count; i++)
                if (nodes[i].F < best.F || (nodes[i].F == best.F && nodes[i].H < best.H))
                    best = nodes[i];
            return best;
        }

        private static void RemoveFromOpen(List<Node> open, HexCoordinate coord)
        {
            for (int i = open.Count - 1; i >= 0; i--)
                if (open[i].Cell.Coordinate == coord)
                    open.RemoveAt(i);
        }

        private static List<HexCell> BuildPath(Node endNode)
        {
            var path = new List<HexCell>();
            for (Node n = endNode; n != null; n = n.Parent)
                path.Add(n.Cell);
            path.Reverse();
            return path;
        }
    }
}
