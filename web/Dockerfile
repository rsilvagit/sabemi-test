FROM node:22-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY . .
RUN npm run build

FROM nginx:alpine AS runtime
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf.template /etc/nginx/templates/default.conf.template
# PORT/API_ORIGIN/WEBHOOK_API_KEY/RESOLVER defaults match docker-compose; Render overrides
# all four at deploy time (PORT is injected automatically, the other three must be set to
# the API service's public URL, its real key, and a public DNS resolver respectively —
# 127.0.0.11 only resolves docker-compose service names, not public hostnames).
ENV PORT=80
ENV API_ORIGIN=http://api:8080
ENV WEBHOOK_API_KEY=dev-local-key
ENV RESOLVER=127.0.0.11
EXPOSE 80
