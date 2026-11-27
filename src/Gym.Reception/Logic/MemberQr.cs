using QRCoder;
using ZXing;
using ZXing.Common;

namespace Gym.Reception.Logic;

/// <summary>
/// The member QR (docs/PLAN.md, "Member QR"): it holds the member's random QR code text exactly as the server gives it
/// (<c>MemberDto.QrCode</c>, "TF1-" + 24 characters) and nothing else; check-in sends the same text back. Made with
/// QRCoder, read back with ZXing.Net (<see cref="QrDecoder"/>).
/// </summary>
public static class MemberQr
{
    /// <summary>Error correction Q (25 %): still readable from a scratched phone screen or a crumpled print.</summary>
    private const QRCodeGenerator.ECCLevel Level = QRCodeGenerator.ECCLevel.Q;

    /// <summary>A black-on-white PNG with the quiet zone, <paramref name="pixelsPerModule"/> pixels per module.</summary>
    public static byte[] CreatePng(string payload, int pixelsPerModule = 16)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        using var data = QRCodeGenerator.GenerateQrCode(payload, Level);
        using var png = new PngByteQRCode(data);
        return png.GetGraphic(pixelsPerModule);
    }

    /// <summary>The same code as 8-bit grey pixels (0 = dark, 255 = light), square, with a white border of 4 modules.</summary>
    public static (byte[] Pixels, int Size) CreateGray(string payload, int pixelsPerModule = 4)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        ArgumentOutOfRangeException.ThrowIfLessThan(pixelsPerModule, 1);
        using var data = QRCodeGenerator.GenerateQrCode(payload, Level);
        var matrix = data.ModuleMatrix;
        const int Border = 4;
        var modules = matrix.Count + 2 * Border;
        var size = modules * pixelsPerModule;
        var pixels = new byte[size * size];
        Array.Fill(pixels, (byte)255);
        for (var row = 0; row < matrix.Count; row++)
        {
            for (var column = 0; column < matrix[row].Length; column++)
            {
                if (!matrix[row][column])
                {
                    continue;
                }

                for (var y = 0; y < pixelsPerModule; y++)
                {
                    var start = ((row + Border) * pixelsPerModule + y) * size + (column + Border) * pixelsPerModule;
                    Array.Fill(pixels, (byte)0, start, pixelsPerModule);
                }
            }
        }

        return (pixels, size);
    }
}

/// <summary>Finds a QR code in a camera frame or an image (ZXing.Net). Not thread-safe: one per camera thread.</summary>
public sealed class QrDecoder
{
    private readonly BarcodeReaderGeneric reader;

    /// <param name="tryHarder">Slower and more thorough; for still images. Camera frames come many per second, so off there.</param>
    public QrDecoder(bool tryHarder = false)
    {
        reader = new BarcodeReaderGeneric
        {
            AutoRotate = false,
            Options = new DecodingOptions
            {
                PossibleFormats = [BarcodeFormat.QR_CODE],
                TryHarder = tryHarder,
            },
        };
    }

    /// <summary>The text of the QR code in 8-bit grey pixels (one byte per pixel, rows without padding), or null.</summary>
    public string? DecodeGray(byte[] pixels, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        return reader.Decode(pixels, width, height, RGBLuminanceSource.BitmapFormat.Gray8)?.Text;
    }
}
