using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Replays input snapshots through the real range player's movement step, without saving the scene.
/// Run in a dedicated editor: -batchmode -executeMethod SprintSlideJumpChecks.Run (no -quit).
/// Does not test legacy keyboard delivery, camera quality or human feel.
/// Add -movementSnapshots (without -nographics) to capture the actual equipped camera at sample frames.
/// </summary>
[InitializeOnLoad]
public static class SprintSlideJumpChecks
{
    const string RunningKey = "SprintSlideJumpChecks.Running";
    static FirstPersonController player;
    static CharacterController controller;
    static int phase, frames, lastFrame, scenario;
    static double deadline;
    static float previousSpeed, launchSpeed, landingSpeed;
    static Vector3 launchPosition;
    static bool airborne;
    static GameObject ceiling;
    static Type inputType;
    static MethodInfo movementStep;
    static readonly HashSet<KeyCode> held = new HashSet<KeyCode>();
    static readonly HashSet<KeyCode> pressed = new HashSet<KeyCode>();
    static readonly bool snapshots = Array.IndexOf(Environment.GetCommandLineArgs(), "-movementSnapshots") >= 0;
    static KeyCode SlideKey => scenario == 1 ? KeyCode.C : KeyCode.LeftControl;

    static SprintSlideJumpChecks()
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
        deadline = EditorApplication.timeSinceStartup + 120;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool(RunningKey, false)) return;
        try
        {
            if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 120;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Transition replay timed out.");
            if (lastFrame == Time.frameCount || Time.deltaTime <= 0f) return;
            lastFrame = Time.frameCount;
            if (player == null)
            {
                Time.captureDeltaTime = 1f / 60f;
                player = GameObject.Find("Player").GetComponent<FirstPersonController>();
                controller = player.GetComponent<CharacterController>();
                player.enabled = false;
                inputType = typeof(FirstPersonController).GetNestedType("MovementInput", BindingFlags.NonPublic);
                movementStep = typeof(FirstPersonController).GetMethod("UpdateMovement", BindingFlags.Instance | BindingFlags.NonPublic);
                ResetPosition();
                if (snapshots)
                {
                    Require(SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null, "Snapshots require a graphics device.");
                    player.GetComponent<RifleController>().Equip();
                }
            }

            Step();
            frames++;
            float speed = player.PlanarVelocity.magnitude;
            if (phase == 0 && frames > 5 && player.IsGrounded)
            {
                Key(KeyCode.W, true);
                Key(KeyCode.LeftShift, true);
                Next(1);
            }
            else if (phase == 1)
            {
                if (frames > 120) throw new Exception($"Sprint replay failed: speed={speed:F3}.");
                if (player.IsSprinting && speed >= player.sprintSpeed * 0.98f)
                {
                    Debug.Log($"SprintSlideJumpChecks: {SlideKey} sprint speed={speed:F3}");
                    Key(SlideKey, true);
                    Next(2);
                }
            }
            else if (phase == 2)
            {
                if (frames > 15 && !player.IsSliding) throw new Exception($"{SlideKey} did not start a slide.");
                if (player.IsSliding && frames >= 6)
                {
                    Require(player.IsCrouching && !player.IsSprinting, "Slide reports incompatible stance/sprint state.");
                    launchSpeed = speed;
                    launchPosition = player.transform.position;
                    Debug.Log($"SprintSlideJumpChecks: {SlideKey} slide speed={speed:F3}, height={controller.height:F3}");
                    Capture("slide");
                    if (scenario == 2)
                    {
                        ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        ceiling.name = "Transition check low ceiling (unsaved)";
                        ceiling.transform.position = new Vector3(1.8f, 1.65f, launchPosition.z + 2f);
                        ceiling.transform.localScale = new Vector3(2f, 0.2f, 8f);
                        Physics.SyncTransforms();
                    }
                    Key(KeyCode.Space, true);
                    Next(3);
                }
            }
            else if (phase == 3)
            {
                if (frames == (scenario == 2 ? 3 : 12)) Capture("airborne");
                if (scenario == 2 && frames >= 3)
                {
                    if (frames == 3) Debug.Log($"SprintSlideJumpChecks: blocked stance crouching={player.IsCrouching}, sprinting={player.IsSprinting}, speed={speed:F3}, grounded={player.IsGrounded}");
                    Require(player.IsCrouching, "Player tried to stand inside the low ceiling after slide-jump.");
                    Require(!player.IsSprinting, "Blocked stand-up reports sprinting while forced to crouch after slide-jump.");
                }
                if (frames == 2)
                {
                    Key(KeyCode.Space, false);
                    Key(SlideKey, false);
                }
                if (!player.IsGrounded)
                {
                    airborne = true;
                    Require(!player.IsSliding, "Slide remained active in the air after jumping.");
                }
                if (frames == 12)
                    Debug.Log($"SprintSlideJumpChecks: {SlideKey} airborne speed at 0.2s={speed:F3}, crouching={player.IsCrouching}, sliding={player.IsSliding}");
                if (frames > 90) throw new Exception("Jump did not leave the ground and land within 90 frames.");
                if (airborne && player.IsGrounded)
                {
                    landingSpeed = speed;
                    Require(player.transform.position.z > launchPosition.z + (scenario == 2 ? 0.1f : 1f), "Slide-jump did not travel forward.");
                    Debug.Log($"SprintSlideJumpChecks: {SlideKey} landed: launch={launchSpeed:F3}, landing={landingSpeed:F3}, distance={player.transform.position.z - launchPosition.z:F3}");
                    Capture("landing");
                    if (scenario == 2) Next(5);
                    else Recover();
                }
            }
            else if (phase == 5)
            {
                Require(player.IsCrouching && !player.IsSprinting, "Blocked landing did not maintain crouch-only movement.");
                if (frames >= 30)
                {
                    Require(speed <= player.crouchSpeed + 0.05f, $"Blocked stand-up kept sprint speed: {speed:F3}.");
                    Debug.Log($"SprintSlideJumpChecks: PASS low-ceiling movement uses crouch speed={speed:F3} despite held sprint");
                    Recover();
                }
            }
            else if (phase == 4 && frames >= 45)
            {
                Require(!player.IsSliding && !player.IsCrouching && !player.IsSprinting, "Stance/sprint remained stuck after landing and releasing input.");
                Require(Mathf.Abs(controller.height - 1.8f) < 0.02f, "Standing capsule height did not recover.");
                Require(Mathf.Abs(player.cameraPivot.localPosition.y - player.standEyeHeight) < 0.02f, "Standing eye height did not recover.");
                Require(speed < 0.1f, "Player kept moving after releasing input.");
                Capture("standing");
                Debug.Log($"SprintSlideJumpChecks: PASS scenario={scenario} {SlideKey} sprint-slide-jump, landing and stance recovery");
                if (++scenario == 3) Finish(0);
                else
                {
                    ResetPosition();
                    airborne = false;
                    Next(0);
                }
            }

            // Measure rather than prescribe air-control tuning. Flag only a discontinuous loss
            // beyond either of the controller's configured deceleration rates in open space.
            if (phase == 3 && airborne && !player.IsGrounded && previousSpeed > 0f)
                Require(previousSpeed - speed <= Mathf.Max(player.acceleration, player.slideFriction) * Time.deltaTime + 0.1f,
                    $"Unexpected momentum discontinuity: {previousSpeed:F3} -> {speed:F3}.");
            previousSpeed = speed;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            Finish(1);
        }
    }

    static void ResetPosition()
    {
        // Clear center-lane path beside the existing desk; no geometry or assets are changed.
        controller.enabled = false;
        player.transform.position = new Vector3(1.8f, 0.05f, -6f);
        controller.enabled = true;
        Physics.SyncTransforms();
    }

    static void Next(int next) { phase = next; frames = 0; }

    static void Recover()
    {
        Key(KeyCode.W, false);
        Key(KeyCode.LeftShift, false);
        if (ceiling) { UnityEngine.Object.DestroyImmediate(ceiling); Physics.SyncTransforms(); }
        Next(4);
    }

    static void Key(KeyCode key, bool down)
    {
        if (down) { if (held.Add(key)) pressed.Add(key); }
        else held.Remove(key);
    }

    static void Step()
    {
        object input = Activator.CreateInstance(inputType);
        inputType.GetField("move").SetValue(input, held.Contains(KeyCode.W) ? Vector2.up : Vector2.zero);
        inputType.GetField("crouchTogglePressed").SetValue(input, pressed.Contains(KeyCode.LeftControl));
        inputType.GetField("crouchHeld").SetValue(input, held.Contains(KeyCode.C));
        inputType.GetField("crouchPressed").SetValue(input, pressed.Contains(KeyCode.C));
        inputType.GetField("sprintHeld").SetValue(input, held.Contains(KeyCode.LeftShift));
        inputType.GetField("sprintPressed").SetValue(input, pressed.Contains(KeyCode.LeftShift));
        inputType.GetField("jumpPressed").SetValue(input, pressed.Contains(KeyCode.Space));
        movementStep.Invoke(player, new[] { input });
        pressed.Clear();
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    static void Capture(string label)
    {
        if (!snapshots) return;
        Camera camera = player.GetComponent<RifleController>().mainCamera;
        RenderTexture oldTarget = camera.targetTexture, oldActive = RenderTexture.active;
        bool oldEnabled = camera.enabled;
        var target = new RenderTexture(1280, 720, 24);
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.enabled = false;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes($"Logs/sprint-slide-jump-{scenario}-{label}.png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = oldTarget;
            camera.enabled = oldEnabled;
            RenderTexture.active = oldActive;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    static void Finish(int code)
    {
        foreach (var key in new[] { KeyCode.W, KeyCode.LeftShift, KeyCode.LeftControl, KeyCode.C, KeyCode.Space }) Key(key, false);
        SessionState.SetBool(RunningKey, false);
        EditorApplication.update -= Tick;
        EditorApplication.Exit(code);
    }
}
