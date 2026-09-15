FROM node:22-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY . .
RUN npm run build

FROM nginx:alpine AS runtime
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf.template /etc/nginx/templates/default.conf.template
# PORT/API_ORIGIN defaults match docker-compose; Render overrides both at deploy time
# (PORT is injected automatically, API_ORIGIN must be set to the API service's URL).
ENV PORT=80
ENV API_ORIGIN=http://api:8080
EXPOSE 80
