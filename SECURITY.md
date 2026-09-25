# Security

## Reporting a vulnerability
Please don't open a public issue. Email **piatkova@herman.cz** with the details and a
way to reproduce; you'll get an answer within a week.

## How the project handles security

**Secrets.** No secret is ever committed. Locally they live in .NET user-secrets
(`dotnet user-secrets`), and for Docker in `.env` (gitignored). In a deployment they are
environment variables. The Mapy.com API key never reaches the browser: tiles are
proxied by the API, which adds the key in a request header (never the query string, so
it stays out of logs). CI scans the whole Git history with Gitleaks.

**Dependencies.** Pinned, locked, audited and updated through reviewed pull requests.
See [ADR 0004](docs/adr/0004-dependency-supply-chain.md).

**HTTP.** Every response has a strict Content-Security-Policy (`script-src 'self'`, no
inline script or style) plus `nosniff`, `frame-ancestors 'none'`, a referrer policy and a
permissions policy. HSTS is on outside development. Errors are returned as RFC 9457
problem details with no stack traces. The tile proxy is rate-limited per client, because
each cache miss spends the operator's map credits.

**Runtime.** The API container runs as a non-root user and can write only its tile cache.

**Data.** The platform processes operators' operations data (vehicle and passenger
counts, no personal data). It runs inside the operator's own network, with one
deployment per operator ([ADR 0002](docs/adr/0002-single-tenant-per-deployment.md)).

**Sign-in.** Users sign in with their Tokari account (Herman's token issuer). Every API
endpoint needs a signed-in user unless it is explicitly public (`/health`, `/api/auth/*`,
map tiles). Each screen needs its permission from a Tokari role. The access token lives
only in memory, and the refresh token only in an HttpOnly, SameSite=Strict cookie. See
[ADR 0005](docs/adr/0005-sign-in-through-tokari.md).

## Not yet in place
- **Forwarded headers.** Behind a reverse proxy, the per-client rate limits need the
  forwarded-headers middleware configured for that proxy.
- **Asymmetric token signing.** Tokari signs with a key shared by every application
  that trusts it (see ADR 0005, Consequences).
