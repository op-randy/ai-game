using UnityEngine;

/// <summary>
/// First-person movement and mouse look. WASD move, Shift sprint, Space jump, Esc frees the mouse (click to capture).
/// The weapon calls AddRecoil() to kick the camera.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("References")]
    public Transform cameraPivot;               // child that holds the camera (pitch)

    [Header("Movement")]
    public float walkSpeed = 4.5f;
    public float sprintSpeed = 7.5f;
    public float aimSpeedMultiplier = 0.55f;
    public float acceleration = 16f;
    public float jumpHeight = 1.1f;
    public float gravity = -22f;

    [Header("Crouch / slide (Ctrl)")]
    public float crouchSpeed = 2.4f;
    public float slideBoost = 9.5f;             // speed at the start of a slide
    public float slideFriction = 7f;            // m/s lost per second
    public float slideMinSpeed = 3.2f;          // slide ends below this (then crouch)
    public float standEyeHeight = 1.62f;
    public float crouchEyeHeight = 1.1f;
    public float slideEyeHeight = 0.95f;
    public float slideTilt = 6f;                // camera roll during a slide

    [Header("Look")]
    public float mouseSensitivity = 2.0f;
    public float aimSensitivityMultiplier = 0.6f;

    [HideInInspector] public bool isAiming;
    public bool IsSprinting { get; private set; }
    public bool IsSliding { get; private set; }
    public bool IsCrouching { get; private set; }
    public Vector3 PlanarVelocity => new Vector3(controller.velocity.x, 0f, controller.velocity.z);
    public bool IsGrounded => controller.isGrounded;

    CharacterController controller;
    float yaw, pitch;
    Vector2 recoilKick;          // short visual kick that springs back (x = up, y = sideways)
    Vector3 planarVelocity;
    float verticalVelocity;
    Vector3 spawnPoint;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        yaw = transform.eulerAngles.y;
        spawnPoint = transform.position;
    }

    void Start() => LockCursor(true);

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) LockCursor(false);
        else if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked) LockCursor(true);

        // ---- look ----
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            float sens = mouseSensitivity * (isAiming ? aimSensitivityMultiplier : 1f);
            yaw += Input.GetAxisRaw("Mouse X") * sens;
            pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * sens, -85f, 85f);
        }
        recoilKick = Vector2.Lerp(recoilKick, Vector2.zero, 1f - Mathf.Exp(-9f * Time.deltaTime));
        transform.rotation = Quaternion.Euler(0f, yaw + recoilKick.y, 0f);

        // ---- move ----
        Vector2 input = new Vector2(
            (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0),
            (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0));
        Vector3 wish = Vector3.ClampMagnitude(transform.forward * input.y + transform.right * input.x, 1f);
        float flatSpeed = planarVelocity.magnitude;
        bool fastOnGround = controller.isGrounded && flatSpeed > walkSpeed * 0.9f;

        // Ctrl toggles crouch (or starts a slide when running); C is hold-to-crouch (or slide when running)
        if (Input.GetKeyDown(KeyCode.LeftControl))
        {
            if (!IsSliding && fastOnGround) { StartSlide(flatSpeed); crouchToggled = true; }
            else { crouchToggled = !crouchToggled; IsSliding = false; }
        }
        if (Input.GetKeyDown(KeyCode.C) && !IsSliding && fastOnGround) StartSlide(flatSpeed);
        if (Input.GetKeyDown(KeyCode.LeftShift)) crouchToggled = false;      // sprinting stands you up
        bool ctrl = crouchToggled || Input.GetKey(KeyCode.C);

        if (IsSliding)
        {
            // keep the momentum, steer a little, bleed off speed
            float s = Mathf.Max(0f, planarVelocity.magnitude - slideFriction * Time.deltaTime);
            Vector3 dir = Vector3.Slerp(planarVelocity.normalized, wish.sqrMagnitude > 0.01f ? wish.normalized : planarVelocity.normalized, 1.5f * Time.deltaTime);
            planarVelocity = dir * s;
            if (!ctrl || s < slideMinSpeed || !controller.isGrounded) IsSliding = false;
            IsSprinting = false;
        }
        else
        {
            IsCrouching = ctrl && controller.isGrounded;
            IsSprinting = !IsCrouching && Input.GetKey(KeyCode.LeftShift) && input.y > 0 && !isAiming;
            float target = (IsCrouching ? crouchSpeed : IsSprinting ? sprintSpeed : walkSpeed) * (isAiming ? aimSpeedMultiplier : 1f);
            planarVelocity = Vector3.MoveTowards(planarVelocity, wish * target, acceleration * Time.deltaTime);
        }
        if (IsSliding) IsCrouching = true;

        // eye height and collider follow stance; camera rolls a little in a slide
        float eye = IsSliding ? slideEyeHeight : IsCrouching ? crouchEyeHeight : standEyeHeight;
        if (!IsCrouching && !CanStandUp()) { eye = crouchEyeHeight; IsCrouching = true; }
        if (cameraPivot)
        {
            var lp = cameraPivot.localPosition;
            lp.y = Mathf.Lerp(lp.y, eye, 1f - Mathf.Exp(-14f * Time.deltaTime));
            cameraPivot.localPosition = lp;
            roll = Mathf.Lerp(roll, IsSliding ? slideTilt : 0f, 1f - Mathf.Exp(-10f * Time.deltaTime));
            cameraPivot.localRotation = Quaternion.Euler(pitch - recoilKick.x, 0f, roll);
        }
        float h = IsCrouching ? 1.15f : 1.8f;
        controller.height = Mathf.Lerp(controller.height, h, 1f - Mathf.Exp(-14f * Time.deltaTime));
        controller.center = new Vector3(0f, controller.height / 2f, 0f);

        if (controller.isGrounded)
        {
            if (verticalVelocity < 0f) verticalVelocity = -2f;
            if (Input.GetKeyDown(KeyCode.Space)) { verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity); IsSliding = false; crouchToggled = false; }
        }
        verticalVelocity += gravity * Time.deltaTime;
        controller.Move((planarVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);

        if (transform.position.y < -20f)
        {
            controller.enabled = false; transform.position = spawnPoint; controller.enabled = true;
            planarVelocity = Vector3.zero; verticalVelocity = 0f;
        }
    }

    float roll;
    bool crouchToggled;

    void StartSlide(float currentSpeed)
    {
        IsSliding = true;
        planarVelocity = planarVelocity.normalized * Mathf.Max(currentSpeed, slideBoost);
    }

    bool CanStandUp()
    {
        float r = controller.radius * 0.9f;
        Vector3 bottom = transform.position + Vector3.up * (1.15f - r);
        Vector3 topP = transform.position + Vector3.up * (1.8f - r);
        return !Physics.CheckCapsule(bottom, topP, r, ~(1 << 2), QueryTriggerInteraction.Ignore);
    }

    /// <summary>up/side = visual kick in degrees that springs back; climb = permanent upward drift the player pulls down.</summary>
    public void AddRecoil(float up, float side, float climb)
    {
        recoilKick += new Vector2(up, side);
        pitch = Mathf.Clamp(pitch - climb, -85f, 85f);
    }

    static void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
