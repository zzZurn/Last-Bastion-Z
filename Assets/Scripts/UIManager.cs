using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    private GUIStyle headerStyle;
    private GUIStyle subHeaderStyle;
    private GUIStyle buttonStyle;
    private GUIStyle smallButtonStyle;
    private GUIStyle boxStyle;
    private GUIStyle resourceStyle;
    private GUIStyle descTitleStyle;
    private GUIStyle descBodyStyle;
    private GUIStyle placementHintStyle;
    private GUIStyle badgeStyle;
    private GUIStyle activeBadgeStyle;
    private bool stylesInitialized = false;

    // UI Toggle states
    public bool isBuildMenuOpen = true;
    public bool isMasterHudVisible = true;

    // Currently inspected building type in the UI
    public Building.BuildingType inspectedType = Building.BuildingType.SMGTent;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        // Hotkey: TAB or B to toggle build menu
        if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.B))
        {
            isBuildMenuOpen = !isBuildMenuOpen;
        }

        // Hotkey: H to toggle master HUD
        if (Input.GetKeyDown(KeyCode.H))
        {
            isMasterHudVisible = !isMasterHudVisible;
        }
    }

    /// <summary>
    /// Checks whether the mouse cursor is over any active UI element.
    /// Used by PlacementManager to prevent placing buildings when clicking buttons!
    /// </summary>
    public bool IsPointerOverUI()
    {
        if (!isMasterHudVisible) return false;

        // Convert Unity screen coordinates (bottom-left origin) to GUI coordinates (top-left origin)
        Vector2 mouseGuiPos = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);

        // 1. Build Menu panel or Open Button on left
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.GameState.Planning)
        {
            if (isBuildMenuOpen)
            {
                Rect menuRect = new Rect(15f, 78f, 360f, Mathf.Min(600f, Screen.height - 120f));
                if (menuRect.Contains(mouseGuiPos)) return true;
            }
            else
            {
                Rect openBtnRect = new Rect(15f, 78f, 200f, 40f);
                if (openBtnRect.Contains(mouseGuiPos)) return true;
            }

            // Start Wave button (bottom right)
            float startBtnW = 240f;
            float startBtnH = 50f;
            Rect startBtnRect = new Rect(Screen.width - startBtnW - 20f, Screen.height - startBtnH - 20f, startBtnW, startBtnH);
            if (startBtnRect.Contains(mouseGuiPos)) return true;

            // Bottom camera controls hint
            Rect bottomHintRect = new Rect(15f, Screen.height - 52f, 430f, 40f);
            if (bottomHintRect.Contains(mouseGuiPos)) return true;
        }

        // 2. Top HUD Bar
        Rect topBarRect = new Rect(10, 26, Screen.width - 20, 44);
        if (topBarRect.Contains(mouseGuiPos)) return true;

        // 3. HUD Toggle button (top right)
        Rect hudToggleRect = new Rect(Screen.width - 110, 4, 100, 22);
        if (hudToggleRect.Contains(mouseGuiPos)) return true;

        // 4. Modal Dialogs (Reward, Route, Victory)
        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.CurrentState == GameManager.GameState.Reward)
            {
                Rect rewardDialog = new Rect(Screen.width / 2f - 220f, Screen.height / 2f - 110f, 440f, 220f);
                if (rewardDialog.Contains(mouseGuiPos)) return true;
            }
            else if (GameManager.Instance.CurrentState == GameManager.GameState.RouteSelection)
            {
                Rect routeDialog = new Rect(Screen.width / 2f - 280f, Screen.height / 2f - 160f, 560f, 320f);
                if (routeDialog.Contains(mouseGuiPos)) return true;
            }
            else if (GameManager.Instance.CurrentState == GameManager.GameState.Victory)
            {
                Rect victoryDialog = new Rect(Screen.width / 2f - 270f, Screen.height / 2f - 140f, 540f, 280f);
                if (victoryDialog.Contains(mouseGuiPos)) return true;
            }
        }

        return false;
    }

    void InitStyles()
    {
        if (stylesInitialized) return;

        headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        headerStyle.normal.textColor = Color.white;

        subHeaderStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        subHeaderStyle.normal.textColor = new Color(1.0f, 0.75f, 0.2f); // Wasteland amber

        resourceStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        resourceStyle.normal.textColor = Color.white;

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold
        };

        smallButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 10,
            fontStyle = FontStyle.Bold
        };

        boxStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 12
        };

        badgeStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        badgeStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);

        activeBadgeStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        activeBadgeStyle.normal.textColor = Color.yellow;

        descTitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft
        };
        descTitleStyle.normal.textColor = new Color(0.3f, 0.9f, 1f);

        descBodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            wordWrap = true,
            alignment = TextAnchor.UpperLeft
        };
        descBodyStyle.normal.textColor = Color.white;

        placementHintStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        stylesInitialized = true;
    }

    void OnGUI()
    {
        InitStyles();

        if (GameManager.Instance == null) return;

        // Toggle master HUD button in corner
        if (GUI.Button(new Rect(Screen.width - 110, 4, 100, 20), isMasterHudVisible ? "👁️ Hide HUD (H)" : "👁️ Show HUD (H)", smallButtonStyle))
        {
            isMasterHudVisible = !isMasterHudVisible;
        }

        if (!isMasterHudVisible) return;

        DrawTopBar();

        switch (GameManager.Instance.CurrentState)
        {
            case GameManager.GameState.Planning:
                DrawPlanningUI();
                break;
            case GameManager.GameState.Combat:
                DrawCombatUI();
                break;
            case GameManager.GameState.Reward:
                DrawRewardUI();
                break;
            case GameManager.GameState.RouteSelection:
                DrawRouteSelectionUI();
                break;
            case GameManager.GameState.Victory:
                DrawVictoryUI();
                break;
        }
    }

    void DrawTopBar()
    {
        // Top HUD bar
        GUI.Box(new Rect(10, 26, Screen.width - 20, 44), GUIContent.none, boxStyle);

        string stageName = GameManager.Instance.GetStageName();
        string stateText = GameManager.Instance.CurrentState.ToString().ToUpper();
        GUI.Label(new Rect(20, 32, 420, 32), $"🏰 LAST FORTRESS Z  |  {stageName} ({stateText})", subHeaderStyle);

        if (ResourceManager.Instance != null)
        {
            string resText = $"💰 Gold: {ResourceManager.Instance.Gold}   |   🧱 Materials: {ResourceManager.Instance.Materials}   |   💎 Special: {ResourceManager.Instance.SpecialMaterials}";
            GUI.Box(new Rect(Screen.width - 460, 30, 440, 34), resText, resourceStyle);
        }
    }

    void DrawPlanningUI()
    {
        float menuX = 15f;
        float menuY = 78f;
        float menuW = 360f;
        float menuH = Mathf.Min(600f, Screen.height - 120f);

        // If Build Menu is toggled closed, show open button
        if (!isBuildMenuOpen)
        {
            GUI.backgroundColor = new Color(0.2f, 0.6f, 1f);
            if (GUI.Button(new Rect(menuX, menuY, 200f, 40f), "▶ 🛠️ Build Menu (TAB)", buttonStyle))
            {
                isBuildMenuOpen = true;
            }
            GUI.backgroundColor = Color.white;

            // Quick placement info if placing while menu collapsed
            if (PlacementManager.Instance != null && PlacementManager.Instance.isPlacing)
            {
                GUI.Box(new Rect(menuX, menuY + 48f, 280f, 48f), $"Placing: {PlacementManager.Instance.selectedType}\nLeft-Click: Place | Right-Click: Cancel", placementHintStyle);
            }

            DrawPlanningBottomBar();
            return;
        }

        // Full Build Menu Panel
        GUI.Box(new Rect(menuX, menuY, menuW, menuH), "🛠️ BASE DEFENSE ARSENAL", boxStyle);

        // Hide Menu Button at top-right of panel
        if (GUI.Button(new Rect(menuX + menuW - 105f, menuY + 4f, 98f, 22f), "◀ Hide (TAB)", smallButtonStyle))
        {
            isBuildMenuOpen = false;
        }

        float btnStartX = menuX + 8f;
        float curY = menuY + 28f;
        float colW = (menuW - 24f) / 2f;
        float badgeW = 36f;
        float btnW = colW - badgeW - 4f;
        float btnH = 42f;

        // --- 📋 1-CLICK AUTO-REBUILD BASE BUTTON (Restores previous day layout) ---
        if (PlacementManager.Instance != null && PlacementManager.Instance.HasSavedLayout())
        {
            int savedCount = PlacementManager.Instance.GetSavedLayoutCount();
            GUI.backgroundColor = new Color(0.18f, 0.72f, 0.95f);
            if (GUI.Button(new Rect(btnStartX, curY, menuW - 16f, 32f), $"📋 REBUILD PREVIOUS BASE ({savedCount} Placed)", buttonStyle))
            {
                PlacementManager.Instance.RestorePreviousLayout();
            }
            GUI.backgroundColor = Color.white;
            curY += 36f;
        }

        // Row 1: SMG Tent & Sniper Tower
        DrawBuildingRow(Building.BuildingType.SMGTent, "SMG Tent", 30, 10, btnStartX, curY, badgeW, btnW, btnH);
        DrawBuildingRow(Building.BuildingType.SniperTower, "Sniper", 60, 25, btnStartX + colW + 8f, curY, badgeW, btnW, btnH);
        curY += btnH + 5f;

        // Row 2: Barricade & Heal Station
        DrawBuildingRow(Building.BuildingType.Barricade, "Barricade", 15, 5, btnStartX, curY, badgeW, btnW, btnH);
        DrawBuildingRow(Building.BuildingType.HealStation, "Heal Stn", 50, 20, btnStartX + colW + 8f, curY, badgeW, btnW, btnH);
        curY += btnH + 6f;

        // --- DETAILED TOWER DESCRIPTION PANEL ---
        float descBoxH = 185f;
        GUI.Box(new Rect(btnStartX, curY, menuW - 16f, descBoxH), GUIContent.none, boxStyle);
        DrawTowerDescription(btnStartX + 8f, curY + 6f, menuW - 32f);
        curY += descBoxH + 6f;

        // --- PLACEMENT STATUS HINT ---
        if (PlacementManager.Instance != null && PlacementManager.Instance.isPlacing)
        {
            int stored = PlacementManager.Instance.GetStoredCount(PlacementManager.Instance.selectedType);
            GUI.backgroundColor = new Color(0.15f, 0.45f, 0.15f, 0.95f);
            GUI.Box(new Rect(btnStartX, curY, menuW - 16f, 52f), GUIContent.none, placementHintStyle);
            GUI.backgroundColor = Color.white;

            GUI.color = Color.green;
            string deployMode = stored > 0 ? $"[Deploying Stock: FREE! ({stored} left)]" : "[Purchasing New]";
            GUI.Label(new Rect(btnStartX + 8, curY + 3, menuW - 32, 19), $"📍 PLACING: {PlacementManager.Instance.selectedType}  {deployMode}", subHeaderStyle);
            GUI.color = Color.white;
            GUI.Label(new Rect(btnStartX + 8, curY + 23, menuW - 32, 26), "🖱️ Left-Click: Place on grid   |   ❌ Right-Click: Cancel", descBodyStyle);
        }
        else
        {
            GUI.Box(new Rect(btnStartX, curY, menuW - 16f, 42f), "Click any building above to begin placing.\nStored buildings from previous stages deploy for FREE!", descBodyStyle);
        }

        DrawPlanningBottomBar();
    }

    void DrawBuildingRow(Building.BuildingType type, string shortName, int goldCost, int matCost, float x, float y, float badgeW, float btnW, float btnH)
    {
        int storedCount = PlacementManager.Instance != null ? PlacementManager.Instance.GetStoredCount(type) : 0;
        bool isInspected = (inspectedType == type);

        // 1. NUMBER BADGE ON THE LEFT (Clicking badge ALSO selects this building!)
        if (storedCount > 0)
        {
            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.3f);
            if (GUI.Button(new Rect(x, y, badgeW, btnH), $"{storedCount}", activeBadgeStyle))
            {
                inspectedType = type;
                PlacementManager.Instance.SelectBuildingToPlace(type);
            }
            GUI.backgroundColor = Color.white;
        }
        else
        {
            if (GUI.Button(new Rect(x, y, badgeW, btnH), "0", badgeStyle))
            {
                inspectedType = type;
                PlacementManager.Instance.SelectBuildingToPlace(type);
            }
        }

        // 2. SELECTION BUTTON ON THE RIGHT
        GUI.backgroundColor = isInspected ? new Color(0.3f, 0.7f, 1.0f) : Color.white;

        string subText = storedCount > 0 ? "★ FREE Stock" : $"({goldCost}G / {matCost}M)";
        if (GUI.Button(new Rect(x + badgeW + 3f, y, btnW, btnH), $"{shortName}\n{subText}", buttonStyle))
        {
            inspectedType = type;
            PlacementManager.Instance.SelectBuildingToPlace(type);
        }
        GUI.backgroundColor = Color.white;
    }

    void DrawPlanningBottomBar()
    {
        // Start Combat Button (Bottom Right)
        float startBtnW = 240f;
        float startBtnH = 50f;
        float startBtnX = Screen.width - startBtnW - 20f;
        float startBtnY = Screen.height - startBtnH - 20f;

        GUI.backgroundColor = new Color(0.9f, 0.2f, 0.2f);
        if (GUI.Button(new Rect(startBtnX, startBtnY, startBtnW, startBtnH), "⚔️ START DEFENSE WAVE", buttonStyle))
        {
            GameManager.Instance.StartCombat();
        }
        GUI.backgroundColor = Color.white;

        // Camera guide hint (Bottom Left/Center)
        GUI.Box(new Rect(15f, Screen.height - 48, 420, 36), "Controls: WASD/Drag to Pan | Scroll to Zoom | TAB: Toggle Menu | H: Toggle HUD", boxStyle);
    }

    void DrawTowerDescription(float x, float y, float w)
    {
        int stored = PlacementManager.Instance != null ? PlacementManager.Instance.GetStoredCount(inspectedType) : 0;
        string stockNotice = stored > 0 ? $"<color=#55ff55>★ Owned in Stock: {stored} (Deploys for FREE!)</color>\n" : "• Owned in Stock: 0 (Purchase required)\n";

        switch (inspectedType)
        {
            case Building.BuildingType.SMGTent:
                GUI.Label(new Rect(x, y, w, 20), "⛺ SMG TENT (3-Round Burst Fire)", descTitleStyle);
                y += 20;
                string smgDesc =
                    stockNotice +
                    "• Cost: 30 Gold, 10 Materials\n" +
                    "• Spawns: 1 SMG Trooper every 8.0s\n" +
                    "• Attack: 3-Round Burst (0.12s burst, 0.66s pause)\n" +
                    "• Damage: 10 dmg / bullet (5 hits to kill Zombie)\n" +
                    "• DPS: 33.3 DPS (0.90s cycle, identical to Sniper!)\n" +
                    "• Ballistics: 24.0 m/s Visible Tracer Projectiles\n" +
                    "★ Role: High fire rate suppression with tactical bursts.";
                GUI.Label(new Rect(x, y, w, 160), smgDesc, descBodyStyle);
                break;

            case Building.BuildingType.SniperTower:
                GUI.Label(new Rect(x, y, w, 20), "🗼 SNIPER TOWER (Heavy Long-Range Rifle)", descTitleStyle);
                y += 20;
                string sniperDesc =
                    stockNotice +
                    "• Cost: 60 Gold, 25 Materials\n" +
                    "• Spawns: 1 Elite Sniper every 10.0s (Elite unit)\n" +
                    "• Attack: High-Velocity Single Fire (0.75s interval)\n" +
                    "• Damage: 25 dmg / shot (2 hits to kill Zombie!)\n" +
                    "• DPS: 33.3 DPS (Equal DPS to SMG!)\n" +
                    "• Ballistics: 42.0 m/s Supersonic Tracer (14.0m range)\n" +
                    "★ Role: High single-shot burst, eliminates threats early.";
                GUI.Label(new Rect(x, y, w, 160), sniperDesc, descBodyStyle);
                break;

            case Building.BuildingType.Barricade:
                GUI.Label(new Rect(x, y, w, 20), "🚧 BARRICADE (Reinforced Wall)", descTitleStyle);
                y += 20;
                string barDesc =
                    stockNotice +
                    "• Cost: 15 Gold, 5 Materials\n" +
                    "• Spawns: None (Static defensive structure)\n" +
                    "• Structure Health: High durability\n" +
                    "• Function: Solid physical obstacle\n" +
                    "★ Role: Bottlenecks and delays accelerating zombie hordes.";
                GUI.Label(new Rect(x, y, w, 160), barDesc, descBodyStyle);
                break;

            case Building.BuildingType.HealStation:
                GUI.Label(new Rect(x, y, w, 20), "🏥 HEAL STATION (Field Medical Outpost)", descTitleStyle);
                y += 20;
                string healDesc =
                    stockNotice +
                    "• Cost: 50 Gold, 20 Materials\n" +
                    "• Spawns: 1 Field Medic every 12.0s\n" +
                    "• Medic Health: 70 HP (Tough unit)\n" +
                    "• Defense Damage: 6 dmg / shot | Range: 7.0m\n" +
                    "★ Role: Frontline durability and squad survivability.";
                GUI.Label(new Rect(x, y, w, 160), healDesc, descBodyStyle);
                break;
        }
    }

    void DrawCombatUI()
    {
        // Combat status box
        float boxW = 340f;
        float boxH = 92f;
        float boxX = Screen.width / 2f - boxW / 2f;
        float boxY = 78f;

        GUI.Box(new Rect(boxX, boxY, boxW, boxH), $"⚠️ {GameManager.Instance.GetStageName().ToUpper()}", boxStyle);

        if (WaveManager.Instance != null)
        {
            GUI.Label(new Rect(boxX + 10, boxY + 28, boxW - 20, 25), $"Zombies Remaining: {WaveManager.Instance.zombiesRemaining} / {WaveManager.Instance.totalZombiesInWave}", subHeaderStyle);
            float progress = (float)(WaveManager.Instance.totalZombiesInWave - WaveManager.Instance.zombiesRemaining) / Mathf.Max(1, WaveManager.Instance.totalZombiesInWave);
            GUI.HorizontalSlider(new Rect(boxX + 15, boxY + 56, boxW - 30, 20), progress, 0f, 1f);
        }
    }

    void DrawRewardUI()
    {
        float dialogW = 440f;
        float dialogH = 220f;
        float dialogX = Screen.width / 2f - dialogW / 2f;
        float dialogY = Screen.height / 2f - dialogH / 2f;

        GUI.Box(new Rect(dialogX, dialogY, dialogW, dialogH), "🎉 STAGE CLEARED SUCCESSFULLY!", boxStyle);

        int day = GameManager.Instance.CurrentDay;
        int goldReward = 8 + (day * 3);
        int matReward = 6 + (day * 2);

        GUI.Label(new Rect(dialogX + 20, dialogY + 45, dialogW - 40, 30), $"Stage {day} Victory! Defense Line Held.", headerStyle);
        GUI.Label(new Rect(dialogX + 20, dialogY + 85, dialogW - 40, 25), $"Rewards Earned: +{goldReward} Gold, +{matReward} Materials", subHeaderStyle);

        GUI.backgroundColor = Color.cyan;
        if (GUI.Button(new Rect(dialogX + 70, dialogY + 140, dialogW - 140, 48), "PROCEED TO NEXT STAGE ➡️", buttonStyle))
        {
            GameManager.Instance.ProceedToNextDay();
        }
        GUI.backgroundColor = Color.white;
    }

    void DrawRouteSelectionUI()
    {
        float dialogW = 560f;
        float dialogH = 320f;
        float dialogX = Screen.width / 2f - dialogW / 2f;
        float dialogY = Screen.height / 2f - dialogH / 2f;

        GUI.Box(new Rect(dialogX, dialogY, dialogW, dialogH), $"🗺️ {GameManager.Instance.GetStageName().ToUpper()}", boxStyle);

        GUI.Label(new Rect(dialogX + 20, dialogY + 35, dialogW - 40, 30), "Choose your strategic route for this stage:", headerStyle);

        // Route A: Battle Route
        float routeW = 240f;
        float routeH = 180f;
        float routeA_X = dialogX + 30f;
        float routeB_X = dialogX + dialogW - routeW - 30f;
        float routeY = dialogY + 80f;

        GUI.Box(new Rect(routeA_X, routeY, routeW, routeH), GUIContent.none, boxStyle);
        GUI.Label(new Rect(routeA_X + 10, routeY + 10, routeW - 20, 25), "⚔️ BATTLE ROUTE", subHeaderStyle);
        GUI.Label(new Rect(routeA_X + 10, routeY + 40, routeW - 20, 65), "Engage regular zombie forces.\nSafe & reliable source of\nGold & Building Materials.");

        GUI.backgroundColor = new Color(0.9f, 0.4f, 0.2f);
        if (GUI.Button(new Rect(routeA_X + 20, routeY + 120, routeW - 40, 42), "CHOOSE BATTLE", buttonStyle))
        {
            GameManager.Instance.SelectRoute(true);
        }
        GUI.backgroundColor = Color.white;

        // Route B: Event Route
        GUI.Box(new Rect(routeB_X, routeY, routeW, routeH), GUIContent.none, boxStyle);
        GUI.Label(new Rect(routeB_X + 10, routeY + 10, routeW - 20, 25), "🎲 EVENT ROUTE", subHeaderStyle);
        GUI.Label(new Rect(routeB_X + 10, routeY + 40, routeW - 20, 65), "Risk wasteland scavenging.\nChance to acquire rare\nSpecial Materials!");

        GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
        if (GUI.Button(new Rect(routeB_X + 20, routeY + 120, routeW - 40, 42), "CHOOSE EVENT", buttonStyle))
        {
            GameManager.Instance.SelectRoute(false);
        }
        GUI.backgroundColor = Color.white;
    }

    void DrawVictoryUI()
    {
        float dialogW = 540f;
        float dialogH = 280f;
        float dialogX = Screen.width / 2f - dialogW / 2f;
        float dialogY = Screen.height / 2f - dialogH / 2f;

        GUI.Box(new Rect(dialogX, dialogY, dialogW, dialogH), "🏆 CAMPAIGN CONQUERED — THE LAST FORTRESS SURVIVES!", boxStyle);

        GUI.Label(new Rect(dialogX + 20, dialogY + 45, dialogW - 40, 32), "ALL 10 STAGES CLEARED!", headerStyle);
        GUI.Label(new Rect(dialogX + 20, dialogY + 85, dialogW - 40, 45), "You successfully defended the fortress across all wasteland sectors!\nYour strategic planning saved humanity.", descBodyStyle);

        // Endless mode button
        GUI.backgroundColor = new Color(0.2f, 0.8f, 0.3f);
        if (GUI.Button(new Rect(dialogX + 40, dialogY + 160, 220, 50), "🔥 ENDLESS MODE (Stage 11+)", buttonStyle))
        {
            GameManager.Instance.ContinueEndlessMode();
        }

        // Restart campaign button
        GUI.backgroundColor = new Color(0.9f, 0.4f, 0.2f);
        if (GUI.Button(new Rect(dialogX + 280, dialogY + 160, 220, 50), "🔄 RESTART CAMPAIGN", buttonStyle))
        {
            GameManager.Instance.RestartCampaign();
        }
        GUI.backgroundColor = Color.white;
    }
}
