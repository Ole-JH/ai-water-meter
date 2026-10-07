using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Diablo-style high-angle camera, always locked on the hero. Mouse wheel zooms, middle mouse drag
    /// (or the arrow keys) rotates and tilts within a limited range, and Space snaps back to the default view.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public static CameraRig I;
        public Transform Target;

        public const float DefaultPitch = 55f, DefaultYaw = 0f, DefaultDistance = 20f;
        public float MinDistance = 9f, MaxDistance = 32f, MinPitch = 38f, MaxPitch = 75f;

        public float Yaw => yaw;

        float yaw = DefaultYaw, pitch = DefaultPitch, distance = DefaultDistance;            // smoothed
        float yawGoal = DefaultYaw, pitchGoal = DefaultPitch, distanceGoal = DefaultDistance; // input targets
        Vector3 focus;
        Vector2 lastMouse;
        bool dragging;
        float shake;

        void Awake()
        {
            I = this;
            focus = new Vector3(WorldGenerator.Center, 0f, WorldGenerator.Center);
        }

        public static void Shake(float amount)
        {
            if (I != null) I.shake = Mathf.Max(I.shake, amount);
        }

        /// <summary>Jump straight to the hero (after a teleport) instead of gliding there.</summary>
        bool hadTarget;

        public void SnapToTarget()
        {
            if (Target != null) focus = Target.position;
        }

        /// <summary>Back to the classic view centered on the hero.</summary>
        public void ResetView()
        {
            yawGoal = Mathf.Round(yaw / 360f) * 360f + DefaultYaw;
            pitchGoal = DefaultPitch;
            distanceGoal = DefaultDistance;
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (Target != null)
            {
                // Coming from the login screen's low cinematic shot (or a free orbit): start from the normal
                // gameplay view instead of keeping that angle.
                if (!hadTarget)
                {
                    hadTarget = true;
                    ResetView();
                    yaw = yawGoal;
                    pitch = pitchGoal;
                    distance = distanceGoal;
                    focus = Target.position;
                }
                HandleInput(dt);
                focus = Vector3.Lerp(focus, Target.position, 1f - Mathf.Exp(-dt * 10f));
            }
            else if (LoginShowcase.Focus.HasValue)
            {
                hadTarget = false;
                // Login screen: a slow, low cinematic shot of the hero preview, framed off-centre so the
                // login panel doesn't cover it.
                dragging = false;
                yawGoal = yaw = Mathf.Sin(Time.time * 0.12f) * 10f;
                pitchGoal = pitch = 16f;
                distanceGoal = distance = 6.8f;
                var cam = GetComponent<Camera>();
                float halfWidth = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * distance * cam.aspect;
                var right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
                focus = LoginShowcase.Focus.Value + Vector3.up * 0.15f - right * (LoginShowcase.ScreenOffset * halfWidth);
                transform.SetPositionAndRotation(focus + Vector3.up * 1f + Quaternion.Euler(pitch, yaw, 0f) * new Vector3(0f, 0f, -distance),
                    Quaternion.Euler(pitch, yaw, 0f));
                return;
            }
            else
            {
                // Slow orbit over the village (before the preview exists).
                hadTarget = false;
                dragging = false;
                yawGoal = yaw = Time.time * 4f;
                pitchGoal = pitch = 48f;
                distanceGoal = distance = 24f;
                focus = new Vector3(WorldGenerator.Center, 0f, WorldGenerator.Center);
            }

            float k = 1f - Mathf.Exp(-dt * 12f);
            yaw = Mathf.Lerp(yaw, yawGoal, k);
            pitch = Mathf.Lerp(pitch, pitchGoal, k);
            distance = Mathf.Lerp(distance, distanceGoal, k);
            var center = focus;

            var rot = Quaternion.Euler(pitch, yaw, 0f);
            var pos = center + Vector3.up * 1f + rot * new Vector3(0f, 0f, -distance);
            if (shake > 0f)
            {
                pos += Random.insideUnitSphere * shake * 0.6f;
                shake = Mathf.Max(0f, shake - dt * 1.5f);
            }
            transform.SetPositionAndRotation(pos, rot);

            // Keep shadows sharp when zoomed in, but still covering the view when zoomed out or tilted.
            QualitySettings.shadowDistance = (30f + distance * (2.6f - pitch / 60f)) * GameSettings.ShadowDistanceScale;
        }

        void HandleInput(float dt)
        {
            var ui = GameUI.I;
            bool overUI = ui != null && ui.MouseOverUI;
            bool typing = ui != null && ui.KeyboardCaptured;

            // Zoom
            float scroll = GameInput.Scroll;
            if (!overUI && Mathf.Abs(scroll) > 0.01f)
                distanceGoal = Mathf.Clamp(distanceGoal - Mathf.Sign(scroll) * distanceGoal * 0.12f, MinDistance, MaxDistance);

            // Middle mouse drag: rotate (side to side) and tilt (up and down)
            var mouse = GameInput.MousePosition;
            if (GameInput.MiddleHeld && (dragging || !overUI))
            {
                if (dragging)
                {
                    var d = mouse - lastMouse;
                    yawGoal += d.x * 0.25f;
                    pitchGoal = Mathf.Clamp(pitchGoal - d.y * 0.2f, MinPitch, MaxPitch);
                }
                dragging = true;
            }
            else dragging = false;
            lastMouse = mouse;

            if (typing) return;

            // Arrow keys: rotate and tilt
            if (GameInput.Held(GKey.Left)) yawGoal += 90f * dt;
            if (GameInput.Held(GKey.Right)) yawGoal -= 90f * dt;
            if (GameInput.Held(GKey.Up)) pitchGoal = Mathf.Clamp(pitchGoal + 45f * dt, MinPitch, MaxPitch);
            if (GameInput.Held(GKey.Down)) pitchGoal = Mathf.Clamp(pitchGoal - 45f * dt, MinPitch, MaxPitch);

            if (GameInput.Down(GKey.Space)) ResetView();
        }
    }
}
