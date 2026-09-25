using System.ComponentModel.DataAnnotations;
using System.Text;

namespace AdaPlatform.Api.Auth;

/// <summary>
/// Bound from the "Tokari" section. Tokari is Herman's in-house token issuer: it signs
/// HS256 JWTs with one key shared by every app that trusts it, and lists the apps a user
/// may use in the token's audience, by application name.
/// </summary>
public sealed class TokariOptions
{
    public const string SectionName = "Tokari";

    /// <summary>Tokari's external (authenticated, rate-limited) endpoint, e.g. http://tokari:8091/.</summary>
    [Required]
    public Uri? BaseUrl { get; set; }

    /// <summary>Tokari's JwtSettings:Issuer.</summary>
    [Required]
    public string Issuer { get; set; } = "Tokari";

    /// <summary>This app's Name as registered in Tokari; it appears in the token's aud.</summary>
    [Required]
    public string Audience { get; set; } = "AdaPlatform";

    /// <summary>
    /// Tokari's JwtSettings:SigningKey. A secret: user-secrets locally, an environment
    /// variable (Tokari__SigningKey) in a deployment, never a config file in the repo.
    /// </summary>
    [Required]
    [MinLength(32, ErrorMessage = "Tokari:SigningKey must be at least 32 characters (HS256 needs a 256-bit key).")]
    public string SigningKey { get; set; } = "";

    /// <summary>
    /// Optional key Tokari accepts in X-Api-Key to lift its per-IP refresh limit. Every
    /// user's refresh comes from this server's IP, so a deployment with more than a few
    /// users needs it.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>How long the refresh cookie lives; match Tokari's RefreshTokenExpirationDays.</summary>
    public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromDays(7);

    public byte[] SigningKeyBytes() => Encoding.UTF8.GetBytes(SigningKey);
}
