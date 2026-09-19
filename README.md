# GTVK Museum Prototype

A small canvas-based portfolio game: a quiet white museum with temporary doors into project worlds.

Current slice:

- White Museum redesigned as the portfolio itself: full-canvas gallery, proximity-revealed exhibits and transient controls.
- Ashes mini field using assets copied from `ashes-of-a-dying-world`.
- Eris child world with interactive nodes and a mock terminal.
- Touch D-pad and interact button remain available on coarse-pointer/mobile devices.

## Run

Use a local server because the app loads ES modules:

```bash
python -m http.server 8080
```

Open:

```text
http://localhost:8080
```

## Controls

- `WASD` or arrow keys: move
- `E` or `Enter`: interact
- `Escape`: close the Eris terminal
- Mobile: D-pad and `E`

## Structure

```text
gtvk-u01/
├── index.html
├── styles.css
├── script.js                    # bootstrap: DOM + terminal + ghép runtime/renderers
├── src/
│   ├── game-core.js             # pure state, movement, interaction actions
│   ├── runtime/
│   │   ├── assets.js
│   │   ├── game-runtime.js
│   │   ├── input.js
│   │   ├── shared-render.js
│   │   └── view.js
│   └── worlds/
│       ├── ashes-renderer.js
│       ├── eris-renderer.js
│       └── museum-renderer.js
├── tests/
│   ├── game-core.test.js
│   └── runtime-architecture.test.js
└── assets/
    └── ashes/
```

`WORLD_DEFS` now describes interactions with declarative `action` objects. Mutable state for each world lives under `state.worlds[worldId]`, so adding a new world no longer requires a special-case clone or pickup branch in the core.

## Test

```bash
npm.cmd test
```

On PowerShell, `npm test` may be blocked by script execution policy. Use `npm.cmd test`.

## Notes

The Ashes and Eris worlds are web-native mini slices. The Godot projects stay separate; this repo only copies selected demo assets and implements lightweight browser interactions.


## White Museum presentation

The museum intentionally avoids normal portfolio chrome. On desktop, the canvas is the page: exhibit names appear only when Hyou is close enough to inspect them, the control hint disappears after the opening seconds, and inventory remains hidden until something has actually been collected. Ashes/Eris keep a small world readout because they are rooms inside the museum rather than the museum shell itself.
