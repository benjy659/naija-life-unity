// Naija Streets (Unity rebuild) — HUDFontBuilder (Editor only, NOT part of the game).
// Builds TextCore SDF font assets from the Inter TTFs so UI Toolkit can use
// them via -unity-font-definition. The character set covers printable ASCII
// plus the naira sign ₦ (U+20A6) and the punctuation the HUD copy uses.
// Entry: -executeMethod HUDFontBuilder.BuildAll (or the Naija Streets menu).
// Output: Assets/UI/Fonts/Inter-{Medium,Bold,ExtraBold} SDF.asset.

using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

public static class HUDFontBuilder
{
    private const string FontsDir = "Assets/UI/Fonts";

    // Printable ASCII 32-126 + naira + the punctuation/arrows the copy uses.
    private const string ExtraChars = "₦–—‘’“”•…←↑↓✓★éèêàáâäöüß";

    private static string CharacterSet()
    {
        var sb = new System.Text.StringBuilder();
        for (int c = 32; c <= 126; c++) sb.Append((char)c);
        sb.Append(ExtraChars);
        return sb.ToString();
    }

    [MenuItem("Naija Streets/Build HUD Fonts")]
    public static void BuildAll()
    {
        string[] weights = { "Medium", "Bold", "ExtraBold" };
        foreach (string weight in weights)
        {
            string ttfPath = $"{FontsDir}/Inter-{weight}.ttf";
            string outPath = $"{FontsDir}/Inter-{weight} SDF.asset";
            var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (font == null)
            {
                Debug.LogError($"[HUDFontBuilder] Missing TTF: {ttfPath}");
                continue;
            }
            if (File.Exists(outPath))
            {
                Debug.Log($"[HUDFontBuilder] {outPath} already exists — rebuilding.");
                AssetDatabase.DeleteAsset(outPath);
            }
            // TMP 5/6 (Unity 6.3, com.unity.ugui): FontAssetCreator is gone.
            // TMP_FontAsset.CreateFontAsset is the creator now; AtlasPopulationMode
            // has no Custom — Static + TryAddCharacters pre-populates the atlas.
            var fontAsset = TMP_FontAsset.CreateFontAsset(
                font, 90, 5, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Static, false);
            if (fontAsset == null)
            {
                Debug.LogError($"[HUDFontBuilder] CreateFontAsset failed for Inter-{weight}");
                continue;
            }
            fontAsset.TryAddCharacters(CharacterSet());
            AssetDatabase.CreateAsset(fontAsset, outPath);
            Debug.Log($"[HUDFontBuilder] Created {outPath} (glyphs: {fontAsset.characterTable.Count})");
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[HUDFontBuilder] Done.");
    }
}
