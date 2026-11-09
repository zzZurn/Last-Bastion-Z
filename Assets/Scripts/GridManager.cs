using UnityEngine;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("Cross Shape Grid Settings (55x45 Cells = 110m x 90m)")]
    public int gridWidth = 55;  // 15 (Base) + 10 (Left Corridor) + 15 (Center) + 15 (Right Head) = 55 cells (110m)
    public int gridHeight = 45; // 15 (South Arm) + 15 (Center) + 15 (North Arm) = 45 cells (90m)
    public float cellSize = 2f;
    public Vector3 gridOrigin = new Vector3(-65f, 0.1f, -45f);

    // Main horizontal corridor (15 cells wide: Y = 15..29, Z = -15m to +15m)
    public int corridorYMin = 15;
    public int corridorYMax = 29;

    // Vertical branches at Center Junction (15 cells wide: X = 25..39, World X = -15m to +15m)
    public int branchXMin = 25;
    public int branchXMax = 39;

    private bool[,] occupied;
    private Dictionary<Vector2Int, Building> buildings = new Dictionary<Vector2Int, Building>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Enforce 55x45 Cross Map dimensions (Base 15x15 at inner left + 10x15 corridor)
        gridWidth = 55;
        gridHeight = 45;
        cellSize = 2f;
        gridOrigin = new Vector3(-65f, 0.1f, -45f);
        corridorYMin = 15;
        corridorYMax = 29;
        branchXMin = 25;
        branchXMax = 39;

        occupied = new bool[gridWidth, gridHeight];

        if (GetComponent<CrossMapVisualizer>() == null)
        {
            gameObject.AddComponent<CrossMapVisualizer>();
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public Vector3 GridToWorld(int x, int y)
    {
        return gridOrigin + new Vector3(x * cellSize + cellSize / 2f, 0, y * cellSize + cellSize / 2f);
    }

    public Vector3 GetBuildingWorldCenter(int x, int y, Building.BuildingType type)
    {
        Vector2Int size = Building.GetFootprintSize(type);
        return gridOrigin + new Vector3((x + size.x * 0.5f) * cellSize, 0f, (y + size.y * 0.5f) * cellSize);
    }

    public bool CanPlaceBuildingAt(int x, int y, Building.BuildingType type)
    {
        Vector2Int size = Building.GetFootprintSize(type);
        for (int dx = 0; dx < size.x; dx++)
        {
            for (int dy = 0; dy < size.y; dy++)
            {
                if (!IsValidCell(x + dx, y + dy, type)) return false;
            }
        }
        return true;
    }

    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        int x = Mathf.FloorToInt((worldPos.x - gridOrigin.x) / cellSize);
        int y = Mathf.FloorToInt((worldPos.z - gridOrigin.z) / cellSize);
        return new Vector2Int(x, y);
    }

    public bool IsWithinBounds(int x, int y)
    {
        return x >= 0 && x < gridWidth && y >= 0 && y < gridHeight;
    }

    /// <summary>
    /// Checks whether this grid cell falls inside the Cross combat area.
    /// - Human Base (15x15): x: 0..14, y: 15..29 (World X: -65 to -35, Z: -15 to +15)
    /// - Frontline Fortification Zone (10x15): x: 15..24, y: 15..29 (World X: -35 to -15, Z: -15 to +15)
    /// - Center Junction (15x15): x: 25..39, y: 15..29 (World X: -15 to +15, Z: -15 to +15)
    /// - Right Head (15x15): x: 40..54, y: 15..29 (World X: +15 to +45, Z: -15 to +15)
    /// - North Arm (15x15): x: 25..39, y: 30..44 (World X: -15 to +15, Z: +15 to +45)
    /// - South Arm (15x15): x: 25..39, y: 0..14 (World X: -15 to +15, Z: -45 to -15)
    /// </summary>
    public bool IsCellInCross(int x, int y)
    {
        if (!IsWithinBounds(x, y)) return false;
        if (y >= corridorYMin && y <= corridorYMax) return true;
        if (x >= branchXMin && x <= branchXMax) return true;
        return false;
    }

    /// <summary>
    /// Returns true if this cell is inside the 15x15 buildable Human Base zone (x: 0..14, y: 15..29).
    /// </summary>
    public bool IsHumanBaseZone(int x, int y)
    {
        return x >= 0 && x < 15 && y >= corridorYMin && y <= corridorYMax;
    }

    /// <summary>
    /// Returns true if this cell is inside the 10x15 Frontline Fortification Zone (x: 15..24, y: 15..29)
    /// extending from the Human Base up to the Crossroads Junction.
    /// Only Frontline Fortifications (Barricade & future turrets) can be placed here; unit-spawning bases cannot.
    /// </summary>
    public bool IsFrontlineFortificationZone(int x, int y)
    {
        return x >= 15 && x < branchXMin && y >= corridorYMin && y <= corridorYMax;
    }

    /// <summary>
    /// Returns true if this cell is located in the North arm or South arm.
    /// </summary>
    public bool IsNorthSouthBranch(int x, int y)
    {
        if (x >= branchXMin && x <= branchXMax)
        {
            if (y < corridorYMin || y > corridorYMax) return true;
        }
        return false;
    }

    /// <summary>
    /// North and South arms unlock only after completing Stage 15 (i.e. Stage 16+).
    /// </summary>
    public bool IsBranchUnlocked()
    {
        if (GameManager.Instance != null && GameManager.Instance.isDevUnconditionalMode) return true;
        return GameManager.Instance != null && GameManager.Instance.CurrentDay > 15;
    }

    /// <summary>
    /// Returns true if this cell is in the North/South branch and currently locked.
    /// </summary>
    public bool IsCellLocked(int x, int y)
    {
        return IsCellInCross(x, y) && IsNorthSouthBranch(x, y) && !IsBranchUnlocked();
    }

    public bool IsCellOccupied(int x, int y)
    {
        return IsWithinBounds(x, y) && occupied[x, y];
    }

    public Building GetBuilding(int x, int y)
    {
        if (buildings.TryGetValue(new Vector2Int(x, y), out Building b))
        {
            return b;
        }
        return null;
    }

    public bool IsValidCell(int x, int y, Building.BuildingType type = Building.BuildingType.SMGTent)
    {
        if (!IsCellInCross(x, y)) return false;

        // Dev Tools Unconditional Mode: Allow placing anywhere inside the Cross map without conditions!
        if (GameManager.Instance != null && GameManager.Instance.isDevUnconditionalMode)
        {
            return !occupied[x, y];
        }

        if (IsCellLocked(x, y)) return false;

        // Buildable areas:
        // 1. The 15x15 Human Base (x: 0..14, y: 15..29) - allows both Unit Spawners and Fortifications
        if (IsHumanBaseZone(x, y))
        {
            return !occupied[x, y];
        }

        // 2. The 10x15 Frontline Fortification Zone up to the Crossroads (x: 15..24, y: 15..29)
        //    Only allows Frontline Fortifications (e.g. Barricade and future turrets), NOT unit-spawning bases!
        if (IsFrontlineFortificationZone(x, y))
        {
            if (Building.IsFrontlineFortification(type))
            {
                return !occupied[x, y];
            }
            return false;
        }

        // 3. North & South Arms once unlocked after Stage 15 (excluding the 1-cell spawn edge)
        if (IsNorthSouthBranch(x, y) && IsBranchUnlocked())
        {
            if (y >= gridHeight - 1 || y <= 0) return false;
            return !occupied[x, y];
        }

        return false;
    }

    public bool PlaceBuilding(int x, int y, Building building)
    {
        if (!CanPlaceBuildingAt(x, y, building.type)) return false;

        Vector2Int size = Building.GetFootprintSize(building.type);
        for (int dx = 0; dx < size.x; dx++)
        {
            for (int dy = 0; dy < size.y; dy++)
            {
                occupied[x + dx, y + dy] = true;
                buildings[new Vector2Int(x + dx, y + dy)] = building;
            }
        }
        building.transform.position = GetBuildingWorldCenter(x, y, building.type);
        return true;
    }

    public void RemoveBuilding(int x, int y)
    {
        Building b = GetBuilding(x, y);
        if (b != null)
        {
            Vector2Int size = Building.GetFootprintSize(b.type);
            for (int dx = 0; dx < size.x; dx++)
            {
                for (int dy = 0; dy < size.y; dy++)
                {
                    int cx = b.gridX + dx;
                    int cy = b.gridY + dy;
                    if (IsWithinBounds(cx, cy))
                    {
                        occupied[cx, cy] = false;
                        buildings.Remove(new Vector2Int(cx, cy));
                    }
                }
            }
        }
        else if (IsWithinBounds(x, y))
        {
            occupied[x, y] = false;
            buildings.Remove(new Vector2Int(x, y));
        }
    }

    public List<Building> GetAllBuildings()
    {
        HashSet<Building> unique = new HashSet<Building>(buildings.Values);
        return new List<Building>(unique);
    }

    public void ClearAll()
    {
        occupied = new bool[gridWidth, gridHeight];
        buildings.Clear();
    }

    public void ClearAllBuildings()
    {
        ClearAll();
    }

    void OnDrawGizmos()
    {
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                if (!IsCellInCross(x, y)) continue;

                Vector3 center = gridOrigin + new Vector3(x * cellSize + cellSize / 2f, 0, y * cellSize + cellSize / 2f);
                if (IsCellLocked(x, y))
                {
                    Gizmos.color = new Color(1f, 0.45f, 0f, 0.35f); // Orange locked North/South arm
                }
                else if (IsHumanBaseZone(x, y))
                {
                    Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.4f); // Green 15x15 Buildable Human Base
                }
                else if (IsFrontlineFortificationZone(x, y))
                {
                    Gizmos.color = new Color(0.95f, 0.72f, 0.2f, 0.38f); // Amber 10x15 Frontline Fortification Zone
                }
                else
                {
                    Gizmos.color = new Color(0.7f, 0.7f, 0.7f, 0.18f); // Combat corridor road
                }
                Gizmos.DrawWireCube(center, new Vector3(cellSize, 0.1f, cellSize));
            }
        }
    }
}
