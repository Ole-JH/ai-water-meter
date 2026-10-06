# Troubleshooting

??? question "The browser shows *“the game client hasn't been built yet”*"
    `server/public` only contains the placeholder page. Run **Shadowfall → Build WebGL** in Unity, or `task client:build`, then refresh. With Docker Compose the folder is mounted, so you don't need to restart.

??? question "*“Your game client doesn't match this server's world”*"
    The world generation changed since the server stored its map, or your browser cached an old build. Hard-refresh the page (++ctrl+shift+r++). If you changed the world on purpose, run `task world:reset` and restart the server.

??? question "*“Could not connect: could not reach the game server”*"
    - In the editor, check the **Server** field on the login screen (default `ws://localhost:7341/ws`).
    - Behind a reverse proxy, make sure `/ws` forwards the `Upgrade` and `Connection` headers. See [HTTPS & reverse proxy](deployment/reverse-proxy.md).
    - Pages served over HTTPS need the proxy to terminate TLS, because the client uses `wss://` on HTTPS pages.

??? question "The page loads forever or fails with a decompression error"
    The build must be served with `Content-Encoding: gzip`. `server.js` does this. If another web server or CDN sits in front, make sure it doesn't strip or double-apply the encoding for `/Build/*.gz`.

??? question "Monsters walk through walls, or get stuck"
    The server paths monsters on the uploaded grid. If the client's world changed but `world.json` wasn't reset, the two disagree. Run `task world:reset`.

??? question "Pink / magenta objects"
    You switched render pipelines after materials were created. Restart Play mode. Materials are always cloned from the active pipeline's default material.

??? question "Glow or fog missing in the WebGL build only"
    Run **Shadowfall → Open Main Scene** once (the build menu does this too). It creates `Assets/Resources/ShadowfallVariants.mat` and enables fog in the scene, so those shader variants are kept in the build.

??? question "I forgot my password"
    See [Operations → Resetting a character's password](deployment/operations.md#resetting-a-characters-password).
