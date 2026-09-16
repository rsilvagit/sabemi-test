FROM node:22-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY . .
RUN npm run build

FROM nginx:alpine AS runtime
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf.template /etc/nginx/templates/default.conf.template
# PORT/API_ORIGIN/WEBHOOK_API_KEY defaults match docker-compose; Render overrides all
# three at deploy time (PORT is injected automatically, API_ORIGIN must be the API
# service's private-network address and WEBHOOK_API_KEY its real key — see README).
ENV PORT=80
ENV API_ORIGIN=http://api:8080
ENV WEBHOOK_API_KEY=dev-local-key
EXPOSE 80
