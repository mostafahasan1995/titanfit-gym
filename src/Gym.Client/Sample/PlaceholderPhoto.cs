using System.Buffers.Binary;
using System.IO.Compression;

namespace Gym.Client.Sample;

/// <summary>
/// Makes a small PNG "member photo" (a light silhouette on a coloured gradient) so the sample data has photos
/// without shipping any real faces. Pure C#, no imaging library.
/// </summary>
public static class PlaceholderPhoto
{
    private static readonly (byte R, byte G, byte B)[] Palette =
    [
        (0x1D, 0x6F, 0x7E), (0x2A, 0x5C, 0x8A), (0x6B, 0x4E, 0x8C), (0x8A, 0x5A, 0x2E),
        (0x2E, 0x7D, 0x5B), (0x7A, 0x3E, 0x52), (0x3B, 0x5B, 0x6E), (0x5E, 0x6B, 0x2A),
    ];

    /// <summary>A PNG of <paramref name="size"/> x <paramref name="size"/> pixels; the colour depends on <paramref name="seed"/>.</summary>
    public static byte[] Create(int seed, int size = 160)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 16);
        var top = Palette[(int)((uint)seed % Palette.Length)];
        var bottom = (R: (byte)(top.R / 2), G: (byte)(top.G / 2), B: (byte)(top.B / 2));

        var raw = new byte[size * (size * 3 + 1)];
        double cx = size / 2.0, headY = size * 0.40, headR = size * 0.19;
        double bodyY = size * 1.04, bodyR = size * 0.38;
        var i = 0;
        for (var y = 0; y < size; y++)
        {
            raw[i++] = 0; // filter: none
            var t = (double)y / (size - 1);
            for (var x = 0; x < size; x++)
            {
                double r = Lerp(top.R, bottom.R, t), g = Lerp(top.G, bottom.G, t), b = Lerp(top.B, bottom.B, t);
                var inHead = Square(x - cx) + Square(y - headY) <= Square(headR);
                var inBody = Square(x - cx) + Square((y - bodyY) * 1.15) <= Square(bodyR);
                if (inHead || inBody)
                {
                    const double a = 0.72;
                    r = Lerp(r, 0xEE, a);
                    g = Lerp(g, 0xF7, a);
                    b = Lerp(b, 0xF9, a);
                }

                raw[i++] = (byte)r;
                raw[i++] = (byte)g;
                raw[i++] = (byte)b;
            }
        }

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0), size);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), size);
        header[8] = 8; // bit depth
        header[9] = 2; // colour type: RGB
        WriteChunk(png, "IHDR", header);
        using (var compressed = new MemoryStream())
        {
            using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
            {
                zlib.Write(raw);
            }

            WriteChunk(png, "IDAT", compressed.ToArray());
        }

        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    private static double Lerp(double from, double to, double t) => from + (to - from) * t;

    private static double Square(double value) => value * value;

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
        stream.Write(number);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes);
        stream.Write(data);
        var crc = Crc32(typeBytes, data);
        BinaryPrimitives.WriteUInt32BigEndian(number, crc);
        stream.Write(number);
    }

    private static uint Crc32(byte[] type, byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var part in new[] { type, data })
        {
            foreach (var value in part)
            {
                crc ^= value;
                for (var k = 0; k < 8; k++)
                {
                    crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
                }
            }
        }

        return crc ^ 0xFFFFFFFFu;
    }
}
