# Troubleshooting

??? question "The browser shows *“the game client hasn't been built yet”*"
    `server/public` only contains the placeholder page. Build the client with `task client:build` (Docker; see [Building the client in Docker](deployment/docker-client-build.md)) or **Shadowfall → Build WebGL** in Unity, then refresh. With Docker Compose the folder is mounted, so you don't need to restart.

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

??? question "Models are all white (no textures)"
    The headless build (`-nographics`) has no GPU, and glTFast keeps textures only on the GPU by default, so the texture pixels were never saved. The build now marks the model textures readable before building, and the affected models are re-imported automatically. Rebuild with `task client:build`. If they are still white, clear the import cache with `task client:clean-cache` and build again.

??? question "Glow or fog missing in the WebGL build only"
    Run **Shadowfall → Open Main Scene** once (the build menu does this too). It creates `Assets/Resources/ShadowfallVariants.mat` and enables fog in the scene, so those shader variants are kept in the build.

??? question "I forgot my password"
    See [Operations → Resetting a character's password](deployment/operations.md#resetting-a-characters-password).

??? question "Docker client build: *“No Unity license found”* or activation errors"
    See [Building the client in Docker → License](deployment/docker-client-build.md#1-provide-a-unity-license). The easiest fix is `task license:activate`. If Unity rejects an existing license file, get a fresh one the same way.

??? question "Build fails with *“Machine bindings don't match”* / *“'com.unity.editor.headless' was not found”*"
    The license was activated for a different machine id than the build container's. This happens with licenses from older versions of the license helper, or `.ulf` files copied from a normal Unity Hub install. Pull the latest code, run `task license:activate` again, copy the **whole** `unity-license/` folder (including `licenses/`) to the build machine, and rebuild.

??? question "`task license:activate`: the sign-in doesn't come back to Unity Hub"
    After you sign in, Firefox (inside the desktop) has to open a `unityhub://` link. If it shows a dialog, choose **Open link** / **Unity Hub**. If nothing happens, close Firefox and click **Sign in** in the Hub again. Unity Hub's log is at `/tmp/unityhub.log` in the container (`docker exec -it <container> cat /tmp/unityhub.log`).
