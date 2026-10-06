using UnityEngine;

namespace Shadowfall
{
    /// <summary>Diablo-style high-angle follow camera with scroll-wheel zoom and screen shake.</summary>
    public class CameraRig : MonoBehaviour
    {
        public static CameraRig I;
        public Transform Target;
        public float Distance = 20f, MinDistance = 10f, MaxDistance = 32f, Pitch = 55f;
        Vector3 focus;
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

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (Target != null)
            {
                focus = Vector3.Lerp(focus, Target.position, 1f - Mathf.Exp(-dt * 10f));
                bool overUI = GameUI.I != null && (GameUI.I.MouseOverUI || GameUI.I.ChatOpen);
                float scroll = GameInput.Scroll;
                if (!overUI && Mathf.Abs(scroll) > 0.01f)
                    Distance = Mathf.Clamp(Distance - Mathf.Sign(scroll) * 2f, MinDistance, MaxDistance);
            }
            else
            {
                // Slow orbit over the village while on the login screen.
                focus = new Vector3(80f + Mathf.Sin(Time.time * 0.1f) * 6f, 0f, 78f + Mathf.Cos(Time.time * 0.1f) * 6f);
            }

            var rot = Quaternion.Euler(Pitch, 0f, 0f);
            var pos = focus + Vector3.up * 1f + rot * new Vector3(0f, 0f, -Distance);
            if (shake > 0f)
            {
                pos += Random.insideUnitSphere * shake * 0.6f;
                shake = Mathf.Max(0f, shake - dt * 1.5f);
            }
            transform.SetPositionAndRotation(pos, rot);
        }
    }
}
