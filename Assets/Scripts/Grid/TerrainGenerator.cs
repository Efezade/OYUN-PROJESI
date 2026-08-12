using System;
using System.Collections.Generic;

namespace TacticalRPG.Grid
{
    /// <summary>Üretimin tüm ayarları. Sayılar koda gömülmez — <c>TerrainConfigSO</c>'dan gelir.</summary>
    public struct TerrainParams
    {
        public int Width, Height;              // tahtanın SINIRLAYICI KUTUSU (kıta bunun içine oturur)
        public int TargetLandMin, TargetLandMax;

        public float RiverPct, MountainPct, BlobPct, BridgePct;

        public int   FringeWidth;              // kıyının dışındaki dekor bandı (sis/deniz) kalınlığı
        public float CoastRoughness;           // kıyı çizgisinin gürültü katkısı (0 = elips, 0.5 = çok girintili)
        public float ShapeFrequency;           // kıta gürültüsünün frekansı (düşük = az sayıda büyük burun)
        public float WarpAmount;               // domain warp şiddeti — "gürültü" hissini siler
        public int   LandmarkCount;
        public int   LandmarkSpacing;

        /// <summary>Kıtanın tahta kenarına bırakmak ZORUNDA olduğu boşluk (karo). Sınır dekoru
        /// (sis/deniz) buraya sığmazsa kesilir ve tam da kaçındığımız DÜZ KENAR ortaya çıkar.</summary>
        public int Margin => FringeWidth + 1;

        public static TerrainParams Default => new TerrainParams
        {
            Width = 36, Height = 34,
            TargetLandMin = 500, TargetLandMax = 600,
            RiverPct = 0.049f, MountainPct = 0.075f, BlobPct = 0.089f, BridgePct = 0.004f,
            FringeWidth = 2, CoastRoughness = 0.42f, ShapeFrequency = 1.55f, WarpAmount = 0.34f,
            LandmarkCount = 12, LandmarkSpacing = 4
        };
    }

    /// <summary>Bir üretimin çıktısı: karo tablosu + doğrulama/denge için istatistikler.</summary>
    public sealed class MapResult
    {
        public string[,] Tiles;                 // [sütun, satır]; TileCatalog.Void = hücre YOK
        public (int q, int r) Start;            // oyuncunun başlayacağı karo (ana bileşen içinde)

        public int Land;                        // kıtadaki karo sayısı (sınır dekoru HARİÇ)
        public int Walkable, River, Mountain, Blob, Crossing, Landmark;
        public int MainComponent;               // başlangıçtan YÜRÜYEREK erişilebilen karo sayısı
        public int EssenceSupply;               // erişilebilir bölgedeki toplam öz
        public int Fringe;                      // dekoratif sınır karosu sayısı

        public float WalkablePct => Land > 0 ? 100f * Walkable / Land : 0f;
        public float RiverPct    => Land > 0 ? 100f * River    / Land : 0f;
        public float MountainPct => Land > 0 ? 100f * Mountain / Land : 0f;
        public float BlobPct     => Land > 0 ? 100f * Blob     / Land : 0f;
        public float CrossingPct => Land > 0 ? 100f * Crossing / Land : 0f;
        public float ReachablePct=> Walkable > 0 ? 100f * MainComponent / Walkable : 0f;
    }

    /// <summary>
    /// ORGANİK KITA ÜRETİCİSİ — bölüm haritasını gerçek coğrafya taklit eden bir boru hattıyla kurar.
    ///
    /// Eski üretici (Python portu, 22×25 DOLU DİKDÖRTGEN + rastgele lekeler) kaldırıldı: haritanın
    /// kare olması "sınır burada" duygusunu ilk bakışta veriyordu ve keşif gerilimini öldürüyordu
    /// (kullanıcı geri bildirimi, 2026-08-12). Arşiv: `Docs/Alternatif_Tasarimlar/`.
    ///
    /// Boru hattı — her adım bir öncekinin ürettiği ALANI kullanır, sıralama bilinçlidir:
    ///   1. KITA MASKESİ — domain-warp'lı fBm + eliptik taban + harmonik "burun/koy" terimi.
    ///      Eşik ikili aramayla ayarlanır → kara sayısı hedef aralığa (500-600) OTURUR.
    ///      Sonra en büyük bileşen alınır, kıyı aşındırılır/temizlenir (tek karo dikenler gider).
    ///   2. ALANLAR — yükseklik (SIRT gürültüsü + iç kesim mesafesi), nem, sıcaklık (seed'e göre
    ///      DÖNEN iklim ekseni: her harita farklı yönde ısınır → hep "kuzey karlı" olmaz).
    ///   3. DAĞLAR — yükseklik sıralamasının tepesi. Sırt gürültüsü sayesinde yuvarlak leke değil
    ///      SİLSİLE çıkar (Lynch'in "edge"i: haritayı bölgelere ayıran doğal duvar).
    ///   4. NEHİRLER — zirve altındaki kaynaklardan YOKUŞ AŞAĞI akış; denize/göle/başka nehre varınca
    ///      durur. Rastgele yürüyüş değil: bu yüzden nehir dağdan denize doğru "mantıklı" akar.
    ///   5. GÖL / SIK ORMAN BLOBLARI — çukurlarda göl, nemli alçak alanda sık orman.
    ///   6. GEÇİTLER — köprü SADECE nehri gerçekten kesen, iki yakasında kara olan karoya konur;
    ///      ayrıca kopuk kalan cepler için dağ geçidi/sığ geçit açılır (harita bitirilebilir kalsın).
    ///   7. BİYOMLAR — kalan yürünür karolar (yükseklik, nem, sıcaklık) üçlüsüne göre ~30 alt tipe
    ///      dağıtılır: ormanlar nehir kenarında, taşlık dağ eteğinde, kum sahilde toplanır.
    ///   8. LANDMARK'LAR — birbirinden uzak, göze çarpan nadir karolar (dikilitaş, harabe, dev ağaç…).
    ///      Sisli haritada yön bulmayı mümkün kılan referans noktaları.
    ///   9. SINIR DEKORU — kıyının dışına düzensiz genişlikte sığ su → derin su → SİS bandı ve birkaç
    ///      uzak adacık. Amaç: haritanın bittiği yer görünmesin, "ötesi var ama göremiyorsun" hissi.
    ///
    /// UnityEngine'e BAĞIMLI DEĞİLDİR (bilerek): seed havuzu taraması Unity açmadan, oyunda çalışan
    /// KODUN AYNISI derlenerek yapılır (`Docs/Balance/tools/seed_taramasi`).
    /// </summary>
    public static class TerrainGenerator
    {
        // Geriye dönük kısayollar (eski çağrı yerleri bunları kullanıyordu).
        public const string OvaId      = TileCatalog.Ova;
        public const string DepletedId = TileCatalog.Depleted;
        public const string VoidId     = TileCatalog.Void;

        // ── Hex komşuluk (odd-r OFFSET) ──────────────────────────────────────
        // Üretici (sütun, satır) indisli bir DİZİ üzerinde çalışır ve bu dizi tahtaya
        // HexCoordinate.FromOffset ile oturur → komşuluk OFFSET kuralına uymak ZORUNDA:
        // tek satırlar yarım karo sağa kaydığı için komşu tablosu satır PARİTESİNE bağlıdır.
        //   sıra:  sağ · sağ-üst · sol-üst · sol · sol-alt · sağ-alt
        private static readonly int[,] DirsEven = { { 1, 0 }, { 0, -1 }, { -1, -1 }, { -1, 0 }, { -1, 1 }, { 0, 1 } };
        private static readonly int[,] DirsOdd  = { { 1, 0 }, { 1, -1 }, {  0, -1 }, { -1, 0 }, {  0, 1 }, { 1, 1 } };

        private static void Neighbor(int q, int r, int d, out int nq, out int nr)
        {
            int[,] t = (r & 1) == 0 ? DirsEven : DirsOdd;
            nq = q + t[d, 0];
            nr = r + t[d, 1];
        }

        private static bool InBounds(int q, int r, int w, int h) => q >= 0 && q < w && r >= 0 && r < h;

        /// <summary>odd-r offset indisinin dünya düzlemindeki yeri (pointy-top). Şekil matematiği
        /// BURADAN gitmeli: dizi indisiyle çalışılırsa kıta yamuk/eğik çıkar.</summary>
        private static void World(int q, int r, out float x, out float z)
        {
            x = 1.7320508f * (q + 0.5f * (r & 1));
            z = 1.5f * r;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  ANA GİRİŞ
        // ═════════════════════════════════════════════════════════════════════

        public static MapResult Generate(in TerrainParams p, int seed)
        {
            int w = p.Width, h = p.Height;
            var rnd = new PythonRandom(seed);

            var tiles = new string[w, h];
            for (int q = 0; q < w; q++)
                for (int r = 0; r < h; r++)
                    tiles[q, r] = TileCatalog.Void;

            // 1) KITA MASKESİ
            bool[,] land = BuildLandmass(p, seed, out int landCount);
            if (landCount == 0) return new MapResult { Tiles = tiles, Start = (0, 0) };

            // 2) ALANLAR
            BuildFields(p, seed, land, out float[,] elev, out float[,] moist, out float[,] temp,
                        out int[,] coastDist);

            // Karo yerleşimi: null = "henüz boş kara" (biyom adımında doldurulacak)
            var t = new string[w, h];

            // 3) DAĞLAR
            int nMountain = (int)Math.Round(landCount * p.MountainPct);
            PlaceMountains(land, elev, temp, moist, t, nMountain);

            // 4) NEHİRLER (köprüye ayrılacak karolar dahil üretilir)
            int nBridge = Math.Max(1, (int)Math.Round(landCount * p.BridgePct));
            int nRiver  = (int)Math.Round(landCount * p.RiverPct);
            PlaceRivers(land, elev, t, rnd, nRiver + nBridge);
            TopUpMountains(land, elev, temp, moist, t, nMountain);   // nehrin yediği dağları geri koy

            // 5) GÖL + SIK ORMAN BLOBLARI
            int nBlob = (int)Math.Round(landCount * p.BlobPct);
            PlaceBlobs(land, elev, moist, temp, t, rnd, nBlob);

            // 6) GEÇİTLER (köprü/sığ geçit/dağ geçidi) + bağlantı onarımı
            PlaceCrossings(land, t, nBridge);
            RepairConnectivity(land, t);

            // 7) BİYOMLAR — kalan boş kara karoları
            AssignBiomes(land, elev, moist, temp, coastDist, t, seed);

            // 8) LANDMARK'LAR
            int landmarks = PlaceLandmarks(land, t, elev, coastDist, rnd, p.LandmarkCount, p.LandmarkSpacing);

            // Tahtaya yaz
            for (int q = 0; q < w; q++)
                for (int r = 0; r < h; r++)
                    if (land[q, r] && t[q, r] != null) tiles[q, r] = t[q, r];

            // 9) SINIR DEKORU (istatistik dışı)
            int fringe = PlaceFringe(p, seed, land, tiles);

            // ── Sonuç + istatistik ──
            var res = new MapResult { Tiles = tiles, Land = landCount, Fringe = fringe };
            foreach (var (q, r) in Cells(w, h))
            {
                if (!land[q, r]) continue;
                var e = TileCatalog.Get(tiles[q, r]);
                if (e == null) continue;
                switch (e.Family)
                {
                    case TileFamily.River:    res.River++;    break;
                    case TileFamily.Mountain: res.Mountain++; break;
                    case TileFamily.Blob:     res.Blob++;     break;
                    case TileFamily.Crossing: res.Crossing++; break;
                    case TileFamily.Landmark: res.Landmark++; break;
                }
                if (e.Walkable) res.Walkable++;
            }
            if (landmarks != res.Landmark) res.Landmark = landmarks;

            res.Start = PickStart(land, tiles, elev);
            var comp  = ConnectedComponent(tiles, res.Start.q, res.Start.r, out res.Start);
            res.MainComponent = comp.Count;
            foreach (var c in comp)
            {
                TileCatalog.EssenceOf(tiles[c.q, c.r], out int amt, out _);
                res.EssenceSupply += amt;
            }
            return res;
        }

        private static IEnumerable<(int q, int r)> Cells(int w, int h)
        {
            for (int q = 0; q < w; q++)
                for (int r = 0; r < h; r++)
                    yield return (q, r);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  1) KITA MASKESİ
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>Organik kıta silueti. Üç katman toplanır:
        /// (a) eliptik taban — kenarlara doğru azalan "kara olma isteği" (tahta kenarına yapışmasın),
        /// (b) harmonik burun/koy terimi — 2-4 dalgalı polar bozulma: yarımadalar ve körfezler,
        /// (c) domain-warp'lı fBm — küçük ölçekli girinti/çıkıntı (fraktal kıyı).
        /// Eşik ikili aramayla hedef karo sayısına ayarlanır.</summary>
        private static bool[,] BuildLandmass(in TerrainParams p, int seed, out int count)
        {
            int w = p.Width, h = p.Height;
            var score = new float[w, h];

            World(w - 1, h - 1, out float maxX, out float maxZ);
            float cx = maxX * 0.5f, cz = maxZ * 0.5f;
            // Kıtanın ana ekseni seed'e göre döner + basıklığı değişir → her harita farklı "kıta tipi"
            // (yuvarlak kıta, uzun ada, çapraz yatan kütle…). Dar aralık bırakılırsa 30 haritanın
            // hepsi aynı silueti alır ve havuz sıkıcı olur.
            float aspect = 0.62f + MapNoise.White(7, 13, seed) * 0.95f;      // 0.62 – 1.57
            float rot    = MapNoise.White(3, 29, seed) * 6.2831853f;
            float rx = cx * 0.94f * aspect, rz = cz * 0.94f / aspect;

            // Harmonik burun/koy terimi: 3 farklı dalga sayısı, seed'e göre faz/genlik.
            // BU, "daire gibi ada" ile "yarımadalı kıta" arasındaki farkı yaratan asıl terim.
            int   k1 = 2 + (int)(MapNoise.White(11, 5, seed) * 3f);          // 2–4 büyük burun
            int   k2 = 5 + (int)(MapNoise.White(19, 2, seed) * 4f);          // 5–8 orta girinti
            int   k3 = 9 + (int)(MapNoise.White(43, 23, seed) * 5f);         // 9–13 ince tırtık
            float a1 = 0.17f + MapNoise.White(23, 7, seed) * 0.26f;
            float a2 = 0.06f + MapNoise.White(31, 3, seed) * 0.12f;
            float a3 = 0.02f + MapNoise.White(47, 19, seed) * 0.05f;
            float ph1 = MapNoise.White(37, 11, seed) * 6.2831853f;
            float ph2 = MapNoise.White(41, 17, seed) * 6.2831853f;
            float ph3 = MapNoise.White(53, 13, seed) * 6.2831853f;

            for (int q = 0; q < w; q++)
                for (int r = 0; r < h; r++)
                {
                    World(q, r, out float x, out float z);
                    float dx = (x - cx) / rx, dz = (z - cz) / rz;

                    // eksen dönüşü
                    float cs = (float)Math.Cos(rot), sn = (float)Math.Sin(rot);
                    float ux = dx * cs - dz * sn, uz = dx * sn + dz * cs;

                    float dist  = (float)Math.Sqrt(ux * ux + uz * uz);
                    float theta = (float)Math.Atan2(uz, ux);

                    float lobes = a1 * (float)Math.Cos(k1 * theta + ph1)
                                + a2 * (float)Math.Cos(k2 * theta + ph2)
                                + a3 * (float)Math.Cos(k3 * theta + ph3);

                    float nx = ux * p.ShapeFrequency, nz = uz * p.ShapeFrequency;
                    MapNoise.Warp(ref nx, ref nz, seed, p.WarpAmount * 2.2f, 0.75f);
                    float n = MapNoise.Fbm(nx, nz, seed, 5);

                    // (1 - dist) merkezde 1, elips kenarında 0. Kenara doğru sert düşüş (^1.35)
                    // tahtanın dışına taşmayı engeller ama kıyıyı düzleştirmez.
                    float baseVal = 1f - (float)Math.Pow(dist, 1.35f);
                    score[q, r] = baseVal + lobes + n * p.CoastRoughness;
                }

            // Eşiği ikili aramayla hedef karo sayısına ayarla.
            float lo = -1.2f, hi = 1.4f;
            bool[,] best = null; int bestCount = 0; float bestErr = float.MaxValue;
            int target = (p.TargetLandMin + p.TargetLandMax) / 2;

            int margin = p.Margin;
            for (int it = 0; it < 22; it++)
            {
                float mid = (lo + hi) * 0.5f;
                bool[,] mask = Threshold(score, mid, w, h, margin);
                CleanCoastline(mask, w, h);
                int c = KeepLargest(mask, w, h);

                int err = Math.Abs(c - target);
                if (err < bestErr) { bestErr = err; best = mask; bestCount = c; }
                if (c > target) lo = mid; else hi = mid;   // eşik ↑ → kara ↓
            }

            // Hâlâ aralık dışındaysa kıyıyı aşındırarak/büyüterek tam oturt.
            if (best != null && (bestCount < p.TargetLandMin || bestCount > p.TargetLandMax))
                bestCount = FitCount(best, w, h, seed, margin, p.TargetLandMin, p.TargetLandMax);

            count = bestCount;
            return best ?? new bool[w, h];
        }

        /// <summary>Eşikleme + zorunlu kenar boşluğu. Kara tahtanın kenarına ASLA değmez: değseydi
        /// hem kıyı düz kesilirdi hem de dışına sis/deniz bandı sığmazdı (=görünür düz sınır).</summary>
        private static bool[,] Threshold(float[,] score, float t, int w, int h, int margin)
        {
            var m = new bool[w, h];
            for (int q = margin; q < w - margin; q++)
                for (int r = margin; r < h - margin; r++)
                    m[q, r] = score[q, r] > t;
            return m;
        }

        private static int CountNbr(bool[,] m, int q, int r, int w, int h)
        {
            int n = 0;
            for (int d = 0; d < 6; d++)
            {
                Neighbor(q, r, d, out int nq, out int nr);
                if (InBounds(nq, nr, w, h) && m[nq, nr]) n++;
            }
            return n;
        }

        /// <summary>Tek karoluk diken/delikleri temizler ama kıyıyı DÜZLEŞTİRMEZ (hücresel otomat).</summary>
        private static void CleanCoastline(bool[,] m, int w, int h)
        {
            // Sadece 2 tur ve SIKI eşikler: amaç kıyıyı düzeltmek DEĞİL, sadece tek karoluk
            // saçmalıkları (havada asılı karo, iğne deliği göl) elemek. Eşik gevşetilirse
            // (ör. n>=5 doldur) körfezler kapanır ve ada gitgide daireye döner.
            for (int pass = 0; pass < 2; pass++)
            {
                var next = (bool[,])m.Clone();
                for (int q = 1; q < w - 1; q++)
                    for (int r = 1; r < h - 1; r++)
                    {
                        int n = CountNbr(m, q, r, w, h);
                        if (m[q, r]) { if (n <= 1) next[q, r] = false; }   // yalnız diken → sil
                        else         { if (n >= 6) next[q, r] = true;  }   // TAM çevrili delik → doldur
                    }
                Array.Copy(next, m, next.Length);
            }
        }

        /// <summary>Yalnız en büyük kara bileşenini bırakır (kopuk adacıklar dekora devredilir).</summary>
        private static int KeepLargest(bool[,] m, int w, int h)
        {
            var seen = new bool[w, h];
            List<(int q, int r)> best = null;
            var stack = new Stack<(int q, int r)>();

            foreach (var (q, r) in Cells(w, h))
            {
                if (!m[q, r] || seen[q, r]) continue;
                var comp = new List<(int q, int r)>();
                stack.Push((q, r)); seen[q, r] = true;
                while (stack.Count > 0)
                {
                    var c = stack.Pop(); comp.Add(c);
                    for (int d = 0; d < 6; d++)
                    {
                        Neighbor(c.q, c.r, d, out int nq, out int nr);
                        if (InBounds(nq, nr, w, h) && m[nq, nr] && !seen[nq, nr])
                        { seen[nq, nr] = true; stack.Push((nq, nr)); }
                    }
                }
                if (best == null || comp.Count > best.Count) best = comp;
            }

            if (best == null) return 0;
            var keep = new HashSet<(int q, int r)>(best);
            foreach (var (q, r) in Cells(w, h))
                if (m[q, r] && !keep.Contains((q, r))) m[q, r] = false;
            return best.Count;
        }

        /// <summary>Hedef aralığa oturtmak için kıyıyı aşındırır/büyütür. TUR TUR çalışır: her turda
        /// kıyı adayları bir kez taranıp sıralanır, sonra gerekli kadarı uygulanır. Aday sırasına
        /// gürültü karıştığı için kıyı düzleşmez — tersine daha girintili olur.</summary>
        private static int FitCount(bool[,] m, int w, int h, int seed, int margin, int min, int max)
        {
            int count = 0;
            foreach (var (q, r) in Cells(w, h)) if (m[q, r]) count++;

            for (int round = 0; round < 12 && (count < min || count > max); round++)
            {
                bool grow = count < min;
                int need = grow ? min - count : count - max;

                var cands = new List<((int q, int r) c, float key)>();
                for (int q = margin; q < w - margin; q++)
                    for (int r = margin; r < h - margin; r++)
                    {
                        if (m[q, r] == grow) continue;                 // aday değil
                        int n = CountNbr(m, q, r, w, h);
                        if (grow) { if (n < 3) continue; }             // körfezleri doldur
                        else      { if (n > 3 || n == 0) continue; }   // burunları aşındır
                        cands.Add(((q, r), (grow ? -n : n) + MapNoise.White(q, r, seed) * 0.9f));
                    }
                if (cands.Count == 0) break;
                cands.Sort((a, b) => a.key.CompareTo(b.key));

                int applied = 0;
                foreach (var cand in cands)
                {
                    if (applied >= need) break;
                    var (q, r) = cand.c;
                    if (m[q, r] == grow) continue;                     // bu turda komşusu değiştiyse
                    int n = CountNbr(m, q, r, w, h);
                    if (grow) { if (n < 3) continue; } else { if (n > 3 || n == 0) continue; }
                    m[q, r] = grow;
                    applied++;
                }
                if (applied == 0) break;
                count += grow ? applied : -applied;
            }
            return KeepLargest(m, w, h);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  2) ALANLAR: yükseklik / nem / sıcaklık
        // ═════════════════════════════════════════════════════════════════════

        private static void BuildFields(in TerrainParams p, int seed, bool[,] land,
                                        out float[,] elev, out float[,] moist, out float[,] temp,
                                        out int[,] coastDist)
        {
            int w = p.Width, h = p.Height;
            elev  = new float[w, h];
            moist = new float[w, h];
            temp  = new float[w, h];

            // Kıyıdan içeri mesafe — hem yükseklik (iç kesim yüksek) hem nem (kıyı nemli) için.
            coastDist = CoastDistance(land, w, h);

            // İklim ekseni seed'e göre DÖNER: bir haritada kuzey, ötekinde güneybatı soğuk olur.
            float climAngle = MapNoise.White(101, 53, seed) * 6.2831853f;
            float ca = (float)Math.Cos(climAngle), sa = (float)Math.Sin(climAngle);
            World(w - 1, h - 1, out float maxX, out float maxZ);
            float cx = maxX * 0.5f, cz = maxZ * 0.5f;
            float span = (float)Math.Sqrt(cx * cx + cz * cz);

            float maxElev = 0.0001f, minElev = 1f;

            for (int q = 0; q < w; q++)
                for (int r = 0; r < h; r++)
                {
                    if (!land[q, r]) continue;
                    World(q, r, out float x, out float z);
                    float nx = (x - cx) / span, nz = (z - cz) / span;

                    // SIRT gürültüsü = çizgisel dağ silsileleri
                    float rx = nx * 2.1f, rz = nz * 2.1f;
                    MapNoise.Warp(ref rx, ref rz, seed + 77, 0.28f, 1.1f);
                    float ridge = MapNoise.Ridged(rx, rz, seed + 77, 4);

                    // İç kesim yükseltir (kıyı ovası → iç yayla)
                    float inland = Math.Min(1f, coastDist[q, r] / 7f);

                    float e = 0.58f * ridge + 0.42f * inland
                            + 0.10f * MapNoise.Fbm(nx * 5.5f, nz * 5.5f, seed + 313, 3);
                    elev[q, r] = e;
                    if (e > maxElev) maxElev = e;
                    if (e < minElev) minElev = e;

                    // Nem: büyük ölçekli gürültü + kıyıya yakınlık
                    float m = 0.55f * (MapNoise.Fbm(nx * 2.6f, nz * 2.6f, seed + 911, 4) * 0.5f + 0.5f)
                            + 0.45f * (1f - Math.Min(1f, coastDist[q, r] / 9f));
                    moist[q, r] = m;

                    // Sıcaklık: dönen iklim ekseni − yükseklik + küçük gürültü
                    float lat = ((x - cx) * ca + (z - cz) * sa) / span;   // −1 … +1
                    temp[q, r] = 0.5f + 0.42f * lat
                               + 0.14f * MapNoise.Fbm(nx * 3.1f, nz * 3.1f, seed + 1777, 3);
                }

            // Yüksekliği 0..1'e normalize et (eşikler seed'den bağımsız anlam kazansın).
            float range = Math.Max(0.0001f, maxElev - minElev);
            for (int q = 0; q < w; q++)
                for (int r = 0; r < h; r++)
                {
                    if (!land[q, r]) continue;
                    elev[q, r] = (elev[q, r] - minElev) / range;
                    temp[q, r] = Clamp01(temp[q, r] - 0.35f * elev[q, r]);   // yükseklik soğutur
                    moist[q, r] = Clamp01(moist[q, r]);
                }
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        /// <summary>Her kara karosunun kıyıya (denize) hex mesafesi — çok kaynaklı BFS.</summary>
        private static int[,] CoastDistance(bool[,] land, int w, int h)
        {
            var dist = new int[w, h];
            var queue = new Queue<(int q, int r)>();
            foreach (var (q, r) in Cells(w, h))
            {
                dist[q, r] = land[q, r] ? int.MaxValue : 0;
                if (!land[q, r]) queue.Enqueue((q, r));
            }
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                for (int d = 0; d < 6; d++)
                {
                    Neighbor(c.q, c.r, d, out int nq, out int nr);
                    if (!InBounds(nq, nr, w, h) || dist[nq, nr] != int.MaxValue) continue;
                    dist[nq, nr] = dist[c.q, c.r] + 1;
                    queue.Enqueue((nq, nr));
                }
            }
            return dist;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  3) DAĞLAR
        // ═════════════════════════════════════════════════════════════════════

        private static void PlaceMountains(bool[,] land, float[,] elev, float[,] temp, float[,] moist,
                                           string[,] t, int count)
        {
            int w = land.GetLength(0), h = land.GetLength(1);
            var cand = new List<(int q, int r)>();
            foreach (var (q, r) in Cells(w, h)) if (land[q, r]) cand.Add((q, r));
            cand.Sort((a, b) => elev[b.q, b.r].CompareTo(elev[a.q, a.r]));

            for (int i = 0; i < count && i < cand.Count; i++)
                t[cand[i].q, cand[i].r] = TileCatalog.Dag;   // alt tip aşağıda belirlenir

            RefineMountains(land, elev, temp, moist, t);
        }

        /// <summary>Nehir bazı dağ karolarını yediyse eksiği sıradaki en yüksek karolarla tamamlar.</summary>
        private static void TopUpMountains(bool[,] land, float[,] elev, float[,] temp, float[,] moist,
                                           string[,] t, int target)
        {
            int w = land.GetLength(0), h = land.GetLength(1);
            int have = 0;
            var cand = new List<(int q, int r)>();
            foreach (var (q, r) in Cells(w, h))
            {
                if (!land[q, r]) continue;
                var e = TileCatalog.Get(t[q, r]);
                if (e != null && e.Family == TileFamily.Mountain) have++;
                else if (t[q, r] == null) cand.Add((q, r));
            }
            if (have >= target) return;

            cand.Sort((a, b) => elev[b.q, b.r].CompareTo(elev[a.q, a.r]));
            for (int i = 0; i < cand.Count && have < target; i++, have++)
                t[cand[i].q, cand[i].r] = TileCatalog.Dag;

            RefineMountains(land, elev, temp, moist, t);
        }

        /// <summary>Dağ karolarını iklime/konuma göre alt tiplere ayırır: silsilenin İÇİ zirve,
        /// KENARI kayalık/uçurum; soğukta karlı zirve, sıcak+kuruda volkanik kaya.</summary>
        private static void RefineMountains(bool[,] land, float[,] elev, float[,] temp, float[,] moist,
                                            string[,] t)
        {
            int w = land.GetLength(0), h = land.GetLength(1);
            foreach (var (q, r) in Cells(w, h))
            {
                var e = TileCatalog.Get(t[q, r]);
                if (e == null || e.Family != TileFamily.Mountain) continue;

                int inner = 0;
                for (int d = 0; d < 6; d++)
                {
                    Neighbor(q, r, d, out int nq, out int nr);
                    if (!InBounds(nq, nr, w, h)) continue;
                    var ne = TileCatalog.Get(t[nq, nr]);
                    if (ne != null && ne.Family == TileFamily.Mountain) inner++;
                }

                string id;
                if (inner >= 4 && temp[q, r] < 0.42f)      id = TileCatalog.YuksekDag;
                else if (inner >= 5)                        id = TileCatalog.YuksekDag;
                else if (temp[q, r] > 0.70f && moist[q, r] < 0.38f) id = TileCatalog.VolkanikKaya;
                else if (inner <= 1)
                {
                    float k = MapNoise.White(q, r, 4242);
                    id = k < 0.34f ? TileCatalog.DevKaya
                       : k < 0.62f ? TileCatalog.Kayalik
                       : (temp[q, r] > 0.62f ? TileCatalog.SutunKaya : TileCatalog.Ucurum);
                }
                else id = MapNoise.White(q, r, 909) < 0.62f ? TileCatalog.Dag : TileCatalog.Kayalik;

                t[q, r] = id;
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  4) NEHİRLER — yokuş aşağı akış
        // ═════════════════════════════════════════════════════════════════════

        private static void PlaceRivers(bool[,] land, float[,] elev, string[,] t,
                                        PythonRandom rnd, int targetCells)
        {
            int w = land.GetLength(0), h = land.GetLength(1);
            if (targetCells <= 0) return;

            // Kaynak adayları: zirvenin hemen ALTI (dağın kendisi değil) — su oradan doğar.
            var springs = new List<(int q, int r)>();
            foreach (var (q, r) in Cells(w, h))
            {
                if (!land[q, r]) continue;
                var e = TileCatalog.Get(t[q, r]);
                if (e != null && e.Family == TileFamily.Mountain) continue;
                if (elev[q, r] < 0.55f) continue;
                springs.Add((q, r));
            }
            // Kaynak adayı hiç yoksa (kıta tamamen alçak/dağla kaplı — nadir ama olabiliyor,
            // ör. seed 4853) nehir üretilmez; boş listede GetRange çağırmak çökerdi.
            if (springs.Count == 0) return;

            springs.Sort((a, b) => elev[b.q, b.r].CompareTo(elev[a.q, a.r]));
            // En yüksek yarıyı karıştır → hep aynı tepeden başlamasın ama hep yüksekten başlasın.
            int half = Math.Max(1, springs.Count / 2);
            var pool = springs.GetRange(0, Math.Min(half, springs.Count));
            rnd.Shuffle(pool);

            var used = new bool[w, h];
            int placed = 0;

            foreach (var spring in pool)
            {
                if (placed >= targetCells) break;
                if (used[spring.q, spring.r]) continue;
                // Kaynaklar birbirine yapışmasın (aynı vadide 3 nehir olmasın).
                if (NearRiver(t, spring.q, spring.r, w, h, 3)) continue;

                var path = new List<(int q, int r)>();
                var cur = spring;
                var visited = new HashSet<(int q, int r)> { cur };

                for (int step = 0; step < 90; step++)
                {
                    path.Add(cur);
                    used[cur.q, cur.r] = true;

                    // Denize ulaştı mı?
                    bool atSea = false;
                    (int q, int r) next = (-1, -1);
                    float bestE = float.MaxValue;

                    for (int d = 0; d < 6; d++)
                    {
                        Neighbor(cur.q, cur.r, d, out int nq, out int nr);
                        if (!InBounds(nq, nr, w, h)) { atSea = true; continue; }
                        if (!land[nq, nr]) { atSea = true; continue; }
                        if (visited.Contains((nq, nr))) continue;

                        var ne = TileCatalog.Get(t[nq, nr]);
                        float pen = 0f;
                        if (ne != null && ne.Family == TileFamily.Mountain) pen = 0.30f;  // dağı tercih etme
                        // Küçük gürültü: iki komşu eşitse hep aynı yöne gitmesin (menderes).
                        float jitter = MapNoise.White(nq, nr, 2718) * 0.035f;
                        float score = elev[nq, nr] + pen + jitter;
                        if (score < bestE) { bestE = score; next = (nq, nr); }
                    }

                    if (atSea) break;                               // kıyıya vardı, bitti
                    if (next.q < 0) break;                          // çukur — burada göl olur
                    var nEnt = TileCatalog.Get(t[next.q, next.r]);
                    if (nEnt != null && nEnt.Family == TileFamily.River) { path.Add(next); break; }  // birleşti

                    visited.Add(next);
                    cur = next;
                }

                if (path.Count < 3) continue;                       // çok kısa akış = nehir değil

                for (int i = 0; i < path.Count && placed < targetCells; i++)
                {
                    var c = path[i];
                    var ex = TileCatalog.Get(t[c.q, c.r]);
                    if (ex != null && ex.Family == TileFamily.River) continue;
                    // Dik düşüş → şelale (görsel çeşitlilik + "burası özel" işareti)
                    bool steep = i > 0 && (elev[path[i - 1].q, path[i - 1].r] - elev[c.q, c.r]) > 0.085f;
                    t[c.q, c.r] = steep ? TileCatalog.Sellale : TileCatalog.Nehir;
                    placed++;
                }
            }
        }

        private static bool NearRiver(string[,] t, int q, int r, int w, int h, int radius)
        {
            for (int dq = -radius; dq <= radius; dq++)
                for (int dr = -radius; dr <= radius; dr++)
                {
                    int nq = q + dq, nr = r + dr;
                    if (!InBounds(nq, nr, w, h)) continue;
                    var e = TileCatalog.Get(t[nq, nr]);
                    if (e != null && e.Family == TileFamily.River) return true;
                }
            return false;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  5) GÖL + SIK ORMAN BLOBLARI
        // ═════════════════════════════════════════════════════════════════════

        private static void PlaceBlobs(bool[,] land, float[,] elev, float[,] moist, float[,] temp,
                                       string[,] t, PythonRandom rnd, int target)
        {
            int w = land.GetLength(0), h = land.GetLength(1);
            if (target <= 0) return;

            // Göl adayları: alçak + nemli çukurlar. Orman adayları: nemli, orta yükseklik.
            var lakeSeeds   = new List<(int q, int r)>();
            var forestSeeds = new List<(int q, int r)>();
            foreach (var (q, r) in Cells(w, h))
            {
                if (!land[q, r] || t[q, r] != null) continue;
                if (elev[q, r] < 0.34f && moist[q, r] > 0.52f) lakeSeeds.Add((q, r));
                if (moist[q, r] > 0.55f && elev[q, r] > 0.22f) forestSeeds.Add((q, r));
            }
            rnd.Shuffle(lakeSeeds);
            rnd.Shuffle(forestSeeds);

            int placed = 0;
            int lakeQuota = (int)(target * 0.38f);
            int li = 0, fi = 0;

            while (placed < target && (li < lakeSeeds.Count || fi < forestSeeds.Count))
            {
                bool wantLake = placed < lakeQuota && li < lakeSeeds.Count;
                if (!wantLake && fi >= forestSeeds.Count) wantLake = li < lakeSeeds.Count;
                if (wantLake && li >= lakeSeeds.Count) wantLake = false;

                (int q, int r) seed = wantLake ? lakeSeeds[li++] : forestSeeds[fi++];
                if (t[seed.q, seed.r] != null) continue;

                int size = wantLake ? rnd.RandInt(4, 13) : rnd.RandInt(6, 20);
                size = Math.Min(size, target - placed);
                if (size < 3) break;

                string id = wantLake ? PickLakeId(temp[seed.q, seed.r], moist[seed.q, seed.r], seed)
                                     : PickForestId(temp[seed.q, seed.r], moist[seed.q, seed.r], seed);

                placed += GrowBlob(land, t, rnd, seed, size, id);
            }
        }

        private static string PickLakeId(float temp, float moist, (int q, int r) c)
        {
            if (temp < 0.30f) return TileCatalog.BuzGolu;
            if (moist > 0.80f && temp > 0.45f) return TileCatalog.BataklikGolu;
            if (MapNoise.White(c.q, c.r, 555) < 0.12f) return TileCatalog.KaynakGolu;
            return TileCatalog.Gol;
        }

        private static string PickForestId(float temp, float moist, (int q, int r) c)
        {
            if (moist < 0.45f) return TileCatalog.DikenliCalilik;
            float k = MapNoise.White(c.q, c.r, 777);
            if (moist > 0.78f && k < 0.35f) return TileCatalog.KadimOrman;
            if (temp < 0.40f || k < 0.30f)  return TileCatalog.KaranlikOrman;
            return TileCatalog.SikOrman;
        }

        /// <summary>Rastgele-cepheli BFS büyütme: dairesel değil, amipsi lekeler üretir.</summary>
        private static int GrowBlob(bool[,] land, string[,] t, PythonRandom rnd,
                                    (int q, int r) seed, int size, string id)
        {
            int w = land.GetLength(0), h = land.GetLength(1);
            var blob = new List<(int q, int r)> { seed };
            var inBlob = new HashSet<(int q, int r)> { seed };
            var frontier = new List<(int q, int r)> { seed };

            while (frontier.Count > 0 && blob.Count < size)
            {
                int idx = rnd.RandRange(frontier.Count);
                var cur = frontier[idx];
                frontier.RemoveAt(idx);

                var nbs = new List<(int q, int r)>();
                for (int d = 0; d < 6; d++)
                {
                    Neighbor(cur.q, cur.r, d, out int nq, out int nr);
                    if (!InBounds(nq, nr, w, h) || !land[nq, nr]) continue;
                    if (t[nq, nr] != null || inBlob.Contains((nq, nr))) continue;
                    nbs.Add((nq, nr));
                }
                rnd.Shuffle(nbs);

                int take = Math.Min(2, nbs.Count);
                for (int i = 0; i < take && blob.Count < size; i++)
                {
                    blob.Add(nbs[i]); inBlob.Add(nbs[i]); frontier.Add(nbs[i]);
                }
            }

            foreach (var c in blob) t[c.q, c.r] = id;
            return blob.Count;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  6) GEÇİTLER — köprü YALNIZCA gerçek nehir geçişinde
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>Köprü/sığ geçit yerleştirir. Aday: iki KARŞIT yanında yürünür kara olan nehir
        /// karosu (yani gerçekten "karşıya geçiren" yer). Aralarından, köprü olmasa yürüyerek
        /// dolaşmanın EN UZUN süreceği yerler seçilir → köprü rastgele değil, ANLAMLI kısayol.</summary>
        private static int PlaceCrossings(bool[,] land, string[,] t, int count)
        {
            int w = land.GetLength(0), h = land.GetLength(1);
            if (count <= 0) return 0;

            var cands = new List<((int q, int r) cell, int detour, int a, int b)>();
            foreach (var (q, r) in Cells(w, h))
            {
                var e = TileCatalog.Get(t[q, r]);
                if (e == null || e.Family != TileFamily.River) continue;

                // Karşıt yön çiftleri: (0,3) (1,4) (2,5)
                for (int d = 0; d < 3; d++)
                {
                    Neighbor(q, r, d,     out int aq, out int ar);
                    Neighbor(q, r, d + 3, out int bq, out int br);
                    if (!InBounds(aq, ar, w, h) || !InBounds(bq, br, w, h)) continue;
                    if (!IsWalkableCell(land, t, aq, ar) || !IsWalkableCell(land, t, bq, br)) continue;

                    int detour = WalkDistance(land, t, (aq, ar), (bq, br), 46);
                    cands.Add(((q, r), detour, d, d + 3));
                    break;
                }
            }
            if (cands.Count == 0) return 0;

            // Dolaşma mesafesi büyük olan (ya da hiç ulaşılamayan) geçitler önce.
            cands.Sort((x, y) => y.detour.CompareTo(x.detour));

            int placed = 0;
            var takenNear = new List<(int q, int r)>();
            foreach (var c in cands)
            {
                if (placed >= count) break;
                bool tooClose = false;
                foreach (var tk in takenNear)
                    if (HexDist(tk, c.cell) < 5) { tooClose = true; break; }
                if (tooClose) continue;

                // Geçitlerin ~üçte biri sığ geçit (tahta köprü her yerde olmaz — doğal sığlık).
                t[c.cell.q, c.cell.r] = (MapNoise.White(c.cell.q, c.cell.r, 31337) < 0.34f)
                    ? TileCatalog.SigGecit : TileCatalog.Kopru;
                takenNear.Add(c.cell);
                placed++;
            }
            return placed;
        }

        private static int HexDist((int q, int r) a, (int q, int r) b)
        {
            // offset → axial → küp mesafesi
            int aq = a.q - (a.r >> 1), ar = a.r;
            int bq = b.q - (b.r >> 1), br = b.r;
            int dq = aq - bq, dr = ar - br;
            return (Math.Abs(dq) + Math.Abs(dr) + Math.Abs(dq + dr)) / 2;
        }

        private static bool IsWalkableCell(bool[,] land, string[,] t, int q, int r)
        {
            if (!land[q, r]) return false;
            if (t[q, r] == null) return true;                 // henüz biyom atanmamış = yürünür olacak
            return TileCatalog.IsWalkable(t[q, r]);
        }

        /// <summary>İki yürünür karo arası yürüme mesafesi (limitli BFS). Ulaşılamazsa limit+1.</summary>
        private static int WalkDistance(bool[,] land, string[,] t, (int q, int r) from, (int q, int r) to, int limit)
        {
            int w = land.GetLength(0), h = land.GetLength(1);
            var dist = new Dictionary<(int q, int r), int> { [from] = 0 };
            var queue = new Queue<(int q, int r)>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                int d0 = dist[c];
                if (c.q == to.q && c.r == to.r) return d0;
                if (d0 >= limit) continue;
                for (int d = 0; d < 6; d++)
                {
                    Neighbor(c.q, c.r, d, out int nq, out int nr);
                    if (!InBounds(nq, nr, w, h) || !IsWalkableCell(land, t, nq, nr)) continue;
                    if (dist.ContainsKey((nq, nr))) continue;
                    dist[(nq, nr)] = d0 + 1;
                    queue.Enqueue((nq, nr));
                }
            }
            return limit + 1;
        }

        /// <summary>Dağ/göl/nehir arkasında kalmış BÜYÜK cepleri ana kütleye bağlar: en kısa
        /// "kazma" yolunu bulur ve karo tipine göre dağ geçidi / köprü / açıklık açar.
        /// Küçük cepler (&lt; 4 karo) bilerek bırakılır — haritada erişilmez köşe olması gizem katar,
        /// ama oyunu bitirilemez yapan büyük kopukluk kalmaz.</summary>
        private static int RepairConnectivity(bool[,] land, string[,] t)
        {
            int w = land.GetLength(0), h = land.GetLength(1);
            int opened = 0;

            for (int guard = 0; guard < 12; guard++)
            {
                var comps = WalkableComponents(land, t);
                if (comps.Count <= 1) break;
                comps.Sort((a, b) => b.Count.CompareTo(a.Count));
                var main = comps[0];
                var mainSet = new HashSet<(int q, int r)>(main);

                bool didWork = false;
                for (int i = 1; i < comps.Count; i++)
                {
                    if (comps[i].Count < 4) continue;
                    var path = CarvePath(land, t, comps[i], mainSet);
                    if (path == null) continue;
                    foreach (var c in path)
                    {
                        var e = TileCatalog.Get(t[c.q, c.r]);
                        if (e == null) continue;
                        t[c.q, c.r] = e.Family switch
                        {
                            TileFamily.River    => TileCatalog.Kopru,
                            TileFamily.Mountain => TileCatalog.DagGecidi,
                            TileFamily.Blob     => e.Surface == Surface.Water || e.Surface == Surface.Ice
                                                    || e.Surface == Surface.Swamp
                                                       ? TileCatalog.SigGecit
                                                       : TileCatalog.AzAgacliOva,   // ormanda açıklık
                            _ => t[c.q, c.r]
                        };
                        opened++;
                    }
                    didWork = true;
                    break;                       // her turda tek cep; bileşenler yeniden hesaplanır
                }
                if (!didWork) break;
            }
            return opened;
        }

        private static List<List<(int q, int r)>> WalkableComponents(bool[,] land, string[,] t)
        {
            int w = land.GetLength(0), h = land.GetLength(1);
            var seen = new bool[w, h];
            var res = new List<List<(int q, int r)>>();
            var stack = new Stack<(int q, int r)>();

            foreach (var (q, r) in Cells(w, h))
            {
                if (seen[q, r] || !IsWalkableCell(land, t, q, r)) continue;
                var comp = new List<(int q, int r)>();
                stack.Push((q, r)); seen[q, r] = true;
                while (stack.Count > 0)
                {
                    var c = stack.Pop(); comp.Add(c);
                    for (int d = 0; d < 6; d++)
                    {
                        Neighbor(c.q, c.r, d, out int nq, out int nr);
                        if (!InBounds(nq, nr, w, h) || seen[nq, nr]) continue;
                        if (!IsWalkableCell(land, t, nq, nr)) continue;
                        seen[nq, nr] = true; stack.Push((nq, nr));
                    }
                }
                res.Add(comp);
            }
            return res;
        }

        /// <summary>Cepten ana kütleye giden, EN AZ engel karosu kazan yolu (0-1 BFS).</summary>
        private static List<(int q, int r)> CarvePath(bool[,] land, string[,] t,
                                                      List<(int q, int r)> from, HashSet<(int q, int r)> target)
        {
            int w = land.GetLength(0), h = land.GetLength(1);
            var cost = new Dictionary<(int q, int r), int>();
            var prev = new Dictionary<(int q, int r), (int q, int r)>();
            var dq = new LinkedList<(int q, int r)>();

            foreach (var c in from) { cost[c] = 0; dq.AddLast(c); }

            (int q, int r) hit = (-1, -1);
            while (dq.Count > 0)
            {
                var c = dq.First.Value; dq.RemoveFirst();
                if (target.Contains(c)) { hit = c; break; }
                int c0 = cost[c];
                if (c0 > 4) continue;                       // 4 karodan uzun tünel kazma
                for (int d = 0; d < 6; d++)
                {
                    Neighbor(c.q, c.r, d, out int nq, out int nr);
                    if (!InBounds(nq, nr, w, h) || !land[nq, nr]) continue;
                    int step = IsWalkableCell(land, t, nq, nr) ? 0 : 1;
                    int nc = c0 + step;
                    if (cost.TryGetValue((nq, nr), out int old) && old <= nc) continue;
                    cost[(nq, nr)] = nc;
                    prev[(nq, nr)] = c;
                    if (step == 0) dq.AddFirst((nq, nr)); else dq.AddLast((nq, nr));
                }
            }
            if (hit.q < 0) return null;

            var path = new List<(int q, int r)>();
            var cur = hit;
            while (prev.ContainsKey(cur))
            {
                if (!IsWalkableCell(land, t, cur.q, cur.r)) path.Add(cur);
                cur = prev[cur];
            }
            return path;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  7) BİYOMLAR
        // ═════════════════════════════════════════════════════════════════════

        // Biyom kovaları — SIRA ÖNEMSİZ, seçim ağırlıklı (ağırlıklar TileCatalog'da).
        // Her kovada ÖZSÜZ karolar da vardır: yoksa "nemli bölge = sınırsız doğa özü" olur ve
        // ekonomi bozulur (hedef ≈ 0.66 öz / yürünür karo, GAME_DESIGN §3 kalibrasyonu).
        private static readonly string[] BucketCoast =
            { TileCatalog.SahilKumu, TileCatalog.Kumul, TileCatalog.Cayir, TileCatalog.Sazlik,
              TileCatalog.YosunTarla, TileCatalog.Ova };
        private static readonly string[] BucketCold =
            { TileCatalog.KarliOva, TileCatalog.Tundra, TileCatalog.CamOrmani, TileCatalog.TaslikOva,
              TileCatalog.CakilYatagi, TileCatalog.YosunTarla };
        private static readonly string[] BucketArid =
            { TileCatalog.Bozkir, TileCatalog.KurakToprak, TileCatalog.Fundalik, TileCatalog.CakilYatagi,
              TileCatalog.KirmiziKaya, TileCatalog.Kumul };
        private static readonly string[] BucketRocky =
            { TileCatalog.TaslikOva, TileCatalog.BolTaslikOva, TileCatalog.KayaYigini, TileCatalog.KilliYamac,
              TileCatalog.MermerDamari, TileCatalog.ObsidyenTarla, TileCatalog.CakilYatagi,
              TileCatalog.KurakToprak, TileCatalog.Tundra, TileCatalog.Bozkir };
        private static readonly string[] BucketWet =
            { TileCatalog.Orman, TileCatalog.CamOrmani, TileCatalog.HusKorusu, TileCatalog.YuksekOrman,
              TileCatalog.MantarOrmani, TileCatalog.BambuKoru, TileCatalog.KavakSirasi, TileCatalog.MeyveBahcesi,
              TileCatalog.UzunOt, TileCatalog.Cayir, TileCatalog.YosunTarla, TileCatalog.Lavanta };
        private static readonly string[] BucketMarsh =
            { TileCatalog.Bataklik, TileCatalog.BataklikOtu, TileCatalog.Sazlik, TileCatalog.YosunTarla };
        private static readonly string[] BucketMild =
            { TileCatalog.Ova, TileCatalog.Cayir, TileCatalog.UzunOt, TileCatalog.AzAgacliOva,
              TileCatalog.Lavanta, TileCatalog.MeyveBahcesi, TileCatalog.Fundalik, TileCatalog.Bozkir };

        private static void AssignBiomes(bool[,] land, float[,] elev, float[,] moist, float[,] temp,
                                         int[,] coastDist, string[,] t, int seed)
        {
            int w = land.GetLength(0), h = land.GetLength(1);

            // Nehre/göle yakınlık — ormanlar suyun kenarında toplansın (gerçek coğrafya).
            int[,] waterDist = FeatureDistance(land, t, w, h);

            foreach (var (q, r) in Cells(w, h))
            {
                if (!land[q, r] || t[q, r] != null) continue;

                float e = elev[q, r], m = moist[q, r], tp = temp[q, r];
                if (waterDist[q, r] <= 2) m = Clamp01(m + 0.22f);      // su kenarı nemlidir

                string[] bucket;
                if (coastDist[q, r] <= 1 && e < 0.45f)      bucket = BucketCoast;
                else if (tp < 0.28f)                         bucket = BucketCold;
                else if (m < 0.34f)                          bucket = BucketArid;
                else if (e > 0.62f)                          bucket = BucketRocky;
                else if (m > 0.74f && e < 0.34f)             bucket = BucketMarsh;
                else if (m > 0.60f)                          bucket = BucketWet;
                else                                         bucket = BucketMild;

                t[q, r] = WeightedPick(bucket, q, r, seed);
            }
        }

        /// <summary>Kovadan ağırlıklı seçim — hücre bazlı deterministik hash ile (üretim sırasına
        /// bağımlı değil, bu yüzden başka adımlar değişse de aynı hücre aynı sonucu verir).</summary>
        private static string WeightedPick(string[] bucket, int q, int r, int seed)
        {
            float total = 0f;
            for (int i = 0; i < bucket.Length; i++)
            {
                var e = TileCatalog.Get(bucket[i]);
                total += e != null ? Math.Max(0.0001f, e.Weight) : 0.0001f;
            }
            float pick = MapNoise.White(q, r, seed ^ 0x51ED270B) * total;
            for (int i = 0; i < bucket.Length; i++)
            {
                var e = TileCatalog.Get(bucket[i]);
                pick -= e != null ? Math.Max(0.0001f, e.Weight) : 0.0001f;
                if (pick <= 0f) return bucket[i];
            }
            return bucket[bucket.Length - 1];
        }

        /// <summary>Nehir/göl karolarına hex mesafesi (çok kaynaklı BFS).</summary>
        private static int[,] FeatureDistance(bool[,] land, string[,] t, int w, int h)
        {
            var dist = new int[w, h];
            var queue = new Queue<(int q, int r)>();
            foreach (var (q, r) in Cells(w, h))
            {
                var e = TileCatalog.Get(t[q, r]);
                bool water = e != null && (e.Family == TileFamily.River ||
                                          (e.Family == TileFamily.Blob && (e.Surface == Surface.Water ||
                                                                           e.Surface == Surface.Ice ||
                                                                           e.Surface == Surface.Swamp)));
                dist[q, r] = water ? 0 : int.MaxValue;
                if (water) queue.Enqueue((q, r));
            }
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                for (int d = 0; d < 6; d++)
                {
                    Neighbor(c.q, c.r, d, out int nq, out int nr);
                    if (!InBounds(nq, nr, w, h) || dist[nq, nr] != int.MaxValue) continue;
                    dist[nq, nr] = dist[c.q, c.r] + 1;
                    queue.Enqueue((nq, nr));
                }
            }
            return dist;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  8) LANDMARK'LAR
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>Birbirinden UZAK, göze çarpan nadir karolar. Sisli bir haritada oyuncunun
        /// "şu dikilitaşın orada sola" diye zihinsel harita kurabilmesi için (Lynch: landmark).
        /// Kıyı/dağ eteği/orman içi gibi karakterli yerler tercih edilir.</summary>
        private static int PlaceLandmarks(bool[,] land, string[,] t, float[,] elev, int[,] coastDist,
                                          PythonRandom rnd, int count, int spacing)
        {
            int w = land.GetLength(0), h = land.GetLength(1);
            var pool = new List<(int q, int r)>();
            foreach (var (q, r) in Cells(w, h))
                if (land[q, r] && TileCatalog.IsWalkable(t[q, r]))
                {
                    var e = TileCatalog.Get(t[q, r]);
                    if (e != null && e.Family == TileFamily.Crossing) continue;   // geçidi kapatma
                    pool.Add((q, r));
                }
            rnd.Shuffle(pool);

            var kinds = TileCatalog.Family(TileFamily.Landmark);
            var placedAt = new List<(int q, int r)>();
            int placed = 0;

            foreach (var c in pool)
            {
                if (placed >= count) break;
                bool tooClose = false;
                foreach (var pAt in placedAt)
                    if (HexDist(pAt, c) < spacing) { tooClose = true; break; }
                if (tooClose) continue;

                // Karakterli yer seç: kıyıya çok yakın ya da yüksek — düz ortada da olabilir ama daha nadir.
                bool special = coastDist[c.q, c.r] <= 2 || elev[c.q, c.r] > 0.55f;
                if (!special && MapNoise.White(c.q, c.r, 8080) > 0.45f) continue;

                var kind = kinds[rnd.RandRange(kinds.Count)];
                // Gemi enkazı kıyıda, krater yüksekte anlamlı — bariz saçmalıkları ele
                if (kind.Id == TileCatalog.GemiEnkazi && coastDist[c.q, c.r] > 2) continue;
                if (kind.Id == TileCatalog.Krater && elev[c.q, c.r] < 0.45f) continue;

                t[c.q, c.r] = kind.Id;
                placedAt.Add(c);
                placed++;
            }
            return placed;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  9) SINIR DEKORU — "haritanın bittiği yer görünmesin"
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>Kıyının dışına DÜZENSİZ genişlikte sığ su → derin su → sis bandı ve birkaç uzak
        /// adacık koyar. Bant kalınlığı gürültüyle değiştiği için düz bir "çerçeve" oluşmaz;
        /// dıştaki sis karoları haritanın nerede bittiğini gizler (kullanıcı isteği: For The King
        /// tarzı, sınırı belirsiz, sonsuz hissi veren harita).</summary>
        private static int PlaceFringe(in TerrainParams p, int seed, bool[,] land, string[,] tiles)
        {
            int w = p.Width, h = p.Height;
            if (p.FringeWidth <= 0) return 0;

            // CoastDistance kara karosunun DENİZE mesafesini verir; burada TERSİ lazım
            // (deniz karosunun KARAYA mesafesi) → kendi çok kaynaklı BFS'i.
            var seaDist = new int[w, h];
            var queue = new Queue<(int q, int r)>();
            foreach (var (q, r) in Cells(w, h))
            {
                seaDist[q, r] = land[q, r] ? 0 : int.MaxValue;
                if (land[q, r]) queue.Enqueue((q, r));
            }
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                for (int d = 0; d < 6; d++)
                {
                    Neighbor(c.q, c.r, d, out int nq, out int nr);
                    if (!InBounds(nq, nr, w, h) || seaDist[nq, nr] != int.MaxValue) continue;
                    seaDist[nq, nr] = seaDist[c.q, c.r] + 1;
                    queue.Enqueue((nq, nr));
                }
            }

            int placed = 0;
            for (int q = 0; q < w; q++)
                for (int r = 0; r < h; r++)
                {
                    if (land[q, r] || seaDist[q, r] == int.MaxValue) continue;

                    // Bant genişliği gürültüyle 1..FringeWidth+1 arasında dalgalanır → düz çerçeve yok.
                    World(q, r, out float x, out float z);
                    float n = MapNoise.Fbm(x * 0.16f, z * 0.16f, seed + 6060, 3);   // −1..1
                    int reach = p.FringeWidth + (int)Math.Round(n * 1.9f);
                    if (seaDist[q, r] > reach) continue;

                    string id;
                    if (seaDist[q, r] == 1)                      id = TileCatalog.SigSu;
                    else if (seaDist[q, r] >= reach)             id = TileCatalog.SisPerdesi;
                    else if (seaDist[q, r] == 2)                 id = MapNoise.White(q, r, 4141) < 0.18f
                                                                       ? TileCatalog.Girdap : TileCatalog.DerinSu;
                    else                                         id = MapNoise.White(q, r, 5151) < 0.72f
                                                                       ? TileCatalog.DerinSu : TileCatalog.SisPerdesi;

                    tiles[q, r] = id;
                    placed++;
                }

            // Uzak adacıklar: sisin içinde, karaya değmeyen 1-3 karoluk kaya öbekleri.
            var rnd = new PythonRandom(seed + 4242);
            int islets = 3 + rnd.RandRange(4);
            for (int i = 0; i < islets; i++)
            {
                for (int tryI = 0; tryI < 40; tryI++)
                {
                    int q = 1 + rnd.RandRange(w - 2), r = 1 + rnd.RandRange(h - 2);
                    if (seaDist[q, r] < p.FringeWidth || seaDist[q, r] == int.MaxValue) continue;
                    if (tiles[q, r] != TileCatalog.SisPerdesi && tiles[q, r] != TileCatalog.DerinSu
                        && tiles[q, r] != TileCatalog.Void) continue;
                    if (tiles[q, r] == TileCatalog.Void) placed++;
                    tiles[q, r] = TileCatalog.UzakKayalik;
                    for (int d = 0; d < 6; d++)
                    {
                        if (rnd.Random() > 0.4) continue;
                        Neighbor(q, r, d, out int nq, out int nr);
                        if (!InBounds(nq, nr, w, h) || land[nq, nr]) continue;
                        if (tiles[nq, nr] == TileCatalog.Void) placed++;
                        tiles[nq, nr] = TileCatalog.UzakKayalik;
                    }
                    break;
                }
            }
            return placed;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  BAŞLANGIÇ + BAĞLANTILI BİLEŞEN (dış API)
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Oyuncunun başlayacağı karo. ÜÇ ŞART, sırayla:
        ///   1. EN BÜYÜK YÜRÜNÜR BİLEŞEN içinde olmalı,
        ///   2. çevresi AÇIK olmalı (2 hex yarıçapta yeterli yürünür karo),
        ///   3. o bileşenin kendi ağırlık merkezine yakın olmalı.
        ///
        /// NEDEN BU KADAR TİTİZ (2026-08-12 hata raporu): eski sürüm yalnız "kıtanın ağırlık
        /// merkezine en yakın yürünür karo" diyordu ve oyunu iki şekilde bozuyordu —
        ///   • Hilal/at nalı biçimli kıtalarda ağırlık merkezi KÖRFEZE düşüyor, en yakın kara da
        ///     ince bir kıstak oluyordu → oyuncu 2 karoluk bir şeritte doğuyordu.
        ///   • Bileşen kontrolü olmadığı için dağların çevrelediği 1-3 karoluk CEPTE doğulabiliyordu
        ///     (RepairConnectivity yalnız 4+ karoluk cepleri açıyor) → hiçbir yere gidilemiyordu.
        /// </summary>
        private static (int q, int r) PickStart(bool[,] land, string[,] tiles, float[,] elev)
        {
            int w = land.GetLength(0), h = land.GetLength(1);

            // 1) En büyük yürünür bileşen
            var comps = WalkableComponents(land, tiles);
            if (comps.Count == 0) return (0, 0);
            comps.Sort((a, b) => b.Count.CompareTo(a.Count));
            var main = comps[0];
            var inMain = new HashSet<(int q, int r)>(main);

            // Bileşenin KENDİ ağırlık merkezi (kıtanınki değil — körfez tuzağı buradan geliyordu)
            double sx = 0, sz = 0;
            foreach (var c in main) { World(c.q, c.r, out float x, out float z); sx += x; sz += z; }
            sx /= main.Count; sz /= main.Count;

            (int q, int r) best = main[0]; double bestKey = double.MaxValue; bool found = false;

            for (int pass = 0; pass < 2 && !found; pass++)
            {
                // İlk geçiş açıklık şartını arar; hiçbir karo geçemezse (çok dar kıta) şart düşer.
                int minOpen = pass == 0 ? 13 : 0;   // 2 yarıçapta 19 hex var; 13 = ~%70 açık

                foreach (var c in main)
                {
                    var e = TileCatalog.Get(tiles[c.q, c.r]);
                    if (e != null && (e.Family == TileFamily.Landmark || e.Family == TileFamily.Crossing))
                        continue;                                  // landmark/geçit üstünde doğma

                    if (minOpen > 0 && OpenNeighborhood(land, tiles, inMain, c.q, c.r, w, h, 2) < minOpen)
                        continue;

                    World(c.q, c.r, out float x, out float z);
                    double d = (x - sx) * (x - sx) + (z - sz) * (z - sz) + elev[c.q, c.r] * 6.0;
                    if (!found || d < bestKey) { bestKey = d; best = c; found = true; }
                }
            }
            return best;
        }

        /// <summary>(q,r) çevresinde <paramref name="radius"/> hex içinde kaç karo ANA BİLEŞENDEN
        /// yürünür. Dar kıstak / küçük cep tespiti için.</summary>
        private static int OpenNeighborhood(bool[,] land, string[,] tiles, HashSet<(int q, int r)> inMain,
                                            int q, int r, int w, int h, int radius)
        {
            int aq = q - (r >> 1), ar = r;                        // offset → axial
            int count = 0;
            for (int dq = -radius; dq <= radius; dq++)
                for (int dr = Math.Max(-radius, -dq - radius); dr <= Math.Min(radius, -dq + radius); dr++)
                {
                    int nAq = aq + dq, nAr = ar + dr;
                    int nq = nAq + (nAr >> 1), nr = nAr;           // axial → offset
                    if (!InBounds(nq, nr, w, h) || !land[nq, nr]) continue;
                    if (inMain.Contains((nq, nr))) count++;
                }
            return count;
        }

        /// <summary><paramref name="startQ"/>,<paramref name="startR"/>'dan BFS ile erişilebilen
        /// yürünür karolar. Başlangıç yürünemezse en yakın yürünür karoya kaydırılır.</summary>
        public static HashSet<(int q, int r)> ConnectedComponent(string[,] terrain, int startQ, int startR,
                                                                 out (int q, int r) start)
        {
            int w = terrain.GetLength(0), h = terrain.GetLength(1);

            if (!InBounds(startQ, startR, w, h) || !TileCatalog.IsWalkable(terrain[startQ, startR]))
            {
                int best = int.MaxValue; (int q, int r) bestT = (0, 0); bool found = false;
                for (int q = 0; q < w; q++)
                    for (int r = 0; r < h; r++)
                    {
                        if (!TileCatalog.IsWalkable(terrain[q, r])) continue;
                        int d = Math.Abs(q - startQ) + Math.Abs(r - startR);
                        if (!found || d < best) { best = d; bestT = (q, r); found = true; }
                    }
                startQ = bestT.q; startR = bestT.r;
            }

            start = (startQ, startR);
            var seen = new HashSet<(int q, int r)> { start };
            var queue = new Queue<(int q, int r)>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                for (int d = 0; d < 6; d++)
                {
                    Neighbor(cur.q, cur.r, d, out int nq, out int nr);
                    if (!InBounds(nq, nr, w, h)) continue;
                    if (!TileCatalog.IsWalkable(terrain[nq, nr])) continue;
                    if (seen.Add((nq, nr))) queue.Enqueue((nq, nr));
                }
            }
            return seen;
        }
    }
}
