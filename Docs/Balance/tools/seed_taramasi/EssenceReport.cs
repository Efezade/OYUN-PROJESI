using System;
using System.Collections.Generic;
using System.Text;
using TacticalRPG.Grid;

/// <summary>
/// ÖZ YERLEŞİMİ RAPORU — "haritada 60-80 öz toplayabilmeliyim" isteğinin (2026-08-17) 30 seed'in
/// HEPSİNDE tutup tutmadığını Unity'yi AÇMADAN ölçer.
///
/// Yerleşim kuralı <c>EssenceFieldManager.Rebuild</c> ile BİREBİR aynı sırayla taklit edilir:
///   aday havuz = erişilebilir + yürünür + taşlık/ormanlık karolar (başlangıç karosu hariç)
///   → PythonRandom(seed + 2000) ile karıştır → hedef = RandInt(60, 80) → hedefe varana dek al,
///     üst sınırı (80) aşacak yatağı kırp.
/// Kural değişirse iki yer birlikte güncellenmeli; bu dosyanın tek işi o kuralı DOĞRULAMAK.
/// </summary>
public static class EssenceReport
{
    private const int TargetMin = 60, TargetMax = 80;
    private const int SeedOffset = 2000;              // EssenceFieldManager ile aynı ofset

    private static readonly int[] SeedPool =
    {
        9941, 436, 6118, 11015, 5059, 10759, 3647, 11192, 8358, 11558,
        3867, 5655, 1342, 7985, 4717, 9981, 7528, 8831, 11574, 5088,
        5421, 1767, 5674, 9049, 1037, 4944, 8226, 3241, 10274, 2471
    };

    public static void Run()
    {
        var p = TerrainParams.Default;
        var sb = new StringBuilder();

        sb.AppendLine($"# Oz yerlesimi raporu — {SeedPool.Length} seed | hedef {TargetMin}-{TargetMax} oz");
        sb.AppendLine($"# Tahta {p.Width}x{p.Height}");
        sb.AppendLine();
        sb.AppendLine("seed   | aday karo | arz (tas/doga) | hedef | SACILAN | yatak | tas  doga | durum");
        sb.AppendLine("-------|-----------|----------------|-------|---------|-------|-----------|-------");

        int fail = 0, minPlaced = int.MaxValue, maxPlaced = 0;

        foreach (int seed in SeedPool)
        {
            MapResult res = TerrainGenerator.Generate(p, seed);
            string[,] tiles = res.Tiles;

            // Erişilebilir bölge (dağ/göl ardındaki cepler hariç) — oyundaki BuildReachable ile aynı.
            var comp = TerrainGenerator.ConnectedComponent(tiles, res.Start.q, res.Start.r,
                                                           out (int q, int r) start);

            var pool = new List<(int q, int r)>();
            int supplyTas = 0, supplyDoga = 0;

            foreach (var t in comp)
            {
                if (t.q == start.q && t.r == start.r) continue;      // oyuncunun karosu
                var entry = TileCatalog.Get(tiles[t.q, t.r]);
                if (entry == null || !entry.Walkable) continue;
                if (entry.Family != TileFamily.Stone && entry.Family != TileFamily.Nature) continue;

                pool.Add(t);
                TileCatalog.EssenceOf(tiles[t.q, t.r], out int amt, out _);
                if (amt <= 0) amt = 1;
                if (entry.Family == TileFamily.Stone) supplyTas += amt; else supplyDoga += amt;
            }

            // ── EssenceFieldManager.Rebuild ile AYNI sıra ────────────────────
            var rnd = new PythonRandom(seed + SeedOffset);
            rnd.Shuffle(pool);
            int target = rnd.RandInt(TargetMin, TargetMax);

            int total = 0, deposits = 0, tas = 0, doga = 0;
            foreach (var c in pool)
            {
                if (total >= target) break;
                int room = TargetMax - total;
                if (room <= 0) break;

                var entry = TileCatalog.Get(tiles[c.q, c.r]);
                TileCatalog.EssenceOf(tiles[c.q, c.r], out int amt, out _);
                if (amt <= 0) amt = 1;
                if (amt > room) amt = room;

                if (entry.Family == TileFamily.Stone) tas += amt; else doga += amt;
                total += amt;
                deposits++;
            }

            bool ok = total >= TargetMin && total <= TargetMax;
            if (!ok) fail++;
            if (total < minPlaced) minPlaced = total;
            if (total > maxPlaced) maxPlaced = total;

            sb.AppendLine($"{seed,6} | {pool.Count,9} | {supplyTas,6}/{supplyDoga,-7} | {target,5} | " +
                          $"{total,7} | {deposits,5} | {tas,4} {doga,4} | {(ok ? "TAMAM" : "!!! HEDEF DISI")}");
        }

        sb.AppendLine();
        sb.AppendLine(fail == 0
            ? $"SONUC: 30 seed'in HEPSI {TargetMin}-{TargetMax} araligina dustu (en az {minPlaced}, en cok {maxPlaced})."
            : $"SONUC: {fail} seed hedef disinda kaldi (en az {minPlaced}, en cok {maxPlaced}) — havuz yetersiz.");

        Console.WriteLine(sb.ToString());
    }
}
