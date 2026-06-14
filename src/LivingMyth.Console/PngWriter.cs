using System;
using System.IO;
using System.IO.Compression;

// Minimal, dependency-free PNG encoder (8-bit truecolor, RGB). Just enough to write the atlas
// the SurfacePainter renders to a real PNG so the headless `paint` command produces faithful
// screenshot evidence on any machine — no System.Drawing, no native libs.
internal static class PngWriter
{
    // rgb: row-major, 3 bytes/pixel, length = width*height*3.
    public static void Write(string path, int width, int height, byte[] rgb)
    {
        using var fs = File.Create(path);
        Span<byte> sig = stackalloc byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        fs.Write(sig);

        var ihdr = new byte[13];
        WriteBE(ihdr, 0, width);
        WriteBE(ihdr, 4, height);
        ihdr[8] = 8;    // bit depth
        ihdr[9] = 2;    // color type: truecolor RGB
        ihdr[10] = 0;   // compression
        ihdr[11] = 0;   // filter
        ihdr[12] = 0;   // interlace
        Chunk(fs, "IHDR", ihdr);

        // Filtered scanlines: filter byte 0 (None) then the row's RGB bytes.
        var raw = new byte[height * (1 + width * 3)];
        int o = 0;
        for (int y = 0; y < height; y++)
        {
            raw[o++] = 0;
            Array.Copy(rgb, y * width * 3, raw, o, width * 3);
            o += width * 3;
        }
        using var comp = new MemoryStream();
        using (var z = new ZLibStream(comp, CompressionLevel.Optimal, leaveOpen: true))
            z.Write(raw, 0, raw.Length);
        Chunk(fs, "IDAT", comp.ToArray());

        Chunk(fs, "IEND", Array.Empty<byte>());
    }

    private static void WriteBE(byte[] b, int i, int v)
    {
        b[i] = (byte)(v >> 24); b[i + 1] = (byte)(v >> 16); b[i + 2] = (byte)(v >> 8); b[i + 3] = (byte)v;
    }

    private static void Chunk(Stream fs, string type, byte[] data)
    {
        var len = new byte[4];
        WriteBE(len, 0, data.Length);
        fs.Write(len);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        fs.Write(typeBytes);
        fs.Write(data);
        uint crc = Crc32(typeBytes, data);
        var crcBytes = new byte[4];
        WriteBE(crcBytes, 0, (int)crc);
        fs.Write(crcBytes);
    }

    private static readonly uint[] CrcTable = BuildCrcTable();
    private static uint[] BuildCrcTable()
    {
        var t = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            t[n] = c;
        }
        return t;
    }

    private static uint Crc32(byte[] a, byte[] b)
    {
        uint c = 0xFFFFFFFFu;
        foreach (var x in a) c = CrcTable[(c ^ x) & 0xff] ^ (c >> 8);
        foreach (var x in b) c = CrcTable[(c ^ x) & 0xff] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }
}
