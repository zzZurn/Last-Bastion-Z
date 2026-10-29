using UnityEngine;

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

    private float attackTimer;
    private Soldier targetSoldier;
    private bool isDead = false;
    private float retargetTimer = 0f;

    // HP Bar texture cache
    private static Texture2D hpTexture;
    private static GUIStyle bossLabelStyle;

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

        // Periodically refresh target or find target if current target is gone/dead
        retargetTimer -= Time.deltaTime;
        if (targetSoldier == null || retargetTimer <= 0f)
        {
            FindTargetSoldier();
            retargetTimer = 0.2f; // Retarget faster (was 0.35s) for more responsive hunting
        }

        // --- ZOMBIES ACTIVELY CHASE & ATTACK SOLDIERS ---
        if (targetSoldier != null)
        {
            float dist = Vector3.Distance(transform.position, targetSoldier.transform.position);
            Vector3 toSoldier = (targetSoldier.transform.position - transform.position);
            toSoldier.y = 0; // Flat horizontal movement

            if (dist > attackRange)
            {
                // Chase towards the soldier — full speed, no hesitation!
                if (toSoldier != Vector3.zero)
                {
                    Vector3 moveDir = toSoldier.normalized;
                    transform.position += moveDir * moveSpeed * Time.deltaTime;
                    transform.rotation = Quaternion.LookRotation(moveDir);
                }
            }
            else
            {
                // In melee attack range: face soldier and strike
                if (toSoldier != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(toSoldier.normalized);
                }

                attackTimer -= Time.deltaTime;
                if (attackTimer <= 0f)
                {
                    targetSoldier.TakeDamage(damage);
                    attackTimer = attackRate;
                }
            }
        }
        else
        {
            // No soldiers alive: march left toward the fortress (fallback)
            transform.position += Vector3.left * moveSpeed * Time.deltaTime;
            transform.rotation = Quaternion.LookRotation(Vector3.left);

            // If reached deep into base, breach defense
            if (transform.position.x <= -24f)
            {
                Debug.LogWarning("[Zombie] Breached the fortress defense line!");
                Die(false);
            }
        }
    }

    void FindTargetSoldier()
    {
        float closestDist = Mathf.Infinity;
        Soldier bestTarget = null;
        Soldier[] soldiers = FindObjectsByType<Soldier>(FindObjectsSortMode.None);

        foreach (Soldier s in soldiers)
        {
            if (s == null) continue;
            float d = Vector3.Distance(transform.position, s.transform.position);
            // No distance limit — zombies hunt ANY soldier on the battlefield!
            if (d < closestDist)
            {
                closestDist = d;
                bestTarget = s;
            }
        }

        targetSoldier = bestTarget;
    }

    public void TakeDamage(float dmg)
    {
        if (isDead) return;
        currentHealth -= dmg;
        if (currentHealth <= 0f)
        {
            Die(true);
        }
    }

    void Die(bool byPlayer)
    {
        if (isDead) return;
        isDead = true;

        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.OnZombieDied(this);
        }

        if (byPlayer && ResourceManager.Instance != null)
        {
            // Reduced bonus gold drop for a tighter economy
            int bonus = (maxHealth >= 200f) ? 10 : ((maxHealth >= 90f) ? 3 : (Random.value < 0.12f ? 1 : 0));
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

        // Offset slightly forward (+Z in top-down view is "up" on screen)
        float zOffset = (maxHealth >= 200f) ? 1.6f : 1.1f;
        Vector3 worldPos = transform.position + new Vector3(0, 0, zOffset);
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

        if (screenPos.z <= 0) return;

        bool isBoss = maxHealth >= 200f;
        bool isElite = maxHealth >= 90f;

        float barW = isBoss ? 64f : (isElite ? 48f : 38f);
        float barH = isBoss ? 9f : (isElite ? 7f : 6f);
        float x = screenPos.x - barW / 2f;
        float y = Screen.height - screenPos.y;

        if (hpTexture == null)
        {
            hpTexture = new Texture2D(1, 1);
            hpTexture.SetPixel(0, 0, Color.white);
            hpTexture.Apply();
        }

        // Draw Boss Title above bar
        if (isBoss)
        {
            if (bossLabelStyle == null)
            {
                bossLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                bossLabelStyle.normal.textColor = new Color(1.0f, 0.3f, 0.3f);
            }
            GUI.Label(new Rect(x - 20f, y - 16f, barW + 40f, 16f), "💀 TYRANT BOSS", bossLabelStyle);
        }

        float pct = Mathf.Clamp01(currentHealth / maxHealth);

        // Background (Dark)
        Color prevColor = GUI.color;
        GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
        GUI.DrawTexture(new Rect(x - 1, y - 1, barW + 2, barH + 2), hpTexture);

        // Health fill
        Color healthCol = isBoss
            ? new Color(0.9f, 0.2f, 0.7f) // Purple for boss
            : (pct > 0.5f ? new Color(0.2f, 0.9f, 0.2f) : (pct > 0.25f ? new Color(1f, 0.8f, 0.1f) : new Color(0.9f, 0.2f, 0.2f)));

        GUI.color = healthCol;
        GUI.DrawTexture(new Rect(x, y, barW * pct, barH), hpTexture);

        GUI.color = prevColor;
    }
}
