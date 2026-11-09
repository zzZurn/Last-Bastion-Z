#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class ApplyPostApocalypseSceneTheme
{
    static ApplyPostApocalypseSceneTheme()
    {
        EditorApplication.delayCall += ApplyTheme;
    }

    [MenuItem("Tools/Rebuild Cross-Shaped Map")]
    public static void ApplyTheme()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying) return;

        bool changed = false;

        // 1. Camera Background & PvZ 2.5D Angle
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.13f, 0.11f, 0.09f, 1.0f);
            cam.transform.rotation = Quaternion.Euler(50f, 0f, 0f);
            cam.transform.position = new Vector3(-15f, 30f, -25.1f);
            cam.orthographic = true;
            cam.orthographicSize = 28f;
            EditorUtility.SetDirty(cam);
            changed = true;

            var camCtrl = cam.GetComponent<CameraController>();
            if (camCtrl != null)
            {
                camCtrl.panLimitX = new Vector2(-70f, 50f);
                camCtrl.panLimitZ = new Vector2(-68f, 48f);
                camCtrl.maxZoom = 80f;
                camCtrl.cameraHeight = 30f;
                EditorUtility.SetDirty(camCtrl);
            }
        }

        // 2. Hide/Remove Legacy Flat Ground & Zones
        string[] legacyNames = new string[] { "Ground", "BaseZone", "ZombieSpawnZone" };
        foreach (var leg in legacyNames)
        {
            var obj = GameObject.Find(leg);
            if (obj != null)
            {
                obj.SetActive(false);
                EditorUtility.SetDirty(obj);
                changed = true;
            }
        }

        // 3. Configure _GameController components with 55x45 Cells (110m x 90m) Cross Grid
        GameObject gc = GameObject.Find("_GameController");
        if (gc != null)
        {
            var grid = gc.GetComponent<GridManager>();
            if (grid != null)
            {
                grid.gridWidth = 55;
                grid.gridHeight = 45;
                grid.cellSize = 2f;
                grid.gridOrigin = new Vector3(-65f, 0.1f, -45f);
                grid.corridorYMin = 15;
                grid.corridorYMax = 29;
                grid.branchXMin = 25;
                grid.branchXMax = 39;
                EditorUtility.SetDirty(grid);
                changed = true;
            }

            var wave = gc.GetComponent<WaveManager>();
            if (wave != null)
            {
                wave.spawnX = 43.5f;
                wave.spawnZMin = -14.0f;
                wave.spawnZMax = 14.0f;
                EditorUtility.SetDirty(wave);
                changed = true;
            }

            var gm = gc.GetComponent<GameManager>();
            if (gm != null)
            {
                gm.maxStages = 20;
                EditorUtility.SetDirty(gm);
                changed = true;
            }

            var cmv = gc.GetComponent<CrossMapVisualizer>();
            if (cmv == null)
            {
                cmv = Undo.AddComponent<CrossMapVisualizer>(gc);
            }
            if (cmv != null)
            {
                cmv.BuildCrossMapEnvironment();
                EditorUtility.SetDirty(cmv);
                changed = true;
            }
        }

        // 4. Directional Light: Warm dusty apocalyptic sun
        Light dirLight = Object.FindAnyObjectByType<Light>();
        if (dirLight != null && dirLight.type == LightType.Directional)
        {
            dirLight.color = new Color(1.0f, 0.83f, 0.64f);
            dirLight.intensity = 0.95f;
            EditorUtility.SetDirty(dirLight);
            changed = true;
        }

        // 5. Atmospheric Fog
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.16f, 0.13f, 0.10f);
        RenderSettings.fogDensity = 0.008f;

        if (changed)
        {
            try
            {
                var activeScene = EditorSceneManager.GetActiveScene();
                if (activeScene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(activeScene);
                    EditorSceneManager.SaveOpenScenes();
                }
                Debug.Log("[CrossMap] Full 15x15-wing (45x45 cells / 90m x 90m) Cross Map without walls rebuilt and saved!");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[CrossMap] Note: Scene save deferred (" + ex.Message + ")");
            }
        }
    }
}
#endif
