// ASSET IMPORT — PLAYER PREFAB WIRING (temporary editor tool, not part of the game).
// Replaces the grey-capsule placeholder children of Assets/Prefabs/Player/Player.prefab
// with the imported v2 body_m model (59-bone skinned rig). The placeholder Body/Head
// GameObjects are DEACTIVATED, not deleted, so the swap is trivially reversible.
// Also assigns the model's generated Avatar to the prefab's Animator when one exists
// (without clips the rig still renders in bind pose — animation is separate work).
// Entry: menu Naija Life > Asset Import > Wire body_m into Player prefab, or
// -executeMethod AssetImportPlayerWire.WirePlayer (exits the Editor).

using UnityEditor;
using UnityEngine;

public static class AssetImportPlayerWire
{
    private const string PrefabPath = "Assets/Prefabs/Player/Player.prefab";
    private const string BodyMPath = "Assets/Imported/Characters/Player/body_m.glb";

    [MenuItem("Naija Life/Asset Import/Wire body_m into Player prefab")]
    public static void WirePlayer()
    {
        var bodyM = AssetDatabase.LoadAssetAtPath<GameObject>(BodyMPath);
        if (bodyM == null)
        {
            Debug.LogError("[AssetImport] body_m model asset not found at " + BodyMPath + " — run the first import first.");
            return;
        }

        // Find the model's generated Avatar, if glTFast created one.
        Avatar modelAvatar = null;
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(BodyMPath))
        {
            if (o is Avatar a) { modelAvatar = a; break; }
        }
        Debug.Log("[AssetImport] body_m model avatar: " + (modelAvatar != null ? modelAvatar.name : "NONE (rig renders in bind pose; animation work still needed)"));

        GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            foreach (string name in new[] { "Body", "Head" })
            {
                Transform t = contents.transform.Find(name);
                if (t != null)
                {
                    t.gameObject.SetActive(false);
                    Debug.Log("[AssetImport] deactivated placeholder '" + name + "'");
                }
                else Debug.LogWarning("[AssetImport] placeholder child '" + name + "' not found in Player prefab");
            }

            Transform old = contents.transform.Find("BodyM");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            GameObject inst;
            try
            {
                inst = (GameObject)PrefabUtility.InstantiatePrefab(bodyM, contents.transform);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[AssetImport] InstantiatePrefab failed, falling back to plain Instantiate: " + e.Message);
                inst = Object.Instantiate(bodyM, contents.transform);
            }
            inst.name = "BodyM";
            inst.transform.localPosition = Vector3.zero;
            // Source convention: Blender forward +Y -> glTF -Z; Unity forward is +Z,
            // so rotate 180 deg so the character faces the prefab's forward.
            inst.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            inst.transform.localScale = Vector3.one;

            var animator = contents.GetComponent<Animator>();
            if (animator != null && modelAvatar != null)
            {
                animator.avatar = modelAvatar;
                Debug.Log("[AssetImport] Animator.avatar set to " + modelAvatar.name);
            }
            else if (animator != null)
            {
                Debug.LogWarning("[AssetImport] Animator.avatar left unset (no Avatar sub-asset on body_m)");
            }

            Renderer[] renderers = inst.GetComponentsInChildren<Renderer>(true);
            Bounds b = new Bounds();
            bool first = true;
            int skinned = 0;
            foreach (Renderer r in renderers)
            {
                if (r is SkinnedMeshRenderer) skinned++;
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            }
            // Bounds above are world-space of the edit-time contents; report local size instead.
            Debug.Log($"[AssetImport] BodyM wired: renderers={renderers.Length} skinned={skinned} worldBoundsSize={b.size} center={b.center}");

            PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            Debug.Log("[AssetImport] Player prefab saved with BodyM.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }
}
