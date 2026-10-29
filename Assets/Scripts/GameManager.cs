using UnityEngine;
using System;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { Planning, Combat, Reward, RouteSelection, Victory }
    public GameState CurrentState { get; private set; } = GameState.Planning;

    public int CurrentDay { get; private set; } = 1;
    public int maxStages = 10; // 10-Stage Campaign
    public bool isEndlessMode = false;

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
        "Citadel Breach Gate",
        "The Last Fortress Core"
    };

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        ApplyPostApocalypseTheme();
        SetState(GameState.Planning);
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
        OnDayChanged?.Invoke(CurrentDay);

        // 3. Enter Route Selection
        SetState(GameState.RouteSelection);
        Debug.Log($"[GameManager] Proceeded to {GetStageName()}");
    }

    public void ContinueEndlessMode()
    {
        isEndlessMode = true;
        ProceedToNextDay();
    }

    public void RestartCampaign()
    {
        isEndlessMode = false;
        CurrentDay = 1;
        ResourceManager.Instance?.ResetStartingResources();
        ResetBattlefieldAndStoreBuildings();
        SetState(GameState.Planning);
    }

    public void ResetBattlefieldAndStoreBuildings()
    {
        // 1. First save current base layout blueprint before clearing!
        if (PlacementManager.Instance != null)
        {
            PlacementManager.Instance.SaveCurrentLayout();
        }

        // 2. Collect all buildings from grid and store counts
        if (GridManager.Instance != null && PlacementManager.Instance != null)
        {
            var allBuildings = GridManager.Instance.GetAllBuildings();
            foreach (var b in allBuildings)
            {
                if (b != null)
                {
                    PlacementManager.Instance.AddStoredCount(b.type, 1);
                    Destroy(b.gameObject);
                }
            }
            GridManager.Instance.ClearAll();
        }

        // Clear all remaining soldiers
        Soldier[] soldiers = FindObjectsByType<Soldier>(FindObjectsSortMode.None);
        foreach (var s in soldiers)
        {
            if (s != null) Destroy(s.gameObject);
        }

        // Clear all remaining zombies
        Zombie[] zombies = FindObjectsByType<Zombie>(FindObjectsSortMode.None);
        foreach (var z in zombies)
        {
            if (z != null) Destroy(z.gameObject);
        }

        // Cancel placement preview
        if (PlacementManager.Instance != null)
        {
            PlacementManager.Instance.CancelPlacement();
        }

        Debug.Log("[GameManager] Battlefield reset completed. Layout saved and all placed buildings stored into inventory!");
    }

    public void SelectRoute(bool isBattle)
    {
        if (isBattle)
        {
            SetState(GameState.Planning);
        }
        else
        {
            // Event route - give special reward and go to planning
            ResourceManager.Instance?.AddSpecialReward();
            SetState(GameState.Planning);
        }
    }
}
