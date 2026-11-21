using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Soldier : MonoBehaviour
{
    [Header("Supply System (SUP)")]
    public const int BASE_MAX_SUPPLY = 10;
    public const int HARD_MAX_SUPPLY = 250;
    public static int MAX_SUPPLY => GetMaxSupply();
    public static readonly List<Soldier> ActiveSoldiers = new List<Soldier>();
    public int supplyCost = 1;

    [Header("Stats")]
    public float maxHealth = 50f;
    public float health = 50f;
    public float damage = 10f;
    public float fireRate = 0.75f;
    public float range = 8f;
    public float moveSpeed = 3f;
    public bool isSMGTrooper = false;
    public bool isSniper = false;
    public bool isHealer = false;
    public bool isGuardian = false;
    public bool isLightTank = false;
    public bool isICBMLauncher = false;
    public int upgradeLevel = 0;
    public bool hasReloadGrenadeAbility = false; // SMG Tent Lv.3: 7% chance on reload to throw 3x3 AOE grenade (2x DPS)
    public bool hasShieldBashCounter = false;    // Special Force Depot Lv.3: 30% chance when hit to push zombies in 3x1 AOE forward (2-block knockback + 1.5s stun)
    public float icbmStopX = -30f;
    public float aoeHalfExtent = 0f; // > 0 means AOE explosion on impact (3.0f = 3x3 cells, 8.0f = 8x8 circle)

    // Manual Cob-Cannon style targeting state for ICBM Launcher
    public static Soldier ActiveTargetingICBM = null;
    private static int targetingStartFrame = -1;
    private static GameObject icbmReticleObj;
    private static GUIStyle icbmStatusStyle;

    [Header("Burst Firing (SMG)")]
    public bool isBurstMode = false;
    public int burstCount = 3;
    public float burstInnerDelay = 0.12f;
    public float burstPauseDelay = 0.66f;
    private bool isFiringBurst = false;

    private float fireTimer;
    public float ICBMCooldownRemaining => Mathf.Max(0f, fireTimer);
    private Transform target;
    public bool isDead { get; private set; } = false;

    // Center Circular Arc Formation (250° wide defense centered on East around the Center Junction at (0, 0, 0))
    private static readonly Vector3 CENTER_FORMATION_ORIGIN = Vector3.zero;
    private const float FORMATION_ARC_DEGREES = 250f;
    private const float FORMATION_ARC_RAD = FORMATION_ARC_DEGREES * Mathf.Deg2Rad;
    private const float FORMATION_ARRIVAL_THRESHOLD = 0.15f;
    private const float GUARDIAN_INTERCEPT_LEASH = 3.5f;
    private const float GUARDIAN_MAX_SLOT_OFFSET = 4.2f;
    private const float HEALER_MAX_CENTER_RADIUS = 13.5f;

    private static readonly float[] GUARDIAN_RING_RADII = { 13.5f, 12.0f, 14.2f };
    private static readonly float[] INFANTRY_RING_RADII = { 11.2f, 9.8f, 12.5f, 8.4f, 6.8f, 5.2f };
    private static readonly float[] LIGHT_TANK_RING_RADII = { 8.6f, 10.2f, 7.0f };
    private static readonly float[] HEALER_RING_RADII = { 6.6f, 5.2f, 7.8f };
    private static readonly float[] SNIPER_RING_RADII = { 4.5f, 2.8f, 5.8f };

    private enum FormationRingGroup
    {
        Guardian = 0,
        Infantry = 1,
        LightTank = 2,
        Healer = 3,
        Sniper = 4
    }

    private static int nextFormationOrderId = 1;
    private static int lastFormationUpdateFrame = -1;
    private static readonly List<Soldier>[] formationBuckets = new List<Soldier>[]
    {
        new List<Soldier>(),
        new List<Soldier>(),
        new List<Soldier>(),
        new List<Soldier>(),
        new List<Soldier>()
    };
    private static readonly System.Comparison<Soldier> formationOrderComparison =
        (a, b) => a.formationOrderId.CompareTo(b.formationOrderId);

    private int formationOrderId = 0;
    private Vector3 assignedFormationPos;
    private Vector3 assignedOutwardDir = Vector3.right;
    private bool hasAssignedFormationSlot = false;
    private float cryoSlowTimer = 0f;
    public bool IsCryoSlowed => cryoSlowTimer > 0f;

    public void ApplyCryoSlow(float duration)
    {
        if (isDead) return;
        cryoSlowTimer = Mathf.Max(cryoSlowTimer, duration);
    }

    // HP Bar texture cache & damage visibility timer
    private float hpBarVisibleTimer = 0f;
    private const float HP_BAR_DISPLAY_DURATION = 3.0f;
    private static Texture2D hpTexture;

    void OnEnable()
    {
        if (formationOrderId == 0)
        {
            formationOrderId = nextFormationOrderId++;
        }
        if (!ActiveSoldiers.Contains(this))
        {
            ActiveSoldiers.Add(this);
        }
    }

    void OnDisable()
    {
        ActiveSoldiers.Remove(this);
    }

    void Start()
    {
        if (maxHealth <= 0f) maxHealth = 50f;
        health = maxHealth;

        // Auto-assign supply cost based on unit flags if not already explicitly set
        if (supplyCost <= 1)
        {
            if (isICBMLauncher) supplyCost = 15;
            else if (isLightTank) supplyCost = 6;
            else if (isGuardian) supplyCost = 3;
            else if (isHealer) supplyCost = 2;
            else if (range >= 13f) supplyCost = 2; // Sniper
            else supplyCost = 1; // SMG Trooper
        }

        if (isICBMLauncher)
        {
            fireTimer = 0f; // Starts loaded & ready to fire immediately!
            transform.rotation = Quaternion.LookRotation(Vector3.right); // Always face East toward zombies
            CapsuleCollider col = GetComponent<CapsuleCollider>();
            if (col != null)
            {
                col.radius = 2.2f;
                col.height = 4.5f;
                col.center = new Vector3(0f, 1.5f, 0f);
            }
        }
    }

    void OnDestroy()
    {
        ActiveSoldiers.Remove(this);
        if (ActiveTargetingICBM == this)
        {
            ActiveTargetingICBM = null;
            if (icbmReticleObj != null) icbmReticleObj.SetActive(false);
        }
    }

    void Update()
    {
        if (isDead) return;
        if (hpBarVisibleTimer > 0f)
        {
            hpBarVisibleTimer -= Time.deltaTime;
        }

        if (isICBMLauncher)
        {
            UpdateICBMLauncher();
            return;
        }

        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.Combat)
        {
            return;
        }

        if (cryoSlowTimer > 0f)
        {
            cryoSlowTimer = Mathf.Max(0f, cryoSlowTimer - Time.deltaTime);
        }
        float cryoRateScale = (cryoSlowTimer > 0f) ? 0.65f : 1.0f;

        if (fireTimer > 0f)
        {
            fireTimer = Mathf.Max(0f, fireTimer - Time.deltaTime * cryoRateScale);
        }

        UpdateCenterCircleFormation();
        FindTarget();

        if (target != null)
        {
            float dist = FlatDistance(transform.position, target.position);
            if (dist > range)
            {
                // Only melee interceptors (Guardian within leash) or Medics moving to heal an ally inside the center move toward target
                Vector3 moveDest = target.position;
                if (isGuardian && hasAssignedFormationSlot)
                {
                    Vector3 offsetFromSlot = moveDest - assignedFormationPos;
                    offsetFromSlot.y = 0f;
                    if (offsetFromSlot.magnitude > GUARDIAN_MAX_SLOT_OFFSET)
                    {
                        moveDest = assignedFormationPos + offsetFromSlot.normalized * GUARDIAN_MAX_SLOT_OFFSET;
                    }
                }
                else if (isHealer)
                {
                    Vector3 offsetFromCenter = moveDest - CENTER_FORMATION_ORIGIN;
                    offsetFromCenter.y = 0f;
                    if (offsetFromCenter.magnitude > HEALER_MAX_CENTER_RADIUS)
                    {
                        moveDest = CENTER_FORMATION_ORIGIN + offsetFromCenter.normalized * HEALER_MAX_CENTER_RADIUS;
                    }
                }

                MoveAlongCrossTowards(moveDest, moveSpeed * cryoRateScale);
            }
            else
            {
                // Face target while aiming/shooting
                Vector3 aimDir = target.position - transform.position;
                aimDir.y = 0f;
                if (aimDir.sqrMagnitude > 0.0001f)
                {
                    transform.rotation = Quaternion.LookRotation(aimDir.normalized);
                }

                // Shoot logic
                if (isBurstMode)
                {
                    if (!isFiringBurst)
                    {
                        StartCoroutine(FireBurstRoutine());
                    }
                }
                else if (fireTimer <= 0f)
                {
                    SpawnBullet(target);
                    fireTimer = fireRate;
                }
            }
            transform.position = Zombie.ClampPositionToCross(transform.position);
        }
        else if (hasAssignedFormationSlot)
        {
            // March to assigned slot on the Center Circular Formation (+40% Deployment Sprint when running from base to slot), then hold position facing outward
            float distToSlot = FlatDistance(transform.position, assignedFormationPos);
            if (distToSlot > FORMATION_ARRIVAL_THRESHOLD)
            {
                float deploySprintMult = (distToSlot > 2.5f) ? 1.40f : 1.0f;
                MoveAlongCrossTowards(assignedFormationPos, moveSpeed * deploySprintMult * cryoRateScale);
                transform.position = Zombie.ClampPositionToCross(transform.position);
            }
            else
            {
                transform.position = new Vector3(assignedFormationPos.x, transform.position.y, assignedFormationPos.z);
                if (assignedOutwardDir.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(assignedOutwardDir);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, 360f * Time.deltaTime);
                }
            }
        }

        // Cleanup if out of map bounds
        if (transform.position.x > 46f || Mathf.Abs(transform.position.z) > 46f) Destroy(gameObject);
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private void MoveAlongCrossTowards(Vector3 destination, float speed)
    {
        Vector3 waypoint = Zombie.GetNextCrossWaypoint(transform.position, destination);
        Vector3 toWaypoint = waypoint - transform.position;
        toWaypoint.y = 0f;
        float dist = toWaypoint.magnitude;
        if (dist <= 0.0001f) return;

        Vector3 dir = toWaypoint / dist;
        float step = speed * Time.deltaTime;
        if (dist <= step)
        {
            transform.position = new Vector3(waypoint.x, transform.position.y, waypoint.z);
        }
        else
        {
            transform.position += dir * step;
        }
        transform.rotation = Quaternion.LookRotation(dir);
    }

    private FormationRingGroup GetFormationRingGroup()
    {
        if (isGuardian) return FormationRingGroup.Guardian;
        if (isLightTank) return FormationRingGroup.LightTank;
        if (isHealer) return FormationRingGroup.Healer;
        if (isSniper || range >= 13f) return FormationRingGroup.Sniper;
        return FormationRingGroup.Infantry;
    }

    private static void UpdateCenterCircleFormation()
    {
        if (lastFormationUpdateFrame == Time.frameCount) return;
        lastFormationUpdateFrame = Time.frameCount;

        for (int g = 0; g < formationBuckets.Length; g++)
        {
            formationBuckets[g].Clear();
        }

        for (int i = ActiveSoldiers.Count - 1; i >= 0; i--)
        {
            Soldier s = ActiveSoldiers[i];
            if (s == null || s.isDead)
            {
                ActiveSoldiers.RemoveAt(i);
                continue;
            }
            if (s.isICBMLauncher) continue;

            if (s.formationOrderId == 0)
            {
                s.formationOrderId = nextFormationOrderId++;
            }

            FormationRingGroup group = s.GetFormationRingGroup();
            formationBuckets[(int)group].Add(s);
        }

        for (int g = 0; g < formationBuckets.Length; g++)
        {
            if (formationBuckets[g].Count > 1)
            {
                formationBuckets[g].Sort(formationOrderComparison);
            }
        }

        AssignConcentricRingSlots(formationBuckets[(int)FormationRingGroup.Guardian],  GUARDIAN_RING_RADII,   2.30f);
        AssignConcentricRingSlots(formationBuckets[(int)FormationRingGroup.Infantry],  INFANTRY_RING_RADII,   1.85f);
        AssignConcentricRingSlots(formationBuckets[(int)FormationRingGroup.LightTank], LIGHT_TANK_RING_RADII, 3.20f);
        AssignConcentricRingSlots(formationBuckets[(int)FormationRingGroup.Healer],    HEALER_RING_RADII,     1.90f);
        AssignConcentricRingSlots(formationBuckets[(int)FormationRingGroup.Sniper],    SNIPER_RING_RADII,     1.65f);
    }

    private static void AssignConcentricRingSlots(List<Soldier> group, float[] ringRadii, float minArcSpacing)
    {
        int total = group.Count;
        if (total == 0) return;

        float halfArcRad = FORMATION_ARC_RAD * 0.5f; // -125° to +125° centered on East (0°)
        int assigned = 0;

        for (int rIdx = 0; rIdx < ringRadii.Length && assigned < total; rIdx++)
        {
            float radius = ringRadii[rIdx];
            int remaining = total - assigned;
            bool isLastRing = (rIdx == ringRadii.Length - 1);

            int capacity;
            if (isLastRing)
            {
                capacity = remaining;
            }
            else
            {
                float arcLength = radius * FORMATION_ARC_RAD;
                capacity = Mathf.Max(3, Mathf.FloorToInt(arcLength / Mathf.Max(0.5f, minArcSpacing)) + 1);
            }

            int countInRing = Mathf.Min(remaining, capacity);

            for (int i = 0; i < countInRing; i++)
            {
                float angle;
                if (countInRing == 1)
                {
                    angle = 0f; // Directly East
                }
                else if (countInRing == 2)
                {
                    // Symmetric NE & SE so 2 units guard the front rather than only the rear tips
                    angle = (i == 0 ? -0.5f : 0.5f) * halfArcRad;
                }
                else if (rIdx % 2 == 1)
                {
                    // Stagger overflow rings inside the 250° arc so units interlock cleanly
                    float step = FORMATION_ARC_RAD / countInRing;
                    angle = -halfArcRad + (i + 0.5f) * step;
                }
                else
                {
                    // Full 250° arc from -125° (SW-South) through 0° (East) to +125° (NW-North)
                    float t = (float)i / (countInRing - 1);
                    angle = Mathf.Lerp(-halfArcRad, halfArcRad, t);
                }

                Soldier s = group[assigned + i];
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                Vector3 outwardDir = new Vector3(cos, 0f, sin);
                s.assignedFormationPos = new Vector3(
                    CENTER_FORMATION_ORIGIN.x + cos * radius,
                    s.transform.position.y,
                    CENTER_FORMATION_ORIGIN.z + sin * radius
                );
                s.assignedOutwardDir = outwardDir;
                s.hasAssignedFormationSlot = true;
            }

            assigned += countInRing;
        }
    }

    private void UpdateICBMLauncher()
    {
        // 1. Move slightly out of the foundry to its parking spot during Combat, then hold position
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.GameState.Combat)
        {
            if (transform.position.x < icbmStopX)
            {
                transform.position += Vector3.right * moveSpeed * Time.deltaTime;
                transform.rotation = Quaternion.LookRotation(Vector3.right);
            }
        }

        // 2. Reload cooldown timer (20.0s)
        if (fireTimer > 0f)
        {
            fireTimer = Mathf.Max(0f, fireTimer - Time.deltaTime);
        }

        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        bool hasGroundHit = groundPlane.Raycast(ray, out float enter);
        Vector3 groundHitPoint = hasGroundHit ? ray.GetPoint(enter) : transform.position + Vector3.right * 10f;

        // 3. If THIS ICBM Launcher is currently in Targeting Mode
        if (ActiveTargetingICBM == this)
        {
            EnsureICBMReticle();
            if (icbmReticleObj != null)
            {
                icbmReticleObj.SetActive(true);
                float diameter = Mathf.Max(2f, aoeHalfExtent * 2f); // 8x8 circle = 16m diameter
                icbmReticleObj.transform.localScale = new Vector3(diameter, 0.05f, diameter);
                icbmReticleObj.transform.position = new Vector3(groundHitPoint.x, 0.08f, groundHitPoint.z);
            }

            // Face the aiming point
            Vector3 lookDir = groundHitPoint - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(lookDir.normalized);
            }

            // Right-Click -> Cancel targeting
            if (Input.GetMouseButtonDown(1))
            {
                CancelICBMTargeting();
                AudioHelper.PlaySound(SoundEffect.ButtonClick, 0.6f);
                return;
            }

            // Left-Click (on a subsequent frame) -> Confirm target & launch ICBM!
            if (Input.GetMouseButtonDown(0) && Time.frameCount > targetingStartFrame)
            {
                if (UIManager.Instance != null && UIManager.Instance.IsPointerOverUI()) return;

                LaunchICBM(groundHitPoint);
                fireTimer = fireRate; // 20.0s reload cooldown
                CancelICBMTargeting();
                return;
            }
        }
        // 4. Otherwise, check if player Left-Clicks THIS ICBM Launcher to enter Targeting Mode
        else if (ActiveTargetingICBM == null)
        {
            if (icbmReticleObj != null && icbmReticleObj.activeSelf)
            {
                icbmReticleObj.SetActive(false);
            }

            if (fireTimer <= 0f && Input.GetMouseButtonDown(0))
            {
                if (UIManager.Instance != null && UIManager.Instance.IsPointerOverUI()) return;

                bool clickedThisUnit = false;
                if (Physics.Raycast(ray, out RaycastHit hit, 450f))
                {
                    if (hit.transform == transform || hit.transform.IsChildOf(transform))
                    {
                        clickedThisUnit = true;
                    }
                }
                if (!clickedThisUnit && hasGroundHit)
                {
                    float flatDist = Vector2.Distance(
                        new Vector2(groundHitPoint.x, groundHitPoint.z),
                        new Vector2(transform.position.x, transform.position.z)
                    );
                    if (flatDist <= 3.0f)
                    {
                        clickedThisUnit = true;
                    }
                }

                if (clickedThisUnit)
                {
                    BeginICBMTargeting(hasGroundHit, groundHitPoint);
                }
            }
        }
    }

    public static Soldier GetPrimaryICBMLauncher()
    {
        Soldier fallback = null;
        for (int i = 0; i < ActiveSoldiers.Count; i++)
        {
            Soldier s = ActiveSoldiers[i];
            if (s == null || !s.isICBMLauncher || s.isDead || s.health <= 0f) continue;
            if (s.fireTimer <= 0f) return s;
            if (fallback == null) fallback = s;
        }
        return fallback;
    }

    public static void CancelICBMTargeting()
    {
        if (ActiveTargetingICBM != null)
        {
            ActiveTargetingICBM.transform.rotation = Quaternion.LookRotation(Vector3.right);
            ActiveTargetingICBM = null;
        }
        if (icbmReticleObj != null)
        {
            icbmReticleObj.SetActive(false);
        }
    }

    public static void TogglePrimaryICBMTargeting()
    {
        if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameManager.GameState.Combat)
        {
            return;
        }

        if (ActiveTargetingICBM != null)
        {
            CancelICBMTargeting();
            AudioHelper.PlaySound(SoundEffect.ButtonClick, 0.6f);
            return;
        }

        Soldier icbm = GetPrimaryICBMLauncher();
        if (icbm == null) return;

        if (icbm.fireTimer > 0f)
        {
            AudioHelper.PlaySound(SoundEffect.Hit, 0.45f);
            return;
        }

        Camera cam = Camera.main;
        bool hasGroundHit = false;
        Vector3 groundHitPoint = icbm.transform.position + Vector3.right * 10f;
        if (cam != null)
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (groundPlane.Raycast(ray, out float enter))
            {
                hasGroundHit = true;
                groundHitPoint = ray.GetPoint(enter);
            }
        }

        icbm.BeginICBMTargeting(hasGroundHit, groundHitPoint);
    }

    private void BeginICBMTargeting(bool hasGroundHit, Vector3 groundHitPoint)
    {
        if (AirSupportManager.Instance != null && (AirSupportManager.Instance.IsTargetingFighter || AirSupportManager.Instance.IsTargetingB2))
        {
            AirSupportManager.Instance.CancelAllTargeting();
        }
        if (PlacementManager.Instance != null && PlacementManager.Instance.currentMode != PlacementManager.PlacementMode.None)
        {
            PlacementManager.Instance.CancelPlacement();
        }

        ActiveTargetingICBM = this;
        targetingStartFrame = Time.frameCount;
        EnsureICBMReticle();
        if (icbmReticleObj != null && hasGroundHit)
        {
            icbmReticleObj.SetActive(true);
            float diameter = Mathf.Max(2f, aoeHalfExtent * 2f);
            icbmReticleObj.transform.localScale = new Vector3(diameter, 0.05f, diameter);
            icbmReticleObj.transform.position = new Vector3(groundHitPoint.x, 0.08f, groundHitPoint.z);
        }
        AudioHelper.PlaySound(SoundEffect.ButtonClick, 0.8f);
    }

    private static void EnsureICBMReticle()
    {
        if (icbmReticleObj != null) return;

        icbmReticleObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        icbmReticleObj.name = "ICBM_AimingReticle_8x8";
        Collider c = icbmReticleObj.GetComponent<Collider>();
        if (c != null) Destroy(c);

        Renderer r = icbmReticleObj.GetComponent<Renderer>();
        if (r != null)
        {
            r.material.color = new Color(1.0f, 0.25f, 0.15f, 0.55f);
        }
        icbmReticleObj.SetActive(false);
    }

    private void LaunchICBM(Vector3 targetWorldPos)
    {
        Vector3 launchPos = transform.position + Vector3.up * 1.4f;

        GameObject missileObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        missileObj.name = "ICBM_Warhead_Missile";
        missileObj.transform.position = launchPos;
        missileObj.transform.localScale = new Vector3(0.45f, 1.1f, 0.45f);

        Collider c = missileObj.GetComponent<Collider>();
        if (c != null) Destroy(c);

        Renderer r = missileObj.GetComponent<Renderer>();
        if (r != null)
        {
            r.material.color = new Color(0.95f, 0.35f, 0.1f);
        }

        Bullet bullet = missileObj.AddComponent<Bullet>();
        bullet.InitializeICBM(launchPos, targetWorldPos, damage, aoeHalfExtent);

        AudioHelper.PlayRandomTankShoot(1.0f);
    }

    public float GetCurrentDPS()
    {
        if (isBurstMode)
        {
            float cycleTime = ((burstCount - 1) * burstInnerDelay) + burstPauseDelay; // 0.90s cycle
            return (burstCount * damage) / Mathf.Max(0.1f, cycleTime);
        }
        return damage / Mathf.Max(0.1f, fireRate);
    }

    private void ThrowReloadGrenade(Vector3 targetWorldPos)
    {
        Vector3 launchPos = transform.position + Vector3.up * 0.7f + transform.forward * 0.35f;
        float grenadeDamage = GetCurrentDPS() * 2.0f; // 2x of DPS (e.g. 40 DPS * 2 = 80.0 damage at Lv.3)
        float grenadeAoeHalfExtent = 3.0f;            // 3x3 blocks = 6.0m x 6.0m square AOE

        GameObject nadeObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        nadeObj.name = "SMG_FragGrenade_3x3";
        nadeObj.transform.position = launchPos;
        nadeObj.transform.localScale = new Vector3(0.32f, 0.38f, 0.32f);

        Collider c = nadeObj.GetComponent<Collider>();
        if (c != null) Destroy(c);

        Renderer r = nadeObj.GetComponent<Renderer>();
        if (r != null)
        {
            r.material.color = new Color(0.25f, 0.48f, 0.18f); // Tactical olive frag grenade
        }

        Bullet bullet = nadeObj.AddComponent<Bullet>();
        bullet.InitializeGrenade(launchPos, targetWorldPos, grenadeDamage, grenadeAoeHalfExtent);
    }

    public static void ApplyUpgradeToActiveUnits(Building.BuildingType bType)
    {
        if (bType == Building.BuildingType.SMGTent)
        {
            int lv = Building.GetUpgradeLevel(Building.BuildingType.SMGTent);
            float dmgMult = Building.GetDamageMultiplier(Building.BuildingType.SMGTent);
            for (int i = ActiveSoldiers.Count - 1; i >= 0; i--)
            {
                Soldier s = ActiveSoldiers[i];
                if (s != null && !s.isDead && (s.isSMGTrooper || s.isBurstMode))
                {
                    s.isSMGTrooper = true;
                    s.upgradeLevel = lv;
                    s.damage = 10f * dmgMult;
                    s.hasReloadGrenadeAbility = (lv >= 3);
                }
            }
        }
        else if (bType == Building.BuildingType.SpecialForceDepot)
        {
            int lv = Building.GetUpgradeLevel(Building.BuildingType.SpecialForceDepot);
            float hpMult = Building.GetHealthMultiplier(Building.BuildingType.SpecialForceDepot);
            for (int i = ActiveSoldiers.Count - 1; i >= 0; i--)
            {
                Soldier s = ActiveSoldiers[i];
                if (s != null && !s.isDead && s.isGuardian)
                {
                    float newMax = 175f * hpMult;
                    float hpGain = Mathf.Max(0f, newMax - s.maxHealth);
                    s.maxHealth = newMax;
                    s.health = Mathf.Min(s.maxHealth, s.health + hpGain);
                    s.upgradeLevel = lv;
                    s.hasShieldBashCounter = (lv >= 3);
                }
            }
        }
        else if (bType == Building.BuildingType.SniperTower)
        {
            int lv = Building.GetUpgradeLevel(Building.BuildingType.SniperTower);
            float dmgMult = Building.GetDamageMultiplier(Building.BuildingType.SniperTower);
            for (int i = ActiveSoldiers.Count - 1; i >= 0; i--)
            {
                Soldier s = ActiveSoldiers[i];
                if (s != null && !s.isDead && s.isSniper)
                {
                    s.upgradeLevel = lv;
                    s.damage = 37.5f * dmgMult;
                    s.fireRate = (lv >= 3) ? 1.25f : 1.0f; // Lv.3: Firerate -20%
                }
            }
        }
    }

    private IEnumerator FireBurstRoutine()
    {
        isFiringBurst = true;

        // 3 consecutive shots in rapid succession
        for (int i = 0; i < burstCount; i++)
        {
            if (target != null)
            {
                SpawnBullet(target);
            }

            if (i < burstCount - 1)
            {
                float innerDelay = (cryoSlowTimer > 0f) ? (burstInnerDelay * 1.45f) : burstInnerDelay;
                yield return new WaitForSeconds(innerDelay); // 0.12s (or slowed by Cryo)
            }
        }

        // Reload / Pause between bursts (0.66s):
        // SMG Tent Tier 3 Ability: Every reload has a 7% chance to throw a grenade (3x3 block AOE, 2x DPS damage)
        if (hasReloadGrenadeAbility && Random.value < 0.07f)
        {
            FindTarget();
            if (target != null)
            {
                ThrowReloadGrenade(target.position);
            }
        }

        float pauseDelay = (cryoSlowTimer > 0f) ? (burstPauseDelay * 1.45f) : burstPauseDelay;
        yield return new WaitForSeconds(pauseDelay);
        isFiringBurst = false;
    }

    void SpawnBullet(Transform targetTransform)
    {
        if (targetTransform == null) return;

        Vector3 spawnPos = transform.position + Vector3.up * (isLightTank ? 0.85f : 0.35f) + transform.forward * (isLightTank ? 1.1f : 0.4f);
        bool isHealing = isHealer && targetTransform.GetComponent<Soldier>() != null;
        float actualDmg = isHealing ? 14f : damage; // 14 HP heal per dart

        // Create visible projectile with trajectory
        GameObject bObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bObj.name = isHealing ? "Medic_HealDart" : (isLightTank ? "Tank_HE_Shell" : (isBurstMode ? "SMG_Tracer" : (damage >= 20f ? "Sniper_Tracer" : "Soldier_Bullet")));
        bObj.transform.position = spawnPos;

        // Remove collider from bullet
        Collider c = bObj.GetComponent<Collider>();
        if (c != null) Destroy(c);

        Renderer rend = bObj.GetComponent<Renderer>();
        if (rend != null)
        {
            if (isHealing)
            {
                // Medic: Glowing healing green energy dart
                bObj.transform.localScale = new Vector3(0.22f, 0.22f, 0.38f);
                rend.material.color = new Color(0.2f, 1.0f, 0.45f);
            }
            else if (isLightTank)
            {
                // Light Tank: Heavy high-explosive artillery shell (3x3 AOE)
                bObj.transform.localScale = new Vector3(0.38f, 0.38f, 0.72f);
                rend.material.color = new Color(1.0f, 0.45f, 0.1f);
            }
            else if (damage >= 20f)
            {
                // Sniper: High-velocity cyan/white supersonic tracer
                if (isSniper && upgradeLevel >= 3)
                {
                    bObj.transform.localScale = new Vector3(0.24f, 0.24f, 0.95f);
                    rend.material.color = new Color(0.2f, 0.95f, 1.0f); // Piercing electric cyan for Lv.3 Sniper
                }
                else
                {
                    bObj.transform.localScale = new Vector3(0.2f, 0.2f, 0.65f);
                    rend.material.color = new Color(0.35f, 0.9f, 1.0f);
                }
            }
            else
            {
                // SMG / pistol: Glowing golden-yellow tracer
                bObj.transform.localScale = new Vector3(0.16f, 0.16f, 0.36f);
                rend.material.color = new Color(1.0f, 0.85f, 0.2f);
            }
        }

        Bullet bullet = bObj.AddComponent<Bullet>();
        bool isSniperShot = isSniper || (damage >= 20f && !isHealing && !isLightTank && !isICBMLauncher);
        float bSpeed = isHealing ? 22f : (isLightTank ? 28f : (isSniperShot ? 42f : 24f)); // Real projectile travel speed (m/s)
        bullet.Initialize(targetTransform, actualDmg, bSpeed, isSniperShot, isHealing, isHealing ? 0f : aoeHalfExtent);

        if (isLightTank)
        {
            bullet.isTankShell = true;
        }

        if (isSniperShot && isSniper)
        {
            if (upgradeLevel >= 3)
            {
                bullet.maxPierces = 2;              // Penetrates 2 enemies (hits up to 3 zombies in a line)
                bullet.pierceDamageRetention = 1.0f; // No damage reduction on pierced hits
            }
            else
            {
                bullet.maxPierces = 1;
                bullet.pierceDamageRetention = 0.5f;
            }
        }

        if (isGuardian)
        {
            AudioHelper.PlayGuardianStomp(0.85f);
        }
        else if (isLightTank)
        {
            AudioHelper.PlayRandomTankShoot(0.95f);
        }
        else if (isSMGTrooper || isBurstMode)
        {
            AudioHelper.PlaySound(SoundEffect.SMGShoot, 0.75f);
        }
        else if (isSniper || (damage >= 20f && !isHealing))
        {
            AudioHelper.PlayRandomSniperShoot(0.50f);
        }
        else
        {
            AudioHelper.PlaySound(SoundEffect.Shoot, 0.45f);
        }
    }

    void FindTarget()
    {
        float closestDist = Mathf.Infinity;
        target = null;

        // Medic priority: Heal wounded allies first (within range, or inside the center defense circle)
        if (isHealer)
        {
            for (int i = 0; i < ActiveSoldiers.Count; i++)
            {
                Soldier s = ActiveSoldiers[i];
                if (s == null || s == this || s.isDead || s.health >= s.maxHealth) continue;
                float d = FlatDistance(transform.position, s.transform.position);
                bool inImmediateRange = d <= range;
                bool inCenterDefense = FlatDistance(s.transform.position, CENTER_FORMATION_ORIGIN) <= 16.5f && d <= range * 2.8f;
                if ((inImmediateRange || inCenterDefense) && d < closestDist)
                {
                    closestDist = d;
                    target = s.transform;
                }
            }
        }

        // If not healing or no wounded allies, engage zombies without abandoning the center circular formation
        if (target == null)
        {
            closestDist = Mathf.Infinity;
            Zombie[] zombies = FindObjectsByType<Zombie>(FindObjectsSortMode.None);
            float maxSearchRange = isGuardian ? (range + GUARDIAN_INTERCEPT_LEASH) : range;

            foreach (Zombie z in zombies)
            {
                if (z == null || z.isDead) continue;
                float d = FlatDistance(transform.position, z.transform.position);
                if (d > maxSearchRange) continue;

                // Guardians may step slightly out of their outer-ring slot to intercept zombies near their sector,
                // while ranged units strictly hold their circle slot and fire when enemies enter weapon range.
                if (isGuardian && d > range)
                {
                    if (!hasAssignedFormationSlot) continue;
                    float distFromSlot = FlatDistance(z.transform.position, assignedFormationPos);
                    if (distFromSlot > GUARDIAN_MAX_SLOT_OFFSET + range) continue;
                }

                if (d < closestDist)
                {
                    closestDist = d;
                    target = z.transform;
                }
            }
        }
    }

    public void Heal(float amount)
    {
        if (isDead) return;
        health = Mathf.Min(maxHealth, health + amount);
    }

    public void TakeDamage(float dmg, bool isExplosion = false)
    {
        if (isDead) return;

        // Guardian Buff: 50% resistance against explosion damage!
        if (isGuardian && isExplosion)
        {
            dmg *= 0.5f;
        }

        health -= dmg;
        hpBarVisibleTimer = HP_BAR_DISPLAY_DURATION;
        if (health <= 0f)
        {
            isDead = true;
            ActiveSoldiers.Remove(this); // Immediately deducted from Supply when killed by zombies!
            Destroy(gameObject, 0.2f);
            return;
        }

        // Special Force Depot Lv.3 Ability:
        // Every time Guardian takes damage, 30% chance to push zombies in a 3x1 block AOE in front
        // (2 blocks knockback = 4.0m & 1.5s stun).
        if (isGuardian && hasShieldBashCounter && Random.value < 0.30f)
        {
            PerformGuardianShieldPush();
        }
    }

    private void PerformGuardianShieldPush()
    {
        // 1. Play punch sound INSTANTLY as the skill is executed (zero latency)
        AudioHelper.PlayGuardianPunch(1.0f);

        if (target != null)
        {
            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(toTarget.normalized);
            }
        }

        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.right;
        forward.Normalize();
        Vector3 right = transform.right;

        // Visual feedback: Guardian model dynamically lunges/punches shield forward
        StartCoroutine(GuardianShieldBashLungeRoutine(forward));

        // 3x1 blocks in front (3 blocks wide = 6.0m, 1 block deep = 2.0m)
        Vector3 pushCenter = transform.position + forward * 1.1f;
        SpawnShieldPushVisual(pushCenter, forward);

        const float KNOCKBACK_DIST = 4.0f; // 2 blocks (2 * 2.0m = 4.0m)
        const float STUN_DURATION = 1.5f;  // 1.5 seconds stun

        Zombie[] allZombies = FindObjectsByType<Zombie>(FindObjectsSortMode.None);
        foreach (Zombie z in allZombies)
        {
            if (z == null || z.isDead) continue;
            Vector3 toZ = z.transform.position - transform.position;
            toZ.y = 0f;
            float fDist = Vector3.Dot(toZ, forward);
            float rDist = Mathf.Abs(Vector3.Dot(toZ, right));

            // 1 block forward depth (-0.3m to 2.4m in front), 3 blocks lateral width (<= 3.2m left/right)
            if (fDist >= -0.3f && fDist <= 2.4f && rDist <= 3.2f)
            {
                z.ApplyKnockbackAndStun(forward, KNOCKBACK_DIST, STUN_DURATION);
            }
        }
    }

    private void SpawnShieldPushVisual(Vector3 center, Vector3 forward)
    {
        // 3 blocks wide (6.0m) x 1 block deep (2.0m) shockwave box in front of Guardian
        GameObject waveBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
        waveBox.name = "Guardian_ShieldPush_3x1";
        waveBox.transform.position = new Vector3(center.x, 0.25f, center.z);
        waveBox.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        waveBox.transform.localScale = new Vector3(6.0f, 0.35f, 2.0f);

        Collider col = waveBox.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer rend = waveBox.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material.color = new Color(0.25f, 0.85f, 1.0f, 0.8f);
        }
        Destroy(waveBox, 0.25f);
    }

    private System.Collections.IEnumerator GuardianShieldBashLungeRoutine(Vector3 forward)
    {
        Vector3 basePos = transform.position;
        float duration = 0.16f;
        float timer = 0f;
        while (timer < duration && !isDead)
        {
            timer += Time.deltaTime;
            float t = timer / duration;
            // Quick punch/shove arc: 0 -> peak 0.35m -> back to 0
            float offset = Mathf.Sin(t * Mathf.PI) * 0.35f;
            transform.position = basePos + forward * offset;
            yield return null;
        }
        if (!isDead)
        {
            transform.position = basePos;
        }
    }

    /// <summary>
    /// Calculates the total Supply (SUP) of all active, living friendly units on the map.
    /// Max cap is 250 SUP.
    /// </summary>
    public static int GetCurrentSupply()
    {
        int total = 0;
        for (int i = ActiveSoldiers.Count - 1; i >= 0; i--)
        {
            Soldier s = ActiveSoldiers[i];
            if (s == null || s.isDead)
            {
                ActiveSoldiers.RemoveAt(i);
                continue;
            }
            total += s.supplyCost;
        }
        return total;
    }

    /// <summary>
    /// Calculates the current Supply Cap: Base 10 + (10 per active Supply Depot on map), up to 250.
    /// </summary>
    public static int GetMaxSupply()
    {
        int depotBonus = Building.GetActiveSupplyDepotCount() * 10;
        return Mathf.Min(HARD_MAX_SUPPLY, BASE_MAX_SUPPLY + depotBonus);
    }

    /// <summary>
    /// Checks if a unit with the specified supply cost can spawn without exceeding current MAX_SUPPLY.
    /// </summary>
    public static bool CanSpawnSupply(int unitSupplyCost)
    {
        int current = GetCurrentSupply();
        int max = GetMaxSupply();
        if (current >= max) return false;
        return (current + unitSupplyCost) <= max;
    }

    void OnGUI()
    {
        if (isDead || health <= 0f) return;
        if (Camera.main == null) return;

        // Offset vertically above soldier/tank/ICBM head in 3D world
        float yHead = isICBMLauncher ? 3.2f : (isLightTank ? 1.85f : 1.55f);
        Vector3 worldPos = transform.position + Vector3.up * yHead;
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

        if (screenPos.z <= 0) return;

        float barW = isICBMLauncher ? 64f : (isLightTank ? 54f : (isGuardian ? 44f : 34f));
        float barH = (isICBMLauncher || isLightTank || isGuardian) ? 6f : 5f;
        float x = screenPos.x - barW / 2f;
        float y = Screen.height - screenPos.y;

        if (hpBarVisibleTimer > 0f)
        {
            if (hpTexture == null)
            {
                hpTexture = new Texture2D(1, 1);
                hpTexture.SetPixel(0, 0, Color.white);
                hpTexture.Apply();
            }

            float pct = Mathf.Clamp01(health / maxHealth);

            // Background (Dark)
            Color prevColor = GUI.color;
            GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
            GUI.DrawTexture(new Rect(x - 1, y - 1, barW + 2, barH + 2), hpTexture);

            // Health fill
            GUI.color = isICBMLauncher
                ? new Color(0.35f, 0.95f, 0.45f)
                : (isLightTank ? new Color(1.0f, 0.5f, 0.15f) : (isGuardian ? new Color(1.0f, 0.8f, 0.2f) : new Color(0.2f, 0.7f, 1f)));
            GUI.DrawTexture(new Rect(x, y, barW * pct, barH), hpTexture);
            GUI.color = prevColor;
        }

        // Draw ICBM Launcher interactive status prompt above HP bar
        if (isICBMLauncher)
        {
            if (icbmStatusStyle == null)
            {
                icbmStatusStyle = new GUIStyle(GUI.skin.box)
                {
                    font = GameFontTheme.BodyBoldFont,
                    fontSize = 13,
                    fontStyle = FontStyle.Normal,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = false,
                    clipping = TextClipping.Overflow
                };
                icbmStatusStyle.normal.textColor = Color.white;
            }

            string statusText;
            Color boxTint;
            if (ActiveTargetingICBM == this)
            {
                statusText = "🎯 AIMING (L-Click: Fire | R-Click: Cancel)";
                boxTint = new Color(1.0f, 0.35f, 0.15f, 0.95f);
            }
            else if (fireTimer <= 0f)
            {
                statusText = "🚀 ICBM READY [R] (Click to Aim)";
                boxTint = new Color(0.15f, 0.65f, 0.25f, 0.92f);
            }
            else
            {
                statusText = $"⏳ RELOAD: {fireTimer:F1}s";
                boxTint = new Color(0.35f, 0.35f, 0.35f, 0.88f);
            }

            float tagW = (ActiveTargetingICBM == this) ? 345f : 265f;
            float tagH = 24f;
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = boxTint;
            GUI.Box(new Rect(screenPos.x - tagW / 2f, y - 28f, tagW, tagH), statusText, icbmStatusStyle);
            GUI.backgroundColor = prevBg;
        }
    }
}
