using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// A mount from Beastmaster Orla. Bought once (the server keeps it in the ledger's companion list as "mount:&lt;id&gt;";
    /// prices are extracted into server/gamedata.json like the companions'), then called with V anywhere outside dungeons.
    /// </summary>
    public class MountDef
    {
        public string Id, Name, Description, Model;
        public int Price, RequiredLevel;
        public float Speed;           // movement speed multiplier while riding
        public float Height = 2f;     // model height
        public float Saddle = 1.05f;  // how high the rider sits
        public Color? Tint;

        public static readonly MountDef[] All =
        {
            new MountDef
            {
                Id = "horse", Name = "Riding Horse", Model = "Mounts/Horse", Price = 1500, RequiredLevel = 8, Speed = 1.6f,
                Description = "A steady bay that knows the roads. 60% faster than walking.",
            },
            new MountDef
            {
                Id = "warhorse", Name = "White Charger", Model = "Mounts/HorseWhite", Price = 6000, RequiredLevel = 15, Speed = 1.75f, Height = 2.1f, Saddle = 1.12f,
                Description = "A knight's charger, bred for the long ride. 75% faster than walking.",
            },
            new MountDef
            {
                Id = "stag", Name = "Frostpeak Stag", Model = "Mounts/Stag", Price = 15000, RequiredLevel = 20, Speed = 1.9f, Height = 2.3f, Saddle = 1.08f,
                Tint = new Color(0.92f, 0.97f, 1.08f),
                Description = "A great white-flanked stag from the high north. It runs like the wind. 90% faster than walking.",
            },
        };

        public static MountDef Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var d in All) if (d.Id == id) return d;
            return null;
        }

        /// <summary>The id the server's ledger and price list use for this mount.</summary>
        public string LedgerId => "mount:" + Id;

        public CharacterLook Look => new CharacterLook { Model = Model, Height = Height, Anims = AnimSet.Mount, RunSpeed = 9f, Tint = Tint };
    }

    /// <summary>
    /// Puts a rider on a mount. Called, the animal gallops in from behind and the rider hops into the saddle; it wears
    /// a saddle and blanket (the charger full barding); it kicks up what it runs on (dust, sand or snow) with its
    /// hoofbeats; and when the rider gets off it trots away, or, knocked off by a hit, rears up and bolts. Used for the
    /// hero and for other players.
    /// </summary>
    public class MountRig
    {
        static readonly EmoteDef Seated = new EmoteDef { Id = "ride", Clip = "Sit_Floor_Idle", Loop = true };
        const float ArriveSpeed = 15f;

        public MountDef Def { get; private set; }
        readonly Transform owner;
        readonly CharacterView rider;
        CharacterView mount;
        GameObject fallback;
        Vector3 riderHome;
        bool arrived;
        float hopT = -1f, nextPuff, stride;
        ParticleSystem dust; // one looping emitter behind the hooves, its rate following the speed

        public MountRig(Transform owner, CharacterView rider, MountDef def)
        {
            this.owner = owner;
            this.rider = rider;
            Def = def;
            mount = CharacterView.Create(owner, def.Look);
            if (mount == null)
            {
                fallback = Factory.Prim(PrimitiveType.Cube, owner, new Vector3(0f, 0.75f, 0f), new Vector3(0.6f, 0.8f, 1.8f), new Color(0.45f, 0.3f, 0.18f));
                Seat();
                return;
            }
            Tack();
            // called from afar: it comes galloping in from behind
            var from = new Vector3(Random.Range(-3f, 3f), 0f, -11f);
            mount.Root.transform.localPosition = from;
            mount.Root.transform.localRotation = Quaternion.LookRotation(-from.normalized);
            if (rider != null) riderHome = rider.Root.transform.localPosition;
            Sfx.Play("wolf_howl", owner.position + owner.rotation * from, 0.15f, 0.1f, 30f); // a distant call
        }

        /// <summary>The saddle and blanket (and the charger's barding), fitted under where the rider sits.</summary>
        void Tack()
        {
            var root = mount.Root.transform;
            float seat = Def.Saddle;
            bool charger = Def.Id == "warhorse";
            var leather = new Color(0.32f, 0.2f, 0.12f);
            var cloth = charger ? new Color(0.62f, 0.1f, 0.1f) : Def.Id == "stag" ? new Color(0.2f, 0.42f, 0.3f) : new Color(0.25f, 0.32f, 0.55f);
            var trim = new Color(0.95f, 0.78f, 0.3f);
            void Piece(Vector3 at, Vector3 size, Color c, Material m = null)
            {
                var go = Factory.Prim(PrimitiveType.Cube, owner, at, size, c, false, m);
                go.transform.SetParent(root, true);
            }
            Piece(new Vector3(0f, seat - 0.06f, -0.15f), new Vector3(0.42f, 0.1f, 0.5f), leather);           // seat
            Piece(new Vector3(0f, seat + 0.02f, 0.08f), new Vector3(0.3f, 0.12f, 0.06f), leather * 1.2f);     // pommel
            Piece(new Vector3(0f, seat - 0.13f, -0.15f), new Vector3(0.62f, 0.04f, 0.72f), cloth);           // blanket
            foreach (float x in new[] { -0.3f, 0.3f })
            {
                Piece(new Vector3(x, seat - 0.32f, -0.15f), new Vector3(0.03f, 0.36f, 0.7f), cloth);           // blanket sides
                Piece(new Vector3(x * 1.05f, seat - 0.48f, -0.15f), new Vector3(0.035f, 0.04f, 0.7f), trim);  // trim
                Piece(new Vector3(x * 1.1f, seat - 0.62f, -0.08f), new Vector3(0.04f, 0.04f, 0.12f), new Color(0.6f, 0.6f, 0.62f)); // stirrups
            }
            if (!charger) return;
            // the charger's barding: long caparison down its flanks and a crest plume
            foreach (float x in new[] { -0.36f, 0.36f })
            {
                Piece(new Vector3(x, seat - 0.6f, 0.05f), new Vector3(0.03f, 0.75f, 1.25f), cloth * 0.9f);
                Piece(new Vector3(x * 1.03f, seat - 0.98f, 0.05f), new Vector3(0.035f, 0.06f, 1.25f), trim);
            }
            Piece(new Vector3(0f, seat - 0.2f, 0.75f), new Vector3(0.5f, 0.5f, 0.04f), cloth * 0.9f);         // breast cloth
            Piece(new Vector3(0f, seat - 0.05f, 0.76f), new Vector3(0.18f, 0.18f, 0.03f), trim, Mat.Glow(trim * 0.6f)); // crest
        }

        /// <summary>The rider in the saddle.</summary>
        void Seat()
        {
            arrived = true;
            if (rider == null) return;
            riderHome = rider.Root.transform.localPosition;
            rider.Root.transform.localPosition = riderHome + new Vector3(0f, Def.Saddle, -0.15f);
            rider.Emote(Seated);
        }

        /// <summary>Every frame while riding, with the ground speed.</summary>
        public void Tick(float speed)
        {
            float dt = Time.deltaTime;
            if (!arrived && mount != null)
            {
                // galloping in
                var t = mount.Root.transform;
                t.localPosition = Vector3.MoveTowards(t.localPosition, Vector3.zero, ArriveSpeed * dt);
                if (t.localPosition.sqrMagnitude > 0.01f) t.localRotation = Quaternion.Slerp(t.localRotation, Quaternion.LookRotation(-t.localPosition.normalized), dt * 10f);
                mount.UpdateLocomotion(ArriveSpeed * 0.7f);
                rider?.UpdateLocomotion(speed); // still on foot until it's here
                Hooves(ArriveSpeed * 0.7f, t.position);
                if (t.localPosition.sqrMagnitude < 0.0025f)
                {
                    t.localPosition = Vector3.zero;
                    t.localRotation = Quaternion.identity;
                    arrived = true;
                    hopT = 0f;
                    SpellFx.Dust(owner.position, 0.8f, GroundDust(owner.position));
                }
                return;
            }
            if (hopT >= 0f && rider != null)
            {
                // the rider swings up into the saddle
                hopT += dt / 0.3f;
                float h = Mathf.Clamp01(hopT);
                rider.Root.transform.localPosition = riderHome + new Vector3(0f, Def.Saddle * h + Mathf.Sin(h * Mathf.PI) * 0.35f, -0.15f * h);
                if (h >= 1f) { hopT = -1f; rider.Emote(Seated); }
            }
            mount?.UpdateLocomotion(speed);
            if (hopT < 0f && rider != null && !rider.Emoting) rider.Emote(Seated); // something interrupted the pose: sit back down
            Hooves(speed, owner.position);
        }

        /// <summary>Hoofbeats, and a puff of whatever it runs on: snow, sand, dust or grass.</summary>
        void Hooves(float speed, Vector3 at)
        {
            if (speed < 2.5f) { stride = 0f; SpellFx.Emitting(dust, false); return; }
            SpellFx.Emitting(dust, true);
            stride += speed * Time.deltaTime;
            if (stride >= 1.6f)
            {
                stride = 0f;
                bool snow = !Dungeon.Active && SnowField.DepthAt(at) > 0.15f;
                bool stone = Dungeon.Active || WorldGenerator.InTown(at);
                Sfx.Play(snow ? "step_snow" : stone ? "step_stone" : "step_grass", at, 0.5f, 0.15f, 25f);
            }
            if (!SpellFx.Ready) return;
            if (dust == null)
                dust = SpellFx.Loop(new SpellFx.P
                {
                    Rate = 15, Duration = 1f, Life = new Vector2(0.5f, 1f), Speed = new Vector2(0.3f, 1.1f), Size = new Vector2(0.15f, 0.35f),
                    Start = new Color(1f, 1f, 1f, 0.45f), End = new Color(1f, 1f, 1f, 0f), Gravity = 0.15f, Smoke = true, Grow = true, Radius = 0.3f, Max = 30,
                }, owner, new Vector3(0f, 0.1f, -0.6f));
            if (dust == null) return;
            var em = dust.emission;
            em.rateOverTime = Mathf.Lerp(15f, 37f, Mathf.InverseLerp(3f, 12f, speed)) * GameSettings.ParticleScale;
            if (Time.time >= nextPuff)
            {
                // the ground changes under it now and then: snow, sand, grass
                nextPuff = Time.time + 0.5f;
                var c = GroundDust(at);
                var main = dust.main;
                main.startColor = new Color(c.r, c.g, c.b, 1f);
            }
        }

        /// <summary>What the ground throws up here: snow white, sand gold, the ground's own colour (lightened) elsewhere.</summary>
        static Color GroundDust(Vector3 at)
        {
            if (!Dungeon.Active && SnowField.DepthAt(at) > 0.15f) return new Color(0.95f, 0.97f, 1f);
            var g = GroundSurface.Current;
            var c = g != null && !Dungeon.Active ? g.ColorAt(at.x, at.z) : new Color(0.45f, 0.4f, 0.35f);
            return Color.Lerp(c, new Color(0.75f, 0.68f, 0.55f), 0.45f);
        }

        /// <summary>Off the mount: it trots away (or, <paramref name="knocked"/>, rears and bolts) and is gone.</summary>
        public void Remove(bool knocked = false)
        {
            if (dust != null) { Object.Destroy(dust.gameObject); dust = null; }
            if (mount != null)
            {
                var root = mount.Root.transform;
                root.SetParent(null, true);
                root.gameObject.AddComponent<MountLeaves>().Init(mount, knocked, GroundDust(root.position));
            }
            if (fallback != null) Object.Destroy(fallback);
            mount = null;
            if (rider != null)
            {
                rider.Root.transform.localPosition = riderHome;
                rider.StopEmote();
            }
            if (knocked) SpellFx.Dust(owner.position, 0.9f, GroundDust(owner.position));
        }
    }

    /// <summary>A mount leaving: rears up first if its rider was knocked off, then runs off and fades away.</summary>
    public class MountLeaves : MonoBehaviour
    {
        CharacterView view;
        bool rear;
        float t;
        Color dust;
        Vector3 scale;

        public void Init(CharacterView v, bool knocked, Color dustColor)
        {
            view = v;
            rear = knocked;
            dust = dustColor;
            scale = transform.localScale;
            if (knocked) Sfx.Play("roar", transform.position, 0.25f, 0.2f, 30f);
            // off at an angle, away from where the rider was
            transform.rotation *= Quaternion.Euler(0f, Random.Range(-50f, 50f) + (knocked ? 180f : 0f), 0f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;
            float rearFor = rear ? 0.9f : 0f;
            if (t < rearFor)
            {
                // up on its hind legs, flailing, and down again
                float k = t / rearFor, pitch = Mathf.Sin(k * Mathf.PI) * 42f;
                transform.localRotation = Quaternion.Euler(-pitch, transform.localEulerAngles.y, 0f);
                view?.UpdateLocomotion(0f);
                return;
            }
            float run = t - rearFor, speed = rear ? 11f : 4.5f;
            transform.localRotation = Quaternion.Euler(0f, transform.localEulerAngles.y, 0f);
            transform.position += transform.forward * speed * dt;
            view?.UpdateLocomotion(speed);
            if (Random.value < 0.25f && SpellFx.Ready) SpellFx.Dust(transform.position, 0.3f, dust);
            if (run > 1.2f) transform.localScale = scale * Mathf.Clamp01(1f - (run - 1.2f) / 0.5f);
            if (run > 1.7f) Destroy(gameObject);
        }
    }
}
