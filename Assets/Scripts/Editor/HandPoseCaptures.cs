using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Play-mode captures of the first-person hands on both weapons (Logs/pose-*.png): hip and aimed through the real
/// game camera, a close-up of the grip, and key reload frames. For tuning, optional JSON overrides are applied before
/// the weapons are drawn: Logs/handpose-rc.json (RifleController), Logs/handpose-rifle.json / handpose-glock.json (profiles).
/// Dedicated batch editor only: -executeMethod HandPoseCaptures.Run (no -quit or -nographics).
/// This produces images for a visual review; it does not judge them.
/// </summary>
[InitializeOnLoad]
public static class HandPoseCaptures
{
    const string RunningKey = "HandPoseCaptures.Running";
    static RifleController rc;
    static int step, wait, lastFrame, sub;
    static double deadline;

    static HandPoseCaptures()
    {
        EditorApplication.playModeStateChanged += s =>
        {
            if (s != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(RunningKey, false)) return;
            deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        };
        if (SessionState.GetBool(RunningKey, false)) EditorApplication.update += Tick;
    }

    public static void Run()
    {
        if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use a dedicated batch editor, with no other editor on this project.");
        ShootingRangeBuilder.ConfigureArmsImport();
        EditorSceneManager.OpenScene("Assets/Scenes/ShootingRange.unity");
        SessionState.SetBool(RunningKey, true);
        EditorApplication.EnterPlaymode();
    }

    static void Override(object target, string file)
    {
        string p = "Logs/" + file;
        if (!File.Exists(p)) return;
        if (target is UnityEngine.Object o) EditorJsonUtility.FromJsonOverwrite(File.ReadAllText(p), o);
        else JsonUtility.FromJsonOverwrite(File.ReadAllText(p), target);
        Debug.Log("HandPoseCaptures: applied " + p);
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool(RunningKey, false)) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Hand pose captures timed out.");
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            if (wait > 0) { wait--; return; }
            switch (step++)
            {
                case 0:
                    Time.captureDeltaTime = 1f / 60f;
                    rc = GameObject.Find("Player").GetComponent<RifleController>();
                    rc.player.enabled = false;
                    rc.player.cameraPivot.localRotation = Quaternion.Euler(2, 0, 0);
                    var armsModel = AssetDatabase.LoadAssetAtPath<GameObject>(ShootingRangeBuilder.FPArmsPath);
                    if (armsModel)
                    {
                        rc.armsCharacterPrefab = armsModel;
                        rc.armsMaterialSource = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAnimationSetup.ModelPath);
                        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(ShootingRangeBuilder.FPArmsPath)) if (a is Avatar av) rc.armsAvatar = av;
                    }
                    Override(rc, "handpose-rc.json");
                    Override(rc.weapons[0], "handpose-rifle.json");
                    Override(rc.weapons[1], "handpose-glock.json");
                    rc.Give(0); rc.Give(1);
                    wait = 40; break;
                case 1:
                    Save("glock-hip"); Close("glock-hip-close"); ThreeQuarter("glock-hip-3q"); LeftSide("glock-hip-left"); LogPose("glock hip");
                    rc.forcedAim = 1f; wait = 25; break;
                case 2:
                    Save("glock-aim"); Close("glock-aim-close");
                    rc.forcedAim = -1f; wait = 25; break;
                case 3:
                    for (int i = 0; i < 3; i++) typeof(RifleController).GetMethod("Fire", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(rc, null);
                    rc.StartReload(); sub = 0; break;
                case 4:
                    sub++;
                    if (sub == 12) Save("reload-tilt");
                    if (sub == 36) { Save("reload-newmag"); ThreeQuarter("reload-newmag-3q"); }
                    if (sub == 52) Save("reload-seat");
                    if (sub < 90) { step--; break; }
                    rc.Select(0); wait = 40; break;
                case 5:
                    Save("rifle-hip"); Close("rifle-hip-close"); ThreeQuarter("rifle-hip-3q"); LeftSide("rifle-hip-left"); Close("rifle-left-close", "GripLeft"); Below("rifle-left-below", "GripLeft"); LogPose("rifle hip");
                    rc.forcedAim = 1f; wait = 25; break;
                case 6:
                    Save("rifle-aim");
                    Debug.Log("HandPoseCaptures: DONE (images in Logs/pose-*.png; quality NOT judged by this tool)");
                    Finish(0); break;
            }
        }
        catch (Exception e) { Debug.LogException(e); Finish(1); }
    }

    static Camera Temp(out GameObject go)
    {
        go = new GameObject("Pose check camera (unsaved)");
        var c = go.AddComponent<Camera>();
        c.cullingMask = 1 << LayerMask.NameToLayer("ViewModel");
        c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0.3f, 0.32f, 0.36f);
        c.nearClipPlane = 0.01f;
        return c;
    }

    static Vector3 GripPoint(string marker = "GripRight")
    {
        var gun = rc.weaponRoot.GetChild(0);
        foreach (var t in gun.GetComponentsInChildren<Transform>(true)) if (t.name == marker) return t.position;
        return gun.position;
    }

    /// <summary>From the eye, zoomed in on the grip (what the player sees, magnified).</summary>
    static void Close(string name, string marker = "GripRight")
    {
        var c = Temp(out var go);
        try
        {
            var eye = rc.mainCamera.transform;
            c.transform.position = eye.position;
            c.transform.LookAt(GripPoint(marker), eye.up);
            c.fieldOfView = 22f;
            Save(c, name);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    /// <summary>From the front-left, below the muzzle: shows how the hands wrap the grip.</summary>
    static void ThreeQuarter(string name)
    {
        var c = Temp(out var go);
        try
        {
            var eye = rc.mainCamera.transform;
            Vector3 g = GripPoint();
            c.transform.position = g + eye.forward * 0.55f - eye.right * 0.35f + eye.up * 0.05f;
            c.transform.LookAt(g, eye.up);
            c.fieldOfView = 30f;
            Save(c, name);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    /// <summary>Straight from the left of the gun: grip height, wrap and thumbs.</summary>
    static void LeftSide(string name)
    {
        var c = Temp(out var go);
        try
        {
            var eye = rc.mainCamera.transform;
            Vector3 g = GripPoint();
            c.transform.position = g - eye.right * 0.7f;
            c.transform.LookAt(g, eye.up);
            c.fieldOfView = 22f;
            Save(c, name);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    /// <summary>Gun-space positions (x right, y up, z forward, metres) of the grip markers and the hand bones.</summary>
    static void LogPose(string label)
    {
        var gun = rc.weaponRoot.GetChild(0);
        var arms = UnityEngine.Object.FindAnyObjectByType<FirstPersonArms>();
        string P(Vector3 w) { var v = gun.InverseTransformPoint(w) * gun.lossyScale.x; return $"({v.x:F3},{v.y:F3},{v.z:F3})"; }
        var sb = new System.Text.StringBuilder($"HandPoseCaptures: {label} gun-space:");
        foreach (var t in gun.GetComponentsInChildren<Transform>(true))
            if (t.name == "GripRight" || t.name == "GripLeft" || t.name == "LeftHandTarget") sb.Append($" {t.name}{P(t.position)}");
        foreach (var r in gun.GetComponentsInChildren<Renderer>(true))
            if (r.name == "Frame") sb.Append($" FrameBounds{P(r.bounds.min)}..{P(r.bounds.max)}");
        if (arms)
        {
            var an = arms.GetComponent<Animator>();
            foreach (var b in new[] { HumanBodyBones.RightHand, HumanBodyBones.LeftHand, HumanBodyBones.RightLowerArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.RightUpperArm, HumanBodyBones.LeftUpperArm })
            { var t = an.GetBoneTransform(b); if (t) sb.Append($" {b}{P(t.position)}"); }
            foreach (var t in arms.GetComponentsInChildren<Transform>(true))
                if (t.name.EndsWith("MiddleProximal") || t.name.EndsWith("ThumbDistal")) sb.Append($" {t.name}{P(t.position)}");
        }
        Debug.Log(sb.ToString());
    }

    /// <summary>From below and slightly left of a marker, looking up.</summary>
    static void Below(string name, string marker)
    {
        var c = Temp(out var go);
        try
        {
            var eye = rc.mainCamera.transform;
            Vector3 g = GripPoint(marker);
            c.transform.position = g - eye.up * 0.45f - eye.right * 0.2f + eye.forward * 0.05f;
            c.transform.LookAt(g, eye.forward);
            c.fieldOfView = 30f;
            Save(c, name);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    static void Save(string name) => Save(rc.mainCamera, name);

    static void Save(Camera camera, string name)
    {
        RenderTexture oldTarget = camera.targetTexture, oldActive = RenderTexture.active;
        var target = new RenderTexture(1280, 720, 24);
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes($"Logs/pose-{name}.png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
            UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
        }
    }

    static void Finish(int code)
    {
        SessionState.SetBool(RunningKey, false);
        EditorApplication.update -= Tick;
        EditorApplication.Exit(code);
    }
}
