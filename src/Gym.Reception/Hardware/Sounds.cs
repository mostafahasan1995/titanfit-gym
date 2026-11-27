using System.IO;
using System.Runtime.InteropServices;
using Gym.Reception.Logic;

namespace Gym.Reception.Hardware;

/// <summary>
/// The check-in sounds (Sounds/*.wav, embedded): a bright double beep for OK, two even beeps for "expires soon" and a
/// low falling buzz for denied, so the staff hear the result without looking. Played asynchronously by Windows.
/// </summary>
internal static class Sounds
{
    private const uint SndAsync = 0x0001;
    private const uint SndNoDefault = 0x0002;
    private const uint SndMemory = 0x0004;

    private static readonly Lazy<IntPtr> Ok = new(() => Load("ok.wav"));
    private static readonly Lazy<IntPtr> Warning = new(() => Load("warning.wav"));
    private static readonly Lazy<IntPtr> Denied = new(() => Load("denied.wav"));

    public static void Play(CardTone tone)
    {
        var sound = tone switch
        {
            CardTone.Good => Ok,
            CardTone.Warning => Warning,
            CardTone.Bad => Denied,
            _ => null,
        };

        if (sound?.Value is { } pointer && pointer != IntPtr.Zero)
        {
            _ = PlaySound(pointer, IntPtr.Zero, SndAsync | SndMemory | SndNoDefault);
        }
    }

    /// <summary>The WAV bytes of an embedded sound (for the tests and the player).</summary>
    internal static byte[] Read(string name)
    {
        using var stream = typeof(Sounds).Assembly.GetManifestResourceStream("Gym.Reception.Sounds." + name)
            ?? throw new FileNotFoundException("Embedded sound not found.", name);
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    // Windows reads the sound from memory while it plays: the bytes stay pinned for the life of the app (3 small files).
    private static IntPtr Load(string name)
    {
        try
        {
            return GCHandle.Alloc(Read(name), GCHandleType.Pinned).AddrOfPinnedObject();
        }
        catch (FileNotFoundException)
        {
            return IntPtr.Zero;
        }
    }

    [DllImport("winmm.dll", EntryPoint = "PlaySoundW", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PlaySound(IntPtr sound, IntPtr module, uint flags);
}
