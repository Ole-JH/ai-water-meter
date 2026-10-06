# Shadowfall game server + WebGL host
FROM node:20-alpine

WORKDIR /app
ENV NODE_ENV=production \
    DROP_PRIVILEGES=1 \
    PORT=8080 \
    DATA_DIR=/data \
    PUBLIC_DIR=/app/public

COPY package.json package-lock.json ./
RUN npm ci --omit=dev && npm cache clean --force

COPY server.js content.js ./
# The Unity WebGL build (Shadowfall > Build WebGL in the editor writes it to server/public).
# docker-compose also mounts this folder, so you can rebuild the client without rebuilding the image.
COPY public/ ./public/

# Starts as root only to chown the /data volume, then server.js switches to the "node" user (uid 1000).
RUN mkdir -p /data && chown -R node:node /data
VOLUME ["/data"]
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=3s CMD wget -qO- http://localhost:8080/healthz || exit 1
CMD ["node", "server.js"]
