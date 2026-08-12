using System;
using System.Collections.Generic;

namespace TacticalRPG.Grid
{
    /// <summary>Savaş karosunun taktik ROLÜ. Görsel id'den ayrı tutulur: aynı görsel (ör. dev kaya)
    /// farklı arenalarda farklı rol taşıyabilsin ve LoS/siper kuralları tek yerden okunsun.</summary>
    public enum CombatRole
    {
        Floor,      // düz zemin
        Wall,       // geçilemez + GÖRÜŞ KESER (siperin arkasına saklanılan şey)
        Cover,      // geçilemez, yarım siper: görüşü kesmez ama isabeti düşürür
        High,       // yürünür YÜKSELTİ: menzil/kritik bonusu
        Hazard,     // yürünür ama girişte hasar
        Difficult   // yürünür, hareket maliyeti 2
    }

    /// <summary>Bir savaş arenasının çıktısı.</summary>
    public sealed class ArenaResult
    {
        public string[,]     Tiles;        // [sütun, satır]; TileCatalog.Void = hücre yok
        public CombatRole[,] Roles;
        public List<(int q, int r)> DeployZone = new();   // oyuncu birimlerinin ineceği karolar
        public List<(int q, int r)> EnemySpawns = new();  // düşman doğma noktaları

        public int Playable, Walls, Covers, Highs, Hazards, Difficults;
        public float BlockedPct     => Playable > 0 ? 100f * (Walls + Covers) / Playable : 0f;
        public float InteractivePct => Playable > 0 ? 100f * (Highs + Hazards + Difficults) / Playable : 0f;
    }

    /// <summary>Arena boyut/yoğunluk kademesi — düğüm tipine göre seçilir.</summary>
    public struct ArenaParams
    {
        public int   Width, Height;
        public int   DeployDepth;      // oyuncu bölgesinin satır derinliği
        public int   EnemyDepth;       // düşman bölgesinin satır derinliği
        public int   EnemyCount;
        public float BlockedPct;       // duvar + siper hedef oranı (0.08-0.10)
        public float InteractivePct;   // yükselti + tehlike + zor arazi (0.10-0.12)
        public float EdgeRoughness;    // kenar tırtıklılığı (0 = düz dikdörtgen)

        /// <summary>Düğüm tipine göre kademe (2026-08-12 hesabı — bkz sohbet kaydı).
        /// Sahadaki birim sayısı × ~10 karo kuralı; savaş 4-12 tur sürsün diye.</summary>
        public static ArenaParams ForTier(ArenaTier tier) => tier switch
        {
            ArenaTier.Encounter => new ArenaParams { Width = 10, Height =  8, DeployDepth = 2, EnemyDepth = 2,
                                                     EnemyCount = 4, BlockedPct = 0.08f, InteractivePct = 0.10f,
                                                     EdgeRoughness = 0.45f },
            ArenaTier.Dungeon   => new ArenaParams { Width = 11, Height =  9, DeployDepth = 2, EnemyDepth = 2,
                                                     EnemyCount = 6, BlockedPct = 0.09f, InteractivePct = 0.11f,
                                                     EdgeRoughness = 0.50f },
            ArenaTier.Mandatory => new ArenaParams { Width = 12, Height = 10, DeployDepth = 2, EnemyDepth = 3,
                                                     EnemyCount = 7, BlockedPct = 0.10f, InteractivePct = 0.12f,
                                                     EdgeRoughness = 0.50f },
            _                   => new ArenaParams { Width = 13, Height = 11, DeployDepth = 3, EnemyDepth = 3,
                                                     EnemyCount = 6, BlockedPct = 0.10f, InteractivePct = 0.12f,
                                                     EdgeRoughness = 0.55f },
        };
    }

    public enum ArenaTier { Encounter, Dungeon, Mandatory, Boss }

    /// <summary>
    /// SAVAŞ ARENASI ÜRETİCİSİ — overworld haritasından TAMAMEN ayrı.
    ///
    /// Neden ayrı bir üretici: overworld 550 karoluk bir KITA (keşif, yol bulma, gün ekonomisi);
    /// arena 65-120 karoluk bir TAKTİK TAHTA (temas 2. turda, her karo bir karar). Aynı boru hattını
    /// paylaşsalardı ikisi de yanlış boyutta olurdu — nitekim öyleydi: savaş 550 karoluk düz bir
    /// "kaya" tarlasıydı, iki tarafın buluşması 7 tur alıyordu (kullanıcı geri bildirimi 2026-08-12).
    ///
    /// Tasarım kararları:
    ///   • Şekil DİKDÖRTGEN AMA KENARLARI TIRTIKLI. Tam kare olmaması gerekiyor (overworld'deki
    ///     gerekçeyle aynı), ama iki uçta düzgün DEPLOY/DÜŞMAN bandı da lazım — bu yüzden çekirdek
    ///     dikdörtgen sabit, yalnız dış halka gürültüyle aşındırılıyor.
    ///   • Engeller ORTA ŞERİTTE toplanır: deploy ve düşman bölgeleri temiz kalır (birimin doğduğu
    ///     yerde duvar olmaz), asıl taktik alan iki bölgenin arasıdır.
    ///   • Üretim sonunda bağlantı GARANTİ edilir: deploy bölgesinden düşman bölgesine yürünebilir
    ///     bir yol yoksa engel silinir. Aksi halde savaş başlar başlamaz kilitlenirdi.
    ///
    /// UnityEngine'e bağımlı DEĞİL — arena kademeleri Unity açmadan taranabilsin diye.
    /// </summary>
    public static class CombatArenaGenerator
    {
        // ── Görsel eşleme: rol → TileCatalog id'si ───────────────────────────
        // Overworld için üretilmiş renkli/dokulu karolar YENİDEN KULLANILIYOR: yeni sanat
        // gerekmiyor, arena ilk günden renkli görünüyor.
        private static readonly string[] FloorIds     = { TileCatalog.Ova, TileCatalog.Cayir, TileCatalog.UzunOt, TileCatalog.Bozkir };
        private static readonly string[] WallIds      = { TileCatalog.Dag, TileCatalog.Kayalik, TileCatalog.Ucurum };
        private static readonly string[] CoverIds     = { TileCatalog.DevKaya, TileCatalog.SikOrman, TileCatalog.DikenliCalilik };
        private static readonly string[] HighIds      = { TileCatalog.TaslikOva, TileCatalog.KayaYigini };
        private static readonly string[] HazardIds    = { TileCatalog.VolkanikKaya, TileCatalog.KaynakGolu };
        private static readonly string[] DifficultIds = { TileCatalog.Bataklik, TileCatalog.Sazlik, TileCatalog.KurakToprak };

        private static readonly int[,] DirsEven = { { 1, 0 }, { 0, -1 }, { -1, -1 }, { -1, 0 }, { -1, 1 }, { 0, 1 } };
        private static readonly int[,] DirsOdd  = { { 1, 0 }, { 1, -1 }, {  0, -1 }, { -1, 0 }, {  0, 1 }, { 1, 1 } };

        private static void Neighbor(int q, int r, int d, out int nq, out int nr)
        {
            int[,] t = (r & 1) == 0 ? DirsEven : DirsOdd;
            nq = q + t[d, 0];
            nr = r + t[d, 1];
        }

        private static bool InBounds(int q, int r, int w, int h) => q >= 0 && q < w && r >= 0 && r < h;

        // ═════════════════════════════════════════════════════════════════════

        public static ArenaResult Generate(in ArenaParams p, int seed)
        {
            int w = p.Width, h = p.Height;
            var res = new ArenaResult
            {
                Tiles = new string[w, h],
                Roles = new CombatRole[w, h]
            };
            var rnd = new PythonRandom(seed);

            // 1) ŞEKİL — çekirdek dikdörtgen + tırtıklı dış halka
            bool[,] inside = BuildShape(p, seed);

            for (int q = 0; q < w; q++)
                for (int r = 0; r < h; r++)
                {
                    res.Tiles[q, r] = inside[q, r] ? Pick(FloorIds, q, r, seed) : TileCatalog.Void;
                    res.Roles[q, r] = CombatRole.Floor;
                    if (inside[q, r]) res.Playable++;
                }

            // 2) BÖLGELER — alt uç oyuncu, üst uç düşman. Buralara engel KONULMAZ.
            var reserved = new bool[w, h];
            for (int q = 0; q < w; q++)
            {
                for (int r = 0; r < p.DeployDepth; r++)
                    if (inside[q, r]) { res.DeployZone.Add((q, r)); reserved[q, r] = true; }

                for (int r = h - p.EnemyDepth; r < h; r++)
                    if (InBounds(q, r, w, h) && inside[q, r]) reserved[q, r] = true;
            }

            // 3) ENGELLER + ETKİLEŞİMLİ KAROLAR — yalnız orta şeritte
            PlaceFeatures(p, seed, inside, reserved, res, rnd);

            // 4) BAĞLANTI GARANTİSİ — deploy'dan düşman bölgesine yol olmalı
            EnsureConnected(p, inside, res);

            // 5) DÜŞMAN DOĞMA NOKTALARI — üst bölgede, birbirinden ayrık
            PlaceEnemySpawns(p, inside, res, rnd);

            // İstatistikler
            res.Walls = res.Covers = res.Highs = res.Hazards = res.Difficults = 0;
            for (int q = 0; q < w; q++)
                for (int r = 0; r < h; r++)
                {
                    if (!inside[q, r]) continue;
                    switch (res.Roles[q, r])
                    {
                        case CombatRole.Wall:      res.Walls++;      break;
                        case CombatRole.Cover:     res.Covers++;     break;
                        case CombatRole.High:      res.Highs++;      break;
                        case CombatRole.Hazard:    res.Hazards++;    break;
                        case CombatRole.Difficult: res.Difficults++; break;
                    }
                }
            return res;
        }

        // ── 1) Şekil ─────────────────────────────────────────────────────────

        /// <summary>Çekirdek dikdörtgen (1 karo içeri) her zaman içeride; DIŞ HALKA gürültüye göre
        /// açılıp kapanır. Böylece silüet kare değil ama iki uçtaki deploy/düşman bandı bozulmaz.</summary>
        private static bool[,] BuildShape(in ArenaParams p, int seed)
        {
            int w = p.Width, h = p.Height;
            var inside = new bool[w, h];

            for (int q = 0; q < w; q++)
                for (int r = 0; r < h; r++)
                {
                    bool onBorder = q == 0 || r == 0 || q == w - 1 || r == h - 1;
                    if (!onBorder) { inside[q, r] = true; continue; }

                    // Kenar karosu: gürültü eşiği geçerse içeride kalır → tırtıklı kenar.
                    float n = MapNoise.Fbm(q * 0.55f, r * 0.55f, seed + 4242, 3) * 0.5f + 0.5f;   // 0..1
                    inside[q, r] = n > p.EdgeRoughness;
                }

            // Kopuk kenar karosu kalmasın (tek başına asılı hücre çirkin durur).
            for (int q = 0; q < w; q++)
                for (int r = 0; r < h; r++)
                {
                    if (!inside[q, r]) continue;
                    int n = 0;
                    for (int d = 0; d < 6; d++)
                    {
                        Neighbor(q, r, d, out int nq, out int nr);
                        if (InBounds(nq, nr, w, h) && inside[nq, nr]) n++;
                    }
                    if (n <= 1) inside[q, r] = false;
                }
            return inside;
        }

        // ── 3) Engel / etkileşim yerleşimi ───────────────────────────────────

        private static void PlaceFeatures(in ArenaParams p, int seed, bool[,] inside, bool[,] reserved,
                                          ArenaResult res, PythonRandom rnd)
        {
            int w = p.Width, h = p.Height;

            var free = new List<(int q, int r)>();
            for (int q = 0; q < w; q++)
                for (int r = 0; r < h; r++)
                    if (inside[q, r] && !reserved[q, r]) free.Add((q, r));

            int blockedTarget      = (int)Math.Round(res.Playable * p.BlockedPct);
            int interactiveTarget  = (int)Math.Round(res.Playable * p.InteractivePct);

            // Engeller KÜMELİ olsun: dağınık tek karolar "gürültü" gibi görünür ve siper işlevi
            // görmez; 2-3 karoluk öbekler gerçek bir duvar/kaya kütlesi okunur.
            rnd.Shuffle(free);
            int placed = 0, idx = 0;
            while (placed < blockedTarget && idx < free.Count)
            {
                var c = free[idx++];
                if (res.Roles[c.q, c.r] != CombatRole.Floor) continue;

                int clump = Math.Min(rnd.RandInt(1, 3), blockedTarget - placed);
                bool isWall = rnd.Random() < 0.55;                 // %55 duvar (görüş keser), %45 siper

                var open = new List<(int q, int r)> { c };
                for (int i = 0; i < clump && open.Count > 0; i++)
                {
                    var cur = open[rnd.RandRange(open.Count)];
                    open.Remove(cur);
                    if (res.Roles[cur.q, cur.r] != CombatRole.Floor || reserved[cur.q, cur.r]) continue;

                    res.Roles[cur.q, cur.r] = isWall ? CombatRole.Wall : CombatRole.Cover;
                    res.Tiles[cur.q, cur.r] = Pick(isWall ? WallIds : CoverIds, cur.q, cur.r, seed);
                    placed++;

                    for (int d = 0; d < 6; d++)
                    {
                        Neighbor(cur.q, cur.r, d, out int nq, out int nr);
                        if (InBounds(nq, nr, w, h) && inside[nq, nr] && !reserved[nq, nr]
                            && res.Roles[nq, nr] == CombatRole.Floor)
                            open.Add((nq, nr));
                    }
                }
            }

            // Etkileşimli (yürünür) karolar — tek tek serpilir, kümelenmesi gerekmez.
            int inter = 0;
            for (int i = 0; i < free.Count && inter < interactiveTarget; i++)
            {
                var c = free[i];
                if (res.Roles[c.q, c.r] != CombatRole.Floor) continue;

                double roll = rnd.Random();
                CombatRole role = roll < 0.45 ? CombatRole.High
                                : roll < 0.75 ? CombatRole.Difficult
                                              : CombatRole.Hazard;
                string[] ids = role == CombatRole.High      ? HighIds
                             : role == CombatRole.Difficult ? DifficultIds
                                                            : HazardIds;
                res.Roles[c.q, c.r] = role;
                res.Tiles[c.q, c.r] = Pick(ids, c.q, c.r, seed);
                inter++;
            }
        }

        private static string Pick(string[] pool, int q, int r, int seed)
            => pool[(int)(MapNoise.White(q, r, seed ^ 0x2545F491) * pool.Length) % pool.Length];

        // ── 4) Bağlantı garantisi ────────────────────────────────────────────

        private static bool IsWalkableRole(CombatRole role)
            => role != CombatRole.Wall && role != CombatRole.Cover;

        /// <summary>Deploy bölgesinden düşman bölgesine yürünebilir yol OLMAK ZORUNDA. Yoksa engelleri
        /// tek tek kaldırarak açar — savaş başlar başlamaz kilitlenmiş bir arena üretmektense
        /// hedef engel oranından biraz sapmak yeğdir.</summary>
        private static void EnsureConnected(in ArenaParams p, bool[,] inside, ArenaResult res)
        {
            int w = p.Width, h = p.Height;

            for (int attempt = 0; attempt < 40; attempt++)
            {
                var seen = Flood(p, inside, res, res.DeployZone);
                bool reached = false;
                for (int q = 0; q < w && !reached; q++)
                    for (int r = h - p.EnemyDepth; r < h; r++)
                        if (InBounds(q, r, w, h) && inside[q, r] && seen.Contains((q, r))) { reached = true; break; }
                if (reached) return;

                // Ulaşılan alanın SINIRINDAKİ ilk engeli kaldır (en dar geçidi açar).
                (int q, int r) best = (-1, -1); int bestRow = -1;
                foreach (var c in seen)
                    for (int d = 0; d < 6; d++)
                    {
                        Neighbor(c.q, c.r, d, out int nq, out int nr);
                        if (!InBounds(nq, nr, w, h) || !inside[nq, nr]) continue;
                        if (IsWalkableRole(res.Roles[nq, nr])) continue;
                        if (nr > bestRow) { bestRow = nr; best = (nq, nr); }   // düşman tarafına doğru aç
                    }
                if (best.q < 0) return;                                        // açılacak engel kalmadı

                res.Roles[best.q, best.r] = CombatRole.Floor;
                res.Tiles[best.q, best.r] = Pick(FloorIds, best.q, best.r, 7);
            }
        }

        private static HashSet<(int q, int r)> Flood(in ArenaParams p, bool[,] inside, ArenaResult res,
                                                     List<(int q, int r)> from)
        {
            int w = p.Width, h = p.Height;
            var seen = new HashSet<(int q, int r)>();
            var queue = new Queue<(int q, int r)>();
            foreach (var c in from)
                if (inside[c.q, c.r] && IsWalkableRole(res.Roles[c.q, c.r]) && seen.Add(c)) queue.Enqueue(c);

            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                for (int d = 0; d < 6; d++)
                {
                    Neighbor(c.q, c.r, d, out int nq, out int nr);
                    if (!InBounds(nq, nr, w, h) || !inside[nq, nr]) continue;
                    if (!IsWalkableRole(res.Roles[nq, nr])) continue;
                    if (seen.Add((nq, nr))) queue.Enqueue((nq, nr));
                }
            }
            return seen;
        }

        // ── 5) Düşman doğma noktaları ────────────────────────────────────────

        private static void PlaceEnemySpawns(in ArenaParams p, bool[,] inside, ArenaResult res, PythonRandom rnd)
        {
            int w = p.Width, h = p.Height;
            var pool = new List<(int q, int r)>();
            for (int q = 0; q < w; q++)
                for (int r = h - p.EnemyDepth; r < h; r++)
                    if (InBounds(q, r, w, h) && inside[q, r] && IsWalkableRole(res.Roles[q, r]))
                        pool.Add((q, r));

            rnd.Shuffle(pool);

            // Yan yana doğmasınlar: 2 karo aralık istenir, havuz yetmezse kural gevşer.
            for (int spacing = 2; spacing >= 0 && res.EnemySpawns.Count < p.EnemyCount; spacing--)
                foreach (var c in pool)
                {
                    if (res.EnemySpawns.Count >= p.EnemyCount) break;
                    if (res.EnemySpawns.Contains(c)) continue;
                    bool tooClose = false;
                    foreach (var s in res.EnemySpawns)
                        if (Dist(s, c) < spacing) { tooClose = true; break; }
                    if (!tooClose) res.EnemySpawns.Add(c);
                }
        }

        private static int Dist((int q, int r) a, (int q, int r) b)
        {
            int aq = a.q - (a.r >> 1), ar = a.r;
            int bq = b.q - (b.r >> 1), br = b.r;
            int dq = aq - bq, dr = ar - br;
            return (Math.Abs(dq) + Math.Abs(dr) + Math.Abs(dq + dr)) / 2;
        }
    }
}
