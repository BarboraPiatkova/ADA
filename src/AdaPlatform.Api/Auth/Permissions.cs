namespace AdaPlatform.Api.Auth;

/// <summary>
/// What a user may do in AdaPlatform. Each is a Tokari permission of the AdaPlatform
/// application, granted through a role there; the names double as policy names here.
/// </summary>
public static class Permissions
{
    /// <summary>Claim type the app_access permissions are copied into.</summary>
    public const string ClaimType = "permission";

    /// <summary>See the network map: stops, lines, patterns.</summary>
    public const string NetworkRead = "network:read";

    /// <summary>See device health and data quality.</summary>
    public const string QualityRead = "quality:read";

    public static readonly IReadOnlyList<string> All = [NetworkRead, QualityRead];
}
