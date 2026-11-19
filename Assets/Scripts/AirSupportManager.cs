using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages Air Base aircraft ownership, upgrades (Lv.0 -> Lv.2), combat targeting reticles, and air strike runs.
/// Fighter CAS: 150 Materials to build, 2x5 -> 3x5 (Lv.1) -> 4x5 (Lv.2), 1->2->3 planes, +15% total dmg per upgrade, 7s repeat cooldown.
/// B-2 Bomber: 450 Materials + 5 Special to build, 5x12 blocks (3 bombs, 5x4 each), manual ICBM-style 1-shot strike, 30s cooldown.
/// </summary>
public class AirSupportManager : MonoBehaviour
{
    public static AirSupportManager Instance { get; private set; }

    public const int FIGHTER_MATERIAL_COST = 150;
    public const int FIGHTER_MAX_UPGRADE_LEVEL = 2;
    public const int FIGHTER_UPGRADE_LV1_COST = 100;
    public const int FIGHTER_UPGRADE_LV2_COST = 150;
    public const int FIGHTER_BASE_WIDTH_BLOCKS = 2;
    public const int FIGHTER_BASE_LENGTH_BLOCKS = 5;
    public const float BLOCK_WORLD_SIZE = 2.0f;
    public const float FIGHTER_STRIKE_LENGTH_X = 10.0f; // 5 blocks along flight path (5 * 2m)
    public const float FIGHTER_STRIKE_WIDTH_Z = 4.0f;   // Base 2 blocks across lanes (2 * 2m)
    public const int FIGHTER_SHOTS_PER_BURST = 6;
    public const float FIGHTER_DAMAGE_PER_SHOT = 135f;  // Base Lv.0: 6 * 135 = 810 total AOE dmg per pass
    public const float MAP_AIRCRAFT_ENTER_X = -126.0f;                           // Far West edge of map (off-screen left)
    public const float MAP_AIRCRAFT_EXIT_X = 126.0f;                             // Far East edge of map (off-screen right)
    public const float FIGHTER_CRUISE_SPEED = 48.0f;                             // Fast jet airspeed (m/s) from map edge to target
    public const float FIGHTER_REATTACK_COOLDOWN = 7.0f;                         // Reduced by 1s (8s -> 7s)
    public const float FIGHTER_FLIGHT_DURATION = 2.4f;

    public const int B2_MATERIAL_COST = 450;
    public const int B2_SPECIAL_COST = 5;
    public const int B2_WIDTH_BLOCKS = 5;
    public const int B2_LENGTH_BLOCKS = 12;
    public const int B2_BOMB_COUNT = 3;
    public const float B2_STRIKE_WIDTH_Z = B2_WIDTH_BLOCKS * BLOCK_WORLD_SIZE;   // 10.0m (5 blocks) across lanes
    public const float B2_STRIKE_LENGTH_X = B2_LENGTH_BLOCKS * BLOCK_WORLD_SIZE; // 24.0m (12 blocks) along flight path
    public const float B2_BOMB_HALF_EXTENT_X = 4.0f;                             // 4 blocks (8m) along flight path per bomb
    public const float B2_BOMB_HALF_EXTENT_Z = B2_STRIKE_WIDTH_Z * 0.5f;         // 5 blocks (10m) across lanes per bomb
    public const float B2_BOMB_DAMAGE = 1100f;                                   // Weaker than ICBM (1,700 dmg)
    public const float B2_COOLDOWN = 30.0f;
    public const float B2_CRUISE_SPEED = 28.0f;                                  // Heavy strategic stealth glide (m/s) from map edge
    public const float B2_FLIGHT_DURATION = 4.2f;
    public const float B2_BOMB_FALL_DURATION = 0.65f;                            // Smooth falling arc so bomb drop whistle & visual read clearly
    public const float B2_WINGSPAN = 18.0f;                                      // Massive flying-wing bomber (~3.2x wider than Fighter's 5.6m)

    public bool IsFighterOwned { get; private set; } = false;
    public int FighterUpgradeLevel { get; private set; } = 0; // 0, 1, or 2
    public bool IsTargetingFighter { get; private set; } = false;
    public bool HasFighterTarget { get; private set; } = false;
    public bool IsFighterFlying { get; private set; } = false;
    public float FighterCooldownRemaining { get; private set; } = 0f;
    public float FighterArrivalETA { get; private set; } = 0f;
    public Vector3 FighterTargetPosition { get; private set; } = Vector3.zero;
    public Vector3 ActiveFighterStrikeCenter { get; private set; } = Vector3.zero;

    public bool IsB2Owned { get; private set; } = false;
    public bool IsTargetingB2 { get; private set; } = false;
    public bool IsB2Flying { get; private set; } = false;
    public float B2CooldownRemaining { get; private set; } = 0f;
    public float B2ArrivalETA { get; private set; } = 0f;
    public Vector3 ActiveB2StrikeCenter { get; private set; } = Vector3.zero;
    public Vector3 LastB2HoverPosition => lastB2HoverPosition;

    public static float EstimateFighterArrivalTime(Vector3 targetPos)
    {
        float startX = Mathf.Min(MAP_AIRCRAFT_ENTER_X, targetPos.x - 52f);
        float firstShotX = targetPos.x - 22f;
        return Mathf.Max(0.4f, (firstShotX - startX) / FIGHTER_CRUISE_SPEED);
    }

    public static float EstimateB2ArrivalTime(Vector3 targetPos)
    {
        float startX = Mathf.Min(MAP_AIRCRAFT_ENTER_X, targetPos.x - 60f);
        float firstDropX = (targetPos.x - 8.0f) - 4.2f;
        return Mathf.Max(0.8f, ((firstDropX - startX) / B2_CRUISE_SPEED) + B2_BOMB_FALL_DURATION);
    }

    private int targetingStartFrame = -1;
    private GameObject fighterReticleObj;
    private GameObject fighterLockedZoneObj;
    private readonly List<GameObject> activePlaneObjs = new List<GameObject>();
    private Coroutine activeStrikeCoroutine;

    private GameObject b2ReticleObj;
    private Vector3 lastB2HoverPosition = Vector3.zero;
    private GameObject activeB2PlaneObj;
    private Coroutine activeB2Coroutine;
    private readonly List<GameObject> activeB2BombObjs = new List<GameObject>();
    private readonly List<GameObject> activeB2IncomingMarkers = new List<GameObject>();

    private GameManager.GameState lastObservedState = GameManager.GameState.MainMenu;

    private static readonly float[] ShotNormalizedTimes = new float[]
    {
        0.22f, 0.27f, 0.32f, 0.37f, 0.42f, 0.47f
    };

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public bool HasActiveAirBase()
    {
        return Building.GetActiveBuildingCount(Building.BuildingType.AirBase) > 0;
    }

    public bool CanOperateFighterInCombat()
    {
        return GameManager.Instance != null
            && GameManager.Instance.CurrentState == GameManager.GameState.Combat
            && IsFighterOwned
            && HasActiveAirBase();
    }

    public bool CanAffordFighter()
    {
        if (GameManager.IsDevFreeEverything)
        {
            return true;
        }
        return ResourceManager.Instance != null && ResourceManager.Instance.Materials >= FIGHTER_MATERIAL_COST;
    }

    public bool CanOperateB2InCombat()
    {
        return GameManager.Instance != null
            && GameManager.Instance.CurrentState == GameManager.GameState.Combat
            && IsB2Owned
            && HasActiveAirBase();
    }

    public bool CanAffordB2()
    {
        if (GameManager.IsDevFreeEverything)
        {
            return true;
        }
        return ResourceManager.Instance != null
            && ResourceManager.Instance.Materials >= B2_MATERIAL_COST
            && ResourceManager.Instance.SpecialMaterials >= B2_SPECIAL_COST;
    }

    public bool TryPurchaseB2()
    {
        if (IsB2Owned) return true;

        if (!HasActiveAirBase())
        {
            AudioHelper.PlaySound(SoundEffect.Hit, 0.5f);
            Debug.LogWarning("[AirSupport] Build an Air Base before purchasing B-2 Bomber.");
            return false;
        }

        if (!GameManager.IsDevFreeEverything)
        {
            if (ResourceManager.Instance == null ||
                ResourceManager.Instance.Materials < B2_MATERIAL_COST ||
                ResourceManager.Instance.SpecialMaterials < B2_SPECIAL_COST)
            {
                AudioHelper.PlaySound(SoundEffect.Hit, 0.5f);
                Debug.LogWarning($"[AirSupport] Not enough resources! Need {B2_MATERIAL_COST} Materials and {B2_SPECIAL_COST} Special Materials to build B-2.");
                return false;
            }
            ResourceManager.Instance.SpendMaterials(B2_MATERIAL_COST);
            ResourceManager.Instance.SpendSpecialMaterials(B2_SPECIAL_COST);
        }

        IsB2Owned = true;
        AudioHelper.PlaySound(SoundEffect.ResourceGain, 0.9f);
        Debug.Log("[AirSupport] B-2 Stealth Bomber built and ready for Combat deployment!");
        return true;
    }

    public int GetFighterPlaneCount()
    {
        return 1 + Mathf.Clamp(FighterUpgradeLevel, 0, FIGHTER_MAX_UPGRADE_LEVEL);
    }

    public int GetFighterBlockWidth()
    {
        return FIGHTER_BASE_WIDTH_BLOCKS + Mathf.Clamp(FighterUpgradeLevel, 0, FIGHTER_MAX_UPGRADE_LEVEL);
    }

    public int GetFighterBlockLength()
    {
        return FIGHTER_BASE_LENGTH_BLOCKS;
    }

    public float GetFighterStrikeWidthZ()
    {
        return GetFighterBlockWidth() * BLOCK_WORLD_SIZE;
    }

    public float GetFighterStrikeLengthX()
    {
        return FIGHTER_STRIKE_LENGTH_X;
    }

    public float GetFighterDamageMultiplier()
    {
        // Lv.0: 1.00x, Lv.1: +15% (1.15x), Lv.2: +15% more (+30% total = 1.30x)
        return 1.0f + (0.15f * Mathf.Clamp(FighterUpgradeLevel, 0, FIGHTER_MAX_UPGRADE_LEVEL));
    }

    public float GetFighterTotalDamage()
    {
        return (FIGHTER_SHOTS_PER_BURST * FIGHTER_DAMAGE_PER_SHOT) * GetFighterDamageMultiplier();
    }

    public float GetFighterDamagePerShot()
    {
        int totalShots = GetFighterPlaneCount() * FIGHTER_SHOTS_PER_BURST;
        return GetFighterTotalDamage() / Mathf.Max(1, totalShots);
    }

    public int GetFighterUpgradeCost(int nextLevel)
    {
        if (nextLevel == 1) return FIGHTER_UPGRADE_LV1_COST;
        if (nextLevel == 2) return FIGHTER_UPGRADE_LV2_COST;
        return 0;
    }

    public bool CanUpgradeFighter()
    {
        if (!IsFighterOwned || FighterUpgradeLevel >= FIGHTER_MAX_UPGRADE_LEVEL || !HasActiveAirBase())
        {
            return false;
        }
        if (GameManager.IsDevFreeEverything)
        {
            return true;
        }
        int cost = GetFighterUpgradeCost(FighterUpgradeLevel + 1);
        return ResourceManager.Instance != null && ResourceManager.Instance.Materials >= cost;
    }

    public bool TryUpgradeFighter()
    {
        if (!IsFighterOwned || FighterUpgradeLevel >= FIGHTER_MAX_UPGRADE_LEVEL || !HasActiveAirBase())
        {
            AudioHelper.PlaySound(SoundEffect.Hit, 0.5f);
            return false;
        }

        int nextLv = FighterUpgradeLevel + 1;
        int cost = GetFighterUpgradeCost(nextLv);
        if (!GameManager.IsDevFreeEverything)
        {
            if (ResourceManager.Instance == null || !ResourceManager.Instance.SpendMaterials(cost))
            {
                AudioHelper.PlaySound(SoundEffect.Hit, 0.5f);
                Debug.LogWarning($"[AirSupport] Not enough Materials! Need {cost} Materials to upgrade Fighter to Lv.{nextLv}.");
                return false;
            }
        }

        FighterUpgradeLevel = nextLv;
        RefreshZoneVisualScales();
        AudioHelper.PlaySound(SoundEffect.ResourceGain, 0.9f);
        Debug.Log($"[AirSupport] Fighter upgraded to Lv.{FighterUpgradeLevel}! Planes: {GetFighterPlaneCount()}, Area: {GetFighterBlockWidth()}x{GetFighterBlockLength()}, Total Dmg: {GetFighterTotalDamage():0.#}");
        return true;
    }

    private void RefreshZoneVisualScales()
    {
        float lenX = GetFighterStrikeLengthX();
        float widZ = GetFighterStrikeWidthZ();
        if (fighterReticleObj != null)
        {
            fighterReticleObj.transform.localScale = new Vector3(lenX, 0.06f, widZ);
        }
        if (fighterLockedZoneObj != null)
        {
            fighterLockedZoneObj.transform.localScale = new Vector3(lenX, 0.04f, widZ);
        }
    }

    public bool TryPurchaseFighter()
    {
        if (IsFighterOwned) return true;

        if (!HasActiveAirBase())
        {
            AudioHelper.PlaySound(SoundEffect.Hit, 0.5f);
            Debug.LogWarning("[AirSupport] Build an Air Base before purchasing aircraft.");
            return false;
        }

        if (!GameManager.IsDevFreeEverything)
        {
            if (ResourceManager.Instance == null || !ResourceManager.Instance.SpendMaterials(FIGHTER_MATERIAL_COST))
            {
                AudioHelper.PlaySound(SoundEffect.Hit, 0.5f);
                Debug.LogWarning($"[AirSupport] Not enough Materials! Need {FIGHTER_MATERIAL_COST} Materials to build Fighter.");
                return false;
            }
        }

        IsFighterOwned = true;
        AudioHelper.PlaySound(SoundEffect.ResourceGain, 0.9f);
        Debug.Log("[AirSupport] Fighter CAS aircraft built and ready for Combat deployment!");
        return true;
    }

    public void ToggleFighterTargeting()
    {
        if (!CanOperateFighterInCombat()) return;

        if (IsTargetingFighter)
        {
            CancelTargeting();
            AudioHelper.PlaySound(SoundEffect.ButtonClick, 0.6f);
            return;
        }

        // Cancel B-2, ICBM, or placement targeting so only one reticle is active
        CancelB2Targeting();
        if (Soldier.ActiveTargetingICBM != null)
        {
            Soldier.ActiveTargetingICBM = null;
        }
        if (PlacementManager.Instance != null && PlacementManager.Instance.currentMode != PlacementManager.PlacementMode.None)
        {
            PlacementManager.Instance.CancelPlacement();
        }

        IsTargetingFighter = true;
        targetingStartFrame = Time.frameCount;
        AudioHelper.PlaySound(SoundEffect.ButtonClick, 0.8f);
    }

    public void ToggleB2Targeting()
    {
        if (!CanOperateB2InCombat()) return;

        if (IsTargetingB2)
        {
            CancelB2Targeting();
            AudioHelper.PlaySound(SoundEffect.ButtonClick, 0.6f);
            return;
        }

        if (B2CooldownRemaining > 0f || IsB2Flying)
        {
            AudioHelper.PlaySound(SoundEffect.Hit, 0.45f);
            return;
        }

        // Cancel Fighter, ICBM, or placement targeting so only one reticle is active
        CancelTargeting();
        if (Soldier.ActiveTargetingICBM != null)
        {
            Soldier.ActiveTargetingICBM = null;
        }
        if (PlacementManager.Instance != null && PlacementManager.Instance.currentMode != PlacementManager.PlacementMode.None)
        {
            PlacementManager.Instance.CancelPlacement();
        }

        IsTargetingB2 = true;
        targetingStartFrame = Time.frameCount;
        AudioHelper.PlaySound(SoundEffect.ButtonClick, 0.85f);
    }

    public void CancelTargeting()
    {
        IsTargetingFighter = false;
        if (fighterReticleObj != null)
        {
            fighterReticleObj.SetActive(false);
        }
    }

    public void CancelB2Targeting()
    {
        IsTargetingB2 = false;
        if (b2ReticleObj != null)
        {
            b2ReticleObj.SetActive(false);
        }
    }

    public void CancelAllTargeting()
    {
        CancelTargeting();
        CancelB2Targeting();
    }

    public void ResetForNewStage()
    {
        StopAllCoroutines();
        activeStrikeCoroutine = null;
        activeB2Coroutine = null;

        foreach (var plane in activePlaneObjs)
        {
            if (plane != null) Destroy(plane);
        }
        activePlaneObjs.Clear();

        if (activeB2PlaneObj != null)
        {
            Destroy(activeB2PlaneObj);
            activeB2PlaneObj = null;
        }
        foreach (var bomb in activeB2BombObjs)
        {
            if (bomb != null) Destroy(bomb);
        }
        activeB2BombObjs.Clear();
        foreach (var marker in activeB2IncomingMarkers)
        {
            if (marker != null) Destroy(marker);
        }
        activeB2IncomingMarkers.Clear();

        IsTargetingFighter = false;
        HasFighterTarget = false;
        IsFighterFlying = false;
        FighterCooldownRemaining = 0f;
        FighterArrivalETA = 0f;

        IsTargetingB2 = false;
        IsB2Flying = false;
        B2CooldownRemaining = 0f;
        B2ArrivalETA = 0f;

        if (fighterReticleObj != null) fighterReticleObj.SetActive(false);
        if (fighterLockedZoneObj != null) fighterLockedZoneObj.SetActive(false);
        if (b2ReticleObj != null) b2ReticleObj.SetActive(false);
    }

    public void ResetCampaign()
    {
        ResetForNewStage();
        IsFighterOwned = false;
        FighterUpgradeLevel = 0;
        IsB2Owned = false;
        RefreshZoneVisualScales();
    }

    void Update()
    {
        if (GameManager.Instance == null) return;

        GameManager.GameState curState = GameManager.Instance.CurrentState;
        if (lastObservedState == GameManager.GameState.Combat && curState != GameManager.GameState.Combat)
        {
            ResetForNewStage();
        }
        lastObservedState = curState;

        if (curState != GameManager.GameState.Combat)
        {
            return;
        }

        // --- Fighter CAS Update ---
        if (!IsFighterOwned || !HasActiveAirBase())
        {
            if (IsTargetingFighter) CancelTargeting();
            if (fighterLockedZoneObj != null && fighterLockedZoneObj.activeSelf)
            {
                fighterLockedZoneObj.SetActive(false);
            }
        }
        else
        {
            if (IsTargetingFighter)
            {
                UpdateFighterTargeting();
            }
            else if (fighterReticleObj != null && fighterReticleObj.activeSelf)
            {
                fighterReticleObj.SetActive(false);
            }

            // Tick repeat cooldown (7s AFTER the previous pass finishes)
            if (HasFighterTarget && !IsFighterFlying)
            {
                if (FighterCooldownRemaining > 0f)
                {
                    FighterCooldownRemaining = Mathf.Max(0f, FighterCooldownRemaining - Time.deltaTime);
                }

                if (FighterCooldownRemaining <= 0f)
                {
                    LaunchFighterStrike();
                }
            }
        }

        // --- B-2 Stealth Bomber Update (ICBM-style manual 1-shot strike, 40s cooldown) ---
        if (!IsB2Owned || !HasActiveAirBase())
        {
            if (IsTargetingB2) CancelB2Targeting();
        }
        else
        {
            if (B2CooldownRemaining > 0f)
            {
                B2CooldownRemaining = Mathf.Max(0f, B2CooldownRemaining - Time.deltaTime);
            }

            if (IsTargetingB2)
            {
                UpdateB2Targeting();
            }
            else if (b2ReticleObj != null && b2ReticleObj.activeSelf)
            {
                b2ReticleObj.SetActive(false);
            }
        }
    }

    private void UpdateFighterTargeting()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        bool hasHit = groundPlane.Raycast(ray, out float enter);
        Vector3 hitPoint = hasHit ? ray.GetPoint(enter) : FighterTargetPosition;
        hitPoint.y = 0f;

        EnsureFighterReticle();
        if (fighterReticleObj != null)
        {
            fighterReticleObj.SetActive(true);
            fighterReticleObj.transform.position = new Vector3(hitPoint.x, 0.09f, hitPoint.z);
        }

        // Right-Click -> Cancel targeting without clearing any previously locked strike target
        if (Input.GetMouseButtonDown(1))
        {
            CancelTargeting();
            AudioHelper.PlaySound(SoundEffect.ButtonClick, 0.6f);
            return;
        }

        // Left-Click on a subsequent frame -> Confirm 2x4 block strike zone
        if (Input.GetMouseButtonDown(0) && Time.frameCount > targetingStartFrame)
        {
            if (UIManager.Instance != null && UIManager.Instance.IsPointerOverUI()) return;

            FighterTargetPosition = hitPoint;
            HasFighterTarget = true;
            CancelTargeting();
            UpdateLockedZoneMarker();
            AudioHelper.PlaySound(SoundEffect.ButtonClick, 0.9f);

            if (!IsFighterFlying && FighterCooldownRemaining <= 0f)
            {
                LaunchFighterStrike();
            }
        }
    }

    private void EnsureFighterReticle()
    {
        if (fighterReticleObj == null)
        {
            fighterReticleObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fighterReticleObj.name = "Fighter_AimingReticle";

            Collider c = fighterReticleObj.GetComponent<Collider>();
            if (c != null) Destroy(c);

            Renderer r = fighterReticleObj.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = CrossMapVisualizer.CreateEmissiveExplosionMaterial(
                    new Color(1.0f, 0.30f, 0.12f, 0.52f), false, null, 3152);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            fighterReticleObj.SetActive(false);
        }
        fighterReticleObj.transform.localScale = new Vector3(GetFighterStrikeLengthX(), 0.06f, GetFighterStrikeWidthZ());
    }

    private void UpdateLockedZoneMarker()
    {
        if (!HasFighterTarget)
        {
            if (fighterLockedZoneObj != null) fighterLockedZoneObj.SetActive(false);
            return;
        }

        if (fighterLockedZoneObj == null)
        {
            fighterLockedZoneObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fighterLockedZoneObj.name = "Fighter_LockedZone";

            Collider c = fighterLockedZoneObj.GetComponent<Collider>();
            if (c != null) Destroy(c);

            Renderer r = fighterLockedZoneObj.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = CrossMapVisualizer.CreateEmissiveExplosionMaterial(
                    new Color(0.25f, 0.85f, 1.0f, 0.16f), false, null, 3148);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        fighterLockedZoneObj.transform.localScale = new Vector3(GetFighterStrikeLengthX(), 0.04f, GetFighterStrikeWidthZ());
        fighterLockedZoneObj.transform.position = new Vector3(FighterTargetPosition.x, 0.075f, FighterTargetPosition.z);
        fighterLockedZoneObj.SetActive(true);
    }

    private void LaunchFighterStrike()
    {
        if (IsFighterFlying || !HasFighterTarget) return;
        activeStrikeCoroutine = StartCoroutine(FighterStrafeCoroutine(FighterTargetPosition));
    }

    private IEnumerator FighterStrafeCoroutine(Vector3 strikeCenter)
    {
        IsFighterFlying = true;
        ActiveFighterStrikeCenter = strikeCenter;
        foreach (var p in activePlaneObjs)
        {
            if (p != null) Destroy(p);
        }
        activePlaneObjs.Clear();

        int planeCount = GetFighterPlaneCount();
        float strikeLengthX = GetFighterStrikeLengthX();
        float strikeWidthZ = GetFighterStrikeWidthZ();
        float perShotDmg = GetFighterDamagePerShot();

        // Formation offsets per squadron size (1, 2, or 3 planes in delta/echelon formation)
        float[] zOffsets;
        float[] timeLags;
        float[] yOffsets;
        if (planeCount == 1)
        {
            zOffsets = new float[] { 0f };
            timeLags = new float[] { 0f };
            yOffsets = new float[] { 0f };
        }
        else if (planeCount == 2)
        {
            zOffsets = new float[] { -1.55f, 1.55f };
            timeLags = new float[] { 0f, 0.14f };
            yOffsets = new float[] { 0f, 0.55f };
        }
        else
        {
            zOffsets = new float[] { 0f, 2.35f, -2.35f };
            timeLags = new float[] { 0f, 0.16f, 0.16f };
            yOffsets = new float[] { 0f, 0.65f, 0.65f };
        }

        // Enter from far West edge of map and exit completely past far East edge of map!
        float startX = Mathf.Min(MAP_AIRCRAFT_ENTER_X, strikeCenter.x - 52f);
        float endX = Mathf.Max(MAP_AIRCRAFT_EXIT_X, strikeCenter.x + 52f);
        float flightDuration = Mathf.Max(1.8f, (endX - startX) / FIGHTER_CRUISE_SPEED);

        // Compute normalized shot times directly from world X relative to strikeCenter so strafe hits target accurately from edge entry
        float[] shotU = new float[FIGHTER_SHOTS_PER_BURST];
        float firstShotX = strikeCenter.x - 22f;
        float lastShotX = strikeCenter.x - 4f;
        for (int s = 0; s < FIGHTER_SHOTS_PER_BURST; s++)
        {
            float frac = (FIGHTER_SHOTS_PER_BURST > 1) ? (float)s / (FIGHTER_SHOTS_PER_BURST - 1) : 0.5f;
            float sx = Mathf.Lerp(firstShotX, lastShotX, frac);
            shotU[s] = Mathf.Clamp((sx - startX) / (endX - startX), 0.03f, 0.94f);
        }

        float maxTimeLag = 0f;
        int[] nextShotIndices = new int[planeCount];

        for (int p = 0; p < planeCount; p++)
        {
            if (timeLags[p] > maxTimeLag) maxTimeLag = timeLags[p];
            GameObject planeObj = BuildFighterPlaneInstance();
            activePlaneObjs.Add(planeObj);

            if (planeObj != null)
            {
                Vector3 initPos = EvaluateFlightPosition(startX, endX, strikeCenter.x, strikeCenter.z + zOffsets[p], yOffsets[p], 0f);
                Vector3 initNext = EvaluateFlightPosition(startX, endX, strikeCenter.x, strikeCenter.z + zOffsets[p], yOffsets[p], 0.01f);
                planeObj.transform.position = initPos;
                Vector3 initDir = initNext - initPos;
                if (initDir.sqrMagnitude > 0.0001f)
                {
                    planeObj.transform.rotation = Quaternion.LookRotation(initDir.normalized, Vector3.up);
                }
                foreach (var tr in planeObj.GetComponentsInChildren<TrailRenderer>(true))
                {
                    if (tr != null) tr.Clear();
                }
            }
        }

        float totalRunTime = flightDuration + maxTimeLag;
        float elapsed = 0f;
        FighterArrivalETA = Mathf.Max(0f, (firstShotX - startX) / FIGHTER_CRUISE_SPEED);

        while (elapsed < totalRunTime)
        {
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameManager.GameState.Combat)
            {
                break;
            }

            elapsed += Time.deltaTime;
            float leadU = Mathf.Clamp01(elapsed / flightDuration);
            float leadX = Mathf.Lerp(startX, endX, leadU);
            FighterArrivalETA = Mathf.Max(0f, (firstShotX - leadX) / FIGHTER_CRUISE_SPEED);

            for (int p = 0; p < planeCount; p++)
            {
                float planeElapsed = elapsed - timeLags[p];
                float planeU = Mathf.Clamp01(planeElapsed / flightDuration);
                float planeZ = strikeCenter.z + zOffsets[p];
                Vector3 pos = EvaluateFlightPosition(startX, endX, strikeCenter.x, planeZ, yOffsets[p], planeU);
                Vector3 nextPos = EvaluateFlightPosition(startX, endX, strikeCenter.x, planeZ, yOffsets[p], Mathf.Min(1f, planeU + 0.01f));

                GameObject planeObj = (p < activePlaneObjs.Count) ? activePlaneObjs[p] : null;
                if (planeObj != null)
                {
                    if (planeElapsed < 0f || planeElapsed > flightDuration)
                    {
                        if (planeObj.activeSelf) planeObj.SetActive(false);
                    }
                    else
                    {
                        if (!planeObj.activeSelf)
                        {
                            planeObj.SetActive(true);
                            planeObj.transform.position = pos;
                            foreach (var tr in planeObj.GetComponentsInChildren<TrailRenderer>(true))
                            {
                                if (tr != null) tr.Clear();
                            }
                        }

                        planeObj.transform.position = pos;
                        Vector3 tangent = nextPos - pos;
                        if (tangent.sqrMagnitude > 0.0001f)
                        {
                            Quaternion lookRot = Quaternion.LookRotation(tangent.normalized, Vector3.up);
                            float rollAngle = Mathf.Sin(planeU * Mathf.PI * 2f) * 8f;
                            planeObj.transform.rotation = lookRot * Quaternion.Euler(0f, 0f, rollAngle);
                        }
                    }
                }

                while (nextShotIndices[p] < shotU.Length && planeElapsed >= 0f && planeU >= shotU[nextShotIndices[p]])
                {
                    Vector3 muzzlePos = (planeObj != null)
                        ? planeObj.transform.position + planeObj.transform.forward * 1.4f - Vector3.up * 0.25f
                        : pos;
                    FireFighterStrafeRound(muzzlePos, strikeCenter, strikeLengthX, strikeWidthZ, zOffsets[p], nextShotIndices[p], perShotDmg, planeCount);
                    nextShotIndices[p]++;
                }
            }

            yield return null;
        }

        foreach (var p in activePlaneObjs)
        {
            if (p != null) Destroy(p);
        }
        activePlaneObjs.Clear();

        FighterArrivalETA = 0f;
        IsFighterFlying = false;
        activeStrikeCoroutine = null;

        // Begin 7-second repeat cooldown AFTER all planes finish flying past
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.GameState.Combat)
        {
            FighterCooldownRemaining = FIGHTER_REATTACK_COOLDOWN;
        }
    }

    private static Vector3 EvaluateFlightPosition(float startX, float endX, float targetX, float targetZ, float yOffset, float u)
    {
        float x = Mathf.Lerp(startX, endX, u);
        // Cruise high from map edge, dive smoothly right over targetX to strafe, then climb back out off far East edge
        float dx = x - targetX;
        float y = 23.0f - 17.2f * Mathf.Exp(-(dx * dx) / 720f) + yOffset;
        return new Vector3(x, y, targetZ);
    }

    private void FireFighterStrafeRound(Vector3 muzzlePos, Vector3 strikeCenter, float strikeLengthX, float strikeWidthZ,
        float laneOffsetZ, int shotIndex, float shotDamage, int planeCount)
    {
        float halfX = strikeLengthX * 0.5f;
        float halfZ = strikeWidthZ * 0.5f;

        // Sweep impact points cleanly across this plane's lane inside the defined strike zone
        float sweepT = (FIGHTER_SHOTS_PER_BURST > 1) ? (float)shotIndex / (FIGHTER_SHOTS_PER_BURST - 1) : 0.5f;
        float impactX = Mathf.Lerp(strikeCenter.x - halfX * 0.85f, strikeCenter.x + halfX * 0.85f, sweepT);
        float laneJitterZ = laneOffsetZ + ((shotIndex % 2 == 0) ? -0.55f : 0.55f) + Random.Range(-0.35f, 0.35f);
        float impactZ = strikeCenter.z + Mathf.Clamp(laneJitterZ, -halfZ * 0.85f, halfZ * 0.85f);
        Vector3 impactPos = new Vector3(impactX, 0.25f, impactZ);

        // Damage all zombies inside the active strike area (2x5 -> 3x5 -> 4x5)
        float hitHalfX = halfX + 0.35f;
        float hitHalfZ = halfZ + 0.35f;
        IEnumerable<Zombie> candidates = (WaveManager.Instance != null && WaveManager.Instance.ActiveZombies != null)
            ? (IEnumerable<Zombie>)WaveManager.Instance.ActiveZombies
            : FindObjectsByType<Zombie>();

        List<Zombie> hitList = new List<Zombie>();
        foreach (Zombie z in candidates)
        {
            if (z == null || z.isDead) continue;
            float dx = Mathf.Abs(z.transform.position.x - strikeCenter.x);
            float dz = Mathf.Abs(z.transform.position.z - strikeCenter.z);
            if (dx <= hitHalfX && dz <= hitHalfZ)
            {
                hitList.Add(z);
            }
        }

        foreach (Zombie z in hitList)
        {
            if (z != null && !z.isDead)
            {
                z.TakeDamage(shotDamage);
            }
        }

        SpawnStrafeTracerAndImpactFx(muzzlePos, impactPos);
        float volScale = 1.0f / Mathf.Sqrt(Mathf.Max(1, planeCount));
        AudioHelper.PlaySound(SoundEffect.SMGShoot, 0.75f * volScale);
        AudioHelper.PlaySound(SoundEffect.Hit, 0.55f * volScale);
    }

    private static void SpawnStrafeTracerAndImpactFx(Vector3 muzzlePos, Vector3 impactPos)
    {
        // 1. Bright tracer beam from plane down to impact point (visible above darkness shroud)
        Vector3 dir = impactPos - muzzlePos;
        float dist = dir.magnitude;
        if (dist > 0.2f)
        {
            GameObject tracer = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tracer.name = "Fighter_StrafeTracer";
            tracer.transform.position = (muzzlePos + impactPos) * 0.5f;
            tracer.transform.up = dir.normalized;
            tracer.transform.localScale = new Vector3(0.14f, dist * 0.5f, 0.14f);
            Collider c = tracer.GetComponent<Collider>();
            if (c != null) Destroy(c);
            Renderer r = tracer.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = CrossMapVisualizer.CreateEmissiveExplosionMaterial(
                    new Color(1.0f, 0.85f, 0.30f, 0.92f), true, null, 3162);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            Destroy(tracer, 0.11f);
        }

        // 2. Impact flash sphere + brief darkness flash
        CrossMapVisualizer.RegisterExplosionFlash(impactPos, 9f, 0.30f, 0.85f);

        GameObject blast = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        blast.name = "Fighter_StrafeImpact";
        blast.transform.position = impactPos + Vector3.up * 0.35f;
        blast.transform.localScale = new Vector3(1.8f, 1.8f, 1.8f);
        Collider c2 = blast.GetComponent<Collider>();
        if (c2 != null) Destroy(c2);
        Renderer r2 = blast.GetComponent<Renderer>();
        if (r2 != null)
        {
            r2.sharedMaterial = CrossMapVisualizer.CreateEmissiveExplosionMaterial(
                new Color(1.0f, 0.60f, 0.18f, 0.92f), true, null, 3160);
            r2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r2.receiveShadows = false;
        }
        Destroy(blast, 0.18f);
    }

    private static GameObject BuildFighterPlaneInstance()
    {
        GameObject root = new GameObject("FighterPlane_StrikeUnit");
        GameObject fbxPrefab = Resources.Load<GameObject>("Models/Fighter_Plane");

        if (fbxPrefab != null)
        {
            GameObject pivot = new GameObject("Fighter_AlignPivot");
            pivot.transform.SetParent(root.transform, false);

            GameObject modelInstance = Instantiate(fbxPrefab, pivot.transform);
            modelInstance.name = "FighterPlane_FBX";
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = fbxPrefab.transform.localRotation;
            modelInstance.transform.localScale = fbxPrefab.transform.localScale;

            // Remove the co-packaged B-2 Bomber hierarchy ("Sketchfab_model") so only the Fighter ("Sketchfab_model.001") renders
            Transform b2Root = modelInstance.transform.Find("Sketchfab_model");
            if (b2Root != null)
            {
                DestroyImmediate(b2Root.gameObject);
            }
            foreach (Transform t in modelInstance.GetComponentsInChildren<Transform>(true))
            {
                if (t == null) continue;
                if (t.name.StartsWith("B-2") || t.name.StartsWith("LGFront") || t.name == "GLTF_SceneRootNode")
                {
                    DestroyImmediate(t.gameObject);
                }
            }

            UnitBlockVisualizer.StripEmbeddedSceneComponents(modelInstance);

            Renderer[] renderers = modelInstance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                ApplyFighterMaterials(renderers);
                OrientAndScaleFighter(pivot, modelInstance, targetWingspan: 5.6f, out Vector3 leftWingTipLocal, out Vector3 rightWingTipLocal);
                AttachWingtipWindTrails(root, leftWingTipLocal, rightWingTipLocal);
                return root;
            }
        }

        // Clean procedural fallback if FBX could not be loaded
        BuildProceduralFighterFallback(root);
        AttachWingtipWindTrails(root, new Vector3(-2.75f, 0f, -0.25f), new Vector3(2.75f, 0f, -0.25f));
        return root;
    }

    private static void ApplyFighterMaterials(Renderer[] renderers)
    {
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ??
                           Shader.Find("Standard") ??
                           Shader.Find("Universal Render Pipeline/Unlit");

        foreach (var r in renderers)
        {
            if (r == null || r.sharedMaterials == null) continue;

            string objLower = r.gameObject.name.ToLowerInvariant();
            bool isDetailPart = objLower.Contains("object_2") || objLower.Contains("object_3");
            Color bodyColor = isDetailPart
                ? new Color(0.18f, 0.21f, 0.25f)
                : new Color(0.28f, 0.37f, 0.46f);

            Material[] mats = r.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                Material mat = (litShader != null) ? new Material(litShader) : mats[i];
                if (mat != null)
                {
                    mat.color = bodyColor;
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", bodyColor);
                    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", isDetailPart ? 0.55f : 0.40f);
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", isDetailPart ? 0.50f : 0.45f);
                }
                mats[i] = mat;
            }
            r.materials = mats;
        }
    }

    private static bool TrySampleWingtipAverage(GameObject modelInstance, Bounds bounds, float thresholdRatio, out float avgY, out float avgZ)
    {
        float centerX = bounds.center.x;
        float wingThresholdX = Mathf.Max(0.1f, bounds.size.x * thresholdRatio);
        float wingZSum = 0f;
        float wingYSum = 0f;
        int wingCount = 0;

        foreach (var mf in modelInstance.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
            Vector3[] verts = mf.sharedMesh.vertices;
            int step = Mathf.Max(1, verts.Length / 1500);
            for (int i = 0; i < verts.Length; i += step)
            {
                Vector3 wp = mf.transform.TransformPoint(verts[i]);
                if (Mathf.Abs(wp.x - centerX) >= wingThresholdX)
                {
                    wingZSum += wp.z;
                    wingYSum += wp.y;
                    wingCount++;
                }
            }
        }

        if (wingCount > 0)
        {
            avgY = wingYSum / wingCount;
            avgZ = wingZSum / wingCount;
            return true;
        }

        avgY = bounds.center.y;
        avgZ = bounds.center.z;
        return false;
    }

    private static void OrientAndScaleFighter(GameObject pivot, GameObject modelInstance, float targetWingspan,
        out Vector3 leftWingTipLocal, out Vector3 rightWingTipLocal)
    {
        leftWingTipLocal = new Vector3(-targetWingspan * 0.485f, 0f, 0f);
        rightWingTipLocal = new Vector3(targetWingspan * 0.485f, 0f, 0f);

        if (!UnitBlockVisualizer.TryCalculateExactWorldBounds(modelInstance, out Bounds bounds)) return;

        // 1. Align widest horizontal axis (wingspan) along X so fuselage length runs along Z
        if (bounds.size.x < bounds.size.z)
        {
            modelInstance.transform.Rotate(0f, 90f, 0f, Space.World);
            UnitBlockVisualizer.TryCalculateExactWorldBounds(modelInstance, out bounds);
        }

        // 2. Main wings sit closer to nose than rear tail
        if (TrySampleWingtipAverage(modelInstance, bounds, 0.35f, out _, out float wingAvgZ))
        {
            float distToMinZ = Mathf.Abs(wingAvgZ - bounds.min.z);
            float distToMaxZ = Mathf.Abs(bounds.max.z - wingAvgZ);
            if (distToMinZ < distToMaxZ)
            {
                modelInstance.transform.Rotate(0f, 180f, 0f, Space.World);
                UnitBlockVisualizer.TryCalculateExactWorldBounds(modelInstance, out bounds);
            }
        }

        // 3. Recompute exact wingtip Y/Z in aligned space and scale uniformly to targetWingspan
        bool hasTips = TrySampleWingtipAverage(modelInstance, bounds, 0.42f, out float tipWorldY, out float tipWorldZ);

        modelInstance.transform.position += (pivot.transform.position - bounds.center);
        float wingspan = Mathf.Max(0.01f, bounds.size.x);
        float uniformScale = targetWingspan / wingspan;
        pivot.transform.localScale = Vector3.one * uniformScale;

        float tipLocalY = hasTips ? (tipWorldY - bounds.center.y) * uniformScale : 0f;
        float tipLocalZ = hasTips ? (tipWorldZ - bounds.center.z) * uniformScale : 0f;
        leftWingTipLocal = new Vector3(-targetWingspan * 0.49f, tipLocalY, tipLocalZ);
        rightWingTipLocal = new Vector3(targetWingspan * 0.49f, tipLocalY, tipLocalZ);
    }

    private static void AttachWingtipWindTrails(GameObject root, Vector3 leftTipLocal, Vector3 rightTipLocal, float startWidth = 0.22f, float trailTime = 0.52f)
    {
        Material trailMat = CrossMapVisualizer.CreateEmissiveExplosionMaterial(
            Color.white, true, null, 3158);

        Gradient windGrad = new Gradient();
        windGrad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.92f, 0.97f, 1.0f), 0f),
                new GradientColorKey(new Color(0.75f, 0.90f, 1.0f), 0.55f),
                new GradientColorKey(new Color(0.65f, 0.85f, 1.0f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.65f, 0f),
                new GradientAlphaKey(0.28f, 0.50f),
                new GradientAlphaKey(0.0f, 1f)
            }
        );

        CreateWingtipTrail(root.transform, "WingtipVortex_Left", leftTipLocal, trailMat, windGrad, startWidth, trailTime);
        CreateWingtipTrail(root.transform, "WingtipVortex_Right", rightTipLocal, trailMat, windGrad, startWidth, trailTime);
    }

    private static void CreateWingtipTrail(Transform parent, string name, Vector3 localPos, Material mat, Gradient grad, float startWidth, float trailTime)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPos;

        TrailRenderer tr = obj.AddComponent<TrailRenderer>();
        tr.sharedMaterial = mat;
        tr.colorGradient = grad;
        tr.time = trailTime;
        tr.minVertexDistance = 0.18f;
        tr.startWidth = startWidth;
        tr.endWidth = 0.02f;
        tr.numCapVertices = 2;
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tr.receiveShadows = false;
    }

    private static void BuildProceduralFighterFallback(GameObject parent)
    {
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material bodyMat = new Material(litShader) { color = new Color(0.28f, 0.37f, 0.46f) };
        Material darkMat = new Material(litShader) { color = new Color(0.15f, 0.18f, 0.22f) };

        var fuselage = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fuselage.transform.SetParent(parent.transform, false);
        fuselage.transform.localScale = new Vector3(0.8f, 0.65f, 4.2f);
        Destroy(fuselage.GetComponent<Collider>());
        fuselage.GetComponent<Renderer>().sharedMaterial = bodyMat;

        var wings = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wings.transform.SetParent(parent.transform, false);
        wings.transform.localScale = new Vector3(5.6f, 0.15f, 1.3f);
        wings.transform.localPosition = new Vector3(0f, 0f, 0.4f);
        Destroy(wings.GetComponent<Collider>());
        wings.GetComponent<Renderer>().sharedMaterial = bodyMat;

        var tail = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tail.transform.SetParent(parent.transform, false);
        tail.transform.localScale = new Vector3(0.14f, 0.9f, 0.8f);
        tail.transform.localPosition = new Vector3(0f, 0.45f, -1.7f);
        Destroy(tail.GetComponent<Collider>());
        tail.GetComponent<Renderer>().sharedMaterial = darkMat;
    }

    // ==================== B-2 STEALTH BOMBER (4x12 CARPET STRIKE, 3 BOMBS, 40s COOLDOWN) ====================

    private void UpdateB2Targeting()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        bool hasHit = groundPlane.Raycast(ray, out float enter);
        Vector3 hitPoint = hasHit ? ray.GetPoint(enter) : lastB2HoverPosition;
        hitPoint.y = 0f;
        lastB2HoverPosition = hitPoint;

        EnsureB2Reticle();
        if (b2ReticleObj != null)
        {
            b2ReticleObj.SetActive(true);
            b2ReticleObj.transform.position = new Vector3(hitPoint.x, 0.095f, hitPoint.z);
        }

        if (Input.GetMouseButtonDown(1))
        {
            CancelB2Targeting();
            AudioHelper.PlaySound(SoundEffect.ButtonClick, 0.6f);
            return;
        }

        if (Input.GetMouseButtonDown(0) && Time.frameCount > targetingStartFrame)
        {
            if (UIManager.Instance != null && UIManager.Instance.IsPointerOverUI()) return;

            CancelB2Targeting();
            B2CooldownRemaining = B2_COOLDOWN;
            activeB2Coroutine = StartCoroutine(B2BombingRunCoroutine(hitPoint));
            AudioHelper.PlaySound(SoundEffect.ButtonClick, 0.95f);
        }
    }

    private void EnsureB2Reticle()
    {
        if (b2ReticleObj != null) return;

        b2ReticleObj = new GameObject("B2_AimingReticle_5x12");

        // Outer 5x12 blocks (24m along X x 10m along Z) carpet corridor
        GameObject strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        strip.name = "B2_CarpetStrip";
        strip.transform.SetParent(b2ReticleObj.transform, false);
        strip.transform.localScale = new Vector3(B2_STRIKE_LENGTH_X, 0.05f, B2_STRIKE_WIDTH_Z);
        Collider stripCol = strip.GetComponent<Collider>();
        if (stripCol != null) Destroy(stripCol);
        Renderer stripRend = strip.GetComponent<Renderer>();
        if (stripRend != null)
        {
            stripRend.sharedMaterial = CrossMapVisualizer.CreateEmissiveExplosionMaterial(
                new Color(1.0f, 0.42f, 0.10f, 0.34f), false, null, 3150);
            stripRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            stripRend.receiveShadows = false;
        }

        // 3 sub-sector pads (each 5x4 blocks = 10m Z x 8m X) showing Left -> Right bomb drop order
        Material padMat = CrossMapVisualizer.CreateEmissiveExplosionMaterial(
            new Color(1.0f, 0.22f, 0.08f, 0.48f), false, null, 3151);
        float[] offsetX = new float[] { -8.0f, 0f, 8.0f };
        for (int i = 0; i < B2_BOMB_COUNT; i++)
        {
            GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = $"B2_BombZone_{i + 1}";
            pad.transform.SetParent(b2ReticleObj.transform, false);
            pad.transform.localPosition = new Vector3(offsetX[i], 0.01f, 0f);
            pad.transform.localScale = new Vector3(7.5f, 0.055f, B2_STRIKE_WIDTH_Z - 0.5f);
            Collider c = pad.GetComponent<Collider>();
            if (c != null) Destroy(c);
            Renderer r = pad.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = padMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        // Right-pointing lead indicator bar at the front (+X) edge of the 4x12 strip
        GameObject leadEdge = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leadEdge.name = "B2_FlightExitEdge";
        leadEdge.transform.SetParent(b2ReticleObj.transform, false);
        leadEdge.transform.localPosition = new Vector3(B2_STRIKE_LENGTH_X * 0.5f - 0.18f, 0.02f, 0f);
        leadEdge.transform.localScale = new Vector3(0.32f, 0.06f, B2_STRIKE_WIDTH_Z);
        Collider edgeCol = leadEdge.GetComponent<Collider>();
        if (edgeCol != null) Destroy(edgeCol);
        Renderer edgeRend = leadEdge.GetComponent<Renderer>();
        if (edgeRend != null)
        {
            edgeRend.sharedMaterial = CrossMapVisualizer.CreateEmissiveExplosionMaterial(
                new Color(1.0f, 0.85f, 0.25f, 0.85f), true, null, 3153);
            edgeRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            edgeRend.receiveShadows = false;
        }

        b2ReticleObj.SetActive(false);
    }

    private IEnumerator B2BombingRunCoroutine(Vector3 strikeCenter)
    {
        IsB2Flying = true;
        ActiveB2StrikeCenter = strikeCenter;
        if (activeB2PlaneObj != null)
        {
            Destroy(activeB2PlaneObj);
            activeB2PlaneObj = null;
        }
        foreach (var m in activeB2IncomingMarkers)
        {
            if (m != null) Destroy(m);
        }
        activeB2IncomingMarkers.Clear();

        activeB2PlaneObj = BuildB2BomberInstance();

        // Twin-layered 2D flyover AudioSources so the B-2 stealth bomber jet roar is deep, loud, and unmistakable!
        AudioSource flyoverAudioMain = null;
        AudioSource flyoverAudioSub = null;
        if (activeB2PlaneObj != null)
        {
            AudioClip whooshClip = AudioHelper.GetClip(SoundEffect.B2FlyoverWhoosh);
            if (whooshClip != null)
            {
                flyoverAudioMain = activeB2PlaneObj.AddComponent<AudioSource>();
                flyoverAudioMain.clip = whooshClip;
                flyoverAudioMain.playOnAwake = false;
                flyoverAudioMain.loop = true;
                flyoverAudioMain.spatialBlend = 0f;
                flyoverAudioMain.volume = 0.65f;
                flyoverAudioMain.pitch = 1.0f;
                flyoverAudioMain.Play();

                flyoverAudioSub = activeB2PlaneObj.AddComponent<AudioSource>();
                flyoverAudioSub.clip = whooshClip;
                flyoverAudioSub.playOnAwake = false;
                flyoverAudioSub.loop = true;
                flyoverAudioSub.spatialBlend = 0f;
                flyoverAudioSub.volume = 0.65f;
                flyoverAudioSub.pitch = 0.84f; // Deep heavy bomber rumble layer
                flyoverAudioSub.Play();
            }
        }

        // Enter from far West edge of map and exit completely past far East edge of map!
        float startX = Mathf.Min(MAP_AIRCRAFT_ENTER_X, strikeCenter.x - 60f);
        float endX = Mathf.Max(MAP_AIRCRAFT_EXIT_X, strikeCenter.x + 60f);
        float flightDuration = Mathf.Max(3.5f, (endX - startX) / B2_CRUISE_SPEED);

        if (activeB2PlaneObj != null)
        {
            Vector3 initPos = EvaluateB2FlightPosition(startX, endX, strikeCenter.x, strikeCenter.z, 0f);
            Vector3 initNext = EvaluateB2FlightPosition(startX, endX, strikeCenter.x, strikeCenter.z, 0.01f);
            activeB2PlaneObj.transform.position = initPos;
            Vector3 initDir = initNext - initPos;
            if (initDir.sqrMagnitude > 0.0001f)
            {
                activeB2PlaneObj.transform.rotation = Quaternion.LookRotation(initDir.normalized, Vector3.up);
            }
            foreach (var tr in activeB2PlaneObj.GetComponentsInChildren<TrailRenderer>(true))
            {
                if (tr != null) tr.Clear();
            }
        }

        Vector3[] bombTargets = new Vector3[B2_BOMB_COUNT]
        {
            new Vector3(strikeCenter.x - 8.0f, 0.25f, strikeCenter.z),
            new Vector3(strikeCenter.x, 0.25f, strikeCenter.z),
            new Vector3(strikeCenter.x + 8.0f, 0.25f, strikeCenter.z)
        };
        GameObject[] sectorMarkers = new GameObject[B2_BOMB_COUNT];
        Material incomingMat = CrossMapVisualizer.CreateEmissiveExplosionMaterial(
            new Color(1.0f, 0.30f, 0.10f, 0.25f), false, null, 3149);
        for (int i = 0; i < B2_BOMB_COUNT; i++)
        {
            GameObject mk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mk.name = $"B2_IncomingSector_{i + 1}";
            mk.transform.position = new Vector3(bombTargets[i].x, 0.085f, bombTargets[i].z);
            mk.transform.localScale = new Vector3(7.6f, 0.04f, B2_STRIKE_WIDTH_Z - 0.4f);
            Collider mc = mk.GetComponent<Collider>();
            if (mc != null) Destroy(mc);
            Renderer mr = mk.GetComponent<Renderer>();
            if (mr != null)
            {
                mr.sharedMaterial = incomingMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            sectorMarkers[i] = mk;
            activeB2IncomingMarkers.Add(mk);
        }

        float[] dropU = new float[B2_BOMB_COUNT];
        float firstDropX = bombTargets[0].x - 4.2f;
        for (int i = 0; i < B2_BOMB_COUNT; i++)
        {
            // Release slightly upstream so bomb glides smoothly onto its 5x4 target point
            dropU[i] = Mathf.Clamp((bombTargets[i].x - 4.2f - startX) / (endX - startX), 0.02f, 0.95f);
        }

        // Prevent a zombie on a 5x4 boundary from taking double bomb damage so max dmg stays strictly below ICBM (1,700)
        HashSet<Zombie> damagedThisPass = new HashSet<Zombie>();
        int nextBombIdx = 0;
        float elapsed = 0f;
        B2ArrivalETA = Mathf.Max(0f, ((firstDropX - startX) / B2_CRUISE_SPEED) + B2_BOMB_FALL_DURATION);

        while (elapsed < flightDuration)
        {
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameManager.GameState.Combat)
            {
                break;
            }

            elapsed += Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / flightDuration);
            Vector3 pos = EvaluateB2FlightPosition(startX, endX, strikeCenter.x, strikeCenter.z, u);
            Vector3 nextPos = EvaluateB2FlightPosition(startX, endX, strikeCenter.x, strikeCenter.z, Mathf.Min(1f, u + 0.01f));

            B2ArrivalETA = Mathf.Max(0f, ((firstDropX - pos.x) / B2_CRUISE_SPEED) + B2_BOMB_FALL_DURATION);

            if (activeB2PlaneObj != null)
            {
                activeB2PlaneObj.transform.position = pos;
                Vector3 tangent = nextPos - pos;
                if (tangent.sqrMagnitude > 0.0001f)
                {
                    Quaternion lookRot = Quaternion.LookRotation(tangent.normalized, Vector3.up);
                    float rollAngle = Mathf.Sin(u * Mathf.PI * 2f) * 3f;
                    activeB2PlaneObj.transform.rotation = lookRot * Quaternion.Euler(0f, 0f, rollAngle);
                }

                // Swell twin-layer engine roar to 100% peak as the B-2 crosses the battlefield, then fade smoothly off East edge
                float distToTarget = Mathf.Abs(pos.x - strikeCenter.x);
                float proximityVol = Mathf.Lerp(1.0f, 0.55f, Mathf.Clamp01(distToTarget / 90f));
                float edgeFadeIn = Mathf.Clamp01(u / 0.08f);
                float edgeFadeOut = (u >= 0.84f) ? Mathf.InverseLerp(1.0f, 0.84f, u) : 1.0f;
                float layerVol = Mathf.Clamp01(proximityVol * edgeFadeIn * edgeFadeOut);
                if (flyoverAudioMain != null && flyoverAudioMain.isPlaying) flyoverAudioMain.volume = layerVol;
                if (flyoverAudioSub != null && flyoverAudioSub.isPlaying) flyoverAudioSub.volume = layerVol * 0.95f;
            }

            while (nextBombIdx < B2_BOMB_COUNT && u >= dropU[nextBombIdx])
            {
                Vector3 releasePos = (activeB2PlaneObj != null)
                    ? activeB2PlaneObj.transform.position - Vector3.up * 0.65f
                    : pos;
                StartCoroutine(DropAndDetonateB2BombCoroutine(releasePos, bombTargets[nextBombIdx], damagedThisPass, sectorMarkers[nextBombIdx]));
                nextBombIdx++;
            }

            yield return null;
        }

        if (activeB2PlaneObj != null)
        {
            Destroy(activeB2PlaneObj);
            activeB2PlaneObj = null;
        }

        B2ArrivalETA = 0f;
        IsB2Flying = false;
        activeB2Coroutine = null;
    }

    private static Vector3 EvaluateB2FlightPosition(float startX, float endX, float targetX, float targetZ, float u)
    {
        float x = Mathf.Lerp(startX, endX, u);
        // Smooth, imposing high-altitude stealth glide dipping gently over targetX
        float dx = x - targetX;
        float y = 23.5f - 7.5f * Mathf.Exp(-(dx * dx) / 1600f);
        return new Vector3(x, y, targetZ);
    }

    private IEnumerator DropAndDetonateB2BombCoroutine(Vector3 releasePos, Vector3 targetPos, HashSet<Zombie> damagedThisPass, GameObject incomingMarker)
    {
        GameObject bombObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        bombObj.name = "B2_HeavyBomb";
        bombObj.transform.position = releasePos;
        bombObj.transform.localScale = new Vector3(0.42f, 0.72f, 0.42f);
        Collider c = bombObj.GetComponent<Collider>();
        if (c != null) Destroy(c);
        Renderer r = bombObj.GetComponent<Renderer>();
        if (r != null)
        {
            r.sharedMaterial = CrossMapVisualizer.CreateEmissiveExplosionMaterial(
                new Color(0.22f, 0.24f, 0.28f, 1.0f), false, null, 3159);
        }
        activeB2BombObjs.Add(bombObj);

        // Play bomb-dropping whistle while descending, fading cleanly into the explosion
        GameObject dropAudioObj = null;
        AudioSource dropAudioSrc = null;
        AudioClip dropClip = AudioHelper.GetClip(SoundEffect.B2BombDropping);
        if (dropClip != null)
        {
            dropAudioObj = new GameObject("B2_BombDropAudio");
            dropAudioObj.transform.position = releasePos;
            dropAudioSrc = dropAudioObj.AddComponent<AudioSource>();
            dropAudioSrc.clip = dropClip;
            dropAudioSrc.playOnAwake = false;
            dropAudioSrc.loop = false;
            dropAudioSrc.spatialBlend = 0f;
            dropAudioSrc.volume = 1.0f;
            dropAudioSrc.pitch = Random.Range(0.96f, 1.05f);
            dropAudioSrc.Play();
            activeB2BombObjs.Add(dropAudioObj);
        }

        float elapsed = 0f;
        while (elapsed < B2_BOMB_FALL_DURATION)
        {
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameManager.GameState.Combat)
            {
                activeB2BombObjs.Remove(bombObj);
                if (bombObj != null) Destroy(bombObj);
                if (dropAudioObj != null)
                {
                    activeB2BombObjs.Remove(dropAudioObj);
                    Destroy(dropAudioObj);
                }
                if (incomingMarker != null)
                {
                    activeB2IncomingMarkers.Remove(incomingMarker);
                    Destroy(incomingMarker);
                }
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / B2_BOMB_FALL_DURATION);
            float easeY = t * t; // Gravity acceleration downward while gliding forward along X
            float bx = Mathf.Lerp(releasePos.x, targetPos.x, t);
            float by = Mathf.Lerp(releasePos.y, targetPos.y, easeY);
            float bz = Mathf.Lerp(releasePos.z, targetPos.z, t);
            Vector3 nextPos = new Vector3(bx, by, bz);

            if (bombObj != null)
            {
                Vector3 vel = nextPos - bombObj.transform.position;
                bombObj.transform.position = nextPos;
                if (vel.sqrMagnitude > 0.0001f)
                {
                    bombObj.transform.up = -vel.normalized;
                }
            }
            yield return null;
        }

        activeB2BombObjs.Remove(bombObj);
        if (bombObj != null) Destroy(bombObj);
        if (incomingMarker != null)
        {
            activeB2IncomingMarkers.Remove(incomingMarker);
            Destroy(incomingMarker);
        }

        DetonateB2Bomb(targetPos, damagedThisPass);

        if (dropAudioObj != null)
        {
            StartCoroutine(FadeOutAndDestroyAudio(dropAudioObj, dropAudioSrc, 0.18f));
        }
    }

    private IEnumerator FadeOutAndDestroyAudio(GameObject audioObj, AudioSource src, float fadeDuration)
    {
        if (audioObj == null) yield break;
        float startVol = (src != null) ? src.volume : 0f;
        float elapsed = 0f;
        while (elapsed < fadeDuration && audioObj != null && src != null && src.isPlaying)
        {
            elapsed += Time.deltaTime;
            src.volume = startVol * (1f - Mathf.Clamp01(elapsed / fadeDuration));
            yield return null;
        }
        activeB2BombObjs.Remove(audioObj);
        if (audioObj != null) Destroy(audioObj);
    }

    private void DetonateB2Bomb(Vector3 impactPos, HashSet<Zombie> damagedThisPass)
    {
        float halfExtentX = B2_BOMB_HALF_EXTENT_X + 0.15f; // 4 blocks along X (8m)
        float halfExtentZ = B2_BOMB_HALF_EXTENT_Z + 0.15f; // 5 blocks wide along Z (10m)
        IEnumerable<Zombie> candidates = (WaveManager.Instance != null && WaveManager.Instance.ActiveZombies != null)
            ? (IEnumerable<Zombie>)WaveManager.Instance.ActiveZombies
            : FindObjectsByType<Zombie>();

        List<Zombie> hitList = new List<Zombie>();
        foreach (Zombie z in candidates)
        {
            if (z == null || z.isDead || damagedThisPass.Contains(z)) continue;
            float dx = Mathf.Abs(z.transform.position.x - impactPos.x);
            float dz = Mathf.Abs(z.transform.position.z - impactPos.z);
            if (dx <= halfExtentX && dz <= halfExtentZ)
            {
                hitList.Add(z);
                damagedThisPass.Add(z);
            }
        }

        foreach (Zombie z in hitList)
        {
            if (z != null && !z.isDead)
            {
                z.TakeDamage(B2_BOMB_DAMAGE);
            }
        }

        SpawnB2ExplosionFx(impactPos);
        AudioHelper.PlaySound(SoundEffect.TankExplosionDebris, 1.0f);
        AudioHelper.PlaySound(SoundEffect.ExplosionSubBass, 0.78f);
    }

    private static void SpawnB2ExplosionFx(Vector3 center)
    {
        float blastSizeX = B2_BOMB_HALF_EXTENT_X * 2f; // 8.0m along X
        float blastSizeZ = B2_BOMB_HALF_EXTENT_Z * 2f; // 10.0m along Z (5 blocks wide)
        CrossMapVisualizer.SpawnExplosionLightAndGlow(center, Mathf.Max(blastSizeX, blastSizeZ) * 1.15f, false, false);
        CrossMapVisualizer.RegisterExplosionFlash(center, 18f, 0.50f, 1.05f);

        GameObject blastZone = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blastZone.name = "B2_BombBlastZone_5x4";
        blastZone.transform.position = new Vector3(center.x, 0.12f, center.z);
        blastZone.transform.localScale = new Vector3(blastSizeX, 0.10f, blastSizeZ);
        Collider c1 = blastZone.GetComponent<Collider>();
        if (c1 != null) Destroy(c1);
        Renderer r1 = blastZone.GetComponent<Renderer>();
        if (r1 != null)
        {
            r1.sharedMaterial = CrossMapVisualizer.CreateEmissiveExplosionMaterial(
                new Color(1.0f, 0.38f, 0.10f, 0.78f), false, null, 3155);
            r1.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r1.receiveShadows = false;
        }
        Destroy(blastZone, 0.45f);

        GameObject fireball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        fireball.name = "B2_BombFireball";
        fireball.transform.position = new Vector3(center.x, 0.85f, center.z);
        float fbScale = ((blastSizeX + blastSizeZ) * 0.5f) * 0.56f;
        fireball.transform.localScale = new Vector3(fbScale, fbScale, fbScale);
        Collider c2 = fireball.GetComponent<Collider>();
        if (c2 != null) Destroy(c2);
        Renderer r2 = fireball.GetComponent<Renderer>();
        if (r2 != null)
        {
            r2.sharedMaterial = CrossMapVisualizer.CreateEmissiveExplosionMaterial(
                new Color(1.0f, 0.82f, 0.32f, 0.94f), true, null, 3161);
            r2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r2.receiveShadows = false;
        }
        Destroy(fireball, 0.36f);
    }

    private static GameObject BuildB2BomberInstance()
    {
        GameObject root = new GameObject("B2Bomber_StrikeUnit");
        GameObject fbxPrefab = Resources.Load<GameObject>("Models/B2") ?? Resources.Load<GameObject>("Models/Fighter_Plane");

        if (fbxPrefab != null)
        {
            GameObject pivot = new GameObject("B2_AlignPivot");
            pivot.transform.SetParent(root.transform, false);

            GameObject modelInstance = Instantiate(fbxPrefab, pivot.transform);
            modelInstance.name = "B2Bomber_FBX";
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = fbxPrefab.transform.localRotation;
            modelInstance.transform.localScale = fbxPrefab.transform.localScale;

            // Strip the co-packaged Fighter ("Sketchfab_model.001") and deployed landing gear so only flying B-2 renders
            Transform fighterRoot = modelInstance.transform.Find("Sketchfab_model.001");
            if (fighterRoot != null)
            {
                DestroyImmediate(fighterRoot.gameObject);
            }
            foreach (Transform t in modelInstance.GetComponentsInChildren<Transform>(true))
            {
                if (t == null) continue;
                string n = t.name;
                if (n.EndsWith(".001") || n.StartsWith("LGFront") || n.IndexOf("LandingOn", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    DestroyImmediate(t.gameObject);
                }
            }

            UnitBlockVisualizer.StripEmbeddedSceneComponents(modelInstance);

            Renderer[] renderers = modelInstance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                ApplyB2Materials(renderers);
                OrientAndScaleB2(pivot, modelInstance, B2_WINGSPAN, out Vector3 leftWingTipLocal, out Vector3 rightWingTipLocal);
                AttachWingtipWindTrails(root, leftWingTipLocal, rightWingTipLocal, startWidth: 0.44f, trailTime: 1.05f);
                return root;
            }
        }

        BuildProceduralB2Fallback(root);
        AttachWingtipWindTrails(root, new Vector3(-B2_WINGSPAN * 0.49f, 0f, -2.8f), new Vector3(B2_WINGSPAN * 0.49f, 0f, -2.8f), startWidth: 0.44f, trailTime: 1.05f);
        return root;
    }

    private static void ApplyB2Materials(Renderer[] renderers)
    {
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ??
                           Shader.Find("Standard") ??
                           Shader.Find("Universal Render Pipeline/Unlit");

        foreach (var r in renderers)
        {
            if (r == null || r.sharedMaterials == null) continue;

            string objLower = r.gameObject.name.ToLowerInvariant();
            bool isCanopy = objLower.Contains("canopy");
            Color partColor = isCanopy
                ? new Color(0.20f, 0.27f, 0.34f)
                : new Color(0.11f, 0.12f, 0.15f);

            Material[] mats = r.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                Material mat = (litShader != null) ? new Material(litShader) : mats[i];
                if (mat != null)
                {
                    mat.color = partColor;
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", partColor);
                    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", isCanopy ? 0.75f : 0.35f);
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", isCanopy ? 0.85f : 0.32f);
                }
                mats[i] = mat;
            }
            r.materials = mats;
        }
    }

    private static void OrientAndScaleB2(GameObject pivot, GameObject modelInstance, float targetWingspan,
        out Vector3 leftWingTipLocal, out Vector3 rightWingTipLocal)
    {
        leftWingTipLocal = new Vector3(-targetWingspan * 0.49f, 0f, -1.6f);
        rightWingTipLocal = new Vector3(targetWingspan * 0.49f, 0f, -1.6f);

        if (!UnitBlockVisualizer.TryCalculateExactWorldBounds(modelInstance, out Bounds bounds)) return;

        // 1. Align widest horizontal axis (flying-wing span) along X
        if (bounds.size.x < bounds.size.z)
        {
            modelInstance.transform.Rotate(0f, 90f, 0f, Space.World);
            UnitBlockVisualizer.TryCalculateExactWorldBounds(modelInstance, out bounds);
        }

        // 2. Ensure B-2 nose faces +Z: cockpit canopy sits forward of bounds center, while swept wingtips sit aft
        bool orientedFromCanopy = false;
        foreach (var mf in modelInstance.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf == null || mf.sharedMesh == null) continue;
            if (mf.gameObject.name.IndexOf("canopy", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Vector3 canopyCenter = mf.transform.TransformPoint(mf.sharedMesh.bounds.center);
                if (canopyCenter.z < bounds.center.z)
                {
                    modelInstance.transform.Rotate(0f, 180f, 0f, Space.World);
                    UnitBlockVisualizer.TryCalculateExactWorldBounds(modelInstance, out bounds);
                }
                orientedFromCanopy = true;
                break;
            }
        }

        if (!orientedFromCanopy && TrySampleWingtipAverage(modelInstance, bounds, 0.35f, out _, out float wingAvgZ))
        {
            if (wingAvgZ > bounds.center.z)
            {
                modelInstance.transform.Rotate(0f, 180f, 0f, Space.World);
                UnitBlockVisualizer.TryCalculateExactWorldBounds(modelInstance, out bounds);
            }
        }

        // 3. Compute swept outer wingtips and uniformly scale to B2_WINGSPAN
        bool hasTips = TrySampleWingtipAverage(modelInstance, bounds, 0.44f, out float tipWorldY, out float tipWorldZ);

        modelInstance.transform.position += (pivot.transform.position - bounds.center);
        float wingspan = Mathf.Max(0.01f, bounds.size.x);
        float uniformScale = targetWingspan / wingspan;
        pivot.transform.localScale = Vector3.one * uniformScale;

        float tipLocalY = hasTips ? (tipWorldY - bounds.center.y) * uniformScale : 0f;
        float tipLocalZ = hasTips ? (tipWorldZ - bounds.center.z) * uniformScale : -bounds.size.z * 0.25f * uniformScale;
        leftWingTipLocal = new Vector3(-targetWingspan * 0.49f, tipLocalY, tipLocalZ);
        rightWingTipLocal = new Vector3(targetWingspan * 0.49f, tipLocalY, tipLocalZ);
    }

    private static void BuildProceduralB2Fallback(GameObject parent)
    {
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material stealthMat = new Material(litShader) { color = new Color(0.11f, 0.12f, 0.15f) };
        float s = B2_WINGSPAN / 10.5f;

        var centerBody = GameObject.CreatePrimitive(PrimitiveType.Cube);
        centerBody.transform.SetParent(parent.transform, false);
        centerBody.transform.localScale = new Vector3(2.8f * s, 0.55f * s, 4.4f * s);
        Destroy(centerBody.GetComponent<Collider>());
        centerBody.GetComponent<Renderer>().sharedMaterial = stealthMat;

        var leftWing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftWing.transform.SetParent(parent.transform, false);
        leftWing.transform.localPosition = new Vector3(-2.8f * s, 0f, -0.7f * s);
        leftWing.transform.localRotation = Quaternion.Euler(0f, 32f, 0f);
        leftWing.transform.localScale = new Vector3(5.6f * s, 0.22f * s, 2.2f * s);
        Destroy(leftWing.GetComponent<Collider>());
        leftWing.GetComponent<Renderer>().sharedMaterial = stealthMat;

        var rightWing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightWing.transform.SetParent(parent.transform, false);
        rightWing.transform.localPosition = new Vector3(2.8f * s, 0f, -0.7f * s);
        rightWing.transform.localRotation = Quaternion.Euler(0f, -32f, 0f);
        rightWing.transform.localScale = new Vector3(5.6f * s, 0.22f * s, 2.2f * s);
        Destroy(rightWing.GetComponent<Collider>());
        rightWing.GetComponent<Renderer>().sharedMaterial = stealthMat;
    }

    private GUIStyle _etaBadgeStyle;

    void OnGUI()
    {
        if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameManager.GameState.Combat) return;
        Camera cam = Camera.main;
        if (cam == null) return;

        bool showB2Eta = IsB2Flying && B2ArrivalETA > 0.05f;
        bool showFighterEta = IsFighterFlying && FighterArrivalETA > 0.05f;
        if (!showB2Eta && !showFighterEta) return;

        if (_etaBadgeStyle == null)
        {
            _etaBadgeStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _etaBadgeStyle.normal.textColor = Color.white;
        }

        if (showB2Eta)
        {
            Vector3 scr = cam.WorldToScreenPoint(ActiveB2StrikeCenter + Vector3.up * 1.6f);
            if (scr.z > 0f)
            {
                float gx = scr.x - 95f;
                float gy = Screen.height - scr.y - 26f;
                GUI.backgroundColor = new Color(0.95f, 0.38f, 0.08f, 0.92f);
                GUI.Box(new Rect(gx, gy, 190f, 26f), $"💣 B-2 INBOUND: {B2ArrivalETA:0.0}s", _etaBadgeStyle);
                GUI.backgroundColor = Color.white;
            }
        }

        if (showFighterEta)
        {
            Vector3 scr = cam.WorldToScreenPoint(ActiveFighterStrikeCenter + Vector3.up * 1.4f);
            if (scr.z > 0f)
            {
                float gx = scr.x - 90f;
                float gy = Screen.height - scr.y - 26f;
                GUI.backgroundColor = new Color(0.14f, 0.68f, 0.95f, 0.92f);
                GUI.Box(new Rect(gx, gy, 180f, 26f), $"✈️ CAS INBOUND: {FighterArrivalETA:0.0}s", _etaBadgeStyle);
                GUI.backgroundColor = Color.white;
            }
        }
    }
}
