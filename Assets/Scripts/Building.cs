using UnityEngine;

public class Building : MonoBehaviour
{
    public enum BuildingType { SMGTent, SniperTower, Barricade, HealStation }

    [Header("Building Config")]
    public BuildingType type = BuildingType.SMGTent;
    public int goldCost = 30;
    public int materialCost = 10;
    public float spawnInterval = 8.0f; // Slower human spawn rate for more tactical value
    public GameObject soldierPrefab;

    [Header("Runtime")]
    public int gridX;
    public int gridY;

    private float spawnTimer;
    private bool isActive = true;

    void Start()
    {
        // Reduced human spawn rate: units take longer to produce
        switch (type)
        {
            case BuildingType.SMGTent:
                spawnInterval = 8.0f; // 1 trooper every 8.0s
                break;
            case BuildingType.SniperTower:
                spawnInterval = 10.0f; // 1 sniper every 10.0s
                break;
            case BuildingType.HealStation:
                spawnInterval = 12.0f; // 1 medic every 12.0s
                break;
            default:
                spawnInterval = 8.0f;
                break;
        }

        spawnTimer = spawnInterval;
    }

    void Update()
    {
        if (!isActive) return;
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.CurrentState != GameManager.GameState.Combat) return;
        if (type == BuildingType.Barricade) return; // Barricade doesn't spawn units

        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            SpawnSoldier();
            spawnTimer = spawnInterval;
        }
    }

    void SpawnSoldier()
    {
        Vector3 spawnPos = transform.position + Vector3.right * 1.5f;
        GameObject go;

        if (soldierPrefab != null)
        {
            go = Instantiate(soldierPrefab, spawnPos, Quaternion.identity);
        }
        else
        {
            // Procedural fallback: blue/cyan capsule for soldier
            go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = $"Soldier_{type}";
            go.transform.position = spawnPos;
            go.transform.localScale = new Vector3(0.6f, 0.8f, 0.6f);

            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                Color c = Color.blue;
                if (type == BuildingType.SMGTent) c = new Color(0.2f, 0.6f, 1f); // Light blue
                else if (type == BuildingType.SniperTower) c = new Color(0.1f, 0.2f, 0.8f); // Dark blue
                else if (type == BuildingType.HealStation) c = new Color(0.2f, 1f, 0.6f); // Mint green
                rend.material.color = c;
            }

            go.AddComponent<Soldier>();
        }

        Soldier soldier = go.GetComponent<Soldier>();
        if (soldier != null)
        {
            switch (type)
            {
                case BuildingType.SMGTent:
                    // 3-Round Burst Fire (3 shots in 0.24s, pause 0.66s -> total cycle 0.90s -> 33.3 DPS!)
                    soldier.isBurstMode = true;
                    soldier.burstCount = 3;
                    soldier.burstInnerDelay = 0.12f;
                    soldier.burstPauseDelay = 0.66f;
                    soldier.damage = 10f; // 10 dmg * 5 shots = 50 to kill zombie
                    soldier.range = 8f;
                    soldier.maxHealth = 50f;
                    soldier.health = 50f;
                    break;

                case BuildingType.SniperTower:
                    // Single shot precision (25 dmg * 2 = 50 to kill zombie, interval 0.75s -> 33.3 DPS!)
                    soldier.isBurstMode = false;
                    soldier.damage = 25f;
                    soldier.fireRate = 0.75f;
                    soldier.range = 14f;
                    soldier.maxHealth = 45f;
                    soldier.health = 45f;
                    break;

                case BuildingType.HealStation:
                    soldier.isBurstMode = false;
                    soldier.damage = 6f;
                    soldier.fireRate = 0.6f;
                    soldier.range = 7f;
                    soldier.maxHealth = 70f;
                    soldier.health = 70f;
                    soldier.isHealer = true;
                    break;
            }
        }
    }

    public static int GetGoldCost(BuildingType t)
    {
        switch (t)
        {
            case BuildingType.SMGTent: return 30;
            case BuildingType.SniperTower: return 60;
            case BuildingType.Barricade: return 15;
            case BuildingType.HealStation: return 50;
            default: return 30;
        }
    }

    public static int GetMaterialCost(BuildingType t)
    {
        switch (t)
        {
            case BuildingType.SMGTent: return 10;
            case BuildingType.SniperTower: return 25;
            case BuildingType.Barricade: return 5;
            case BuildingType.HealStation: return 20;
            default: return 10;
        }
    }
}
