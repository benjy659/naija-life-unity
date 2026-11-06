// Naija Streets: radar (minimap) for the HUD (HUD-3). Spec docs/specs/hud-and-phone.md 5.1, 10.
//   - Orthographic top-down camera over the player, 256x256 RenderTexture,
//     refreshed at 10 Hz, no shadows, no post. Shown inside #nl-radar (round).
//   - Rotates with the follow camera's yaw so "up" on the radar is where the
//     camera looks; the player arrow stays in the centre.
//   - Baked map tiles replace this with B-35 streaming later.
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using NaijaLife.Player;

namespace NaijaLife.UI
{
    public sealed class NLRadar : NLUIPart
    {
        [SerializeField] private float height = 80f;
        [Tooltip("Half the visible width in metres.")]
        [SerializeField] private float radiusMetres = 45f;
        [SerializeField] private int textureSize = 256;
        [SerializeField] private float refreshHz = 10f;
        [Tooltip("Layers drawn on the radar. Default: everything. Set to roads and building footprints once M2 layers exist.")]
        [SerializeField] private LayerMask layers = ~0;

        private Camera _cam;
        private RenderTexture _rt;
        private Transform _player;
        private PlayerCamera _follow;
        private float _nextRender;
        private VisualElement _radar;

        protected override void OnBind(VisualElement root)
        {
            _radar = root.Q("nl-radar");
            EnsureCamera();
            _radar.style.backgroundImage = Background.FromRenderTexture(_rt);
        }

        private void EnsureCamera()
        {
            if (_cam != null) return;
            _rt = new RenderTexture(textureSize, textureSize, 16, RenderTextureFormat.ARGB32) { name = "NL Radar RT" };
            var go = new GameObject("NL Radar Camera");
            go.transform.SetParent(transform, false);
            _cam = go.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = radiusMetres;
            _cam.targetTexture = _rt;
            _cam.cullingMask = layers;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color32(0x14, 0x1A, 0x20, 0xFF);
            _cam.nearClipPlane = 1f;
            _cam.farClipPlane = height + 40f;
            _cam.depth = -50;
            var data = _cam.GetUniversalAdditionalCameraData();
            data.renderShadows = false;
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            _cam.enabled = false;
        }

        protected override void Tick()
        {
            if (_cam == null) return;
            if (_player == null)
            {
                var pc = FindFirstObjectByType<PlayerController>();
                if (pc != null) _player = pc.transform;
                _follow = FindFirstObjectByType<PlayerCamera>();
            }
            // render one frame every 1/refreshHz seconds (camera enabled for that frame only)
            if (_cam.enabled) _cam.enabled = false;
            if (_player == null || NLPhone.IsOpen || Time.unscaledTime < _nextRender) return;
            _nextRender = Time.unscaledTime + 1f / Mathf.Max(1f, refreshHz);

            float yaw = _follow != null ? _follow.FollowYawDegrees : _player.eulerAngles.y;
            _cam.transform.SetPositionAndRotation(
                _player.position + Vector3.up * height,
                Quaternion.Euler(90f, yaw, 0f));
            _cam.enabled = true;
        }

        private void OnDestroy()
        {
            if (_rt != null) { _rt.Release(); Destroy(_rt); }
        }
    }
}
