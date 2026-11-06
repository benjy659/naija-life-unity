// ASSET IMPORT — URP MATERIAL PASS (temporary editor tool, not part of the game).
// Upgrades the flat-colour materials glTFast generated for the imported GLBs,
// by material NAME (the source GLBs carry no textures; names are semantic:
// A4_glass, MK_wood, V_danfo_yellow, ...). Only mutates materials whose shader
// is Universal Render Pipeline/Lit (what glTFast generates under URP); any
// other shader is logged and left alone, so the pass is safe to re-run.
// Entry: menu Naija Life > Asset Import > Run URP Material Pass, or
// -executeMethod AssetImportMaterialPass.RunMaterialPass (exits the Editor).

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class AssetImportMaterialPass
{
    private const string OutRoot = "/var/cache/apt/archives/unity/asset-import";
    private const string LitShader = "Universal Render Pipeline/Lit";

    [MenuItem("Naija Life/Asset Import/Run URP Material Pass")]
    public static void RunMaterialPass()
    {
        Directory.CreateDirectory(OutRoot);
        var report = new List<string>();
        var shaderCounts = new Dictionary<string, int>();
        var unhandledShaders = new HashSet<string>();
        int examined = 0, changed = 0, skippedShader = 0;

        // Materials embedded in imported .glb files (glTFast sub-assets).
        string[] glbs = Directory.GetFiles("Assets/Imported", "*.glb", SearchOption.AllDirectories);
        foreach (string glb in glbs)
        {
            foreach (UnityEngine.Object o in AssetDatabase.LoadAllAssetsAtPath(glb))
            {
                if (o is Material m)
                {
                    examined++;
                    ApplyRules(m, report, shaderCounts, unhandledShaders, ref changed, ref skippedShader);
                }
            }
        }
        // Standalone .mat files under Assets/Imported, if any.
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Imported" }))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (m == null) continue;
            examined++;
            ApplyRules(m, report, shaderCounts, unhandledShaders, ref changed, ref skippedShader);
        }

        AssetDatabase.SaveAssets();

        var header = new List<string>
        {
            $"MATERIAL PASS {DateTime.UtcNow:o}",
            $"examined={examined} changed={changed} skippedOtherShader={skippedShader}",
            "shader usage:"
        };
        foreach (var kv in shaderCounts) header.Add($"  {kv.Key}: {kv.Value}");
        header.Add("changes:");
        header.AddRange(report);
        File.WriteAllLines(Path.Combine(OutRoot, "material-pass-report.txt"), header);
        Debug.Log($"[AssetImport] material pass done: examined={examined} changed={changed} skippedOtherShader={skippedShader}");
        foreach (string s in unhandledShaders) Debug.LogWarning("[AssetImport] unhandled shader (left alone): " + s);
    }

    private static void ApplyRules(Material m, List<string> report, Dictionary<string, int> shaderCounts,
        HashSet<string> unhandled, ref int changed, ref int skipped)
    {
        string shader = m.shader != null ? m.shader.name : "<null>";
        if (!shaderCounts.ContainsKey(shader)) shaderCounts[shader] = 0;
        shaderCounts[shader]++;
        if (shader != LitShader)
        {
            unhandled.Add(shader);
            skipped++;
            return;
        }

        string n = m.name.ToLowerInvariant();
        bool did = false;
        string what = "";

        // ---- Glass / windows: transparent, smooth, barely metallic. ----
        if (n.Contains("glass") || n.Contains("window") || n.Contains("win_") || n.Contains("pane"))
        {
            SetFloat(m, "_Surface", 1f);          // Transparent
            SetFloat(m, "_Blend", 0f);            // Alpha
            SetFloat(m, "_SrcBlend", 1f);         // SrcAlpha
            SetFloat(m, "_DstBlend", 10f);        // OneMinusSrcAlpha
            SetFloat(m, "_ZWrite", 0f);
            SetFloat(m, "_AlphaClip", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = 3000;
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_SURFACE_TYPE_OPAQUE");
            if (m.HasProperty("_BaseColor"))
            {
                Color c = m.GetColor("_BaseColor");
                c.a = 0.35f;
                m.SetColor("_BaseColor", c);
            }
            SetFloat(m, "_Smoothness", 0.9f);
            SetFloat(m, "_Metallic", 0.05f);
            did = true; what = "glass -> transparent a=0.35 smooth=0.9";
        }
        // ---- Signage / destination boards: gentle emission so they read at night. ----
        else if (n.Contains("sign") || n.Contains("board") || n.Contains("decal") || n.Contains("dest") || n.Contains("plate"))
        {
            if (m.HasProperty("_EmissionColor"))
            {
                Color baseCol = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
                m.SetColor("_EmissionColor", baseCol * 0.6f);
                m.EnableKeyword("_EMISSION");
                did = true; what = "signage -> emission 0.6x base";
            }
        }
        // ---- Headlights / taillights: emissive. ----
        else if (n.Contains("headlight") || n.Contains("taillight") || n.Contains("lightbar"))
        {
            if (m.HasProperty("_EmissionColor"))
            {
                m.SetColor("_EmissionColor", n.Contains("tail") ? new Color(0.9f, 0.05f, 0.05f) : new Color(1f, 0.95f, 0.8f));
                m.EnableKeyword("_EMISSION");
                did = true; what = "light -> emission";
            }
        }
        // ---- Rust / grime: very rough, no metal. ----
        else if (n.Contains("rust") || n.Contains("grime") || n.Contains("dust"))
        {
            SetFloat(m, "_Smoothness", 0.05f);
            SetFloat(m, "_Metallic", 0.1f);
            did = true; what = "rust -> smooth=0.05 metal=0.1";
        }
        // ---- Metals: gold, chrome, steel, metal. ----
        else if (n.Contains("gold") || n.Contains("chrome") || n.Contains("steel") || n.Contains("metal"))
        {
            SetFloat(m, "_Metallic", 0.9f);
            SetFloat(m, "_Smoothness", 0.6f);
            did = true; what = "metal -> metal=0.9 smooth=0.6";
        }
        // ---- Wood: mid roughness. ----
        else if (n.Contains("wood") || n.Contains("hardwood"))
        {
            SetFloat(m, "_Smoothness", 0.5f);
            SetFloat(m, "_Metallic", 0f);
            did = true; what = "wood -> smooth=0.5";
        }
        // ---- Masonry: concrete, marble, wall, plaster: rough, no metal. ----
        else if (n.Contains("concrete") || n.Contains("marble") || n.Contains("wall") || n.Contains("plaster"))
        {
            SetFloat(m, "_Smoothness", 0.15f);
            SetFloat(m, "_Metallic", 0f);
            did = true; what = "masonry -> smooth=0.15";
        }
        // ---- Skin: soft sheen. ----
        else if (n.Contains("skin"))
        {
            SetFloat(m, "_Smoothness", 0.4f);
            SetFloat(m, "_Metallic", 0f);
            did = true; what = "skin -> smooth=0.4";
        }
        // ---- Tires: dead matte. ----
        else if (n.Contains("tire") || n.Contains("tyre"))
        {
            SetFloat(m, "_Smoothness", 0.05f);
            SetFloat(m, "_Metallic", 0f);
            did = true; what = "tire -> smooth=0.05";
        }

        if (did)
        {
            changed++;
            EditorUtility.SetDirty(m);
            report.Add($"  {m.name}: {what}");
        }
    }

    private static void SetFloat(Material m, string prop, float value)
    {
        if (m.HasProperty(prop)) m.SetFloat(prop, value);
    }
}
