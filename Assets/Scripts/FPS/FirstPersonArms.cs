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

    void OnEnable() => anim = GetComponent<Animator>();

    /// <summary>Call while the model is still in its T-pose (before the first animation update).</summary>
    public void CaptureRestPose()
    {
        anim = GetComponent<Animator>();
        var r = anim.GetBoneTransform(HumanBodyBones.RightHand);
        var l = anim.GetBoneTransform(HumanBodyBones.LeftHand);
        restR = Quaternion.Inverse(transform.rotation) * r.rotation;
        restL = Quaternion.Inverse(transform.rotation) * l.rotation;
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

    /// <summary>Creates the arms under the camera: only the arm pieces are shown, all on the view-model layer.</summary>
    public static FirstPersonArms Spawn(GameObject characterPrefab, RuntimeAnimatorController controller, Avatar avatar,
                                        Transform camera, int layer, float characterHeight, Vector3 shoulderTarget)
    {
        var go = Instantiate(characterPrefab, camera);
        go.name = "FirstPersonArms";
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        // scale to the given height (measured in the rest pose before any animation runs)
        var rs = go.GetComponentsInChildren<Renderer>(true);
        Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        go.transform.localScale *= characterHeight / Mathf.Max(0.01f, b.size.y);

        foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
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
