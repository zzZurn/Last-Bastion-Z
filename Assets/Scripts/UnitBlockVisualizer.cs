using UnityEngine;

public static class UnitBlockVisualizer
{
    private static Shader GetShader()
    {
        return Shader.Find("Universal Render Pipeline/Lit") ??
               Shader.Find("Standard") ??
               Shader.Find("Universal Render Pipeline/Unlit") ??
               Shader.Find("Unlit/Color");
    }

    private static Material MakeMat(Color color, float metallic = 0.1f, float smoothness = 0.35f)
    {
        var mat = new Material(GetShader()) { color = color };
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        return mat;
    }

    public static void AttachSoldierVisuals(GameObject root, Building.BuildingType role)
    {
        // Remove default primitive mesh renderer/filter from root if present
        var rootRend = root.GetComponent<Renderer>();
        if (rootRend != null) rootRend.enabled = false;

        var visualGroup = new GameObject("BlockModel");
        visualGroup.transform.SetParent(root.transform, false);

        if (role == Building.BuildingType.LightTankFactory)
        {
            BuildLightTankModel(visualGroup);
            return;
        }
        if (role == Building.BuildingType.MissileFoundry)
        {
            BuildICBMLauncherModel(visualGroup);
            return;
        }

        Color uniformColor = new Color(0.2f, 0.45f, 0.9f); // Default Blue SMG
        Color helmetColor = new Color(0.18f, 0.22f, 0.28f);
        Color gunColor = new Color(0.15f, 0.15f, 0.15f);

        if (role == Building.BuildingType.SniperTower)
        {
            uniformColor = new Color(0.12f, 0.2f, 0.45f); // Deep navy sniper
            helmetColor = new Color(0.12f, 0.15f, 0.2f);
        }
        else if (role == Building.BuildingType.HealStation)
        {
            uniformColor = new Color(0.2f, 0.85f, 0.5f); // Mint medic
            helmetColor = Color.white;
        }
        else if (role == Building.BuildingType.SpecialForceDepot)
        {
            uniformColor = new Color(0.18f, 0.19f, 0.22f); // Heavy Obsidian Special Forces Armor
            helmetColor = new Color(0.85f, 0.68f, 0.18f);  // Tactical Gold-Trimmed Helmet
        }

        float bodyScale = (role == Building.BuildingType.SpecialForceDepot) ? 1.18f : 1.0f;

        // 1. Torso
        var torso = GameObject.CreatePrimitive(PrimitiveType.Cube);
        torso.name = "Torso";
        torso.transform.SetParent(visualGroup.transform, false);
        torso.transform.localScale = new Vector3(0.55f * bodyScale, 0.65f * bodyScale, 0.42f * bodyScale);
        torso.transform.localPosition = new Vector3(0f, 0.45f * bodyScale, 0f);
        Object.Destroy(torso.GetComponent<Collider>());
        torso.GetComponent<MeshRenderer>().material = MakeMat(uniformColor);

        // 2. Head
        var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.name = "Head";
        head.transform.SetParent(visualGroup.transform, false);
        head.transform.localScale = new Vector3(0.48f * bodyScale, 0.46f * bodyScale, 0.48f * bodyScale);
        head.transform.localPosition = new Vector3(0f, 0.98f * bodyScale, 0f);
        Object.Destroy(head.GetComponent<Collider>());
        head.GetComponent<MeshRenderer>().material = MakeMat(helmetColor);

        // 3. Visor / Tactical Goggles
        var visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visor.name = "Visor";
        visor.transform.SetParent(head.transform, false);
        visor.transform.localScale = new Vector3(0.85f, 0.25f, 0.25f);
        visor.transform.localPosition = new Vector3(0f, 0.05f, 0.42f);
        Object.Destroy(visor.GetComponent<Collider>());
        Color visorColor = role == Building.BuildingType.HealStation
            ? Color.green
            : (role == Building.BuildingType.SpecialForceDepot ? new Color(1.0f, 0.78f, 0.15f) : new Color(0.2f, 0.9f, 1f));
        visor.GetComponent<MeshRenderer>().material = MakeMat(visorColor);

        // 4. Weapon
        var gun = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gun.name = "Weapon";
        gun.transform.SetParent(visualGroup.transform, false);
        if (role == Building.BuildingType.SniperTower)
        {
            gun.transform.localScale = new Vector3(0.14f, 0.15f, 0.95f);
            gun.transform.localPosition = new Vector3(0.32f, 0.55f, 0.48f);
        }
        else if (role == Building.BuildingType.SpecialForceDepot)
        {
            // Compact tactical sidearm + Heavy Riot Shield on left arm
            gun.transform.localScale = new Vector3(0.14f, 0.16f, 0.42f);
            gun.transform.localPosition = new Vector3(0.38f, 0.55f, 0.32f);

            var shield = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shield.name = "GuardianShield";
            shield.transform.SetParent(visualGroup.transform, false);
            shield.transform.localScale = new Vector3(0.52f, 0.82f, 0.12f);
            shield.transform.localPosition = new Vector3(-0.32f, 0.55f, 0.36f);
            Object.Destroy(shield.GetComponent<Collider>());
            shield.GetComponent<MeshRenderer>().material = MakeMat(new Color(0.22f, 0.25f, 0.30f), 0.65f, 0.6f);

            var shieldTrim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shieldTrim.name = "ShieldGoldEmblem";
            shieldTrim.transform.SetParent(shield.transform, false);
            shieldTrim.transform.localScale = new Vector3(0.6f, 0.25f, 1.15f);
            shieldTrim.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            Object.Destroy(shieldTrim.GetComponent<Collider>());
            shieldTrim.GetComponent<MeshRenderer>().material = MakeMat(new Color(0.95f, 0.78f, 0.2f), 0.7f, 0.7f);
        }
        else
        {
            gun.transform.localScale = new Vector3(0.15f, 0.18f, 0.55f);
            gun.transform.localPosition = new Vector3(0.32f, 0.52f, 0.35f);
        }
        Object.Destroy(gun.GetComponent<Collider>());
        gun.GetComponent<MeshRenderer>().material = MakeMat(gunColor, 0.6f, 0.7f);

        // 5. Medic Red Cross (if Medic)
        if (role == Building.BuildingType.HealStation)
        {
            var crossH = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crossH.transform.SetParent(torso.transform, false);
            crossH.transform.localScale = new Vector3(0.6f, 0.2f, 0.1f);
            crossH.transform.localPosition = new Vector3(0f, 0.05f, 0.48f);
            Object.Destroy(crossH.GetComponent<Collider>());
            crossH.GetComponent<MeshRenderer>().material = MakeMat(Color.red);

            var crossV = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crossV.transform.SetParent(torso.transform, false);
            crossV.transform.localScale = new Vector3(0.2f, 0.6f, 0.1f);
            crossV.transform.localPosition = new Vector3(0f, 0.05f, 0.48f);
            Object.Destroy(crossV.GetComponent<Collider>());
            crossV.GetComponent<MeshRenderer>().material = MakeMat(Color.red);
        }
    }

    private static void BuildLightTankModel(GameObject parent)
    {
        Material hullMat = MakeMat(new Color(0.26f, 0.34f, 0.22f), 0.45f, 0.45f); // Olive military tank hull
        Material trackMat = MakeMat(new Color(0.14f, 0.14f, 0.15f), 0.3f, 0.25f); // Dark steel treads
        Material turretMat = MakeMat(new Color(0.32f, 0.40f, 0.26f), 0.5f, 0.5f);
        Material barrelMat = MakeMat(new Color(0.18f, 0.20f, 0.18f), 0.7f, 0.6f);

        // 1. Left & Right Caterpillar Tracks
        foreach (float xSide in new float[] { -0.58f, 0.58f })
        {
            var track = GameObject.CreatePrimitive(PrimitiveType.Cube);
            track.name = "TankTrack";
            track.transform.SetParent(parent.transform, false);
            track.transform.localScale = new Vector3(0.28f, 0.42f, 1.65f);
            track.transform.localPosition = new Vector3(xSide, 0.21f, 0f);
            Object.Destroy(track.GetComponent<Collider>());
            track.GetComponent<MeshRenderer>().material = trackMat;
        }

        // 2. Armored Tank Hull
        var hull = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hull.name = "TankHull";
        hull.transform.SetParent(parent.transform, false);
        hull.transform.localScale = new Vector3(1.05f, 0.46f, 1.55f);
        hull.transform.localPosition = new Vector3(0f, 0.38f, 0f);
        Object.Destroy(hull.GetComponent<Collider>());
        hull.GetComponent<MeshRenderer>().material = hullMat;

        // 3. Rotating Turret Body
        var turret = GameObject.CreatePrimitive(PrimitiveType.Cube);
        turret.name = "TankTurret";
        turret.transform.SetParent(parent.transform, false);
        turret.transform.localScale = new Vector3(0.78f, 0.42f, 0.88f);
        turret.transform.localPosition = new Vector3(0f, 0.80f, -0.05f);
        Object.Destroy(turret.GetComponent<Collider>());
        turret.GetComponent<MeshRenderer>().material = turretMat;

        // 4. Heavy High-Explosive Main Cannon Barrel
        var barrel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        barrel.name = "MainCannon";
        barrel.transform.SetParent(parent.transform, false);
        barrel.transform.localScale = new Vector3(0.20f, 0.20f, 1.25f);
        barrel.transform.localPosition = new Vector3(0f, 0.82f, 0.85f);
        Object.Destroy(barrel.GetComponent<Collider>());
        barrel.GetComponent<MeshRenderer>().material = barrelMat;

        // 5. Glowing Tactical Target Sensor on Turret
        var optic = GameObject.CreatePrimitive(PrimitiveType.Cube);
        optic.name = "TurretOptic";
        optic.transform.SetParent(parent.transform, false);
        optic.transform.localScale = new Vector3(0.32f, 0.14f, 0.12f);
        optic.transform.localPosition = new Vector3(0.22f, 0.90f, 0.40f);
        Object.Destroy(optic.GetComponent<Collider>());
        optic.GetComponent<MeshRenderer>().material = MakeMat(new Color(1.0f, 0.45f, 0.1f));
    }

    private static bool TryCalculateExactWorldBounds(GameObject obj, out Bounds worldBounds)
    {
        worldBounds = new Bounds();
        bool hasBounds = false;

        MeshFilter[] meshFilters = obj.GetComponentsInChildren<MeshFilter>(true);
        foreach (var mf in meshFilters)
        {
            if (mf == null || mf.sharedMesh == null) continue;
            Bounds lb = mf.sharedMesh.bounds;
            Vector3 min = lb.min;
            Vector3 max = lb.max;
            Vector3[] corners = new Vector3[8]
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, max.y, max.z)
            };
            foreach (Vector3 c in corners)
            {
                Vector3 wc = mf.transform.TransformPoint(c);
                if (!hasBounds)
                {
                    worldBounds = new Bounds(wc, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    worldBounds.Encapsulate(wc);
                }
            }
        }

        if (!hasBounds)
        {
            Renderer[] rends = obj.GetComponentsInChildren<Renderer>(true);
            foreach (var r in rends)
            {
                if (r == null) continue;
                if (!hasBounds)
                {
                    worldBounds = r.bounds;
                    hasBounds = true;
                }
                else
                {
                    worldBounds.Encapsulate(r.bounds);
                }
            }
        }

        return hasBounds;
    }

    private static void StripEmbeddedSceneComponents(GameObject modelInstance)
    {
        // Destroy any embedded Blender Cameras/AudioListeners/Lights/Colliders inside the FBX so they never hijack the screen!
        foreach (var cam in modelInstance.GetComponentsInChildren<Camera>(true))
        {
            cam.enabled = false;
            Object.DestroyImmediate(cam);
        }
        foreach (var listener in modelInstance.GetComponentsInChildren<AudioListener>(true))
        {
            listener.enabled = false;
            Object.DestroyImmediate(listener);
        }
        foreach (var light in modelInstance.GetComponentsInChildren<Light>(true))
        {
            light.enabled = false;
            Object.DestroyImmediate(light);
        }
        foreach (var col in modelInstance.GetComponentsInChildren<Collider>(true))
        {
            col.enabled = false;
            Object.DestroyImmediate(col);
        }
    }

    private static void BuildICBMLauncherModel(GameObject parent)
    {
        GameObject fbxPrefab = Resources.Load<GameObject>("Models/ICBM Launcher");
        if (fbxPrefab != null)
        {
            GameObject modelInstance = Object.Instantiate(fbxPrefab, parent.transform);
            modelInstance.name = "ICBMLauncher_FBX";
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = fbxPrefab.transform.localRotation;
            modelInstance.transform.localScale = fbxPrefab.transform.localScale;

            // Remove embedded Blender camera (Camera_Iso), lights, and colliders
            StripEmbeddedSceneComponents(modelInstance);

            Renderer[] renderers = modelInstance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0 && TryCalculateExactWorldBounds(modelInstance, out Bounds combined))
            {
                Shader validShader = GetShader();
                foreach (var r in renderers)
                {
                    if (r.sharedMaterials != null)
                    {
                        Material[] mats = r.materials;
                        for (int m = 0; m < mats.Length; m++)
                        {
                            if (mats[m] == null || mats[m].shader == null || mats[m].shader.name == "Hidden/InternalErrorShader" || (mats[m].shader.name == "Standard" && validShader.name.Contains("Universal")))
                            {
                                Color c = (mats[m] != null && mats[m].HasProperty("_Color")) ? mats[m].color : new Color(0.28f, 0.33f, 0.25f);
                                Texture mainTex = (mats[m] != null && mats[m].HasProperty("_MainTex")) ? mats[m].mainTexture : null;
                                Material upgraded = MakeMat(c, 0.45f, 0.45f);
                                if (mainTex != null)
                                {
                                    upgraded.mainTexture = mainTex;
                                    if (upgraded.HasProperty("_BaseMap")) upgraded.SetTexture("_BaseMap", mainTex);
                                }
                                mats[m] = upgraded;
                            }
                        }
                        r.materials = mats;
                    }
                }

                // Orient longer horizontal axis along +Z (Soldier forward direction)
                if (combined.size.x > combined.size.z)
                {
                    modelInstance.transform.Rotate(0f, 90f, 0f, Space.World);
                }
                // Flip 180 degrees around Y so the ICBM Launcher truck faces forward (+Z -> East toward zombies)
                modelInstance.transform.Rotate(0f, 180f, 0f, Space.World);
                TryCalculateExactWorldBounds(modelInstance, out combined);

                Vector3 rootWorldPos = parent.transform.position;
                modelInstance.transform.position += new Vector3(
                    rootWorldPos.x - combined.center.x,
                    rootWorldPos.y - combined.min.y,
                    rootWorldPos.z - combined.center.z
                );

                // Preserve 100% true model proportions (uniform scale) and scale length to 3 blocks (3 * 2.0m = 6.0m)
                float sizeZ = Mathf.Max(0.01f, combined.size.z);
                float targetLength = 6.0f; // 3 blocks long (6.0m)
                float uniformScale = targetLength / sizeZ;

                parent.transform.localScale = Vector3.one * uniformScale;
                return;
            }
        }

        // Procedural fallback if FBX is unavailable
        var chassis = GameObject.CreatePrimitive(PrimitiveType.Cube);
        chassis.transform.SetParent(parent.transform, false);
        chassis.transform.localScale = new Vector3(1.2f, 0.5f, 2.4f);
        chassis.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        Object.Destroy(chassis.GetComponent<Collider>());
        chassis.GetComponent<MeshRenderer>().material = MakeMat(new Color(0.25f, 0.30f, 0.22f), 0.5f, 0.4f);

        var missileTube = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        missileTube.transform.SetParent(parent.transform, false);
        missileTube.transform.localScale = new Vector3(0.55f, 1.1f, 0.55f);
        missileTube.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
        missileTube.transform.localPosition = new Vector3(0f, 1.05f, -0.1f);
        Object.Destroy(missileTube.GetComponent<Collider>());
        missileTube.GetComponent<MeshRenderer>().material = MakeMat(new Color(0.35f, 0.38f, 0.32f), 0.6f, 0.5f);
    }

    public static void AttachZombieVisuals(GameObject root, string variant, bool isEliteUpgrade = false)
    {
        var rootRend = root.GetComponent<Renderer>();
        if (rootRend != null) rootRend.enabled = false;

        var visualGroup = new GameObject("BlockModel");
        visualGroup.transform.SetParent(root.transform, false);

        Color skinColor = new Color(0.28f, 0.45f, 0.25f); // Rotten Walker Green
        float scale = 1f;

        if (variant == "Runner")
        {
            skinColor = new Color(0.85f, 0.25f, 0.15f); // Crimson Runner
            scale = 0.85f;
        }
        else if (variant == "Tank")
        {
            skinColor = new Color(0.15f, 0.35f, 0.18f); // Dark Mutant Tank
            scale = 1.35f;
        }
        else if (variant == "Armored")
        {
            skinColor = new Color(0.32f, 0.42f, 0.35f);
            scale = 1.1f;
        }
        else if (variant == "Berserker")
        {
            skinColor = new Color(0.75f, 0.15f, 0.2f);
            scale = 1.05f;
        }
        else if (variant == "Bombaz")
        {
            skinColor = new Color(0.42f, 0.38f, 0.20f); // Volatile Charred Olive Bomber Zombie
            scale = 1.15f;
        }
        else if (variant == "Buffed")
        {
            skinColor = new Color(0.55f, 0.22f, 0.16f); // Heavy Crimson/Rust Muscular Zombie
            scale = 1.48f;
        }
        else if (variant == "Boss")
        {
            skinColor = new Color(0.48f, 0.12f, 0.52f); // Toxic Purple Tyrant
            scale = 1.9f;
        }

        if (isEliteUpgrade && variant != "Boss")
        {
            scale *= 1.18f;
            skinColor = Color.Lerp(skinColor, new Color(0.85f, 0.45f, 0.1f), 0.35f);
        }

        // 1. Zombie Torso
        var torso = GameObject.CreatePrimitive(PrimitiveType.Cube);
        torso.name = "Torso";
        torso.transform.SetParent(visualGroup.transform, false);
        torso.transform.localScale = new Vector3(0.6f * scale, 0.75f * scale, 0.45f * scale);
        torso.transform.localPosition = new Vector3(0f, 0.5f * scale, 0f);
        Object.Destroy(torso.GetComponent<Collider>());
        torso.GetComponent<MeshRenderer>().material = MakeMat(skinColor);

        // 2. Zombie Head
        var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.name = "Head";
        head.transform.SetParent(visualGroup.transform, false);
        head.transform.localScale = new Vector3(0.5f * scale, 0.5f * scale, 0.5f * scale);
        head.transform.localPosition = new Vector3(0f, 1.05f * scale, 0.05f * scale);
        Object.Destroy(head.GetComponent<Collider>());
        head.GetComponent<MeshRenderer>().material = MakeMat(skinColor * 0.9f);

        // 3. Glowing Eyes
        Color eyeColor = (variant == "Boss")
            ? new Color(1f, 0.4f, 0f)
            : (variant == "Buffed"
                ? new Color(1f, 0.72f, 0.15f)
                : (variant == "Bombaz" ? new Color(1f, 0.65f, 0.05f) : new Color(1f, 0.12f, 0.1f)));

        var eyeL = GameObject.CreatePrimitive(PrimitiveType.Cube);
        eyeL.transform.SetParent(head.transform, false);
        eyeL.transform.localScale = new Vector3(0.18f, 0.18f, 0.15f);
        eyeL.transform.localPosition = new Vector3(-0.16f, 0.08f, 0.45f);
        Object.Destroy(eyeL.GetComponent<Collider>());
        eyeL.GetComponent<MeshRenderer>().material = MakeMat(eyeColor);

        var eyeR = GameObject.CreatePrimitive(PrimitiveType.Cube);
        eyeR.transform.SetParent(head.transform, false);
        eyeR.transform.localScale = new Vector3(0.18f, 0.18f, 0.15f);
        eyeR.transform.localPosition = new Vector3(0.16f, 0.08f, 0.45f);
        Object.Destroy(eyeR.GetComponent<Collider>());
        eyeR.GetComponent<MeshRenderer>().material = MakeMat(eyeColor);

        // 4. Outstretched Zombie Arms
        var armL = GameObject.CreatePrimitive(PrimitiveType.Cube);
        armL.name = "ArmL";
        armL.transform.SetParent(visualGroup.transform, false);
        armL.transform.localScale = new Vector3(0.2f * scale, 0.2f * scale, 0.65f * scale);
        armL.transform.localPosition = new Vector3(-0.38f * scale, 0.65f * scale, 0.32f * scale);
        Object.Destroy(armL.GetComponent<Collider>());
        armL.GetComponent<MeshRenderer>().material = MakeMat(skinColor * 0.85f);

        var armR = GameObject.CreatePrimitive(PrimitiveType.Cube);
        armR.name = "ArmR";
        armR.transform.SetParent(visualGroup.transform, false);
        armR.transform.localScale = new Vector3(0.2f * scale, 0.2f * scale, 0.65f * scale);
        armR.transform.localPosition = new Vector3(0.38f * scale, 0.65f * scale, 0.32f * scale);
        Object.Destroy(armR.GetComponent<Collider>());
        armR.GetComponent<MeshRenderer>().material = MakeMat(skinColor * 0.85f);

        // 5. Heavy Steel Helmet for Armored Zombie
        if (variant == "Armored")
        {
            var helmet = GameObject.CreatePrimitive(PrimitiveType.Cube);
            helmet.transform.SetParent(head.transform, false);
            helmet.transform.localScale = new Vector3(1.1f, 0.6f, 1.1f);
            helmet.transform.localPosition = new Vector3(0f, 0.28f, 0f);
            Object.Destroy(helmet.GetComponent<Collider>());
            helmet.GetComponent<MeshRenderer>().material = MakeMat(new Color(0.4f, 0.45f, 0.5f), 0.7f, 0.6f);
        }
        else if (variant == "Bombaz")
        {
            // Strapped Explosive Harness Vest
            var vest = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vest.name = "BombazVest";
            vest.transform.SetParent(visualGroup.transform, false);
            vest.transform.localScale = new Vector3(0.66f * scale, 0.48f * scale, 0.50f * scale);
            vest.transform.localPosition = new Vector3(0f, 0.52f * scale, 0f);
            Object.Destroy(vest.GetComponent<Collider>());
            vest.GetComponent<MeshRenderer>().material = MakeMat(new Color(0.22f, 0.20f, 0.18f), 0.5f, 0.4f);

            // Twin Volatile Dynamite / Fuel Drums on Back
            Material tntMat = MakeMat(new Color(0.88f, 0.18f, 0.12f), 0.4f, 0.5f);
            foreach (float xOff in new float[] { -0.16f, 0.16f })
            {
                var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                barrel.name = "BombazTNTBarrel";
                barrel.transform.SetParent(visualGroup.transform, false);
                barrel.transform.localScale = new Vector3(0.24f * scale, 0.30f * scale, 0.24f * scale);
                barrel.transform.localPosition = new Vector3(xOff * scale, 0.58f * scale, -0.28f * scale);
                Object.Destroy(barrel.GetComponent<Collider>());
                barrel.GetComponent<MeshRenderer>().material = tntMat;
            }

            // Glowing Volatile Fuse Core on Chest
            var fuseCore = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fuseCore.name = "BombazFuseCore";
            fuseCore.transform.SetParent(visualGroup.transform, false);
            fuseCore.transform.localScale = new Vector3(0.28f * scale, 0.28f * scale, 0.22f * scale);
            fuseCore.transform.localPosition = new Vector3(0f, 0.54f * scale, 0.26f * scale);
            Object.Destroy(fuseCore.GetComponent<Collider>());
            fuseCore.GetComponent<MeshRenderer>().material = MakeMat(new Color(1.0f, 0.55f, 0.1f), 0.2f, 0.8f);
        }
        else if (variant == "Buffed")
        {
            Material buffedSpikeMat = MakeMat(new Color(0.35f, 0.14f, 0.12f), 0.6f, 0.5f);
            foreach (float xShoulder in new float[] { -0.36f, 0.36f })
            {
                var pauldron = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pauldron.name = "BuffedPauldron";
                pauldron.transform.SetParent(visualGroup.transform, false);
                pauldron.transform.localScale = new Vector3(0.24f * scale, 0.22f * scale, 0.42f * scale);
                pauldron.transform.localPosition = new Vector3(xShoulder * scale, 0.82f * scale, 0f);
                Object.Destroy(pauldron.GetComponent<Collider>());
                pauldron.GetComponent<MeshRenderer>().material = buffedSpikeMat;
            }
        }
        else if (variant == "Boss")
        {
            // Reinforced Dark Steel Chest Carapace (Visualizing 50% damage resistance)
            var carapace = GameObject.CreatePrimitive(PrimitiveType.Cube);
            carapace.name = "BossArmorCarapace";
            carapace.transform.SetParent(visualGroup.transform, false);
            carapace.transform.localScale = new Vector3(0.68f * scale, 0.58f * scale, 0.52f * scale);
            carapace.transform.localPosition = new Vector3(0f, 0.58f * scale, 0.05f * scale);
            Object.Destroy(carapace.GetComponent<Collider>());
            carapace.GetComponent<MeshRenderer>().material = MakeMat(new Color(0.20f, 0.18f, 0.24f), 0.75f, 0.65f);

            // Spiked Heavy Obsidian Pauldrons
            Material bossPlateMat = MakeMat(new Color(0.28f, 0.14f, 0.32f), 0.7f, 0.55f);
            foreach (float xShoulder in new float[] { -0.42f, 0.42f })
            {
                var pauldron = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pauldron.name = "BossSpikedPauldron";
                pauldron.transform.SetParent(visualGroup.transform, false);
                pauldron.transform.localScale = new Vector3(0.28f * scale, 0.26f * scale, 0.50f * scale);
                pauldron.transform.localPosition = new Vector3(xShoulder * scale, 0.88f * scale, 0f);
                Object.Destroy(pauldron.GetComponent<Collider>());
                pauldron.GetComponent<MeshRenderer>().material = bossPlateMat;
            }
        }

        // 6. Elite Upgrade Crown / Glowing Shoulder Spikes (if Elite non-boss)
        if (isEliteUpgrade && variant != "Boss")
        {
            Material eliteGoldMat = MakeMat(new Color(1.0f, 0.78f, 0.15f), 0.8f, 0.75f);
            foreach (float xShoulder in new float[] { -0.42f, 0.42f })
            {
                var pauldron = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pauldron.name = "ElitePauldron";
                pauldron.transform.SetParent(visualGroup.transform, false);
                pauldron.transform.localScale = new Vector3(0.28f * scale, 0.24f * scale, 0.48f * scale);
                pauldron.transform.localPosition = new Vector3(xShoulder * scale, 0.85f * scale, 0f);
                Object.Destroy(pauldron.GetComponent<Collider>());
                pauldron.GetComponent<MeshRenderer>().material = eliteGoldMat;
            }
        }
    }
}
