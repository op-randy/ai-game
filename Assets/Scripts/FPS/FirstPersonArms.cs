using UnityEngine;

/// <summary>
/// First-person arms: a copy of the character showing only her arms, attached to the camera.
/// Humanoid IK puts the right hand on the pistol grip and the left hand on the foregrip of the held weapon.
/// Hand directions are given in the weapon's space (x right, y up, z forward).
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Animator))]
public class FirstPersonArms : MonoBehaviour
{
    public Transform gripRight;
    public Transform gripLeft;
    public Transform cameraTransform;

    [Header("Right hand (pistol grip)")]
    public Vector3 rightFingers = new Vector3(-0.15f, -0.55f, 0.8f);
    public Vector3 rightPalm = new Vector3(-1f, 0f, 0f);
    public Vector3 rightRotationOffset = Vector3.zero;
    public Vector3 rightElbowHint = new Vector3(0.45f, -0.55f, 0.05f);     // camera space

    [Header("Left hand (foregrip)")]
    public Vector3 leftFingers = new Vector3(0.35f, -0.55f, 0.75f);
    public Vector3 leftPalm = new Vector3(1f, 0f, 0f);
    public Vector3 leftRotationOffset = Vector3.zero;
    public Vector3 leftElbowHint = new Vector3(-0.5f, -0.6f, 0.25f);

    [Header("Finger curl in degrees past the rest pose: thumb, index, middle, ring, pinky")]
    public float[] rightCurl = { 10f, 30f, 75f, 80f, 80f };
    public float[] leftCurl = { 10f, 70f, 70f, 70f, 70f };

    [Tooltip("Distance from the wrist to the middle of the palm, along the fingers")]
    public float palmLength = 0.07f;
    public float palmToGrip = 0.025f;

    Animator anim;
    // hand bone orientation in the T-pose, relative to the arms root (fingers along ±X)
    Quaternion restR = Quaternion.identity, restL = Quaternion.identity;
    // constant offset between the humanoid IK goal rotation and the actual hand bone rotation
    Quaternion goalToBoneR = Quaternion.identity, goalToBoneL = Quaternion.identity;
    Quaternion lastGoalR, lastGoalL;
    int calibrations;

    public bool Calibrated => calibrations >= 2;

    // fingers: bones found by name (the avatar does not map them), each bent about a fixed axis toward the palm
    static readonly string[] FingerNames = { "Thumb", "Index", "Middle", "Ring", "Pinky" };
    static readonly string[] JointNames = { "Proximal", "Intermediate", "Distal" };
    static readonly float[] JointShare = { 0.9f, 1.15f, 0.8f };         // the middle joint bends the most
    struct FingerJoint { public Transform bone; public Quaternion restLocal; public Vector3 axisInParent; }
    FingerJoint[] rightJoints, leftJoints;

    void OnEnable() => anim = GetComponent<Animator>();

    /// <summary>Call while the model is still in its T-pose (before the first animation update).</summary>
    public void CaptureRestPose()
    {
        anim = GetComponent<Animator>();
        var r = anim.GetBoneTransform(HumanBodyBones.RightHand);
        var l = anim.GetBoneTransform(HumanBodyBones.LeftHand);
        restR = Quaternion.Inverse(transform.rotation) * r.rotation;
        restL = Quaternion.Inverse(transform.rotation) * l.rotation;
        rightJoints = CaptureFingers("Right_", r);
        leftJoints = CaptureFingers("Left_", l);
    }

    FingerJoint[] CaptureFingers(string prefix, Transform hand)
    {
        var all = GetComponentsInChildren<Transform>(true);
        Transform Find(string n) { foreach (var t in all) if (t.name == n) return t; return null; }
        var joints = new FingerJoint[15];
        // the palm faces where the slightly curled rest fingers point, away from the line of the hand
        Vector3 handDir = Vector3.zero, palm = Vector3.zero;
        var mid = Find(prefix + "MiddleProximal");
        if (mid == null) return null;
        handDir = (mid.position - hand.position).normalized;
        for (int f = 1; f < 5; f++)
        {
            var k = Find(prefix + FingerNames[f] + "Proximal"); var tip = Find(prefix + FingerNames[f] + "DistalEnd");
            if (k && tip) palm += Vector3.ProjectOnPlane(tip.position - k.position, handDir);
        }
        palm.Normalize();
        for (int f = 0; f < 5; f++)
        {
            var k = Find(prefix + FingerNames[f] + "Proximal"); var tip = Find(prefix + FingerNames[f] + "DistalEnd");
            if (!k || !tip) return null;
            Vector3 dir = (tip.position - k.position).normalized;
            Vector3 axis = Vector3.Cross(dir, palm).normalized;
            // make a positive angle bend the finger toward the palm
            if (Vector3.Dot(Quaternion.AngleAxis(10f, axis) * dir, palm) < Vector3.Dot(dir, palm)) axis = -axis;
            for (int j = 0; j < 3; j++)
            {
                var b = Find(prefix + FingerNames[f] + JointNames[j]);
                if (!b || !b.parent) return null;
                joints[f * 3 + j] = new FingerJoint { bone = b, restLocal = b.localRotation, axisInParent = Quaternion.Inverse(b.parent.rotation) * axis };
            }
        }
        return joints;
    }

    void CurlFingers(FingerJoint[] joints, float[] curl)
    {
        if (joints == null || curl == null) return;
        for (int f = 0; f < 5 && f < curl.Length; f++)
            for (int j = 0; j < 3; j++)
            {
                var fj = joints[f * 3 + j];
                if (fj.bone) fj.bone.localRotation = Quaternion.AngleAxis(curl[f] * JointShare[j], fj.axisInParent) * fj.restLocal;
            }
    }

    /// <summary>After an animation update without IK: compare the IK goal with the real hand bone.</summary>
    public void Calibrate()
    {
        if (anim == null) return;
        goalToBoneR = Quaternion.Inverse(lastGoalR) * anim.GetBoneTransform(HumanBodyBones.RightHand).rotation;
        goalToBoneL = Quaternion.Inverse(lastGoalL) * anim.GetBoneTransform(HumanBodyBones.LeftHand).rotation;
        calibrations++;
    }

    void LateUpdate()
    {
        if (Application.isPlaying && !Calibrated) Calibrate();
        CurlFingers(rightJoints, rightCurl);
        CurlFingers(leftJoints, leftCurl);
    }

    void OnAnimatorIK(int layer)
    {
        if (anim == null) return;
        if (!Calibrated)
        {
            lastGoalR = anim.GetIKRotation(AvatarIKGoal.RightHand);
            lastGoalL = anim.GetIKRotation(AvatarIKGoal.LeftHand);
            SetWeights(AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, 0f);
            SetWeights(AvatarIKGoal.LeftHand, AvatarIKHint.LeftElbow, 0f);
            return;
        }
        bool on = gripRight != null && gripLeft != null;
        Apply(AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, on ? gripRight : null, rightFingers, rightPalm, false, rightRotationOffset, rightElbowHint, restR, goalToBoneR);
        Apply(AvatarIKGoal.LeftHand, AvatarIKHint.LeftElbow, on ? gripLeft : null, leftFingers, leftPalm, true, leftRotationOffset, leftElbowHint, restL, goalToBoneL);
    }

    void SetWeights(AvatarIKGoal goal, AvatarIKHint hint, float w)
    {
        anim.SetIKPositionWeight(goal, w);
        anim.SetIKRotationWeight(goal, w);
        anim.SetIKHintPositionWeight(hint, w);
    }

    void Apply(AvatarIKGoal goal, AvatarIKHint hint, Transform grip, Vector3 fingers, Vector3 palm, bool left,
               Vector3 offset, Vector3 elbow, Quaternion rest, Quaternion goalToBone)
    {
        SetWeights(goal, hint, grip ? 1f : 0f);
        if (!grip) return;

        Quaternion gunRot = grip.rotation;
        Vector3 f = (gunRot * fingers).normalized;
        Vector3 p = (gunRot * palm).normalized;
        // map the T-pose hand frame (fingers along ±X, palm down) onto the wanted fingers / palm directions
        Vector3 x = left ? -f : f;
        Vector3 y = Vector3.ProjectOnPlane(-p, x).normalized;
        Quaternion frame = Quaternion.LookRotation(Vector3.Cross(x, y), y) * Quaternion.Euler(offset);
        Quaternion boneRot = frame * rest;
        anim.SetIKRotation(goal, boneRot * Quaternion.Inverse(goalToBone));

        anim.SetIKPosition(goal, grip.position - f * palmLength - p * palmToGrip);
        if (cameraTransform) anim.SetIKHintPosition(hint, cameraTransform.TransformPoint(elbow));
    }

    // ---------------- spawning ----------------
    static readonly string[] ArmParts = { "tripo_part_18", "tripo_part_17", "tripo_part_36", "tripo_part_10",   // right: upper arm, forearm, forearm, hand
                                          "tripo_part_30", "tripo_part_32", "tripo_part_39", "tripo_part_16",   // left
                                          "CG_ArmL", "CG_ArmR" };                                                 // high-poly model

    static readonly System.Collections.Generic.Dictionary<GameObject, float> restHeights = new System.Collections.Generic.Dictionary<GameObject, float>();

    static float RestHeight(Renderer[] rs)
    {
        Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        return b.size.y;
    }

    /// <summary>Height of a model in its rest pose at unit scale (measured once per prefab).</summary>
    static float RestHeight(GameObject prefab)
    {
        if (restHeights.TryGetValue(prefab, out float h)) return h;
        var tmp = Instantiate(prefab);
        tmp.SetActive(true);
        tmp.transform.localScale = prefab.transform.localScale;
        h = RestHeight(tmp.GetComponentsInChildren<Renderer>(true));
        tmp.SetActive(false);
        if (Application.isPlaying) Destroy(tmp); else DestroyImmediate(tmp);
        return restHeights[prefab] = h;
    }

    /// <summary>Creates the arms under the camera: only the arm pieces are shown, all on the view-model layer.</summary>
    public static FirstPersonArms Spawn(GameObject characterPrefab, RuntimeAnimatorController controller, Avatar avatar,
                                        Transform camera, int layer, float characterHeight, Vector3 shoulderTarget, GameObject materialSource = null)
    {
        var go = Instantiate(characterPrefab, camera);
        go.name = "FirstPersonArms";
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        // scale to the given height (measured in the rest pose before any animation runs);
        // an arms-only model is scaled by the full character it was cut from
        var rs = go.GetComponentsInChildren<Renderer>(true);
        float h = materialSource != null ? RestHeight(materialSource) : RestHeight(rs);
        go.transform.localScale *= characterHeight / Mathf.Max(0.01f, h);

        foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        // the arms model imports without materials: take the character's own by renderer name
        if (materialSource != null)
            foreach (var src in materialSource.GetComponentsInChildren<Renderer>(true))
                foreach (var r in rs)
                    if (r.gameObject.name == src.gameObject.name) r.sharedMaterials = src.sharedMaterials;
        foreach (var r in rs)
        {
            bool keep = System.Array.IndexOf(ArmParts, r.gameObject.name) >= 0;
            r.enabled = keep;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
        }

        var anim = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;
        if (avatar) anim.avatar = avatar;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        var arms = go.AddComponent<FirstPersonArms>();
        arms.cameraTransform = camera;
        arms.CaptureRestPose();                 // still in the T-pose here

        // evaluate the idle pose twice without IK to calibrate the hands
        anim.Rebind();
        for (int i = 0; i < 2; i++) { anim.Update(0f); arms.Calibrate(); }

        // put the shoulders (in the idle pose) where they should sit relative to the eye
        var ls = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        var rsh = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
        if (ls && rsh)
        {
            Vector3 mid = camera.InverseTransformPoint((ls.position + rsh.position) * 0.5f);
            go.transform.localPosition += shoulderTarget - mid;
        }
        return arms;
    }
}
