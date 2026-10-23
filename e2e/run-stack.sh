#!/bin/bash
# Lance la pile CreditFlow complète pour les tests navigateur :
#   API ASP.NET Core (PostgreSQL, Development + données de démo) -> build de production Angular servi par serve-spa.js (proxy /api).
# La base n'est jamais vidée : chaque test crée ses propres comptes. Utilisez une base dédiée (nom contenant « e2e » ou « test »).
#
#   E2E_DB_PROVIDER   PostgreSQL (défaut) | SqlServer
#   E2E_DB            chaîne de connexion (défaut : PostgreSQL local, base creditflow_e2e)
#   E2E_SEED_PASSWORD mot de passe des comptes de démo (défaut : valeur factice propre aux tests)
#
# Exemple : createdb creditflow_e2e  puis  npm run stack && npm test  puis  npm run stop
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/.." && pwd)"
TMP="$HERE/.tmp"
mkdir -p "$TMP"

PROVIDER="${E2E_DB_PROVIDER:-PostgreSQL}"
DB="${E2E_DB:-Host=localhost;Port=5432;Database=creditflow_e2e;Username=postgres;Password=postgres}"
case "$DB" in
  *e2e*|*test*) ;;
  *) echo "Refus : le nom de la base doit contenir « e2e » ou « test »."; exit 1 ;;
esac

# Arrête une pile e2e précédente (processus enregistrés dans .tmp/pids)
if [ -f "$TMP/pids" ]; then xargs -r kill < "$TMP/pids" 2>/dev/null || true; rm -f "$TMP/pids"; sleep 2; fi

for port in 5080 4200; do
  if curl -s -m 2 -o /dev/null "http://localhost:$port/"; then echo "Port $port déjà utilisé : arrêtez le processus qui l'occupe."; exit 1; fi
done

(cd "$ROOT/backend" && dotnet build src/CreditFlow.Api -c Release > "$TMP/backend-build.log" 2>&1) \
  || { echo "Build backend KO"; tail -15 "$TMP/backend-build.log"; exit 1; }

# L'API applique les migrations au démarrage puis sème les données de démo (idempotent)
export ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5080
export Database__Provider="$PROVIDER" ConnectionStrings__Default="$DB" Database__MigrateOnStartup=true
export Jwt__SigningKey="$(python3 -c 'import secrets;print(secrets.token_urlsafe(48))')"
export Storage__RootPath="$TMP/uploads" RateLimiting__AuthPermitLimit=1000
SEED="${E2E_SEED_PASSWORD:-E2e-Demo-2026!}"
export Seed__AdminPassword="$SEED" Seed__AnalystPassword="$SEED" Seed__ClientPassword="$SEED"
# « exec » : le PID enregistré est celui du processus lui-même (et non d'un sous-shell), pour que « npm run stop » l'arrête vraiment
(cd "$ROOT/backend/src/CreditFlow.Api/bin/Release/net10.0" && exec nohup dotnet CreditFlow.Api.dll > "$TMP/backend.log" 2>&1) &
echo $! >> "$TMP/pids"

# Build de production servi tel quel (comme derrière nginx), /api relayé vers l'API
(cd "$ROOT/frontend" && npx ng build --configuration production --output-path "$TMP/dist-ui" > "$TMP/frontend-build.log" 2>&1) \
  || { echo "Build frontend KO"; tail -15 "$TMP/frontend-build.log"; exit 1; }
(exec nohup node "$HERE/serve-spa.js" "$TMP/dist-ui/browser" 4200 http://localhost:5080 > "$TMP/frontend.log" 2>&1) &
echo $! >> "$TMP/pids"

for _ in $(seq 1 90); do
  b=$(curl -s -m 5 -o /dev/null -w "%{http_code}" http://localhost:5080/api/v1/health || true)
  f=$(curl -s -m 5 -o /dev/null -w "%{http_code}" http://localhost:4200/api/v1/products || true)
  if [ "$b" = "200" ] && [ "$f" = "200" ]; then echo "Pile prête : API http://localhost:5080, frontend http://localhost:4200"; exit 0; fi
  sleep 2
done
echo "Délai dépassé (api=$b, frontend=$f)"; tail -15 "$TMP/backend.log"; tail -15 "$TMP/frontend.log"; exit 2
