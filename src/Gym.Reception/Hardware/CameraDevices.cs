using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Gym.Reception.Hardware;

/// <summary>
/// The webcams of this PC by their Windows names, in DirectShow order (the same order OpenCV's DirectShow backend uses
/// for its camera index). Two cameras with the same name get "(2)", "(3)" after the name.
/// </summary>
internal static class CameraDevices
{
    private static readonly Guid SystemDeviceEnum = new("62BE5D10-60EB-11D0-BD3B-00A0C911CE86");
    private static readonly Guid VideoInputDeviceCategory = new("860BB310-5D01-11D0-BD3B-00A0C911CE86");

    /// <summary>The camera names; empty when there is none or DirectShow is not available.</summary>
    public static IReadOnlyList<string> List()
    {
        var names = new List<string>();
        try
        {
            var type = Type.GetTypeFromCLSID(SystemDeviceEnum, throwOnError: false);
            if (type is null || Activator.CreateInstance(type) is not ICreateDevEnum devices)
            {
                return names;
            }

            var category = VideoInputDeviceCategory;
            if (devices.CreateClassEnumerator(ref category, out var monikers, 0) != 0 || monikers is null)
            {
                return names; // S_FALSE: no camera
            }

            try
            {
                var one = new IMoniker[1];
                while (monikers.Next(1, one, IntPtr.Zero) == 0)
                {
                    var moniker = one[0];
                    try
                    {
                        names.Add(FriendlyName(moniker) ?? $"Camera {names.Count + 1}");
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(moniker);
                    }
                }
            }
            finally
            {
                Marshal.ReleaseComObject(monikers);
                Marshal.ReleaseComObject(devices);
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or UnauthorizedAccessException or NotSupportedException)
        {
            // No DirectShow: the picker stays empty and the page says "No camera".
        }

        return Label(names);
    }

    /// <summary>The DirectShow index of a camera label from <see cref="List"/>, or 0 (the first camera) when it is not found.</summary>
    public static int IndexOf(IReadOnlyList<string> labels, string? label)
    {
        ArgumentNullException.ThrowIfNull(labels);
        for (var i = 0; i < labels.Count; i++)
        {
            if (string.Equals(labels[i], label, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return 0;
    }

    /// <summary>Makes duplicate names unique: "USB Camera", "USB Camera (2)".</summary>
    internal static IReadOnlyList<string> Label(IReadOnlyList<string> names)
    {
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        var labels = new List<string>(names.Count);
        foreach (var name in names)
        {
            var count = seen.GetValueOrDefault(name) + 1;
            seen[name] = count;
            labels.Add(count == 1 ? name : $"{name} ({count})");
        }

        return labels;
    }

    private static string? FriendlyName(IMoniker moniker)
    {
        var iid = typeof(IPropertyBag).GUID;
        moniker.BindToStorage(null!, null, ref iid, out var storage);
        try
        {
            return storage is IPropertyBag bag && bag.Read("FriendlyName", out var value, IntPtr.Zero) == 0 && value is string { Length: > 0 } name
                ? name
                : null;
        }
        finally
        {
            if (storage is not null && Marshal.IsComObject(storage))
            {
                Marshal.ReleaseComObject(storage);
            }
        }
    }

    [ComImport]
    [Guid("29840822-5B84-11D0-BD3B-00A0C911CE86")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ICreateDevEnum
    {
        [PreserveSig]
        int CreateClassEnumerator([In] ref Guid deviceClass, out IEnumMoniker? enumMoniker, int flags);
    }

    [ComImport]
    [Guid("55272A00-42CB-11CE-8135-00AA004BB851")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyBag
    {
        [PreserveSig]
        int Read([In, MarshalAs(UnmanagedType.LPWStr)] string propertyName, [MarshalAs(UnmanagedType.Struct)] out object? value, IntPtr errorLog);

        [PreserveSig]
        int Write([In, MarshalAs(UnmanagedType.LPWStr)] string propertyName, [In, MarshalAs(UnmanagedType.Struct)] ref object value);
    }
}
