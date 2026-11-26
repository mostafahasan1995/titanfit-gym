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

    public static Geometry Search { get; } = Make("M4,11 A7,7 0 1 0 18,11 A7,7 0 1 0 4,11 Z M20,20 L16,16");

    public static Geometry Close { get; } = Make("M7,7 L17,17 M17,7 L7,17");

    public static Geometry Check { get; } = Make("M5,12.5 L9.5,17 L19,7.5");

    public static Geometry Alert { get; } = Make("M12,4 L21,20 H3 Z M12,10 v4 M12,17 v0.5");

    public static Geometry Info { get; } = Make("M4,12 A8,8 0 1 0 20,12 A8,8 0 1 0 4,12 Z M12,11 v5 M12,7.8 v0.4");

    public static Geometry Clock { get; } = Make("M4,12 A8,8 0 1 0 20,12 A8,8 0 1 0 4,12 Z M12,8 v4.5 l2.5,1.5");

    public static Geometry Plus { get; } = Make("M6,12 h12 M12,6 v12");

    public static Geometry Minus { get; } = Make("M6,12 h12");

    public static Geometry ChevronDown { get; } = Make("M6,9.5 L12,15.5 L18,9.5");

    public static Geometry ChevronUp { get; } = Make("M6,14.5 L12,8.5 L18,14.5");

    public static Geometry ChevronLeft { get; } = Make("M14.5,6 L8.5,12 L14.5,18");

    public static Geometry ChevronRight { get; } = Make("M9.5,6 L15.5,12 L9.5,18");

    public static Geometry Camera { get; } = Make("M4,8 h3 l2,-3 h6 l2,3 h3 v11 H4 Z M8.5,13 A3.5,3.5 0 1 0 15.5,13 A3.5,3.5 0 1 0 8.5,13 Z");

    public static Geometry Qr { get; } = Make("M4,3 H9 A1,1 0 0 1 10,4 V9 A1,1 0 0 1 9,10 H4 A1,1 0 0 1 3,9 V4 A1,1 0 0 1 4,3 Z M15,3 H20 A1,1 0 0 1 21,4 V9 A1,1 0 0 1 20,10 H15 A1,1 0 0 1 14,9 V4 A1,1 0 0 1 15,3 Z M4,14 H9 A1,1 0 0 1 10,15 V20 A1,1 0 0 1 9,21 H4 A1,1 0 0 1 3,20 V15 A1,1 0 0 1 4,14 Z M14,14 h3 v3 h-3 Z M18,18 h3 v3 h-3 Z M18,14 h3 M14,18 v3");

    public static Geometry Barcode { get; } = Make("M4,7 V5 h3 M17,5 h3 v2 M20,17 v2 h-3 M7,19 H4 v-2 M7,9 v6 M10,9 v6 M13,9 v6 M16.5,9 v6");

    public static Geometry Trash { get; } = Make("M6,8 h12 M10,4.5 h4 M7,8 h10 l-1.2,11.2 a1,1 0 0 1 -1,0.8 H9.2 a1,1 0 0 1 -1,-0.8 Z");

    public static Geometry Cash { get; } = Make("M4.5,6 H19.5 A2,2 0 0 1 21.5,8 V16 A2,2 0 0 1 19.5,18 H4.5 A2,2 0 0 1 2.5,16 V8 A2,2 0 0 1 4.5,6 Z M9.5,12 A2.5,2.5 0 1 0 14.5,12 A2.5,2.5 0 1 0 9.5,12 Z M6,9.5 v5 M18,9.5 v5");

    public static Geometry Dumbbell { get; } = Make("M6,6.5 v11 M18,6.5 v11 M3,9.5 v5 M21,9.5 v5 M6,12 h12");

    public static Geometry Database { get; } = Make("M5,6 c0,-1.7 3.1,-3 7,-3 s7,1.3 7,3 s-3.1,3 -7,3 s-7,-1.3 -7,-3 Z M5,6 v12 c0,1.7 3.1,3 7,3 s7,-1.3 7,-3 V6 M5,12 c0,1.7 3.1,3 7,3 s7,-1.3 7,-3");

    public static Geometry Server { get; } = Make("M5,4 H19 A1.5,1.5 0 0 1 20.5,5.5 V9.5 A1.5,1.5 0 0 1 19,11 H5 A1.5,1.5 0 0 1 3.5,9.5 V5.5 A1.5,1.5 0 0 1 5,4 Z M5,13 H19 A1.5,1.5 0 0 1 20.5,14.5 V18.5 A1.5,1.5 0 0 1 19,20 H5 A1.5,1.5 0 0 1 3.5,18.5 V14.5 A1.5,1.5 0 0 1 5,13 Z M7,7.5 h0.5 M7,16.5 h0.5");

    public static Geometry User { get; } = Make("M8,8 A4,4 0 1 0 16,8 A4,4 0 1 0 8,8 Z M4,20 c0,-3.6 3.6,-6 8,-6 s8,2.4 8,6");

    public static Geometry Lock { get; } = Make("M6.5,11 H17.5 A1.5,1.5 0 0 1 19,12.5 V19.5 A1.5,1.5 0 0 1 17.5,21 H6.5 A1.5,1.5 0 0 1 5,19.5 V12.5 A1.5,1.5 0 0 1 6.5,11 Z M8,11 V7.5 a4,4 0 0 1 8,0 V11");

    public static Geometry Calendar { get; } = Make("M5.5,5 H18.5 A1.5,1.5 0 0 1 20,6.5 V19.5 A1.5,1.5 0 0 1 18.5,21 H5.5 A1.5,1.5 0 0 1 4,19.5 V6.5 A1.5,1.5 0 0 1 5.5,5 Z M4,10 h16 M8,3 v4 M16,3 v4");

    public static Geometry Star { get; } = Make("M12,3.5 l2.6,5.3 l5.8,0.8 l-4.2,4.1 l1,5.8 L12,16.8 l-5.2,2.7 l1,-5.8 L3.6,9.6 l5.8,-0.8 Z");

    public static Geometry Printer { get; } = Make("M7,9 V4 h10 v5 M7,17 H5 a1.5,1.5 0 0 1 -1.5,-1.5 V10.5 A1.5,1.5 0 0 1 5,9 h14 a1.5,1.5 0 0 1 1.5,1.5 v5 A1.5,1.5 0 0 1 19,17 h-2 M7,14 h10 v6 H7 Z");

    private static Geometry Make(string pathData)
    {
        var geometry = Geometry.Parse(pathData);
        geometry.Freeze();
        return geometry;
    }
}
