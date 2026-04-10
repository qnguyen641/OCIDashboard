# ── Build stage ──
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first (cached layer)
COPY OracleWebApplication/OracleWebApplication.csproj OracleWebApplication/
RUN dotnet restore OracleWebApplication/OracleWebApplication.csproj

# Copy everything and publish
COPY . .
WORKDIR /src/OracleWebApplication
RUN dotnet publish -c Release -o /app/publish --no-restore

# ── Runtime stage ──
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Render injects PORT env var
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "OracleWebApplication.dll"]
