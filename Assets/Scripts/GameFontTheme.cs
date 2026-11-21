using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Centralized Font theme loader for all IMGUI HUD, Menu, and World-Space Labels.
/// Pairs Pixelify Sans (hero logo), Silkscreen (pixel headers &amp; unambiguous pixel badges),
/// and JetBrains Mono (ultra-legible UI text, buttons, resources &amp; numbers so 3/5/8 never blur together),
/// with automatic OS Emoji/Symbol fallbacks so icons never render as missing-glyph squares.
/// </summary>
public static class GameFontTheme
{
    private static bool _initialized = false;
    private static Font _displayFont;
    private static Font _headerFont;
    private static Font _headerRegularFont;
    private static Font _bodyFont;
    private static Font _bodySemiBoldFont;
    private static Font _bodyBoldFont;

    private static readonly string[] FallbackFamilies = new string[]
    {
        "Segoe UI Emoji",
        "Segoe UI Symbol",
        "Malgun Gothic",
        "Arial"
    };

    public static Font DisplayFont
    {
        get { EnsureLoaded(); return _displayFont ?? _headerFont ?? _bodyBoldFont; }
    }

    public static Font HeaderFont
    {
        get { EnsureLoaded(); return _headerFont ?? _bodyBoldFont ?? _bodyFont; }
    }

    public static Font HeaderRegularFont
    {
        get { EnsureLoaded(); return _headerRegularFont ?? _headerFont ?? _bodyBoldFont; }
    }

    public static Font BodyFont
    {
        get { EnsureLoaded(); return _bodyFont ?? _bodySemiBoldFont ?? _bodyBoldFont; }
    }

    public static Font BodySemiBoldFont
    {
        get { EnsureLoaded(); return _bodySemiBoldFont ?? _bodyBoldFont ?? _bodyFont; }
    }

    public static Font BodyBoldFont
    {
        get { EnsureLoaded(); return _bodyBoldFont ?? _bodySemiBoldFont ?? _bodyFont; }
    }

    public static void EnsureLoaded()
    {
        if (_initialized) return;
        _initialized = true;

        _displayFont       = LoadWithFallback("Fonts/PixelifySans-Bold", "Pixelify Sans Bold", "Pixelify Sans");
        _headerRegularFont = LoadWithFallback("Fonts/Silkscreen-Regular", "Silkscreen");
        _headerFont        = LoadWithFallback("Fonts/Silkscreen-Bold", "Silkscreen Bold", "Silkscreen");
        _bodyFont          = LoadWithFallback("Fonts/JetBrainsMono-Regular", "JetBrains Mono");
        _bodySemiBoldFont  = LoadWithFallback("Fonts/JetBrainsMono-SemiBold", "JetBrains Mono SemiBold", "JetBrains Mono");
        _bodyBoldFont      = LoadWithFallback("Fonts/JetBrainsMono-Bold", "JetBrains Mono Bold", "JetBrains Mono");
    }

    private static Font LoadWithFallback(string resourcePath, params string[] primaryFamilies)
    {
        Font font = Resources.Load<Font>(resourcePath);
        if (font == null) return null;

        if (font.dynamic)
        {
            List<string> merged = new List<string>(primaryFamilies.Length + FallbackFamilies.Length);
            if (font.fontNames != null)
            {
                for (int i = 0; i < font.fontNames.Length; i++)
                {
                    if (!string.IsNullOrEmpty(font.fontNames[i]) && !merged.Contains(font.fontNames[i]))
                    {
                        merged.Add(font.fontNames[i]);
                    }
                }
            }
            for (int i = 0; i < primaryFamilies.Length; i++)
            {
                if (!string.IsNullOrEmpty(primaryFamilies[i]) && !merged.Contains(primaryFamilies[i]))
                {
                    merged.Add(primaryFamilies[i]);
                }
            }
            for (int i = 0; i < FallbackFamilies.Length; i++)
            {
                if (!merged.Contains(FallbackFamilies[i]))
                {
                    merged.Add(FallbackFamilies[i]);
                }
            }
            font.fontNames = merged.ToArray();
        }
        return font;
    }
}
