using UnityEngine;
using System.Collections.Generic;

public class Zombie : MonoBehaviour
{
    [Header("Stats")]
    public float maxHealth = 50f;
    public float currentHealth;
    public float damage = 10f;
    public float moveSpeed = 1.8f;
    public float attackRate = 1.0f;
    public float attackRange = 1.3f;
    public float detectionRange = 60f; // Full-map aggro: zombies ALWAYS hunt soldiers if any exist!

    [Header("Elite & Boss Flags")]
    public bool isBossUnit = false;
    public bool isBuffedZombie = false;
    public bool isBombaz = false;
    public bool isBombazEnraged = false;
    public float bombazBaseNormalSpeed = 1.8f;
    public bool isEliteUpgrade = false;
    public string eliteTitle = "⚡ ELITE ZOMBIE";
    public float damageResistance = 0f; // 0.5f = 50% bullet & weapon damage resistance

    private float attackTimer;
    private float postAttackFreezeTimer = 0f; // Elite zombies stand still for 3.0s after landing an attack
    private float stunTimer = 0f; // Stunned duration (e.g. 1.5s from Guardian Lv.3 Shield Push)
    public bool IsStunned => stunTimer > 0f;
    private Soldier targetSoldier;
    private Building targetBuilding;
    public bool isDead { get; private set; } = false;
    private float retargetTimer = 0f;

    // HP Bar texture cache
    private static Texture2D hpTexture;
    private static GUIStyle bossLabelStyle;
    private static GUIStyle buffedLabelStyle;
    private static GUIStyle bombazLabelStyle;
    private static GUIStyle eliteLabelStyle;

    void Start()
    {
        if (currentHealth <= 0f) currentHealth = maxHealth;
        // Default facing left towards base
        transform.rotation = Quaternion.LookRotation(Vector3.left);
    }

    void Update()
    {
        if (isDead) return;
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.Combat)
        {
            return;
        }

        // Bombaz Ability Check: When HP < 40%, speed surges to 150% of normal speed!
        if (isBombaz && !isBombazEnraged && currentHealth <= maxHealth * 0.40f && currentHealth > 0f)
        {
            ActivateBombazEnrage();
        }

        // Stun Mechanic (e.g. Guardian Lv.3 Shield Push stuns for 1.5s)
        if (stunTimer > 0f)
        {
            stunTimer -= Time.deltaTime;
            return;
        }

        // Elite Zombie Mechanic: Stand completely still for 3.0 seconds after landing a hit!
        if (postAttackFreezeTimer > 0f)
        {
            postAttackFreezeTimer -= Time.deltaTime;
            return;
        }

        // Periodically refresh target or find target if current target is gone/dead
        retargetTimer -= Time.deltaTime;
        if ((targetSoldier == null && targetBuilding == null) || retargetTimer <= 0f)
        {
            FindBestTarget();
            retargetTimer = 0.22f;
        }

        // 1. Attack barricade/building if engaged
        if (targetBuilding != null)
        {
            float dist = Vector3.Distance(transform.position, targetBuilding.transform.position);
            Vector3 waypoint = GetNextCrossWaypoint(transform.position, targetBuilding.transform.position);
            Vector3 toWaypoint = (waypoint - transform.position);
            toWaypoint.y = 0;

            if (dist > attackRange + 0.4f)
            {
                if (toWaypoint != Vector3.zero)
                {
                    Vector3 moveDir = toWaypoint.normalized;
                    transform.position += moveDir * moveSpeed * Time.deltaTime;
                    transform.rotation = Quaternion.LookRotation(moveDir);
                }
            }
            else
            {
                if (isBombaz)
                {
                    // Bombaz explodes immediately in melee range (4x4 circular AOE)!
                    PerformBombazExplosion();
                    return;
                }

                Vector3 toB = (targetBuilding.transform.position - transform.position);
                toB.y = 0;
                if (toB != Vector3.zero) transform.rotation = Quaternion.LookRotation(toB.normalized);
                attackTimer -= Time.deltaTime;
                if (attackTimer <= 0f)
                {
                    if (isBossUnit)
                    {
                        PerformGroundSlam(targetBuilding.transform.position);
                    }
                    else
                    {
                        targetBuilding.TakeDamage(damage);
                    }
                    attackTimer = attackRate;
                    if (isEliteUpgrade)
                    {
                        postAttackFreezeTimer = 3.0f; // Elite stands still for 3s after hitting
                    }
                }
            }
            transform.position = ClampPositionToCross(transform.position);
            return;
        }

        // 2. ZOMBIES ACTIVELY CHASE & ATTACK SOLDIERS
        if (targetSoldier != null)
        {
            float dist = Vector3.Distance(transform.position, targetSoldier.transform.position);
            Vector3 waypoint = GetNextCrossWaypoint(transform.position, targetSoldier.transform.position);
            Vector3 toWaypoint = (waypoint - transform.position);
            toWaypoint.y = 0; // Flat horizontal movement

            if (dist > attackRange)
            {
                // Follow corridor waypoint to prevent walking across hills
                if (toWaypoint != Vector3.zero)
                {
                    Vector3 moveDir = toWaypoint.normalized;
                    transform.position += moveDir * moveSpeed * Time.deltaTime;
                    transform.rotation = Quaternion.LookRotation(moveDir);
                }
            }
            else
            {
                if (isBombaz)
                {
                    // Bombaz explodes immediately in melee range (4x4 circular AOE)!
                    PerformBombazExplosion();
                    return;
                }

                // In melee attack range: face soldier directly and strike
                Vector3 toSoldier = (targetSoldier.transform.position - transform.position);
                toSoldier.y = 0;
                if (toSoldier != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(toSoldier.normalized);
                }

                attackTimer -= Time.deltaTime;
                if (attackTimer <= 0f)
                {
                    if (isBossUnit)
                    {
                        // Tyrant Boss: Ground Slam AOE (Guardian Tier 3 survives 1 hit / dies in 2 hits)
                        PerformGroundSlam(targetSoldier.transform.position, targetSoldier);
                    }
                    else
                    {
                        if (isEliteUpgrade && targetSoldier.isGuardian)
                        {
                            if (targetSoldier.upgradeLevel >= 3 || targetSoldier.hasShieldBashCounter)
                            {
                                // Guardian Tier 3 Buff: Survives 1 hit and dies in 2 hits from Elite!
                                float twoHitDmg = targetSoldier.maxHealth * 0.5f;
                                targetSoldier.TakeDamage(twoHitDmg);
                                Debug.Log($"[Elite Zombie] ⚡ Hit Guardian Lv.3 for {twoHitDmg:0.#} dmg (2-Hit kill rule). Remaining HP: {targetSoldier.health:0.#}/{targetSoldier.maxHealth:0.#}");
                            }
                            else
                            {
                                // Elite zombie one-shots Guardian (Lv.0 - Lv.2) in a single hit!
                                targetSoldier.TakeDamage(Mathf.Max(damage, targetSoldier.health + 999f));
                            }
                        }
                        else
                        {
                            targetSoldier.TakeDamage(damage);
                        }
                    }
                    attackTimer = attackRate;
                    if (isEliteUpgrade)
                    {
                        postAttackFreezeTimer = 3.0f; // Elite stands still for 3s after hitting!
                    }
                }
            }
            transform.position = ClampPositionToCross(transform.position);
            return;
        }

        // 3. MARCHING TOWARDS HUMAN BASE FORTRESS VIA CROSS CORRIDORS
        Vector3 baseDest = new Vector3(-65.0f, transform.position.y, Mathf.Clamp(transform.position.z, -10.0f, 10.0f));
        Vector3 marchWaypoint = GetNextCrossWaypoint(transform.position, baseDest);
        Vector3 toMarch = marchWaypoint - transform.position;
        toMarch.y = 0;

        if (toMarch != Vector3.zero)
        {
            Vector3 marchDir = toMarch.normalized;
            transform.position += marchDir * moveSpeed * Time.deltaTime;
            transform.rotation = Quaternion.LookRotation(marchDir);
        }
        transform.position = ClampPositionToCross(transform.position);

        // If reached the far-left edge of the 15x15 base (X <= -64), breach defense and damage fortress core!
        if (transform.position.x <= -64.0f)
        {
            Debug.LogWarning("[Zombie] Breached the fortress defense line!");
            if (FortressCore.Instance != null)
            {
                FortressCore.Instance.TakeDamage(damage);
            }
            Die(false);
        }
    }

    /// <summary>
    /// Calculates the next path waypoint on the Cross-shaped road network to reach destination.
    /// Prevents zombies from cutting across the hills (the 4 outer quadrants between the branches).
    /// </summary>
    public static Vector3 GetNextCrossWaypoint(Vector3 fromPos, Vector3 targetPos)
    {
        bool fromInNorth = (fromPos.z > 14.8f) && (Mathf.Abs(fromPos.x) <= 14.8f);
        bool fromInSouth = (fromPos.z < -14.8f) && (Mathf.Abs(fromPos.x) <= 14.8f);
        bool targetInNorth = (targetPos.z > 14.8f) && (Mathf.Abs(targetPos.x) <= 14.8f);
        bool targetInSouth = (targetPos.z < -14.8f) && (Mathf.Abs(targetPos.x) <= 14.8f);

        // 1. From North Arm
        if (fromInNorth)
        {
            if (targetInNorth)
            {
                return new Vector3(Mathf.Clamp(targetPos.x, -14.0f, 14.0f), fromPos.y, targetPos.z);
            }
            // Otherwise, travel South down into the Center Junction entrance
            return new Vector3(Mathf.Clamp(fromPos.x, -14.0f, 14.0f), fromPos.y, 12.0f);
        }

        // 2. From South Arm
        if (fromInSouth)
        {
            if (targetInSouth)
            {
                return new Vector3(Mathf.Clamp(targetPos.x, -14.0f, 14.0f), fromPos.y, targetPos.z);
            }
            // Otherwise, travel North up into the Center Junction entrance
            return new Vector3(Mathf.Clamp(fromPos.x, -14.0f, 14.0f), fromPos.y, -12.0f);
        }

        // 3. From Main Horizontal Corridor (Base, Left Corridor, Center Junction, Right Head)
        if (targetInNorth)
        {
            // If not yet in Center Junction (|X| > 14.0f), move horizontally into Junction first
            if (Mathf.Abs(fromPos.x) > 14.0f)
            {
                return new Vector3(0f, fromPos.y, Mathf.Clamp(fromPos.z, -14.0f, 14.0f));
            }
            // Already in Center Junction, can move North towards target
            return new Vector3(Mathf.Clamp(targetPos.x, -14.0f, 14.0f), fromPos.y, targetPos.z);
        }

        if (targetInSouth)
        {
            // If not yet in Center Junction (|X| > 14.0f), move horizontally into Junction first
            if (Mathf.Abs(fromPos.x) > 14.0f)
            {
                return new Vector3(0f, fromPos.y, Mathf.Clamp(fromPos.z, -14.0f, 14.0f));
            }
            // Already in Center Junction, can move South towards target
            return new Vector3(Mathf.Clamp(targetPos.x, -14.0f, 14.0f), fromPos.y, targetPos.z);
        }

        // Both fromPos and targetPos are in the main horizontal corridor!
        return new Vector3(targetPos.x, fromPos.y, Mathf.Clamp(targetPos.z, -14.5f, 14.5f));
    }

    /// <summary>
    /// Clamps position strictly inside the Cross-shaped road corridors, guaranteeing entities
    /// never step into the 4 hill quadrants.
    /// - Horizontal Corridor: X in [-64.8f, 44.8f], Z in [-14.8f, 14.8f]
    /// - Vertical Corridor (North/South Arms): X in [-14.8f, 14.8f], Z in [-44.8f, 44.8f]
    /// </summary>
    public static Vector3 ClampPositionToCross(Vector3 pos)
    {
        float corridorZMax = 14.8f;
        float corridorZMin = -14.8f;
        float corridorXMin = -64.8f;
        float corridorXMax = 44.8f;
        float armXMin = -14.8f;
        float armXMax = 14.8f;
        float armZMin = -44.8f;
        float armZMax = 44.8f;

        // 1. Inside Horizontal Corridor (Base, Left Corridor, Center Junction, Right Head)
        if (pos.z >= corridorZMin && pos.z <= corridorZMax)
        {
            pos.x = Mathf.Clamp(pos.x, corridorXMin, corridorXMax);
            return pos;
        }

        // 2. Inside Vertical Corridor (North Arm / South Arm)
        if (pos.x >= armXMin && pos.x <= armXMax)
        {
            pos.z = Mathf.Clamp(pos.z, armZMin, armZMax);
            return pos;
        }

        // 3. Outside both corridors (in one of the 4 hill quadrants).
        // Snap to whichever corridor wall is closest!
        if (pos.z > corridorZMax)
        {
            float dz = pos.z - corridorZMax;
            if (pos.x < armXMin)
            {
                // North-West Quadrant
                float dx = armXMin - pos.x;
                if (dz <= dx)
                {
                    pos.z = corridorZMax;
                    pos.x = Mathf.Clamp(pos.x, corridorXMin, armXMin);
                }
                else
                {
                    pos.x = armXMin;
                    pos.z = Mathf.Clamp(pos.z, corridorZMax, armZMax);
                }
            }
            else
            {
                // North-East Quadrant
                float dx = pos.x - armXMax;
                if (dz <= dx)
                {
                    pos.z = corridorZMax;
                    pos.x = Mathf.Clamp(pos.x, armXMax, corridorXMax);
                }
                else
                {
                    pos.x = armXMax;
                    pos.z = Mathf.Clamp(pos.z, corridorZMax, armZMax);
                }
            }
        }
        else
        {
            float dz = corridorZMin - pos.z;
            if (pos.x < armXMin)
            {
                // South-West Quadrant
                float dx = armXMin - pos.x;
                if (dz <= dx)
                {
                    pos.z = corridorZMin;
                    pos.x = Mathf.Clamp(pos.x, corridorXMin, armXMin);
                }
                else
                {
                    pos.x = armXMin;
                    pos.z = Mathf.Clamp(pos.z, armZMin, corridorZMin);
                }
            }
            else
            {
                // South-East Quadrant
                float dx = pos.x - armXMax;
                if (dz <= dx)
                {
                    pos.z = corridorZMin;
                    pos.x = Mathf.Clamp(pos.x, armXMax, corridorXMax);
                }
                else
                {
                    pos.x = armXMax;
                    pos.z = Mathf.Clamp(pos.z, armZMin, corridorZMin);
                }
            }
        }

        return pos;
    }

    /// <summary>
    /// Tyrant Boss Special Ability: Ground Slam (ทุ่มพื้น).
    /// Deals AOE damage in a 1*3 block area (2.0m depth along forward x 6.0m lateral width)
    /// directly in front of the boss.
    /// </summary>
    private void PerformGroundSlam(Vector3 primaryTargetPos, Soldier primaryTargetSoldier = null)
    {
        Vector3 toTarget = primaryTargetPos - transform.position;
        toTarget.y = 0;
        if (toTarget != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(toTarget.normalized);
        }

        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        Vector3 slamCenter = transform.position + forward * 1.5f;

        // Visual 1x3 blocks slam zone (1 block deep = 2.0m along forward, 3 blocks wide = 6.0m along lateral)
        SpawnGroundSlamVisual(slamCenter, forward);
        AudioHelper.PlaySound(SoundEffect.BuildingDemolish, 1.0f);
        AudioHelper.PlaySound(SoundEffect.Hit, 0.9f);

        HashSet<Soldier> hitSoldiers = new HashSet<Soldier>();

        // AOE Damage: All soldiers in 1x3 blocks area in front of the boss
        Soldier[] allSoldiers = FindObjectsByType<Soldier>(FindObjectsSortMode.None);
        foreach (Soldier s in allSoldiers)
        {
            if (s == null || s.isDead) continue;
            Vector3 toUnit = s.transform.position - transform.position;
            float fDist = Vector3.Dot(toUnit, forward);
            float rDist = Mathf.Abs(Vector3.Dot(toUnit, right));

            // 1 block depth (0.1m to 2.8m in front), 3 blocks width (<= 3.2m lateral to left/right)
            if (fDist >= 0.1f && fDist <= 2.8f && rDist <= 3.2f)
            {
                hitSoldiers.Add(s);
                ApplyBossDamageToSoldier(s);
            }
        }

        // Guarantee primary target soldier is struck if designated and wasn't inside AOE bounding check
        if (primaryTargetSoldier != null && !hitSoldiers.Contains(primaryTargetSoldier) && !primaryTargetSoldier.isDead)
        {
            ApplyBossDamageToSoldier(primaryTargetSoldier);
        }

        // AOE Damage: All buildings/barricades in 1x3 blocks area in front of the boss
        Building[] allBuildings = FindObjectsByType<Building>(FindObjectsSortMode.None);
        foreach (Building b in allBuildings)
        {
            if (b == null) continue;
            Vector3 toB = b.transform.position - transform.position;
            float fDist = Vector3.Dot(toB, forward);
            float rDist = Mathf.Abs(Vector3.Dot(toB, right));

            if (fDist >= 0.1f && fDist <= 3.0f && rDist <= 3.4f)
            {
                b.TakeDamage(damage);
            }
        }
    }

    private void ApplyBossDamageToSoldier(Soldier s)
    {
        if (s == null || s.isDead) return;

        if (s.isGuardian)
        {
            if (s.upgradeLevel >= 3 || s.hasShieldBashCounter)
            {
                // Guardian Tier 3 Buff: Survives 1 hit and dies in 2-hits from Boss Tyrant!
                float twoHitDmg = s.maxHealth * 0.5f;
                s.TakeDamage(twoHitDmg);
                Debug.Log($"[Boss Tyrant] 💥 Hit Guardian Lv.3 for {twoHitDmg:0.#} dmg (2-Hit kill rule). Remaining HP: {s.health:0.#}/{s.maxHealth:0.#}");
            }
            else
            {
                // Tyrant Boss 1-shots Guardian (Lv.0 - Lv.2)!
                s.TakeDamage(Mathf.Max(damage, s.health + 9999f));
            }
        }
        else if (s.isSMGTrooper || s.isBurstMode || (s.maxHealth <= 50f && !s.isLightTank && !s.isICBMLauncher))
        {
            // Tyrant Boss Buff: Can ONE-SHOT SMG Trooper (and frail 50 HP infantry)!
            s.TakeDamage(Mathf.Max(damage, s.health + 9999f));
            Debug.Log($"[Boss Tyrant] 💥 Tyrant Ground Slam ONE-SHOT SMG Trooper ({s.name})!");
        }
        else
        {
            s.TakeDamage(damage);
        }
    }

    private void SpawnGroundSlamVisual(Vector3 center, Vector3 forward)
    {
        // 1 block along forward (2.0m depth), 3 blocks along lateral (6.0m width)
        GameObject slamBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
        slamBox.name = "Boss_GroundSlam_1x3";
        slamBox.transform.position = new Vector3(center.x, 0.12f, center.z);
        slamBox.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        slamBox.transform.localScale = new Vector3(6.0f, 0.14f, 2.0f);

        Collider col = slamBox.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer rend = slamBox.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material.color = new Color(1.0f, 0.28f, 0.08f, 0.85f);
        }
        Destroy(slamBox, 0.35f);

        // Ground shockwave crater ring
        GameObject shockRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shockRing.name = "Boss_SlamRing";
        shockRing.transform.position = new Vector3(center.x, 0.14f, center.z);
        shockRing.transform.localScale = new Vector3(5.0f, 0.04f, 2.5f);
        Collider rCol = shockRing.GetComponent<Collider>();
        if (rCol != null) Destroy(rCol);
        Renderer rRend = shockRing.GetComponent<Renderer>();
        if (rRend != null)
        {
            rRend.material.color = new Color(1.0f, 0.65f, 0.15f, 0.9f);
        }
        Destroy(shockRing, 0.25f);
    }

    /// <summary>
    /// Bombaz Enrage Ability: When HP drops below 40%, movement speed surges to 150% of normal zombie speed.
    /// </summary>
    private void ActivateBombazEnrage()
    {
        if (isBombazEnraged) return;
        isBombazEnraged = true;
        moveSpeed = bombazBaseNormalSpeed * 1.5f;
        postAttackFreezeTimer = 0f;

        Transform fuse = transform.Find("BlockModel/BombazFuseCore");
        if (fuse != null)
        {
            Renderer r = fuse.GetComponent<Renderer>();
            if (r != null) r.material.color = new Color(1.0f, 0.12f, 0.05f);
            fuse.localScale *= 1.4f;
        }
        Debug.Log($"[Zombie] 💥 Bombaz HP < 40%! Speed surged to 150% ({moveSpeed:0.00} m/s)!");
    }

    /// <summary>
    /// Bombaz Melee Detonation: Explodes when in melee range, dealing circular 4x4 block AOE damage
    /// (4 blocks diameter = 8.0m -> radius = 4.0m).
    /// </summary>
    private void PerformBombazExplosion()
    {
        if (isDead) return;

        const float BOMBAZ_BLAST_RADIUS = 4.0f; // 4x4 blocks circular AOE (diameter = 8.0m, radius = 4.0m)
        Vector3 center = transform.position;

        SpawnBombazExplosionVisual(center, BOMBAZ_BLAST_RADIUS * 2f);
        AudioHelper.PlayRandomSMGGrenade(1.0f);
        AudioHelper.PlaySound(SoundEffect.Hit, 0.9f);

        // 1. Damage all soldiers within 4x4 block circular radius (4.0m)
        Soldier[] allSoldiers = FindObjectsByType<Soldier>(FindObjectsSortMode.None);
        foreach (Soldier s in allSoldiers)
        {
            if (s == null) continue;
            float dist2D = Vector2.Distance(
                new Vector2(s.transform.position.x, s.transform.position.z),
                new Vector2(center.x, center.z)
            );
            if (dist2D <= BOMBAZ_BLAST_RADIUS)
            {
                s.TakeDamage(damage, true);
            }
        }

        // 2. Damage all buildings/barricades within 4x4 block circular radius
        Building[] allBuildings = FindObjectsByType<Building>(FindObjectsSortMode.None);
        foreach (Building b in allBuildings)
        {
            if (b == null) continue;
            float dist2D = Vector2.Distance(
                new Vector2(b.transform.position.x, b.transform.position.z),
                new Vector2(center.x, center.z)
            );
            if (dist2D <= BOMBAZ_BLAST_RADIUS + 0.5f)
            {
                b.TakeDamage(damage);
            }
        }

        Debug.Log($"[Zombie] 💣 BOMBAZ DETONATED in 4x4 circular AOE (radius {BOMBAZ_BLAST_RADIUS}m) for {damage:0.0} dmg!");
        Die(false);
    }

    private void SpawnBombazExplosionVisual(Vector3 center, float diameter)
    {
        // Dynamic 3D Point Light + Additive Ground/Overhead Light Glow Halo
        CrossMapVisualizer.SpawnExplosionLightAndGlow(center, diameter, false, false);

        // 4x4 Blocks Circular Blast Zone (Cylinder diameter = 8.0m)
        GameObject blastCircle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        blastCircle.name = "Bombaz_BlastCircle_4x4";
        blastCircle.transform.position = new Vector3(center.x, 0.12f, center.z);
        blastCircle.transform.localScale = new Vector3(diameter, 0.08f, diameter);
        Collider c1 = blastCircle.GetComponent<Collider>();
        if (c1 != null) Destroy(c1);
        Renderer r1 = blastCircle.GetComponent<Renderer>();
        if (r1 != null)
        {
            r1.sharedMaterial = CrossMapVisualizer.CreateEmissiveExplosionMaterial(new Color(1.0f, 0.35f, 0.08f, 0.80f), false, null, 3155);
            r1.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r1.receiveShadows = false;
        }
        Destroy(blastCircle, 0.42f);

        // Inner Fireball Sphere
        GameObject fireball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        fireball.name = "Bombaz_Fireball";
        fireball.transform.position = new Vector3(center.x, 0.85f, center.z);
        float fbScale = diameter * 0.52f;
        fireball.transform.localScale = new Vector3(fbScale, fbScale, fbScale);
        Collider c2 = fireball.GetComponent<Collider>();
        if (c2 != null) Destroy(c2);
        Renderer r2 = fireball.GetComponent<Renderer>();
        if (r2 != null)
        {
            r2.sharedMaterial = CrossMapVisualizer.CreateEmissiveExplosionMaterial(new Color(1.0f, 0.82f, 0.25f, 0.94f), true, null, 3160);
            r2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r2.receiveShadows = false;
        }
        Destroy(fireball, 0.32f);
    }

    void FindBestTarget()
    {
        bool zInNorth = (transform.position.z > 14.8f) && (Mathf.Abs(transform.position.x) <= 14.8f);
        bool zInSouth = (transform.position.z < -14.8f) && (Mathf.Abs(transform.position.x) <= 14.8f);

        // Priority 1: Check if a building (like a Barricade) is right in front blocking the path
        Building[] buildings = FindObjectsByType<Building>(FindObjectsSortMode.None);
        Building closestBlocker = null;
        float closestBDist = Mathf.Infinity;

        foreach (Building b in buildings)
        {
            if (b == null) continue;
            float d = Vector3.Distance(transform.position, b.transform.position);
            
            // Check if building is in front along current movement direction
            bool isAhead = false;
            if (zInNorth)
            {
                isAhead = (b.transform.position.z <= transform.position.z + 0.4f) && Mathf.Abs(b.transform.position.x - transform.position.x) <= 3.5f;
            }
            else if (zInSouth)
            {
                isAhead = (b.transform.position.z >= transform.position.z - 0.4f) && Mathf.Abs(b.transform.position.x - transform.position.x) <= 3.5f;
            }
            else
            {
                isAhead = b.transform.position.x <= transform.position.x + 0.4f && Mathf.Abs(b.transform.position.z - transform.position.z) <= 3.5f;
            }

            if (isAhead && d < 2.5f && d < closestBDist)
            {
                closestBDist = d;
                closestBlocker = b;
            }
        }

        if (closestBlocker != null)
        {
            targetBuilding = closestBlocker;
            targetSoldier = null;
            return;
        }

        targetBuilding = null;

        // Priority 2: Hunt soldiers
        // If in North/South branch, prioritize soldiers that are in the same branch first
        float closestDist = Mathf.Infinity;
        Soldier bestTarget = null;
        Soldier[] soldiers = FindObjectsByType<Soldier>(FindObjectsSortMode.None);

        foreach (Soldier s in soldiers)
        {
            if (s == null) continue;
            float d = Vector3.Distance(transform.position, s.transform.position);

            // Add virtual path penalty if soldier is across the junction while zombie is in branch
            float pathDist = d;
            bool sInNorth = (s.transform.position.z > 14.8f) && (Mathf.Abs(s.transform.position.x) <= 14.8f);
            bool sInSouth = (s.transform.position.z < -14.8f) && (Mathf.Abs(s.transform.position.x) <= 14.8f);

            if (zInNorth && !sInNorth)
            {
                pathDist += (transform.position.z - 14.8f) + Mathf.Abs(s.transform.position.x - transform.position.x);
            }
            else if (zInSouth && !sInSouth)
            {
                pathDist += (-14.8f - transform.position.z) + Mathf.Abs(s.transform.position.x - transform.position.x);
            }

            if (pathDist < closestDist)
            {
                closestDist = pathDist;
                bestTarget = s;
            }
        }

        targetSoldier = bestTarget;

        // Priority 3: If no soldiers alive, target any building ahead on the way to the base
        if (targetSoldier == null)
        {
            closestBDist = Mathf.Infinity;
            foreach (Building b in buildings)
            {
                if (b == null) continue;
                bool isReachable = false;
                if (zInNorth)
                {
                    isReachable = (b.transform.position.z > 14.8f && b.transform.position.z <= transform.position.z + 0.5f);
                }
                else if (zInSouth)
                {
                    isReachable = (b.transform.position.z < -14.8f && b.transform.position.z >= transform.position.z - 0.5f);
                }
                else
                {
                    isReachable = (Mathf.Abs(b.transform.position.z) <= 14.8f && b.transform.position.x <= transform.position.x + 0.5f);
                }

                if (isReachable)
                {
                    float d = Vector3.Distance(transform.position, b.transform.position);
                    if (d < 16f && d < closestBDist)
                    {
                        closestBDist = d;
                        targetBuilding = b;
                    }
                }
            }
        }
    }

    public void TakeDamage(float dmg)
    {
        if (isDead) return;

        // Apply bullet / weapon damage resistance (e.g. Tyrant Boss has 50% damage reduction)
        // Skip damage reduction for dev-tools instant kill (dmg >= 50000)
        if (dmg < 50000f)
        {
            if (damageResistance > 0f)
            {
                dmg *= (1f - Mathf.Clamp01(damageResistance));
            }
            else if (isBossUnit)
            {
                dmg *= 0.5f; // 50% bullet resistance for Tyrant Boss
            }
        }

        currentHealth -= dmg;
        AudioHelper.PlaySound(SoundEffect.ZombieHit, 0.12f);

        if (isBombaz && currentHealth > 0f && !isBombazEnraged && currentHealth <= maxHealth * 0.40f)
        {
            ActivateBombazEnrage();
        }

        if (currentHealth <= 0f)
        {
            Die(true);
        }
    }

    /// <summary>
    /// Pushes the zombie back by knockbackDist (e.g. 2 blocks = 4.0m) along pushDir and stuns it for stunDuration (e.g. 1.5s).
    /// </summary>
    public void ApplyKnockbackAndStun(Vector3 pushDir, float knockbackDist, float stunDuration)
    {
        if (isDead) return;
        pushDir.y = 0f;
        if (pushDir.sqrMagnitude < 0.001f) pushDir = Vector3.right;
        pushDir.Normalize();

        transform.position = ClampPositionToCross(transform.position + pushDir * knockbackDist);
        stunTimer = Mathf.Max(stunTimer, stunDuration);
    }

    void Die(bool byPlayer)
    {
        if (isDead) return;
        isDead = true;
        AudioHelper.PlaySound(SoundEffect.ZombieDeath, 0.55f);

        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.OnZombieDied(this);
        }

        if (byPlayer && ResourceManager.Instance != null)
        {
            // Rebalanced: Significantly reduced zombie gold drops to maintain a tight, strategic economy.
            // Stage Clear rewards remain the primary dependable income source.
            // - Tyrant Boss: 30 Gold (Bounty for defeating the Boss!)
            // - Buffed Zombie: 2 Gold (reduced from 10)
            // - Elite Zombie: 1 Gold
            // - Standard Zombie: 6% chance for 1 Gold scrap (~1 gold per 16 zombies)
            int bonus = 0;
            if (isBossUnit)
            {
                bonus = 30;
                Debug.Log($"[Zombie] 🏆 TYRANT BOSS DEFEATED! Rewarded +30 Gold bounty!");
            }
            else if (isBuffedZombie)
            {
                bonus = 2;
            }
            else if (isEliteUpgrade)
            {
                bonus = 1;
            }
            else
            {
                bonus = (Random.value < 0.06f) ? 1 : 0;
            }

            if (bonus > 0)
            {
                ResourceManager.Instance.SpendGold(-bonus);
            }
        }

        Destroy(gameObject, 0.1f);
    }

    void OnGUI()
    {
        if (isDead || currentHealth <= 0f) return;
        if (Camera.main == null) return;

        // Darkness shroud visibility check: hide HP bar and labels when enveloped in darkness, fade in as it emerges
        float darkness = CrossMapVisualizer.GetDarknessAtPosition(transform.position);
        if (darkness >= 0.82f) return;

        float visAlpha = (darkness > 0.05f) ? Mathf.Clamp01(1f - (darkness - 0.05f) / 0.77f) : 1f;
        Color prevGuiColor = GUI.color;
        GUI.color = new Color(prevGuiColor.r, prevGuiColor.g, prevGuiColor.b, prevGuiColor.a * visAlpha);

        bool isBoss = isBossUnit; // Strictly actual Boss unit!
        bool isBuffed = isBuffedZombie && !isBoss;
        bool isBomb = isBombaz && !isBoss;
        bool isElite = isEliteUpgrade && !isBoss;

        // Offset vertically above zombie head in 3D world
        float yOffset = isBoss ? 2.6f : (isBuffed ? 2.3f : ((isElite || isBomb) ? 2.15f : (maxHealth >= 90f ? 2.0f : 1.6f)));
        Vector3 worldPos = transform.position + Vector3.up * yOffset;
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

        if (screenPos.z <= 0) return;

        float barW = isBoss ? 76f : (isBuffed ? 56f : ((isElite || isBomb) ? 52f : 38f));
        float barH = isBoss ? 10.5f : (isBuffed ? 8f : ((isElite || isBomb) ? 7.5f : 6f));
        float x = screenPos.x - barW / 2f;
        float y = Screen.height - screenPos.y;

        if (hpTexture == null)
        {
            hpTexture = new Texture2D(1, 1);
            hpTexture.SetPixel(0, 0, Color.white);
            hpTexture.Apply();
        }

        // Draw Boss or Buffed or Bombaz or Elite Title above bar
        if (isBoss)
        {
            if (bossLabelStyle == null)
            {
                bossLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = false,
                    clipping = TextClipping.Overflow
                };
                bossLabelStyle.normal.textColor = new Color(1.0f, 0.35f, 0.35f);
            }
            float labelW = 220f;
            float labelH = 24f;
            GUI.Label(new Rect(screenPos.x - labelW / 2f, y - 22f, labelW, labelH), "Tyrant Boss", bossLabelStyle);
        }
        else if (isBuffed)
        {
            if (buffedLabelStyle == null)
            {
                buffedLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = false,
                    clipping = TextClipping.Overflow
                };
                buffedLabelStyle.normal.textColor = new Color(1.0f, 0.65f, 0.2f);
            }
            float labelW = 220f;
            float labelH = 22f;
            GUI.Label(new Rect(screenPos.x - labelW / 2f, y - 21f, labelW, labelH), "💪 BUFFED ZOMBIE", buffedLabelStyle);
        }
        else if (isBomb)
        {
            if (bombazLabelStyle == null)
            {
                bombazLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = false,
                    clipping = TextClipping.Overflow
                };
            }
            bombazLabelStyle.normal.textColor = isBombazEnraged ? new Color(1.0f, 0.28f, 0.1f) : new Color(1.0f, 0.72f, 0.18f);
            string bombTitle = isBombazEnraged ? "💥 BOMBAZ (150% SPRINT!)" : "💣 BOMBAZ";
            if (isElite) bombTitle = "⚡ ELITE " + bombTitle;
            float labelW = 240f;
            float labelH = 22f;
            GUI.Label(new Rect(screenPos.x - labelW / 2f, y - 21f, labelW, labelH), bombTitle, bombazLabelStyle);
        }
        else if (isElite)
        {
            if (eliteLabelStyle == null)
            {
                eliteLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = false,
                    clipping = TextClipping.Overflow
                };
                eliteLabelStyle.normal.textColor = new Color(1.0f, 0.82f, 0.2f);
            }
            float labelW = 220f;
            float labelH = 22f;
            string statusSuffix = (postAttackFreezeTimer > 0f) ? $" ({postAttackFreezeTimer:0.0}s)" : "";
            GUI.Label(new Rect(screenPos.x - labelW / 2f, y - 21f, labelW, labelH), eliteTitle + statusSuffix, eliteLabelStyle);
        }
        else if (stunTimer > 0f)
        {
            if (eliteLabelStyle == null)
            {
                eliteLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = false,
                    clipping = TextClipping.Overflow
                };
                eliteLabelStyle.normal.textColor = new Color(1.0f, 0.82f, 0.2f);
            }
            GUI.Label(new Rect(screenPos.x - 90f, y - 20f, 180f, 20f), $"💫 STUNNED ({stunTimer:0.0}s)", eliteLabelStyle);
        }

        float pct = Mathf.Clamp01(currentHealth / maxHealth);

        // Background (Dark)
        GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.9f * visAlpha);
        GUI.DrawTexture(new Rect(x - 1, y - 1, barW + 2, barH + 2), hpTexture);

        // Health fill
        Color healthCol = isBoss
            ? new Color(0.95f, 0.15f, 0.35f) // Crimson red for Tyrant boss
            : (isBuffed
                ? new Color(1.0f, 0.62f, 0.15f) // Amber-orange for Buffed Zombie
                : (isElite
                    ? new Color(1.0f, 0.8f, 0.2f) // Gold for Elite
                    : (pct > 0.5f ? new Color(0.2f, 0.9f, 0.2f) : (pct > 0.25f ? new Color(1f, 0.8f, 0.1f) : new Color(0.9f, 0.2f, 0.2f)))));

        GUI.color = new Color(healthCol.r, healthCol.g, healthCol.b, healthCol.a * visAlpha);
        GUI.DrawTexture(new Rect(x, y, barW * pct, barH), hpTexture);

        GUI.color = prevGuiColor;
    }
}
