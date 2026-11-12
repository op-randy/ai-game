using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Play starts whatever scene is open. If the editor starts on an empty, unsaved "Untitled" scene,
/// the third-person test level (or the shooting range) is opened so Play always has something to run.
/// Switch scenes with the AI Game menu: Build Demo Level (third person + animations) or Build Shooting Range (first person).
/// </summary>
[InitializeOnLoad]
static class OpenDemoLevelOnStart
{
    static readonly string[] Candidates = { "Assets/Scenes/DemoLevel.unity", "Assets/Scenes/ShootingRange.unity" };

    static OpenDemoLevelOnStart()
    {
        EditorSceneManager.playModeStartScene = null;
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var active = EditorSceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(active.path) || active.isDirty) return;
            foreach (var c in Candidates)
                if (File.Exists(c)) { EditorSceneManager.OpenScene(c); break; }
        };
    }
}
