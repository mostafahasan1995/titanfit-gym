using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace Gym.Infrastructure.Storage;

/// <summary>Member photos on the server disk (JPEG or PNG, up to 5 MB).</summary>
public sealed class PhotoStore(IOptions<StorageOptions> options)
{
    public const int MaxBytes = 5 * 1024 * 1024;

    private string Folder => options.Value.PhotosPath;

    /// <summary>".jpg" or ".png" for a supported content type, otherwise null.</summary>
    public static string? ExtensionFor(string? contentType) =>
        contentType?.Split(';')[0].Trim().ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/png" => ".png",
            _ => null,
        };

    public static string ContentTypeFor(string fileName) =>
        Path.GetExtension(fileName).Equals(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg";

    /// <summary>Saves the photo under a new file name and returns that name.</summary>
    public async Task<string> SaveAsync(Guid memberId, Stream content, string extension, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var bytes = await ReadLimitedAsync(content, ct);
        if (!LooksLike(bytes, extension))
        {
            throw new DomainException("invalid_photo", "The file is not a JPEG or PNG image.");
        }

        Directory.CreateDirectory(Folder);
        var name = $"{memberId:N}-{RandomNumberGenerator.GetHexString(12, lowercase: true)}{extension}";
        var path = Path.Combine(Folder, name);
        var temp = path + ".tmp";
        await File.WriteAllBytesAsync(temp, bytes, ct);
        File.Move(temp, path, overwrite: true);
        return name;
    }

    /// <summary>Full path of an existing photo, or null.</summary>
    public string? GetPath(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var path = Path.Combine(Folder, Path.GetFileName(fileName));
        return File.Exists(path) ? path : null;
    }

    public void Delete(string? fileName)
    {
        var path = GetPath(fileName);
        if (path is null)
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // An old photo that cannot be deleted now is harmless.
        }
    }

    private static async Task<byte[]> ReadLimitedAsync(Stream content, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > MaxBytes)
            {
                throw new DomainException("photo_too_large", $"The photo can be at most {MaxBytes / (1024 * 1024)} MB.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static bool LooksLike(byte[] bytes, string extension) => extension switch
    {
        ".jpg" => bytes is [0xFF, 0xD8, 0xFF, ..],
        ".png" => bytes is [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, ..],
        _ => false,
    };
}
