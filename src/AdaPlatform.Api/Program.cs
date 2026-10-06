using System.Text.Json.Serialization;
using AdaPlatform.Api.Auth;
using AdaPlatform.Api.Endpoints;
using AdaPlatform.Api.Map;
using AdaPlatform.Api.Security;
using AdaPlatform.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// Enums as text in JSON ("Fault", not 2) — readable, and stable if the enum is reordered.
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Engine (Postgres or SQL Server) comes from Database:Provider — see ADR 0003.
builder.Services.AddAdaPlatformDatabase(builder.Configuration);
builder.Services.AddAdaPlatformReporting(builder.Configuration);

// Base maps: Mapy.com through a caching proxy (key stays server-side), OSM as fallback.
builder.Services.AddMapTiles(builder.Configuration);

// Errors as RFC 9457 problem details: a status and a title, never a stack trace.
builder.Services.AddProblemDetails();
builder.Services.AddAdaPlatformRateLimits();

// Sign-in through Tokari (Herman's token issuer) — see ADR 0005.
builder.Services.AddTokariAuthentication(builder.Configuration);
builder.Services.AddTokariLogin();

var app = builder.Build();

await app.Services.MigrateAdaPlatformDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSecurityHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// Liveness/readiness probe — verifies the API is up AND can reach the database.
app.MapHealthChecks("/health").AllowAnonymous();

app.MapAuthEndpoints();
app.MapNetworkEndpoints();
app.MapMapEndpoints();
app.MapQualityEndpoints();
app.MapOperationsEndpoints();
app.MapFleetEndpoints();
app.MapTripEndpoints();
app.MapStopStatisticsEndpoints();

app.Run();

// Needed so WebApplicationFactory<Program> can find the entry point from the test project.
public partial class Program;
