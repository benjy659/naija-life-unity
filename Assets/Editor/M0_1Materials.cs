// TEMP setup script (M0.1 materials): give the greybox + Player real URP Lit
// materials. The scene + Player prefab use Unity's built-in Default-Material,
// which renders MAGENTA under URP (M0.1 acceptance: "no pink materials").
// Creates Assets/Settings/Mat_*.mat (URP Lit) via the Editor API, assigns
// them to the scene renderers + the Player prefab Body/Head, saves both.
// Run once via -executeMethod M0_1Materials.Run. L-1: Editor APIs only.
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class M0_1Materials
{
    private const string LogPath = "/var/cache/apt/archives/unity/m1-verify/m01-materials.txt";

    private static Material MakeMat(string path, Color c)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new System.Exception("URP Lit shader not found");
        var mat = new Material(shader);
        mat.color = c;
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    public static void Run()
    {
        int exitCode = 0;
        using (var w = new StreamWriter(LogPath, false))
        {
            void L(string s) { Debug.Log("[M0_1Materials] " + s); w.WriteLine(s); w.Flush(); }
            try
            {
                var ground = MakeMat("Assets/Settings/Mat_Ground.mat", new Color(0.75f, 0.75f, 0.75f));
                var prop = MakeMat("Assets/Settings/Mat_Prop.mat", new Color(0.50f, 0.50f, 0.50f));
                var body = MakeMat("Assets/Settings/Mat_PlayerBody.mat", new Color(1.00f, 0.50f, 0.10f));
                var head = MakeMat("Assets/Settings/Mat_PlayerHead.mat", new Color(0.40f, 0.25f, 0.15f));
                L("Created 4 URP Lit materials in Assets/Settings/");

                // Scene: assign by GameObject name.
                EditorSceneManager.OpenScene("Assets/Scenes/Greybox_M1.unity", OpenSceneMode.Single);
                int assigned = 0;
                foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                {
                    var m = r.gameObject.name == "Ground" ? ground : prop;
                    r.sharedMaterial = m;
                    L($"Scene {r.gameObject.name}: {m.name}");
                    assigned++;
                }
                EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                L($"Scene saved ({assigned} renderers assigned)");

                // Player prefab: Body + Head.
                string prefabPath = "Assets/Prefabs/Player/Player.prefab";
                var contents = PrefabUtility.LoadPrefabContents(prefabPath);
                int pAssigned = 0;
                foreach (var r in contents.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var m = r.gameObject.name == "Body" ? body : head;
                    r.sharedMaterial = m;
                    L($"Prefab {r.gameObject.name}: {m.name}");
                    pAssigned++;
                }
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                PrefabUtility.UnloadPrefabContents(contents);
                L($"Prefab saved ({pAssigned} renderers assigned)");

                AssetDatabase.SaveAssets();
                L(exitCode == 0 ? "M0_1Materials DONE" : "M0_1Materials DONE WITH FAILURES");
            }
            catch (System.Exception e)
            {
                L("EXCEPTION: " + e);
                exitCode = 1;
            }
        }
        EditorApplication.Exit(exitCode);
    }
}
