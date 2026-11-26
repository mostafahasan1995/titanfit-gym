using System.Security.Cryptography;
using System.Text;
using Gym.Infrastructure.Storage;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Gym.Api.Auth;

/// <summary>
/// The token signing key: <c>Jwt:Key</c> from the configuration if set, otherwise a random key created once in
/// <c>{Storage:DataPath}/keys/jwt-signing.key</c>. No key is ever committed to the repository.
/// </summary>
internal sealed class JwtKeyProvider(IConfiguration configuration, IOptions<StorageOptions> storage, ILogger<JwtKeyProvider> logger)
{
    private const int MinKeyBytes = 32;
    private readonly Lazy<SymmetricSecurityKey> key = new(() => Load(configuration, storage.Value, logger));

    public SymmetricSecurityKey Key => key.Value;

    private static SymmetricSecurityKey Load(IConfiguration configuration, StorageOptions storage, ILogger logger)
    {
        var configured = configuration["Jwt:Key"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var bytes = Encoding.UTF8.GetBytes(configured);
            return bytes.Length >= MinKeyBytes
                ? new SymmetricSecurityKey(bytes)
                : throw new InvalidOperationException($"Jwt:Key must be at least {MinKeyBytes} bytes long.");
        }

        var path = Path.Combine(storage.KeysPath, "jwt-signing.key");
        if (File.Exists(path))
        {
            return new SymmetricSecurityKey(Convert.FromBase64String(File.ReadAllText(path).Trim()));
        }

        Directory.CreateDirectory(storage.KeysPath);
        var generated = RandomNumberGenerator.GetBytes(64);
        File.WriteAllText(path, Convert.ToBase64String(generated));
        logger.LogInformation("Created a new token signing key at {Path}.", path);
        return new SymmetricSecurityKey(generated);
    }
}
