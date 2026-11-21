#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ensures pixel fonts placed in Assets/Resources/Fonts/ import cleanly as crisp Dynamic fonts
/// with Windows Emoji/Symbol fallbacks configured in their fontNames list.
/// </summary>
public class ConfigurePixelFonts : AssetPostprocessor
{
    private static readonly string[] SharedFallbacks = new string[]
    {
        "Segoe UI Emoji",
        "Segoe UI Symbol",
        "Malgun Gothic",
        "Arial"
    };

    void OnPreprocessAsset()
    {
        if (!assetPath.StartsWith("Assets/Resources/Fonts/") || !assetPath.EndsWith(".ttf", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (assetImporter is TrueTypeFontImporter importer)
        {
            ConfigureImporter(importer, assetPath);
        }
    }

    [MenuItem("Tools/Configure Pixel Fonts")]
    public static void ConfigureAllPixelFonts()
    {
        string[] guids = AssetDatabase.FindAssets("t:Font", new[] { "Assets/Resources/Fonts" });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (AssetImporter.GetAtPath(path) is TrueTypeFontImporter importer)
            {
                if (ConfigureImporter(importer, path))
                {
                    importer.SaveAndReimport();
                }
            }
        }
        Debug.Log($"[ConfigurePixelFonts] Configured {guids.Length} pixel font(s) in Assets/Resources/Fonts.");
    }

    private static bool ConfigureImporter(TrueTypeFontImporter importer, string path)
    {
        bool changed = false;

        if (importer.fontTextureCase != FontTextureCase.Dynamic)
        {
            importer.fontTextureCase = FontTextureCase.Dynamic;
            changed = true;
        }

        if (!importer.includeFontData)
        {
            importer.includeFontData = true;
            changed = true;
        }

        if (importer.fontRenderingMode != FontRenderingMode.HintedSmooth)
        {
            importer.fontRenderingMode = FontRenderingMode.HintedSmooth;
            changed = true;
        }

        string primary = GetPrimaryFamilyName(path);

        string[] desiredNames = new string[1 + SharedFallbacks.Length];
        desiredNames[0] = primary;
        for (int i = 0; i < SharedFallbacks.Length; i++)
        {
            desiredNames[i + 1] = SharedFallbacks[i];
        }

        string[] existing = importer.fontNames;
        bool namesMatch = existing != null && existing.Length == desiredNames.Length;
        if (namesMatch)
        {
            for (int i = 0; i < desiredNames.Length; i++)
            {
                if (existing[i] != desiredNames[i])
                {
                    namesMatch = false;
                    break;
                }
            }
        }

        if (!namesMatch)
        {
            importer.fontNames = desiredNames;
            changed = true;
        }

        return changed;
    }

    private static string GetPrimaryFamilyName(string path)
    {
        if (path.IndexOf("silkscreen", System.StringComparison.OrdinalIgnoreCase) >= 0) return "Silkscreen";
        if (path.IndexOf("jetbrains", System.StringComparison.OrdinalIgnoreCase) >= 0) return "JetBrains Mono";
        if (path.IndexOf("sharetech", System.StringComparison.OrdinalIgnoreCase) >= 0) return "Share Tech Mono";
        if (path.IndexOf("vt323", System.StringComparison.OrdinalIgnoreCase) >= 0) return "VT323";
        if (path.IndexOf("dogica", System.StringComparison.OrdinalIgnoreCase) >= 0) return "Dogica Pixel";
        return "Pixelify Sans";
    }
}
#endif
