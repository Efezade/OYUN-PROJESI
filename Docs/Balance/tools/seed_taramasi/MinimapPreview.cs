using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using TacticalRPG.Grid;

/// <summary>
/// MİNİHARİTA ÖNİZLEMESİ — <c>MinimapRenderer</c>'ın boyama algoritmasının AYNISINI Unity'siz
/// koşturup PNG olarak diske yazar.
///
/// Neden: minihatita boyaması (altıgen damgalama + Minecraft kabartması + sis) Play'e basmadan
/// doğrulanamayan bir görsel iş. Burada aynı matematik konsol programında koşuyor ve çıktı
/// GÖRÜLEBİLİR bir resim oluyor — "derleniyor" demekle "harita doğru çıkıyor" demek arasındaki
/// fark bu. Renkler katalogdan alınır (oyunda palet prefabının materyali kullanılır; katalog
/// onun da yedeği olduğu için siluet/kabartma birebir aynıdır).
///
/// Kullanım: tara.ps1 -Minimap
/// </summary>
public static class MinimapPreview
{
    // MinimapStyleSO varsayılanlarıyla AYNI değerler.
    private const float PixelsPerUnit = 8f;
    private const float ShadeLower    = 0.706f;
    private const float ShadeEqual    = 0.863f;
    private const float ShadeHigher   = 1.0f;
    private const float Dither        = 0.045f;
    private const float EdgeDarken    = 0.16f;
    private const float ExploredDim   = 0.62f;

    // Keşfedilmemiş = HİÇ ÇİZİLMEZ (alfa 0). Aksi halde kıtanın silueti keşfedilmeden sızardı.
    private static readonly (byte r, byte g, byte b, byte a) Unexplored = (61, 51, 38, 0);
    private static readonly (byte r, byte g, byte b, byte a) Empty      = (0, 0, 0, 0);

    private const float Outer = 1f;                 // HexMetrics.OuterRadius
    private const float Inner = 0.866025404f;       // HexMetrics.InnerRadius

    public static void Run()
    {
        var p = TerrainParams.Default;
        int[] seeds = { 9941, 6118, 5088 };

        string dir = AppContext.BaseDirectory;
        foreach (int seed in seeds)
        {
            MapResult res = TerrainGenerator.Generate(p, seed);

            // 1) Tamamen keşfedilmiş: arazi ve kabartma doğru mu?
            Write(res, p, seed, revealRadius: -1, $"minimap_{seed}_acik.png");

            // 2) Kısmen keşfedilmiş: sis kuralı doğru mu (bilinmeyen HİÇ çizilmiyor,
            //    bilinen ama görüş dışı soluk)?
            Write(res, p, seed, revealRadius: 5, $"minimap_{seed}_sisli.png");
        }

        Console.WriteLine($"Onizlemeler yazildi: {dir}");
    }

    private static void Write(MapResult res, TerrainParams p, int seed, int revealRadius, string file)
    {
        string[,] tiles = res.Tiles;
        int cols = tiles.GetLength(0), rows = tiles.GetLength(1);

        // ── Hücre kümesi: HexGridManager.GenerateGrid ile aynı kural (Void = hücre YOK) ──
        var cells = new Dictionary<(int q, int r), (int col, int row)>();
        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        for (int row = 0; row < rows; row++)
            for (int col = 0; col < cols; col++)
            {
                string id = tiles[col, row];
                if (TileCatalog.IsVoid(id)) continue;

                (int q, int r) axial = FromOffset(col, row);
                cells[axial] = (col, row);

                (float x, float z) w = ToWorld(axial);
                if (w.x < minX) minX = w.x;
                if (w.x > maxX) maxX = w.x;
                if (w.z < minZ) minZ = w.z;
                if (w.z > maxZ) maxZ = w.z;
            }
        if (cells.Count == 0) { Console.WriteLine($"seed {seed}: hucre yok"); return; }

        float originX = minX - Inner, originZ = minZ - Outer;
        int W = (int)Math.Ceiling((maxX + Inner - originX) * PixelsPerUnit);
        int H = (int)Math.Ceiling((maxZ + Outer - originZ) * PixelsPerUnit);

        var buf = new byte[W * H * 4];
        for (int i = 0; i < buf.Length; i += 4)
        { buf[i] = Empty.r; buf[i + 1] = Empty.g; buf[i + 2] = Empty.b; buf[i + 3] = Empty.a; }

        // Sis benzetimi: başlangıç karosundan revealRadius içi "görülüyor", 2 katı içi
        // "keşfedilmiş ama görüş dışı", ötesi "hiç görülmemiş".
        (int q, int r) start = FromOffset(res.Start.q, res.Start.r);

        int painted = 0, unknown = 0;
        foreach (var kv in cells)
        {
            (int q, int r) axial = kv.Key;
            string id = tiles[kv.Value.col, kv.Value.row];

            int fog = 2;                                  // 2 = görünür
            if (revealRadius > 0)
            {
                int d = HexDistance(axial, start);
                fog = d <= revealRadius ? 2 : (d <= revealRadius * 2 ? 1 : 0);
            }

            (byte r, byte g, byte b, byte a) color;
            if (fog == 0) { color = Unexplored; unknown++; }
            else
            {
                (float r, float g, float b) c = ColorOf(id);
                float shade = ShadeOf(axial, id, cells, tiles) * DitherOf(axial);
                if (fog == 1) shade *= ExploredDim;
                color = (Clamp(c.r * shade), Clamp(c.g * shade), Clamp(c.b * shade), 255);
            }

            Stamp(buf, W, H, originX, originZ, ToWorld(axial), color);
            painted++;
        }

        WritePng(Path.Combine(AppContext.BaseDirectory, file), buf, W, H);
        Console.WriteLine($"seed {seed,6} → {file,-28} {W}x{H} px | {painted} karo " +
                          $"({unknown} kesfedilmemis) | oran {W / (float)H:F2}");
    }

    // ── MinimapRenderer.Stamp ile AYNI matematik ─────────────────────────────
    private static void Stamp(byte[] buf, int W, int H, float originX, float originZ,
                              (float x, float z) center, (byte r, byte g, byte b, byte a) color)
    {
        const float slope = 0.5f * Outer / Inner;
        float shrink = 1f - Math.Min(1f, 1.2f / (PixelsPerUnit * Outer));

        int px0 = Math.Max(0,     (int)Math.Floor((center.x - Inner - originX) * PixelsPerUnit));
        int px1 = Math.Min(W - 1, (int)Math.Ceiling((center.x + Inner - originX) * PixelsPerUnit));
        int py0 = Math.Max(0,     (int)Math.Floor((center.z - Outer - originZ) * PixelsPerUnit));
        int py1 = Math.Min(H - 1, (int)Math.Ceiling((center.z + Outer - originZ) * PixelsPerUnit));

        var edge = (Clamp(color.r / 255f * (1f - EdgeDarken)),
                    Clamp(color.g / 255f * (1f - EdgeDarken)),
                    Clamp(color.b / 255f * (1f - EdgeDarken)), color.a);

        for (int py = py0; py <= py1; py++)
        {
            float wz = originZ + (py + 0.5f) / PixelsPerUnit;
            float dz = Math.Abs(wz - center.z);

            for (int px = px0; px <= px1; px++)
            {
                float wx = originX + (px + 0.5f) / PixelsPerUnit;
                float dx = Math.Abs(wx - center.x);

                if (dx > Inner || dz > Outer - slope * dx) continue;

                bool inner = dx <= Inner * shrink && dz <= (Outer - slope * dx) * shrink;
                var c = inner ? color : edge;

                int i = (py * W + px) * 4;
                buf[i] = c.Item1; buf[i + 1] = c.Item2; buf[i + 2] = c.Item3; buf[i + 3] = c.Item4;
            }
        }
    }

    private static (float r, float g, float b) ColorOf(string id)
    {
        TileCatalog.Entry e = TileCatalog.Get(id);
        return e != null ? (e.R, e.G, e.B) : (0.5f, 0.5f, 0.5f);
    }

    private static float ShadeOf((int q, int r) c, string id,
                                 Dictionary<(int, int), (int, int)> cells, string[,] tiles)
    {
        (int q, int r) north = (c.q - 1, c.r + 1);
        if (!cells.TryGetValue(north, out (int col, int row) n)) return ShadeEqual;

        int here  = Elevation(id);
        int there = Elevation(tiles[n.col, n.row]);
        if (here < there) return ShadeLower;
        if (here > there) return ShadeHigher;
        return ShadeEqual;
    }

    private static int Elevation(string id)
    {
        TileCatalog.Entry e = TileCatalog.Get(id);
        if (e == null) return 1;
        return e.Family switch
        {
            TileFamily.Fringe   => 0,
            TileFamily.Void     => 0,
            TileFamily.Nature   => 2,
            TileFamily.Mountain => 3,
            _                   => 1
        };
    }

    private static float DitherOf((int q, int r) c)
    {
        int hash = (c.q * 73856093) ^ (c.r * 19349663);
        float t = ((hash >> 3) & 0xFF) / 255f;
        return 1f + (t - 0.5f) * 2f * Dither;
    }

    private static byte Clamp(float v) => (byte)Math.Max(0, Math.Min(255, (int)(v * 255f + 0.5f)));

    private static (int q, int r) FromOffset(int col, int row) => (col - (row >> 1), row);

    private static (float x, float z) ToWorld((int q, int r) a)
    {
        const float S3 = 1.7320508f;
        return (S3 * a.q + S3 * 0.5f * a.r, 1.5f * a.r);
    }

    private static int HexDistance((int q, int r) a, (int q, int r) b)
    {
        int aS = -a.q - a.r, bS = -b.q - b.r;
        return (Math.Abs(a.q - b.q) + Math.Abs(a.r - b.r) + Math.Abs(aS - bS)) / 2;
    }

    // ── Minimal PNG yazıcı (bağımlılık yok) ──────────────────────────────────
    // PNG = imza + IHDR + IDAT(zlib) + IEND. Sadece bu önizleme için; oyunun kodunda yok.

    private static void WritePng(string path, byte[] rgba, int w, int h)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);

        var ihdr = new byte[13];
        BeInt(ihdr, 0, w); BeInt(ihdr, 4, h);
        ihdr[8] = 8;    // bit derinliği
        ihdr[9] = 6;    // renk tipi: RGBA
        Chunk(fs, "IHDR", ihdr);

        // Tarama satırları: PNG YUKARIDAN aşağı, tampon ALTTAN yukarı → ters çevir.
        var raw = new byte[(w * 4 + 1) * h];
        int o = 0;
        for (int y = h - 1; y >= 0; y--)
        {
            raw[o++] = 0;                                   // filtre: None
            Buffer.BlockCopy(rgba, y * w * 4, raw, o, w * 4);
            o += w * 4;
        }

        using var ms = new MemoryStream();
        ms.WriteByte(0x78); ms.WriteByte(0x01);             // zlib başlığı
        using (var ds = new DeflateStream(ms, CompressionLevel.Optimal, true)) ds.Write(raw, 0, raw.Length);
        uint adler = Adler32(raw);
        ms.WriteByte((byte)(adler >> 24)); ms.WriteByte((byte)(adler >> 16));
        ms.WriteByte((byte)(adler >> 8));  ms.WriteByte((byte)adler);

        Chunk(fs, "IDAT", ms.ToArray());
        Chunk(fs, "IEND", Array.Empty<byte>());
    }

    private static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4]; BeInt(len, 0, data.Length);
        s.Write(len, 0, 4);

        var full = new byte[4 + data.Length];
        for (int i = 0; i < 4; i++) full[i] = (byte)type[i];
        Buffer.BlockCopy(data, 0, full, 4, data.Length);
        s.Write(full, 0, full.Length);

        var crc = new byte[4]; BeInt(crc, 0, unchecked((int)Crc32(full)));
        s.Write(crc, 0, 4);
    }

    private static void BeInt(byte[] b, int i, int v)
    {
        b[i] = (byte)(v >> 24); b[i + 1] = (byte)(v >> 16);
        b[i + 2] = (byte)(v >> 8); b[i + 3] = (byte)v;
    }

    private static uint[] _crcTable;
    private static uint Crc32(byte[] data)
    {
        if (_crcTable == null)
        {
            _crcTable = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                _crcTable[n] = c;
            }
        }
        uint crc = 0xFFFFFFFFu;
        foreach (byte b in data) crc = _crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFFu;
    }

    private static uint Adler32(byte[] data)
    {
        uint a = 1, b = 0;
        foreach (byte x in data) { a = (a + x) % 65521; b = (b + a) % 65521; }
        return (b << 16) | a;
    }
}
