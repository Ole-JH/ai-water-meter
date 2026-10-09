using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Blocky procedural humanoid built from primitives, with simple procedural
    /// walk and attack animation. Used by the player, NPCs and humanoid monsters.
    /// </summary>
    public class HumanoidModel
    {
        public Transform Root, Body, Head, LArm, RArm, LLeg, RLeg, Weapon, Helm;
        public Renderer BodyRenderer, LegRendererL, LegRendererR, ArmRendererL, ArmRendererR, WeaponRenderer, HelmRenderer;
        float walkPhase;

        public static HumanoidModel Build(Transform parent, float scale, Color skin, Color body, Color legs,
            Color weapon, bool hasWeapon = true, bool robe = false, bool glowingEyes = false, Color? eyeColor = null)
        {
            var m = new HumanoidModel();
            m.Root = Factory.Empty("Model", parent, Vector3.zero);
            m.Root.localScale = Vector3.one * scale;

            if (robe)
            {
                var r = Factory.Prim(PrimitiveType.Cube, m.Root, new Vector3(0, 0.5f, 0), new Vector3(0.62f, 1.0f, 0.45f), legs);
                m.LegRendererL = m.LegRendererR = r.GetComponent<Renderer>();
            }
            else
            {
                m.LLeg = Factory.Empty("LLeg", m.Root, new Vector3(-0.15f, 0.9f, 0));
                m.RLeg = Factory.Empty("RLeg", m.Root, new Vector3(0.15f, 0.9f, 0));
                m.LegRendererL = Factory.Prim(PrimitiveType.Cube, m.LLeg, new Vector3(0, -0.45f, 0), new Vector3(0.22f, 0.9f, 0.24f), legs).GetComponent<Renderer>();
                m.LegRendererR = Factory.Prim(PrimitiveType.Cube, m.RLeg, new Vector3(0, -0.45f, 0), new Vector3(0.22f, 0.9f, 0.24f), legs).GetComponent<Renderer>();
            }

            m.Body = Factory.Prim(PrimitiveType.Cube, m.Root, new Vector3(0, 1.3f, 0), new Vector3(0.62f, 0.78f, 0.36f), body).transform;
            m.BodyRenderer = m.Body.GetComponent<Renderer>();
            m.Head = Factory.Prim(PrimitiveType.Sphere, m.Root, new Vector3(0, 1.9f, 0), Vector3.one * 0.42f, skin).transform;

            if (glowingEyes)
            {
                var ec = eyeColor ?? new Color(1f, 0.15f, 0.1f);
                Factory.Prim(PrimitiveType.Sphere, m.Head, new Vector3(-0.2f, 0.08f, 0.42f), Vector3.one * 0.18f, ec, false, Mat.Glow(ec));
                Factory.Prim(PrimitiveType.Sphere, m.Head, new Vector3(0.2f, 0.08f, 0.42f), Vector3.one * 0.18f, ec, false, Mat.Glow(ec));
            }

            m.Helm = Factory.Prim(PrimitiveType.Cube, m.Head, new Vector3(0, 0.25f, 0), new Vector3(1.15f, 0.65f, 1.15f), Color.gray).transform;
            m.HelmRenderer = m.Helm.GetComponent<Renderer>();
            m.Helm.gameObject.SetActive(false);

            m.LArm = Factory.Empty("LArm", m.Root, new Vector3(-0.42f, 1.62f, 0));
            m.RArm = Factory.Empty("RArm", m.Root, new Vector3(0.42f, 1.62f, 0));
            m.ArmRendererL = Factory.Prim(PrimitiveType.Cube, m.LArm, new Vector3(0, -0.33f, 0), new Vector3(0.18f, 0.7f, 0.2f), body).GetComponent<Renderer>();
            m.ArmRendererR = Factory.Prim(PrimitiveType.Cube, m.RArm, new Vector3(0, -0.33f, 0), new Vector3(0.18f, 0.7f, 0.2f), body).GetComponent<Renderer>();
            Factory.Prim(PrimitiveType.Sphere, m.LArm, new Vector3(0, -0.72f, 0), Vector3.one * 0.2f, skin);
            Factory.Prim(PrimitiveType.Sphere, m.RArm, new Vector3(0, -0.72f, 0), Vector3.one * 0.2f, skin);

            if (hasWeapon)
            {
                m.Weapon = Factory.Prim(PrimitiveType.Cube, m.RArm, new Vector3(0, -0.72f, 0.45f), new Vector3(0.08f, 0.1f, 0.95f), weapon).transform;
                m.WeaponRenderer = m.Weapon.GetComponent<Renderer>();
                Factory.Prim(PrimitiveType.Cube, m.RArm, new Vector3(0, -0.72f, 0.05f), new Vector3(0.3f, 0.08f, 0.08f), new Color(0.35f, 0.25f, 0.15f));
            }
            return m;
        }

        /// <param name="speed01">0 = idle, 1 = full run</param>
        /// <param name="attack01">-1 = not attacking, else 0..1 progress through the swing</param>
        public void Animate(float speed01, float attack01, float dt)
        {
            walkPhase += dt * 11f * Mathf.Max(0.15f, speed01);
            float swing = Mathf.Sin(walkPhase) * 35f * speed01;
            if (LLeg != null) LLeg.localRotation = Quaternion.Euler(swing, 0, 0);
            if (RLeg != null) RLeg.localRotation = Quaternion.Euler(-swing, 0, 0);
            LArm.localRotation = Quaternion.Euler(-swing * 0.7f, 0, 0);

            if (attack01 >= 0f)
            {
                float a = attack01 < 0.35f
                    ? Mathf.Lerp(0f, -160f, attack01 / 0.35f)
                    : Mathf.Lerp(-160f, -35f, (attack01 - 0.35f) / 0.65f);
                RArm.localRotation = Quaternion.Euler(a, 0, 0);
            }
            else
            {
                RArm.localRotation = Quaternion.Euler(swing * 0.7f - 10f, 0, 0);
            }

            float bob = Mathf.Abs(Mathf.Sin(walkPhase)) * 0.06f * speed01;
            Body.localPosition = new Vector3(0, 1.3f + bob, 0);
            Head.localPosition = new Vector3(0, 1.9f + bob, 0);
        }

        /// <summary>Casting pose: both arms raised forward.</summary>
        public void CastPose(float t01)
        {
            float a = Mathf.Sin(t01 * Mathf.PI) * -110f;
            LArm.localRotation = Quaternion.Euler(a, 0, 0);
            RArm.localRotation = Quaternion.Euler(a, 0, 0);
        }
    }
}
