# HTTPS & reverse proxy

Browsers only allow secure WebSockets (`wss://`) from pages loaded over HTTPS. The client picks `ws://` or `wss://` to match the page automatically, so you only need a reverse proxy that terminates TLS and forwards WebSocket upgrades.

=== "Caddy"

    Caddy obtains certificates automatically and proxies WebSockets with no extra configuration.

    ```caddyfile
    play.example.com {
        encode zstd gzip
        reverse_proxy shadowfall:7341
    }
    ```

    Example `docker-compose.yml` addition:

    ```yaml
      caddy:
        image: caddy:2
        restart: unless-stopped
        ports: ["80:80", "443:443"]
        volumes:
          - ./Caddyfile:/etc/caddy/Caddyfile:ro
          - caddy_data:/data
    volumes:
      caddy_data:
    ```

    Then remove the `ports:` mapping from the `shadowfall` service so only Caddy is exposed.

=== "nginx"

    ```nginx
    server {
        listen 443 ssl http2;
        server_name play.example.com;
        ssl_certificate     /etc/letsencrypt/live/play.example.com/fullchain.pem;
        ssl_certificate_key /etc/letsencrypt/live/play.example.com/privkey.pem;

        location /ws {
            proxy_pass http://127.0.0.1:7341;
            proxy_http_version 1.1;
            proxy_set_header Upgrade $http_upgrade;
            proxy_set_header Connection "upgrade";
            proxy_set_header Host $host;
            proxy_read_timeout 3600s;
        }

        location / {
            proxy_pass http://127.0.0.1:7341;
            proxy_set_header Host $host;
        }
    }
    ```

=== "Traefik"

    ```yaml
      shadowfall:
        labels:
          - traefik.enable=true
          - traefik.http.routers.shadowfall.rule=Host(`play.example.com`)
          - traefik.http.routers.shadowfall.entrypoints=websecure
          - traefik.http.routers.shadowfall.tls.certresolver=letsencrypt
          - traefik.http.services.shadowfall.loadbalancer.server.port=7341
    ```

!!! warning "Don't double-compress"
    The WebGL files are already gzip-compressed, and the server sends them with `Content-Encoding: gzip`. If your proxy recompresses responses, exclude `/Build/*`.
