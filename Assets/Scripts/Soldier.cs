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
    private Transform target;
    public bool isDead { get; private set; } = false;

    // HP Bar texture cache
    private static Texture2D hpTexture;

    void OnEnable()
    {
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

        if (isICBMLauncher)
        {
            UpdateICBMLauncher();
            return;
        }

        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.Combat)
        {
            return;
        }

        FindTarget();

        if (target != null)
        {
            float dist = Vector3.Distance(transform.position, target.position);
            if (dist > range)
            {
                // Move toward target via Cross road waypoints to stay out of hills
                Vector3 waypoint = Zombie.GetNextCrossWaypoint(transform.position, target.position);
                Vector3 toWaypoint = waypoint - transform.position;
                toWaypoint.y = 0;
                if (toWaypoint != Vector3.zero)
                {
                    Vector3 dir = toWaypoint.normalized;
                    transform.position += dir * moveSpeed * Time.deltaTime;
                    transform.rotation = Quaternion.LookRotation(dir);
                }
            }
            else
            {
                // Face target while aiming/shooting
                Vector3 aimDir = (target.position - transform.position).normalized;
                aimDir.y = 0;
                if (aimDir != Vector3.zero) transform.rotation = Quaternion.LookRotation(aimDir);

                // Shoot logic
                if (isBurstMode)
                {
                    if (!isFiringBurst)
                    {
                        StartCoroutine(FireBurstRoutine());
                    }
                }
                else
                {
                    fireTimer -= Time.deltaTime;
                    if (fireTimer <= 0f)
                    {
                        SpawnBullet(target);
                        fireTimer = fireRate;
                    }
                }
            }
            transform.position = Zombie.ClampPositionToCross(transform.position);
        }
        else
        {
            // Patrol forward towards active vectors
            Vector3 patrolDir = Vector3.right;
            if (transform.position.z > 14.8f && Mathf.Abs(transform.position.x) <= 14.8f)
            {
                patrolDir = Vector3.back;
            }
            else if (transform.position.z < -14.8f && Mathf.Abs(transform.position.x) <= 14.8f)
            {
                patrolDir = Vector3.forward;
            }

            transform.position += patrolDir * moveSpeed * 0.5f * Time.deltaTime;
            transform.rotation = Quaternion.LookRotation(patrolDir);
            transform.position = Zombie.ClampPositionToCross(transform.position);
        }

        // Cleanup if out of map bounds
        if (transform.position.x > 46f || Mathf.Abs(transform.position.z) > 46f) Destroy(gameObject);
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
                ActiveTargetingICBM = null;
                transform.rotation = Quaternion.LookRotation(Vector3.right);
                if (icbmReticleObj != null) icbmReticleObj.SetActive(false);
                AudioHelper.PlaySound(SoundEffect.ButtonClick, 0.6f);
                return;
            }

            // Left-Click (on a subsequent frame) -> Confirm target & launch ICBM!
            if (Input.GetMouseButtonDown(0) && Time.frameCount > targetingStartFrame)
            {
                if (UIManager.Instance != null && UIManager.Instance.IsPointerOverUI()) return;

                LaunchICBM(groundHitPoint);
                fireTimer = fireRate; // 20.0s reload cooldown
                ActiveTargetingICBM = null;
                transform.rotation = Quaternion.LookRotation(Vector3.right);
                if (icbmReticleObj != null) icbmReticleObj.SetActive(false);
                return;
            }
        }
        // 4. Otherwise, check if player Left-Clicks THIS ICBM Launcher to enter Targeting Mode
        else if (ActiveTargetingICBM == null && fireTimer <= 0f)
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (UIManager.Instance != null && UIManager.Instance.IsPointerOverUI()) return;

                bool clickedThisUnit = false;
                if (Physics.Raycast(ray, out RaycastHit hit, 250f))
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
                    if (PlacementManager.Instance != null && PlacementManager.Instance.currentMode != PlacementManager.PlacementMode.None)
                    {
                        PlacementManager.Instance.CancelPlacement();
                    }

                    ActiveTargetingICBM = this;
                    targetingStartFrame = Time.frameCount;
                    EnsureICBMReticle();
                    if (icbmReticleObj != null)
                    {
                        icbmReticleObj.SetActive(true);
                        float diameter = Mathf.Max(2f, aoeHalfExtent * 2f);
                        icbmReticleObj.transform.localScale = new Vector3(diameter, 0.05f, diameter);
                        icbmReticleObj.transform.position = new Vector3(groundHitPoint.x, 0.08f, groundHitPoint.z);
                    }
                    AudioHelper.PlaySound(SoundEffect.ButtonClick, 0.8f);
                }
            }
        }
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
                yield return new WaitForSeconds(burstInnerDelay); // 0.12s
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

        yield return new WaitForSeconds(burstPauseDelay);
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

        // Medic priority: Heal wounded allies first!
        if (isHealer)
        {
            Soldier[] allies = FindObjectsByType<Soldier>(FindObjectsSortMode.None);
            foreach (Soldier s in allies)
            {
                if (s == null || s == this || s.health >= s.maxHealth) continue;
                float d = Vector3.Distance(transform.position, s.transform.position);
                if (d < closestDist && d <= range * 1.5f)
                {
                    closestDist = d;
                    target = s.transform;
                }
            }
        }

        // If not healing or no wounded allies, hunt zombies
        if (target == null)
        {
            closestDist = Mathf.Infinity;
            Zombie[] zombies = FindObjectsByType<Zombie>(FindObjectsSortMode.None);
            foreach (Zombie z in zombies)
            {
                if (z == null) continue;
                float d = Vector3.Distance(transform.position, z.transform.position);
                if (d < closestDist && d < range * 1.5f)
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

        // Draw ICBM Launcher interactive status prompt above HP bar
        if (isICBMLauncher)
        {
            if (icbmStatusStyle == null)
            {
                icbmStatusStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
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
                statusText = "🚀 ICBM READY (Click to Aim)";
                boxTint = new Color(0.15f, 0.65f, 0.25f, 0.92f);
            }
            else
            {
                statusText = $"⏳ RELOAD: {fireTimer:F1}s";
                boxTint = new Color(0.35f, 0.35f, 0.35f, 0.88f);
            }

            float tagW = (ActiveTargetingICBM == this) ? 235f : 165f;
            float tagH = 22f;
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = boxTint;
            GUI.Box(new Rect(screenPos.x - tagW / 2f, y - 26f, tagW, tagH), statusText, icbmStatusStyle);
            GUI.backgroundColor = prevBg;
        }
    }
}
