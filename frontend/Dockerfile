# syntax=docker/dockerfile:1
#
# Image du frontend CreditFlow (SPA Angular servie par nginx non privilégié).
# Construire depuis la racine du dépôt :  docker build -t creditflow-frontend ./frontend
# L'image attend un service « api » joignable sur api:8080 (voir docker-compose.yml).

############################
# 1) Build Angular (Node 24)
############################
FROM node:24-slim AS build
WORKDIR /app

ENV CI=true \
    NG_CLI_ANALYTICS=false \
    npm_config_fund=false \
    npm_config_update_notifier=false

# Dépendances en couche séparée : ne change que si package*.json change.
COPY package.json package-lock.json ./
RUN npm ci --no-audit

COPY . .
# Build de production (configuration par défaut d'angular.json).
# Le nom du projet n'est pas codé en dur : on récupère dist/<projet>/browser.
RUN npm run build \
 && mkdir -p /out \
 && cp -R dist/*/browser/. /out/ \
 && test -f /out/index.html

############################
# 2) Exécution (nginx non root, port 8080)
############################
FROM nginxinc/nginx-unprivileged:1.30-alpine AS runtime

# Configuration du site (SPA + proxy /api/ + en-têtes de sécurité).
COPY --chown=root:root nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build --chown=root:root /out/ /usr/share/nginx/html/

# L'image de base tourne déjà avec l'utilisateur nginx (UID 101) ; on le rend explicite.
USER 101
EXPOSE 8080

HEALTHCHECK --interval=15s --timeout=3s --start-period=5s --retries=3 \
  CMD ["wget", "-q", "-O", "/dev/null", "http://127.0.0.1:8080/healthz"]
