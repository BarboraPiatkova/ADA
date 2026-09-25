# ADR 0005: Sign-in through Tokari, with the refresh token in an HttpOnly cookie

## Status
Decided.

## Context
Herman's applications (Transportella, PIK, Atlas, the WebShop) sign users in with
**Tokari**, the in-house token issuer. Operators already have their accounts there, so
AdaPlatform should use the same ones rather than add another user store.

Tokari is not an OpenID Connect server. It is a JSON API: `POST /api/auth/login` with a
user name and password returns an access token and a refresh token.
- **Access token:** an HS256 JWT, 5 minutes. Its `aud` holds the name of every
  application the user has a role in. Its `app_access` claim carries the roles and
  permissions per application.
- **Refresh token:** opaque, 7 days, rotated on every `POST /api/auth/refresh`.
- Tokari has no discovery document, JWKS, redirect flow or PKCE, and no cookies.

The other applications all follow one pattern: their own login form, a small login proxy in
their own backend, and token validation with Tokari's shared signing key. Their SPAs keep
both tokens in `localStorage`.

## Decision
Follow the house pattern, with two changes where it is weakest.

1. **Validation.** The API accepts only tokens that pass all of these checks:
   - HS256, signed with Tokari's key;
   - issuer `Tokari`;
   - `AdaPlatform` in `aud`;
   - not expired (30 s clock skew).
   
   `alg: none` and other algorithms are refused. The `app_access` entry named
   AdaPlatform becomes `permission` claims.
2. **Authorization.** It is secure by default: a fallback policy requires a signed-in
   user on every endpoint, and each feature requires its permission (`network:read`,
   `quality:read`). Only `/health`, `/api/auth/*` and the map tiles are public. Tiles
   are public because Leaflet loads them as `<img>`, which can't send a bearer token;
   the tile proxy is rate-limited instead.
3. **Login proxy.** `/api/auth/login`, `/refresh` and `/logout` forward to Tokari.
   Every token Tokari returns is validated before the browser gets it, **on refresh
   too**, so removing a user's role takes effect within one access-token lifetime.
   A Tokari user with no role in AdaPlatform gets 403, and the session just opened
   for them is revoked again.
4. **Tokens in the browser.** This is the change from the house pattern:
   - **Refresh token:** kept in a cookie with `HttpOnly; Secure; SameSite=Strict;
     Path=/api/auth`. JavaScript can't read it, and it is sent only to the auth
     endpoints.
   - **Access token:** kept in memory only, never in Web Storage.
   
   An XSS bug therefore cannot take a session away to use later. The strict CSP
   (ADR 0004 / SECURITY.md) makes such a bug less likely in the first place.
   Cookie-authenticated endpoints also require an `X-Requested-With` header (CSRF).
5. **In the SPA:**
   - The session is checked once on load (a refresh through the cookie).
   - It is refreshed a minute before the token expires.
   - A request that gets 401 refreshes once and is retried.
   - Refreshes are single-flight within a tab, and serialised across tabs with a Web
     Lock, because two tabs refreshing the same rotated token at once would end one of
     them.
   - Tabs share sign-in and sign-out over a `BroadcastChannel`.
   - Signing out clears the query cache.
6. **Rate limits.** Login is limited per client IP, because Tokari doesn't limit it.
   Refresh calls to Tokari carry `X-Api-Key`, because every user's refresh now comes
   from the API's single IP address.

## Consequences
- **Shared key:** it is symmetric, so AdaPlatform holds the key that signs tokens for
  *every* Tokari application. A leak of this server's configuration would let an
  attacker mint any token for any Herman app. This is inherent to Tokari today. The
  fix belongs in Tokari: asymmetric signing (RS256/ES256) with a published JWKS,
  after which clients hold only a public key. The validation here then changes in one
  place (`TokariAuthentication.ValidationParameters`).
- **Proxy dependency:** the API must reach Tokari's external port. If Tokari is down,
  nobody can sign in, but open sessions keep working until their access token expires.
- **Cross-origin:** the cookie is same-origin, so the SPA must be served from the API's
  origin (as planned) or through the same reverse proxy.
- **Local development:** `docker-compose.tokari.yml` runs a Tokari locally, and
  `tools/tokari/seed-dev.ps1` registers the app, its permissions, a Viewer role and a
  dev user.
