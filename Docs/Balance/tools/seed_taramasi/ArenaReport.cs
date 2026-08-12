using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TacticalRPG.Grid;
using TacticalRPG.Core;

/// <summary>
/// SAVAŞ ARENASI DOĞRULAMASI — dört kademeyi çok sayıda seed'le üretip ölçer:
/// oynanabilir karo sayısı, engel/etkileşim oranları, deploy↔düşman mesafesi (temas turu),
/// ve arena kilitli mi (yol var mı). Ayrıca hasar formülünün vuruş-ölüm tablosunu basar.
///
/// Unity gerekmez — oyunda çalışan sınıfların AYNISI derleniyor.
/// </summary>
public static class ArenaReport
{
    const int Seeds = 400;

    public static void Run()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# SAVAS ARENASI RAPORU");
        sb.AppendLine();
        sb.AppendLine(DamageTable());
        sb.AppendLine();

        sb.AppendLine("## Arena kademeleri (" + Seeds + " seed ortalamasi)");
        sb.AppendLine("kademe      kutu    oynanabilir  engel%  etkilesim%  deploy  dusman  mesafe  temas-tur  kilitli");

        foreach (ArenaTier tier in Enum.GetValues(typeof(ArenaTier)))
        {
            var p = ArenaParams.ForTier(tier);
            double playable = 0, blocked = 0, inter = 0, deploy = 0, spawns = 0, dist = 0;
            int locked = 0, minPlay = int.MaxValue, maxPlay = 0;

            for (int seed = 1; seed <= Seeds; seed++)
            {
                var a = CombatArenaGenerator.Generate(p, seed);
                playable += a.Playable;
                blocked  += a.BlockedPct;
                inter    += a.InteractivePct;
                deploy   += a.DeployZone.Count;
                spawns   += a.EnemySpawns.Count;
                minPlay = Math.Min(minPlay, a.Playable);
                maxPlay = Math.Max(maxPlay, a.Playable);

                int d = ContactDistance(a, p);
                if (d < 0) locked++; else dist += d;
            }

            int n = Seeds;
            double avgDist = locked < n ? dist / (n - locked) : 0;
            // Temas turu: iki taraf da yaklasiyor, tur basina ~6 karo kapaniyor (hareket 3 + 3).
            double contactTurn = Math.Ceiling(avgDist / 6.0);

            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "{0,-11} {1,2}x{2,-2}  {3,5:F0} ({4}-{5}) {6,6:F1} {7,10:F1} {8,7:F0} {9,7:F1} {10,7:F1} {11,10:F0} {12,8}",
                tier, p.Width, p.Height, playable / n, minPlay, maxPlay,
                blocked / n, inter / n, deploy / n, spawns / n, avgDist, contactTurn,
                locked == 0 ? "yok" : locked + "/" + n));
        }

        sb.AppendLine();
        sb.AppendLine("## ONIZLEME");
        foreach (ArenaTier tier in Enum.GetValues(typeof(ArenaTier)))
        {
            sb.AppendLine();
            sb.AppendLine($"### {tier} (seed 7)");
            sb.AppendLine(Ascii(CombatArenaGenerator.Generate(ArenaParams.ForTier(tier), 7),
                                ArenaParams.ForTier(tier)));
        }

        Console.Out.Write(sb.ToString());
    }

    // ── Hasar formulu tablosu ────────────────────────────────────────────────

    static string DamageTable()
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Hasar formulu — vurus/olum tablosu");
        sb.AppendLine($"formul: ATK x 100 / (100 + DEF x {CombatMath.DefenseScale}), taban {CombatMath.MinimumDamage}");
        sb.AppendLine();

        var atk = new (string name, int a)[]
            { ("Savasci", 5), ("Ranger", 6), ("Kam", 4), ("Goblin", 3), ("Yamyam", 4), ("GoblinSaman", 5) };
        var def = new (string name, int d, int hp)[]
            { ("Savasci", 3, 14), ("Ranger", 1, 10), ("Kam", 1, 8), ("Goblin", 0, 8), ("Yamyam", 0, 7), ("GoblinSaman", 1, 10) };

        sb.Append("saldiran \\ hedef ");
        foreach (var d in def) sb.Append(string.Format("{0,-13}", d.name));
        sb.AppendLine();

        foreach (var a in atk)
        {
            sb.Append(string.Format("{0,-16} ", a.name));
            foreach (var d in def)
            {
                int dmg = CombatMath.Damage(a.a, d.d);
                int hits = CombatMath.HitsToKill(a.a, d.d, d.hp);
                sb.Append(string.Format("{0,-13}", a.name == d.name ? "-" : $"{dmg} ({hits}v)"));
            }
            sb.AppendLine();
        }
        sb.AppendLine();
        sb.AppendLine("ESKI formul (max(0, ATK-DEF)) ile Goblin -> Savasci = 0 hasar (ASLA olmezdi).");
        return sb.ToString();
    }

    // ── Temas mesafesi ───────────────────────────────────────────────────────

    static readonly int[,] DE = { { 1, 0 }, { 0, -1 }, { -1, -1 }, { -1, 0 }, { -1, 1 }, { 0, 1 } };
    static readonly int[,] DO = { { 1, 0 }, { 1, -1 }, {  0, -1 }, { -1, 0 }, {  0, 1 }, { 1, 1 } };
    static void Nb(int q, int r, int d, out int nq, out int nr)
    {
        int[,] t = (r & 1) == 0 ? DE : DO;
        nq = q + t[d, 0]; nr = r + t[d, 1];
    }

    /// <summary>Deploy bolgesinden en yakin dusman doguma noktasina YURUME mesafesi.
    /// -1 = ulasilamiyor (arena kilitli — asla olmamali).</summary>
    static int ContactDistance(ArenaResult a, in ArenaParams p)
    {
        int w = p.Width, h = p.Height;
        var dist = new Dictionary<(int q, int r), int>();
        var queue = new Queue<(int q, int r)>();
        foreach (var c in a.DeployZone)
            if (Walkable(a, c.q, c.r)) { dist[c] = 0; queue.Enqueue(c); }

        var targets = new HashSet<(int q, int r)>(a.EnemySpawns);
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            if (targets.Contains(c)) return dist[c];
            for (int d = 0; d < 6; d++)
            {
                Nb(c.q, c.r, d, out int nq, out int nr);
                if (nq < 0 || nr < 0 || nq >= w || nr >= h) continue;
                if (!Walkable(a, nq, nr) || dist.ContainsKey((nq, nr))) continue;
                dist[(nq, nr)] = dist[c] + 1;
                queue.Enqueue((nq, nr));
            }
        }
        return -1;
    }

    static bool Walkable(ArenaResult a, int q, int r)
        => !TileCatalog.IsVoid(a.Tiles[q, r])
           && a.Roles[q, r] != CombatRole.Wall && a.Roles[q, r] != CombatRole.Cover;

    // ── ASCII ────────────────────────────────────────────────────────────────

    static string Ascii(ArenaResult a, in ArenaParams p)
    {
        var deploy = new HashSet<(int q, int r)>(a.DeployZone);
        var spawns = new HashSet<(int q, int r)>(a.EnemySpawns);
        var sb = new StringBuilder();

        for (int r = p.Height - 1; r >= 0; r--)          // ust satir = dusman tarafi
        {
            if ((r & 1) == 1) sb.Append(' ');
            for (int q = 0; q < p.Width; q++)
            {
                char ch;
                if (TileCatalog.IsVoid(a.Tiles[q, r]))      ch = ' ';
                else if (spawns.Contains((q, r)))           ch = 'X';
                else if (deploy.Contains((q, r)))           ch = 'o';
                else switch (a.Roles[q, r])
                {
                    case CombatRole.Wall:      ch = '#'; break;
                    case CombatRole.Cover:     ch = 'n'; break;
                    case CombatRole.High:      ch = '^'; break;
                    case CombatRole.Hazard:    ch = '!'; break;
                    case CombatRole.Difficult: ch = '~'; break;
                    default:                   ch = '.'; break;
                }
                sb.Append(ch).Append(' ');
            }
            sb.Append('\n');
        }
        sb.AppendLine("  'X'=dusman  'o'=deploy  '#'=duvar(gorus keser)  'n'=siper  '^'=yukselti  '!'=tehlike  '~'=zor arazi");
        return sb.ToString();
    }
}
