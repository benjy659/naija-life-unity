// Naija Streets (Unity rebuild) — NLRadar
// Minimap. Spec section 5.1 and 10. An orthographic top-down camera
// rendering roads and building footprints only into a 256x256
// RenderTexture at 10 Hz, no shadows, no post. The map rotates with the
// camera yaw; the player arrow stays in the centre and points along the
// player's facing relative to the camera. (Baked map tiles replace it with
// B-35 streaming.)

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NaijaLife.UI
{
    public class NLRadar : MonoBehaviour
    {
        [Tooltip("Orthographic top-down camera that renders the map.")]
        [SerializeField] private Camera radarCamera;

        [Tooltip("Height above the player.")]
        [SerializeField] private float height = 60f;

        [Tooltip("Half-width of the map area shown, in metres.")]
        [SerializeField] private float halfExtent = 40f;

        [Tooltip("RenderTexture the radar camera draws into (256x256).")]
        [SerializeField] private RenderTexture renderTexture;

        private Transform _player;
        private Func<float> _cameraYaw;
        private Func<Vector3> _playerFacing;
        private VisualElement _mapEl;
        private VisualElement _arrowEl;
        private float _nextRenderTime;

        public RenderTexture Texture => renderTexture;

        public void Setup(Transform player, Func<float> cameraYaw, Func<Vector3> playerFacing,
            VisualElement mapElement, VisualElement arrowElement)
        {
            _player = player;
            _cameraYaw = cameraYaw;
            _playerFacing = playerFacing;
            _mapEl = mapElement;
            _arrowEl = arrowElement;
            if (renderTexture == null)
            {
                renderTexture = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32);
                renderTexture.name = "NLRadarRT";
            }
            if (radarCamera != null)
            {
                radarCamera.targetTexture = renderTexture;
                radarCamera.orthographic = true;
                radarCamera.orthographicSize = halfExtent;
                radarCamera.nearClipPlane = 0.1f;
                radarCamera.farClipPlane = height + 50f;
                radarCamera.clearFlags = CameraClearFlags.SolidColor;
                radarCamera.backgroundColor = new Color(0.05f, 0.07f, 0.10f, 1f);
                radarCamera.enabled = false; // manual render at 10 Hz
            }
            if (_mapEl != null)
                _mapEl.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(renderTexture));
        }

        private void LateUpdate()
        {
            if (_player == null || radarCamera == null) return;
            Vector3 p = _player.position;
            radarCamera.transform.position = new Vector3(p.x, p.y + height, p.z);
            radarCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            float yaw = _cameraYaw != null ? _cameraYaw() : 0f;
            if (_mapEl != null)
                _mapEl.style.rotate = new StyleRotate(new Rotate(-yaw));
            if (_arrowEl != null)
            {
                Vector3 facing = _playerFacing != null ? _playerFacing() : Vector3.forward;
                float facingYaw = facing.sqrMagnitude > 0.001f
                    ? Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg : 0f;
                _arrowEl.style.rotate = new StyleRotate(new Rotate(facingYaw - yaw));
            }

            if (Time.time >= _nextRenderTime)
            {
                _nextRenderTime = Time.time + 0.1f;
                radarCamera.Render();
            }
        }
    }
}
