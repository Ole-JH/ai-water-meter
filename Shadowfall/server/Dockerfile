# Shadowfall game server + WebGL host
FROM node:20-alpine

WORKDIR /app
ENV NODE_ENV=production \
    DROP_PRIVILEGES=1 \
    PORT=7341 \
    DATA_DIR=/data \
    PUBLIC_DIR=/app/public

COPY package.json package-lock.json ./
RUN npm ci --omit=dev && npm cache clean --force

# Every server module (server.js, content.js, dungeon.js, ...). Tests are excluded by .dockerignore.
COPY *.js ./
# Quest rewards and companion prices, extracted from the C# sources (task gamedata).
COPY gamedata.json ./
# The Unity WebGL build (Shadowfall > Build WebGL in the editor writes it to server/public).
# docker-compose also mounts this folder, so you can rebuild the client without rebuilding the image.
COPY public/ ./public/

# Starts as root only to chown the /data volume, then server.js switches to the "node" user (uid 1000).
RUN mkdir -p /data && chown -R node:node /data
VOLUME ["/data"]
EXPOSE 7341

HEALTHCHECK --interval=30s --timeout=3s CMD wget -qO- http://localhost:7341/healthz || exit 1
CMD ["node", "server.js"]
