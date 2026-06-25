using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Grid;

namespace TacticalRPG.Data
{
    /// <summary>
    /// Hangi HexCoordinate'te hangi öz türünden kaç adet bulunduğunu saklar (EL YAPIMI harita).
    /// Rastgele DEĞİL — EssencePainterWindow ile boyanır, elle de düzenlenebilir.
    /// Aynı karoda birden çok tür olabilir (örn. 3 Toprak + 1 Su) — her (coord, type) ayrı giriş.
    /// </summary>
    [CreateAssetMenu(fileName = "EssenceMap", menuName = "TacticalRPG/Essence Map")]
    public class EssenceMapSO : ScriptableObject
    {
        [System.Serializable]
        public struct Placement
        {
            public HexCoordinate coord;
            public EssenceType   type;
            [Min(0)] public int  amount;
        }

        public List<Placement> placements = new();

        // ── Sorgu ──────────────────────────────────────────────────────────────

        public int GetAmount(HexCoordinate coord, EssenceType type)
        {
            foreach (var p in placements)
                if (p.coord == coord && p.type == type) return p.amount;
            return 0;
        }

        public bool HasAny(HexCoordinate coord)
        {
            foreach (var p in placements)
                if (p.coord == coord && p.amount > 0) return true;
            return false;
        }

        // ── Düzenleme (painter çağırır) ──────────────────────────────────────────

        /// <summary>Bir karodaki bir türün miktarını belirler (0 → o tür girişini siler).</summary>
        public void SetAmount(HexCoordinate coord, EssenceType type, int amount)
        {
            amount = Mathf.Max(0, amount);
            for (int i = 0; i < placements.Count; i++)
            {
                if (placements[i].coord == coord && placements[i].type == type)
                {
                    if (amount <= 0) placements.RemoveAt(i);
                    else placements[i] = new Placement { coord = coord, type = type, amount = amount };
                    return;
                }
            }
            if (amount > 0)
                placements.Add(new Placement { coord = coord, type = type, amount = amount });
        }

        /// <summary>Bir karoya bir türden delta kadar ekler/çıkarır (stack).</summary>
        public void AddAmount(HexCoordinate coord, EssenceType type, int delta)
            => SetAmount(coord, type, GetAmount(coord, type) + delta);

        /// <summary>Bir karodaki TÜM öz türlerini siler.</summary>
        public void ClearCoord(HexCoordinate coord)
            => placements.RemoveAll(p => p.coord == coord);

        public void ClearAll() => placements.Clear();

        // ── Runtime için toplu okuma ──────────────────────────────────────────────

        /// <summary>coord → türden miktar dizisi (EssenceNodeManager spawn'da kullanır).</summary>
        public Dictionary<HexCoordinate, int[]> BuildLookup(int typeCount)
        {
            var map = new Dictionary<HexCoordinate, int[]>();
            foreach (var p in placements)
            {
                if (p.amount <= 0 || (int)p.type >= typeCount) continue;
                if (!map.TryGetValue(p.coord, out int[] amts))
                {
                    amts = new int[typeCount];
                    map[p.coord] = amts;
                }
                amts[(int)p.type] += p.amount;
            }
            return map;
        }
    }
}
