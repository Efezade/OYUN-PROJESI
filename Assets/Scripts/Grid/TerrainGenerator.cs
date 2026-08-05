using System;
using System.Collections.Generic;

namespace TacticalRPG.Grid
{
    /// <summary>
    /// Bölüm 1 prosedürel terrain üreticisi — `Docs/Balance/tools/harita_terrain_v2.py`'nin
    /// **birebir portu** (aynı seed → aynı harita; TASK-005 kabul kriteri).
    /// Rastgelelik <see cref="PythonRandom"/>'dan gelir, çünkü 10 sabit seed Python tarafında
    /// elle doğrulandı (bkz Docs/GAME_DESIGN.md §3).
    ///
    /// Taksonomi (GAME_DESIGN.md §3):
    ///   YÜRÜNEMEZ: sık orman, dağ, göl, nehir (köprü geçitli).
    ///   YÜRÜNÜR + ÖZ TAŞIYAN — öz ayrı bir node değil, KARONUN KENDİSİ:
    ///     ova (öz yok) · taşlık ova (1 taş) · bol taşlık ova (2 taş) ·
    ///     az ağaçlı ova (1 doğa) · orman (2 doğa) · nadir yüksek orman (3 doğa, nadir).
    ///   Öz TEK SEFERLİK — toplanınca karo tükenir (<see cref="DepletedId"/>'e döner).
    ///
    /// UnityEngine'e BAĞIMLI DEĞİLDİR (bilerek) — Unity'siz doğrulama koşumunda da derlenir.
    /// Karo id'leri Python'daki string'lerin AYNISI; TilePalette girişleri de bu id'leri kullanır.
    /// </summary>
    public static class TerrainGenerator
    {
        // ── Karo id'leri (Python'daki string'lerle birebir) ──────────────────
        public const string OvaId               = "ova";
        public const string TaslikOvaId         = "taslik_ova";
        public const string BolTaslikOvaId      = "bol_taslik_ova";
        public const string AzAgacliOvaId       = "az_agacli_ova";
        public const string OrmanId             = "orman";
        public const string NadirYuksekOrmanId  = "nadir_yuksek_orman";
        public const string SikOrmanId          = "sik_orman";
        public const string DagId               = "dag";
        public const string GolId               = "gol";
        public const string NehirId             = "nehir";
        /// <summary>Nehrin geçidi. Palette'te ZATEN VAR (köprü FBX'li, yürünür) — yeniden kullanılıyor.</summary>
        public const string KopruId             = "kopru";

        /// <summary>Özü toplanmış karo buna döner (ova = öz yok). Öz TEK SEFERLİK.</summary>
        public const string DepletedId = OvaId;

        /// <summary>Yürünür alt-tipler — SIRA ÖNEMLİ: Python'daki sözlük ekleme sırasıyla aynı
        /// (ağırlıklı seçim indise göre yapılıyor).</summary>
        public static readonly string[] SubtypeIds =
            { OvaId, TaslikOvaId, BolTaslikOvaId, AzAgacliOvaId, OrmanId, NadirYuksekOrmanId };

        /// <summary>`DEFAULT_SUBTYPE_WEIGHTS` — SubtypeIds ile aynı sırada.</summary>
        public static readonly double[] DefaultSubtypeWeights =
            { 0.55, 0.12, 0.05, 0.15, 0.10, 0.03 };

        /// <summary>`IMPASSABLE_BLOBS` — blob tipi seçimi bu sıradan yapılır.</summary>
        public static readonly string[] ImpassableBlobIds = { SikOrmanId, DagId, GolId };

        /// <summary>Yürünemez karo id'leri (köprü YÜRÜNÜR — nehrin geçidi).</summary>
        public static bool IsImpassable(string id)
            => id == SikOrmanId || id == DagId || id == GolId || id == NehirId;

        /// <summary>`ESSENCE_TABLE` — karo tipinin taşıdığı öz (miktar + tür). Yoksa (0, null).</summary>
        public static void EssenceOf(string id, out int amount, out EssenceKind kind)
        {
            switch (id)
            {
                case TaslikOvaId:        amount = 1; kind = EssenceKind.Tas;  return;
                case BolTaslikOvaId:     amount = 2; kind = EssenceKind.Tas;  return;
                case AzAgacliOvaId:      amount = 1; kind = EssenceKind.Doga; return;
                case OrmanId:            amount = 2; kind = EssenceKind.Doga; return;
                case NadirYuksekOrmanId: amount = 3; kind = EssenceKind.Doga; return;
                default:                 amount = 0; kind = EssenceKind.None; return;
            }
        }

        /// <summary>Bölüm 1'in öz türleri (GAME_DESIGN.md §3: sadece taş + doğa).</summary>
        public enum EssenceKind { None = 0, Tas = 1, Doga = 2 }

        // ── Hex komşuluk (odd-r OFFSET) ──────────────────────────────────────
        // Üretici (sütun, satır) indisli bir DİZİ üzerinde çalışır ve bu dizi tahtaya
        // `HexCoordinate.FromOffset` ile oturur → komşuluk da OFFSET kuralına uymak ZORUNDA:
        // tek satırlar yarım karo sağa kaydığı için komşu tablosu satır PARİTESİNE bağlıdır.
        //
        // 2026-08-05 DÜZELTMESİ: burada eskiden düz AXIAL tablo vardı
        // ({1,0},{1,-1},{0,-1},{-1,0},{-1,1},{0,1}) — dizi bir axial EŞKENAR DÖRTGEN sanılıyordu,
        // oysa tahta bir DİKDÖRTGEN. Sonuç: nehir kopuk kopuk, bloblar delikli, "bağlantılı"
        // denen alan tahtada bağlantısız olabiliyordu. Tablolar aşağıda axial karşılıklarıyla
        // AYNI SIRADA yazıldı (aday listesinin sırası RNG çekimini etkiler).
        //   sıra:  sağ · sağ-üst · sol-üst · sol · sol-alt · sağ-alt
        private static readonly int[,] DirsEven = { { 1, 0 }, { 0, -1 }, { -1, -1 }, { -1, 0 }, { -1, 1 }, { 0, 1 } };
        private static readonly int[,] DirsOdd  = { { 1, 0 }, { 1, -1 }, {  0, -1 }, { -1, 0 }, {  0, 1 }, { 1, 1 } };

        /// <summary>(sütun, satır)'ın <paramref name="d"/>. offset komşusu.</summary>
        private static void Neighbor(int q, int r, int d, out int nq, out int nr)
        {
            int[,] t = (r & 1) == 0 ? DirsEven : DirsOdd;
            nq = q + t[d, 0];
            nr = r + t[d, 1];
        }

        /// <summary>
        /// `generate_terrain(w, h, seed, ...)` portu. Dönen dizi `[q, r]` ile indislenir.
        /// Varsayılanlar Python imzasıyla aynı; 10-seed havuzu bu değerlerle doğrulandı.
        /// </summary>
        public static string[,] Generate(int w, int h, int seed,
                                         double obstaclePct = 0.20,
                                         int riverCount = 1,
                                         int bridgesPerRiver = 2,
                                         int blobSizeMin = 8,
                                         int blobSizeMax = 30,
                                         double[] subtypeWeights = null)
        {
            var rnd = new PythonRandom(seed);

            var terrain  = new string[w, h];   // null = henüz atanmadı (açık alan)
            var obstacle = new bool[w, h];     // Python'daki obstacle_tiles KÜMESİ (tekrar saymaz)
            int obstacleCount   = 0;
            int total           = w * h;
            int targetObstacles = (int)(total * obstaclePct);

            // ── 1) Nehir ─────────────────────────────────────────────────────
            for (int riv = 0; riv < riverCount; riv++)
            {
                int r0 = rnd.RandInt(0, h - 1);
                int q = 0, r = r0;
                var path = new List<(int q, int r)> { (q, r) };
                int steps = 0;

                while (q < w - 1 && steps < w * 4)
                {
                    steps++;
                    var candidates = new List<(int q, int r)>();
                    for (int d = 0; d < 6; d++)
                    {
                        Neighbor(q, r, d, out int nq, out int nr);
                        if (InBounds(nq, nr, w, h) && nq >= q) candidates.Add((nq, nr));
                    }
                    if (candidates.Count == 0)
                        for (int d = 0; d < 6; d++)
                        {
                            Neighbor(q, r, d, out int nq, out int nr);
                            if (InBounds(nq, nr, w, h)) candidates.Add((nq, nr));
                        }

                    var pick = rnd.Choice(candidates);
                    q = pick.q; r = pick.r;
                    path.Add((q, r));
                }

                foreach (var t in path)
                    if (terrain[t.q, t.r] == null)
                    {
                        terrain[t.q, t.r] = NehirId;
                        if (!obstacle[t.q, t.r]) { obstacle[t.q, t.r] = true; obstacleCount++; }
                    }

                foreach (var b in rnd.Sample(path, Math.Min(bridgesPerRiver, path.Count)))
                {
                    terrain[b.q, b.r] = KopruId;
                    if (obstacle[b.q, b.r]) { obstacle[b.q, b.r] = false; obstacleCount--; }
                }
            }

            // ── 2) Sık orman / dağ / göl blobları — hedef yüzdeye kadar ──────
            int attempts = 0;
            while (obstacleCount < targetObstacles && attempts < 300)
            {
                attempts++;

                // rnd.choice(all_tiles): all_tiles sırası [q dış, r iç]
                int flat = rnd.RandRange(total);
                int sq = flat / h, sr = flat % h;
                if (terrain[sq, sr] != null) continue;

                string blobType = rnd.Choice(ImpassableBlobIds);
                int    size     = rnd.RandInt(blobSizeMin, blobSizeMax);

                var blob     = new HashSet<(int q, int r)> { (sq, sr) };
                var frontier = new List<(int q, int r)>    { (sq, sr) };

                while (frontier.Count > 0 && blob.Count < size)
                {
                    int idx = rnd.RandRange(frontier.Count);
                    var cur = frontier[idx];
                    frontier.RemoveAt(idx);

                    var nbs = new List<(int q, int r)>();
                    for (int d = 0; d < 6; d++)
                    {
                        Neighbor(cur.q, cur.r, d, out int nq, out int nr);
                        if (InBounds(nq, nr, w, h) && !blob.Contains((nq, nr)) && terrain[nq, nr] == null)
                            nbs.Add((nq, nr));
                    }
                    rnd.Shuffle(nbs);

                    int take = Math.Min(2, nbs.Count);
                    for (int i = 0; i < take; i++)
                    {
                        blob.Add(nbs[i]);
                        frontier.Add(nbs[i]);
                        if (blob.Count >= size) break;
                    }
                }

                foreach (var t in blob)
                    if (terrain[t.q, t.r] == null)
                    {
                        terrain[t.q, t.r] = blobType;
                        if (!obstacle[t.q, t.r]) { obstacle[t.q, t.r] = true; obstacleCount++; }
                    }
            }

            // ── 3) Kalan açık alanları yürünür alt-tiplere AĞIRLIKLI dağıt ───
            double[] cum = PythonRandom.Accumulate(subtypeWeights ?? DefaultSubtypeWeights);
            for (int q = 0; q < w; q++)
                for (int r = 0; r < h; r++)
                    if (terrain[q, r] == null)
                        terrain[q, r] = SubtypeIds[rnd.ChoicesIndex(cum)];

            return terrain;
        }

        /// <summary>`largest_connected_component` portu: <paramref name="start"/>'tan BFS ile
        /// erişilebilen yürünür karolar. Başlangıç yürünemezse en yakın yürünür karoya kaydırılır
        /// (eşitlikte q, sonra r küçük olan — Python'da küme sırasına bağlıydı, burada belirli).</summary>
        public static HashSet<(int q, int r)> ConnectedComponent(string[,] terrain, int startQ, int startR,
                                                                 out (int q, int r) start)
        {
            int w = terrain.GetLength(0), h = terrain.GetLength(1);

            if (!InBounds(startQ, startR, w, h) || IsImpassable(terrain[startQ, startR]))
            {
                int best = int.MaxValue; (int q, int r) bestT = (0, 0); bool found = false;
                for (int q = 0; q < w; q++)
                    for (int r = 0; r < h; r++)
                    {
                        if (IsImpassable(terrain[q, r])) continue;
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
                    if (IsImpassable(terrain[nq, nr])) continue;
                    if (seen.Add((nq, nr))) queue.Enqueue((nq, nr));
                }
            }
            return seen;
        }

        private static bool InBounds(int q, int r, int w, int h) => q >= 0 && q < w && r >= 0 && r < h;
    }
}
