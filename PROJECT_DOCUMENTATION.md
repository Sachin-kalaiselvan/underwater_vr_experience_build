# ABYSS

An underwater VR exhibition for Meta Quest. Visitors drop into a calm sea, learn the controls in under a minute, then either roam freely or follow short guided activities. Built to be handed a client's real training content later without code changes.

Other docs: [TECHNICAL_ARCHITECTURE.md](TECHNICAL_ARCHITECTURE.md) · [BUILD_INSTRUCTIONS.md](BUILD_INSTRUCTIONS.md) · [TESTING_PLAN.md](TESTING_PLAN.md) · [ASSET_REGISTER.md](ASSET_REGISTER.md)

## Where it's at

**Milestone 1 (foundation): code done, not verified on a headset.** Read TESTING_PLAN.md for exactly what has and hasn't been run. Short version: everything compiles against the real Unity 6.3 + package code and the logic is unit tested, but the project has never been opened in the Unity editor and never run on a Quest.

Done in session 1:
- environment check, version selection (see architecture doc)
- Unity project skeleton, package manifest, git
- all core systems: state, settings, progress, XR config, interaction contract, inspection, info panels, feedback (visual + audio + haptic), activities (data driven), comfort/locomotion, navigation, environment blending, fish, UI (6 menus, HUD, onboarding), audio, perf logging, kiosk mode
- editor tooling: one-click project config, content + scene generation, APK build
- Blender pipeline + 28 first-pass models, original audio, caustics texture
- this documentation

Not done yet (honest list):
- never opened in Unity, never built, never on a headset. shaders never compiled.
- art: rocks, corals, wreck and props are real scans now. still code-made and weaker: sea fans, kelp, anemones, fish, the ray. no LODs yet.
- no demo video or screenshots, because nothing has run yet. none will be faked.
- no measured performance
- target headset not confirmed with the client yet (defaults assume Quest 3 / 3S, 72 Hz)

## The world

Five connected areas along one loop, all within ~75 m of the start, so nobody gets lost:

1. **The Descent** — start point. A sandy clearing, kelp swaying to one side, a natural rock arch framing the view toward the reef. Brightest, calmest area.
2. **The Reef** — dense coral garden. Brain, branching, table corals, sea fans, sponges, fish. Four interactables.
3. **Exhibition** — a clearing to the side of the reef with three information markers on an arc. The "museum" part.
4. **The Wreck** — a broken wooden hull half in the sand, fallen mast, crates, anchor, a ship's bell to inspect. Darker, more contrast.
5. **The Deep** — the floor drops away to ~14 m below the start. Basalt columns as landmarks, glowing anemones, tube worms, a big ray cruising at the edge of visibility. Darker but with a minimum light level so it's always navigable.

Walkways between areas are kept clear by the scene builder. Area looks (fog, light, caustics) blend as you move, there's no hard cut.

## Modes

- **Start exploring** — free roam, everything interactable.
- **Guided activities** — pick one of three, HUD guides you.
- **Exhibition mode (kiosk)** — free roam that resets itself to the intro when the headset is taken off for 8 s or nobody moves for 2 min. Pause menu gets "Reset for next visitor".

## Sample activities (demo content, flagged as SAMPLE in the headset)

1. **Discover the reef** — find the brain coral, sea fan and giant clam in any order.
2. **Take a closer look** — go to the wreck, inspect the ship's bell (rotate it with the thumbstick).
3. **Guided exploration** — exhibition → read a marker → wreck → find the anchor → the deep → find something glowing.

All text is general and placeholder. Replace with the client's material by editing the assets in `Assets/ABYSS/Data`.

## Controls (also taught in the intro)

| | |
|---|---|
| Point + trigger | select |
| Left stick forward, release | teleport |
| Right stick flick | snap turn |
| Thumbstick while inspecting | rotate the object |
| Left menu button | pause / settings / help / return to start |
| Both thumbsticks clicked | staff: perf logging on/off |

## Repo layout

```
Assets/ABYSS/
  Art/        models (fbx), shaders, textures, materials
  Audio/      generated wavs
  Data/       activities, interactable text, area looks, audio cues  <- client content goes here
  Scenes/     Bootstrap, MainExperience (generated)
  Scripts/    Core, XR, Interaction, Activities, Locomotion, Environment, UI, Audio, Performance, Editor
  Settings/   URP + lighting assets (generated)
  Tests/      edit mode unit tests
Tools/
  blender/        asset generation + preview renders
  audio/          sound generation
  textures/       caustics generation
  compile-check/  build everything with dotnet, no Unity needed
```

## Decisions worth knowing

- **OpenXR only, no Meta SDK.** Everything ABYSS needs is in OpenXR. Keeps it portable and lighter. If the client later wants passthrough or hand tracking, add `com.unity.xr.meta-openxr` / `com.unity.xr.hands`.
- **Photoscans for anything you look at closely, generated art for soft filler.** Rocks, corals, the wreck, props are CC0 scans (Poly Haven, Smithsonian) cut down to 1–6k tris with 1k textures. Sea floor uses the generated terrain shape with a scanned sand texture. Look-dev renders in `docs/lookdev`. First pass art looked fake; this is the fix.
- **UI built from code.** One look everywhere, no prefab drift, text size and high contrast apply to every panel automatically.
- **Progress resets each launch by default.** Right for exhibitions. Tick `Persist Between Sessions` on ProgressTracker for training use.
- **72 Hz default.** OpenXR 1.16 doesn't expose a refresh rate request API. If the client wants 90 Hz on Quest 3, that's a small custom OpenXR feature (XR_FB_display_refresh_rate), only worth doing after the 72 Hz numbers are in.

## Open questions for the client

1. Which headset(s) exactly? (affects refresh rate and performance targets)
2. What's the real training content, and in what form?
3. Kiosk use with staff, or self-service?
4. Any brand guidelines for the panels?
5. Seated, standing, or both? (affects the default height and whether stationary mode should be default)
