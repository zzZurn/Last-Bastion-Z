using UnityEngine;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("Grid Settings")]
    public int gridWidth = 10;
    public int gridHeight = 12;
    public float cellSize = 2f;
    public Vector3 gridOrigin = new Vector3(-25f, 0.1f, -12f);

    private bool[,] occupied;
    private Dictionary<Vector2Int, Building> buildings = new Dictionary<Vector2Int, Building>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        occupied = new bool[gridWidth, gridHeight];
    }

    public Vector3 GridToWorld(int x, int y)
    {
        return gridOrigin + new Vector3(x * cellSize + cellSize / 2f, 0, y * cellSize + cellSize / 2f);
    }

    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        int x = Mathf.FloorToInt((worldPos.x - gridOrigin.x) / cellSize);
        int y = Mathf.FloorToInt((worldPos.z - gridOrigin.z) / cellSize);
        return new Vector2Int(x, y);
    }

    public bool IsValidCell(int x, int y)
    {
        return x >= 0 && x < gridWidth && y >= 0 && y < gridHeight && !occupied[x, y];
    }

    public bool PlaceBuilding(int x, int y, Building building)
    {
        if (!IsValidCell(x, y)) return false;
        occupied[x, y] = true;
        buildings[new Vector2Int(x, y)] = building;
        building.transform.position = GridToWorld(x, y);
        return true;
    }

    public void RemoveBuilding(int x, int y)
    {
        if (x >= 0 && x < gridWidth && y >= 0 && y < gridHeight)
        {
            occupied[x, y] = false;
            buildings.Remove(new Vector2Int(x, y));
        }
    }

    public List<Building> GetAllBuildings()
    {
        return new List<Building>(buildings.Values);
    }

    public void ClearAll()
    {
        occupied = new bool[gridWidth, gridHeight];
        buildings.Clear();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                Vector3 center = gridOrigin + new Vector3(x * cellSize + cellSize / 2f, 0, y * cellSize + cellSize / 2f);
                Gizmos.DrawWireCube(center, new Vector3(cellSize, 0.1f, cellSize));
            }
        }
    }
}
