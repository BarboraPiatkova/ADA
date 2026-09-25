using System.Security.Claims;
using System.Text.Json;

namespace AdaPlatform.Api.Auth;

/// <summary>
/// Reads Tokari's app_access claim: one JSON object for all the user's applications,
/// keyed by client id — <c>{ "&lt;clientId&gt;": { "app_name", "roles": [], "permissions": [] } }</c>.
/// Entries are matched by app_name (the same name Tokari puts in aud), as every other
/// Tokari client does; the client id is a GUID that differs per Tokari installation.
/// </summary>
public static class AppAccess
{
    public const string ClaimType = "app_access";

    public sealed record Grant(IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions)
    {
        public static readonly Grant None = new([], []);
    }

    /// <summary>This application's roles and permissions, lower-cased; <see cref="Grant.None"/> if it has no entry.</summary>
    public static Grant For(ClaimsPrincipal user, string appName)
    {
        var json = user.FindFirst(ClaimType)?.Value;
        if (string.IsNullOrEmpty(json))
        {
            return Grant.None;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Grant.None;
            }

            foreach (var app in document.RootElement.EnumerateObject())
            {
                if (app.Value.ValueKind == JsonValueKind.Object
                    && app.Value.TryGetProperty("app_name", out var name)
                    && string.Equals(name.GetString(), appName, StringComparison.OrdinalIgnoreCase))
                {
                    return new Grant(Strings(app.Value, "roles"), Strings(app.Value, "permissions"));
                }
            }
        }
        catch (JsonException)
        {
            // A malformed claim grants nothing. The signature was valid, so this would be
            // a Tokari bug, not an attack; fail closed either way.
        }

        return Grant.None;
    }

    private static string[] Strings(JsonElement app, string property) =>
        app.TryGetProperty(property, out var values) && values.ValueKind == JsonValueKind.Array
            ? values.EnumerateArray()
                .Where(v => v.ValueKind == JsonValueKind.String)
                .Select(v => v.GetString()!.ToLowerInvariant())
                .Distinct()
                .ToArray()
            : [];
}
