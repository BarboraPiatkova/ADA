# ADR 0003: Switchable database engine — PostgreSQL or SQL Server

## Status
Decided. Supersedes ADR 0001 (PostgreSQL only).

## Context
With one deployment per operator (ADR 0002), the database runs on the **operator's**
infrastructure, and operators differ:

- Some already run and administer **SQL Server** (old Transportella and the Helios ERP
  use it). They have backups, monitoring and licences in place and will want this
  platform's data there too, not a second database engine to learn and maintain.
- Others have no database administrator. For them the simplest install is a bundled
  **PostgreSQL** container that ships with the platform, with no licence to buy.

ADR 0001 chose PostgreSQL mainly for Row-Level Security as a multi-tenancy backstop.
That reason is gone.

## Decision
- One **provider-neutral EF Core model**. The engine is chosen at deploy time by
  configuration: `Database:Provider` = `Postgres` | `SqlServer`, plus the connection string.
- **A separate migrations assembly per engine**: `AdaPlatform.Migrations.Postgres` and
  `AdaPlatform.Migrations.SqlServer`. This is the approach Microsoft documents for EF Core
  with multiple providers. The generated SQL and column types differ, so one shared
  migration set can't serve both.
- Both migration sets ship in the same build, so an operator switches engines without a
  different binary.
- **The same test suite runs against both engines** in real containers (Testcontainers).
  "Supports both" is a tested property, not a claim.
- PostgreSQL is the default for the bundled Docker install.

## Rationale
1. **Fits the customer's IT instead of fighting it.** Operators with SQL Server reuse
   their existing operations. Operators without one get a licence-free bundled database.
2. **Licence and size limits.** SQL Server Express (free) caps a database at 10 GB. The
   sample ADA file holds one month for 5 vehicles in about 2.4 MB. A 100-vehicle fleet over
   10 years extrapolates to roughly 6 GB before indexes (rough estimate, not measured).
   That is close enough to the cap that Express can't be the assumed default. PostgreSQL
   has no such limit.
3. **EF Core already abstracts the dialect.** LINQ queries are translated per provider,
   so supporting two engines costs a small, contained amount of code, not two data layers.
4. **Verifiable.** Every model change is checked against both engines by the contract
   tests, including a check that neither engine's migrations fall behind the model.

## Constraints this imposes (the price, stated explicitly)
- **Only features both engines share in the core model.** No Postgres-only features
  (jsonb columns, PostGIS geometry, RLS) and no SQL Server-only features (temporal tables)
  in shared code. If one becomes necessary, it goes behind a provider-specific
  implementation of a small interface, and this ADR is revisited.
- **Two migrations per model change.** The `Migrations_are_up_to_date_with_the_model`
  test fails for whichever engine was forgotten.
- **No hand-written SQL in shared code.** It would have to be written and tested twice.
  Heavy KPI aggregations must be LINQ that both providers translate, and they're tested
  on both.
- **Known dialect differences are handled once, centrally:**
  - Date and time: transit times are local wall-clock times. Both engines store them
    without a time zone (`timestamp without time zone` / `datetime2`). This is the only
    provider check in `AppDbContext`.
  - Delays are stored as integer seconds. SQL Server's `time` type can't hold negative
    values, and early departures are negative delays.
  - Foreign keys use `Restrict` except where rows are owned (ride → stop records),
    because SQL Server rejects multiple cascade paths that PostgreSQL would accept.

## Alternatives considered
- **PostgreSQL only** (ADR 0001). Simplest, but it forces a second database engine onto
  operators who already run SQL Server.
- **SQL Server only.** Matches the existing products, but it pushes a licence or the
  Express size cap onto operators who have no SQL Server.
- **A hand-written repository layer per engine** (Dapper or raw SQL). Full control of
  SQL, but two data layers to write, test and keep in sync. EF Core providers already
  do this job.
