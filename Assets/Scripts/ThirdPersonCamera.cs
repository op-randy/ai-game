using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orbit camera with a first-person toggle (V). Mouse looks around, scroll wheel zooms (third person),
/// Esc frees the cursor, click captures it again. In first person the camera sits at her head and
/// the head pieces are hidden (they still cast shadows).
/// </summary>
public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target;
    public Vector3 targetOffset = new Vector3(0f, 1.5f, 0f);
    public float distance = 4.5f;
    public float minDistance = 1.5f;
    public float maxDistance = 9f;
    public float mouseSensitivity = 0.15f;
    public float minPitch = -30f;
    public float maxPitch = 70f;
    public LayerMask collisionMask = ~(1 << 2);
    public KeyCode toggleViewKey = KeyCode.V;
    public bool firstPerson;

    float yaw;
    float pitch = 15f;
    Camera cam;
    ThirdPersonController mover;
    Transform head;
    readonly List<Renderer> headParts = new List<Renderer>();
    Vector3 fpPos;

    void Start()
    {
        yaw = transform.eulerAngles.y;
        cam = GetComponent<Camera>();
        if (target)
        {
            mover = target.GetComponent<ThirdPersonController>();
            var anim = target.GetComponentInChildren<Animator>();
            if (anim && anim.isHuman) head = anim.GetBoneTransform(HumanBodyBones.Head);
            var neck = anim && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Neck) : null;
            if (head)
                foreach (var r in target.GetComponentsInChildren<Renderer>())
                {
                    Vector3 c = r.bounds.center;
                    // pieces centred on the head (mask, horns, head cap) – hair hangs lower and stays visible
                    if (r.name == "CG_Head" || r.name == "CG_Hair" || (Vector3.Distance(c, head.position) < 0.28f && (!neck || c.y > neck.position.y))) headParts.Add(r);
                }
        }
        LockCursor(true);
        ApplyView();
    }

    void LateUpdate()
    {
        if (target == null) return;

        if (Input.GetKeyDown(toggleViewKey)) { firstPerson = !firstPerson; ApplyView(); }
        if (Input.GetKeyDown(KeyCode.Escape)) LockCursor(false);
        else if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked) LockCursor(true);

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            yaw += Input.GetAxisRaw("Mouse X") * 10f * mouseSensitivity;
            pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * 10f * mouseSensitivity, firstPerson ? -80f : minPitch, firstPerson ? 80f : maxPitch);
        }
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);

        if (firstPerson && head)
        {
            Vector3 fwd = Vector3.ProjectOnPlane(rot * Vector3.forward, Vector3.up).normalized;
            Vector3 want = head.position + fwd * 0.1f + Vector3.up * 0.04f;
            fpPos = Vector3.Lerp(fpPos, want, 1f - Mathf.Exp(-25f * Time.deltaTime));   // soften head bob a little
            transform.SetPositionAndRotation(fpPos, rot);
            return;
        }

        distance = Mathf.Clamp(distance - Input.mouseScrollDelta.y * 0.5f, minDistance, maxDistance);
        Vector3 pivot = target.position + targetOffset;
        Vector3 dir = rot * Vector3.back;
        float d = distance;
        if (Physics.SphereCast(pivot, 0.2f, dir, out RaycastHit hit, distance, collisionMask, QueryTriggerInteraction.Ignore)
            && !hit.transform.IsChildOf(target))
            d = Mathf.Max(hit.distance - 0.1f, 0.3f);
        transform.SetPositionAndRotation(pivot + dir * d, rot);
    }

    void ApplyView()
    {
        if (mover) mover.FirstPersonView = firstPerson;
        if (target)
        {
            var hair = target.GetComponentInChildren<PonytailHair>();
            if (hair) hair.SetFirstPerson(firstPerson);
        }
        if (cam) cam.nearClipPlane = firstPerson ? 0.05f : 0.3f;
        foreach (var r in headParts)
            r.shadowCastingMode = firstPerson ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
        if (firstPerson && head) fpPos = head.position;
    }

    static void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
