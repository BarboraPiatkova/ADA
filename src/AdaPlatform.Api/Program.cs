using AdaPlatform.Api.Endpoints;
using AdaPlatform.Api.Map;
using System.Text.Json.Serialization;
using AdaPlatform.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddMemoryCache();

// Enums as text in JSON ("Fault", not 2) — readable, and stable if the enum is reordered.
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Engine (Postgres or SQL Server) comes from Database:Provider — see ADR 0003.
builder.Services.AddAdaPlatformDatabase(builder.Configuration);

// Base maps: Mapy.com through a caching proxy (key stays server-side), OSM as fallback.
builder.Services.AddMapTiles(builder.Configuration);

var app = builder.Build();

await app.Services.MigrateAdaPlatformDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Liveness/readiness probe — verifies the API is up AND can reach the database.
app.MapHealthChecks("/health");

app.MapNetworkEndpoints();
app.MapMapEndpoints();
app.MapQualityEndpoints();

app.Run();

// Needed so WebApplicationFactory<Program> can find the entry point from the test project.
public partial class Program;
