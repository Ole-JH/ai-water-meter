using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// The hero: Diablo-style click-to-move / click-to-attack controller, WoW-style ability bar,
    /// Diablo-style attributes and loot, RuneScape-style gathering skills.
    /// </summary>
    public class Player : Combatant
    {
        public static Player I;

        // ---- Attributes (Diablo style)
        public int Strength = 10, Dexterity = 10, Intelligence = 10, Vitality = 10, StatPoints;
        public int Xp, Gold;
        public readonly Inventory Inventory = new Inventory(40);
        /// <summary>The stash chest in Hollowmere (shared by nothing else; saved with the character).</summary>
        public readonly Inventory Stash = new Inventory(40);
        public readonly SkillSet Skills = new SkillSet();
        public readonly QuestLog Quests = new QuestLog();
        public readonly float[] CooldownEnd = new float[5];
        /// <summary>This hero's five abilities (keys 1-5), from its class.</summary>
        public AbilityDef[] Kit { get; private set; } = ClassKits.For("Knight");
        /// <summary>Talent ranks by id (see <see cref="Shadowfall.Talents"/>).</summary>
        public readonly Dictionary<string, int> Talents = new Dictionary<string, int>();
        public readonly List<Buff> Buffs = new List<Buff>();
        /// <summary>Companions hired from Beastmaster Orla (ids), and the one following us (null = none).</summary>
        public readonly List<string> OwnedCompanions = new List<string>();
        public string ActiveCompanion { get; private set; }
        public Companion CompanionInstance { get; private set; }
        float whirlUntil, nextWhirlTick, leapT = -1f;
        Vector3 leapFrom, leapTo;

        // ---- Derived stats
        public int TotStr, TotDex, TotInt, TotVit;
        public float ArmorValue, CritChance, AttackSpeed, MinDamage, MaxDamage, MoveSpeed, LifeOnHit, HealthRegen, ManaRegen;
        public override float Armor => ArmorValue;
        /// <summary>The attribute that powers this class's weapon attacks.</summary>
        public int PrimaryStat => Look == "Rogue" ? TotDex : Look == "Mage" ? TotInt : TotStr;
        public float MeleeMultiplier => (1f + PrimaryStat / 50f) * (1f + 0.06f * Tal("brute")) * BuffDamage;
        public float SpellMultiplier => (1f + TotInt / 40f) * (1f + (Level - 1) * 0.06f) * (1f + 0.06f * Tal("arcane")) * BuffDamage;
        public bool Whirling => Time.time < whirlUntil;
        public float AttackRange = 1.8f;
        public int XpToNext => Mathf.RoundToInt(100f * Mathf.Pow(Level, 1.55f));

        // ---- Hover / targeting (read by UI)
        public Enemy HoveredEnemy { get; private set; }
        public Interactable HoveredInteractable { get; private set; }
        public Combatant AttackTarget { get; private set; }
        public Vector3 MouseGround { get; private set; }

        // ---- Gathering (read by UI)
        public ResourceNode GatherNode { get; private set; }
        public float GatherProgress { get; private set; }

        enum Action { None, Move, Attack, Interact, Gather }
        Action action;
        Interactable interactTarget;
        readonly List<Vector3> path = new List<Vector3>();
        Vector3 pathGoal;
        float repathAt, nextAttackTime, attackAnim = -1f, castAnim = -1f, castLockUntil, holdMoveAt;
        float globalCooldownEnd, gatherTimer;
        bool holdMoving, standAttack;
        Vector3 standAttackPoint;
        string lastZone;
        HumanoidModel model;        // primitive fallback
        CharacterView view;         // animated model
        public string Look { get; private set; } = "Knight";
        Light torch;
        float currentSpeed;
        static readonly List<Combatant> buffer = new List<Combatant>();

        // ---- Networking helpers
        public bool IsMoving => currentSpeed > 0.01f;
        public bool IsAttacking => attackAnim >= 0f || castAnim >= 0f;
        public string BodyHex, LegsHex, WeaponHex, HelmHex;

        public static Player Create(Vector3 pos, string look)
        {
            var go = new GameObject("Player");
            go.transform.position = pos;
            var p = go.AddComponent<Player>();
            p.SetLook(look);
            p.RecalculateStats();
            p.Health = p.MaxHealth;
            p.Mana = p.MaxMana;
            return p;
        }

        /// <summary>Builds (or rebuilds) the hero's model: an animated KayKit character, or primitives as a fallback.</summary>
        public void SetLook(string look)
        {
            Look = System.Array.IndexOf(CharacterLook.HeroModels, look) >= 0 ? look : CharacterLook.HeroModels[0];
            Kit = ClassKits.For(Look);
            var st = ClassKits.StartingStats(Look);
            Strength = st[0]; Dexterity = st[1]; Intelligence = st[2]; Vitality = st[3];
            if (view != null) Destroy(view.Root);
            if (model != null) Destroy(model.Root.gameObject);
            view = CharacterView.Create(transform, CharacterLook.ForHero(Look));
            model = view == null
                ? HumanoidModel.Build(transform, 1f, new Color(0.95f, 0.78f, 0.62f), new Color(0.5f, 0.4f, 0.3f),
                    new Color(0.3f, 0.25f, 0.2f), new Color(0.75f, 0.75f, 0.8f))
                : null;
            RefreshVisuals();
        }

        void AnimAttack()
        {
            attackAnim = 0f;
            view?.Attack(1f / Mathf.Max(0.6f, AttackSpeed));
            Sfx.Play("swing", transform.position + Vector3.up, 0.55f, 0.12f);
        }

        float stepDistance;

        /// <summary>Footsteps: stone in the village and the crypt, grass and earth everywhere else.</summary>
        void Footsteps(float speed, float dt)
        {
            if (speed < 0.5f) { stepDistance = 0.6f; return; }
            stepDistance += speed * dt;
            if (stepDistance < 1.15f) return;
            stepDistance = 0f;
            var pos = transform.position;
            bool stone = WorldGenerator.InTown(pos) || WorldGenerator.InCrypt(pos) || Dungeon.Active;
            Sfx.Play(stone ? "step_stone" : "step_grass", pos, stone ? 0.35f : 0.45f, 0.1f, 20f);
        }

        void AnimCast(Color? glow = null)
        {
            castAnim = 0f;
            view?.Cast();
            if (glow.HasValue && view != null) SpellFx.CastGlow(view.Hand, glow.Value, 0.35f);
        }

        void Awake()
        {
            I = this;
            DisplayName = "Hero";
            Faction = Faction.Player;
            Radius = 0.45f;
            Height = 2f;

            var col = gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 1f, 0);
            col.height = 2f;
            col.radius = 0.4f;

            // Diablo-style torch light that follows the hero
            var lightGo = new GameObject("Torch");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0, 3.5f, 0);
            torch = lightGo.AddComponent<Light>();
            torch.type = LightType.Point;
            torch.range = 12f;
            torch.intensity = 1.6f;
            torch.color = new Color(1f, 0.8f, 0.55f);

            Inventory.Changed += RecalculateStats;
            Inventory.Equipped[EquipSlot.Weapon] = ItemDatabase.StarterWeapon();
            Inventory.Equipped[EquipSlot.Chest] = ItemDatabase.StarterChest();
            var hp = ItemDatabase.HealthPotion(); hp.Count = 5; Inventory.Add(hp);
            var mp = ItemDatabase.ManaPotion(); mp.Count = 3; Inventory.Add(mp);
            RecalculateStats();
            Health = MaxHealth;
            Mana = MaxMana;
        }

        // =====================================================================================
        // Stats
        // =====================================================================================

        public int ItemStat(Stat s)
        {
            int total = 0;
            foreach (var kv in Inventory.Equipped) if (kv.Value != null) total += kv.Value.GetStat(s);
            return total;
        }

        public void RecalculateStats()
        {
            TotStr = Strength + ItemStat(Stat.Strength);
            TotDex = Dexterity + ItemStat(Stat.Dexterity);
            TotInt = Intelligence + ItemStat(Stat.Intelligence);
            TotVit = Vitality + ItemStat(Stat.Vitality);

            MaxHealth = (60 + TotVit * 6 + Level * 8 + ItemStat(Stat.Health)) * (1f + 0.05f * (Tal("toughness") + Tal("thickskin")));
            MaxMana = 40 + TotInt * 3 + Level * 3 + ItemStat(Stat.Mana);

            int armor = ItemStat(Stat.Armor);
            foreach (var kv in Inventory.Equipped) if (kv.Value != null) armor += kv.Value.Armor;
            float armorMul = 1f + 0.08f * Tal("bulwark");
            foreach (var b in Buffs) armorMul *= b.ArmorMul;
            ArmorValue = (armor + TotDex * 0.25f) * armorMul;

            CritChance = Mathf.Min(75f, 5f + TotDex * 0.15f + ItemStat(Stat.CritChance) + 2f * (Tal("focus") + Tal("precision")));
            var weapon = Inventory.GetEquipped(EquipSlot.Weapon);
            float baseAps = weapon != null ? weapon.AttacksPerSecond : 1.4f;
            MinDamage = weapon != null ? weapon.MinDamage : 1;
            MaxDamage = weapon != null ? weapon.MaxDamage : 3;
            AttackSpeed = baseAps * (1f + ItemStat(Stat.AttackSpeed) / 100f);
            MoveSpeed = 6.2f * (1f + ItemStat(Stat.MoveSpeed) / 100f + 0.04f * Tal("swiftness"));
            LifeOnHit = ItemStat(Stat.LifeOnHit);
            HealthRegen = 1f + Level * 0.2f + ItemStat(Stat.HealthRegen);
            ManaRegen = (2f + TotInt * 0.06f + ItemStat(Stat.ManaRegen)) * (1f + 0.12f * Tal("manafont"));
            ItemPowers.ApplySetBonuses(this);

            Health = Mathf.Min(Health, MaxHealth);
            Mana = Mathf.Min(Mana, MaxMana);
            RefreshVisuals();
        }

        /// <summary>Weapon model kind shown in hand (sent to other players).</summary>
        public string WeaponKind { get; private set; }

        void RefreshVisuals()
        {
            var weapon = Inventory.GetEquipped(EquipSlot.Weapon);
            WeaponKind = CharacterView.WeaponKind(weapon);
            var worn = Inventory.GetEquipped(EquipSlot.Helm);
            view?.Equip(WeaponKind, worn != null);
            if (model == null)
            {
                HelmHex = worn != null ? Item.Hex(worn.IconColor) : "";
                WeaponHex = weapon != null ? Item.Hex(weapon.IconColor) : "";
                return;
            }
            if (model.WeaponRenderer != null)
            {
                model.WeaponRenderer.gameObject.SetActive(weapon != null);
                if (weapon != null)
                    model.WeaponRenderer.sharedMaterial = weapon.Rarity >= Rarity.Rare
                        ? Mat.Glow(Item.RarityColor(weapon.Rarity))
                        : Mat.Get(weapon.IconColor);
            }
            var chest = Inventory.GetEquipped(EquipSlot.Chest);
            Color chestColor = chest != null ? Color.Lerp(chest.IconColor, Item.RarityColor(chest.Rarity), 0.35f) : new Color(0.5f, 0.4f, 0.3f);
            model.BodyRenderer.sharedMaterial = Mat.Get(chestColor);
            var gloves = Inventory.GetEquipped(EquipSlot.Gloves);
            Color armColor = gloves != null ? Color.Lerp(gloves.IconColor, chestColor, 0.3f) : chestColor;
            model.ArmRendererL.sharedMaterial = model.ArmRendererR.sharedMaterial = Mat.Get(armColor);
            var legs = Inventory.GetEquipped(EquipSlot.Legs);
            Color legColor = legs != null ? Color.Lerp(legs.IconColor, Item.RarityColor(legs.Rarity), 0.25f) : new Color(0.3f, 0.25f, 0.2f);
            model.LegRendererL.sharedMaterial = model.LegRendererR.sharedMaterial = Mat.Get(legColor);
            var helm = Inventory.GetEquipped(EquipSlot.Helm);
            model.Helm.gameObject.SetActive(helm != null);
            Color helmColor = helm != null ? Color.Lerp(helm.IconColor, Item.RarityColor(helm.Rarity), 0.3f) : Color.clear;
            if (helm != null) model.HelmRenderer.sharedMaterial = Mat.Get(helmColor);

            BodyHex = Item.Hex(chestColor);
            LegsHex = Item.Hex(legColor);
            WeaponHex = weapon != null ? Item.Hex(weapon.Rarity >= Rarity.Rare ? Item.RarityColor(weapon.Rarity) : weapon.IconColor) : "";
            HelmHex = helm != null ? Item.Hex(helmColor) : "";
        }

        public void SpendStatPoint(Stat s)
        {
            if (StatPoints <= 0) return;
            StatPoints--;
            switch (s)
            {
                case Stat.Strength: Strength++; break;
                case Stat.Dexterity: Dexterity++; break;
                case Stat.Intelligence: Intelligence++; break;
                case Stat.Vitality: Vitality++; Health += 6; break;
            }
            RecalculateStats();
        }

        public void AddXp(int amount)
        {
            if (amount <= 0) return;
            Xp += amount;
            GameUI.Float(transform.position + Vector3.up * 2.9f, "+" + amount + " XP", new Color(0.75f, 0.5f, 1f), 0.9f);
            while (Xp >= XpToNext)
            {
                Xp -= XpToNext;
                Level++;
                StatPoints += 5;
                RecalculateStats();
                Health = MaxHealth;
                Mana = MaxMana;
                GameUI.Banner("LEVEL UP!  You are now level " + Level, new Color(1f, 0.85f, 0.2f));
                Sfx.Play2D("levelup", 0.8f);
                GameUI.Log("You have reached level " + Level + "! You have " + StatPoints + " attribute points to spend (C).", new Color(1f, 0.85f, 0.2f));
                SpellFx.LevelUp(transform.position);
                foreach (var a in Kit)
                    if (a.RequiredLevel == Level) GameUI.Log("New ability unlocked: " + a.Name + " [" + a.Key + "]", a.Color);
                GameUI.Log("You gained a talent point (T).", new Color(0.75f, 0.6f, 1f));
                NetClient.I?.SendFx("levelup", transform.position, transform.position);
                NetClient.I?.SaveNow();
            }
        }

        public void AddGold(int amount)
        {
            if (amount == 0) return;
            Gold += amount;
            if (amount > 0) GameUI.Float(transform.position + Vector3.up * 2.4f, "+" + amount + " gold", new Color(1f, 0.85f, 0.2f), 0.8f);
        }

        // =====================================================================================
        // Main loop
        // =====================================================================================

        void Update()
        {
            if (IsDead)
            {
                if (model != null)
                    model.Root.localRotation = Quaternion.Slerp(model.Root.localRotation, Quaternion.Euler(-90, 0, 0), Time.deltaTime * 5f);
                return;
            }

            float dt = Time.deltaTime;
            Health = Mathf.Min(MaxHealth, Health + HealthRegen * dt);
            Mana = Mathf.Min(MaxMana, Mana + ManaRegen * dt);

            UpdateHover();
            HandleInput();
            UpdateChannels(dt);
            if (leapT < 0f)
            {
                UpdateAction(dt);
                FollowPath(dt);
            }
            AutoPickupGold();
            UpdateZone();

            float atk = -1f;
            if (attackAnim >= 0f)
            {
                attackAnim += dt * Mathf.Max(2.2f, AttackSpeed * 2.4f);
                atk = attackAnim;
                if (attackAnim >= 1f) attackAnim = -1f;
            }
            if (view != null) view.UpdateLocomotion(currentSpeed);
            else model?.Animate(Mathf.Clamp01(currentSpeed / MoveSpeed), atk, dt);
            Footsteps(currentSpeed, dt);
            if (castAnim >= 0f)
            {
                castAnim += dt * 3.5f;
                if (view == null) model?.CastPose(Mathf.Clamp01(castAnim));
                if (castAnim >= 1f) castAnim = -1f;
            }
            // Brighter and wider at night: the hero's torch is the main light source in the dark.
            torch.intensity = (1.5f + Mathf.PerlinNoise(Time.time * 3f, 0f) * 0.4f) * Mathf.Lerp(0.6f, 1.3f, DayNight.Night);
            torch.range = Mathf.Lerp(11f, 16f, DayNight.Night);
        }

        void UpdateHover()
        {
            HoveredEnemy = null;
            HoveredInteractable = null;
            var cam = GameManager.I.Cam;
            Ray ray = cam.ScreenPointToRay(GameInput.MousePosition);
            if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float d)) MouseGround = ray.GetPoint(d);

            if (GameUI.I != null && GameUI.I.MouseOverUI) return;

            float bestEnemy = float.MaxValue, bestInter = float.MaxValue;
            var hits = Physics.RaycastAll(ray, 400f);
            foreach (var h in hits)
            {
                var e = h.collider.GetComponentInParent<Enemy>();
                if (e != null && !e.IsDead && h.distance < bestEnemy) { bestEnemy = h.distance; HoveredEnemy = e; continue; }
                var it = h.collider.GetComponentInParent<Interactable>();
                if (it != null && it.CanInteract && h.distance < bestInter) { bestInter = h.distance; HoveredInteractable = it; }
            }

            // Generous fallback so small monsters are easy to click.
            if (HoveredEnemy == null)
            {
                float best = 1.1f;
                foreach (var c in All)
                {
                    if (c.IsDead || c.Faction == Faction.Player) continue;
                    float dist = Factory.FlatDistance(c.transform.position, MouseGround);
                    if (dist < best + c.Radius) { best = dist - c.Radius; HoveredEnemy = c as Enemy; }
                }
            }
        }

        void HandleInput()
        {
            bool overUI = GameUI.I != null && GameUI.I.MouseOverUI;
            bool blockedByWindow = GameUI.I != null && GameUI.I.BlocksWorldInput;

            if (!GameInput.LeftHeld) holdMoving = false;

            if (!overUI && !blockedByWindow)
            {
                if (GameInput.LeftDown)
                {
                    standAttack = GameInput.Held(GKey.Shift);
                    if (standAttack)
                    {
                        standAttackPoint = MouseGround;
                        StopMoving();
                        action = Action.None;
                    }
                    else if (HoveredEnemy != null) SetAttackTarget(HoveredEnemy);
                    else if (HoveredInteractable != null) SetInteract(HoveredInteractable);
                    else
                    {
                        MoveTo(MouseGround);
                        holdMoving = true;
                    }
                }
                else if (GameInput.LeftHeld)
                {
                    if (standAttack) standAttackPoint = MouseGround;
                    else if (holdMoving && Time.time >= holdMoveAt)
                    {
                        holdMoveAt = Time.time + 0.12f;
                        MoveTo(MouseGround);
                    }
                }

                if (GameInput.RightHeld) CastAbility(1, MouseGround, true);
            }

            if (!GameInput.LeftHeld) standAttack = false;
            if (GameUI.I != null && GameUI.I.KeyboardCaptured) return;

            if (GameInput.Down(GKey.Alpha1)) CastAbility(0, MouseGround);
            if (GameInput.Down(GKey.Alpha2)) CastAbility(1, MouseGround);
            if (GameInput.Down(GKey.Alpha3)) CastAbility(2, MouseGround);
            if (GameInput.Down(GKey.Alpha4)) CastAbility(3, MouseGround);
            if (GameInput.Down(GKey.Alpha5)) CastAbility(4, MouseGround);
            if (GameInput.Down(GKey.Q)) UseItemByName("Health Potion");
            if (GameInput.Down(GKey.E)) UseItemByName("Mana Potion");
        }

        // =====================================================================================
        // Actions
        // =====================================================================================

        public void MoveTo(Vector3 point)
        {
            action = Action.Move;
            AttackTarget = null;
            interactTarget = null;
            StopGathering();
            SetPath(point);
        }

        public void SetAttackTarget(Combatant target)
        {
            StopGathering();
            action = Action.Attack;
            AttackTarget = target;
            interactTarget = null;
            repathAt = 0f;
        }

        public void SetInteract(Interactable target)
        {
            StopGathering();
            action = Action.Interact;
            interactTarget = target;
            AttackTarget = null;
            SetPath(target.Position);
        }

        void SetPath(Vector3 goal)
        {
            pathGoal = goal;
            WorldGrid.Instance.FindPath(transform.position, goal, path);
        }

        void StopMoving() => path.Clear();

        /// <summary>Instantly moves the hero (entering or leaving a dungeon).</summary>
        public void TeleportTo(Vector3 pos)
        {
            path.Clear();
            action = Action.None;
            AttackTarget = null;
            StopGathering();
            transform.position = pos;
            CameraRig.I?.SnapToTarget();
            SpellFx.Ring(pos, new Color(0.7f, 0.6f, 1f), 1.5f, 0.6f);
        }

        void UpdateAction(float dt)
        {
            if (Time.time < castLockUntil) return;

            if (standAttack)
            {
                Factory.Face(transform, standAttackPoint, 0.4f);
                if (Time.time >= nextAttackTime) PerformAttack(null, standAttackPoint);
                return;
            }

            switch (action)
            {
                case Action.Attack:
                    if (AttackTarget == null || AttackTarget.IsDead) { action = Action.None; AttackTarget = null; StopMoving(); break; }
                    float dist = Factory.FlatDistance(transform.position, AttackTarget.transform.position) - AttackTarget.Radius;
                    if (dist > AttackRange)
                    {
                        if (Time.time >= repathAt)
                        {
                            repathAt = Time.time + 0.25f;
                            SetPath(AttackTarget.transform.position);
                        }
                    }
                    else
                    {
                        StopMoving();
                        Factory.Face(transform, AttackTarget.transform.position, 0.5f);
                        if (Time.time >= nextAttackTime) PerformAttack(AttackTarget, AttackTarget.transform.position);
                    }
                    break;

                case Action.Interact:
                    if (interactTarget == null || !interactTarget.CanInteract) { action = Action.None; break; }
                    if (Factory.FlatDistance(transform.position, interactTarget.Position) <= interactTarget.InteractRange)
                    {
                        StopMoving();
                        Factory.Face(transform, interactTarget.Position);
                        var target = interactTarget;
                        action = Action.None;
                        interactTarget = null;
                        target.Interact(this);
                    }
                    else if (path.Count == 0)
                    {
                        // Re-path once in case the target moved; give up if still unreachable.
                        SetPath(interactTarget.Position);
                        if (path.Count == 0)
                        {
                            GameUI.Log("I can't reach that.", Color.gray);
                            action = Action.None;
                        }
                    }
                    break;

                case Action.Gather:
                    UpdateGathering(dt);
                    break;

                case Action.Move:
                    if (path.Count == 0) action = Action.None;
                    break;
            }
        }

        void FollowPath(float dt)
        {
            currentSpeed = 0f;
            if (path.Count == 0 || Time.time < castLockUntil) return;
            Vector3 target = path[0];
            Vector3 to = Factory.Flat(target - transform.position);
            float step = MoveSpeed * dt;
            if (to.magnitude <= step)
            {
                transform.position = new Vector3(target.x, 0f, target.z);
                path.RemoveAt(0);
            }
            else
            {
                transform.position += to.normalized * step;
            }
            currentSpeed = MoveSpeed;
            if (to.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), dt * 15f);
        }

        void PerformAttack(Combatant target, Vector3 point)
        {
            if (Whirling) return; // the spin does the hitting
            nextAttackTime = Time.time + 1f / Mathf.Max(0.2f, AttackSpeed);
            AnimAttack();
            Factory.Face(transform, point);

            Combatant victim = target;
            if (victim == null)
            {
                // Stand-still attack: hit whatever is in front of us.
                Overlap(transform.position + transform.forward * 1.2f, 1.2f, Faction, buffer);
                if (buffer.Count > 0) victim = buffer[0];
            }
            if (victim == null) return;

            bool crit = RollCrit();
            DealDamage(victim, WeaponHit(1f), crit);
            FxPulse.Sparks(victim.Center, crit ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.95f, 0.85f), crit ? 8 : 4);
        }

        // =====================================================================================
        // Abilities (each class has its own kit of five, see ClassKits)
        // =====================================================================================

        /// <summary>Ranks spent in a talent of this hero's class.</summary>
        public int Tal(string id) => Talents.TryGetValue(id, out var r) ? r : 0;
        public int TalentPointsTotal => Mathf.Max(0, Level - 1);
        public int TalentPointsSpent { get { int n = 0; foreach (var kv in Talents) n += kv.Value; return n; } }
        public int TalentPoints => Mathf.Max(0, TalentPointsTotal - TalentPointsSpent);

        public bool LearnTalent(TalentDef t)
        {
            if (TalentPoints <= 0 || Tal(t.Id) >= t.MaxRank) return false;
            Talents[t.Id] = Tal(t.Id) + 1;
            RecalculateStats();
            Sfx.Play2D("ui_confirm", 0.5f);
            return true;
        }

        public void ResetTalents()
        {
            Talents.Clear();
            RecalculateStats();
        }

        public void AddBuff(Buff b)
        {
            Buffs.RemoveAll(x => x.Name == b.Name);
            Buffs.Add(b);
            RecalculateStats();
        }

        public bool HasPower(string id) => ItemPowers.Has(this, id);
        public float PowerValue(string id) => ItemPowers.Value(this, id);

        public bool HasBuff(string name)
        {
            foreach (var b in Buffs) if (b.Name == name && Time.time < b.Until) return true;
            return false;
        }

        float BuffDamage { get { float m = 1f; foreach (var b in Buffs) m *= b.DamageMul; return m; } }

        /// <summary>Holy power for the Knight's spells (Strength and Intelligence both count).</summary>
        public float HolyMultiplier => (1f + (TotStr + TotInt) / 80f) * (1f + (Level - 1) * 0.06f) * (1f + 0.06f * Tal("righteous")) * BuffDamage * (HasPower("set_lightbringer") ? 1.4f : 1f);

        float WeaponHit(float mul) => Random.Range(MinDamage, MaxDamage) * MeleeMultiplier * mul;

        bool RollCrit(float bonus = 0f) => Random.value * 100f < CritChance + bonus;

        public float CritDamage => 2f + 0.12f * Tal("lethality");

        /// <summary>Deals one hit of player damage: crits, life on hit, Bloodlust, legendary procs.</summary>
        public void DealDamage(Combatant c, float dmg, bool crit)
        {
            if (c == null || c.IsDead) return;
            if (c is Enemy e && e.Elite) dmg *= 1f + PowerValue("elitebane");
            c.TakeDamage(dmg * (crit ? CritDamage : 1f), this, crit);
            OnHitDealt(c);
        }

        void OnHitDealt(Combatant c)
        {
            if (LifeOnHit > 0) Heal(LifeOnHit, false);
            int blood = Tal("bloodlust");
            if (blood > 0) Heal(2f * blood, false);
            ItemPowers.OnHit(this, c);
        }

        /// <summary>The nearest living enemy within range of a point (null if none).</summary>
        Combatant NearestEnemy(Vector3 pos, float range, ICollection<Combatant> exclude = null)
        {
            Combatant best = null;
            float bestD = range;
            foreach (var c in All)
            {
                if (c.IsDead || c.Faction == Faction || (exclude != null && exclude.Contains(c))) continue;
                float d = Factory.FlatDistance(c.transform.position, pos) - c.Radius;
                if (d < bestD) { bestD = d; best = c; }
            }
            return best;
        }

        /// <summary>Clamps a target point to <paramref name="range"/> and to somewhere the hero can actually walk to.</summary>
        bool ReachablePoint(Vector3 aim, float range, out Vector3 point)
        {
            var from = transform.position;
            var dir = Factory.Flat(aim - from);
            float dist = Mathf.Min(dir.magnitude, range);
            dir = dir.sqrMagnitude > 0.001f ? dir.normalized : transform.forward;
            for (float d = dist; d >= 1f; d -= 0.5f)
            {
                var p = from + dir * d;
                if (!WorldGrid.Instance.IsWalkable(p)) continue;
                if (WorldGrid.Instance.FindPath(from, p, probe, 4000) && PathLength(probe, from) <= range * 1.8f) { point = new Vector3(p.x, 0f, p.z); return true; }
            }
            point = from;
            return false;
        }

        static readonly List<Vector3> probe = new List<Vector3>();

        static float PathLength(List<Vector3> p, Vector3 from)
        {
            float len = 0f;
            var prev = from;
            foreach (var v in p) { len += Factory.FlatDistance(prev, v); prev = v; }
            return len;
        }

        public void CastAbility(int index, Vector3 aim, bool silent = false)
        {
            if (IsDead || index < 0 || index >= Kit.Length || leapT >= 0f) return;
            var a = Kit[index];
            if (Level < a.RequiredLevel)
            {
                if (!silent) GameUI.Float(transform.position + Vector3.up * 2.5f, a.Name + " unlocks at level " + a.RequiredLevel, Color.gray, 0.8f);
                return;
            }
            if (Time.time < CooldownEnd[index] || Time.time < globalCooldownEnd) return;
            if (Mana < a.ManaCost)
            {
                if (!silent || Time.frameCount % 30 == 0) GameUI.Float(transform.position + Vector3.up * 2.5f, "Not enough mana", new Color(0.4f, 0.6f, 1f), 0.8f);
                if (!silent) Sfx.Play2D("ui_error", 0.4f);
                return;
            }

            // Abilities that need a valid destination check it before spending anything.
            Vector3 dest = aim;
            if ((a.Id == AbilityId.Teleport || a.Id == AbilityId.Leap) && !ReachablePoint(aim, a.Id == AbilityId.Teleport ? 12f : 10f, out dest))
            {
                if (!silent) GameUI.Float(transform.position + Vector3.up * 2.5f, "Can't go there", Color.gray, 0.8f);
                return;
            }

            Mana -= a.ManaCost;
            float cd = a.Cooldown;
            if (a.Id == AbilityId.Teleport) cd -= 1.2f * Tal("blink");
            CooldownEnd[index] = Time.time + Mathf.Max(0.3f, cd);
            globalCooldownEnd = Time.time + 0.3f;
            StopGathering();
            if (action == Action.Move || action == Action.Interact) { action = Action.None; StopMoving(); }
            if (a.Id != AbilityId.Whirlwind) castLockUntil = Time.time + 0.2f;
            Factory.Face(transform, aim);
            var pos = transform.position;
            var net = NetClient.I;

            switch (a.Id)
            {
                // ---------------------------------------------------------------- Barbarian (and the old kit)
                case AbilityId.Cleave:
                {
                    attackAnim = 0f;
                    view?.Action("2H_Melee_Attack_Spin", 0.55f);
                    Sfx.Play("swing_heavy", pos + Vector3.up, 0.7f);
                    Overlap(pos, 3.4f, Faction, buffer);
                    foreach (var c in buffer.ToArray())
                    {
                        Vector3 to = Factory.Flat(c.transform.position - pos);
                        if (to.sqrMagnitude > 0.01f && Vector3.Angle(transform.forward, to) > 70f) continue;
                        DealDamage(c, WeaponHit(1.7f), RollCrit());
                    }
                    SpellFx.Cleave(pos, transform.rotation, 3.4f, a.Color);
                    net?.SendFx("cleave", pos, pos + transform.forward);
                    break;
                }
                case AbilityId.ThrowingAxe:
                {
                    view?.Shoot();
                    AbilityFx.ThrowingAxe(pos);
                    bool crit = RollCrit();
                    Projectile.Fire(this, pos + Vector3.up * 1.2f + transform.forward * 0.6f, new Vector3(aim.x, 1.2f, aim.z), 22f,
                        WeaponHit(1.1f) * (crit ? CritDamage : 1f), AbilityFx.Steel, 0.4f, 0f, 18f, crit, OnHitDealt).WithTrail(SpellFx.Trail.Arrow);
                    net?.SendFx("axe", pos, aim);
                    break;
                }
                case AbilityId.Whirlwind:
                {
                    whirlUntil = Time.time + 2.5f + 0.7f * Tal("cyclone") + (HasPower("set_ancients") ? 1f : 0f);
                    nextWhirlTick = 0f;
                    Sfx.Play("swing_heavy", pos + Vector3.up, 0.8f);
                    break;
                }
                case AbilityId.Leap:
                {
                    leapFrom = pos;
                    leapTo = dest;
                    leapT = 0f;
                    path.Clear();
                    view?.Action("2H_Melee_Attack_Spin", 0.6f);
                    Sfx.Play("swing_heavy", pos + Vector3.up, 0.6f, 0.1f);
                    break;
                }
                case AbilityId.WarCry:
                {
                    AnimRaise();
                    int r = Tal("battlerage");
                    AddBuff(new Buff { Name = "War Cry", Icon = "war_cry", Until = Time.time + 10f + 2f * r, DamageMul = 1.4f + 0.1f * r, ArmorMul = 1.3f, Color = a.Color });
                    Heal(MaxHealth * 0.15f);
                    AbilityFx.WarCry(pos);
                    net?.SendFx("warcry", pos, pos);
                    break;
                }

                // ---------------------------------------------------------------- Knight
                case AbilityId.ShieldBash:
                {
                    attackAnim = 0f;
                    view?.Action("1H_Melee_Attack_Slice_Diagonal", 0.45f);
                    int r = Tal("bash");
                    Overlap(pos + transform.forward * 1.1f, 1.6f, Faction, buffer);
                    foreach (var c in buffer.ToArray())
                    {
                        DealDamage(c, WeaponHit(1.4f * (1f + 0.15f * r)), RollCrit());
                        if (c is Enemy e) e.Stun(1.5f + 0.4f * r);
                    }
                    AbilityFx.ShieldBash(pos, aim);
                    net?.SendFx("bash", pos, aim);
                    break;
                }
                case AbilityId.HolyBolt:
                {
                    AnimCast(a.Color);
                    AbilityFx.HolyBolt(pos, aim);
                    bool crit = RollCrit();
                    float dmg = (9f + (MinDamage + MaxDamage) * 0.3f) * HolyMultiplier * Random.Range(0.9f, 1.1f) * (crit ? CritDamage : 1f);
                    Projectile.Fire(this, pos + Vector3.up * 1.2f + transform.forward * 0.6f, new Vector3(aim.x, 1.2f, aim.z), 22f, dmg, a.Color, 0.45f, 0f, 20f, crit,
                        c => { Heal(MaxHealth * 0.03f, false); OnHitDealt(c); }).WithTrail(SpellFx.Trail.Magic);
                    net?.SendFx("holybolt", pos, aim);
                    break;
                }
                case AbilityId.Consecration:
                {
                    AnimRaise();
                    int r = Tal("consecrate");
                    float radius = 4f + 0.5f * r;
                    GroundEffect.Spawn(GroundEffect.Kind.Consecration, this, pos, radius, 6f + 1.5f * r, 4.5f * HolyMultiplier, MaxHealth * 0.012f);
                    AbilityFx.Consecration(pos);
                    net?.SendFx("consecrate", pos, pos);
                    break;
                }
                case AbilityId.DivineShield:
                {
                    AnimRaise();
                    AddBuff(new Buff { Name = "Divine Shield", Icon = "divine_shield", Until = Time.time + 6f, DamageTakenMul = 0.5f, Color = a.Color });
                    Heal(MaxHealth * (0.2f + 0.07f * Tal("layonhands")));
                    AbilityFx.DivineShield(pos);
                    net?.SendFx("dshield", pos, pos);
                    break;
                }
                case AbilityId.Judgement:
                {
                    AnimCast(a.Color);
                    var target = Factory.FlatDistance(pos, aim) > 14f ? pos + Factory.Flat(aim - pos).normalized * 14f : aim;
                    target.y = 0f;
                    MeteorFx.CastJudgement(this, target, 45f * HolyMultiplier, 3.5f, 1.2f);
                    net?.SendFx("judgement", pos, target);
                    break;
                }

                // ---------------------------------------------------------------- Mage
                case AbilityId.ChainLightning:
                {
                    AnimCast(a.Color);
                    AbilityFx.ChainCast(pos);
                    var first = HoveredEnemy != null && !HoveredEnemy.IsDead && Factory.FlatDistance(pos, HoveredEnemy.transform.position) < 16f
                        ? HoveredEnemy : NearestEnemy(aim, 4f) ?? NearestEnemy(pos + transform.forward * 5f, 7f);
                    var hand = view != null && view.Hand != null ? view.Hand.position : pos + Vector3.up * 1.4f;
                    if (first == null)
                    {
                        AbilityFx.Lightning(hand, new Vector3(aim.x, 0.2f, aim.z));
                        break;
                    }
                    bool tempest = HasPower("set_tempest");
                    float dmg = 11f * SpellMultiplier * (tempest ? 1.3f : 1f);
                    var hit = new List<Combatant>();
                    Combatant cur = first;
                    var from = hand;
                    for (int i = 0; i < (tempest ? 8 : 5) && cur != null; i++)
                    {
                        AbilityFx.Lightning(from, cur.Center);
                        DealDamage(cur, dmg * Random.Range(0.9f, 1.1f), RollCrit());
                        hit.Add(cur);
                        from = cur.Center;
                        dmg *= 0.85f;
                        cur = NearestEnemy(cur.transform.position, 6f, hit);
                    }
                    Sfx.Play("hit_heavy", first.Center, 0.4f, 0.2f);
                    net?.SendFx("chain", pos, first.transform.position);
                    break;
                }
                case AbilityId.Fireball:
                {
                    AnimCast(a.Color);
                    Sfx.Play("fire_cast", pos + Vector3.up, 0.6f, 0.1f);
                    int r = Tal("pyromancy");
                    bool crit = RollCrit();
                    float dmg = 14f * SpellMultiplier * (1f + 0.1f * r) * Random.Range(0.9f, 1.1f) * (crit ? CritDamage : 1f);
                    int count = HasPower("fireball_split") ? 3 : 1;
                    for (int i = 0; i < count; i++)
                    {
                        var dir = Quaternion.Euler(0f, (i - (count - 1) / 2f) * 14f, 0f) * Factory.Flat(aim - pos);
                        Projectile.Fire(this, pos + Vector3.up * 1.2f + transform.forward * 0.6f, pos + dir + Vector3.up * 1.2f, 20f, dmg, a.Color,
                            0.5f, 2.2f + 0.4f * r, 22f, crit, OnHitDealt).WithTrail(SpellFx.Trail.Fire);
                    }
                    net?.SendFx("fireball", pos, aim);
                    break;
                }
                case AbilityId.FrostNova:
                {
                    AnimCast(a.Color);
                    Sfx.Play("frost_cast", pos, 0.8f);
                    Sfx.Play("shatter", pos, 0.5f);
                    int r = Tal("deepfreeze");
                    Overlap(pos, 6f, Faction, buffer);
                    foreach (var c in buffer.ToArray())
                    {
                        DealDamage(c, 12f * SpellMultiplier * (1f + 0.15f * r) * Random.Range(0.9f, 1.1f), false);
                        if (c is Enemy e) e.Slow(4f + r);
                    }
                    SpellFx.FrostNova(pos, 6f);
                    net?.SendFx("nova", pos, pos);
                    break;
                }
                case AbilityId.Teleport:
                {
                    AbilityFx.Teleport(pos, dest);
                    net?.SendFx("teleport", pos, dest);
                    path.Clear();
                    transform.position = dest;
                    CameraRig.I?.SnapToTarget();
                    break;
                }
                case AbilityId.Heal:
                {
                    AnimCast(a.Color);
                    Sfx.Play("holy_cast", pos, 0.7f, 0.02f);
                    Heal(MaxHealth * 0.35f + TotInt * 3f);
                    SpellFx.HolyLight(pos);
                    net?.SendFx("heal", pos, pos);
                    break;
                }
                case AbilityId.Meteor:
                {
                    AnimCast(a.Color);
                    float range = Factory.FlatDistance(pos, aim);
                    Vector3 target = range > 16f ? pos + Factory.Flat(aim - pos).normalized * 16f : aim;
                    MeteorFx.Cast(this, new Vector3(target.x, 0f, target.z), 70f * SpellMultiplier);
                    net?.SendFx("meteor", pos, target);
                    break;
                }

                // ---------------------------------------------------------------- Rogue
                case AbilityId.TwinStrike:
                {
                    var target = AttackTarget != null && !AttackTarget.IsDead && Factory.FlatDistance(pos, AttackTarget.transform.position) < 3.2f ? AttackTarget
                        : HoveredEnemy != null && Factory.FlatDistance(pos, HoveredEnemy.transform.position) < 3.2f ? HoveredEnemy
                        : NearestEnemy(pos + transform.forward * 1.2f, 1.8f);
                    attackAnim = 0f;
                    view?.Attack(0.35f);
                    AbilityFx.TwinStrike(pos, aim);
                    net?.SendFx("twin", pos, aim);
                    if (target == null) break;
                    Factory.Face(transform, target.transform.position);
                    DealDamage(target, WeaponHit(0.9f), RollCrit(15f));
                    StartCoroutine(SecondStrike(target));
                    break;
                }
                case AbilityId.Multishot:
                {
                    view?.Shoot();
                    Sfx.Play("swing", pos + Vector3.up, 0.5f, 0.25f);
                    int arrows = 5 + Tal("volley") + (HasPower("multishot_plus") ? 3 : 0);
                    var dir = Factory.Flat(aim - pos);
                    if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
                    for (int i = 0; i < arrows; i++)
                    {
                        var d = Quaternion.Euler(0f, (i - (arrows - 1) / 2f) * 10f, 0f) * dir.normalized;
                        bool crit = RollCrit();
                        Projectile.Fire(this, pos + Vector3.up * 1.2f + d * 0.6f, pos + Vector3.up * 1.2f + d * 10f, 26f,
                            WeaponHit(0.75f * (HasPower("set_nightstalker") ? 1.5f : 1f)) * (crit ? CritDamage : 1f), AbilityFx.Steel, 0.25f, 0f, 16f, crit, OnHitDealt).WithTrail(SpellFx.Trail.Arrow);
                    }
                    net?.SendFx("multi", pos, aim);
                    break;
                }
                case AbilityId.FanOfKnives:
                {
                    attackAnim = 0f;
                    view?.Action("2H_Melee_Attack_Spin", 0.4f);
                    int r = Tal("venom");
                    Overlap(pos, 5f, Faction, buffer);
                    foreach (var c in buffer.ToArray())
                    {
                        DealDamage(c, WeaponHit(1.2f * (1f + 0.15f * r)), RollCrit());
                        if (c is Enemy e) e.Slow(3f + r);
                    }
                    AbilityFx.FanOfKnives(pos, 5f);
                    net?.SendFx("knives", pos, pos);
                    break;
                }
                case AbilityId.SmokeBomb:
                {
                    float dur = 4f + Tal("shadowstep");
                    AddBuff(new Buff { Name = "Vanished", Icon = "smoke_bomb", Until = Time.time + dur, Color = a.Color });
                    net?.SendVanish(dur);
                    Heal(MaxHealth * 0.1f);
                    AbilityFx.SmokeBomb(pos);
                    net?.SendFx("smoke", pos, pos);
                    if (action == Action.Attack) { action = Action.None; AttackTarget = null; }
                    break;
                }
                case AbilityId.RainOfArrows:
                {
                    view?.Shoot();
                    var target = Factory.FlatDistance(pos, aim) > 16f ? pos + Factory.Flat(aim - pos).normalized * 16f : aim;
                    target.y = 0f;
                    GroundEffect.Spawn(GroundEffect.Kind.RainOfArrows, this, target, 3.5f, 3f, WeaponHit(0.4f * (HasPower("set_nightstalker") ? 1.5f : 1f)));
                    AbilityFx.RainOfArrows(target, 3.5f);
                    net?.SendFx("rain", pos, target);
                    break;
                }
            }
            ItemPowers.OnCast(this, a.Id, aim);
        }

        System.Collections.IEnumerator SecondStrike(Combatant target)
        {
            yield return new WaitForSeconds(0.18f);
            if (target == null || target.IsDead || IsDead) yield break;
            Sfx.Play("swing", transform.position + Vector3.up, 0.5f, 0.2f);
            DealDamage(target, WeaponHit(0.9f), RollCrit(15f));
        }

        void AnimRaise()
        {
            castAnim = 0f;
            view?.Action("Spellcast_Raise", 0.7f);
        }

        /// <summary>Whirlwind ticks and the Leap arc, run every frame.</summary>
        void UpdateChannels(float dt)
        {
            Buffs.RemoveAll(b => Time.time >= b.Until && ExpireBuff(b));
            if (RecalculateStatsDeferred) { RecalculateStatsDeferred = false; RecalculateStats(); }

            if (Time.time < whirlUntil && Time.time >= nextWhirlTick)
            {
                nextWhirlTick = Time.time + 0.3f;
                view?.Action("2H_Melee_Attack_Spin", 0.3f);
                var pos = transform.position;
                Overlap(pos, 3f, Faction, buffer);
                float mul = 0.6f * (1f + 0.1f * Tal("cyclone")) * (HasPower("set_ancients") ? 1.6f : 1f);
                foreach (var c in buffer.ToArray()) DealDamage(c, WeaponHit(mul), RollCrit());
                if (HasPower("whirl_slow"))
                    foreach (var c in buffer)
                        if (c is Enemy e) e.Slow(1.5f);
                AbilityFx.Whirl(pos, transform.rotation);
                Sfx.Play("swing", pos + Vector3.up, 0.45f, 0.2f);
                if (Time.frameCount % 2 == 0) NetClient.I?.SendFx("whirl", pos, pos + transform.forward);
            }

            if (leapT >= 0f)
            {
                leapT += dt / 0.5f;
                float t = Mathf.Clamp01(leapT);
                var p = Vector3.Lerp(leapFrom, leapTo, t);
                p.y = Mathf.Sin(t * Mathf.PI) * 2.6f;
                transform.position = p;
                if (t >= 1f)
                {
                    leapT = -1f;
                    transform.position = leapTo;
                    int r = Tal("earthshaker");
                    float radius = 3f + 0.6f * r;
                    Overlap(leapTo, radius, Faction, buffer);
                    foreach (var c in buffer.ToArray())
                    {
                        DealDamage(c, WeaponHit(2f * (1f + 0.15f * r)), RollCrit());
                        if (c is Enemy e) e.Slow(2f);
                    }
                    AbilityFx.LeapLand(leapTo, radius);
                    NetClient.I?.SendFx("leap", leapFrom, leapTo);
                }
            }
        }

        bool ExpireBuff(Buff b)
        {
            GameUI.Log(b.Name + " fades.", Color.gray);
            RecalculateStatsDeferred = true;
            return true;
        }

        bool RecalculateStatsDeferred;

        // =====================================================================================
        // Items
        // =====================================================================================

        public void UseItemByName(string name)
        {
            int idx = Inventory.IndexOf(name);
            if (idx < 0)
            {
                GameUI.Float(transform.position + Vector3.up * 2.5f, "No " + name + "s left!", Color.gray, 0.8f);
                return;
            }
            UseItem(idx);
        }

        /// <summary>Left-click behaviour for an inventory slot: equip equipment, consume consumables.</summary>
        public void UseItem(int index)
        {
            var item = Inventory.Slots[index];
            if (item == null || IsDead) return;
            if (item.Kind == ItemKind.Equipment) { Equip(index); return; }
            if (item.Kind != ItemKind.Consumable) return;

            if (item.HealAmount > 0 && Health >= MaxHealth && item.ManaAmount <= 0)
            {
                GameUI.Float(transform.position + Vector3.up * 2.5f, "Already at full health", Color.gray, 0.8f);
                return;
            }
            if (item.ManaAmount > 0 && Mana >= MaxMana && item.HealAmount <= 0)
            {
                GameUI.Float(transform.position + Vector3.up * 2.5f, "Already at full mana", Color.gray, 0.8f);
                return;
            }
            var used = Inventory.TakeOne(index);
            Sfx.Play2D("potion", 0.6f, Random.Range(0.92f, 1.08f));
            if (used.HealAmount > 0)
            {
                Heal(used.HealAmount + MaxHealth * 0.1f);
                FxPulse.Ring(transform.position, new Color(0.9f, 0.2f, 0.2f), 1.5f, 0.35f);
            }
            if (used.ManaAmount > 0)
            {
                RestoreMana(used.ManaAmount + MaxMana * 0.1f);
                FxPulse.Ring(transform.position, new Color(0.2f, 0.4f, 1f), 1.5f, 0.35f);
            }
        }

        // =====================================================================================
        // Companions
        // =====================================================================================

        public bool OwnsCompanion(string id) => OwnedCompanions.Contains(id);

        /// <summary>Buys a companion from Beastmaster Orla; it starts following right away.</summary>
        public bool HireCompanion(CompanionDef def)
        {
            if (OwnsCompanion(def.Id)) return false;
            if (Level < def.RequiredLevel) { GameUI.Log(def.Name + " won't follow anyone below level " + def.RequiredLevel + ".", new Color(1f, 0.4f, 0.4f)); return false; }
            if (Gold < def.Price) { GameUI.Log("You need " + def.Price + " gold to hire " + def.Name + ".", new Color(1f, 0.4f, 0.4f)); return false; }
            Gold -= def.Price;
            OwnedCompanions.Add(def.Id);
            Sfx.Play2D("coins", 0.6f);
            GameUI.Log(def.Name + " joins you!", def.Color);
            SummonCompanion(def.Id);
            NetClient.I?.SaveNow();
            return true;
        }

        public void SummonCompanion(string id, bool silent = false)
        {
            var def = CompanionDef.Get(id);
            if (def == null || !OwnsCompanion(id)) return;
            DismissCompanion(true);
            ActiveCompanion = id;
            CompanionInstance = Companion.Spawn(def, transform, true);
            if (!silent) Sfx.Play2D("ui_confirm", 0.5f);
        }

        public void DismissCompanion(bool silent = false)
        {
            if (CompanionInstance != null) CompanionInstance.Dismiss();
            CompanionInstance = null;
            if (!silent && ActiveCompanion != null) GameUI.Log("Your companion waits for you in Hollowmere.", Color.gray);
            ActiveCompanion = null;
        }

        void OnDestroy()
        {
            if (CompanionInstance != null) Destroy(CompanionInstance.gameObject);
        }

        /// <summary>Puts the gem at bag index <paramref name="gemIndex"/> into the first empty socket of <paramref name="target"/>.</summary>
        public bool SocketGem(int gemIndex, Item target)
        {
            var gem = gemIndex >= 0 && gemIndex < Inventory.Slots.Length ? Inventory.Slots[gemIndex] : null;
            if (gem == null || gem.Kind != ItemKind.Gem || target == null || target.Kind != ItemKind.Equipment) return false;
            if (target.Gems == null) target.Gems = new List<string>();
            if (target.Gems.Count >= target.Sockets)
            {
                GameUI.Log(target.Sockets == 0 ? target.Name + " has no sockets." : target.Name + " has no empty sockets.", new Color(1f, 0.4f, 0.4f));
                return false;
            }
            Inventory.TakeOne(gemIndex);
            target.Gems.Add(gem.Name);
            Sfx.Play2D("anvil", 0.5f, 1.3f);
            GameUI.Log("You socket the " + gem.Name + " into " + target.Name + ".", gem.IconColor);
            Inventory.NotifyChanged();
            return true;
        }

        /// <summary>Vex the curio dealer fuses three gems of a kind into one of the next quality.</summary>
        public void CombineGems()
        {
            for (int tier = 0; tier < 2; tier++)
                foreach (var type in ItemPowers.GemTypes)
                {
                    string name = ItemPowers.GemTiers[tier] + " " + type;
                    if (Inventory.CountOf(name) < 3) continue;
                    int cost = tier == 0 ? 50 : 250;
                    if (Gold < cost) { GameUI.Log("Vex wants " + cost + " gold to fuse " + name + "s.", new Color(1f, 0.4f, 0.4f)); return; }
                    Inventory.Remove(name, 3);
                    Gold -= cost;
                    var better = ItemPowers.Gem(type, tier + 1);
                    if (!Inventory.Add(better)) LootDrop.Spawn(transform.position, better, 0);
                    Sfx.Play2D("anvil", 0.6f);
                    GameUI.Log("Vex fuses three " + name + "s into a " + better.Name + " (" + cost + " gold).", better.IconColor);
                    return;
                }
            GameUI.Log("You need three gems of the same kind and quality (Chipped or Flawless).", Color.gray);
        }

        public void Equip(int index)
        {
            var item = Inventory.Slots[index];
            if (item == null || item.Kind != ItemKind.Equipment) return;
            if (item.RequiredLevel > Level)
            {
                GameUI.Log("You must be level " + item.RequiredLevel + " to equip " + item.Name + ".", new Color(1f, 0.4f, 0.4f));
                return;
            }
            var old = Inventory.GetEquipped(item.Slot);
            Inventory.Slots[index] = old;
            Inventory.Equipped[item.Slot] = item;
            Inventory.NotifyChanged();
            Sfx.Play2D("equip", 0.6f);
        }

        public void Unequip(EquipSlot slot)
        {
            var item = Inventory.GetEquipped(slot);
            if (item == null) return;
            if (Inventory.FreeSlots == 0) { GameUI.Log("Your inventory is full.", new Color(1f, 0.4f, 0.4f)); return; }
            Inventory.Equipped.Remove(slot);
            Inventory.Add(item);
        }

        public void DropItem(int index)
        {
            var item = Inventory.TakeAll(index);
            if (item == null) return;
            LootDrop.Spawn(transform.position + transform.forward, item, 0);
            Sfx.Play2D("drop", 0.6f);
            GameUI.Log("You drop " + item.Name + ".", Color.gray);
        }

        void AutoPickupGold()
        {
            for (int i = Interactable.All.Count - 1; i >= 0; i--)
            {
                if (Interactable.All[i] is LootDrop drop && drop.Gold > 0 && drop.CanInteract &&
                    Factory.FlatDistance(drop.Position, transform.position) < 1.6f)
                    drop.Interact(this);
            }
        }

        // =====================================================================================
        // Gathering (RuneScape style)
        // =====================================================================================

        public void StartGathering(ResourceNode node)
        {
            if (Skills.Level(node.Skill) < node.LevelRequired)
            {
                GameUI.Log("You need a " + node.Skill + " level of " + node.LevelRequired + " to gather from this " + node.DisplayName + ".",
                    new Color(1f, 0.4f, 0.4f));
                return;
            }
            if (Inventory.FreeSlots == 0 && Inventory.IndexOf(node.ItemName) < 0)
            {
                GameUI.Log("Your inventory is too full to hold any more.", new Color(1f, 0.4f, 0.4f));
                return;
            }
            GatherNode = node;
            gatherTimer = 0f;
            action = Action.Gather;
            GameUI.Log("You begin " + SkillSet.Verb(node.Skill).ToLower() + " the " + node.DisplayName + "...", Color.gray);
        }

        void StopGathering()
        {
            if (action == Action.Gather) action = Action.None;
            GatherNode = null;
            GatherProgress = 0f;
        }

        void UpdateGathering(float dt)
        {
            if (GatherNode == null || GatherNode.Depleted) { StopGathering(); return; }
            Factory.Face(transform, GatherNode.Position, 0.2f);
            gatherTimer += dt;
            const float tick = 1.6f;
            GatherProgress = gatherTimer / tick;
            if (gatherTimer < tick) return;
            gatherTimer = 0f;
            attackAnim = 0f;
            view?.Interact();
            var skill = GatherNode.Skill;
            Sfx.Play(skill == SkillType.Woodcutting ? "chop" : skill == SkillType.Mining ? "mine" : "splash", GatherNode.Position + Vector3.up, 0.6f, 0.1f);

            int lvl = Skills.Level(GatherNode.Skill);
            float chance = Mathf.Clamp(0.4f + (lvl - GatherNode.LevelRequired) * 0.05f, 0.4f, 0.95f);
            if (Random.value > chance) return;

            var item = ItemDatabase.Material(GatherNode.ItemName);
            if (!Inventory.Add(item))
            {
                GameUI.Log("Your inventory is too full to hold any more.", new Color(1f, 0.4f, 0.4f));
                StopGathering();
                return;
            }
            GameUI.Log("You get some " + GatherNode.ItemName + ".", Color.white);
            Skills.AddXp(GatherNode.Skill, GatherNode.Xp);
            FxPulse.Sparks(GatherNode.Position + Vector3.up, SkillSet.SkillColor(GatherNode.Skill), 5);
            GatherNode.Harvested();
            if (GatherNode == null || GatherNode.Depleted) StopGathering();
        }

        // =====================================================================================
        // Persistence (characters are stored by the server)
        // =====================================================================================

        public SaveData ToSave()
        {
            var slots = new List<SlotSave>();
            for (int i = 0; i < Inventory.Slots.Length; i++)
                if (Inventory.Slots[i] != null) slots.Add(new SlotSave { index = i, item = Inventory.Slots[i] });
            // Items sitting in an open trade window are still ours: save them in free bag slots.
            var escrow = NetClient.I != null ? NetClient.I.EscrowItems : null;
            if (escrow != null)
                for (int e = 0, i = 0; e < escrow.Count && i < Inventory.Slots.Length; i++)
                    if (Inventory.Slots[i] == null) slots.Add(new SlotSave { index = i, item = escrow[e++] });
            var stash = new List<SlotSave>();
            for (int i = 0; i < Stash.Slots.Length; i++)
                if (Stash.Slots[i] != null) stash.Add(new SlotSave { index = i, item = Stash.Slots[i] });
            var equipped = new List<Item>();
            foreach (var kv in Inventory.Equipped) if (kv.Value != null) equipped.Add(kv.Value);
            var active = new List<QuestSave>();
            foreach (var q in Quests.Active) active.Add(new QuestSave { id = q.Def.Id, kills = q.Kills });

            return new SaveData
            {
                level = Level, xp = Xp, gold = Gold + (NetClient.I != null ? NetClient.I.EscrowGold : 0), look = Look,
                stash = stash.ToArray(),
                talents = SaveTalents(),
                companions = OwnedCompanions.ToArray(), companion = ActiveCompanion ?? "",
                str = Strength, dex = Dexterity, intel = Intelligence, vit = Vitality, statPoints = StatPoints,
                x = transform.position.x, z = transform.position.z,
                hp = IsDead ? MaxHealth : Health, mana = Mana,
                skillXp = Skills.SaveXp(),
                completedQuests = new List<string>(Quests.Completed).ToArray(),
                activeQuests = active.ToArray(),
                inventory = slots.ToArray(),
                equipped = equipped.ToArray(),
            };
        }

        string[] SaveTalents()
        {
            var list = new List<string>();
            foreach (var kv in Talents) if (kv.Value > 0) list.Add(kv.Key + ":" + kv.Value);
            return list.ToArray();
        }

        public void LoadSave(SaveData s)
        {
            if (s == null || s.level <= 0) return;
            if (!string.IsNullOrEmpty(s.look) && s.look != Look) SetLook(s.look); // a character keeps the class it was created with
            Level = s.level;
            Xp = s.xp;
            Gold = s.gold;
            Strength = s.str; Dexterity = s.dex; Intelligence = s.intel; Vitality = s.vit;
            StatPoints = s.statPoints;
            OwnedCompanions.Clear();
            if (s.companions != null)
                foreach (var c in s.companions) if (CompanionDef.Get(c) != null && !OwnedCompanions.Contains(c)) OwnedCompanions.Add(c);
            Talents.Clear();
            if (s.talents != null)
                foreach (var t in s.talents)
                {
                    var parts = (t ?? "").Split(':');
                    if (parts.Length == 2 && int.TryParse(parts[1], out int r) && r > 0) Talents[parts[0]] = r;
                }
            Skills.LoadXp(s.skillXp);

            Quests.Active.Clear();
            Quests.Completed.Clear();
            if (s.completedQuests != null) foreach (var id in s.completedQuests) Quests.Completed.Add(id);
            if (s.activeQuests != null)
                foreach (var q in s.activeQuests)
                {
                    var def = QuestDatabase.Find(q.id);
                    if (def != null) Quests.Active.Add(new QuestState(def) { Kills = q.kills });
                }

            for (int i = 0; i < Inventory.Slots.Length; i++) Inventory.Slots[i] = null;
            Inventory.Equipped.Clear();
            if (s.inventory != null)
                foreach (var slot in s.inventory)
                    if (slot.item != null && slot.index >= 0 && slot.index < Inventory.Slots.Length && !string.IsNullOrEmpty(slot.item.Name))
                        Inventory.Slots[slot.index] = slot.item;
            for (int i = 0; i < Stash.Slots.Length; i++) Stash.Slots[i] = null;
            if (s.stash != null)
                foreach (var slot in s.stash)
                    if (slot.item != null && slot.index >= 0 && slot.index < Stash.Slots.Length && !string.IsNullOrEmpty(slot.item.Name))
                        Stash.Slots[slot.index] = slot.item;
            if (s.equipped != null)
                foreach (var it in s.equipped)
                    if (it != null && it.Slot != EquipSlot.None && !string.IsNullOrEmpty(it.Name))
                        Inventory.Equipped[it.Slot] = it;

            var pos = new Vector3(s.x, 0, s.z);
            if (s.x > 0 && WorldGrid.Instance.IsWalkable(pos)) transform.position = pos;
            RecalculateStats();
            Health = s.hp > 0 ? Mathf.Min(s.hp, MaxHealth) : MaxHealth;
            Mana = Mathf.Min(s.mana, MaxMana);
            if (!string.IsNullOrEmpty(s.companion) && OwnedCompanions.Contains(s.companion)) SummonCompanion(s.companion, true);
        }

        // =====================================================================================
        // Zones, damage, death
        // =====================================================================================

        void UpdateZone()
        {
            string zone = WorldGenerator.ZoneAt(transform.position);
            if (zone != lastZone)
            {
                if (lastZone != null)
                    GameUI.Banner(zone, WorldGenerator.InTown(transform.position) ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.85f, 0.6f));
                lastZone = zone;
            }
        }

        public override void TakeDamage(float amount, Combatant source, bool crit = false)
        {
            foreach (var b in Buffs) if (Time.time < b.Until) amount *= b.DamageTakenMul;
            base.TakeDamage(amount, source, crit);
        }

        protected override void OnDamaged(Combatant source, int amount)
        {
            if (action == Action.Gather) StopGathering();
            CameraRig.Shake(Mathf.Clamp(amount / MaxHealth, 0.05f, 0.3f));
            Sfx.Play(amount > MaxHealth * 0.12f ? "hit_heavy" : "hit_armor", transform.position + Vector3.up, 0.55f, 0.1f);
            SpellFx.Hit(transform.position + Vector3.up * 1.2f, new Color(0.55f, 0.03f, 0.03f), true, 6);
            if (Health > 0f && amount > MaxHealth * 0.04f) view?.Hit();
        }

        protected override void Die(Combatant killer)
        {
            path.Clear();
            action = Action.None;
            AttackTarget = null;
            view?.Die();
            Sfx.Play2D("death", 0.8f);
            GameUI.Log("You have been slain" + (killer != null ? " by " + killer.DisplayName : "") + ".", new Color(1f, 0.3f, 0.3f));
        }

        public void Respawn()
        {
            NetClient.I?.LeaveDungeon(true); // dying in the Catacombs sends you home
            int lost = Gold / 10;
            Gold -= lost;
            IsDead = false;
            Health = MaxHealth;
            Mana = MaxMana;
            Buffs.Clear();
            whirlUntil = 0f;
            leapT = -1f;
            RecalculateStats();
            transform.position = GameManager.I.SpawnPoint;
            if (model != null) model.Root.localRotation = Quaternion.identity;
            view?.Revive();
            path.Clear();
            GameUI.Log("You awaken in Hollowmere. You lost " + lost + " gold.", new Color(1f, 0.6f, 0.3f));
            NetClient.I?.SaveNow();
            SpellFx.HolyLight(transform.position);
            Sfx.Play2D("holy_cast", 0.6f);
        }
    }
}
