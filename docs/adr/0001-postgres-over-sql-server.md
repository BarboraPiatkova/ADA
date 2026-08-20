# ADR 0001: PostgreSQL over SQL Server for the platform's own datastore

## Status
Decided.

## Context
The backend needs one relational datastore of its own for the multi-tenant domain
data (lines, traces, rides, vehicle stop records, tenants). This is separate from —
and does not replace — the operator's existing systems: old Transportella's SQL
Server database stays read/export-only as a data *source*, and ADA's SQLite file is
a one-off import target for local seed data. This ADR is only about the database
this platform owns and writes to.

## Decision
PostgreSQL.

## Rationale
- **Row-Level Security.** The multi-tenant design (see the planning doc) relies on
  isolation enforced at two layers: EF Core global query filters as the ergonomic
  default, and database-level RLS as a backstop that holds even if the ORM layer is
  ever bypassed (raw SQL, a migration script, a future service querying the same
  database directly). Postgres's native `CREATE POLICY` RLS is more mature and more
  ergonomic to work with than SQL Server's security-predicate-function approach.
- **No licensing story to manage.** Postgres is fully open source. For a project
  that starts as a diploma thesis artifact and may or may not become a licensed
  product, this removes a category of decision entirely.
- **First-class local dev and CI story.** `postgres:17-alpine` in docker-compose,
  and `Testcontainers.PostgreSql` for integration tests that spin up a real,
  disposable database per test run — no separate SQL Server license or heavier
  container image required just to run the test suite.
- **`jsonb` covers the "do we need a document store too" question.** Semi-structured
  staging data (e.g. a raw parsed import before normalization) can live in a
  `jsonb` column instead of justifying a second database technology.

## Consequences
- EF Core uses the `Npgsql.EntityFrameworkCore.PostgreSQL` provider.
- RLS policies must be added as raw SQL in migrations (EF Core has no first-class
  RLS API) — tracked as a follow-up once the first tenant-scoped tables exist.
- Any future integration that reads FROM old Transportella's SQL Server database
  (via its own connection string, read-only) is unaffected by this decision — that
  remains a separate, external SQL Server connection, not this platform's own store.

## Alternatives considered
- **SQL Server** — would have kept the stack aligned with old Transportella and
  ADA, but neither of those factors matter for a datastore this platform owns
  outright, and it would have meant licensing considerations and a less ergonomic
  RLS story for no offsetting benefit.
- **Separate database per tenant** — strongest possible isolation, but
  disproportionate operational overhead (N migration paths, N backup schedules)
  for a customer base of small-to-mid-sized regional transit operators, several of
  which are too small to justify dedicated infrastructure each.
