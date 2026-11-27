using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager.UI;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

namespace Abyss.EditorTools
{
    // one click (or one -executeMethod) to get a fresh clone into a buildable Quest state.
    // safe to run again, it only sets things, never deletes
    public static class AbyssProjectSetup
    {
        public const string SettingsDir = "Assets/ABYSS/Settings";
        const string Loader = "UnityEngine.XR.OpenXR.OpenXRLoader";
        const string XriPackage = "com.unity.xr.interaction.toolkit";

        [MenuItem("ABYSS/1. Configure project for Quest", priority = 1)]
        public static void ConfigureAll()
        {
            ConfigurePlayer();
            ConfigureUrp();
            ConfigureXR(BuildTargetGroup.Android, quest: true);
            ConfigureXR(BuildTargetGroup.Standalone, quest: false); // for editor play over Quest Link
            ImportSamples();
            ImportTmpEssentials();
            AssetDatabase.SaveAssets();
            Debug.Log("[ABYSS] project configured. next: ABYSS > 2. Build content and scenes");
        }

        public static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "The Ingredient List";
            PlayerSettings.productName = "ABYSS";
            var android = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(android, "co.theingredientlist.abyss");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            PlayerSettings.enableFrameTimingStats = true;  // PerformanceManager needs this for cpu/gpu times
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Low);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.stripEngineCode = true;
        }

        public static UniversalRenderPipelineAsset ConfigureUrp()
        {
            EnsureFolder(SettingsDir);
            string assetPath = SettingsDir + "/ABYSS_URP.asset", rendererPath = SettingsDir + "/ABYSS_Renderer.asset";
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if (asset == null)
            {
                var data = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(data, rendererPath);
                asset = UniversalRenderPipelineAsset.Create(data);
                AssetDatabase.CreateAsset(asset, assetPath);
            }

            // quest profile: 4x msaa (tile gpus get it nearly free), no hdr, no depth/opaque copies,
            // no realtime shadows (soft underwater light doesn't need them), one main light with a cookie
            var so = new SerializedObject(asset);
            Set(so, "m_MSAA", 4);
            Set(so, "m_SupportsHDR", false);
            Set(so, "m_RequireDepthTexture", false);
            Set(so, "m_RequireOpaqueTexture", false);
            Set(so, "m_MainLightShadowsSupported", false);
            Set(so, "m_AdditionalLightShadowsSupported", false);
            Set(so, "m_AnyShadowsSupported", false);
            Set(so, "m_AdditionalLightsRenderingMode", 0); // disabled
            Set(so, "m_UseSRPBatcher", true);
            Set(so, "m_RenderScale", 1f);
            Set(so, "m_SupportsLightCookies", true);
            so.ApplyModifiedPropertiesWithoutUndo();

            GraphicsSettings.defaultRenderPipeline = asset;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }
            QualitySettings.SetQualityLevel(current, false);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        static void Set(SerializedObject so, string field, object v)
        {
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"[ABYSS] URP field {field} not found, skipped"); return; }
            switch (v)
            {
                case bool b: p.boolValue = b; break;
                case int i: p.intValue = i; break;
                case float f: p.floatValue = f; break;
            }
        }

        public static void ConfigureXR(BuildTargetGroup group, bool quest)
        {
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget per) || per == null)
            {
                EnsureFolder("Assets/XR");
                per = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(per, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, per, true);
            }
            if (!per.HasSettingsForBuildTarget(group)) per.CreateDefaultSettingsForBuildTarget(group);
            if (!per.HasManagerSettingsForBuildTarget(group)) per.CreateDefaultManagerSettingsForBuildTarget(group);
            var general = per.SettingsForBuildTarget(group);
            general.InitManagerOnStart = true;
            var manager = per.ManagerSettingsForBuildTarget(group);
            XRPackageMetadataStore.AssignLoader(manager, Loader, group);
            EditorUtility.SetDirty(per);

            var oxr = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
            if (oxr == null) { Debug.LogWarning($"[ABYSS] no OpenXR settings for {group} yet, run this again after the import finishes"); return; }
            oxr.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            Enable<OculusTouchControllerProfile>(oxr);
            if (quest)
            {
                Enable<MetaQuestFeature>(oxr);
                Enable<MetaQuestTouchPlusControllerProfile>(oxr);
                Enable<MetaQuestTouchProControllerProfile>(oxr);
            }
            EditorUtility.SetDirty(oxr);
        }

        static void Enable<T>(OpenXRSettings s) where T : UnityEngine.XR.OpenXR.Features.OpenXRFeature
        {
            var f = s.GetFeature<T>();
            if (f != null) { f.enabled = true; EditorUtility.SetDirty(f); }
            else Debug.LogWarning($"[ABYSS] OpenXR feature {typeof(T).Name} missing");
        }

        // starter assets = the XR rig with teleport/turn/ray interactors + haptics.
        // interaction simulator = drive the rig with mouse/keyboard in the editor when there's no headset
        public static void ImportSamples()
        {
            foreach (var s in Sample.FindByPackage(XriPackage, null))
            {
                if ((s.displayName == "Starter Assets" || s.displayName == "XR Interaction Simulator") && !s.isImported)
                {
                    s.Import(Sample.ImportOptions.OverridePreviousImports | Sample.ImportOptions.HideImportWindow);
                    Debug.Log($"[ABYSS] imported XRI sample {s.displayName}");
                }
            }
            NameTeleportLayer();
        }

        // starter assets expect interaction layer 31 to be called Teleport
        static void NameTeleportLayer()
        {
            var guid = AssetDatabase.FindAssets("t:InteractionLayerSettings").FirstOrDefault();
            if (guid == null) return;
            var so = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid)));
            var names = so.FindProperty("m_LayerNames");
            if (names != null && names.arraySize > 31 && string.IsNullOrEmpty(names.GetArrayElementAtIndex(31).stringValue))
            {
                names.GetArrayElementAtIndex(31).stringValue = "Teleport";
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        public static void ImportTmpEssentials()
        {
            if (AssetDatabase.FindAssets("t:TMP_Settings").Length > 0) return;
            AssetDatabase.ImportPackage("Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage", false);
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
