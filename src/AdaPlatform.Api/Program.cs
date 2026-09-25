using AdaPlatform.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// Engine (Postgres or SQL Server) comes from Database:Provider — see ADR 0003.
builder.Services.AddAdaPlatformDatabase(builder.Configuration);

var app = builder.Build();

await app.Services.MigrateAdaPlatformDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Liveness/readiness probe — verifies the API is up AND can reach the database.
app.MapHealthChecks("/health");


app.Run();

// Needed so WebApplicationFactory<Program> can find the entry point from the test project.
public partial class Program;
