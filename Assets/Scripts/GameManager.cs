using UnityEngine;
using System;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { Planning, Combat, Reward, RouteSelection, EventDilemma, Victory, GameOver }
    public GameState CurrentState { get; private set; } = GameState.Planning;

    public int CurrentDay { get; private set; } = 1;
    public int maxStages = 30; // 30-Stage Campaign with Cross Map unlock after Stage 15
    public bool isEndlessMode = false;
    public bool isEliteRouteAvailable { get; private set; } = false; // 35% chance to appear after Stage 5 (CurrentDay > 5)
    public bool isEliteBattleRoute { get; private set; } = false;    // Active when player picks Elite Battle Route (2x rewards!)
    public bool isEventRouteAvailable { get; private set; } = false; // 20% chance to appear after clearing a stage

    [Header("Dev Tools (Passcode: 2348)")]
    public const string DEV_PASSCODE = "2348";
    public bool isDevModeUnlocked { get; private set; } = false;
    public bool isDevUnconditionalMode = false; // Place any building (current & future) for free, no stage/grid/spawn lock!

    public static readonly float[] SpeedOptions = new float[] { 1.0f, 1.5f, 2.0f, 3.0f };
    public float CurrentGameSpeed { get; private set; } = 1.0f;

    public event Action<GameState> OnStateChanged;
    public event Action<int> OnDayChanged;

    private readonly string[] stageNames = new string[]
    {
        "Wasteland Perimeter",
        "Abandoned Highway",
        "Ruined Fuel Depot",
        "Suburban Barricade",
        "Fallen Military Outpost",
        "Toxic Canal Sector",
        "Metro Transit Ruins",
        "Quarantine Checkpoint",
        "Scorched Overpass",
        "The East Redoubt",
        "Ironclad Convoy Remains",
        "Biohazard Ridge",
        "Ashfall Boulevard",
        "The Dead Intersection",
        "Crossroads Lockdown (Stage 15 Gatekeeper)",
        "Northern Chemical Siphon (North Branch Unlocked!)",
        "Southern Military Silo (South Branch Unlocked!)",
        "Tri-Vector Assault (Triple Flank Horde!)",
        "The Crucible Convergence",
        "Citadel Outer Perimeter",
        "Midnight Blackout (Nightfall Flank)",
        "Seismic Tremor (Tyrant Vanguard)",
        "Desolation Trench (Barricade Siege)",
        "Dreadnought Vector (Armored Tyrants)",
        "Crimson Horizon (The Blood Tide)",
        "Oblivion Pass (Heavy Armor Standoff)",
        "Apocalypse Dawn (Tri-Vector Swarm)",
        "Zero Hour (The Shattered Line)",
        "Bastion's Edge (Pre-Climax Defense)",
        "The Final Stand: Last Bastion Omega (Campaign Climax)"
    };

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (maxStages < 30) maxStages = 30;
    }

    void Start()
    {
        if (FindFirstObjectByType<FortressCore>() == null)
        {
            gameObject.AddComponent<FortressCore>();
        }
        if (FindFirstObjectByType<EventDilemmaSystem>() == null)
        {
            gameObject.AddComponent<EventDilemmaSystem>();
        }
        if (FindFirstObjectByType<CrossMapVisualizer>() == null)
        {
            gameObject.AddComponent<CrossMapVisualizer>();
        }

        ApplyPostApocalypseTheme();
        SetGameSpeed(1.0f);
        SetState(GameState.Planning);
    }

    public void SetGameSpeed(float speed)
    {
        CurrentGameSpeed = Mathf.Clamp(speed, 1.0f, 3.0f);
        Time.timeScale = CurrentGameSpeed;
        Debug.Log($"[GameManager] Game Speed set to {CurrentGameSpeed:0.#}x");
    }

    public void CycleGameSpeed()
    {
        int nextIndex = 0;
        for (int i = 0; i < SpeedOptions.Length; i++)
        {
            if (Mathf.Approximately(CurrentGameSpeed, SpeedOptions[i]))
            {
                nextIndex = (i + 1) % SpeedOptions.Length;
                break;
            }
        }
        SetGameSpeed(SpeedOptions[nextIndex]);
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Time.timeScale = 1.0f;
            Instance = null;
        }
    }

    public void ApplyPostApocalypseTheme()
    {
        // 1. Camera Background: Dark dusty ash / toxic smog
        if (Camera.main != null)
        {
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = new Color(0.13f, 0.11f, 0.09f, 1.0f); // Dark scorched wasteland sky/fog
        }

        // 2. Ground Plane: Arid scorched earth / dried cracked dirt
        GameObject ground = GameObject.Find("Ground");
        if (ground != null)
        {
            var rend = ground.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material.color = new Color(0.24f, 0.19f, 0.14f); // Deep wasteland dust/mud
            }
        }

        // 3. Base Zone: Weathered military bunker & perimeter
        GameObject baseZone = GameObject.Find("BaseZone");
        if (baseZone != null)
        {
            var rend = baseZone.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material.color = new Color(0.17f, 0.23f, 0.18f); // Tactical fortified army olive/gray
            }
        }

        // 4. Zombie Spawn Zone: Biohazard contaminated wasteland
        GameObject spawnZone = GameObject.Find("ZombieSpawnZone");
        if (spawnZone != null)
        {
            var rend = spawnZone.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material.color = new Color(0.35f, 0.11f, 0.10f); // Scorched crimson dead zone
            }
        }

        // 5. Directional Light: Warm dusty apocalyptic sun
        Light dirLight = FindFirstObjectByType<Light>();
        if (dirLight != null && dirLight.type == LightType.Directional)
        {
            dirLight.color = new Color(1.0f, 0.83f, 0.64f); // Warm dusty amber haze
            dirLight.intensity = 0.95f;
        }

        // 6. Atmospheric Fog
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.16f, 0.13f, 0.10f);
        RenderSettings.fogDensity = 0.012f;

        Debug.Log("[GameManager] Post-Apocalypse visual theme applied successfully.");
    }

    public string GetStageName()
    {
        if (CurrentDay <= stageNames.Length)
        {
            return $"STAGE {CurrentDay}/{maxStages}: {stageNames[CurrentDay - 1]}";
        }
        return $"STAGE {CurrentDay} (ENDLESS MODE)";
    }

    public void SetState(GameState newState)
    {
        CurrentState = newState;
        OnStateChanged?.Invoke(newState);
        Debug.Log($"[GameManager] State -> {newState}");
    }

    public void StartCombat()
    {
        SetState(GameState.Combat);
        WaveManager.Instance?.StartWave(CurrentDay);
    }

    public void OnCombatComplete()
    {
        // Check if finished final campaign stage
        if (CurrentDay >= maxStages && !isEndlessMode)
        {
            SetState(GameState.Victory);
            ResourceManager.Instance?.AddReward(CurrentDay);
            Debug.Log("[GameManager] Campaign Won! Reached Victory State!");
            return;
        }

        SetState(GameState.Reward);
        ResourceManager.Instance?.AddReward(CurrentDay);
    }

    public void ProceedToNextDay()
    {
        // 1. Reset everything on the battlefield & store previously placed buildings into inventory count
        ResetBattlefieldAndStoreBuildings();

        // 2. Advance Day / Stage
        CurrentDay++;
        isEliteBattleRoute = false;

        // After Stage 5 (CurrentDay > 5), 35% chance for Elite Battle Route to appear!
        isEliteRouteAvailable = (CurrentDay > 5) && (UnityEngine.Random.value < 0.35f);

        // Event Route: 20% chance to appear after clearing a stage
        isEventRouteAvailable = (UnityEngine.Random.value < 0.20f);
        OnDayChanged?.Invoke(CurrentDay);

        // 3. Enter Route Selection if alternative routes exist, otherwise proceed directly to Planning
        if (isEliteRouteAvailable || isEventRouteAvailable)
        {
            SetState(GameState.RouteSelection);
            Debug.Log($"[GameManager] Proceeded to {GetStageName()} | Route Selection Available (Elite: {isEliteRouteAvailable}, Event: {isEventRouteAvailable})");
        }
        else
        {
            SetState(GameState.Planning);
            Debug.Log($"[GameManager] Proceeded to {GetStageName()} | Standard Battle (Direct to Planning)");
        }
    }

    public void ContinueEndlessMode()
    {
        isEndlessMode = true;
        ProceedToNextDay();
    }

    public void OnFortressFell()
    {
        if (CurrentState == GameState.GameOver) return;

        SetState(GameState.GameOver);
        SetGameSpeed(1.0f); // Keep normal 1.0x timeScale so UI/buttons/input are always 100% responsive
        WaveManager.Instance?.StopAllCoroutines();
        PlacementManager.Instance?.CancelPlacement();
        AudioHelper.PlaySound(SoundEffect.GameOver, 1.0f);
        Debug.Log("[GameManager] Base Defeated -> Game Over!");
    }

    public void RestartCampaign()
    {
        Time.timeScale = 1.0f;
        SetGameSpeed(1.0f);

        // Clear static collections and active references
        Building.ActiveBuildings.Clear();
        Building.ResetAllUpgrades();
        Soldier.ActiveSoldiers.Clear();
        Soldier.ActiveTargetingICBM = null;

        // Perform instant, clean in-place campaign reset to Stage 1 Planning
        isEndlessMode = false;
        isEliteRouteAvailable = false;
        isEliteBattleRoute = false;
        isEventRouteAvailable = false;
        CurrentDay = 1;
        OnDayChanged?.Invoke(CurrentDay);
        ResourceManager.Instance?.ResetStartingResources();
        FortressCore.Instance?.ResetHealth();
        PlacementManager.Instance?.ClearStoredCounts();
        WaveManager.Instance?.ResetWaveState();
        ClearBattlefield(false);
        SetState(GameState.Planning);
        Debug.Log("[GameManager] Campaign restarted successfully to Day 1 Planning.");
    }

    public void ResetBattlefieldAndStoreBuildings()
    {
        ClearBattlefield(true);
    }

    public void ClearBattlefield(bool storeBuildingsToStock)
    {
        // 1. Save blueprint if advancing days
        if (storeBuildingsToStock && PlacementManager.Instance != null)
        {
            PlacementManager.Instance.SaveCurrentLayout();
        }

        // 2. Collect all buildings from grid
        if (GridManager.Instance != null)
        {
            var allBuildings = GridManager.Instance.GetAllBuildings();
            foreach (var b in allBuildings)
            {
                if (b != null)
                {
                    if (storeBuildingsToStock && PlacementManager.Instance != null)
                    {
                        PlacementManager.Instance.AddStoredCount(b.type, 1);
                    }
                    Building.ActiveBuildings.Remove(b);
                    Destroy(b.gameObject);
                }
            }
            Building.ActiveBuildings.Clear();
            GridManager.Instance.ClearAll();
        }

        // Clear all remaining soldiers
        Soldier[] soldiers = FindObjectsByType<Soldier>(FindObjectsSortMode.None);
        foreach (var s in soldiers)
        {
            if (s != null) Destroy(s.gameObject);
        }
        Soldier.ActiveSoldiers.Clear();

        // Clear all remaining zombies
        Zombie[] zombies = FindObjectsByType<Zombie>(FindObjectsSortMode.None);
        foreach (var z in zombies)
        {
            if (z != null) Destroy(z.gameObject);
        }

        // Clear all remaining bullets
        Bullet[] bullets = FindObjectsByType<Bullet>(FindObjectsSortMode.None);
        foreach (var bl in bullets)
        {
            if (bl != null) Destroy(bl.gameObject);
        }

        // Cancel placement preview
        if (PlacementManager.Instance != null)
        {
            PlacementManager.Instance.CancelPlacement();
        }

        Debug.Log($"[GameManager] Battlefield reset completed. (storedToStock: {storeBuildingsToStock})");
    }

    public void SelectRoute(bool isBattle)
    {
        isEliteBattleRoute = false;
        if (isBattle)
        {
            SetState(GameState.Planning);
        }
        else
        {
            // Event Route: trigger wasteland dilemma scenario
            if (EventDilemmaSystem.Instance != null)
            {
                EventDilemmaSystem.Instance.TriggerRandomEvent();
                SetState(GameState.EventDilemma);
            }
            else
            {
                ResourceManager.Instance?.AddSpecialReward();
                SetState(GameState.Planning);
            }
        }
    }

    public void SelectEliteBattleRoute()
    {
        isEliteBattleRoute = true;
        AudioHelper.PlaySound(SoundEffect.EventAlert, 0.85f);
        SetState(GameState.Planning);
        Debug.Log("[GameManager] Selected ELITE BATTLE ROUTE! (Elite Zombies enabled, 2x Stage Rewards)");
    }

    public bool TryUnlockDevTools(string passcode)
    {
        if (passcode != null && passcode.Trim() == DEV_PASSCODE)
        {
            isDevModeUnlocked = true;
            isDevUnconditionalMode = true;
            AudioHelper.PlaySound(SoundEffect.ResourceGain, 0.9f);
            Debug.Log("[DevTools] Passcode 2348 verified! Unconditional Build & Dev Tools unlocked.");
            return true;
        }
        AudioHelper.PlaySound(SoundEffect.Hit, 0.6f);
        return false;
    }

    public void DevSetStage(int stage)
    {
        CurrentDay = Mathf.Max(1, stage);
        isEliteRouteAvailable = (CurrentDay > 5) && (UnityEngine.Random.value < 0.35f);
        isEventRouteAvailable = (UnityEngine.Random.value < 0.20f);
        OnDayChanged?.Invoke(CurrentDay);
        Debug.Log($"[DevTools] Jumped to Stage {CurrentDay}");
    }

    public void DevToggleEliteBattleRoute()
    {
        isEliteRouteAvailable = true;
        isEliteBattleRoute = !isEliteBattleRoute;
        Debug.Log($"[DevTools] Elite Battle Route set to: {isEliteBattleRoute}");
    }

    public void DevToggleEventRoute()
    {
        isEventRouteAvailable = !isEventRouteAvailable;
        Debug.Log($"[DevTools] Event Route set to: {isEventRouteAvailable}");
    }
}
