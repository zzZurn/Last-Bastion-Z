using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("Spawning Settings")]
    public GameObject zombiePrefab;
    public float spawnZMin = -10f;
    public float spawnZMax = 10f;
    public float spawnX = 22f;

    [Header("Wave Status")]
    public int currentWave = 1;
    public int totalZombiesInWave = 0;
    public int zombiesSpawned = 0;
    public int zombiesRemaining = 0;
    public bool isSpawning = false;

    private List<Zombie> activeZombies = new List<Zombie>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void StartWave(int day)
    {
        currentWave = day;

        // Buff: Mid-game stages (4-7) receive additional horde size!
        int midBonus = (day >= 4) ? (day - 3) * 3 : 0;
        totalZombiesInWave = 5 + (day * 4) + midBonus;

        zombiesSpawned = 0;
        zombiesRemaining = totalZombiesInWave;
        activeZombies.Clear();

        StartCoroutine(SpawnWaveRoutine(day));
    }

    private IEnumerator SpawnWaveRoutine(int day)
    {
        isSpawning = true;
        Debug.Log($"[WaveManager] Stage/Wave {day} started! Total zombies: {totalZombiesInWave}");

        while (zombiesSpawned < totalZombiesInWave)
        {
            SpawnZombie(day);
            zombiesSpawned++;

            // Mid-Game Buff: In Stages 4+, chance to spawn paired cluster rush!
            if (day >= 4 && Random.value < 0.35f && zombiesSpawned < totalZombiesInWave)
            {
                yield return new WaitForSeconds(0.18f);
                SpawnZombie(day);
                zombiesSpawned++;
            }

            // Accelerating spawn rate as the horde numbers increase
            float progress = (float)zombiesSpawned / Mathf.Max(1, totalZombiesInWave);
            float startDelay = Mathf.Max(1.0f, 2.4f - (day * 0.12f));
            float endDelay = Mathf.Max(0.28f, 0.75f - (day * 0.05f));
            float delay = Mathf.Lerp(startDelay, endDelay, progress);

            yield return new WaitForSeconds(delay);
        }

        isSpawning = false;
        Debug.Log("[WaveManager] All zombies for this wave have been spawned.");
    }

    private void SpawnZombie(int day)
    {
        float randomZ = Random.Range(spawnZMin, spawnZMax);
        Vector3 spawnPos = new Vector3(spawnX, 0.5f, randomZ);

        GameObject zObj;
        if (zombiePrefab != null)
        {
            zObj = Instantiate(zombiePrefab, spawnPos, Quaternion.identity);
        }
        else
        {
            // Procedural fallback primitive
            zObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            zObj.name = $"Zombie_{zombiesSpawned + 1}";
            zObj.transform.position = spawnPos;
            zObj.transform.localScale = new Vector3(0.8f, 1.2f, 0.8f);

            var renderer = zObj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0.2f, 0.8f, 0.2f); // default green
            }
            zObj.AddComponent<Zombie>();
        }

        Zombie z = zObj.GetComponent<Zombie>();
        if (z != null)
        {
            // Base zombie scaling with MID-GAME MULTIPLIER (Stages 4-7)
            float midGameHpBuff = (day >= 4) ? 1.25f : 1.0f;
            if (day >= 6) midGameHpBuff = 1.45f;

            float baseHp = (50f + ((day - 1) * 8f)) * midGameHpBuff;
            float baseDmg = (8f + (day * 1.5f)) * ((day >= 4) ? 1.3f : 1.0f);
            float baseSpeed = Mathf.Min(3.3f, 1.6f + (day * 0.1f));

            z.maxHealth = baseHp;
            z.currentHealth = baseHp;
            z.damage = baseDmg;
            z.moveSpeed = baseSpeed;

            // --- MID-GAME VARIANTS & BOSSES ---

            // 1. Stage 5 Mid-Game Boss: Military Tyrant Goliath (spawns once near middle of stage 5 wave)
            if (day == 5 && zombiesSpawned == Mathf.FloorToInt(totalZombiesInWave * 0.5f))
            {
                z.name = "BOSS_TyrantGoliath";
                z.maxHealth = 280f;
                z.currentHealth = 280f;
                z.damage = 25f;
                z.moveSpeed = 1.2f;
                zObj.transform.localScale = new Vector3(1.7f, 2.4f, 1.7f);
                var rend = zObj.GetComponent<Renderer>();
                if (rend != null) rend.material.color = new Color(0.55f, 0.12f, 0.55f); // Deep Toxic Purple Boss
            }
            // 2. Mid-Game Armored Riot Zombie (Stage 4+, 22% chance)
            else if (day >= 4 && Random.value < 0.22f)
            {
                z.name = "ArmoredZombie";
                z.maxHealth = baseHp * 1.8f; // Heavy armor plating
                z.currentHealth = z.maxHealth;
                z.moveSpeed = 1.4f;
                z.damage = baseDmg * 1.2f;
                zObj.transform.localScale = new Vector3(1.1f, 1.4f, 1.1f);
                var rend = zObj.GetComponent<Renderer>();
                if (rend != null) rend.material.color = new Color(0.35f, 0.45f, 0.55f); // Steel Riot Armor
            }
            // 3. Mid-Game Berserker Mutant (Stage 4+, 25% chance)
            else if (day >= 4 && Random.value < 0.25f)
            {
                z.name = "BerserkerZombie";
                z.maxHealth = baseHp * 1.2f;
                z.currentHealth = z.maxHealth;
                z.moveSpeed = 2.6f; // Rapid sprint!
                z.damage = baseDmg * 1.4f;
                zObj.transform.localScale = new Vector3(0.9f, 1.3f, 0.9f);
                var rend = zObj.GetComponent<Renderer>();
                if (rend != null) rend.material.color = new Color(0.85f, 0.15f, 0.25f); // Crimson Berserker
            }
            // 4. Runner Zombie (from Stage 2+, 25% chance)
            else if (day >= 2 && Random.value < 0.25f)
            {
                z.name = "RunnerZombie";
                z.moveSpeed = 2.7f;
                z.maxHealth = baseHp * 0.75f;
                z.currentHealth = z.maxHealth;
                zObj.transform.localScale = new Vector3(0.7f, 1.0f, 0.7f);
                var rend = zObj.GetComponent<Renderer>();
                if (rend != null) rend.material.color = new Color(0.9f, 0.45f, 0.1f); // Orange Runner
            }
            // 5. Heavy Tank Zombie (from Stage 5+, 20% chance)
            else if (day >= 5 && Random.value < 0.2f)
            {
                z.name = "TankZombie";
                z.moveSpeed = 1.1f;
                z.maxHealth = baseHp * 2.2f;
                z.currentHealth = z.maxHealth;
                zObj.transform.localScale = new Vector3(1.3f, 1.7f, 1.3f);
                var rend = zObj.GetComponent<Renderer>();
                if (rend != null) rend.material.color = new Color(0.12f, 0.38f, 0.12f); // Dark Tank Green
            }

            activeZombies.Add(z);
        }
    }

    public void OnZombieDied(Zombie z)
    {
        if (activeZombies.Contains(z))
        {
            activeZombies.Remove(z);
        }

        zombiesRemaining--;

        if (!isSpawning && zombiesRemaining <= 0)
        {
            OnWaveCleared();
        }
    }

    private void OnWaveCleared()
    {
        Debug.Log($"[WaveManager] Wave {currentWave} cleared successfully!");
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnCombatComplete();
        }
    }
}
