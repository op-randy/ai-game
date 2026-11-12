using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Better graphics: switches the project to URP with MSAA, HDR, soft 4-cascade shadows, SSAO and a post-processing
/// profile (ACES tonemapping, bloom, colour grading, vignette), and sets every quality level to full texture
/// resolution with anisotropic filtering. Imported models are re-imported so they get URP materials.
/// Menu: AI Game > Apply Graphics Settings (also run by the automatic rebuild)
/// </summary>
public static class GraphicsSetup
{
    const string Dir = "Assets/Settings";
    const string AssetPath = Dir + "/URP_Pipeline.asset";
    const string RendererPath = Dir + "/URP_Renderer.asset";
    public const string ProfilePath = Dir + "/PostProcessing.asset";

    [MenuItem("AI Game/Apply Graphics Settings")]
    public static void Apply()
    {
        Directory.CreateDirectory(Dir);
        var pipeline = EnsurePipeline();
        EnsureProfile();

        // every quality level: URP asset, sharp textures, anisotropic filtering
        int levels = QualitySettings.names.Length;
        int current = QualitySettings.GetQualityLevel();
        for (int i = 0; i < levels; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = pipeline;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.globalTextureMipmapLimit = 0;
            QualitySettings.vSyncCount = 1;
            QualitySettings.lodBias = 2f;
        }
        QualitySettings.SetQualityLevel(levels - 1, true);
        // use the highest level by default on every platform
        var qs = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
        var per = qs.FindProperty("m_PerPlatformDefaultQuality");
        for (int i = 0; i < per.arraySize; i++) per.GetArrayElementAtIndex(i).FindPropertyRelative("second").intValue = levels - 1;
        qs.ApplyModifiedProperties();

        bool switched = GraphicsSettings.defaultRenderPipeline != pipeline;
        GraphicsSettings.defaultRenderPipeline = pipeline;
        PlayerSettings.colorSpace = ColorSpace.Linear;
        AssetDatabase.SaveAssets();

        if (switched)
        {
            // models get their materials from the active pipeline at import time
            foreach (var p in new[] { "Assets/Characters/CatGirl/CatGirl.fbx", "Assets/Weapons/AssaultRifle/AssaultRifle.fbx" })
                if (File.Exists(p)) AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"GraphicsSetup: URP active ({(switched ? "switched now" : "already active")}), {levels} quality levels set to high detail");
    }

    static UniversalRenderPipelineAsset EnsurePipeline()
    {
        var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        if (rendererData == null)
        {
            rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, RendererPath);
            // screen-space ambient occlusion: contact shadows in corners and under objects
            var ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
            ssao.name = "SSAO";
            AssetDatabase.AddObjectToAsset(ssao, rendererData);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out string _, out long localId);
            var so = new SerializedObject(rendererData);
            var feats = so.FindProperty("m_RendererFeatures");
            feats.arraySize++; feats.GetArrayElementAtIndex(feats.arraySize - 1).objectReferenceValue = ssao;
            var map = so.FindProperty("m_RendererFeatureMap");
            map.arraySize++; map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(rendererData);
        }
        // subtle contact shadows (a large radius darkens whole floors)
        foreach (var f in rendererData.rendererFeatures)
            if (f is ScreenSpaceAmbientOcclusion)
            {
                f.SetActive(true);
                EditorUtility.SetDirty(f);
                EditorUtility.SetDirty(rendererData);
                var sso = new SerializedObject(f);
                var settings = sso.FindProperty("m_Settings");
                if (settings != null)
                {
                    settings.FindPropertyRelative("Intensity").floatValue = 1.0f;
                    settings.FindPropertyRelative("Radius").floatValue = 0.12f;
                    sso.ApplyModifiedProperties();
                }
            }

        var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetPath);
        if (asset == null)
        {
            asset = UniversalRenderPipelineAsset.Create(rendererData);
            AssetDatabase.CreateAsset(asset, AssetPath);
        }
        asset.supportsHDR = true;
        asset.msaaSampleCount = 4;
        asset.renderScale = 1f;
        asset.shadowDistance = 80f;
        asset.shadowCascadeCount = 4;
        asset.mainLightShadowmapResolution = 4096;
        var aso = new SerializedObject(asset);
        SetIfExists(aso, "m_MainLightShadowsSupported", true);
        SetIfExists(aso, "m_SoftShadowsSupported", true);
        var q = aso.FindProperty("m_SoftShadowQuality"); if (q != null) q.intValue = 3;   // high
        aso.ApplyModifiedProperties();
        EditorUtility.SetDirty(asset);
        return asset;
    }

    static void SetIfExists(SerializedObject so, string prop, bool v)
    {
        var p = so.FindProperty(prop);
        if (p != null) p.boolValue = v;
    }

    static VolumeProfile EnsureProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile != null) return profile;
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, ProfilePath);

        var tone = profile.Add<Tonemapping>(true);
        tone.mode.value = TonemappingMode.ACES;

        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.value = 1.0f;
        bloom.intensity.value = 0.55f;
        bloom.scatter.value = 0.65f;

        var color = profile.Add<ColorAdjustments>(true);
        color.postExposure.value = 0.35f;
        color.contrast.value = 14f;
        color.saturation.value = 8f;

        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.value = 0.22f;
        vignette.smoothness.value = 0.45f;

        foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    /// <summary>Adds a global post-processing volume to the open scene.</summary>
    public static void AddVolume()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null) return;
        var go = new GameObject("Post Processing");
        var v = go.AddComponent<Volume>();
        v.isGlobal = true;
        v.sharedProfile = profile;
    }

    /// <summary>Post-processing + SMAA on a game camera.</summary>
    public static void SetupCamera(Camera cam)
    {
        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        data.antialiasingQuality = AntialiasingQuality.High;
    }

    /// <summary>Makes the weapon camera draw on top of the main camera (URP camera stack).</summary>
    public static void SetupOverlay(Camera main, Camera overlay)
    {
        var o = overlay.GetUniversalAdditionalCameraData();
        o.renderType = CameraRenderType.Overlay;
        var m = main.GetUniversalAdditionalCameraData();
        if (!m.cameraStack.Contains(overlay)) m.cameraStack.Add(overlay);
    }
}
