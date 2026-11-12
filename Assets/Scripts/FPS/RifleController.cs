using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// First-person assault rifle: full auto, infinite ammo, hip fire and aim-down-sights (red dot),
/// procedural recoil / sway / bob, muzzle flash, tracers, impacts and sounds.
/// LMB fire, RMB aim (hold).
/// </summary>
public class RifleController : MonoBehaviour
{
    [Header("References")]
    public FirstPersonController player;
    public Camera mainCamera;
    public Camera viewModelCamera;
    public Transform weaponRoot;            // child of the camera; the equipped rifle is placed here
    public GameObject riflePrefab;

    [Header("First-person arms")]
    public GameObject armsCharacterPrefab;          // the CatGirl model; only her arms are shown
    public RuntimeAnimatorController armsController;
    public Avatar armsAvatar;
    public float armsCharacterHeight = 2.8f;        // larger than her real 1.75 m: first-person arms are exaggerated so they reach the foregrip
    public Vector3 shoulderPosition = new Vector3(0f, -0.28f, 0.15f);    // shoulder midpoint relative to the eye (below the view)
    public float gunScale = 0.85f;

    [Header("Sounds")]
    public AudioClip[] shotClips;
    public AudioClip targetHitClip;
    public AudioClip impactClip;
    public AudioClip pickupClip;

    [Header("Firing")]
    public float roundsPerMinute = 700f;
    public float range = 300f;
    public float hipSpread = 2.2f;          // degrees
    public float aimSpread = 0.12f;
    public float moveSpreadBonus = 1.5f;

    [Header("Aiming")]
    public float hipFov = 70f;
    public float aimFov = 45f;
    public float aimTime = 0.14f;
    [Tooltip("The weapon is drawn by its own camera with a narrower field of view: bigger, less distorted gun")]
    public float viewModelHipFov = 50f;
    public float viewModelAimFov = 38f;
    public Vector3 hipRotation = new Vector3(2f, -6f, -6f);     // slight inward yaw and roll at the hip
    // The rifle's origin sits on its sight line, so aimPosition = (0, 0, eye relief) lines the red dot up with the screen centre.
    public Vector3 hipPosition = new Vector3(0.16f, -0.12f, 0.40f);
    public Vector3 aimPosition = new Vector3(0f, 0f, 0.34f);

    [Header("Recoil")]
    public float cameraKick = 1.1f;         // degrees up, springs back
    public float cameraSideKick = 0.4f;
    public float cameraClimb = 0.16f;       // degrees up that stays (pull down to control)
    public float gunKickBack = 0.05f;       // metres
    public float gunKickUp = 5f;            // degrees
    public float aimRecoilMultiplier = 0.45f;

    public bool Equipped { get; private set; }
    public float Aim { get; private set; }
    public float CurrentSpread { get; private set; }
    public int Shots { get; private set; }
    public int Hits { get; private set; }
    public float HitMarker { get; private set; }

    Transform gun, muzzle;
    float nextShot;
    Vector3 kickPos, kickPosVel, kickRot, kickRotVel;
    Vector2 sway;
    float bobPhase, sprintBlend, slideBlend;
    int shotIndex;
    AudioSource audio2D;
    Light flashLight;
    Transform flashQuad;
    LineRenderer tracer;
    float flashTimer, tracerTimer, flashRoll;
    ParticleSystem sparks;
    readonly Queue<GameObject> holes = new Queue<GameObject>();
    int viewModelLayer;
    int hitMask;

    void Awake()
    {
        viewModelLayer = LayerMask.NameToLayer("ViewModel");
        if (viewModelLayer < 0) viewModelLayer = 8;
        hitMask = ~((1 << viewModelLayer) | (1 << 2));   // ignore view model + "Ignore Raycast" (the player)
        audio2D = gameObject.AddComponent<AudioSource>();
        audio2D.playOnAwake = false;
        audio2D.spatialBlend = 0f;
        BuildEffects();
    }

    public void Equip()
    {
        if (Equipped || riflePrefab == null) return;
        gun = Instantiate(riflePrefab, weaponRoot).transform;
        gun.name = "Rifle (view model)";
        gun.localPosition = hipPosition;
        gun.localRotation = Quaternion.identity;
        gun.localScale = Vector3.one * gunScale;
        foreach (var t in gun.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = viewModelLayer;
        foreach (var r in gun.GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        foreach (var c in gun.GetComponentsInChildren<Collider>(true)) Destroy(c);
        muzzle = FindDeep(gun, "Muzzle") ?? gun;
        var dotMarker = FindDeep(gun, "RedDot");
        if (dotMarker != null) CreateRedDot(dotMarker);
        flashQuad.SetParent(muzzle, false);
        flashLight.transform.SetParent(muzzle, false);
        if (armsCharacterPrefab != null)
        {
            arms = FirstPersonArms.Spawn(armsCharacterPrefab, armsController, armsAvatar, mainCamera.transform, viewModelLayer, armsCharacterHeight, shoulderPosition);
            arms.gripRight = FindDeep(gun, "GripRight");
            arms.gripLeft = FindDeep(gun, "GripLeft");
        }
        Equipped = true;
        if (pickupClip) audio2D.PlayOneShot(pickupClip, 0.8f);
    }

    FirstPersonArms arms;

    /// <summary>Throws the held rifle forward as a physics object that can be picked up again (E).</summary>
    public void Throw()
    {
        if (!Equipped) return;
        // keep the muzzle effects alive, they are re-attached on the next pick-up
        flashQuad.SetParent(null, false); flashQuad.gameObject.SetActive(false);
        flashLight.transform.SetParent(null, false); flashLight.enabled = false;

        var cam = mainCamera.transform;
        var thrown = Instantiate(riflePrefab, gun.position + cam.forward * 0.25f, gun.rotation);
        thrown.name = "AssaultRifle (thrown)";
        foreach (var t in thrown.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
        var mr = thrown.GetComponentInChildren<MeshRenderer>();
        var col = mr.gameObject.AddComponent<BoxCollider>();
        var body = thrown.AddComponent<Rigidbody>();
        body.mass = 3.5f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearVelocity = cam.forward * 7f + Vector3.up * 2.2f + player.PlanarVelocity;
        body.angularVelocity = cam.right * Random.Range(-4f, -2f) + cam.up * Random.Range(-3f, 3f);
        var cc = player.GetComponent<CharacterController>();
        if (cc) Physics.IgnoreCollision(col, cc);
        var pickup = thrown.AddComponent<WeaponPickup>();
        pickup.landClip = impactClip;

        Destroy(gun.gameObject);
        if (arms) Destroy(arms.gameObject);
        gun = null; muzzle = null; arms = null;
        Equipped = false;
        Aim = 0f;
        player.isAiming = false;
        mainCamera.fieldOfView = hipFov; viewModelCamera.fieldOfView = viewModelHipFov;
        if (pickupClip) audio2D.PlayOneShot(pickupClip, 0.6f);
    }

    /// <summary>Places the rifle in its hip (0) or aimed (1) pose right away (used for editor snapshots).</summary>
    public void SetPoseImmediate(float aim)
    {
        Aim = aim;
        float s = Smooth(aim);
        if (gun) { gun.localPosition = Vector3.Lerp(hipPosition, aimPosition, s); gun.localRotation = Quaternion.Euler(Vector3.Lerp(hipRotation, Vector3.zero, s)); }
        if (mainCamera) mainCamera.fieldOfView = Mathf.Lerp(hipFov, aimFov, s);
        if (viewModelCamera) viewModelCamera.fieldOfView = Mathf.Lerp(viewModelHipFov, viewModelAimFov, s);
    }

    void Update()
    {
        HitMarker = Mathf.Max(0f, HitMarker - Time.deltaTime * 6f);
        UpdateEffects();
        if (!Equipped) return;

        bool locked = Cursor.lockState == CursorLockMode.Locked;
        bool wantAim = locked && Input.GetMouseButton(1) && !player.IsSprinting;
        Aim = Mathf.MoveTowards(Aim, wantAim ? 1f : 0f, Time.deltaTime / aimTime);
        float s = Smooth(Aim);
        player.isAiming = Aim > 0.5f;
        mainCamera.fieldOfView = Mathf.Lerp(hipFov, aimFov, s);
        viewModelCamera.fieldOfView = Mathf.Lerp(viewModelHipFov, viewModelAimFov, s);

        float moving = Mathf.Clamp01(player.PlanarVelocity.magnitude / 4.5f);
        CurrentSpread = Mathf.Lerp(hipSpread + moveSpreadBonus * moving, aimSpread + moveSpreadBonus * 0.2f * moving, s);

        if (locked && Input.GetKeyDown(KeyCode.G)) { Throw(); return; }
        if (locked && Input.GetMouseButton(0) && !player.IsSprinting && Time.time >= nextShot) Fire();

        // ---- procedural motion: recoil springs, sway, bob, sprint pose ----
        kickPos = Vector3.SmoothDamp(kickPos, Vector3.zero, ref kickPosVel, 0.07f);
        kickRot = Vector3.SmoothDamp(kickRot, Vector3.zero, ref kickRotVel, 0.08f);

        float sw = locked ? 1f : 0f;
        Vector2 mouse = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * sw;
        sway = Vector2.Lerp(sway, Vector2.ClampMagnitude(mouse, 6f), 1f - Mathf.Exp(-10f * Time.deltaTime));
        float swayAmt = Mathf.Lerp(1.2f, 0.25f, s);

        float speed = player.PlanarVelocity.magnitude;
        if (player.IsGrounded) bobPhase += Time.deltaTime * speed * 1.9f;
        float bobAmt = Mathf.Clamp01(speed / 7.5f) * Mathf.Lerp(1f, 0.15f, s);
        Vector3 bob = new Vector3(Mathf.Sin(bobPhase) * 0.012f, -Mathf.Abs(Mathf.Cos(bobPhase)) * 0.010f, 0f) * bobAmt;

        sprintBlend = Mathf.MoveTowards(sprintBlend, player.IsSprinting ? 1f : 0f, Time.deltaTime * 5f);
        Vector3 sprintPos = new Vector3(-0.06f, -0.05f, -0.04f) * sprintBlend;
        Vector3 sprintRot = new Vector3(18f, -35f, 10f) * sprintBlend;

        gun.localPosition = Vector3.Lerp(hipPosition, aimPosition, s) + kickPos + bob + sprintPos;
        slideBlend = Mathf.MoveTowards(slideBlend, player.IsSliding ? 1f : 0f, Time.deltaTime * 6f);
        Vector3 slideRot = new Vector3(0f, 0f, 10f) * slideBlend * (1f - s * 0.7f);
        gun.localRotation = Quaternion.Euler(Vector3.Lerp(hipRotation, Vector3.zero, s) + kickRot + new Vector3(sway.y * swayAmt, -sway.x * swayAmt, -sway.x * swayAmt * 0.6f) + sprintRot + slideRot);
    }

    void Fire()
    {
        nextShot = Time.time + 60f / roundsPerMinute;
        Shots++;
        float k = Mathf.Lerp(1f, aimRecoilMultiplier, Smooth(Aim));

        // sound
        if (shotClips != null && shotClips.Length > 0)
        {
            audio2D.pitch = Random.Range(0.94f, 1.06f);
            audio2D.PlayOneShot(shotClips[shotIndex++ % shotClips.Length], 0.9f);
        }

        // hitscan from the screen centre with spread
        Vector2 cone = Random.insideUnitCircle * CurrentSpread;
        Vector3 dir = mainCamera.transform.rotation * Quaternion.Euler(-cone.y, cone.x, 0f) * Vector3.forward;
        Vector3 origin = mainCamera.transform.position;
        Vector3 end = origin + dir * range;
        if (Physics.Raycast(origin, dir, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
        {
            end = hit.point;
            var target = hit.collider.GetComponentInParent<ShootingTarget>();
            if (target != null)
            {
                target.Hit(hit.point, dir);
                Hits++;
                HitMarker = 1f;
                if (targetHitClip) audio2D.PlayOneShot(targetHitClip, 0.75f);
                EmitSparks(hit.point, hit.normal, 14, new Color(1f, 0.85f, 0.4f));
            }
            else
            {
                if (impactClip) AudioSource.PlayClipAtPoint(impactClip, hit.point, 0.8f);
                EmitSparks(hit.point, hit.normal, 7, new Color(0.8f, 0.75f, 0.65f));
            }
            AddBulletHole(hit);
            if (hit.rigidbody) hit.rigidbody.AddForceAtPosition(dir * 4f, hit.point, ForceMode.Impulse);
        }

        // recoil
        player.AddRecoil(cameraKick * k, Random.Range(-cameraSideKick, cameraSideKick) * k, cameraClimb * k);
        kickPos += new Vector3(Random.Range(-0.004f, 0.004f), 0.004f * k, -gunKickBack * k);
        kickRot += new Vector3(-gunKickUp * k, Random.Range(-1.2f, 1.2f) * k, Random.Range(-2f, 2f) * k);

        // muzzle flash + tracer
        flashTimer = 0.045f;
        flashRoll = Random.Range(0f, 360f);
        flashQuad.localScale = Vector3.one * Random.Range(0.14f, 0.22f);
        tracerTimer = 0.035f;
        tracer.SetPosition(0, muzzle.position);
        tracer.SetPosition(1, end);
    }

    // ---------------- effects ----------------
    void BuildEffects()
    {
        flashQuad = GameObject.CreatePrimitive(PrimitiveType.Quad).transform;
        Destroy(flashQuad.GetComponent<Collider>());
        flashQuad.name = "MuzzleFlash";
        flashQuad.gameObject.layer = viewModelLayer;
        var fm = FxMaterials.Additive(RadialTexture(), new Color(1f, 0.72f, 0.32f, 1f) * 2.5f);   // HDR: blooms with post-processing
        var fr = flashQuad.GetComponent<Renderer>();
        fr.sharedMaterial = fm;
        fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        flashQuad.gameObject.SetActive(false);

        var lightGo = new GameObject("MuzzleLight");
        flashLight = lightGo.AddComponent<Light>();
        flashLight.type = LightType.Point; flashLight.range = 6f; flashLight.intensity = 3.5f;
        flashLight.color = new Color(1f, 0.75f, 0.45f);
        flashLight.enabled = false;

        var tr = new GameObject("Tracer");
        tracer = tr.AddComponent<LineRenderer>();
        tracer.positionCount = 2; tracer.useWorldSpace = true;
        tracer.startWidth = 0.012f; tracer.endWidth = 0.004f;
        tracer.sharedMaterial = FxMaterials.Additive(null, new Color(1f, 0.85f, 0.55f, 1f) * 2f);
        tracer.startColor = new Color(1f, 0.9f, 0.6f, 0.9f); tracer.endColor = new Color(1f, 0.8f, 0.5f, 0.2f);
        tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tracer.enabled = false;

        var sp = new GameObject("Sparks");
        sparks = sp.AddComponent<ParticleSystem>();
        sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = sparks.main;
        main.playOnAwake = false; main.loop = false; main.maxParticles = 600;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 1.2f;
        var em = sparks.emission; em.enabled = false;
        var shape = sparks.shape; shape.enabled = false;
        var pr = sp.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = FxMaterials.Additive(RadialTexture(), Color.white * 2.5f);
        pr.renderMode = ParticleSystemRenderMode.Stretch; pr.velocityScale = 0.04f; pr.lengthScale = 1f;
        sparks.Play();
    }

    void UpdateEffects()
    {
        flashTimer -= Time.deltaTime;
        bool flash = flashTimer > 0f;
        if (flashQuad.gameObject.activeSelf != flash) flashQuad.gameObject.SetActive(flash);
        if (flash && mainCamera) flashQuad.rotation = Quaternion.LookRotation(mainCamera.transform.forward) * Quaternion.Euler(0f, 0f, flashRoll);
        flashLight.enabled = flash;
        tracerTimer -= Time.deltaTime;
        tracer.enabled = tracerTimer > 0f;
    }

    void EmitSparks(Vector3 pos, Vector3 normal, int count, Color color)
    {
        var ep = new ParticleSystem.EmitParams();
        for (int i = 0; i < count; i++)
        {
            ep.position = pos + normal * 0.01f;
            ep.velocity = (normal + Random.insideUnitSphere * 0.8f).normalized * Random.Range(2f, 6f);
            ep.startLifetime = Random.Range(0.12f, 0.35f);
            ep.startSize = Random.Range(0.015f, 0.035f);
            ep.startColor = color;
            sparks.Emit(ep, 1);
        }
    }

    void AddBulletHole(RaycastHit hit)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(q.GetComponent<Collider>());
        q.name = "BulletHole";
        q.transform.SetPositionAndRotation(hit.point + hit.normal * 0.003f, Quaternion.LookRotation(-hit.normal) * Quaternion.Euler(0, 0, Random.Range(0f, 360f)));
        q.transform.localScale = Vector3.one * 0.035f;
        q.transform.SetParent(hit.collider.transform, true);
        var r = q.GetComponent<Renderer>();
        r.sharedMaterial = HoleMaterial();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        holes.Enqueue(q);
        while (holes.Count > 80) { var old = holes.Dequeue(); if (old) Destroy(old); }
    }

    // ---------------- helpers ----------------
    void CreateRedDot(Transform marker)
    {
        var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(dot.GetComponent<Collider>());
        dot.name = "RedDotReticle";
        dot.layer = viewModelLayer;
        dot.transform.SetParent(marker, false);
        dot.transform.localScale = Vector3.one * 0.0024f;
        var m = FxMaterials.Unlit(new Color(1f, 0.05f, 0.05f) * 3f);
        var r = dot.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static Material holeMat;
    static Material HoleMaterial()
    {
        if (holeMat == null)
        {
            holeMat = FxMaterials.AlphaBlended(RadialTexture(), new Color(0.03f, 0.03f, 0.03f, 0.95f));
        }
        return holeMat;
    }

    static Texture2D radial;
    static Texture2D RadialTexture()
    {
        if (radial != null) return radial;
        const int n = 64;
        radial = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(n / 2f - 0.5f, n / 2f - 0.5f)) / (n / 2f);
                float a = Mathf.Clamp01(1f - d); a = a * a;
                radial.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        radial.Apply();
        return radial;
    }

    static float Smooth(float t) => t * t * (3f - 2f * t);

    static Transform FindDeep(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }
}
