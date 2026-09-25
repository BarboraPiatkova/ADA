using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace AdaPlatform.Api.Auth;

/// <summary>Calls Tokari's /api/auth endpoints (the external port) on the browser's behalf.</summary>
public sealed class TokariClient(HttpClient http, IOptions<TokariOptions> options, ILogger<TokariClient> logger)
{
    public sealed record TokenPair(string AccessToken, string RefreshToken);

    public enum Failure
    {
        /// <summary>Tokari said no: wrong credentials, or an expired or revoked refresh token.</summary>
        Rejected,
        /// <summary>Tokari is down, slow, or answered something unexpected.</summary>
        Unavailable,
    }

    public sealed record Result(TokenPair? Tokens, Failure? Error)
    {
        public static Result Ok(TokenPair tokens) => new(tokens, null);
        public static Result Fail(Failure error) => new(null, error);
    }

    public Task<Result> LoginAsync(string userName, string password, CancellationToken ct) =>
        SendAsync("api/auth/login", new { email = userName, password }, bearer: null, ct);

    public Task<Result> RefreshAsync(string refreshToken, CancellationToken ct) =>
        SendAsync("api/auth/refresh", new { refreshToken }, bearer: null, ct);

    /// <summary>Revokes the refresh token. Tokari requires a valid access token for it.</summary>
    public async Task LogoutAsync(TokenPair tokens, CancellationToken ct)
    {
        using var request = Request("api/auth/logout", new { refreshToken = tokens.RefreshToken }, tokens.AccessToken);
        try
        {
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Tokari logout answered {Status}", (int)response.StatusCode);
            }
        }
        catch (HttpRequestException e)
        {
            // The local session ends anyway; the refresh token then expires on its own.
            logger.LogWarning(e, "Tokari logout failed");
        }
    }

    private async Task<Result> SendAsync(string path, object body, string? bearer, CancellationToken ct)
    {
        using var request = Request(path, body, bearer);
        try
        {
            using var response = await http.SendAsync(request, ct);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
            {
                return Result.Fail(Failure.Rejected);
            }
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Tokari {Path} answered {Status}", path, (int)response.StatusCode);
                return Result.Fail(Failure.Unavailable);
            }

            var tokens = await response.Content.ReadFromJsonAsync<TokenPair>(ct);
            return tokens is { AccessToken.Length: > 0, RefreshToken.Length: > 0 }
                ? Result.Ok(tokens)
                : Result.Fail(Failure.Unavailable);
        }
        catch (Exception e) when ((e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
                                  && !ct.IsCancellationRequested)
        {
            logger.LogWarning(e, "Tokari {Path} failed", path);
            return Result.Fail(Failure.Unavailable);
        }
    }

    private HttpRequestMessage Request(string path, object body, string? bearer)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        if (bearer is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }
        // All users' refreshes come from this server's address; the key lifts Tokari's per-IP limit.
        if (!string.IsNullOrEmpty(options.Value.ApiKey))
        {
            request.Headers.Add("X-Api-Key", options.Value.ApiKey);
        }
        return request;
    }
}
