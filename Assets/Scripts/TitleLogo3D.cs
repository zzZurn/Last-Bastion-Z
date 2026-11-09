using System.Collections.Generic;
using UnityEngine;

public class TitleLogo3D : MonoBehaviour
{
    public static TitleLogo3D Instance { get; private set; }

    private GameObject logoRoot;
    private readonly List<BlockAnimData> blocks = new List<BlockAnimData>();

    private struct BlockAnimData
    {
        public Transform frontTransform;
        public Transform backTransform;
        public Vector3 baseFrontPos;
        public Vector3 baseBackPos;
        public float phase;
    }

    private static Shader GetShader()
    {
        return Shader.Find("Universal Render Pipeline/Lit") ??
               Shader.Find("Standard") ??
               Shader.Find("Universal Render Pipeline/Unlit") ??
               Shader.Find("Unlit/Color");
    }

    private static Material MakeMat(Color color, float metallic = 0.25f, float smoothness = 0.55f)
    {
        var mat = new Material(GetShader()) { color = color };
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        return mat;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Build3DBlockLogo();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (logoRoot != null) Destroy(logoRoot);
    }

    private void Build3DBlockLogo()
    {
        if (logoRoot != null) Destroy(logoRoot);

        logoRoot = new GameObject("LastBastionZ_3DBlockLogo");
        blocks.Clear();

        // Shared materials for the 3D blocks
        Material goldFrontMat = MakeMat(new Color(1.0f, 0.78f, 0.22f), 0.35f, 0.65f);
        Material goldBackMat = MakeMat(new Color(0.55f, 0.36f, 0.08f), 0.2f, 0.4f);

        Material steelFrontMat = MakeMat(new Color(0.86f, 0.91f, 0.96f), 0.4f, 0.6f);
        Material steelBackMat = MakeMat(new Color(0.32f, 0.38f, 0.45f), 0.25f, 0.4f);

        Material zCrimsonFrontMat = MakeMat(new Color(0.96f, 0.18f, 0.16f), 0.3f, 0.7f);
        Material zCrimsonBackMat = MakeMat(new Color(0.45f, 0.05f, 0.05f), 0.2f, 0.4f);

        Material baseBarFrontMat = MakeMat(new Color(0.32f, 0.43f, 0.28f), 0.2f, 0.4f);
        Material baseBarBackMat = MakeMat(new Color(0.16f, 0.22f, 0.14f), 0.1f, 0.3f);

        // 5-row bitmap font definitions
        Dictionary<char, string[]> font5x5 = new Dictionary<char, string[]>
        {
            ['L'] = new[] {
                "#...",
                "#...",
                "#...",
                "#...",
                "####"
            },
            ['A'] = new[] {
                ".##.",
                "#..#",
                "####",
                "#..#",
                "#..#"
            },
            ['S'] = new[] {
                ".###",
                "#...",
                ".##.",
                "...#",
                "###."
            },
            ['T'] = new[] {
                "#####",
                "..#..",
                "..#..",
                "..#..",
                "..#.."
            },
            ['B'] = new[] {
                "###.",
                "#..#",
                "###.",
                "#..#",
                "###."
            },
            ['I'] = new[] {
                "###",
                ".#.",
                ".#.",
                ".#.",
                "###"
            },
            ['O'] = new[] {
                ".##.",
                "#..#",
                "#..#",
                "#..#",
                ".##."
            },
            ['N'] = new[] {
                "#...#",
                "##..#",
                "#.#.#",
                "#..##",
                "#...#"
            }
        };

        // Giant 11-row 'Z' emblem
        string[] giantZ = new[]
        {
            "########",
            "########",
            ".....##.",
            "....##..",
            "...##...",
            "..###...",
            "..##....",
            ".##.....",
            "##......",
            "########",
            "########"
        };

        const float cellStep = 1.05f;
        const float centerX = 22.5f;
        const float centerY = 5.0f;

        // 1. Build top word: "LAST" (rows 0..4, starting at col 7 to center over BASTION)
        PlaceWord("LAST", 7, 0, font5x5, cellStep, centerX, centerY, 0f, goldFrontMat, goldBackMat);

        // 2. Build bottom word: "BASTION" (rows 6..10, starting at col 0)
        PlaceWord("BASTION", 0, 6, font5x5, cellStep, centerX, centerY, 0f, steelFrontMat, steelBackMat);

        // 3. Build giant "Z" on the right (rows 0..10, starting at col 38, popped forward in Z)
        for (int r = 0; r < giantZ.Length; r++)
        {
            string line = giantZ[r];
            for (int c = 0; c < line.Length; c++)
            {
                if (line[c] == '#')
                {
                    Spawn3DVoxel(38 + c, r, cellStep, centerX, centerY, -0.65f, 1.55f, zCrimsonFrontMat, zCrimsonBackMat);
                }
            }
        }

        // 4. Build 3D fortified base bar underneath (row 12)
        for (int c = 0; c < 46; c += 2)
        {
            Spawn3DVoxel(c + 0.5f, 12.2f, cellStep, centerX, centerY, 0.2f, 1.1f,
                (c % 4 == 0) ? goldFrontMat : baseBarFrontMat,
                baseBarBackMat,
                new Vector3(1.85f, 0.55f, 1.4f));
        }
    }

    private void PlaceWord(
        string word,
        int startCol,
        int startRow,
        Dictionary<char, string[]> font,
        float cellStep,
        float centerX,
        float centerY,
        float zOffset,
        Material frontMat,
        Material backMat)
    {
        int curCol = startCol;
        foreach (char ch in word)
        {
            if (!font.TryGetValue(ch, out string[] glyph)) continue;
            int glyphWidth = glyph[0].Length;
            for (int r = 0; r < glyph.Length; r++)
            {
                for (int c = 0; c < glyphWidth; c++)
                {
                    if (glyph[r][c] == '#')
                    {
                        Spawn3DVoxel(curCol + c, startRow + r, cellStep, centerX, centerY, zOffset, 1.25f, frontMat, backMat);
                    }
                }
            }
            curCol += glyphWidth + 1;
        }
    }

    private void Spawn3DVoxel(
        float col,
        float row,
        float cellStep,
        float centerX,
        float centerY,
        float zOffset,
        float depthScale,
        Material frontMat,
        Material backMat,
        Vector3? customScale = null)
    {
        float x = (col - centerX) * cellStep;
        float y = (centerY - row) * cellStep;

        Vector3 frontScale = customScale ?? new Vector3(0.86f * cellStep, 0.86f * cellStep, depthScale);
        Vector3 backScale = new Vector3(frontScale.x * 1.06f, frontScale.y * 1.06f, frontScale.z);

        // Front 3D Cube
        GameObject frontCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        frontCube.name = "VoxelFront";
        frontCube.transform.SetParent(logoRoot.transform, false);
        Destroy(frontCube.GetComponent<Collider>());
        frontCube.GetComponent<MeshRenderer>().sharedMaterial = frontMat;

        // Back 3D Extrusion/Shadow Cube (gives clear 3D block thickness from all angles)
        GameObject backCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        backCube.name = "VoxelBack3D";
        backCube.transform.SetParent(logoRoot.transform, false);
        Destroy(backCube.GetComponent<Collider>());
        backCube.GetComponent<MeshRenderer>().sharedMaterial = backMat;

        Vector3 baseFront = new Vector3(x, y, zOffset);
        Vector3 baseBack = new Vector3(x + 0.16f, y - 0.16f, zOffset + frontScale.z * 0.55f);

        frontCube.transform.localPosition = baseFront;
        frontCube.transform.localScale = frontScale;

        backCube.transform.localPosition = baseBack;
        backCube.transform.localScale = backScale;

        blocks.Add(new BlockAnimData
        {
            frontTransform = frontCube.transform,
            backTransform = backCube.transform,
            baseFrontPos = baseFront,
            baseBackPos = baseBack,
            phase = col * 0.22f + row * 0.35f
        });
    }

    void LateUpdate()
    {
        if (logoRoot == null) return;

        bool showLogo = GameManager.Instance != null &&
                        GameManager.Instance.CurrentState == GameManager.GameState.MainMenu;

        if (logoRoot.activeSelf != showLogo)
        {
            logoRoot.SetActive(showLogo);
        }

        if (!showLogo) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        float t = Time.unscaledTime;
        float orthoFactor = cam.orthographic ? (cam.orthographicSize / 28f) : 1f;

        // Position in upper-half of camera view
        logoRoot.transform.position =
            cam.transform.position +
            cam.transform.forward * 24f +
            cam.transform.up * (5.5f * orthoFactor);

        logoRoot.transform.localScale = Vector3.one * (0.88f * orthoFactor);

        // 3D tilt & gentle oscillation so top, side, and front faces of the 3D cubes are clearly visible
        float tiltX = -15f + Mathf.Sin(t * 1.4f) * 4.5f;
        float tiltY = -14f + Mathf.Sin(t * 0.9f) * 16f;
        float tiltZ = Mathf.Cos(t * 1.1f) * 1.8f;
        logoRoot.transform.rotation = cam.transform.rotation * Quaternion.Euler(tiltX, tiltY, tiltZ);

        // Subtle 3D wave motion across individual voxel blocks
        for (int i = 0; i < blocks.Count; i++)
        {
            BlockAnimData b = blocks[i];
            if (b.frontTransform == null || b.backTransform == null) continue;

            float waveZ = Mathf.Sin(t * 2.6f + b.phase) * 0.22f;
            float waveY = Mathf.Cos(t * 2.6f + b.phase) * 0.08f;
            Vector3 offset = new Vector3(0f, waveY, waveZ);

            b.frontTransform.localPosition = b.baseFrontPos + offset;
            b.backTransform.localPosition = b.baseBackPos + offset;
        }
    }
}
