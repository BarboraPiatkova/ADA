# AdaPlatform

Backend for the web platform replacing ADA's manual desktop workflow — automated
ingestion of transit operations data (old Transportella exports, ADA-style APC
data), unified KPI computation, multi-tenant per-operator isolation.

This is an early skeleton: solution + API project + Postgres wiring + one real
health check. No domain entities yet — that's the next step (see below).

## Stack
- **.NET 10**, ASP.NET Core Web API (minimal APIs)
- **PostgreSQL** via EF Core / Npgsql — see [`docs/adr/0001-postgres-over-sql-server.md`](docs/adr/0001-postgres-over-sql-server.md) for why, over SQL Server
- **xUnit** + **Testcontainers.PostgreSql** for integration tests against a real, disposable database

## Run it

```bash
docker compose up
```

Starts Postgres + the API on `http://localhost:8080`. Check `http://localhost:8080/health`
— it verifies the API can actually reach Postgres, not just that the process is up.

### Local dev without Docker

```bash
dotnet run --project src/AdaPlatform.Api
```

Needs a local Postgres reachable at the connection string in
`appsettings.Development.json` (a throwaway local default — never a real credential).

## Test

```bash
dotnet test
```

Requires Docker running — the test suite starts its own disposable Postgres
container per run via Testcontainers, rather than mocking the database or using
EF Core's InMemory provider. The point of the current test is to prove real
connectivity works, which a mock can't demonstrate.

## Repo layout

```
src/AdaPlatform.Api/     the API project
tests/AdaPlatform.Api.Tests/
docs/adr/                architecture decision records
docker-compose.yml
```

## Next steps

1. First EF Core migration: `Tenants` table + `TenantId` on the domain entities
   ported from ADA's shape (`Line`/`Trace`/`Ride`/`VehicleStopRecord`/`Vehicle`/`ApcError`),
   with EF Core global query filters + Postgres RLS as a backstop.
2. One-off import: ADA's existing SQLite export → this schema, as tenant #1.
3. XLSX ingestion for the fields already confirmed against a real Transportella
   export (stop, scheduled/actual times, dwell, delay) — occupancy/km/kWh ingestion
   stays behind an interface until it's confirmed which system actually supplies them.
4. Hangfire (Postgres-backed) for the per-tenant scheduled import job.
