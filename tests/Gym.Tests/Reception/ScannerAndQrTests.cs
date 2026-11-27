using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Gym.Domain;
using Gym.Reception.Hardware;
using Gym.Reception.Logic;

namespace Gym.Tests.Reception;

/// <summary>USB scanner keystrokes, check-in debounce and the member QR round trip.</summary>
public sealed class ScannerAndQrTests
{
    [Fact]
    public void A_fast_burst_ending_with_Enter_is_a_scan()
    {
        var wedge = new KeyboardWedge();
        var t = 1_000L;
        foreach (var c in "TF1-ABC")
        {
            wedge.Press(c, t);
            t += 8;
        }

        Assert.Equal("TF1-ABC", wedge.Enter(t));
        Assert.Null(wedge.Enter(t + 5)); // the buffer is empty again
    }

    [Fact]
    public void Slow_typing_is_not_a_scan()
    {
        var wedge = new KeyboardWedge();
        var t = 1_000L;
        foreach (var c in "00231")
        {
            wedge.Press(c, t);
            t += 140;
        }

        Assert.Null(wedge.Enter(t));
    }

    [Fact]
    public void A_pause_starts_a_new_burst_and_short_bursts_do_not_count()
    {
        var wedge = new KeyboardWedge();
        wedge.Press('9', 0);
        wedge.Press('9', 300); // typed by hand: the burst restarts here
        wedge.Press('0', 305);
        wedge.Press('0', 310);
        wedge.Press('2', 315);
        Assert.Equal("9002", wedge.Enter(320));

        wedge.Press('1', 1_000);
        wedge.Press('2', 1_005);
        Assert.Null(wedge.Enter(1_010)); // fewer than 3 characters
    }

    [Fact]
    public void Enter_long_after_the_last_key_is_not_a_scan()
    {
        var wedge = new KeyboardWedge();
        foreach (var (c, i) in "TF1-XYZ".Select((c, i) => (c, i)))
        {
            wedge.Press(c, i * 5L);
        }

        Assert.Null(wedge.Enter(1_000));
    }

    [Theory]
    [InlineData(Key.T, false, 'T')]
    [InlineData(Key.F, true, 'F')]
    [InlineData(Key.D1, false, '1')]
    [InlineData(Key.NumPad7, false, '7')]
    [InlineData(Key.OemMinus, false, '-')]
    [InlineData(Key.Subtract, false, '-')]
    public void Keys_map_to_code_characters_whatever_the_keyboard_layout(Key key, bool shift, char expected)
    {
        Assert.Equal(expected, KeyboardWedge.CharFor(key, shift));
    }

    [Theory]
    [InlineData(Key.D1, true)] // "!"
    [InlineData(Key.Space, false)]
    [InlineData(Key.OemComma, false)]
    public void Other_keys_are_not_part_of_a_code(Key key, bool shift)
    {
        Assert.Null(KeyboardWedge.CharFor(key, shift));
    }

    [Fact]
    public void The_same_code_checks_in_once_while_it_stays_in_view()
    {
        var debouncer = new CheckInDebouncer(TimeSpan.FromSeconds(5));
        var start = new DateTime(2026, 11, 27, 18, 0, 0, DateTimeKind.Utc);

        Assert.True(debouncer.ShouldCheckIn("TF1-AAA", start));
        for (var second = 1; second <= 20; second++)
        {
            Assert.False(debouncer.ShouldCheckIn("TF1-AAA", start.AddSeconds(second))); // seen every second
        }

        Assert.True(debouncer.ShouldCheckIn("TF1-AAA", start.AddSeconds(26))); // gone for 6 s, back again
    }

    [Fact]
    public void Different_codes_and_a_later_return_check_in()
    {
        var debouncer = new CheckInDebouncer(TimeSpan.FromSeconds(5));
        var start = new DateTime(2026, 11, 27, 18, 0, 0, DateTimeKind.Utc);

        Assert.True(debouncer.ShouldCheckIn("TF1-AAA", start));
        Assert.True(debouncer.ShouldCheckIn("TF1-BBB", start.AddSeconds(1)));
        Assert.False(debouncer.ShouldCheckIn(" tf1-aaa ", start.AddSeconds(3))); // same code, other case
        Assert.True(debouncer.ShouldCheckIn("TF1-AAA", start.AddSeconds(9)));

        debouncer.Clear();
        Assert.True(debouncer.ShouldCheckIn("TF1-BBB", start.AddSeconds(10)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(6)]
    public void The_member_QR_reads_back_as_the_same_code(int pixelsPerModule)
    {
        var code = MemberQrCode.Generate();

        var (pixels, size) = MemberQr.CreateGray(code, pixelsPerModule + 1);

        Assert.Equal(code, new QrDecoder(tryHarder: true).DecodeGray(pixels, size, size));
    }

    [Fact]
    public void The_member_QR_PNG_reads_back_as_the_same_code()
    {
        var code = MemberQrCode.Generate();
        var png = MemberQr.CreatePng(code);

        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png.Take(4).ToArray());
        var decoded = RunOnSta(() =>
        {
            using var stream = new MemoryStream(png);
            var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
            var gray = new FormatConvertedBitmap(frame, PixelFormats.Gray8, null, 0);
            var pixels = new byte[gray.PixelWidth * gray.PixelHeight];
            gray.CopyPixels(pixels, gray.PixelWidth, 0);
            return new QrDecoder(tryHarder: true).DecodeGray(pixels, gray.PixelWidth, gray.PixelHeight);
        });
        Assert.Equal(code, decoded);
    }

    [Fact]
    public void A_picture_without_a_QR_gives_nothing()
    {
        var pixels = Enumerable.Range(0, 200 * 200).Select(i => (byte)(i * 37 % 256)).ToArray();

        Assert.Null(new QrDecoder().DecodeGray(pixels, 200, 200));
    }

    [Theory]
    [InlineData(1280, 720, 280, 0, 720)]
    [InlineData(640, 480, 80, 0, 480)]
    [InlineData(480, 640, 0, 80, 480)]
    [InlineData(500, 500, 0, 0, 500)]
    public void The_photo_is_the_square_in_the_middle(int width, int height, int x, int y, int side)
    {
        Assert.Equal((x, y, side), PhotoCrop.CenterSquare(width, height));
    }

    [Fact]
    public void Cameras_with_the_same_name_get_a_number()
    {
        Assert.Equal(new[] { "USB Camera", "Logitech C920", "USB Camera (2)" }, CameraDevices.Label(["USB Camera", "Logitech C920", "USB Camera"]));
        Assert.Equal(2, CameraDevices.IndexOf(["USB Camera", "Logitech C920", "USB Camera (2)"], "USB Camera (2)"));
        Assert.Equal(0, CameraDevices.IndexOf(["USB Camera"], "Unplugged camera"));
    }

    [Theory]
    [InlineData("ok.wav")]
    [InlineData("warning.wav")]
    [InlineData("denied.wav")]
    public void The_check_in_sounds_are_embedded_wav_files(string name)
    {
        var wav = Sounds.Read(name);

        Assert.True(wav.Length > 4_000);
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal("WAVE", System.Text.Encoding.ASCII.GetString(wav, 8, 4));
    }

    private static T RunOnSta<T>(Func<T> work)
    {
        T result = default!;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = work();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null)
        {
            throw new InvalidOperationException("The STA work failed.", error);
        }

        return result;
    }
}
