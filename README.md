# AdaPlatform

Web platform replacing ADA's manual desktop workflow: automated ingestion of transit
operations data (ADA-style APC data, old Transportella exports), a unified data model,
KPI computation, gap-filling of missing measurements, and a React web frontend.

One deployment serves one operator, inside the operator's own network
([ADR 0002](docs/adr/0002-single-tenant-per-deployment.md)). The database engine is
switchable between **PostgreSQL** and **SQL Server**
([ADR 0003](docs/adr/0003-switchable-database-provider.md)).

## Stack
- **.NET 10**, ASP.NET Core minimal APIs, EF Core 10
- **PostgreSQL or SQL Server**, chosen by config (`Database:Provider`)
- **React + TypeScript + Vite** frontend, Leaflet for the map
- **xUnit + Testcontainers**: the same test suite runs against both engines

## Run it

```bash
docker compose up -d                                  # API + PostgreSQL
docker compose -f docker-compose.sqlserver.yml up -d  # API + SQL Server (same image)

# With a local Tokari for sign-in (built from ../Tokari), then register AdaPlatform in it once:
docker compose -f docker-compose.yml -f docker-compose.tokari.yml up -d --build
./tools/tokari/seed-dev.ps1                           # user "dispecer", password "Dispecer-dev-1"
```

The API listens on `http://localhost:8080`. `/health` checks that it can actually
reach the database. Migrations are applied on startup.

### Load data

```bash
# Raw UCP-01/UCP-02 logs (APC_<vehicle>.<yyyy-MM-dd>.csv), from a folder or a .zip. Safe to re-run.
dotnet run -c Release --project tools/AdaPlatform.Cli -- import-ucp --source T:\Projects\DPMB\ADA\ADA_20220808\APC_Logs.zip

# Legacy ADA database — one-off seed into an empty database (reference + derived data only).
dotnet run --project tools/AdaPlatform.Cli -- import-ada --source C:\Projects\ADA\ADA.dbFile

# Thesis data report: every figure with an ID and its provenance, plus CSVs for charts.
dotnet run -c Release --project tools/AdaPlatform.Cli -- profile --out docs/thesis/data
```

The target comes from `tools/AdaPlatform.Cli/appsettings.json`, or from
`Database__Provider` / `ConnectionStrings__Default` in the environment.

## Data model

Four layers, named with standard transit terms (GTFS / Transmodel):

| Layer | Tables | Rule |
| --- | --- | --- |
| Raw | `SourceFiles`, `DeviceEvents` | every log line as reported; never modified; files identified by SHA-256 |
| Fleet | `Vehicles`, `CountingDevices` | one counting device per door — the unit fault detection scores |
| Derived | `Trips`, `StopVisits`, `DoorCounts` | reconstructed from raw events (or imported from ADA); every count says Measured / Manual / Imputed |
| Quality | `DeviceFaults` | known device problems (legacy ADA, later the detector) |

Reference data (`Stops`, `Lines`, `Patterns`, `PatternStops`, `Blocks`) keeps the
operator's natural codes. ADA → new names: Station → Stop, Trace → Pattern,
Service → Block, Ride → Trip, VehicleStopRecord → StopVisit, ApcError → DeviceFault.

### Map key (Mapy.com)

Base maps come from Mapy.com through the API's caching tile proxy, so the key never
reaches the browser and each tile is fetched once a day at most. Without a key the map
falls back to OpenStreetMap.

```bash
# dotnet run: stored in the user-secrets store, outside the repo
dotnet user-secrets set "Map:MapyComApiKey" "<key>" --project src/AdaPlatform.Api
# docker compose: a gitignored .env file next to docker-compose.yml
echo MAPY_API_KEY=<key> >> .env
```

### Sign-in (Tokari)

Users sign in with their Tokari account, as in Herman's other applications. The API
checks Tokari's tokens with Tokari's signing key and proxies login, so the refresh token
stays in an HttpOnly cookie (see [ADR 0005](docs/adr/0005-sign-in-through-tokari.md)).
Access comes from a role in Tokari's **AdaPlatform** application that grants
`network:read` (network map) and/or `quality:read` (device health).

```bash
# .env (gitignored) for docker compose: Tokari's JwtSettings:SigningKey, and the key that
# lifts Tokari's per-IP refresh limit. docker-compose.tokari.yml uses the same values.
TOKARI_SIGNING_KEY=<at least 32 characters>
TOKARI_API_KEY=<random>
TOKARI_DB_PASSWORD=<random, for the local Tokari's SQL Server>

# dotnet run: the same key in user-secrets; Tokari:BaseUrl defaults to localhost:8091
dotnet user-secrets set "Tokari:SigningKey" "<key>" --project src/AdaPlatform.Api
```

### Frontend

```bash
cd frontend
npm install
npm run dev       # http://localhost:5173, proxies /api to localhost:8080
```

## Switching the database

Set both of these together (appsettings or environment):

| Setting | PostgreSQL | SQL Server |
| --- | --- | --- |
| `Database:Provider` | `Postgres` | `SqlServer` |
| `ConnectionStrings:Default` | `Host=…;Database=adaplatform;Username=…;Password=…` | `Server=…;Database=adaplatform;User Id=…;Password=…;TrustServerCertificate=True` |

### Changing the model

Every model change needs a migration **for each engine**:

```bash
dotnet tool restore
dotnet ef migrations add <Name> --project src/AdaPlatform.Migrations.Postgres  --startup-project src/AdaPlatform.Migrations.Postgres  --output-dir Migrations
dotnet ef migrations add <Name> --project src/AdaPlatform.Migrations.SqlServer --startup-project src/AdaPlatform.Migrations.SqlServer --output-dir Migrations
```

If one is forgotten, the `Migrations_are_up_to_date_with_the_model` test fails for that engine.

## Test

```bash
dotnet test
```

Docker must be running. Each engine gets its own disposable container, and each test
its own fresh database on it. Nothing is mocked and nothing uses EF Core InMemory.

## Repo layout

```
src/AdaPlatform.Domain/              entities (network, fleet, measurements) — no dependencies
src/AdaPlatform.Infrastructure/      AppDbContext, entity configurations, provider switch, importers
src/AdaPlatform.Migrations.Postgres/ migrations for PostgreSQL
src/AdaPlatform.Migrations.SqlServer/ migrations for SQL Server
src/AdaPlatform.Api/                 the API
tools/AdaPlatform.Cli/                CLI: import legacy ADA data and raw UCP logs
tests/AdaPlatform.Api.Tests/         contract tests, run once per engine
frontend/                            React SPA
docs/adr/                            architecture decision records
```

## Next steps

1. **Trip reconstruction** from raw events: trip start (code 7) → stop visits (codes 8/9/15) →
   door counts (code 11 per device), with restarts and missing devices handled explicitly.
2. **Fault detection** over devices: silent devices, heartbeats with `alive=false`, restart
   rates, flagged `chyba` stops, vehicle-day in/out balance, negative occupancy drift.
3. **Gap-filling comparison**: mask measured stop visits and compare methods (ADA's
   time-of-day average as baseline, interpolation, historical profile, nearest neighbour,
   one ML model) by MAE/RMSE.
4. **Transportella XLSX parser** for the confirmed fields; must tolerate cells without the
   `r` attribute.
5. **Frontend:** patterns on the map, charts (ECharts), the stop × trip occupancy matrix.
