using System.Windows.Media;

namespace Gym.UI;

/// <summary>
/// Line icons from the design files (24 x 24 grid, drawn with a 1.8 stroke).
/// Converted from the SVG paths, circles and rects in design/*.dc.html.
/// </summary>
public static class UiIcons
{
    public static Geometry Scan { get; } = Make("M4,8 V5.5 A1.5,1.5 0 0 1 5.5,4 H8 M16,4 h2.5 A1.5,1.5 0 0 1 20,5.5 V8 M20,16 v2.5 a1.5,1.5 0 0 1 -1.5,1.5 H16 M8,20 H5.5 A1.5,1.5 0 0 1 4,18.5 V16 M4,12 h16");

    public static Geometry UserPlus { get; } = Make("M6,8 A4,4 0 1 0 14,8 A4,4 0 1 0 6,8 Z M3,20 c0,-3.3 3.1,-6 7,-6 c1.4,0 2.7,0.3 3.8,0.9 M18,14 v6 M15,17 h6");

    public static Geometry Users { get; } = Make("M5.5,8 A3.5,3.5 0 1 0 12.5,8 A3.5,3.5 0 1 0 5.5,8 Z M2.5,20 c0,-3 2.9,-5.5 6.5,-5.5 s6.5,2.5 6.5,5.5 M16,4.6 a3.5,3.5 0 0 1 0,6.8 M18.5,14.8 c1.8,0.8 3,2.6 3,5.2");

    public static Geometry Renew { get; } = Make("M20,11 a8,8 0 0 0 -14.6,-4.4 L4,8.5 M4,4 v4.5 h4.5 M4,13 a8,8 0 0 0 14.6,4.4 l1.4,-1.9 M20,20 v-4.5 h-4.5");

    public static Geometry Cart { get; } = Make("M3,4 h2 l2.2,10.5 a1,1 0 0 0 1,0.8 h9.4 a1,1 0 0 0 1,-0.8 L20,8 H6 M7.5,19 A1.5,1.5 0 1 0 10.5,19 A1.5,1.5 0 1 0 7.5,19 Z M15.5,19 A1.5,1.5 0 1 0 18.5,19 A1.5,1.5 0 1 0 15.5,19 Z");

    public static Geometry Box { get; } = Make("M3.5,7.5 L12,3 l8.5,4.5 v9 L12,21 l-8.5,-4.5 Z M3.5,7.5 L12,12 l8.5,-4.5 M12,12 v9");

    public static Geometry Receipt { get; } = Make("M6,3 h12 v18 l-3,-2 l-3,2 l-3,-2 l-3,2 Z M9,8 h6 M9,12 h6");

    public static Geometry Dashboard { get; } = Make("M5,3.5 H9 A1.5,1.5 0 0 1 10.5,5 V9 A1.5,1.5 0 0 1 9,10.5 H5 A1.5,1.5 0 0 1 3.5,9 V5 A1.5,1.5 0 0 1 5,3.5 Z M15,3.5 H19 A1.5,1.5 0 0 1 20.5,5 V9 A1.5,1.5 0 0 1 19,10.5 H15 A1.5,1.5 0 0 1 13.5,9 V5 A1.5,1.5 0 0 1 15,3.5 Z M5,13.5 H9 A1.5,1.5 0 0 1 10.5,15 V19 A1.5,1.5 0 0 1 9,20.5 H5 A1.5,1.5 0 0 1 3.5,19 V15 A1.5,1.5 0 0 1 5,13.5 Z M15,13.5 H19 A1.5,1.5 0 0 1 20.5,15 V19 A1.5,1.5 0 0 1 19,20.5 H15 A1.5,1.5 0 0 1 13.5,19 V15 A1.5,1.5 0 0 1 15,13.5 Z");

    public static Geometry Tag { get; } = Make("M3,12 V4.5 A1.5,1.5 0 0 1 4.5,3 H12 l9,9 l-9,9 Z M6.5,8 A1.5,1.5 0 1 0 9.5,8 A1.5,1.5 0 1 0 6.5,8 Z");

    public static Geometry Staff { get; } = Make("M6,3 H18 A2,2 0 0 1 20,5 V19 A2,2 0 0 1 18,21 H6 A2,2 0 0 1 4,19 V5 A2,2 0 0 1 6,3 Z M9,10 A3,3 0 1 0 15,10 A3,3 0 1 0 9,10 Z M8,17 c0.7,-1.8 2.2,-2.8 4,-2.8 s3.3,1 4,2.8");

    public static Geometry Reports { get; } = Make("M5,20 V11 M11,20 V5 M17,20 v-7 M3,20 h18");

    public static Geometry Settings { get; } = Make("M4,6 h10 M18,6 h2 M4,12 h4 M12,12 h8 M4,18 h12 M14,6 A2,2 0 1 0 18,6 A2,2 0 1 0 14,6 Z M8,12 A2,2 0 1 0 12,12 A2,2 0 1 0 8,12 Z M16,18 A2,2 0 1 0 20,18 A2,2 0 1 0 16,18 Z");

    public static Geometry Logout { get; } = Make("M9,21 H5 a2,2 0 0 1 -2,-2 V5 a2,2 0 0 1 2,-2 h4 M16,17 l5,-5 l-5,-5 M21,12 H9");

    private static Geometry Make(string pathData)
    {
        var geometry = Geometry.Parse(pathData);
        geometry.Freeze();
        return geometry;
    }
}
