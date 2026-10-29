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

    [MenuItem("Tools/Apply Post-Apocalypse Theme")]
    public static void ApplyTheme()
    {
        bool changed = false;

        // 1. Camera Background: Dark dusty scorched ash
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.13f, 0.11f, 0.09f, 1.0f);
            EditorUtility.SetDirty(cam);
            changed = true;
        }

        // 2. Ground: Deep scorched wasteland dirt
        GameObject ground = GameObject.Find("Ground");
        if (ground != null)
        {
            var rend = ground.GetComponent<Renderer>();
            if (rend != null)
            {
                if (rend.sharedMaterial != null)
                {
                    rend.sharedMaterial.color = new Color(0.24f, 0.19f, 0.14f);
                    EditorUtility.SetDirty(rend.sharedMaterial);
                }
                changed = true;
            }
        }

        // 3. Base Zone: Weathered tactical bunker green/gray
        GameObject baseZone = GameObject.Find("BaseZone");
        if (baseZone != null)
        {
            var rend = baseZone.GetComponent<Renderer>();
            if (rend != null)
            {
                if (rend.sharedMaterial != null)
                {
                    rend.sharedMaterial.color = new Color(0.17f, 0.23f, 0.18f);
                    EditorUtility.SetDirty(rend.sharedMaterial);
                }
                changed = true;
            }
        }

        // 4. Zombie Spawn Zone: Biohazard contaminated wasteland
        GameObject spawnZone = GameObject.Find("ZombieSpawnZone");
        if (spawnZone != null)
        {
            var rend = spawnZone.GetComponent<Renderer>();
            if (rend != null)
            {
                if (rend.sharedMaterial != null)
                {
                    rend.sharedMaterial.color = new Color(0.35f, 0.11f, 0.10f);
                    EditorUtility.SetDirty(rend.sharedMaterial);
                }
                changed = true;
            }
        }

        // 5. Directional Light: Warm dusty apocalyptic sun
        Light dirLight = Object.FindFirstObjectByType<Light>();
        if (dirLight != null && dirLight.type == LightType.Directional)
        {
            dirLight.color = new Color(1.0f, 0.83f, 0.64f);
            dirLight.intensity = 0.95f;
            EditorUtility.SetDirty(dirLight);
            changed = true;
        }

        // 6. Atmospheric Fog
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.16f, 0.13f, 0.10f);
        RenderSettings.fogDensity = 0.012f;

        if (changed)
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveOpenScenes();
            }
            Debug.Log("[Post-Apocalypse Theme] Applied and saved in Unity Editor!");
        }
    }
}
#endif
