using System.Windows.Media;
using Gym.UI;

namespace Gym.Cafeteria;

/// <summary>Line icons for the product categories (24 x 24 grid, like <see cref="UiIcons"/>).</summary>
public static class CafeteriaIcons
{
    /// <summary>A cup with a straw.</summary>
    public static Geometry Drink { get; } = Make("M6.5,8 h11 l-1.3,11.2 a1.5,1.5 0 0 1 -1.5,1.3 H9.3 a1.5,1.5 0 0 1 -1.5,-1.3 Z M5.5,8 h13 M12.5,8 l1.5,-5 h3");

    /// <summary>A shaker bottle.</summary>
    public static Geometry Shake { get; } = Make("M8,8.5 h8 v10.5 a2,2 0 0 1 -2,2 h-4 a2,2 0 0 1 -2,-2 Z M9,8.5 v-3 a2.5,2.5 0 0 1 2.5,-2.5 h1 a2.5,2.5 0 0 1 2.5,2.5 v3 M8,13 h8");

    /// <summary>A snack bar.</summary>
    public static Geometry Snack { get; } = Make("M4.5,8 h15 a1.5,1.5 0 0 1 1.5,1.5 v5 a1.5,1.5 0 0 1 -1.5,1.5 h-15 a1.5,1.5 0 0 1 -1.5,-1.5 v-5 a1.5,1.5 0 0 1 1.5,-1.5 Z M8.5,8 v8 M15.5,8 v8");

    /// <summary>A supplement tub.</summary>
    public static Geometry Supplement { get; } = Make("M6.5,8.5 h11 v10 a2.5,2.5 0 0 1 -2.5,2.5 h-6 a2.5,2.5 0 0 1 -2.5,-2.5 Z M7.5,4 h9 a1,1 0 0 1 1,1 v3.5 h-11 V5 a1,1 0 0 1 1,-1 Z M9.5,13 h5 M9.5,16 h5");

    /// <summary>The icon for a category by its English name (Drinks, Shakes, Snacks, Supplements), else a tag.</summary>
    public static Geometry ForCategory(string? categoryNameEn)
    {
        var name = categoryNameEn?.Trim().ToUpperInvariant() ?? "";
        return name switch
        {
            _ when name.Contains("DRINK", StringComparison.Ordinal) => Drink,
            _ when name.Contains("SHAKE", StringComparison.Ordinal) => Shake,
            _ when name.Contains("SNACK", StringComparison.Ordinal) => Snack,
            _ when name.Contains("SUPPLEMENT", StringComparison.Ordinal) => Supplement,
            _ => UiIcons.Tag,
        };
    }

    private static Geometry Make(string pathData)
    {
        var geometry = Geometry.Parse(pathData);
        geometry.Freeze();
        return geometry;
    }
}
