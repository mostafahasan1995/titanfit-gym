using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Gym.Domain;

namespace Gym.Reception.Logic;

/// <summary>
/// The data of a Syrian ID card (docs/PLAN.md, "ID fields"): first name, father's name, surname, mother's name,
/// birth date, birth place, national number. Any of them may be missing from a scan.
/// </summary>
public sealed record IdCardFields
{
    public static IdCardFields Empty { get; } = new();

    public string? FirstName { get; init; }

    public string? FatherName { get; init; }

    public string? LastName { get; init; }

    public string? MotherName { get; init; }

    public DateOnly? BirthDate { get; init; }

    public string? BirthPlace { get; init; }

    /// <summary>11 digits.</summary>
    public string? NationalNo { get; init; }

    /// <summary>How many fields were found.</summary>
    public int Count =>
        new[] { FirstName, FatherName, LastName, MotherName, BirthPlace, NationalNo }.Count(v => !string.IsNullOrWhiteSpace(v))
        + (BirthDate is null ? 0 : 1);

    public bool IsEmpty => Count == 0;
}

/// <summary>
/// Reads the text a USB 2D scanner in keyboard (HID) mode types when it scans the barcode on the back of an ID card.
/// The real layout of the Syrian ID barcode is not known yet (open item 1): replace or extend the implementation when
/// a real sample arrives. Every field stays editable on the screen, whatever the parser finds.
/// </summary>
public interface IIdCardParser
{
    /// <summary>The fields found in <paramref name="raw"/>. Never throws; text it does not understand gives <see cref="IdCardFields.Empty"/>.</summary>
    IdCardFields Parse(string? raw);
}

/// <summary>
/// Placeholder parser until a real ID scan is available. It accepts:
/// <list type="bullet">
/// <item>fields split by common separators (new line, tab, |, ;, #, ^, *, ~, the ASCII group separators) or, when
/// there are none, by spaces;</item>
/// <item>labelled fields ("الاسم: عمر", "Mother: Salma", "الرقم الوطني=01020045871") in any order;</item>
/// <item>unlabelled fields in the card order: first name, father, surname, mother, birth place;</item>
/// <item>the national number as the first run of exactly 11 digits, the birth date as d/m/yyyy, yyyy-m-d or 8 digits
/// (yyyymmdd or ddmmyyyy), with - / . as separators; Arabic-Indic digits are read too.</item>
/// </list>
/// Numbers that are neither (card serials, codes) are ignored.
/// </summary>
public sealed partial class TolerantIdCardParser : IIdCardParser
{
    public const int MaxFieldLength = 100;

    private static readonly char[] Separators = ['\r', '\n', '\t', '|', ';', '#', '^', '*', '~', '\u001C', '\u001D', '\u001E', '\u001F'];

    private static readonly (string Key, Field Field)[] Labels =
    [
        ("الاسم الأول", Field.First), ("الاسم", Field.First), ("first name", Field.First), ("first", Field.First), ("name", Field.First),
        ("اسم الأب", Field.Father), ("الأب", Field.Father), ("father's name", Field.Father), ("father", Field.Father),
        ("النسبة", Field.Last), ("الكنية", Field.Last), ("اللقب", Field.Last), ("surname", Field.Last), ("last name", Field.Last), ("family name", Field.Last),
        ("اسم الأم", Field.Mother), ("الأم", Field.Mother), ("mother's name", Field.Mother), ("mother", Field.Mother),
        ("تاريخ الولادة", Field.BirthDate), ("تاريخ الميلاد", Field.BirthDate), ("date of birth", Field.BirthDate), ("birth date", Field.BirthDate), ("dob", Field.BirthDate),
        ("مكان الولادة", Field.BirthPlace), ("محل الولادة", Field.BirthPlace), ("مكان الميلاد", Field.BirthPlace), ("place of birth", Field.BirthPlace), ("birth place", Field.BirthPlace),
        ("الرقم الوطني", Field.NationalNo), ("national number", Field.NationalNo), ("national no", Field.NationalNo), ("national", Field.NationalNo),
    ];

    private static readonly Field[] CardOrder = [Field.First, Field.Father, Field.Last, Field.Mother, Field.BirthPlace];

    private enum Field
    {
        First,
        Father,
        Last,
        Mother,
        BirthDate,
        BirthPlace,
        NationalNo,
    }

    public IdCardFields Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return IdCardFields.Empty;
        }

        var text = SearchText.NormalizeDigits(raw);
        var tokens = text.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (tokens.Count <= 1)
        {
            tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }

        var found = new Dictionary<Field, string>();
        var unlabelled = new List<string>();
        foreach (var token in tokens)
        {
            if (SplitLabel(token) is { } labelled)
            {
                if (labelled.Value.Length > 0 && !found.ContainsKey(labelled.Field))
                {
                    found[labelled.Field] = labelled.Value;
                }
            }
            else
            {
                unlabelled.Add(token);
            }
        }

        string? nationalNo = found.TryGetValue(Field.NationalNo, out var labelledNo) ? ElevenDigits(labelledNo) : null;
        DateOnly? birthDate = found.TryGetValue(Field.BirthDate, out var labelledDate) ? ReadDate(labelledDate) : null;

        var names = new List<string>();
        foreach (var token in unlabelled)
        {
            if (nationalNo is null && ElevenDigits(token) is { } number)
            {
                nationalNo = number;
                continue;
            }

            if (birthDate is null && ReadDate(token) is { } date)
            {
                birthDate = date;
                continue;
            }

            if (token.Any(char.IsLetter))
            {
                names.Add(Clean(token));
            }
        }

        // Unlabelled words fill the free fields in the card order.
        var queue = new Queue<string>(names.Where(n => n.Length > 0));
        foreach (var field in CardOrder)
        {
            if (!found.ContainsKey(field) && queue.Count > 0)
            {
                found[field] = queue.Dequeue();
            }
        }

        string? Get(Field field) => found.TryGetValue(field, out var value) && value.Length > 0 ? Limit(Clean(value)) : null;
        return new IdCardFields
        {
            FirstName = Get(Field.First),
            FatherName = Get(Field.Father),
            LastName = Get(Field.Last),
            MotherName = Get(Field.Mother),
            BirthPlace = Get(Field.BirthPlace),
            BirthDate = birthDate,
            NationalNo = nationalNo,
        };
    }

    /// <summary>A date as printed or encoded: d/m/yyyy (day first, the Syrian order), yyyy-m-d, yyyymmdd or ddmmyyyy.</summary>
    public static DateOnly? ReadDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var value = SearchText.NormalizeDigits(text.Trim());
        var match = DatePattern().Match(value);
        if (match.Success)
        {
            var a = int.Parse(match.Groups["a"].Value, CultureInfo.InvariantCulture);
            var b = int.Parse(match.Groups["b"].Value, CultureInfo.InvariantCulture);
            var c = int.Parse(match.Groups["c"].Value, CultureInfo.InvariantCulture);
            return match.Groups["a"].Value.Length == 4 ? Make(a, b, c) : Make(c, b, a);
        }

        if (value.Length == 8 && value.All(char.IsAsciiDigit))
        {
            var yearFirst = Make(Number(value, 0, 4), Number(value, 4, 2), Number(value, 6, 2));
            return yearFirst ?? Make(Number(value, 4, 4), Number(value, 2, 2), Number(value, 0, 2));
        }

        return null;
    }

    private static DateOnly? Make(int year, int month, int day)
    {
        if (year is < 1900 or > 2100 || month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
        {
            return null;
        }

        return new DateOnly(year, month, day);
    }

    private static int Number(string text, int start, int length) =>
        int.Parse(text.AsSpan(start, length), NumberStyles.None, CultureInfo.InvariantCulture);

    private static string? ElevenDigits(string token)
    {
        var match = NationalNoPattern().Match(token);
        if (!match.Success)
        {
            return null;
        }

        // A token that is mostly letters ("ID 01020045871") still counts; one with other long numbers does not.
        return token.Count(char.IsAsciiDigit) == 11 ? match.Value : null;
    }

    private static (Field Field, string Value)? SplitLabel(string token)
    {
        var index = token.IndexOfAny([':', '=']);
        if (index <= 0)
        {
            return null;
        }

        var key = SearchText.Normalize(token[..index]);
        var value = token[(index + 1)..].Trim();
        foreach (var (label, field) in Labels)
        {
            if (key == SearchText.Normalize(label))
            {
                return (field, value);
            }
        }

        return null;
    }

    private static string Clean(string value)
    {
        var builder = new StringBuilder(value.Length);
        var space = false;
        foreach (var c in value.Trim())
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c))
            {
                space = builder.Length > 0;
                continue;
            }

            if (space)
            {
                builder.Append(' ');
                space = false;
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    private static string Limit(string value) => value.Length <= MaxFieldLength ? value : value[..MaxFieldLength];

    [GeneratedRegex(@"(?<![0-9])[0-9]{11}(?![0-9])")]
    private static partial Regex NationalNoPattern();

    [GeneratedRegex(@"^(?<a>[0-9]{1,4})[-/.](?<b>[0-9]{1,2})[-/.](?<c>[0-9]{1,4})$")]
    private static partial Regex DatePattern();
}
