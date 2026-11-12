using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spring-bone physics for the cape, hair and jiggle bones. Bones are found by name prefix
/// (Cape_*, Hair_*, Jiggle_*); each chain swings with gravity and inertia, springs back toward the
/// animated pose and collides with capsules built from the humanoid skeleton.
/// Runs after the Animator (LateUpdate) at a fixed sub-step for stability.
/// </summary>
[DefaultExecutionOrder(1000)]
public class SpringBones : MonoBehaviour
{
    [System.Serializable]
    public class Group
    {
        public string prefix;
        [Range(0f, 1f)] public float stiffness = 0.1f;   // pull back toward the animated shape
        [Range(0f, 1f)] public float damping = 0.1f;     // velocity lost per step
        public float gravity = 5f;                      // m/s² downward
        public float radius = 0.02f;                    // collision radius of the particles
        [Range(0f, 1f)] public float inertia = 1f;      // how much body movement is passed on (1 = full lag)
        [Range(0f, 1f)] public float tipStiffness = 1f; // stiffness multiplier at the end of a chain
        [Range(0f, 180f)] public float maxAngle = 180f; // bend limit relative to the parent's rest pose
    }

    public Group[] groups =
    {
        new Group { prefix = "Cape_",   stiffness = 0.09f, damping = 0.20f, gravity = 7f,   radius = 0.035f, inertia = 0.85f },
        new Group { prefix = "Hair_",   stiffness = 0.08f, damping = 0.12f, gravity = 5f, radius = 0.025f, inertia = 0.94f, tipStiffness = 0.5f, maxAngle = 50f },
        new Group { prefix = "Jiggle_", stiffness = 0.28f, damping = 0.07f, gravity = 0.6f, radius = 0f,     inertia = 0.8f },
    };
    public bool collide = true;
    public float stepRate = 90f;

    class Bone
    {
        public Transform t;
        public Group g;
        public Quaternion restLocalRot;
        public Vector3 tailLocal;       // tail position in the bone's local space (at rest)
        public float length;
        public Vector3 pos, prev;       // simulated tail position
    }
    readonly List<List<Bone>> chains = new List<List<Bone>>();
    struct Capsule { public Transform a, b; public float r; }
    readonly List<Capsule> capsules = new List<Capsule>();
    float accumulator;
    bool initialised;
    Vector3 lastRootPos;

    void Start()
    {
        var all = GetComponentsInChildren<Transform>(true);
        foreach (var g in groups)
        {
            // The replacement ponytail has its own continuous solver. Do not animate the hidden old hair.
            if (g.prefix == "Hair_" && GetComponent<PonytailHair>() != null) continue;
            foreach (var t in all)
            {
                if (!t.name.StartsWith(g.prefix)) continue;
                if (t.parent != null && t.parent.name.StartsWith(g.prefix)) continue;     // not a chain root
                var chain = new List<Bone>();
                for (var cur = t; cur != null; cur = FindChildWithPrefix(cur, g.prefix))
                {
                    var child = FindChildWithPrefix(cur, g.prefix);
                    Vector3 tailLocal;
                    if (child != null) tailLocal = child.localPosition;
                    else if (chain.Count > 0) tailLocal = cur.InverseTransformVector(cur.position - chain[chain.Count - 1].t.position);
                    else tailLocal = cur.InverseTransformVector((cur.position - cur.parent.position).normalized * 0.06f);  // single jiggle bone
                    chain.Add(new Bone { t = cur, g = g, restLocalRot = cur.localRotation, tailLocal = tailLocal, length = cur.TransformVector(tailLocal).magnitude });
                }
                chains.Add(chain);
            }
        }
        BuildCapsules();
    }

    static Transform FindChildWithPrefix(Transform t, string prefix)
    {
        for (int i = 0; i < t.childCount; i++) if (t.GetChild(i).name.StartsWith(prefix)) return t.GetChild(i);
        return null;
    }

    void BuildCapsules()
    {
        var anim = GetComponentInChildren<Animator>();
        if (anim == null || !anim.isHuman) return;
        // character height in metres, measured from the skeleton (the head bone sits at ~88% of the height)
        var headT = anim.GetBoneTransform(HumanBodyBones.Head);
        var footT = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
        float h = headT && footT ? Mathf.Max(0.5f, (headT.position.y - footT.position.y + 0.08f) / 0.88f) : 1.75f;
        void Add(HumanBodyBones a, HumanBodyBones b, float r)
        {
            var ta = anim.GetBoneTransform(a); var tb = anim.GetBoneTransform(b);
            if (ta && tb) capsules.Add(new Capsule { a = ta, b = tb, r = r * h / 1.75f });
        }
        Add(HumanBodyBones.Hips, HumanBodyBones.Spine, 0.13f);
        Add(HumanBodyBones.Spine, HumanBodyBones.Chest, 0.12f);
        Add(HumanBodyBones.Chest, HumanBodyBones.Neck, 0.12f);
        Add(HumanBodyBones.Neck, HumanBodyBones.Head, 0.06f);
        Add(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, 0.085f);
        Add(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, 0.085f);
        Add(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, 0.06f);
        Add(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, 0.06f);
        Add(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, 0.045f);
        Add(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 0.045f);
        var head = anim.GetBoneTransform(HumanBodyBones.Head);
        if (head) capsules.Add(new Capsule { a = head, b = head, r = 0.11f * h / 1.75f });
    }

    void LateUpdate()
    {
        if (chains.Count == 0) return;
        // start every frame from the animated pose (the Animator does not touch these bones)
        foreach (var c in chains) foreach (var b in c) b.t.localRotation = b.restLocalRot;

        if (!initialised || (transform.position - lastRootPos).sqrMagnitude > 4f) { ResetParticles(); initialised = true; }
        lastRootPos = transform.position;

        float step = 1f / stepRate;
        accumulator = Mathf.Min(accumulator + Time.deltaTime, step * 6f);
        while (accumulator >= step) { Simulate(step); accumulator -= step; }
        Apply();
    }

    void ResetParticles()
    {
        foreach (var c in chains) foreach (var b in c) { b.pos = b.prev = b.t.TransformPoint(b.tailLocal); }
    }

    void Simulate(float dt)
    {
        foreach (var c in chains)
        {
            // rest pose relative to the (already simulated) parent, then walk down the chain aiming each bone
            foreach (var b0 in c) b0.t.localRotation = b0.restLocalRot;
            for (int i = 0; i < c.Count; i++)
            {
                var b = c[i];
                var g = b.g;
                Vector3 head = b.t.position;
                Vector3 rest = b.t.TransformPoint(b.tailLocal);
                Vector3 vel = (b.pos - b.prev) * (1f - g.damping);
                b.prev = b.pos;
                b.pos += vel * g.inertia + (rest - b.pos) * (1f - g.inertia) * 0.5f + Vector3.down * g.gravity * dt * dt;
                // Keep the scalp supported, but let the ends bend instead of moving as one stiff block.
                float alongChain = c.Count > 1 ? i / (float)(c.Count - 1) : 0f;
                float stiffness = g.stiffness * Mathf.Lerp(1f, g.tipStiffness, alongChain);
                b.pos += (rest - b.pos) * stiffness;
                // keep the bone length
                Vector3 d = b.pos - head;
                b.pos = head + (d.sqrMagnitude > 1e-8f ? d.normalized : (rest - head).normalized) * b.length;
                if (g.maxAngle < 180f)
                {
                    Vector3 limited = Vector3.RotateTowards((rest - head).normalized, (b.pos - head).normalized,
                        g.maxAngle * Mathf.Deg2Rad, 0f);
                    b.pos = head + limited * b.length;
                }
                if (collide && g.radius > 0f) Collide(ref b.pos, head, b.length, g.radius);
                Aim(b);
            }
        }
    }

    void Collide(ref Vector3 p, Vector3 head, float len, float pr)
    {
        foreach (var cap in capsules)
        {
            Vector3 a = cap.a.position, bb = cap.b.position, ab = bb - a;
            float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            Vector3 closest = a + ab * t;
            Vector3 off = p - closest;
            float minD = cap.r + pr;
            if (off.sqrMagnitude < minD * minD && off.sqrMagnitude > 1e-10f)
            {
                p = closest + off.normalized * minD;
                p = head + (p - head).normalized * len;
            }
        }
    }

    static void Aim(Bone b)
    {
        Vector3 from = b.t.TransformDirection(b.tailLocal);
        Vector3 to = b.pos - b.t.position;
        if (from.sqrMagnitude < 1e-10f || to.sqrMagnitude < 1e-10f) return;
        b.t.rotation = Quaternion.FromToRotation(from, to) * b.t.rotation;
    }

    void Apply()
    {
        foreach (var c in chains) foreach (var b in c) Aim(b);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.4f);
        foreach (var cap in capsules)
        {
            if (cap.a == null || cap.b == null) continue;
            Gizmos.DrawWireSphere(cap.a.position, cap.r); Gizmos.DrawWireSphere(cap.b.position, cap.r);
        }
    }
}
