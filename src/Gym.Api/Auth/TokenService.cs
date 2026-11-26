using System.Globalization;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Gym.Api.Auth;

/// <summary>Creates the access token (JWT) after a login. It carries the staff id, name, role and security stamp.</summary>
internal sealed class TokenService(JwtKeyProvider keys, IConfiguration configuration)
{
    public const string Issuer = "TitanFit";
    public const string Audience = "TitanFit";

    public LoginResponse CreateLogin(StaffMember staff)
    {
        ArgumentNullException.ThrowIfNull(staff);
        var role = staff.Role ?? throw new InvalidOperationException("This staff member has no login role.");
        var now = DateTime.UtcNow; // real time, not the business clock
        var expires = now.AddHours(Math.Clamp(configuration.GetValue("Jwt:LifetimeHours", 12), 1, 24 * 7));
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(keys.Key, SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                [GymClaims.StaffId] = staff.Id.ToString(CultureInfo.InvariantCulture),
                [GymClaims.Name] = staff.FullName,
                [GymClaims.Role] = role.ToString(),
                [GymClaims.SecurityStamp] = staff.SecurityStamp.ToString(),
            },
        };

        return new LoginResponse(new JsonWebTokenHandler().CreateToken(descriptor), expires, StaffService.ToDto(staff));
    }
}
