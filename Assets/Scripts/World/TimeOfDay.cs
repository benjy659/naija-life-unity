// M2.2 time of day (docs/specs/oshodi-greybox.md section 6.1 to 6.3).
// Drives the sun, ambient light and haze from real WAT (UTC+1) time.
// Fixed sun table for M2: sunrise 06:30, sunset 18:30 WAT. The real table and
// live weather come later. Put this on the Directional Light (the sun).
//
// QA: development builds and the Editor show a small panel (top right) with
// 07:00, 13:00, 17:30, 21:00 and "Live". No keyboard key is used, so it never
// clashes with game controls.

using System;
using UnityEngine;

namespace NaijaLife.World
{
    [RequireComponent(typeof(Light))]
    public class TimeOfDay : MonoBehaviour
    {
        public static TimeOfDay Instance { get; private set; }

        [Tooltip("Hours (0-24). Negative = follow real WAT time.")]
        public float overrideHour = -1f;
        public float sunriseHour = 6.5f;
        public float sunsetHour = 18.5f;

        [Header("Sun")]
        public float noonIntensity = 1.2f;
        public Gradient sunColour = DefaultSunColour();
        public float moonIntensity = 0.12f;
        public Color moonColour = new Color(0.55f, 0.65f, 0.9f);

        [Header("Ambient (trilight)")]
        public Color daySky = new Color(0.62f, 0.66f, 0.72f);
        public Color dayEquator = new Color(0.62f, 0.58f, 0.5f);
        public Color dayGround = new Color(0.36f, 0.3f, 0.24f);
        public Color nightSky = new Color(0.08f, 0.1f, 0.18f);
        public Color nightEquator = new Color(0.07f, 0.07f, 0.11f);
        public Color nightGround = new Color(0.04f, 0.04f, 0.05f);

        [Header("Lagos haze (linear fog)")]
        public float fogStart = 80f;
        public float fogEnd = 380f;
        public Color dayFog = new Color(0.8f, 0.77f, 0.7f);
        public Color nightFog = new Color(0.06f, 0.08f, 0.16f);

        [Tooltip("Show the QA time panel in development builds and the Editor.")]
        public bool showQaPanel = true;

        /// <summary>0 in full day, 1 in full night, blends over dusk and dawn.</summary>
        public float Night01 { get; private set; }
        /// <summary>Current hour 0-24 (WAT or override).</summary>
        public float Hour { get; private set; }

        Light _sun;

        // The first active TimeOfDay wins. A streamed district carries its own
        // playtest sun; Bootstrap's stays in charge and the streamer hides the other.
        void OnEnable()
        {
            if (Instance == null) Instance = this;
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
        }

        void Awake()
        {
            _sun = GetComponent<Light>();
            _sun.type = LightType.Directional;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        }

        /// <summary>Apply the current hour now. Also works in edit mode (QA capture runner),
        /// where Awake and Update do not run.</summary>
        public void ApplyNow()
        {
            if (_sun == null) Awake();
            Hour = overrideHour >= 0f ? Mathf.Repeat(overrideHour, 24f) : WatHourNow();
            Apply(Hour);
        }

        public static float WatHourNow()
        {
            var wat = DateTime.UtcNow.AddHours(1);
            return wat.Hour + wat.Minute / 60f + wat.Second / 3600f;
        }

        void Update()
        {
            ApplyNow();
        }

        void Apply(float h)
        {
            float dayLen = sunsetHour - sunriseHour;
            float t = (h - sunriseHour) / dayLen;                 // 0 at sunrise, 1 at sunset
            float elevation = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * 78f;
            // dusk/dawn blend: night when the sun is below about 4 degrees
            float dayAmount = (t > 0f && t < 1f) ? Mathf.Clamp01(elevation / 10f) : 0f;
            Night01 = 1f - dayAmount;

            if (dayAmount > 0f)
            {
                // morning: light points west (yaw 270), noon: north (360), evening: east (450)
                float yaw = Mathf.Lerp(270f, 450f, Mathf.Clamp01(t));
                transform.rotation = Quaternion.Euler(Mathf.Max(elevation, 2f), yaw, 0f);
                _sun.color = sunColour.Evaluate(Mathf.Clamp01(t));
                _sun.intensity = Mathf.Lerp(moonIntensity, noonIntensity, dayAmount);
                _sun.shadows = LightShadows.Soft;
            }
            else
            {
                transform.rotation = Quaternion.Euler(50f, 200f, 0f);   // moonlight, fixed
                _sun.color = moonColour;
                _sun.intensity = moonIntensity;
                _sun.shadows = LightShadows.None;
            }

            RenderSettings.ambientSkyColor = Color.Lerp(daySky, nightSky, Night01);
            RenderSettings.ambientEquatorColor = Color.Lerp(dayEquator, nightEquator, Night01);
            RenderSettings.ambientGroundColor = Color.Lerp(dayGround, nightGround, Night01);
            RenderSettings.fogColor = Color.Lerp(dayFog, nightFog, Night01);
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
        }

        static Gradient DefaultSunColour()
        {
            var g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.62f, 0.38f), 0f),
                    new GradientColorKey(new Color(1f, 0.93f, 0.82f), 0.2f),
                    new GradientColorKey(new Color(1f, 0.97f, 0.92f), 0.5f),
                    new GradientColorKey(new Color(1f, 0.9f, 0.75f), 0.8f),
                    new GradientColorKey(new Color(1f, 0.55f, 0.32f), 1f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        void OnGUI()
        {
            if (!showQaPanel || !(Debug.isDebugBuild || Application.isEditor)) return;
            const float w = 60f, hgt = 26f;
            float x = Screen.width - (w + 4f) * 5f - 4f, y = 4f;
            string[] labels = { "07:00", "13:00", "17:30", "21:00", "Live" };
            float[] hours = { 7f, 13f, 17.5f, 21f, -1f };
            for (int i = 0; i < labels.Length; i++)
                if (GUI.Button(new Rect(x + i * (w + 4f), y, w, hgt), labels[i])) overrideHour = hours[i];
        }
    }
}
