// ASSET IMPORT — REIMPORT POSTPROCESSOR (editor tool, not part of the game).
// glTFast regenerates the imported materials on every reimport, which would
// wipe the URP material pass. This postprocessor re-applies the pass to any
// .glb under Assets/Imported whenever it is (re)imported, so the upgrades
// survive. Defensive by design: one bad file must never break the import.

using UnityEditor;

public class AssetImportPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
        string[] movedAssets, string[] movedFromAssetPaths)
    {
        foreach (string path in importedAssets)
        {
            if (!path.StartsWith("Assets/Imported/", System.StringComparison.Ordinal)) continue;
            if (!path.EndsWith(".glb", System.StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                AssetImportMaterialPass.ApplyToGlb(path);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning($"[AssetImport] postprocessor skipped {path}: {e.Message}");
            }
        }
    }
}
