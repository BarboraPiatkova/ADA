using AdaPlatform.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// Connection string is resolved lazily from IConfiguration per DbContext instance
// (not captured once at startup) so it always reflects the final, fully-merged
// configuration — including overrides applied in integration tests.
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("Default")
        ?? throw new InvalidOperationException(
            "Connection string 'Default' is not configured. Set ConnectionStrings:Default " +
            "(appsettings.Development.json for local dev, or the ConnectionStrings__Default " +
            "environment variable when running via docker-compose).");
    options.UseNpgsql(connectionString);
});

// Tied to the same DbContext/connection above, rather than a separately configured
// connection string — one source of truth for "can we reach Postgres".
builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(name: "postgres");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Liveness/readiness probe — verifies the API is up AND can reach Postgres.
app.MapHealthChecks("/health");

app.Run();

// Needed so WebApplicationFactory<Program> can find the entry point from the test project.
public partial class Program;
