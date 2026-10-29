using UnityEngine;
using System.Collections;

public class Soldier : MonoBehaviour
{
    [Header("Stats")]
    public float maxHealth = 50f;
    public float health = 50f;
    public float damage = 10f;
    public float fireRate = 0.75f;
    public float range = 8f;
    public float moveSpeed = 3f;
    public bool isHealer = false;

    [Header("Burst Firing (SMG)")]
    public bool isBurstMode = false;
    public int burstCount = 3;
    public float burstInnerDelay = 0.12f;
    public float burstPauseDelay = 0.66f;
    private bool isFiringBurst = false;

    private float fireTimer;
    private Transform target;
    private bool isDead;

    // HP Bar texture cache
    private static Texture2D hpTexture;

    void Start()
    {
        if (maxHealth <= 0f) maxHealth = 50f;
        health = maxHealth;
    }

    void Update()
    {
        if (isDead) return;
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
                // Move toward target
                Vector3 dir = (target.position - transform.position).normalized;
                transform.position += dir * moveSpeed * Time.deltaTime;
                if (dir != Vector3.zero) transform.rotation = Quaternion.LookRotation(dir);
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
        }
        else
        {
            // Patrol forward (right)
            transform.position += Vector3.right * moveSpeed * 0.5f * Time.deltaTime;
            transform.rotation = Quaternion.LookRotation(Vector3.right);
        }

        // Cleanup if too far
        if (transform.position.x > 30f) Destroy(gameObject);
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

        // Pause/cooldown between bursts (0.66s) -> Total cycle 0.90s -> Exactly 33.33 DPS!
        yield return new WaitForSeconds(burstPauseDelay);
        isFiringBurst = false;
    }

    void SpawnBullet(Transform targetTransform)
    {
        if (targetTransform == null) return;

        Vector3 spawnPos = transform.position + Vector3.up * 0.35f + transform.forward * 0.4f;

        // Create visible projectile with trajectory
        GameObject bObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bObj.name = isBurstMode ? "SMG_Tracer" : (damage >= 20f ? "Sniper_Tracer" : "Medic_Bullet");
        bObj.transform.position = spawnPos;

        // Remove collider from bullet
        Collider c = bObj.GetComponent<Collider>();
        if (c != null) Destroy(c);

        Renderer rend = bObj.GetComponent<Renderer>();
        if (rend != null)
        {
            if (damage >= 20f)
            {
                // Sniper: High-velocity cyan/white supersonic tracer
                bObj.transform.localScale = new Vector3(0.2f, 0.2f, 0.65f);
                rend.material.color = new Color(0.35f, 0.9f, 1.0f);
            }
            else
            {
                // SMG / pistol: Glowing golden-yellow tracer
                bObj.transform.localScale = new Vector3(0.16f, 0.16f, 0.36f);
                rend.material.color = new Color(1.0f, 0.85f, 0.2f);
            }
        }

        Bullet bullet = bObj.AddComponent<Bullet>();
        float bSpeed = (damage >= 20f) ? 42f : 24f; // Real projectile travel speed (m/s)
        bullet.Initialize(targetTransform, damage, bSpeed, damage >= 20f);
    }

    void FindTarget()
    {
        float closestDist = Mathf.Infinity;
        target = null;
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

    public void TakeDamage(float dmg)
    {
        health -= dmg;
        if (health <= 0f)
        {
            isDead = true;
            Destroy(gameObject, 0.2f);
        }
    }

    void OnGUI()
    {
        if (isDead || health <= 0f) return;
        if (Camera.main == null) return;

        // Offset slightly forward (+Z in top-down view is "up" on screen)
        Vector3 worldPos = transform.position + new Vector3(0, 0, 1.0f);
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

        if (screenPos.z <= 0) return;

        float barW = 34f;
        float barH = 5f;
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

        // Health fill (Cyan / Blue for friendly soldiers)
        GUI.color = new Color(0.2f, 0.7f, 1f);
        GUI.DrawTexture(new Rect(x, y, barW * pct, barH), hpTexture);

        GUI.color = prevColor;
    }
}
