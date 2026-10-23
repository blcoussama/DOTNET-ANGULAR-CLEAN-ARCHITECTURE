# syntax=docker/dockerfile:1
#
# Image de l'API CreditFlow (ASP.NET Core .NET 10).
# Construire depuis la racine du dépôt :  docker build -t creditflow-api ./backend
# Seuls les projets de src/ entrent dans l'image (les tests tournent en CI, hors image).

############################
# 1) Build / publish (SDK)
############################
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

ENV DOTNET_NOLOGO=true \
    DOTNET_CLI_TELEMETRY_OPTOUT=true

# Restauration NuGet dans une couche séparée : elle n'est rejouée que si un fichier
# de projet change, pas à chaque modification du code.
# (global.json : version du SDK ; Directory.*.props : réglages communs et versions centralisées.)
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Directory.Build.props                                      src/
COPY src/CreditFlow.Domain/CreditFlow.Domain.csproj                 src/CreditFlow.Domain/
COPY src/CreditFlow.Application/CreditFlow.Application.csproj       src/CreditFlow.Application/
COPY src/CreditFlow.Infrastructure/CreditFlow.Infrastructure.csproj src/CreditFlow.Infrastructure/
COPY src/CreditFlow.Api/CreditFlow.Api.csproj                       src/CreditFlow.Api/
RUN dotnet restore src/CreditFlow.Api/CreditFlow.Api.csproj

# .editorconfig : mêmes règles d'analyse qu'en CI (EnforceCodeStyleInBuild + TreatWarningsAsErrors).
COPY .editorconfig ./
COPY src/ src/
RUN dotnet publish src/CreditFlow.Api/CreditFlow.Api.csproj \
      --configuration Release \
      --no-restore \
      --output /app/publish \
      -p:UseAppHost=false

############################
# 2) Exécution (runtime ASP.NET Core, utilisateur non root)
############################
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Kestrel en HTTP sur 8080 (TLS terminé en amont : nginx / reverse proxy).
# Pièces justificatives hors de la racine de l'application, dans un volume.
# Clés Data Protection d'ASP.NET Core persistées dans un second dossier (volume) pour survivre à la recréation du conteneur.
ENV ASPNETCORE_HTTP_PORTS=8080 \
    Storage__RootPath=/var/lib/creditflow/documents \
    DataProtection__KeysPath=/var/lib/creditflow/keys

# Dossiers inscriptibles par l'utilisateur applicatif ($APP_UID fourni par l'image
# Microsoft). Un volume nommé monté ici hérite de ces droits à sa création.
RUN mkdir -p /var/lib/creditflow/documents /var/lib/creditflow/keys \
 && chown "$APP_UID:$APP_UID" /var/lib/creditflow/documents /var/lib/creditflow/keys \
 && chmod 0750 /var/lib/creditflow/documents /var/lib/creditflow/keys

# Binaires possédés par root : l'utilisateur applicatif peut les lire, pas les modifier.
COPY --from=build --chown=root:root /app/publish/ ./

# $APP_UID = UID numérique (1654) défini par l'image de base : hadolint ne peut pas le résoudre.
# hadolint ignore=DL3066
USER $APP_UID
EXPOSE 8080

# Sonde sans outil supplémentaire (pas de curl/wget dans l'image) : requête HTTP via /dev/tcp de bash.
HEALTHCHECK --interval=15s --timeout=5s --start-period=60s --retries=5 \
  CMD ["bash", "-c", "exec 3<>/dev/tcp/127.0.0.1/8080 && printf 'GET /api/v1/health HTTP/1.1\\r\\nHost: localhost\\r\\nConnection: close\\r\\n\\r\\n' >&3 && head -n 1 <&3 | grep -q ' 200 '"]

ENTRYPOINT ["dotnet", "CreditFlow.Api.dll"]
