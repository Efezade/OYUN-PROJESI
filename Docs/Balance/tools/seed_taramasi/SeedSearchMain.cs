using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TacticalRPG.Grid;

/// <summary>
/// 30-SEED HAVUZU TARAMASI — oyunda çalışan ÜRETİCİNİN AYNISINI derleyip binlerce seed üretir,
/// her haritayı ölçer, sert filtrelerden geçenleri puanlar ve BİRBİRİNDEN FARKLI en iyi 30'u seçer.
///
/// Neden Python değil de gerçek C#: eski hatta (harita_terrain_v2.py) Python referansı ile C# portu
/// birbirinden ayrışabiliyordu ve doğrulama ayrı bir iş haline geliyordu. Burada tek kaynak var:
/// oyundaki dosyalar. Havuz = kodun kendi çıktısı, yorum farkı imkânsız.
/// </summary>
public static class SeedSearchMain
{
    // Kullanıcının (Efe) verdiği hedef dağılım — KITAYA oranla.
    const float TW = 78.4f, TR = 4.9f, TM = 7.5f, TB = 8.9f;

    // Zaman ekonomisi (GAME_DESIGN §0 + TimeSlotConfig.asset): 4 AP × 6 dilim = 24 AP/gün, 14 gün.
    const int APPerDay = 24, HardCutDay = 14;
    const int EssenceGoal = 70;                       // bölümü bitirmek için harcanması hedeflenen öz
    // Hareket dışı AP payı (zindan/encounter/savaş/market). Yürüyüşe kalan bütçe bunun tersi.
    const float MoveShare = 0.60f;

    sealed class Metrics
    {
        public int Seed;
        public int Land, Walkable, Main, Essence, Crossings, Landmarks, Fringe, Variety;
        public float WPct, RPct, MPct, BPct, ReachPct;
        public int Radius, ApToGoal, NearSupply;
        public int StartOpen;   // baslangicin 1 hex cevresinde kac yurunur komsu (max 6)
        public float Fill, Coast;
        public float Score;
    }

    public static void Main(string[] args)
    {
        // "arena" modu: overworld seed havuzu yerine SAVAŞ ARENALARINI üretip ölçer.
        // Ayrı bir program yerine aynı derlemede bir dal — iki .ps1 bakmak zorunda kalmayalım.
        if (args.Length > 0 && args[0] == "arena") { ArenaReport.Run(); return; }

        // "oz" modu: 30 seed'in oz yerlesimini olcer (60-80 hedefi tutuyor mu).
        if (args.Length > 0 && args[0] == "oz") { EssenceReport.Run(); return; }

        // "minimap" modu: minihatita boyamasini PNG olarak yazar (Unity'siz gorsel dogrulama).
        if (args.Length > 0 && args[0] == "minimap") { MinimapPreview.Run(); return; }

        int seedCount = args.Length > 0 ? int.Parse(args[0]) : 4000;
        int want      = args.Length > 1 ? int.Parse(args[1]) : 30;
        var p = TerrainParams.Default;

        var passed  = new List<Metrics>();
        int rejected = 0;
        var rejectReasons = new Dictionary<string, int>();

        for (int seed = 1; seed <= seedCount; seed++)
        {
            MapResult res;
            try { res = TerrainGenerator.Generate(p, seed); }
            catch (Exception ex) { Bump(rejectReasons, "istisna seed=" + seed + " " + ex.GetType().Name + ": " + ex.Message); rejected++; continue; }

            var m = Measure(res, p, seed);
            string why = HardFilter(m, p);
            if (why != null) { Bump(rejectReasons, why); rejected++; continue; }

            m.Score = ScoreOf(m);
            passed.Add(m);
        }

        passed.Sort((a, b) => b.Score.CompareTo(a.Score));
        var chosen = SelectDiverse(passed, want);

        var sb = new StringBuilder();
        sb.AppendLine($"# Seed taramasi — {seedCount} aday, {passed.Count} filtreyi gecti, {chosen.Count} secildi");
        sb.AppendLine($"# Tahta {p.Width}x{p.Height} | hedef kara {p.TargetLandMin}-{p.TargetLandMax}");
        sb.AppendLine($"# Hedef oranlar: yurunur %{TW} · nehir %{TR} · dag %{TM} · orman/gol %{TB} · gecit %{p.BridgePct * 100f:F1}");
        sb.AppendLine();
        sb.AppendLine("## Elenme nedenleri");
        foreach (var kv in Sorted(rejectReasons)) sb.AppendLine($"  {kv.Key,-26} {kv.Value}");
        sb.AppendLine();
        sb.AppendLine("## SECILEN HAVUZ");
        sb.AppendLine("seed  kara yur%  neh%  dag%  blob% eris%  oz  gecit  yaricap  AP70  cesit  dolu%  kiyi%  komsu  puan");
        foreach (var m in chosen)
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "{0,-5} {1,4} {2,5:F1} {3,5:F1} {4,5:F1} {5,5:F1} {6,5:F1} {7,4} {8,5} {9,7} {10,5} {11,6} {12,6:F2} {13,6:F2} {14,6} {15,6:F1}",
                m.Seed, m.Land, m.WPct, m.RPct, m.MPct, m.BPct, m.ReachPct, m.Essence,
                m.Crossings, m.Radius, m.ApToGoal, m.Variety, m.Fill, m.Coast, m.StartOpen, m.Score));

        sb.AppendLine();
        sb.AppendLine("## C# HAVUZ SATIRI");
        var ids = new List<string>();
        foreach (var m in chosen) ids.Add(m.Seed.ToString());
        sb.AppendLine(string.Join(", ", ids));

        sb.AppendLine();
        sb.AppendLine("## ORTALAMALAR (secilen havuz)");
        sb.AppendLine(Averages(chosen));

        // İlk 3 haritanın ASCII önizlemesi — kıyının gerçekten organik olduğunu GÖZLE doğrulamak için.
        for (int i = 0; i < Math.Min(6, chosen.Count); i++)
        {
            sb.AppendLine();
            sb.AppendLine($"## ONIZLEME — seed {chosen[i].Seed}");
            sb.AppendLine(Ascii(TerrainGenerator.Generate(p, chosen[i].Seed)));
        }

        Console.Out.Write(sb.ToString());
    }

    // ── Ölçüm ────────────────────────────────────────────────────────────────

    static Metrics Measure(MapResult res, in TerrainParams p, int seed)
    {
        int w = p.Width, h = p.Height;
        var m = new Metrics
        {
            Seed = seed, Land = res.Land, Walkable = res.Walkable, Main = res.MainComponent,
            Essence = res.EssenceSupply, Crossings = res.Crossing, Landmarks = res.Landmark,
            Fringe = res.Fringe,
            WPct = res.WalkablePct, RPct = res.RiverPct, MPct = res.MountainPct,
            BPct = res.BlobPct, ReachPct = res.ReachablePct
        };

        // Görsel çeşitlilik: haritada kaç FARKLI karo id'si var
        var seen = new HashSet<string>();
        int minQ = int.MaxValue, maxQ = int.MinValue, minR = int.MaxValue, maxR = int.MinValue;
        int coastCells = 0;
        for (int q = 0; q < w; q++)
            for (int r = 0; r < h; r++)
            {
                string id = res.Tiles[q, r];
                if (TileCatalog.IsVoid(id)) continue;
                seen.Add(id);
                var e = TileCatalog.Get(id);
                if (e == null || e.Family == TileFamily.Fringe) continue;
                if (q < minQ) minQ = q; if (q > maxQ) maxQ = q;
                if (r < minR) minR = r; if (r > maxR) maxR = r;
                if (IsCoast(res.Tiles, q, r, w, h)) coastCells++;
            }
        m.Variety = seen.Count;

        int bw = maxQ - minQ + 1, bh = maxR - minR + 1;
        m.Fill  = bw > 0 && bh > 0 ? res.Land / (float)(bw * bh) : 0f;
        m.Coast = res.Land > 0 ? coastCells / (float)res.Land : 0f;

        // AP simülasyonu — başlangıçtan BFS
        var dist = Bfs(res.Tiles, res.Start, w, h);
        int radius = 0;
        foreach (var kv in dist) if (kv.Value > radius) radius = kv.Value;
        m.Radius = radius;

        int near = 0;
        foreach (var kv in dist)
        {
            if (kv.Value > 12) continue;
            TileCatalog.EssenceOf(res.Tiles[kv.Key.q, kv.Key.r], out int amt, out _);
            near += amt;
        }
        m.NearSupply = near;

        // Baslangicin ANINDA hareket edilebilirligi: 6 komsudan kaci yurunur?
        // 0 ise oyuncu ilk turdan itibaren KILITLI kalir (2026-08-12 hata raporu).
        for (int d = 0; d < 6; d++)
        {
            Nb(res.Start.q, res.Start.r, d, out int nq, out int nr);
            if (nq < 0 || nr < 0 || nq >= w || nr >= h) continue;
            if (TileCatalog.IsWalkable(res.Tiles[nq, nr])) m.StartOpen++;
        }
        m.ApToGoal   = GreedyEssenceRun(res.Tiles, res.Start, w, h, EssenceGoal);
        return m;
    }

    static bool IsCoast(string[,] t, int q, int r, int w, int h)
    {
        for (int d = 0; d < 6; d++)
        {
            Nb(q, r, d, out int nq, out int nr);
            if (nq < 0 || nr < 0 || nq >= w || nr >= h) return true;
            var e = TileCatalog.Get(t[nq, nr]);
            if (e == null || e.Family == TileFamily.Void || e.Family == TileFamily.Fringe) return true;
        }
        return false;
    }

    static readonly int[,] DE = { { 1, 0 }, { 0, -1 }, { -1, -1 }, { -1, 0 }, { -1, 1 }, { 0, 1 } };
    static readonly int[,] DO = { { 1, 0 }, { 1, -1 }, {  0, -1 }, { -1, 0 }, {  0, 1 }, { 1, 1 } };
    static void Nb(int q, int r, int d, out int nq, out int nr)
    {
        int[,] t = (r & 1) == 0 ? DE : DO;
        nq = q + t[d, 0]; nr = r + t[d, 1];
    }

    static Dictionary<(int q, int r), int> Bfs(string[,] t, (int q, int r) from, int w, int h)
    {
        var dist = new Dictionary<(int q, int r), int> { [from] = 0 };
        var queue = new Queue<(int q, int r)>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            for (int d = 0; d < 6; d++)
            {
                Nb(c.q, c.r, d, out int nq, out int nr);
                if (nq < 0 || nr < 0 || nq >= w || nr >= h) continue;
                if (!TileCatalog.IsWalkable(t[nq, nr])) continue;
                if (dist.ContainsKey((nq, nr))) continue;
                dist[(nq, nr)] = dist[c] + 1;
                queue.Enqueue((nq, nr));
            }
        }
        return dist;
    }

    /// <summary>Açgözlü öz toplama: her adımda "en yakın öz karosu"na yürür, 1 AP ile toplar.
    /// Hedefe ulaşana kadar harcanan AP'yi döner (ulaşılamazsa büyük sayı). Gerçek oyuncu daha iyi
    /// rota kurar ama bu ALT SINIRI verir — harita bu bütçede bile bitirilemiyorsa adaletsizdir.</summary>
    static int GreedyEssenceRun(string[,] t, (int q, int r) start, int w, int h, int goal)
    {
        var taken = new HashSet<(int q, int r)>();
        var cur = start;
        int ap = 0, got = 0, budget = APPerDay * HardCutDay;

        while (got < goal && ap < budget)
        {
            var dist = Bfs(t, cur, w, h);
            (int q, int r) best = (-1, -1); float bestKey = float.MaxValue; int bestAmt = 0;
            foreach (var kv in dist)
            {
                if (taken.Contains(kv.Key)) continue;
                TileCatalog.EssenceOf(t[kv.Key.q, kv.Key.r], out int amt, out _);
                if (amt <= 0) continue;
                float key = (kv.Value + 1f) / amt;                 // AP başına öz — en verimli hedef
                if (key < bestKey) { bestKey = key; best = kv.Key; bestAmt = amt; }
            }
            if (best.q < 0) break;
            ap += dist[best] + 1;
            got += bestAmt;
            taken.Add(best);
            cur = best;
        }
        return got >= goal ? ap : 9999;
    }

    // ── Filtre + puan ────────────────────────────────────────────────────────

    static string HardFilter(Metrics m, in TerrainParams p)
    {
        if (m.Land < p.TargetLandMin || m.Land > p.TargetLandMax) return "kara sayisi";
        if (Math.Abs(m.WPct - TW) > 2.0f) return "yurunur oran";
        if (Math.Abs(m.RPct - TR) > 1.3f) return "nehir oran";
        if (Math.Abs(m.MPct - TM) > 1.3f) return "dag oran";
        if (Math.Abs(m.BPct - TB) > 1.3f) return "blob oran";
        if (m.Crossings < 1)              return "gecit yok";
        if (m.ReachPct < 93f)             return "harita parcali";
        if (m.Essence < 235)              return "oz arzi dusuk";
        if (m.Essence > 380)              return "oz arzi asiri";
        if (m.NearSupply < 40)            return "baslangic kisir";
        if (m.StartOpen < 4)              return "baslangic kapali";
        if (m.Radius < 13 || m.Radius > 34) return "yaricap";
        // Hareket bütçesi: 14 gün × 24 AP'nin %60'ı yürüyüşe kalır varsayımı.
        if (m.ApToGoal > (int)(APPerDay * HardCutDay * MoveShare)) return "70 oz yetismiyor";
        if (m.ApToGoal < 70)              return "cok kolay";
        if (m.Fill < 0.42f || m.Fill > 0.80f) return "silhouette (dolgu)";
        if (m.Coast < 0.17f)              return "kiyi cok duz";
        if (m.Variety < 34)               return "karo cesidi az";
        return null;
    }

    static float ScoreOf(Metrics m)
    {
        float s = 0f;

        // 1) Oran isabeti (hedeften sapma cezası)
        s -= Math.Abs(m.WPct - TW) * 4f;
        s -= Math.Abs(m.RPct - TR) * 6f;
        s -= Math.Abs(m.MPct - TM) * 6f;
        s -= Math.Abs(m.BPct - TB) * 6f;

        // 2) Organik siluet: girintili kıyı ödül, dolgu oranı 0.55'e yakınlık ödül
        s += m.Coast * 90f;
        s -= Math.Abs(m.Fill - 0.60f) * 45f;

        // 3) Zaman baskısı: 70 özü ~gün 6-9 arasında bitirmek ideal (kalan süre boss/zindan/keşfe).
        //    Çok erken = baskı yok; çok geç = adaletsiz.
        int ideal = (int)(APPerDay * 7.5f * MoveShare);        // ≈108 AP
        s -= Math.Abs(m.ApToGoal - ideal) * 0.20f;

        // 4) Rota bulmacası: geçitler (köprü/dağ geçidi) karar noktası yaratır — 1-3 arası ideal.
        s += (m.Crossings >= 1 && m.Crossings <= 3) ? 12f : 4f;

        // 5) Erişilebilirlik ve görsel çeşitlilik
        s += (m.ReachPct - 93f) * 1.2f;
        s += (m.Variety - 34) * 1.6f;

        // 6) Harita büyüklüğü: hedef ortası (550) civarı
        s -= Math.Abs(m.Land - 550) * 0.05f;

        // 7) Keşif yarıçapı: 20-26 arası "büyük ama aşılabilir"
        s -= Math.Abs(m.Radius - 23) * 0.7f;

        return s;
    }

    /// <summary>Puanı yüksek ama BİRBİRİNE BENZEYEN haritalar havuzu sıkıcı yapar (30 harita
    /// oynanacak). Seçim, metrik uzayında yeterince uzak olanları alır.</summary>
    static List<Metrics> SelectDiverse(List<Metrics> ranked, int want)
    {
        var chosen = new List<Metrics>();
        float threshold = 1.15f;
        for (int pass = 0; pass < 14 && chosen.Count < want; pass++)
        {
            foreach (var m in ranked)
            {
                if (chosen.Count >= want) break;
                if (chosen.Contains(m)) continue;
                bool ok = true;
                foreach (var c in chosen)
                    if (Signature(m, c) < threshold) { ok = false; break; }
                if (ok) chosen.Add(m);
            }
            threshold *= 0.72f;                 // yeterince aday yoksa benzerlik eşiğini gevşet
        }
        return chosen;
    }

    static float Signature(Metrics a, Metrics b)
    {
        float d = 0f;
        d += Sq((a.Fill     - b.Fill)     / 0.045f);
        d += Sq((a.Coast    - b.Coast)    / 0.05f);
        d += Sq((a.Radius   - b.Radius)   / 3.0f);
        d += Sq((a.ApToGoal - b.ApToGoal) / 22.0f);
        d += Sq((a.Land     - b.Land)     / 28.0f);
        d += Sq((a.MPct     - b.MPct)     / 0.9f);
        d += Sq((a.RPct     - b.RPct)     / 0.7f);
        return (float)Math.Sqrt(d);
    }
    static float Sq(float v) => v * v;

    // ── Çıktı yardımcıları ───────────────────────────────────────────────────

    static void Bump(Dictionary<string, int> d, string k) { d.TryGetValue(k, out int v); d[k] = v + 1; }

    static List<KeyValuePair<string, int>> Sorted(Dictionary<string, int> d)
    {
        var l = new List<KeyValuePair<string, int>>(d);
        l.Sort((a, b) => b.Value.CompareTo(a.Value));
        return l;
    }

    static string Averages(List<Metrics> l)
    {
        if (l.Count == 0) return "(bos)";
        float land = 0, wp = 0, rp = 0, mp = 0, bp = 0, re = 0, oz = 0, ap = 0, va = 0, co = 0, fi = 0, ra = 0, cr = 0;
        foreach (var m in l)
        {
            land += m.Land; wp += m.WPct; rp += m.RPct; mp += m.MPct; bp += m.BPct;
            re += m.ReachPct; oz += m.Essence; ap += m.ApToGoal; va += m.Variety;
            co += m.Coast; fi += m.Fill; ra += m.Radius; cr += m.Crossings;
        }
        int n = l.Count;
        return string.Format(CultureInfo.InvariantCulture,
            "kara {0:F0} | yurunur %{1:F2} | nehir %{2:F2} | dag %{3:F2} | blob %{4:F2} | gecit {5:F1}\n" +
            "erisilebilir %{6:F1} | oz {7:F0} | 70-oz icin {8:F0} AP (~gun {9:F1}) | karo cesidi {10:F0}\n" +
            "kiyi karmasikligi {11:F2} | dolgu {12:F2} | yaricap {13:F1}",
            land / n, wp / n, rp / n, mp / n, bp / n, cr / n, re / n, oz / n, ap / n,
            (ap / n) / (APPerDay * MoveShare), va / n, co / n, fi / n, ra / n);
    }

    /// <summary>Haritayı ASCII olarak çizer — kıyının organikliği ve dağ silsileleri gözle görülsün.</summary>
    static string Ascii(MapResult res)
    {
        int w = res.Tiles.GetLength(0), h = res.Tiles.GetLength(1);
        var sb = new StringBuilder();
        for (int r = 0; r < h; r++)
        {
            if ((r & 1) == 1) sb.Append(' ');
            for (int q = 0; q < w; q++)
            {
                string id = res.Tiles[q, r];
                var e = TileCatalog.Get(id);
                char ch;
                if (e == null || e.Family == TileFamily.Void) ch = ' ';
                else switch (e.Family)
                {
                    case TileFamily.Fringe:   ch = id == TileCatalog.SisPerdesi ? '.' :
                                                   id == TileCatalog.UzakKayalik ? 'o' : '~'; break;
                    case TileFamily.River:    ch = '='; break;
                    case TileFamily.Mountain: ch = '^'; break;
                    case TileFamily.Blob:     ch = e.Surface == Surface.Water || e.Surface == Surface.Ice
                                                   ? 'O' : '#'; break;
                    case TileFamily.Crossing: ch = 'H'; break;
                    case TileFamily.Landmark: ch = '*'; break;
                    case TileFamily.Stone:    ch = 'n'; break;
                    case TileFamily.Nature:   ch = 't'; break;
                    default:                  ch = ','; break;
                }
                if (q == res.Start.q && r == res.Start.r) ch = '@';
                sb.Append(ch).Append(' ');
            }
            sb.Append('\n');
        }
        sb.AppendLine("  ' '=bos  '.'=sis  '~'=deniz  'o'=adacik  ','=ova  't'=orman  'n'=taslik");
        sb.AppendLine("  '^'=dag  '#'=sik orman  'O'=gol  '='=nehir  'H'=gecit  '*'=landmark  '@'=baslangic");
        return sb.ToString();
    }
}
