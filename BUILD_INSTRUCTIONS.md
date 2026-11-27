# Build instructions

## What you need

- Unity Hub + **Unity 6000.3.29f1** with **Android Build Support** (incl. OpenJDK and Android SDK & NDK tools)
- a Unity licence (Personal is fine)
- a Meta Quest (Quest 3 / 3S / Pro / 2) with **developer mode** on, a USB-C data cable
- Git (+ Git LFS, the repo tracks fbx/png/wav through LFS)

## First time

1. Clone, then open the folder in Unity Hub with 6000.3.29f1. First import pulls the packages, takes a few minutes.
2. Menu **ABYSS > 1. Configure project for Quest**
   - switches to Android, IL2CPP, ARM64, Vulkan, ASTC, min API 32, linear colour
   - makes the URP asset with the Quest profile (Settings folder)
   - turns on OpenXR for Android with Meta Quest support + Touch / Touch Plus / Touch Pro profiles, and for Standalone (so you can press Play over Quest Link)
   - imports the XRI Starter Assets + XR Interaction Simulator samples and TMP Essentials
   - run it **twice** on a fresh clone: the second run catches OpenXR settings that only exist after the first import finishes
3. Menu **ABYSS > 2. Build content and scenes**. Makes materials, data assets and both scenes, adds them to Build Settings.
4. Open `Edit > Project Settings > XR Plug-in Management > Project Validation` and fix anything red (usually nothing).

## Run in the editor

- **With a headset:** Quest Link on, press Play with `Bootstrap` open.
- **Without a headset:** Project Settings > XR Plug-in Management > XR Interaction Toolkit > tick *Use XR Interaction Simulator in scenes*, then Play. Mouse/keyboard drive the rig. Esc = menu button, F9 = perf logging, WASD rotates during inspection.

## Build the APK

Menu: **ABYSS > 4. Build Quest APK** → `Builds/ABYSS.apk`.

Command line:

```
Unity -batchmode -quit -projectPath . -buildTarget Android \
  -executeMethod Abyss.EditorTools.AbyssBuild.BuildQuestApk [-abyssDev] -logFile build.log
```

`-abyssDev` makes a development build (`Builds/ABYSS_dev.apk`). Use that one for profiling, the draw call / batch counters only report in development builds.

## Install on the headset

```
adb devices                       # headset should show as "device", accept the prompt inside the headset
adb install -r Builds/ABYSS.apk
```

Then in the headset: App Library > filter *Unknown sources* > ABYSS.

Or drag the APK into Meta Quest Developer Hub.

## Pull performance logs

In the app, click **both thumbsticks together** to start logging, again to stop. Files land in:

```
adb pull /sdcard/Android/data/co.theingredientlist.abyss/files/perf/
```

## Regenerating the generated stuff

```
blender -b -P Tools/blender/build_assets.py -- Assets/ABYSS/Art        # models
blender -b -P Tools/blender/preview_assets.py -- Assets/ABYSS/Art /tmp/previews
python3 Tools/audio/make_audio.py Assets/ABYSS/Audio                    # sounds
python3 Tools/textures/make_caustics.py Assets/ABYSS/Art/Textures/caustics_cookie.png
```

Then ABYSS > 2 again.

## Compile check without Unity

`Tools/compile-check` builds every script against the real Unity 6.3 DLLs and package sources with plain `dotnet`. See the readme in there. Handy for CI, not a replacement for opening the project.
