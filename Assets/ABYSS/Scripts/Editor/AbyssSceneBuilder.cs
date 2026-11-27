using System.Collections.Generic;
using System.Linq;
using Abyss.Activities;
using Abyss.Audio;
using Abyss.Core;
using Abyss.Environment;
using Abyss.Interaction;
using Abyss.Locomotion;
using Abyss.Performance;
using Abyss.UI;
using Abyss.XR;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Abyss.EditorTools
{
    // builds Bootstrap (rig + every system) and MainExperience (the world) from the generated assets.
    // the layout is code so it can be regenerated after assets change. hand edits to the saved
    // scenes are fine, just know a rebuild overwrites them
    public static class AbyssSceneBuilder
    {
        const string ScenesDir = "Assets/ABYSS/Scenes";
        const string BootstrapPath = ScenesDir + "/Bootstrap/Bootstrap.unity";
        const string WorldPath = ScenesDir + "/MainExperience/MainExperience.unity";
        const string Art = "Assets/ABYSS/Art";

        static AbyssContentFactory.Content _c;
        static Vector3 _f, _r; // world forward (toward the deep) and right
        static readonly List<(Vector3 p, float r)> _keepClear = new List<(Vector3, float)>();
        static System.Random _rng;

        [MenuItem("ABYSS/2. Build content and scenes", priority = 20)]
        public static void BuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            _c = AbyssContentFactory.BuildAll();
            BuildWorld();
            BuildBootstrap();
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootstrapPath, true),
                new EditorBuildSettingsScene(WorldPath, true),
            };
            EditorSceneManager.OpenScene(BootstrapPath);
            EditorSceneManager.OpenScene(WorldPath, OpenSceneMode.Additive);
            Debug.Log("[ABYSS] scenes built. press play with Bootstrap as the first scene");
        }

        // ------------------------------------------------------------------ world
        static void BuildWorld()
        {
            AbyssProjectSetup.EnsureFolder(ScenesDir + "/MainExperience");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _rng = new System.Random(1234);
            _keepClear.Clear();

            var env = new GameObject("Environment").transform;
            var terrain = Spawn("Environment/seafloor_terrain", _c.World, env, Vector3.zero, Quaternion.identity, 1f, false);
            if (terrain == null) { Debug.LogError("[ABYSS] terrain fbx missing, run the blender build first"); return; }
            terrain.name = "Seafloor";
            var mf = terrain.GetComponentInChildren<MeshFilter>();
            var col = mf.gameObject.AddComponent<MeshCollider>();
            col.sharedMesh = mf.sharedMesh;
            var area = mf.gameObject.AddComponent<TeleportationArea>();
            area.interactionLayers = InteractionLayerMask.GetMask("Teleport") is int m && m != 0 ? m : 1 << 31;
            area.colliders.Clear(); area.colliders.Add(col);
            Physics.SyncTransforms();

            FindDeepDirection(mf);

            // lighting: one sun with the caustic cookie. no baking, no realtime gi
            var sunGo = new GameObject("Sun (surface light)");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.None;
            sun.transform.rotation = Quaternion.LookRotation(Vector3.down * 2.2f + _f * 0.6f + _r * 0.4f);
            sun.cookie = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/Textures/caustics_cookie.png");
            var urpLight = sunGo.GetComponent<UniversalAdditionalLightData>() ?? sunGo.AddComponent<UniversalAdditionalLightData>();
            urpLight.lightCookieSize = new Vector2(7, 7);
            sunGo.AddComponent<CausticsAnimator>();
            RenderSettings.sun = sun;
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            var d = _c.Profiles["descent"];
            RenderSettings.fogColor = d.FogColor; RenderSettings.fogDensity = d.FogDensity;
            RenderSettings.ambientSkyColor = d.AmbientSky; RenderSettings.ambientEquatorColor = d.AmbientEquator; RenderSettings.ambientGroundColor = d.AmbientGround;
            var ls = new LightingSettings { bakedGI = false, realtimeGI = false, name = "ABYSS_Lighting" };
            var lsPath = "Assets/ABYSS/Settings/ABYSS_Lighting.lighting";
            if (AssetDatabase.LoadAssetAtPath<LightingSettings>(lsPath) == null) AssetDatabase.CreateAsset(ls, lsPath);
            Lightmapping.lightingSettings = AssetDatabase.LoadAssetAtPath<LightingSettings>(lsPath);

            var envMgr = new GameObject("EnvironmentManager").AddComponent<EnvironmentManager>();
            SetRef(envMgr, "_fallback", d);
            SetRef(envMgr, "_sun", sun);

            Zone("descent", "The Descent", L(0, 0), new Vector3(36, 40, 34), 0, 0);
            Zone("reef", "The Reef", L(26, 0), new Vector3(34, 40, 22), 0, 1);
            Zone("exhibition", "Exhibition", L(30, -24), new Vector3(20, 40, 22), 0, 1);
            Zone("wreck", "The Wreck", L(46, 22), new Vector3(24, 40, 26), 0, 1);
            Zone("deep", "The Deep", L(72, 0) + Vector3.down * 10, new Vector3(60, 50, 34), 0, 2);

            var nav = new GameObject("NavAnchors").transform;
            Anchor(nav, "hub", "Start point", L(0, 0), _f);
            Anchor(nav, "reef", "The reef", L(17, 0), _f);
            Anchor(nav, "exhibition", "Exhibition", L(27, -20), Dir(3, -4));
            Anchor(nav, "wreck", "The wreck", L(38, 16), Dir(8, 6));
            Anchor(nav, "deep", "The deep", L(62, 0), _f);

            // walkways stay clear so scatter never blocks a route
            ClearPath(L(0, 0), L(30, 0), 2.6f);
            ClearPath(L(18, 0), L(29, -23), 2.4f);
            ClearPath(L(30, 0), L(40, 19), 2.4f);
            ClearPath(L(40, 19), L(66, 0), 2.6f);
            foreach (Transform a in nav) _keepClear.Add((a.position, 3f));

            BuildDescent(env);
            BuildReef(env);
            BuildExhibition(env);
            BuildWreck(env);
            BuildDeep(env);
            BuildAtmosphere(env);

            foreach (var t in env.GetComponentsInChildren<Transform>(true))
                if (t.GetComponent<InteractableObject>() == null && t.GetComponentInParent<GentleSway>() == null && t.GetComponentInParent<FishSchool>() == null && t.GetComponent<ParticleSystem>() == null && t.GetComponentInParent<InteractableObject>() == null && t.GetComponentInParent<DestinationMarker>() == null)
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);

            EditorSceneManager.SaveScene(scene, WorldPath);
        }

        static void BuildDescent(Transform env)
        {
            var root = Group(env, "Area1_Descent");
            // the arch frames the view toward the reef: first thing you see, gives a sense of scale
            var arch = Spawn("Environment/rock_arch", _c.World, root, Ground(L(13, 0), -0.6f), Quaternion.LookRotation(_r), 1.15f);
            Scatter(root, "Environment/rock_boulder_a", _c.World, 6, L(0, 0), 9, 16, 0.6f, 1.6f, -0.3f);
            Scatter(root, "Environment/rock_slab_a", _c.World, 4, L(0, 0), 8, 15, 0.8f, 1.5f, -0.25f);
            Scatter(root, "Environment/rock_spire_a", _c.World, 3, L(-4, 0), 12, 18, 1.0f, 1.8f, -0.4f);
            Scatter(root, "Environment/seagrass_clump", _c.WorldTwoSided, 30, L(2, 0), 3, 16, 0.8f, 1.4f, 0f);
            Scatter(root, "Coral/coral_brain", _c.World, 4, L(6, 6), 0, 5, 0.6f, 1.0f, -0.1f);

            var kelp = Group(root, "KelpBed");
            for (int i = 0; i < 28; i++)
            {
                var p = L(8 + Rand(-4, 4), -12 + Rand(-4, 4));
                if (Blocked(p)) continue;
                Spawn("Environment/kelp_strand", _c.WorldTwoSided, kelp, Ground(p, -0.05f), Quaternion.Euler(0, Rand(0, 360), 0), Rand(0.7f, 1.3f), false);
            }
            kelp.gameObject.AddComponent<GentleSway>();
            School(root, "FishSchool_Descent", L(6, 6) + Vector3.up * 3.5f, 36, new Vector3(10, 3, 10), 0.8f);
        }

        static void BuildReef(Transform env)
        {
            var root = Group(env, "Area2_Reef");
            Scatter(root, "Coral/coral_brain", _c.World, 10, L(26, 0), 3, 12, 0.6f, 1.4f, -0.12f);
            Scatter(root, "Coral/coral_staghorn", _c.World, 14, L(26, 0), 2.5f, 12, 0.8f, 1.5f, -0.05f);
            Scatter(root, "Coral/coral_table", _c.World, 8, L(26, 0), 3, 11, 0.8f, 1.6f, -0.05f);
            Scatter(root, "Coral/coral_seafan", _c.WorldTwoSided, 9, L(26, 0), 4, 12, 0.8f, 1.3f, -0.05f);
            Scatter(root, "Coral/sponge_barrel", _c.World, 7, L(26, 0), 4, 12, 0.7f, 1.3f, -0.1f);
            Scatter(root, "Environment/rock_boulder_b", _c.World, 8, L(26, 0), 7, 14, 0.8f, 1.6f, -0.4f);
            Scatter(root, "Environment/rock_cluster_a", _c.World, 6, L(26, 0), 6, 14, 0.7f, 1.3f, -0.3f);
            Scatter(root, "Environment/seagrass_clump", _c.WorldTwoSided, 20, L(26, 0), 2, 13, 0.7f, 1.2f, 0f);

            Interactable(root, "Coral/coral_brain", _c.World, "reef_brain_coral", L(22, -4), 1.2f);
            Interactable(root, "Coral/coral_seafan", _c.WorldTwoSided, "reef_sea_fan", L(27, 5), 1.3f);
            Interactable(root, "InteractiveObjects/giant_clam", _c.World, "reef_giant_clam", L(31, -2.5f), 1.4f);
            Interactable(root, "Coral/coral_staghorn", _c.World, "reef_staghorn", L(24, 4), 1.2f);
            School(root, "FishSchool_Reef", L(26, 0) + Vector3.up * 2.5f, 50, new Vector3(12, 3, 10), 0.9f);
        }

        static void BuildExhibition(Transform env)
        {
            var root = Group(env, "Area3_Exhibition");
            var c = L(30, -24);
            // three exhibits on a gentle arc, all within a step of the centre so nobody has to hunt
            Interactable(root, "InteractiveObjects/info_marker", _c.World, "exhibit_light", L(27.5f, -21.5f), 1.6f);
            Interactable(root, "InteractiveObjects/info_marker", _c.World, "exhibit_pressure", L(30.5f, -26.5f), 1.6f);
            Interactable(root, "InteractiveObjects/info_marker", _c.World, "exhibit_currents", L(33f, -22f), 1.6f);
            Scatter(root, "Environment/rock_slab_a", _c.World, 5, c, 6, 10, 1.0f, 1.8f, -0.3f);
            Scatter(root, "Environment/rock_boulder_a", _c.World, 4, c, 7, 11, 0.9f, 1.6f, -0.3f);
            Scatter(root, "Environment/seagrass_clump", _c.WorldTwoSided, 10, c, 4, 10, 0.7f, 1.1f, 0f);
            Destination(root, "dest_exhibition", "Exhibition", L(29, -23));
        }

        static void BuildWreck(Transform env)
        {
            var root = Group(env, "Area4_Wreck");
            var yaw = Quaternion.LookRotation(Dir(1, 0.6f));
            Spawn("Structures/wreck_hull", _c.World, root, Ground(L(47, 23), -1.1f), yaw * Quaternion.Euler(0, 0, 8), 1f);
            Spawn("Structures/wreck_mast", _c.World, root, Ground(L(43, 15), -0.1f), Quaternion.LookRotation(Dir(0.3f, -1)), 1f);
            for (int i = 0; i < 4; i++)
                Spawn("Structures/wreck_crate", _c.World, root, Ground(L(43 + Rand(-3, 4), 24 + Rand(-4, 4)), -0.25f), Quaternion.Euler(Rand(-10, 10), Rand(0, 360), Rand(-15, 15)), Rand(0.7f, 1.1f));
            Scatter(root, "Environment/rock_boulder_b", _c.World, 5, L(46, 22), 9, 14, 0.8f, 1.5f, -0.4f);
            Scatter(root, "Coral/sponge_barrel", _c.World, 4, L(46, 22), 5, 10, 0.5f, 0.9f, -0.1f);
            Scatter(root, "Coral/coral_seafan", _c.WorldTwoSided, 4, L(46, 22), 5, 10, 0.6f, 1.0f, -0.05f);

            var anchor = Interactable(root, "Structures/wreck_anchor", _c.World, "wreck_anchor", L(40.5f, 24.5f), 1.0f);
            if (anchor != null) anchor.transform.rotation = Quaternion.LookRotation(_r) * Quaternion.Euler(72, 0, 0);
            Interactable(root, "InteractiveObjects/ships_bell", _c.World, "wreck_bell", L(42, 18.5f), 1.0f);
            Destination(root, "dest_wreck", "The wreck", L(40, 19));
            School(root, "FishSchool_Wreck", L(46, 22) + Vector3.up * 2f, 28, new Vector3(9, 2.5f, 9), 0.6f);
        }

        static void BuildDeep(Transform env)
        {
            var root = Group(env, "Area5_Deep");
            Spawn("Environment/basalt_columns", _c.World, root, Ground(L(64, -7), -0.3f), Quaternion.Euler(0, 20, 0), 1.3f);
            Interactable(root, "Environment/basalt_columns", _c.World, "deep_columns", L(73, 9), 1.6f);
            Scatter(root, "Coral/anemone_glow", _c.WorldGlow, 14, L(70, 0), 3, 12, 0.8f, 1.6f, -0.05f);
            Scatter(root, "Coral/tube_worms", _c.World, 8, L(70, 0), 4, 12, 0.8f, 1.4f, -0.05f);
            Scatter(root, "Environment/rock_spire_a", _c.World, 6, L(70, 0), 9, 18, 1.2f, 2.4f, -0.6f);
            Scatter(root, "Environment/rock_boulder_a", _c.World, 6, L(68, 0), 7, 15, 1.0f, 2.0f, -0.4f);
            Interactable(root, "Coral/anemone_glow", _c.WorldGlow, "deep_anemone", L(67.5f, 3), 1.8f);
            Destination(root, "dest_deep", "The deep", L(66, 0));

            // a big ray cruising slowly at the edge of visibility
            var ray = new GameObject("Ray_Distant").transform;
            ray.SetParent(root, false);
            ray.position = Ground(L(74, 0), 4f);
            var fs = ray.gameObject.AddComponent<FishSchool>();
            fs.Configure(Mesh("MarineLife/ray_large"), _c.Fish, 1, new Vector3(34, 5, 34), 0.25f);
        }

        static void BuildAtmosphere(Transform env)
        {
            // drifting particles around the head. world space so they hang in the water
            var go = new GameObject("MarineSnow");
            go.transform.SetParent(env, false);
            go.AddComponent<FollowHead>();
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true; main.prewarm = true;
            main.startLifetime = 14f;
            main.startSpeed = 0.03f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.028f);
            main.maxParticles = 650;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.85f, 0.95f, 0.95f, 0.8f);
            var em = ps.emission; em.rateOverTime = 45f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(16, 8, 16);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.02f, 0.04f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.025f, -0.005f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.02f, 0.03f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = _c.Snow;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        }

        // ------------------------------------------------------------------ bootstrap
        static void BuildBootstrap()
        {
            AbyssProjectSetup.EnsureFolder(ScenesDir + "/Bootstrap");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<XRUIInputModule>();

            var rigPrefab = AssetDatabase.FindAssets("\"XR Origin (XR Rig)\" t:Prefab")
                .Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => p.Contains("Starter Assets"));
            GameObject rig = null;
            if (rigPrefab != null) rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(rigPrefab));
            else Debug.LogError("[ABYSS] XR rig prefab not found, run ABYSS > 1 (imports the XRI Starter Assets)");
            if (rig != null)
            {
                var cam = rig.GetComponentInChildren<Camera>();
                cam.nearClipPlane = 0.05f; cam.farClipPlane = 140f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = _c.Profiles["descent"].FogColor;
                var camData = cam.GetComponent<UniversalAdditionalCameraData>();
                if (camData != null) { camData.renderPostProcessing = false; camData.antialiasing = AntialiasingMode.None; }
            }

            var app = new GameObject("ABYSS_App");
            var boot = app.AddComponent<ApplicationBootstrap>();
            app.AddComponent<ExperienceStateManager>();
            app.AddComponent<SettingsManager>();
            app.AddComponent<ProgressTracker>();
            var exp = app.AddComponent<ExperienceManager>(); SetRef(exp, "_bootstrap", boot);
            var xr = app.AddComponent<XRConfigurationManager>(); if (rig != null) SetRef(xr, "_origin", rig.GetComponent<XROrigin>());
            app.AddComponent<InteractionManager>();
            app.AddComponent<InteractionFeedback>();
            app.AddComponent<ObjectInspection>();
            var gam = app.AddComponent<GuidedActivityManager>(); SetRef(gam, "_catalog", _c.Catalog);
            var comfort = app.AddComponent<ComfortController>(); if (rig != null) SetRef(comfort, "_rig", rig.transform);
            app.AddComponent<NavigationManager>();
            var fader = app.AddComponent<ScreenFader>(); SetRef(fader, "_material", _c.Fade);
            var audio = app.AddComponent<AudioManager>(); SetRef(audio, "_library", _c.Audio);
            app.AddComponent<PerformanceManager>();
            app.AddComponent<IdleWatcher>();

            var ui = new GameObject("UI"); ui.transform.SetParent(app.transform, false);
            ui.AddComponent<UIManager>();
            Child<MainMenuScreen>(ui, "MainMenu");
            Child<ActivityPickerScreen>(ui, "ActivityPicker");
            Child<PauseScreen>(ui, "Pause");
            Child<SettingsScreen>(ui, "Settings");
            Child<HelpScreen>(ui, "Help");
            Child<CompletionScreen>(ui, "Completion");
            Child<InformationDisplay>(app, "InfoPanel");
            Child<ActivityHud>(app, "ActivityHud");
            Child<OnboardingSequence>(app, "Onboarding");

            EditorSceneManager.SaveScene(scene, BootstrapPath);
        }

        // ------------------------------------------------------------------ helpers
        static void FindDeepDirection(MeshFilter mf)
        {
            // the terrain slopes away toward the deep. find which way that is in unity space
            // instead of trusting axis conversion between blender and unity
            var verts = mf.sharedMesh.vertices;
            var m = mf.transform.localToWorldMatrix;
            Vector3 sum = Vector3.zero; int n = 0;
            foreach (var v in verts)
            {
                var w = m.MultiplyPoint3x4(v);
                if (w.y < -6f) { sum += new Vector3(w.x, 0, w.z); n++; }
            }
            _f = n > 0 ? sum.normalized : Vector3.forward;
            _r = Vector3.Cross(Vector3.up, _f).normalized;
        }

        static Vector3 L(float fwd, float right) => _f * fwd + _r * right;
        static Vector3 Dir(float fwd, float right) => (_f * fwd + _r * right).normalized;
        static float Rand(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

        static Vector3 Ground(Vector3 p, float offset)
        {
            var o = new Vector3(p.x, 60f, p.z);
            if (Physics.Raycast(o, Vector3.down, out var hit, 200f)) return hit.point + Vector3.up * offset;
            return new Vector3(p.x, offset, p.z);
        }

        static void ClearPath(Vector3 a, Vector3 b, float r)
        {
            int steps = Mathf.CeilToInt(Vector3.Distance(a, b) / r);
            for (int i = 0; i <= steps; i++) _keepClear.Add((Vector3.Lerp(a, b, i / (float)steps), r));
        }

        static bool Blocked(Vector3 p)
        {
            foreach (var (c, r) in _keepClear)
            {
                var d = p - c; d.y = 0;
                if (d.sqrMagnitude < r * r) return true;
            }
            return false;
        }

        static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static Mesh Mesh(string rel)
        {
            return AssetDatabase.LoadAllAssetsAtPath($"{Art}/{rel}.fbx").OfType<Mesh>().FirstOrDefault();
        }

        static GameObject Spawn(string rel, Material mat, Transform parent, Vector3 pos, Quaternion rot, float scale, bool block = true)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Art}/{rel}.fbx");
            if (prefab == null) { Debug.LogWarning($"[ABYSS] missing model {rel}"); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = Vector3.one * scale;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            if (block) _keepClear.Add((pos, 0.6f * scale));
            return go;
        }

        static void Scatter(Transform parent, string rel, Material mat, int count, Vector3 center, float minR, float maxR, float minS, float maxS, float sink)
        {
            int placed = 0, tries = 0;
            while (placed < count && tries++ < count * 12)
            {
                float a = Rand(0, Mathf.PI * 2), r = Mathf.Sqrt(Rand(minR * minR, maxR * maxR));
                var p = center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r;
                if (Blocked(p)) continue;
                float s = Rand(minS, maxS);
                // slight tilt so nothing looks stamped out of the same mould
                var rot = Quaternion.Euler(Rand(-6, 6), Rand(0, 360), Rand(-6, 6));
                Spawn(rel, mat, parent, Ground(p, sink * s), rot, s);
                placed++;
            }
        }

        static GameObject Interactable(Transform parent, string rel, Material mat, string id, Vector3 at, float scale)
        {
            var go = Spawn(rel, mat, parent, Ground(at, 0f), Quaternion.Euler(0, Rand(0, 360), 0), scale);
            if (go == null) return null;
            go.name = "Interactable_" + id;
            // collider = bounds box, generous so it's easy to hit with the ray (collision volume, not art)
            var b = new Bounds(go.transform.position, Vector3.zero);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            var box = go.AddComponent<BoxCollider>();
            box.center = go.transform.InverseTransformPoint(b.center);
            var size = go.transform.InverseTransformVector(b.size);
            box.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)) * 1.15f;
            var xr = go.AddComponent<XRSimpleInteractable>();
            xr.colliders.Clear(); xr.colliders.Add(box);
            var io = go.AddComponent<InteractableObject>();
            _c.Defs.TryGetValue(id, out var def);
            io.Configure(def, _c.Highlight);
            EditorUtility.SetDirty(io);
            _keepClear.Add((go.transform.position, 1.6f));
            return go;
        }

        static void Destination(Transform parent, string id, string label, Vector3 at)
        {
            var root = new GameObject("Destination_" + id);
            root.transform.SetParent(parent, false);
            root.transform.position = Ground(at, 0.02f);
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var ring = Spawn("InteractiveObjects/destination_ring", _c.WorldGlow, visual.transform, root.transform.position + Vector3.up * 0.03f, Quaternion.identity, 1.2f, false);
            Spawn("InteractiveObjects/destination_beam", _c.Beam, visual.transform, root.transform.position, Quaternion.identity, 1.2f, false);
            var dm = root.AddComponent<DestinationMarker>();
            dm.Configure(id, label, 1.6f, visual, ring != null ? ring.transform : null);
            EditorUtility.SetDirty(dm);
            _keepClear.Add((root.transform.position, 2f));
        }

        static void School(Transform parent, string name, Vector3 at, int count, Vector3 area, float speed)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            go.AddComponent<FishSchool>().Configure(Mesh("MarineLife/fish_small"), _c.Fish, count, area, speed);
        }

        static void Zone(string id, string label, Vector3 center, Vector3 size, float yOffset, int priority)
        {
            var go = new GameObject("Zone_" + id);
            go.transform.position = center + Vector3.up * yOffset;
            go.transform.rotation = Quaternion.LookRotation(_f);
            var z = go.AddComponent<AreaZone>();
            z.Id = id; z.DisplayName = label; z.Profile = _c.Profiles[id]; z.Size = size; z.Priority = priority; z.Blend = 8f;
        }

        static void Anchor(Transform parent, string id, string label, Vector3 at, Vector3 facing)
        {
            var go = new GameObject("Anchor_" + id);
            go.transform.SetParent(parent, false);
            go.transform.position = Ground(at, 0f);
            go.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(facing, Vector3.up).normalized);
            var a = go.AddComponent<NavAnchor>(); a.Id = id; a.Label = label;
        }

        static T Child<T>(GameObject parent, string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go.AddComponent<T>();
        }

        static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"[ABYSS] {target.GetType().Name}.{field} not found"); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
