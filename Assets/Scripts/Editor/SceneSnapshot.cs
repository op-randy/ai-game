using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Renders the demo scene's main camera (and a close-up of the player) to PNG files in Logs/.
/// Command line: Unity -batchmode -projectPath . -executeMethod SceneSnapshot.Capture -quit
/// </summary>
public static class SceneSnapshot
{
    [MenuItem("AI Game/Save Snapshot")]
    public static void Capture()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/DemoLevel.unity");
        var cam = Camera.main;
        var player = GameObject.Find("Player");
        Directory.CreateDirectory("Logs");
        Shoot(cam, "Logs/snapshot_game_camera.png");

        if (player != null)
        {
            var go = new GameObject("SnapCam");
            var c = go.AddComponent<Camera>();
            c.CopyFrom(cam);
            c.transform.position = player.transform.position + new Vector3(0, 1.1f, 3.2f);
            c.transform.LookAt(player.transform.position + Vector3.up * 0.95f);
            Shoot(c, "Logs/snapshot_player_front.png");

            c.transform.position = new Vector3(0, 22, -38);
            c.transform.LookAt(new Vector3(0, 0, 2));
            Shoot(c, "Logs/snapshot_level_overview.png");
            Object.DestroyImmediate(go);
        }
        Debug.Log("SceneSnapshot: saved");
    }

    /// <summary>Plays the walk animation in the editor and saves a few frames from the side.</summary>
    public static void CaptureWalk()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/DemoLevel.unity");
        var player = GameObject.Find("Player");
        var anim = player != null ? player.GetComponentInChildren<Animator>() : null;
        if (anim == null) { Debug.LogError("SceneSnapshot: no Animator on the player"); return; }
        Debug.Log($"SceneSnapshot: animator controller={anim.runtimeAnimatorController?.name} avatar={anim.avatar?.name} human={anim.isHuman}");
        var go = new GameObject("SnapCam");
        var c = go.AddComponent<Camera>();
        c.CopyFrom(Camera.main);
        c.transform.position = player.transform.position + new Vector3(3.2f, 1.0f, 0.4f);
        c.transform.LookAt(player.transform.position + Vector3.up * 0.9f);
        anim.Rebind();
        anim.Update(0f);
        Directory.CreateDirectory("Logs");
        Shoot(c, "Logs/walk_0_idle.png");
        anim.SetFloat("Speed", 2f);
        anim.SetFloat("AnimSpeed", 1f);
        anim.Update(0.25f);  // finish the idle -> walk blend
        for (int i = 1; i <= 4; i++)
        {
            anim.Update(0.35f);
            Shoot(c, $"Logs/walk_{i}.png");
        }
        Object.DestroyImmediate(go);
        Debug.Log("SceneSnapshot: walk frames saved");
    }

    /// <summary>Renders the character in each animation state (side view) to Logs/anim_*.png.</summary>
    public static void CaptureAnimations()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/DemoLevel.unity");
        var player = GameObject.Find("Player");
        var anim = player.GetComponentInChildren<Animator>();
        Debug.Log($"SceneSnapshot: avatar human={anim.isHuman} controller={anim.runtimeAnimatorController?.name}");
        var go = new GameObject("SnapCam");
        var c = go.AddComponent<Camera>();
        c.CopyFrom(Camera.main);
        Vector3 p = player.transform.position;
        Directory.CreateDirectory("Logs");
        var shots = new (string state, float time, float speed, string file)[]
        {
            ("Locomotion", 0.3f, 0f, "idle"), ("Locomotion", 0.3f, 1.03f, "walk"), ("Locomotion", 0.4f, 3.4f, "run"),
            ("Punch1", 0.32f, 0f, "punch1"), ("Punch2", 0.25f, 0f, "punch2"), ("Kick", 0.6f, 0f, "kick"),
            ("Climb", 0.15f, 0f, "climb_a"), ("Climb", 0.5f, 0f, "climb_b"), ("Climb", 0.85f, 0f, "climb_c"),
        };
        foreach (var s in shots)
        {
            anim.Rebind();
            anim.SetFloat("Speed", s.speed);
            anim.Play(s.state, 0, s.time);
            anim.Update(0f);
            anim.Update(0.001f);
            foreach (var (name, offset) in new[] { ("side", new Vector3(3.4f, 1.0f, 0f)), ("front", new Vector3(0f, 1.0f, 3.4f)) })
            {
                c.transform.position = p + offset;
                c.transform.LookAt(p + Vector3.up * 0.95f);
                Shoot(c, $"Logs/anim_{s.file}_{name}.png");
            }
        }
        Object.DestroyImmediate(go);
        Debug.Log("SceneSnapshot: animation frames saved");
    }

    static void Shoot(Camera cam, string path)
    {
        var rt = new RenderTexture(1280, 720, 24);
        cam.targetTexture = rt;
        cam.Render();   // warm-up: the first render after loading has no ambient light yet
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        cam.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
    }
}
