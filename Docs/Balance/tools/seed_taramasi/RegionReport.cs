using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TacticalRPG.Grid;

/// <summary>
/// BÖLGE RAPORU (2026-10-10) — Kök Ahdi bölge planını 30'luk seed havuzunun her seed'inde,
/// birden çok YERLEŞİM seed'iyle üretir ve ölçer:
///   • kalite: yürünür %, erişilebilir %, bölgesiz üretimle farkı (bölge katmanı haritayı bozuyor mu?)
///   • iskelet: oyuncu giriş bölgesinde mi, 5 çürüme kaynağı + 3 tekil karo kondu mu
///   • bölge alanı ve bölge başına düzlük (düğüm yeri) / öz karosu sayısı
///   • KARO SIKLIĞI: her karo haritanın yüzde kaçını kaplıyor → modelleme önceliği + varyant sayısı
/// Ayrıca birkaç haritayı bölge renkli PNG olarak yazar (görsel doğrulama).
///
/// Kullanım: tara.ps1 -Bolge
/// </summary>
public static class RegionReport
{
    private static readonly int[] SeedPool =
    {
        9941, 436, 6118, 11015, 5059, 10759, 3647, 11192, 8358, 11558,
        3867, 5655, 1342, 7985, 4717, 9981, 7528, 8831, 11574, 5088,
        5421, 1767, 5674, 9049, 1037, 4944, 8226, 3241, 10274, 2471
    };
    private const int LayoutsPerSeed = 8;

    public static void Run()
    {
        var sb = new StringBuilder();
        var defs = KokAhdiRegions.CreateDefs();
        int n = defs.Length;

        var tileCount = new Dictionary<string, long>();
        var tileByRegion = new Dictionary<string, long>[n];
        for (int i = 0; i < n; i++) tileByRegion[i] = new Dictionary<string, long>();
        long totalLand = 0, totalWalk = 0;
        var regionLand = new long[n];
        var regionPlain = new List<int>[n];
        var regionEss = new List<int>[n];
        for (int i = 0; i < n; i++) { regionPlain[i] = new List<int>(); regionEss[i] = new List<int>(); }

        int runs = 0, startOk = 0, srcOk = 0, uniqOk = 0, srcReach = 0, srcTotal = 0;
        double dWalk = 0, dReach = 0, minReach = 100, minReachBase = 100;
        double sumStartDistToHub = 0;
        var problems = new List<string>();

        foreach (int seed in SeedPool)
        {
            var baseP = TerrainParams.Default;
            MapResult b = TerrainGenerator.Generate(baseP, seed);
            minReachBase = Math.Min(minReachBase, b.ReachablePct);

            for (int lay = 1; lay <= LayoutsPerSeed; lay++)
            {
                var p = TerrainParams.Default;
                p.Regions = KokAhdiRegions.CreatePlan(seed * 31 + lay);
                MapResult m = TerrainGenerator.Generate(p, seed);
                runs++;

                dWalk  += m.WalkablePct  - b.WalkablePct;
                dReach += m.ReachablePct - b.ReachablePct;
                minReach = Math.Min(minReach, m.ReachablePct);

                int startReg = m.RegionAt(m.Start.q, m.Start.r);
                if (startReg >= 0 && defs[startReg].Role == RegionRole.Start) startOk++;
                else problems.Add($"seed {seed} lay {lay}: oyuncu giris bolgesinde DEGIL ({(startReg >= 0 ? defs[startReg].Id : "-")})");

                int wantSrc = defs.Count(d => d.CorruptionSource);
                if (m.Sources.Count == wantSrc) srcOk++;
                else problems.Add($"seed {seed} lay {lay}: kaynak {m.Sources.Count}/{wantSrc}");

                var comp = TerrainGenerator.ConnectedComponent(m.Tiles, m.Start.q, m.Start.r, out _);
                foreach (var s in m.Sources) { srcTotal++; if (comp.Contains((s.Q, s.R))) srcReach++; }

                int wantUniq = defs.Sum(d => d.Unique.Length), haveUniq = 0;
                foreach (var d in defs)
                    foreach (var u in d.Unique)
                        if (Count(m.Tiles, u) > 0) haveUniq++;
                if (haveUniq == wantUniq) uniqOk++;
                else problems.Add($"seed {seed} lay {lay}: tekil {haveUniq}/{wantUniq}");

                // Giriş → merkez mesafesi (yol bütçesi hissi)
                var hubCore = m.Cores.FirstOrDefault(c => defs[c.region].Role == RegionRole.Hub);
                sumStartDistToHub += Hex(m.Start, (hubCore.q, hubCore.r));

                // Sıklık
                int w = m.Tiles.GetLength(0), h = m.Tiles.GetLength(1);
                var plainHere = new int[n]; var essHere = new int[n];
                for (int q = 0; q < w; q++)
                    for (int r = 0; r < h; r++)
                    {
                        string id = m.Tiles[q, r];
                        var e = TileCatalog.Get(id);
                        if (e == null || !TileCatalog.CountsInStats(id) || TileCatalog.IsVoid(id)) continue;
                        totalLand++;
                        if (e.Walkable) totalWalk++;
                        tileCount[id] = tileCount.GetValueOrDefault(id) + 1;
                        int g = m.RegionAt(q, r);
                        if (g < 0) continue;
                        regionLand[g]++;
                        tileByRegion[g][id] = tileByRegion[g].GetValueOrDefault(id) + 1;
                        if (e.Family == TileFamily.Plain) plainHere[g]++;
                        if (e.Family == TileFamily.Stone || e.Family == TileFamily.Nature) essHere[g]++;
                    }
                for (int g = 0; g < n; g++) { regionPlain[g].Add(plainHere[g]); regionEss[g].Add(essHere[g]); }

                if (seed == 9941 || seed == 6118 || seed == 5088)
                    if (lay <= 2) WritePng(m, defs, $"bolge_{seed}_{lay}.png");
            }
        }

        sb.AppendLine($"# Bolge raporu — {SeedPool.Length} seed x {LayoutsPerSeed} yerlesim = {runs} harita");
        sb.AppendLine();
        sb.AppendLine("## Kalite (bolgesiz uretime gore fark)");
        sb.AppendLine($"  yurunur %  ortalama fark : {dWalk / runs:+0.00;-0.00}");
        sb.AppendLine($"  erisilebilir % ort. fark  : {dReach / runs:+0.00;-0.00}");
        sb.AppendLine($"  en dusuk erisilebilir %   : bolgeli {minReach:F1} | bolgesiz {minReachBase:F1}");
        sb.AppendLine();
        sb.AppendLine("## Iskelet");
        sb.AppendLine($"  oyuncu giris bolgesinde   : {startOk}/{runs}");
        sb.AppendLine($"  tum cürume kaynaklari     : {srcOk}/{runs}  (erisilebilir kaynak {srcReach}/{srcTotal})");
        sb.AppendLine($"  tum tekil karolar         : {uniqOk}/{runs}");
        sb.AppendLine($"  giris→koy ort. mesafe     : {sumStartDistToHub / runs:F1} karo");
        foreach (var pr in problems.Take(20)) sb.AppendLine("  ! " + pr);
        sb.AppendLine();
        sb.AppendLine("## Bolgeler (harita basi ortalama)");
        sb.AppendLine("  bolge                 kara   %kita  duzluk(min-ort)  oz karosu(min-ort)");
        for (int g = 0; g < n; g++)
        {
            double avg = regionLand[g] / (double)runs;
            sb.AppendLine($"  {defs[g].Id,-20} {avg,6:F0}  {100.0 * regionLand[g] / totalLand,5:F1}   " +
                          $"{regionPlain[g].Min(),4}-{regionPlain[g].Average(),5:F1}      " +
                          $"{regionEss[g].Min(),4}-{regionEss[g].Average(),5:F1}");
        }
        sb.AppendLine();
        sb.AppendLine("## Karo sikligi (kita karolarinin %'si, tum haritalar) — modelleme onceligi");
        sb.AppendLine("  karo                      %kita   harita basi   gorulen bolgeler");
        foreach (var kv in tileCount.OrderByDescending(k => k.Value))
        {
            var where = new List<string>();
            for (int g = 0; g < n; g++)
            {
                long c = tileByRegion[g].GetValueOrDefault(kv.Key);
                if (c * 100 >= regionLand[g] * 2) where.Add($"{defs[g].Id}:{100.0 * c / regionLand[g]:F0}");
            }
            sb.AppendLine($"  {kv.Key,-24} {100.0 * kv.Value / totalLand,6:F2}   {kv.Value / (double)runs,8:F1}     {string.Join(" ", where)}");
        }

        string outPath = Path.Combine(AppContext.BaseDirectory, "bolge_sonuc.txt");
        File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
        Console.Write(sb.ToString());
    }

    private static int Count(string[,] t, string id)
    {
        int c = 0;
        foreach (var x in t) if (x == id) c++;
        return c;
    }

    private static int Hex((int q, int r) a, (int q, int r) b)
    {
        int aq = a.q - (a.r >> 1), bq = b.q - (b.r >> 1);
        int dq = aq - bq, dr = a.r - b.r;
        return (Math.Abs(dq) + Math.Abs(dr) + Math.Abs(dq + dr)) / 2;
    }

    // ── Bölge renkli önizleme ───────────────────────────────────────────────

    private static readonly (float r, float g, float b)[] RegionTint =
    {
        (0.62f, 0.30f, 0.75f), // giriş — mor
        (0.95f, 0.70f, 0.25f), // köy — kehribar
        (0.25f, 0.55f, 0.50f), // bataklık — camgöbeği
        (0.35f, 0.70f, 0.30f), // vadi — yeşil
        (0.70f, 0.55f, 0.35f), // tepeler — toprak
        (0.85f, 0.30f, 0.30f), // kalp — kızıl
        (0.55f, 0.60f, 0.45f), // koru — gri yeşil
    };

    private static void WritePng(MapResult m, RegionDef[] defs, string file)
    {
        const int S = 14;                       // karo başı piksel
        int w = m.Tiles.GetLength(0), h = m.Tiles.GetLength(1);
        int W = (int)((w + 1) * S * 1.0f), H = (int)(h * S * 0.87f) + S;
        var buf = new byte[W * H * 4];

        for (int q = 0; q < w; q++)
            for (int r = 0; r < h; r++)
            {
                string id = m.Tiles[q, r];
                if (TileCatalog.IsVoid(id)) continue;
                var e = TileCatalog.Get(id);
                (float r, float g, float b) c = e != null ? (e.R, e.G, e.B) : (0.5f, 0.5f, 0.5f);
                int g0 = m.RegionAt(q, r);
                if (g0 >= 0)
                {
                    var t = RegionTint[g0 % RegionTint.Length];
                    c = (c.r * 0.55f + t.r * 0.45f, c.g * 0.55f + t.g * 0.45f, c.b * 0.55f + t.b * 0.45f);
                }
                if (e != null && !e.Walkable) c = (c.r * 0.45f, c.g * 0.45f, c.b * 0.45f);
                if (e != null && e.RegionOnly) c = (1f, 1f, 1f);
                if (e != null && (e.Id == TileCatalog.KaraAsi || e.Id == TileCatalog.ZarYirtigi)) c = (1f, 0.1f, 0.9f);
                if (q == m.Start.q && r == m.Start.r) c = (1f, 1f, 0.2f);

                // Sınır: komşusu başka bölgeyse koyu kenar
                bool border = false;
                for (int d = 0; d < 6 && !border; d++)
                {
                    RegionLayout.Neighbor(q, r, d, out int nq, out int nr);
                    int g1 = m.RegionAt(nq, nr);
                    if (g1 >= 0 && g1 != g0) border = true;
                }

                int cx = (int)((q + 0.5f * (r & 1)) * S) + S / 2;
                int cy = (int)(r * S * 0.87f) + S / 2;
                int rad = S / 2;
                for (int y = -rad; y <= rad; y++)
                    for (int x = -rad; x <= rad; x++)
                    {
                        if (Math.Abs(x) + Math.Abs(y) * 0.58f > rad) continue;
                        int px = cx + x, py = cy + y;
                        if (px < 0 || py < 0 || px >= W || py >= H) continue;
                        bool edge = border && (Math.Abs(x) >= rad - 1 || Math.Abs(y) >= rad - 1);
                        float k = edge ? 0.35f : 1f;
                        int i = (py * W + px) * 4;
                        buf[i] = B(c.r * k); buf[i + 1] = B(c.g * k); buf[i + 2] = B(c.b * k); buf[i + 3] = 255;
                    }
            }
        MinimapPreview.WritePng(Path.Combine(AppContext.BaseDirectory, file), buf, W, H);
    }

    private static byte B(float v) => (byte)Math.Max(0, Math.Min(255, (int)(v * 255f)));
}
