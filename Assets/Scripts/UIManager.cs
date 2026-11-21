using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    private GUIStyle headerStyle;
    private GUIStyle subHeaderStyle;
    private GUIStyle rewardCenteredStyle;
    private GUIStyle buttonStyle;
    private GUIStyle smallButtonStyle;
    private GUIStyle boxStyle;
    private GUIStyle resourceStyle;
    private GUIStyle descTitleStyle;
    private GUIStyle descBodyStyle;
    private GUIStyle placementHintStyle;
    private GUIStyle badgeStyle;
    private GUIStyle activeBadgeStyle;
    private GUIStyle panelHeaderStyle;
    private GUIStyle singleLineLabelStyle;
    private GUIStyle dayTransitionTitleStyle;
    private GUIStyle dayTransitionHintStyle;
    private Texture2D blackFadeTexture;
    private bool stylesInitialized = false;

    // Day Transition ("DAY X" black screen fade + left-click skip)
    private bool isDayTransitionActive = false;
    private int dayTransitionNumber = 1;
    private float dayTransitionElapsed = 0f;
    private float dayTransitionAlpha = 0f;
    private int dayTransitionStartFrame = -1;
    private int dayTransitionSuppressFrame = -1;
    private const float DAY_TRANSITION_HOLD = 1.10f;
    private const float DAY_TRANSITION_FADE = 1.70f;

    // UI Toggle states
    public bool isBuildMenuOpen = true;
    public bool isMasterHudVisible = true;
    public bool isDevPanelOpen = false;
    public bool isQuitConfirmOpen = false;
    private string devPasscodeInput = "";
    private string devAuthStatusMsg = "";
    private string devBaseStatusMsg = "";

    // Currently inspected building type in the UI
    public enum BuildMenuTab { UnitBases, FrontlineFortifications, AirSupport }
    public BuildMenuTab currentBuildTab = BuildMenuTab.UnitBases;
    public enum AircraftType { FighterJet, B2Bomber }
    public static AircraftType SelectedAircraft = AircraftType.FighterJet;
    public Building.BuildingType inspectedType = Building.BuildingType.SMGTent;
    private Vector2 descScrollPos = Vector2.zero;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (blackFadeTexture != null)
        {
            Destroy(blackFadeTexture);
            blackFadeTexture = null;
        }
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void TriggerDayTransition(int dayNumber)
    {
        dayTransitionNumber = Mathf.Max(1, dayNumber);
        isDayTransitionActive = true;
        dayTransitionElapsed = 0f;
        dayTransitionAlpha = 1f;
        dayTransitionStartFrame = Time.frameCount;
    }

    public void CancelDayTransition()
    {
        isDayTransitionActive = false;
        dayTransitionAlpha = 0f;
    }

    private void DismissDayTransitionImmediate()
    {
        isDayTransitionActive = false;
        dayTransitionAlpha = 0f;
        dayTransitionSuppressFrame = Time.frameCount;
    }

    private void UpdateDayTransition()
    {
        if (!isDayTransitionActive) return;

        if (Time.frameCount > dayTransitionStartFrame && Input.GetMouseButtonDown(0))
        {
            DismissDayTransitionImmediate();
            return;
        }

        dayTransitionElapsed += Time.unscaledDeltaTime;
        if (dayTransitionElapsed <= DAY_TRANSITION_HOLD)
        {
            dayTransitionAlpha = 1f;
        }
        else
        {
            float t = Mathf.Clamp01((dayTransitionElapsed - DAY_TRANSITION_HOLD) / DAY_TRANSITION_FADE);
            dayTransitionAlpha = Mathf.SmoothStep(1f, 0f, t);
            if (t >= 1f)
            {
                isDayTransitionActive = false;
                dayTransitionAlpha = 0f;
            }
        }
    }

    void Update()
    {
        UpdateDayTransition();
        // Hotkey: Enter / Space to start game from Main Menu
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.GameState.MainMenu)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                AudioHelper.PlaySound(SoundEffect.ButtonClick);
                GameManager.Instance.StartGameFromMenu();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                QuitGame();
                return;
            }
            return;
        }

        // Hotkey: Escape to toggle in-game Quit confirmation dialog
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            isQuitConfirmOpen = !isQuitConfirmOpen;
            AudioHelper.PlaySound(SoundEffect.ButtonClick);
        }

        // Hotkey: Enter / Space / R to restart when Game Over
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.GameState.GameOver)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.R))
            {
                AudioHelper.PlaySound(SoundEffect.ButtonClick);
                GameManager.Instance.RestartCampaign();
                return;
            }
        }

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

        // Hotkey: G to toggle battlefield grid visibility
        if (Input.GetKeyDown(KeyCode.G))
        {
            if (CrossMapVisualizer.Instance != null)
            {
                CrossMapVisualizer.Instance.ToggleGridVisible();
                AudioHelper.PlaySound(SoundEffect.ButtonClick);
            }
        }

        // Hotkeys for Volume: M (Mute), [-] / [[] decrease, [=] / [+] / []] increase
        if (Input.GetKeyDown(KeyCode.M))
        {
            AudioHelper.ToggleMute();
            AudioHelper.PlaySound(SoundEffect.ButtonClick);
        }
        else if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus) || Input.GetKeyDown(KeyCode.LeftBracket))
        {
            AudioHelper.AdjustMasterVolume(-0.1f);
            AudioHelper.PlaySound(SoundEffect.ButtonClick);
        }
        else if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus) || Input.GetKeyDown(KeyCode.RightBracket))
        {
            AudioHelper.AdjustMasterVolume(0.1f);
            AudioHelper.PlaySound(SoundEffect.ButtonClick);
        }

        // Hotkey: F12 to toggle Dev Tools panel
        if (Input.GetKeyDown(KeyCode.F12))
        {
            isDevPanelOpen = !isDevPanelOpen;
        }

        // Hotkeys for Game Speed (1x, 1.5x, 2x, 3x, or F to cycle)
        if (GameManager.Instance != null)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) GameManager.Instance.SetGameSpeed(1.0f);
            else if (Input.GetKeyDown(KeyCode.Alpha2)) GameManager.Instance.SetGameSpeed(1.5f);
            else if (Input.GetKeyDown(KeyCode.Alpha3)) GameManager.Instance.SetGameSpeed(2.0f);
            else if (Input.GetKeyDown(KeyCode.Alpha4)) GameManager.Instance.SetGameSpeed(3.0f);
            else if (Input.GetKeyDown(KeyCode.F)) GameManager.Instance.CycleGameSpeed();

            // Combat Strike Hotkeys: Q (Fighter CAS), E (B-2 Stealth Bomber), R (ICBM Launcher)
            if (!isQuitConfirmOpen && GameManager.Instance.CurrentState == GameManager.GameState.Combat)
            {
                if (Input.GetKeyDown(KeyCode.Q) && AirSupportManager.Instance != null)
                {
                    AirSupportManager.Instance.ToggleFighterTargeting();
                }
                else if (Input.GetKeyDown(KeyCode.E) && AirSupportManager.Instance != null)
                {
                    AirSupportManager.Instance.ToggleB2Targeting();
                }
                else if (Input.GetKeyDown(KeyCode.R))
                {
                    Soldier.TogglePrimaryICBMTargeting();
                }
            }
        }
    }

    /// <summary>
    /// Checks whether the mouse cursor is over any active UI element.
    /// Used by PlacementManager to prevent placing buildings when clicking buttons!
    /// </summary>
    public bool IsPointerOverUI()
    {
        if (isDayTransitionActive || Time.frameCount <= dayTransitionSuppressFrame) return true;
        if (isQuitConfirmOpen) return true;
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.GameState.MainMenu)
        {
            return true;
        }
        if (!isMasterHudVisible) return false;

        // Convert Unity screen coordinates (bottom-left origin) to GUI coordinates (top-left origin)
        Vector2 mouseGuiPos = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);

        // 1. Build Menu panel or Open Button on left
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.GameState.Planning)
        {
            if (isBuildMenuOpen)
            {
                Rect menuRect = new Rect(15f, 78f, 485f, Mathf.Min(740f, Screen.height - 90f));
                if (menuRect.Contains(mouseGuiPos)) return true;
            }
            else
            {
                Rect openBtnRect = new Rect(15f, 78f, 225f, 42f);
                if (openBtnRect.Contains(mouseGuiPos)) return true;
            }

            // Start Wave button (bottom right)
            float startBtnW = 260f;
            float startBtnH = 52f;
            Rect startBtnRect = new Rect(Screen.width - startBtnW - 20f, Screen.height - startBtnH - 20f, startBtnW, startBtnH);
            if (startBtnRect.Contains(mouseGuiPos)) return true;

            // Bottom camera controls hint
            Rect bottomHintRect = new Rect(15f, Screen.height - 54f, 740f, 42f);
            if (bottomHintRect.Contains(mouseGuiPos)) return true;
        }

        // 2. Top HUD Bar, Game Speed Bar & Volume Controls Bar
        Rect topBarRect = new Rect(10, 26, Screen.width - 20, 44);
        if (topBarRect.Contains(mouseGuiPos)) return true;

        Rect speedPanelRect = new Rect(Screen.width - 365f, 76f, 355f, 38f);
        if (speedPanelRect.Contains(mouseGuiPos)) return true;

        Rect volumePanelRect = new Rect(Screen.width - 645f, 76f, 270f, 38f);
        if (volumePanelRect.Contains(mouseGuiPos)) return true;

        // 3. HUD Toggle button, Dev Tools button, Grid Toggle button & Quit button (top right)
        Rect hudToggleRect = new Rect(Screen.width - 128, 4, 118, 22);
        if (hudToggleRect.Contains(mouseGuiPos)) return true;

        Rect devToggleRect = new Rect(Screen.width - 274, 4, 140, 22);
        if (devToggleRect.Contains(mouseGuiPos)) return true;

        Rect gridToggleRect = new Rect(Screen.width - 402, 4, 122, 22);
        if (gridToggleRect.Contains(mouseGuiPos)) return true;

        Rect quitToggleRect = new Rect(Screen.width - 520, 4, 112, 22);
        if (quitToggleRect.Contains(mouseGuiPos)) return true;

        if (isDevPanelOpen && GameManager.Instance != null)
        {
            float devH = GameManager.Instance.isDevModeUnlocked ? 595f : 190f;
            Rect devPanelRect = new Rect(Screen.width - 385f, 118f, 375f, devH);
            if (devPanelRect.Contains(mouseGuiPos)) return true;
        }

        if (AirSupportManager.Instance != null)
        {
            if (AirSupportManager.Instance.CanOperateFighterInCombat() && GetFighterHudButtonRect().Contains(mouseGuiPos))
            {
                return true;
            }
            if (AirSupportManager.Instance.CanOperateB2InCombat() && GetB2HudButtonRect().Contains(mouseGuiPos))
            {
                return true;
            }
        }
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.GameState.Combat &&
            Soldier.GetPrimaryICBMLauncher() != null && GetIcbmHudButtonRect().Contains(mouseGuiPos))
        {
            return true;
        }

        // 4. Modal Dialogs (Reward, Route, Victory)
        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.CurrentState == GameManager.GameState.Reward)
            {
                Rect rewardDialog = new Rect(Screen.width / 2f - 300f, Screen.height / 2f - 128f, 600f, 255f);
                if (rewardDialog.Contains(mouseGuiPos)) return true;
            }
            else if (GameManager.Instance.CurrentState == GameManager.GameState.RouteSelection)
            {
                bool showElite = GameManager.Instance.isEliteRouteAvailable;
                bool showEvent = GameManager.Instance.isEventRouteAvailable;
                int cardCount = 1 + (showElite ? 1 : 0) + (showEvent ? 1 : 0);
                float rW = (cardCount == 3) ? 920f : ((cardCount == 2) ? 620f : 360f);
                float rH = 355f;
                Rect routeDialog = new Rect(Screen.width / 2f - rW / 2f, Screen.height / 2f - rH / 2f, rW, rH);
                if (routeDialog.Contains(mouseGuiPos)) return true;
            }
            else if (GameManager.Instance.CurrentState == GameManager.GameState.Victory)
            {
                Rect victoryDialog = new Rect(Screen.width / 2f - 300f, Screen.height / 2f - 165f, 600f, 330f);
                if (victoryDialog.Contains(mouseGuiPos)) return true;
            }
            else if (GameManager.Instance.CurrentState == GameManager.GameState.EventDilemma)
            {
                Rect dilemmaDialog = new Rect(Screen.width / 2f - 330f, Screen.height / 2f - 212f, 660f, 425f);
                if (dilemmaDialog.Contains(mouseGuiPos)) return true;
            }
            else if (GameManager.Instance.CurrentState == GameManager.GameState.GameOver)
            {
                Rect gameOverDialog = new Rect(Screen.width / 2f - 290f, Screen.height / 2f - 158f, 580f, 315f);
                if (gameOverDialog.Contains(mouseGuiPos)) return true;
            }
        }

        return false;
    }

    void InitStyles()
    {
        if (stylesInitialized) return;

        GameFontTheme.EnsureLoaded();
        if (GameFontTheme.BodyFont != null)
        {
            GUI.skin.font = GameFontTheme.BodyFont;
        }
        GUI.skin.label.fontSize = 13;
        GUI.skin.label.wordWrap = true;
        GUI.skin.button.fontSize = 12;
        GUI.skin.box.fontSize = 13;
        GUI.skin.textField.fontSize = 13;
        GUI.skin.textField.alignment = TextAnchor.MiddleLeft;

        Font headerFont   = GameFontTheme.HeaderFont;
        Font badgeFont    = GameFontTheme.HeaderRegularFont;
        Font bodyFont     = GameFontTheme.BodyFont;
        Font bodySemiBold = GameFontTheme.BodySemiBoldFont;
        Font bodyBold     = GameFontTheme.BodyBoldFont;

        headerStyle = new GUIStyle(GUI.skin.label)
        {
            font = headerFont,
            fontSize = 15,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = false,
            clipping = TextClipping.Overflow
        };
        headerStyle.normal.textColor = Color.white;

        subHeaderStyle = new GUIStyle(GUI.skin.label)
        {
            font = bodyBold,
            fontSize = 14,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleLeft,
            wordWrap = false,
            clipping = TextClipping.Overflow
        };
        subHeaderStyle.normal.textColor = new Color(1.0f, 0.75f, 0.2f); // Wasteland amber

        rewardCenteredStyle = new GUIStyle(GUI.skin.label)
        {
            font = bodyBold,
            fontSize = 15,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };
        rewardCenteredStyle.normal.textColor = new Color(1.0f, 0.78f, 0.22f);

        resourceStyle = new GUIStyle(GUI.skin.box)
        {
            font = bodyBold,
            fontSize = 13,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleCenter,
            richText = true,
            wordWrap = false,
            clipping = TextClipping.Overflow
        };
        resourceStyle.normal.textColor = Color.white;

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            font = bodyBold,
            fontSize = 12,
            fontStyle = FontStyle.Normal,
            wordWrap = false,
            clipping = TextClipping.Overflow
        };

        smallButtonStyle = new GUIStyle(GUI.skin.button)
        {
            font = bodySemiBold,
            fontSize = 12,
            fontStyle = FontStyle.Normal,
            wordWrap = false,
            clipping = TextClipping.Overflow
        };

        boxStyle = new GUIStyle(GUI.skin.box)
        {
            font = bodySemiBold,
            fontSize = 13,
            wordWrap = false
        };

        badgeStyle = new GUIStyle(GUI.skin.button)
        {
            font = badgeFont,
            fontSize = 13,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = false
        };
        badgeStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);

        activeBadgeStyle = new GUIStyle(GUI.skin.button)
        {
            font = badgeFont,
            fontSize = 13,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = false
        };
        activeBadgeStyle.normal.textColor = Color.yellow;

        descTitleStyle = new GUIStyle(GUI.skin.label)
        {
            font = bodyBold,
            fontSize = 14,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.UpperLeft,
            wordWrap = true
        };
        descTitleStyle.normal.textColor = new Color(0.3f, 0.9f, 1f);

        descBodyStyle = new GUIStyle(GUI.skin.label)
        {
            font = bodyFont,
            fontSize = 13,
            wordWrap = true,
            alignment = TextAnchor.UpperLeft
        };
        descBodyStyle.normal.textColor = Color.white;

        placementHintStyle = new GUIStyle(GUI.skin.box)
        {
            font = bodyBold,
            fontSize = 12,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = false,
            clipping = TextClipping.Overflow
        };

        panelHeaderStyle = new GUIStyle(GUI.skin.label)
        {
            font = badgeFont,
            fontSize = 13,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleLeft,
            wordWrap = false,
            clipping = TextClipping.Overflow
        };
        panelHeaderStyle.normal.textColor = Color.white;

        singleLineLabelStyle = new GUIStyle(GUI.skin.label)
        {
            font = bodySemiBold,
            fontSize = 12,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleLeft,
            wordWrap = false,
            clipping = TextClipping.Overflow
        };
        singleLineLabelStyle.normal.textColor = Color.white;

        stylesInitialized = true;
    }

    void OnGUI()
    {
        GUI.depth = -100;
        InitStyles();

        if (GameManager.Instance == null) return;

        if (GameManager.Instance.CurrentState == GameManager.GameState.MainMenu)
        {
            CancelDayTransition();
            DrawMainMenuUI();
            return;
        }

        // Intercept left-click while DAY transition is visible so clicking skips immediately without hitting UI beneath
        Event curEvt = Event.current;
        if (curEvt != null && curEvt.isMouse)
        {
            if (isDayTransitionActive && Time.frameCount > dayTransitionStartFrame && curEvt.type == EventType.MouseDown && curEvt.button == 0)
            {
                DismissDayTransitionImmediate();
                curEvt.Use();
            }
            else if (Time.frameCount <= dayTransitionSuppressFrame)
            {
                curEvt.Use();
            }
        }

        // Toggle master HUD button in corner
        if (GUI.Button(new Rect(Screen.width - 128, 4, 118, 22), isMasterHudVisible ? "👁️ Hide HUD (H)" : "👁️ Show HUD (H)", smallButtonStyle))
        {
            isMasterHudVisible = !isMasterHudVisible;
        }

        // Toggle Dev Tools button in top corner
        bool devUnlocked = GameManager.Instance.isDevModeUnlocked;
        GUI.backgroundColor = devUnlocked ? new Color(0.2f, 0.9f, 0.45f) : (isDevPanelOpen ? new Color(1f, 0.8f, 0.2f) : Color.white);
        if (GUI.Button(new Rect(Screen.width - 274, 4, 140, 22), devUnlocked ? "🛠️ Dev Tools (ON)" : "🔒 Dev Tools (F12)", smallButtonStyle))
        {
            isDevPanelOpen = !isDevPanelOpen;
        }
        GUI.backgroundColor = Color.white;

        // Toggle Grid visibility button in top corner
        bool isGridOn = CrossMapVisualizer.Instance == null || CrossMapVisualizer.Instance.isGridVisible;
        GUI.backgroundColor = isGridOn ? new Color(0.3f, 0.85f, 0.5f) : new Color(0.45f, 0.45f, 0.45f);
        if (GUI.Button(new Rect(Screen.width - 402, 4, 122, 22), isGridOn ? "▦ Grid: ON (G)" : "⬚ Grid: OFF (G)", smallButtonStyle))
        {
            if (CrossMapVisualizer.Instance != null)
            {
                CrossMapVisualizer.Instance.ToggleGridVisible();
                AudioHelper.PlaySound(SoundEffect.ButtonClick);
            }
        }

        // Quit / Exit game button in top corner
        GUI.backgroundColor = new Color(0.9f, 0.35f, 0.35f);
        if (GUI.Button(new Rect(Screen.width - 520, 4, 112, 22), "🚪 Quit (Esc)", smallButtonStyle))
        {
            isQuitConfirmOpen = !isQuitConfirmOpen;
            AudioHelper.PlaySound(SoundEffect.ButtonClick);
        }
        GUI.backgroundColor = Color.white;

        if (!isMasterHudVisible)
        {
            DrawDayTransitionOverlay();
            return;
        }

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
            case GameManager.GameState.EventDilemma:
                DrawEventDilemmaUI();
                break;
            case GameManager.GameState.Victory:
                DrawVictoryUI();
                break;
            case GameManager.GameState.GameOver:
                DrawGameOverUI();
                break;
        }

        if (isDevPanelOpen)
        {
            DrawDevToolsPanel();
        }

        if (isQuitConfirmOpen)
        {
            DrawQuitConfirmDialog();
        }

        DrawDayTransitionOverlay();
    }

    void DrawDayTransitionOverlay()
    {
        if (!isDayTransitionActive || dayTransitionAlpha <= 0.005f) return;

        if (blackFadeTexture == null)
        {
            blackFadeTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            blackFadeTexture.SetPixel(0, 0, Color.white);
            blackFadeTexture.Apply();
        }

        if (dayTransitionTitleStyle == null)
        {
            dayTransitionTitleStyle = new GUIStyle(GUI.skin.label)
            {
                font = GameFontTheme.HeaderFont,
                fontSize = 64,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Overflow
            };
            dayTransitionTitleStyle.normal.textColor = Color.white;
            dayTransitionTitleStyle.hover.textColor = Color.white;
        }

        if (dayTransitionHintStyle == null)
        {
            dayTransitionHintStyle = new GUIStyle(GUI.skin.label)
            {
                font = GameFontTheme.BodySemiBoldFont,
                fontSize = 14,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Overflow
            };
            dayTransitionHintStyle.normal.textColor = Color.white;
            dayTransitionHintStyle.hover.textColor = Color.white;
        }

        Color prevColor = GUI.color;
        Color prevContentColor = GUI.contentColor;

        GUI.contentColor = Color.white;
        GUI.color = new Color(0f, 0f, 0f, dayTransitionAlpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), blackFadeTexture);

        int dynamicSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.075f), 44, 72);
        dayTransitionTitleStyle.fontSize = dynamicSize;
        GUI.color = new Color(1f, 1f, 1f, dayTransitionAlpha);
        GUI.Label(new Rect(0f, Screen.height * 0.5f - 54f, Screen.width, 84f), $"DAY {dayTransitionNumber}", dayTransitionTitleStyle);

        GUI.color = new Color(0.68f, 0.72f, 0.78f, dayTransitionAlpha * 0.85f);
        GUI.Label(new Rect(0f, Screen.height * 0.5f + 42f, Screen.width, 28f), "[ Left-Click to Skip ]", dayTransitionHintStyle);

        GUI.contentColor = prevContentColor;
        GUI.color = prevColor;
    }

    void DrawMainMenuUI()
    {
        float panelW = 350f;
        float panelH = 185f;
        float panelX = Screen.width / 2f - panelW / 2f;
        float panelY = Screen.height * 0.65f;

        GUI.Box(new Rect(panelX, panelY, panelW, panelH), GUIContent.none, boxStyle);

        GUIStyle heroTitleStyle = new GUIStyle(headerStyle)
        {
            font = GameFontTheme.DisplayFont,
            fontSize = 20,
            fontStyle = FontStyle.Normal
        };
        GUI.Label(new Rect(panelX, panelY + 10f, panelW, 30f), "LAST BASTION Z", heroTitleStyle);

        GUIStyle startBtnStyle = new GUIStyle(buttonStyle)
        {
            font = GameFontTheme.HeaderFont,
            fontSize = 15,
            fontStyle = FontStyle.Normal
        };

        GUI.backgroundColor = new Color(0.25f, 0.85f, 0.35f);
        if (GUI.Button(new Rect(panelX + 35f, panelY + 48f, panelW - 70f, 52f), "▶ START GAME", startBtnStyle))
        {
            AudioHelper.PlaySound(SoundEffect.ButtonClick);
            GameManager.Instance.StartGameFromMenu();
        }

        GUIStyle quitBtnStyle = new GUIStyle(buttonStyle)
        {
            font = GameFontTheme.BodyBoldFont,
            fontSize = 15,
            fontStyle = FontStyle.Normal
        };

        GUI.backgroundColor = new Color(0.85f, 0.25f, 0.25f);
        if (GUI.Button(new Rect(panelX + 35f, panelY + 112f, panelW - 70f, 46f), "🚪 QUIT GAME", quitBtnStyle))
        {
            AudioHelper.PlaySound(SoundEffect.ButtonClick);
            QuitGame();
        }
        GUI.backgroundColor = Color.white;

        GUI.color = new Color(0.82f, 0.82f, 0.82f, 0.85f);
        GUI.Label(new Rect(0f, Screen.height - 28f, Screen.width, 24f),
            "Fonts: Silkscreen, JetBrains Mono & Pixelify Sans (SIL OFL)", rewardCenteredStyle);
        GUI.color = Color.white;
    }

    void DrawTopBar()
    {
        // Top HUD bar
        GUI.Box(new Rect(10, 26, Screen.width - 20, 44), GUIContent.none, boxStyle);

        string stageName = GameManager.Instance.GetStageName();
        string stateText = GameManager.Instance.CurrentState.ToString().ToUpper();

        float rightPanelW = 630f;
        float rightPanelX = Screen.width - rightPanelW - 15f;
        float leftLabelW = Mathf.Max(220f, rightPanelX - 25f);
        GUI.Label(new Rect(20, 32, leftLabelW, 32), $"🏰 LAST FORTRESS Z  |  {stageName} ({stateText})", subHeaderStyle);

        if (ResourceManager.Instance != null)
        {
            int curSupply = Soldier.GetCurrentSupply();
            int maxSupply = Soldier.MAX_SUPPLY;
            string supColor = (curSupply >= maxSupply) ? "#ff4d4d" : ((curSupply >= Mathf.FloorToInt(maxSupply * 0.85f)) ? "#ffcc00" : "#66e0ff");
            string supTag = (curSupply >= maxSupply) ? " [MAX]" : "";
            string resText = $"💰 Gold: {ResourceManager.Instance.Gold}   |   🧱 Mats: {ResourceManager.Instance.Materials}   |   💎 Spec: {ResourceManager.Instance.SpecialMaterials}   |   📦 <color={supColor}>SUP: {curSupply}/{maxSupply}{supTag}</color>";
            GUI.Box(new Rect(rightPanelX, 30, rightPanelW, 36), resText, resourceStyle);
        }

        DrawGameSpeedControls();
        DrawVolumeControls();
    }

    void DrawGameSpeedControls()
    {
        float panelW = 355f;
        float panelH = 38f;
        float panelX = Screen.width - panelW - 10f;
        float panelY = 76f;

        GUI.Box(new Rect(panelX, panelY, panelW, panelH), GUIContent.none, boxStyle);
        GUI.Label(new Rect(panelX + 10f, panelY + 7f, 118f, 24f), "⏩ Speed (F):", singleLineLabelStyle);

        float[] speeds = GameManager.SpeedOptions;
        string[] labels = new string[] { "1x", "1.5x", "2x", "3x" };
        float btnW = 52f;
        float btnH = 26f;
        float startX = panelX + 128f;

        for (int i = 0; i < speeds.Length; i++)
        {
            bool isSelected = Mathf.Approximately(GameManager.Instance.CurrentGameSpeed, speeds[i]);
            GUI.backgroundColor = isSelected ? new Color(0.25f, 0.9f, 0.4f) : new Color(0.35f, 0.35f, 0.35f);

            if (GUI.Button(new Rect(startX + i * (btnW + 4f), panelY + 6f, btnW, btnH), labels[i], isSelected ? activeBadgeStyle : smallButtonStyle))
            {
                GameManager.Instance.SetGameSpeed(speeds[i]);
            }
        }
        GUI.backgroundColor = Color.white;
    }

    void DrawVolumeControls()
    {
        float panelW = 270f;
        float panelH = 38f;
        float speedW = 355f;
        float panelX = Screen.width - speedW - 10f - panelW - 10f;
        float panelY = 76f;

        GUI.Box(new Rect(panelX, panelY, panelW, panelH), GUIContent.none, boxStyle);

        // Mute / Speaker icon button
        string volIcon = (AudioHelper.IsMuted || AudioHelper.MasterVolume <= 0.001f) ? "🔇" :
                         (AudioHelper.MasterVolume < 0.35f ? "🔈" :
                         (AudioHelper.MasterVolume < 0.7f ? "🔉" : "🔊"));
        GUI.backgroundColor = AudioHelper.IsMuted ? new Color(1f, 0.35f, 0.35f) : new Color(0.25f, 0.25f, 0.25f);
        if (GUI.Button(new Rect(panelX + 6f, panelY + 6f, 34f, 26f), volIcon, smallButtonStyle))
        {
            AudioHelper.ToggleMute();
            AudioHelper.PlaySound(SoundEffect.ButtonClick);
        }
        GUI.backgroundColor = Color.white;

        // Volume minus button
        if (GUI.Button(new Rect(panelX + 44f, panelY + 6f, 24f, 26f), "-", smallButtonStyle))
        {
            AudioHelper.AdjustMasterVolume(-0.1f);
            AudioHelper.PlaySound(SoundEffect.ButtonClick);
        }

        // Horizontal Volume Slider
        float curVol = AudioHelper.IsMuted ? 0f : AudioHelper.MasterVolume;
        float newVol = GUI.HorizontalSlider(new Rect(panelX + 72f, panelY + 12f, 96f, 16f), curVol, 0f, 1f);
        if (Mathf.Abs(newVol - curVol) > 0.005f)
        {
            AudioHelper.SetMasterVolume(newVol);
        }

        // Volume plus button
        if (GUI.Button(new Rect(panelX + 172f, panelY + 6f, 24f, 26f), "+", smallButtonStyle))
        {
            AudioHelper.AdjustMasterVolume(0.1f);
            AudioHelper.PlaySound(SoundEffect.ButtonClick);
        }

        // Volume percentage label / Quick Mute toggle
        string pctText = AudioHelper.IsMuted ? "MUTE" : $"{Mathf.RoundToInt(AudioHelper.MasterVolume * 100f)}%";
        GUI.backgroundColor = AudioHelper.IsMuted ? new Color(1f, 0.35f, 0.35f) : new Color(0.25f, 0.25f, 0.25f);
        if (GUI.Button(new Rect(panelX + 200f, panelY + 6f, 64f, 26f), pctText, smallButtonStyle))
        {
            AudioHelper.ToggleMute();
            AudioHelper.PlaySound(SoundEffect.ButtonClick);
        }
        GUI.backgroundColor = Color.white;
    }

    void DrawPlanningUI()
    {
        float menuX = 15f;
        float menuY = 78f;
        float menuW = 485f;
        float menuH = Mathf.Min(740f, Screen.height - 90f);

        // If Build Menu is toggled closed, show open button
        if (!isBuildMenuOpen)
        {
            GUI.backgroundColor = new Color(0.2f, 0.6f, 1f);
            if (GUI.Button(new Rect(menuX, menuY, 225f, 42f), "▶ 🛠️ Build Menu (TAB)", buttonStyle))
            {
                isBuildMenuOpen = true;
            }
            GUI.backgroundColor = Color.white;

            // Quick placement info if placing while menu collapsed
            if (PlacementManager.Instance != null && PlacementManager.Instance.isPlacing)
            {
                GUI.Box(new Rect(menuX, menuY + 50f, 340f, 52f), $"Placing: {GetBuildingDisplayName(PlacementManager.Instance.selectedType)}\nLeft-Click: Place | Right-Click: Cancel", placementHintStyle);
            }

            DrawPlanningBottomBar();
            return;
        }

        // Full Build Menu Panel Background
        GUI.Box(new Rect(menuX, menuY, menuW, menuH), GUIContent.none, boxStyle);

        // Header Title (Left-aligned with dedicated style so it never overlaps the Hide button)
        GUI.Label(new Rect(menuX + 12f, menuY + 4f, menuW - 125f, 24f), "🛠️ BASE DEFENSE ARSENAL", panelHeaderStyle);

        // Hide Menu Button at top-right of panel
        if (GUI.Button(new Rect(menuX + menuW - 108f, menuY + 4f, 100f, 24f), "◀ Hide (TAB)", smallButtonStyle))
        {
            isBuildMenuOpen = false;
        }

        float btnStartX = menuX + 8f;
        float curY = menuY + 30f;
        float colW = (menuW - 24f) / 2f;
        float badgeW = 36f;
        float btnW = colW - badgeW - 4f;
        float btnH = 42f;

        // --- 📋 1-CLICK AUTO-REBUILD BASE BUTTON (Restores previous day layout) ---
        if (PlacementManager.Instance != null && PlacementManager.Instance.HasSavedLayout())
        {
            int savedCount = PlacementManager.Instance.GetSavedLayoutCount();
            GUI.backgroundColor = new Color(0.18f, 0.72f, 0.95f);
            if (GUI.Button(new Rect(btnStartX, curY, menuW - 16f, 28f), $"📋 REBUILD PREVIOUS BASE ({savedCount} Placed)", buttonStyle))
            {
                PlacementManager.Instance.RestorePreviousLayout();
            }
            GUI.backgroundColor = Color.white;
            curY += 32f;
        }

        // --- CATEGORY TAB SELECTOR: UNIT BASES VS FRONTLINE FORTIFICATIONS VS AIR SUPPORT ---
        bool hasAirBase = (Building.GetActiveBuildingCount(Building.BuildingType.AirBase) > 0);
        if (!hasAirBase && currentBuildTab == BuildMenuTab.AirSupport)
        {
            currentBuildTab = BuildMenuTab.UnitBases;
        }

        int numTabs = hasAirBase ? 3 : 2;
        float tabGap = 6f;
        float tabW = (menuW - 16f - (numTabs - 1) * tabGap) / numTabs;

        bool isUnitTab = (currentBuildTab == BuildMenuTab.UnitBases);
        GUI.backgroundColor = isUnitTab ? new Color(0.22f, 0.75f, 1.0f) : new Color(0.32f, 0.32f, 0.36f);
        if (GUI.Button(new Rect(btnStartX, curY, tabW, 28f), hasAirBase ? "⛺ Unit Bases" : "⛺ Unit Bases (Units)", buttonStyle))
        {
            currentBuildTab = BuildMenuTab.UnitBases;
            if (Building.IsFrontlineFortification(inspectedType))
            {
                inspectedType = Building.BuildingType.SMGTent;
                descScrollPos = Vector2.zero;
            }
        }

        bool isFrontTab = (currentBuildTab == BuildMenuTab.FrontlineFortifications);
        GUI.backgroundColor = isFrontTab ? new Color(0.95f, 0.68f, 0.2f) : new Color(0.32f, 0.32f, 0.36f);
        if (GUI.Button(new Rect(btnStartX + tabW + tabGap, curY, tabW, 28f), hasAirBase ? "🛡️ Frontline" : "🛡️ Frontline Defenses", buttonStyle))
        {
            currentBuildTab = BuildMenuTab.FrontlineFortifications;
            inspectedType = Building.BuildingType.Barricade;
            descScrollPos = Vector2.zero;
            PlacementManager.Instance?.SelectBuildingToPlace(Building.BuildingType.Barricade);
        }

        if (hasAirBase)
        {
            bool isAirTab = (currentBuildTab == BuildMenuTab.AirSupport);
            GUI.backgroundColor = isAirTab ? new Color(0.35f, 0.85f, 0.45f) : new Color(0.32f, 0.32f, 0.36f);
            if (GUI.Button(new Rect(btnStartX + (tabW + tabGap) * 2f, curY, tabW, 28f), "✈️ Air Base", buttonStyle))
            {
                currentBuildTab = BuildMenuTab.AirSupport;
                descScrollPos = Vector2.zero;
                PlacementManager.Instance?.CancelPlacement();
            }
        }
        GUI.backgroundColor = Color.white;
        curY += 33f;

        if (currentBuildTab == BuildMenuTab.UnitBases)
        {
            // Row 1: SMG Tent & Sniper's Nest
            DrawBuildingRow(Building.BuildingType.SMGTent, "SMG Tent", 30, 10, btnStartX, curY, badgeW, btnW, btnH);
            DrawBuildingRow(Building.BuildingType.SniperTower, "Sniper's Nest", 60, 25, btnStartX + colW + 8f, curY, badgeW, btnW, btnH);
            curY += btnH + 4f;

            // Row 2: Heal Station & Special Force Depot (Stage 6+)
            DrawBuildingRow(Building.BuildingType.HealStation, "Heal Stn", 50, 20, btnStartX, curY, badgeW, btnW, btnH);
            DrawBuildingRow(Building.BuildingType.SpecialForceDepot, "Spec Force", 50, 30, btnStartX + colW + 8f, curY, badgeW, btnW, btnH);
            curY += btnH + 4f;

            // Row 3: Light Tank Factory (Stage 11+) & Supply Depot (+10 Max SUP)
            int supplyDepotGold = Building.GetGoldCost(Building.BuildingType.SupplyDepot);
            DrawBuildingRow(Building.BuildingType.LightTankFactory, "Light Tank", 600, 200, btnStartX, curY, badgeW, btnW, btnH);
            DrawBuildingRow(Building.BuildingType.SupplyDepot, "Supply Depot", supplyDepotGold, 0, btnStartX + colW + 8f, curY, badgeW, btnW, btnH);
            curY += btnH + 4f;

            // Row 4: Missile Foundry / ICBM Launcher (Stage 16+) & Air Base (1500G, 500M)
            DrawBuildingRow(Building.BuildingType.MissileFoundry, "ICBM Foundry", 900, 350, btnStartX, curY, badgeW, btnW, btnH);
            DrawBuildingRow(Building.BuildingType.AirBase, "Air Base", 1500, 500, btnStartX + colW + 8f, curY, badgeW, btnW, btnH);
            curY += btnH + 5f;
        }
        else if (currentBuildTab == BuildMenuTab.FrontlineFortifications)
        {
            // Frontline Fortification Tab (Barricade & Frontline Sniper Tower)
            // Row 1: Barricade & Sniper Tower
            DrawBuildingRow(Building.BuildingType.Barricade, "Barricade", 15, 5, btnStartX, curY, badgeW, btnW, btnH);
            DrawBuildingRow(Building.BuildingType.FrontlineSniperTower, "Sniper Tower", 100, 40, btnStartX + colW + 8f, curY, badgeW, btnW, btnH);
            curY += (btnH + 4f) * 3 + 5f;
        }
        else if (currentBuildTab == BuildMenuTab.AirSupport)
        {
            DrawAirSupportTabUI(btnStartX, curY, menuW - 16f, (btnH + 4f) * 4);
            curY += (btnH + 4f) * 4 + 5f;
        }

        // Row 5: Tactical Base Management (Relocate & Demolish)
        if (currentBuildTab != BuildMenuTab.AirSupport)
        {
            bool isRelocating = (PlacementManager.Instance != null && PlacementManager.Instance.currentMode == PlacementManager.PlacementMode.Relocate);
            bool isDemolishing = (PlacementManager.Instance != null && PlacementManager.Instance.currentMode == PlacementManager.PlacementMode.Demolish);

            GUI.backgroundColor = isRelocating ? new Color(1f, 0.85f, 0.2f) : Color.white;
            if (GUI.Button(new Rect(btnStartX, curY, colW, 28f), "🔧 Relocate", buttonStyle))
            {
                if (isRelocating) PlacementManager.Instance.CancelPlacement();
                else PlacementManager.Instance.SelectRelocateMode();
            }

            GUI.backgroundColor = isDemolishing ? new Color(1f, 0.35f, 0.35f) : Color.white;
            if (GUI.Button(new Rect(btnStartX + colW + 8f, curY, colW, 28f), "💥 Demolish (70%)", buttonStyle))
            {
                if (isDemolishing) PlacementManager.Instance.CancelPlacement();
                else PlacementManager.Instance.SelectDemolishMode();
            }
            GUI.backgroundColor = Color.white;
            curY += 34f;
        }

        // --- DETAILED TOWER / AIRCRAFT DESCRIPTION PANEL ---
        float descBoxH = (currentBuildTab == BuildMenuTab.AirSupport) ? 240f : 226f;
        GUI.Box(new Rect(btnStartX, curY, menuW - 16f, descBoxH), GUIContent.none, boxStyle);
        if (currentBuildTab == BuildMenuTab.AirSupport)
        {
            DrawAircraftDescription(btnStartX + 6f, curY + 6f, menuW - 28f, descBoxH - 12f);
        }
        else
        {
            DrawTowerDescription(btnStartX + 6f, curY + 6f, menuW - 28f, descBoxH - 12f);
        }
        curY += descBoxH + 6f;

        // --- PLACEMENT STATUS HINT ---
        if (PlacementManager.Instance != null && PlacementManager.Instance.currentMode == PlacementManager.PlacementMode.Place)
        {
            bool isDragging = PlacementManager.Instance.isDraggingPlacement;
            int queuedCount = PlacementManager.Instance.DragQueuedCount;
            int stored = PlacementManager.Instance.GetStoredCount(PlacementManager.Instance.selectedType);
            string bName = GetBuildingDisplayName(PlacementManager.Instance.selectedType);

            float hintH = 62f;
            GUI.backgroundColor = isDragging ? new Color(0.12f, 0.35f, 0.65f, 0.95f) : new Color(0.15f, 0.45f, 0.15f, 0.95f);
            GUI.Box(new Rect(btnStartX, curY, menuW - 16f, hintH), GUIContent.none, placementHintStyle);
            GUI.backgroundColor = Color.white;

            if (isDragging)
            {
                GUI.color = new Color(0.35f, 0.85f, 1.0f);
                GUI.Label(new Rect(btnStartX + 8, curY + 4, menuW - 32, 20), $"🔵 DRAGGING: {bName} ({queuedCount} Queued)", subHeaderStyle);
                GUI.color = Color.white;
                GUI.Label(new Rect(btnStartX + 8, curY + 24, menuW - 32, 18), "🖱️ Release Left-Click: Place All Queued", singleLineLabelStyle);
                GUI.color = new Color(1f, 0.7f, 0.7f);
                GUI.Label(new Rect(btnStartX + 8, curY + 42, menuW - 32, 18), "❌ Right-Click: Cancel Drag Batch", singleLineLabelStyle);
            }
            else
            {
                GUI.color = Color.green;
                string deployMode = stored > 0 ? $"[FREE Stock: {stored} Left]" : "[Purchasing New]";
                GUI.Label(new Rect(btnStartX + 8, curY + 4, menuW - 32, 20), $"📍 PLACING: {bName}", subHeaderStyle);
                GUI.color = Color.white;
                GUI.Label(new Rect(btnStartX + 8, curY + 24, menuW - 32, 18), $"Mode: {deployMode}  |  ❌ R-Click: Cancel", singleLineLabelStyle);
                int maxLimit = Building.GetMaxBuildingPlacementLimit(PlacementManager.Instance.selectedType);
                int activeCount = Building.GetActiveBuildingCount(PlacementManager.Instance.selectedType);
                if (maxLimit > 0 && activeCount >= maxLimit)
                {
                    GUI.color = new Color(1f, 0.4f, 0.4f);
                    GUI.Label(new Rect(btnStartX + 8, curY + 42, menuW - 32, 18), $"⚠️ Max {maxLimit} Limit Reached ({activeCount}/{maxLimit})!", singleLineLabelStyle);
                    GUI.color = Color.white;
                }
                else
                {
                    GUI.Label(new Rect(btnStartX + 8, curY + 42, menuW - 32, 18), "🖱️ Click or Hold-Drag: Multi-Place", singleLineLabelStyle);
                }
            }
        }
        else if (PlacementManager.Instance != null && PlacementManager.Instance.currentMode == PlacementManager.PlacementMode.Demolish)
        {
            GUI.backgroundColor = new Color(0.5f, 0.1f, 0.1f, 0.95f);
            GUI.Box(new Rect(btnStartX, curY, menuW - 16f, 52f), GUIContent.none, placementHintStyle);
            GUI.backgroundColor = Color.white;

            GUI.color = new Color(1f, 0.4f, 0.4f);
            GUI.Label(new Rect(btnStartX + 8, curY + 4, menuW - 32, 20), "💥 DEMOLISH / SELL MODE (70% Refund)", subHeaderStyle);
            GUI.color = Color.white;
            GUI.Label(new Rect(btnStartX + 8, curY + 26, menuW - 32, 22), "🖱️ Left-Click building to sell   |   ❌ Right-Click: Cancel", singleLineLabelStyle);
        }
        else if (PlacementManager.Instance != null && PlacementManager.Instance.currentMode == PlacementManager.PlacementMode.Relocate)
        {
            GUI.backgroundColor = new Color(0.5f, 0.4f, 0.1f, 0.95f);
            GUI.Box(new Rect(btnStartX, curY, menuW - 16f, 52f), GUIContent.none, placementHintStyle);
            GUI.backgroundColor = Color.white;

            GUI.color = new Color(1f, 0.9f, 0.2f);
            GUI.Label(new Rect(btnStartX + 8, curY + 4, menuW - 32, 20), "🔧 RELOCATE MODE", subHeaderStyle);
            GUI.color = Color.white;
            GUI.Label(new Rect(btnStartX + 8, curY + 26, menuW - 32, 22), "🖱️ Click building, then new cell  |  ❌ R-Click: Cancel", singleLineLabelStyle);
        }
        else
        {
            bool crossUnlocked = (GameManager.Instance != null && GameManager.Instance.CurrentDay > 15);
            string sectorInfo = crossUnlocked ? "🔓 Cross Arms: Unlocked (North/South active)" : "🔒 Cross Arms: Locked until Stage 15";
            GUI.Box(new Rect(btnStartX, curY, menuW - 16f, 48f), $"Click building to place. Stock deploys FREE!\n{sectorInfo}", descBodyStyle);
        }

        DrawPlanningBottomBar();
    }

    void DrawBuildingRow(Building.BuildingType type, string shortName, int goldCost, int matCost, float x, float y, float badgeW, float btnW, float btnH)
    {
        int storedCount = PlacementManager.Instance != null ? PlacementManager.Instance.GetStoredCount(type) : 0;
        bool isInspected = (inspectedType == type);
        bool isUnlocked = Building.IsBuildingUnlocked(type);

        // 1. NUMBER BADGE ON THE LEFT (Clicking badge ALSO selects this building!)
        if (!isUnlocked)
        {
            GUI.backgroundColor = new Color(0.35f, 0.3f, 0.25f);
            if (GUI.Button(new Rect(x, y, badgeW, btnH), "🔒", badgeStyle))
            {
                inspectedType = type;
                descScrollPos = Vector2.zero;
                PlacementManager.Instance.SelectBuildingToPlace(type);
            }
            GUI.backgroundColor = Color.white;
        }
        else if (storedCount > 0)
        {
            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.3f);
            if (GUI.Button(new Rect(x, y, badgeW, btnH), $"{storedCount}", activeBadgeStyle))
            {
                inspectedType = type;
                descScrollPos = Vector2.zero;
                PlacementManager.Instance.SelectBuildingToPlace(type);
            }
            GUI.backgroundColor = Color.white;
        }
        else
        {
            if (GUI.Button(new Rect(x, y, badgeW, btnH), "0", badgeStyle))
            {
                inspectedType = type;
                descScrollPos = Vector2.zero;
                PlacementManager.Instance.SelectBuildingToPlace(type);
            }
        }

        // 2. SELECTION BUTTON ON THE RIGHT
        if (!isUnlocked)
        {
            GUI.backgroundColor = isInspected ? new Color(0.65f, 0.5f, 0.25f) : new Color(0.45f, 0.42f, 0.38f);
        }
        else
        {
            GUI.backgroundColor = isInspected ? new Color(0.3f, 0.7f, 1.0f) : Color.white;
        }

        int reqStage = (type == Building.BuildingType.AirBase) ? 30
            : ((type == Building.BuildingType.MissileFoundry) ? 15
            : ((type == Building.BuildingType.LightTankFactory) ? 10 : 5));
        int upgLv = Building.GetUpgradeLevel(type);
        string lvBadge = (upgLv > 0) ? $" ★{upgLv}" : "";

        int maxLimit = Building.GetMaxBuildingPlacementLimit(type);
        int activeCount = Building.GetActiveBuildingCount(type);
        bool isUncond = GameManager.IsDevUnconditional;
        bool isFreeDev = GameManager.IsDevFreeEverything;
        bool isLimitReached = !isUncond && (maxLimit > 0 && activeCount >= maxLimit);
        string limitTag = (maxLimit > 0) ? $" [{activeCount}/{maxLimit}]" : "";

        int specCost = Building.GetSpecialMaterialCost(type);
        string costLabel = (specCost > 0)
            ? $"({goldCost}G/{matCost}M/{specCost}💎)"
            : ((matCost > 0) ? $"({goldCost}G / {matCost}M)" : $"({goldCost}G / +10 SUP)");
        string subText = !isUnlocked
            ? $"🔒 After Stg {reqStage}"
            : (isLimitReached ? $"⛔ MAX {maxLimit} REACHED" : (isFreeDev ? "★ FREE [DEV]" : (storedCount > 0 ? "★ FREE Stock" : costLabel)));
        if (GUI.Button(new Rect(x + badgeW + 3f, y, btnW, btnH), $"{shortName}{lvBadge}{limitTag}\n{subText}", buttonStyle))
        {
            inspectedType = type;
            descScrollPos = Vector2.zero;
            PlacementManager.Instance.SelectBuildingToPlace(type);
        }
        GUI.backgroundColor = Color.white;
    }

    void DrawPlanningBottomBar()
    {
        // Start Combat Button (Bottom Right)
        float startBtnW = 260f;
        float startBtnH = 52f;
        float startBtnX = Screen.width - startBtnW - 20f;
        float startBtnY = Screen.height - startBtnH - 20f;

        GUI.backgroundColor = new Color(0.9f, 0.2f, 0.2f);
        if (GUI.Button(new Rect(startBtnX, startBtnY, startBtnW, startBtnH), "⚔️ START DEFENSE WAVE", buttonStyle))
        {
            GameManager.Instance.StartCombat();
        }
        GUI.backgroundColor = Color.white;

        // Camera guide hint (Bottom Left/Center)
        GUI.Box(new Rect(15f, Screen.height - 50, 740, 38), "Controls: WASD: Pan | Scroll: Zoom | V: View | G: Grid | M: Mute | TAB: Menu | H: HUD", boxStyle);
    }

    public static Rect GetFighterHudButtonRect()
    {
        return new Rect(20f, Screen.height - 108f, 205f, 88f);
    }

    public static Rect GetB2HudButtonRect()
    {
        bool fighterActive = (AirSupportManager.Instance != null && AirSupportManager.Instance.CanOperateFighterInCombat());
        float x = fighterActive ? 237f : 20f;
        return new Rect(x, Screen.height - 108f, 205f, 88f);
    }

    public static Rect GetIcbmHudButtonRect()
    {
        bool fighterActive = (AirSupportManager.Instance != null && AirSupportManager.Instance.CanOperateFighterInCombat());
        bool b2Active = (AirSupportManager.Instance != null && AirSupportManager.Instance.CanOperateB2InCombat());
        float x = 20f + (fighterActive ? 217f : 0f) + (b2Active ? 217f : 0f);
        return new Rect(x, Screen.height - 108f, 205f, 88f);
    }

    void DrawAirSupportTabUI(float startX, float curY, float width, float totalH)
    {
        // Air Support Fleet Selection Cards
        float cardH = 84f;
        float cardGap = 8f;

        bool fighterOwned = (AirSupportManager.Instance != null && AirSupportManager.Instance.IsFighterOwned);
        bool canAffordFighter = (AirSupportManager.Instance != null && AirSupportManager.Instance.CanAffordFighter());
        bool isDevFree = GameManager.IsDevFreeEverything;
        int fighterLv = (AirSupportManager.Instance != null) ? AirSupportManager.Instance.FighterUpgradeLevel : 0;
        int fighterW = (AirSupportManager.Instance != null) ? AirSupportManager.Instance.GetFighterBlockWidth() : 2;
        int fighterL = (AirSupportManager.Instance != null) ? AirSupportManager.Instance.GetFighterBlockLength() : 5;
        int fighterPlanes = (AirSupportManager.Instance != null) ? AirSupportManager.Instance.GetFighterPlaneCount() : 1;
        string fighterStarBadge = fighterOwned ? (fighterLv > 0 ? $" [Lv.{fighterLv}/2 {new string('★', fighterLv)}]" : " [Lv.0/2]") : "";

        // --- Card 1: Fighter Jet ---
        bool isJetSelected = (SelectedAircraft == AircraftType.FighterJet);
        GUI.backgroundColor = isJetSelected ? new Color(0.2f, 0.55f, 0.85f, 0.95f) : new Color(0.18f, 0.22f, 0.26f, 0.9f);
        GUI.Box(new Rect(startX, curY, width, cardH), GUIContent.none, boxStyle);
        GUI.backgroundColor = Color.white;

        // Aircraft Icon / Select Button
        GUI.backgroundColor = fighterOwned ? new Color(0.25f, 0.9f, 0.45f) : (isJetSelected ? new Color(0.35f, 0.95f, 1.0f) : new Color(0.45f, 0.55f, 0.65f));
        if (GUI.Button(new Rect(startX + 8f, curY + 8f, 54f, 68f), "✈️\n\nJET", buttonStyle))
        {
            SelectedAircraft = AircraftType.FighterJet;
            descScrollPos = Vector2.zero;
            AudioHelper.PlaySound(SoundEffect.ButtonClick);
        }

        // Aircraft Details
        GUI.Label(new Rect(startX + 70f, curY + 8f, width - 172f, 22f), $"FIGHTER{fighterStarBadge}", subHeaderStyle);
        GUI.Label(new Rect(startX + 70f, curY + 32f, width - 172f, 20f), $"• {fighterW}x{fighterL} ({fighterPlanes} Plane{(fighterPlanes > 1 ? "s" : "")}, 7s CD)", singleLineLabelStyle);
        GUI.color = fighterOwned ? new Color(0.35f, 1.0f, 0.55f) : (canAffordFighter ? new Color(0.45f, 1.0f, 0.75f) : new Color(1.0f, 0.45f, 0.45f));
        string fighterCostLabel = fighterOwned
            ? "✓ OWNED — Combat Icon Ready!"
            : (isDevFree ? "🔩 Cost: FREE [DEV]" : $"🔩 Cost: {AirSupportManager.FIGHTER_MATERIAL_COST} Materials");
        GUI.Label(new Rect(startX + 70f, curY + 54f, width - 172f, 20f), fighterCostLabel, singleLineLabelStyle);
        GUI.color = Color.white;

        // Purchase / Owned Action Button on right
        GUI.backgroundColor = fighterOwned
            ? new Color(0.2f, 0.85f, 0.45f)
            : (canAffordFighter ? new Color(0.22f, 0.78f, 0.95f) : new Color(0.45f, 0.25f, 0.25f));
        string fighterBtnText = fighterOwned
            ? "OWNED\n✓ READY"
            : (isDevFree ? "BUILD\nFREE" : $"BUILD\n{AirSupportManager.FIGHTER_MATERIAL_COST} M");
        if (GUI.Button(new Rect(startX + width - 96f, curY + 20f, 88f, 44f), fighterBtnText, buttonStyle))
        {
            SelectedAircraft = AircraftType.FighterJet;
            descScrollPos = Vector2.zero;
            if (!fighterOwned && AirSupportManager.Instance != null)
            {
                AirSupportManager.Instance.TryPurchaseFighter();
            }
            else
            {
                AudioHelper.PlaySound(SoundEffect.ButtonClick);
            }
        }
        GUI.backgroundColor = Color.white;

        curY += cardH + cardGap;

        // --- Card 2: B-2 Stealth Bomber ---
        bool b2Owned = (AirSupportManager.Instance != null && AirSupportManager.Instance.IsB2Owned);
        bool canAffordB2 = (AirSupportManager.Instance != null && AirSupportManager.Instance.CanAffordB2());
        bool isB2Selected = (SelectedAircraft == AircraftType.B2Bomber);
        GUI.backgroundColor = isB2Selected ? new Color(0.2f, 0.55f, 0.85f, 0.95f) : new Color(0.16f, 0.19f, 0.23f, 0.9f);
        GUI.Box(new Rect(startX, curY, width, cardH), GUIContent.none, boxStyle);
        GUI.backgroundColor = Color.white;

        // Aircraft Icon / Select Button
        GUI.backgroundColor = b2Owned ? new Color(0.25f, 0.9f, 0.45f) : (isB2Selected ? new Color(0.35f, 0.95f, 1.0f) : new Color(0.38f, 0.42f, 0.48f));
        if (GUI.Button(new Rect(startX + 8f, curY + 8f, 54f, 68f), "💣\n\nB-2", buttonStyle))
        {
            SelectedAircraft = AircraftType.B2Bomber;
            descScrollPos = Vector2.zero;
            AudioHelper.PlaySound(SoundEffect.ButtonClick);
        }

        // Aircraft Details
        string b2Tag = b2Owned ? " [READY]" : "";
        GUI.Label(new Rect(startX + 70f, curY + 8f, width - 172f, 22f), $"B-2 STEALTH{b2Tag}", subHeaderStyle);
        GUI.Label(new Rect(startX + 70f, curY + 32f, width - 172f, 20f), $"• {AirSupportManager.B2_WIDTH_BLOCKS}x{AirSupportManager.B2_LENGTH_BLOCKS} ({AirSupportManager.B2_BOMB_COUNT} Bombs, {AirSupportManager.B2_COOLDOWN:0}s CD)", singleLineLabelStyle);
        GUI.color = b2Owned ? new Color(0.35f, 1.0f, 0.55f) : (canAffordB2 ? new Color(0.45f, 1.0f, 0.75f) : new Color(1.0f, 0.45f, 0.45f));
        string b2CostLabel = b2Owned
            ? "✓ OWNED — Tactical HUD Icon Ready!"
            : (isDevFree ? "🔩 Cost: FREE [DEV]" : $"🔩 Cost: {AirSupportManager.B2_MATERIAL_COST}M + {AirSupportManager.B2_SPECIAL_COST} 💎 Spec");
        GUI.Label(new Rect(startX + 70f, curY + 54f, width - 172f, 20f), b2CostLabel, singleLineLabelStyle);
        GUI.color = Color.white;

        // Purchase / Owned Action Button on right
        GUI.backgroundColor = b2Owned
            ? new Color(0.2f, 0.85f, 0.45f)
            : (canAffordB2 ? new Color(0.22f, 0.78f, 0.95f) : new Color(0.45f, 0.25f, 0.25f));
        string b2BtnText = b2Owned
            ? "OWNED\n✓ READY"
            : (isDevFree ? "BUILD\nFREE" : $"BUILD\n{AirSupportManager.B2_MATERIAL_COST}M+{AirSupportManager.B2_SPECIAL_COST}💎");
        if (GUI.Button(new Rect(startX + width - 96f, curY + 20f, 88f, 44f), b2BtnText, buttonStyle))
        {
            SelectedAircraft = AircraftType.B2Bomber;
            descScrollPos = Vector2.zero;
            if (!b2Owned && AirSupportManager.Instance != null)
            {
                AirSupportManager.Instance.TryPurchaseB2();
            }
            else
            {
                AudioHelper.PlaySound(SoundEffect.ButtonClick);
            }
        }
        GUI.backgroundColor = Color.white;
    }

    void DrawAircraftDescription(float x, float y, float viewW, float viewH)
    {
        string title;
        string body;
        var air = AirSupportManager.Instance;
        bool fighterOwned = (air != null && air.IsFighterOwned);
        int fighterLv = (air != null) ? air.FighterUpgradeLevel : 0;
        int maxLv = AirSupportManager.FIGHTER_MAX_UPGRADE_LEVEL;
        bool b2Owned = (air != null && air.IsB2Owned);

        if (SelectedAircraft == AircraftType.FighterJet)
        {
            string lvTag = fighterOwned ? (fighterLv > 0 ? $" [Lv.{fighterLv}/{maxLv} {new string('★', fighterLv)}]" : $" [Lv.0/{maxLv}]") : "";
            title = $"✈️ FIGHTER{lvTag} (Close Air Support)";
            string ownershipLine = fighterOwned
                ? "<color=#55ff55>✓ Status: BUILT — Click HUD Icon during Combat to Strike!</color>\n"
                : $"• Build Cost: {AirSupportManager.FIGHTER_MATERIAL_COST} Materials (0 Gold)\n";
            int planeCount = (air != null) ? air.GetFighterPlaneCount() : 1;
            int areaW = (air != null) ? air.GetFighterBlockWidth() : 2;
            int areaL = (air != null) ? air.GetFighterBlockLength() : 5;
            float dmgMult = (air != null) ? air.GetFighterDamageMultiplier() : 1.0f;
            float totalStrafeDmg = (air != null) ? air.GetFighterTotalDamage() : (AirSupportManager.FIGHTER_SHOTS_PER_BURST * AirSupportManager.FIGHTER_DAMAGE_PER_SHOT);

            body =
                ownershipLine +
                $"• Squadron: {planeCount} Fighter{(planeCount > 1 ? "s" : "")} | Area: {areaW}x{areaL} Blocks\n" +
                $"• Total Burst Damage: {totalStrafeDmg:0.#} Dmg (+{(dmgMult - 1f) * 100f:0}% Bonus, 6 Shots/Plane)\n" +
                $"• Flight Pattern: Left → Dive Strafe → Right (Repeats every {AirSupportManager.FIGHTER_REATTACK_COOLDOWN:0}s)\n" +
                "• Re-Target: Click HUD Icon or Press [Q] in battle to relocate target!";
        }
        else
        {
            title = "💣 B-2 STEALTH BOMBER (Carpet Strike)";
            string b2StatusLine = b2Owned
                ? "<color=#55ff55>✓ Status: BUILT — Click HUD Icon or Press [E] in Combat!</color>\n"
                : $"• Build Cost: {AirSupportManager.B2_MATERIAL_COST} Materials + {AirSupportManager.B2_SPECIAL_COST} 💎 Special (0 Gold)\n";
            body =
                b2StatusLine +
                $"• Strike Zone: {AirSupportManager.B2_WIDTH_BLOCKS}x{AirSupportManager.B2_LENGTH_BLOCKS} Blocks ({AirSupportManager.B2_BOMB_COUNT} Heavy Bombs, 5x4 each)\n" +
                $"• Bomb Damage: {AirSupportManager.B2_BOMB_DAMAGE:0} AOE Dmg/Target\n" +
                $"• Targeting: Manual Strike [E] | {AirSupportManager.B2_COOLDOWN:0}s CD | ~1.7s Lead Time\n" +
                "• Flight Profile: Heavy Stealth Glide (Left → Right)";
        }

        float contentW = viewW - 16f;
        float titleH = Mathf.Max(20f, descTitleStyle.CalcHeight(new GUIContent(title), contentW));
        float bodyH = Mathf.Max(30f, descBodyStyle.CalcHeight(new GUIContent(body), contentW));

        string upgPreviewText = "";
        float upgPreviewH = 0f;
        float upgSectionH = 0f;
        if (SelectedAircraft == AircraftType.FighterJet)
        {
            if (!fighterOwned)
            {
                upgPreviewText = "ℹ️ Build Fighter above, then click the Plane Icon in Combat to select strike zone.";
                upgPreviewH = Mathf.Max(20f, descBodyStyle.CalcHeight(new GUIContent(upgPreviewText), contentW));
                upgSectionH = 8f + upgPreviewH + 4f;
            }
            else if (fighterLv >= maxLv)
            {
                upgPreviewText = "★ MAX UPGRADE (Lv.2): 3 Fighters | 4x5 Area | +30% Total Dmg (1,053)";
                upgPreviewH = Mathf.Max(20f, descBodyStyle.CalcHeight(new GUIContent(upgPreviewText), contentW));
                upgSectionH = 8f + upgPreviewH + 6f + 30f + 6f;
            }
            else
            {
                int nextLv = fighterLv + 1;
                upgPreviewText = (nextLv == 1)
                    ? "Lv.1 Bonus: +1 Plane (2 total), +15% Overall Dmg (931.5), Area Width +1 (3x5)"
                    : "Lv.2 Bonus: +1 Plane (3 total), +15% More Dmg (+30% = 1,053), Area Width +1 (4x5)";
                upgPreviewH = Mathf.Max(20f, descBodyStyle.CalcHeight(new GUIContent(upgPreviewText), contentW));
                upgSectionH = 8f + upgPreviewH + 6f + 30f + 6f;
            }
        }
        else
        {
            upgPreviewText = b2Owned
                ? $"✓ READY: Single-pass carpet strike — aim manually each {AirSupportManager.B2_COOLDOWN:0}s cooldown."
                : "ℹ️ Build B-2 above (450M + 5 Special), then click the B-2 Icon in Combat to drop 3 bombs.";
            upgPreviewH = Mathf.Max(20f, descBodyStyle.CalcHeight(new GUIContent(upgPreviewText), contentW));
            upgSectionH = 8f + upgPreviewH + 4f;
        }

        float totalContentH = titleH + 6f + bodyH + upgSectionH + 12f;
        Rect viewRect = new Rect(x, y, viewW, viewH);
        Rect contentRect = new Rect(0, 0, contentW, totalContentH);

        Event e = Event.current;
        if (e != null && e.type == EventType.ScrollWheel && viewRect.Contains(e.mousePosition))
        {
            descScrollPos.y += e.delta.y * 24f;
            float maxScroll = Mathf.Max(0f, totalContentH - viewH);
            descScrollPos.y = Mathf.Clamp(descScrollPos.y, 0f, maxScroll);
            e.Use();
        }
        descScrollPos.y = Mathf.Clamp(descScrollPos.y, 0f, Mathf.Max(0f, totalContentH - viewH));

        descScrollPos = GUI.BeginScrollView(viewRect, descScrollPos, contentRect, false, false);
        float curY = 0f;

        GUI.Label(new Rect(0f, curY, contentW, titleH), title, descTitleStyle);
        curY += titleH + 6f;

        GUI.Label(new Rect(0f, curY, contentW, bodyH), body, descBodyStyle);
        curY += bodyH + 8f;

        GUI.color = new Color(1f, 1f, 1f, 0.2f);
        GUI.Box(new Rect(0f, curY, contentW, 1f), GUIContent.none, boxStyle);
        GUI.color = Color.white;
        curY += 7f;

        if (SelectedAircraft == AircraftType.FighterJet)
        {
            if (!fighterOwned)
            {
                GUI.color = new Color(0.3f, 0.85f, 1.0f);
                GUI.Label(new Rect(0f, curY, contentW, upgPreviewH), upgPreviewText, descBodyStyle);
                GUI.color = Color.white;
            }
            else if (fighterLv >= maxLv)
            {
                GUI.color = new Color(1.0f, 0.85f, 0.25f);
                GUI.Label(new Rect(0f, curY, contentW, upgPreviewH), upgPreviewText, descBodyStyle);
                GUI.color = Color.white;
                curY += upgPreviewH + 6f;

                GUI.backgroundColor = new Color(0.85f, 0.68f, 0.15f);
                GUI.Box(new Rect(0f, curY, contentW, 30f), "🌟 MAX UPGRADE REACHED (LV.2 ★★)", buttonStyle);
                GUI.backgroundColor = Color.white;
            }
            else
            {
                int nextLv = fighterLv + 1;
                int matCost = (air != null) ? air.GetFighterUpgradeCost(nextLv) : 100;
                bool canAfford = (air != null && air.CanUpgradeFighter());
                bool isFreeDev = GameManager.IsDevFreeEverything;

                GUI.color = new Color(0.75f, 0.95f, 1.0f);
                GUI.Label(new Rect(0f, curY, contentW, upgPreviewH), upgPreviewText, descBodyStyle);
                GUI.color = Color.white;
                curY += upgPreviewH + 6f;

                GUI.backgroundColor = canAfford ? new Color(0.2f, 0.85f, 0.35f) : new Color(0.45f, 0.25f, 0.25f);
                string costStr = isFreeDev ? "FREE [DEV]" : $"{matCost} Materials";
                if (GUI.Button(new Rect(0f, curY, contentW, 30f), $"⬆️ UPGRADE FIGHTER LV.{nextLv} ({costStr})", buttonStyle))
                {
                    air?.TryUpgradeFighter();
                }
                GUI.backgroundColor = Color.white;
            }
        }
        else
        {
            GUI.color = b2Owned ? new Color(0.45f, 1.0f, 0.65f) : new Color(0.3f, 0.85f, 1.0f);
            GUI.Label(new Rect(0f, curY, contentW, upgPreviewH), upgPreviewText, descBodyStyle);
            GUI.color = Color.white;
        }

        GUI.EndScrollView();
    }

    void DrawTowerDescription(float x, float y, float viewW, float viewH)
    {
        GetTowerDescriptionData(inspectedType, out string title, out string body);

        int maxLv = Building.GetMaxUpgradeLevel(inspectedType);
        int curLv = Building.GetUpgradeLevel(inspectedType);
        string nextDesc = Building.GetNextUpgradeDescription(inspectedType);

        // Content width gives margin for vertical scrollbar (approx 16px)
        float contentW = viewW - 16f;

        float titleH = Mathf.Max(20f, descTitleStyle.CalcHeight(new GUIContent(title), contentW));
        float bodyH = Mathf.Max(30f, descBodyStyle.CalcHeight(new GUIContent(body), contentW));

        float upgH;
        float nextDescH = 0f;
        string noUpgText = "🔧 Upgrades: SMG Tent, Sniper's Nest & Spec Force";
        float noUpgH = 0f;
        if (maxLv <= 0)
        {
            noUpgH = Mathf.Max(20f, descBodyStyle.CalcHeight(new GUIContent(noUpgText), contentW));
            upgH = 8f + noUpgH + 4f;
        }
        else
        {
            nextDescH = Mathf.Max(20f, descBodyStyle.CalcHeight(new GUIContent(nextDesc), contentW));
            upgH = 8f + nextDescH + 6f + 30f + 6f; // separator + nextDesc + gap + button + bottom gap
        }

        float totalContentH = titleH + 4f + bodyH + 8f + upgH;

        Rect viewRect = new Rect(x, y, viewW, viewH);
        Rect contentRect = new Rect(0, 0, contentW, totalContentH);

        // Mouse wheel scroll handling
        Event e = Event.current;
        if (e != null && e.type == EventType.ScrollWheel && viewRect.Contains(e.mousePosition))
        {
            descScrollPos.y += e.delta.y * 24f;
            float maxScroll = Mathf.Max(0f, totalContentH - viewH);
            descScrollPos.y = Mathf.Clamp(descScrollPos.y, 0f, maxScroll);
            e.Use();
        }

        // Clamp scroll in case content size changed dynamically
        float currentMaxScroll = Mathf.Max(0f, totalContentH - viewH);
        descScrollPos.y = Mathf.Clamp(descScrollPos.y, 0f, currentMaxScroll);

        descScrollPos = GUI.BeginScrollView(viewRect, descScrollPos, contentRect, false, false);

        float curY = 0f;

        // 1. Title
        GUI.Label(new Rect(0f, curY, contentW, titleH), title, descTitleStyle);
        curY += titleH + 4f;

        // 2. Body Text
        GUI.Label(new Rect(0f, curY, contentW, bodyH), body, descBodyStyle);
        curY += bodyH + 8f;

        // 3. Subtle Divider Line
        GUI.color = new Color(1f, 1f, 1f, 0.2f);
        GUI.Box(new Rect(0f, curY, contentW, 1f), GUIContent.none, boxStyle);
        GUI.color = Color.white;
        curY += 7f;

        // 4. Upgrade Section
        if (maxLv <= 0)
        {
            GUI.color = new Color(0.65f, 0.65f, 0.65f);
            GUI.Label(new Rect(0f, curY, contentW, noUpgH), noUpgText, descBodyStyle);
            GUI.color = Color.white;
        }
        else
        {
            // Next upgrade preview text
            GUI.color = (curLv >= maxLv) ? new Color(1.0f, 0.85f, 0.25f) : new Color(0.75f, 0.95f, 1.0f);
            GUI.Label(new Rect(0f, curY, contentW, nextDescH), nextDesc, descBodyStyle);
            GUI.color = Color.white;
            curY += nextDescH + 6f;

            if (curLv >= maxLv)
            {
                GUI.backgroundColor = new Color(0.85f, 0.68f, 0.15f);
                GUI.Box(new Rect(0f, curY, contentW, 30f), "🌟 MAX UPGRADE REACHED (LV.3 ★★★)", buttonStyle);
                GUI.backgroundColor = Color.white;
            }
            else
            {
                int nextLv = curLv + 1;
                Building.GetUpgradeCost(inspectedType, nextLv, out int gCost, out int mCost);
                bool canAfford = Building.CanAffordUpgrade(inspectedType);
                bool isFreeDev = GameManager.IsDevFreeEverything;

                GUI.backgroundColor = canAfford ? new Color(0.2f, 0.85f, 0.35f) : new Color(0.45f, 0.25f, 0.25f);
                string costStr = isFreeDev ? "FREE [DEV]" : $"{gCost}G / {mCost}M";
                if (GUI.Button(new Rect(0f, curY, contentW, 30f), $"⬆️ UPGRADE TO LV.{nextLv} ({costStr})", buttonStyle))
                {
                    Building.TryUpgradeBuilding(inspectedType);
                }
                GUI.backgroundColor = Color.white;
            }
        }

        GUI.EndScrollView();
    }

    void GetTowerDescriptionData(Building.BuildingType bType, out string title, out string body)
    {
        int stored = PlacementManager.Instance != null ? PlacementManager.Instance.GetStoredCount(bType) : 0;
        string stockNotice = stored > 0 ? $"<color=#55ff55>★ Owned in Stock: {stored} (Deploys for FREE!)</color>\n" : "• Owned in Stock: 0 (Purchase required)\n";

        switch (bType)
        {
            case Building.BuildingType.SMGTent:
                int smgLv = Building.GetUpgradeLevel(Building.BuildingType.SMGTent);
                float smgMult = Building.GetDamageMultiplier(Building.BuildingType.SMGTent);
                float smgBulletDmg = 10f * smgMult;
                float smgDps = (3f * smgBulletDmg) / 0.90f;
                string smgStars = smgLv > 0 ? $" [Lv.{smgLv}/3 {new string('★', smgLv)}]" : " [Lv.0/3]";
                title = $"⛺ SMG TENT{smgStars}";

                string nadeInfo = (smgLv >= 3)
                    ? $"<color=#ffcc33>★ Lv.3 Active: 7% Reload Grenade ({smgDps * 2f:0.#} Dmg!)</color>"
                    : "★ Role: Rapid suppression bursts.";

                int smgActive = Building.GetActiveBuildingCount(Building.BuildingType.SMGTent);
                string smgCapNotice = (smgActive >= 10)
                    ? "<color=#ff5555>• Limit: 10/10 Placed (MAX REACHED)</color>\n"
                    : $"• Limit: Max 10 Tents (Active: {smgActive}/10)\n";

                body =
                    stockNotice +
                    smgCapNotice +
                    "• Cost: 30 Gold, 10 Materials | Supply: 1 SUP\n" +
                    "• Spawns: 1 SMG Trooper every 14.0s\n" +
                    "• Attack: 3-Round Burst (0.12s burst, 0.66s reload)\n" +
                    $"• Damage: {smgBulletDmg:0.#} dmg / bullet (+{(smgMult - 1f) * 100f:0}% Bonus)\n" +
                    $"• DPS: {smgDps:0.0} DPS (0.90s cycle)\n" +
                    nadeInfo;
                break;

            case Building.BuildingType.SniperTower:
                int sniperLv = Building.GetUpgradeLevel(Building.BuildingType.SniperTower);
                float sniperMult = Building.GetDamageMultiplier(Building.BuildingType.SniperTower);
                float sniperDmg = 37.5f * sniperMult;
                float sniperInterval = (sniperLv >= 3) ? 1.25f : 1.0f;
                float sniperDps = sniperDmg / sniperInterval;
                string sniperStars = sniperLv > 0 ? $" [Lv.{sniperLv}/3 {new string('★', sniperLv)}]" : " [Lv.0/3]";
                title = $"🎯 SNIPER'S NEST{sniperStars}";
                int sniperActive = Building.GetActiveBuildingCount(Building.BuildingType.SniperTower);
                int sniperLimit = Building.GetMaxBuildingPlacementLimit(Building.BuildingType.SniperTower);
                int activeSnipers = Building.CountActiveSnipers();
                string sniperCapNotice = (sniperActive >= sniperLimit)
                    ? $"<color=#ff5555>• Nest Limit: {sniperLimit}/{sniperLimit} Placed (MAX REACHED)</color>\n"
                    : $"• Nest Limit: Max {sniperLimit} Nests (Active: {sniperActive}/{sniperLimit})\n";
                string sniperUnitNotice = (activeSnipers >= Building.MAX_ACTIVE_SNIPERS)
                    ? $"<color=#ff5555>• Sniper Cap: {activeSnipers}/{Building.MAX_ACTIVE_SNIPERS} (MAX CAP)</color>\n"
                    : $"• Sniper Cap: Max {Building.MAX_ACTIVE_SNIPERS} Snipers (Active: {activeSnipers}/{Building.MAX_ACTIVE_SNIPERS})\n";

                string sniperAbility = (sniperLv >= 3)
                    ? "<color=#33ccff>★ Lv.3: Pierces 2 enemies with NO dropoff!</color>\n"
                    : "★ Ability: Pierces 1st enemy (-50% on 2nd).\n";

                body =
                    stockNotice +
                    sniperCapNotice +
                    sniperUnitNotice +
                    "• Cost: 60G, 25M | Supply: 2 SUP | Size: 2x1\n" +
                    $"• Spawns: 1 Sniper / 18.0s (Max {Building.MAX_ACTIVE_SNIPERS} active)\n" +
                    $"• Attack: Single Fire ({sniperInterval:0.00}s interval)\n" +
                    $"• Damage: {sniperDmg:0.#} dmg / shot (+{(sniperMult - 1f) * 100f:0}% Bonus)\n" +
                    $"• DPS: {sniperDps:0.0} DPS (Line penetrator)\n" +
                    "• Ballistics: 42 m/s Tracer (14m range)\n" +
                    sniperAbility;
                break;

            case Building.BuildingType.Barricade:
                title = "🚧 BARRICADE (Frontline Wall)";
                body =
                    stockNotice +
                    "• Cost: 15 Gold, 5 Materials | Supply: 0 SUP\n" +
                    "• Placement: Base & Frontline to Crossroads\n" +
                    "• Spawns: None (Static defensive structure)\n" +
                    "• Health: 220 HP (High durability)\n" +
                    "• Function: Solid physical obstacle\n" +
                    "★ Role: Bottlenecks and delays zombie hordes.";
                break;

            case Building.BuildingType.HealStation:
                title = "🏥 HEAL STATION (Field Medical Outpost)";
                body =
                    stockNotice +
                    "• Cost: 50 Gold, 20 Materials | Supply: 2 SUP\n" +
                    "• Spawns: 1 Field Medic every 20.0s\n" +
                    "• Medic Health: 70 HP (Tough unit)\n" +
                    "• Attack: 6 dmg / shot | Range: 7.0m\n" +
                    "★ Role: Heals squad & boosts frontline survival.";
                break;

            case Building.BuildingType.SpecialForceDepot:
                bool unlocked = Building.IsBuildingUnlocked(Building.BuildingType.SpecialForceDepot);
                string unlockStatus = unlocked
                    ? "<color=#55ff55>🔓 UNLOCKED (Cleared Stage 5+)</color>\n"
                    : "<color=#ffcc44>🔒 LOCKED: Cleared Stage 5+ (Stage 6+)</color>\n";
                int specLv = Building.GetUpgradeLevel(Building.BuildingType.SpecialForceDepot);
                float specHpMult = Building.GetHealthMultiplier(Building.BuildingType.SpecialForceDepot);
                float guardianHp = 175f * specHpMult;
                string specStars = specLv > 0 ? $" [Lv.{specLv}/3 {new string('★', specLv)}]" : " [Lv.0/3]";
                title = $"🛡️ SPECIAL FORCE DEPOT{specStars}";

                string pushInfo = (specLv >= 3)
                    ? "<color=#ffcc33>★ Lv.3: 30% Shield Push & Survives 2 Boss Hits!</color>"
                    : "★ Role: Fast, tanky frontline shield unit.";

                body =
                    unlockStatus +
                    stockNotice +
                    "• Cost: 50 Gold, 30 Materials | SUP: 3 | Size: 1x1\n" +
                    "• Spawns: Guardian every 13.0s (Max: 6)\n" +
                    $"• Health: {guardianHp:0.#} HP (+{(specHpMult - 1f) * 100f:0}% Bonus)\n" +
                    "• Speed: 4.5 m/s | DPS: 5.0 DPS (4 dmg / 0.8s)\n" +
                    "★ Perk: Blast Shield (50% Explosive Res)\n" +
                    pushInfo;
                break;

            case Building.BuildingType.LightTankFactory:
                bool tankUnlocked = Building.IsBuildingUnlocked(Building.BuildingType.LightTankFactory);
                string tankUnlockStatus = tankUnlocked
                    ? "<color=#55ff55>🔓 UNLOCKED (Cleared Stage 10+)</color>\n"
                    : "<color=#ffcc44>🔒 LOCKED: Cleared Stage 10+ (Stage 11+)</color>\n";
                int tankFactoryActive = Building.GetActiveBuildingCount(Building.BuildingType.LightTankFactory);
                string tankCapNotice = $"• Placement Limit: Max 4 Factories (Active: {tankFactoryActive}/4)\n";
                title = "🏭 LIGHT TANK FACTORY";
                body =
                    tankUnlockStatus +
                    stockNotice +
                    tankCapNotice +
                    "• Cost: 600 Gold, 200 Materials | SUP: 6 | Size: 2x2\n" +
                    "• Spawns: Light Tank every 22.0s (Max: 4 Tanks)\n" +
                    "• Attack: 4.0s / Shot | Circular AOE (3x3 Eq. Area)\n" +
                    "• DPS: 100 DPS (400 dmg/shot)\n" +
                    "• Speed: 1.1 m/s (Armored vehicle)\n" +
                    "★ Role: Heavy AOE artillery obliterates hordes.";
                break;

            case Building.BuildingType.MissileFoundry:
                bool icbmUnlocked = Building.IsBuildingUnlocked(Building.BuildingType.MissileFoundry);
                string icbmUnlockStatus = icbmUnlocked
                    ? "<color=#55ff55>🔓 UNLOCKED (Cleared Stage 15+)</color>\n"
                    : "<color=#ffcc44>🔒 LOCKED: Cleared Stage 15+ (Stage 16+)</color>\n";
                int foundryActive = Building.GetActiveBuildingCount(Building.BuildingType.MissileFoundry);
                string foundryCapNotice = (foundryActive >= 1)
                    ? "<color=#ff5555>• Placement Limit: 1/1 Placed (MAX REACHED)</color>\n"
                    : "• Placement Limit: Max 1 Base on Map (0/1)\n";
                title = "🚀 MISSILE FOUNDRY (ICBM Launcher)";
                body =
                    icbmUnlockStatus +
                    stockNotice +
                    foundryCapNotice +
                    "• Cost: 900G, 350M, 4 💎 Spec | SUP: 15 | 3x4\n" +
                    "• Spawns: 1 ICBM Launcher\n" +
                    "• Control: Press [R] or L-Click Truck -> L-Click Target\n" +
                    "• Stats: 85 DPS | 20.0s Reload (1,700 Dmg)\n" +
                    "• Blast Radius: Global Range | 8x8 Circular AOE\n" +
                    "★ Role: Tactical nuclear strike across map.";
                break;

            case Building.BuildingType.SupplyDepot:
                int curSupGold = Building.GetGoldCost(Building.BuildingType.SupplyDepot);
                int activeDepots = Building.GetActiveSupplyDepotCount();
                title = "📦 SUPPLY DEPOT (+10 Max Supply)";
                body =
                    stockNotice +
                    $"• Cost: {curSupGold} Gold (+50G per placement)\n" +
                    "• Effect: Increases Max SUP limit by +10\n" +
                    $"• Active on Map: {activeDepots} Depots (+{activeDepots * 10} SUP)\n" +
                    $"• Current Supply: {Soldier.MAX_SUPPLY} / {Soldier.HARD_MAX_SUPPLY} SUP\n" +
                    "• Health: 130 HP | Size: 1x1 Cell\n" +
                    "★ Role: Expands army Supply limit.";
                break;

            case Building.BuildingType.FrontlineSniperTower:
                title = "🗼 SNIPER TOWER (Frontline Watchtower)";
                body =
                    stockNotice +
                    "• Cost: 100 Gold, 40 Materials | Supply: 0 SUP\n" +
                    "• Placement: Base & Frontline to Crossroads\n" +
                    "• Attack: Heavy Fire (1.00s interval | 14m Range)\n" +
                    "• Damage: 75.0 dmg / shot (75.0 DPS)\n" +
                    "• Health: 160 HP | Stationary Steel Tower\n" +
                    "★ Role: High-damage stationary watchtower (Single-target).";
                break;

            case Building.BuildingType.AirBase:
                bool airUnlocked = Building.IsBuildingUnlocked(Building.BuildingType.AirBase);
                string airUnlockStatus = airUnlocked
                    ? "<color=#55ff55>🔓 UNLOCKED (Cleared Stage 30+ / Apocalypse City)</color>\n"
                    : "<color=#ffcc44>🔒 LOCKED: Cleared Stage 30+ (Stage 31+)</color>\n";
                int airBaseActive = Building.GetActiveBuildingCount(Building.BuildingType.AirBase);
                string airBaseCapNotice = (airBaseActive >= 1)
                    ? "<color=#ff5555>• Placement Limit: 1/1 Placed (MAX REACHED)</color>\n"
                    : "• Placement Limit: Max 1 Base on Map (0/1)\n";
                title = "🛫 AIR BASE (Air Command Facility)";
                body =
                    airUnlockStatus +
                    stockNotice +
                    airBaseCapNotice +
                    "• Cost: 1,500 Gold, 500 Materials | SUP: 0 | Size: 4x5\n" +
                    "• Feature: Unlocks [✈️ Air Base] Tab in Menu\n" +
                    "• Available Aircraft: Fighter Jet & B-2 Bomber\n" +
                    "• Fleet Cost: Materials (Fighter) & 💎 Special (B-2)\n" +
                    "★ Role: Strategic air strikes & carpet bombardment.";
                break;

            default:
                title = "BUILDING INFO";
                body = stockNotice;
                break;
        }
    }

    void DrawCombatUI()
    {
        // Combat status box
        float boxW = 520f;
        float boxH = 96f;
        float boxX = Screen.width / 2f - boxW / 2f;
        float boxY = 78f;

        bool isElite = (GameManager.Instance != null && GameManager.Instance.isEliteBattleRoute);
        string routeBadge = isElite ? " [⚡ ELITE ROUTE]" : "";
        GUI.Box(new Rect(boxX, boxY, boxW, boxH), $"⚠️ {GameManager.Instance.GetStageName().ToUpper()}{routeBadge}", boxStyle);

        if (WaveManager.Instance != null)
        {
            int curSup = Soldier.GetCurrentSupply();
            string supTag = (curSup >= Soldier.MAX_SUPPLY) ? $"  |  <color=#ff4d4d>SUP: {Soldier.MAX_SUPPLY} (CAP)</color>" : $"  |  SUP: {curSup}/{Soldier.MAX_SUPPLY}";
            int totalBosses = WaveManager.Instance.TotalBossesInCurrentWave;
            string bossTag = (totalBosses > 0) ? $"  |  💀 Tyrants: {totalBosses}" : "";
            GUI.Label(new Rect(boxX + 12, boxY + 30, boxW - 24, 26), $"Zombies: {WaveManager.Instance.zombiesRemaining}/{WaveManager.Instance.totalZombiesInWave}{bossTag}{supTag}", subHeaderStyle);
            float progress = (float)(WaveManager.Instance.totalZombiesInWave - WaveManager.Instance.zombiesRemaining) / Mathf.Max(1, WaveManager.Instance.totalZombiesInWave);
            GUI.HorizontalSlider(new Rect(boxX + 15, boxY + 60, boxW - 30, 20), progress, 0f, 1f);
        }

        // Combat Air Support Icon Button (Shown once Fighter is built & Air Base is active)
        if (AirSupportManager.Instance != null && AirSupportManager.Instance.CanOperateFighterInCombat())
        {
            var air = AirSupportManager.Instance;
            Rect btnRect = GetFighterHudButtonRect();
            int fWidth = air.GetFighterBlockWidth();
            int fLength = air.GetFighterBlockLength();
            int fPlanes = air.GetFighterPlaneCount();
            string lvBadge = (air.FighterUpgradeLevel > 0) ? $" {new string('★', air.FighterUpgradeLevel)}" : "";

            string statusLine;
            if (air.IsTargetingFighter)
            {
                GUI.backgroundColor = new Color(1.0f, 0.45f, 0.15f, 0.98f);
                statusLine = $"🎯 AIMING {fWidth}x{fLength} ZONE\nClick Map (R-Cancel)";
            }
            else if (!air.HasFighterTarget)
            {
                GUI.backgroundColor = new Color(0.20f, 0.78f, 0.98f, 0.96f);
                statusLine = $"✓ READY ({fPlanes}x {fWidth}x{fLength})\n[CLICK TO TARGET]";
            }
            else if (air.IsFighterFlying)
            {
                GUI.backgroundColor = new Color(1.0f, 0.72f, 0.18f, 0.96f);
                statusLine = (air.FighterArrivalETA > 0.05f)
                    ? $"✈️ INBOUND: {air.FighterArrivalETA:0.0}s\n(Click to Move)"
                    : $"🔥 {fPlanes}x STRAFING\n(Click to Move)";
            }
            else
            {
                GUI.backgroundColor = new Color(0.22f, 0.72f, 0.45f, 0.95f);
                statusLine = $"⏱️ NEXT: {air.FighterCooldownRemaining:0.0}s\n(Click to Move)";
            }

            if (GUI.Button(btnRect, $"✈️ FIGHTER [Q]{lvBadge}\n{statusLine}", buttonStyle))
            {
                air.ToggleFighterTargeting();
            }
            GUI.backgroundColor = Color.white;

            if (air.IsTargetingFighter)
            {
                GUI.backgroundColor = new Color(0.85f, 0.25f, 0.12f, 0.92f);
                GUI.Box(new Rect(Screen.width / 2f - 370f, boxY + boxH + 8f, 740f, 36f),
                    $"🎯 FIGHTER STRIKE ({fWidth}x{fLength} Blocks) — Left-Click Battlefield to Confirm | Right-Click to Cancel",
                    placementHintStyle);
                GUI.backgroundColor = Color.white;
            }
        }

        // Combat B-2 Stealth Bomber Icon Button (Shown once B-2 is built & Air Base is active)
        if (AirSupportManager.Instance != null && AirSupportManager.Instance.CanOperateB2InCombat())
        {
            var air = AirSupportManager.Instance;
            Rect b2Rect = GetB2HudButtonRect();

            string b2StatusLine;
            if (air.IsTargetingB2)
            {
                GUI.backgroundColor = new Color(1.0f, 0.38f, 0.12f, 0.98f);
                b2StatusLine = $"🎯 AIMING {AirSupportManager.B2_WIDTH_BLOCKS}x{AirSupportManager.B2_LENGTH_BLOCKS}\nClick Map (R-Cancel)";
            }
            else if (air.IsB2Flying)
            {
                GUI.backgroundColor = new Color(1.0f, 0.65f, 0.15f, 0.96f);
                b2StatusLine = (air.B2ArrivalETA > 0.05f)
                    ? $"✈️ B-2 ETA: {air.B2ArrivalETA:0.0}s\n⏳ CD: {air.B2CooldownRemaining:0.0}s"
                    : $"💣 CARPET BOMBING\n⏳ CD: {air.B2CooldownRemaining:0.0}s";
            }
            else if (air.B2CooldownRemaining > 0f)
            {
                GUI.backgroundColor = new Color(0.38f, 0.38f, 0.42f, 0.92f);
                b2StatusLine = $"⏳ REARMING\nCD: {air.B2CooldownRemaining:0.0}s";
            }
            else
            {
                GUI.backgroundColor = new Color(0.85f, 0.45f, 0.18f, 0.96f);
                b2StatusLine = $"✓ READY ({AirSupportManager.B2_WIDTH_BLOCKS}x{AirSupportManager.B2_LENGTH_BLOCKS})\n[CLICK TO TARGET]";
            }

            if (GUI.Button(b2Rect, $"💣 B-2 [E]\n{b2StatusLine}", buttonStyle))
            {
                air.ToggleB2Targeting();
            }
            GUI.backgroundColor = Color.white;

            if (air.IsTargetingB2)
            {
                float estEta = AirSupportManager.EstimateB2ArrivalTime(air.LastB2HoverPosition);
                GUI.backgroundColor = new Color(0.88f, 0.28f, 0.10f, 0.92f);
                GUI.Box(new Rect(Screen.width / 2f - 370f, boxY + boxH + 8f, 740f, 36f),
                    $"💣 B-2 ({AirSupportManager.B2_WIDTH_BLOCKS}x{AirSupportManager.B2_LENGTH_BLOCKS}, ETA ~{estEta:0.0}s from West Edge) — Aim Ahead! | L-Click: Bomb | R-Click: Cancel",
                    placementHintStyle);
                GUI.backgroundColor = Color.white;
            }
        }

        // Combat ICBM Launcher Icon Button [R] (Shown once ICBM Launcher is deployed on the map)
        Soldier primaryIcbm = Soldier.GetPrimaryICBMLauncher();
        if (primaryIcbm != null)
        {
            Rect icbmRect = GetIcbmHudButtonRect();
            bool isTargetingIcbm = (Soldier.ActiveTargetingICBM != null);
            string icbmStatusLine;
            if (isTargetingIcbm)
            {
                GUI.backgroundColor = new Color(1.0f, 0.28f, 0.12f, 0.98f);
                icbmStatusLine = "🎯 AIMING 8x8 NUKE\nL-Click Fire (R-Cancel)";
            }
            else if (primaryIcbm.ICBMCooldownRemaining > 0f)
            {
                GUI.backgroundColor = new Color(0.38f, 0.38f, 0.42f, 0.92f);
                icbmStatusLine = $"⏳ RELOADING\nCD: {primaryIcbm.ICBMCooldownRemaining:0.0}s";
            }
            else
            {
                GUI.backgroundColor = new Color(0.20f, 0.78f, 0.35f, 0.96f);
                icbmStatusLine = "✓ READY (8x8 AOE)\n[CLICK OR PRESS R]";
            }

            if (GUI.Button(icbmRect, $"🚀 ICBM [R]\n{icbmStatusLine}", buttonStyle))
            {
                Soldier.TogglePrimaryICBMTargeting();
            }
            GUI.backgroundColor = Color.white;

            if (isTargetingIcbm)
            {
                GUI.backgroundColor = new Color(0.92f, 0.20f, 0.10f, 0.92f);
                GUI.Box(new Rect(Screen.width / 2f - 370f, boxY + boxH + 8f, 740f, 36f),
                    "🚀 ICBM NUCLEAR STRIKE (8x8 AOE) — L-Click Battlefield to Fire | R-Click or [R] to Cancel",
                    placementHintStyle);
                GUI.backgroundColor = Color.white;
            }
        }
    }

    void DrawRewardUI()
    {
        float dialogW = 600f;
        float dialogH = 255f;
        float dialogX = Screen.width / 2f - dialogW / 2f;
        float dialogY = Screen.height / 2f - dialogH / 2f;

        bool isElite = (GameManager.Instance != null && GameManager.Instance.isEliteBattleRoute);
        string title = isElite ? "🔥 ELITE BATTLE CLEARED! (2X REWARDS!)" : "🎉 STAGE CLEARED SUCCESSFULLY!";
        GUI.Box(new Rect(dialogX, dialogY, dialogW, dialogH), title, boxStyle);

        int day = GameManager.Instance.CurrentDay;
        int goldReward = ResourceManager.GetStageGoldReward(day);
        int matReward = ResourceManager.GetStageMaterialReward(day);

        GUI.Label(new Rect(dialogX + 20, dialogY + 42, dialogW - 40, 34), $"Stage {day} Victory! Defense Line Held.", headerStyle);

        string milestoneTag = (day == 30)
            ? "+1,000G WAVE 30 MILESTONE BONUS!"
            : (day == 15 ? "+200G WAVE 15 MILESTONE BONUS!" : "");

        string bonusTag;
        if (isElite && !string.IsNullOrEmpty(milestoneTag))
        {
            bonusTag = $"(2x ELITE + {milestoneTag})";
        }
        else if (isElite)
        {
            bonusTag = "(2x ELITE BONUS!)";
        }
        else if (!string.IsNullOrEmpty(milestoneTag))
        {
            bonusTag = $"({milestoneTag})";
        }
        else
        {
            bonusTag = (day <= 4) ? "(+44% EARLY BONUS!)" : "";
        }

        GUI.Label(new Rect(dialogX + 20, dialogY + 84, dialogW - 40, 30), $"Rewards Earned: +{goldReward} Gold, +{matReward} Materials", rewardCenteredStyle);

        if (!string.IsNullOrEmpty(bonusTag))
        {
            Color prevC = GUI.color;
            GUI.color = new Color(0.45f, 1.0f, 0.55f);
            GUI.Label(new Rect(dialogX + 20, dialogY + 118, dialogW - 40, 28), bonusTag, rewardCenteredStyle);
            GUI.color = prevC;
        }

        GUI.backgroundColor = Color.cyan;
        if (GUI.Button(new Rect(dialogX + 100, dialogY + 168, dialogW - 200, 50), "PROCEED TO NEXT STAGE ➡️", buttonStyle))
        {
            GameManager.Instance.ProceedToNextDay();
        }
        GUI.backgroundColor = Color.white;
    }

    void DrawEventDilemmaUI()
    {
        float dialogW = 660f;
        float dialogH = 425f;
        float dialogX = Screen.width / 2f - dialogW / 2f;
        float dialogY = Screen.height / 2f - dialogH / 2f;

        var dilemmaSys = EventDilemmaSystem.Instance;
        if (dilemmaSys == null || dilemmaSys.currentEvent == null)
        {
            GUI.Box(new Rect(dialogX, dialogY, dialogW, dialogH), "🎲 EVENT IN PROGRESS", boxStyle);
            if (GUI.Button(new Rect(dialogX + dialogW / 2f - 125f, dialogY + 170f, 250f, 48f), "CONTINUE TO BASE ➡️", buttonStyle))
            {
                GameManager.Instance.SetState(GameManager.GameState.Planning);
            }
            return;
        }

        if (dilemmaSys.isShowingOutcome)
        {
            GUI.Box(new Rect(dialogX, dialogY, dialogW, 300f), "📋 EVENT RESOLUTION", boxStyle);
            GUI.Label(new Rect(dialogX + 20f, dialogY + 40f, dialogW - 40f, 32f), dilemmaSys.currentEvent.title, headerStyle);
            GUI.Label(new Rect(dialogX + 30f, dialogY + 88f, dialogW - 60f, 100f), dilemmaSys.lastOutcomeMessage, descBodyStyle);

            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.4f);
            if (GUI.Button(new Rect(dialogX + dialogW / 2f - 145f, dialogY + 210f, 290f, 50f), "PROCEED TO BASE PLANNING ➡️", buttonStyle))
            {
                dilemmaSys.isShowingOutcome = false;
                GameManager.Instance.SetState(GameManager.GameState.Planning);
            }
            GUI.backgroundColor = Color.white;
        }
        else
        {
            GUI.Box(new Rect(dialogX, dialogY, dialogW, dialogH), "🎲 WASTELAND DILEMMA EVENT", boxStyle);
            GUI.Label(new Rect(dialogX + 20f, dialogY + 32f, dialogW - 40f, 30f), dilemmaSys.currentEvent.title, headerStyle);
            GUI.Label(new Rect(dialogX + 25f, dialogY + 70f, dialogW - 50f, 75f), dilemmaSys.currentEvent.description, descBodyStyle);

            float choiceY = dialogY + 152f;
            for (int i = 0; i < dilemmaSys.currentEvent.choices.Length; i++)
            {
                var choice = dilemmaSys.currentEvent.choices[i];
                bool canAfford = GameManager.IsDevFreeEverything;
                if (!canAfford && ResourceManager.Instance != null)
                {
                    canAfford = ResourceManager.Instance.Gold >= choice.goldCost && ResourceManager.Instance.Materials >= choice.materialCost;
                }

                GUI.backgroundColor = canAfford ? new Color(0.25f, 0.75f, 0.95f) : new Color(0.5f, 0.5f, 0.5f);
                if (GUI.Button(new Rect(dialogX + 25f, choiceY, 260f, 48f), choice.buttonText, buttonStyle))
                {
                    if (canAfford)
                    {
                        dilemmaSys.ExecuteChoice(i);
                    }
                }
                GUI.backgroundColor = Color.white;

                GUI.Label(new Rect(dialogX + 298f, choiceY + 2f, dialogW - 320f, 48f), choice.description, descBodyStyle);
                choiceY += 60f;
            }
        }
    }

    void DrawGameOverUI()
    {
        float dialogW = 580f;
        float dialogH = 315f;
        float dialogX = Screen.width / 2f - dialogW / 2f;
        float dialogY = Screen.height / 2f - dialogH / 2f;

        GUI.Box(new Rect(dialogX, dialogY, dialogW, dialogH), "💀 THE LAST FORTRESS HAS FALLEN", boxStyle);
        GUI.Label(new Rect(dialogX + 20f, dialogY + 46f, dialogW - 40f, 34f), "FORTRESS DEFENSES BREACHED!", headerStyle);

        int reachedStage = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1;
        GUI.Label(new Rect(dialogX + 30f, dialogY + 95f, dialogW - 60f, 80f), $"The zombie horde overwhelmed your barricades and destroyed the fortress core.\nYou survived until Stage {reachedStage}.\nWill you take command again and rebuild humanity's bastion?", descBodyStyle);

        Rect restartRect = new Rect(dialogX + 40f, dialogY + 215f, 240f, 52f);
        Rect quitRect = new Rect(dialogX + 300f, dialogY + 215f, 240f, 52f);
        bool triggerRestart = false;

        Event e = Event.current;
        if (e != null)
        {
            if (e.isMouse && e.type == EventType.MouseDown && restartRect.Contains(e.mousePosition))
            {
                triggerRestart = true;
                e.Use();
            }
            else if (e.isKey && e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.Space || e.keyCode == KeyCode.R)
                {
                    triggerRestart = true;
                    e.Use();
                }
            }
        }

        GUI.backgroundColor = new Color(0.9f, 0.35f, 0.25f);
        if (GUI.Button(restartRect, "🔄 RESTART (R / ENTER)", buttonStyle))
        {
            triggerRestart = true;
        }

        GUI.backgroundColor = new Color(0.5f, 0.5f, 0.5f);
        if (GUI.Button(quitRect, "🚪 QUIT GAME", buttonStyle))
        {
            AudioHelper.PlaySound(SoundEffect.ButtonClick);
            QuitGame();
        }
        GUI.backgroundColor = Color.white;

        if (triggerRestart)
        {
            AudioHelper.PlaySound(SoundEffect.ButtonClick);
            GameManager.Instance?.RestartCampaign();
        }
    }

    void DrawRouteSelectionUI()
    {
        if (GameManager.Instance != null &&
            (GameManager.Instance.CurrentDay == 30 || GameManager.Instance.CurrentDay >= GameManager.Instance.maxStages))
        {
            DrawWave30MandatoryRouteUI();
            return;
        }

        bool showEliteRoute = (GameManager.Instance != null && GameManager.Instance.isEliteRouteAvailable);
        bool showEventRoute = (GameManager.Instance != null && GameManager.Instance.isEventRouteAvailable);

        int cardCount = 1 + (showEliteRoute ? 1 : 0) + (showEventRoute ? 1 : 0);
        float cardW = 275f;
        float cardH = 215f;
        float spacing = 20f;
        float totalCardsW = (cardCount * cardW) + ((cardCount - 1) * spacing);
        float dialogW = (cardCount == 3) ? 920f : ((cardCount == 2) ? 620f : 360f);
        float dialogH = 355f;
        float dialogX = Screen.width / 2f - dialogW / 2f;
        float dialogY = Screen.height / 2f - dialogH / 2f;

        GUI.Box(new Rect(dialogX, dialogY, dialogW, dialogH), $"🗺️ {GameManager.Instance.GetStageName().ToUpper()}", boxStyle);
        GUI.Label(new Rect(dialogX + 20, dialogY + 35, dialogW - 40, 32), "Choose your strategic route for this stage:", headerStyle);

        float startX = dialogX + (dialogW - totalCardsW) / 2f;
        float routeY = dialogY + 84f;
        float curCardX = startX;

        // 1. Standard Battle Route (Always available)
        DrawBattleRouteCard(curCardX, routeY, cardW, cardH);
        curCardX += cardW + spacing;

        // 2. Elite Battle Route (35% chance after Stage 5)
        if (showEliteRoute)
        {
            DrawEliteRouteCard(curCardX, routeY, cardW, cardH);
            curCardX += cardW + spacing;
        }

        // 3. Event Route (20% chance after any stage)
        if (showEventRoute)
        {
            DrawEventRouteCard(curCardX, routeY, cardW, cardH);
            curCardX += cardW + spacing;
        }
    }

    void DrawWave30MandatoryRouteUI()
    {
        float dialogW = 660f;
        float dialogH = 365f;
        float dialogX = Screen.width / 2f - dialogW / 2f;
        float dialogY = Screen.height / 2f - dialogH / 2f;

        int curDay = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 30;
        bool isStage30Gatekeeper = (curDay == 30);

        GUI.backgroundColor = new Color(0.45f, 0.08f, 0.08f, 0.96f);
        string boxHeader = isStage30Gatekeeper
            ? "⚠️ STAGE 30 GATEKEEPER — MANDATORY ELITE ASSAULT"
            : $"⚠️ FINAL STAGE {curDay} — MANDATORY ELITE ASSAULT";
        GUI.Box(new Rect(dialogX, dialogY, dialogW, dialogH), boxHeader, boxStyle);
        GUI.backgroundColor = Color.white;

        GUIStyle climaxTitleStyle = new GUIStyle(GUI.skin.label)
        {
            font = GameFontTheme.HeaderRegularFont,
            fontSize = 12,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = false,
            clipping = TextClipping.Overflow
        };
        climaxTitleStyle.normal.textColor = new Color(1f, 0.85f, 0.2f);
        string subTitle = isStage30Gatekeeper
            ? "⚡ CLEAR STAGE 30 TO UNLOCK AIR BASE & APOCALYPSE CITY!"
            : "⚡ FINAL CLIMAX: SURVIVE THE ULTIMATE HORDE INVASION";
        GUI.Label(new Rect(dialogX + 15, dialogY + 38, dialogW - 30, 30), subTitle, climaxTitleStyle);

        GUIStyle bodyStyle = new GUIStyle(descBodyStyle)
        {
            fontSize = 14,
            richText = true,
            wordWrap = true
        };

        string goalLine = isStage30Gatekeeper
            ? "• <b>Unlock Reward</b>: Unlocks <b>Air Base (Fighter & B-2)</b>, <b>City (31–40)</b> & <b>+1,000G Bonus</b>!"
            : "• <b>Final Stand</b>: Eliminate all hostiles to win the 40-Stage Campaign!";

        string surgeLine = isStage30Gatekeeper
            ? "• <b>+20% Surge Horde Size</b>: +20% more zombies swarming all lanes!\n"
            : "• <b>Apocalypse Horde Surge</b>: +20% (Stg 26+), +35% Zombies & +20% Spawn Speed (Stg 31+)!\n";

        string alertText =
            $"<color=#ff6666><b>CRITICAL ALERT: WAVE {curDay} FORCES ELITE ASSAULT MODE!</b></color>\n\n" +
            "• <b>2X Elite Replacement Rate</b>: 36% of zombies are upgraded to Elite!\n" +
            surgeLine +
            "• <b>Tyrant Boss Alpha</b>: Bosses will ONE-SHOT SMG troopers!\n" +
            goalLine;

        GUI.Label(new Rect(dialogX + 30, dialogY + 78, dialogW - 60, 175), alertText, bodyStyle);

        GUIStyle engageBtnStyle = new GUIStyle(buttonStyle)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold
        };

        GUI.backgroundColor = new Color(1.0f, 0.22f, 0.15f);
        if (GUI.Button(new Rect(dialogX + 50, dialogY + 268, dialogW - 100, 54), "💀 ENGAGE FINAL ELITE HORDE (2X ELITE)", engageBtnStyle))
        {
            AudioHelper.PlaySound(SoundEffect.EventAlert, 1.0f);
            GameManager.Instance.SelectEliteBattleRoute();
        }
        GUI.backgroundColor = Color.white;
    }

    void DrawBattleRouteCard(float x, float y, float w, float h)
    {
        GUI.Box(new Rect(x, y, w, h), GUIContent.none, boxStyle);
        GUI.Label(new Rect(x + 12, y + 10, w - 24, 26), "⚔️ BATTLE ROUTE", subHeaderStyle);
        GUI.Label(new Rect(x + 12, y + 40, w - 24, 100), "Engage regular zombie forces.\nSafe & reliable source of\nGold & Building Materials.", descBodyStyle);

        GUI.backgroundColor = new Color(0.9f, 0.4f, 0.2f);
        if (GUI.Button(new Rect(x + 20, y + 152, w - 40, 44), "CHOOSE BATTLE", buttonStyle))
        {
            GameManager.Instance.SelectRoute(true);
        }
        GUI.backgroundColor = Color.white;
    }

    void DrawEliteRouteCard(float x, float y, float w, float h)
    {
        GUI.backgroundColor = new Color(0.45f, 0.12f, 0.12f, 0.95f);
        GUI.Box(new Rect(x, y, w, h), GUIContent.none, boxStyle);
        GUI.backgroundColor = Color.white;

        GUI.color = new Color(1.0f, 0.82f, 0.2f);
        GUI.Label(new Rect(x + 12, y + 10, w - 24, 26), "⚡ ELITE BATTLE ROUTE", subHeaderStyle);
        GUI.color = Color.white;
        GUI.Label(new Rect(x + 12, y + 38, w - 24, 108),
            "Spawns upgraded <color=#ffd133>ELITE</color> zombies:\n" +
            "• +75% HP | 1-Shots Guardians (2-Hits on Lv.3)!\n" +
            "• Freezes 3s after attacking\n" +
            "<color=#55ff55>★ CLEAR REWARD: 2X BONUS!</color>", descBodyStyle);

        GUI.backgroundColor = new Color(1.0f, 0.72f, 0.15f);
        if (GUI.Button(new Rect(x + 20, y + 152, w - 40, 44), "⚡ CHOOSE ELITE (2X)", buttonStyle))
        {
            GameManager.Instance.SelectEliteBattleRoute();
        }
        GUI.backgroundColor = Color.white;
    }

    void DrawEventRouteCard(float x, float y, float w, float h)
    {
        GUI.Box(new Rect(x, y, w, h), GUIContent.none, boxStyle);
        GUI.Label(new Rect(x + 12, y + 10, w - 24, 26), "🎲 EVENT ROUTE", subHeaderStyle);
        GUI.Label(new Rect(x + 12, y + 40, w - 24, 100), "Risk wasteland scavenging.\nChance to acquire rare\nSpecial Materials!", descBodyStyle);

        GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
        if (GUI.Button(new Rect(x + 20, y + 152, w - 40, 44), "CHOOSE EVENT", buttonStyle))
        {
            GameManager.Instance.SelectRoute(false);
        }
        GUI.backgroundColor = Color.white;
    }

    void DrawVictoryUI()
    {
        float dialogW = 600f;
        float dialogH = 330f;
        float dialogX = Screen.width / 2f - dialogW / 2f;
        float dialogY = Screen.height / 2f - dialogH / 2f;

        GUI.Box(new Rect(dialogX, dialogY, dialogW, dialogH), "🏆 CAMPAIGN CONQUERED — THE LAST FORTRESS SURVIVES!", boxStyle);

        int totalStages = GameManager.Instance != null ? GameManager.Instance.maxStages : 30;
        GUI.Label(new Rect(dialogX + 20, dialogY + 46, dialogW - 40, 34), $"ALL {totalStages} WAVES CLEARED!", headerStyle);
        GUI.Label(new Rect(dialogX + 25, dialogY + 88, dialogW - 50, 56), "You successfully defended the fortress across all wasteland sectors!\nYour strategic planning saved humanity.", descBodyStyle);

        // Endless mode button
        GUI.backgroundColor = new Color(0.2f, 0.8f, 0.3f);
        if (GUI.Button(new Rect(dialogX + 40, dialogY + 162, 245, 50), $"🔥 ENDLESS (Wave {totalStages + 1}+)", buttonStyle))
        {
            GameManager.Instance.ContinueEndlessMode();
        }

        // Restart campaign button
        GUI.backgroundColor = new Color(0.9f, 0.4f, 0.2f);
        if (GUI.Button(new Rect(dialogX + 315, dialogY + 162, 245, 50), "🔄 RESTART CAMPAIGN", buttonStyle))
        {
            GameManager.Instance.RestartCampaign();
        }

        // Quit game button
        GUI.backgroundColor = new Color(0.55f, 0.55f, 0.55f);
        if (GUI.Button(new Rect(dialogX + 180, dialogY + 232, 240, 46), "🚪 QUIT GAME", buttonStyle))
        {
            AudioHelper.PlaySound(SoundEffect.ButtonClick);
            QuitGame();
        }
        GUI.backgroundColor = Color.white;
    }

    void DrawQuitConfirmDialog()
    {
        float dialogW = 420f;
        float dialogH = 205f;
        float dialogX = Screen.width / 2f - dialogW / 2f;
        float dialogY = Screen.height / 2f - dialogH / 2f;

        GUI.backgroundColor = new Color(0.22f, 0.12f, 0.12f, 0.98f);
        GUI.Box(new Rect(dialogX, dialogY, dialogW, dialogH), "🚪 QUIT GAME", boxStyle);
        GUI.backgroundColor = Color.white;

        GUI.Label(new Rect(dialogX + 20, dialogY + 38, dialogW - 40, 28), "What would you like to do?", headerStyle);

        // 1. Return to Main Menu
        GUI.backgroundColor = new Color(1.0f, 0.72f, 0.2f);
        if (GUI.Button(new Rect(dialogX + 35, dialogY + 76, dialogW - 70, 38), "🏠 RETURN TO MAIN MENU", buttonStyle))
        {
            isQuitConfirmOpen = false;
            AudioHelper.PlaySound(SoundEffect.ButtonClick);
            GameManager.Instance?.SetState(GameManager.GameState.MainMenu);
        }

        // 2. Exit to Desktop
        GUI.backgroundColor = new Color(0.9f, 0.28f, 0.25f);
        if (GUI.Button(new Rect(dialogX + 35, dialogY + 124, 165, 40), "🚪 EXIT GAME", buttonStyle))
        {
            AudioHelper.PlaySound(SoundEffect.ButtonClick);
            QuitGame();
        }

        // 3. Cancel
        GUI.backgroundColor = new Color(0.55f, 0.55f, 0.55f);
        if (GUI.Button(new Rect(dialogX + 220, dialogY + 124, 165, 40), "❌ CANCEL (ESC)", buttonStyle))
        {
            isQuitConfirmOpen = false;
            AudioHelper.PlaySound(SoundEffect.ButtonClick);
        }
        GUI.backgroundColor = Color.white;
    }

    public static void QuitGame()
    {
        Debug.Log("[Game] Application Quit requested.");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void DrawDevToolsPanel()
    {
        float panelW = 375f;
        bool unlocked = GameManager.Instance.isDevModeUnlocked;
        float panelH = unlocked ? 595f : 190f;
        float panelX = Screen.width - panelW - 10f;
        float panelY = 118f;

        GUI.Box(new Rect(panelX, panelY, panelW, panelH), unlocked ? "🛠️ DEV TOOLS (UNLOCKED)" : "🔒 DEV TOOLS AUTHENTICATION", boxStyle);

        if (GUI.Button(new Rect(panelX + panelW - 28f, panelY + 4f, 24f, 20f), "✖", smallButtonStyle))
        {
            isDevPanelOpen = false;
            return;
        }

        float curX = panelX + 12f;
        float curY = panelY + 30f;
        float innerW = panelW - 24f;

        // 1. LOCKED STATE -> REQUIRE PASSCODE "2348"
        if (!unlocked)
        {
            GUI.Label(new Rect(curX, curY, innerW, 22f), "Enter Dev Passcode to unlock testing tools:", descBodyStyle);
            curY += 24f;

            GUI.SetNextControlName("DevPasscodeField");
            devPasscodeInput = GUI.TextField(new Rect(curX, curY, innerW - 95f, 28f), devPasscodeInput ?? "", 12);

            GUI.backgroundColor = new Color(0.25f, 0.85f, 0.4f);
            bool submitClicked = GUI.Button(new Rect(curX + innerW - 88f, curY, 88f, 28f), "🔓 UNLOCK", smallButtonStyle);
            GUI.backgroundColor = Color.white;

            bool enterPressed = Event.current.isKey && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
            if (submitClicked || (enterPressed && !string.IsNullOrEmpty(devPasscodeInput)))
            {
                if (GameManager.Instance.TryUnlockDevTools(devPasscodeInput))
                {
                    devAuthStatusMsg = "<color=#55ff55>✔ Unlocked! All buildings & unconditional placement active.</color>";
                    devPasscodeInput = "";
                }
                else
                {
                    devAuthStatusMsg = "<color=#ff5555>❌ Invalid Passcode!</color>";
                }
            }
            curY += 34f;

            if (!string.IsNullOrEmpty(devAuthStatusMsg))
            {
                GUI.Label(new Rect(curX, curY, innerW, 24f), devAuthStatusMsg, descBodyStyle);
                curY += 24f;
            }

            GUI.Label(new Rect(curX, curY, innerW, 36f), "<color=#aaaaaa>Tip: Unlocks free unconditional placement for all current & future bases.</color>", descBodyStyle);
            return;
        }

        // 2. UNLOCKED STATE -> FULL DEV TOOLS & UNCONDITIONAL BUILDING SPAWNER
        bool uncond = GameManager.Instance.isDevUnconditionalMode;
        GUI.backgroundColor = uncond ? new Color(0.2f, 0.9f, 0.4f) : new Color(0.5f, 0.5f, 0.5f);
        if (GUI.Button(new Rect(curX, curY, innerW, 30f), uncond ? "🔓 UNCONDITIONAL BUILD: ON (All Unlocked)" : "🔒 UNCONDITIONAL BUILD: OFF", buttonStyle))
        {
            GameManager.Instance.isDevUnconditionalMode = !GameManager.Instance.isDevUnconditionalMode;
            AudioHelper.PlaySound(SoundEffect.ButtonClick);
        }
        GUI.backgroundColor = Color.white;
        curY += 34f;

        // Free Everything Toggle (All purchases, upgrades, repairs, aircraft & event costs = 0)
        bool freeAll = GameManager.Instance.isDevFreeEverything;
        GUI.backgroundColor = freeAll ? new Color(0.15f, 0.85f, 0.78f) : new Color(0.5f, 0.5f, 0.5f);
        if (GUI.Button(new Rect(curX, curY, innerW, 30f), freeAll ? "🎁 FREE EVERYTHING: ON (All Costs = 0)" : "💰 FREE EVERYTHING: OFF (Normal Costs)", buttonStyle))
        {
            GameManager.Instance.isDevFreeEverything = !GameManager.Instance.isDevFreeEverything;
            AudioHelper.PlaySound(SoundEffect.ButtonClick);
        }
        GUI.backgroundColor = Color.white;
        curY += 36f;

        // Dynamic list of ALL Building.BuildingType values (automatically includes future buildings!)
        GUI.Label(new Rect(curX, curY, innerW, 20f), "🏗️ Place Any Base (Current & Future):", descTitleStyle);
        curY += 22f;

        var allBuildingTypes = (Building.BuildingType[])System.Enum.GetValues(typeof(Building.BuildingType));
        float bBtnW = (innerW - 6f) / 2f;
        float bBtnH = 30f;

        for (int i = 0; i < allBuildingTypes.Length; i++)
        {
            Building.BuildingType bType = allBuildingTypes[i];
            int col = i % 2;
            int row = i / 2;
            float bx = curX + col * (bBtnW + 6f);
            float by = curY + row * (bBtnH + 4f);

            bool isSelected = (PlacementManager.Instance != null && PlacementManager.Instance.isPlacing && PlacementManager.Instance.selectedType == bType);
            GUI.backgroundColor = isSelected ? new Color(0.3f, 0.85f, 1f) : new Color(0.28f, 0.32f, 0.38f);
            if (GUI.Button(new Rect(bx, by, bBtnW, bBtnH), $"📍 {GetBuildingDisplayName(bType)}", smallButtonStyle))
            {
                GameManager.Instance.isDevUnconditionalMode = true;
                inspectedType = bType;
                PlacementManager.Instance?.SelectBuildingToPlace(bType);
            }
            GUI.backgroundColor = Color.white;
        }

        int totalRows = Mathf.CeilToInt(allBuildingTypes.Length / 2f);
        curY += totalRows * (bBtnH + 4f) + 6f;

        // Quick Testing Utilities
        GUI.Label(new Rect(curX, curY, innerW, 20f), "⚡ Instant Testing Actions:", descTitleStyle);
        curY += 22f;

        // Row A: Instant Spawn Units from all placed buildings
        GUI.backgroundColor = new Color(1f, 0.75f, 0.2f);
        if (GUI.Button(new Rect(curX, curY, innerW, 28f), "⚡ Spawn Units From All Placed Bases Now", smallButtonStyle))
        {
            if (GridManager.Instance != null)
            {
                foreach (var b in GridManager.Instance.GetAllBuildings())
                {
                    if (b != null) b.ForceSpawnUnitImmediate();
                }
            }
        }
        GUI.backgroundColor = Color.white;
        curY += 32f;

        // Row B: +1000 Resources & Toggle Elite Route & Toggle Event Route
        float tBtnW = (innerW - 12f) / 3f;
        if (GUI.Button(new Rect(curX, curY, tBtnW, 28f), "💰 +1000 Res", smallButtonStyle))
        {
            ResourceManager.Instance?.SpendGold(-1000);
            ResourceManager.Instance?.SpendMaterials(-1000);
            ResourceManager.Instance?.SpendSpecialMaterials(-20);
        }

        bool isElite = GameManager.Instance.isEliteBattleRoute;
        GUI.backgroundColor = isElite ? new Color(1f, 0.5f, 0.2f) : Color.white;
        if (GUI.Button(new Rect(curX + (tBtnW + 6f) * 1, curY, tBtnW, 28f), isElite ? "⚡ Elite: ON" : "⚡ Elite: OFF", smallButtonStyle))
        {
            GameManager.Instance.DevToggleEliteBattleRoute();
        }
        GUI.backgroundColor = GameManager.Instance.isEventRouteAvailable ? new Color(0.3f, 0.7f, 1f) : Color.white;
        if (GUI.Button(new Rect(curX + (tBtnW + 6f) * 2, curY, tBtnW, 28f), GameManager.Instance.isEventRouteAvailable ? "🎲 Event: ON" : "🎲 Event: OFF", smallButtonStyle))
        {
            GameManager.Instance.DevToggleEventRoute();
        }
        GUI.backgroundColor = Color.white;
        curY += 32f;

        // Row C1: Stage Switcher (Stage -1, +1, 6, 11, 16)
        float sBtnW = (innerW - 16f) / 5f;
        if (GUI.Button(new Rect(curX, curY, sBtnW, 26f), "Stg -1", smallButtonStyle))
            GameManager.Instance.DevSetStage(GameManager.Instance.CurrentDay - 1);
        if (GUI.Button(new Rect(curX + (sBtnW + 4f) * 1, curY, sBtnW, 26f), "Stg +1", smallButtonStyle))
            GameManager.Instance.DevSetStage(GameManager.Instance.CurrentDay + 1);
        if (GUI.Button(new Rect(curX + (sBtnW + 4f) * 2, curY, sBtnW, 26f), "Stg 6", smallButtonStyle))
            GameManager.Instance.DevSetStage(6);
        if (GUI.Button(new Rect(curX + (sBtnW + 4f) * 3, curY, sBtnW, 26f), "Stg 11", smallButtonStyle))
            GameManager.Instance.DevSetStage(11);
        if (GUI.Button(new Rect(curX + (sBtnW + 4f) * 4, curY, sBtnW, 26f), "Stg 16", smallButtonStyle))
            GameManager.Instance.DevSetStage(16);
        curY += 28f;

        // Row C2: High Stage Switcher (Stg 20, 30, 31 City/AirBase, 35, 40 Climax)
        if (GUI.Button(new Rect(curX, curY, sBtnW, 26f), "Stg 20", smallButtonStyle))
            GameManager.Instance.DevSetStage(20);
        if (GUI.Button(new Rect(curX + (sBtnW + 4f) * 1, curY, sBtnW, 26f), "Stg 30", smallButtonStyle))
            GameManager.Instance.DevSetStage(30);
        GUI.backgroundColor = new Color(1.0f, 0.65f, 0.25f);
        if (GUI.Button(new Rect(curX + (sBtnW + 4f) * 2, curY, sBtnW, 26f), "Stg 31", smallButtonStyle))
            GameManager.Instance.DevSetStage(31);
        GUI.backgroundColor = Color.white;
        if (GUI.Button(new Rect(curX + (sBtnW + 4f) * 3, curY, sBtnW, 26f), "Stg 35", smallButtonStyle))
            GameManager.Instance.DevSetStage(35);
        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
        if (GUI.Button(new Rect(curX + (sBtnW + 4f) * 4, curY, sBtnW, 26f), "Stg 40", smallButtonStyle))
            GameManager.Instance.DevSetStage(40);
        GUI.backgroundColor = Color.white;
        curY += 30f;

        // Row D: Clear All Zombies / Reset Base
        GUI.backgroundColor = new Color(0.95f, 0.35f, 0.35f);
        if (GUI.Button(new Rect(curX, curY, bBtnW, 28f), "💥 Kill All Zombies", smallButtonStyle))
        {
            Zombie[] zombies = FindObjectsByType<Zombie>(FindObjectsSortMode.None);
            foreach (var z in zombies)
            {
                if (z != null) z.TakeDamage(99999f);
            }
        }
        GUI.backgroundColor = new Color(0.35f, 0.75f, 0.95f);
        if (GUI.Button(new Rect(curX + bBtnW + 6f, curY, bBtnW, 28f), "🛡️ Heal Fortress Full", smallButtonStyle))
        {
            FortressCore.Instance?.ResetHealth();
        }
        GUI.backgroundColor = Color.white;
        curY += 32f;

        // Row E: Restart Current Wave back to Planning phase
        GUI.backgroundColor = new Color(0.95f, 0.62f, 0.15f);
        if (GUI.Button(new Rect(curX, curY, innerW, 30f), $"🔄 Restart Wave -> Planning (Stg {GameManager.Instance.CurrentDay})", buttonStyle))
        {
            GameManager.Instance.DevRestartCurrentWave();
        }
        GUI.backgroundColor = Color.white;
        curY += 35f;

        // Row F: Persistent Dev Base Save / Load (survives game restarts + saves upgrades!)
        GUI.Label(new Rect(curX, curY, innerW, 20f), "💾 Dev Base Save (Layout + Upgrades):", descTitleStyle);
        curY += 22f;

        int devSavedCount = (PlacementManager.Instance != null) ? PlacementManager.Instance.GetDevSavedBlueprintCount() : 0;
        bool hasDevSave = devSavedCount > 0;
        float clearBtnW = 34f;
        float actionBtnW = (innerW - clearBtnW - 10f) / 2f;

        GUI.backgroundColor = new Color(0.22f, 0.72f, 0.95f);
        if (GUI.Button(new Rect(curX, curY, actionBtnW, 28f), "💾 Save Base", smallButtonStyle))
        {
            if (PlacementManager.Instance != null)
            {
                int saved = PlacementManager.Instance.SaveDevBlueprint();
                devBaseStatusMsg = $"<color=#55ff55>✔ Saved {saved} building(s) + upgrades!</color>";
            }
        }

        GUI.backgroundColor = hasDevSave ? new Color(0.25f, 0.88f, 0.45f) : new Color(0.45f, 0.45f, 0.45f);
        string loadLabel = hasDevSave ? $"📥 Place ({devSavedCount})" : "📥 No Save";
        if (GUI.Button(new Rect(curX + actionBtnW + 5f, curY, actionBtnW, 28f), loadLabel, smallButtonStyle))
        {
            if (PlacementManager.Instance != null && hasDevSave)
            {
                int placed = PlacementManager.Instance.LoadAndPlaceDevBlueprint();
                devBaseStatusMsg = $"<color=#66e0ff>✔ Placed {placed} building(s) + upgrades!</color>";
            }
            else
            {
                devBaseStatusMsg = "<color=#ffaa55>No saved Dev base found yet.</color>";
            }
        }

        GUI.backgroundColor = hasDevSave ? new Color(0.85f, 0.30f, 0.30f) : new Color(0.38f, 0.38f, 0.38f);
        if (GUI.Button(new Rect(curX + (actionBtnW + 5f) * 2f, curY, clearBtnW, 28f), "🗑️", smallButtonStyle))
        {
            if (PlacementManager.Instance != null && hasDevSave)
            {
                PlacementManager.Instance.ClearDevBlueprint();
                devBaseStatusMsg = "<color=#ff8888>Cleared saved Dev base.</color>";
            }
        }
        GUI.backgroundColor = Color.white;
        curY += 30f;

        if (!string.IsNullOrEmpty(devBaseStatusMsg))
        {
            GUI.Label(new Rect(curX, curY, innerW, 18f), devBaseStatusMsg, singleLineLabelStyle);
        }
    }

    public static string GetBuildingDisplayName(Building.BuildingType t)
    {
        switch (t)
        {
            case Building.BuildingType.SMGTent: return "SMG Tent";
            case Building.BuildingType.SniperTower: return "Sniper's Nest";
            case Building.BuildingType.Barricade: return "Barricade";
            case Building.BuildingType.HealStation: return "Heal Station";
            case Building.BuildingType.SpecialForceDepot: return "Special Force Depot";
            case Building.BuildingType.LightTankFactory: return "Light Tank Factory";
            case Building.BuildingType.MissileFoundry: return "Missile Foundry";
            case Building.BuildingType.AirBase: return "Air Base";
            case Building.BuildingType.SupplyDepot: return "Supply Depot";
            case Building.BuildingType.FrontlineSniperTower: return "Sniper Tower";
            default: return t.ToString();
        }
    }
}
