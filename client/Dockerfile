# ---------------------------------------------------------------------------
# TAMS SPA — build the static Vite bundle, then serve it from nginx, which
# also terminates TLS, applies security headers, and reverse-proxies /api to
# the API (11 §7/§11). The SPA calls the API at the relative path /api/v1, so
# serving both from one origin means no CORS is needed in production.
# Build context is the client/ directory.
# ---------------------------------------------------------------------------
FROM node:22-alpine AS build
WORKDIR /app

# Install with a clean, reproducible tree from the lockfile.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

FROM nginx:1.27-alpine AS runtime
# Static SPA assets. The nginx site config (TLS, security headers, SPA history
# fallback, /api reverse proxy) is supplied at run time via a bind-mount from
# deploy/nginx/ so cert paths and upstream hosts stay out of the image.
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80 443
