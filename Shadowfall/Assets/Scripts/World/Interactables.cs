using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>Something the player can walk up to and click: loot, NPCs, trees, rocks, anvils...</summary>
    public abstract class Interactable : MonoBehaviour
    {
        public static readonly List<Interactable> All = new List<Interactable>();

        public string DisplayName;
        public float InteractRange = 1.8f;
        public virtual bool CanInteract => true;
        public virtual Vector3 Position => transform.position;
        public virtual string HoverText => DisplayName;
        public virtual Color LabelColor => Color.white;
        public virtual float LabelHeight => 2.4f;

        protected virtual void OnEnable() => All.Add(this);
        protected virtual void OnDisable() => All.Remove(this);

        public abstract void Interact(Player p);

        protected void AddClickCollider(float radius, float height)
        {
            var col = gameObject.AddComponent<CapsuleCollider>();
            col.radius = radius;
            col.height = height;
            col.center = new Vector3(0, height * 0.5f, 0);
        }
    }

    // =====================================================================================
    // Loot
    // =====================================================================================

    public class LootDrop : Interactable
    {
        public Item Item;
        public int Gold;
        bool taken;
        float spawnTime;
        Transform visual;
        Vector3 fallFrom;

        public override bool CanInteract => !taken;
        public override string HoverText => Gold > 0 ? Gold + " Gold" : Item.Count > 1 ? Item.Name + " (" + Item.Count + ")" :
            Item.Kind == ItemKind.Equipment && Item.Rarity >= Rarity.Rare ? Item.Name + "  [" + Item.RarityName(Item.Rarity) + "]" : Item.Name;
        public override Color LabelColor => Gold > 0 ? new Color(1f, 0.85f, 0.2f) : Item.NameColor;
        public override float LabelHeight => 0.6f;

        public static LootDrop Spawn(Vector3 around, Item item, int gold)
        {
            Vector3 pos = around;
            for (int i = 0; i < 8; i++)
            {
                var p = around + new Vector3(Random.Range(-1.3f, 1.3f), 0, Random.Range(-1.3f, 1.3f));
                if (WorldGrid.Instance.IsWalkable(p)) { pos = p; break; }
            }
            pos.y = 0f;

            var go = new GameObject(item != null ? "Loot " + item.Name : "Gold");
            go.transform.position = pos;
            var d = go.AddComponent<LootDrop>();
            d.Item = item;
            d.Gold = gold;
            d.DisplayName = d.HoverText;
            d.InteractRange = 1.4f;
            d.spawnTime = Time.time;
            d.fallFrom = new Vector3(0, 1.5f, 0);
            d.BuildVisual();

            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(0.9f, 0.6f, 0.9f);
            box.center = new Vector3(0, 0.3f, 0);
            return d;
        }

        void BuildVisual()
        {
            visual = Factory.Empty("Visual", transform, fallFrom);
            if (Gold > 0)
            {
                var gc = new Color(1f, 0.8f, 0.15f);
                int coins = Mathf.Clamp(Gold / 10 + 1, 1, 5);
                for (int i = 0; i < coins; i++)
                    Factory.Prim(PrimitiveType.Cylinder, visual, new Vector3(Random.Range(-0.15f, 0.15f), 0.03f + i * 0.04f, Random.Range(-0.15f, 0.15f)),
                        new Vector3(0.22f, 0.02f, 0.22f), gc, false, Mat.Glow(gc * 0.7f));
                return;
            }

            var c = Item.Kind == ItemKind.Equipment ? Item.IconColor : Item.IconColor;
            var body = Factory.Prim(PrimitiveType.Cube, visual, new Vector3(0, 0.12f, 0), new Vector3(0.45f, 0.18f, 0.3f), c).transform;
            body.localRotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
            if (Item.Kind == ItemKind.Equipment && Item.Rarity >= Rarity.Magic)
            {
                var rc = Item.RarityColor(Item.Rarity);
                Factory.Prim(PrimitiveType.Cube, body, new Vector3(0, 0.6f, 0), new Vector3(0.6f, 0.3f, 0.6f), rc, false, Mat.Glow(rc));
                if (Item.Rarity >= Rarity.Rare)
                {
                    // Loot beam
                    float h = Item.Rarity >= Rarity.Legendary ? 9f : 3.5f;
                    var beam = Factory.Prim(PrimitiveType.Cylinder, transform, new Vector3(0, h * 0.5f, 0), new Vector3(0.12f, h * 0.5f, 0.12f), rc, false, Mat.Glow(rc));
                    beam.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
        }

        void Update()
        {
            float t = (Time.time - spawnTime) / 0.35f;
            if (t <= 1f) visual.localPosition = Vector3.Lerp(fallFrom, Vector3.zero, t * t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.6f;
            else visual.localPosition = Vector3.zero;
        }

        public override void Interact(Player p)
        {
            if (taken) return;
            if (Gold > 0)
            {
                taken = true;
                p.AddGold(Gold);
                Sfx.Play2D("coins", 0.5f, Random.Range(0.95f, 1.05f));
                Destroy(gameObject);
                return;
            }
            int before = Item.Count;
            if (p.Inventory.Add(Item))
            {
                taken = true;
                Sfx.Play2D("loot", 0.6f);
                GameUI.Log("You pick up " + (before > 1 ? before + "x " : "") + Item.Name + ".", Item.NameColor);
                Destroy(gameObject);
            }
            else
            {
                GameUI.Float(p.transform.position + Vector3.up * 2.5f, "Inventory full", new Color(1f, 0.4f, 0.4f), 0.9f);
            }
        }
    }

    // =====================================================================================
    // Gathering nodes
    // =====================================================================================

    public enum ResourceKind { Tree, Rock, FishingSpot }

    public class ResourceNode : Interactable
    {
        public ResourceKind Kind;
        public SkillType Skill;
        public string ItemName;
        public int LevelRequired, Xp;
        public bool Depleted { get; private set; }
        int charges, maxCharges;
        float respawnAt;
        GameObject activeVisual, depletedVisual;

        public override bool CanInteract => !Depleted;
        public override string HoverText =>
            Depleted ? DisplayName + " (depleted)" :
            (Kind == ResourceKind.Tree ? "Chop down " : Kind == ResourceKind.Rock ? "Mine " : "Fish at ") + DisplayName +
            "  (" + Skill + " " + LevelRequired + ")";
        public override Color LabelColor =>
            Player.I != null && Player.I.Skills.Level(Skill) < LevelRequired ? new Color(1f, 0.45f, 0.45f) : new Color(0.85f, 1f, 0.7f);

        static readonly string[] treeNames = { "Oak Tree", "Willow Tree", "Yew Tree" };
        static readonly string[] logNames = { "Oak Logs", "Willow Logs", "Yew Logs" };
        static readonly string[] rockNames = { "Copper Rock", "Iron Rock", "Mithril Rock" };
        static readonly string[] oreNames = { "Copper Ore", "Iron Ore", "Mithril Ore" };
        static readonly int[] tierLevel = { 1, 8, 15 };
        static readonly int[] tierXp = { 35, 65, 120 };

        public static ResourceNode Create(ResourceKind kind, int tier, Vector3 pos, Transform parent)
        {
            tier = Mathf.Clamp(tier, 0, 2);
            var go = new GameObject(kind.ToString());
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var n = go.AddComponent<ResourceNode>();
            n.Kind = kind;
            n.LevelRequired = tierLevel[tier];
            n.Xp = tierXp[tier];
            switch (kind)
            {
                case ResourceKind.Tree:
                    n.Skill = SkillType.Woodcutting;
                    n.DisplayName = treeNames[tier];
                    n.ItemName = logNames[tier];
                    n.maxCharges = Random.Range(2, 6);
                    n.InteractRange = 1.6f;
                    n.BuildTree(tier);
                    n.AddClickCollider(0.6f, 3.5f);
                    WorldGrid.Instance.SetBlocked(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.z), true);
                    break;
                case ResourceKind.Rock:
                    n.Skill = SkillType.Mining;
                    n.DisplayName = rockNames[tier];
                    n.ItemName = oreNames[tier];
                    n.maxCharges = Random.Range(2, 5);
                    n.InteractRange = 1.6f;
                    n.BuildRock(tier);
                    n.AddClickCollider(0.6f, 1.2f);
                    WorldGrid.Instance.SetBlocked(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.z), true);
                    break;
                default:
                    tier = Mathf.Min(tier, 1);
                    n.LevelRequired = tier == 0 ? 1 : 8;
                    n.Xp = tier == 0 ? 40 : 75;
                    n.Skill = SkillType.Fishing;
                    n.DisplayName = tier == 0 ? "Trout Spot" : "Salmon Spot";
                    n.ItemName = tier == 0 ? "Raw Trout" : "Raw Salmon";
                    n.maxCharges = 999;
                    n.InteractRange = 2.6f;
                    n.BuildFishingSpot();
                    n.AddClickCollider(0.8f, 0.6f);
                    break;
            }
            n.charges = n.maxCharges;
            return n;
        }

        // Visual-only randomness (the layout RNG must stay in step so the world map doesn't change).
        static readonly System.Random visualRandom = new System.Random(4242);
        static string PickVisual(params string[] options) => options[visualRandom.Next(options.Length)];

        void BuildTree(int tier)
        {
            activeVisual = new GameObject("Tree");
            activeVisual.transform.SetParent(transform, false);
            var t = activeVisual.transform;
            float s = Random.Range(0.85f, 1.2f);
            float leafShade = tier == 0 ? Random.Range(0.9f, 1.1f) : 1f;

            string model = tier == 0 ? PickVisual("Nature/tree_oak", "Nature/tree_default", "Nature/tree_detailed")
                : tier == 1 ? PickVisual("Nature/tree_fat", "Nature/tree_plateau")
                : PickVisual("Nature/tree_pineTallA_detailed", "Nature/tree_pineTallD_detailed");
            float height = (tier == 0 ? 4.4f : tier == 1 ? 4.8f : 6f) * s;
            var tree = ArtLibrary.Spawn(model, t, Vector3.zero, height, ArtLibrary.Fit.Height, (float)visualRandom.NextDouble() * 360f, true, true, true);
            if (tree != null)
            {
                if (tier == 1) ArtLibrary.Tint(tree, new Color(1.05f, 1.15f, 0.75f)); // willow: yellow-green
                if (tier == 2) ArtLibrary.Tint(tree, new Color(0.7f, 0.85f, 0.75f));  // yew: dark
                depletedVisual = ArtLibrary.Spawn(PickVisual("Nature/stump_roundDetailed", "Nature/stump_old"), transform, Vector3.zero, 0.9f,
                    ArtLibrary.Fit.Width, (float)visualRandom.NextDouble() * 360f, true, true, true);
                if (depletedVisual != null) { depletedVisual.SetActive(false); return; }
            }

            var trunk = new Color(0.4f, 0.28f, 0.16f);
            Factory.Prim(PrimitiveType.Cylinder, t, new Vector3(0, 1f * s, 0), new Vector3(0.35f, 1f * s, 0.35f), trunk);
            if (tier == 0) // Oak: round canopy
            {
                var leaf = new Color(0.22f, 0.5f, 0.18f) * leafShade;
                Factory.Prim(PrimitiveType.Sphere, t, new Vector3(0, 2.6f * s, 0), new Vector3(2.2f, 1.9f, 2.2f) * s, leaf);
                Factory.Prim(PrimitiveType.Sphere, t, new Vector3(0.5f, 3.2f * s, 0.2f), new Vector3(1.3f, 1.2f, 1.3f) * s, leaf * 1.1f);
            }
            else if (tier == 1) // Willow: wide drooping canopy
            {
                var leaf = new Color(0.45f, 0.62f, 0.25f);
                Factory.Prim(PrimitiveType.Sphere, t, new Vector3(0, 2.7f * s, 0), new Vector3(2.8f, 1.3f, 2.8f) * s, leaf);
                for (int i = 0; i < 6; i++)
                {
                    var dir = Quaternion.Euler(0, i * 60f, 0) * Vector3.forward * 1.1f * s;
                    Factory.Prim(PrimitiveType.Cube, t, dir + Vector3.up * 1.9f * s, new Vector3(0.35f, 1.4f, 0.35f) * s, leaf * 0.9f);
                }
            }
            else // Yew: tall dark layered
            {
                var leaf = new Color(0.1f, 0.3f, 0.15f);
                for (int i = 0; i < 3; i++)
                {
                    float w = (1.9f - i * 0.5f) * s;
                    Factory.Prim(PrimitiveType.Cube, t, new Vector3(0, (1.9f + i * 0.9f) * s, 0), new Vector3(w, 0.8f * s, w), leaf * (1f + i * 0.15f))
                        .transform.localRotation = Quaternion.Euler(0, 45f * i, 0);
                }
            }
            depletedVisual = new GameObject("Stump");
            depletedVisual.transform.SetParent(transform, false);
            Factory.Prim(PrimitiveType.Cylinder, depletedVisual.transform, new Vector3(0, 0.2f, 0), new Vector3(0.45f, 0.2f, 0.45f), trunk);
            Factory.Prim(PrimitiveType.Cylinder, depletedVisual.transform, new Vector3(0, 0.41f, 0), new Vector3(0.38f, 0.01f, 0.38f), new Color(0.75f, 0.6f, 0.4f));
            depletedVisual.SetActive(false);
        }

        void BuildRock(int tier)
        {
            var stone = new Color(0.45f, 0.43f, 0.4f);
            Color ore = tier == 0 ? new Color(0.9f, 0.5f, 0.2f) : tier == 1 ? new Color(0.6f, 0.3f, 0.25f) : new Color(0.35f, 0.5f, 1f);
            activeVisual = new GameObject("Rock");
            activeVisual.transform.SetParent(transform, false);
            depletedVisual = new GameObject("Empty");
            depletedVisual.transform.SetParent(transform, false);

            // A real rock model stays visible; only the ore crystals disappear when it's mined out.
            var rock = ArtLibrary.SpawnBox(PickVisual("Nature/rock_largeA", "Nature/rock_largeB", "Nature/rock_largeC", "Nature/rock_largeD"),
                transform, Vector3.zero, new Vector3(1.5f, 1.05f, 1.4f), (float)visualRandom.NextDouble() * 360f);
            if (rock != null && tier > 0) ArtLibrary.Tint(rock, tier == 1 ? new Color(0.85f, 0.75f, 0.72f) : new Color(0.75f, 0.8f, 0.95f));

            if (rock == null)
            foreach (var parent in new[] { activeVisual.transform, depletedVisual.transform })
            {
                Factory.Prim(PrimitiveType.Cube, parent, new Vector3(0, 0.45f, 0), new Vector3(1.1f, 0.9f, 1f), stone)
                    .transform.localRotation = Quaternion.Euler(8, 30, 5);
                Factory.Prim(PrimitiveType.Cube, parent, new Vector3(0.3f, 0.8f, 0.1f), new Vector3(0.6f, 0.5f, 0.6f), stone * 0.9f)
                    .transform.localRotation = Quaternion.Euler(20, 10, 15);
            }
            var oreMat = tier == 2 ? Mat.Glow(ore) : Mat.Get(ore);
            for (int i = 0; i < 4; i++)
            {
                var p = new Vector3(Random.Range(-0.45f, 0.45f), Random.Range(0.4f, 0.95f), Random.Range(-0.5f, 0.5f));
                Factory.Prim(PrimitiveType.Cube, activeVisual.transform, p, Vector3.one * Random.Range(0.18f, 0.3f), ore, false, oreMat)
                    .transform.localRotation = Random.rotation;
            }
            depletedVisual.SetActive(false);
        }

        void BuildFishingSpot()
        {
            activeVisual = new GameObject("Ripples");
            activeVisual.transform.SetParent(transform, false);
            activeVisual.AddComponent<FishingRipple>();
            var c = new Color(0.75f, 0.9f, 1f);
            Factory.Prim(PrimitiveType.Cylinder, activeVisual.transform, new Vector3(0, 0.02f, 0), new Vector3(1.2f, 0.01f, 1.2f), c, false, Mat.Glow(c * 0.6f));
            Factory.Prim(PrimitiveType.Sphere, activeVisual.transform, new Vector3(0, 0.1f, 0), new Vector3(0.25f, 0.1f, 0.25f), Color.white, false, Mat.Glow(Color.white));
            depletedVisual = new GameObject("Calm");
            depletedVisual.transform.SetParent(transform, false);
            depletedVisual.SetActive(false);
        }

        public void Harvested()
        {
            charges--;
            if (charges > 0) return;
            Depleted = true;
            respawnAt = Time.time + Random.Range(10f, 20f);
            activeVisual.SetActive(false);
            depletedVisual.SetActive(true);
            StartCoroutine(RegrowLater());
            if (Kind == ResourceKind.Tree) GameUI.Log("The tree falls.", Color.gray);
            if (Kind == ResourceKind.Rock) GameUI.Log("You have mined the rock clean.", Color.gray);
        }

        // No Update(): there are hundreds of nodes. Regrowth is a coroutine and only fishing spots animate (FishingRipple).
        System.Collections.IEnumerator RegrowLater()
        {
            while (Time.time < respawnAt) yield return new WaitForSeconds(Mathf.Max(0.1f, respawnAt - Time.time));
            Depleted = false;
            charges = maxCharges;
            activeVisual.SetActive(true);
            depletedVisual.SetActive(false);
        }

        public override void Interact(Player p) => p.StartGathering(this);
    }

    // =====================================================================================
    // Crafting stations (anvil = Smithing, campfire = Cooking)
    // =====================================================================================

    public class Recipe
    {
        public string Name, Input;
        public int InputCount = 1, LevelRequired = 1, Xp;
        public SkillType Skill;
        public System.Func<Player, Item> Make;
        public string FailItem;

        public static readonly Recipe[] Smithing =
        {
            new Recipe { Name = "Forge Copper Gear", Input = "Copper Ore", InputCount = 3, LevelRequired = 1, Xp = 45, Skill = SkillType.Smithing,
                Make = p => ItemDatabase.RandomEquipment(3 + p.Skills.Level(SkillType.Smithing) / 3, p.Skills.Level(SkillType.Smithing) * 0.01f) },
            new Recipe { Name = "Forge Iron Gear", Input = "Iron Ore", InputCount = 3, LevelRequired = 8, Xp = 90, Skill = SkillType.Smithing,
                Make = p => ItemDatabase.RandomEquipment(10 + p.Skills.Level(SkillType.Smithing) / 3, 0.1f + p.Skills.Level(SkillType.Smithing) * 0.01f) },
            new Recipe { Name = "Forge Mithril Gear", Input = "Mithril Ore", InputCount = 3, LevelRequired = 15, Xp = 170, Skill = SkillType.Smithing,
                Make = p => ItemDatabase.RandomEquipment(18 + p.Skills.Level(SkillType.Smithing) / 3, 0.25f + p.Skills.Level(SkillType.Smithing) * 0.01f) },
        };

        public static readonly Recipe[] Cooking =
        {
            new Recipe { Name = "Cook Trout", Input = "Raw Trout", LevelRequired = 1, Xp = 30, Skill = SkillType.Cooking,
                Make = p => ItemDatabase.Food("Cooked Trout"), FailItem = "Burnt Fish" },
            new Recipe { Name = "Cook Salmon", Input = "Raw Salmon", LevelRequired = 8, Xp = 60, Skill = SkillType.Cooking,
                Make = p => ItemDatabase.Food("Cooked Salmon"), FailItem = "Burnt Fish" },
        };

        /// <summary>Attempts one craft. Returns false if the player lacks the level or materials.</summary>
        public bool Craft(Player p)
        {
            if (p.Skills.Level(Skill) < LevelRequired)
            {
                GameUI.Log("You need " + Skill + " level " + LevelRequired + " to do that.", new Color(1f, 0.4f, 0.4f));
                return false;
            }
            if (p.Inventory.CountOf(Input) < InputCount)
            {
                GameUI.Log("You need " + InputCount + " " + Input + ".", new Color(1f, 0.4f, 0.4f));
                return false;
            }
            p.Inventory.Remove(Input, InputCount);

            if (FailItem != null)
            {
                float burn = Mathf.Clamp(0.45f - (p.Skills.Level(Skill) - LevelRequired) * 0.04f, 0.03f, 0.45f);
                if (Random.value < burn)
                {
                    p.Inventory.Add(ItemDatabase.Material(FailItem));
                    GameUI.Log("You accidentally burn the fish.", new Color(0.8f, 0.5f, 0.3f));
                    return true;
                }
            }

            var result = Make(p);
            if (!p.Inventory.Add(result)) LootDrop.Spawn(p.transform.position, result, 0);
            Sfx.Play(Skill == SkillType.Smithing ? "anvil" : "sizzle", p.transform.position, 0.6f);
            p.Skills.AddXp(Skill, Xp);
            GameUI.Log("You make: " + result.Name, result.NameColor);
            return true;
        }
    }

    public class CraftingStation : Interactable
    {
        public SkillType Skill;
        public Recipe[] Recipes;
        Light fireLight;

        public override string HoverText => DisplayName + " (" + Skill + ")";
        public override Color LabelColor => new Color(1f, 0.8f, 0.5f);

        public static CraftingStation Create(SkillType skill, Vector3 pos, Transform parent)
        {
            var go = new GameObject(skill == SkillType.Smithing ? "Anvil" : "Campfire");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var s = go.AddComponent<CraftingStation>();
            s.Skill = skill;
            s.InteractRange = 2f;
            if (skill == SkillType.Smithing)
            {
                s.DisplayName = "Anvil";
                s.Recipes = Recipe.Smithing;
                var iron = new Color(0.25f, 0.25f, 0.28f);
                Factory.Prim(PrimitiveType.Cube, go.transform, new Vector3(0, 0.3f, 0), new Vector3(0.5f, 0.6f, 0.4f), new Color(0.35f, 0.25f, 0.15f));
                Factory.Prim(PrimitiveType.Cube, go.transform, new Vector3(0, 0.7f, 0), new Vector3(1.0f, 0.25f, 0.45f), iron);
                Factory.Prim(PrimitiveType.Cube, go.transform, new Vector3(0.55f, 0.75f, 0), new Vector3(0.3f, 0.12f, 0.25f), iron);
            }
            else
            {
                s.DisplayName = "Campfire";
                s.Recipes = Recipe.Cooking;
                var stones = ArtLibrary.Spawn("Nature/campfire_stones", go.transform, Vector3.zero, 1.4f, ArtLibrary.Fit.Width, 0f, true, true, true);
                if (stones != null)
                    ArtLibrary.Spawn("Nature/campfire_logs", go.transform, Vector3.zero, 0.9f, ArtLibrary.Fit.Width, 30f, true, true, true);
                else
                {
                    for (int i = 0; i < 6; i++)
                        Factory.Prim(PrimitiveType.Cube, go.transform, Quaternion.Euler(0, i * 60, 0) * Vector3.forward * 0.6f + Vector3.up * 0.1f,
                            new Vector3(0.3f, 0.2f, 0.3f), new Color(0.4f, 0.4f, 0.42f));
                    Factory.Prim(PrimitiveType.Cylinder, go.transform, new Vector3(0, 0.15f, 0), new Vector3(0.8f, 0.08f, 0.15f), new Color(0.35f, 0.2f, 0.1f))
                        .transform.localRotation = Quaternion.Euler(0, 30, 90);
                    Factory.Prim(PrimitiveType.Cylinder, go.transform, new Vector3(0, 0.15f, 0), new Vector3(0.8f, 0.08f, 0.15f), new Color(0.35f, 0.2f, 0.1f))
                        .transform.localRotation = Quaternion.Euler(0, -30, 90);
                }
                var flame = new Color(1f, 0.55f, 0.1f);
                Factory.Prim(PrimitiveType.Sphere, go.transform, new Vector3(0, 0.45f, 0), new Vector3(0.45f, 0.65f, 0.45f), flame, false, Mat.Glow(flame))
                    .GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var lightGo = new GameObject("FireLight");
                lightGo.transform.SetParent(go.transform, false);
                lightGo.transform.localPosition = new Vector3(0, 1.2f, 0);
                s.fireLight = lightGo.AddComponent<Light>();
                s.fireLight.type = LightType.Point;
                s.fireLight.color = flame;
                s.fireLight.range = 8f;
                s.fireLight.intensity = 1.5f;
                Sfx.LoopAt("fire_loop", pos, 0.8f, 12f);
            }
            s.AddClickCollider(0.7f, 1.2f);
            WorldGrid.Instance.SetBlocked(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.z), true);
            return s;
        }

        void Update()
        {
            if (fireLight != null)
                fireLight.intensity = (1.3f + Mathf.PerlinNoise(Time.time * 6f, transform.position.x) * 0.8f) * Mathf.Lerp(0.7f, 1.4f, DayNight.Night);
        }

        public override void Interact(Player p) => GameUI.I.OpenCrafting(this);
    }

    // =====================================================================================
    // NPCs
    // =====================================================================================

    public enum NpcRole { QuestGiver, Vendor, Healer }

    public class Npc : Interactable
    {
        public NpcRole Role;
        public string Title, Greeting;
        public VendorStock Shop;    // vendors only
        HumanoidModel model;
        CharacterView view;
        float idleTimer;
        float chatterAt, greetedAt = -999f;
        bool playerWasNear;

        public override Color LabelColor => new Color(0.4f, 1f, 0.4f);
        public override float LabelHeight => 2.6f;
        public override string HoverText => DisplayName + (string.IsNullOrEmpty(Title) ? "" : "\n<" + Title + ">");

        /// <param name="blocksTile">
        /// False for NPCs added after the world map was first uploaded to servers, so existing servers keep
        /// accepting the client (the walkability map, and its hash, stay the same).
        /// </param>
        public static Npc Create(string name, string title, NpcRole role, Vector3 pos, Color robe, string greeting, Transform parent,
            bool hasWeapon = false, bool? wearsRobe = null, bool blocksTile = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            // The original NPCs take their facing from the world's layout RNG (keeping the layout, and so the
            // server's world hash, unchanged); newer, non-blocking NPCs must not touch that sequence.
            float facing = blocksTile ? Random.Range(0, 360f) : ((name.GetHashCode() & 0x7fffffff) % 360);
            go.transform.rotation = Quaternion.Euler(0, facing, 0);
            var n = go.AddComponent<Npc>();
            n.DisplayName = name;
            n.Title = title;
            n.Role = role;
            n.Greeting = greeting;
            n.InteractRange = 2.4f;
            n.view = CharacterView.Create(go.transform, CharacterLook.ForNpc(name));
            if (n.view == null)
            n.model = HumanoidModel.Build(go.transform, 1f, new Color(0.9f, 0.75f, 0.6f), robe, Factory.Shade(robe, 0.7f),
                new Color(0.7f, 0.7f, 0.75f), hasWeapon, wearsRobe ?? !hasWeapon);
            n.AddClickCollider(0.5f, 2.1f);
            if (blocksTile) WorldGrid.Instance.SetBlocked(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.z), true);
            n.chatterAt = Time.time + 5f + (name.GetHashCode() & 0x7fffffff) % 20; // not Random: see above
            return n;
        }

        public Npc SellsAs(VendorKind kind)
        {
            Shop = new VendorStock(kind);
            return this;
        }

        /// <summary>
        /// Restyles the NPC after the Jenkins automation server's butler mascot:
        /// black tailcoat, white shirt, red bow tie, neat grey hair and a napkin over the arm.
        /// </summary>
        public void DressAsButler()
        {
            if (model == null) return;
            var black = new Color(0.08f, 0.08f, 0.1f);
            var white = new Color(0.95f, 0.95f, 0.93f);
            var red = new Color(0.8f, 0.1f, 0.1f);
            var grey = new Color(0.72f, 0.72f, 0.74f);

            model.BodyRenderer.sharedMaterial = Mat.Get(black);
            model.ArmRendererL.sharedMaterial = model.ArmRendererR.sharedMaterial = Mat.Get(black);
            model.LegRendererL.sharedMaterial = model.LegRendererR.sharedMaterial = Mat.Get(Factory.Shade(black, 1.4f));

            // White shirt front with a red bow tie
            Factory.Prim(PrimitiveType.Cube, model.Root, new Vector3(0, 1.38f, 0.185f), new Vector3(0.24f, 0.62f, 0.02f), white);
            Factory.Prim(PrimitiveType.Cube, model.Root, new Vector3(-0.07f, 1.63f, 0.2f), new Vector3(0.13f, 0.09f, 0.04f), red)
                .transform.localRotation = Quaternion.Euler(0, 0, 15);
            Factory.Prim(PrimitiveType.Cube, model.Root, new Vector3(0.07f, 1.63f, 0.2f), new Vector3(0.13f, 0.09f, 0.04f), red)
                .transform.localRotation = Quaternion.Euler(0, 0, -15);
            Factory.Prim(PrimitiveType.Cube, model.Root, new Vector3(0, 1.63f, 0.21f), new Vector3(0.05f, 0.06f, 0.04f), Factory.Shade(red, 0.8f));
            // Tailcoat tails
            Factory.Prim(PrimitiveType.Cube, model.Root, new Vector3(0, 0.85f, -0.16f), new Vector3(0.5f, 0.4f, 0.05f), black);

            // Neat grey hair on top and back of the head (face stays visible)
            Factory.Prim(PrimitiveType.Sphere, model.Head, new Vector3(0, 0.12f, -0.1f), new Vector3(1.06f, 0.92f, 1.0f), grey);
            // Eyebrows
            Factory.Prim(PrimitiveType.Cube, model.Head, new Vector3(-0.2f, 0.18f, 0.45f), new Vector3(0.22f, 0.06f, 0.06f), grey);
            Factory.Prim(PrimitiveType.Cube, model.Head, new Vector3(0.2f, 0.18f, 0.45f), new Vector3(0.22f, 0.06f, 0.06f), grey);

            // Napkin draped over the left forearm
            Factory.Prim(PrimitiveType.Cube, model.LArm, new Vector3(0, -0.5f, 0.06f), new Vector3(0.24f, 0.38f, 0.26f), white);
        }

        /// <summary>The next quest this NPC can offer or accept, or null.</summary>
        public QuestDef CurrentQuest(Player p)
        {
            if (!QuestDatabase.Chains.TryGetValue(DisplayName, out var chain)) return null;
            // A quest you already have (it may have been shared out of order by a party member) comes first.
            foreach (var q in chain)
                if (p.Quests.IsActive(q.Id)) return q;
            foreach (var q in chain)
                if (!p.Quests.Completed.Contains(q.Id)) return q;
            return null;
        }

        /// <summary>"!" (available), "?" (ready to turn in), "…" (in progress) or null.</summary>
        public string Marker(Player p, out Color color)
        {
            color = new Color(1f, 0.85f, 0.1f);
            var q = CurrentQuest(p);
            if (q == null) return null;
            var state = p.Quests.Get(q.Id);
            if (state != null)
            {
                if (state.IsReady(p)) return "?";
                color = new Color(0.7f, 0.7f, 0.7f);
                return "?";
            }
            if (p.Level < q.MinLevel) { color = new Color(0.6f, 0.6f, 0.6f); return "!"; }
            return "!";
        }

        void Update()
        {
            if (view != null) view.UpdateLocomotion(0f);
            else model.Animate(0f, -1f, Time.deltaTime);
            var p = Player.I;
            float dist = p != null ? Factory.FlatDistance(p.transform.position, transform.position) : 999f;
            Chatter(p, dist);
            if (dist < 6f)
                Factory.Face(transform, p.transform.position, Time.deltaTime * 4f);
            else if ((idleTimer -= Time.deltaTime) < 0f)
            {
                idleTimer = Random.Range(4f, 9f);
                transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            }
        }

        /// <summary>Greets heroes who walk up, and mutters to itself now and then.</summary>
        void Chatter(Player p, float dist)
        {
            if (p == null) return;
            bool near = dist < 5f;
            if (near && !playerWasNear && Time.time - greetedAt > 90f && Random.value < 0.6f)
            {
                greetedAt = Time.time;
                Speech.Say(transform, LabelHeight + 0.9f, NpcChatter.Greeting(p.DisplayName));
                chatterAt = Time.time + Random.Range(12f, 25f);
            }
            playerWasNear = near;
            if (Time.time < chatterAt) return;
            chatterAt = Time.time + Random.Range(20f, 45f);
            if (dist > 22f || GameUI.I == null || GameUI.I.IsTalkingTo(this)) return;
            var line = NpcChatter.Line(DisplayName, p.DisplayName);
            if (line != null) Speech.Say(transform, LabelHeight + 0.9f, line);
        }

        public override void Interact(Player p)
        {
            GameUI.I.OpenDialog(this);
            if (view != null) { if (Role == NpcRole.QuestGiver) view.Cheer(); else view.Interact(); }
        }
    }

    /// <summary>The bobbing ripple on a fishing spot.</summary>
    public class FishingRipple : MonoBehaviour
    {
        void Update()
        {
            float s = 1f + Mathf.Sin(Time.time * 3f + transform.position.x) * 0.15f;
            transform.localScale = new Vector3(s, 1f, s);
        }
    }
}
