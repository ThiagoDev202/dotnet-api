# ── Stage 1: Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia arquivos de projeto antes do código para aproveitar cache de layers
COPY Directory.Build.props .
COPY OrderService.sln .
COPY src/OrderService.Api/OrderService.Api.csproj             src/OrderService.Api/
COPY src/OrderService.Application/OrderService.Application.csproj  src/OrderService.Application/
COPY src/OrderService.Domain/OrderService.Domain.csproj            src/OrderService.Domain/
COPY src/OrderService.Infrastructure/OrderService.Infrastructure.csproj  src/OrderService.Infrastructure/

RUN dotnet restore src/OrderService.Api/OrderService.Api.csproj

# Copia fontes e publica
COPY src/ src/
RUN dotnet publish src/OrderService.Api/OrderService.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Stage 2: Runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Usuário não-root para reduzir superfície de ataque
RUN addgroup --system --gid 1001 dotnet \
    && adduser --system --uid 1001 --ingroup dotnet dotnet

COPY --from=build --chown=dotnet:dotnet /app/publish .

USER dotnet
EXPOSE 8080

ENTRYPOINT ["dotnet", "OrderService.Api.dll"]
