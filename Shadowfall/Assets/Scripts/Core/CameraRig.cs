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
            focus = new Vector3(80f, 0f, 80f);
        }

        public static void Shake(float amount)
        {
            if (I != null) I.shake = Mathf.Max(I.shake, amount);
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
                HandleInput(dt);
                focus = Vector3.Lerp(focus, Target.position, 1f - Mathf.Exp(-dt * 10f));
            }
            else
            {
                // Slow orbit over the village while on the login screen.
                dragging = false;
                yawGoal = yaw = Time.time * 4f;
                pitchGoal = pitch = 48f;
                distanceGoal = distance = 24f;
                focus = new Vector3(80f, 0f, 80f);
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
            QualitySettings.shadowDistance = 30f + distance * (2.6f - pitch / 60f);
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
