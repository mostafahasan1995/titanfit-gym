namespace Gym.UI;

/// <summary>Strings shared by every app shell.</summary>
public static class CommonText
{
    public static LocalText LogOut { get; } = new("Log out", "تسجيل الخروج");

    public static LocalText Language { get; } = new("Language", "اللغة");

    public static LocalText NotSignedIn { get; } = new("Not signed in", "لم يتم تسجيل الدخول");

    public static LocalText ServerNotConnected { get; } = new("Server not connected", "الخادم غير متصل");

    public static LocalText ComingSoonTitle { get; } = new("This screen is coming next", "هذه الشاشة قادمة قريبًا");

    public static LocalText ComingSoonText { get; } = new(
        "The TITANFIT theme, logo and layout are in place. The screens are built in the next steps.",
        "هوية TITANFIT والشعار والتخطيط جاهزة. تُبنى الشاشات في الخطوات القادمة.");
}
