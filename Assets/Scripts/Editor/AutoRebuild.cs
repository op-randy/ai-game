using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Rebuilds the generated scenes (DemoLevel + ShootingRange, animations included) automatically
/// whenever the builder code is newer than the last build. Bump ContentVersion when the builders change.
/// The scenes are generated: changes made to them by hand are replaced on a rebuild.
/// </summary>
[InitializeOnLoad]
static class AutoRebuild
{
    const int ContentVersion = 21;
    const string StampFile = "ProjectSettings/AIGameContentVersion.txt";

    static AutoRebuild()
    {
        if (Application.isBatchMode) return;
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        EditorApplication.update -= Tick;

        int built = 0;
        if (File.Exists(StampFile)) int.TryParse(File.ReadAllText(StampFile).Trim(), out built);
        if (built >= ContentVersion) return;

        Debug.Log($"AI Game: rebuilding scenes (content version {built} -> {ContentVersion})...");
        rebuilding = true;
        EditorApplication.playModeStateChanged += BlockPlay;
        try
        {
            EditorUtility.DisplayProgressBar("AI Game", "Rebuilding scenes, animations and graphics (please wait, don't press Play)...", 0.3f);
            GraphicsSetup.Apply();          // URP, post-processing, quality settings
            DemoLevelBuilder.Build();       // also sets up the character animations
            ShootingRangeBuilder.Build();
            File.WriteAllText(StampFile, ContentVersion.ToString());
            Debug.Log("AI Game: scenes rebuilt. Open Assets/Scenes/ShootingRange or DemoLevel and press Play.");
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            rebuilding = false;
            EditorApplication.playModeStateChanged -= BlockPlay;
        }
    }

    static bool rebuilding;
    static void BlockPlay(PlayModeStateChange s)
    {
        if (rebuilding && s == PlayModeStateChange.ExitingEditMode)
        {
            EditorApplication.isPlaying = false;
            Debug.LogWarning("AI Game: still rebuilding the scenes, press Play again when it's done.");
        }
    }

    [MenuItem("AI Game/Rebuild Everything")]
    static void RebuildNow()
    {
        if (File.Exists(StampFile)) File.Delete(StampFile);
        EditorApplication.update += Tick;
    }
}
