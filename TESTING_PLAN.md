# Testing plan

Rule: nothing gets ticked because the code exists. Each item says **where** it was actually checked.

Where things can be tested:
- **CI** = plain dotnet in a Linux box, no Unity (compile check + unit tests)
- **Editor** = Unity editor, XR Interaction Simulator
- **Link** = Unity editor, Play over Quest Link
- **Device** = APK installed on the headset

## Status right now (end of session 1)

What was actually run:

| Check | Where | Result |
|---|---|---|
| Unit tests for activity runner, settings, progress (17 tests) | CI | **17/17 pass** |
| All runtime scripts compile against Unity 6000.3.29f1 engine DLLs + real XRI 3.3.2 / OpenXR 1.16.1 / Input System 1.20.1 / uGUI 2.0 / Core Utils 2.6.0 / Burst / Mathematics source | CI | **0 errors, 0 warnings in project code** |
| All editor scripts compile against the UnityEditor module DLLs + XR Management / OpenXR / Input System editor source | CI | **0 errors** |
| URP API used (render scale, light cookie size/offset, camera data, asset create) | CI, against a stub whose signatures were checked by hand against URP 17.3.0 source | compiles. stub, not the real URP |
| 28 Blender models export and re-import as FBX | Blender 4.5.17 headless | **all 28 ok**, preview sheet reviewed by eye |
| Audio + caustic texture generate | python | ok |

What has **not** been run, at all:
- the Unity editor has not opened this project (no licence in the build machine)
- the 5 shaders have never been compiled
- the scene builder and project setup menus have never been executed
- nothing has run on a headset, so **zero** of the device tests below are done
- no performance numbers exist

## Test cases

Status column: ⬜ not run · ✅ pass · ❌ fail. Fill in date + device when run.

| # | Test | How | Pass when | Where | Status |
|---|---|---|---|---|---|
| 1 | App launch | install APK, launch from library | logo shows within 5 s, no black screen > 1 s, fades into water | Device | ⬜ |
| 2 | Head tracking | look around, crouch, step | world stays locked, no judder, floor height right | Device | ⬜ |
| 3 | Controller tracking | move both controllers | models follow 1:1, rays come from the controllers | Device | ⬜ |
| 4 | Selection | point at the brain coral, trigger | glow on hover + light buzz, select gives sound + buzz + panel | Device / Editor | ⬜ |
| 5 | Inspection | select clam, Inspect, rotate with stick, Done | object comes to ~0.6 m, rotates both axes, returns home, locomotion blocked meanwhile | Device / Editor | ⬜ |
| 6 | Info panels | open several | text readable without leaning in, panel beside the object not over it, sample tag visible | Device | ⬜ |
| 7 | Teleport | left stick forward, aim, release | arc on floor only, fade, land facing sensibly | Device | ⬜ |
| 8 | Snap turn | flick right stick | 45° default, change to 30/60/smooth in settings and it applies right away | Device | ⬜ |
| 9 | Re-centre | Pause > Face forward | rig turns, camera never jumps vertically | Device | ⬜ |
| 10 | Menu navigation | every button on every screen | each does what it says, Back returns to the previous screen, nothing opens behind you | Device / Editor | ⬜ |
| 11 | Activity progression | run each activity, also select wrong things | HUD updates, wrong object gives "Not part of this task" + double buzz, arrow points at next target | Device / Editor | ⬜ |
| 12 | Activity completion | finish all 3 | completion screen, summary, count goes up | Device / Editor | ⬜ |
| 13 | Activity restart | Pause > Restart mid-activity, and Do it again at the end | back to step 1, markers reset | Device / Editor | ⬜ |
| 14 | Audio settings | each slider to 0 / 100, mute | each category changes independently, mute silences all, nothing ever startling | Device | ⬜ |
| 15 | Comfort settings | every toggle | stationary blocks all movement but allows turning, vignette shows while moving, reduced motion calms fish/panels | Device | ⬜ |
| 16 | Exit | Main menu > Exit | returns to Quest home cleanly | Device | ⬜ |
| 17 | Performance | perf log, 2 min per area, Standard then Reduced | no frame over budget for sustained periods at the device refresh rate (72 Hz = 13.9 ms) | Device (dev build) | ⬜ |
| 18 | Memory | perf log over a 20 min session | allocated memory flat (no climb) | Device (dev build) | ⬜ |
| 19 | Scene transitions | launch, every Return to start, every area boundary | no hitches, fog/light blend smoothly, deep never pitch black | Device | ⬜ |
| 20 | Recovery | take headset off mid-activity in kiosk mode; spam buttons; break an activity asset on purpose | kiosk resets after 8 s off-head; broken activity logs an error and returns to menu instead of hanging | Device / Editor | ⬜ |
| 21 | Onboarding | first launch, skip at each card, replay from Help | never stuck, Skip always visible, menu button skips | Device | ⬜ |
| 22 | Text size / contrast | 80% and 160%, high contrast on | nothing clipped at 160%, high contrast readable on every panel | Device | ⬜ |
| 23 | Audio off | mute, run all 3 activities | every success/fail still has a visible label | Device | ⬜ |
| 24 | First-timer test | someone who has never used VR, no help | finishes intro + activity 1 alone | Device | ⬜ |

## Performance report template

Fill one row per run. Leave blank rather than estimate.

| Date | Device | Build | Profile | Area | Avg FPS | CPU avg/max ms | GPU avg/max ms | Draw calls | Batches | Tris | Mem MB | Eye texture | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| | | | | | | | | | | | | | |

The CSV from the in-app logger has all of these columns.
