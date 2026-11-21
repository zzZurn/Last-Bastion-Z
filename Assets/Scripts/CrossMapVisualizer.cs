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
    private bool _envVerified = false;
    private bool _isApocalypseThemeActive = false;
    private const string ENV_VERSION_TAG = "EnvVersion_11_FixCityCamClipping";
    private readonly System.Collections.Generic.List<UnityEngine.Object> _generatedAssets = new System.Collections.Generic.List<UnityEngine.Object>();

    public static bool IsApocalypseCityTheme()
    {
        return GameManager.Instance != null && GameManager.Instance.CurrentDay > 30;
    }

    private T TrackGeneratedAsset<T>(T asset) where T : UnityEngine.Object
    {
        if (asset != null) _generatedAssets.Add(asset);
        return asset;
    }

    private void ClearGeneratedAssets()
    {
        for (int i = 0; i < _generatedAssets.Count; i++)
        {
            var asset = _generatedAssets[i];
            if (asset == null) continue;
            if (Application.isPlaying) Destroy(asset);
            else DestroyImmediate(asset);
        }
        _generatedAssets.Clear();
    }

    void OnDestroy()
    {
        ClearGeneratedAssets();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (Application.isPlaying) { Destroy(gameObject); return; }
        }
        Instance = this;
        _envVerified = false;
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

        bool shouldBeApocalypse = IsApocalypseCityTheme();
        if (shouldBeApocalypse != _isApocalypseThemeActive)
        {
            _envVerified = false;
        }

        if (!_envVerified || rootCross == null)
        {
            if (rootCross == null)
            {
                rootCross = GameObject.Find("CrossMap_Environment");
            }

            if (rootCross == null || rootCross.transform.Find(ENV_VERSION_TAG) == null || shouldBeApocalypse != _isApocalypseThemeActive)
            {
                BuildCrossMapEnvironment();
                UpdateGateStatus(true);
            }
            else if (gridOverlayRoot == null)
            {
                var child = rootCross.transform.Find("Grid_Overlay_Visuals");
                if (child != null) gridOverlayRoot = child.gameObject;
            }
            _envVerified = (rootCross != null);
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
        ClearGeneratedAssets();
        _isApocalypseThemeActive = IsApocalypseCityTheme();

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
        var versionMarker = new GameObject(ENV_VERSION_TAG);
        versionMarker.transform.SetParent(rootCross.transform, false);

        gridOverlayRoot = new GameObject("Grid_Overlay_Visuals");
        gridOverlayRoot.transform.SetParent(rootCross.transform, false);

        // --- 0. UNIFIED SEAMLESS BIOME GROUND & OUTER HILLS ---
        BuildOuterBackgroundHills();

        // --- 0b. THEME SCENERY: STAGE 1-30 FOREST vs STAGE 31+ POST-APOCALYPSE RUINED CITY ---
        if (_isApocalypseThemeActive)
        {
            BuildOuterApocalypseCity();
        }
        else
        {
            BuildOuterNatureForest();
        }

        // --- 1. BUILDABLE HUMAN BASE GRID TILES (15x15 Cells = 30m x 30m, X: -65 to -35, Z: -15 to +15) ---
        var baseEvenRects = new System.Collections.Generic.List<Vector4>(113);
        var baseOddRects  = new System.Collections.Generic.List<Vector4>(112);
        for (int gx = 0; gx < 15; gx++)
        {
            for (int gz = 0; gz < 15; gz++)
            {
                float wx = -65f + gx * 2f + 1f;
                float wz = -15f + gz * 2f + 1f;
                Vector4 rect = new Vector4(wx, wz, 1.92f, 1.92f);
                if ((gx + gz) % 2 == 0) baseEvenRects.Add(rect);
                else baseOddRects.Add(rect);
            }
        }
        Color baseEvenCol = _isApocalypseThemeActive ? new Color(0.38f, 0.31f, 0.23f) : new Color(0.22f, 0.33f, 0.26f);
        Color baseOddCol  = _isApocalypseThemeActive ? new Color(0.32f, 0.26f, 0.19f) : new Color(0.18f, 0.28f, 0.22f);
        CreateCombinedFlatRectsMesh("Grid_BaseTiles_Even", baseEvenRects, 0.056f, baseEvenCol, gridOverlayRoot.transform);
        CreateCombinedFlatRectsMesh("Grid_BaseTiles_Odd",  baseOddRects,  0.056f, baseOddCol,  gridOverlayRoot.transform);

        // Boundary line at the front edge of the 15x15 Human Base (X = -35)
        CreateDecal("BaseFrontLine", new Vector3(-35f, 0.065f, 0f), new Vector3(0.35f, 0.01f, 30f),
            _isApocalypseThemeActive ? new Color(0.95f, 0.72f, 0.30f) : new Color(0.3f, 0.85f, 0.5f), gridOverlayRoot.transform);

        // --- 2. FRONTLINE FORTIFICATION GRID TILES (10x15 Cells = 20m x 30m, X: -35 to -15, Z: -15 to +15) ---
        var frontEvenRects = new System.Collections.Generic.List<Vector4>(75);
        var frontOddRects  = new System.Collections.Generic.List<Vector4>(75);
        for (int gx = 15; gx < 25; gx++)
        {
            for (int gz = 0; gz < 15; gz++)
            {
                float wx = -65f + gx * 2f + 1f;
                float wz = -15f + gz * 2f + 1f;
                Vector4 rect = new Vector4(wx, wz, 1.92f, 1.92f);
                if ((gx + gz) % 2 == 0) frontEvenRects.Add(rect);
                else frontOddRects.Add(rect);
            }
        }
        Color frontEvenCol = _isApocalypseThemeActive ? new Color(0.33f, 0.27f, 0.22f) : new Color(0.27f, 0.24f, 0.20f);
        Color frontOddCol  = _isApocalypseThemeActive ? new Color(0.28f, 0.23f, 0.19f) : new Color(0.22f, 0.20f, 0.17f);
        CreateCombinedFlatRectsMesh("Grid_FrontlineTiles_Even", frontEvenRects, 0.056f, frontEvenCol, gridOverlayRoot.transform);
        CreateCombinedFlatRectsMesh("Grid_FrontlineTiles_Odd",  frontOddRects,  0.056f, frontOddCol,  gridOverlayRoot.transform);

        // Frontline Boundary line at the entrance to the Center Junction Crossroads (X = -15)
        CreateDecal("FrontlineCrossroadsLine", new Vector3(-15f, 0.065f, 0f), new Vector3(0.35f, 0.01f, 30f), new Color(0.95f, 0.68f, 0.2f), gridOverlayRoot.transform);

        // East Zombie Spawn Strip at far right of Head (X: +43 to +45, Z: -15 to +15)
        CreateDecal("EastZombieSpawnStrip", new Vector3(44f, 0.065f, 0f), new Vector3(2.0f, 0.02f, 30f),
            new Color(0.55f, 0.12f, 0.10f));

        // Subtle lane lines along the entire 55-cell (110m) horizontal corridor (X: -65 to +45) combined into 1 mesh
        var laneLineRects = new System.Collections.Generic.List<Vector4>(14);
        for (int row = 1; row < 15; row++)
        {
            float zLine = -15f + row * 2f;
            laneLineRects.Add(new Vector4(-10f, zLine, 110f, 0.08f));
        }
        CreateCombinedFlatRectsMesh("Grid_LaneLines", laneLineRects, 0.059f, new Color(0.35f, 0.30f, 0.25f), gridOverlayRoot.transform);

        // North Zombie Spawn Strip (Z: +43 to +45)
        CreateDecal("NorthZombieSpawnStrip", new Vector3(0f, 0.065f, 44f), new Vector3(30f, 0.02f, 2.0f),
            new Color(0.55f, 0.12f, 0.10f));

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
        _envVerified = true;
    }

    /// <summary>
    /// Builds one single flat upward-facing quad mesh from a list of (centerX, centerZ, sizeX, sizeZ) rectangles.
    /// Replaces hundreds of Cube GameObjects with 1 lightweight MeshRenderer & 1 shared Material.
    /// </summary>
    private void CreateCombinedFlatRectsMesh(string name, System.Collections.Generic.List<Vector4> rects, float yPos, Color col, Transform parent)
    {
        if (rects == null || rects.Count == 0) return;

        int count = rects.Count;
        Vector3[] verts = new Vector3[count * 4];
        Vector3[] normals = new Vector3[count * 4];
        Vector2[] uvs = new Vector2[count * 4];
        int[] tris = new int[count * 6];

        for (int i = 0; i < count; i++)
        {
            Vector4 r = rects[i];
            float hx = r.z * 0.5f;
            float hz = r.w * 0.5f;
            int v = i * 4;
            int t = i * 6;

            verts[v + 0] = new Vector3(r.x - hx, yPos, r.y - hz);
            verts[v + 1] = new Vector3(r.x + hx, yPos, r.y - hz);
            verts[v + 2] = new Vector3(r.x - hx, yPos, r.y + hz);
            verts[v + 3] = new Vector3(r.x + hx, yPos, r.y + hz);

            normals[v + 0] = Vector3.up;
            normals[v + 1] = Vector3.up;
            normals[v + 2] = Vector3.up;
            normals[v + 3] = Vector3.up;

            uvs[v + 0] = new Vector2(0f, 0f);
            uvs[v + 1] = new Vector2(1f, 0f);
            uvs[v + 2] = new Vector2(0f, 1f);
            uvs[v + 3] = new Vector2(1f, 1f);

            tris[t + 0] = v + 0;
            tris[t + 1] = v + 2;
            tris[t + 2] = v + 1;
            tris[t + 3] = v + 2;
            tris[t + 4] = v + 3;
            tris[t + 5] = v + 1;
        }

        Mesh mesh = TrackGeneratedAsset(new Mesh { name = "Mesh_" + name });
        mesh.vertices = verts;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateBounds();

        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = TrackGeneratedAsset(MakeMat(col));
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = true;
    }

    private struct HillVolume
    {
        public Vector3 center;
        public Vector3 halfExtents;
    }

    private readonly System.Collections.Generic.List<HillVolume> _outerHills = new System.Collections.Generic.List<HillVolume>();
    private const float OUTER_GROUND_TOP_Y = 0.050f;
    private const float UNIFIED_GROUND_MIN_X = -140f;
    private const float UNIFIED_GROUND_MAX_X = 120f;
    private const float UNIFIED_GROUND_MIN_Z = -120f;
    private const float UNIFIED_GROUND_MAX_Z = 120f;

    /// <summary>
    /// Builds one continuous URP-Lit ground surface (top at Y=0.050f) with a procedural Perlin-noise
    /// dirt-to-grass biome texture so there are zero straight box edges between the base/battlefield and grass!
    /// </summary>
    private void BuildOuterBackgroundHills()
    {
        _outerHills.Clear();

        var bgRoot = new GameObject("OuterBackground_HillTerrain");
        bgRoot.transform.SetParent(rootCross.transform, false);

        float centerX = (UNIFIED_GROUND_MIN_X + UNIFIED_GROUND_MAX_X) * 0.5f;
        float centerZ = (UNIFIED_GROUND_MIN_Z + UNIFIED_GROUND_MAX_Z) * 0.5f;
        float sizeX = UNIFIED_GROUND_MAX_X - UNIFIED_GROUND_MIN_X;
        float sizeZ = UNIFIED_GROUND_MAX_Z - UNIFIED_GROUND_MIN_Z;

        var bgGround = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bgGround.name = "UnifiedBattlefieldAndForestGround";
        bgGround.transform.SetParent(bgRoot.transform, false);
        bgGround.transform.position = new Vector3(centerX, -0.05f, centerZ);
        bgGround.transform.localScale = new Vector3(sizeX, 0.2f, sizeZ);
        DestroyColliderSafe(bgGround);

        var bgRend = bgGround.GetComponent<Renderer>();
        if (bgRend != null)
        {
            bgRend.sharedMaterial = BuildUnifiedBiomeGroundMaterial();
        }

        // Gentle rolling hills placed further back (forest-floor green for Stage 1-30, burnt desert sandstone for Stage 31+ City)
        Color hillCol1 = _isApocalypseThemeActive ? new Color(0.46f, 0.31f, 0.19f) : new Color(0.16f, 0.25f, 0.13f);
        Color hillCol2 = _isApocalypseThemeActive ? new Color(0.39f, 0.26f, 0.16f) : new Color(0.18f, 0.28f, 0.15f);

        CreateHill(bgRoot.transform, "Hill_NW_1", new Vector3(-56f, -1.0f, 34f), new Vector3(16f, 2.8f, 14f), hillCol1);
        CreateHill(bgRoot.transform, "Hill_NW_2", new Vector3(-38f, -1.0f, 38f), new Vector3(16f, 3.0f, 14f), hillCol2);
        CreateHill(bgRoot.transform, "Hill_SW_1", new Vector3(-56f, -1.0f, -34f), new Vector3(16f, 2.8f, 14f), hillCol1);
        CreateHill(bgRoot.transform, "Hill_SW_2", new Vector3(-38f, -1.0f, -38f), new Vector3(16f, 3.0f, 14f), hillCol2);
        CreateHill(bgRoot.transform, "Hill_NE_1", new Vector3(36f, -1.0f, 36f), new Vector3(16f, 2.8f, 14f), hillCol1);
        CreateHill(bgRoot.transform, "Hill_SE_1", new Vector3(36f, -1.0f, -36f), new Vector3(16f, 2.8f, 14f), hillCol1);
        CreateHill(bgRoot.transform, "Hill_FarWest_1", new Vector3(-82f, -1.0f, 10f), new Vector3(16f, 3.0f, 16f), hillCol1);
        CreateHill(bgRoot.transform, "Hill_FarWest_2", new Vector3(-82f, -1.0f, -10f), new Vector3(16f, 3.0f, 16f), hillCol2);
    }

    private static readonly Color ApocalypseEdgeMistColor = new Color(0.54f, 0.28f, 0.14f, 1f);

    private void CreateHill(Transform parent, string name, Vector3 pos, Vector3 scale, Color col)
    {
        var hill = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        hill.name = name;
        hill.transform.SetParent(parent, false);
        hill.transform.position = pos;
        hill.transform.localScale = scale;
        DestroyColliderSafe(hill);

        var rend = hill.GetComponent<Renderer>();
        if (rend != null)
        {
            if (_isApocalypseThemeActive)
            {
                float mist = GetApocalypseEdgeMistAtPosition(new Vector3(pos.x, 0f, pos.z));
                Color mistBlended = Color.Lerp(col, ApocalypseEdgeMistColor, mist * 0.85f);
                rend.sharedMaterial = TrackGeneratedAsset(MakeMat(mistBlended, 0.02f, 0.08f * (1f - mist)));
            }
            else
            {
                float darkness = GetStaticDarknessAtPosition(new Vector3(pos.x, 0f, pos.z));
                if (darkness >= 0.92f)
                {
                    rend.sharedMaterial = CreatePitchBlackShroudPropMaterial();
                    rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    rend.receiveShadows = false;
                }
                else
                {
                    float alpha = Mathf.Clamp01(darkness * 0.985f);
                    float mul = Mathf.LinearToGammaSpace(Mathf.Clamp01(1f - alpha));
                    Color shadedCol = new Color(col.r * mul, col.g * mul, col.b * mul, 1f);
                    rend.sharedMaterial = TrackGeneratedAsset(MakeMat(shadedCol, 0.03f, 0.12f * (1f - darkness)));
                }
            }
        }

        _outerHills.Add(new HillVolume
        {
            center = pos,
            halfExtents = scale * 0.5f
        });
    }

    private float GetOuterTerrainSurfaceY(float x, float z)
    {
        float topY = OUTER_GROUND_TOP_Y;
        for (int i = 0; i < _outerHills.Count; i++)
        {
            HillVolume h = _outerHills[i];
            float nx = (x - h.center.x) / Mathf.Max(0.1f, h.halfExtents.x);
            float nz = (z - h.center.z) / Mathf.Max(0.1f, h.halfExtents.z);
            float rSq = nx * nx + nz * nz;
            if (rSq < 1f)
            {
                float hillTop = h.center.y + h.halfExtents.y * Mathf.Sqrt(1f - rSq);
                if (hillTop > topY) topY = hillTop;
            }
        }
        return topY;
    }

    /// <summary>
    /// Signed distance to the playable Cross boundary:
    /// < 0 inside the Cross (Base or Combat corridors), 0 on the edge, > 0 outside in the grass/forest.
    /// </summary>
    private static float GetSignedDistanceToPlayableCross(float x, float z)
    {
        float sdHoriz = SignedDistanceToBox(x, z, -65f, 45f, -15f, 15f);
        float sdVert  = SignedDistanceToBox(x, z, -15f, 15f, -45f, 45f);
        return Mathf.Min(sdHoriz, sdVert);
    }

    /// <summary>
    /// In Stage 31+ Post-Apocalypse City theme, the cracked asphalt highways extend all the way to the map edges
    /// (East to +120, North/South to +/-120), while zombie spawn points remain at X=43.5 and Z=+/-43.5!
    /// </summary>
    private static float GetSignedDistanceToExtendedCityCross(float x, float z)
    {
        float sdHoriz = SignedDistanceToBox(x, z, -65f, 120f, -15f, 15f);
        float sdVert  = SignedDistanceToBox(x, z, -15f, 15f, -120f, 120f);
        return Mathf.Min(sdHoriz, sdVert);
    }

    private static float GetDistanceOutsideExtendedCityCross(float x, float z)
    {
        return Mathf.Max(0f, GetSignedDistanceToExtendedCityCross(x, z));
    }

    private static float SignedDistanceToBox(float x, float z, float minX, float maxX, float minZ, float maxZ)
    {
        float cx = (minX + maxX) * 0.5f;
        float cz = (minZ + maxZ) * 0.5f;
        float hx = (maxX - minX) * 0.5f;
        float hz = (maxZ - minZ) * 0.5f;
        float dx = Mathf.Abs(x - cx) - hx;
        float dz = Mathf.Abs(z - cz) - hz;
        float outsideDist = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dz, 0f) * Mathf.Max(dz, 0f));
        float insideDist = Mathf.Min(Mathf.Max(dx, dz), 0f);
        return outsideDist + insideDist;
    }

    /// <summary>
    /// Returns shortest distance outside the Cross playable/combat zones (0 if inside base or combat corridors).
    /// </summary>
    private static float GetDistanceOutsidePlayableCross(float x, float z)
    {
        return Mathf.Max(0f, GetSignedDistanceToPlayableCross(x, z));
    }

    private static float SampleFbmNoise(float x, float z, float scale)
    {
        float n1 = Mathf.PerlinNoise(x * scale + 31.7f, z * scale + 17.3f);
        float n2 = Mathf.PerlinNoise(x * scale * 2.3f + 71.1f, z * scale * 2.3f + 53.9f) * 0.5f;
        float n3 = Mathf.PerlinNoise(x * scale * 4.7f + 13.5f, z * scale * 4.7f + 91.2f) * 0.25f;
        return (n1 + n2 + n3) / 1.75f;
    }

    /// <summary>
    /// Creates a single URP-Lit material with a 512x512 procedural biome texture that organically blends:
    /// - Stage 1-30: Base earth, Battlefield dirt roads, and surrounding Grass Meadow.
    /// - Stage 31+ (Apocalypse City): Base concrete slab, Cracked Asphalt Highways stretching all the way to the map edges
    ///   with faded lane markings, and Sun-baked Cracked Sandstone/Ochre Wasteland Desert & dry scrubland.
    /// </summary>
    private Material BuildUnifiedBiomeGroundMaterial()
    {
        const int texW = 512;
        const int texH = 512;

        Texture2D biomeTex = TrackGeneratedAsset(new Texture2D(texW, texH, TextureFormat.RGBA32, false));
        biomeTex.name = _isApocalypseThemeActive ? "Tex_UnifiedApocalypseGround_512" : "Tex_UnifiedBiomeGround_512";
        biomeTex.wrapMode = TextureWrapMode.Clamp;
        biomeTex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[texW * texH];

        Color baseEarthCol = _isApocalypseThemeActive ? new Color(0.35f, 0.29f, 0.22f) : new Color(0.18f, 0.27f, 0.20f);
        Color frontlineDirtCol = _isApocalypseThemeActive ? new Color(0.28f, 0.24f, 0.21f) : new Color(0.22f, 0.20f, 0.17f);
        Color combatRoadDirtCol = _isApocalypseThemeActive ? new Color(0.25f, 0.22f, 0.19f) : new Color(0.23f, 0.19f, 0.16f);
        Color dirtDarkGrain = _isApocalypseThemeActive ? new Color(0.16f, 0.14f, 0.12f) : new Color(0.19f, 0.17f, 0.13f);
        Color mossyEarthCol = _isApocalypseThemeActive ? new Color(0.45f, 0.33f, 0.21f) : new Color(0.19f, 0.26f, 0.15f);
        Color meadowGrass1 = _isApocalypseThemeActive ? new Color(0.56f, 0.40f, 0.25f) : new Color(0.16f, 0.25f, 0.13f);
        Color meadowGrass2 = _isApocalypseThemeActive ? new Color(0.44f, 0.31f, 0.19f) : new Color(0.20f, 0.30f, 0.16f);
        Color deepForestGrass = _isApocalypseThemeActive ? new Color(0.34f, 0.25f, 0.16f) : new Color(0.13f, 0.21f, 0.11f);
        Color roadStripeCol = new Color(0.68f, 0.58f, 0.38f);

        for (int py = 0; py < texH; py++)
        {
            // Unity's Cube top face maps U from +X to -X and V from +Z to -Z (rotated 180 degrees in XZ)!
            float v = 1f - (float)py / (texH - 1);
            float wz = Mathf.Lerp(UNIFIED_GROUND_MIN_Z, UNIFIED_GROUND_MAX_Z, v);

            for (int px = 0; px < texW; px++)
            {
                float u = 1f - (float)px / (texW - 1);
                float wx = Mathf.Lerp(UNIFIED_GROUND_MIN_X, UNIFIED_GROUND_MAX_X, u);

                float sd = _isApocalypseThemeActive
                    ? GetSignedDistanceToExtendedCityCross(wx, wz)
                    : GetSignedDistanceToPlayableCross(wx, wz);

                // Multi-scale Perlin noise for organic wavy shoreline and mottled soil/grass/sandstone patches
                float nLow = SampleFbmNoise(wx, wz, 0.11f);
                float nHigh = SampleFbmNoise(wx + 63.4f, wz - 41.8f, 0.32f);

                // Interior surface color transitions from Base (-65..-35) -> Frontline (-35..-15) -> Battlefield road
                float baseToRoadT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-38f, -28f, wx));
                Color rawDirt = Color.Lerp(baseEarthCol, Color.Lerp(frontlineDirtCol, combatRoadDirtCol, Mathf.Clamp01((wx + 15f) / 30f)), baseToRoadT);
                Color texturedDirt = Color.Lerp(rawDirt, dirtDarkGrain, (nHigh - 0.35f) * 0.55f);

                // In Post-Apocalypse City theme, add weathered cracked asphalt fissures & faded highway centerline markings all the way to the map edges!
                if (_isApocalypseThemeActive && sd < -0.5f)
                {
                    bool onHorizCenter = (wx > -34f && wx < 118f && Mathf.Abs(wz) < 0.38f && Mathf.Repeat(wx + 60f, 6.0f) < 3.4f);
                    bool onVertCenter  = (Mathf.Abs(wz) > 4f && Mathf.Abs(wz) < 118f && Mathf.Abs(wx) < 0.38f && Mathf.Repeat(wz + 60f, 6.0f) < 3.4f);
                    if ((onHorizCenter || onVertCenter) && nHigh > 0.34f)
                    {
                        texturedDirt = Color.Lerp(texturedDirt, roadStripeCol, 0.55f * nHigh);
                    }
                }

                // Outer biome (Meadow/Forest vs Sun-baked Cracked Wasteland Sandstone & Dry Scrub)
                Color meadowCol = Color.Lerp(meadowGrass1, meadowGrass2, nHigh);
                float deepForestT = Mathf.Clamp01((sd - 8f) / 22f);
                Color texturedGrass = Color.Lerp(meadowCol, deepForestGrass, deepForestT * 0.55f);

                // Wavy Perlin-noise perturbation (+/- 3.2m) so the border is completely organic (never a straight line!)
                float waveOffset = (nLow - 0.48f) * 5.4f + (nHigh - 0.5f) * 2.2f;
                float noisySd = sd + waveOffset;

                // Smooth 7.5m blend band
                float grassBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-2.4f, 5.1f, noisySd));
                float mossBand = Mathf.Sin(grassBlend * Mathf.PI);

                Color finalCol = Color.Lerp(texturedDirt, texturedGrass, grassBlend);
                finalCol = Color.Lerp(finalCol, mossyEarthCol, mossBand * 0.42f);

                pixels[py * texW + px] = new Color(finalCol.r, finalCol.g, finalCol.b, 1f);
            }
        }

        biomeTex.SetPixels(pixels);
        biomeTex.Apply(false, true);

        Material mat = TrackGeneratedAsset(MakeMat(Color.white, 0.03f, 0.14f));
        mat.name = "Mat_UnifiedBiomeGround_Lit";
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", biomeTex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", biomeTex);
        return mat;
    }

    private Material CreatePitchBlackShroudPropMaterial()
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ??
                    Shader.Find("Unlit/Color") ??
                    GetShader();
        Material mat = TrackGeneratedAsset(new Material(sh));
        mat.name = "Mat_NaturePack_PitchBlackShroud";
        mat.enableInstancing = true;
        Color shroudCol = new Color(0.015f, 0.02f, 0.03f, 1f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", shroudCol);
        mat.color = shroudCol;
        return mat;
    }

    /// <summary>
    /// Creates an instanced palette of nature materials shaded smoothly from full brightness (d=0)
    /// into pitch-black darkness (d>=0.92) so tall tree canopies rising above Y=2.35f shroud plane
    /// are smoothly and completely covered by the right-side/flank darkness without parallax artifacts.
    /// </summary>
    private Material[] CreateNatureDarknessPalette(Texture2D atlas, Material pitchBlackMat, int bucketCount, bool isTallCanopy)
    {
        Material[] palette = new Material[bucketCount];
        for (int i = 0; i < bucketCount; i++)
        {
            float d = (float)i / (bucketCount - 1);
            if (d >= 0.92f)
            {
                palette[i] = pitchBlackMat;
                continue;
            }

            float alpha = Mathf.Clamp01(d * 0.985f);
            float oneMinusA = Mathf.Clamp01(1f - alpha);
            // Tall trees rise above Darkness_OverheadShroud_Plane (Y=2.35f), so they need (1-alpha)^2
            // to match the ground (which passes through both GroundShadow and OverheadShroud).
            // Low props (grass/rocks) sit below OverheadShroud, so (1-alpha) combined with OverheadShroud gives (1-alpha)^2.
            float linearAtten = isTallCanopy ? (oneMinusA * oneMinusA) : oneMinusA;
            float srgbMul = Mathf.LinearToGammaSpace(linearAtten);
            Color tint = new Color(srgbMul, srgbMul, srgbMul, 1f);

            Material mat = TrackGeneratedAsset(MakeMat(tint, 0f, 0.10f * (1f - d)));
            mat.name = isTallCanopy ? $"Mat_NatureTree_Dark_{i}" : $"Mat_NatureLow_Dark_{i}";
            mat.enableInstancing = true;

            if (atlas != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", atlas);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", atlas);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);

            // Suppress specular sheen & skybox reflections on darkened props so silhouettes never glow in the dark
            if (d > 0.15f)
            {
                if (mat.HasProperty("_SpecularHighlights")) mat.SetFloat("_SpecularHighlights", 0f);
                if (mat.HasProperty("_EnvironmentReflections")) mat.SetFloat("_EnvironmentReflections", 0f);
                mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            }

            palette[i] = mat;
        }
        return palette;
    }

    private static int PickDarknessBucket(int bucketCount, float darkness)
    {
        return Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(darkness) * (bucketCount - 1)), 0, bucketCount - 1);
    }

    private static GameObject[] LoadNaturePrefabs(params string[] names)
    {
        var list = new System.Collections.Generic.List<GameObject>(names.Length);
        foreach (string n in names)
        {
            GameObject p = Resources.Load<GameObject>("Models/Nature/" + n);
            if (p != null) list.Add(p);
        }
        return list.ToArray();
    }

    private struct PrefabMeshPart
    {
        public Mesh mesh;
        public Matrix4x4 localMatrix;
    }

    private struct ChunkBatchKey : System.IEquatable<ChunkBatchKey>
    {
        public int chunkX;
        public int chunkZ;
        public int matId;
        public bool castShadows;
        public bool receiveShadows;

        public bool Equals(ChunkBatchKey other)
        {
            return chunkX == other.chunkX && chunkZ == other.chunkZ && matId == other.matId &&
                   castShadows == other.castShadows && receiveShadows == other.receiveShadows;
        }

        public override bool Equals(object obj) => obj is ChunkBatchKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = chunkX * 73856093 ^ chunkZ * 19349663 ^ matId * 83492791;
                if (castShadows) h ^= 1 << 29;
                if (receiveShadows) h ^= 1 << 30;
                return h;
            }
        }
    }

    private static PrefabMeshPart[] ExtractReadablePrefabMeshes(GameObject prefab)
    {
        if (prefab == null) return null;
        var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
        if (filters == null || filters.Length == 0) return null;

        Matrix4x4 rootLocal = Matrix4x4.TRS(Vector3.zero, prefab.transform.localRotation, prefab.transform.localScale);
        Matrix4x4 invPrefabWorld = prefab.transform.worldToLocalMatrix;
        var parts = new System.Collections.Generic.List<PrefabMeshPart>(filters.Length);

        for (int i = 0; i < filters.Length; i++)
        {
            Mesh m = filters[i].sharedMesh;
            if (m == null || !m.isReadable) return null; // Fall back safely if any submesh is not readable
            Matrix4x4 localMat = (filters[i].transform == prefab.transform)
                ? rootLocal
                : rootLocal * (invPrefabWorld * filters[i].transform.localToWorldMatrix);
            parts.Add(new PrefabMeshPart { mesh = m, localMatrix = localMat });
        }
        return parts.Count > 0 ? parts.ToArray() : null;
    }

    /// <summary>
    /// Scatters grass around the Human Base & combat perimeters, gradually transitioning outward
    /// into a dense low-poly forest. Uses 36m x 36m chunked static mesh combining (Mesh.CombineMeshes)
    /// so thousands of grass tufts & trees render as ~50 lightweight static chunks instead of 20,000 GameObjects!
    /// Strictly never spawns inside the base or combat corridors.
    /// </summary>
    private void BuildOuterNatureForest()
    {
        GameObject[] grassPrefabs = LoadNaturePrefabs("Grass_01", "Grass_02", "Grass_03", "Grass_04", "Grass_05");
        GameObject[] smallTreePrefabs = LoadNaturePrefabs("Tree_Pine_Small", "Tree_Pine_Med", "Tree_Oak");
        GameObject[] denseTreePrefabs = LoadNaturePrefabs("Tree_Pine_Small", "Tree_Pine_Med", "Tree_Pine_Dark", "Tree_Pine_Large", "Tree_Pine_Giant", "Tree_Oak");
        GameObject[] deadTreePrefabs = LoadNaturePrefabs("Tree_Dead", "Tree_Stump");
        GameObject[] rockPrefabs = LoadNaturePrefabs("Rock_01", "Rock_02", "Rock_03", "Rock_04", "Rock_05");

        if (grassPrefabs.Length == 0 && denseTreePrefabs.Length == 0)
        {
            Debug.LogWarning("[CrossMapVisualizer] Nature models not found in Resources/Models/Nature/.");
            return;
        }

        var natureRoot = new GameObject("OuterBackground_NatureForest");
        natureRoot.transform.SetParent(rootCross.transform, false);

        Texture2D natureAtlas = Resources.Load<Texture2D>("Textures/Nature/NatureAtlas");
        Material pitchBlackMat = CreatePitchBlackShroudPropMaterial();
        const int DARKNESS_BUCKETS = 10;
        Material[] tallTreeMats = CreateNatureDarknessPalette(natureAtlas, pitchBlackMat, DARKNESS_BUCKETS, isTallCanopy: true);
        Material[] lowPropMats = CreateNatureDarknessPalette(natureAtlas, pitchBlackMat, DARKNESS_BUCKETS, isTallCanopy: false);

        // Cache extracted readable meshes per prefab for zero-allocation chunk combining
        var meshCache = new System.Collections.Generic.Dictionary<GameObject, PrefabMeshPart[]>();
        void CacheArray(GameObject[] arr)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] != null && !meshCache.ContainsKey(arr[i]))
                    meshCache[arr[i]] = ExtractReadablePrefabMeshes(arr[i]);
            }
        }
        CacheArray(grassPrefabs);
        CacheArray(smallTreePrefabs);
        CacheArray(denseTreePrefabs);
        CacheArray(deadTreePrefabs);
        CacheArray(rockPrefabs);

        const float CHUNK_SIZE = 36f;
        var chunkBatches = new System.Collections.Generic.Dictionary<ChunkBatchKey, System.Collections.Generic.List<CombineInstance>>();
        var batchMaterials = new System.Collections.Generic.Dictionary<int, Material>();

        void QueueNatureProp(GameObject prefab, Material[] palette, int bucketIdx, bool isTallPalette, float x, float z, float scale, float rotY, bool castShadows, bool receiveShadows)
        {
            if (prefab == null) return;
            Material mat = palette[bucketIdx];

            if (meshCache.TryGetValue(prefab, out PrefabMeshPart[] parts) && parts != null)
            {
                float surfaceY = GetOuterTerrainSurfaceY(x, z) - 0.04f;
                Matrix4x4 propMat = Matrix4x4.TRS(new Vector3(x, surfaceY, z), Quaternion.Euler(0f, rotY, 0f), Vector3.one * scale);

                int matId = (isTallPalette ? 100 : 0) + bucketIdx;
                if (mat == pitchBlackMat) matId = 999; // Merge all pitch-black props across palettes into one single batch!
                if (!batchMaterials.ContainsKey(matId)) batchMaterials[matId] = mat;

                var key = new ChunkBatchKey
                {
                    chunkX = Mathf.FloorToInt(x / CHUNK_SIZE),
                    chunkZ = Mathf.FloorToInt(z / CHUNK_SIZE),
                    matId = matId,
                    castShadows = castShadows,
                    receiveShadows = receiveShadows
                };

                if (!chunkBatches.TryGetValue(key, out var list))
                {
                    list = new System.Collections.Generic.List<CombineInstance>(64);
                    chunkBatches[key] = list;
                }

                for (int p = 0; p < parts.Length; p++)
                {
                    Mesh m = parts[p].mesh;
                    Matrix4x4 combinedMat = propMat * parts[p].localMatrix;
                    int subCount = Mathf.Max(1, m.subMeshCount);
                    for (int sub = 0; sub < subCount; sub++)
                    {
                        list.Add(new CombineInstance
                        {
                            mesh = m,
                            subMeshIndex = sub,
                            transform = combinedMat
                        });
                    }
                }
            }
            else
            {
                // Fallback if mesh isn't marked Read/Write yet
                SpawnNatureProp(prefab, natureRoot.transform, mat, x, z, scale, rotY, castShadows, receiveShadows);
            }
        }

        var rng = new System.Random(421337);

        // Tightened to camera-visible bounds (Camera panLimitX: -70..50, panLimitZ: -68..48)
        // and East side past X=+30 is 100% pitch-black shroud anyway!
        const float minX = -108f;
        const float maxX = 32f;
        const float minZ = -82f;
        const float maxZ = 82f;

        // 1a. BORDER MICRO-GRASS FRINGE: Short, low grass tufts right along the dirt-to-grass border (0.22m..1.10m outside)
        if (grassPrefabs.Length > 0)
        {
            const float fringeStep = 1.45f;
            for (float fx = -70f; fx <= 26f; fx += fringeStep)
            {
                for (float fz = -46f; fz <= 46f; fz += fringeStep)
                {
                    float x = fx + ((float)rng.NextDouble() * 2f - 1f) * 0.45f;
                    float z = fz + ((float)rng.NextDouble() * 2f - 1f) * 0.45f;
                    float dist = GetDistanceOutsidePlayableCross(x, z);
                    if (dist < 0.22f || dist > 1.10f) continue;
                    if ((float)rng.NextDouble() > 0.76f) continue;

                    float darkness = GetStaticDarknessAtPosition(new Vector3(x, 0f, z));
                    if (darkness >= 0.85f) continue; // Invisible under deep darkness shroud

                    int bucket = PickDarknessBucket(DARKNESS_BUCKETS, darkness);
                    GameObject prefab = grassPrefabs[rng.Next(grassPrefabs.Length)];
                    float tEdge = Mathf.InverseLerp(0.22f, 1.10f, dist);
                    float scale = Mathf.Lerp(0.18f, 0.36f, tEdge) + (float)rng.NextDouble() * 0.06f;
                    QueueNatureProp(prefab, lowPropMats, bucket, false, x, z, scale, (float)rng.NextDouble() * 360f, castShadows: false, receiveShadows: darkness < 0.65f);
                }
            }
        }

        // 1b. GRASS PASS: Lush grass ring directly hugging the Human Base & corridor perimeter,
        //     tapering smoothly under deep forest canopies where trees already cover the floor.
        if (grassPrefabs.Length > 0)
        {
            const float grassStep = 2.25f;
            for (float gx = minX; gx <= maxX; gx += grassStep)
            {
                for (float gz = minZ; gz <= maxZ; gz += grassStep)
                {
                    float x = gx + ((float)rng.NextDouble() * 2f - 1f) * 0.9f;
                    float z = gz + ((float)rng.NextDouble() * 2f - 1f) * 0.9f;

                    float dist = GetDistanceOutsidePlayableCross(x, z);
                    // Strict safety buffer so grass never enters the playable base or combat lanes
                    if (dist < 0.85f) continue;

                    float darkness = GetStaticDarknessAtPosition(new Vector3(x, 0f, z));
                    if (darkness >= 0.85f) continue; // Skip tiny invisible grass inside pitch-black darkness

                    // Inner grass belt (0.85m..9m): lush (~92%); deeper under dense forest canopy: lighter undergrowth (~20%)
                    float grassChance = (dist <= 9f) ? 0.92f : Mathf.Lerp(0.92f, 0.20f, Mathf.Clamp01((dist - 9f) / 12f));
                    if ((float)rng.NextDouble() > grassChance) continue;

                    int bucket = PickDarknessBucket(DARKNESS_BUCKETS, darkness);
                    GameObject prefab = grassPrefabs[rng.Next(grassPrefabs.Length)];
                    float scale = 0.42f + (float)rng.NextDouble() * 0.26f;
                    QueueNatureProp(prefab, lowPropMats, bucket, false, x, z, scale, (float)rng.NextDouble() * 360f, castShadows: false, receiveShadows: darkness < 0.65f);

                    // Extra companion tuft only in the visible inner grass clearing around the base
                    if (dist <= 7.0f && darkness < 0.55f && (float)rng.NextDouble() < 0.48f)
                    {
                        float ox = x + ((float)rng.NextDouble() * 2f - 1f) * 0.85f;
                        float oz = z + ((float)rng.NextDouble() * 2f - 1f) * 0.85f;
                        if (GetDistanceOutsidePlayableCross(ox, oz) >= 0.85f)
                        {
                            float d2 = GetStaticDarknessAtPosition(new Vector3(ox, 0f, oz));
                            int b2 = PickDarknessBucket(DARKNESS_BUCKETS, d2);
                            GameObject prefab2 = grassPrefabs[rng.Next(grassPrefabs.Length)];
                            float scale2 = 0.36f + (float)rng.NextDouble() * 0.24f;
                            QueueNatureProp(prefab2, lowPropMats, b2, false, ox, oz, scale2, (float)rng.NextDouble() * 360f, castShadows: false, receiveShadows: d2 < 0.65f);
                        }
                    }
                }
            }
        }

        // 2. TREE & FOREST PASS: Short grass-only margin (0.85m..4.2m) right around the base,
        //    then rapidly thickening over a short 9.5m transition (4.2m -> 13.7m) into a dense forest!
        const float treeClearanceDist = 4.2f;
        const float treeRampSpan = 9.5f;
        const float treeStep = 3.4f;

        for (float tx = minX; tx <= maxX; tx += treeStep)
        {
            for (float tz = minZ; tz <= maxZ; tz += treeStep)
            {
                float x = tx + ((float)rng.NextDouble() * 2f - 1f) * 1.35f;
                float z = tz + ((float)rng.NextDouble() * 2f - 1f) * 1.35f;

                float dist = GetDistanceOutsidePlayableCross(x, z);
                float darkness = GetStaticDarknessAtPosition(new Vector3(x, 0f, z));

                // Skip completely pitch-black trees deep inside the solid black shroud (d >= 0.96)
                // while keeping the full twilight-to-black silhouette fade (d = 0..0.95) intact!
                if (darkness >= 0.96f) continue;

                // Strictly grass-only ring right next to the base & combat lanes
                if (dist < treeClearanceDist)
                {
                    if (dist >= 2.2f && darkness < 0.80f && rockPrefabs.Length > 0 && (float)rng.NextDouble() < 0.07f)
                    {
                        int rockBucket = PickDarknessBucket(DARKNESS_BUCKETS, darkness);
                        GameObject rock = rockPrefabs[rng.Next(rockPrefabs.Length)];
                        float rScale = 0.26f + (float)rng.NextDouble() * 0.22f;
                        QueueNatureProp(rock, lowPropMats, rockBucket, false, x, z, rScale, (float)rng.NextDouble() * 360f, castShadows: darkness < 0.45f, receiveShadows: darkness < 0.70f);
                    }
                    continue;
                }

                // Short, steep density ramp: 34% right at 4.2m -> ~78% at 9m -> 95% dense forest at 13.7m+
                float forestT = Mathf.Clamp01((dist - treeClearanceDist) / treeRampSpan);
                float spawnChance = Mathf.Lerp(0.34f, 0.95f, Mathf.Pow(forestT, 0.62f));
                if ((float)rng.NextDouble() > spawnChance) continue;

                GameObject chosenPrefab = null;
                float propScale;
                bool isTallTree = true;

                double roll = rng.NextDouble();
                float deadChance = (x > 5f) ? 0.18f : 0.05f;
                if (deadTreePrefabs.Length > 0 && roll < deadChance)
                {
                    chosenPrefab = deadTreePrefabs[rng.Next(deadTreePrefabs.Length)];
                    propScale = 0.50f + (float)rng.NextDouble() * 0.28f;
                }
                else if (rockPrefabs.Length > 0 && roll < deadChance + 0.05f)
                {
                    chosenPrefab = rockPrefabs[rng.Next(rockPrefabs.Length)];
                    propScale = 0.34f + (float)rng.NextDouble() * 0.32f;
                    isTallTree = false;
                }
                else if (forestT < 0.28f && smallTreePrefabs.Length > 0)
                {
                    // Inner woodland fringe (4.2m..6.8m): compact pines & oaks
                    chosenPrefab = smallTreePrefabs[rng.Next(smallTreePrefabs.Length)];
                    propScale = Mathf.Lerp(0.45f, 0.62f, forestT / 0.28f) + (float)rng.NextDouble() * 0.08f;
                }
                else if (denseTreePrefabs.Length > 0)
                {
                    // Full dense forest (6.8m+): rich mix of Small/Med/Dark/Large/Giant pines & oaks
                    chosenPrefab = denseTreePrefabs[rng.Next(denseTreePrefabs.Length)];
                    propScale = Mathf.Lerp(0.55f, 0.78f, forestT) + (float)rng.NextDouble() * 0.15f;
                }
                else
                {
                    continue;
                }

                int bucket = PickDarknessBucket(DARKNESS_BUCKETS, darkness);
                // Only cast realtime shadows for lit/mid-range trees near the playable cross to keep GPU shadow pass fast!
                bool shouldCastShadows = isTallTree && darkness < 0.50f && dist < 26f;
                QueueNatureProp(chosenPrefab, isTallTree ? tallTreeMats : lowPropMats, bucket, isTallTree, x, z, propScale, (float)rng.NextDouble() * 360f,
                    castShadows: shouldCastShadows, receiveShadows: darkness < 0.70f);
            }
        }

        // 3. BAKE EACH SPATIAL CHUNK BATCH INTO A SINGLE COMBINED STATIC MESH
        foreach (var kvp in chunkBatches)
        {
            var key = kvp.Key;
            var instances = kvp.Value;
            if (instances == null || instances.Count == 0) continue;

            Mesh combinedMesh = TrackGeneratedAsset(new Mesh());
            combinedMesh.name = $"NatureChunk_{key.chunkX}_{key.chunkZ}_M{key.matId}";
            combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            combinedMesh.CombineMeshes(instances.ToArray(), true, true, false);
            combinedMesh.RecalculateBounds();
            if (Application.isPlaying) combinedMesh.UploadMeshData(true); // Free CPU-side mesh copy once uploaded to GPU!

            GameObject chunkObj = new GameObject(combinedMesh.name);
            chunkObj.transform.SetParent(natureRoot.transform, false);
            chunkObj.isStatic = true;

            var mf = chunkObj.AddComponent<MeshFilter>();
            mf.sharedMesh = combinedMesh;

            var mr = chunkObj.AddComponent<MeshRenderer>();
            mr.sharedMaterial = batchMaterials[key.matId];
            mr.shadowCastingMode = key.castShadows
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = key.receiveShadows;
        }
    }

    private void SpawnNatureProp(GameObject prefab, Transform parent, Material sharedMat, float x, float z, float scale, float rotY, bool castShadows, bool receiveShadows = true)
    {
        if (prefab == null) return;

        float surfaceY = GetOuterTerrainSurfaceY(x, z) - 0.04f;
        GameObject holder = new GameObject(prefab.name);
        holder.transform.SetParent(parent, false);
        holder.transform.position = new Vector3(x, surfaceY, z);
        holder.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
        holder.transform.localScale = Vector3.one * scale;

        GameObject inst = Instantiate(prefab, holder.transform);
        inst.name = prefab.name + "_Mesh";
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = prefab.transform.localRotation;
        inst.transform.localScale = prefab.transform.localScale;

        // Strip any colliders so raycasts for building placement & targeting never hit scenery
        foreach (var col in inst.GetComponentsInChildren<Collider>(true))
        {
            if (col == null) continue;
            if (Application.isPlaying) Destroy(col);
            else DestroyImmediate(col);
        }

        foreach (var rend in inst.GetComponentsInChildren<Renderer>(true))
        {
            if (rend == null) continue;
            if (sharedMat != null) rend.sharedMaterial = sharedMat;
            rend.shadowCastingMode = castShadows
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = receiveShadows;
        }
    }

    private struct CityMeshPart
    {
        public Mesh mesh;
        public Matrix4x4 localMatrix;
        public int matId;
    }

    private static GameObject[] LoadApocalypseCityPrefabs(params string[] names)
    {
        var list = new System.Collections.Generic.List<GameObject>(names.Length);
        foreach (string n in names)
        {
            GameObject p = Resources.Load<GameObject>("Models/ApocalypseCity/" + n);
            if (p != null) list.Add(p);
        }
        return list.ToArray();
    }

    /// <summary>
    /// Stage 31+ Post-Apocalyptic Ruined City Theme:
    /// Replaces the forest with dry desert rocks & scrub near the highway perimeter, transitioning into
    /// bombed-out city blocks and towering ruined apartment skyscrapers against a burning crimson sunset sky.
    /// Combined into 36m x 36m static chunks for ultra-high FPS. Strictly zero props inside Base or Combat lanes!
    /// </summary>
    private void BuildOuterApocalypseCity()
    {
        GameObject[] lowRuinPrefabs = LoadApocalypseCityPrefabs(
            "Ruin_Collapsed_01", "Ruin_Collapsed_02", "Ruin_Collapsed_03",
            "Ruin_CityBlock_01", "Ruin_CityBlock_02", "Ruin_CityBlock_03",
            "Ruin_CityBlock_05", "Ruin_CityBlock_06", "Ruin_Wreckage");

        GameObject[] tallSkyscraperPrefabs = LoadApocalypseCityPrefabs(
            "Ruin_MegaHighrise", "Ruin_Tower_01", "Ruin_Tower_02",
            "Ruin_Apartment_01", "Ruin_Apartment_02", "Ruin_Apartment_03",
            "Ruin_Apartment_04", "Ruin_Apartment_05", "Ruin_CityBlock_04",
            "Ruin_Collapsed_02");

        GameObject[] wreckagePrefabs = LoadApocalypseCityPrefabs("Ruin_Wreckage", "Ruin_Collapsed_01");
        GameObject[] rockPrefabs = LoadNaturePrefabs("Rock_01", "Rock_02", "Rock_03", "Rock_04", "Rock_05");
        GameObject[] scrubPrefabs = LoadNaturePrefabs("Grass_01", "Grass_02", "Grass_03", "Grass_04", "Grass_05");

        var cityRoot = new GameObject("OuterBackground_ApocalypseCity");
        cityRoot.transform.SetParent(rootCross.transform, false);

        // Material registry for chunk combining (with 6 edge-mist buckets per base material; NO _EmissionColor so buildings never wash out white!)
        const int CITY_MIST_BUCKETS = 6;
        var batchMaterials = new System.Collections.Generic.Dictionary<int, Material>();
        var matNameToBaseId = new System.Collections.Generic.Dictionary<string, int>();
        int nextBaseMatGroup = 1;

        Color sunsetBuildingTint = new Color(0.82f, 0.71f, 0.60f, 1f);
        Color weatheredDarkConcreteTint = new Color(0.55f, 0.46f, 0.38f, 1f);
        Color deepMistShadowTint = new Color(0.36f, 0.19f, 0.10f, 1f);

        int RegisterMaterialMistGroup(string label, Texture2D tex, Color baseTint, float metallic, float smoothness)
        {
            int groupIndex = nextBaseMatGroup++;
            int baseId = groupIndex * 10;

            for (int b = 0; b < CITY_MIST_BUCKETS; b++)
            {
                float mist = (float)b / (CITY_MIST_BUCKETS - 1);
                int matId = baseId + b;

                // Keep 100% of _BaseMap texture detail (never add flat _EmissionColor which washes buildings white!)
                // Smoothly shade _BaseColor toward warm dusky amber-brown in the outer mist perimeter.
                Color shadedTint = Color.Lerp(baseTint, deepMistShadowTint, mist * 0.82f);

                Material mat = TrackGeneratedAsset(MakeMat(shadedTint, metallic * (1f - mist), smoothness * (1f - mist)));
                mat.name = $"Mat_{label}_Mist{b}";
                mat.enableInstancing = true;
                if (tex != null)
                {
                    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                    if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
                }
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", shadedTint);

                if (b > 0)
                {
                    if (mat.HasProperty("_SpecularHighlights")) mat.SetFloat("_SpecularHighlights", 0f);
                    if (mat.HasProperty("_EnvironmentReflections")) mat.SetFloat("_EnvironmentReflections", 0f);
                    mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                    mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                }

                batchMaterials[matId] = mat;
            }
            return baseId;
        }

        int GetOrCreateCityMaterialBaseId(string rawMatName)
        {
            string key = string.IsNullOrEmpty(rawMatName) ? "CollapsedBuilding02a" : rawMatName;
            if (matNameToBaseId.TryGetValue(key, out int existingBaseId)) return existingBaseId;

            Texture2D tex = Resources.Load<Texture2D>("Textures/ApocalypseCity/Tex_" + key);
            if (tex == null) tex = Resources.Load<Texture2D>("Textures/ApocalypseCity/Tex_CollapsedBuilding02a");
            if (tex != null) tex.wrapMode = TextureWrapMode.Repeat;

            // Brighter raw concrete textures (project_building01/01a, destroyed_cityblock01d, CollapsedBuilding02a)
            // receive a grittier weathered soot/charcoal-amber tint so no building ever looks pale or white!
            bool isBrightConcreteTex =
                key.IndexOf("project_building01", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                key.IndexOf("destroyed_cityblock01d", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                key.IndexOf("CollapsedBuilding02a", System.StringComparison.OrdinalIgnoreCase) >= 0;

            Color chosenTint = isBrightConcreteTex ? weatheredDarkConcreteTint : sunsetBuildingTint;
            int baseId = RegisterMaterialMistGroup("ApocalypseCity_" + key, tex, chosenTint, 0.04f, 0.10f);
            matNameToBaseId[key] = baseId;
            return baseId;
        }

        // Dedicated material group for desert rocks & dry desert scrub (using NatureAtlas tinted warm sandy-ochre)
        Texture2D natureAtlas = Resources.Load<Texture2D>("Textures/Nature/NatureAtlas");
        Color desertScrubTint = new Color(0.76f, 0.60f, 0.40f, 1f);
        int desertNatureBaseId = RegisterMaterialMistGroup("ApocalypseDesertScrub", natureAtlas, desertScrubTint, 0.02f, 0.10f);

        // Extract readable mesh parts & resolve texture material base ID per part
        var cityMeshCache = new System.Collections.Generic.Dictionary<GameObject, CityMeshPart[]>();
        string[] knownCityMatKeys = new string[]
        {
            "buildingsheet_03a", "CollapsedBuilding02a", "plasterwall021a",
            "destroyed_cityblock01a", "destroyed_cityblock01b", "destroyed_cityblock01c", "destroyed_cityblock01d",
            "project_building01a", "project_building01", "project_building03c", "project_building03", "project_building04d"
        };

        CityMeshPart[] ExtractCityPrefabParts(GameObject prefab, int overrideBaseId = -1)
        {
            if (prefab == null) return null;
            var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            if (filters == null || filters.Length == 0) return null;

            Matrix4x4 rootLocal = Matrix4x4.TRS(Vector3.zero, prefab.transform.localRotation, prefab.transform.localScale);
            Matrix4x4 invPrefabWorld = prefab.transform.worldToLocalMatrix;
            var parts = new System.Collections.Generic.List<CityMeshPart>(filters.Length);

            for (int i = 0; i < filters.Length; i++)
            {
                Mesh m = filters[i].sharedMesh;
                if (m == null || !m.isReadable) return null;

                Matrix4x4 localMat = (filters[i].transform == prefab.transform)
                    ? rootLocal
                    : rootLocal * (invPrefabWorld * filters[i].transform.localToWorldMatrix);

                int resolvedBaseId = overrideBaseId;
                if (resolvedBaseId < 0)
                {
                    string objName = filters[i].gameObject.name;
                    var mr = filters[i].GetComponent<MeshRenderer>();
                    string matName = (mr != null && mr.sharedMaterial != null) ? mr.sharedMaterial.name : "";
                    string matchedKey = "CollapsedBuilding02a";
                    for (int k = 0; k < knownCityMatKeys.Length; k++)
                    {
                        string candidate = knownCityMatKeys[k];
                        if (objName.IndexOf(candidate, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                            matName.IndexOf(candidate, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            matchedKey = candidate;
                            break;
                        }
                    }
                    resolvedBaseId = GetOrCreateCityMaterialBaseId(matchedKey);
                }

                parts.Add(new CityMeshPart { mesh = m, localMatrix = localMat, matId = resolvedBaseId });
            }
            return parts.Count > 0 ? parts.ToArray() : null;
        }

        void CacheCityArray(GameObject[] arr, int overrideBaseId = -1)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] != null && !cityMeshCache.ContainsKey(arr[i]))
                    cityMeshCache[arr[i]] = ExtractCityPrefabParts(arr[i], overrideBaseId);
            }
        }

        CacheCityArray(lowRuinPrefabs);
        CacheCityArray(tallSkyscraperPrefabs);
        CacheCityArray(wreckagePrefabs);
        CacheCityArray(rockPrefabs, desertNatureBaseId);
        CacheCityArray(scrubPrefabs, desertNatureBaseId);

        const float CHUNK_SIZE = 38f;
        var chunkBatches = new System.Collections.Generic.Dictionary<ChunkBatchKey, System.Collections.Generic.List<CombineInstance>>();

        void QueueCityProp(GameObject prefab, float x, float z, float scale, float rotY, float mist, bool castShadows)
        {
            if (prefab == null) return;
            float surfaceY = GetOuterTerrainSurfaceY(x, z) - 0.04f;
            int mistBucket = PickDarknessBucket(CITY_MIST_BUCKETS, mist);

            if (cityMeshCache.TryGetValue(prefab, out CityMeshPart[] parts) && parts != null)
            {
                Matrix4x4 propMat = Matrix4x4.TRS(new Vector3(x, surfaceY, z), Quaternion.Euler(0f, rotY, 0f), Vector3.one * scale);
                int cx = Mathf.FloorToInt(x / CHUNK_SIZE);
                int cz = Mathf.FloorToInt(z / CHUNK_SIZE);

                for (int p = 0; p < parts.Length; p++)
                {
                    int actualMatId = parts[p].matId + mistBucket;
                    var key = new ChunkBatchKey
                    {
                        chunkX = cx,
                        chunkZ = cz,
                        matId = actualMatId,
                        castShadows = castShadows && mist < 0.55f,
                        receiveShadows = mist < 0.75f
                    };

                    if (!chunkBatches.TryGetValue(key, out var list))
                    {
                        list = new System.Collections.Generic.List<CombineInstance>(64);
                        chunkBatches[key] = list;
                    }

                    Mesh m = parts[p].mesh;
                    Matrix4x4 combinedMat = propMat * parts[p].localMatrix;
                    int subCount = Mathf.Max(1, m.subMeshCount);
                    for (int sub = 0; sub < subCount; sub++)
                    {
                        list.Add(new CombineInstance
                        {
                            mesh = m,
                            subMeshIndex = sub,
                            transform = combinedMat
                        });
                    }
                }
            }
            else
            {
                int fallbackId = GetOrCreateCityMaterialBaseId("CollapsedBuilding02a") + mistBucket;
                SpawnNatureProp(prefab, cityRoot.transform, batchMaterials[fallbackId], x, z, scale, rotY, castShadows, mist < 0.75f);
            }
        }

        var rng = new System.Random(903140);
        const float minX = -112f;
        const float maxX = 88f;
        const float minZ = -88f;
        const float maxZ = 88f;

        // 1. ARID ROADSIDE SCRUB & DESERT ROCKS (0.85m .. 18m outside the extended highways)
        if (scrubPrefabs.Length > 0 || rockPrefabs.Length > 0)
        {
            const float scrubStep = 3.2f;
            for (float gx = minX; gx <= maxX; gx += scrubStep)
            {
                for (float gz = minZ; gz <= maxZ; gz += scrubStep)
                {
                    float x = gx + ((float)rng.NextDouble() * 2f - 1f) * 1.2f;
                    float z = gz + ((float)rng.NextDouble() * 2f - 1f) * 1.2f;
                    float dist = GetDistanceOutsideExtendedCityCross(x, z);
                    if (dist < 0.90f || dist > 18f) continue;

                    float mist = GetApocalypseEdgeMistAtPosition(new Vector3(x, 0f, z));
                    if (mist >= 0.88f) continue;

                    double roll = rng.NextDouble();
                    if (rockPrefabs.Length > 0 && dist >= 1.5f && roll < 0.22)
                    {
                        GameObject rock = rockPrefabs[rng.Next(rockPrefabs.Length)];
                        float rScale = 0.35f + (float)rng.NextDouble() * 0.45f;
                        QueueCityProp(rock, x, z, rScale, (float)rng.NextDouble() * 360f, mist, castShadows: false);
                    }
                    else if (scrubPrefabs.Length > 0 && roll < 0.62)
                    {
                        GameObject scrub = scrubPrefabs[rng.Next(scrubPrefabs.Length)];
                        float sScale = 0.28f + (float)rng.NextDouble() * 0.22f;
                        QueueCityProp(scrub, x, z, sScale, (float)rng.NextDouble() * 360f, mist, castShadows: false);
                    }
                }
            }
        }

        // 2. WRECKAGE & COLLAPSED RUBBLE RING (3.8m .. 8.2m outside the extended highways)
        if (wreckagePrefabs.Length > 0)
        {
            const float wreckStep = 7.5f;
            for (float wx = -92f; wx <= 76f; wx += wreckStep)
            {
                for (float wz = -76f; wz <= 76f; wz += wreckStep)
                {
                    float x = wx + ((float)rng.NextDouble() * 2f - 1f) * 2.2f;
                    float z = wz + ((float)rng.NextDouble() * 2f - 1f) * 2.2f;
                    float dist = GetDistanceOutsideExtendedCityCross(x, z);
                    if (dist < 4.0f || dist > 8.2f) continue;
                    if ((float)rng.NextDouble() > 0.55f) continue;

                    float mist = GetApocalypseEdgeMistAtPosition(new Vector3(x, 0f, z));
                    if (mist >= 0.92f) continue;

                    GameObject wreck = wreckagePrefabs[rng.Next(wreckagePrefabs.Length)];
                    float wScale = 1.6f + (float)rng.NextDouble() * 0.9f;
                    QueueCityProp(wreck, x, z, wScale, (float)rng.NextDouble() * 360f, mist, castShadows: false);
                }
            }
        }

        // 3. POST-APOCALYPTIC RUINED CITY BLOCKS & SKYSCRAPER SKYLINE (7.2m+ outside the extended highways)
        const float cityClearanceDist = 7.2f;
        const float cityStep = 10.5f;
        float[] cardinalAngles = new float[] { 0f, 90f, 180f, 270f };

        for (float cx = minX; cx <= maxX; cx += cityStep)
        {
            for (float cz = minZ; cz <= maxZ; cz += cityStep)
            {
                float x = cx + ((float)rng.NextDouble() * 2f - 1f) * 2.8f;
                float z = cz + ((float)rng.NextDouble() * 2f - 1f) * 2.8f;

                float dist = GetDistanceOutsideExtendedCityCross(x, z);
                if (dist < cityClearanceDist) continue;

                float mist = GetApocalypseEdgeMistAtPosition(new Vector3(x, 0f, z));
                if (mist >= 0.96f) continue; // Completely inside solid edge fog shroud

                float cityT = Mathf.Clamp01((dist - cityClearanceDist) / 14f);
                float spawnChance = Mathf.Lerp(0.62f, 0.92f, cityT);
                if ((float)rng.NextDouble() > spawnChance) continue;

                GameObject chosenBuilding = null;
                float bScale;

                if (cityT < 0.35f && lowRuinPrefabs.Length > 0)
                {
                    // Inner ruined city perimeter: collapsed shells & bombed city blocks
                    chosenBuilding = lowRuinPrefabs[rng.Next(lowRuinPrefabs.Length)];
                    bScale = Mathf.Lerp(2.4f, 3.6f, cityT / 0.35f) + (float)rng.NextDouble() * 0.5f;
                }
                else if (tallSkyscraperPrefabs.Length > 0)
                {
                    // Outer skyline: towering post-apocalyptic apartment blocks & high-rises
                    chosenBuilding = tallSkyscraperPrefabs[rng.Next(tallSkyscraperPrefabs.Length)];
                    bScale = Mathf.Lerp(3.6f, 5.6f, cityT) + (float)rng.NextDouble() * 0.9f;
                }
                else if (lowRuinPrefabs.Length > 0)
                {
                    chosenBuilding = lowRuinPrefabs[rng.Next(lowRuinPrefabs.Length)];
                    bScale = 3.2f;
                }
                else
                {
                    continue;
                }

                float rotY = cardinalAngles[rng.Next(cardinalAngles.Length)] + ((float)rng.NextDouble() * 2f - 1f) * 8f;
                bool castShadow = dist < 28f && mist < 0.50f;
                QueueCityProp(chosenBuilding, x, z, bScale, rotY, mist, castShadows: castShadow);
            }
        }

        // 4. BAKE ALL CITY CHUNKS INTO COMBINED STATIC MESHES
        foreach (var kvp in chunkBatches)
        {
            var key = kvp.Key;
            var instances = kvp.Value;
            if (instances == null || instances.Count == 0) continue;

            Mesh combinedMesh = TrackGeneratedAsset(new Mesh());
            combinedMesh.name = $"ApocalypseCityChunk_{key.chunkX}_{key.chunkZ}_M{key.matId}";
            combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            combinedMesh.CombineMeshes(instances.ToArray(), true, true, false);
            combinedMesh.RecalculateBounds();
            if (Application.isPlaying) combinedMesh.UploadMeshData(true);

            GameObject chunkObj = new GameObject(combinedMesh.name);
            chunkObj.transform.SetParent(cityRoot.transform, false);
            chunkObj.isStatic = true;

            var mf = chunkObj.AddComponent<MeshFilter>();
            mf.sharedMesh = combinedMesh;

            var mr = chunkObj.AddComponent<MeshRenderer>();
            mr.sharedMaterial = batchMaterials[key.matId];
            mr.shadowCastingMode = key.castShadows
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = key.receiveShadows;
        }

        // 5. BURNING CRIMSON-AMBER APOCALYPSE SUNSET SKY BACKDROP ON THE HORIZON
        BuildApocalypseSunsetSkyBackdrop(cityRoot.transform);
    }

    /// <summary>
    /// Creates a panoramic procedural burning crimson-orange sunset sky backdrop along the North, West, and East horizons
    /// matching the dramatic red-orange clouds & golden horizon glow of the Post-Apocalypse reference artwork.
    /// </summary>
    private void BuildApocalypseSunsetSkyBackdrop(Transform parent)
    {
        const int skyW = 256;
        const int skyH = 128;
        Texture2D skyTex = TrackGeneratedAsset(new Texture2D(skyW, skyH, TextureFormat.RGBA32, false));
        skyTex.name = "Tex_ApocalypseCrimsonSunsetSky";
        skyTex.wrapMode = TextureWrapMode.Clamp;
        skyTex.filterMode = FilterMode.Bilinear;

        Color zenithDarkCrimson = new Color(0.12f, 0.03f, 0.01f, 1f);
        Color upperBloodRed     = new Color(0.54f, 0.12f, 0.03f, 1f);
        Color midFieryOrange    = new Color(0.88f, 0.34f, 0.07f, 1f);
        Color lowerGoldenAmber  = new Color(1.00f, 0.72f, 0.28f, 1f);
        Color horizonDustGlow   = new Color(0.95f, 0.82f, 0.52f, 1f);

        Color[] pixels = new Color[skyW * skyH];
        for (int y = 0; y < skyH; y++)
        {
            float v = (float)y / (skyH - 1); // 0 = bottom horizon, 1 = top zenith
            for (int x = 0; x < skyW; x++)
            {
                float u = (float)x / (skyW - 1);
                float cloudNoise = SampleFbmNoise(u * 14f, v * 8f, 1.0f);

                Color grad;
                if (v < 0.18f)
                    grad = Color.Lerp(horizonDustGlow, lowerGoldenAmber, v / 0.18f);
                else if (v < 0.48f)
                    grad = Color.Lerp(lowerGoldenAmber, midFieryOrange, (v - 0.18f) / 0.30f);
                else if (v < 0.78f)
                    grad = Color.Lerp(midFieryOrange, upperBloodRed, (v - 0.48f) / 0.30f);
                else
                    grad = Color.Lerp(upperBloodRed, zenithDarkCrimson, (v - 0.78f) / 0.22f);

                // Dramatic storm cloud streaks across the burning sky
                Color cloudTint = Color.Lerp(zenithDarkCrimson, lowerGoldenAmber, Mathf.Clamp01(1f - v * 0.85f));
                Color finalSky = Color.Lerp(grad, cloudTint, (cloudNoise - 0.45f) * 0.55f);

                // Bright sun haze concentration near center-right horizon
                float sunDx = (u - 0.62f) * 1.6f;
                float sunDy = (v - 0.12f) * 2.4f;
                float sunGlow = Mathf.Exp(-(sunDx * sunDx + sunDy * sunDy) * 4.5f);
                finalSky = Color.Lerp(finalSky, new Color(1.0f, 0.92f, 0.68f, 1f), sunGlow * 0.65f);

                pixels[y * skyW + x] = finalSky;
            }
        }
        skyTex.SetPixels(pixels);
        skyTex.Apply(false, true);

        Shader unlitSh = Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Unlit/Texture") ??
                         GetShader();
        Material skyMat = TrackGeneratedAsset(new Material(unlitSh));
        skyMat.name = "Mat_ApocalypseSunsetSkyBackdrop";
        if (skyMat.HasProperty("_BaseMap")) skyMat.SetTexture("_BaseMap", skyTex);
        if (skyMat.HasProperty("_MainTex")) skyMat.SetTexture("_MainTex", skyTex);
        if (skyMat.HasProperty("_BaseColor")) skyMat.SetColor("_BaseColor", Color.white);
        skyMat.color = Color.white;

        // Place distant vertical sky panels along North, East, and West horizons so tilted 50° camera sees burning clouds behind the skyline
        void CreateSkyWall(string wallName, Vector3 pos, Vector3 rotEuler, Vector3 scale)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Quad);
            wall.name = wallName;
            wall.transform.SetParent(parent, false);
            wall.transform.position = pos;
            wall.transform.rotation = Quaternion.Euler(rotEuler);
            wall.transform.localScale = scale;
            DestroyColliderSafe(wall);
            var mr = wall.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = skyMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
        }

        CreateSkyWall("ApocalypseSky_NorthHorizon", new Vector3(-15f, 48f, 108f), new Vector3(0f, 0f, 0f), new Vector3(320f, 104f, 1f));
        CreateSkyWall("ApocalypseSky_EastHorizon",  new Vector3(112f, 48f, 0f),   new Vector3(0f, 90f, 0f), new Vector3(260f, 104f, 1f));
        CreateSkyWall("ApocalypseSky_WestHorizon",  new Vector3(-128f, 48f, 0f), new Vector3(0f, -90f, 0f), new Vector3(260f, 104f, 1f));
    }

    private void BuildFlatLockOverlays()
    {
        // North Locked Overlay (flat hazard strip at entrance only — no solid dark box so ground blends seamlessly!)
        northGate = new GameObject("LockedOverlay_NorthSector");
        northGate.transform.SetParent(rootCross.transform, false);
        northGate.transform.position = new Vector3(0f, 0.07f, 30f);

        var nLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
        nLine.name = "NorthLockedHazardLine";
        nLine.transform.SetParent(northGate.transform, false);
        nLine.transform.localPosition = new Vector3(0f, 0.015f, -14.7f);
        nLine.transform.localScale = new Vector3(30f, 0.02f, 0.6f);
        nLine.GetComponent<Renderer>().sharedMaterial = MakeMat(new Color(0.9f, 0.45f, 0.05f));
        DestroyColliderSafe(nLine);

        // South Locked Overlay (flat hazard strip at entrance only — no solid dark box so ground blends seamlessly!)
        southGate = new GameObject("LockedOverlay_SouthSector");
        southGate.transform.SetParent(rootCross.transform, false);
        southGate.transform.position = new Vector3(0f, 0.07f, -30f);

        var sLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sLine.name = "SouthLockedHazardLine";
        sLine.transform.SetParent(southGate.transform, false);
        sLine.transform.localPosition = new Vector3(0f, 0.015f, 14.7f);
        sLine.transform.localScale = new Vector3(30f, 0.02f, 0.6f);
        sLine.GetComponent<Renderer>().sharedMaterial = MakeMat(new Color(0.9f, 0.45f, 0.05f));
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
    /// Computes the Stage 31+ Post-Apocalypse edge fog/mist factor (0.0 across the base, frontline, crossroads,
    /// and inner ruined city; ramping up smoothly across the outer highway perimeters & zombie spawn horizons into 1.0 dense dust mist).
    /// Replaces the nighttime darkness shroud at the edges of the map!
    /// </summary>
    public static float GetApocalypseEdgeMistAtPosition(Vector3 pos)
    {
        float wx = pos.x;
        float wz = pos.z;
        float absZ = Mathf.Abs(wz);

        // 1. East & West outer horizon mist (keeps all buildings around the base & inner city 100% clear)
        float mX = 0f;
        if (wx < -86f)
        {
            mX = Mathf.SmoothStep(0f, 1.0f, Mathf.Clamp01((-86f - wx) / 30f));
        }
        else if (wx > 38f)
        {
            // Smoothly thickens from X=38 across East Zombie Spawn (X=43.5) -> 1.0 dense dust fog at X=70+
            mX = Mathf.SmoothStep(0f, 1.0f, Mathf.Clamp01((wx - 38f) / 32f));
        }

        // 2. North & South outer horizon mist
        float mZ = 0f;
        if (absZ > 38f)
        {
            // Smoothly thickens from |Z|=38 across North/South Zombie Spawns (|Z|=43.5) -> 1.0 dense dust fog at |Z|=70+
            mZ = Mathf.SmoothStep(0f, 1.0f, Mathf.Clamp01((absZ - 38f) / 32f));
        }

        return Mathf.Max(mX, mZ);
    }

    /// <summary>
    /// Computes static darkness factor (0.0 = full light at base, ~0.25-0.45 = dim twilight at center junction, 1.0 = pitch black in U-shaped zombie spawn arms).
    /// </summary>
    public static float GetStaticDarknessAtPosition(Vector3 pos)
    {
        // Stage 31+ Post-Apocalypse Ruined City theme removes all darkness shroud completely!
        if (IsApocalypseCityTheme())
        {
            return 0f;
        }

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

            if (wx <= -24f)
            {
                dZ = (absZ > 14.5f) ? dSideBase : 0f;
            }
            else if (wx >= 4f)
            {
                dZ = dSideZombie;
            }
            else
            {
                float tBlend = Mathf.SmoothStep(0f, 1f, (wx - (-24f)) / 28f);
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

        bool isCityMist = IsApocalypseCityTheme();
        int width = 512;
        int height = 512;
        _darknessTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        _darknessTexture.name = isCityMist ? "Tex_ApocalypseEdgeMist_FullMap" : "Tex_DarknessShroud_FullMap";
        _darknessTexture.wrapMode = TextureWrapMode.Clamp;
        _darknessTexture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[width * height];
        Color mistLight = new Color(0.64f, 0.36f, 0.18f, 1f);
        Color mistDeep  = ApocalypseEdgeMistColor;

        for (int py = 0; py < height; py++)
        {
            float v = (float)py / (height - 1);
            float wz = Mathf.Lerp(minZ, maxZ, v);

            for (int px = 0; px < width; px++)
            {
                float u = (float)px / (width - 1);
                float wx = Mathf.Lerp(minX, maxX, u);

                if (isCityMist)
                {
                    float m = GetApocalypseEdgeMistAtPosition(new Vector3(wx, 0f, wz));
                    float n = SampleFbmNoise(wx, wz, 0.045f);
                    // Wavy cloud edge so the fog bank rolls organically across the roads and outer ruins
                    float noisyMist = Mathf.Clamp01(m + (n - 0.48f) * 0.22f * Mathf.Sin(m * Mathf.PI));
                    float alpha = Mathf.Clamp01(noisyMist * 0.96f);
                    Color c = Color.Lerp(mistLight, mistDeep, Mathf.Clamp01(noisyMist * 0.85f + (1f - n) * 0.25f));
                    pixels[py * width + px] = new Color(c.r, c.g, c.b, alpha);
                }
                else
                {
                    float d = GetStaticDarknessAtPosition(new Vector3(wx, 0f, wz));
                    float alpha = Mathf.Clamp01(d * 0.985f);
                    pixels[py * width + px] = new Color(0.015f, 0.02f, 0.03f, alpha);
                }
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

        var darknessRoot = new GameObject(_isApocalypseThemeActive ? "ApocalypseEdgeMist_Atmosphere_FullMap" : "Darkness_Atmosphere_FullMap");
        darknessRoot.transform.SetParent(rootCross.transform, false);

        Texture2D tex = TrackGeneratedAsset(GetOrCreateDarknessTexture(MAP_MIN_X, MAP_MAX_X, MAP_MIN_Z, MAP_MAX_Z));
        Material shroudMat = TrackGeneratedAsset(CreateDarknessMaterial(tex));
        Mesh quadMesh = TrackGeneratedAsset(GetOrCreateProceduralQuadMesh(MAP_MIN_X, MAP_MAX_X, MAP_MIN_Z, MAP_MAX_Z));

        // 1. Ground Shadow / Dust Mist Plane (Y = 0.062f, right above terrain)
        CreateDarknessPlane(_isApocalypseThemeActive ? "ApocalypseMist_GroundFog_Plane" : "Darkness_GroundShadow_Plane",
            0.062f, shroudMat, darknessRoot.transform, quadMesh);

        // 2. Overhead Shroud / Swirling Fog Plane (Y = 2.35f, above zombie and soldier heads)
        var overheadShroud = CreateDarknessPlane(_isApocalypseThemeActive ? "ApocalypseMist_OverheadFog_Plane" : "Darkness_OverheadShroud_Plane",
            2.35f, shroudMat, darknessRoot.transform, quadMesh);
        overheadShroud.AddComponent<DarknessShroudAnimator>();
    }

    private void BuildBastionDefenseLighting()
    {
        var lightsRoot = new GameObject("Bastion_Defense_Lighting");
        lightsRoot.transform.SetParent(rootCross.transform, false);

        if (_isApocalypseThemeActive)
        {
            // STAGE 31+ POST-APOCALYPSE RUINED CITY SUNSET LIGHTING (Warm fiery crimson-amber dusk sun, zero darkness!)
            Color sunsetFillCol = new Color(1.0f, 0.72f, 0.44f);
            Color horizonGlowCol = new Color(1.0f, 0.56f, 0.28f);

            void AddApocalypseFillLight(string name, Vector3 pos, Color col, float intensity, float range)
            {
                var lObj = new GameObject(name);
                lObj.transform.SetParent(lightsRoot.transform, false);
                lObj.transform.position = pos;
                var l = lObj.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = col;
                l.intensity = intensity;
                l.range = range;
            }

            AddApocalypseFillLight("SunsetLight_BaseWest",     new Vector3(-52f, 14f, 0f), sunsetFillCol,  3.8f, 65f);
            AddApocalypseFillLight("SunsetLight_CenterCross",  new Vector3(-5f,  15f, 0f), sunsetFillCol,  3.6f, 68f);
            AddApocalypseFillLight("SunsetLight_EastHighway",  new Vector3(32f,  16f, 0f), horizonGlowCol, 4.0f, 72f);
            AddApocalypseFillLight("SunsetLight_NorthSkyline", new Vector3(-10f, 18f, 38f), horizonGlowCol, 3.5f, 75f);
            AddApocalypseFillLight("SunsetLight_SouthSkyline", new Vector3(-10f, 18f, -38f), sunsetFillCol, 3.2f, 75f);

            var allLights = FindObjectsByType<Light>();
            foreach (var l in allLights)
            {
                if (l.type == LightType.Directional)
                {
                    l.intensity = 1.18f;
                    l.color = new Color(1.0f, 0.72f, 0.42f);
                }
            }

            RenderSettings.ambientLight = new Color(0.45f, 0.28f, 0.18f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.42f, 0.19f, 0.09f);
            RenderSettings.fogDensity = 0.0009f; // Compensated for cameraHeight=120m orthographic distance

            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.36f, 0.13f, 0.05f);
            }
            return;
        }

        // STAGE 1-30 FOREST NIGHT LIGHTING
        // 1. Main Base Central Floodlight (covers full 15x15 base including rear)
        var l1Obj = new GameObject("Floodlight_BaseMain");
        l1Obj.transform.SetParent(lightsRoot.transform, false);
        l1Obj.transform.position = new Vector3(-53f, 11.5f, 0f);
        var l1 = l1Obj.AddComponent<Light>();
        l1.type = LightType.Point;
        l1.color = new Color(1.0f, 0.94f, 0.86f);
        l1.intensity = 5.2f;
        l1.range = 52f;

        // 2. Frontline Corridor Light (covers frontline & threshold)
        var l3Obj = new GameObject("Floodlight_Frontline");
        l3Obj.transform.SetParent(lightsRoot.transform, false);
        l3Obj.transform.position = new Vector3(-26f, 9.5f, 0f);
        var l3 = l3Obj.AddComponent<Light>();
        l3.type = LightType.Point;
        l3.color = new Color(0.96f, 0.94f, 0.92f);
        l3.intensity = 4.2f;
        l3.range = 42f;

        // 3. Center Junction Ambient Light (Softly illuminates the center crossroads so mid-map is visible)
        var lCenterObj = new GameObject("Floodlight_CenterJunction");
        lCenterObj.transform.SetParent(lightsRoot.transform, false);
        lCenterObj.transform.position = new Vector3(-2f, 10.5f, 0f);
        var lCenter = lCenterObj.AddComponent<Light>();
        lCenter.type = LightType.Point;
        lCenter.color = new Color(0.95f, 0.87f, 0.74f);
        lCenter.intensity = 2.6f;
        lCenter.range = 36f;

        // 4. Flank Ambient Lights (subtly illuminates the grass meadow and surrounding forest edge beside & behind the base)
        var lNorthObj = new GameObject("Floodlight_FlankNorth");
        lNorthObj.transform.SetParent(lightsRoot.transform, false);
        lNorthObj.transform.position = new Vector3(-48f, 10f, 26f);
        var lNorth = lNorthObj.AddComponent<Light>();
        lNorth.type = LightType.Point;
        lNorth.color = new Color(0.75f, 0.86f, 0.95f);
        lNorth.intensity = 2.4f;
        lNorth.range = 48f;

        var lSouthObj = new GameObject("Floodlight_FlankSouth");
        lSouthObj.transform.SetParent(lightsRoot.transform, false);
        lSouthObj.transform.position = new Vector3(-48f, 10f, -26f);
        var lSouth = lSouthObj.AddComponent<Light>();
        lSouth.type = LightType.Point;
        lSouth.color = new Color(0.75f, 0.86f, 0.95f);
        lSouth.intensity = 2.4f;
        lSouth.range = 48f;

        var lWestObj = new GameObject("Floodlight_ForestWest");
        lWestObj.transform.SetParent(lightsRoot.transform, false);
        lWestObj.transform.position = new Vector3(-82f, 11f, 0f);
        var lWest = lWestObj.AddComponent<Light>();
        lWest.type = LightType.Point;
        lWest.color = new Color(0.75f, 0.86f, 0.95f);
        lWest.intensity = 2.4f;
        lWest.range = 48f;

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

        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.07f, 0.09f, 0.11f);
        RenderSettings.fogDensity = 0.0011f; // Compensated for cameraHeight=120m orthographic distance

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

        // Inner bright ground core glow disc (kept at ground level Y=0.17f so it never slices through tall units like Tyrant Boss)
        GameObject overheadGlow = GameObject.CreatePrimitive(PrimitiveType.Quad);
        overheadGlow.name = "Blast_InnerCoreLightGlow";
        overheadGlow.transform.SetParent(fxRoot.transform, false);
        overheadGlow.transform.localPosition = new Vector3(0f, 0.17f, 0f);
        overheadGlow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        overheadGlow.transform.localScale = new Vector3(glowDiameter * 0.62f, glowDiameter * 0.62f, 1f);
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
            float d = _initialDiameter * 0.62f * scaleMult;
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
