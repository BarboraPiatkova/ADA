using AdaPlatform.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Npgsql;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

namespace AdaPlatform.Api.Tests.Infrastructure;

/// <summary>
/// One real database server (container) per engine, shared by a test class. Each test
/// asks for its own fresh database on it, so tests never see each other's data.
/// </summary>
public abstract class DatabaseFixture : IAsyncLifetime
{
    public abstract DatabaseProvider Provider { get; }

    /// <summary>Connection string to a brand-new, not-yet-created database.</summary>
    public string NewDatabaseConnectionString()
    {
        var name = "t_" + Guid.NewGuid().ToString("N")[..12];
        return WithDatabase(ServerConnectionString, name);
    }

    protected abstract string ServerConnectionString { get; }
    protected abstract string WithDatabase(string connectionString, string database);

    public abstract Task InitializeAsync();
    public abstract Task DisposeAsync();
}

public sealed class PostgresFixture : DatabaseFixture
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public override DatabaseProvider Provider => DatabaseProvider.Postgres;
    protected override string ServerConnectionString => _container.GetConnectionString();

    protected override string WithDatabase(string connectionString, string database) =>
        new NpgsqlConnectionStringBuilder(connectionString) { Database = database }.ToString();

    public override Task InitializeAsync() => _container.StartAsync();
    public override Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

public sealed class SqlServerFixture : DatabaseFixture
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public override DatabaseProvider Provider => DatabaseProvider.SqlServer;
    protected override string ServerConnectionString => _container.GetConnectionString();

    protected override string WithDatabase(string connectionString, string database) =>
        new SqlConnectionStringBuilder(connectionString) { InitialCatalog = database }.ToString();

    public override Task InitializeAsync() => _container.StartAsync();
    public override Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
