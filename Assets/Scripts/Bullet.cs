using UnityEngine;
using System.Collections.Generic;

public class Bullet : MonoBehaviour
{
    public float damage = 10f;
    public float speed = 25f;
    public Transform target;
    public Vector3 targetLastPos;
    public bool isSniper = false;
    public bool isHeal = false;
    public float aoeHalfExtent = 0f; // > 0 means AOE splash (e.g. 3.0m half-extent = 6m x 6m = 3x3 cells)
    public bool isCircularAOE = false; // true = circular radius AOE (e.g. ICBM 8x8 circle -> radius = 8.0m)
    public bool isBallisticICBM = false;
    public bool isGrenade = false;
    public bool isTankShell = false;

    // Sniper Bullet Piercing (ทะลุซอมบี้ตัวหลังได้ 1 ครั้ง ดาเมจลด 50% ทิศทางไม่เปลี่ยน, หรือ Lv.3 ทะลุได้ 2 ตัว ดาเมจไม่ลด)
    public bool hasPierced = false;
    public int maxPierces = 1;
    public int currentPierces = 0;
    public float pierceDamageRetention = 0.5f;
    private Vector3 pierceDirection = Vector3.forward;
    private float pierceDistanceRemaining = 0f;
    private const float MAX_PIERCE_DISTANCE = 16.0f;
    private HashSet<Zombie> hitZombies = new HashSet<Zombie>();

    private Vector3 ballisticStartPos;
    private float ballisticDuration = 1.2f;
    private float ballisticElapsed = 0f;
    private float ballisticArcHeight = 14f;
    private GameObject targetMarkerObj;

    private float lifeTime = 3.0f;

    public void Initialize(Transform targetTransform, float dmg, float spd, bool sniper, bool heal = false, float aoeExtent = 0f)
    {
        target = targetTransform;
        damage = dmg;
        speed = spd;
        isSniper = sniper;
        isHeal = heal;
        aoeHalfExtent = aoeExtent;
        isCircularAOE = false;
        isBallisticICBM = false;
        isGrenade = false;
        isTankShell = false;
        hasPierced = false;
        maxPierces = 1;
        currentPierces = 0;
        pierceDamageRetention = 0.5f;
        pierceDistanceRemaining = 0f;
        hitZombies.Clear();

        if (target != null)
        {
            targetLastPos = target.position + Vector3.up * 0.4f;
        }
        else
        {
            targetLastPos = transform.position + transform.forward * 10f;
        }
    }

    public void InitializeICBM(Vector3 startPos, Vector3 targetWorldPos, float dmg, float circleRadius)
    {
        target = null;
        damage = dmg;
        aoeHalfExtent = circleRadius; // 8.0m radius = 16.0m diameter (8x8 cells circle)
        isCircularAOE = true;
        isBallisticICBM = true;
        ballisticStartPos = startPos;
        targetLastPos = new Vector3(targetWorldPos.x, 0.1f, targetWorldPos.z);
        ballisticElapsed = 0f;
        ballisticDuration = 1.2f;
        ballisticArcHeight = 14f;
        lifeTime = 5.0f;
        hasPierced = false;
        hitZombies.Clear();

        // Simple target marker ring on the ground until impact
        targetMarkerObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        targetMarkerObj.name = "ICBM_TargetMarker_8x8";
        targetMarkerObj.transform.position = new Vector3(targetWorldPos.x, 0.06f, targetWorldPos.z);
        targetMarkerObj.transform.localScale = new Vector3(circleRadius * 2f, 0.03f, circleRadius * 2f);
        Collider c = targetMarkerObj.GetComponent<Collider>();
        if (c != null) Destroy(c);
        Renderer r = targetMarkerObj.GetComponent<Renderer>();
        if (r != null)
        {
            r.material.color = new Color(1.0f, 0.2f, 0.1f, 0.45f);
        }
    }

    public void InitializeGrenade(Vector3 startPos, Vector3 targetWorldPos, float dmg, float halfExtent = 3.0f)
    {
        target = null;
        damage = dmg;
        aoeHalfExtent = halfExtent; // 3.0m half-extent = 6.0m x 6.0m (3x3 blocks square AOE)
        isCircularAOE = false;
        isBallisticICBM = true;
        isGrenade = true;
        ballisticStartPos = startPos;
        targetLastPos = new Vector3(targetWorldPos.x, 0.1f, targetWorldPos.z);
        ballisticElapsed = 0f;
        ballisticDuration = 0.42f;
        ballisticArcHeight = 2.6f;
        lifeTime = 2.5f;
        hasPierced = false;
        hitZombies.Clear();
    }

    void OnDestroy()
    {
        if (targetMarkerObj != null)
        {
            Destroy(targetMarkerObj);
        }
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.Combat)
        {
            return;
        }

        lifeTime -= Time.deltaTime;
        if (lifeTime <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        if (isBallisticICBM)
        {
            ballisticElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(ballisticElapsed / ballisticDuration);
            Vector3 flatPos = Vector3.Lerp(ballisticStartPos, targetLastPos, t);
            float arcY = 4f * ballisticArcHeight * t * (1f - t);
            Vector3 nextPos = new Vector3(flatPos.x, flatPos.y + arcY, flatPos.z);

            Vector3 velDir = nextPos - transform.position;
            if (velDir.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(velDir.normalized);
            }
            transform.position = nextPos;

            if (t >= 1f)
            {
                OnHit(targetLastPos);
            }
            return;
        }

        // --- SNIPER PIERCED STRAIGHT FLIGHT ---
        // Once pierced, bullet continues straight without changing direction to any other target
        if (hasPierced)
        {
            float step = speed * Time.deltaTime;
            Vector3 prevPos = transform.position;
            Vector3 nextPos = prevPos + pierceDirection * step;
            pierceDistanceRemaining -= step;

            // Check if bullet penetrates through any zombie along this straight line segment
            Zombie hitZombie = CheckPierceZombieHit(prevPos, nextPos);
            if (hitZombie != null)
            {
                hitZombies.Add(hitZombie);
                hitZombie.TakeDamage(damage);
                SpawnHitImpactVisual(hitZombie.transform.position, true);

                // If allowed more pierces (e.g. Lv.3 Sniper allows 2 pierces, hitting up to 3 zombies)
                if (currentPierces < maxPierces)
                {
                    currentPierces++;
                    damage *= pierceDamageRetention;
                    transform.position = nextPos + pierceDirection * 0.4f;
                    return;
                }

                Destroy(gameObject);
                return;
            }

            transform.position = nextPos;
            if (pierceDistanceRemaining <= 0f)
            {
                Destroy(gameObject);
                return;
            }
            return;
        }

        // --- NORMAL FLIGHT TOWARDS INITIAL TARGET ---
        if (target != null)
        {
            targetLastPos = target.position + Vector3.up * 0.4f;
        }

        Vector3 dest = targetLastPos;
        Vector3 toDest = dest - transform.position;
        float dist = toDest.magnitude;

        // When reaching impact destination
        if (dist <= speed * Time.deltaTime || dist <= 0.35f)
        {
            OnHit(dest);
            return;
        }

        Vector3 dir = toDest.normalized;
        transform.position += dir * speed * Time.deltaTime;
        if (dir != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(dir);
        }
    }

    void OnHit(Vector3 impactPos)
    {
        if (aoeHalfExtent > 0f && !isHeal)
        {
            Zombie[] allZombies = FindObjectsByType<Zombie>(FindObjectsSortMode.None);
            foreach (Zombie z in allZombies)
            {
                if (z == null) continue;
                if (isCircularAOE)
                {
                    // 8x8 Circular AOE (radius = aoeHalfExtent = 8.0m)
                    float dist2D = Vector2.Distance(
                        new Vector2(z.transform.position.x, z.transform.position.z),
                        new Vector2(impactPos.x, impactPos.z)
                    );
                    if (dist2D <= aoeHalfExtent)
                    {
                        z.TakeDamage(damage);
                    }
                }
                else
                {
                    // 3x3 Cells Square AOE Damage (half-extent = 3.0m)
                    float dx = Mathf.Abs(z.transform.position.x - impactPos.x);
                    float dz = Mathf.Abs(z.transform.position.z - impactPos.z);
                    if (dx <= aoeHalfExtent && dz <= aoeHalfExtent)
                    {
                        z.TakeDamage(damage);
                    }
                }
            }

            SpawnAoeExplosionVisual(impactPos, aoeHalfExtent * 2f, isCircularAOE);
            if (isBallisticICBM || isCircularAOE)
            {
                AudioHelper.PlaySound(SoundEffect.ICBMExplosion, 1.0f);
                AudioHelper.PlaySound(SoundEffect.ExplosionSubBass, 1.0f);
            }
            else if (isTankShell)
            {
                AudioHelper.PlaySound(SoundEffect.TankExplosionDebris, 1.0f);
            }
            else if (isGrenade)
            {
                AudioHelper.PlayRandomSMGGrenade(1.0f);
            }
            else
            {
                AudioHelper.PlayRandomSMGGrenade(0.9f);
            }
            Destroy(gameObject);
            return;
        }

        if (target != null)
        {
            if (isHeal)
            {
                Soldier s = target.GetComponent<Soldier>();
                if (s != null)
                {
                    s.Heal(damage);
                }
                Destroy(gameObject);
                return;
            }
            else
            {
                Zombie z = target.GetComponent<Zombie>();
                if (z != null)
                {
                    z.TakeDamage(damage);
                    hitZombies.Add(z);
                }
            }
        }
        else if (!isHeal)
        {
            // If designated target died right before impact, check nearby zombie at impactPos
            Zombie nearby = CheckPierceZombieHit(transform.position - transform.forward * 0.4f, transform.position + transform.forward * 0.4f);
            if (nearby != null)
            {
                nearby.TakeDamage(damage);
                hitZombies.Add(nearby);
            }
        }

        // --- SNIPER BULLET PENETRATION CHECK ---
        // กระสุนทะลุไปโดนตัวหลังได้ แต่ดาเมจลดลง 50% และไม่เปลี่ยนทิศทาง (ทะลุได้แค่ครั้งเดียว หรือ Lv.3 ทะลุได้ 2 ตัว ดาเมจไม่ลด)
        if (isSniper && currentPierces < maxPierces)
        {
            currentPierces++;
            hasPierced = true;
            damage *= pierceDamageRetention; // ปกติลด 50% (0.5f), แต่ขั้น 3 ดาเมจไม่ลด (1.0f)

            // ไม่เปลี่ยนทิศไปโดนตัวอื่น: ปลดล็อก target และวิ่งตรงไปตามทิศทางเดิมอย่างแท้จริง
            target = null;

            Vector3 forwardFlat = transform.forward;
            forwardFlat.y = 0f;
            if (forwardFlat.sqrMagnitude < 0.001f) forwardFlat = Vector3.left;
            pierceDirection = forwardFlat.normalized;
            transform.rotation = Quaternion.LookRotation(pierceDirection);

            pierceDistanceRemaining = MAX_PIERCE_DISTANCE;
            SpawnHitImpactVisual(impactPos, false);
            AudioHelper.PlaySound(SoundEffect.SniperShoot, 0.35f);

            // Nudge bullet slightly forward past the first zombie to avoid re-colliding
            transform.position += pierceDirection * 0.4f;
            return; // Do NOT Destroy! Continue flying straight to hit zombie behind!
        }

        Destroy(gameObject);
    }

    private Zombie CheckPierceZombieHit(Vector3 fromPos, Vector3 toPos)
    {
        Zombie closestZ = null;
        float minT = float.MaxValue;

        Vector3 segment = toPos - fromPos;
        float segLen = segment.magnitude;
        if (segLen <= 0.0001f) return null;
        Vector3 segDir = segment / segLen;

        IEnumerable<Zombie> zombieList;
        if (WaveManager.Instance != null && WaveManager.Instance.ActiveZombies != null)
        {
            zombieList = WaveManager.Instance.ActiveZombies;
        }
        else
        {
            zombieList = FindObjectsByType<Zombie>(FindObjectsSortMode.None);
        }

        foreach (Zombie z in zombieList)
        {
            if (z == null || z.isDead || hitZombies.Contains(z)) continue;

            Vector3 zPos = z.transform.position;
            Vector3 toZ = zPos - fromPos;
            float t = Vector3.Dot(toZ, segDir);

            // Check if closest point on segment to zombie is within reach
            float clampedT = Mathf.Clamp(t, 0f, segLen);
            Vector3 closest = fromPos + segDir * clampedT;

            float horizDistSq = (closest.x - zPos.x) * (closest.x - zPos.x) + (closest.z - zPos.z) * (closest.z - zPos.z);
            float vertDist = Mathf.Abs(closest.y - zPos.y);

            // Zombie hit cylinder: 0.85m horizontal radius, 1.6m vertical tolerance
            if (horizDistSq <= (0.85f * 0.85f) && vertDist <= 1.6f)
            {
                if (t < minT)
                {
                    minT = t;
                    closestZ = z;
                }
            }
        }

        return closestZ;
    }

    private void SpawnHitImpactVisual(Vector3 pos, bool isPiercedHit)
    {
        GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        spark.name = isPiercedHit ? "Sniper_PierceHit_Spark" : "Sniper_Puncture_Spark";
        spark.transform.position = new Vector3(pos.x, 0.4f, pos.z);
        spark.transform.localScale = isPiercedHit ? new Vector3(0.22f, 0.22f, 0.22f) : new Vector3(0.32f, 0.32f, 0.32f);
        Collider c = spark.GetComponent<Collider>();
        if (c != null) Destroy(c);
        Renderer r = spark.GetComponent<Renderer>();
        if (r != null)
        {
            r.material.color = isPiercedHit ? new Color(0.25f, 0.85f, 1.0f, 0.9f) : new Color(1.0f, 0.95f, 0.3f, 0.9f);
        }
        Destroy(spark, 0.12f);
    }

    private void SpawnAoeExplosionVisual(Vector3 center, float size, bool circular = false)
    {
        // Dynamic 3D Point Light + Additive Ground/Overhead Light Glow Halo
        CrossMapVisualizer.SpawnExplosionLightAndGlow(center, size, circular, isTankShell);

        // Ground AOE blast zone indicator (Cylinder for 8x8 circle ICBM, Cube for 3x3 tank) + bright unlit fireball
        GameObject blastZone = GameObject.CreatePrimitive(circular ? PrimitiveType.Cylinder : PrimitiveType.Cube);
        blastZone.name = circular ? "ICBM_BlastCircle_8x8" : "TankAOE_BlastZone_3x3";
        blastZone.transform.position = new Vector3(center.x, 0.12f, center.z);
        blastZone.transform.localScale = circular
            ? new Vector3(size, 0.08f, size)
            : new Vector3(size, 0.12f, size);
        Collider c1 = blastZone.GetComponent<Collider>();
        if (c1 != null) Destroy(c1);
        Renderer r1 = blastZone.GetComponent<Renderer>();
        if (r1 != null)
        {
            Color zoneCol = circular
                ? new Color(1.0f, 0.45f, 0.12f, 0.78f)
                : new Color(1.0f, 0.55f, 0.15f, 0.72f);
            r1.sharedMaterial = CrossMapVisualizer.CreateEmissiveExplosionMaterial(zoneCol, false, null, 3155);
            r1.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r1.receiveShadows = false;
        }
        Destroy(blastZone, circular ? 0.55f : 0.32f);

        GameObject fireball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        fireball.name = circular ? "ICBM_Fireball" : "TankAOE_Fireball";
        fireball.transform.position = new Vector3(center.x, 0.9f, center.z);
        float fbScale = circular ? size * 0.48f : size * 0.62f;
        fireball.transform.localScale = new Vector3(fbScale, fbScale, fbScale);
        Collider c2 = fireball.GetComponent<Collider>();
        if (c2 != null) Destroy(c2);
        Renderer r2 = fireball.GetComponent<Renderer>();
        if (r2 != null)
        {
            Color fbCol = circular
                ? new Color(1.0f, 0.92f, 0.55f, 0.95f)
                : new Color(1.0f, 0.85f, 0.35f, 0.92f);
            r2.sharedMaterial = CrossMapVisualizer.CreateEmissiveExplosionMaterial(fbCol, true, null, 3160);
            r2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r2.receiveShadows = false;
        }
        Destroy(fireball, circular ? 0.42f : 0.26f);
    }
}
