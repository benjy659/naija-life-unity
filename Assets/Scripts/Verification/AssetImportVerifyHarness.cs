// ASSET IMPORT — VERIFICATION HARNESS (temporary, NOT part of the game).
// Play-mode component: waits for the scene to settle, screenshots three camera
// angles, writes done.txt. Driven by AssetImportVerify (Assets/Editor).

using System.Collections;
using System.IO;
using UnityEngine;

public class AssetImportVerifyHarness : MonoBehaviour
{
    public string outDir;
    public Camera shotCam;
    public Vector3[] camPositions;
    public Vector3[] camTargets;

    private IEnumerator Start()
    {
        Directory.CreateDirectory(Path.Combine(outDir, "shots"));
        yield return new WaitForSeconds(1.5f);
        for (int i = 0; i < camPositions.Length; i++)
        {
            shotCam.transform.position = camPositions[i];
            shotCam.transform.LookAt(camTargets[i]);
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(outDir, "shots", $"0{i + 1}-asset-import.png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[AssetImport] screenshot: " + path);
            yield return new WaitForEndOfFrame();
        }
        File.WriteAllText(Path.Combine(outDir, "done.txt"), System.DateTime.UtcNow.ToString("o"));
        Debug.Log("[AssetImport] verify done.");
    }
}
