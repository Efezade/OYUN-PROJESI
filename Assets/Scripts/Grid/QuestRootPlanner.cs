using System.Collections.Generic;

namespace TacticalRPG.Grid
{
    /// <summary>Plans a continuous root across land while keeping existing objectives accessible.
    /// Input is a snapshot: no scene mutations, no randomness or Unity lifecycle dependencies.</summary>
    public static class QuestRootPlanner
    {
        public static List<HexCoordinate> Plan(IReadOnlyDictionary<HexCoordinate, bool> terrain,
            HashSet<HexCoordinate> reserved, HashSet<HexCoordinate> protectedCells,
            IList<HexCoordinate> objectives, HexCoordinate player, HexCoordinate tree, HexCoordinate quest)
        {
            if (!terrain.ContainsKey(tree) || !terrain.ContainsKey(quest)) return null;
            var avoid = new HashSet<HexCoordinate>(protectedCells) { player, quest };
            avoid.Remove(tree);
            var before = Flood(terrain, reserved, player);
            var required = new List<HexCoordinate>();
            foreach (var c in objectives) if (Accessible(before, c)) required.Add(c);
            if (Accessible(before, quest) && !required.Contains(quest)) required.Add(quest);

            for (int attempt = 0; attempt < 96; attempt++)
            {
                var route = Search(terrain, reserved, avoid, tree, quest, true);
                if (route == null) return null;
                var afterBlocks = new HashSet<HexCoordinate>(reserved);
                foreach (var c in route) afterBlocks.Add(c);
                var after = Flood(terrain, afterBlocks, player);
                bool valid = after.Count > 1;
                bool changed = false;
                foreach (var objective in required)
                {
                    if (Accessible(after, objective)) continue;
                    valid = false;
                    // Prefer a walking corridor crossing the proposed root as little as possible.
                    // Reserve only its actual crossings. Reserving the entire shortest corridor
                    // can unnecessarily fence the tree in when several landmarks need access.
                    var crossing = new HashSet<HexCoordinate>(route);
                    var corridor = Search(terrain, reserved, null, player, objective, false, crossing);
                    if (corridor == null) continue;
                    foreach (var c in corridor)
                        if (c != tree && crossing.Contains(c) && avoid.Add(c)) changed = true;
                }
                if (valid) return route;
                if (!changed) return null;
            }
            return null;
        }

        public static HashSet<HexCoordinate> Flood(IReadOnlyDictionary<HexCoordinate, bool> terrain,
            HashSet<HexCoordinate> blocked, HexCoordinate start)
        {
            var seen = new HashSet<HexCoordinate>();
            if (!terrain.TryGetValue(start, out bool walkable) || !walkable || blocked.Contains(start)) return seen;
            var queue = new Queue<HexCoordinate>(); queue.Enqueue(start); seen.Add(start);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                for (int d = 0; d < 6; d++)
                {
                    var n = c.GetNeighbor(d);
                    if (!terrain.TryGetValue(n, out walkable) || !walkable || blocked.Contains(n) || !seen.Add(n)) continue;
                    queue.Enqueue(n);
                }
            }
            return seen;
        }

        public static bool Accessible(HashSet<HexCoordinate> reachable, HexCoordinate objective)
        {
            if (reachable.Contains(objective)) return true;
            for (int d = 0; d < 6; d++) if (reachable.Contains(objective.GetNeighbor(d))) return true;
            return false;
        }

        private static List<HexCoordinate> Search(IReadOnlyDictionary<HexCoordinate, bool> terrain,
            HashSet<HexCoordinate> reserved, HashSet<HexCoordinate> avoid,
            HexCoordinate start, HexCoordinate target, bool root, HashSet<HexCoordinate> costly = null)
        {
            var open = new List<HexCoordinate> { start };
            var cost = new Dictionary<HexCoordinate, int> { [start] = 0 };
            var parents = new Dictionary<HexCoordinate, HexCoordinate>();
            var closed = new HashSet<HexCoordinate>();
            while (open.Count > 0)
            {
                int best = 0, score = int.MaxValue;
                for (int i = 0; i < open.Count; i++)
                {
                    int s = cost[open[i]] + open[i].DistanceTo(target) * 7;
                    if (s < score) { score = s; best = i; }
                }
                var c = open[best]; open.RemoveAt(best);
                bool arrived = root ? c.DistanceTo(target) == 1 : c == target ||
                    (!terrain.TryGetValue(target, out bool targetWalkable) || !targetWalkable || reserved.Contains(target)) && c.DistanceTo(target) == 1;
                if (arrived && c != start)
                {
                    var path = new List<HexCoordinate> { c };
                    while (parents.TryGetValue(c, out var previous)) { c = previous; path.Add(c); }
                    path.Reverse(); return path;
                }
                closed.Add(c);
                for (int d = 0; d < 6; d++)
                {
                    var n = c.GetNeighbor(d);
                    if (!terrain.TryGetValue(n, out bool walkable) || closed.Contains(n)) continue;
                    if (root ? avoid.Contains(n) : !walkable || reserved.Contains(n)) continue;
                    int nextCost = cost[c] + (root ? reserved.Contains(n) ? 7 : walkable ? 13 : 9 :
                        costly != null && costly.Contains(n) ? 1000 : 10);
                    if (cost.TryGetValue(n, out int old) && old <= nextCost) continue;
                    cost[n] = nextCost; parents[n] = c;
                    if (!open.Contains(n)) open.Add(n);
                }
            }
            return null;
        }
    }
}
