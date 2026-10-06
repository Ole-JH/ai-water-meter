# Unity project

## No scenes, no prefabs

`Assets/Scripts/Core/GameManager.cs` contains a `[RuntimeInitializeOnLoadMethod]` bootstrap that creates the `GameManager` whenever a scene loads. The GameManager then:

1. sets up the camera, sun light, ambient light and fog;
2. generates the world (`WorldGenerator`) from a fixed seed: ground texture, village, trees, rocks, lakes and NPCs;
3. adds the `NetClient` (networking) and `GameUI` (all UI, drawn with IMGUI).

All models are built from Unity primitives, and materials are cloned from the render pipeline's default material. As a result the project works with both the **Built-in Render Pipeline** and **URP**.

The build script (**Shadowfall → Build WebGL**) creates `Assets/Scenes/Main.unity` for you if it doesn't exist. Any empty scene works for Play mode.

## Input handling

`GameInput.cs` supports both of Unity's input backends. It uses whichever one **Project Settings → Player → Active Input Handling** is set to, and you don't need to change anything.

## Render pipeline notes

| | Built-in | URP |
| --- | --- | --- |
| Materials | `Standard` (default material) | `Universal Render Pipeline/Lit` (default material) |
| Emission (spell glow, loot beams) | ✓ | ✓ |
| Fog | ✓ | ✓ |

The build script also creates `Assets/Resources/ShadowfallVariants.mat`, a material with emission turned on. It also enables fog in the scene. Both are there so WebGL builds don't strip the shader variants the game turns on at runtime.

## WebGL specifics

- **Networking:** browsers can't open raw sockets, so the client talks to the server over a WebSocket. In WebGL builds `WebSocketConnection` calls `Plugins/WebGL/ShadowfallWebSocket.jslib`, which wraps the browser `WebSocket`. In the editor and desktop builds it uses `System.Net.WebSockets.ClientWebSocket`.
- **Page template:** `Assets/WebGLTemplates/Shadowfall` makes the canvas fill the browser window and disables the right-click context menu, because right-click casts Fireball.
- **Compression:** builds use **Gzip**. `server.js` sends `*.gz` files with `Content-Encoding: gzip`, so no decompression fallback is needed.

## Compiling without Unity

```bash
task client:check
```

This compiles `Assets/Scripts` against Unity's reference assemblies from NuGet using only the .NET SDK, which is handy in CI. Code inside `#if UNITY_WEBGL` or Input-System-only branches, and everything in `Assets/Editor`, is only compiled by Unity itself.
