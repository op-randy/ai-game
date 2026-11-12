using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// A fixed scalp, supported high ponytail root and overlapping wavy locks over a
/// world-space particle chain, not independently rotating chunks of the original hair.
/// Body/cape clearance is deliberately conservative; this is stylized hair, not strand simulation.
/// </summary>
[DefaultExecutionOrder(1100)]
public class PonytailHair : MonoBehaviour
{
    public Material hairMaterial;
    public Material tieMaterial;
    [Range(0.4f, 1f)] public float length = 0.72f;

    const int Segments = 12, Rings = 61, Sides = 24, Locks = 7, Supported = 2;
    const float Step = 1f / 90f;
    readonly List<GameObject> pieces = new List<GameObject>();
    readonly List<Mesh> meshes = new List<Mesh>();
    struct Capsule { public Transform a, b; public Vector3 offsetA, offsetB; public float radius; }
    readonly List<Capsule> capsules = new List<Capsule>();
    readonly Vector3[] points = new Vector3[Segments + 1];
    readonly Vector3[] previous = new Vector3[Segments + 1];
    readonly Vector3[] rest = new Vector3[Segments + 1];
    readonly float[] lengths = new float[Segments];
    readonly Vector3[] vertices = new Vector3[Locks * Rings * (Sides + 1)];
    readonly Vector3[] lockCenters = new Vector3[Rings];
    Transform head, anchor;
    Mesh tailMesh;
    float scale = 1f, accumulator;
    Vector3 lastAnchor;
    bool initialized, firstPerson;
    Material fallbackMaterial;

    public float MaxBodyPenetration { get; private set; }
    public float MaxSegmentError { get; private set; }
    public Vector3 TipPosition => points[Segments];
    public Vector3 RootPosition => points[0];
    public float RootLift => anchor ? Vector3.Dot(Curve(Supported / (float)Segments) - Curve(0f), anchor.up) : 0f;

    void Start() => Initialize();
    void LateUpdate() => Simulate(Time.deltaTime);

    public void Initialize()
    {
        if (initialized) return;
        var animator = GetComponent<Animator>();
        if (!animator || !animator.isHuman) return;
        head = animator.GetBoneTransform(HumanBodyBones.Head);
        var foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        if (!head || !foot) return;
        scale = Mathf.Clamp((head.position.y - foot.position.y) / 1.45f, 0.5f, 2f);
        if (!hairMaterial)
        {
            fallbackMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            fallbackMaterial.SetColor("_BaseColor", new Color(0.88f, 0.90f, 0.94f));
            fallbackMaterial.SetFloat("_Smoothness", 0.28f);
            hairMaterial = fallbackMaterial;
        }

        // Preserve the imported model so this replacement remains reversible.
        foreach (var renderer in GetComponentsInChildren<Renderer>())
            if (renderer.name == "CG_Hair") renderer.enabled = false;

        anchor = new GameObject("PonytailAnchor").transform;
        anchor.SetParent(head, true);
        anchor.position = head.position + (transform.up * 0.14f - transform.forward * 0.11f) * scale;
        anchor.rotation = transform.rotation;
        pieces.Add(anchor.gameObject);

        BuildScalp();
        BuildTie();
        var tail = MakePiece("WhitePonytail", transform, hairMaterial);
        tailMesh = new Mesh { name = "Continuous ponytail" };
        meshes.Add(tailMesh);
        tail.GetComponent<MeshFilter>().sharedMesh = tailMesh;
        var oneLock = TubeTriangles(Rings, Sides);
        var triangles = new int[oneLock.Length * Locks];
        for (int k = 0; k < Locks; k++) for (int i = 0; i < oneLock.Length; i++)
            triangles[k * oneLock.Length + i] = oneLock[i] + k * Rings * (Sides + 1);
        var uv = new Vector2[vertices.Length];
        for (int k = 0; k < Locks; k++) for (int r = 0; r < Rings; r++) for (int s = 0; s <= Sides; s++)
            uv[(k * Rings + r) * (Sides + 1) + s] = new Vector2(s / (float)Sides + k * 0.137f, r / (float)(Rings - 1));
        tailMesh.vertices = vertices;
        tailMesh.triangles = triangles;
        tailMesh.uv = uv;
        tailMesh.MarkDynamic();

        // A high tie arches away from the back before dropping to roughly waist length.
        for (int i = 0; i <= Segments; i++)
        {
            float t = i / (float)Segments;
            float rise = 0.065f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, t / 0.34f));
            float fall = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.20f) / 0.80f));
            float back = 0.055f * (1f - Mathf.Exp(-t * 12f))
                + 0.20f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.12f) / 0.70f));
            rest[i] = new Vector3(0f, rise - length * fall,
                -back) * scale;
            if (i > 0) lengths[i - 1] = Vector3.Distance(rest[i - 1], rest[i]);
        }

        void Add(HumanBodyBones a, HumanBodyBones b, float radius, float back = 0f)
        {
            var ta = animator.GetBoneTransform(a); var tb = animator.GetBoneTransform(b);
            if (!ta || !tb) return;
            Vector3 offset = -transform.forward * (back * scale);
            capsules.Add(new Capsule { a = ta, b = tb, radius = radius * scale,
                offsetA = ta.InverseTransformVector(offset), offsetB = tb.InverseTransformVector(offset) });
        }
        Add(HumanBodyBones.Head, HumanBodyBones.Head, 0.115f);
        Add(HumanBodyBones.Neck, HumanBodyBones.Head, 0.075f);
        // The rear envelope includes the cape, rather than pretending the back is bare skin.
        Add(HumanBodyBones.Hips, HumanBodyBones.Chest, 0.19f, 0.16f);
        Add(HumanBodyBones.Chest, HumanBodyBones.Neck, 0.14f, 0.06f);
        Add(HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm, 0.10f, 0.02f);
        Add(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, 0.065f);
        Add(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 0.065f);
        Add(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, 0.05f);
        Add(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, 0.05f);
        Add(HumanBodyBones.LeftHand, HumanBodyBones.LeftHand, 0.045f);
        Add(HumanBodyBones.RightHand, HumanBodyBones.RightHand, 0.045f);
        initialized = true;
        ResetParticles();
        UpdateMesh();
        SetFirstPerson(firstPerson);
    }

    GameObject MakePiece(string name, Transform parent, Material material)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        pieces.Add(go);
        return go;
    }

    void BuildScalp()
    {
        var go = MakePiece("WhitePonytailScalp", head, hairMaterial);
        // Author in model axes, then bind the fixed scalp to the animated head.
        go.transform.rotation = transform.rotation;
        go.transform.localScale = Vector3.one;
        const int rows = 21, sides = 64;
        var v = new Vector3[rows * (sides + 1)];
        var uv = new Vector2[v.Length];
        Vector3 center = head.position + (transform.up * 0.062f - transform.forward * 0.004f) * scale;
        for (int r = 0; r < rows; r++) for (int s = 0; s <= sides; s++)
        {
            float theta = s / (float)sides * Mathf.PI * 2f;
            float front = (Mathf.Cos(theta) + 1f) * 0.5f;
            // High forehead hairline, lower coverage at the back; no loose side chunks.
            float edge = Mathf.Lerp(2.0f, 1.08f, front);
            float phi = Mathf.Lerp(0.015f, edge, r / (float)(rows - 1));
            float groove = 1f + 0.012f * Mathf.Cos(theta * 32f + phi * 3f);
            Vector3 offset = new Vector3(Mathf.Sin(theta) * Mathf.Sin(phi) * 0.107f * groove,
                Mathf.Cos(phi) * 0.128f, Mathf.Cos(theta) * Mathf.Sin(phi) * 0.108f * groove);
            v[r * (sides + 1) + s] = go.transform.InverseTransformPoint(center + transform.rotation * offset * scale);
            uv[r * (sides + 1) + s] = new Vector2(s / (float)sides, r / (float)(rows - 1));
        }
        var mesh = new Mesh { name = "Swept white scalp", vertices = v, uv = uv, triangles = TubeTriangles(rows, sides) };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        meshes.Add(mesh);
    }

    void BuildTie()
    {
        var go = MakePiece("PonytailTie", anchor, tieMaterial ? tieMaterial : hairMaterial);
        const int rows = 13, sides = 24;
        var v = new Vector3[rows * (sides + 1)];
        Vector3 axis = (Vector3.up * 0.8f + Vector3.back).normalized;
        Vector3 right = Vector3.right, up = Vector3.Cross(right, axis).normalized;
        for (int r = 0; r < rows; r++) for (int s = 0; s <= sides; s++)
        {
            float a = r / (float)(rows - 1) * Mathf.PI * 2f;
            float b = s / (float)sides * Mathf.PI * 2f;
            Vector3 radial = right * Mathf.Cos(b) + up * Mathf.Sin(b);
            Vector3 p = radial * (0.038f + Mathf.Cos(a) * 0.005f) + axis * (Mathf.Sin(a) * 0.005f);
            v[r * (sides + 1) + s] = go.transform.InverseTransformVector(transform.rotation * p * scale);
        }
        var mesh = new Mesh { name = "Ponytail band", vertices = v, triangles = TubeTriangles(rows, sides) };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        meshes.Add(mesh);
    }

    static int[] TubeTriangles(int rings, int sides)
    {
        var result = new int[(rings - 1) * sides * 6];
        int index = 0;
        for (int r = 0; r < rings - 1; r++) for (int s = 0; s < sides; s++)
        {
            int a = r * (sides + 1) + s, b = a + sides + 1;
            result[index++] = a; result[index++] = b; result[index++] = a + 1;
            result[index++] = a + 1; result[index++] = b; result[index++] = b + 1;
        }
        return result;
    }

    void ResetParticles()
    {
        for (int i = 0; i <= Segments; i++)
            points[i] = previous[i] = anchor.position + anchor.rotation * rest[i];
        lastAnchor = anchor.position;
        accumulator = 0f;
    }

    public void Simulate(float deltaTime)
    {
        if (!initialized || deltaTime <= 0f) return;
        if ((anchor.position - lastAnchor).sqrMagnitude > 1f) ResetParticles();
        accumulator = Mathf.Min(accumulator + deltaTime, Step * 8f);
        int steps = Mathf.FloorToInt(accumulator / Step);
        if (steps > 0)
        {
            Vector3 startAnchor = lastAnchor;
            for (int s = 0; s < steps; s++)
                Integrate(Vector3.Lerp(startAnchor, anchor.position, (s + 1f) / steps));
            lastAnchor = anchor.position;
            accumulator -= steps * Step;
        }
        UpdateMesh();
    }

    void Integrate(Vector3 root)
    {
        Vector3 carry = (root - points[0]) * 0.94f;
        for (int i = Supported + 1; i <= Segments; i++)
        {
            Vector3 velocity = Vector3.ClampMagnitude((points[i] - previous[i]) * Mathf.Exp(-10f * Step), 0.035f * scale);
            points[i] += carry;
            previous[i] = points[i];
            Vector3 goal = root + anchor.rotation * rest[i];
            float support = Mathf.Lerp(75f, 20f, i / (float)Segments);
            points[i] += velocity + (Vector3.down * (9.81f * scale) + (goal - points[i]) * support) * (Step * Step);
        }
        points[0] = root;
        // The tied section is supported by the head, so gravity cannot collapse the upward arch.
        for (int i = 1; i <= Supported; i++)
            points[i] = previous[i] = root + anchor.rotation * rest[i];
        for (int pass = 0; pass < 12; pass++)
        {
            for (int i = Supported + 1; i <= Segments; i++)
            {
                Vector3 priorRest = anchor.rotation * (rest[i - 1] - rest[i - 2]);
                Quaternion bend = Quaternion.FromToRotation(priorRest, points[i - 1] - points[i - 2]);
                Vector3 reference = bend * (anchor.rotation * (rest[i] - rest[i - 1]));
                Vector3 direction = points[i] - points[i - 1];
                if (direction.sqrMagnitude < 1e-10f) direction = reference;
                // A gentle bend per joint avoids kinks, without locking the whole tail to a pose.
                direction = Vector3.RotateTowards(reference.normalized, direction.normalized, 16f * Mathf.Deg2Rad, 0f);
                // Below the lifted root, hair hangs under gravity instead of folding into an upward hook.
                if (i >= 4) direction = Vector3.RotateTowards(Vector3.down, direction, 40f * Mathf.Deg2Rad, 0f);
                points[i] = points[i - 1] + direction * lengths[i - 1];
                Collide(ref points[i], Radius(i / (float)Segments) + 0.008f * scale);
            }
        }
        MaxBodyPenetration = MaxSegmentError = 0f;
        for (int i = 1; i <= Segments; i++)
        {
            MaxSegmentError = Mathf.Max(MaxSegmentError, Mathf.Abs(Vector3.Distance(points[i], points[i - 1]) - lengths[i - 1]));
            foreach (var cap in capsules)
                MaxBodyPenetration = Mathf.Max(MaxBodyPenetration, cap.radius + Radius(i / (float)Segments)
                    - Vector3.Distance(points[i], Closest(cap, points[i])));
        }
    }

    Vector3 Closest(Capsule cap, Vector3 p)
    {
        Vector3 a = cap.a.TransformPoint(cap.offsetA), b = cap.b.TransformPoint(cap.offsetB), d = b - a;
        return a + d * (d.sqrMagnitude > 1e-10f ? Mathf.Clamp01(Vector3.Dot(p - a, d) / d.sqrMagnitude) : 0f);
    }

    void Collide(ref Vector3 p, float radius)
    {
        foreach (var cap in capsules)
        {
            Vector3 closest = Closest(cap, p), offset = p - closest;
            float distance = offset.magnitude, clearance = cap.radius + radius;
            if (distance < clearance)
            {
                Vector3 normal = distance > 1e-6f ? offset / distance : -transform.forward;
                p = closest + normal * clearance;
            }
        }
    }

    float Radius(float t)
    {
        float fullness = 0.035f + 0.045f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI)), 0.65f);
        return Mathf.Lerp(fullness, 0.004f, Mathf.Pow(t, 3f)) * scale;
    }

    Vector3 Curve(float t)
    {
        float f = t * Segments;
        int i = Mathf.Min((int)f, Segments - 1);
        float u = f - i;
        Vector3 a = RenderPoint(Mathf.Max(0, i - 1)), b = RenderPoint(i), c = RenderPoint(i + 1), d = RenderPoint(Mathf.Min(Segments, i + 2));
        return 0.5f * ((2f * b) + (-a + c) * u + (2f * a - 5f * b + 4f * c - d) * u * u
            + (-a + 3f * b - 3f * c + d) * u * u * u);
    }

    // Head-bound control points must follow every render frame, not only 90 Hz physics ticks.
    Vector3 RenderPoint(int index) => index <= Supported ? anchor.position + anchor.rotation * rest[index] : points[index];

    void UpdateMesh()
    {
        MaxBodyPenetration = 0f;
        for (int k = 0; k < Locks; k++)
        {
            float end = k == 0 ? 1f : 0.87f + (k % 3) * 0.045f;
            float phase = k * Mathf.PI * 2f / (Locks - 1);
            Vector3 priorTangent = (Curve(0.005f) - Curve(0f)).normalized;
            Vector3 right = Vector3.ProjectOnPlane(anchor.right, priorTangent).normalized;
            for (int r = 0; r < Rings; r++)
            {
                float u = r / (float)(Rings - 1), t = u * end;
                Vector3 tangent = (Curve(Mathf.Min(1f, t + 0.005f)) - Curve(Mathf.Max(0f, t - 0.005f))).normalized;
                right = Quaternion.FromToRotation(priorTangent, tangent) * right;
                priorTangent = tangent;
                Vector3 up = Vector3.Cross(right, tangent).normalized;
                float branch = Mathf.SmoothStep(0f, 1f, u / 0.28f);
                Vector3 center = Curve(t);
                if (k > 0)
                    center += (right * Mathf.Cos(phase) + up * Mathf.Sin(phase))
                        * (Radius(u) * 0.58f * branch + 0.018f * scale * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.65f, 1f, u)));
                float wave = Mathf.Sin(u * Mathf.PI) * branch * scale * (k == 0 ? 0.006f : 0.016f);
                center += (right * Mathf.Sin(u * Mathf.PI * 3f + phase)
                    + up * Mathf.Cos(u * Mathf.PI * 3f + phase) * 0.5f) * wave;
                float radius = LockRadius(k, u);
                if (r > 0 && u > 0.18f)
                    for (int pass = 0; pass < 3; pass++) Collide(ref center, radius + 0.004f * scale);
                foreach (var cap in capsules)
                    MaxBodyPenetration = Mathf.Max(MaxBodyPenetration, cap.radius + radius
                        - Vector3.Distance(center, Closest(cap, center)));
                lockCenters[r] = center;
            }
            priorTangent = (lockCenters[1] - lockCenters[0]).normalized;
            right = Vector3.ProjectOnPlane(anchor.right, priorTangent).normalized;
            for (int r = 0; r < Rings; r++)
            {
                float u = r / (float)(Rings - 1);
                Vector3 tangent = (lockCenters[Mathf.Min(Rings - 1, r + 1)] - lockCenters[Mathf.Max(0, r - 1)]).normalized;
                // Parallel transport prevents the cross-section flipping when the tail swings sideways.
                right = (Quaternion.FromToRotation(priorTangent, tangent) * right).normalized;
                priorTangent = tangent;
                Vector3 up = Vector3.Cross(right, tangent).normalized;
                for (int s = 0; s <= Sides; s++)
                {
                    float theta = s / (float)Sides * Mathf.PI * 2f;
                    Vector3 p = lockCenters[r] + (right * Mathf.Cos(theta) + up * Mathf.Sin(theta) * 0.92f) * LockRadius(k, u);
                    vertices[(k * Rings + r) * (Sides + 1) + s] = transform.InverseTransformPoint(p);
                }
            }
        }
        tailMesh.vertices = vertices;
        tailMesh.RecalculateNormals();
        tailMesh.RecalculateBounds();
    }

    float LockRadius(int index, float t)
    {
        float branch = Mathf.SmoothStep(0f, 1f, t / 0.28f);
        return Radius(t) * (index == 0 ? Mathf.Lerp(1f, 0.72f, branch) : 0.42f);
    }

    public void SetFirstPerson(bool value)
    {
        firstPerson = value;
        foreach (var go in pieces)
        {
            var renderer = go ? go.GetComponent<Renderer>() : null;
            if (renderer) renderer.shadowCastingMode = value ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
        }
    }

    void OnDestroy()
    {
        foreach (var go in pieces) if (go) { if (Application.isPlaying) Destroy(go); else DestroyImmediate(go); }
        foreach (var mesh in meshes) if (mesh) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
        if (fallbackMaterial) { if (Application.isPlaying) Destroy(fallbackMaterial); else DestroyImmediate(fallbackMaterial); }
    }
}
