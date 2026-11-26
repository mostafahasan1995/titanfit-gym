using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Gym.Api.Auth;

internal static class AuthSetup
{
    public static IServiceCollection AddGymAuth(this IServiceCollection services)
    {
        services.AddSingleton<JwtKeyProvider>();
        services.AddSingleton<TokenService>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwtKeyProvider>((options, keys) =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = TokenService.Issuer,
                    ValidAudience = TokenService.Audience,
                    IssuerSigningKey = keys.Key,
                    ValidateIssuerSigningKey = true,
                    NameClaimType = GymClaims.Name,
                    RoleClaimType = GymClaims.Role,
                    ClockSkew = TimeSpan.FromMinutes(1),
                };
                options.Events = new JwtBearerEvents
                {
                    // Browsers and WebSockets cannot send headers to the hub; SignalR then passes the token in the query.
                    OnMessageReceived = context =>
                    {
                        var token = context.Request.Query["access_token"].ToString();
                        if (token.Length > 0 && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                        {
                            context.Token = token;
                        }

                        return Task.CompletedTask;
                    },

                    // A deactivated staff member or a changed password/role makes older tokens invalid at once.
                    OnTokenValidated = async context =>
                    {
                        var principal = context.Principal;
                        if (!int.TryParse(principal?.FindFirstValue(GymClaims.StaffId), out var staffId)
                            || !Guid.TryParse(principal?.FindFirstValue(GymClaims.SecurityStamp), out var stamp))
                        {
                            context.Fail("The token is missing the staff claims.");
                            return;
                        }

                        var staff = context.HttpContext.RequestServices.GetRequiredService<StaffService>();
                        if (!await staff.IsTokenValidAsync(staffId, stamp, context.HttpContext.RequestAborted))
                        {
                            context.Fail("This login is no longer valid. Log in again.");
                        }
                    },
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.Admin, policy => policy.RequireRole(GymRoles.Admin))
            .AddPolicy(Policies.Reception, policy => policy.RequireRole(GymRoles.Admin, GymRoles.Reception))
            .AddPolicy(Policies.Cafeteria, policy => policy.RequireRole(GymRoles.Admin, GymRoles.Cashier))
            .AddPolicy(Policies.AnyStaff, policy => policy.RequireRole(GymRoles.Admin, GymRoles.Reception, GymRoles.Cashier));
        return services;
    }
}
