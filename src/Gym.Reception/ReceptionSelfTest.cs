using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Gym.Domain;
using Gym.Reception.Hardware;
using Gym.Reception.Logic;
using Gym.UI;
using OpenCvSharp;

namespace Gym.Reception;

/// <summary>
/// The Reception app's <c>--self-test</c> checks (see <see cref="SelfTest"/>), without a camera, scanner or sound:
/// the OpenCV native library (it needs the Visual C++ 2015-2022 x64 runtime, which the Station setup installs; without
/// it the camera card says "Camera not available"), a camera-like frame through OpenCV and ZXing back to the member
/// code, the member QR image (QRCoder PNG read back by ZXing) and the embedded check-in sounds (read, not played).
/// </summary>
internal static class ReceptionSelfTest
{
    public static IReadOnlyList<SelfTestCheck> Checks { get; } =
    [
        new("OpenCV native library", OpenCvLibrary),
        new("Camera frame to member code (OpenCV + ZXing, no camera)", FrameToMemberCode),
        new("Member QR image (QRCoder PNG read by ZXing)", MemberQrPng),
        new("Check-in sounds (embedded, not played)", CheckInSounds),
    ];

    internal static string OpenCvLibrary() =>
        $"OpenCV {Cv2.GetVersionString()}, {SelfTest.LoadedModule("OpenCvSharpExtern.dll")}";

    /// <summary>The camera path of <see cref="CameraFeed"/>: a BGR frame, OpenCV's grey conversion, ZXing (camera settings).</summary>
    internal static string FrameToMemberCode()
    {
        var code = MemberQrCode.Generate();
        var (pixels, size) = MemberQr.CreateGray(code, 6);
        using var source = new Mat(size, size, MatType.CV_8UC1);
        Marshal.Copy(pixels, 0, source.Data, pixels.Length);
        using var frame = new Mat();
        Cv2.CvtColor(source, frame, ColorConversionCodes.GRAY2BGR);
        using var gray = new Mat();
        Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
        if (frame.Channels() != 3 || !gray.IsContinuous())
        {
            throw new InvalidOperationException($"Unexpected OpenCV result: {frame.Channels()} channels.");
        }

        var buffer = new byte[gray.Width * gray.Height];
        Marshal.Copy(gray.Data, buffer, 0, buffer.Length);
        var read = new QrDecoder().DecodeGray(buffer, gray.Width, gray.Height);
        return read == code
            ? $"{frame.Width}x{frame.Height} BGR frame, the code read back"
            : throw new InvalidOperationException($"ZXing read \"{read}\" instead of the code.");
    }

    /// <summary>The QR image staff copy, save and print, decoded by WPF and read by ZXing.</summary>
    internal static string MemberQrPng()
    {
        var code = MemberQrCode.Generate();
        var png = MemberQr.CreatePng(code);
        using var stream = new MemoryStream(png);
        var image = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        var gray = new FormatConvertedBitmap(image, PixelFormats.Gray8, null, 0);
        var pixels = new byte[gray.PixelWidth * gray.PixelHeight];
        gray.CopyPixels(pixels, gray.PixelWidth, 0);
        var read = new QrDecoder(tryHarder: true).DecodeGray(pixels, gray.PixelWidth, gray.PixelHeight);
        return read == code
            ? $"{png.Length}-byte PNG, {gray.PixelWidth}x{gray.PixelHeight}, the code read back"
            : throw new InvalidOperationException($"ZXing read \"{read}\" instead of the code.");
    }

    internal static string CheckInSounds()
    {
        var sounds = new List<string>();
        foreach (var name in new[] { "ok.wav", "warning.wav", "denied.wav" })
        {
            var wav = Sounds.Read(name);
            if (wav.Length < 1_000 || Encoding.ASCII.GetString(wav, 0, 4) != "RIFF" || Encoding.ASCII.GetString(wav, 8, 4) != "WAVE")
            {
                throw new InvalidDataException($"{name} is not a WAV file ({wav.Length} bytes).");
            }

            sounds.Add($"{name} {wav.Length} bytes");
        }

        return string.Join(", ", sounds);
    }
}
