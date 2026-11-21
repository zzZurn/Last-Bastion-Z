using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("Spawning Settings")]
    public GameObject zombiePrefab;
    [Header("Cross Map Spawn Settings")]
    public float spawnZMin = -14.0f;
    public float spawnZMax = 14.0f;
    public float spawnX = 43.5f;

    [Header("Wave Status")]
    public int currentWave = 1;
    public int totalZombiesInWave = 0;
    public int zombiesSpawned = 0;
    public int zombiesRemaining = 0;
    public bool isSpawning = false;
    private int spittersSpawnedInWave = 0;

    // Track randomly selected spawn slots for Tyrant Bosses in current wave
    // Tyrant Boss spawns strictly after Day 4 onwards (Day 5+), +1 boss every 5 waves
    private HashSet<int> bossSpawnIndices = new HashSet<int>();
    public int TotalBossesInCurrentWave => (currentWave > 4) ? (1 + (currentWave - 5) / 5) : 0;

    private List<Zombie> activeZombies = new List<Zombie>();
    public List<Zombie> ActiveZombies => activeZombies;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Force correct East Head spawn settings on 15x15-scaled Cross map
        spawnX = 43.5f;
        spawnZMin = -14.0f;
        spawnZMax = 14.0f;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void ResetWaveState()
    {
        StopAllCoroutines();
        isSpawning = false;
        totalZombiesInWave = 0;
        zombiesSpawned = 0;
        zombiesRemaining = 0;
        spittersSpawnedInWave = 0;
        bossSpawnIndices.Clear();
        activeZombies.Clear();
    }

    public void StartWave(int day)
    {
        currentWave = day;

        // Buff: Stages after Stage 3 (day > 3) receive additional horde size!
        int midBonus = (day > 3) ? ((day - 3) * 4 + 2) : 0;
        totalZombiesInWave = 5 + (day * 4) + midBonus;

        // Wave 26+ (after Wave 25): Zombie count increased by 20%!
        if (day > 25)
        {
            totalZombiesInWave = Mathf.RoundToInt(totalZombiesInWave * 1.20f);
        }

        // Stages after Stage 30 (Stage 31+): Apocalypse City buff increases zombie spawns by +35%!
        if (day > 30)
        {
            totalZombiesInWave = Mathf.RoundToInt(totalZombiesInWave * 1.35f);
        }

        zombiesSpawned = 0;
        zombiesRemaining = totalZombiesInWave;
        spittersSpawnedInWave = 0;
        activeZombies.Clear();

        // Tyrant Boss Configuration:
        // Tyrant Boss only spawns after Day 4 onwards (Day 5+), +1 additional boss every 5 waves:
        // Days 1-4: 0 bosses
        // Days 5-9: 1 boss
        // Days 10-14: 2 bosses
        // Days 15-19: 3 bosses
        // Days 20-24: 4 bosses
        // Days 25-29: 5 bosses
        // Day 30: 6 bosses (Final Climax Wave)
        bossSpawnIndices.Clear();
        int targetBosses = (day > 4) ? (1 + (day - 5) / 5) : 0;

        if (targetBosses > 0)
        {
            List<int> availableSlots = new List<int>();
            int minSlot = Mathf.Min(Mathf.Max(2, Mathf.FloorToInt(totalZombiesInWave * 0.22f)), totalZombiesInWave - 1);
            int maxSlot = totalZombiesInWave - 1;
            for (int i = minSlot; i <= maxSlot; i++)
            {
                availableSlots.Add(i);
            }

            for (int b = 0; b < targetBosses && availableSlots.Count > 0; b++)
            {
                int rIdx = Random.Range(0, availableSlots.Count);
                bossSpawnIndices.Add(availableSlots[rIdx]);
                availableSlots.RemoveAt(rIdx);
            }
        }

        AudioHelper.PlaySound(SoundEffect.WaveStart, 0.8f);
        StartCoroutine(SpawnWaveRoutine(day));
    }

    private IEnumerator SpawnWaveRoutine(int day)
    {
        isSpawning = true;
        // Stages after Stage 30 (Stage 31+): Zombies spawn 20% faster (interval scaled by 1 / 1.20)
        float spawnIntervalScale = (day > 30) ? (1f / 1.20f) : 1.0f;
        Debug.Log($"[WaveManager] Stage/Wave {day} started! Total zombies: {totalZombiesInWave}{(day > 30 ? " (+35% Horde, +20% Spawn Speed)" : "")}");

        while (zombiesSpawned < totalZombiesInWave)
        {
            SpawnZombie(day);
            zombiesSpawned++;

            // Dynamic cluster rush: scales with wave intensity
            float clusterChance = (day >= 20) ? 0.48f : (day >= 10 ? 0.40f : 0.35f);
            if (day >= 4 && Random.value < clusterChance && zombiesSpawned < totalZombiesInWave)
            {
                yield return new WaitForSeconds(0.18f * spawnIntervalScale);
                SpawnZombie(day);
                zombiesSpawned++;

                // End-game multi-cluster rush for stages 22+
                if (day >= 22 && Random.value < 0.30f && zombiesSpawned < totalZombiesInWave)
                {
                    yield return new WaitForSeconds(0.15f * spawnIntervalScale);
                    SpawnZombie(day);
                    zombiesSpawned++;
                }
            }

            // Accelerating spawn rate as the horde numbers increase
            float progress = (float)zombiesSpawned / Mathf.Max(1, totalZombiesInWave);
            float startDelay = Mathf.Max(0.85f, 2.4f - (day * 0.10f));
            float endDelay = Mathf.Max(0.22f, 0.75f - (day * 0.04f));
            float delay = Mathf.Lerp(startDelay, endDelay, progress) * spawnIntervalScale;

            yield return new WaitForSeconds(delay);
        }

        isSpawning = false;
        Debug.Log("[WaveManager] All zombies for this wave have been spawned.");
    }

    private void SpawnZombie(int day)
    {
        Vector3 spawnPos;
        Quaternion spawnRot = Quaternion.LookRotation(Vector3.left);

        // Multi-directional cross spawns after Stage 15
        if (day > 15)
        {
            float roll = Random.value;
            if (roll < 0.22f)
            {
                // North Arm Vector (+Z)
                spawnPos = new Vector3(Random.Range(-14.0f, 14.0f), 0.5f, 43.5f);
                spawnRot = Quaternion.LookRotation(Vector3.back);
            }
            else if (roll < 0.44f)
            {
                // South Arm Vector (-Z)
                spawnPos = new Vector3(Random.Range(-14.0f, 14.0f), 0.5f, -43.5f);
                spawnRot = Quaternion.LookRotation(Vector3.forward);
            }
            else
            {
                // East Head Vector (+X)
                float randomZ = Random.Range(spawnZMin, spawnZMax);
                spawnPos = new Vector3(spawnX, 0.5f, randomZ);
                spawnRot = Quaternion.LookRotation(Vector3.left);
            }
        }
        else
        {
            // Main East Head Spawn
            float randomZ = Random.Range(spawnZMin, spawnZMax);
            spawnPos = new Vector3(spawnX, 0.5f, randomZ);
            spawnRot = Quaternion.LookRotation(Vector3.left);
        }

        string variant = "Walker";
        // Tyrant Boss: Spawns after Day 4 (Day 5+), scales +1 every 5 waves
        if (bossSpawnIndices.Contains(zombiesSpawned))
        {
            variant = "Boss";
        }
        else
        {
            variant = PickBalancedZombieVariant(day);
        }

        // Elite Battle Route: Randomly upgrade spawned non-boss zombies into Elite units (Base 18%, Climax Wave 30 & Wave 40+ is 2x = 36%)
        bool isClimaxWave = (day == 30 || day >= 40);
        bool isEliteRoute = (GameManager.Instance != null && GameManager.Instance.isEliteBattleRoute) || isClimaxWave;
        float eliteChance = isClimaxWave ? 0.36f : 0.18f;
        bool upgradeToElite = (isEliteRoute && variant != "Boss" && Random.value < eliteChance);

        GameObject zObj;
        if (zombiePrefab != null)
        {
            zObj = Instantiate(zombiePrefab, spawnPos, spawnRot);
        }
        else
        {
            zObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            zObj.name = upgradeToElite ? $"Elite_{variant}_{zombiesSpawned + 1}" : $"Zombie_{variant}_{zombiesSpawned + 1}";
            zObj.transform.position = spawnPos;
            zObj.transform.rotation = spawnRot;
            zObj.transform.localScale = Vector3.one;

            UnitBlockVisualizer.AttachZombieVisuals(zObj, variant, upgradeToElite);
            zObj.AddComponent<Zombie>();
        }

        Zombie z = zObj.GetComponent<Zombie>();
        if (z != null)
        {
            // Base zombie scaling with slight buff after Stage 3 (day > 3)
            float midGameHpBuff = (day > 3) ? 1.38f : 1.0f;
            if (day >= 6) midGameHpBuff = 1.60f;

            float baseHp = (50f + ((day - 1) * 9f)) * midGameHpBuff;
            float baseDmg = (8f + (day * 1.6f)) * ((day > 3) ? 1.42f : 1.0f);
            float baseSpeed = Mathf.Min(3.5f, 1.6f + (day * 0.11f) + ((day > 3) ? 0.12f : 0f));

            z.maxHealth = baseHp;
            z.currentHealth = baseHp;
            z.damage = baseDmg;
            z.moveSpeed = baseSpeed;
            z.isBossUnit = (variant == "Boss");
            z.isBuffedZombie = (variant == "Buffed");
            z.isBombaz = (variant == "Bombaz");
            z.isBombazEnraged = false;
            z.bombazBaseNormalSpeed = baseSpeed;
            z.isSpitter = (variant == "Spitter");
            z.isHowler = (variant == "Howler");
            z.isLeaper = (variant == "Leaper");
            z.isBroodmother = (variant == "Broodmother");
            z.isCryoHazmat = (variant == "CryoHazmat");
            z.isEliteUpgrade = false;

            if (variant == "Boss")
            {
                z.name = "Tyrant Boss";
                // Buffed Tyrant Boss stats: Higher HP + 50% damage resistance to bullets + Ground Slam
                float bossHp = 750f + (day * 65f);
                z.maxHealth = bossHp;
                z.currentHealth = bossHp;
                z.damage = 55f; // Buffed to 55f: naturally one-shots SMG troopers (50 HP)
                z.attackRate = 1.8f;
                z.moveSpeed = 1.15f;
                z.damageResistance = 0.5f; // 50% bullet resistance
                AudioHelper.PlaySound(SoundEffect.BuildingDemolish, 0.75f);
                Debug.Log($"[WaveManager] 💀 TYRANT BOSS SPAWNED at stage {day}! HP: {bossHp}, 50% Bullet Resistance.");
            }
            else if (variant == "Buffed")
            {
                z.name = $"BuffedZombie_{zombiesSpawned + 1}";
                // Buffed Zombie preserves the exact former Tyrant Boss stats
                float buffedHp = 360f + Mathf.Max(0, day - 5) * 40f;
                z.maxHealth = buffedHp;
                z.currentHealth = buffedHp;
                z.damage = 32f;
                z.attackRate = 1.2f;
                z.moveSpeed = 1.25f;
            }
            else if (variant == "Bombaz")
            {
                z.name = $"BombazZombie_{zombiesSpawned + 1}";
                // Bombaz: +50% HP over normal zombie, 25% slower moveSpeed (75% of normal speed).
                // When HP < 40%, speed surges to 150% of normal speed and explodes in melee (4x4 circular AOE).
                z.maxHealth = baseHp * 1.5f;
                z.currentHealth = z.maxHealth;
                z.bombazBaseNormalSpeed = baseSpeed;
                z.moveSpeed = baseSpeed * 0.75f;
                z.damage = baseDmg * 2.2f;
            }
            else if (variant == "Spitter")
            {
                spittersSpawnedInWave++;
                z.name = $"AcidSpitter_{zombiesSpawned + 1}";
                z.maxHealth = baseHp * 0.68f;
                z.currentHealth = z.maxHealth;
                z.moveSpeed = baseSpeed * 0.92f;
                z.damage = baseDmg * 0.42f;
                z.attackRange = 6.8f; // Inside SMG 8.0m range so frontline infantry can always shoot back!
                z.attackRate = 2.6f;
            }
            else if (variant == "Howler")
            {
                z.name = $"HowlerZombie_{zombiesSpawned + 1}";
                z.maxHealth = baseHp * 1.45f;
                z.currentHealth = z.maxHealth;
                z.moveSpeed = baseSpeed * 0.88f;
                z.damage = baseDmg * 1.0f;
                z.attackRate = 1.2f;
            }
            else if (variant == "Leaper")
            {
                z.name = $"LeaperZombie_{zombiesSpawned + 1}";
                z.maxHealth = baseHp * 0.90f;
                z.currentHealth = z.maxHealth;
                z.moveSpeed = Mathf.Max(2.4f, baseSpeed * 1.25f);
                z.damage = baseDmg * 1.25f;
                z.attackRate = 0.9f;
            }
            else if (variant == "Broodmother")
            {
                z.name = $"BroodmotherZombie_{zombiesSpawned + 1}";
                z.maxHealth = baseHp * 2.15f;
                z.currentHealth = z.maxHealth;
                z.moveSpeed = 1.05f;
                z.damage = baseDmg * 1.3f;
                z.attackRate = 1.4f;
                z.attackRange = 1.45f;
            }
            else if (variant == "CryoHazmat")
            {
                z.name = $"CryoHazmatZombie_{zombiesSpawned + 1}";
                z.maxHealth = baseHp * 1.60f;
                z.currentHealth = z.maxHealth;
                z.moveSpeed = baseSpeed * 0.92f;
                z.damage = baseDmg * 1.1f;
                z.damageResistance = 0.20f; // 20% armored hazmat suit resistance
            }
            else if (variant == "Armored")
            {
                z.name = "ArmoredZombie";
                z.maxHealth = baseHp * 1.8f;
                z.currentHealth = z.maxHealth;
                z.moveSpeed = 1.4f;
                z.damage = baseDmg * 1.2f;
            }
            else if (variant == "Berserker")
            {
                z.name = "BerserkerZombie";
                z.maxHealth = baseHp * 1.2f;
                z.currentHealth = z.maxHealth;
                z.moveSpeed = 2.6f;
                z.damage = baseDmg * 1.4f;
            }
            else if (variant == "Runner")
            {
                z.name = "RunnerZombie";
                z.moveSpeed = 2.7f;
                z.maxHealth = baseHp * 0.75f;
                z.currentHealth = z.maxHealth;
            }
            else if (variant == "Tank")
            {
                z.name = "TankZombie";
                z.moveSpeed = 1.1f;
                z.maxHealth = baseHp * 2.2f;
                z.currentHealth = z.maxHealth;
            }

            // Apply Elite Battle Route Upgrade (never applies to Boss)
            if (upgradeToElite && variant != "Boss")
            {
                z.isEliteUpgrade = true;
                z.eliteTitle = $"⚡ ELITE {variant.ToUpper()}";
                z.maxHealth *= 1.75f; // +75% tougher (อึดขึ้น 75%)
                z.currentHealth = z.maxHealth;
            }

            activeZombies.Add(z);
        }
    }

    /// <summary>
    /// Selects a non-boss zombie variant using a stage-balanced 2-tier weighted pool:
    /// - Core frontline zombies (Walker, Runner, Armored, Berserker, Tank) make up >= 55% of the horde.
    /// - Special skill zombies (Buffed, Spitter, Bombaz, Howler, Leaper, Broodmother, CryoHazmat) scale from 20% at Stage 6 up to 45% at Stage 22+.
    /// </summary>
    private string PickBalancedZombieVariant(int day)
    {
        if (day >= 6)
        {
            float specialChance = (day >= 22) ? 0.42f : ((day >= 18) ? 0.36f : ((day >= 16) ? 0.30f : ((day >= 11) ? 0.24f : 0.14f)));
            if (Random.value < specialChance)
            {
                int maxSpittersForStage = (day < 7) ? 0 : ((day <= 9) ? 1 : ((day <= 14) ? 2 : ((day <= 21) ? 3 : 4)));
                bool canSpawnSpitter = (day >= 7) && (spittersSpawnedInWave < maxSpittersForStage);

                int wBuffed = 14;
                int wSpitter = canSpawnSpitter ? 8 : 0;
                int wBombaz = (day > 10) ? 18 : 0;
                int wHowler = (day >= 12) ? 15 : 0;
                int wLeaper = (day >= 16) ? 18 : 0;
                int wBrood = (day >= 18) ? 13 : 0;
                int wCryo = (day >= 22) ? 15 : 0;

                int totalSpecialW = wBuffed + wSpitter + wBombaz + wHowler + wLeaper + wBrood + wCryo;
                int roll = Random.Range(0, totalSpecialW);
                if ((roll -= wSpitter) < 0) return "Spitter";
                if ((roll -= wBombaz) < 0) return "Bombaz";
                if ((roll -= wHowler) < 0) return "Howler";
                if ((roll -= wLeaper) < 0) return "Leaper";
                if ((roll -= wBrood) < 0) return "Broodmother";
                if ((roll -= wCryo) < 0) return "CryoHazmat";
                return "Buffed";
            }
        }

        int wWalker = 36;
        int wRunner = (day >= 2) ? 22 : 0;
        int wArmored = (day >= 4) ? 18 : 0;
        int wBerserker = (day >= 4) ? 18 : 0;
        int wTank = (day >= 5) ? 14 : 0;

        int totalCoreW = wWalker + wRunner + wArmored + wBerserker + wTank;
        int coreRoll = Random.Range(0, totalCoreW);
        if ((coreRoll -= wRunner) < 0) return "Runner";
        if ((coreRoll -= wArmored) < 0) return "Armored";
        if ((coreRoll -= wBerserker) < 0) return "Berserker";
        if ((coreRoll -= wTank) < 0) return "Tank";
        return "Walker";
    }

    public void RegisterExtraSpawnedZombie(Zombie extraZombie)
    {
        if (extraZombie == null) return;
        totalZombiesInWave++;
        zombiesSpawned++;
        zombiesRemaining++;
        if (!activeZombies.Contains(extraZombie))
        {
            activeZombies.Add(extraZombie);
        }
    }

    public void OnZombieDied(Zombie z)
    {
        if (activeZombies.Contains(z))
        {
            activeZombies.Remove(z);
        }

        zombiesRemaining--;

        // If Fortress already fell (Game Over), do NOT trigger wave completion or override GameOver!
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.GameState.GameOver)
        {
            return;
        }

        if (!isSpawning && zombiesRemaining <= 0)
        {
            OnWaveCleared();
        }
    }

    private void OnWaveCleared()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.Combat)
        {
            return;
        }

        Debug.Log($"[WaveManager] Wave {currentWave} cleared successfully!");
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnCombatComplete();
        }
    }
}
