# Performance

Shadowfall runs in the browser: one thread (WebGL), often on a laptop or phone GPU, and the browser's garbage collector
stops the game while it runs. These are the rules the client follows to stay fast; keep to them when adding things.

## Drawing

- **The ground is the most expensive surface.** It covers most of the screen and is drawn once more for every lantern
  that lights it (the forward "add" pass). `ShadowfallTerrain.shader` only runs its leaf and snow noise while there are
  leaves or snow, and only in the main pass; the lantern passes get the plain layer blend.
- **Grass and decals are lit per vertex by lanterns** (`noforwardadd`): no extra pass per light over thousands of
  blades or splats. Grass has no fallback shader, so it never enters the depth pre-pass that sun shadows use.
- **Shadows reach only as far as the ground in view.** `CameraRig` works out where the top edge of the screen meets the
  ground and adds a margin for tall things just beyond it; the Shadow distance option is a share of that. There are no
  sun shadows underground (`GameSettings.SunShadows`).
- **Small things don't cast shadows.** `Factory.Prim` turns casting off for anything under 1.4 m, and the world
  generator's `Art()` for props under 1.2 m. Buildings, trees, characters and walls still cast.
- **Lights are expensive in forward rendering**: each one is another pass over everything it touches.
    - Lanterns, windows and fires switch off beyond the Lights setting's distance (`NightLight`, `LightCull`).
    - The hero's torch goes out in broad daylight.
    - Spell lights (`SpellFx.Flash`, projectile glows) are capped at 2, 4 or 8 at once by the Lights setting.
- **No full-screen pass unless it's used.** The colour grade (`ColorGrade`) is switched off, not just skipped, when
  grading is off: a camera with `OnRenderImage` copies the whole frame even when the effect does nothing.
- **Extra cameras render on demand.** The HUD portrait (`Avatar`) renders about ten times a second, the party
  portraits on a timer, both without sun shadows.
- **Phones and tablets start on the Low preset.**

## The CPU and garbage

- **Effects are pooled.** `SpellFx.Emit` without a `follow` reuses a finished particle system instead of making a new
  GameObject; gradients and curves are cached. Never keep a one-shot emitter: anything kept must be attached (`follow`).
- **No lambdas, LINQ or string building in anything that runs every frame** (Update, OnGUI). Use plain loops and cache
  strings until their value changes.
- **OnGUI runs for every input event, not just painting.** Labels skip non-paint events (`UISkin.Shadowed`), world
  labels are only projected when painting or clicking, and the HUD isn't run at all for the drag events that holding the
  mouse to walk sends every frame.
- **Font sizes come in steps** (damage numbers in 3s, quest marks in 4s): each new size makes the font draw its letters
  again.
- **Big arrays are worked on incrementally.**
    - The exploration percentage is counted as tiles are revealed.
    - The fog save is cached until the map changes.
    - The snow mask stops refilling once every cell is back at its floor.
    - The minimap reads the fog tiles directly.
- **Far things tick slowly or not at all.** Far NPCs update three times a second, fires out of range stop emitting,
  far dungeon torches stop flickering, and hit flashes switch themselves off when settled.
- **The 10-a-second snapshot is parsed as its own small type** (`SnapMsg`), not as the big `NetMsg` union, which made
  a save, an item, a guild and more for every message.

## Measuring

The longer browser playtest reports the frame rate of each scene (`?sfcheck=1&tour=1`, see [Testing](testing.md)), with
the number of particle systems and lights. The container renders in software, so compare runs with each other, not with
a real machine.
