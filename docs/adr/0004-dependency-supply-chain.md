# ADR 0004: Dependencies are pinned, audited and updated through CI

## Status
Decided.

## Context
The platform runs on-prem inside operators' networks (ADR 0002) and processes customer
operations data. Most of its code is third-party: NuGet and npm packages, CI actions,
base images. Each is a way in, through a known vulnerability or through a compromised
release (hijacked npm maintainer accounts, moved Git tags, install scripts that run on
the developer's machine). The project has one maintainer, so the checks must run on
their own, not rely on someone remembering them.

## Decision
- **Everything is pinned to an exact, verified artifact.**
  - NuGet: central versions (`Directory.Packages.props`), `packages.lock.json` with content
    hashes, locked-mode restore in CI and in the Docker build.
  - npm: exact versions in `package.json` (`save-exact`), `npm ci` from `package-lock.json`.
  - CI actions: pinned to a commit SHA. Scanner images: pinned to a digest.
  - The SDK is pinned in `global.json`, and Node in `.nvmrc` and `engines`.
- **One package source.** `nuget.config` clears machine-wide sources and maps every package
  to nuget.org, which prevents dependency confusion.
- **No install-time code.** npm `ignore-scripts=true`; the stack needs no install scripts.
- **Vulnerabilities fail the build.** NuGet audit covers transitive packages at every
  severity, and `TreatWarningsAsErrors` makes a finding a build error. CI also runs
  `npm audit`, `npm audit signatures` (registry signatures and provenance), Trivy on the
  container image and Gitleaks on the whole history. It runs weekly as well, so a CVE
  published against an unchanged dependency is still caught.
- **Updates arrive as pull requests.** Dependabot opens grouped PRs (EF Core with its
  providers, React with its types, ...) that go through the same CI. npm updates wait a
  3-day cooldown, because most malicious versions are pulled within hours.
- **Least privilege at runtime and in CI.** The container runs as a non-root user.
  Workflows have `contents: read` only and do not persist the checkout token.

## Consequences
- Builds are reproducible: the same commit restores the same bytes.
- Adding or updating a package is a deliberate, reviewed change to a lock file.
- Some Dependabot PR noise, reduced by grouping and weekly batching.
- A newly published CVE can turn a previously green `main` red with no code change.
  That is intended: the build reports the state of the dependencies, not just the code.
