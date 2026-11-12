using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds a small test level with the CatGirl as a walkable third-person player.
/// Menu: AI Game > Build Demo Level
/// Command line: Unity -batchmode -projectPath . -executeMethod DemoLevelBuilder.Build -quit
/// </summary>
public static class DemoLevelBuilder
{
    const string ModelPath = "Assets/Characters/CatGirl/CatGirl.fbx";
    const string LevelDir = "Assets/Level";
    const string ScenePath = "Assets/Scenes/DemoLevel.unity";
    const float PlayerHeight = 1.75f;

    [MenuItem("AI Game/Build Demo Level")]
    public static void Build()
    {
        Directory.CreateDirectory(LevelDir + "/Materials");
        Directory.CreateDirectory(LevelDir + "/Textures");
        Directory.CreateDirectory("Assets/Scenes");
        ConfigureModelImport();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // ---------- materials ----------
        var grid = MakeGridTexture();
        var groundMat = MakeMaterial("Ground", new Color(0.55f, 0.58f, 0.62f), grid, new Vector2(30, 30));
        var wallMat = MakeMaterial("Wall", new Color(0.35f, 0.38f, 0.45f));
        var accentMat = MakeMaterial("Accent", new Color(0.85f, 0.65f, 0.25f));
        var crateMat = MakeMaterial("Crate", new Color(0.55f, 0.35f, 0.2f));
        var platformMat = MakeMaterial("Platform", new Color(0.3f, 0.55f, 0.75f));

        var level = new GameObject("Level").transform;

        // ---------- ground + boundary walls ----------
        Box("Ground", level, new Vector3(0, -0.5f, 0), new Vector3(60, 1, 60), groundMat);
        Box("Wall_N", level, new Vector3(0, 1.5f, 30.5f), new Vector3(62, 3, 1), wallMat);
        Box("Wall_S", level, new Vector3(0, 1.5f, -30.5f), new Vector3(62, 3, 1), wallMat);
        Box("Wall_E", level, new Vector3(30.5f, 1.5f, 0), new Vector3(1, 3, 60), wallMat);
        Box("Wall_W", level, new Vector3(-30.5f, 1.5f, 0), new Vector3(1, 3, 60), wallMat);

        // ---------- ramp up to a platform (east) ----------
        Box("RampPlatform", level, new Vector3(15, 1.25f, 10), new Vector3(8, 2.5f, 8), platformMat);
        float angle = Mathf.Asin(2.5f / 10f) * Mathf.Rad2Deg;
        var ramp = Box("Ramp", level, new Vector3(15, 1.05f, 6f - 5f * Mathf.Cos(angle * Mathf.Deg2Rad)), new Vector3(4, 0.4f, 10), accentMat);
        ramp.transform.rotation = Quaternion.Euler(-angle, 0, 0);

        // ---------- stairs up to a platform (west) ----------
        for (int i = 0; i < 8; i++)
        {
            float h = (i + 1) * 0.25f;
            Box($"Step_{i}", level, new Vector3(-15, h / 2f, -5f + i * 0.5f), new Vector3(3, h, 0.5f), i % 2 == 0 ? accentMat : wallMat);
        }
        Box("StairPlatform", level, new Vector3(-15, 1f, 1.75f), new Vector3(6, 2, 6), platformMat);

        // ---------- jumping platforms (center) ----------
        Box("Jump_1", level, new Vector3(0, 0.6f, 6), new Vector3(2.5f, 0.3f, 2.5f), platformMat);
        Box("Jump_2", level, new Vector3(0, 1.4f, 9.5f), new Vector3(2.5f, 0.3f, 2.5f), platformMat);
        Box("Jump_3", level, new Vector3(0, 2.2f, 13f), new Vector3(2.5f, 0.3f, 2.5f), platformMat);
        Box("Jump_Top", level, new Vector3(0, 3.0f, 17f), new Vector3(5f, 0.3f, 4f), accentMat);

        // ---------- climbing course (near the start): 0.8 / 1.3 / 1.8 / 2.3 m are climbable, 3.2 m is too high ----------
        float[] heights = { 0.8f, 1.3f, 1.8f, 2.3f };
        for (int i = 0; i < heights.Length; i++)
            Box($"Climb_{heights[i]:0.0}m", level, new Vector3(7f + i * 3.2f, heights[i] / 2f, -16f), new Vector3(2.4f, heights[i], 2.4f), i % 2 == 0 ? platformMat : accentMat);
        Box("Climb_TooHigh_3.2m", level, new Vector3(-8f, 1.6f, -15f), new Vector3(4f, 3.2f, 1.2f), wallMat);

        // ---------- pillars and crates ----------
        foreach (var p in new[] { new Vector3(-8, 0, 12), new Vector3(8, 0, 20), new Vector3(-20, 0, 18), new Vector3(20, 0, -15), new Vector3(-22, 0, -20) })
        {
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "Pillar"; pillar.transform.SetParent(level);
            pillar.transform.position = p + Vector3.up * 2f;
            pillar.transform.localScale = new Vector3(1.2f, 2f, 1.2f);
            pillar.GetComponent<Renderer>().sharedMaterial = wallMat;
        }
        var rng = new System.Random(7);
        for (int i = 0; i < 14; i++)
        {
            float s = 0.8f + (float)rng.NextDouble() * 0.8f;
            var pos = new Vector3(-25 + (float)rng.NextDouble() * 50, s / 2f, -25 + (float)rng.NextDouble() * 20);
            if (Mathf.Abs(pos.x) < 4 && pos.z < -14) continue; // keep the spawn area clear
            if ((pos.x > 4f || pos.x < -4f) && pos.z < -12f && pos.z > -20f) continue; // keep the climbing course clear
            var c = Box("Crate", level, pos, Vector3.one * s, crateMat);
            c.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 90, 0);
        }

        // ---------- light ----------
        var sun = GameObject.Find("Directional Light");
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(50, -35, 0);
            var l = sun.GetComponent<Light>(); l.intensity = 1.2f; l.shadows = LightShadows.Soft;
        }

        // ---------- player ----------
        var player = new GameObject("Player");
        player.transform.position = new Vector3(0, 0.05f, -20);
        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f; cc.radius = 0.35f; cc.center = new Vector3(0, 0.9f, 0); cc.stepOffset = 0.3f;
        player.layer = 2;   // Ignore Raycast: climbing probes and the camera ignore the player
        var tpc = player.AddComponent<ThirdPersonController>();
        player.AddComponent<MeleeCombo>();
        player.AddComponent<ControlsHelp>();

        var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (modelAsset != null)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            model.name = "CatGirl";
            model.transform.SetParent(player.transform, false);
            FitModel(model.transform);
            CharacterAnimationSetup.AttachAnimator(model);
            model.AddComponent<SpringBones>();       // cape, hair and jiggle physics
            tpc.model = model.transform;
        }
        else
        {
            Debug.LogWarning($"DemoLevelBuilder: {ModelPath} not found, using a capsule as the player.");
            var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.DestroyImmediate(cap.GetComponent<Collider>());
            cap.transform.SetParent(player.transform, false);
            cap.transform.localPosition = new Vector3(0, 0.9f, 0);
            tpc.model = cap.transform;
        }

        // ---------- camera ----------
        var camGO = Camera.main != null ? Camera.main.gameObject : new GameObject("Main Camera", typeof(Camera));
        camGO.tag = "MainCamera";
        var tpcam = camGO.GetComponent<ThirdPersonCamera>() ?? camGO.AddComponent<ThirdPersonCamera>();
        tpcam.target = player.transform;
        camGO.transform.position = player.transform.position + new Vector3(0, 2.5f, -4.5f);
        camGO.transform.LookAt(player.transform.position + Vector3.up * 1.5f);
        tpc.cameraTransform = camGO.transform;
        GraphicsSetup.SetupCamera(camGO.GetComponent<Camera>());
        GraphicsSetup.AddVolume();
        ShootingRangeBuilder.SetupSky();      // same HDRI sky and lighting as the range

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("DemoLevelBuilder: built " + ScenePath);
    }

    static void ConfigureModelImport()
    {
        var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null) return;

        // Unpack the textures embedded in the FBX so the materials can use them
        string texDir = Path.GetDirectoryName(ModelPath) + "/Textures";
        if (!Directory.Exists(texDir) || Directory.GetFiles(texDir).Length == 0)
        {
            Directory.CreateDirectory(texDir);
            importer.ExtractTextures(texDir);
            AssetDatabase.Refresh();
        }
        // Normal maps must be flagged as such; keep the 4K textures at full resolution
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { texDir }))
        {
            var ti = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;
            if (ti == null) continue;
            bool changed = false;
            if (ti.assetPath.ToLowerInvariant().Contains("normal") && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; changed = true; }
            if (ti.maxTextureSize < 4096) { ti.maxTextureSize = 4096; changed = true; }
            if (ti.anisoLevel < 8) { ti.anisoLevel = 8; changed = true; }
            if (changed) ti.SaveAndReimport();
        }

        importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        importer.materialSearch = ModelImporterMaterialSearch.Local;
        importer.SaveAndReimport();

        // Humanoid rig + animation clips + Animator Controller
        CharacterAnimationSetup.Setup();
    }

    // Scale the model to PlayerHeight and stand it on the player's origin.
    static void FitModel(Transform model)
    {
        var rs = model.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        float s = PlayerHeight / Mathf.Max(b.size.y, 0.01f);
        model.localScale *= s;
        b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        Vector3 root = model.parent.position;
        model.position += new Vector3(root.x - b.center.x, root.y - b.min.y, root.z - b.center.z);
    }

    static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        go.isStatic = true;
        return go;
    }

    static Material MakeMaterial(string name, Color color, Texture2D tex = null, Vector2? tiling = null)
    {
        string path = $"{LevelDir}/Materials/{name}.mat";
        bool srp = GraphicsSettings.currentRenderPipeline != null;
        var shader = Shader.Find(srp ? "Universal Render Pipeline/Lit" : "Standard") ?? Shader.Find("Standard");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
        mat.shader = shader;
        mat.color = color;
        if (tex != null) { mat.mainTexture = tex; mat.mainTextureScale = tiling ?? Vector2.one; }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // 1 m grid lines on a light tile, so you can judge distances while walking.
    static Texture2D MakeGridTexture()
    {
        string path = $"{LevelDir}/Textures/Grid.png";
        if (!File.Exists(path))
        {
            const int n = 256;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    bool line = x < 3 || y < 3 || x >= n - 3 || y >= n - 3;
                    bool mid = (x / (n / 2) + y / (n / 2)) % 2 == 0;
                    float v = line ? 0.35f : (mid ? 0.92f : 0.85f);
                    t.SetPixel(x, y, new Color(v, v, v, 1));
                }
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.wrapMode = TextureWrapMode.Repeat; ti.anisoLevel = 8; ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
