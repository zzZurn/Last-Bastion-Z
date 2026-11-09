using UnityEngine;
using System.Collections.Generic;

public class Building : MonoBehaviour
{
    public enum BuildingType { SMGTent, SniperTower, Barricade, HealStation, SpecialForceDepot, LightTankFactory, MissileFoundry, SupplyDepot, FrontlineSniperTower }

    public static readonly List<Building> ActiveBuildings = new List<Building>();
    private static readonly Dictionary<BuildingType, int> upgradeLevels = new Dictionary<BuildingType, int>();
    public const int MAX_ACTIVE_SNIPERS = 15; // Nerf: Max 15 active snipers spawned on the battlefield

    [Header("Building Config")]
    public BuildingType type = BuildingType.SMGTent;
    public int goldCost = 30;
    public int materialCost = 10;
    public float maxHealth = 100f;
    public float currentHealth = 100f;
    public float spawnInterval = 14.0f; // Nerfed slower human spawn rate
    public int maxSpawnCount = 0; // 0 = unlimited, >0 = capped (e.g. 6 for Guardian)
    public int spawnedCount = 0;
    public GameObject soldierPrefab;

    [Header("Runtime")]
    public int gridX;
    public int gridY;

    private float spawnTimer;
    private float turretFireTimer;
    private Transform turretMountTransform;
    private bool isActive = true;
    private static Texture2D hpTexture;

    void Awake()
    {
        if (!ActiveBuildings.Contains(this))
        {
            ActiveBuildings.Add(this);
        }
    }

    void OnDestroy()
    {
        ActiveBuildings.Remove(this);
    }

    void Start()
    {
        switch (type)
        {
            case BuildingType.SMGTent:
                maxHealth = 80f;
                spawnInterval = 14.0f; // Nerfed further: 1 trooper every 14.0s (was 11.0s)
                maxSpawnCount = 0;
                break;
            case BuildingType.SniperTower:
                maxHealth = 70f;
                spawnInterval = 18.0f; // Nerfed further: 1 sniper every 18.0s (was 14.0s)
                maxSpawnCount = MAX_ACTIVE_SNIPERS;
                break;
            case BuildingType.HealStation:
                maxHealth = 95f;
                spawnInterval = 20.0f; // Nerfed further: 1 medic every 20.0s (was 16.0s)
                maxSpawnCount = 0;
                break;
            case BuildingType.SpecialForceDepot:
                maxHealth = 110f;
                spawnInterval = 13.0f; // Spawns Guardian every 13.0s, max 6
                maxSpawnCount = 6;
                break;
            case BuildingType.LightTankFactory:
                maxHealth = 260f;
                spawnInterval = 22.0f; // Spawns Light Tank every 22.0s, max 4
                maxSpawnCount = 4;
                break;
            case BuildingType.MissileFoundry:
                maxHealth = 350f;
                spawnInterval = 25.0f; // Spawns 1 ICBM Launcher only (max 1)
                maxSpawnCount = 1;
                break;
            case BuildingType.SupplyDepot:
                maxHealth = 130f; // Increases Max Supply (SUP) cap by +10 per depot
                spawnInterval = 999f;
                maxSpawnCount = 0;
                break;
            case BuildingType.Barricade:
                maxHealth = 220f; // Sturdy defensive wall
                spawnInterval = 999f;
                maxSpawnCount = 0;
                break;
            case BuildingType.FrontlineSniperTower:
                maxHealth = 160f; // Tall steel sniper watchtower
                spawnInterval = 999f;
                maxSpawnCount = 0;
                turretMountTransform = transform.Find("BuildingVisual/TurretMount");
                break;
            default:
                maxHealth = 80f;
                spawnInterval = 14.0f;
                maxSpawnCount = 0;
                break;
        }

        currentHealth = maxHealth;
        spawnTimer = (type == BuildingType.MissileFoundry) ? 1.5f : spawnInterval;
        spawnedCount = 0;
    }

    public void TakeDamage(float dmg)
    {
        if (currentHealth <= 0f) return;
        currentHealth -= dmg;
        AudioHelper.PlaySound(SoundEffect.Hit, 0.4f);
        if (currentHealth <= 0f)
        {
            Demolish();
        }
    }

    public void Demolish()
    {
        ActiveBuildings.Remove(this);
        AudioHelper.PlaySound(SoundEffect.BuildingDemolish, 0.7f);
        if (GridManager.Instance != null)
        {
            GridManager.Instance.RemoveBuilding(gridX, gridY);
        }
        Destroy(gameObject);
    }

    // Supply blocked style
    private static GUIStyle supBlockStyle;

    void OnGUI()
    {
        if (Camera.main == null) return;

        // Visual indicator when building is ready to spawn but blocked by Supply cap
        if (isActive && GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.GameState.Combat && !IsFrontlineFortification(type) && type != BuildingType.SupplyDepot)
        {
            int supCost = GetUnitSupplyCost(type);
            if (spawnTimer <= 0.05f && !Soldier.CanSpawnSupply(supCost))
            {
                float yOff = (type == BuildingType.LightTankFactory || type == BuildingType.MissileFoundry) ? 3.4f : 2.0f;
                Vector3 scr = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * yOff);
                if (scr.z > 0)
                {
                    if (supBlockStyle == null)
                    {
                        supBlockStyle = new GUIStyle(GUI.skin.label)
                        {
                            fontSize = 10,
                            fontStyle = FontStyle.Bold,
                            alignment = TextAnchor.MiddleCenter
                        };
                    }
                    Color prevC = GUI.color;
                    GUI.color = new Color(1f, 0.35f, 0.35f);
                    GUI.Label(new Rect(scr.x - 70f, Screen.height - scr.y - 14f, 140f, 20f), $"⚠️ SUP FULL ({Soldier.MAX_SUPPLY})", supBlockStyle);
                    GUI.color = prevC;
                }
            }
        }

        if (currentHealth >= maxHealth || currentHealth <= 0f) return;

        float yOffset = (type == BuildingType.FrontlineSniperTower) ? 3.8f : ((type == BuildingType.LightTankFactory || type == BuildingType.MissileFoundry) ? 3.2f : 1.8f);
        Vector3 worldPos = transform.position + Vector3.up * yOffset;
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);
        if (screenPos.z <= 0) return;

        float barW = 42f;
        float barH = 5f;
        float x = screenPos.x - barW / 2f;
        float y = Screen.height - screenPos.y;

        if (hpTexture == null)
        {
            hpTexture = new Texture2D(1, 1);
            hpTexture.SetPixel(0, 0, Color.white);
            hpTexture.Apply();
        }

        float pct = Mathf.Clamp01(currentHealth / maxHealth);
        Color prevColor = GUI.color;
        GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
        GUI.DrawTexture(new Rect(x - 1, y - 1, barW + 2, barH + 2), hpTexture);

        GUI.color = new Color(0.95f, 0.65f, 0.2f);
        GUI.DrawTexture(new Rect(x, y, barW * pct, barH), hpTexture);
        GUI.color = prevColor;
    }

    void Update()
    {
        if (!isActive) return;
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.CurrentState != GameManager.GameState.Combat) return;

        if (type == BuildingType.FrontlineSniperTower)
        {
            UpdateFrontlineSniperTower();
            return;
        }

        if (type == BuildingType.Barricade || type == BuildingType.SupplyDepot) return; // Doesn't spawn units

        // Always enforce unit spawn limits (15 Snipers max, 6 Guardians, 4 Tanks, 1 ICBM Launcher max!)
        if (type == BuildingType.SniperTower && CountActiveSnipers() >= maxSpawnCount) return;
        if (type == BuildingType.SpecialForceDepot && CountActiveGuardians() >= maxSpawnCount) return;
        if (type == BuildingType.LightTankFactory && CountActiveLightTanks() >= maxSpawnCount) return;
        if (type == BuildingType.MissileFoundry && (spawnedCount >= 1 || CountActiveICBMLaunchers() >= 1)) return;

        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            int supCost = GetUnitSupplyCost(type);
            // Check if current SUP + cost <= 250 (supply cap check)
            if (Soldier.CanSpawnSupply(supCost))
            {
                SpawnSoldier();
                spawnTimer = spawnInterval;
            }
            else
            {
                // Supply capped (250): pause timer at 0 so it immediately spawns when supply frees up
                spawnTimer = 0f;
            }
        }
    }

    public static int CountActiveSnipers()
    {
        int count = 0;
        for (int i = 0; i < Soldier.ActiveSoldiers.Count; i++)
        {
            var s = Soldier.ActiveSoldiers[i];
            if (s != null && !s.isDead && s.isSniper) count++;
        }
        return count;
    }

    private int CountActiveGuardians()
    {
        int count = 0;
        Soldier[] soldiers = FindObjectsByType<Soldier>(FindObjectsSortMode.None);
        foreach (Soldier s in soldiers)
        {
            if (s != null && s.isGuardian) count++;
        }
        return count;
    }

    private int CountActiveLightTanks()
    {
        int count = 0;
        Soldier[] soldiers = FindObjectsByType<Soldier>(FindObjectsSortMode.None);
        foreach (Soldier s in soldiers)
        {
            if (s != null && s.isLightTank) count++;
        }
        return count;
    }

    private int CountActiveICBMLaunchers()
    {
        int count = 0;
        Soldier[] soldiers = FindObjectsByType<Soldier>(FindObjectsSortMode.None);
        foreach (Soldier s in soldiers)
        {
            if (s != null && s.isICBMLauncher) count++;
        }
        return count;
    }

    private void UpdateFrontlineSniperTower()
    {
        // Frontline Sniper Tower stats:
        // - Range: 14.0m (same as mobile Sniper)
        // - Damage: 75.0 dmg / shot (2x mobile Sniper's 37.5 dmg!)
        // - Fire Rate: 1.0s interval (75.0 DPS)
        // - Tradeoff: Non-piercing shots & stationary structure
        const float TOWER_RANGE = 14.0f;
        const float TOWER_FIRE_INTERVAL = 1.0f;
        const float TOWER_DAMAGE = 75.0f;

        if (turretFireTimer > 0f)
        {
            turretFireTimer -= Time.deltaTime;
        }

        Zombie closestZombie = null;
        float closestDist = Mathf.Infinity;
        Vector2 towerPos2D = new Vector2(transform.position.x, transform.position.z);

        Zombie[] zombies = FindObjectsByType<Zombie>(FindObjectsSortMode.None);
        foreach (Zombie z in zombies)
        {
            if (z == null) continue;
            float d2D = Vector2.Distance(towerPos2D, new Vector2(z.transform.position.x, z.transform.position.z));
            if (d2D <= TOWER_RANGE && d2D < closestDist)
            {
                closestDist = d2D;
                closestZombie = z;
            }
        }

        if (closestZombie != null)
        {
            Vector3 aimDir = closestZombie.transform.position - transform.position;
            aimDir.y = 0f;
            if (aimDir.sqrMagnitude > 0.001f)
            {
                if (turretMountTransform == null)
                {
                    turretMountTransform = transform.Find("BuildingVisual/TurretMount");
                }
                if (turretMountTransform != null)
                {
                    turretMountTransform.rotation = Quaternion.LookRotation(aimDir.normalized, Vector3.up);
                }
            }

            if (turretFireTimer <= 0f)
            {
                FireTowerSniperShot(closestZombie.transform, TOWER_DAMAGE);
                turretFireTimer = TOWER_FIRE_INTERVAL;
            }
        }
    }

    private void FireTowerSniperShot(Transform targetTransform, float dmg)
    {
        if (targetTransform == null) return;

        Vector3 aimDir = targetTransform.position - transform.position;
        aimDir.y = 0f;
        if (aimDir.sqrMagnitude > 0.001f) aimDir.Normalize();
        else aimDir = Vector3.right;

        // Fire from the elevated steel tower platform (y = 2.85m)
        Vector3 spawnPos = transform.position + Vector3.up * 2.85f + aimDir * 0.75f;

        GameObject bObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bObj.name = "Tower_HeavySniper_Round";
        bObj.transform.position = spawnPos;
        bObj.transform.localScale = new Vector3(0.24f, 0.24f, 0.78f);

        Collider c = bObj.GetComponent<Collider>();
        if (c != null) Destroy(c);

        Renderer rend = bObj.GetComponent<Renderer>();
        if (rend != null)
        {
            // High-caliber golden-amber tracer to distinguish 2x damage non-piercing tower rounds
            rend.material = PlacementManager.CreateSafeMaterial(new Color(1.0f, 0.78f, 0.22f), false);
        }

        // sniper = false -> NO piercing through 1st enemy!
        Bullet bullet = bObj.AddComponent<Bullet>();
        bullet.Initialize(targetTransform, dmg, 42f, false, false, 0f);

        AudioHelper.PlayRandomSniperShoot(0.60f);
    }

    public void ForceSpawnUnitImmediate()
    {
        if (IsFrontlineFortification(type) || type == BuildingType.SupplyDepot) return;
        SpawnSoldier(true);
    }

    void SpawnSoldier(bool bypassCap = false)
    {
        // Strictly limit ICBM Launcher to 1 active unit maximum
        if (type == BuildingType.MissileFoundry && (spawnedCount >= 1 || CountActiveICBMLaunchers() >= 1))
        {
            return;
        }

        if (!bypassCap)
        {
            int supCost = GetUnitSupplyCost(type);
            if (!Soldier.CanSpawnSupply(supCost)) return;

            if (type == BuildingType.SniperTower && CountActiveSnipers() >= maxSpawnCount) return;
            if (type == BuildingType.SpecialForceDepot && CountActiveGuardians() >= maxSpawnCount) return;
            if (type == BuildingType.LightTankFactory && CountActiveLightTanks() >= maxSpawnCount) return;
            if (type == BuildingType.MissileFoundry && (spawnedCount >= 1 || CountActiveICBMLaunchers() >= 1)) return;
        }

        float spawnOffsetX = (type == BuildingType.MissileFoundry) ? 5.8f : ((type == BuildingType.LightTankFactory) ? 2.6f : 1.5f);
        Vector3 spawnPos = transform.position + Vector3.right * spawnOffsetX;
        spawnPos = Zombie.ClampPositionToCross(spawnPos);
        GameObject go;

        if (soldierPrefab != null)
        {
            go = Instantiate(soldierPrefab, spawnPos, Quaternion.identity);
        }
        else
        {
            // Procedural block soldier / tank / ICBM launcher
            go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = type == BuildingType.SpecialForceDepot
                ? "Guardian"
                : (type == BuildingType.LightTankFactory
                    ? "LightTank"
                    : (type == BuildingType.MissileFoundry ? "ICBMLauncher" : $"Soldier_{type}"));
            go.transform.position = spawnPos;
            go.transform.localScale = Vector3.one;

            UnitBlockVisualizer.AttachSoldierVisuals(go, type);
            go.transform.rotation = Quaternion.LookRotation(Vector3.right); // Face East toward zombies immediately
            go.AddComponent<Soldier>();
        }

        Soldier soldier = go.GetComponent<Soldier>();
        if (soldier != null)
        {
            switch (type)
            {
                case BuildingType.SMGTent:
                    // 3-Round Burst Fire (3 shots in 0.24s, pause 0.66s -> total cycle 0.90s -> 33.3 DPS!)
                    // Upgrades: Lv.1 (+5% dmg), Lv.2 (+10% dmg), Lv.3 (+20% dmg + 7% Reload Grenade 3x3 AOE at 2x DPS)
                    int smgLv = GetUpgradeLevel(BuildingType.SMGTent);
                    float smgDmgMult = GetDamageMultiplier(BuildingType.SMGTent);
                    soldier.isSMGTrooper = true;
                    soldier.upgradeLevel = smgLv;
                    soldier.hasReloadGrenadeAbility = (smgLv >= 3);
                    soldier.isBurstMode = true;
                    soldier.burstCount = 3;
                    soldier.burstInnerDelay = 0.12f;
                    soldier.burstPauseDelay = 0.66f;
                    soldier.damage = 10f * smgDmgMult; // Lv0: 10.0, Lv1: 10.5, Lv2: 11.0, Lv3: 12.0
                    soldier.range = 8f;
                    soldier.moveSpeed = 3.0f;
                    soldier.maxHealth = 50f;
                    soldier.health = 50f;
                    break;

                case BuildingType.SniperTower:
                    // Sniper Rebalance: +50% Damage (25 -> 37.5), -25% Firerate (interval 0.75s -> 1.0s -> 37.5 DPS!)
                    // Upgrades: Lv.1 (+15% dmg), Lv.2 (+20% more dmg -> +35%), Lv.3 (+125% more dmg -> +160%, Firerate -20%, Pierces 2 enemies with NO dmg dropoff)
                    int sniperLv = GetUpgradeLevel(BuildingType.SniperTower);
                    float sniperDmgMult = GetDamageMultiplier(BuildingType.SniperTower);
                    soldier.isSniper = true;
                    soldier.isBurstMode = false;
                    soldier.upgradeLevel = sniperLv;
                    soldier.damage = 37.5f * sniperDmgMult;
                    soldier.fireRate = (sniperLv >= 3) ? 1.25f : 1.0f; // Lv.3: Firerate -20% (interval 1.0s -> 1.25s)
                    soldier.range = 14f;
                    soldier.moveSpeed = 3.0f;
                    soldier.maxHealth = 45f;
                    soldier.health = 45f;
                    break;

                case BuildingType.HealStation:
                    soldier.isBurstMode = false;
                    soldier.damage = 6f;
                    soldier.fireRate = 0.6f;
                    soldier.range = 7f;
                    soldier.moveSpeed = 3.0f;
                    soldier.maxHealth = 70f;
                    soldier.health = 70f;
                    soldier.isHealer = true;
                    break;

                case BuildingType.SpecialForceDepot:
                    // Guardian: Faster than Sniper (4.5 vs 3.0), Very Low DPS (4 dmg / 0.8s = 5.0 DPS), 3.5x HP (50 * 3.5 = 175 HP)
                    int specLv = GetUpgradeLevel(BuildingType.SpecialForceDepot);
                    float hpMult = GetHealthMultiplier(BuildingType.SpecialForceDepot);
                    soldier.isBurstMode = false;
                    soldier.isGuardian = true;
                    soldier.upgradeLevel = specLv;
                    soldier.hasShieldBashCounter = (specLv >= 3);
                    soldier.moveSpeed = 4.5f;
                    soldier.damage = 4f;
                    soldier.fireRate = 0.8f; // 5.0 DPS (very low DPS)
                    soldier.range = 3.0f; // Short-range frontline tank
                    soldier.maxHealth = 175f * hpMult; // Base 175 HP * upgrade multiplier
                    soldier.health = soldier.maxHealth;
                    break;

                case BuildingType.LightTankFactory:
                    // Light Tank: Very slow (1.1 m/s), 3x DPS (400 dmg / 4.0s = 100.0 DPS = 3x of 33.3 DPS!), 4s/shot, 3x3 cells AOE (halfExtent = 3.0m)
                    soldier.isBurstMode = false;
                    soldier.isLightTank = true;
                    soldier.moveSpeed = 1.1f; // Very slow move speed
                    soldier.damage = 400f;    // 400 dmg / 4.0s = 100.0 DPS (3x of 33.33 DPS!)
                    soldier.fireRate = 4.0f;  // 4 seconds per shot
                    soldier.range = 12.0f;
                    soldier.aoeHalfExtent = 3.0f; // 3x3 cells = 6m x 6m (half-extent 3.0m)
                    soldier.maxHealth = 280f;
                    soldier.health = 280f;
                    break;

                case BuildingType.MissileFoundry:
                    // ICBM Launcher: Cob Cannon manual global targeting, 85 DPS * 20s reload = 1,700 damage per missile, 8x8 circular AOE (radius = 8.0m)
                    soldier.isBurstMode = false;
                    soldier.isICBMLauncher = true;
                    soldier.moveSpeed = 0.9f;
                    soldier.damage = 1700f;   // 85 DPS * 20.0s reload = 1,700 single-shot damage!
                    soldier.fireRate = 20.0f; // 20 seconds reload cooldown
                    soldier.range = 999f;     // Global full-map range
                    soldier.aoeHalfExtent = 8.0f; // 8x8 circular AOE (diameter = 8 cells = 16m -> radius = 4 cells = 8.0m)
                    soldier.maxHealth = 320f;
                    soldier.health = 320f;
                    soldier.icbmStopX = transform.position.x + 8.5f;
                    break;
            }

            soldier.supplyCost = GetUnitSupplyCost(type);
        }

        spawnedCount++;
    }

    /// <summary>
    /// Supply (SUP) cost per unit.
    /// SMG: 1 SUP | Sniper: 2 SUP | Medic: 2 SUP | Guardian: 3 SUP | Light Tank: 6 SUP | ICBM: 15 SUP
    /// </summary>
    public static int GetUnitSupplyCost(BuildingType t)
    {
        switch (t)
        {
            case BuildingType.SMGTent: return 1;           // SMG: 1 SUP
            case BuildingType.SniperTower: return 2;       // Sniper: 2 SUP
            case BuildingType.HealStation: return 2;       // Medic: 2 SUP
            case BuildingType.SpecialForceDepot: return 3; // Guardian: 3 SUP
            case BuildingType.LightTankFactory: return 6;  // Light Tank: 6 SUP
            case BuildingType.MissileFoundry: return 15;   // ICBM Launcher: 15 SUP
            default: return 0;
        }
    }

    /// <summary>
    /// Returns the number of currently active buildings of the given type placed on the battlefield.
    /// </summary>
    public static int GetActiveBuildingCount(BuildingType t)
    {
        int count = 0;
        for (int i = ActiveBuildings.Count - 1; i >= 0; i--)
        {
            Building b = ActiveBuildings[i];
            if (b == null)
            {
                ActiveBuildings.RemoveAt(i);
                continue;
            }
            if (b.currentHealth > 0f && b.type == t)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Returns the maximum allowed placed buildings of this type on the map.
    /// SMG Tent capped at 10 max.
    /// Sniper's Nest (SniperTower) capped at 6 max.
    /// Light Tank Factory is capped at 4 max.
    /// Missile Foundry is capped at 1 max.
    /// 0 = unlimited.
    /// </summary>
    public static int GetMaxBuildingPlacementLimit(BuildingType t)
    {
        if (t == BuildingType.SMGTent) return 10;
        if (t == BuildingType.SniperTower) return 6; // Nerfed: Limit building placement to 6 bases max
        if (t == BuildingType.LightTankFactory) return 4;
        if (t == BuildingType.MissileFoundry) return 1;
        return 0;
    }

    /// <summary>
    /// Counts active Supply Depots currently placed on the battlefield.
    /// Each Supply Depot increases Max SUP by +10.
    /// </summary>
    public static int GetActiveSupplyDepotCount()
    {
        int count = 0;
        for (int i = ActiveBuildings.Count - 1; i >= 0; i--)
        {
            Building b = ActiveBuildings[i];
            if (b == null)
            {
                ActiveBuildings.RemoveAt(i);
                continue;
            }
            if (b.currentHealth > 0f && b.type == BuildingType.SupplyDepot)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Total Supply Depots owned (placed on map + stored in stock), used for +50G scaling cost.
    /// </summary>
    public static int GetTotalSupplyDepotsOwned()
    {
        int onMap = GetActiveSupplyDepotCount();
        int inStock = (PlacementManager.Instance != null) ? PlacementManager.Instance.GetStoredCount(BuildingType.SupplyDepot) : 0;
        return onMap + inStock;
    }

    public static int GetGoldCost(BuildingType t)
    {
        switch (t)
        {
            case BuildingType.SMGTent: return 30;
            case BuildingType.SniperTower: return 60;
            case BuildingType.Barricade: return 15;
            case BuildingType.HealStation: return 50;
            case BuildingType.SpecialForceDepot: return 50;
            case BuildingType.LightTankFactory: return 600;
            case BuildingType.MissileFoundry: return 900;
            case BuildingType.SupplyDepot: return 100 + GetTotalSupplyDepotsOwned() * 50;
            case BuildingType.FrontlineSniperTower: return 100;
            default: return 30;
        }
    }

    /// <summary>
    /// Calculates the cumulative Gold cost when placing multiple buildings at once (e.g. via drag placement).
    /// Handles SupplyDepot's +50G per-placement escalation accurately.
    /// </summary>
    public static int GetTotalGoldCostForCount(BuildingType t, int count)
    {
        if (count <= 0) return 0;
        if (t == BuildingType.SupplyDepot)
        {
            int baseOwned = GetTotalSupplyDepotsOwned();
            int total = 0;
            for (int i = 0; i < count; i++)
            {
                total += 100 + (baseOwned + i) * 50;
            }
            return total;
        }
        return count * GetGoldCost(t);
    }

    public static int GetMaterialCost(BuildingType t)
    {
        switch (t)
        {
            case BuildingType.SMGTent: return 10;
            case BuildingType.SniperTower: return 25;
            case BuildingType.Barricade: return 5;
            case BuildingType.HealStation: return 20;
            case BuildingType.SpecialForceDepot: return 30;
            case BuildingType.LightTankFactory: return 200;
            case BuildingType.MissileFoundry: return 350;
            case BuildingType.SupplyDepot: return 0; // Uses 0 Materials!
            case BuildingType.FrontlineSniperTower: return 40;
            default: return 10;
        }
    }

    /// <summary>
    /// Checks whether a building type is unlocked at the current stage.
    /// Special Force Depot unlocks after Stage 5 (Stage 6+, CurrentDay > 5).
    /// Light Tank Factory unlocks after Stage 10 (Stage 11+, CurrentDay > 10).
    /// Missile Foundry unlocks after Stage 15 (Stage 16+, CurrentDay > 15).
    /// </summary>
    public static bool IsBuildingUnlocked(BuildingType t)
    {
        // Dev Tools Unconditional Mode unlocks all current & future buildings immediately!
        if (GameManager.Instance != null && GameManager.Instance.isDevUnconditionalMode)
        {
            return true;
        }
        if (t == BuildingType.SpecialForceDepot)
        {
            return GameManager.Instance != null && GameManager.Instance.CurrentDay > 5;
        }
        if (t == BuildingType.LightTankFactory)
        {
            return GameManager.Instance != null && GameManager.Instance.CurrentDay > 10;
        }
        if (t == BuildingType.MissileFoundry)
        {
            return GameManager.Instance != null && GameManager.Instance.CurrentDay > 15;
        }
        return true;
    }

    /// <summary>
    /// Returns the grid cell footprint (sizeX = length along X, sizeY = width along Z/Y).
    /// SniperTower is 2 wide x 1 long (sizeX = 1, sizeY = 2).
    /// LightTankFactory is 2x2 cells (sizeX = 2, sizeY = 2).
    /// MissileFoundry is 3 wide x 4 long (sizeX = 4, sizeY = 3).
    /// </summary>
    public static Vector2Int GetFootprintSize(BuildingType t)
    {
        if (t == BuildingType.SniperTower) return new Vector2Int(1, 2);
        if (t == BuildingType.LightTankFactory) return new Vector2Int(2, 2);
        if (t == BuildingType.MissileFoundry) return new Vector2Int(4, 3);
        return new Vector2Int(1, 1);
    }

    /// <summary>
    /// Returns true if this building is a frontline fortification / turret structure (such as Barricade and future turrets)
    /// that does not spawn mobile soldiers and can be placed in the extended Frontline Zone (x: 15..24 up to the crossroads)
    /// as well as the main base.
    /// </summary>
    public static bool IsFrontlineFortification(BuildingType t)
    {
        return t == BuildingType.Barricade || t == BuildingType.FrontlineSniperTower;
    }

    // =========================================================================
    // BASE UPGRADE SYSTEM
    // =========================================================================

    public static int GetUpgradeLevel(BuildingType t)
    {
        return upgradeLevels.TryGetValue(t, out int lv) ? lv : 0;
    }

    public static int GetMaxUpgradeLevel(BuildingType t)
    {
        if (t == BuildingType.SMGTent || t == BuildingType.SpecialForceDepot || t == BuildingType.SniperTower) return 3;
        return 0;
    }

    public static float GetDamageMultiplier(BuildingType t)
    {
        int lv = GetUpgradeLevel(t);
        if (t == BuildingType.SMGTent)
        {
            // Lv.1: +5%, Lv.2: +5% more (+10% total), Lv.3: +10% more (+20% total)
            if (lv == 1) return 1.05f;
            if (lv == 2) return 1.10f;
            if (lv >= 3) return 1.20f;
        }
        else if (t == BuildingType.SniperTower)
        {
            // Lv.1: +15%, Lv.2: +20% more (+35% total), Lv.3: +125% more (+160% total)
            if (lv == 1) return 1.15f;
            if (lv == 2) return 1.35f;
            if (lv >= 3) return 2.60f;
        }
        return 1.0f;
    }

    public static float GetHealthMultiplier(BuildingType t)
    {
        int lv = GetUpgradeLevel(t);
        if (t == BuildingType.SpecialForceDepot)
        {
            // Lv.1: +10%, Lv.2: +15% more (+25% total), Lv.3: +15% more (+40% total)
            if (lv == 1) return 1.10f;
            if (lv == 2) return 1.25f;
            if (lv >= 3) return 1.40f;
        }
        return 1.0f;
    }

    public static void GetUpgradeCost(BuildingType t, int nextLevel, out int goldCost, out int matCost)
    {
        goldCost = 0;
        matCost = 0;
        if (t == BuildingType.SMGTent)
        {
            switch (nextLevel)
            {
                case 1:
                    goldCost = 45;
                    matCost = 15;
                    break;
                case 2:
                    goldCost = 80;
                    matCost = 30;
                    break;
                case 3:
                    goldCost = 130;
                    matCost = 50;
                    break;
            }
        }
        else if (t == BuildingType.SpecialForceDepot)
        {
            switch (nextLevel)
            {
                case 1:
                    goldCost = 65;
                    matCost = 25;
                    break;
                case 2:
                    goldCost = 110;
                    matCost = 45;
                    break;
                case 3:
                    goldCost = 170;
                    matCost = 75;
                    break;
            }
        }
        else if (t == BuildingType.SniperTower)
        {
            switch (nextLevel)
            {
                case 1:
                    goldCost = 85;
                    matCost = 35;
                    break;
                case 2:
                    goldCost = 140;
                    matCost = 60;
                    break;
                case 3:
                    goldCost = 220;
                    matCost = 95;
                    break;
            }
        }
    }

    public static string GetNextUpgradeDescription(BuildingType t)
    {
        int lv = GetUpgradeLevel(t);
        int maxLv = GetMaxUpgradeLevel(t);
        if (maxLv <= 0) return "No upgrades available for this structure.";
        if (lv >= maxLv)
        {
            if (t == BuildingType.SpecialForceDepot)
            {
                return "★ MAX UPGRADE (Lv.3): +40% HP (245 HP) + 30% Shield Push + Survives 2-Hits from Boss";
            }
            if (t == BuildingType.SniperTower)
            {
                return "★ MAX UPGRADE (Lv.3): +160% DMG (97.5 dmg) | Firerate -20% | Pierces 2 enemies with NO dropoff!";
            }
            return "★ MAX UPGRADE (Lv.3): +20% DMG + 7% Reload Grenade (3x3 AOE, 2x DPS)";
        }

        int nextLv = lv + 1;
        if (t == BuildingType.SMGTent)
        {
            if (nextLv == 1) return "Lv.1 Bonus: Overall Damage +5% (10.5 dmg / 35.0 DPS)";
            if (nextLv == 2) return "Lv.2 Bonus: Overall Damage +5% [Total +10%] (11.0 dmg / 36.7 DPS)";
            if (nextLv == 3) return "Lv.3 Bonus: Damage +10% [Total +20%] + 7% Reload Grenade (3x3 AOE)";
        }
        else if (t == BuildingType.SniperTower)
        {
            if (nextLv == 1) return "Lv.1 Bonus: Sniper Damage +15% (43.1 dmg / 43.1 DPS)";
            if (nextLv == 2) return "Lv.2 Bonus: Sniper Damage +20% [Total +35%] (50.6 dmg / 50.6 DPS)";
            if (nextLv == 3) return "Lv.3 Bonus: Damage +125% [Total +160%] (97.5 dmg), Firerate -20%, Pierces 2 enemies!";
        }
        else if (t == BuildingType.SpecialForceDepot)
        {
            if (nextLv == 1) return "Lv.1 Bonus: Guardian Health +10% (192.5 HP)";
            if (nextLv == 2) return "Lv.2 Bonus: Guardian Health +15% [Total +25%] (218.8 HP)";
            if (nextLv == 3) return "Lv.3 Bonus: Health +15% [Total +40%] (245 HP) + 30% Shield Push + Survives 2-Hits";
        }
        return "";
    }

    public static bool CanAffordUpgrade(BuildingType t)
    {
        int lv = GetUpgradeLevel(t);
        int maxLv = GetMaxUpgradeLevel(t);
        if (lv >= maxLv || !IsBuildingUnlocked(t)) return false;

        if (GameManager.Instance != null && GameManager.Instance.isDevUnconditionalMode)
        {
            return true;
        }

        GetUpgradeCost(t, lv + 1, out int gCost, out int mCost);
        if (ResourceManager.Instance == null) return false;
        return ResourceManager.Instance.Gold >= gCost && ResourceManager.Instance.Materials >= mCost;
    }

    public static bool TryUpgradeBuilding(BuildingType t)
    {
        int lv = GetUpgradeLevel(t);
        int maxLv = GetMaxUpgradeLevel(t);
        if (lv >= maxLv || !IsBuildingUnlocked(t)) return false;

        int nextLv = lv + 1;
        GetUpgradeCost(t, nextLv, out int gCost, out int mCost);

        bool isFreeDev = (GameManager.Instance != null && GameManager.Instance.isDevUnconditionalMode);
        if (!isFreeDev)
        {
            if (ResourceManager.Instance == null) return false;
            if (ResourceManager.Instance.Gold < gCost || ResourceManager.Instance.Materials < mCost)
            {
                AudioHelper.PlaySound(SoundEffect.Hit, 0.5f);
                return false;
            }
            ResourceManager.Instance.SpendGold(gCost);
            ResourceManager.Instance.SpendMaterials(mCost);
        }

        upgradeLevels[t] = nextLv;
        Soldier.ApplyUpgradeToActiveUnits(t);
        AudioHelper.PlaySound(SoundEffect.ResourceGain, 0.9f);
        Debug.Log($"[BuildingUpgrade] {t} upgraded to Lv.{nextLv}! (Damage Multiplier: {GetDamageMultiplier(t) * 100f:0}%)");
        return true;
    }

    public static void ResetAllUpgrades()
    {
        upgradeLevels.Clear();
    }
}
