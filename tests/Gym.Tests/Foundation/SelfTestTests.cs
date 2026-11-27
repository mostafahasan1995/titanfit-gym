using Gym.Admin;
using Gym.Cafeteria;
using Gym.Reception;
using Gym.Tests.Cafeteria;
using Gym.UI;

namespace Gym.Tests.Foundation;

/// <summary>The <c>--self-test</c> runner and the checks of the three apps (natives load on the Windows test runner).</summary>
public sealed class SelfTestTests
{
    [Fact]
    public void All_passing_checks_give_one_line_each_and_exit_code_0()
    {
        using var output = new StringWriter();

        var code = SelfTest.Run("TITANFIT Test 0.1.0", [new("First", () => "fine"), new("Second", () => "two\r\nlines")], output);

        Assert.Equal(0, code);
        Assert.Equal(
            new[] { "PASS  First: fine", "PASS  Second: two lines", "TITANFIT Test 0.1.0 self-test: all 2 checks passed." },
            output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void A_failing_check_is_reported_with_its_inner_error_and_the_rest_still_runs()
    {
        using var output = new StringWriter();
        var ran = false;

        var code = SelfTest.Run("TITANFIT Test", [
            new("Native", () => throw new TypeInitializationException("OpenCvSharp.NativeMethods",
                new DllNotFoundException("Unable to load DLL 'OpenCvSharpExtern' or one of its dependencies."))),
            new("After", () => { ran = true; return "ok"; }),
        ], output);

        var lines = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(1, code);
        Assert.True(ran);
        Assert.Equal("FAIL  Native: DllNotFoundException: Unable to load DLL 'OpenCvSharpExtern' or one of its dependencies.", lines[0]);
        Assert.Equal("PASS  After: ok", lines[1]);
        Assert.Equal("TITANFIT Test self-test: 1 of 2 checks FAILED.", lines[2]);
    }

    [Fact]
    public void The_flag_is_found_in_any_case()
    {
        Assert.True(SelfTest.IsRequested(["--lang", "ar", "--SELF-TEST"]));
        Assert.False(SelfTest.IsRequested(["--smoke-screenshots", "out"]));
    }

    [Fact]
    public void Reception_checks_load_OpenCV_and_read_the_member_QR_back()
    {
        Assert.StartsWith("OpenCV 4.", ReceptionSelfTest.OpenCvLibrary(), StringComparison.Ordinal);
        Assert.Contains("the code read back", ReceptionSelfTest.FrameToMemberCode(), StringComparison.Ordinal);
        Assert.Contains("denied.wav", ReceptionSelfTest.CheckInSounds(), StringComparison.Ordinal);
        Sta.Run(() =>
        {
            Assert.Contains("the code read back", ReceptionSelfTest.MemberQrPng(), StringComparison.Ordinal);
            Assert.Same(ReceptionSelfTest.Checks, ReceptionApp.Definition.SelfTestChecks); // the app's pages build WPF icons
        });
    }

    [Fact]
    public void Admin_checks_load_SkiaSharp_and_HarfBuzz_and_draw_the_chart()
    {
        Assert.Contains("libSkiaSharp.dll", AdminSelfTest.SkiaSharpLibrary(), StringComparison.Ordinal);
        Assert.Contains("libHarfBuzzSharp.dll", AdminSelfTest.HarfBuzzLibrary(), StringComparison.Ordinal);
        Assert.Contains("column pixels", AdminSelfTest.DashboardChart(), StringComparison.Ordinal);
    }

    [Fact]
    public void Cafeteria_checks_draw_the_receipt_and_write_its_print_document()
    {
        Sta.Run(() =>
        {
            Assert.Contains("ar ", CafeteriaSelfTest.ReceiptDrawing(), StringComparison.Ordinal);
            Assert.Contains("1 page", CafeteriaSelfTest.ReceiptPrintDocument(), StringComparison.Ordinal);
        });
    }
}
