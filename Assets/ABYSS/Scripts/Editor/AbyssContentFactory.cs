using System.Collections.Generic;
using Abyss.Activities;
using Abyss.Audio;
using Abyss.Environment;
using Abyss.Interaction;
using Abyss.Logic;
using UnityEditor;
using UnityEngine;

namespace Abyss.EditorTools
{
    // makes (or updates) every data asset: materials, area looks, interactable text,
    // activities, audio cues. all demo text is flagged as sample so it's obvious what to replace.
    // re-running keeps edits made in the inspector unless the asset doesn't exist yet
    public static class AbyssContentFactory
    {
        public const string Data = "Assets/ABYSS/Data";
        public const string Mats = "Assets/ABYSS/Art/Materials";

        public class Content
        {
            public Material World, WorldTwoSided, WorldGlow, Fish, Highlight, Fade, Beam, Snow;
            public Dictionary<string, UnderwaterProfile> Profiles = new Dictionary<string, UnderwaterProfile>();
            public Dictionary<string, InteractableDefinition> Defs = new Dictionary<string, InteractableDefinition>();
            public ActivityCatalog Catalog;
            public AudioCueLibrary Audio;
        }

        public static Content BuildAll()
        {
            AbyssProjectSetup.EnsureFolder(Mats);
            AbyssProjectSetup.EnsureFolder(Data + "/Activities");
            AbyssProjectSetup.EnsureFolder(Data + "/Interactables");
            AbyssProjectSetup.EnsureFolder(Data + "/Profiles");
            var c = new Content();

            c.World = Mat("M_Underwater", "ABYSS/UnderwaterVertexLit");
            c.WorldTwoSided = Mat("M_UnderwaterTwoSided", "ABYSS/UnderwaterVertexLit", m => m.SetFloat("_Cull", 0));
            c.WorldGlow = Mat("M_UnderwaterGlow", "ABYSS/UnderwaterVertexLit", m => m.SetFloat("_Glow", 1.3f));
            c.Fish = Mat("M_Fish", "ABYSS/UnderwaterVertexLit", m => m.SetFloat("_Wrap", 0.6f));
            c.Highlight = Mat("M_HighlightRim", "ABYSS/HighlightRim");
            c.Fade = Mat("M_FadeOverlay", "ABYSS/FadeOverlay");
            c.Beam = Mat("M_MarkerBeam", "ABYSS/MarkerBeam");
            c.Snow = Mat("M_MarineSnow", "ABYSS/MarineSnow");
            foreach (var m in new[] { c.World, c.WorldTwoSided, c.WorldGlow, c.Fish }) { m.enableInstancing = true; EditorUtility.SetDirty(m); }

            // area looks. each one is tuned to stay readable, deep included (min ambient floor)
            c.Profiles["descent"] = Profile("Underwater_Descent", new Color(0.06f, 0.33f, 0.40f), 0.026f, new Color(0.36f, 0.62f, 0.66f), new Color(0.15f, 0.36f, 0.42f), new Color(0.10f, 0.16f, 0.16f), 1.25f, 1f, 0.08f);
            c.Profiles["reef"] = Profile("Underwater_Reef", new Color(0.07f, 0.36f, 0.42f), 0.03f, new Color(0.40f, 0.64f, 0.64f), new Color(0.17f, 0.38f, 0.42f), new Color(0.12f, 0.17f, 0.15f), 1.35f, 1f, 0.08f);
            c.Profiles["exhibition"] = Profile("Underwater_Exhibition", new Color(0.05f, 0.29f, 0.37f), 0.028f, new Color(0.34f, 0.56f, 0.62f), new Color(0.14f, 0.32f, 0.40f), new Color(0.09f, 0.13f, 0.15f), 1.1f, 0.8f, 0.08f);
            c.Profiles["wreck"] = Profile("Underwater_Wreck", new Color(0.04f, 0.22f, 0.29f), 0.038f, new Color(0.24f, 0.42f, 0.48f), new Color(0.10f, 0.24f, 0.30f), new Color(0.06f, 0.09f, 0.10f), 0.8f, 0.55f, 0.07f);
            c.Profiles["deep"] = Profile("Underwater_Deep", new Color(0.015f, 0.08f, 0.13f), 0.045f, new Color(0.10f, 0.20f, 0.28f), new Color(0.05f, 0.11f, 0.16f), new Color(0.03f, 0.04f, 0.05f), 0.35f, 0f, 0.06f);

            // ---- interactables. SAMPLE text: general, non-specialist, replace with the client's material
            Def(c, "reef_brain_coral", "Brain coral", "A slow-growing reef builder",
                "Brain corals are colonies of many tiny animals living together. The grooves are where neighbouring polyps share tissue. A colony this size can be decades old.", false);
            Def(c, "reef_sea_fan", "Sea fan", "Faces into the current",
                "Sea fans grow as a flat net, usually side-on to the water flow. That way every branch gets a share of the tiny food drifting past.", false);
            Def(c, "reef_giant_clam", "Giant clam", "Partly powered by sunlight",
                "The coloured tissue between the shells holds algae that turn sunlight into food for the clam. That's why giant clams live in shallow, bright water.", true, 0.45f);
            Def(c, "reef_staghorn", "Branching coral", "Shelter for small fish",
                "Branching corals grow fast and build a maze of gaps that small fish use to hide. They are also some of the most fragile corals on a reef.", false);
            Def(c, "exhibit_light", "Light underwater", "Why everything turns blue",
                "Water soaks up red light within the first few metres, then orange and yellow. By the time you're deep, mostly blue is left. Look at how the colours change as you go down.", false);
            Def(c, "exhibit_pressure", "Pressure", "Heavier with every metre",
                "Every ten metres of seawater adds roughly the weight of the whole atmosphere again. Animals in the deep are built for it, people need equipment.", false);
            Def(c, "exhibit_currents", "Currents", "The ocean is always moving",
                "Currents carry heat, food and larvae across huge distances. The swaying kelp and drifting particles around you show the water moving.", false);
            Def(c, "wreck_bell", "Ship's bell", "A sample object for inspection",
                "Bells were used to keep time on board and signal in fog. The green patches are what happens to brass after years in seawater. Turn it around and look at the crown and lip.", true, 0.4f);
            Def(c, "wreck_anchor", "Anchor", "Where the ship stopped",
                "This anchor lies a few metres from the hull. Its position is a clue for how the ship came to rest. (Sample content: replace with the client's own story.)", false);
            Def(c, "deep_anemone", "Glowing anemone", "Making its own light",
                "Many deep animals make light with chemistry inside their bodies. It's used to find food, confuse predators or recognise each other in the dark.", false);
            Def(c, "deep_columns", "Rock columns", "A landmark in the deep",
                "Columns like these form when lava cools slowly and cracks into regular shapes. Use them to find your way back.", false);

            // ---- activities
            c.Catalog = Load<ActivityCatalog>(Data + "/ActivityCatalog.asset", out bool newCatalog);
            var a1 = Activity("discover_reef", "Discover the reef", 3,
                "Three things on this reef are worth a closer look. Find them.",
                "You've found the brain coral, the sea fan and the giant clam.",
                new ActivityDefinition.Step { Id = "find_three", Kind = StepKind.Discover, Instruction = "Find and select the brain coral, the sea fan and the giant clam", CompletionText = "All three found", TargetIds = { "reef_brain_coral", "reef_sea_fan", "reef_giant_clam" }, RequireAllTargets = true });
            var a2 = Activity("inspect_object", "Take a closer look", 3,
                "Head to the wreck and examine an object up close.",
                "You've inspected the ship's bell up close.",
                new ActivityDefinition.Step { Id = "go_wreck", Kind = StepKind.ReachDestination, Instruction = "Go to the wreck. Follow the arrow and the light on the sea floor", CompletionText = "You're at the wreck", TargetIds = { "dest_wreck" } },
                new ActivityDefinition.Step { Id = "inspect_bell", Kind = StepKind.Inspect, Instruction = "Select the ship's bell, choose Inspect, turn it with the thumbstick, then press Done", CompletionText = "Inspection complete", TargetIds = { "wreck_bell" } });
            var a3 = Activity("guided_tour", "Guided exploration", 6,
                "A short tour through the exhibition, the wreck and the deep.",
                "You've visited the exhibition, the wreck and the deep.",
                new ActivityDefinition.Step { Id = "go_exhibition", Kind = StepKind.ReachDestination, Instruction = "Go to the exhibition area", CompletionText = "Arrived", TargetIds = { "dest_exhibition" } },
                new ActivityDefinition.Step { Id = "read_currents", Kind = StepKind.Interact, Instruction = "Select the Currents marker and read it", CompletionText = "Got it", TargetIds = { "exhibit_currents" } },
                new ActivityDefinition.Step { Id = "go_wreck", Kind = StepKind.ReachDestination, Instruction = "Now head to the wreck", CompletionText = "Arrived", TargetIds = { "dest_wreck" } },
                new ActivityDefinition.Step { Id = "find_anchor", Kind = StepKind.Interact, Instruction = "Find the anchor and select it", CompletionText = "Anchor found", TargetIds = { "wreck_anchor" } },
                new ActivityDefinition.Step { Id = "go_deep", Kind = StepKind.ReachDestination, Instruction = "Go down into the deep, past the rock columns", CompletionText = "Arrived", TargetIds = { "dest_deep" } },
                new ActivityDefinition.Step { Id = "find_glow", Kind = StepKind.Interact, Instruction = "Find something glowing and select it", CompletionText = "Found it", TargetIds = { "deep_anemone" } });
            if (newCatalog || c.Catalog.Activities.Count == 0)
            {
                c.Catalog.Activities = new List<ActivityDefinition> { a1, a2, a3 };
                EditorUtility.SetDirty(c.Catalog);
            }

            c.Audio = BuildAudio();
            AssetDatabase.SaveAssets();
            return c;
        }

        static Material Mat(string name, string shader, System.Action<Material> setup = null)
        {
            var path = $"{Mats}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var sh = Shader.Find(shader);
            if (sh == null) { Debug.LogError($"[ABYSS] shader {shader} missing"); sh = Shader.Find("Universal Render Pipeline/Unlit"); }
            if (m == null) { m = new Material(sh); setup?.Invoke(m); AssetDatabase.CreateAsset(m, path); }
            else if (m.shader != sh) m.shader = sh;
            return m;
        }

        static T Load<T>(string path, out bool created) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            created = a == null;
            if (created) { a = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(a, path); }
            return a;
        }

        static UnderwaterProfile Profile(string name, Color fog, float density, Color sky, Color eq, Color ground, float sun, float caustic, float minLuma)
        {
            var p = Load<UnderwaterProfile>($"{Data}/Profiles/{name}.asset", out bool created);
            if (!created) return p;
            p.FogColor = fog; p.FogDensity = density; p.AmbientSky = sky; p.AmbientEquator = eq; p.AmbientGround = ground;
            p.SunIntensity = sun; p.CausticStrength = caustic; p.MinAmbientLuma = minLuma;
            p.SunColor = Color.Lerp(new Color(0.78f, 0.95f, 0.97f), fog, 0.25f);
            EditorUtility.SetDirty(p);
            return p;
        }

        static void Def(Content c, string id, string title, string sub, string body, bool inspectable, float inspectSize = 0.35f)
        {
            var d = Load<InteractableDefinition>($"{Data}/Interactables/Interactable_{id}.asset", out bool created);
            if (created)
            {
                d.Id = id; d.Title = title; d.Subtitle = sub; d.Body = body;
                d.Inspectable = inspectable; d.InspectSize = inspectSize; d.IsSampleContent = true;
                EditorUtility.SetDirty(d);
            }
            c.Defs[id] = d;
        }

        static ActivityDefinition Activity(string id, string title, int minutes, string intro, string summary, params ActivityDefinition.Step[] steps)
        {
            var a = Load<ActivityDefinition>($"{Data}/Activities/Activity_{id}.asset", out bool created);
            if (created)
            {
                a.Id = id; a.Title = title; a.Minutes = minutes; a.Intro = intro; a.Summary = summary;
                a.IsSampleContent = true; a.Steps = new List<ActivityDefinition.Step>(steps);
                EditorUtility.SetDirty(a);
            }
            return a;
        }

        static AudioCueLibrary BuildAudio()
        {
            var lib = Load<AudioCueLibrary>($"{Data}/AudioCueLibrary.asset", out bool created);
            if (!created && lib.Cues.Count > 0) return lib;
            AudioClip Clip(string n) => AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/ABYSS/Audio/{n}.wav");
            lib.AmbientLoop = Clip("amb_underwater_loop"); lib.AmbientVolume = 0.55f;
            lib.BubblesLoop = Clip("amb_bubbles_loop"); lib.BubblesVolume = 0.22f;
            lib.Cues = new List<AudioCueLibrary.Entry>
            {
                new AudioCueLibrary.Entry { Cue = AudioCue.Hover, Clips = new[] { Clip("ui_hover") }, Volume = 0.25f, Spatial = 0.5f },
                new AudioCueLibrary.Entry { Cue = AudioCue.UiClick, Clips = new[] { Clip("ui_click") }, Volume = 0.45f, Spatial = 0.3f },
                new AudioCueLibrary.Entry { Cue = AudioCue.MenuOpen, Clips = new[] { Clip("ui_menu_open") }, Volume = 0.4f, Spatial = 0.2f },
                new AudioCueLibrary.Entry { Cue = AudioCue.Select, Clips = new[] { Clip("int_select") }, Volume = 0.5f, Spatial = 0.7f },
                new AudioCueLibrary.Entry { Cue = AudioCue.Fail, Clips = new[] { Clip("int_fail") }, Volume = 0.45f, Spatial = 0.7f },
                new AudioCueLibrary.Entry { Cue = AudioCue.Discover, Clips = new[] { Clip("int_discover") }, Volume = 0.55f, Spatial = 0.6f },
                new AudioCueLibrary.Entry { Cue = AudioCue.StepComplete, Clips = new[] { Clip("int_step_complete") }, Volume = 0.55f, Spatial = 0.2f },
                new AudioCueLibrary.Entry { Cue = AudioCue.ActivityComplete, Clips = new[] { Clip("int_activity_complete") }, Volume = 0.6f, Spatial = 0f },
                new AudioCueLibrary.Entry { Cue = AudioCue.Teleport, Clips = new[] { Clip("nav_teleport") }, Volume = 0.35f, Spatial = 0f },
            };
            EditorUtility.SetDirty(lib);
            return lib;
        }
    }
}
