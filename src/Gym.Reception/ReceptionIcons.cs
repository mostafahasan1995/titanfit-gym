using System.Windows.Media;

namespace Gym.Reception;

/// <summary>Line icons only the Reception app needs (24 x 24 grid like <see cref="Gym.UI.UiIcons"/>).</summary>
public static class ReceptionIcons
{
    public static Geometry Phone { get; } = Make("M7,3.5 H10 L11.5,7.5 L9.5,9 A11,11 0 0 0 15,14.5 L16.5,12.5 L20.5,14 V17 A2,2 0 0 1 18.5,19 A15.5,15.5 0 0 1 5,5.5 A2,2 0 0 1 7,3.5 Z");

    public static Geometry Link { get; } = Make("M10,14 a4,4 0 0 0 5.7,0 l3,-3 a4,4 0 0 0 -5.7,-5.7 l-1,1 M14,10 a4,4 0 0 0 -5.7,0 l-3,3 a4,4 0 0 0 5.7,5.7 l1,-1");

    private static Geometry Make(string pathData)
    {
        var geometry = Geometry.Parse(pathData);
        geometry.Freeze();
        return geometry;
    }
}
