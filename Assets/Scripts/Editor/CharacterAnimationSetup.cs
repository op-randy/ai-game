using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Sets the CatGirl up for Humanoid animation and builds her Animator Controller from the clips in
/// Assets/Characters/CatGirl/Animations (CatGirl_Idle, _Walk, _Run, _Punch1, _Punch2, _Kick, _Climb).
/// Menu: AI Game > Setup Character Animations   (also run by Build Demo Level)
/// </summary>
public static class CharacterAnimationSetup
{
    public const string ModelPath = "Assets/Characters/CatGirl/CatGirl.fbx";
    public const string AnimDir = "Assets/Characters/CatGirl/Animations";
    public const string ControllerPath = "Assets/Characters/CatGirl/CatGirl.controller";

    // Ground speeds of the clips at the in-game character size (1.75 m), measured from the planted foot.
    public const float WalkClipSpeed = 1.03f;
    public const float RunClipSpeed = 2.85f;

    static readonly HashSet<string> Looping = new HashSet<string> { "Idle", "Walk", "Run", "WalkRifle" };

    [MenuItem("AI Game/Setup Character Animations")]
    public static void Setup()
    {
        // 1. The character: Humanoid avatar from her T-pose
        var mi = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        mi.animationType = ModelImporterAnimationType.Human;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.importAnimation = false;
        mi.SaveAndReimport();
        var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
        Debug.Log($"CharacterAnimationSetup: CatGirl avatar valid={avatar != null && avatar.isValid} human={avatar != null && avatar.isHuman}");

        // 2. Clips
        var clips = new Dictionary<string, AnimationClip>();
        foreach (var path in Directory.GetFiles(AnimDir, "*.fbx").Select(p => p.Replace('\\', '/')))
        {
            string clipName = Path.GetFileNameWithoutExtension(path).Replace("CatGirl_", "");
            var ai = (ModelImporter)AssetImporter.GetAtPath(path);
            ai.animationType = ModelImporterAnimationType.Human;
            ai.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            ai.materialImportMode = ModelImporterMaterialImportMode.None;
            ai.importAnimation = true;
            bool loop = Looping.Contains(clipName);
            ai.clipAnimations = ai.defaultClipAnimations.Select(c =>
            {
                c.name = clipName;
                c.loopTime = loop;
                c.loopPose = loop;
                c.lockRootRotation = true;              // body orientation stays with the character
                c.keepOriginalOrientation = false;
                c.lockRootPositionXZ = true;            // in place: the scripts move the character
                c.keepOriginalPositionXZ = false;
                // Climb: the upward motion goes to the root, and the climbing script moves her up the ledge itself
                c.lockRootHeightY = clipName != "Climb";
                c.keepOriginalPositionY = true;
                return c;
            }).ToArray();
            ai.SaveAndReimport();
            var srcAvatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            Debug.Log($"CharacterAnimationSetup: {clipName} human={srcAvatar != null && srcAvatar.isHuman} length={(clip ? clip.length.ToString("0.00") : "-")}s");
            if (clip != null) clips[clipName] = clip;
        }

        // Unarmed actions are authored from the existing humanoid clips, not the rifle pose.
        clips["Jump"] = BuildStanceClip(clips["Idle"], "Jump", false);
        clips["CrouchIdle"] = BuildStanceClip(clips["Idle"], "CrouchIdle", true);
        clips["CrouchWalk"] = BuildStanceClip(clips["Walk"], "CrouchWalk", true);

        // 3. Controller
        // update the controller in place: deleting it would give it a new ID and break the scenes that use it
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        else
        {
            foreach (var p in ctrl.parameters) ctrl.RemoveParameter(p);
            var oldSm = ctrl.layers[0].stateMachine;
            foreach (var st in oldSm.states) oldSm.RemoveState(st.state);
            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
                if (sub is BlendTree) AssetDatabase.RemoveObjectFromAsset(sub);
        }
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter(new AnimatorControllerParameter { name = "AnimSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
        var layers = ctrl.layers;
        layers[0].iKPass = true;            // needed for the first-person arms (hands on the weapon)
        ctrl.layers = layers;
        var sm = ctrl.layers[0].stateMachine;

        // Locomotion: Idle -> Walk -> Run by ground speed
        var loco = ctrl.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        if (clips.TryGetValue("Idle", out var idle)) tree.AddChild(idle, 0f);
        if (clips.TryGetValue("Walk", out var walk)) tree.AddChild(walk, WalkClipSpeed);
        if (clips.TryGetValue("Run", out var run)) tree.AddChild(run, RunClipSpeed);
        loco.speedParameterActive = true;
        loco.speedParameter = "AnimSpeed";
        sm.defaultState = loco;

        // One-shot actions, started from script with CrossFade
        AddState(sm, clips, "Punch1", 1.35f, new Vector3(320, 140));
        AddState(sm, clips, "Punch2", 1.35f, new Vector3(320, 210));
        AddState(sm, clips, "Kick", 1.2f, new Vector3(320, 280));
        AddState(sm, clips, "Climb", 1.6f, new Vector3(320, 350));
        AddState(sm, clips, "Jump", 1f, new Vector3(320, 420));

        var crouch = ctrl.CreateBlendTreeInController("Crouch", out BlendTree crouchTree, 0);
        crouchTree.blendType = BlendTreeType.Simple1D;
        crouchTree.blendParameter = "Speed";
        crouchTree.useAutomaticThresholds = false;
        crouchTree.AddChild(clips["CrouchIdle"], 0f);
        crouchTree.AddChild(clips["CrouchWalk"], 0.8f);
        crouch.iKOnFeet = true;
        crouch.speedParameterActive = true;
        crouch.speedParameter = "AnimSpeed";

        AssetDatabase.SaveAssets();
        Debug.Log($"CharacterAnimationSetup: controller built with {clips.Count} clip(s): {string.Join(", ", clips.Keys)}");
    }

    static AnimationClip BuildStanceClip(AnimationClip source, string name, bool crouch)
    {
        string path = $"{AnimDir}/CatGirl_{name}.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
        }
        clip.ClearCurves();
        clip.name = name;
        clip.frameRate = 30f;
        float duration = crouch ? source.length : 0.8f;
        int frames = Mathf.CeilToInt(duration * clip.frameRate);
        var offsets = new Dictionary<string, float>
        {
            { "Left Upper Leg Front-Back", crouch ? 0.42f : 0.28f },
            { "Right Upper Leg Front-Back", crouch ? 0.42f : 0.20f },
            { "Left Lower Leg Stretch", crouch ? -0.85f : -0.65f },
            { "Right Lower Leg Stretch", crouch ? -0.85f : -0.55f },
            { "Left Foot Up-Down", -0.12f },
            { "Right Foot Up-Down", -0.12f },
            { "Spine Front-Back", crouch ? 0.18f : 0.08f },
            { "Left Arm Down-Up", 0.08f },
            { "Right Arm Down-Up", 0.08f },
        };
        var bindings = AnimationUtility.GetCurveBindings(source).ToList();
        foreach (string muscle in offsets.Keys)
            if (!bindings.Any(b => b.type == typeof(Animator) && b.propertyName == muscle))
                bindings.Add(EditorCurveBinding.FloatCurve("", typeof(Animator), muscle));

        foreach (var binding in bindings)
        {
            var original = AnimationUtility.GetEditorCurve(source, binding);
            var keys = new Keyframe[frames + 1];
            for (int i = 0; i <= frames; i++)
            {
                float t = duration * i / frames;
                float amount = crouch ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.16f, t))
                    * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, duration, t)));
                float value = original != null ? original.Evaluate(crouch ? t : 0f) : 0f;
                if (binding.type == typeof(Animator))
                {
                    if (offsets.TryGetValue(binding.propertyName, out float offset))
                        value = Mathf.Clamp(value + offset * amount, -1f, 1f);
                    // Body height is normalized by humanoid scale; keep crouched feet near the floor.
                    if (crouch && binding.propertyName == "RootT.y") value -= 0.23f;
                }
                keys[i] = new Keyframe(t, value);
            }
            AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(keys));
        }
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = crouch;
        settings.loopBlend = crouch;
        settings.startTime = 0f;
        settings.stopTime = duration;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    static void AddState(AnimatorStateMachine sm, Dictionary<string, AnimationClip> clips, string name, float speed, Vector3 pos)
    {
        if (!clips.TryGetValue(name, out var clip)) { Debug.LogWarning($"CharacterAnimationSetup: no clip for {name}"); return; }
        var s = sm.AddState(name, pos);
        s.motion = clip;
        s.speed = speed;
    }

    /// <summary>Puts an Animator with the controller and avatar on the character instance.</summary>
    public static void AttachAnimator(GameObject model)
    {
        var anim = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
        anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        anim.avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var hair = model.GetComponent<PonytailHair>() ?? model.AddComponent<PonytailHair>();
        hair.hairMaterial = HairMaterial("WhitePonytail", new Color(0.88f, 0.90f, 0.94f), 0.28f);
        hair.tieMaterial = HairMaterial("PonytailBand", new Color(0.045f, 0.04f, 0.055f), 0.18f);
    }

    static Material HairMaterial(string name, Color color, float smoothness)
    {
        string path = $"Assets/Characters/CatGirl/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Smoothness", smoothness);
        if (name == "WhitePonytail")
        {
            const string texturePath = "Assets/Characters/CatGirl/PonytailStrands.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (!texture)
            {
                texture = new Texture2D(256, 256, TextureFormat.RGBA32, true) { name = "Fine white hair strands", wrapMode = TextureWrapMode.Repeat };
                var pixels = new Color[256 * 256];
                for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++)
                {
                    float u = x / 256f * Mathf.PI * 2f, v = y / 256f;
                    float fine = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(u * 43f + Mathf.Sin(v * 5f) * 0.4f), 10f);
                    float lockShade = 0.5f + 0.5f * Mathf.Cos(u * 9f + v * 0.6f);
                    float shade = 0.90f - fine * 0.18f - lockShade * 0.09f;
                    pixels[y * 256 + x] = new Color(shade, shade, Mathf.Min(1f, shade + 0.015f));
                }
                texture.SetPixels(pixels); texture.Apply(true, false);
                AssetDatabase.CreateAsset(texture, texturePath);
            }
            material.SetTexture("_BaseMap", texture);
        }
        EditorUtility.SetDirty(material);
        return material;
    }
}
