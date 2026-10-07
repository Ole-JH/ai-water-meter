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
    /// Puts a rider on a mount: the animal is spawned under the character, the rider's model is lifted into the saddle
    /// and holds a seated pose, and the animal walks and gallops with the character's speed. Used for the hero and for
    /// other players.
    /// </summary>
    public class MountRig
    {
        static readonly EmoteDef Seated = new EmoteDef { Id = "ride", Clip = "Sit_Floor_Idle", Loop = true };

        public MountDef Def { get; private set; }
        readonly Transform owner;
        readonly CharacterView rider;
        CharacterView mount;
        GameObject fallback;
        Vector3 riderHome;

        public MountRig(Transform owner, CharacterView rider, MountDef def)
        {
            this.owner = owner;
            this.rider = rider;
            Def = def;
            mount = CharacterView.Create(owner, def.Look);
            if (mount == null)
                fallback = Factory.Prim(PrimitiveType.Cube, owner, new Vector3(0f, 0.75f, 0f), new Vector3(0.6f, 0.8f, 1.8f), new Color(0.45f, 0.3f, 0.18f));
            if (rider != null)
            {
                riderHome = rider.Root.transform.localPosition;
                rider.Root.transform.localPosition = riderHome + new Vector3(0f, def.Saddle, -0.15f);
                rider.Emote(Seated);
            }
            SpellFx.Swirl(owner.position + Vector3.up * 0.4f, null, new Color(0.85f, 0.75f, 0.55f), 1.2f, 0.5f, 40f, false);
            Sfx.Play("whoosh", owner.position, 0.4f, 0.1f);
        }

        /// <summary>Every frame while riding, with the ground speed.</summary>
        public void Tick(float speed)
        {
            mount?.UpdateLocomotion(speed);
            if (rider != null && !rider.Emoting) rider.Emote(Seated); // something interrupted the pose: sit back down
        }

        public void Remove()
        {
            if (mount != null) Object.Destroy(mount.Root);
            if (fallback != null) Object.Destroy(fallback);
            mount = null;
            if (rider != null)
            {
                rider.Root.transform.localPosition = riderHome;
                rider.StopEmote();
            }
            SpellFx.Swirl(owner.position + Vector3.up * 0.4f, null, new Color(0.85f, 0.75f, 0.55f), 1f, 0.4f, 40f, false);
        }
    }
}
