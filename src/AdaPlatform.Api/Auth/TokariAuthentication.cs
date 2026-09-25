using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AdaPlatform.Api.Auth;

/// <summary>
/// Sign-in through Tokari: every request carries Tokari's access token as a bearer token,
/// validated here with the shared key. Every endpoint needs a signed-in user unless it
/// opts out; each feature additionally needs its permission.
/// </summary>
public static class TokariAuthentication
{
    public static IServiceCollection AddTokariAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<TokariOptions>()
            .Bind(configuration.GetSection(TokariOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<TokariOptions>>((bearer, tokari) =>
            {
                // Keep Tokari's claim types as issued (it uses the long ClaimTypes URIs).
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = ValidationParameters(tokari.Value);
                bearer.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        AddPermissionClaims(context.Principal!, tokari.Value.Audience);
                        return Task.CompletedTask;
                    },
                };
            });

        services.AddAuthorizationBuilder()
            // Secure by default: an endpoint without its own policy still needs a user.
            // Public endpoints say so with AllowAnonymous().
            .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());
        foreach (var permission in Permissions.All)
        {
            services.AddAuthorizationBuilder().AddPolicy(permission, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(Permissions.ClaimType, permission));
        }

        return services;
    }

    /// <summary>
    /// What a Tokari token must satisfy to be accepted here. Also used to check the tokens
    /// the login proxy receives, so the browser never gets one this API would reject.
    /// </summary>
    public static TokenValidationParameters ValidationParameters(TokariOptions tokari) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = tokari.Issuer,
        // Tokari lists every app the user may use in aud; this app must be one of them.
        ValidateAudience = true,
        ValidAudience = tokari.Audience,
        ValidateLifetime = true,
        RequireExpirationTime = true,
        // Tokari's tokens live 5 minutes; allow for small clock drift, not more.
        ClockSkew = TimeSpan.FromSeconds(30),
        ValidateIssuerSigningKey = true,
        RequireSignedTokens = true,
        IssuerSigningKey = new SymmetricSecurityKey(tokari.SigningKeyBytes()),
        // Only the algorithm Tokari uses: "none", or a swap to another algorithm, is refused.
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        NameClaimType = ClaimTypes.Name,
        RoleClaimType = ClaimTypes.Role,
    };

    /// <summary>Copies this app's app_access permissions into <see cref="Permissions.ClaimType"/> claims.</summary>
    public static void AddPermissionClaims(ClaimsPrincipal user, string appName)
    {
        if (user.Identity is not ClaimsIdentity identity)
        {
            return;
        }
        foreach (var permission in AppAccess.For(user, appName).Permissions)
        {
            identity.AddClaim(new Claim(Permissions.ClaimType, permission));
        }
    }
}
