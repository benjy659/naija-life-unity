// M2.3 district streamer (docs/specs/oshodi-greybox.md section 7).
// Lives in Bootstrap.unity. On start it loads the always-on scenes (in M2
// just Approach_South, from Build Settings) and only then switches the player
// on, so the player never falls through missing ground. Then every 0.5 s it
// measures the player's flat distance to each DistrictTile:
//   inside wakeDistance  -> Addressables.LoadSceneAsync(address, Additive)
//   outside sleepDistance -> Addressables.UnloadSceneAsync
// One load or unload at a time. Each one opens a FrameSpikeLogger window.
// A streamed-in district keeps its own playtest root (sun, camera, player) for
// solo QA; the streamer switches that root off so there is only one player.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace NaijaLife.World
{
    public class DistrictStreamer : MonoBehaviour
    {
        public enum TileState { Asleep, Loading, Awake, Unloading, Failed }

        [Tooltip("The player. Kept inactive until the always-on scenes are in.")]
        public Transform player;
        public DistrictTile[] tiles;
        [Tooltip("Scenes in Build Settings that stay loaded the whole time (M2: Approach_South).")]
        public string[] alwaysLoadedScenes = { "Approach_South" };
        [Tooltip("Root object name inside streamed scenes that only exists for solo playtests.")]
        public string playtestRootName = "Oshodi_Playtest";
        public float checkInterval = 0.5f;

        class Slot
        {
            public DistrictTile tile;
            public TileState state;
            public AsyncOperationHandle<SceneInstance> handle;
            public float lastLoadSeconds;
        }

        readonly List<Slot> _slots = new List<Slot>();
        bool _busy;
        bool _ready;

        IEnumerator Start()
        {
            if (player != null) player.gameObject.SetActive(false);

            foreach (var name in alwaysLoadedScenes)
            {
                if (string.IsNullOrEmpty(name) || SceneManager.GetSceneByName(name).isLoaded) continue;
                var op = SceneManager.LoadSceneAsync(name, LoadSceneMode.Additive);
                if (op == null) { Debug.LogError("[Streamer] " + name + " is not in Build Settings."); continue; }
                while (!op.isDone) yield return null;
            }

            if (tiles != null)
                foreach (var t in tiles)
                    if (t != null) _slots.Add(new Slot { tile = t, state = TileState.Asleep });

            if (player != null) player.gameObject.SetActive(true);
            _ready = true;

            var wait = new WaitForSecondsRealtime(checkInterval);
            while (true)
            {
                Check();
                yield return wait;
            }
        }

        void Check()
        {
            if (!_ready || player == null) return;
            var pos = player.position;
            foreach (var s in _slots)
            {
                if (_busy) break;
                float d = s.tile.DistanceTo(pos);
                if (s.state == TileState.Asleep && d <= s.tile.wakeDistance) StartCoroutine(Load(s));
                else if (s.state == TileState.Awake && d > s.tile.sleepDistance) StartCoroutine(Unload(s));
            }
            PublishState(pos);
        }

        IEnumerator Load(Slot s)
        {
            _busy = true;
            s.state = TileState.Loading;
            if (FrameSpikeLogger.Instance != null) FrameSpikeLogger.Instance.BeginWindow("load " + s.tile.id);
            float t0 = Time.realtimeSinceStartup;
            s.handle = Addressables.LoadSceneAsync(s.tile.sceneAddress, LoadSceneMode.Additive);
            yield return s.handle;
            s.lastLoadSeconds = Time.realtimeSinceStartup - t0;
            if (s.handle.Status == AsyncOperationStatus.Succeeded)
            {
                s.state = TileState.Awake;
                HidePlaytestRoot(s.handle.Result.Scene);
                Debug.Log($"[Streamer] {s.tile.id} in after {s.lastLoadSeconds:F2} s");
            }
            else
            {
                s.state = TileState.Failed;
                Debug.LogError($"[Streamer] {s.tile.id} failed to load from address '{s.tile.sceneAddress}': {s.handle.OperationException}");
            }
            _busy = false;
        }

        IEnumerator Unload(Slot s)
        {
            _busy = true;
            s.state = TileState.Unloading;
            if (FrameSpikeLogger.Instance != null) FrameSpikeLogger.Instance.BeginWindow("unload " + s.tile.id);
            var op = Addressables.UnloadSceneAsync(s.handle);
            yield return op;
            s.state = TileState.Asleep;
            Debug.Log($"[Streamer] {s.tile.id} out");
            _busy = false;
        }

        void HidePlaytestRoot(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == playtestRootName) root.SetActive(false);
        }

        void PublishState(Vector3 pos)
        {
            if (FrameSpikeLogger.Instance == null) return;
            var text = "";
            foreach (var s in _slots)
                text += $"{s.tile.id}: {s.state}, {s.tile.DistanceTo(pos):F0} m (wake {s.tile.wakeDistance:F0} / sleep {s.tile.sleepDistance:F0}), last load {s.lastLoadSeconds:F2} s\n";
            FrameSpikeLogger.Instance.StreamerState = text.TrimEnd();
        }

        // For tests and the debug overlay.
        public TileState StateOf(string tileId)
        {
            foreach (var s in _slots) if (s.tile.id == tileId) return s.state;
            return TileState.Asleep;
        }
    }
}
