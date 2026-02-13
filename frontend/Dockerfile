# syntax=docker/dockerfile:1

# Build
FROM node:20-alpine AS build
WORKDIR /app

COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci --no-audit --no-fund

COPY frontend/ ./
RUN npm run build

# Runtime
FROM nginx:1.27-alpine

# Nginx config to serve SPA + proxy /api to backend
COPY frontend/nginx/default.conf /etc/nginx/conf.d/default.conf

COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80
