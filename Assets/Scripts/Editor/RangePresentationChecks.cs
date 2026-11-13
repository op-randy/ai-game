using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Fires the saved range rifle and captures its actual hip/ADS camera views.
/// A supplementary black-background render measures the same flash material's transparent corners.
/// Dedicated batch editor only: -executeMethod RangePresentationChecks.Run (no -quit or -nographics).
/// Add -expectSoftFlash to label post-fix captures. No scenes/assets are saved or rebuilt.
/// Audio checks cover imported clips/references, not subjective listening quality.
/// </summary>
[InitializeOnLoad]
public static class RangePresentationChecks
{
    const string RunningKey = "RangePresentationChecks.Running";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static RifleController rifle;
    static int frames, lastFrame;
    static double deadline;
    static readonly string label = Array.IndexOf(Environment.GetCommandLineArgs(), "-expectSoftFlash") >= 0 ? "fixed" : "baseline";

    static RangePresentationChecks()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
        if (SessionState.GetBool(RunningKey, false)) EditorApplication.update += Tick;
    }

    public static void Run()
    {
        if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use a dedicated batch editor, with no other editor on this project.");
        EditorSceneManager.OpenScene("Assets/Scenes/ShootingRange.unity");
        SessionState.SetBool(RunningKey, true);
        EditorApplication.EnterPlaymode();
    }

    static void OnPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(RunningKey, false)) return;
        deadline = EditorApplication.timeSinceStartup + 90;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool(RunningKey, false)) return;
        try
        {
            if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 90;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Range presentation check timed out.");
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            if (rifle == null)
            {
                Require(SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null, "A graphics device is required.");
                Time.captureDeltaTime = 1f / 60f;
                rifle = GameObject.Find("Player").GetComponent<RifleController>();
                rifle.player.enabled = false;
                rifle.Equip();
                rifle.enabled = false;
            }
            if (++frames < 8) return;
            foreach (var clip in rifle.shotClips) CheckClip(clip);
            Require(rifle.shotClips.Length == 3, "Expected three rifle shot variations.");
            CheckClip(rifle.targetHitClip);
            CaptureShot(0f, "hip");
            CaptureShot(1f, "ads");
            CheckFlashCorners();
            Debug.Log("RangePresentationChecks: PASS flash transparency, actual hip/ADS captures and imported audio references. Listening quality NOT ASSESSED.");
            Finish(0);
        }
        catch (Exception e) { Debug.LogException(e); Finish(1); }
    }

    static void CheckClip(AudioClip clip)
    {
        Require(clip != null && clip.length > 0.05f && clip.length < 2f, "Missing or unsuitable one-shot audio clip.");
        Require(clip.LoadAudioData(), "Could not load audio data for " + clip.name);
        var samples = new float[clip.samples * clip.channels];
        Require(clip.GetData(samples, 0), "Could not read " + clip.name);
        float peak = 0f;
        foreach (float sample in samples) peak = Mathf.Max(peak, Mathf.Abs(sample));
        Require(peak > 0.01f && peak < 1f, "Silent or clipped imported sound: " + clip.name);
        Debug.Log($"RangePresentationChecks: audio {clip.name}, length={clip.length:F3}s, peak={peak:F3}");
    }

    static void CaptureShot(float aim, string pose)
    {
        var flash = (Transform)typeof(RifleController).GetField("flashQuad", Private).GetValue(rifle);
        var light = (Light)typeof(RifleController).GetField("flashLight", Private).GetValue(rifle);
        rifle.SetPoseImmediate(aim);
        flash.gameObject.SetActive(false);
        light.enabled = false;
        Save(rifle.mainCamera, pose + "-off");
        UnityEngine.Random.InitState(54);
        typeof(RifleController).GetMethod("Fire", Private).Invoke(rifle, null);
        typeof(RifleController).GetMethod("UpdateEffects", Private).Invoke(rifle, null);
        Require(flash.gameObject.activeSelf, "Firing did not show the muzzle flash.");
        Save(rifle.mainCamera, pose + "-fire");
    }

    static void CheckFlashCorners()
    {
        var flash = (Transform)typeof(RifleController).GetField("flashQuad", Private).GetValue(rifle);
        GameObject sample = UnityEngine.Object.Instantiate(flash.gameObject);
        var cameraObject = new GameObject("Flash corner check camera (unsaved)");
        try
        {
            sample.transform.SetPositionAndRotation(new Vector3(10000f, 10000f, 10000f), Quaternion.identity);
            sample.transform.localScale = Vector3.one * 0.8f;
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.transform.SetPositionAndRotation(sample.transform.position - Vector3.forward, Quaternion.identity);
            camera.orthographic = true; camera.orthographicSize = 0.5f;
            camera.nearClipPlane = 0.01f; camera.farClipPlane = 2f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.cullingMask = 1 << sample.layer;
            var image = Render(camera, 128, 128);
            try
            {
                Write(image, "material");
                float corner = image.GetPixel(20, 20).maxColorComponent;
                float center = image.GetPixel(64, 64).maxColorComponent;
                Debug.Log($"RangePresentationChecks: flash corner={corner:F4}, center={center:F4}");
                Require(center > 0.1f, "Muzzle flash disappeared.");
                Require(corner < 0.02f, "Transparent muzzle-flash corners render as a bright square.");
            }
            finally { UnityEngine.Object.DestroyImmediate(image); }
        }
        finally { UnityEngine.Object.DestroyImmediate(sample); UnityEngine.Object.DestroyImmediate(cameraObject); }
    }

    static Texture2D Render(Camera camera, int width, int height)
    {
        RenderTexture oldTarget = camera.targetTexture, oldActive = RenderTexture.active;
        var target = new RenderTexture(width, height, 24);
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            return image;
        }
        catch { UnityEngine.Object.DestroyImmediate(image); throw; }
        finally
        {
            camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    static void Save(Camera camera, string name)
    {
        var image = Render(camera, 1280, 720);
        try { Write(image, name); }
        finally { UnityEngine.Object.DestroyImmediate(image); }
    }

    static void Write(Texture2D image, string name)
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllBytes($"Logs/muzzle-flash-{label}-{name}.png", image.EncodeToPNG());
    }

    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }

    static void Finish(int code)
    {
        SessionState.SetBool(RunningKey, false);
        EditorApplication.update -= Tick;
        EditorApplication.Exit(code);
    }
}
