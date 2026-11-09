using UnityEngine;

/// <summary>
/// Generates and renders the Cross-shaped battlefield environment without walls:
/// - Outer Background around the Cross: Natural terrain with small rolling hills
/// - Buildable Human Base (15x15 cells = 30m x 30m): X: -65 to -35, Z: -15 to +15
/// - Left Combat Corridor Extension (10x15 cells = 20m x 30m): X: -35 to -15, Z: -15 to +15
/// - Center Junction (15x15 cells = 30m x 30m): X: -15 to +15, Z: -15 to +15
/// - Right Head / Zombie Spawn (15x15 cells = 30m x 30m): X: +15 to +45, Z: -15 to +15
/// - North & South Arms (15x15 cells each = 30m x 30m): Unlocked after Stage 15
/// </summary>
[ExecuteAlways]
public class CrossMapVisualizer : MonoBehaviour
{
    public static CrossMapVisualizer Instance { get; private set; }

    [Header("Sector Lock Overlays (Flat Ground Indicators, No Walls)")]
    public GameObject northGate;
    public GameObject southGate;

    private GameObject rootCross;
    private GameObject gridOverlayRoot;
    public bool isGridVisible { get; private set; } = true;
    private bool gatesUnlocked = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (Application.isPlaying) { Destroy(gameObject); return; }
        }
        Instance = this;
    }

    void Start()
    {
        isGridVisible = PlayerPrefs.GetInt("LastBastion_GridVisible", 1) == 1;
        BuildCrossMapEnvironment();
        UpdateGateStatus(true);
    }

    void Update()
    {
        bool shouldUnlock = GameManager.Instance != null && GameManager.Instance.CurrentDay > 15;
        if (shouldUnlock != gatesUnlocked)
        {
            UpdateGateStatus(false);
        }

        if (!Application.isPlaying && rootCross == null)
        {
            var existing = GameObject.Find("CrossMap_Environment");
            if (existing == null)
            {
                BuildCrossMapEnvironment();
            }
            else
            {
                rootCross = existing;
                var child = rootCross.transform.Find("Grid_Overlay_Visuals");
                if (child != null) gridOverlayRoot = child.gameObject;
            }
        }
    }

    public void SetGridVisible(bool visible)
    {
        isGridVisible = visible;
        PlayerPrefs.SetInt("LastBastion_GridVisible", isGridVisible ? 1 : 0);
        PlayerPrefs.Save();

        if (gridOverlayRoot == null && rootCross != null)
        {
            var child = rootCross.transform.Find("Grid_Overlay_Visuals");
            if (child != null) gridOverlayRoot = child.gameObject;
        }

        if (gridOverlayRoot != null)
        {
            gridOverlayRoot.SetActive(isGridVisible);
        }
    }

    public void ToggleGridVisible()
    {
        SetGridVisible(!isGridVisible);
    }

    public void UpdateGateStatus(bool immediate)
    {
        gatesUnlocked = GameManager.Instance != null && GameManager.Instance.CurrentDay > 15;

        if (northGate != null)
        {
            northGate.SetActive(!gatesUnlocked);
        }
        if (southGate != null)
        {
            southGate.SetActive(!gatesUnlocked);
        }
    }

    [ContextMenu("Rebuild Cross Map Environment")]
    public void BuildCrossMapEnvironment()
    {
        // 1. Hide legacy scene objects if present
        string[] oldNames = new string[] { "Ground", "BaseZone", "ZombieSpawnZone" };
        foreach (var name in oldNames)
        {
            var oldObj = GameObject.Find(name);
            if (oldObj != null)
            {
                var rend = oldObj.GetComponent<Renderer>();
                if (rend != null) rend.enabled = false;
                var col = oldObj.GetComponent<Collider>();
                if (col != null) col.enabled = false;
                oldObj.SetActive(false);
            }
        }

        // Clean up previous generation
        var oldRoot = GameObject.Find("CrossMap_Environment");
        if (oldRoot != null)
        {
            if (Application.isPlaying) Destroy(oldRoot);
            else DestroyImmediate(oldRoot);
        }

        rootCross = new GameObject("CrossMap_Environment");
        gridOverlayRoot = new GameObject("Grid_Overlay_Visuals");
        gridOverlayRoot.transform.SetParent(rootCross.transform, false);

        // --- 0. OUTER BACKGROUND TERRAIN & SMALL HILLS (Surrounding the Cross Combat Area) ---
        BuildOuterBackgroundHills();

        // --- 1. LEFT WING INNER END: BUILDABLE HUMAN BASE (15x15 Cells = 30m x 30m, X: -65 to -35, Z: -15 to +15) ---
        CreateGroundSection("HumanBase_15x15", new Vector3(-50f, -0.05f, 0f), new Vector3(30f, 0.2f, 30f),
            new Color(0.18f, 0.28f, 0.22f), 0.2f, 0.35f);

        // Checkerboard tiles on the 15x15 Human Base (X: -65 to -35, Z: -15 to +15)
        Color baseTileAlt = new Color(0.22f, 0.33f, 0.26f);
        for (int gx = 0; gx < 15; gx++)
        {
            for (int gz = 0; gz < 15; gz++)
            {
                if ((gx + gz) % 2 == 0)
                {
                    float wx = -65f + gx * 2f + 1f;
                    float wz = -15f + gz * 2f + 1f;
                    CreateDecal($"BaseTile_{gx}_{gz}", new Vector3(wx, 0.055f, wz), new Vector3(1.92f, 0.01f, 1.92f), baseTileAlt, gridOverlayRoot.transform);
                }
            }
        }

        // Boundary line at the front edge of the 15x15 Human Base (X = -35)
        CreateDecal("BaseFrontLine", new Vector3(-35f, 0.065f, 0f), new Vector3(0.35f, 0.01f, 30f), new Color(0.3f, 0.85f, 0.5f), gridOverlayRoot.transform);

        // --- 2. LEFT WING EXTENSION CORRIDOR / FRONTLINE FORTIFICATION ZONE (10x15 Cells = 20m x 30m, X: -35 to -15, Z: -15 to +15) ---
        CreateGroundSection("LeftCorridor_10x15", new Vector3(-25f, -0.05f, 0f), new Vector3(20f, 0.2f, 30f),
            new Color(0.22f, 0.20f, 0.17f), 0.15f, 0.3f);

        // Checkerboard tiles on the 10x15 Frontline Fortification Zone (X: -35 to -15, Z: -15 to +15)
        Color frontlineTileAlt = new Color(0.27f, 0.24f, 0.20f);
        for (int gx = 15; gx < 25; gx++)
        {
            for (int gz = 0; gz < 15; gz++)
            {
                if ((gx + gz) % 2 == 0)
                {
                    float wx = -65f + gx * 2f + 1f;
                    float wz = -15f + gz * 2f + 1f;
                    CreateDecal($"FrontlineTile_{gx}_{gz}", new Vector3(wx, 0.055f, wz), new Vector3(1.92f, 0.01f, 1.92f), frontlineTileAlt, gridOverlayRoot.transform);
                }
            }
        }

        // Frontline Boundary line at the entrance to the Center Junction Crossroads (X = -15)
        CreateDecal("FrontlineCrossroadsLine", new Vector3(-15f, 0.065f, 0f), new Vector3(0.35f, 0.01f, 30f), new Color(0.95f, 0.68f, 0.2f), gridOverlayRoot.transform);

        // --- 3. CENTER JUNCTION (15x15 Cells = 30m x 30m, X: -15 to +15, Z: -15 to +15) ---
        CreateGroundSection("CenterJunction_15x15", new Vector3(0f, -0.05f, 0f), new Vector3(30f, 0.2f, 30f),
            new Color(0.22f, 0.19f, 0.16f), 0.15f, 0.3f);

        // --- 4. RIGHT WING / HEAD (15x15 Cells = 30m x 30m, X: +15 to +45, Z: -15 to +15) ---
        CreateGroundSection("RightHead_15x15", new Vector3(30f, -0.05f, 0f), new Vector3(30f, 0.2f, 30f),
            new Color(0.24f, 0.18f, 0.15f), 0.15f, 0.3f);

        // East Zombie Spawn Strip at far right of Head (X: +43 to +45, Z: -15 to +15)
        CreateDecal("EastZombieSpawnStrip", new Vector3(44f, 0.065f, 0f), new Vector3(2.0f, 0.02f, 30f),
            new Color(0.55f, 0.12f, 0.10f));

        // Subtle lane lines along the entire 55-cell (110m) horizontal corridor (X: -65 to +45)
        for (int row = 1; row < 15; row++)
        {
            float zLine = -15f + row * 2f;
            CreateDecal("LaneLine_" + row, new Vector3(-10f, 0.058f, zLine), new Vector3(110f, 0.01f, 0.08f),
                new Color(0.35f, 0.30f, 0.25f), gridOverlayRoot.transform);
        }

        // --- 5. TOP WING: NORTH ARM (15x15 Cells = 30m x 30m, X: -15 to +15, Z: +15 to +45) ---
        CreateGroundSection("NorthArm_15x15", new Vector3(0f, -0.05f, 30f), new Vector3(30f, 0.2f, 30f),
            new Color(0.23f, 0.20f, 0.17f), 0.15f, 0.25f);

        // North Zombie Spawn Strip (Z: +43 to +45)
        CreateDecal("NorthZombieSpawnStrip", new Vector3(0f, 0.065f, 44f), new Vector3(30f, 0.02f, 2.0f),
            new Color(0.55f, 0.12f, 0.10f));

        // --- 6. BOTTOM WING: SOUTH ARM (15x15 Cells = 30m x 30m, X: -15 to +15, Z: -45 to -15) ---
        CreateGroundSection("SouthArm_15x15", new Vector3(0f, -0.05f, -30f), new Vector3(30f, 0.2f, 30f),
            new Color(0.23f, 0.20f, 0.17f), 0.15f, 0.25f);

        // South Zombie Spawn Strip (Z: -45 to -43)
        CreateDecal("SouthZombieSpawnStrip", new Vector3(0f, 0.065f, -44f), new Vector3(30f, 0.02f, 2.0f),
            new Color(0.55f, 0.12f, 0.10f));

        // --- 7. FLAT LOCKED SECTOR OVERLAYS (NO WALLS!) ---
        BuildFlatLockOverlays();

        // --- 8. U-SHAPED DARKNESS SHROUD (Envelops Zombie Spawn Perimeter in Darkness) ---
        BuildDarknessShroud();

        // --- 9. BASTION DEFENSE LIGHTING (Illuminates Human Base and Frontline) ---
        BuildBastionDefenseLighting();

        // Apply initial grid visibility
        SetGridVisible(isGridVisible);
    }

    /// <summary>
    /// Builds the non-playable outer background ground and small rolling hills surrounding the Cross combat zone.
    /// Zero hills are placed inside the Cross combat zone.
    /// </summary>
    private void BuildOuterBackgroundHills()
    {
        var bgRoot = new GameObject("OuterBackground_HillTerrain");
        bgRoot.transform.SetParent(rootCross.transform, false);

        // Underlying outer landscape ground plane (slightly lower than the Cross combat platform)
        var bgGround = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bgGround.name = "OuterLandscapeGround";
        bgGround.transform.SetParent(bgRoot.transform, false);
        bgGround.transform.position = new Vector3(-10f, -0.35f, 0f);
        bgGround.transform.localScale = new Vector3(170f, 0.2f, 150f);
        DestroyColliderSafe(bgGround);
        var bgRend = bgGround.GetComponent<Renderer>();
        if (bgRend != null) bgRend.sharedMaterial = MakeMat(new Color(0.15f, 0.20f, 0.14f), 0.05f, 0.15f);

        Color hillGreen1 = new Color(0.19f, 0.29f, 0.17f);
        Color hillGreen2 = new Color(0.23f, 0.33f, 0.20f);
        Color hillEarth  = new Color(0.24f, 0.22f, 0.16f);

        // 1. Northwest Outer Quadrant (X: -65 to -18, Z: +18 to +48)
        CreateHill(bgRoot.transform, "Hill_NW_1", new Vector3(-56f, -0.3f, 25f), new Vector3(14f, 3.2f, 12f), hillGreen1);
        CreateHill(bgRoot.transform, "Hill_NW_2", new Vector3(-42f, -0.3f, 34f), new Vector3(16f, 3.8f, 14f), hillGreen2);
        CreateHill(bgRoot.transform, "Hill_NW_3", new Vector3(-28f, -0.3f, 24f), new Vector3(12f, 2.6f, 11f), hillEarth);
        CreateHill(bgRoot.transform, "Hill_NW_4", new Vector3(-52f, -0.3f, 40f), new Vector3(15f, 3.4f, 13f), hillGreen1);
        CreateHill(bgRoot.transform, "Hill_NW_5", new Vector3(-26f, -0.3f, 38f), new Vector3(13f, 3.0f, 12f), hillGreen2);

        // 2. Southwest Outer Quadrant (X: -65 to -18, Z: -48 to -18)
        CreateHill(bgRoot.transform, "Hill_SW_1", new Vector3(-56f, -0.3f, -25f), new Vector3(14f, 3.2f, 12f), hillGreen1);
        CreateHill(bgRoot.transform, "Hill_SW_2", new Vector3(-42f, -0.3f, -34f), new Vector3(16f, 3.8f, 14f), hillGreen2);
        CreateHill(bgRoot.transform, "Hill_SW_3", new Vector3(-28f, -0.3f, -24f), new Vector3(12f, 2.6f, 11f), hillEarth);
        CreateHill(bgRoot.transform, "Hill_SW_4", new Vector3(-52f, -0.3f, -40f), new Vector3(15f, 3.4f, 13f), hillGreen1);
        CreateHill(bgRoot.transform, "Hill_SW_5", new Vector3(-26f, -0.3f, -38f), new Vector3(13f, 3.0f, 12f), hillGreen2);

        // 3. Northeast Outer Quadrant (X: +18 to +48, Z: +18 to +48)
        CreateHill(bgRoot.transform, "Hill_NE_1", new Vector3(26f, -0.3f, 25f), new Vector3(13f, 2.8f, 12f), hillEarth);
        CreateHill(bgRoot.transform, "Hill_NE_2", new Vector3(38f, -0.3f, 35f), new Vector3(16f, 3.6f, 14f), hillGreen1);
        CreateHill(bgRoot.transform, "Hill_NE_3", new Vector3(25f, -0.3f, 39f), new Vector3(12f, 3.0f, 11f), hillGreen2);
        CreateHill(bgRoot.transform, "Hill_NE_4", new Vector3(40f, -0.3f, 23f), new Vector3(11f, 2.4f, 10f), hillEarth);

        // 4. Southeast Outer Quadrant (X: +18 to +48, Z: -48 to -18)
        CreateHill(bgRoot.transform, "Hill_SE_1", new Vector3(26f, -0.3f, -25f), new Vector3(13f, 2.8f, 12f), hillEarth);
        CreateHill(bgRoot.transform, "Hill_SE_2", new Vector3(38f, -0.3f, -35f), new Vector3(16f, 3.6f, 14f), hillGreen1);
        CreateHill(bgRoot.transform, "Hill_SE_3", new Vector3(25f, -0.3f, -39f), new Vector3(12f, 3.0f, 11f), hillGreen2);
        CreateHill(bgRoot.transform, "Hill_SE_4", new Vector3(40f, -0.3f, -23f), new Vector3(11f, 2.4f, 10f), hillEarth);

        // 5. Far Outer Perimeter Hills (Behind Left Base X < -68, Beyond Right Head X > +48, Beyond North/South Arms)
        CreateHill(bgRoot.transform, "Hill_FarWest_1", new Vector3(-73f, -0.3f, 8f), new Vector3(12f, 3.2f, 14f), hillGreen1);
        CreateHill(bgRoot.transform, "Hill_FarWest_2", new Vector3(-74f, -0.3f, -7f), new Vector3(13f, 3.5f, 14f), hillGreen2);
        CreateHill(bgRoot.transform, "Hill_FarEast_1", new Vector3(53f, -0.3f, 9f), new Vector3(12f, 2.8f, 13f), hillEarth);
        CreateHill(bgRoot.transform, "Hill_FarEast_2", new Vector3(53f, -0.3f, -9f), new Vector3(12f, 2.8f, 13f), hillEarth);
    }

    private void CreateHill(Transform parent, string name, Vector3 pos, Vector3 scale, Color col)
    {
        var hill = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        hill.name = name;
        hill.transform.SetParent(parent, false);
        hill.transform.position = pos;
        hill.transform.localScale = scale;
        DestroyColliderSafe(hill);

        var rend = hill.GetComponent<Renderer>();
        if (rend != null) rend.sharedMaterial = MakeMat(col, 0.05f, 0.2f);
    }

    private void BuildFlatLockOverlays()
    {
        // North Locked Overlay (flat hazard strip & dark tint on ground, no vertical wall)
        northGate = new GameObject("LockedOverlay_NorthSector");
        northGate.transform.SetParent(rootCross.transform, false);
        northGate.transform.position = new Vector3(0f, 0.07f, 30f);

        var nTint = GameObject.CreatePrimitive(PrimitiveType.Cube);
        nTint.name = "NorthLockedGroundTint";
        nTint.transform.SetParent(northGate.transform, false);
        nTint.transform.localPosition = Vector3.zero;
        nTint.transform.localScale = new Vector3(29.8f, 0.02f, 29.8f);
        nTint.GetComponent<Renderer>().material = MakeMat(new Color(0.12f, 0.08f, 0.06f));
        DestroyColliderSafe(nTint);

        var nLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
        nLine.name = "NorthLockedHazardLine";
        nLine.transform.SetParent(northGate.transform, false);
        nLine.transform.localPosition = new Vector3(0f, 0.015f, -14.7f);
        nLine.transform.localScale = new Vector3(30f, 0.02f, 0.6f);
        nLine.GetComponent<Renderer>().material = MakeMat(new Color(0.9f, 0.45f, 0.05f));
        DestroyColliderSafe(nLine);

        // South Locked Overlay (flat hazard strip & dark tint on ground, no vertical wall)
        southGate = new GameObject("LockedOverlay_SouthSector");
        southGate.transform.SetParent(rootCross.transform, false);
        southGate.transform.position = new Vector3(0f, 0.07f, -30f);

        var sTint = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sTint.name = "SouthLockedGroundTint";
        sTint.transform.SetParent(southGate.transform, false);
        sTint.transform.localPosition = Vector3.zero;
        sTint.transform.localScale = new Vector3(29.8f, 0.02f, 29.8f);
        sTint.GetComponent<Renderer>().material = MakeMat(new Color(0.12f, 0.08f, 0.06f));
        DestroyColliderSafe(sTint);

        var sLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sLine.name = "SouthLockedHazardLine";
        sLine.transform.SetParent(southGate.transform, false);
        sLine.transform.localPosition = new Vector3(0f, 0.015f, 14.7f);
        sLine.transform.localScale = new Vector3(30f, 0.02f, 0.6f);
        sLine.GetComponent<Renderer>().material = MakeMat(new Color(0.9f, 0.45f, 0.05f));
        DestroyColliderSafe(sLine);
    }

    private GameObject CreateGroundSection(string name, Vector3 pos, Vector3 scale, Color col, float metallic = 0.15f, float smoothness = 0.3f)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = name;
        obj.transform.SetParent(rootCross.transform, false);
        obj.transform.position = pos;
        obj.transform.localScale = scale;

        var rend = obj.GetComponent<Renderer>();
        if (rend != null) rend.sharedMaterial = MakeMat(col, metallic, smoothness);

        return obj;
    }

    private void CreateDecal(string name, Vector3 pos, Vector3 scale, Color col, Transform parentTransform = null)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = name;
        obj.transform.SetParent(parentTransform != null ? parentTransform : rootCross.transform, false);
        obj.transform.position = pos;
        obj.transform.localScale = scale;
        DestroyColliderSafe(obj);

        var rend = obj.GetComponent<Renderer>();
        if (rend != null) rend.sharedMaterial = MakeMat(col);
    }

    private void DestroyColliderSafe(GameObject obj)
    {
        var col = obj.GetComponent<Collider>();
        if (col != null)
        {
            if (Application.isPlaying) Destroy(col);
            else DestroyImmediate(col);
        }
    }

    private Shader GetShader()
    {
        return Shader.Find("Universal Render Pipeline/Lit") ??
               Shader.Find("Standard") ??
               Shader.Find("Universal Render Pipeline/Unlit") ??
               Shader.Find("Unlit/Color");
    }

    private Material MakeMat(Color color, float metallic = 0.1f, float smoothness = 0.3f)
    {
        var mat = new Material(GetShader()) { color = color };
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        return mat;
    }

    #region Full-Map Darkness Shroud & Bastion Defense Lighting

    private static Texture2D _darknessTexture;
    private static Mesh _darknessQuadMesh;
    private const float MAP_MIN_X = -350f;
    private const float MAP_MAX_X = 350f;
    private const float MAP_MIN_Z = -350f;
    private const float MAP_MAX_Z = 350f;

    public struct ActiveExplosionFlash
    {
        public Vector3 position;
        public float radius;
        public float startTime;
        public float duration;
        public float intensityFactor;
    }

    private static readonly System.Collections.Generic.List<ActiveExplosionFlash> _activeExplosionFlashes = new System.Collections.Generic.List<ActiveExplosionFlash>();
    private static Texture2D _radialGlowTexture = null;
    private static Shader _cachedUnlitShader = null;

    /// <summary>
    /// Registers a temporary explosion light flash so nearby darkness is dynamically reduced.
    /// </summary>
    public static void RegisterExplosionFlash(Vector3 pos, float radius, float duration, float intensityFactor = 1.0f)
    {
        _activeExplosionFlashes.RemoveAll(f => Time.time - f.startTime >= f.duration);
        _activeExplosionFlashes.Add(new ActiveExplosionFlash
        {
            position = pos,
            radius = Mathf.Max(1f, radius),
            startTime = Time.time,
            duration = Mathf.Max(0.1f, duration),
            intensityFactor = Mathf.Clamp01(intensityFactor)
        });
    }

    /// <summary>
    /// Computes static darkness factor (0.0 = full light at base, ~0.25-0.45 = dim twilight at center junction, 1.0 = pitch black in U-shaped zombie spawn arms).
    /// </summary>
    public static float GetStaticDarknessAtPosition(Vector3 pos)
    {
        float wx = pos.x;
        float wz = pos.z;
        float absZ = Mathf.Abs(wz);

        // 1. Horizontal (X-axis) darkness profile along the central corridor
        float dX = 0f;
        if (wx < -65f)
        {
            // Background Behind Base (West, X < -65): Soft twilight (~0.08 to 0.32)
            float distBehind = -65f - wx;
            dX = (distBehind <= 45f)
                ? Mathf.Lerp(0.08f, 0.32f, distBehind / 45f)
                : Mathf.Lerp(0.32f, 0.72f, Mathf.Clamp01((distBehind - 45f) / 75f));
        }
        else if (wx <= -16f)
        {
            // Human Base & Frontline Fortification (X: -65 to -16): 100% Bright
            dX = 0f;
        }
        else if (wx <= 12f)
        {
            // Center Junction / Mid-Map (X: -16 to +12): Reduced darkness (~0.0 to 0.45, ~0.25 at X=0)
            float tCenter = (wx - (-16f)) / 28f;
            dX = Mathf.SmoothStep(0f, 0.45f, tCenter);
        }
        else if (wx < 23f)
        {
            // Entrance into East Zombie Arm (X: +12 to +23): Smooth ramp into pitch-black darkness
            float tEast = (wx - 12f) / 11f;
            dX = Mathf.SmoothStep(0.45f, 1.0f, tEast);
        }
        else
        {
            // Deep East Zombie Arm & beyond (X >= +23): 100% Pitch Black
            dX = 1.0f;
        }

        // 2. Vertical / Flank (Z-axis) darkness profile outside the central corridor (|Z| > 13)
        float dZ = 0f;
        if (absZ > 13f)
        {
            // Base Flank Twilight (NW & SW hills beside the base)
            float distSideBase = Mathf.Max(0f, absZ - 14.5f);
            float dSideBase = (distSideBase <= 45f)
                ? Mathf.Lerp(0.06f, 0.35f, distSideBase / 45f)
                : Mathf.Lerp(0.35f, 0.75f, Mathf.Clamp01((distSideBase - 45f) / 75f));

            // North & South Zombie Arms (|Z| ramping from 13 to 22 into 100% pitch black)
            float tArmZ = Mathf.Clamp01((absZ - 13f) / 9.5f);
            float dSideZombie = Mathf.SmoothStep(0f, 1.0f, tArmZ);

            if (wx <= -18f)
            {
                dZ = (absZ > 14.5f) ? dSideBase : 0f;
            }
            else if (wx >= -5f)
            {
                dZ = dSideZombie;
            }
            else
            {
                float tBlend = Mathf.SmoothStep(0f, 1f, (wx - (-18f)) / 13f);
                float baseVal = (absZ > 14.5f) ? dSideBase : 0f;
                dZ = Mathf.Lerp(baseVal, dSideZombie, tBlend);
            }
        }

        return Mathf.Max(dX, dZ);
    }

    /// <summary>
    /// Computes effective runtime darkness at a world position, accounting for dynamic explosion light flashes.
    /// </summary>
    public static float GetDarknessAtPosition(Vector3 pos)
    {
        float baseDarkness = GetStaticDarknessAtPosition(pos);
        if (baseDarkness <= 0.01f || _activeExplosionFlashes.Count == 0)
        {
            return baseDarkness;
        }

        float now = Time.time;
        float maxLightCleared = 0f;

        for (int i = _activeExplosionFlashes.Count - 1; i >= 0; i--)
        {
            var flash = _activeExplosionFlashes[i];
            float elapsed = now - flash.startTime;
            if (elapsed >= flash.duration)
            {
                _activeExplosionFlashes.RemoveAt(i);
                continue;
            }

            float dx = pos.x - flash.position.x;
            float dz = pos.z - flash.position.z;
            float distSq = dx * dx + dz * dz;
            if (distSq < flash.radius * flash.radius)
            {
                float dist = Mathf.Sqrt(distSq);
                float spatialFalloff = 1f - Mathf.SmoothStep(0f, 1f, dist / flash.radius);
                float timeNorm = Mathf.Clamp01(elapsed / flash.duration);
                float timeFalloff = 1f - timeNorm * timeNorm;
                float clearAmount = spatialFalloff * timeFalloff * flash.intensityFactor;
                if (clearAmount > maxLightCleared)
                {
                    maxLightCleared = clearAmount;
                }
            }
        }

        return Mathf.Clamp01(baseDarkness * (1f - maxLightCleared));
    }

    public static Texture2D GetOrCreateDarknessTexture(float minX, float maxX, float minZ, float maxZ)
    {
        if (_darknessTexture != null) return _darknessTexture;

        int width = 512;
        int height = 512;
        _darknessTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        _darknessTexture.name = "Tex_DarknessShroud_FullMap";
        _darknessTexture.wrapMode = TextureWrapMode.Clamp;
        _darknessTexture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[width * height];

        for (int py = 0; py < height; py++)
        {
            float v = (float)py / (height - 1);
            float wz = Mathf.Lerp(minZ, maxZ, v);

            for (int px = 0; px < width; px++)
            {
                float u = (float)px / (width - 1);
                float wx = Mathf.Lerp(minX, maxX, u);

                float d = GetStaticDarknessAtPosition(new Vector3(wx, 0f, wz));
                float alpha = Mathf.Clamp01(d * 0.985f);

                pixels[py * width + px] = new Color(0.015f, 0.02f, 0.03f, alpha);
            }
        }

        _darknessTexture.SetPixels(pixels);
        _darknessTexture.Apply(false, true);
        return _darknessTexture;
    }

    private static Mesh GetOrCreateProceduralQuadMesh(float minX, float maxX, float minZ, float maxZ)
    {
        if (_darknessQuadMesh != null) return _darknessQuadMesh;

        _darknessQuadMesh = new Mesh();
        _darknessQuadMesh.name = "Mesh_Darkness_FullMap_700m";

        _darknessQuadMesh.vertices = new Vector3[]
        {
            new Vector3(minX, 0f, minZ),
            new Vector3(maxX, 0f, minZ),
            new Vector3(minX, 0f, maxZ),
            new Vector3(maxX, 0f, maxZ)
        };

        _darknessQuadMesh.uv = new Vector2[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };

        _darknessQuadMesh.triangles = new int[]
        {
            0, 2, 1,
            2, 3, 1
        };

        _darknessQuadMesh.normals = new Vector3[]
        {
            Vector3.up,
            Vector3.up,
            Vector3.up,
            Vector3.up
        };

        _darknessQuadMesh.RecalculateBounds();
        return _darknessQuadMesh;
    }

    private Material CreateDarknessMaterial(Texture2D tex)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ??
                    Shader.Find("Unlit/Transparent") ??
                    Shader.Find("Universal Render Pipeline/Lit") ??
                    Shader.Find("Standard");

        Material mat = new Material(sh);
        mat.name = "Mat_DarknessShroud_FullMap";

        // URP Transparent setup
        mat.SetFloat("_Surface", 1f); // Transparent
        mat.SetFloat("_Blend", 0f);   // Alpha blend
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.SetFloat("_Cull", 0f); // Double-sided
        mat.renderQueue = 3100;    // Render after standard opaque geometry
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

        // Standard shader fallback setup
        mat.SetFloat("_Mode", 3f); // Transparent
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");

        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        mat.color = Color.white;

        return mat;
    }

    private GameObject CreateDarknessPlane(string name, float yPos, Material mat, Transform parent, Mesh mesh)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(0f, yPos, 0f);

        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        return go;
    }

    private void BuildDarknessShroud()
    {
        // Clear cached statics so new values take immediate effect
        _darknessTexture = null;
        _darknessQuadMesh = null;

        var darknessRoot = new GameObject("Darkness_Atmosphere_FullMap");
        darknessRoot.transform.SetParent(rootCross.transform, false);

        Texture2D tex = GetOrCreateDarknessTexture(MAP_MIN_X, MAP_MAX_X, MAP_MIN_Z, MAP_MAX_Z);
        Material darknessMat = CreateDarknessMaterial(tex);
        Mesh quadMesh = GetOrCreateProceduralQuadMesh(MAP_MIN_X, MAP_MAX_X, MAP_MIN_Z, MAP_MAX_Z);

        // 1. Ground Shadow Tint Plane (Y = 0.062f, right above terrain cubes)
        CreateDarknessPlane("Darkness_GroundShadow_Plane", 0.062f, darknessMat, darknessRoot.transform, quadMesh);

        // 2. Overhead Darkness Shroud Plane (Y = 2.35f, above zombie and soldier heads)
        var overheadShroud = CreateDarknessPlane("Darkness_OverheadShroud_Plane", 2.35f, darknessMat, darknessRoot.transform, quadMesh);
        overheadShroud.AddComponent<DarknessShroudAnimator>();
    }

    private void BuildBastionDefenseLighting()
    {
        var lightsRoot = new GameObject("Bastion_Defense_Lighting");
        lightsRoot.transform.SetParent(rootCross.transform, false);

        // 1. Main Base Central Floodlight (warm incandescent beacon of humanity)
        var l1Obj = new GameObject("Floodlight_BaseMain");
        l1Obj.transform.SetParent(lightsRoot.transform, false);
        l1Obj.transform.position = new Vector3(-50f, 11.0f, 0f);
        var l1 = l1Obj.AddComponent<Light>();
        l1.type = LightType.Point;
        l1.color = new Color(1.0f, 0.94f, 0.85f);
        l1.intensity = 4.5f;
        l1.range = 48f;

        // 2. Base Rear Light
        var l2Obj = new GameObject("Floodlight_BaseRear");
        l2Obj.transform.SetParent(lightsRoot.transform, false);
        l2Obj.transform.position = new Vector3(-62f, 9.5f, 0f);
        var l2 = l2Obj.AddComponent<Light>();
        l2.type = LightType.Point;
        l2.color = new Color(1.0f, 0.96f, 0.90f);
        l2.intensity = 3.0f;
        l2.range = 35f;

        // 3. Frontline Corridor Light (cool defense searchlight)
        var l3Obj = new GameObject("Floodlight_Frontline");
        l3Obj.transform.SetParent(lightsRoot.transform, false);
        l3Obj.transform.position = new Vector3(-30f, 9.0f, 0f);
        var l3 = l3Obj.AddComponent<Light>();
        l3.type = LightType.Point;
        l3.color = new Color(0.92f, 0.96f, 1.0f);
        l3.intensity = 3.8f;
        l3.range = 38f;

        // 4. Threshold Watch Light (Guards the boundary between frontline and crossroads)
        var l4Obj = new GameObject("Floodlight_Threshold");
        l4Obj.transform.SetParent(lightsRoot.transform, false);
        l4Obj.transform.position = new Vector3(-14f, 8.5f, 0f);
        var l4 = l4Obj.AddComponent<Light>();
        l4.type = LightType.Point;
        l4.color = new Color(1.0f, 0.88f, 0.70f);
        l4.intensity = 2.6f;
        l4.range = 30f;

        // 4b. Center Junction Ambient Light (Softly illuminates the center crossroads so mid-map is visible)
        var lCenterObj = new GameObject("Floodlight_CenterJunction");
        lCenterObj.transform.SetParent(lightsRoot.transform, false);
        lCenterObj.transform.position = new Vector3(0f, 10.5f, 0f);
        var lCenter = lCenterObj.AddComponent<Light>();
        lCenter.type = LightType.Point;
        lCenter.color = new Color(0.92f, 0.85f, 0.75f);
        lCenter.intensity = 2.2f;
        lCenter.range = 34f;

        // 5. Flank Ambient Lights (subtly illuminates the background ground and hills beside the base)
        var lNorthObj = new GameObject("Floodlight_FlankNorth");
        lNorthObj.transform.SetParent(lightsRoot.transform, false);
        lNorthObj.transform.position = new Vector3(-45f, 8.5f, 22f);
        var lNorth = lNorthObj.AddComponent<Light>();
        lNorth.type = LightType.Point;
        lNorth.color = new Color(0.70f, 0.80f, 0.95f);
        lNorth.intensity = 1.8f;
        lNorth.range = 35f;

        var lSouthObj = new GameObject("Floodlight_FlankSouth");
        lSouthObj.transform.SetParent(lightsRoot.transform, false);
        lSouthObj.transform.position = new Vector3(-45f, 8.5f, -22f);
        var lSouth = lSouthObj.AddComponent<Light>();
        lSouth.type = LightType.Point;
        lSouth.color = new Color(0.70f, 0.80f, 0.95f);
        lSouth.intensity = 1.8f;
        lSouth.range = 35f;

        // 6. Tune global directional light to gentle moonlit night
        var lights = FindObjectsByType<Light>();
        foreach (var l in lights)
        {
            if (l.type == LightType.Directional)
            {
                l.intensity = 0.24f;
                l.color = new Color(0.85f, 0.90f, 0.98f);
            }
        }

        // 7. Camera night sky background
        var mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = new Color(0.06f, 0.08f, 0.10f);
        }
    }

    #region Dynamic Explosion Light & Glow System

    private static Texture2D GetOrCreateRadialGlowTexture()
    {
        if (_radialGlowTexture != null) return _radialGlowTexture;

        int res = 128;
        _radialGlowTexture = new Texture2D(res, res, TextureFormat.RGBA32, false);
        _radialGlowTexture.name = "Tex_ExplosionRadialGlow";
        _radialGlowTexture.wrapMode = TextureWrapMode.Clamp;
        _radialGlowTexture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[res * res];
        float half = (res - 1) * 0.5f;

        for (int y = 0; y < res; y++)
        {
            float dy = (y - half) / half;
            for (int x = 0; x < res; x++)
            {
                float dx = (x - half) / half;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float falloff = Mathf.Clamp01(1f - r);
                // Smooth cubic falloff for natural light bloom
                float alpha = falloff * falloff * (3f - 2f * falloff);
                pixels[y * res + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        _radialGlowTexture.SetPixels(pixels);
        _radialGlowTexture.Apply(false, true);
        return _radialGlowTexture;
    }

    /// <summary>
    /// Creates an Unlit Transparent/Additive material that renders above the darkness shroud (renderQueue >= 3150).
    /// </summary>
    public static Material CreateEmissiveExplosionMaterial(Color col, bool additive = false, Texture2D tex = null, int queue = 3155)
    {
        if (_cachedUnlitShader == null)
        {
            _cachedUnlitShader = Shader.Find("Universal Render Pipeline/Unlit") ??
                                 Shader.Find("Unlit/Transparent") ??
                                 Shader.Find("Universal Render Pipeline/Lit") ??
                                 Shader.Find("Standard");
        }

        Material mat = new Material(_cachedUnlitShader);
        mat.name = additive ? "Mat_ExplosionAdditiveGlow" : "Mat_ExplosionUnlitCore";

        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)(additive
            ? UnityEngine.Rendering.BlendMode.One
            : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        mat.SetInt("_ZWrite", 0);
        mat.SetFloat("_Cull", 0f);
        mat.renderQueue = queue;
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

        mat.SetFloat("_Mode", 3f);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");

        if (tex != null)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        }

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
        mat.color = col;

        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", col * 2.5f);
        }

        return mat;
    }

    /// <summary>
    /// Spawns a high-intensity dynamic 3D Point Light + Additive Ground/Overhead Glow Halo for any explosion.
    /// Illuminates the battlefield and pierces through the darkness shroud.
    /// </summary>
    public static void SpawnExplosionLightAndGlow(Vector3 center, float blastSize, bool isNuclearICBM = false, bool isTankShell = false)
    {
        float duration = isNuclearICBM ? 2.4f : (isTankShell ? 0.85f : 0.68f);
        float lightIntensity = isNuclearICBM ? 48f : (isTankShell ? 22f : 16f);
        float lightRange = isNuclearICBM ? 65f : (isTankShell ? 28f : 22f);
        float glowDiameter = isNuclearICBM ? blastSize * 2.6f : blastSize * 2.4f;

        Color startColor = isNuclearICBM
            ? new Color(1.0f, 0.96f, 0.82f, 0.95f)
            : new Color(1.0f, 0.85f, 0.40f, 0.85f);
        Color endColor = isNuclearICBM
            ? new Color(1.0f, 0.35f, 0.06f, 0f)
            : new Color(1.0f, 0.38f, 0.08f, 0f);

        // Register dynamic darkness clearing around the explosion
        RegisterExplosionFlash(center, lightRange * 0.75f, duration, isNuclearICBM ? 1.0f : 0.90f);

        GameObject fxRoot = new GameObject(isNuclearICBM ? "ExplosionLight_ICBM" : "ExplosionLight_Blast");
        fxRoot.transform.position = new Vector3(center.x, 0f, center.z);

        // 1. Dynamic 3D Point Light (illuminates terrain, buildings, soldiers, and zombies)
        GameObject lightObj = new GameObject("Blast_PointLight");
        lightObj.transform.SetParent(fxRoot.transform, false);
        lightObj.transform.localPosition = new Vector3(0f, isNuclearICBM ? 4.5f : 2.5f, 0f);
        Light ptLight = lightObj.AddComponent<Light>();
        ptLight.type = LightType.Point;
        ptLight.color = new Color(startColor.r, startColor.g, startColor.b, 1f);
        ptLight.intensity = lightIntensity;
        ptLight.range = lightRange;
        ptLight.shadows = LightShadows.None;

        // 2. Additive Ground & Shroud Light Glow Discs (renders above darkness shroud at queue 3150)
        Texture2D radialTex = GetOrCreateRadialGlowTexture();
        Material glowMat = CreateEmissiveExplosionMaterial(startColor, true, radialTex, 3150);

        // Ground glow disc
        GameObject groundGlow = GameObject.CreatePrimitive(PrimitiveType.Quad);
        groundGlow.name = "Blast_GroundLightGlow";
        groundGlow.transform.SetParent(fxRoot.transform, false);
        groundGlow.transform.localPosition = new Vector3(0f, 0.14f, 0f);
        groundGlow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        groundGlow.transform.localScale = new Vector3(glowDiameter, glowDiameter, 1f);
        Collider c1 = groundGlow.GetComponent<Collider>();
        if (c1 != null) Destroy(c1);
        Renderer r1 = groundGlow.GetComponent<Renderer>();
        if (r1 != null)
        {
            r1.sharedMaterial = glowMat;
            r1.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r1.receiveShadows = false;
        }

        // Overhead shroud-piercing glow disc (above Y=2.35 shroud so darkness visibly lights up)
        GameObject overheadGlow = GameObject.CreatePrimitive(PrimitiveType.Quad);
        overheadGlow.name = "Blast_OverheadLightGlow";
        overheadGlow.transform.SetParent(fxRoot.transform, false);
        overheadGlow.transform.localPosition = new Vector3(0f, 2.42f, 0f);
        overheadGlow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        overheadGlow.transform.localScale = new Vector3(glowDiameter * 1.15f, glowDiameter * 1.15f, 1f);
        Collider c2 = overheadGlow.GetComponent<Collider>();
        if (c2 != null) Destroy(c2);
        Renderer r2 = overheadGlow.GetComponent<Renderer>();
        if (r2 != null)
        {
            r2.sharedMaterial = glowMat;
            r2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r2.receiveShadows = false;
        }

        var anim = fxRoot.AddComponent<ExplosionFlashAnimator>();
        anim.Initialize(ptLight, glowMat, groundGlow.transform, overheadGlow.transform,
            lightIntensity, lightRange, glowDiameter, duration, startColor, endColor);
    }

    #endregion

    #endregion
}

/// <summary>
/// Animates the dynamic point light intensity and additive glow discs for explosions.
/// </summary>
public class ExplosionFlashAnimator : MonoBehaviour
{
    private Light _light;
    private Material _glowMat;
    private Transform _groundDisc;
    private Transform _overheadDisc;
    private float _initialIntensity;
    private float _initialRange;
    private float _initialDiameter;
    private float _duration;
    private float _elapsed;
    private Color _startColor;
    private Color _endColor;

    public void Initialize(Light lightComp, Material glowMat, Transform groundDisc, Transform overheadDisc,
        float intensity, float range, float diameter, float duration, Color startCol, Color endCol)
    {
        _light = lightComp;
        _glowMat = glowMat;
        _groundDisc = groundDisc;
        _overheadDisc = overheadDisc;
        _initialIntensity = intensity;
        _initialRange = range;
        _initialDiameter = diameter;
        _duration = Mathf.Max(0.1f, duration);
        _elapsed = 0f;
        _startColor = startCol;
        _endColor = endCol;
    }

    void Update()
    {
        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _duration);

        // Fast bright attack, smooth exponential decay
        float fade = (1f - t) * (1f - t);
        Color curCol = Color.Lerp(_startColor, _endColor, Mathf.Sqrt(t));
        curCol.a = _startColor.a * fade;

        if (_light != null)
        {
            _light.intensity = _initialIntensity * fade;
            _light.range = _initialRange * Mathf.Lerp(1.0f, 1.25f, t);
            _light.color = new Color(curCol.r, curCol.g, curCol.b, 1f);
        }

        if (_glowMat != null)
        {
            _glowMat.color = curCol;
            if (_glowMat.HasProperty("_BaseColor")) _glowMat.SetColor("_BaseColor", curCol);
        }

        float scaleMult = Mathf.Lerp(0.85f, 1.35f, Mathf.Sqrt(t));
        if (_groundDisc != null)
        {
            float d = _initialDiameter * scaleMult;
            _groundDisc.localScale = new Vector3(d, d, 1f);
        }
        if (_overheadDisc != null)
        {
            float d = _initialDiameter * 1.15f * scaleMult;
            _overheadDisc.localScale = new Vector3(d, d, 1f);
        }

        if (t >= 1f)
        {
            if (_glowMat != null) Destroy(_glowMat);
            Destroy(gameObject);
        }
    }
}

/// <summary>
/// Subtle animated breathing pulse for the overhead darkness shroud
/// </summary>
public class DarknessShroudAnimator : MonoBehaviour
{
    private Material _mat;
    private Color _baseColor;

    void Start()
    {
        var rend = GetComponent<Renderer>();
        if (rend != null)
        {
            _mat = rend.material;
            _baseColor = _mat.color;
        }
    }

    void Update()
    {
        if (_mat != null)
        {
            float pulse = 0.94f + Mathf.Sin(Time.time * 0.8f) * 0.04f;
            Color c = _baseColor;
            c.a = Mathf.Clamp01(_baseColor.a * pulse);
            _mat.color = c;
            if (_mat.HasProperty("_BaseColor")) _mat.SetColor("_BaseColor", c);
        }
    }
}
