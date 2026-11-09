using UnityEngine;

public static class BuildingBlockVisualizer
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

    public static void AttachBuildingVisuals(GameObject root, Building.BuildingType type)
    {
        var rootRend = root.GetComponent<Renderer>();
        if (rootRend != null) rootRend.enabled = false;

        var visualGroup = new GameObject("BuildingVisual");
        visualGroup.transform.SetParent(root.transform, false);

        switch (type)
        {
            case Building.BuildingType.SMGTent:
                BuildSMGTent(visualGroup);
                break;
            case Building.BuildingType.SniperTower:
                BuildSniperTower(visualGroup);
                break;
            case Building.BuildingType.Barricade:
                BuildBarricade(visualGroup);
                break;
            case Building.BuildingType.HealStation:
                BuildHealStation(visualGroup);
                break;
            case Building.BuildingType.SpecialForceDepot:
                BuildSpecialForceDepot(visualGroup);
                break;
            case Building.BuildingType.LightTankFactory:
                BuildLightTankFactory(visualGroup);
                break;
            case Building.BuildingType.MissileFoundry:
                BuildMissileFoundry(visualGroup);
                break;
            case Building.BuildingType.SupplyDepot:
                BuildSupplyDepot(visualGroup);
                break;
            case Building.BuildingType.FrontlineSniperTower:
                BuildFrontlineSniperTower(visualGroup);
                break;
            default:
                BuildSMGTent(visualGroup);
                break;
        }
    }

    private static void BuildSMGTent(GameObject parent)
    {
        // 1. Main Tent Canvas
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.transform.SetParent(parent.transform, false);
        body.transform.localScale = new Vector3(1.6f, 0.9f, 1.6f);
        body.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        Object.Destroy(body.GetComponent<Collider>());
        body.GetComponent<MeshRenderer>().material = MakeMat(new Color(0.28f, 0.42f, 0.58f)); // Military Blue Canvas

        // 2. Pitched Roof Ridge
        var ridge = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ridge.transform.SetParent(parent.transform, false);
        ridge.transform.localScale = new Vector3(1.1f, 0.5f, 1.62f);
        ridge.transform.localPosition = new Vector3(0f, 1.05f, 0f);
        Object.Destroy(ridge.GetComponent<Collider>());
        ridge.GetComponent<MeshRenderer>().material = MakeMat(new Color(0.18f, 0.32f, 0.46f));

        // 3. Entrance Opening
        var entrance = GameObject.CreatePrimitive(PrimitiveType.Cube);
        entrance.transform.SetParent(parent.transform, false);
        entrance.transform.localScale = new Vector3(0.1f, 0.65f, 0.55f);
        entrance.transform.localPosition = new Vector3(0.81f, 0.33f, 0f);
        Object.Destroy(entrance.GetComponent<Collider>());
        entrance.GetComponent<MeshRenderer>().material = MakeMat(new Color(0.1f, 0.1f, 0.12f));

        // 4. Tactical Ammo Crate outside tent
        var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crate.transform.SetParent(parent.transform, false);
        crate.transform.localScale = new Vector3(0.45f, 0.35f, 0.45f);
        crate.transform.localPosition = new Vector3(0.65f, 0.18f, 0.65f);
        Object.Destroy(crate.GetComponent<Collider>());
        crate.GetComponent<MeshRenderer>().material = MakeMat(new Color(0.42f, 0.32f, 0.18f));
    }

    private static void BuildSniperTower(GameObject parent)
    {
        // Sniper's Nest Footprint: 2 wide x 1 long (1 cell in X = 2m, 2 cells in Z = 4m)
        // Designed as an elongated Camouflage Sniper Tent / Recon Nest with firing slits & sandbag berms.

        // 1. Base Ground Tarp
        var tarp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tarp.transform.SetParent(parent.transform, false);
        tarp.transform.localScale = new Vector3(1.7f, 0.08f, 3.6f);
        tarp.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        Object.Destroy(tarp.GetComponent<Collider>());
        tarp.GetComponent<MeshRenderer>().material = MakeMat(new Color(0.18f, 0.22f, 0.16f));

        // 2. Main Tent Canvas (Woodland Olive Drab)
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.transform.SetParent(parent.transform, false);
        body.transform.localScale = new Vector3(1.6f, 0.85f, 3.4f);
        body.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        Object.Destroy(body.GetComponent<Collider>());
        body.GetComponent<MeshRenderer>().material = MakeMat(new Color(0.22f, 0.34f, 0.20f));

        // 3. Camouflage Sloped Roof Ridge
        var ridge = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ridge.transform.SetParent(parent.transform, false);
        ridge.transform.localScale = new Vector3(1.15f, 0.55f, 3.45f);
        ridge.transform.localPosition = new Vector3(0f, 1.10f, 0f);
        Object.Destroy(ridge.GetComponent<Collider>());
        ridge.GetComponent<MeshRenderer>().material = MakeMat(new Color(0.16f, 0.27f, 0.15f));

        // 4. Ghillie Camouflage Netting Layer across the top
        var netting = GameObject.CreatePrimitive(PrimitiveType.Cube);
        netting.transform.SetParent(parent.transform, false);
        netting.transform.localScale = new Vector3(0.85f, 0.18f, 3.2f);
        netting.transform.localPosition = new Vector3(0f, 1.40f, 0f);
        Object.Destroy(netting.GetComponent<Collider>());
        netting.GetComponent<MeshRenderer>().material = MakeMat(new Color(0.28f, 0.38f, 0.22f));

        // 5. Front Sandbag Emplacement Berm (facing East +X toward enemy lines)
        var sandbags = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sandbags.transform.SetParent(parent.transform, false);
        sandbags.transform.localScale = new Vector3(0.35f, 0.42f, 3.3f);
        sandbags.transform.localPosition = new Vector3(0.72f, 0.22f, 0f);
        Object.Destroy(sandbags.GetComponent<Collider>());
        sandbags.GetComponent<MeshRenderer>().material = MakeMat(new Color(0.55f, 0.46f, 0.33f));

        // 6. Dual Sniper Firing Slits & Mounted Heavy Sniper Rifles (facing +X)
        Material interiorMat = MakeMat(new Color(0.08f, 0.08f, 0.1f));
        Material rifleMat = MakeMat(new Color(0.12f, 0.12f, 0.14f), 0.7f, 0.6f);
        Material opticMat = MakeMat(new Color(0.08f, 0.08f, 0.08f), 0.8f, 0.7f);

        foreach (float zOff in new float[] { -0.8f, 0.8f })
        {
            // Dark interior embrasure slit
            var slit = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slit.transform.SetParent(parent.transform, false);
            slit.transform.localScale = new Vector3(0.1f, 0.32f, 0.65f);
            slit.transform.localPosition = new Vector3(0.81f, 0.55f, zOff);
            Object.Destroy(slit.GetComponent<Collider>());
            slit.GetComponent<MeshRenderer>().material = interiorMat;

            // Long sniper barrel extending forward
            var barrel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barrel.transform.SetParent(parent.transform, false);
            barrel.transform.localScale = new Vector3(0.95f, 0.12f, 0.12f);
            barrel.transform.localPosition = new Vector3(0.95f, 0.52f, zOff);
            Object.Destroy(barrel.GetComponent<Collider>());
            barrel.GetComponent<MeshRenderer>().material = rifleMat;

            // Optical telescopic sight
            var scope = GameObject.CreatePrimitive(PrimitiveType.Cube);
            scope.transform.SetParent(parent.transform, false);
            scope.transform.localScale = new Vector3(0.45f, 0.09f, 0.09f);
            scope.transform.localPosition = new Vector3(0.85f, 0.62f, zOff);
            Object.Destroy(scope.GetComponent<Collider>());
            scope.GetComponent<MeshRenderer>().material = opticMat;
        }

        // 7. High-Powered Spotting Scope on Center Stand
        var spotScope = GameObject.CreatePrimitive(PrimitiveType.Cube);
        spotScope.transform.SetParent(parent.transform, false);
        spotScope.transform.localScale = new Vector3(0.5f, 0.14f, 0.14f);
        spotScope.transform.localPosition = new Vector3(0.55f, 1.35f, 0f);
        Object.Destroy(spotScope.GetComponent<Collider>());
        spotScope.GetComponent<MeshRenderer>().material = MakeMat(new Color(0.18f, 0.22f, 0.18f));

        var scopeStand = GameObject.CreatePrimitive(PrimitiveType.Cube);
        scopeStand.transform.SetParent(parent.transform, false);
        scopeStand.transform.localScale = new Vector3(0.14f, 0.45f, 0.14f);
        scopeStand.transform.localPosition = new Vector3(0.55f, 1.05f, 0f);
        Object.Destroy(scopeStand.GetComponent<Collider>());
        scopeStand.GetComponent<MeshRenderer>().material = MakeMat(new Color(0.1f, 0.1f, 0.1f));

        // 8. Military Ammo Crate at side
        var ammoBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ammoBox.transform.SetParent(parent.transform, false);
        ammoBox.transform.localScale = new Vector3(0.45f, 0.35f, 0.55f);
        ammoBox.transform.localPosition = new Vector3(-0.65f, 0.20f, 1.3f);
        Object.Destroy(ammoBox.GetComponent<Collider>());
        ammoBox.GetComponent<MeshRenderer>().material = MakeMat(new Color(0.32f, 0.38f, 0.22f));

        // 9. Comms Antenna at rear corner
        var antenna = GameObject.CreatePrimitive(PrimitiveType.Cube);
        antenna.transform.SetParent(parent.transform, false);
        antenna.transform.localScale = new Vector3(0.06f, 1.3f, 0.06f);
        antenna.transform.localPosition = new Vector3(-0.7f, 1.15f, -1.5f);
        Object.Destroy(antenna.GetComponent<Collider>());
        antenna.GetComponent<MeshRenderer>().material = MakeMat(new Color(0.1f, 0.1f, 0.12f));
    }

    private static void BuildBarricade(GameObject parent)
    {
        Material sandbagMat = MakeMat(new Color(0.62f, 0.5f, 0.32f));
        Material woodSpikeMat = MakeMat(new Color(0.32f, 0.22f, 0.14f));

        // 1. Lower row of sandbags
        for (int i = -1; i <= 1; i++)
        {
            var bag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bag.transform.SetParent(parent.transform, false);
            bag.transform.localScale = new Vector3(0.55f, 0.32f, 0.52f);
            bag.transform.localPosition = new Vector3(0f, 0.16f, i * 0.54f);
            Object.Destroy(bag.GetComponent<Collider>());
            bag.GetComponent<MeshRenderer>().material = sandbagMat;
        }

        // 2. Upper row of sandbags
        for (int i = 0; i < 2; i++)
        {
            var bag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bag.transform.SetParent(parent.transform, false);
            bag.transform.localScale = new Vector3(0.52f, 0.32f, 0.52f);
            bag.transform.localPosition = new Vector3(0f, 0.46f, (i - 0.5f) * 0.54f);
            Object.Destroy(bag.GetComponent<Collider>());
            bag.GetComponent<MeshRenderer>().material = sandbagMat;
        }

        // 3. Wooden Defensive Spikes facing east
        var spike1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        spike1.transform.SetParent(parent.transform, false);
        spike1.transform.localScale = new Vector3(0.75f, 0.14f, 0.14f);
        spike1.transform.localRotation = Quaternion.Euler(0, 0, -25);
        spike1.transform.localPosition = new Vector3(0.35f, 0.35f, -0.3f);
        Object.Destroy(spike1.GetComponent<Collider>());
        spike1.GetComponent<MeshRenderer>().material = woodSpikeMat;

        var spike2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        spike2.transform.SetParent(parent.transform, false);
        spike2.transform.localScale = new Vector3(0.75f, 0.14f, 0.14f);
        spike2.transform.localRotation = Quaternion.Euler(0, 0, -25);
        spike2.transform.localPosition = new Vector3(0.35f, 0.35f, 0.3f);
        Object.Destroy(spike2.GetComponent<Collider>());
        spike2.GetComponent<MeshRenderer>().material = woodSpikeMat;
    }

    private static void BuildHealStation(GameObject parent)
    {
        // 1. Clean White Clinic Tent
        var tent = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tent.transform.SetParent(parent.transform, false);
        tent.transform.localScale = new Vector3(1.5f, 1.1f, 1.5f);
        tent.transform.localPosition = new Vector3(0f, 0.55f, 0f);
        Object.Destroy(tent.GetComponent<Collider>());
        tent.GetComponent<MeshRenderer>().material = MakeMat(new Color(0.92f, 0.95f, 0.92f));

        // 2. Red Cross on Top
        Material redMat = MakeMat(new Color(0.85f, 0.15f, 0.15f));

        var crossH = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crossH.transform.SetParent(parent.transform, false);
        crossH.transform.localScale = new Vector3(0.9f, 0.12f, 0.3f);
        crossH.transform.localPosition = new Vector3(0f, 1.16f, 0f);
        Object.Destroy(crossH.GetComponent<Collider>());
        crossH.GetComponent<MeshRenderer>().material = redMat;

        var crossV = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crossV.transform.SetParent(parent.transform, false);
        crossV.transform.localScale = new Vector3(0.3f, 0.12f, 0.9f);
        crossV.transform.localPosition = new Vector3(0f, 1.16f, 0f);
        Object.Destroy(crossV.GetComponent<Collider>());
        crossV.GetComponent<MeshRenderer>().material = redMat;

        // 3. Glowing Medical Beacon Lamp
        var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        lamp.transform.SetParent(parent.transform, false);
        lamp.transform.localScale = new Vector3(0.28f, 0.28f, 0.28f);
        lamp.transform.localPosition = new Vector3(0f, 1.35f, 0f);
        Object.Destroy(lamp.GetComponent<Collider>());
        lamp.GetComponent<MeshRenderer>().material = MakeMat(new Color(0.2f, 1f, 0.4f));
    }

    private static void BuildSpecialForceDepot(GameObject parent)
    {
        // 1. Armored Obsidian Depot Bunker (1x1 cell)
        var bunker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bunker.transform.SetParent(parent.transform, false);
        bunker.transform.localScale = new Vector3(1.65f, 1.15f, 1.65f);
        bunker.transform.localPosition = new Vector3(0f, 0.58f, 0f);
        Object.Destroy(bunker.GetComponent<Collider>());
        bunker.GetComponent<MeshRenderer>().material = MakeMat(new Color(0.18f, 0.20f, 0.24f), 0.55f, 0.5f);

        // 2. Gold Tactical Roof Trim / Command Crown
        var roofTrim = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roofTrim.transform.SetParent(parent.transform, false);
        roofTrim.transform.localScale = new Vector3(1.42f, 0.22f, 1.42f);
        roofTrim.transform.localPosition = new Vector3(0f, 1.24f, 0f);
        Object.Destroy(roofTrim.GetComponent<Collider>());
        roofTrim.GetComponent<MeshRenderer>().material = MakeMat(new Color(0.92f, 0.74f, 0.18f), 0.65f, 0.65f);

        // 3. Reinforced Blast Door facing East
        var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
        door.transform.SetParent(parent.transform, false);
        door.transform.localScale = new Vector3(0.12f, 0.78f, 0.65f);
        door.transform.localPosition = new Vector3(0.82f, 0.39f, 0f);
        Object.Destroy(door.GetComponent<Collider>());
        door.GetComponent<MeshRenderer>().material = MakeMat(new Color(0.1f, 0.11f, 0.13f), 0.7f, 0.6f);

        // 4. Guardian Shield Emblem on Top
        var emblem = GameObject.CreatePrimitive(PrimitiveType.Cube);
        emblem.transform.SetParent(parent.transform, false);
        emblem.transform.localScale = new Vector3(0.45f, 0.38f, 0.45f);
        emblem.transform.localPosition = new Vector3(0f, 1.48f, 0f);
        Object.Destroy(emblem.GetComponent<Collider>());
        emblem.GetComponent<MeshRenderer>().material = MakeMat(new Color(1.0f, 0.82f, 0.22f), 0.75f, 0.75f);
    }

    private static void BuildLightTankFactory(GameObject parent)
    {
        // Light Tank Factory Footprint: 2x2 cells (4m x 4m in world space)
        // 1. Heavy Concrete & Steel Foundation Pad
        var basePad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        basePad.transform.SetParent(parent.transform, false);
        basePad.transform.localScale = new Vector3(3.8f, 0.25f, 3.8f);
        basePad.transform.localPosition = new Vector3(0f, 0.125f, 0f);
        Object.Destroy(basePad.GetComponent<Collider>());
        basePad.GetComponent<MeshRenderer>().material = MakeMat(new Color(0.24f, 0.25f, 0.26f), 0.4f, 0.35f);

        // 2. Main Armored Factory Hangar (2x2 wide)
        var hangar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hangar.transform.SetParent(parent.transform, false);
        hangar.transform.localScale = new Vector3(3.4f, 1.85f, 3.4f);
        hangar.transform.localPosition = new Vector3(0f, 1.15f, 0f);
        Object.Destroy(hangar.GetComponent<Collider>());
        hangar.GetComponent<MeshRenderer>().material = MakeMat(new Color(0.28f, 0.36f, 0.25f), 0.5f, 0.45f);

        // 3. Industrial Sawtooth / Gantry Roof
        var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roof.transform.SetParent(parent.transform, false);
        roof.transform.localScale = new Vector3(3.55f, 0.32f, 3.55f);
        roof.transform.localPosition = new Vector3(0f, 2.22f, 0f);
        Object.Destroy(roof.GetComponent<Collider>());
        roof.GetComponent<MeshRenderer>().material = MakeMat(new Color(0.18f, 0.22f, 0.18f), 0.6f, 0.5f);

        // 4. Wide Tank Roll-Out Bay Door (East side)
        var bayDoor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bayDoor.transform.SetParent(parent.transform, false);
        bayDoor.transform.localScale = new Vector3(0.16f, 1.45f, 2.1f);
        bayDoor.transform.localPosition = new Vector3(1.72f, 0.95f, 0f);
        Object.Destroy(bayDoor.GetComponent<Collider>());
        bayDoor.GetComponent<MeshRenderer>().material = MakeMat(new Color(0.12f, 0.13f, 0.14f), 0.7f, 0.5f);

        // 5. Hazard Orange Frame Above Bay Door
        var hazardHeader = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hazardHeader.transform.SetParent(parent.transform, false);
        hazardHeader.transform.localScale = new Vector3(0.22f, 0.24f, 2.3f);
        hazardHeader.transform.localPosition = new Vector3(1.74f, 1.78f, 0f);
        Object.Destroy(hazardHeader.GetComponent<Collider>());
        hazardHeader.GetComponent<MeshRenderer>().material = MakeMat(new Color(0.95f, 0.52f, 0.12f), 0.5f, 0.6f);

        // 6. Twin Heavy Exhaust Smokestacks on Back-Left
        Material stackMat = MakeMat(new Color(0.16f, 0.17f, 0.18f), 0.7f, 0.5f);
        foreach (float zOff in new float[] { -0.9f, 0.9f })
        {
            var stack = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stack.transform.SetParent(parent.transform, false);
            stack.transform.localScale = new Vector3(0.42f, 0.75f, 0.42f);
            stack.transform.localPosition = new Vector3(-1.15f, 2.75f, zOff);
            Object.Destroy(stack.GetComponent<Collider>());
            stack.GetComponent<MeshRenderer>().material = stackMat;
        }
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

    private static void BuildMissileFoundry(GameObject parent)
    {
        // Missile Foundry Footprint: กว้าง 3 ยาว 4 ช่อง (4 cells in X = 8.0m, 3 cells in Z = 6.0m)
        GameObject fbxPrefab = Resources.Load<GameObject>("Models/Missile Foundry");
        if (fbxPrefab != null)
        {
            GameObject modelInstance = Object.Instantiate(fbxPrefab, parent.transform);
            modelInstance.name = "MissileFoundry_FBX";
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = fbxPrefab.transform.localRotation;
            modelInstance.transform.localScale = fbxPrefab.transform.localScale;

            // Remove embedded Blender camera (Camera_Factory_Iso), lights, and colliders
            StripEmbeddedSceneComponents(modelInstance);

            Renderer[] renderers = modelInstance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0 && TryCalculateExactWorldBounds(modelInstance, out Bounds combined))
            {
                // Upgrade materials if needed so FBX never renders magenta in URP
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
                                Color c = (mats[m] != null && mats[m].HasProperty("_Color")) ? mats[m].color : new Color(0.32f, 0.36f, 0.40f);
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

                // Orient longer horizontal side along X (4 blocks = 8.0m) and shorter side along Z (3 blocks = 6.0m)
                if (combined.size.z > combined.size.x)
                {
                    modelInstance.transform.Rotate(0f, 90f, 0f, Space.World);
                }
                // Flip 180 degrees around Y so Missile Foundry faces East (+X) toward the incoming zombies!
                modelInstance.transform.Rotate(0f, 180f, 0f, Space.World);
                TryCalculateExactWorldBounds(modelInstance, out combined);

                // Align bottom-center of the model to parent's origin before stretching parent
                Vector3 rootWorldPos = parent.transform.position;
                modelInstance.transform.position += new Vector3(
                    rootWorldPos.x - combined.center.x,
                    rootWorldPos.y - combined.min.y,
                    rootWorldPos.z - combined.center.z
                );

                // Fit neatly into 4x3 blocks (7.6m x 5.6m) with proportional height
                float sizeX = Mathf.Max(0.01f, combined.size.x);
                float sizeY = Mathf.Max(0.01f, combined.size.y);
                float sizeZ = Mathf.Max(0.01f, combined.size.z);

                float targetX = 7.6f; // 4 cells (8.0m) with clean border margin
                float targetZ = 5.6f; // 3 cells (6.0m) with clean border margin
                float avgHorizScale = ((targetX / sizeX) + (targetZ / sizeZ)) * 0.5f;
                float targetY = Mathf.Clamp(sizeY * avgHorizScale, 2.6f, 3.8f);

                parent.transform.localScale = new Vector3(
                    targetX / sizeX,
                    targetY / sizeY,
                    targetZ / sizeZ
                );
                return;
            }
        }

        // Procedural fallback if FBX is unavailable (4x3 cells = 8m x 6m)
        var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pad.transform.SetParent(parent.transform, false);
        pad.transform.localScale = new Vector3(7.6f, 0.25f, 5.6f);
        pad.transform.localPosition = new Vector3(0f, 0.125f, 0f);
        Object.Destroy(pad.GetComponent<Collider>());
        pad.GetComponent<MeshRenderer>().material = MakeMat(new Color(0.22f, 0.24f, 0.27f), 0.5f, 0.4f);

        var siloBunker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        siloBunker.transform.SetParent(parent.transform, false);
        siloBunker.transform.localScale = new Vector3(7.0f, 2.2f, 5.0f);
        siloBunker.transform.localPosition = new Vector3(0f, 1.25f, 0f);
        Object.Destroy(siloBunker.GetComponent<Collider>());
        siloBunker.GetComponent<MeshRenderer>().material = MakeMat(new Color(0.28f, 0.32f, 0.36f), 0.6f, 0.5f);
    }

    private static void BuildSupplyDepot(GameObject parent)
    {
        // 1. Reinforced Concrete Logistics Pad (1x1 cell)
        var basePad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        basePad.transform.SetParent(parent.transform, false);
        basePad.transform.localScale = new Vector3(1.75f, 0.18f, 1.75f);
        basePad.transform.localPosition = new Vector3(0f, 0.09f, 0f);
        Object.Destroy(basePad.GetComponent<Collider>());
        basePad.GetComponent<MeshRenderer>().material = MakeMat(new Color(0.28f, 0.30f, 0.32f), 0.4f, 0.35f);

        // 2. Main Supply Storage Container / Warehouse Body
        var warehouse = GameObject.CreatePrimitive(PrimitiveType.Cube);
        warehouse.transform.SetParent(parent.transform, false);
        warehouse.transform.localScale = new Vector3(1.35f, 0.95f, 1.45f);
        warehouse.transform.localPosition = new Vector3(-0.12f, 0.65f, 0f);
        Object.Destroy(warehouse.GetComponent<Collider>());
        warehouse.GetComponent<MeshRenderer>().material = MakeMat(new Color(0.76f, 0.62f, 0.28f), 0.35f, 0.45f); // Military Cargo Khaki/Gold

        // 3. Corrugated Roof Trim
        var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roof.transform.SetParent(parent.transform, false);
        roof.transform.localScale = new Vector3(1.45f, 0.16f, 1.55f);
        roof.transform.localPosition = new Vector3(-0.12f, 1.20f, 0f);
        Object.Destroy(roof.GetComponent<Collider>());
        roof.GetComponent<MeshRenderer>().material = MakeMat(new Color(0.22f, 0.28f, 0.34f), 0.5f, 0.5f);

        // 4. Stacked Supply & Ammo Crates at Front-Right
        Material crateMat1 = MakeMat(new Color(0.35f, 0.48f, 0.28f)); // Olive ammo crate
        Material crateMat2 = MakeMat(new Color(0.55f, 0.38f, 0.20f)); // Wooden ration crate

        var crateA = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crateA.transform.SetParent(parent.transform, false);
        crateA.transform.localScale = new Vector3(0.42f, 0.38f, 0.48f);
        crateA.transform.localPosition = new Vector3(0.62f, 0.37f, -0.35f);
        Object.Destroy(crateA.GetComponent<Collider>());
        crateA.GetComponent<MeshRenderer>().material = crateMat1;

        var crateB = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crateB.transform.SetParent(parent.transform, false);
        crateB.transform.localScale = new Vector3(0.42f, 0.38f, 0.48f);
        crateB.transform.localPosition = new Vector3(0.62f, 0.37f, 0.32f);
        Object.Destroy(crateB.GetComponent<Collider>());
        crateB.GetComponent<MeshRenderer>().material = crateMat2;

        var crateTop = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crateTop.transform.SetParent(parent.transform, false);
        crateTop.transform.localScale = new Vector3(0.38f, 0.34f, 0.44f);
        crateTop.transform.localPosition = new Vector3(0.60f, 0.73f, -0.02f);
        Object.Destroy(crateTop.GetComponent<Collider>());
        crateTop.GetComponent<MeshRenderer>().material = crateMat1;

        // 5. Glowing Cyan Supply Antenna / Beacon on Roof
        var beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beacon.transform.SetParent(parent.transform, false);
        beacon.transform.localScale = new Vector3(0.28f, 0.18f, 0.28f);
        beacon.transform.localPosition = new Vector3(-0.12f, 1.42f, 0f);
        Object.Destroy(beacon.GetComponent<Collider>());
        beacon.GetComponent<MeshRenderer>().material = MakeMat(new Color(0.25f, 0.88f, 1.0f), 0.6f, 0.8f);
    }

    private static void BuildFrontlineSniperTower(GameObject parent)
    {
        // Tall Steel Watchtower for Frontline Sniper (1x1 cell footprint = 2m x 2m base, ~3.6m tall)
        Material steelBeamMat = MakeMat(new Color(0.26f, 0.29f, 0.33f), 0.75f, 0.55f);
        Material darkSteelMat = MakeMat(new Color(0.16f, 0.18f, 0.21f), 0.8f, 0.6f);
        Material concretePadMat = MakeMat(new Color(0.32f, 0.33f, 0.35f), 0.2f, 0.3f);
        Material hazardStripMat = MakeMat(new Color(0.92f, 0.68f, 0.15f), 0.4f, 0.5f);

        // 1. Concrete Anchor Base Pad
        var basePad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        basePad.transform.SetParent(parent.transform, false);
        basePad.transform.localScale = new Vector3(1.65f, 0.14f, 1.65f);
        basePad.transform.localPosition = new Vector3(0f, 0.07f, 0f);
        Object.Destroy(basePad.GetComponent<Collider>());
        basePad.GetComponent<MeshRenderer>().material = concretePadMat;

        // 2. Four Tall Structural Steel Legs (rising up to the watchtower deck at y = 2.45m)
        float[] legOffsets = new float[] { -0.62f, 0.62f };
        foreach (float lx in legOffsets)
        {
            foreach (float lz in legOffsets)
            {
                var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leg.transform.SetParent(parent.transform, false);
                leg.transform.localScale = new Vector3(0.14f, 2.40f, 0.14f);
                leg.transform.localPosition = new Vector3(lx, 1.25f, lz);
                Object.Destroy(leg.GetComponent<Collider>());
                leg.GetComponent<MeshRenderer>().material = steelBeamMat;
            }
        }

        // 3. Horizontal Steel Cross-Bracing Struts (Lower & Upper tiers)
        foreach (float tierY in new float[] { 0.85f, 1.68f })
        {
            foreach (float side in legOffsets)
            {
                var beamX = GameObject.CreatePrimitive(PrimitiveType.Cube);
                beamX.transform.SetParent(parent.transform, false);
                beamX.transform.localScale = new Vector3(1.32f, 0.08f, 0.08f);
                beamX.transform.localPosition = new Vector3(0f, tierY, side);
                Object.Destroy(beamX.GetComponent<Collider>());
                beamX.GetComponent<MeshRenderer>().material = darkSteelMat;

                var beamZ = GameObject.CreatePrimitive(PrimitiveType.Cube);
                beamZ.transform.SetParent(parent.transform, false);
                beamZ.transform.localScale = new Vector3(0.08f, 0.08f, 1.32f);
                beamZ.transform.localPosition = new Vector3(side, tierY, 0f);
                Object.Destroy(beamZ.GetComponent<Collider>());
                beamZ.GetComponent<MeshRenderer>().material = darkSteelMat;
            }
        }

        // 4. Steel Access Ladder on Rear Side (-X)
        for (int r = 0; r < 6; r++)
        {
            var rung = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rung.transform.SetParent(parent.transform, false);
            rung.transform.localScale = new Vector3(0.05f, 0.04f, 0.38f);
            rung.transform.localPosition = new Vector3(-0.66f, 0.35f + r * 0.36f, 0f);
            Object.Destroy(rung.GetComponent<Collider>());
            rung.GetComponent<MeshRenderer>().material = hazardStripMat;
        }

        // 5. Elevated Steel Crow's Nest Platform Deck (y = 2.45m)
        var deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
        deck.transform.SetParent(parent.transform, false);
        deck.transform.localScale = new Vector3(1.55f, 0.14f, 1.55f);
        deck.transform.localPosition = new Vector3(0f, 2.45f, 0f);
        Object.Destroy(deck.GetComponent<Collider>());
        deck.GetComponent<MeshRenderer>().material = darkSteelMat;

        // 6. Armored Steel Guardrails / Parapet around Platform
        var frontShield = GameObject.CreatePrimitive(PrimitiveType.Cube);
        frontShield.transform.SetParent(parent.transform, false);
        frontShield.transform.localScale = new Vector3(0.10f, 0.42f, 1.52f);
        frontShield.transform.localPosition = new Vector3(0.72f, 2.72f, 0f);
        Object.Destroy(frontShield.GetComponent<Collider>());
        frontShield.GetComponent<MeshRenderer>().material = steelBeamMat;

        var backRail = GameObject.CreatePrimitive(PrimitiveType.Cube);
        backRail.transform.SetParent(parent.transform, false);
        backRail.transform.localScale = new Vector3(0.08f, 0.36f, 1.52f);
        backRail.transform.localPosition = new Vector3(-0.72f, 2.70f, 0f);
        Object.Destroy(backRail.GetComponent<Collider>());
        backRail.GetComponent<MeshRenderer>().material = steelBeamMat;

        foreach (float sideZ in new float[] { -0.72f, 0.72f })
        {
            var sideRail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sideRail.transform.SetParent(parent.transform, false);
            sideRail.transform.localScale = new Vector3(1.48f, 0.38f, 0.08f);
            sideRail.transform.localPosition = new Vector3(0f, 2.70f, sideZ);
            Object.Destroy(sideRail.GetComponent<Collider>());
            sideRail.GetComponent<MeshRenderer>().material = steelBeamMat;
        }

        // 7. Steel Watchtower Canopy Roof & Corner Posts (y = 3.45m)
        foreach (float lx in new float[] { -0.66f, 0.66f })
        {
            foreach (float lz in new float[] { -0.66f, 0.66f })
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                post.transform.SetParent(parent.transform, false);
                post.transform.localScale = new Vector3(0.07f, 0.85f, 0.07f);
                post.transform.localPosition = new Vector3(lx, 2.95f, lz);
                Object.Destroy(post.GetComponent<Collider>());
                post.GetComponent<MeshRenderer>().material = darkSteelMat;
            }
        }

        var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roof.transform.SetParent(parent.transform, false);
        roof.transform.localScale = new Vector3(1.62f, 0.12f, 1.62f);
        roof.transform.localPosition = new Vector3(0f, 3.42f, 0f);
        Object.Destroy(roof.GetComponent<Collider>());
        roof.GetComponent<MeshRenderer>().material = steelBeamMat;

        // 8. Rotating Sniper & Heavy Anti-Materiel Rifle Mount on Top Platform ("TurretMount")
        var turretMount = new GameObject("TurretMount");
        turretMount.transform.SetParent(parent.transform, false);
        turretMount.transform.localPosition = new Vector3(0f, 2.52f, 0f);

        // Sniper Operator Torso & Helmet
        var torso = GameObject.CreatePrimitive(PrimitiveType.Cube);
        torso.transform.SetParent(turretMount.transform, false);
        torso.transform.localScale = new Vector3(0.32f, 0.44f, 0.36f);
        torso.transform.localPosition = new Vector3(-0.08f, 0.24f, 0f);
        Object.Destroy(torso.GetComponent<Collider>());
        torso.GetComponent<MeshRenderer>().material = MakeMat(new Color(0.20f, 0.32f, 0.24f));

        var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.transform.SetParent(turretMount.transform, false);
        head.transform.localScale = new Vector3(0.24f, 0.24f, 0.24f);
        head.transform.localPosition = new Vector3(-0.08f, 0.58f, 0f);
        Object.Destroy(head.GetComponent<Collider>());
        head.GetComponent<MeshRenderer>().material = MakeMat(new Color(0.15f, 0.24f, 0.18f));

        var visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visor.transform.SetParent(turretMount.transform, false);
        visor.transform.localScale = new Vector3(0.06f, 0.09f, 0.22f);
        visor.transform.localPosition = new Vector3(0.05f, 0.58f, 0f);
        Object.Destroy(visor.GetComponent<Collider>());
        visor.GetComponent<MeshRenderer>().material = hazardStripMat;

        // Heavy Anti-Materiel Sniper Rifle Barrel & Scope aiming forward (+Z of TurretMount)
        var rifleBody = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rifleBody.transform.SetParent(turretMount.transform, false);
        rifleBody.transform.localScale = new Vector3(0.12f, 0.12f, 1.15f);
        rifleBody.transform.localPosition = new Vector3(0.14f, 0.36f, 0.42f);
        Object.Destroy(rifleBody.GetComponent<Collider>());
        rifleBody.GetComponent<MeshRenderer>().material = darkSteelMat;

        var scope = GameObject.CreatePrimitive(PrimitiveType.Cube);
        scope.transform.SetParent(turretMount.transform, false);
        scope.transform.localScale = new Vector3(0.08f, 0.08f, 0.42f);
        scope.transform.localPosition = new Vector3(0.14f, 0.46f, 0.28f);
        Object.Destroy(scope.GetComponent<Collider>());
        scope.GetComponent<MeshRenderer>().material = hazardStripMat;

        // Default orient TurretMount facing East (+X)
        turretMount.transform.localRotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
    }
}
