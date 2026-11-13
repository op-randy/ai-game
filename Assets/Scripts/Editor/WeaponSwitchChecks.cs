using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Play-mode check of the range weapons: slot ownership / switching / holster / mouse-wheel cycling / throw,
/// Glock slide blow-back measured frame by frame, and real camera captures (Logs/weapons-*.png) of the desk,
/// both guns at the hip and aimed, and the slide mid-cycle.
/// Dedicated batch editor only: -executeMethod WeaponSwitchChecks.Run (no -quit or -nographics).
/// Keyboard / mouse input itself is NOT exercised (no input in batch mode); switching goes through the same methods the keys call.
/// </summary>
[InitializeOnLoad]
public static class WeaponSwitchChecks
{
    const string RunningKey = "WeaponSwitchChecks.Running";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static RifleController rc;
    static int step, wait, lastFrame, sub;
    static double deadline;
    static readonly List<string> results = new List<string>();
    static readonly List<float> slideTrace = new List<float>();

    static WeaponSwitchChecks()
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
        EditorSceneManager.OpenScene("Assets/Scenes/ShootingRange.unity");
        SessionState.SetBool(RunningKey, true);
        EditorApplication.EnterPlaymode();
    }

    static void Check(bool ok, string what)
    {
        results.Add((ok ? "PASS " : "FAIL ") + what);
        Debug.Log("WeaponSwitchChecks: " + (ok ? "PASS " : "FAIL ") + what);
    }

    static Transform Find(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }

    static float SlideOffset()
    {
        var gun = rc.weaponRoot.childCount > 0 ? rc.weaponRoot.GetChild(0) : null;
        var slide = gun ? Find(gun, "Slide") : null;
        var frame = gun ? Find(gun, "Frame") : null;
        if (!slide || !frame) return float.NaN;
        // slide position relative to the frame, along the barrel, in metres (positive = back)
        return -Vector3.Dot(slide.position - frame.position, gun.forward);
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool(RunningKey, false)) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Weapon check timed out.");
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            if (wait > 0) { wait--; return; }
            switch (step++)
            {
                case 0:
                    Time.captureDeltaTime = 1f / 60f;
                    rc = GameObject.Find("Player").GetComponent<RifleController>();
                    rc.player.enabled = false;
                    rc.player.cameraPivot.localRotation = Quaternion.Euler(30, 0, 0);
                    wait = 5; break;
                case 1:
                    Save("desk");
                    rc.player.cameraPivot.localRotation = Quaternion.Euler(2, 0, 0);
                    Check(rc.weapons.Count == 2 && rc.weapons[1].prefab != null, "two weapon profiles, Glock prefab assigned");
                    Check(!rc.Equipped && !rc.Owns(0) && !rc.Owns(1), "starts unarmed");
                    rc.Select(1);
                    Check(!rc.Equipped, "cannot draw a weapon that is not owned");
                    rc.Give(0);
                    Check(rc.Equipped && rc.CurrentSlot == 0, "picking up the rifle draws slot 1");
                    rc.Give(1);
                    Check(rc.CurrentSlot == 1 && rc.Owns(0) && rc.Owns(1), "picking up the Glock draws slot 2, keeps the rifle");
                    rc.Cycle(1);
                    Check(rc.CurrentSlot == 0, "wheel down from slot 2 wraps to slot 1");
                    rc.Cycle(-1);
                    Check(rc.CurrentSlot == 1, "wheel up from slot 1 wraps to slot 2");
                    rc.Select(-1);
                    Check(!rc.Equipped && rc.CurrentSlot == -1, "slot 3 holsters");
                    rc.Cycle(1);
                    Check(rc.CurrentSlot == 0, "wheel from the holster draws the first owned weapon");
                    rc.Select(1);
                    wait = 40; break;                     // draw animation + arm calibration
                case 2:
                    Check(Mathf.Abs(SlideOffset()) < 0.001f, $"slide at rest before firing (offset {SlideOffset():F4} m)");
                    Save("glock-hip");
                    slideTrace.Clear();
                    typeof(RifleController).GetMethod("Fire", Private).Invoke(rc, null);
                    break;
                case 3: case 4: case 5: case 6: case 7: case 8: case 9: case 10: case 11: case 12:
                    slideTrace.Add(SlideOffset());
                    if (step == 4) Save("glock-hip-fire-frame1");
                    if (step == 5) Save("glock-hip-fire-frame2");
                    if (step == 13)
                    {
                        float max = 0f; foreach (var v in slideTrace) max = Mathf.Max(max, v);
                        Debug.Log("WeaponSwitchChecks: slide trace (m, per 1/60 s frame) " + string.Join(", ", slideTrace.ConvertAll(v => v.ToString("F4"))));
                        Check(max > 0.025f && max < 0.035f, $"slide runs back ~30 mm after a shot (max {max * 1000f:F1} mm)");
                        Check(slideTrace[slideTrace.Count - 1] < 0.0005f, "slide is home again within 10 frames");
                        rc.forcedAim = 1f;
                        wait = 20;
                    }
                    break;
                case 13:
                    Save("glock-aim");
                    typeof(RifleController).GetMethod("Fire", Private).Invoke(rc, null);
                    break;
                case 14:
                    Save("glock-aim-fire-frame1");
                    rc.forcedAim = -1f;
                    // view from the side for the grip
                    SideView("glock-side");
                    wait = 20; break;
                case 15:
                    Check(rc.Ammo == 13, $"two shots used two rounds ({rc.Ammo}/15)");
                    rc.StartReload();
                    Check(rc.Reloading, "R starts a reload");
                    sub = 0; break;
                case 16:                                   // tactical reload, 1/60 s per frame
                    sub++;
                    if (sub == 12) Save("reload-tilt");
                    if (sub == 21) { Save("reload-magout"); SideView("reload-magout-side"); }
                    if (sub == 36) { Save("reload-newmag"); SideView("reload-newmag-side"); }
                    if (sub == 52) Save("reload-seat");
                    if (sub < 90) { step--; break; }
                    Check(!rc.Reloading && rc.Ammo == 16, $"tactical reload ends with 15+1 ({rc.Ammo})");
                    var dropped = GameObject.Find("Glock 19 magazine (dropped)");
                    Check(dropped != null && dropped.GetComponent<Rigidbody>() != null && Find(dropped.transform, "MagRound") != null, "old (partly full) magazine dropped as a physics object");
                    if (dropped) UnityEngine.Object.Destroy(dropped);
                    break;
                case 17:
                    for (int i = 0; i < 16; i++) typeof(RifleController).GetMethod("Fire", Private).Invoke(rc, null);
                    Check(rc.Ammo == 0, "16 shots empty the gun");
                    int shotsBefore = rc.Shots;
                    typeof(RifleController).GetMethod("Fire", Private).Invoke(rc, null);
                    Check(rc.Shots == shotsBefore, "an empty gun does not fire");
                    wait = 15; break;
                case 18:
                    Check(SlideOffset() > 0.025f, $"slide locks back on empty ({SlideOffset() * 1000f:F1} mm)");
                    Save("glock-empty-locked");
                    rc.StartReload();
                    sub = 0; break;
                case 19:                                   // empty reload with overhand rack
                    sub++;
                    if (sub == 36) Save("empty-newmag");
                    if (sub == 52) Save("empty-seat");
                    if (sub == 62) { Save("empty-rack-reach"); SideView("empty-rack-side"); }
                    if (sub == 68) Save("empty-rack-release");
                    if (sub == 74) Save("empty-slide-home");
                    if (sub < 115) { step--; break; }
                    Check(!rc.Reloading && rc.Ammo == 15, $"empty reload ends with 15 ({rc.Ammo})");
                    Check(Mathf.Abs(SlideOffset()) < 0.001f, $"slide is home after the rack ({SlideOffset() * 1000f:F1} mm)");
                    var emptyMag = GameObject.Find("Glock 19 magazine (dropped)");
                    Check(emptyMag != null && Find(emptyMag.transform, "MagRound") == null, "dropped empty magazine has no round on top");
                    rc.Select(0);
                    wait = 40; break;
                case 20:
                    Check(rc.CurrentSlot == 0 && float.IsNaN(SlideOffset()), "switched back to the rifle (no slide)");
                    Save("rifle-hip");
                    rc.Select(1);
                    wait = 5; break;
                case 21:
                    rc.Throw();
                    Check(!rc.Owns(1) && rc.CurrentSlot == 0, "throwing the Glock removes it and draws the rifle");
                    var thrown = GameObject.Find("Glock 19 (thrown)");
                    var pickup = thrown ? thrown.GetComponent<WeaponPickup>() : null;
                    Check(pickup != null && pickup.weaponSlot == 1 && thrown.GetComponent<BoxCollider>().size.z > 0.1f, "thrown Glock is a slot-2 pickup with a collider around the whole gun");
                    if (pickup) pickup.PickUp(rc);
                    Check(rc.Owns(1) && rc.CurrentSlot == 1, "picking the thrown Glock up again draws it");
                    wait = 2; break;
                default:
                    int fails = results.FindAll(r => r.StartsWith("FAIL")).Count;
                    Debug.Log($"WeaponSwitchChecks: {(fails == 0 ? "PASS" : "FAIL")} ({results.Count - fails}/{results.Count}). Input keys/wheel and arm-pose quality NOT ASSESSED by this check.");
                    Finish(fails == 0 ? 0 : 1);
                    break;
            }
        }
        catch (Exception e) { Debug.LogException(e); Finish(1); }
    }

    static void SideView(string name)
    {
        var cam = rc.mainCamera.transform;
        var side = new GameObject("Side check camera (unsaved)").AddComponent<Camera>();
        try
        {
            side.cullingMask = 1 << LayerMask.NameToLayer("ViewModel");
            side.clearFlags = CameraClearFlags.SolidColor; side.backgroundColor = new Color(0.3f, 0.32f, 0.36f);
            side.nearClipPlane = 0.01f; side.fieldOfView = 35f;
            Vector3 c = cam.TransformPoint(new Vector3(0.05f, -0.15f, 0.3f));
            side.transform.position = c + cam.right * 0.9f + cam.up * 0.1f;
            side.transform.LookAt(c);
            Save(side, name);
        }
        finally { UnityEngine.Object.DestroyImmediate(side.gameObject); }
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
            File.WriteAllBytes($"Logs/weapons-{name}.png", image.EncodeToPNG());
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
