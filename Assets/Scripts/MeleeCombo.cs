using UnityEngine;

/// <summary>
/// Three-hit melee combo: punch -> punch -> kick (LMB). Press again while an attack plays to queue the next one;
/// it starts right after the current strike lands. Stop pressing and she returns to idle/run.
/// </summary>
public class MeleeCombo : MonoBehaviour
{
    public Animator animator;
    public ThirdPersonController mover;
    public Transform cameraTransform;

    [System.Serializable]
    public struct Step
    {
        public string state;
        [Tooltip("Normalized time at which the next attack may start (just after the strike lands)")]
        public float chainAt;
        [Tooltip("Normalized time of the strike: the forward lunge happens here")]
        public float strikeAt;
        public float lunge;
    }

    public Step[] steps =
    {
        new Step { state = "Punch1", chainAt = 0.48f, strikeAt = 0.30f, lunge = 1.6f },
        new Step { state = "Punch2", chainAt = 0.42f, strikeAt = 0.22f, lunge = 1.8f },
        new Step { state = "Kick",   chainAt = 0.95f, strikeAt = 0.58f, lunge = 2.4f },
    };
    public float crossFade = 0.07f;

    public bool IsAttacking => current >= 0;

    int current = -1;
    float time, length;
    bool queued, lunged;

    void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (mover == null) mover = GetComponent<ThirdPersonController>();
        if (cameraTransform == null && Camera.main) cameraTransform = Camera.main.transform;
    }

    void Update()
    {
        if (animator == null || mover == null || mover.IsClimbing) return;
        bool press = Cursor.lockState == CursorLockMode.Locked && Input.GetMouseButtonDown(0);

        if (current < 0)
        {
            if (press && mover.IsGrounded && !mover.IsCrouching) Begin(0);
            return;
        }

        time += Time.deltaTime;
        float t = time / length;
        var step = steps[current];
        if (press && t > 0.1f) queued = true;

        if (!lunged && t >= step.strikeAt) { lunged = true; mover.Lunge(step.lunge); }

        if (queued && t >= step.chainAt && current < steps.Length - 1) Begin(current + 1);
        else if (t >= 1f) End();
    }

    void Begin(int index)
    {
        current = index;
        time = 0f;
        queued = false;
        lunged = false;
        var st = steps[index].state;
        length = StateLength(st);
        animator.CrossFadeInFixedTime(st, crossFade, 0, 0f);
        // attack where the camera looks
        if (cameraTransform) mover.FaceDirection(Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up));
    }

    void End()
    {
        current = -1;
        animator.CrossFadeInFixedTime("Locomotion", 0.2f);
    }

    /// <summary>Called when something else (climbing) takes over.</summary>
    public void Cancel() { current = -1; queued = false; }

    float StateLength(string state)
    {
        float speed = state == "Kick" ? 1.2f : 1.35f;   // matches the state speeds in the Animator Controller
        foreach (var c in animator.runtimeAnimatorController.animationClips)
            if (c.name == state) return c.length / speed;
        return 1f;
    }
}
