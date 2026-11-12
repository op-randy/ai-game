using UnityEngine;

/// <summary>
/// Third-person movement: WASD walk relative to the camera, Ctrl/C crouch, Shift run, Space jump or climb
/// (when facing a ledge 0.6–2.4 m high). Drives the Animator (Speed / AnimSpeed) and supports the melee combo.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class ThirdPersonController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 1.3f;
    public float runSpeed = 3.4f;
    public float sprintSpeed = 6.2f;
    public float acceleration = 14f;
    public float turnSpeed = 720f;

    [Header("Crouching")]
    public float crouchSpeed = 0.8f;
    public float crouchHeight = 1.15f;

    [Header("Jumping")]
    public float jumpHeight = 1.2f;
    public float gravity = -20f;

    [Header("Climbing")]
    public float minClimbHeight = 0.6f;
    public float maxClimbHeight = 2.4f;
    public float climbReach = 0.9f;
    public float climbDuration = 2.1f;      // Climb clip length / its state speed

    [Header("References")]
    public Transform cameraTransform;
    public Transform model;
    public Animator animator;

    public bool IsClimbing => climbing;
    public bool IsGrounded => controller.isGrounded;
    public bool IsCrouching { get; private set; }
    public bool FirstPersonView { get; set; }

    // Ground speeds of the walk/run clips (see CharacterAnimationSetup)
    const float WalkClipSpeed = 1.03f, RunClipSpeed = 2.85f;
    // Hip height over the climb clip (0 = start, 1 = top), measured from the animation
    static readonly float[] ClimbRise = { 0, 0, 0, 0, 0, 0, 0.245f, 0.38f, 0.483f, 0.608f, 0.685f, 0.756f, 0.842f, 0.893f, 0.966f, 0.993f, 1f };

    CharacterController controller;
    MeleeCombo combo;
    Vector3 planarVelocity;
    float verticalVelocity;
    Vector3 spawnPoint;
    Vector3 lungeVelocity;
    float standingHeight;
    Vector3 standingCenter;
    bool crouchToggled;
    string movementState = "Locomotion";

    bool climbing;
    float climbT;
    Vector3 climbStart, climbEnd;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        standingHeight = controller.height;
        standingCenter = controller.center;
        combo = GetComponent<MeleeCombo>();
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    void Start() { spawnPoint = transform.position; }

    void Update()
    {
        if (climbing) { UpdateClimb(); return; }

        bool attacking = combo != null && combo.IsAttacking;
        Vector2 input = new Vector2(
            (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0),
            (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0));

        Vector3 forward = cameraTransform ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized : Vector3.forward;
        Vector3 right = cameraTransform ? Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized : Vector3.right;
        Vector3 wishDir = Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);
        if (attacking) wishDir = Vector3.zero;

        if (Input.GetKeyDown(KeyCode.LeftControl)) crouchToggled = !crouchToggled;
        bool wantCrouch = crouchToggled || Input.GetKey(KeyCode.C);
        IsCrouching = !attacking && controller.isGrounded && wantCrouch;
        if (!IsCrouching && !CanStandUp()) IsCrouching = true;
        float height = IsCrouching ? crouchHeight : standingHeight;
        controller.height = height;
        controller.center = standingCenter - Vector3.up * ((standingHeight - height) * 0.5f);

        float target = IsCrouching ? crouchSpeed : Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : walkSpeed;
        planarVelocity = Vector3.MoveTowards(planarVelocity, wishDir * target, acceleration * Time.deltaTime);
        lungeVelocity = Vector3.MoveTowards(lungeVelocity, Vector3.zero, 12f * Time.deltaTime);

        if (controller.isGrounded)
        {
            if (verticalVelocity < 0f) verticalVelocity = -2f;
            if (Input.GetKeyDown(KeyCode.Space) && !attacking && !IsCrouching)
            {
                if (TryStartClimb(wishDir.sqrMagnitude > 0.01f ? wishDir : ModelForward())) return;
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }
        verticalVelocity += gravity * Time.deltaTime;
        controller.Move((planarVelocity + lungeVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);

        // facing: in first person the body follows the camera, otherwise the move direction
        Transform turn = model ? model : transform;
        if (FirstPersonView && !attacking) turn.rotation = Quaternion.LookRotation(forward, Vector3.up);
        else if (wishDir.sqrMagnitude > 0.001f)
            turn.rotation = Quaternion.RotateTowards(turn.rotation, Quaternion.LookRotation(wishDir, Vector3.up), turnSpeed * Time.deltaTime);

        if (animator != null)
        {
            float speed = new Vector3(controller.velocity.x, 0f, controller.velocity.z).magnitude;
            if (attacking) speed = 0f;
            bool running = !IsCrouching && Input.GetKey(KeyCode.LeftShift);
            // Walking must not blend in the run clip just because her walk speed is above its threshold.
            float blendSpeed = running || IsCrouching ? speed : Mathf.Min(speed, WalkClipSpeed);
            float clipSpeed = IsCrouching ? crouchSpeed : running ? RunClipSpeed : WalkClipSpeed;
            animator.SetFloat("Speed", blendSpeed, 0.08f, Time.deltaTime);
            animator.SetFloat("AnimSpeed", speed > 0.2f ? Mathf.Max(0.6f, speed / clipSpeed) : 1f);
            if (!attacking)
            {
                string state = !controller.isGrounded ? "Jump" : IsCrouching ? "Crouch" : "Locomotion";
                if (state != movementState)
                {
                    animator.CrossFadeInFixedTime(state, 0.12f, 0, 0f);
                    movementState = state;
                }
            }
            else movementState = "Locomotion";
        }

        if (transform.position.y < -30f)
        {
            controller.enabled = false; transform.position = spawnPoint; controller.enabled = true;
            planarVelocity = Vector3.zero; verticalVelocity = 0f;
        }
    }

    bool CanStandUp()
    {
        if (controller.height >= standingHeight - 0.01f) return true;
        float radius = controller.radius * 0.9f;
        Vector3 center = transform.position + standingCenter;
        Vector3 top = center + Vector3.up * (standingHeight * 0.5f - radius);
        Vector3 bottom = transform.position + controller.center + Vector3.up * (controller.height * 0.5f - radius);
        return !Physics.CheckCapsule(bottom, top, radius, ~(1 << 2), QueryTriggerInteraction.Ignore);
    }

    // ---------------- melee support ----------------
    public void Lunge(float speed) => lungeVelocity = ModelForward() * speed;

    public void FaceDirection(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.001f) return;
        (model ? model : transform).rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    Vector3 ModelForward() => Vector3.ProjectOnPlane((model ? model : transform).forward, Vector3.up).normalized;

    // ---------------- climbing ----------------
    bool TryStartClimb(Vector3 dir)
    {
        dir = Vector3.ProjectOnPlane(dir, Vector3.up).normalized;
        if (dir.sqrMagnitude < 0.01f) return false;
        int mask = ~(1 << 2);   // ignore the player
        Vector3 feet = transform.position;
        // 1. a wall in front, at knee and chest height
        if (!Physics.Raycast(feet + Vector3.up * 0.5f, dir, out RaycastHit wall, climbReach, mask, QueryTriggerInteraction.Ignore)) return false;
        if (Mathf.Abs(wall.normal.y) > 0.35f) return false;   // not a wall (a slope or a ramp)
        Vector3 wallDir = -Vector3.ProjectOnPlane(wall.normal, Vector3.up).normalized;
        // 2. the top of that wall, a bit past its edge
        Vector3 probe = wall.point + wallDir * 0.35f + Vector3.up * (maxClimbHeight + 0.5f);
        if (!Physics.Raycast(probe, Vector3.down, out RaycastHit top, maxClimbHeight + 0.5f, mask, QueryTriggerInteraction.Ignore)) return false;
        float h = top.point.y - feet.y;
        if (h < minClimbHeight || h > maxClimbHeight || top.normal.y < 0.7f) return false;
        // 3. room to stand up there
        Vector3 standAt = top.point + Vector3.up * 0.02f;
        if (Physics.CheckCapsule(standAt + Vector3.up * (controller.radius + 0.05f), standAt + Vector3.up * (controller.height - controller.radius), controller.radius * 0.9f, mask, QueryTriggerInteraction.Ignore)) return false;

        climbing = true;
        climbT = 0f;
        climbStart = feet;
        climbEnd = standAt;
        controller.enabled = false;
        planarVelocity = Vector3.zero; verticalVelocity = 0f; lungeVelocity = Vector3.zero;
        if (combo) combo.Cancel();
        FaceDirection(wallDir);
        if (animator) animator.CrossFadeInFixedTime("Climb", 0.1f, 0, 0f);
        return true;
    }

    void UpdateClimb()
    {
        float height = climbEnd.y - climbStart.y;
        float duration = climbDuration * Mathf.Lerp(0.7f, 1f, Mathf.InverseLerp(minClimbHeight, 1.9f, height));
        climbT = Mathf.Min(1f, climbT + Time.deltaTime / duration);

        float f = climbT * (ClimbRise.Length - 1);
        int i = Mathf.Min((int)f, ClimbRise.Length - 2);
        float rise = Mathf.Lerp(ClimbRise[i], ClimbRise[i + 1], f - i);
        float over = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 0.97f, climbT));   // step onto the ledge near the end

        Vector3 flatStart = new Vector3(climbStart.x, 0f, climbStart.z), flatEnd = new Vector3(climbEnd.x, 0f, climbEnd.z);
        Vector3 flat = Vector3.Lerp(flatStart, flatEnd, over);
        transform.position = new Vector3(flat.x, climbStart.y + height * rise, flat.z);

        if (climbT >= 1f)
        {
            climbing = false;
            transform.position = climbEnd;
            controller.enabled = true;
            if (animator) animator.CrossFadeInFixedTime("Locomotion", 0.2f);
            movementState = "Locomotion";
        }
    }
}
