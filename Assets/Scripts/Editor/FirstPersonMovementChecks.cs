using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Headless physics regression on the saved range player, with an unsaved ceiling fixture.
/// Run with -batchmode -executeMethod FirstPersonMovementChecks.Run (without -quit).
/// Seeds jump velocity rather than synthesizing keyboard input; not a visual or input test.
/// </summary>
[InitializeOnLoad]
public static class FirstPersonMovementChecks
{
    const string RunningKey = "FirstPersonMovementChecks.Running";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static FirstPersonController player;
    static CharacterController controller;
    static GameObject ceiling;
    static FieldInfo vertical;
    static MethodInfo update;
    static int phase, lastFrame;
    static double deadline;
    static float started, peak;
    static Vector3 spawn;

    static FirstPersonMovementChecks()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
        if (SessionState.GetBool(RunningKey, false))
            EditorApplication.update += Tick;
    }

    public static void Run()
    {
        if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run only in a dedicated batch editor with no other editor on this project.");
        EditorSceneManager.OpenScene("Assets/Scenes/ShootingRange.unity");
        SessionState.SetBool(RunningKey, true);
        EditorApplication.EnterPlaymode();
    }

    static void OnPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(RunningKey, false)) return;
        deadline = EditorApplication.timeSinceStartup + 30;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool(RunningKey, false)) return;
        try
        {
            if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 30;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Movement check timed out.");
            if (Time.frameCount == lastFrame || Time.deltaTime <= 0f) return;
            lastFrame = Time.frameCount;
            if (player == null)
            {
                // Batch editors can advance by large wall-clock steps. Use a stable game step.
                Time.captureDeltaTime = 1f / 60f;
                player = GameObject.Find("Player").GetComponent<FirstPersonController>();
                controller = player.GetComponent<CharacterController>();
                player.enabled = false; // Invoke the production Update once per frame with no keyboard input.
                vertical = typeof(FirstPersonController).GetField("verticalVelocity", Private);
                update = typeof(FirstPersonController).GetMethod("Update", Private);
                spawn = player.transform.position;
                started = Time.time;
            }

            update.Invoke(player, null);
            if (phase == 0 && controller.isGrounded)
            {
                ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ceiling.name = "Movement check ceiling (unsaved)";
                ceiling.transform.position = spawn + Vector3.up * 2.5f;
                ceiling.transform.localScale = new Vector3(4f, 0.2f, 4f);
                Physics.SyncTransforms();
                Launch();
                phase = 1;
            }
            else if (phase == 1 && (controller.collisionFlags & CollisionFlags.Above) != 0)
            {
                float velocity = (float)vertical.GetValue(player);
                Debug.Log($"FirstPersonMovementChecks: ceiling contact y={player.transform.position.y:F3}, verticalVelocity={velocity:F3}");
                if (velocity > 0f) throw new Exception("Ceiling collision retained upward velocity instead of stopping ascent.");
                started = Time.time;
                phase = 2;
            }
            else if (phase == 2 && controller.isGrounded)
            {
                Debug.Log($"FirstPersonMovementChecks: PASS ceiling collision and landing ({Time.time - started:F3}s after contact)");
                UnityEngine.Object.DestroyImmediate(ceiling);
                Physics.SyncTransforms();
                peak = player.transform.position.y;
                Launch();
                phase = 3;
            }
            else if (phase == 3)
            {
                peak = Mathf.Max(peak, player.transform.position.y);
                if (Time.time - started > 0.1f && controller.isGrounded)
                {
                    float height = peak - spawn.y;
                    if (height < player.jumpHeight * 0.75f || height > player.jumpHeight * 1.25f)
                        throw new Exception($"Unobstructed jump height {height:F3} outside tolerance for {player.jumpHeight:F3}.");
                    Debug.Log($"FirstPersonMovementChecks: PASS unobstructed jump and landing, height={height:F3}");
                    Finish(0);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            Finish(1);
        }
    }

    static void Launch()
    {
        vertical.SetValue(player, Mathf.Sqrt(player.jumpHeight * -2f * player.gravity));
        started = Time.time;
    }

    static void Finish(int code)
    {
        SessionState.SetBool(RunningKey, false);
        EditorApplication.update -= Tick;
        EditorApplication.Exit(code);
    }
}
