# ABYSS web

browser + webxr version of the ABYSS world. same scanned corals, rocks, wreck and generated sounds as the quest build, running on three.js (r170, vendored so there's no cdn dependency).

- **explore** — drag to look, WASD to swim, click anything that glows
- **guided** — the same three sample activities as the quest app, with a guide arrow
- **tour** — cinematic fly-through with captions. `?tour` starts it straight away, `?tour&clean` hides the UI for screen recording
- **VR** — Enter VR shows up on headsets with webxr (quest browser). point + trigger to select or teleport, right stick to snap turn, grab the compass / conch to inspect them

no build step. serve the folder:

```
python3 -m http.server 8765
```

## deploy on vercel

import the repo, set **Root Directory** to `web`, framework preset **Other**, no build command. done.

## files

- `js/world.js` scene, layout, shaders (caustics, sand, sway, glow), fish, god rays, particles
- `js/main.js` controls, selection, inspect, activities, tour, vr
- `js/content.js` all text + activity data. swap this for real client content
- `assets/models` glb exports, made by `Tools/blender/export_web.py` from the same art the unity project uses

`?at=fwd,right,height,lookFwd,lookRight` drops the camera anywhere, handy for screenshots. `?capture` makes the page step frame by frame for rendering video.

licences: corals are smithsonian open access (CC0), rocks/props/ship/sand from poly haven (CC0). full list in `../ASSET_REGISTER.md`.
