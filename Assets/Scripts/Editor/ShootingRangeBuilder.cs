using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds the first-person shooting range: a desk with the assault rifle, three lanes with
/// static, knock-down and moving targets at 10–50 m.
/// Menu: AI Game > Build Shooting Range
/// Command line: Unity -batchmode -projectPath . -executeMethod ShootingRangeBuilder.Build -quit
/// </summary>
public static class ShootingRangeBuilder
{
    const string ScenePath = "Assets/Scenes/ShootingRange.unity";
    const string RiflePath = "Assets/Weapons/AssaultRifle/AssaultRifle.fbx";
    const string Dir = "Assets/Range";
    const int ViewModelLayer = 8;

    [MenuItem("AI Game/Build Shooting Range")]
    public static void Build()
    {
        Directory.CreateDirectory(Dir + "/Materials");
        Directory.CreateDirectory(Dir + "/Textures");
        Directory.CreateDirectory("Assets/Scenes");
        EnsureLayer(ViewModelLayer, "ViewModel");
        ConfigureRifleImport();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var defaultCam = GameObject.Find("Main Camera");
        if (defaultCam) Object.DestroyImmediate(defaultCam);

        // ---------- materials ----------
        PrepareTextures();
        var floorMat = Pbr("Floor", "concrete_floor_02", Color.white, 0.25f, 0f, 3f);
        var wallMat = Pbr("Wall", "concrete_wall_006", new Color(0.9f, 0.9f, 0.92f), 0.2f, 0f, 4f);
        var bermMat = Pbr("Backstop", "concrete_wall_004", new Color(0.85f, 0.8f, 0.75f), 0.15f, 0f, 4f);
        var dividerMat = Pbr("Divider", "metal_plate_02", new Color(1f, 0.72f, 0.18f), 0.45f, 0.25f, 1.2f);
        var woodMat = Pbr("Wood", "wood_table_worn", Color.white, 0.35f, 0f, 1.5f);
        var metalMat = Pbr("Metal", "metal_plate_02", new Color(0.75f, 0.75f, 0.78f), 0.55f, 0.6f, 1f);
        var faceMat = Mat("TargetFace", Color.white, TargetTexture(), Vector2.one);

        var range = new GameObject("Range").transform;

        // ---------- hall: floor, side walls, backstop, firing line ----------
        Box("Floor", range, new Vector3(0, -0.5f, 30), new Vector3(24, 1, 82), floorMat);
        Box("Wall_Left", range, new Vector3(-12.5f, 2.5f, 30), new Vector3(1, 5, 82), wallMat);
        Box("Wall_Right", range, new Vector3(12.5f, 2.5f, 30), new Vector3(1, 5, 82), wallMat);
        Box("Wall_Back", range, new Vector3(0, 2.5f, -11.5f), new Vector3(26, 5, 1), wallMat);
        Box("Backstop", range, new Vector3(0, 4f, 66), new Vector3(26, 8, 4), bermMat);
        Box("FiringLine_Counter", range, new Vector3(-7, 0.55f, 1.2f), new Vector3(8, 1.1f, 0.5f), woodMat);
        Box("FiringLine_Counter", range, new Vector3(7, 0.55f, 1.2f), new Vector3(8, 1.1f, 0.5f), woodMat);
        foreach (float x in new[] { -9f, -3f, 3f, 9f })
            Box("LaneDivider", range, new Vector3(x, 0.6f, 28), new Vector3(0.2f, 1.2f, 52), dividerMat);

        // distance labels on the floor
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        foreach (int d in new[] { 10, 20, 30, 40, 50 })
            Label(range, $"{d} m", new Vector3(-10.5f, 0.02f, d), font);

        // ---------- desk with the rifle ----------
        var desk = new GameObject("Desk").transform;
        desk.SetParent(range);
        desk.position = new Vector3(0, 0, -3.2f);
        Box("Top", desk, desk.position + new Vector3(0, 0.92f, 0), new Vector3(1.8f, 0.06f, 0.8f), woodMat);
        foreach (var o in new[] { new Vector3(-0.8f, 0.45f, -0.33f), new Vector3(0.8f, 0.45f, -0.33f), new Vector3(-0.8f, 0.45f, 0.33f), new Vector3(0.8f, 0.45f, 0.33f) })
            Box("Leg", desk, desk.position + o, new Vector3(0.07f, 0.9f, 0.07f), metalMat);
        var riflePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RiflePath);
        if (riflePrefab != null)
        {
            var r = (GameObject)PrefabUtility.InstantiatePrefab(riflePrefab);
            r.name = "AssaultRifle (pickup)";
            r.transform.SetParent(desk);
            // lying on its side, barrel pointing along the desk
            r.transform.rotation = Quaternion.LookRotation(Vector3.right, Vector3.back);
            var b = Bounds(r);
            r.transform.position += new Vector3(desk.position.x - b.center.x, desk.position.y + 0.95f - b.min.y, desk.position.z - b.center.z);
            var mr = r.GetComponentInChildren<MeshRenderer>();
            var col = mr.gameObject.AddComponent<BoxCollider>();
            col.size = Vector3.Scale(col.size, new Vector3(1.4f, 1.4f, 1.05f));
            r.AddComponent<WeaponPickup>();
        }
        else Debug.LogError("ShootingRangeBuilder: rifle not found at " + RiflePath);

        // ---------- targets ----------
        var targets = new GameObject("Targets").transform;
        targets.SetParent(range);
        // centre lane: static targets at increasing distance
        Target(targets, new Vector3(0, 0, 10), faceMat, metalMat, false);
        Target(targets, new Vector3(-1.2f, 0, 20), faceMat, metalMat, false);
        Target(targets, new Vector3(1.2f, 0, 35), faceMat, metalMat, false);
        Target(targets, new Vector3(0, 0, 50), faceMat, metalMat, false);
        // left lane: knock-down targets
        Target(targets, new Vector3(-6.8f, 0, 12), faceMat, metalMat, true);
        Target(targets, new Vector3(-5.2f, 0, 18), faceMat, metalMat, true);
        Target(targets, new Vector3(-6.5f, 0, 26), faceMat, metalMat, true);
        Target(targets, new Vector3(-5.5f, 0, 38), faceMat, metalMat, true);
        // right lane: moving targets on rails
        Mover(targets, new Vector3(6, 0, 15), faceMat, metalMat, 4.5f, 2.4f);
        Mover(targets, new Vector3(6, 0, 30), faceMat, metalMat, 4.5f, 3.2f);
        Target(targets, new Vector3(6, 0, 45), faceMat, metalMat, true);

        // ---------- light ----------
        var sun = GameObject.Find("Directional Light");
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(38, -35, 0);
            var l = sun.GetComponent<Light>(); l.intensity = 1.6f; l.shadows = LightShadows.Soft;
            l.color = new Color(1f, 0.93f, 0.82f);
        }

        SetupSky();

        // ---------- player ----------
        var player = new GameObject("Player");
        player.layer = 2;   // Ignore Raycast: bullets and the pick-up ray pass through the player
        player.transform.position = new Vector3(0, 0.05f, -6f);
        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f; cc.radius = 0.35f; cc.center = new Vector3(0, 0.9f, 0); cc.stepOffset = 0.35f;
        var fpc = player.AddComponent<FirstPersonController>();

        var pivot = new GameObject("CameraPivot").transform;
        pivot.SetParent(player.transform, false);
        pivot.localPosition = new Vector3(0, 1.62f, 0);
        fpc.cameraPivot = pivot;

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.SetParent(pivot, false);
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 70f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 500f;
        cam.cullingMask = ~(1 << ViewModelLayer);
        camGo.AddComponent<AudioListener>();

        var weaponRoot = new GameObject("WeaponRoot").transform;
        weaponRoot.SetParent(camGo.transform, false);

        var vmGo = new GameObject("ViewModelCamera");
        vmGo.transform.SetParent(camGo.transform, false);
        var vmCam = vmGo.AddComponent<Camera>();
        vmCam.clearFlags = CameraClearFlags.Depth;
        vmCam.cullingMask = 1 << ViewModelLayer;
        vmCam.depth = cam.depth + 1;
        vmCam.fieldOfView = 50f; vmCam.nearClipPlane = 0.01f; vmCam.farClipPlane = 10f;

        GraphicsSetup.SetupCamera(cam);
        GraphicsSetup.SetupOverlay(cam, vmCam);
        GraphicsSetup.AddVolume();

        var rifle = player.AddComponent<RifleController>();
        rifle.player = fpc; rifle.mainCamera = cam; rifle.viewModelCamera = vmCam;
        rifle.weaponRoot = weaponRoot; rifle.riflePrefab = riflePrefab;
        rifle.shotClips = new[] { Clip("Gunshot_1"), Clip("Gunshot_2"), Clip("Gunshot_3") };
        rifle.targetHitClip = Clip("TargetHit");
        rifle.impactClip = Clip("Impact");
        rifle.pickupClip = Clip("Pickup");
        // first-person arms: her own arms, hands locked on the grips with IK
        rifle.armsCharacterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAnimationSetup.ModelPath);
        rifle.armsController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CharacterAnimationSetup.ControllerPath);
        rifle.armsAvatar = System.Linq.Enumerable.FirstOrDefault(System.Linq.Enumerable.OfType<Avatar>(AssetDatabase.LoadAllAssetsAtPath(CharacterAnimationSetup.ModelPath)));
        if (rifle.armsController == null) Debug.LogWarning("ShootingRangeBuilder: no animator controller yet – run AI Game > Setup Character Animations for first-person arms");

        var hud = player.AddComponent<RangeHUD>();
        hud.rifle = rifle; hud.mainCamera = cam;

        EditorSceneManager.SaveScene(scene, ScenePath);
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
        if (File.Exists("Assets/Scenes/DemoLevel.unity")) scenes.Add(new EditorBuildSettingsScene("Assets/Scenes/DemoLevel.unity", true));
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("ShootingRangeBuilder: built " + ScenePath);
    }

    // ---------------- snapshots (editor check without playing) ----------------
    public static void Snapshots()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var player = GameObject.Find("Player");
        var rifle = player.GetComponent<RifleController>();
        var cam = rifle.mainCamera; var vm = rifle.viewModelCamera;
        Directory.CreateDirectory("Logs");

        // desk view before pick-up
        var pivot = rifle.player.cameraPivot;
        pivot.localRotation = Quaternion.Euler(28, 0, 0);
        Composite(cam, vm, "Logs/range_desk.png");
        pivot.localRotation = Quaternion.identity;

        // equip a copy of the rifle in the view model slot (edit-mode stand-in for Equip())
        var gun = (GameObject)Object.Instantiate(rifle.riflePrefab, rifle.weaponRoot);
        foreach (var t in gun.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = ViewModelLayer;
        foreach (var t in gun.GetComponentsInChildren<Transform>(true))
            if (t.name == "RedDot")
            {
                var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(dot.GetComponent<Collider>());
                dot.layer = ViewModelLayer; dot.transform.SetParent(t, false); dot.transform.localScale = Vector3.one * 0.0024f;
                dot.GetComponent<Renderer>().sharedMaterial = FxMaterials.Unlit(Color.red * 3f);
            }
        gun.transform.localScale = Vector3.one * rifle.gunScale;
        FirstPersonArms arms = null;
        if (rifle.armsCharacterPrefab != null)
        {
            arms = FirstPersonArms.Spawn(rifle.armsCharacterPrefab, rifle.armsController, rifle.armsAvatar, cam.transform, ViewModelLayer, rifle.armsCharacterHeight, rifle.shoulderPosition);
            foreach (var t in gun.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "GripRight") arms.gripRight = t;
                if (t.name == "GripLeft") arms.gripLeft = t;
            }
        }
        foreach (var (aim, file) in new[] { (0f, "Logs/range_hipfire.png"), (1f, "Logs/range_aim.png") })
        {
            float s = aim * aim * (3 - 2 * aim);
            gun.transform.localPosition = Vector3.Lerp(rifle.hipPosition, rifle.aimPosition, s);
            gun.transform.localRotation = Quaternion.Euler(Vector3.Lerp(rifle.hipRotation, Vector3.zero, s));
            cam.fieldOfView = Mathf.Lerp(rifle.hipFov, rifle.aimFov, s);
            vm.fieldOfView = Mathf.Lerp(rifle.viewModelHipFov, rifle.viewModelAimFov, s);
            if (arms)
            {
                var an = arms.GetComponent<Animator>();
                an.Rebind();
                for (int i = 0; i < 6; i++) an.Update(0.02f);   // runs the IK pass (FirstPersonArms is ExecuteAlways)
                foreach (var (hand, lower, upper, grip) in new[] {
                    (HumanBodyBones.RightHand, HumanBodyBones.RightLowerArm, HumanBodyBones.RightUpperArm, arms.gripRight),
                    (HumanBodyBones.LeftHand, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftUpperArm, arms.gripLeft) })
                {
                    var h = an.GetBoneTransform(hand); var lo = an.GetBoneTransform(lower); var up = an.GetBoneTransform(upper);
                    float armLen = Vector3.Distance(up.position, lo.position) + Vector3.Distance(lo.position, h.position);
                    Vector3 ct = cam.transform.InverseTransformPoint(grip.position), ch = cam.transform.InverseTransformPoint(h.position), cs = cam.transform.InverseTransformPoint(up.position);
                    Debug.Log($"ARMDEBUG aim={aim} {hand}: shoulder {cs:F3} hand {ch:F3} grip {ct:F3} armLength {armLen:F3} shoulder->grip {Vector3.Distance(up.position, grip.position):F3} hand->grip {Vector3.Distance(h.position, grip.position):F3}");
                }
            }
            Composite(cam, vm, file);
            // a wider look at the arms from the side, for checking the grip
            if (arms)
            {
                var side = new GameObject("SideCam").AddComponent<Camera>();
                side.cullingMask = 1 << ViewModelLayer; side.clearFlags = CameraClearFlags.SolidColor; side.backgroundColor = new Color(0.3f, 0.32f, 0.36f);
                side.nearClipPlane = 0.01f; side.fieldOfView = 40f;
                Vector3 c = cam.transform.TransformPoint(new Vector3(0.05f, -0.25f, 0.35f));
                side.transform.position = c + cam.transform.right * 1.1f + Vector3.up * 0.15f;
                side.transform.LookAt(c);
                Composite(side, null, file.Replace(".png", "_side.png"));
                Object.DestroyImmediate(side.gameObject);
            }
        }
        if (arms) Object.DestroyImmediate(arms.gameObject);
        Object.DestroyImmediate(gun);

        var over = new GameObject("Overview").AddComponent<Camera>();
        over.transform.position = new Vector3(0, 14, -10); over.transform.LookAt(new Vector3(0, 0, 25));
        over.fieldOfView = 60;
        Composite(over, null, "Logs/range_overview.png");
        Object.DestroyImmediate(over.gameObject);
        Debug.Log("ShootingRangeBuilder: snapshots saved");
    }

    /// <summary>Renders several hip-fire poses so the framing can be compared (Logs/hip_variant_*.png).</summary>
    public static void HipVariants()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var rifle = GameObject.Find("Player").GetComponent<RifleController>();
        var cam = rifle.mainCamera; var vm = rifle.viewModelCamera;
        rifle.player.cameraPivot.localRotation = Quaternion.Euler(4, 0, 0);
        var variants = new (Vector3 pos, Vector3 rot, float fov)[]
        {
            (new Vector3(0.13f, -0.115f, 0.32f), new Vector3(0, -3, -3), 50),
            (new Vector3(0.15f, -0.13f, 0.36f), new Vector3(0, -5, -4), 50),
            (new Vector3(0.11f, -0.10f, 0.30f), new Vector3(0, -2, -2), 55),
            (new Vector3(0.16f, -0.12f, 0.40f), new Vector3(2, -6, -6), 50),
        };
        for (int v = 0; v < variants.Length; v++)
        {
            var gun = (GameObject)Object.Instantiate(rifle.riflePrefab, rifle.weaponRoot);
            foreach (var t in gun.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = ViewModelLayer;
            gun.transform.localScale = Vector3.one * rifle.gunScale;
            gun.transform.localPosition = variants[v].pos;
            gun.transform.localRotation = Quaternion.Euler(variants[v].rot);
            var arms = FirstPersonArms.Spawn(rifle.armsCharacterPrefab, rifle.armsController, rifle.armsAvatar, cam.transform, ViewModelLayer, rifle.armsCharacterHeight, rifle.shoulderPosition);
            foreach (var t in gun.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "GripRight") arms.gripRight = t;
                if (t.name == "GripLeft") arms.gripLeft = t;
            }
            var an = arms.GetComponent<Animator>();
            for (int i = 0; i < 6; i++) an.Update(0.02f);
            float reach = Vector3.Distance(an.GetBoneTransform(HumanBodyBones.LeftHand).position, arms.gripLeft.position);
            Debug.Log($"VARIANT {v}: pos {variants[v].pos} rot {variants[v].rot} vmFov {variants[v].fov} leftHand->grip {reach:F3}");
            cam.fieldOfView = rifle.hipFov; vm.fieldOfView = variants[v].fov;
            Composite(cam, vm, $"Logs/hip_variant_{v}.png");
            Object.DestroyImmediate(arms.gameObject);
            Object.DestroyImmediate(gun);
        }
    }

    static void Composite(Camera main, Camera overlay, string path)
    {
        var rt = new RenderTexture(1280, 720, 24);
        main.targetTexture = rt; main.Render(); main.Render(); main.targetTexture = null;   // second render: ambient light is ready
        // URP renders the overlay (weapon) camera as part of the main camera's stack
        if (overlay && UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null) { overlay.targetTexture = rt; overlay.Render(); overlay.targetTexture = null; }
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        RenderTexture.active = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
    }

    // ---------------- helpers ----------------
    static void Target(Transform parent, Vector3 pos, Material face, Material metal, bool knockDown)
    {
        var root = new GameObject(knockDown ? "Target (knock-down)" : "Target").transform;
        root.SetParent(parent, false);
        root.localPosition = pos;
        BuildTargetInto(root, face, metal, knockDown);
    }

    static void Mover(Transform parent, Vector3 pos, Material face, Material metal, float distance, float speed)
    {
        var rail = new GameObject("Moving target rail").transform;
        rail.SetParent(parent, false);
        rail.localPosition = pos;
        Box("Rail", rail, rail.position + new Vector3(0, 0.03f, 0), new Vector3(distance + 1f, 0.06f, 0.15f), metal);
        var carriage = new GameObject("Carriage").transform;
        carriage.SetParent(rail, false);
        var mv = carriage.gameObject.AddComponent<TargetMover>();
        mv.distance = distance; mv.speed = speed;
        BuildTargetInto(carriage, face, metal, false);
    }

    static void BuildTargetInto(Transform root, Material face, Material metal, bool knockDown)
    {
        Box("Base", root, root.position + new Vector3(0, 0.05f, 0), new Vector3(0.7f, 0.1f, 0.3f), metal).isStatic = false;
        var hinge = new GameObject("Hinge").transform;
        hinge.SetParent(root, false);
        hinge.localPosition = new Vector3(0, 0.1f, 0);
        var post = Box("Post", hinge, hinge.position + new Vector3(0, 0.5f, 0.03f), new Vector3(0.06f, 1.0f, 0.06f), metal);
        var board = Box("Board", hinge, hinge.position + new Vector3(0, 1.4f, 0), new Vector3(0.6f, 0.9f, 0.04f), face, false);
        post.isStatic = false; board.isStatic = false;
        var t = root.gameObject.AddComponent<ShootingTarget>();
        t.hinge = hinge; t.face = board.GetComponent<Renderer>(); t.knockDown = knockDown;
    }

    static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, bool worldUV = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, true);
        go.transform.position = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (worldUV) go.GetComponent<MeshFilter>().sharedMesh = BoxMesh(size);
        go.isStatic = true;
        return go;
    }

    // Cube mesh whose UVs are in metres (box mapping), so textures keep their real size on any box.
    static readonly System.Collections.Generic.Dictionary<string, Mesh> boxMeshes = new System.Collections.Generic.Dictionary<string, Mesh>();
    static Mesh BoxMesh(Vector3 size)
    {
        string key = $"{size.x:0.###}x{size.y:0.###}x{size.z:0.###}";
        if (boxMeshes.TryGetValue(key, out var cached) && cached) return cached;
        string path = $"{Dir}/Meshes/Box_{key}.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            Directory.CreateDirectory(Dir + "/Meshes");
            var src = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            mesh = Object.Instantiate(src);
            var v = mesh.vertices; var n = mesh.normals; var uv = new Vector2[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 w = Vector3.Scale(v[i], size), a = new Vector3(Mathf.Abs(n[i].x), Mathf.Abs(n[i].y), Mathf.Abs(n[i].z));
                uv[i] = a.x > 0.5f ? new Vector2(w.z, w.y) : a.y > 0.5f ? new Vector2(w.x, w.z) : new Vector2(w.x, w.y);
            }
            mesh.uv = uv;
            mesh.RecalculateTangents();
            AssetDatabase.CreateAsset(mesh, path);
        }
        boxMeshes[key] = mesh;
        return mesh;
    }

    const string PH = "Assets/Range/PolyHaven";

    static void PrepareTextures()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { PH }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            var ti = AssetImporter.GetAtPath(p) as TextureImporter;
            if (ti == null) continue;
            bool changed = false;
            if (p.EndsWith("_nor.jpg") && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; changed = true; }
            if (p.EndsWith("_ao.jpg") && ti.sRGBTexture) { ti.sRGBTexture = false; changed = true; }
            if (ti.anisoLevel < 8) { ti.anisoLevel = 8; changed = true; }
            if (ti.maxTextureSize < 2048) { ti.maxTextureSize = 2048; changed = true; }
            if (changed) ti.SaveAndReimport();
        }
    }

    // Lit PBR material from a Poly Haven texture set; tile = size of one texture repeat in metres.
    static Material Pbr(string name, string set, Color tint, float smoothness, float metallic, float tile)
    {
        var mat = Mat(name, tint);
        var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{PH}/{set}/{set}_diff.jpg");
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{PH}/{set}/{set}_nor.jpg");
        var ao = AssetDatabase.LoadAssetAtPath<Texture2D>($"{PH}/{set}/{set}_ao.jpg");
        Vector2 scale = Vector2.one / tile;
        if (albedo) { mat.mainTexture = albedo; mat.mainTextureScale = scale; }
        foreach (var (prop, tex, kw) in new[] { ("_BumpMap", normal, "_NORMALMAP"), ("_OcclusionMap", ao, "_OCCLUSIONMAP") })
            if (tex && mat.HasProperty(prop)) { mat.SetTexture(prop, tex); mat.SetTextureScale(prop, scale); mat.EnableKeyword(kw); }
        mat.SetFloat("_Smoothness", smoothness); mat.SetFloat("_Glossiness", smoothness);
        mat.SetFloat("_Metallic", metallic);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    public static void SetupSky()
    {
        var hdr = AssetDatabase.LoadAssetAtPath<Texture>($"{PH}/HDRI/derelict_airfield_01_2k.hdr");
        var shader = Shader.Find("Skybox/Panoramic");
        if (hdr == null || shader == null) return;
        string path = $"{Dir}/Materials/Sky.mat";
        var sky = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (sky == null) { sky = new Material(shader); AssetDatabase.CreateAsset(sky, path); }
        sky.SetTexture("_MainTex", hdr);
        sky.SetFloat("_Exposure", 1.1f);
        sky.SetFloat("_Rotation", 40f);
        RenderSettings.skybox = sky;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 1.1f;
        RenderSettings.reflectionIntensity = 1f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.004f;
        RenderSettings.fogColor = new Color(0.62f, 0.66f, 0.7f);
        DynamicGI.UpdateEnvironment();
    }

    static void Label(Transform parent, string text, Vector3 pos, Font font)
    {
        var go = new GameObject("Label " + text);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(90, 0, 0);
        var tm = go.AddComponent<TextMesh>();
        tm.text = text; tm.font = font; tm.fontSize = 64; tm.characterSize = 0.06f;
        tm.anchor = TextAnchor.MiddleCenter; tm.color = new Color(1f, 0.85f, 0.3f);
        go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
    }

    static Bounds Bounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static Material Mat(string name, Color color, Texture2D tex = null, Vector2? tiling = null)
    {
        string path = $"{Dir}/Materials/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard");
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
        mat.shader = shader;
        mat.color = color;
        if (tex != null) { mat.mainTexture = tex; mat.mainTextureScale = tiling ?? Vector2.one; }
        mat.SetFloat("_Glossiness", 0.15f);
        mat.SetFloat("_Smoothness", 0.15f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/{name}.wav");

    // White board with red/black scoring rings
    static Texture2D TargetTexture()
    {
        string path = $"{Dir}/Textures/TargetFace.png";
        if (!File.Exists(path))
        {
            const int w = 256, h = 384;
            var t = new Texture2D(w, h, TextureFormat.RGB24, true);
            var c = new Vector2(w / 2f, h * 0.58f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c) / (w * 0.45f);
                    Color col = new Color(0.95f, 0.93f, 0.88f);
                    if (d < 1f)
                    {
                        int ring = Mathf.FloorToInt(d * 5f);
                        col = ring == 0 ? new Color(0.85f, 0.1f, 0.1f) : (ring % 2 == 1 ? new Color(0.12f, 0.12f, 0.12f) : new Color(0.95f, 0.93f, 0.88f));
                        if (Mathf.Abs(d * 5f - Mathf.Round(d * 5f)) < 0.04f) col = new Color(0.6f, 0.6f, 0.6f);
                    }
                    if (x < 4 || y < 4 || x >= w - 4 || y >= h - 4) col = new Color(0.2f, 0.2f, 0.2f);
                    t.SetPixel(x, y, col);
                }
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path);
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void ConfigureRifleImport()
    {
        var imp = AssetImporter.GetAtPath(RiflePath) as ModelImporter;
        if (imp == null) return;
        string texDir = Path.GetDirectoryName(RiflePath) + "/Textures";
        if (!Directory.Exists(texDir) || Directory.GetFiles(texDir).Length == 0)
        {
            Directory.CreateDirectory(texDir);
            imp.ExtractTextures(texDir);
            AssetDatabase.Refresh();
            imp = (ModelImporter)AssetImporter.GetAtPath(RiflePath);
        }
        imp.animationType = ModelImporterAnimationType.None;
        imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        imp.materialSearch = ModelImporterMaterialSearch.Local;
        imp.SaveAndReimport();
    }

    static void EnsureLayer(int index, string name)
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tagManager.FindProperty("layers");
        var p = layers.GetArrayElementAtIndex(index);
        if (p.stringValue != name)
        {
            if (!string.IsNullOrEmpty(p.stringValue)) Debug.LogWarning($"ShootingRangeBuilder: layer {index} was '{p.stringValue}', renaming to '{name}'");
            p.stringValue = name;
            tagManager.ApplyModifiedProperties();
        }
    }
}
