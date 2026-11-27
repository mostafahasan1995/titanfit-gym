using Gym.Cafeteria.Hardware;

namespace Gym.Tests.Cafeteria;

public sealed class BarcodeWedgeTests
{
    /// <summary>Types each character <paramref name="gapMs"/> apart from <paramref name="start"/>; returns the time of the last key.</summary>
    private static long Type(BarcodeWedge wedge, string text, long start, int gapMs)
    {
        var at = start;
        foreach (var c in text)
        {
            wedge.Feed(c.ToString(), at);
            at += gapMs;
        }

        return at - gapMs;
    }

    [Fact]
    public void A_fast_burst_ending_in_Enter_is_a_scan()
    {
        var wedge = new BarcodeWedge();
        var last = Type(wedge, "6291001000011", 1_000, 8);

        Assert.Equal("6291001000011", wedge.Complete(last + 10));
        Assert.Equal("", wedge.Pending);
    }

    [Fact]
    public void Normal_typing_is_not_a_scan()
    {
        var wedge = new BarcodeWedge();
        var last = Type(wedge, "6291", 1_000, 180);

        Assert.Null(wedge.Complete(last + 150));
    }

    [Fact]
    public void Slow_keys_before_a_burst_are_dropped()
    {
        var wedge = new BarcodeWedge();
        Type(wedge, "ab", 1_000, 300);
        var last = Type(wedge, "5060495110124", 3_000, 5);

        Assert.Equal("5060495110124", wedge.Complete(last + 5));
    }

    [Fact]
    public void A_burst_shorter_than_the_minimum_is_not_a_scan()
    {
        var wedge = new BarcodeWedge(minLength: 4);
        var last = Type(wedge, "12", 1_000, 5);

        Assert.Null(wedge.Complete(last + 5));
    }

    [Fact]
    public void Enter_long_after_the_last_key_is_not_a_scan()
    {
        var wedge = new BarcodeWedge();
        var last = Type(wedge, "6291001000011", 1_000, 5);

        Assert.Null(wedge.Complete(last + 500));
    }

    [Fact]
    public void Feed_tells_when_a_new_burst_starts()
    {
        var wedge = new BarcodeWedge();

        Assert.True(wedge.Feed("6", 1_000));
        Assert.False(wedge.Feed("2", 1_010));
        Assert.True(wedge.Feed("9", 2_000));
        Assert.Equal("9", wedge.Pending);
    }

    [Fact]
    public void Control_characters_are_ignored_and_a_clock_wrap_starts_over()
    {
        var wedge = new BarcodeWedge();
        wedge.Feed("12\t34", 5_000);
        Assert.Equal("1234", wedge.Pending);

        Assert.True(wedge.Feed("9", 10));
        Assert.Equal("9", wedge.Pending);
    }

    [Fact]
    public void After_a_scan_the_next_one_starts_clean()
    {
        var wedge = new BarcodeWedge();
        var last = Type(wedge, "1111", 1_000, 5);
        Assert.Equal("1111", wedge.Complete(last + 5));

        last = Type(wedge, "2222", last + 20, 5);
        Assert.Equal("2222", wedge.Complete(last + 5));
    }
}
