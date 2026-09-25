# Build context is the repo root (the API depends on sibling projects and the
# central Directory.*.props files).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from the project files and lock files only, so this layer is cached
# until a dependency changes. Locked mode: the image gets exactly the reviewed packages.
COPY global.json nuget.config Directory.Build.props Directory.Packages.props ./
COPY src/AdaPlatform.Domain/*.csproj src/AdaPlatform.Domain/packages.lock.json src/AdaPlatform.Domain/
COPY src/AdaPlatform.Infrastructure/*.csproj src/AdaPlatform.Infrastructure/packages.lock.json src/AdaPlatform.Infrastructure/
COPY src/AdaPlatform.Migrations.Postgres/*.csproj src/AdaPlatform.Migrations.Postgres/packages.lock.json src/AdaPlatform.Migrations.Postgres/
COPY src/AdaPlatform.Migrations.SqlServer/*.csproj src/AdaPlatform.Migrations.SqlServer/packages.lock.json src/AdaPlatform.Migrations.SqlServer/
COPY src/AdaPlatform.Api/*.csproj src/AdaPlatform.Api/packages.lock.json src/AdaPlatform.Api/
RUN dotnet restore src/AdaPlatform.Api/AdaPlatform.Api.csproj --locked-mode

COPY src/ src/
RUN dotnet publish src/AdaPlatform.Api/AdaPlatform.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
# The tile cache is the only thing the app writes; everything else stays root-owned.
RUN mkdir tile-cache && chown "$APP_UID" tile-cache
# Run unprivileged (the image's built-in non-root user), never as root.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "AdaPlatform.Api.dll"]
