using System;
using System.Text;
using TacticalRPG.Grid;

public static class VerifyMain
{
    // GAME_DESIGN.md §3 — 10 sabit seed havuzu
    static readonly int[] Seeds = { 89, 7, 20, 108, 219, 64, 173, 283, 141, 286 };

    public static void Main()
    {
        const int w = 22, h = 25;
        var sb = new StringBuilder();
        foreach (int seed in Seeds)
        {
            string[,] t = TerrainGenerator.Generate(w, h, seed, 0.20);
            for (int q = 0; q < w; q++)
                for (int r = 0; r < h; r++)
                    sb.Append(seed).Append(' ').Append(q).Append(' ').Append(r).Append(' ')
                      .Append(t[q, r]).Append('\n');
        }
        Console.Out.Write(sb.ToString());
    }
}
