// Starts the M3 self-check (Assets/Scripts/Verification/M3VerifyHarness.cs).
// Menu:  Naija Streets > M3 > Run self-check
// Batch: Unity -projectPath . -executeMethod NaijaLife.Vehicles.EditorTools.M3VerifyRunner.RunBatch -logFile -
// Builds Greybox_M3 first if it is missing. The harness object is added to the
// open scene in memory only (the scene file is not changed).

using NaijaLife.Verification;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NaijaLife.Vehicles.EditorTools
{
    [InitializeOnLoad]
    public static class M3VerifyRunner
    {
        const string RunningKey = "NaijaLife.M3Verify.Running";
        const string BatchKey = "NaijaLife.M3Verify.Batch";

        static M3VerifyRunner()
        {
            // Survives the domain reload on entering play mode.
            if (SessionState.GetBool(RunningKey, false))
                M3VerifyHarness.Finished += OnFinished;
        }

        [MenuItem("Naija Streets/M3/Run self-check")]
        public static void RunFromMenu() { Start(false); }

        public static void RunBatch() { Start(true); }

        static void Start(bool batch)
        {
            if (!System.IO.File.Exists(M3SceneBuilder.ScenePath)) Debug.Log("[M3Verify] " + M3SceneBuilder.Build());
            EditorSceneManager.OpenScene(M3SceneBuilder.ScenePath, OpenSceneMode.Single);
            new GameObject("M3VerifyHarness").AddComponent<M3VerifyHarness>();
            SessionState.SetBool(RunningKey, true);
            SessionState.SetBool(BatchKey, batch);
            M3VerifyHarness.Finished += OnFinished;
            EditorApplication.isPlaying = true;
        }

        static void OnFinished(int code)
        {
            M3VerifyHarness.Finished -= OnFinished;
            bool batch = SessionState.GetBool(BatchKey, false);
            SessionState.SetBool(RunningKey, false);
            EditorApplication.delayCall += () =>
            {
                EditorApplication.isPlaying = false;
                if (batch) EditorApplication.Exit(code);
                else Debug.Log("[M3Verify] finished with code " + code + ". Report under qa/m3-selfcheck/.");
            };
        }
    }
}
