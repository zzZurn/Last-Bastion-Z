using UnityEngine;
using System.Collections.Generic;

public class PlacementManager : MonoBehaviour
{
    public static PlacementManager Instance { get; private set; }

    public enum PlacementMode
    {
        None,
        Place,
        Relocate,
        Demolish
    }

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
    public PlacementMode currentMode = PlacementMode.None;
    public bool isPlacing => currentMode == PlacementMode.Place;

    // Relocate handling
    private Building relocatingBuilding = null;
    private int relocateOriginX;
    private int relocateOriginY;

    // Blueprint of the previous day's base layout for instant 1-click rebuild!
    public List<SavedBuildingSlot> lastSavedLayout = new List<SavedBuildingSlot>();

    // Stored/Recovered buildings from previous days
    private Dictionary<Building.BuildingType, int> storedBuildingCounts = new Dictionary<Building.BuildingType, int>()
    {
        { Building.BuildingType.SMGTent, 0 },
        { Building.BuildingType.SniperTower, 0 },
        { Building.BuildingType.Barricade, 0 },
        { Building.BuildingType.HealStation, 0 },
        { Building.BuildingType.SpecialForceDepot, 0 },
        { Building.BuildingType.LightTankFactory, 0 },
        { Building.BuildingType.MissileFoundry, 0 },
        { Building.BuildingType.SupplyDepot, 0 },
        { Building.BuildingType.FrontlineSniperTower, 0 }
    };

    private GameObject previewObject;
    private Camera mainCam;

    // Multi-Placement Drag State (Hold Left-Click -> Blue Blocks -> Release to Place / Right-Click to Cancel)
    public bool isDraggingPlacement { get; private set; } = false;
    public int DragQueuedCount => dragQueuedOrigins.Count;
    private List<Vector2Int> dragQueuedOrigins = new List<Vector2Int>();
    private HashSet<Vector2Int> dragOccupiedCells = new HashSet<Vector2Int>();
    private List<GameObject> dragPreviewBlocks = new List<GameObject>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
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

    public void ClearStoredCounts()
    {
        storedBuildingCounts = new Dictionary<Building.BuildingType, int>()
        {
            { Building.BuildingType.SMGTent, 0 },
            { Building.BuildingType.SniperTower, 0 },
            { Building.BuildingType.Barricade, 0 },
            { Building.BuildingType.HealStation, 0 },
            { Building.BuildingType.SpecialForceDepot, 0 },
            { Building.BuildingType.LightTankFactory, 0 },
            { Building.BuildingType.MissileFoundry, 0 },
            { Building.BuildingType.SupplyDepot, 0 },
            { Building.BuildingType.FrontlineSniperTower, 0 }
        };
        lastSavedLayout.Clear();
        Debug.Log("[PlacementManager] Stored building stock and layout blueprint cleared.");
    }

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
            if (GridManager.Instance != null && GridManager.Instance.CanPlaceBuildingAt(slot.gridX, slot.gridY, slot.type))
            {
                selectedType = slot.type;
                int stored = GetStoredCount(selectedType);
                int goldCost = stored > 0 ? 0 : Building.GetGoldCost(selectedType);
                int matCost = stored > 0 ? 0 : Building.GetMaterialCost(selectedType);

                if (stored > 0 || (ResourceManager.Instance.Gold >= goldCost && ResourceManager.Instance.Materials >= matCost))
                {
                    TryPlaceBuilding(slot.gridX, slot.gridY);
                    placedCount++;
                }
            }
        }

        AudioHelper.PlaySound(SoundEffect.BuildingPlace, 0.8f);
        Debug.Log($"[PlacementManager] Successfully restored {placedCount}/{lastSavedLayout.Count} buildings from previous layout!");
        return placedCount;
    }

    void CreatePreviewObject()
    {
        previewObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        previewObject.name = "PlacementPreview";

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

        switch (selectedType)
        {
            case Building.BuildingType.SMGTent:
                previewObject.transform.localScale = new Vector3(1.8f, 1.4f, 1.8f);
                break;
            case Building.BuildingType.SniperTower:
                // กว้าง 2 ยาว 1 ช่อง (1 cell in X = 2m, 2 cells in Z = 4m) - เต็นท์พรางพลซุ่มยิง (Sniper's Nest)
                previewObject.transform.localScale = new Vector3(1.8f, 1.4f, 3.8f);
                break;
            case Building.BuildingType.Barricade:
                previewObject.transform.localScale = new Vector3(1.8f, 0.8f, 1.8f);
                break;
            case Building.BuildingType.HealStation:
                previewObject.transform.localScale = new Vector3(1.6f, 1.4f, 1.6f);
                break;
            case Building.BuildingType.SpecialForceDepot:
                previewObject.transform.localScale = new Vector3(1.7f, 1.5f, 1.7f);
                break;
            case Building.BuildingType.LightTankFactory:
                // 2x2 cells (4m x 4m in world space)
                previewObject.transform.localScale = new Vector3(3.8f, 2.2f, 3.8f);
                break;
            case Building.BuildingType.MissileFoundry:
                // กว้าง 3 ยาว 4 ช่อง (4 cells in X = 8m, 3 cells in Z = 6m)
                previewObject.transform.localScale = new Vector3(7.8f, 2.8f, 5.8f);
                break;
            case Building.BuildingType.SupplyDepot:
                previewObject.transform.localScale = new Vector3(1.75f, 1.45f, 1.75f);
                break;
            case Building.BuildingType.FrontlineSniperTower:
                previewObject.transform.localScale = new Vector3(1.7f, 3.4f, 1.7f);
                break;
            default:
                Vector2Int fp = Building.GetFootprintSize(selectedType);
                previewObject.transform.localScale = new Vector3(fp.x * 1.9f, 1.6f, fp.y * 1.9f);
                break;
        }
    }

    void Update()
    {
        if (GameManager.Instance == null) return;

        // Pause building placement preview/clicks while manually aiming an ICBM Launcher
        if (Soldier.ActiveTargetingICBM != null)
        {
            if (isDraggingPlacement) ClearDragQueue();
            if (previewObject != null && previewObject.activeSelf) previewObject.SetActive(false);
            return;
        }

        bool isPlanning = (GameManager.Instance.CurrentState == GameManager.GameState.Planning);
        bool allowDevBuildInCombat = (GameManager.Instance.isDevUnconditionalMode && GameManager.Instance.CurrentState == GameManager.GameState.Combat);

        if (!isPlanning && !allowDevBuildInCombat)
        {
            if (isDraggingPlacement) ClearDragQueue();
            if (previewObject != null && previewObject.activeSelf) previewObject.SetActive(false);
            return;
        }

        // Handle right-click cancel or mouse-up commit even if cursor moves over UI while dragging
        if (isDraggingPlacement)
        {
            if (Input.GetMouseButtonDown(1))
            {
                ClearDragQueue();
                AudioHelper.PlaySound(SoundEffect.ButtonClick, 0.5f);
                Debug.Log("[Placement] Cancelled drag-placement batch.");
                return;
            }
            if (Input.GetMouseButtonUp(0))
            {
                CommitDragPlacement();
                return;
            }
        }

        // Cancel if mouse is over UI
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

        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

        if (groundPlane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            Vector2Int cell = GridManager.Instance.WorldToGrid(hitPoint);
            bool isDevFree = (GameManager.Instance != null && GameManager.Instance.isDevUnconditionalMode);
            bool isValid = GridManager.Instance.CanPlaceBuildingAt(cell.x, cell.y, selectedType);
            bool isOccupied = GridManager.Instance.IsCellOccupied(cell.x, cell.y);
            bool isLocked = GridManager.Instance.IsCellLocked(cell.x, cell.y);

            // 1. PLACE MODE (Supports Hold & Drag Multi-Placement with Blue Preview Blocks!)
            if (currentMode == PlacementMode.Place)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    ClearDragQueue();
                    isDraggingPlacement = true;
                    TryAddCellToDragQueue(cell.x, cell.y);
                }
                else if (Input.GetMouseButton(0) && isDraggingPlacement)
                {
                    TryAddCellToDragQueue(cell.x, cell.y);
                }

                if (previewObject != null)
                {
                    previewObject.SetActive(true);
                    float previewYOffset = previewObject.transform.localScale.y / 2f;
                    previewObject.transform.position = GridManager.Instance.GetBuildingWorldCenter(cell.x, cell.y, selectedType) + Vector3.up * previewYOffset;

                    Renderer rend = previewObject.GetComponent<Renderer>();
                    if (rend != null)
                    {
                        if (isDraggingPlacement)
                        {
                            // While holding & dragging, preview block turns BLUE!
                            bool canQueueHere = CanAddToDragQueue(cell.x, cell.y, selectedType) || dragOccupiedCells.Contains(cell);
                            rend.material.color = canQueueHere
                                ? new Color(0.15f, 0.65f, 1.0f, 0.78f) // Blue drag block
                                : new Color(1f, 0.2f, 0.2f, 0.6f);
                        }
                        else if (isLocked)
                        {
                            rend.material.color = new Color(1f, 0.55f, 0f, 0.7f); // Orange locked indicator
                        }
                        else
                        {
                            int maxLimit = Building.GetMaxBuildingPlacementLimit(selectedType);
                            bool isLimitReached = !isDevFree && (maxLimit > 0 && Building.GetActiveBuildingCount(selectedType) >= maxLimit);
                            rend.material.color = (isValid && !isLimitReached)
                                ? new Color(0.2f, 1f, 0.2f, 0.6f)      // Green hover block
                                : new Color(1f, 0.2f, 0.2f, 0.6f);     // Red blocked/capped
                        }
                    }
                }

                if (!isDraggingPlacement && Input.GetMouseButtonDown(1))
                {
                    CancelPlacement();
                }
            }
            // 2. DEMOLISH / SELL MODE
            else if (currentMode == PlacementMode.Demolish)
            {
                if (previewObject != null)
                {
                    previewObject.SetActive(true);
                    previewObject.transform.localScale = new Vector3(1.9f, 0.4f, 1.9f);
                    previewObject.transform.position = GridManager.Instance.GridToWorld(cell.x, cell.y) + Vector3.up * 0.2f;

                    Renderer rend = previewObject.GetComponent<Renderer>();
                    if (rend != null)
                    {
                        rend.material.color = isOccupied ? new Color(1f, 0.1f, 0.1f, 0.7f) : new Color(0.5f, 0.5f, 0.5f, 0.3f);
                    }
                }

                if (Input.GetMouseButtonDown(0) && isOccupied)
                {
                    DemolishBuilding(cell.x, cell.y);
                }

                if (Input.GetMouseButtonDown(1))
                {
                    CancelPlacement();
                }
            }
            // 3. RELOCATE MODE
            else if (currentMode == PlacementMode.Relocate)
            {
                if (relocatingBuilding == null)
                {
                    // Step A: Pick up building
                    if (previewObject != null)
                    {
                        previewObject.SetActive(true);
                        previewObject.transform.localScale = new Vector3(1.9f, 0.4f, 1.9f);
                        previewObject.transform.position = GridManager.Instance.GridToWorld(cell.x, cell.y) + Vector3.up * 0.2f;

                        Renderer rend = previewObject.GetComponent<Renderer>();
                        if (rend != null)
                        {
                            rend.material.color = isOccupied ? new Color(1f, 0.9f, 0.2f, 0.7f) : new Color(0.4f, 0.4f, 0.4f, 0.3f);
                        }
                    }

                    if (Input.GetMouseButtonDown(0) && isOccupied)
                    {
                        Building b = GridManager.Instance.GetBuilding(cell.x, cell.y);
                        if (b != null)
                        {
                            relocatingBuilding = b;
                            relocateOriginX = b.gridX;
                            relocateOriginY = b.gridY;
                            GridManager.Instance.RemoveBuilding(b.gridX, b.gridY);
                            relocatingBuilding.gameObject.SetActive(false);
                            selectedType = b.type;
                            UpdatePreviewAppearance();
                            AudioHelper.PlaySound(SoundEffect.ButtonClick);
                            Debug.Log($"[Placement] Picked up {b.type} to relocate!");
                        }
                    }
                }
                else
                {
                    // Step B: Place building down at new location
                    if (previewObject != null)
                    {
                        previewObject.SetActive(true);
                        float previewYOffset = previewObject.transform.localScale.y / 2f;
                        previewObject.transform.position = GridManager.Instance.GetBuildingWorldCenter(cell.x, cell.y, selectedType) + Vector3.up * previewYOffset;

                        Renderer rend = previewObject.GetComponent<Renderer>();
                        if (rend != null)
                        {
                            rend.material.color = isValid ? new Color(0.2f, 1f, 0.3f, 0.65f) : new Color(1f, 0.2f, 0.2f, 0.65f);
                        }
                    }

                    if (Input.GetMouseButtonDown(0) && isValid)
                    {
                        relocatingBuilding.gameObject.SetActive(true);
                        relocatingBuilding.gridX = cell.x;
                        relocatingBuilding.gridY = cell.y;
                        GridManager.Instance.PlaceBuilding(cell.x, cell.y, relocatingBuilding);
                        AudioHelper.PlaySound(SoundEffect.BuildingPlace);
                        Debug.Log($"[Placement] Relocated {relocatingBuilding.type} to ({cell.x}, {cell.y})!");
                        relocatingBuilding = null;
                        currentMode = PlacementMode.None;
                        if (previewObject != null) previewObject.SetActive(false);
                    }

                    if (Input.GetMouseButtonDown(1))
                    {
                        // Cancel relocate, restore to origin
                        relocatingBuilding.gameObject.SetActive(true);
                        GridManager.Instance.PlaceBuilding(relocateOriginX, relocateOriginY, relocatingBuilding);
                        relocatingBuilding = null;
                        CancelPlacement();
                    }
                }
            }
        }
        else
        {
            if (previewObject != null) previewObject.SetActive(false);
        }
    }

    private bool CanAddToDragQueue(int x, int y, Building.BuildingType type)
    {
        if (GridManager.Instance == null) return false;
        if (!Building.IsBuildingUnlocked(type)) return false;
        if (!GridManager.Instance.CanPlaceBuildingAt(x, y, type)) return false;

        // Ensure footprint doesn't overlap any cell already queued in the current drag
        Vector2Int size = Building.GetFootprintSize(type);
        for (int dx = 0; dx < size.x; dx++)
        {
            for (int dy = 0; dy < size.y; dy++)
            {
                if (dragOccupiedCells.Contains(new Vector2Int(x + dx, y + dy)))
                {
                    return false;
                }
            }
        }

        // Check if player has enough resources/stock for (alreadyQueued + 1) buildings
        bool isDevFree = (GameManager.Instance != null && GameManager.Instance.isDevUnconditionalMode);
        if (!isDevFree && ResourceManager.Instance != null)
        {
            // Check placement limit (e.g. SMG Tent & Sniper's Nest capped at 10)
            int maxLimit = Building.GetMaxBuildingPlacementLimit(type);
            if (maxLimit > 0)
            {
                int currentActive = Building.GetActiveBuildingCount(type);
                if (currentActive + dragQueuedOrigins.Count + 1 > maxLimit)
                {
                    return false;
                }
            }

            int nextTotalCount = dragQueuedOrigins.Count + 1;
            int stored = GetStoredCount(type);
            int paidCount = Mathf.Max(0, nextTotalCount - stored);
            int totalGoldNeeded = Building.GetTotalGoldCostForCount(type, paidCount);
            int totalMatNeeded = paidCount * Building.GetMaterialCost(type);

            if (ResourceManager.Instance.Gold < totalGoldNeeded || ResourceManager.Instance.Materials < totalMatNeeded)
            {
                return false;
            }
        }

        return true;
    }

    private void TryAddCellToDragQueue(int x, int y)
    {
        if (!CanAddToDragQueue(x, y, selectedType)) return;

        dragQueuedOrigins.Add(new Vector2Int(x, y));

        Vector2Int size = Building.GetFootprintSize(selectedType);
        for (int dx = 0; dx < size.x; dx++)
        {
            for (int dy = 0; dy < size.y; dy++)
            {
                dragOccupiedCells.Add(new Vector2Int(x + dx, y + dy));
            }
        }

        // Spawn a Blue Preview Block at this queued position
        GameObject blueBlock = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blueBlock.name = $"DragPreviewBlock_{x}_{y}";
        Collider c = blueBlock.GetComponent<Collider>();
        if (c != null) Destroy(c);

        Vector3 scale = (previewObject != null) ? previewObject.transform.localScale : new Vector3(1.8f, 1.4f, 1.8f);
        blueBlock.transform.localScale = scale;
        blueBlock.transform.position = GridManager.Instance.GetBuildingWorldCenter(x, y, selectedType) + Vector3.up * (scale.y / 2f);

        Renderer rend = blueBlock.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material.color = new Color(0.15f, 0.65f, 1.0f, 0.72f); // Blue queued block
        }

        dragPreviewBlocks.Add(blueBlock);
    }

    private void CommitDragPlacement()
    {
        if (dragQueuedOrigins.Count > 0)
        {
            foreach (Vector2Int origin in dragQueuedOrigins)
            {
                TryPlaceBuilding(origin.x, origin.y);
            }
        }
        ClearDragQueue();
    }

    public void ClearDragQueue()
    {
        foreach (GameObject block in dragPreviewBlocks)
        {
            if (block != null) Destroy(block);
        }
        dragPreviewBlocks.Clear();
        dragQueuedOrigins.Clear();
        dragOccupiedCells.Clear();
        isDraggingPlacement = false;
    }

    public void CancelPlacement()
    {
        ClearDragQueue();

        if (relocatingBuilding != null)
        {
            relocatingBuilding.gameObject.SetActive(true);
            GridManager.Instance.PlaceBuilding(relocateOriginX, relocateOriginY, relocatingBuilding);
            relocatingBuilding = null;
        }

        currentMode = PlacementMode.None;
        if (previewObject != null) previewObject.SetActive(false);
    }

    public void SelectBuildingToPlace(Building.BuildingType type)
    {
        ClearDragQueue();
        if (!Building.IsBuildingUnlocked(type))
        {
            CancelPlacement();
            AudioHelper.PlaySound(SoundEffect.ButtonClick);
            int reqStage = (type == Building.BuildingType.MissileFoundry) ? 15 : ((type == Building.BuildingType.LightTankFactory) ? 10 : 5);
            Debug.LogWarning($"[PlacementManager] {type} is locked! Unlocks after Stage {reqStage} (Stage {reqStage + 1}+).");
            return;
        }

        selectedType = type;
        currentMode = PlacementMode.Place;
        UpdatePreviewAppearance();
        AudioHelper.PlaySound(SoundEffect.ButtonClick);
        Debug.Log($"[PlacementManager] Switched selected building to: {selectedType}");
    }

    public void SelectDemolishMode()
    {
        currentMode = PlacementMode.Demolish;
        AudioHelper.PlaySound(SoundEffect.ButtonClick);
        Debug.Log("[PlacementManager] Entered Demolish/Sell Mode.");
    }

    public void SelectRelocateMode()
    {
        currentMode = PlacementMode.Relocate;
        relocatingBuilding = null;
        AudioHelper.PlaySound(SoundEffect.ButtonClick);
        Debug.Log("[PlacementManager] Entered Relocate Mode.");
    }

    public void DemolishBuilding(int x, int y)
    {
        Building b = GridManager.Instance.GetBuilding(x, y);
        if (b == null) return;

        // Calculate base cost to refund (70%)
        int baseGoldCost = (b.goldCost > 0)
            ? b.goldCost
            : (b.type == Building.BuildingType.SupplyDepot
                ? (100 + Mathf.Max(0, Building.GetTotalSupplyDepotsOwned() - 1) * 50)
                : Building.GetGoldCost(b.type));
        int refundGold = Mathf.RoundToInt(baseGoldCost * 0.7f);
        int refundMats = Mathf.RoundToInt(Building.GetMaterialCost(b.type) * 0.7f);

        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.SpendGold(-refundGold);
            ResourceManager.Instance.SpendMaterials(-refundMats);
        }

        Building.ActiveBuildings.Remove(b);
        GridManager.Instance.RemoveBuilding(x, y);
        Destroy(b.gameObject);

        AudioHelper.PlaySound(SoundEffect.BuildingDemolish);
        Debug.Log($"[Placement] Demolished {b.type} at ({x}, {y})! Refunded {refundGold} Gold and {refundMats} Materials.");
    }

    public void TryPlaceBuilding(int x, int y)
    {
        if (!Building.IsBuildingUnlocked(selectedType))
        {
            int reqStage = (selectedType == Building.BuildingType.MissileFoundry) ? 15 : ((selectedType == Building.BuildingType.LightTankFactory) ? 10 : 5);
            Debug.LogWarning($"[Placement] {selectedType} is locked! Unlocks after Stage {reqStage} (Stage {reqStage + 1}+).");
            return;
        }

        if (!GridManager.Instance.CanPlaceBuildingAt(x, y, selectedType))
        {
            if (GridManager.Instance.IsCellLocked(x, y))
            {
                Debug.LogWarning("[Placement] This sector is locked! Clear Stage 15 to unlock North and South branches.");
            }
            else if (GridManager.Instance.IsFrontlineFortificationZone(x, y) && !Building.IsFrontlineFortification(selectedType))
            {
                Debug.LogWarning("[Placement] Frontline zone (up to the crossroads) only allows Fortification / Turret structures (e.g. Barricade)! Unit-spawning bases must be placed inside the main base.");
            }
            else
            {
                Debug.LogWarning("[Placement] Invalid cell, outside Cross boundary, or already occupied!");
            }
            return;
        }

        bool isDevFree = (GameManager.Instance != null && GameManager.Instance.isDevUnconditionalMode);

        int maxLimit = Building.GetMaxBuildingPlacementLimit(selectedType);
        if (maxLimit > 0 && !isDevFree)
        {
            int currentActive = Building.GetActiveBuildingCount(selectedType);
            if (currentActive >= maxLimit)
            {
                Debug.LogWarning($"[Placement] Cannot place more than {maxLimit} {selectedType}! Maximum limit reached ({currentActive}/{maxLimit}).");
                AudioHelper.PlaySound(SoundEffect.ButtonClick, 0.5f);
                return;
            }
        }

        int stored = GetStoredCount(selectedType);
        bool useStock = !isDevFree && (stored > 0);

        int goldCost = (isDevFree || useStock) ? 0 : Building.GetGoldCost(selectedType);
        int matCost = (isDevFree || useStock) ? 0 : Building.GetMaterialCost(selectedType);

        if (!isDevFree && !useStock)
        {
            if (ResourceManager.Instance.Gold < goldCost || ResourceManager.Instance.Materials < matCost)
            {
                Debug.LogWarning("[Placement] Not enough resources to purchase new building!");
                return;
            }

            ResourceManager.Instance.SpendGold(goldCost);
            ResourceManager.Instance.SpendMaterials(matCost);
        }
        else if (useStock)
        {
            storedBuildingCounts[selectedType]--;
            Debug.Log($"[Placement] Deployed {selectedType} from inventory! Remaining stored: {storedBuildingCounts[selectedType]}");
        }

        // Create building GameObject with composite 2.5D block model
        GameObject bObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bObj.name = $"Building_{selectedType}_{x}_{y}";
        bObj.transform.localScale = Vector3.one;

        BuildingBlockVisualizer.AttachBuildingVisuals(bObj, selectedType);

        Building building = bObj.AddComponent<Building>();
        building.type = selectedType;
        building.gridX = x;
        building.gridY = y;
        building.goldCost = goldCost;
        building.materialCost = matCost;

        GridManager.Instance.PlaceBuilding(x, y, building);
        AudioHelper.PlaySound(SoundEffect.BuildingPlace);
        Debug.Log($"[Placement] Built {selectedType} at ({x}, {y})");
    }
}
