using UnityEngine;
using System.Collections.Generic;

public class PlacementManager : MonoBehaviour
{
    public static PlacementManager Instance { get; private set; }

    [System.Serializable]
    public struct SavedBuildingSlot
    {
        public int gridX;
        public int gridY;
        public Building.BuildingType type;

        public SavedBuildingSlot(int x, int y, Building.BuildingType t)
        {
            gridX = x;
            gridY = y;
            type = t;
        }
    }

    [Header("Placement State")]
    public Building.BuildingType selectedType = Building.BuildingType.SMGTent;
    public bool isPlacing = false;

    // Blueprint of the previous day's base layout for instant 1-click rebuild!
    public List<SavedBuildingSlot> lastSavedLayout = new List<SavedBuildingSlot>();

    // Stored/Recovered buildings from previous days
    private Dictionary<Building.BuildingType, int> storedBuildingCounts = new Dictionary<Building.BuildingType, int>()
    {
        { Building.BuildingType.SMGTent, 0 },
        { Building.BuildingType.SniperTower, 0 },
        { Building.BuildingType.Barricade, 0 },
        { Building.BuildingType.HealStation, 0 }
    };

    private GameObject previewObject;
    private Camera mainCam;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        mainCam = Camera.main;
        CreatePreviewObject();
    }

    public int GetStoredCount(Building.BuildingType type)
    {
        if (storedBuildingCounts.TryGetValue(type, out int count))
        {
            return count;
        }
        return 0;
    }

    public void AddStoredCount(Building.BuildingType type, int amount = 1)
    {
        if (!storedBuildingCounts.ContainsKey(type))
        {
            storedBuildingCounts[type] = 0;
        }
        storedBuildingCounts[type] += amount;
        Debug.Log($"[PlacementManager] Stored {type} count is now: {storedBuildingCounts[type]}");
    }

    /// <summary>
    /// Saves the current battlefield base layout before clearing for the next day.
    /// </summary>
    public void SaveCurrentLayout()
    {
        lastSavedLayout.Clear();
        if (GridManager.Instance != null)
        {
            var buildings = GridManager.Instance.GetAllBuildings();
            foreach (var b in buildings)
            {
                if (b != null)
                {
                    lastSavedLayout.Add(new SavedBuildingSlot(b.gridX, b.gridY, b.type));
                }
            }
        }
        Debug.Log($"[PlacementManager] Saved base blueprint with {lastSavedLayout.Count} buildings.");
    }

    public bool HasSavedLayout()
    {
        return lastSavedLayout != null && lastSavedLayout.Count > 0;
    }

    public int GetSavedLayoutCount()
    {
        return lastSavedLayout != null ? lastSavedLayout.Count : 0;
    }

    /// <summary>
    /// 1-Click Instant Base Rebuild: recreates the previous day's layout using stored buildings or affordable purchase.
    /// </summary>
    public int RestorePreviousLayout()
    {
        if (lastSavedLayout == null || lastSavedLayout.Count == 0)
        {
            Debug.LogWarning("[PlacementManager] No previous layout saved to restore!");
            return 0;
        }

        int placedCount = 0;
        foreach (var slot in lastSavedLayout)
        {
            if (GridManager.Instance != null && GridManager.Instance.IsValidCell(slot.gridX, slot.gridY))
            {
                selectedType = slot.type;
                int stored = GetStoredCount(selectedType);
                int goldCost = stored > 0 ? 0 : Building.GetGoldCost(selectedType);
                int matCost = stored > 0 ? 0 : Building.GetMaterialCost(selectedType);

                // Place if owned in stock OR can afford to buy
                if (stored > 0 || (ResourceManager.Instance.Gold >= goldCost && ResourceManager.Instance.Materials >= matCost))
                {
                    TryPlaceBuilding(slot.gridX, slot.gridY);
                    placedCount++;
                }
            }
        }

        Debug.Log($"[PlacementManager] Successfully restored {placedCount}/{lastSavedLayout.Count} buildings from previous layout!");
        return placedCount;
    }

    void CreatePreviewObject()
    {
        previewObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        previewObject.name = "PlacementPreview";

        // Remove collider from preview so it doesn't block raycast
        Collider c = previewObject.GetComponent<Collider>();
        if (c != null) Destroy(c);

        Renderer rend = previewObject.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material.color = new Color(0.2f, 1f, 0.2f, 0.5f);
        }

        UpdatePreviewAppearance();
        previewObject.SetActive(false);
    }

    void UpdatePreviewAppearance()
    {
        if (previewObject == null) return;

        // Distinct shape matching the selected building type
        switch (selectedType)
        {
            case Building.BuildingType.SMGTent:
                previewObject.transform.localScale = new Vector3(1.8f, 1.4f, 1.8f);
                break;
            case Building.BuildingType.SniperTower:
                previewObject.transform.localScale = new Vector3(1.4f, 2.5f, 1.4f); // Taller sniper tower
                break;
            case Building.BuildingType.Barricade:
                previewObject.transform.localScale = new Vector3(1.8f, 0.8f, 1.8f); // Lower wall barricade
                break;
            case Building.BuildingType.HealStation:
                previewObject.transform.localScale = new Vector3(1.6f, 1.4f, 1.6f);
                break;
        }
    }

    void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameManager.GameState.Planning)
        {
            if (previewObject != null && previewObject.activeSelf) previewObject.SetActive(false);
            return;
        }

        // Prevent placing while mouse is over UI
        if (UIManager.Instance != null && UIManager.Instance.IsPointerOverUI())
        {
            if (previewObject != null && previewObject.activeSelf)
            {
                previewObject.SetActive(false);
            }
            return;
        }

        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null) return;

        // Raycast from mouse to ground plane (y=0)
        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

        if (groundPlane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            Vector2Int cell = GridManager.Instance.WorldToGrid(hitPoint);
            bool isValid = GridManager.Instance.IsValidCell(cell.x, cell.y);

            if (isPlacing)
            {
                if (previewObject != null)
                {
                    previewObject.SetActive(true);
                    float previewYOffset = previewObject.transform.localScale.y / 2f;
                    previewObject.transform.position = GridManager.Instance.GridToWorld(cell.x, cell.y) + Vector3.up * previewYOffset;

                    Renderer rend = previewObject.GetComponent<Renderer>();
                    if (rend != null)
                    {
                        rend.material.color = isValid ? new Color(0.2f, 1f, 0.2f, 0.6f) : new Color(1f, 0.2f, 0.2f, 0.6f);
                    }
                }

                // Place on click ONLY when not over UI
                if (Input.GetMouseButtonDown(0))
                {
                    TryPlaceBuilding(cell.x, cell.y);
                }

                // Cancel on right click
                if (Input.GetMouseButtonDown(1))
                {
                    CancelPlacement();
                }
            }
        }
        else
        {
            if (previewObject != null) previewObject.SetActive(false);
        }
    }

    public void CancelPlacement()
    {
        isPlacing = false;
        if (previewObject != null) previewObject.SetActive(false);
    }

    public void SelectBuildingToPlace(Building.BuildingType type)
    {
        selectedType = type;
        isPlacing = true;
        UpdatePreviewAppearance();
        Debug.Log($"[PlacementManager] Switched selected building to: {selectedType}");
    }

    public void TryPlaceBuilding(int x, int y)
    {
        if (!GridManager.Instance.IsValidCell(x, y))
        {
            Debug.LogWarning("[Placement] Invalid cell or already occupied!");
            return;
        }

        int stored = GetStoredCount(selectedType);
        bool useStock = stored > 0;

        int goldCost = useStock ? 0 : Building.GetGoldCost(selectedType);
        int matCost = useStock ? 0 : Building.GetMaterialCost(selectedType);

        if (!useStock)
        {
            if (ResourceManager.Instance.Gold < goldCost || ResourceManager.Instance.Materials < matCost)
            {
                Debug.LogWarning("[Placement] Not enough resources to purchase new building!");
                return;
            }

            // Spend resources
            ResourceManager.Instance.SpendGold(goldCost);
            ResourceManager.Instance.SpendMaterials(matCost);
        }
        else
        {
            // Deploy from saved inventory (free!)
            storedBuildingCounts[selectedType]--;
            Debug.Log($"[Placement] Deployed {selectedType} from inventory! Remaining stored: {storedBuildingCounts[selectedType]}");
        }

        // Create building GameObject
        GameObject bObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bObj.name = $"Building_{selectedType}_{x}_{y}";
        bObj.transform.localScale = new Vector3(1.8f, 1.4f, 1.8f);

        Renderer rend = bObj.GetComponent<Renderer>();
        if (rend != null)
        {
            switch (selectedType)
            {
                case Building.BuildingType.SMGTent:
                    rend.material.color = new Color(0.3f, 0.6f, 0.85f);
                    break;
                case Building.BuildingType.SniperTower:
                    rend.material.color = new Color(0.2f, 0.25f, 0.5f);
                    bObj.transform.localScale = new Vector3(1.4f, 2.5f, 1.4f);
                    break;
                case Building.BuildingType.Barricade:
                    rend.material.color = new Color(0.55f, 0.4f, 0.25f);
                    bObj.transform.localScale = new Vector3(1.8f, 0.8f, 1.8f);
                    break;
                case Building.BuildingType.HealStation:
                    rend.material.color = new Color(0.2f, 0.85f, 0.5f);
                    break;
            }
        }

        Building building = bObj.AddComponent<Building>();
        building.type = selectedType;
        building.gridX = x;
        building.gridY = y;
        building.goldCost = goldCost;
        building.materialCost = matCost;

        GridManager.Instance.PlaceBuilding(x, y, building);
        Debug.Log($"[Placement] Built {selectedType} at ({x}, {y})");
    }
}
