using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>Headless smoke checks: Unity -batchmode -executeMethod CharacterMotionChecks.Run.</summary>
public static class CharacterMotionChecks
{
    public static void Run()
    {
        try
        {
            CheckHairResponse();
            CharacterAnimationSetup.Setup();
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterAnimationSetup.ControllerPath);
            foreach (string name in new[] { "Locomotion", "Jump", "Crouch", "Climb", "Punch1" })
            {
                bool found = Array.Exists(controller.layers[0].stateMachine.states, s => s.state.name == name);
                if (!found) throw new Exception($"Missing character state: {name}");
            }
            foreach (string name in new[] { "Jump", "CrouchIdle", "CrouchWalk" })
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{CharacterAnimationSetup.AnimDir}/CatGirl_{name}.anim");
                if (clip == null || !clip.humanMotion || clip.length <= 0f)
                    throw new Exception($"Invalid humanoid clip: {name}");
                if (!Array.Exists(AnimationUtility.GetCurveBindings(clip), b => b.propertyName == "RootT.y"))
                    throw new Exception($"Missing humanoid body height curve: {name}");
            }
            Debug.Log("CharacterMotionChecks: PASS");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    static void CheckHairResponse()
    {
        var root = new GameObject("Hair response fixture");
        try
        {
            Transform parent = root.transform;
            for (int i = 0; i < 4; i++)
            {
                var bone = new GameObject($"Hair_Test_{i}").transform;
                bone.SetParent(parent, false);
                bone.localPosition = Vector3.down * 0.1f;
                parent = bone;
            }
            var physics = root.AddComponent<SpringBones>();
            physics.collide = false;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(SpringBones).GetMethod("Start", flags).Invoke(physics, null);
            typeof(SpringBones).GetMethod("ResetParticles", flags).Invoke(physics, null);
            var simulate = typeof(SpringBones).GetMethod("Simulate", flags);
            float peak = 0f;
            for (int i = 0; i < 45; i++)
            {
                // A 4 cm head sway, with no scene, animator or frame-rate variability.
                root.transform.position = Vector3.right * (0.04f * Mathf.Sin(i / 90f * 2f * Mathf.PI * 2f));
                simulate.Invoke(physics, new object[] { 1f / 90f });
                peak = Mathf.Max(peak, Vector3.Angle(Vector3.down, parent.rotation * Vector3.down));
            }
            Debug.Log($"CharacterMotionChecks: hair tip sway = {peak:F2} degrees");
            if (peak < 12f) throw new Exception("Hair is too stiff: a 4 cm head sway should bend the tips at least 12 degrees.");
            if (peak > 85f) throw new Exception("Hair bends too far during a small head sway.");
            float followThrough = 0f;
            for (int i = 0; i < 30; i++)
            {
                simulate.Invoke(physics, new object[] { 1f / 90f });
                if (i >= 20)
                    followThrough = Mathf.Max(followThrough, Vector3.Angle(Vector3.down, parent.rotation * Vector3.down));
            }
            Debug.Log($"CharacterMotionChecks: hair follow-through after stopping = {followThrough:F2} degrees");
            if (followThrough < 2f) throw new Exception("Hair stops too abruptly: missing soft follow-through after head movement.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
