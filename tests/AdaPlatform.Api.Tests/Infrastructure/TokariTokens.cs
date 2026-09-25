using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace AdaPlatform.Api.Tests.Infrastructure;

/// <summary>
/// Mints access tokens the way Tokari does (Tokari.API/Services/TokenService.cs): the
/// JwtSecurityToken constructor, ClaimTypes URIs, one aud per application name, roles as
/// "App:Role", and app_access as one JSON claim keyed by client id.
/// </summary>
public static class TokariTokens
{
    public const string Issuer = "Tokari";
    public const string Audience = "AdaPlatform";
    public const string SigningKey = "test-signing-key-for-tokari-tokens-only-0123456789";

    public sealed record App(string Name, string ClientId, string[] Roles, string[] Permissions);

    public static App AdaPlatform(params string[] permissions) =>
        new(Audience, "5d1f0c2e-ada0-4c1a-9d3e-0123456789ab", ["Viewer"], permissions);

    public static string Create(
        IEnumerable<App> apps,
        string key = SigningKey,
        string issuer = Issuer,
        TimeSpan? lifetime = null,
        string algorithm = SecurityAlgorithms.HmacSha256)
    {
        var access = apps.ToList();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "42"),
            new(ClaimTypes.Name, "dispecer"),
            new(ClaimTypes.Email, "dispecer@example.com"),
            new(JwtRegisteredClaimNames.Sub, "42"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iss, issuer),
        };
        claims.AddRange(access.Select(app => new Claim(JwtRegisteredClaimNames.Aud, app.Name)));

        var appAccess = access.ToDictionary(
            app => app.ClientId,
            app => new { app_name = app.Name, roles = app.Roles, permissions = app.Permissions });
        claims.Add(new Claim("app_access", JsonSerializer.Serialize(appAccess), JsonClaimValueTypes.Json));
        claims.AddRange(access.SelectMany(app => app.Roles.Select(role => new Claim(ClaimTypes.Role, $"{app.Name}:{role}"))));

        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), algorithm);
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: null,
            claims: claims,
            expires: DateTime.UtcNow + (lifetime ?? TimeSpan.FromMinutes(5)),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>A token for a user with the given AdaPlatform permissions (and a role in another app).</summary>
    public static string For(params string[] permissions) =>
        Create([AdaPlatform(permissions), new App("Transportella", "transportella", ["Dispatcher"], ["dispatcher"])]);
}
