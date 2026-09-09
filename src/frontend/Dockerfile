FROM node:24-alpine AS source
WORKDIR /app
COPY src/frontend/package.json src/frontend/package-lock.json ./
RUN npm ci
COPY src/frontend/ .

FROM source AS test
RUN npm run lint && npm run test && npm run build

FROM source AS build
RUN npm run build

FROM nginx:1.29-alpine AS runtime
COPY src/frontend/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
HEALTHCHECK --interval=10s --timeout=3s --start-period=5s --retries=5 \
    CMD wget -q -O - http://127.0.0.1/ >/dev/null || exit 1

