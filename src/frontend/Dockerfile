# syntax=docker/dockerfile:1
FROM node:22-alpine AS build
WORKDIR /app

# ARG must be redeclared in every stage that uses it (Docker build-arg scoping).
ARG APP_PROJECT

COPY package*.json ./
RUN npm ci

COPY . .
RUN npm run build:${APP_PROJECT}

# nginx serves the already-built static bundle — no Node runtime, no
# production reverse-proxy/gateway behaviour beyond a single SPA vhost.
FROM nginx:1.27-alpine AS final
ARG APP_PROJECT

COPY --from=build /app/dist/${APP_PROJECT}/browser /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
