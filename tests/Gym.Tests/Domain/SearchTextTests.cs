using Gym.Domain;

namespace Gym.Tests.Domain;

public sealed class SearchTextTests
{
    [Theory]
    [InlineData("أحمد", "احمد")]
    [InlineData("إبراهيم", "ابراهيم")]
    [InlineData("آمنة", "امنه")]
    [InlineData("مُحَمَّد", "محمد")] // diacritics removed
    [InlineData("مـحـمـد", "محمد")] // tatweel removed
    [InlineData("مصطفى", "مصطفي")]
    [InlineData("لؤي", "لوي")]
    [InlineData("  Ahmad   KHALED ", "ahmad khaled")]
    [InlineData("٠٩٤٤", "0944")]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    public void Normalize(string? input, string expected)
    {
        Assert.Equal(expected, SearchText.Normalize(input));
    }

    [Theory]
    [InlineData("0944 123-456", "0944123456")]
    [InlineData("+963 944 123 456", "+963944123456")]
    [InlineData("٠٩٤٤١٢٣٤٥٦", "0944123456")]
    [InlineData("  ", null)]
    [InlineData("abc", null)]
    public void Phone_numbers_are_stored_without_separators(string input, string? expected)
    {
        Assert.Equal(expected, PhoneNumber.Normalize(input));
    }

    [Theory]
    [InlineData("0944 123 456", "944123456")]
    [InlineData("+963 944 123 456", "944123456")]
    [InlineData("00963944123456", "944123456")]
    [InlineData("944123456", "944123456")]
    [InlineData("123", "123")]
    public void Phone_search_key_drops_country_code_and_trunk_zero(string input, string expected)
    {
        Assert.Equal(expected, PhoneNumber.SearchKey(input));
    }
}
