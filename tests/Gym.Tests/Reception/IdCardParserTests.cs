using Gym.Reception.Logic;
using static Gym.Tests.Domain.Dates;

namespace Gym.Tests.Reception;

/// <summary>The placeholder ID parser (the real Syrian ID barcode layout is still unknown, open item 1).</summary>
public sealed class IdCardParserTests
{
    private readonly TolerantIdCardParser parser = new();

    [Theory]
    [InlineData("عمر|خالد|الخطيب|سلمى|1994-03-02|دمشق|01020045871")]
    [InlineData("عمر\nخالد\nالخطيب\nسلمى\n02/03/1994\nدمشق\n01020045871")]
    [InlineData("عمر\tخالد\tالخطيب\tسلمى\t02.03.1994\tدمشق\t01020045871\r\n")]
    [InlineData("عمر;خالد;الخطيب;سلمى;19940302;دمشق;01020045871")]
    public void Separated_fields_in_card_order_are_read(string raw)
    {
        var fields = parser.Parse(raw);

        AssertOmar(fields);
        Assert.Equal(7, fields.Count);
    }

    [Fact]
    public void Numbers_can_come_first_and_in_Arabic_Indic_digits()
    {
        var fields = parser.Parse("٠١٠٢٠٠٤٥٨٧١|عمر|خالد|الخطيب|سلمى|٠٢/٠٣/١٩٩٤|دمشق");

        AssertOmar(fields);
    }

    [Fact]
    public void Labelled_fields_are_read_in_any_order()
    {
        var fields = parser.Parse(
            "الرقم الوطني: 01020045871\nاسم الأم: سلمى\nالنسبة: الخطيب\nالاسم: عمر\nاسم الأب: خالد\nمكان الولادة: دمشق\nتاريخ الولادة: 1994/03/02");

        AssertOmar(fields);
    }

    [Fact]
    public void English_labels_work_too()
    {
        var fields = parser.Parse("Surname=Khoury|First name=Maya|Father=George|Mother=Rita|DOB=1997-06-15|Place of birth=Damascus|National no=01050023987");

        Assert.Equal("Maya", fields.FirstName);
        Assert.Equal("George", fields.FatherName);
        Assert.Equal("Khoury", fields.LastName);
        Assert.Equal("Rita", fields.MotherName);
        Assert.Equal(D("1997-06-15"), fields.BirthDate);
        Assert.Equal("Damascus", fields.BirthPlace);
        Assert.Equal("01050023987", fields.NationalNo);
    }

    [Fact]
    public void A_single_line_with_spaces_is_split_on_the_spaces()
    {
        var fields = parser.Parse("عمر خالد الخطيب سلمى 1994-03-02 دمشق 01020045871");

        AssertOmar(fields);
    }

    [Fact]
    public void Other_numbers_are_ignored()
    {
        var fields = parser.Parse("SY12345|عمر|خالد|الخطيب|سلمى|1994-03-02|دمشق|01020045871|000123");

        Assert.Equal("01020045871", fields.NationalNo);
        Assert.Equal("SY12345", fields.FirstName); // a code with letters reads as a word: staff fix it on screen
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("6291001000011")] // a product barcode
    [InlineData("12345")]
    public void Text_without_ID_data_gives_no_fields(string? raw)
    {
        Assert.True(parser.Parse(raw).IsEmpty);
    }

    [Fact]
    public void A_partial_scan_keeps_what_it_found()
    {
        var fields = parser.Parse("عمر|خالد|01020045871");

        Assert.Equal("عمر", fields.FirstName);
        Assert.Equal("خالد", fields.FatherName);
        Assert.Null(fields.LastName);
        Assert.Null(fields.BirthDate);
        Assert.Equal("01020045871", fields.NationalNo);
        Assert.Equal(3, fields.Count);
    }

    [Fact]
    public void Extra_spaces_inside_names_are_collapsed_and_long_values_cut()
    {
        var fields = parser.Parse("عبد   الله|" + new string('ا', 150));

        Assert.Equal("عبد الله", fields.FirstName);
        Assert.Equal(TolerantIdCardParser.MaxFieldLength, fields.FatherName!.Length);
    }

    [Theory]
    [InlineData("02/03/1994", "1994-03-02")]
    [InlineData("2/3/1994", "1994-03-02")]
    [InlineData("1994-03-02", "1994-03-02")]
    [InlineData("1994/3/2", "1994-03-02")]
    [InlineData("19940302", "1994-03-02")]
    [InlineData("02031994", "1994-03-02")]
    [InlineData("29/02/2000", "2000-02-29")]
    public void Dates_are_read_day_first(string text, string expected)
    {
        Assert.Equal(D(expected), TolerantIdCardParser.ReadDate(text));
    }

    [Theory]
    [InlineData("31/02/1994")]
    [InlineData("29/02/2001")]
    [InlineData("02/03/94")]
    [InlineData("1994")]
    [InlineData("abc")]
    [InlineData(null)]
    public void Impossible_or_incomplete_dates_are_not_read(string? text)
    {
        Assert.Null(TolerantIdCardParser.ReadDate(text));
    }

    private static void AssertOmar(IdCardFields fields)
    {
        Assert.Equal("عمر", fields.FirstName);
        Assert.Equal("خالد", fields.FatherName);
        Assert.Equal("الخطيب", fields.LastName);
        Assert.Equal("سلمى", fields.MotherName);
        Assert.Equal(D("1994-03-02"), fields.BirthDate);
        Assert.Equal("دمشق", fields.BirthPlace);
        Assert.Equal("01020045871", fields.NationalNo);
    }
}
