FROM node:22-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY . .
# Baked into the JS bundle at build time — Vite has no runtime env mechanism, unlike the
# nginx layer below. Empty defaults mean same-origin relative paths (docker-compose/local);
# the CI build passes real values only for the Render image (see .github/workflows/deploy.yml
# and web/src/api/client.ts for why: nginx proxying to the API cross-service on Render hits a
# TLS handshake failure, so that build calls the API's public URL directly instead).
ARG VITE_API_ORIGIN=""
ARG VITE_API_KEY=""
ENV VITE_API_ORIGIN=$VITE_API_ORIGIN
ENV VITE_API_KEY=$VITE_API_KEY
RUN npm run build

FROM nginx:alpine AS runtime
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf.template /etc/nginx/templates/default.conf.template
# PORT/API_ORIGIN/DASHBOARD_API_KEY defaults match docker-compose. On Render, PORT is
# injected automatically and API_ORIGIN must still be set to the API's public URL (even
# though nginx's /api and /webhooks proxy locations go unused there now — the browser calls
# the API directly, see above) because nginx resolves API_ORIGIN's hostname at startup and
# refuses to boot ("host not found in upstream") if it can't — a placeholder that doesn't
# resolve on Render's network, like the docker-compose default below, would crash it.
ENV PORT=80
ENV API_ORIGIN=http://api:8080
ENV DASHBOARD_API_KEY=dev-local-dashboard-key
EXPOSE 80
