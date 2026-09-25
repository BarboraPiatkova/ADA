# ADR 0002: One deployment per operator (single-tenant)

## Status
Decided. Replaces the shared-schema multi-tenancy design in the planning doc
(`TenantId` column, EF Core global query filters, Postgres RLS, JWT `tenant_id` claim).

## Context
The operators this platform serves (regional public transport companies) run their
systems **inside their own private networks**, and the company's existing products
are already deployed that way — one installation per customer:

- ADA runs on the operator's analyst machine, with a read-only SQL Server link into
  the operator's own Helios/Noris ERP.
- Old Transportella runs as a per-customer client–server installation.

The platform's data sources sit in that same network: APC device log drops, the
operator's timetable packages, the Transportella database and its XLSX exports.

## Decision
One deployment serves exactly one operator. The data model has **no tenant concept**:
no `TenantId` columns, no query filters, no tenant resolution per request. Isolation
between operators comes from the fact that each one has its own installation and database.

The platform stays **multi-user**: many people at one operator share one deployment.

## Rationale
1. **The data stays where it already is.** Ride-level operational data, and personal
   data within it (driver codes in the Transportella exports), never leaves the
   operator's network. This is what operators already expect from the company's
   products, and it avoids a hosting and data-processing arrangement for every customer.
2. **Infrastructure isolation is the strongest isolation.** With a shared multi-tenant
   database, isolation depends on every query being filtered correctly. One forgotten
   filter (raw SQL, a report query, a background job) leaks another operator's data.
   With separate deployments that whole class of bug can't happen.
3. **The data sources are only reachable from inside.** A central hosted instance would
   need a VPN or tunnel into every operator's network to reach file drops and the
   Transportella database. On-prem deployment reaches them directly.
4. **It matches how the company already operates.** Installation, upgrades and support
   follow the same per-customer process as Transportella.
5. **It's simpler, and the effort goes where the thesis contribution is.** Dropping
   tenancy removes a column from every table and index, a filter from every query,
   tenant resolution from the auth flow, and a dimension from every test. That effort
   goes into data quality, gap-filling and KPI work instead.

## Consequences
- **Per-operator differences are configuration, not code.** Each deployment has its own
  appsettings/environment (data source paths, WMS server URL, colour thresholds). This
  replaces ADA's `#if DPO` build flags and old Transportella's per-customer classes: one
  build, configured per site.
- **Upgrades happen per deployment.** Migrations must run unattended and reliably on
  each site (`Database:MigrateOnStartup`, or scripts a DBA applies). Every schema change
  ships as a migration, never as a manual step.
- **The operator's IT runs the database**, which is why the engine is switchable
  (ADR 0003).
- **Auth fits the network.** Users are the operator's employees, so authentication should
  plug into the operator's directory (Active Directory / LDAP, or OIDC where they have
  an identity provider), with local accounts as a fallback.
- **No cross-operator comparison out of the box.** Benchmarking operators against each
  other would need an explicit export of anonymised aggregates. That's future work, and a
  deliberate opt-in rather than a side effect of shared storage.

## Alternatives considered
- **Shared schema with a `TenantId` column** (the previous plan). Efficient for a hosted
  SaaS, but it solves a problem this deployment model doesn't have, and it adds
  query-filter risk to every data access.
- **Schema per tenant / database per tenant on a central server.** Stronger isolation
  than a shared schema, but it still needs central hosting and network access into every
  operator, so it keeps the operational costs of SaaS without the efficiency benefits.
