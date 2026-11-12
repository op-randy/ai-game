using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Checks the replacement on the real avatar; renders front, side and rear previews.</summary>
public static class PonytailChecks
{
    public static void Run()
    {
        try
        {
            foreach (float dt in new[] { 1f / 30f, 1f / 60f, 1f / 144f }) CheckMotion(dt);
            Debug.Log("PonytailChecks: PASS (actual avatar, walking/running/crouching/jumping/turning/teleporting)");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static GameObject Character(out Animator animator, out PonytailHair hair)
    {
        var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAnimationSetup.ModelPath));
        model.name = "Ponytail preview";
        var renderers = model.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        model.transform.localScale *= 1.75f / bounds.size.y;
        CharacterAnimationSetup.AttachAnimator(model);
        animator = model.GetComponent<Animator>();
        animator.Rebind(); animator.Update(0f);
        hair = model.GetComponent<PonytailHair>();
        hair.Initialize();
        return model;
    }

    static void CheckMotion(float dt)
    {
        var model = Character(out var animator, out var hair);
        try
        {
            if (hair.RootLift < 0.035f) throw new Exception($"Ponytail must rise above the tie before falling; lift={hair.RootLift:F3} m");
            float peakError = 0f, peakPenetration = 0f, movement = 0f;
            foreach (string state in new[] { "Locomotion", "Crouch", "Jump" })
            {
                animator.Play(state, 0, 0f);
                animator.SetFloat("Speed", state == "Locomotion" ? 2.85f : 0.8f);
                animator.SetFloat("AnimSpeed", 1f);
                for (int frame = 0; frame < Mathf.CeilToInt(3f / dt); frame++)
                {
                    float t = frame * dt;
                    animator.Update(dt);
                    model.transform.position = new Vector3(Mathf.Sin(t * 3f) * 0.18f, state == "Jump" ? Mathf.Max(0f, Mathf.Sin(t * 3f)) * 0.5f : 0f, t * 1.3f);
                    model.transform.rotation = Quaternion.Euler(0f, Mathf.Sin(t * 2f) * 70f, 0f);
                    hair.Simulate(dt);
                    if (!float.IsFinite(hair.MaxBodyPenetration) || !float.IsFinite(hair.MaxSegmentError))
                        throw new Exception("Non-finite ponytail surface or constraint result.");
                    if (hair.RootLift < 0.025f) throw new Exception("The supported ponytail root collapsed during movement.");
                    Vector3 tip = hair.TipPosition;
                    if (!float.IsFinite(tip.x) || !float.IsFinite(tip.y) || !float.IsFinite(tip.z))
                        throw new Exception("Non-finite ponytail position.");
                    peakError = Mathf.Max(peakError, hair.MaxSegmentError);
                    peakPenetration = Mathf.Max(peakPenetration, hair.MaxBodyPenetration);
                    movement = Mathf.Max(movement, Mathf.Abs(model.transform.InverseTransformVector(tip - hair.RootPosition).x));
                    if (Vector3.Distance(tip, hair.RootPosition) > 1.1f)
                        throw new Exception("Ponytail stretched beyond its length.");
                    if (t > 0.25f && Vector3.Dot(hair.RootPosition - tip, Vector3.up) < 0.22f)
                        throw new Exception("Ponytail curled up toward the head instead of hanging down.");
                }
            }
            model.transform.position += Vector3.right * 10f;
            hair.Simulate(dt);
            if (Vector3.Distance(hair.RootPosition, hair.TipPosition) > 1.1f)
                throw new Exception("Ponytail failed to reset after teleport.");
            if (peakError > 0.04f) throw new Exception($"Ponytail segment stretching: {peakError:F4} m");
            if (peakPenetration > 0.005f) throw new Exception($"Ponytail body penetration: {peakPenetration:F4} m");
            if (movement < 0.01f) throw new Exception("Ponytail is rigid during turns.");
            Debug.Log($"PonytailChecks: {1f / dt:F0} fps, segment error={peakError:F4} m, body penetration={peakPenetration:F4} m, lateral movement={movement:F3}");
        }
        finally { UnityEngine.Object.DestroyImmediate(model); }
    }

    public static void Capture()
    {
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.65f);
            var key = new GameObject("Key light").AddComponent<Light>();
            key.type = LightType.Directional; key.intensity = 1.7f;
            key.transform.rotation = Quaternion.Euler(35f, -35f, 0f);
            var fill = new GameObject("Rim light").AddComponent<Light>();
            fill.type = LightType.Directional; fill.intensity = 1.1f;
            fill.transform.rotation = Quaternion.Euler(20f, 140f, 0f);
            var model = Character(out var animator, out var hair);
            var camera = new GameObject("Preview camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.10f, 0.13f, 0.18f);
            camera.orthographic = true; camera.orthographicSize = 0.64f;
            camera.nearClipPlane = 0.01f; camera.farClipPlane = 20f;
            for (int i = 0; i < 180; i++) { animator.Update(1f / 90f); hair.Simulate(1f / 90f); }
            Vector3 target = animator.GetBoneTransform(HumanBodyBones.Head).position + Vector3.down * 0.26f;
            Directory.CreateDirectory("Logs/Ponytail");
            foreach (var view in new[] { ("front", new Vector3(0.8f, 0.12f, 2f)), ("side", new Vector3(2f, 0.08f, 0f)), ("rear", new Vector3(0.8f, 0.12f, -2f)) })
            {
                camera.transform.position = target + view.Item2;
                camera.transform.LookAt(target);
                Shoot(camera, $"Logs/Ponytail/{view.Item1}.png");
            }
            // A separate moving frame catches a tail that only looks acceptable while standing still.
            animator.SetFloat("Speed", 2.85f);
            for (int i = 0; i < 45; i++)
            {
                animator.Update(1f / 90f);
                model.transform.rotation = Quaternion.Euler(0f, Mathf.Sin(i / 90f * 5f) * 35f, 0f);
                hair.Simulate(1f / 90f);
            }
            Shoot(camera, "Logs/Ponytail/turning.png");
            Debug.Log("PonytailChecks: previews saved to Logs/Ponytail");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static void Shoot(Camera camera, string path)
    {
        var rt = new RenderTexture(1000, 1000, 24);
        var image = new Texture2D(1000, 1000, TextureFormat.RGB24, false);
        var old = RenderTexture.active;
        try
        {
            camera.targetTexture = rt;
            camera.Render(); camera.Render();
            RenderTexture.active = rt;
            image.ReadPixels(new Rect(0, 0, 1000, 1000), 0, 0); image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null; RenderTexture.active = old;
            UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
