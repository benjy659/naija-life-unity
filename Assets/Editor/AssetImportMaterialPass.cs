// ASSET IMPORT — URP MATERIAL PASS v2 (temporary editor tool, not part of the game).
// Upgrades the flat-colour materials glTFast generated for the imported GLBs,
// by material NAME (the source GLBs carry no textures; names are semantic:
// A4_glass, MK_wood, V_danfo_yellow, ...).
//
// v2: glTFast does NOT generate URP/Lit materials — it generates its own
// "glTF/PbrMetallicRoughness" Shader Graph materials even under URP, with
// glTF property names (baseColorFactor / roughnessFactor / metallicFactor /
// emissiveFactor / alphaCutoff / _Surface / _SrcBlend / _DstBlend / _ZWrite).
// This pass handles BOTH shaders (URP/Lit via _BaseColor/_Smoothness, glTF via
// the names above); any other shader is logged and left alone.
// Entry: menu Naija Life > Asset Import > Run URP Material Pass, or
// -executeMethod AssetImportMaterialPass.RunMaterialPass (exits the Editor).
// NOTE: glTFast regenerates these materials on reimport, so this pass is
// re-runnable by design; AssetImportPostprocessor re-applies it on reimport.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class AssetImportMaterialPass
{
    private const string OutRoot = "/var/cache/apt/archives/unity/asset-import";
    private const string LitShader = "Universal Render Pipeline/Lit";
    private const string GltfShader = "glTF/PbrMetallicRoughness";

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
        EditorApplication.Exit(0);
    }

    /// <summary>Re-applies the pass to one .glb's materials (used by the postprocessor on reimport).</summary>
    public static void ApplyToGlb(string glbPath)
    {
        foreach (UnityEngine.Object o in AssetDatabase.LoadAllAssetsAtPath(glbPath))
        {
            if (o is Material m)
            {
                var dummyReport = new List<string>();
                var dummyCounts = new Dictionary<string, int>();
                var dummyUnhandled = new HashSet<string>();
                int dummyChanged = 0, dummySkipped = 0;
                ApplyRules(m, dummyReport, dummyCounts, dummyUnhandled, ref dummyChanged, ref dummySkipped);
            }
        }
    }

    private static void ApplyRules(Material m, List<string> report, Dictionary<string, int> shaderCounts,
        HashSet<string> unhandled, ref int changed, ref int skipped)
    {
        string shader = m.shader != null ? m.shader.name : "<null>";
        if (!shaderCounts.ContainsKey(shader)) shaderCounts[shader] = 0;
        shaderCounts[shader]++;
        if (shader != LitShader && shader != GltfShader)
        {
            unhandled.Add(shader);
            skipped++;
            return;
        }
        bool gltf = shader == GltfShader;

        // Property names per shader family.
        string colorProp = gltf ? "baseColorFactor" : "_BaseColor";
        string roughProp = gltf ? "roughnessFactor" : "_Smoothness"; // glTF roughness = 1 - smoothness
        string metalProp = gltf ? "metallicFactor" : "_Metallic";
        string emissProp = gltf ? "emissiveFactor" : "_EmissionColor";

        string n = m.name.ToLowerInvariant();
        bool did = false;
        string what = "";

        // ---- Glass / windows: transparent, smooth, barely metallic. ----
        if (n.Contains("glass") || n.Contains("window") || n.Contains("win_") || n.Contains("pane"))
        {
            if (gltf)
            {
                SetFloat(m, "_Surface", 1f);       // Transparent (Shader Graph)
                SetFloat(m, "_SrcBlend", 1f);      // SrcAlpha
                SetFloat(m, "_DstBlend", 10f);     // OneMinusSrcAlpha
                SetFloat(m, "_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else
            {
                SetFloat(m, "_Surface", 1f);
                SetFloat(m, "_Blend", 0f);
                SetFloat(m, "_SrcBlend", 1f);
                SetFloat(m, "_DstBlend", 10f);
                SetFloat(m, "_ZWrite", 0f);
                SetFloat(m, "_AlphaClip", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.DisableKeyword("_SURFACE_TYPE_OPAQUE");
            }
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = 3000;
            if (m.HasProperty(colorProp))
            {
                Color c = m.GetColor(colorProp);
                c.a = 0.35f;
                m.SetColor(colorProp, c);
            }
            SetFloat(m, roughProp, gltf ? 0.1f : 0.9f);
            SetFloat(m, metalProp, 0.05f);
            did = true; what = "glass -> transparent a=0.35 rough=0.1";
        }
        // ---- Signage / destination boards: gentle emission so they read at night. ----
        else if (n.Contains("sign") || n.Contains("board") || n.Contains("decal") || n.Contains("dest") || n.Contains("plate"))
        {
            if (m.HasProperty(emissProp))
            {
                Color baseCol = m.HasProperty(colorProp) ? m.GetColor(colorProp) : Color.white;
                m.SetColor(emissProp, baseCol * 0.6f);
                m.EnableKeyword("_EMISSION");
                did = true; what = "signage -> emission 0.6x base";
            }
        }
        // ---- Headlights / taillights: emissive. ----
        else if (n.Contains("headlight") || n.Contains("taillight") || n.Contains("lightbar"))
        {
            if (m.HasProperty(emissProp))
            {
                m.SetColor(emissProp, n.Contains("tail") ? new Color(0.9f, 0.05f, 0.05f) : new Color(1f, 0.95f, 0.8f));
                m.EnableKeyword("_EMISSION");
                did = true; what = "light -> emission";
            }
        }
        // ---- Rust / grime: very rough, no metal. ----
        else if (n.Contains("rust") || n.Contains("grime") || n.Contains("dust"))
        {
            SetFloat(m, roughProp, gltf ? 0.95f : 0.05f);
            SetFloat(m, metalProp, 0.1f);
            did = true; what = "rust -> rough=0.95 metal=0.1";
        }
        // ---- Metals: gold, chrome, steel, metal. ----
        else if (n.Contains("gold") || n.Contains("chrome") || n.Contains("steel") || n.Contains("metal"))
        {
            SetFloat(m, metalProp, 0.9f);
            SetFloat(m, roughProp, gltf ? 0.4f : 0.6f);
            did = true; what = "metal -> metal=0.9 rough=0.4";
        }
        // ---- Wood: mid roughness. ----
        else if (n.Contains("wood") || n.Contains("hardwood"))
        {
            SetFloat(m, roughProp, 0.5f);
            SetFloat(m, metalProp, 0f);
            did = true; what = "wood -> rough=0.5";
        }
        // ---- Masonry: concrete, marble, wall, plaster: rough, no metal. ----
        else if (n.Contains("concrete") || n.Contains("marble") || n.Contains("wall") || n.Contains("plaster"))
        {
            SetFloat(m, roughProp, gltf ? 0.85f : 0.15f);
            SetFloat(m, metalProp, 0f);
            did = true; what = "masonry -> rough=0.85";
        }
        // ---- Skin: soft sheen. ----
        else if (n.Contains("skin"))
        {
            SetFloat(m, roughProp, gltf ? 0.6f : 0.4f);
            SetFloat(m, metalProp, 0f);
            did = true; what = "skin -> rough=0.6";
        }
        // ---- Tires: dead matte. ----
        else if (n.Contains("tire") || n.Contains("tyre"))
        {
            SetFloat(m, roughProp, gltf ? 0.95f : 0.05f);
            SetFloat(m, metalProp, 0f);
            did = true; what = "tire -> rough=0.95";
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
