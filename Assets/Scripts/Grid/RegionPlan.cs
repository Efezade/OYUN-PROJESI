using System;
using System.Collections.Generic;

namespace TacticalRPG.Grid
{
    /// <summary>Bölgenin haritadaki rolü — YARI SABİT İSKELETİ bu belirler (Efe 2026-10-10:
    /// "her girişte bölgelerin yeri değişebilir ama bölgeler Minecraft biyomları gibi olacak").</summary>
    public enum RegionRole
    {
        Filler,  // ara doku (sıradan koru) — boşlukları doldurur, birden çok parçası olabilir
        Start,   // giriş bölgesi — kıyıya yakın; oyuncu bu bölgenin İÇ kenarında doğar
        Hub,     // merkez — giriş ile final arasında, iç kesimde
        Graft,   // aşı bölgesi — merkezin çevresine dağılır, arındırılabilir çürüme kaynağı taşır
        Final    // son bölge — girişten EN UZAK yerde
    }

    /// <summary>Bölge tablosunda bir karo + çekilme ağırlığı.</summary>
    public sealed class TileWeight
    {
        public string Id;
        public float  Weight;
        public TileWeight(string id, float weight) { Id = id; Weight = weight; }
    }

    /// <summary>
    /// TEK BİR BÖLGENİN üretim tarifi (Unity'siz saf veri — seed taraması aynı kodu derlesin).
    /// Oyunda <c>RegionSO</c>'dan doldurulur; burada yalnız üreticinin anladığı alanlar var.
    /// Boş liste = o adımda bölge kuralı yok, eski (iklim) kuralı işler.
    /// </summary>
    public sealed class RegionDef
    {
        public string     Id   = "bolge";
        public string     Name = "Bölge";
        public RegionRole Role = RegionRole.Filler;

        /// <summary>Kaç çekirdekten büyür (ara doku 2 parça olabilir; diğerleri 1).</summary>
        public int   Seeds = 1;
        /// <summary>Göreli alan payı (toplamla oranlanır).</summary>
        public float Area  = 1f;

        /// <summary>Yükseklik/nem bükmesi (−0.3…+0.3). Dağlar, göller ve nehirler sonradan bu
        /// alanlardan çıktığı için bataklık kendiliğinden alçak-göllü, tepeler sırtlı olur.</summary>
        public float ElevBias, MoistBias;

        public TileWeight[] Ground    = Array.Empty<TileWeight>();  // yürünür zemin tablosu
        public string[]     Forest    = Array.Empty<string>();      // geçilmez sık orman blobu
        public string[]     Lake      = Array.Empty<string>();      // göl blobu
        /// <summary>Dağ türleri: [0] silsilenin içi, kalanı etek/kenar. Boş = iklim kuralı (karlı
        /// zirve, volkanik kaya…) — orman evreninde kar olmasın diye bölge söyler.</summary>
        public string[]     Mountain  = Array.Empty<string>();
        public string[]     Landmarks = Array.Empty<string>();      // bölgede rastgele landmark havuzu
        public string[]     Unique    = Array.Empty<string>();      // bölge çekirdeğine BİR KEZ konan karolar

        /// <summary>Çekirdekte çürüme kaynağı var mı (kaynak karosu <see cref="SourceTile"/>).</summary>
        public bool   CorruptionSource;
        public string SourceTile;
        /// <summary>Kalıcı kaynak (zorunlu görevle arınmaz — Zar yırtığı, Kalp).</summary>
        public bool   SourcePermanent;
    }

    /// <summary>Bir bölümün bölge planı. Null = bölgesiz eski üretim (iklim kovaları).</summary>
    public sealed class RegionPlan
    {
        public RegionDef[] Regions = Array.Empty<RegionDef>();

        /// <summary>Bölge sınırının ne kadar girintili olduğu (0 = düz Voronoi).</summary>
        public float BorderNoise = 0.9f;

        /// <summary>Sınır karolarında komşu bölgenin tablosundan çekme olasılığı — geçiş yumuşar.</summary>
        public float BorderMix = 0.25f;

        /// <summary>Oyuncu giriş (Start) bölgesinde doğsun mu.</summary>
        public bool StartInStartRegion = true;

        /// <summary>Bölge YERLEŞİM seed'i — arazi seed'inden AYRI: aynı kıtada bölgeler her
        /// koşuda başka yere düşer (30'luk seed havuzu oran filtrelerinden geçmiş kalır).</summary>
        public int LayoutSeed = 1;

        public bool IsEmpty => Regions == null || Regions.Length == 0;
    }

    /// <summary>Bir çürüme kaynağı (üretim çıktısı).</summary>
    public struct RegionSource
    {
        public int  Q, R;        // dizi indisi (sütun, satır)
        public int  Region;      // RegionPlan.Regions indisi
        public bool Permanent;
    }

    /// <summary>
    /// BÖLGE YERLEŞİMİ — kıtayı rollere göre çekirdeklere bölüp ağırlıklı, gürültülü bir Voronoi
    /// ile büyütür. Saf C#; <see cref="TerrainGenerator"/> çağırır.
    ///
    /// İskelet (her koşuda aynı MANTIK, farklı KONUM):
    ///   1. Giriş: kıyıya yakın (kıyıdan 2-3 karo) rastgele bir karo.
    ///   2. Final: girişten yürüme mesafesi en uzak %12'lik dilimden rastgele.
    ///   3. Merkez: giriş ile finale EŞİT uzaklıktaki, kıyıdan en içerdeki karolardan.
    ///   4. Aşı ve ara doku: kalan çekirdekler "en uzak nokta" örneklemesiyle boşluklara dağılır
    ///      (aşı bölgeleri önce → merkezin çevresine yayılırlar).
    /// Büyütme: çok kaynaklı Dijkstra; her bölgenin hızı alan payına göre, karo maliyeti gürültüyle
    /// değişir → sınırlar Minecraft biyomları gibi organik. Alan payı 5 turda hedefe yaklaştırılır.
    /// </summary>
    public static class RegionLayout
    {
        public sealed class Result
        {
            public int[,] Region;                       // -1 = bölge yok (deniz)
            public List<(int region, int q, int r)> Sites = new();
            public int[] Size;                          // bölge başına karo sayısı
        }

        private static readonly int[,] DirsEven = { { 1, 0 }, { 0, -1 }, { -1, -1 }, { -1, 0 }, { -1, 1 }, { 0, 1 } };
        private static readonly int[,] DirsOdd  = { { 1, 0 }, { 1, -1 }, {  0, -1 }, { -1, 0 }, {  0, 1 }, { 1, 1 } };

        internal static void Neighbor(int q, int r, int d, out int nq, out int nr)
        {
            int[,] t = (r & 1) == 0 ? DirsEven : DirsOdd;
            nq = q + t[d, 0];
            nr = r + t[d, 1];
        }

        internal static void World(int q, int r, out float x, out float z)
        {
            x = 1.7320508f * (q + 0.5f * (r & 1));
            z = 1.5f * r;
        }

        public static Result Build(bool[,] land, int[,] coastDist, RegionPlan plan)
        {
            int w = land.GetLength(0), h = land.GetLength(1);
            var res = new Result { Region = new int[w, h], Size = new int[plan.Regions.Length] };
            for (int q = 0; q < w; q++) for (int r = 0; r < h; r++) res.Region[q, r] = -1;

            var cells = new List<(int q, int r)>();
            for (int q = 0; q < w; q++) for (int r = 0; r < h; r++) if (land[q, r]) cells.Add((q, r));
            if (cells.Count == 0) return res;

            var rnd = new PythonRandom(plan.LayoutSeed);
            PlaceSites(land, coastDist, plan, cells, rnd, res.Sites);
            if (res.Sites.Count == 0) return res;

            Grow(land, plan, cells, res);
            Smooth(land, res, plan.Regions.Length);
            return res;
        }

        // ── Çekirdekler ──────────────────────────────────────────────────────

        private static void PlaceSites(bool[,] land, int[,] coastDist, RegionPlan plan,
                                       List<(int q, int r)> cells, PythonRandom rnd,
                                       List<(int region, int q, int r)> sites)
        {
            int n = plan.Regions.Length;
            int Find(RegionRole role) { for (int i = 0; i < n; i++) if (plan.Regions[i].Role == role) return i; return -1; }

            int start = Find(RegionRole.Start), final = Find(RegionRole.Final), hub = Find(RegionRole.Hub);
            var placed = new List<(int q, int r)>();

            (int q, int r) S = (-1, -1), F = (-1, -1);

            if (start >= 0)
            {
                var coastal = cells.FindAll(c => coastDist[c.q, c.r] >= 2 && coastDist[c.q, c.r] <= 3);
                if (coastal.Count == 0) coastal = cells;
                S = coastal[rnd.RandRange(coastal.Count)];
                sites.Add((start, S.q, S.r)); placed.Add(S);
            }

            if (final >= 0)
            {
                int[,] dS = S.q >= 0 ? Distances(land, new List<(int q, int r)> { S }) : null;
                var sorted = new List<(int q, int r)>(cells);
                if (dS != null) sorted.Sort((a, b) => dS[b.q, b.r].CompareTo(dS[a.q, a.r]));
                else rnd.Shuffle(sorted);
                int top = Math.Max(1, sorted.Count * 12 / 100);
                F = sorted[rnd.RandRange(top)];
                sites.Add((final, F.q, F.r)); placed.Add(F);
            }

            if (hub >= 0)
            {
                (int q, int r) H;
                if (S.q >= 0 && F.q >= 0)
                {
                    int[,] dS = Distances(land, new List<(int q, int r)> { S });
                    int[,] dF = Distances(land, new List<(int q, int r)> { F });
                    // Eşit uzaklık şeridi; içinden kıyıdan en içerdeki 5 aday arasından rastgele.
                    var band = cells.FindAll(c => Math.Abs(dS[c.q, c.r] - dF[c.q, c.r]) <= 1);
                    if (band.Count == 0) band = new List<(int q, int r)>(cells);
                    band.Sort((a, b) => coastDist[b.q, b.r].CompareTo(coastDist[a.q, a.r]));
                    H = band[rnd.RandRange(Math.Min(5, band.Count))];
                }
                else
                {
                    var inner = new List<(int q, int r)>(cells);
                    inner.Sort((a, b) => coastDist[b.q, b.r].CompareTo(coastDist[a.q, a.r]));
                    H = inner[rnd.RandRange(Math.Min(5, inner.Count))];
                }
                sites.Add((hub, H.q, H.r)); placed.Add(H);
            }

            // Kalan çekirdekler: önce aşı bölgeleri, sonra diğerleri — en uzak nokta örneklemesi.
            var rest = new List<int>();
            for (int i = 0; i < n; i++) if (plan.Regions[i].Role == RegionRole.Graft) rest.Add(i);
            for (int i = 0; i < n; i++)
            {
                RegionRole role = plan.Regions[i].Role;
                if (role == RegionRole.Graft) continue;
                int extra = Math.Max(1, plan.Regions[i].Seeds);
                bool roleSited = role == RegionRole.Start || role == RegionRole.Final || role == RegionRole.Hub;
                if (roleSited && i == Find(role)) extra--;          // rol çekirdeği zaten kondu
                for (int k = 0; k < extra; k++) rest.Add(i);
            }
            // Aşı bölgelerinin ek çekirdekleri (Seeds > 1) en sona.
            for (int i = 0; i < n; i++)
                if (plan.Regions[i].Role == RegionRole.Graft)
                    for (int k = 1; k < plan.Regions[i].Seeds; k++) rest.Add(i);

            foreach (int reg in rest)
            {
                int[,] d = placed.Count > 0 ? Distances(land, placed) : null;
                (int q, int r) best = cells[0]; float bestKey = float.MinValue;
                foreach (var c in cells)
                {
                    if (coastDist[c.q, c.r] < 2) continue;              // kıyı şeridine çekirdek koyma
                    float baseD = d != null ? d[c.q, c.r] : 1f;
                    if (baseD == int.MaxValue) continue;
                    float key = baseD * (0.8f + 0.4f * (float)rnd.Random());
                    if (key > bestKey) { bestKey = key; best = c; }
                }
                sites.Add((reg, best.q, best.r)); placed.Add(best);
            }
        }

        /// <summary>Çok kaynaklı BFS (yalnız kara karoları).</summary>
        internal static int[,] Distances(bool[,] land, List<(int q, int r)> from)
        {
            int w = land.GetLength(0), h = land.GetLength(1);
            var d = new int[w, h];
            for (int q = 0; q < w; q++) for (int r = 0; r < h; r++) d[q, r] = int.MaxValue;
            var queue = new Queue<(int q, int r)>();
            foreach (var c in from) { d[c.q, c.r] = 0; queue.Enqueue(c); }
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                for (int k = 0; k < 6; k++)
                {
                    Neighbor(c.q, c.r, k, out int nq, out int nr);
                    if (nq < 0 || nr < 0 || nq >= w || nr >= h || !land[nq, nr]) continue;
                    if (d[nq, nr] != int.MaxValue) continue;
                    d[nq, nr] = d[c.q, c.r] + 1;
                    queue.Enqueue((nq, nr));
                }
            }
            return d;
        }

        // ── Büyütme ──────────────────────────────────────────────────────────

        private static void Grow(bool[,] land, RegionPlan plan, List<(int q, int r)> cells, Result res)
        {
            int w = land.GetLength(0), h = land.GetLength(1);
            int n = plan.Regions.Length;

            // Karo maliyeti: düzgün gürültü (sınırlar dalgalı). Seed = yerleşim seed'i → her koşu farklı.
            var cost = new float[w, h];
            foreach (var (q, r) in cells)
            {
                World(q, r, out float x, out float z);
                float nz = MapNoise.Fbm(x * 0.22f, z * 0.22f, plan.LayoutSeed + 5003, 3) * 0.5f + 0.5f;
                cost[q, r] = 1f + plan.BorderNoise * nz;
            }

            float totalArea = 0f;
            foreach (var d in plan.Regions) totalArea += Math.Max(0.05f, d.Area);
            var target = new float[n];
            for (int i = 0; i < n; i++) target[i] = cells.Count * Math.Max(0.05f, plan.Regions[i].Area) / totalArea;

            var speed = new float[n];
            for (int i = 0; i < n; i++) speed[i] = 1f;

            var dist = new float[w, h];
            for (int iter = 0; iter < 6; iter++)
            {
                for (int q = 0; q < w; q++) for (int r = 0; r < h; r++) { dist[q, r] = float.MaxValue; res.Region[q, r] = -1; }

                var open = new SortedSet<(float key, int q, int r, int reg)>();
                foreach (var s in res.Sites)
                {
                    dist[s.q, s.r] = 0f;
                    res.Region[s.q, s.r] = s.region;
                    open.Add((0f, s.q, s.r, s.region));
                }

                while (open.Count > 0)
                {
                    var cur = open.Min; open.Remove(cur);
                    if (cur.key > dist[cur.q, cur.r]) continue;
                    if (res.Region[cur.q, cur.r] != cur.reg) continue;
                    for (int k = 0; k < 6; k++)
                    {
                        Neighbor(cur.q, cur.r, k, out int nq, out int nr);
                        if (nq < 0 || nr < 0 || nq >= w || nr >= h || !land[nq, nr]) continue;
                        float nd = cur.key + cost[nq, nr] / speed[cur.reg];
                        if (nd >= dist[nq, nr]) continue;
                        dist[nq, nr] = nd;
                        res.Region[nq, nr] = cur.reg;
                        open.Add((nd, nq, nr, cur.reg));
                    }
                }

                Count(res, n);
                // Alan payını hedefe yaklaştır: alan ~ hız² → hız *= √(hedef/gerçek).
                for (int i = 0; i < n; i++)
                {
                    float actual = Math.Max(1, res.Size[i]);
                    float f = (float)Math.Sqrt(target[i] / actual);
                    speed[i] *= Math.Max(0.7f, Math.Min(1.4f, f));
                }
            }
        }

        private static void Count(Result res, int n)
        {
            Array.Clear(res.Size, 0, res.Size.Length);
            int w = res.Region.GetLength(0), h = res.Region.GetLength(1);
            for (int q = 0; q < w; q++) for (int r = 0; r < h; r++)
            {
                int g = res.Region[q, r];
                if (g >= 0 && g < n) res.Size[g]++;
            }
        }

        /// <summary>Tek karoluk bölge dikenlerini (5+ komşusu başka bölge) komşu çoğunluğa verir.</summary>
        private static void Smooth(bool[,] land, Result res, int n)
        {
            int w = land.GetLength(0), h = land.GetLength(1);
            var counts = new int[n];
            var next = (int[,])res.Region.Clone();
            for (int q = 0; q < w; q++) for (int r = 0; r < h; r++)
            {
                int mine = res.Region[q, r];
                if (mine < 0) continue;
                Array.Clear(counts, 0, n);
                int same = 0, total = 0;
                for (int k = 0; k < 6; k++)
                {
                    Neighbor(q, r, k, out int nq, out int nr);
                    if (nq < 0 || nr < 0 || nq >= w || nr >= h) continue;
                    int g = res.Region[nq, nr];
                    if (g < 0) continue;
                    total++;
                    if (g == mine) same++; else counts[g]++;
                }
                if (total >= 4 && same <= 1)
                {
                    int best = mine, bestN = 0;
                    for (int i = 0; i < n; i++) if (counts[i] > bestN) { bestN = counts[i]; best = i; }
                    next[q, r] = best;
                }
            }
            // Çekirdek karoları asla el değiştirmez.
            foreach (var s in res.Sites) next[s.q, s.r] = s.region;
            Array.Copy(next, res.Region, next.Length);
            Count(res, n);
        }

        /// <summary>
        /// Bölgelerin yükseklik/nem bükmesini alanlara uygular. Bükme önce bölge bölge yazılır,
        /// sonra 2 tur komşu ortalamasıyla yumuşatılır (sınırda uçurum gibi basamak olmasın),
        /// en son yükseklik yeniden 0..1'e oturtulur (dağ/göl eşikleri anlamını korusun).
        /// </summary>
        public static void BiasFields(Result lay, RegionPlan plan, bool[,] land, float[,] elev, float[,] moist)
        {
            int w = land.GetLength(0), h = land.GetLength(1);
            var be = new float[w, h];
            var bm = new float[w, h];
            for (int q = 0; q < w; q++) for (int r = 0; r < h; r++)
            {
                int g = lay.Region[q, r];
                if (g < 0) continue;
                be[q, r] = plan.Regions[g].ElevBias;
                bm[q, r] = plan.Regions[g].MoistBias;
            }
            for (int pass = 0; pass < 2; pass++) { be = Blur(land, be); bm = Blur(land, bm); }

            float min = float.MaxValue, max = float.MinValue;
            for (int q = 0; q < w; q++) for (int r = 0; r < h; r++)
            {
                if (!land[q, r]) continue;
                elev[q, r] += be[q, r];
                moist[q, r] = Clamp01(moist[q, r] + bm[q, r]);
                if (elev[q, r] < min) min = elev[q, r];
                if (elev[q, r] > max) max = elev[q, r];
            }
            float range = Math.Max(0.0001f, max - min);
            for (int q = 0; q < w; q++) for (int r = 0; r < h; r++)
                if (land[q, r]) elev[q, r] = (elev[q, r] - min) / range;
        }

        private static float[,] Blur(bool[,] land, float[,] f)
        {
            int w = land.GetLength(0), h = land.GetLength(1);
            var o = new float[w, h];
            for (int q = 0; q < w; q++) for (int r = 0; r < h; r++)
            {
                if (!land[q, r]) continue;
                float sum = f[q, r] * 2f, wsum = 2f;
                for (int k = 0; k < 6; k++)
                {
                    Neighbor(q, r, k, out int nq, out int nr);
                    if (nq < 0 || nr < 0 || nq >= w || nr >= h || !land[nq, nr]) continue;
                    sum += f[nq, nr]; wsum += 1f;
                }
                o[q, r] = sum / wsum;
            }
            return o;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
