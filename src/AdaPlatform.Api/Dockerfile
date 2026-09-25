# Build context is the repo root (the API depends on sibling projects and the
# central Directory.*.props files).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/AdaPlatform.Api/AdaPlatform.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "AdaPlatform.Api.dll"]
