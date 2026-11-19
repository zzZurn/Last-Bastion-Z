using UnityEngine;
using System;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { MainMenu, Planning, Combat, Reward, RouteSelection, EventDilemma, Victory, GameOver }
    public GameState CurrentState { get; private set; } = GameState.MainMenu;

    public int CurrentDay { get; private set; } = 1;
    public int maxStages = 40; // 40-Stage Campaign: Cross Map after Stage 15, Air Base & Apocalypse City after Stage 30
    public bool isEndlessMode = false;
    public bool IsApocalypseCityStage => CurrentDay > 30;
    public bool isEliteRouteAvailable { get; private set; } = false; // 35% chance to appear after Stage 5 (CurrentDay > 5)
    public bool isEliteBattleRoute { get; private set; } = false;    // Active when player picks Elite Battle Route (2x rewards!)
    public bool isEventRouteAvailable { get; private set; } = false; // 20% chance to appear after clearing a stage

    [Header("Dev Tools (Passcode: 2348)")]
    public const string DEV_PASSCODE = "2348";
    public bool isDevModeUnlocked { get; private set; } = false;
    public bool isDevUnconditionalMode = false; // Unlock all buildings/zones/limits & combat building
    public bool isDevFreeEverything = false;    // Make all purchases, upgrades, repairs & event costs 0

    public static bool IsDevUnconditional => Instance != null && Instance.isDevUnconditionalMode;
    public static bool IsDevFreeEverything => Instance != null && Instance.isDevFreeEverything;

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
        "Bastion Forest Climax (Stage 30 Gatekeeper — Unlocks Air Base & City!)",
        "Outskirts of the Dead Metropolis (Air Base & Apocalypse City Unlocked!)",
        "Shattered Skyscraper Canyon",
        "Burning Amber Expressway",
        "Collapsed Plaza Siege",
        "Cathedral of Rust & Ash",
        "Crimson Dusk Boulevard",
        "Ground Zero Ruined District",
        "Hellfire Spire Convergence",
        "Last Horizon: Citadel Ruins",
        "The Final Stand: Apocalypse Metropolis Omega (Stage 40 Campaign Climax)"
    };

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (maxStages < 40) maxStages = 40;
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
        if (FindFirstObjectByType<TitleLogo3D>() == null)
        {
            gameObject.AddComponent<TitleLogo3D>();
        }
        if (FindAnyObjectByType<AirSupportManager>() == null)
        {
            gameObject.AddComponent<AirSupportManager>();
        }

        ApplyPostApocalypseTheme();
        SetGameSpeed(1.0f);
        SetState(GameState.MainMenu);
    }

    public void StartGameFromMenu()
    {
        SetState(GameState.Planning);
        Debug.Log("[GameManager] Started game from Main Menu -> Stage 1 Planning.");
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
        bool isCity = IsApocalypseCityStage;

        // 1. Camera Background: Crimson-amber burning sunset for Stage 31+ Apocalypse City, moonlit night for Stage 1-30
        if (Camera.main != null)
        {
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = isCity
                ? new Color(0.36f, 0.13f, 0.05f, 1.0f)
                : new Color(0.06f, 0.08f, 0.10f, 1.0f);
        }

        // 2. Ground Plane fallback
        GameObject ground = GameObject.Find("Ground");
        if (ground != null)
        {
            var rend = ground.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material.color = new Color(0.24f, 0.19f, 0.14f);
            }
        }

        // 3. Directional Light: Fiery amber sunset sun in Stage 31+ City, soft moonlight in Stage 1-30 Forest
        Light dirLight = FindFirstObjectByType<Light>();
        if (dirLight != null && dirLight.type == LightType.Directional)
        {
            dirLight.color = isCity
                ? new Color(1.0f, 0.72f, 0.42f)
                : new Color(0.85f, 0.90f, 0.98f);
            dirLight.intensity = isCity ? 1.15f : 0.24f;
        }

        // 4. Atmospheric Fog
        RenderSettings.fog = true;
        RenderSettings.fogColor = isCity
            ? new Color(0.42f, 0.19f, 0.09f)
            : new Color(0.07f, 0.09f, 0.11f);
        RenderSettings.fogDensity = isCity ? 0.0038f : 0.0045f;

        Debug.Log($"[GameManager] Visual theme applied (ApocalypseCity={isCity}, Stage={CurrentDay}).");
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
        PlacementManager.Instance?.SaveWaveStartLayout();
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
        PlacementManager.Instance?.ClearWaveStartLayout();
        ResetBattlefieldAndStoreBuildings();

        // 2. Advance Day / Stage
        CurrentDay++;
        isEliteBattleRoute = false;
        ApplyPostApocalypseTheme();

        // Wave 30 (Chapter 1 Gatekeeper) & Wave 40 (Final Campaign Climax): Mandatory Elite Battle Route
        if (CurrentDay == 30 || CurrentDay == maxStages)
        {
            isEliteBattleRoute = true;
            isEliteRouteAvailable = true;
            isEventRouteAvailable = false;
            OnDayChanged?.Invoke(CurrentDay);
            SetState(GameState.RouteSelection);
            Debug.Log($"[GameManager] Proceeded to Wave {CurrentDay} | MANDATORY ELITE ASSAULT CLIMAX!");
            return;
        }

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
        ApplyPostApocalypseTheme();
        OnDayChanged?.Invoke(CurrentDay);
        ResourceManager.Instance?.ResetStartingResources();
        FortressCore.Instance?.ResetHealth();
        PlacementManager.Instance?.ClearStoredCounts();
        WaveManager.Instance?.ResetWaveState();
        AirSupportManager.Instance?.ResetCampaign();
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

        ClearActiveCombatEntities();

        // Cancel placement preview
        if (PlacementManager.Instance != null)
        {
            PlacementManager.Instance.CancelPlacement();
        }

        Debug.Log($"[GameManager] Battlefield reset completed. (storedToStock: {storeBuildingsToStock})");
    }

    private void ClearActiveCombatEntities()
    {
        Soldier.ActiveTargetingICBM = null;

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
    }

    public void SelectRoute(bool isBattle)
    {
        if (CurrentDay == 30 || CurrentDay == maxStages)
        {
            SelectEliteBattleRoute();
            return;
        }

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
            isDevFreeEverything = true;
            AudioHelper.PlaySound(SoundEffect.ResourceGain, 0.9f);
            Debug.Log("[DevTools] Passcode 2348 verified! Unconditional Build & Free Everything unlocked.");
            return true;
        }
        AudioHelper.PlaySound(SoundEffect.Hit, 0.6f);
        return false;
    }

    public void DevSetStage(int stage)
    {
        CurrentDay = Mathf.Max(1, stage);
        ApplyPostApocalypseTheme();
        if (CurrentDay == 30 || CurrentDay == maxStages)
        {
            isEliteBattleRoute = true;
            isEliteRouteAvailable = true;
            isEventRouteAvailable = false;
        }
        else
        {
            isEliteBattleRoute = false;
            isEliteRouteAvailable = (CurrentDay > 5) && (UnityEngine.Random.value < 0.35f);
            isEventRouteAvailable = (UnityEngine.Random.value < 0.20f);
        }
        OnDayChanged?.Invoke(CurrentDay);
        Debug.Log($"[DevTools] Jumped to Stage {CurrentDay} (ApocalypseCity={IsApocalypseCityStage})");
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

    /// <summary>
    /// Restarts the current Stage/Wave back to the Planning phase while preserving/restoring placed buildings.
    /// </summary>
    public void DevRestartCurrentWave()
    {
        Time.timeScale = CurrentGameSpeed;

        WaveManager.Instance?.ResetWaveState();
        AirSupportManager.Instance?.ResetForNewStage();
        ClearActiveCombatEntities();
        FortressCore.Instance?.ResetHealth();

        // Restore any buildings destroyed earlier in this wave, then heal & reset timers on all placed buildings
        if (CurrentState != GameState.Planning)
        {
            PlacementManager.Instance?.RestoreDestroyedFromWaveStartLayout();
        }

        if (GridManager.Instance != null)
        {
            foreach (var b in GridManager.Instance.GetAllBuildings())
            {
                if (b != null) b.ResetForWaveRestart();
            }
        }

        PlacementManager.Instance?.CancelPlacement();
        AudioHelper.PlaySound(SoundEffect.ButtonClick);
        SetState(GameState.Planning);
        Debug.Log($"[DevTools] Restarted Stage {CurrentDay} back to Planning phase!");
    }
}
