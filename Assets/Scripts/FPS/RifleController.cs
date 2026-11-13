using System.Collections.Generic;
using UnityEngine;

/// <summary>Everything that differs between the guns: model, fire mode, handling, recoil, sounds, slide and hand poses.</summary>
[System.Serializable]
public class WeaponProfile
{
    public string displayName = "Assault Rifle";
    public GameObject prefab;
    public float scale = 0.85f;

    [Header("Firing")]
    public bool automatic = true;
    public float roundsPerMinute = 700f;
    public float hipSpread = 2.2f;          // degrees
    public float aimSpread = 0.12f;
    public float moveSpreadBonus = 1.5f;

    [Header("Aiming")]
    public float aimFov = 45f;
    [Tooltip("The weapon is drawn by its own camera with a narrower field of view: bigger, less distorted gun")]
    public float viewModelHipFov = 50f;
    public float viewModelAimFov = 38f;
    public Vector3 hipRotation = new Vector3(2f, -6f, -6f);     // slight inward yaw and roll at the hip
    // The gun's origin sits on its sight line, so aimPosition = (0, 0, eye relief) lines the sight up with the screen centre.
    public Vector3 hipPosition = new Vector3(0.16f, -0.12f, 0.40f);
    public Vector3 aimPosition = new Vector3(0f, 0f, 0.34f);
    public Vector3 sprintPosition = new Vector3(-0.06f, -0.05f, -0.04f);
    public Vector3 sprintRotation = new Vector3(18f, -35f, 10f);

    [Header("Recoil")]
    public float cameraKick = 1.1f;         // degrees up, springs back
    public float cameraSideKick = 0.4f;
    public float cameraClimb = 0.16f;       // degrees up that stays (pull down to control)
    public float gunKickBack = 0.05f;       // metres
    public float gunKickUp = 5f;            // degrees
    public float aimRecoilMultiplier = 0.45f;

    [Header("Sounds and flash")]
    public AudioClip[] shotClips;
    public float shotVolume = 0.9f;
    public float shotPitch = 1f;
    public float flashScale = 1f;

    [Header("Slide blow-back (child named 'Slide', optional 'Barrel')")]
    public float slideTravel = 0f;          // metres the slide runs back on each shot
    public float barrelTravel = 0f;
    public float slideBackTime = 0.025f;
    public float slideReturnTime = 0.07f;

    [Header("Hands (weapon space: x right, y up, z forward)")]
    public Vector3 rightFingers = new Vector3(-0.15f, -0.55f, 0.8f);
    public Vector3 rightPalm = new Vector3(-1f, 0f, 0f);
    public Vector3 rightElbowHint = new Vector3(0.45f, -0.55f, 0.05f);     // camera space
    public Vector3 leftFingers = new Vector3(0.35f, -0.55f, 0.75f);
    public Vector3 leftPalm = new Vector3(1f, 0f, 0f);
    public Vector3 leftElbowHint = new Vector3(-0.5f, -0.6f, 0.25f);
    public Vector3 leftGripOffset = Vector3.zero;                           // moves the GripLeft marker (weapon space, metres)
    [Tooltip("Degrees each finger bends past the rest pose: thumb, index, middle, ring, pinky")]
    public float[] rightCurl = { 10f, 30f, 75f, 80f, 80f };
    public float[] leftCurl = { 10f, 70f, 70f, 70f, 70f };

    [Header("Magazine and reload (0 = infinite ammo, no reload; children 'Magazine', 'MagTop', 'MagBottom'; empty reload: overhand rack of a 'Slide' or a palm on the 'BoltCatch')")]
    public int magazineSize = 0;
    public float reloadTime = 1.35f;        // tactical: mag out, new mag in, palm slap
    public float emptyReloadTime = 1.75f;   // empty: the same plus an overhand rack, slide locked back until then
    public bool lockSlideWhenEmpty = true;
    public Vector3 reloadPosition = new Vector3(-0.05f, 0.04f, -0.01f);
    public Vector3 reloadRotation = new Vector3(-18f, -8f, -32f);       // muzzle up, top rolled right so the mag well faces the support hand
    public AudioClip magOutClip, magInClip, slideReleaseClip;
    public AudioClip grabClip, slapClip;                                 // hand goes for the new magazine; palm slap

    [Header("Arms")]
    [Tooltip("First-person arm scale for this weapon (0 = the controller's armsCharacterHeight)")]
    public float armsHeight = 0f;

    [Header("Thrown")]
    public float mass = 3.5f;
}

/// <summary>
/// First-person weapons: carries up to one of each profile (1 rifle, 2 pistol, 3 holster, mouse wheel cycles),
/// hip fire and aim-down-sights, procedural recoil / sway / bob, animated pistol slide, muzzle flash, tracers,
/// impacts and sounds. LMB fire, RMB aim (hold), G throw the held weapon.
/// </summary>
public class RifleController : MonoBehaviour
{
    [Header("References")]
    public FirstPersonController player;
    public Camera mainCamera;
    public Camera viewModelCamera;
    public Transform weaponRoot;            // child of the camera; the equipped gun is placed here

    [Header("Weapons (slot 1, 2, ...; the slot after the last one holsters)")]
    public List<WeaponProfile> weapons = new List<WeaponProfile>();

    [Header("First-person arms")]
    public GameObject armsCharacterPrefab;          // CatGirl_FPArms: her arms with working fingers
    public GameObject armsMaterialSource;           // CatGirl: the arms take her materials
    public RuntimeAnimatorController armsController;
    public Avatar armsAvatar;
    public float armsCharacterHeight = 2.8f;        // larger than her real 1.75 m: first-person arms are exaggerated so they reach the foregrip
    public Vector3 shoulderPosition = new Vector3(0f, -0.28f, 0.15f);    // shoulder midpoint relative to the eye (below the view)

    [Header("Sounds")]
    public AudioClip targetHitClip;
    public AudioClip impactClip;
    public AudioClip pickupClip;

    [Header("Shared handling")]
    public float range = 300f;
    public float hipFov = 70f;
    public float aimTime = 0.14f;
    public float drawTime = 0.28f;

    public bool Equipped => current >= 0 && gun != null;
    public WeaponProfile Current => current >= 0 && current < weapons.Count ? weapons[current] : null;
    public int CurrentSlot => current;
    public bool Owns(int slot) => slot >= 0 && slot < owned.Count && owned[slot];
    public float Aim { get; private set; }
    /// <summary>Rounds in the held weapon (magazine + chamber); -1 = infinite.</summary>
    public int Ammo => Current == null ? 0 : Current.magazineSize <= 0 ? -1 : ammo[current];
    public bool Reloading => reloadT >= 0f;
    public float CurrentSpread { get; private set; }
    public int Shots { get; private set; }
    public int Hits { get; private set; }
    public float HitMarker { get; private set; }
    /// <summary>Shot clips of the held weapon (the first weapon when nothing is held).</summary>
    /// <summary>Editor checks: holds the aim blend at this value (negative = normal right-mouse aiming).</summary>
    [System.NonSerialized] public float forcedAim = -1f;
    public AudioClip[] shotClips => (Current ?? (weapons.Count > 0 ? weapons[0] : null))?.shotClips ?? new AudioClip[0];

    int current = -1;
    readonly List<bool> owned = new List<bool>();
    readonly List<int> ammo = new List<int>();
    float reloadT = -1f;
    bool reloadEmpty, slideLocked, magDropped, magSeated;
    Transform mag, magRound, gripLeftMarker, leftHandTarget, boltCatch;
    Vector3 magRest, magBottomRest, magAxis;        // gun space; axis points out of the mag well
    Quaternion magRestRot;
    Transform gun, muzzle, slide, barrel;
    Vector3 slideRest, barrelRest;          // in gun space
    float slideTime = 99f, draw;
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
    FirstPersonArms arms;

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

    void SyncOwned()
    {
        while (owned.Count < weapons.Count) owned.Add(false);
        while (ammo.Count < weapons.Count) ammo.Add(weapons[ammo.Count].magazineSize);
    }

    /// <summary>Gives and draws the first weapon (the rifle).</summary>
    public void Equip() => Give(0);

    /// <summary>Adds the weapon in this slot and draws it.</summary>
    public void Give(int slot)
    {
        SyncOwned();
        if (slot < 0 || slot >= weapons.Count || weapons[slot].prefab == null) return;
        owned[slot] = true;
        ammo[slot] = weapons[slot].magazineSize;
        if (pickupClip) audio2D.PlayOneShot(pickupClip, 0.8f);
        Select(slot);
    }

    /// <summary>Draws an owned weapon, or holsters with -1.</summary>
    public void Select(int slot)
    {
        SyncOwned();
        if (slot >= 0 && !Owns(slot)) return;
        if (slot == current && (slot < 0 || gun != null)) return;
        Holster();
        if (slot < 0) return;
        current = slot;
        Spawn(weapons[slot]);
    }

    void Holster()
    {
        DetachEffects();
        if (gun) Destroy(gun.gameObject);
        if (arms) Destroy(arms.gameObject);
        gun = muzzle = slide = barrel = mag = magRound = gripLeftMarker = leftHandTarget = boltCatch = null; arms = null;
        current = -1;
        reloadT = -1f;
        Aim = 0f;
        if (player) player.isAiming = false;
        if (mainCamera) mainCamera.fieldOfView = hipFov;
    }

    void Spawn(WeaponProfile w)
    {
        gun = Instantiate(w.prefab, weaponRoot).transform;
        gun.name = w.displayName + " (view model)";
        gun.localPosition = w.hipPosition;
        gun.localRotation = Quaternion.identity;
        gun.localScale = Vector3.one * w.scale;
        foreach (var t in gun.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = viewModelLayer;
        foreach (var r in gun.GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        foreach (var c in gun.GetComponentsInChildren<Collider>(true)) Destroy(c);
        muzzle = FindDeep(gun, "Muzzle") ?? gun;
        slide = FindDeep(gun, "Slide");
        barrel = FindDeep(gun, "Barrel");
        if (slide) slideRest = gun.InverseTransformPoint(slide.position);
        if (barrel) barrelRest = gun.InverseTransformPoint(barrel.position);
        slideTime = 99f;
        var dotMarker = FindDeep(gun, "RedDot");
        if (dotMarker != null) CreateRedDot(dotMarker);
        flashQuad.SetParent(muzzle, false);
        flashLight.transform.SetParent(muzzle, false);
        gripLeftMarker = FindDeep(gun, "GripLeft");
        if (gripLeftMarker) gripLeftMarker.position += gun.TransformVector(w.leftGripOffset / Mathf.Max(0.0001f, w.scale));
        // the support hand follows this target: the grip normally, the magazine and the slide while reloading
        leftHandTarget = new GameObject("LeftHandTarget").transform;
        leftHandTarget.SetParent(gun, false);
        if (gripLeftMarker) leftHandTarget.SetPositionAndRotation(gripLeftMarker.position, gripLeftMarker.rotation);
        mag = FindDeep(gun, "Magazine");
        var magTop = FindDeep(gun, "MagTop"); var magBottom = FindDeep(gun, "MagBottom");
        if (mag && magTop && magBottom)
        {
            magRest = gun.InverseTransformPoint(mag.position);
            magRestRot = mag.localRotation;
            magBottomRest = gun.InverseTransformPoint(magBottom.position);
            magAxis = (magBottomRest - gun.InverseTransformPoint(magTop.position)).normalized;
        }
        else mag = null;
        magRound = mag ? FindDeep(mag, "MagRound") : null;
        boltCatch = FindDeep(gun, "BoltCatch");
        if (magRound) magRound.gameObject.SetActive(false);       // only seen while the magazine is out of the grip
        reloadT = -1f;
        slideLocked = w.magazineSize > 0 && w.lockSlideWhenEmpty && ammo[current] == 0;
        if (armsCharacterPrefab != null)
        {
            arms = FirstPersonArms.Spawn(armsCharacterPrefab, armsController, armsAvatar, mainCamera.transform, viewModelLayer, w.armsHeight > 0f ? w.armsHeight : armsCharacterHeight, shoulderPosition, armsMaterialSource);
            ApplyHands(arms, w);
            arms.gripRight = FindDeep(gun, "GripRight");
            arms.gripLeft = gripLeftMarker ? leftHandTarget : null;
        }
        draw = Application.isPlaying ? 1f : 0f;
        kickPos = kickRot = kickPosVel = kickRotVel = Vector3.zero;
    }

    public static void ApplyHands(FirstPersonArms a, WeaponProfile w)
    {
        a.rightFingers = w.rightFingers; a.rightPalm = w.rightPalm; a.rightElbowHint = w.rightElbowHint;
        a.leftFingers = w.leftFingers; a.leftPalm = w.leftPalm; a.leftElbowHint = w.leftElbowHint;
        CopyCurl(w.rightCurl, ref a.rightCurl);
        CopyCurl(w.leftCurl, ref a.leftCurl);
    }

    static void CopyCurl(float[] from, ref float[] to)
    {
        if (from == null || from.Length != 5) return;
        if (to == null || to.Length != 5) to = new float[5];
        System.Array.Copy(from, to, 5);
    }

    void DetachEffects()
    {
        // keep the muzzle effects alive, they are re-attached to the next gun
        flashQuad.SetParent(null, false); flashQuad.gameObject.SetActive(false);
        flashLight.transform.SetParent(null, false); flashLight.enabled = false;
    }

    /// <summary>Throws the held weapon forward as a physics object that can be picked up again (E).</summary>
    public void Throw()
    {
        if (!Equipped) return;
        var w = Current; int slot = current;
        var cam = mainCamera.transform;
        var thrown = Instantiate(w.prefab, gun.position + cam.forward * 0.25f, gun.rotation);
        thrown.name = w.displayName + " (thrown)";
        thrown.transform.localScale = Vector3.one * w.scale;
        foreach (var t in thrown.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
        var col = AddBoundsCollider(thrown);
        var body = thrown.AddComponent<Rigidbody>();
        body.mass = w.mass;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearVelocity = cam.forward * 7f + Vector3.up * 2.2f + player.PlanarVelocity;
        body.angularVelocity = cam.right * Random.Range(-4f, -2f) + cam.up * Random.Range(-3f, 3f);
        var cc = player.GetComponent<CharacterController>();
        if (cc) Physics.IgnoreCollision(col, cc);
        var pickup = thrown.AddComponent<WeaponPickup>();
        pickup.landClip = impactClip;
        pickup.weaponSlot = slot;
        pickup.displayName = w.displayName;

        owned[slot] = false;
        Holster();
        if (pickupClip) audio2D.PlayOneShot(pickupClip, 0.6f);
        int next = NextOwned(slot, 1);
        if (next >= 0) Select(next);
    }

    /// <summary>One box around all of a model's renderers, on the root (in its local space).</summary>
    public static BoxCollider AddBoundsCollider(GameObject go)
    {
        var col = go.AddComponent<BoxCollider>();
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return col;
        var t = go.transform;
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        foreach (var r in rs)
        {
            var mf = r.GetComponent<MeshFilter>();
            if (!mf || !mf.sharedMesh) continue;
            var b = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var p = t.InverseTransformPoint(r.transform.TransformPoint(c));
                min = Vector3.Min(min, p); max = Vector3.Max(max, p);
            }
        }
        if (min.x <= max.x) { col.center = (min + max) * 0.5f; col.size = max - min; }
        return col;
    }

    int NextOwned(int from, int dir)
    {
        SyncOwned();
        int n = weapons.Count;
        for (int i = 1; i <= n; i++)
        {
            int s = ((from + dir * i) % n + n) % n;
            if (owned[s]) return s;
        }
        return -1;
    }

    /// <summary>Places the gun in its hip (0) or aimed (1) pose right away (used for editor snapshots).</summary>
    public void SetPoseImmediate(float aim)
    {
        Aim = aim;
        draw = 0f;
        float s = Smooth(aim);
        var w = Current ?? (weapons.Count > 0 ? weapons[0] : null);
        if (w == null) return;
        if (gun) { gun.localPosition = Vector3.Lerp(w.hipPosition, w.aimPosition, s); gun.localRotation = Quaternion.Euler(Vector3.Lerp(w.hipRotation, Vector3.zero, s)); }
        if (mainCamera) mainCamera.fieldOfView = Mathf.Lerp(hipFov, w.aimFov, s);
        if (viewModelCamera) viewModelCamera.fieldOfView = Mathf.Lerp(w.viewModelHipFov, w.viewModelAimFov, s);
    }

    void Update()
    {
        HitMarker = Mathf.Max(0f, HitMarker - Time.deltaTime * 6f);
        UpdateEffects();
        bool locked = Cursor.lockState == CursorLockMode.Locked;
        if (locked) HandleSwitching();
        if (!Equipped) return;
        var w = Current;

        bool wantAim = locked && Input.GetMouseButton(1) && !player.IsSprinting && draw < 0.5f && !Reloading;
        Aim = forcedAim >= 0f ? forcedAim : Mathf.MoveTowards(Aim, wantAim ? 1f : 0f, Time.deltaTime / aimTime);
        float s = Smooth(Aim);
        player.isAiming = Aim > 0.5f;
        mainCamera.fieldOfView = Mathf.Lerp(hipFov, w.aimFov, s);
        viewModelCamera.fieldOfView = Mathf.Lerp(w.viewModelHipFov, w.viewModelAimFov, s);

        float moving = Mathf.Clamp01(player.PlanarVelocity.magnitude / 4.5f);
        CurrentSpread = Mathf.Lerp(w.hipSpread + w.moveSpreadBonus * moving, w.aimSpread + w.moveSpreadBonus * 0.2f * moving, s);

        if (locked && Input.GetKeyDown(KeyCode.G)) { Throw(); return; }
        if (locked && Input.GetKeyDown(KeyCode.R)) StartReload();
        bool trigger = w.automatic ? Input.GetMouseButton(0) : Input.GetMouseButtonDown(0);
        if (locked && trigger && !player.IsSprinting && draw < 0.35f && !Reloading && Time.time >= nextShot)
        {
            if (w.magazineSize > 0 && ammo[current] <= 0) StartReload();
            else Fire();
        }
        Vector3 reloadPos, reloadRot;
        UpdateReload(Time.deltaTime, out reloadPos, out reloadRot);

        // ---- procedural motion: recoil springs, sway, bob, sprint pose, draw ----
        kickPos = Vector3.SmoothDamp(kickPos, Vector3.zero, ref kickPosVel, 0.07f);
        kickRot = Vector3.SmoothDamp(kickRot, Vector3.zero, ref kickRotVel, 0.08f);
        draw = Mathf.MoveTowards(draw, 0f, Time.deltaTime / drawTime);
        float d = Smooth(draw);

        Vector2 mouse = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * (locked ? 1f : 0f);
        sway = Vector2.Lerp(sway, Vector2.ClampMagnitude(mouse, 6f), 1f - Mathf.Exp(-10f * Time.deltaTime));
        float swayAmt = Mathf.Lerp(1.2f, 0.25f, s);

        float speed = player.PlanarVelocity.magnitude;
        if (player.IsGrounded) bobPhase += Time.deltaTime * speed * 1.9f;
        float bobAmt = Mathf.Clamp01(speed / 7.5f) * Mathf.Lerp(1f, 0.15f, s);
        Vector3 bob = new Vector3(Mathf.Sin(bobPhase) * 0.012f, -Mathf.Abs(Mathf.Cos(bobPhase)) * 0.010f, 0f) * bobAmt;

        sprintBlend = Mathf.MoveTowards(sprintBlend, player.IsSprinting ? 1f : 0f, Time.deltaTime * 5f);
        Vector3 drawPos = new Vector3(0f, -0.16f, -0.05f) * d;
        Vector3 drawRot = new Vector3(35f, 0f, 10f) * d;

        gun.localPosition = Vector3.Lerp(w.hipPosition, w.aimPosition, s) + kickPos + bob + w.sprintPosition * sprintBlend + drawPos + reloadPos;
        slideBlend = Mathf.MoveTowards(slideBlend, player.IsSliding ? 1f : 0f, Time.deltaTime * 6f);
        Vector3 slideRot = new Vector3(0f, 0f, 10f) * slideBlend * (1f - s * 0.7f);
        gun.localRotation = Quaternion.Euler(Vector3.Lerp(w.hipRotation, Vector3.zero, s) + kickRot + new Vector3(sway.y * swayAmt, -sway.x * swayAmt, -sway.x * swayAmt * 0.6f) + w.sprintRotation * sprintBlend + slideRot + drawRot + reloadRot);
        AnimateSlide(Time.deltaTime);
    }

    void HandleSwitching()
    {
        SyncOwned();
        for (int i = 0; i < 9; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                if (i < weapons.Count) { if (Owns(i)) Select(i); }
                else if (i == weapons.Count) Select(-1);     // the slot after the last weapon holsters
                return;
            }
        float wheel = Input.mouseScrollDelta.y;
        if (Mathf.Abs(wheel) > 0.01f) Cycle(wheel > 0f ? -1 : 1);     // wheel up = previous slot, wheel down = next slot
    }

    /// <summary>Draws the next (dir 1) or previous (dir -1) owned weapon, wrapping around; from the holster it starts at the first / last.</summary>
    public void Cycle(int dir)
    {
        int from = current >= 0 ? current : (dir > 0 ? -1 : weapons.Count);
        int next = NextOwned(from, dir);
        if (next >= 0) Select(next);
    }

    /// <summary>Slide (and a little barrel) runs back fast after a shot and springs home: offset 0..1 of the travel.</summary>
    public static float SlideOffset(float t, float back, float ret)
    {
        if (t < 0f) return 0f;
        if (t < back) { float u = t / back; return 1f - (1f - u) * (1f - u); }
        t -= back;
        if (t < ret) { float u = t / ret; return 1f - u * u * (3f - 2f * u); }
        return 0f;
    }

    void AnimateSlide(float dt)
    {
        var w = Current;
        if (w == null || gun == null || (slide == null && barrel == null)) return;
        slideTime += dt;
        float o = SlideOffset(slideTime, w.slideBackTime, w.slideReturnTime);
        if (slideLocked && slideTime >= w.slideBackTime) o = 1f;          // empty: the slide stays back
        // gun space is in model units; travel is in metres
        float unit = 1f / Mathf.Max(0.0001f, w.scale);
        if (slide) slide.position = gun.TransformPoint(slideRest + Vector3.back * (w.slideTravel * o * unit));
        if (barrel) barrel.position = gun.TransformPoint(barrelRest + Vector3.back * (w.barrelTravel * o * unit));
    }

    // ---------------- reload ----------------
    // Timeline in seconds (empty reloads add an overhand rack after the mag is seated):
    //   0.00-0.20 tilt in | 0.15 mag released, slides out and falls | 0.42 dropped mag becomes a physics object
    //   0.30-0.80 support hand brings the new mag up | 0.80-0.88 palm slap seats it | then rack (empty) | tilt out
    const float MagRelease = 0.15f, MagFree = 0.25f, MagDrop = 0.42f, MagNewStart = 0.42f, MagNear = 0.80f, MagSeat = 0.88f;
    const float RackReach = 1.06f, RackRelease = 1.14f;
    const float NewMagStartDistance = 0.2f, NearDistance = 0.03f;
    static readonly float[] MagHoldCurl = { 20f, 45f, 45f, 45f, 45f };      // fingers around the new magazine
    static readonly float[] RackCurl = { 15f, 80f, 80f, 80f, 75f };         // gripping the rear of the slide
    static readonly float[] SlapCurl = { 5f, 8f, 8f, 10f, 12f };            // flat hand on the bolt catch

    /// <summary>Starts a reload of the held weapon (R, or the trigger on an empty magazine).</summary>
    public void StartReload()
    {
        var w = Current;
        if (w == null || w.magazineSize <= 0 || Reloading || mag == null) return;
        if (ammo[current] >= w.magazineSize + 1) return;          // full magazine + one in the chamber
        reloadEmpty = ammo[current] == 0;
        reloadT = 0f;
        magDropped = magSeated = false;
    }

    void UpdateReload(float dt, out Vector3 pos, out Vector3 rot)
    {
        pos = rot = Vector3.zero;
        var w = Current;
        if (w == null || gun == null) return;
        Vector3 handPos = gripLeftMarker ? gun.InverseTransformPoint(gripLeftMarker.position) : Vector3.zero;
        if (!Reloading)
        {
            if (leftHandTarget && gripLeftMarker) leftHandTarget.SetPositionAndRotation(gripLeftMarker.position, gripLeftMarker.rotation);
            if (arms) ApplyHands(arms, w);
            return;
        }
        float prev = reloadT;
        reloadT += dt;
        float t = reloadT, total = reloadEmpty ? w.emptyReloadTime : w.reloadTime;
        float unit = 1f / Mathf.Max(0.0001f, w.scale);

        // ---- gun pose: tilt in (overshoots and settles), tug as the mag comes out, knocked up by the new mag,
        //      a slow hand drift throughout, tilt out with a small settle ----
        float weight = BackOut(Mathf.Clamp01(t / 0.24f)) * BackOut(Mathf.Clamp01((total - t) / 0.32f));
        pos = w.reloadPosition * weight;
        rot = w.reloadRotation * weight;
        rot += new Vector3(Mathf.Sin(t * 5.3f) * 1.4f, Mathf.Sin(t * 3.7f + 1f) * 1.2f, Mathf.Sin(t * 4.4f + 2f) * 2.2f) * weight;
        float tugOut = Bump(t, MagRelease, 0.14f);
        pos += new Vector3(0.004f, 0.01f, 0f) * tugOut; rot += new Vector3(-3f, 0f, 5f) * tugOut;      // mag pulled down: gun pulled with it, then rebounds
        float slap = Bump(t, MagSeat - 0.03f, 0.13f);
        float ring = Wobble(t, MagSeat, 13f, 0.12f);
        pos += new Vector3(0f, 0.02f, 0f) * slap + new Vector3(0f, 0.004f, 0f) * ring;
        rot += new Vector3(-8f, 0f, -5f) * slap + new Vector3(-1.5f, 0f, 2.5f) * ring;
        if (reloadEmpty && slide)
        {
            float rack = Smooth(Mathf.Clamp01((t - MagSeat) / 0.15f)) * Smooth(Mathf.Clamp01((total - 0.35f - t) / 0.2f));
            rot += new Vector3(-4f, -10f, 14f) * rack;                          // turned further so the hand can come over the top
            float tug = Bump(t, RackRelease - 0.06f, 0.12f);
            pos += new Vector3(0f, 0f, 0.025f) * tug;                            // gun pushed forward against the rack
        }
        else if (reloadEmpty && boltCatch)
        {
            float turn = Smooth(Mathf.Clamp01((t - MagSeat) / 0.15f)) * Smooth(Mathf.Clamp01((total - 0.3f - t) / 0.25f));
            rot += new Vector3(-3f, 6f, 10f) * turn;                             // rolled back a little: the left side faces the hand
            float hit = Bump(t, RackRelease - 0.02f, 0.14f);
            pos += new Vector3(0.012f, 0.004f, 0f) * hit; rot += new Vector3(0f, 0f, -4f) * hit;     // the palm knocks the gun right
        }

        // ---- magazines ----
        float d = 0f; bool magVisible = true;
        if (t < MagRelease) d = 0f;
        else if (t < MagFree) d = 0.07f * Smooth((t - MagRelease) / (MagFree - MagRelease));
        else if (t < MagDrop) { float u = t - MagFree; d = 0.07f + 0.9f * u + 4.9f * u * u; }
        else if (t < MagNewStart) d = NewMagStartDistance;
        else if (t < MagNear) { float u = (t - MagNewStart) / (MagNear - MagNewStart); u = 1f - (1f - u) * (1f - u); d = Mathf.Lerp(NewMagStartDistance, NearDistance, u); }
        else if (t < MagSeat) d = Mathf.Lerp(NearDistance, 0f, Smooth((t - MagNear) / (MagSeat - MagNear)));
        if (prev < MagRelease && t >= MagRelease) Play(w.magOutClip, 0.6f);
        if (prev < 0.26f && t >= 0.26f) Play(w.grabClip, 0.5f);
        if (prev < MagDrop && t >= MagDrop && !magDropped) { DropMagazine(w); magDropped = true; }
        if (t >= MagDrop - 0.0001f && t < MagNewStart + 0.02f) magVisible = false;    // the old mag is gone, the new one is still off screen
        if (!magSeated && t >= MagSeat)
        {
            magSeated = true;
            ammo[current] = reloadEmpty ? w.magazineSize : w.magazineSize + 1;
            Play(w.magInClip, 0.7f);
            Play(w.slapClip, 0.45f);
            if (player) player.AddRecoil(0.5f, Random.Range(-0.2f, 0.2f), 0f);
        }
        // the new magazine swings in from the support side, tilted, and straightens as it enters the well
        float approach = t >= MagNewStart && t < MagSeat ? 1f - Smooth(Mathf.Clamp01((t - MagNewStart) / (MagNear - MagNewStart + 0.04f))) : 0f;
        Vector3 swingIn = new Vector3(-0.07f, -0.02f, -0.03f) * (approach * approach * unit);
        if (mag)
        {
            mag.gameObject.SetActive(magVisible);
            mag.position = gun.TransformPoint(magRest + magAxis * (d * unit) + swingIn);
            mag.localRotation = magRestRot * Quaternion.Euler(0f, 0f, -25f * approach * approach);
            if (magRound) magRound.gameObject.SetActive(d > 0.004f);
        }
        Vector3 magHand = magBottomRest + magAxis * (Mathf.Max(d, t < MagNewStart ? NewMagStartDistance : 0f) * unit) + swingIn;

        // ---- support hand: grip -> new mag -> (slide) -> grip ----
        float toMag = Smooth(Mathf.Clamp01((t - 0.12f) / 0.22f));
        float back = reloadEmpty ? Smooth(Mathf.Clamp01((t - (RackRelease + 0.04f)) / 0.2f)) : Smooth(Mathf.Clamp01((t - (MagSeat + 0.02f)) / 0.22f));
        Vector3 target = Vector3.Lerp(handPos, magHand, toMag);
        target += new Vector3(-0.05f, -0.07f, -0.02f) * (Mathf.Sin(Mathf.PI * toMag) * unit);         // dips out and down toward the pouch
        Vector3 fingers = Vector3.Lerp(w.leftFingers.normalized, new Vector3(0.15f, 0.35f, 1f).normalized, toMag);
        Vector3 palm = Vector3.Lerp(w.leftPalm.normalized, new Vector3(0.2f, 1f, 0f).normalized, toMag);   // palm up under the floor plate
        if (reloadEmpty && slide && !boltCatch)
        {
            // overhand: palm down on the rear of the slide, fingers across it
            float over = Smooth(Mathf.Clamp01((t - (MagSeat + 0.02f)) / (RackReach - MagSeat - 0.02f)));
            Vector3 slideRear = gun.InverseTransformPoint(slide.position) + new Vector3(0f, 0.016f, -0.012f) * unit;
            target = Vector3.Lerp(target, slideRear, over);
            fingers = Vector3.Lerp(fingers, new Vector3(1f, -0.2f, 0.25f).normalized, over);
            palm = Vector3.Lerp(palm, new Vector3(0f, -1f, 0f), over);
            if (prev < RackRelease && t >= RackRelease)
            {
                slideLocked = false;
                slideTime = w.slideBackTime;                                    // the slide runs home from fully back
                Play(w.slideReleaseClip, 0.8f);
            }
        }
        else if (reloadEmpty && boltCatch)
        {
            // flat palm slaps the bolt catch on the left of the receiver, fingers forward
            float over = Smooth(Mathf.Clamp01((t - (MagSeat + 0.02f)) / (RackReach - MagSeat - 0.02f)));
            Vector3 catchPos = gun.InverseTransformPoint(boltCatch.position) + new Vector3(-0.03f, -0.01f, -0.005f) * unit;
            float hit = Bump(t, RackRelease - 0.04f, 0.1f);
            target = Vector3.Lerp(target, catchPos + new Vector3(0.015f, 0f, 0f) * (hit * unit), over);
            fingers = Vector3.Lerp(fingers, new Vector3(0.1f, 0.25f, 1f).normalized, over);
            palm = Vector3.Lerp(palm, new Vector3(1f, 0f, 0f), over);
            if (prev < RackRelease && t >= RackRelease)
            {
                slideLocked = false;
                Play(w.slideReleaseClip, 0.8f);
                if (player) player.AddRecoil(0.7f, Random.Range(-0.3f, 0.3f), 0f);
            }
        }
        else if (prev < MagSeat + 0.05f && t >= MagSeat + 0.05f && slideLocked) { slideLocked = false; slideTime = w.slideBackTime; Play(w.slideReleaseClip, 0.8f); }
        target = Vector3.Lerp(target, handPos, back);
        target += new Vector3(-0.03f, -0.03f, 0f) * (Mathf.Sin(Mathf.PI * back) * unit);
        fingers = Vector3.Lerp(fingers, w.leftFingers.normalized, back);
        palm = Vector3.Lerp(palm, w.leftPalm.normalized, back);
        if (leftHandTarget) { leftHandTarget.position = gun.TransformPoint(target); leftHandTarget.rotation = gun.rotation; }
        if (arms)
        {
            arms.leftFingers = fingers; arms.leftPalm = palm;
            float overW = reloadEmpty && (slide || boltCatch) ? Smooth(Mathf.Clamp01((t - (MagSeat + 0.02f)) / (RackReach - MagSeat - 0.02f))) * (1f - back) : 0f;
            float magW = toMag * (1f - back) * (1f - overW);
            for (int i = 0; i < 5 && w.leftCurl != null && i < w.leftCurl.Length; i++)
                arms.leftCurl[i] = w.leftCurl[i] * (1f - magW - overW) + MagHoldCurl[i] * magW + (boltCatch ? SlapCurl[i] : RackCurl[i]) * overW;
        }

        if (t >= total)
        {
            reloadT = -1f;
            if (mag) { mag.gameObject.SetActive(true); mag.position = gun.TransformPoint(magRest); mag.localRotation = magRestRot; }
            if (magRound) magRound.gameObject.SetActive(false);
        }
    }

    /// <summary>The empty magazine leaves the view model and falls to the floor as a physics object.</summary>
    void DropMagazine(WeaponProfile w)
    {
        if (!mag) return;
        var go = Instantiate(mag.gameObject, mag.position, mag.rotation);
        go.name = w.displayName + " magazine (dropped)";
        go.transform.localScale = mag.lossyScale;
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true)) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; r.receiveShadows = true; }
        var col = AddBoundsCollider(go);
        var body = go.AddComponent<Rigidbody>();
        body.mass = 0.08f;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearVelocity = gun.TransformDirection(magAxis) * 2.2f + (player ? player.PlanarVelocity : Vector3.zero);
        body.angularVelocity = Random.insideUnitSphere * 6f;
        var cc = player ? player.GetComponent<CharacterController>() : null;
        if (cc) Physics.IgnoreCollision(col, cc);
        if (reloadEmpty) { var round = FindDeep(go.transform, "MagRound"); if (round) Destroy(round.gameObject); }   // an emptied mag has no round on top
        Destroy(go, 20f);
    }

    void Play(AudioClip clip, float volume)
    {
        if (!clip) return;
        audio2D.pitch = Random.Range(0.96f, 1.04f);
        audio2D.PlayOneShot(clip, volume);
    }

    /// <summary>Ease out that overshoots by ~6% and settles (0 -> 1).</summary>
    static float BackOut(float u)
    {
        const float c = 1.2f;
        u -= 1f;
        return 1f + (c + 1f) * u * u * u + c * u * u;
    }

    /// <summary>Damped oscillation after t0 (starts at 0).</summary>
    static float Wobble(float t, float t0, float freq, float decay)
    {
        float u = t - t0;
        return u <= 0f ? 0f : Mathf.Sin(u * freq * 2f * Mathf.PI) * Mathf.Exp(-u / decay);
    }

    /// <summary>0 -> 1 -> 0 hump starting at t0, lasting len seconds.</summary>
    static float Bump(float t, float t0, float len)
    {
        float u = (t - t0) / len;
        return u <= 0f || u >= 1f ? 0f : Mathf.Sin(u * Mathf.PI) * (1f - u);
    }

    void Fire()
    {
        var w = Current;
        if (w == null) return;
        if (w.magazineSize > 0)
        {
            if (ammo[current] <= 0) return;
            ammo[current]--;
            if (ammo[current] == 0 && w.lockSlideWhenEmpty) slideLocked = true;
        }
        nextShot = Time.time + 60f / w.roundsPerMinute;
        Shots++;
        slideTime = 0f;
        float k = Mathf.Lerp(1f, w.aimRecoilMultiplier, Smooth(Aim));

        // sound
        if (w.shotClips != null && w.shotClips.Length > 0)
        {
            audio2D.pitch = w.shotPitch * Random.Range(0.94f, 1.06f);
            audio2D.PlayOneShot(w.shotClips[shotIndex++ % w.shotClips.Length], w.shotVolume);
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
        player.AddRecoil(w.cameraKick * k, Random.Range(-w.cameraSideKick, w.cameraSideKick) * k, w.cameraClimb * k);
        kickPos += new Vector3(Random.Range(-0.004f, 0.004f), 0.004f * k, -w.gunKickBack * k);
        kickRot += new Vector3(-w.gunKickUp * k, Random.Range(-1.2f, 1.2f) * k, Random.Range(-2f, 2f) * k);

        // muzzle flash + tracer
        flashTimer = 0.045f;
        flashRoll = Random.Range(0f, 360f);
        flashQuad.localScale = Vector3.one * Random.Range(0.14f, 0.22f) * w.flashScale / Mathf.Max(0.0001f, w.scale);
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
        bool flash = flashTimer > 0f && Equipped;
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
