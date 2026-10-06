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
        public readonly SkillSet Skills = new SkillSet();
        public readonly QuestLog Quests = new QuestLog();
        public readonly float[] CooldownEnd = new float[AbilityDef.All.Length];

        // ---- Derived stats
        public int TotStr, TotDex, TotInt, TotVit;
        public float ArmorValue, CritChance, AttackSpeed, MinDamage, MaxDamage, MoveSpeed, LifeOnHit, HealthRegen, ManaRegen;
        public override float Armor => ArmorValue;
        public float MeleeMultiplier => 1f + TotStr / 50f;
        public float SpellMultiplier => (1f + TotInt / 40f) * (1f + (Level - 1) * 0.06f);
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
            return p;
        }

        /// <summary>Builds (or rebuilds) the hero's model: an animated KayKit character, or primitives as a fallback.</summary>
        public void SetLook(string look)
        {
            Look = System.Array.IndexOf(CharacterLook.HeroModels, look) >= 0 ? look : CharacterLook.HeroModels[0];
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
        }

        void AnimCast()
        {
            castAnim = 0f;
            view?.Cast();
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

            MaxHealth = 60 + TotVit * 6 + Level * 8 + ItemStat(Stat.Health);
            MaxMana = 40 + TotInt * 3 + Level * 3 + ItemStat(Stat.Mana);

            int armor = ItemStat(Stat.Armor);
            foreach (var kv in Inventory.Equipped) if (kv.Value != null) armor += kv.Value.Armor;
            ArmorValue = armor + TotDex * 0.25f;

            CritChance = Mathf.Min(60f, 5f + TotDex * 0.15f + ItemStat(Stat.CritChance));
            var weapon = Inventory.GetEquipped(EquipSlot.Weapon);
            float baseAps = weapon != null ? weapon.AttacksPerSecond : 1.4f;
            MinDamage = weapon != null ? weapon.MinDamage : 1;
            MaxDamage = weapon != null ? weapon.MaxDamage : 3;
            AttackSpeed = baseAps * (1f + ItemStat(Stat.AttackSpeed) / 100f);
            MoveSpeed = 6.2f * (1f + ItemStat(Stat.MoveSpeed) / 100f);
            LifeOnHit = ItemStat(Stat.LifeOnHit);
            HealthRegen = 1f + Level * 0.2f + ItemStat(Stat.HealthRegen);
            ManaRegen = 2f + TotInt * 0.06f + ItemStat(Stat.ManaRegen);

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
                GameUI.Log("You have reached level " + Level + "! You have " + StatPoints + " attribute points to spend (C).", new Color(1f, 0.85f, 0.2f));
                FxPulse.Ring(transform.position, new Color(1f, 0.85f, 0.2f), 4f, 0.8f);
                FxPulse.Spawn(transform.position + Vector3.up, new Color(1f, 0.9f, 0.4f), new Vector3(1.5f, 0.1f, 1.5f), new Vector3(0.2f, 8f, 0.2f), 0.9f, PrimitiveType.Cylinder);
                foreach (var a in AbilityDef.All)
                    if (a.RequiredLevel == Level) GameUI.Log("New ability unlocked: " + a.Name + " [" + a.Key + "]", a.Color);
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
            UpdateAction(dt);
            FollowPath(dt);
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

            bool crit = Random.value * 100f < CritChance;
            float dmg = Random.Range(MinDamage, MaxDamage) * MeleeMultiplier * (crit ? 2f : 1f);
            victim.TakeDamage(dmg, this, crit);
            FxPulse.Sparks(victim.Center, crit ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.95f, 0.85f), crit ? 8 : 4);
            if (LifeOnHit > 0) Heal(LifeOnHit, false);
        }

        // =====================================================================================
        // Abilities
        // =====================================================================================

        public void CastAbility(int index, Vector3 aim, bool silent = false)
        {
            if (IsDead || index < 0 || index >= AbilityDef.All.Length) return;
            var a = AbilityDef.All[index];
            if (Level < a.RequiredLevel)
            {
                if (!silent) GameUI.Float(transform.position + Vector3.up * 2.5f, a.Name + " unlocks at level " + a.RequiredLevel, Color.gray, 0.8f);
                return;
            }
            if (Time.time < CooldownEnd[index] || Time.time < globalCooldownEnd) return;
            if (Mana < a.ManaCost)
            {
                if (!silent || Time.frameCount % 30 == 0) GameUI.Float(transform.position + Vector3.up * 2.5f, "Not enough mana", new Color(0.4f, 0.6f, 1f), 0.8f);
                return;
            }

            Mana -= a.ManaCost;
            CooldownEnd[index] = Time.time + a.Cooldown;
            globalCooldownEnd = Time.time + 0.3f;
            StopGathering();
            if (action == Action.Move || action == Action.Interact) { action = Action.None; StopMoving(); }
            castLockUntil = Time.time + 0.2f;
            Factory.Face(transform, aim);

            switch (a.Id)
            {
                case AbilityId.Cleave:
                {
                    attackAnim = 0f;
                    view?.Action("2H_Melee_Attack_Spin", 0.55f);
                    Overlap(transform.position, 3.4f, Faction, buffer);
                    foreach (var c in buffer)
                    {
                        Vector3 to = Factory.Flat(c.transform.position - transform.position);
                        if (to.sqrMagnitude > 0.01f && Vector3.Angle(transform.forward, to) > 70f) continue;
                        bool crit = Random.value * 100f < CritChance;
                        c.TakeDamage(Random.Range(MinDamage, MaxDamage) * MeleeMultiplier * 1.7f * (crit ? 2f : 1f), this, crit);
                        if (LifeOnHit > 0) Heal(LifeOnHit, false);
                    }
                    var arc = FxPulse.Spawn(transform.position + transform.forward * 1.5f + Vector3.up * 0.9f, a.Color,
                        new Vector3(0.5f, 0.06f, 0.5f), new Vector3(5.5f, 0.06f, 3f), 0.22f, PrimitiveType.Cylinder);
                    arc.transform.rotation = transform.rotation;
                    NetClient.I?.SendFx("cleave", transform.position, transform.position + transform.forward);
                    break;
                }
                case AbilityId.Fireball:
                {
                    AnimCast();
                    bool crit = Random.value * 100f < CritChance;
                    float dmg = 14f * SpellMultiplier * Random.Range(0.9f, 1.1f) * (crit ? 2f : 1f);
                    Projectile.Fire(this, transform.position + Vector3.up * 1.2f + transform.forward * 0.6f,
                        new Vector3(aim.x, 1.2f, aim.z), 20f, dmg, a.Color, 0.5f, 2.2f, 22f, crit);
                    NetClient.I?.SendFx("fireball", transform.position, aim);
                    break;
                }
                case AbilityId.FrostNova:
                {
                    AnimCast();
                    Overlap(transform.position, 6f, Faction, buffer);
                    foreach (var c in buffer)
                    {
                        c.TakeDamage(12f * SpellMultiplier * Random.Range(0.9f, 1.1f), this);
                        if (c is Enemy e) e.Slow(4f);
                    }
                    FxPulse.Ring(transform.position, a.Color, 6f, 0.45f);
                    FxPulse.Burst(transform.position + Vector3.up * 0.5f, a.Color, 1.5f, 0.3f);
                    NetClient.I?.SendFx("nova", transform.position, transform.position);
                    break;
                }
                case AbilityId.Heal:
                {
                    AnimCast();
                    Heal(MaxHealth * 0.35f + TotInt * 3f);
                    FxPulse.Spawn(transform.position + Vector3.up, a.Color, new Vector3(2f, 0.05f, 2f), new Vector3(0.2f, 5f, 0.2f), 0.7f, PrimitiveType.Cylinder);
                    FxPulse.Ring(transform.position, a.Color, 2f, 0.5f);
                    NetClient.I?.SendFx("heal", transform.position, transform.position);
                    break;
                }
                case AbilityId.Meteor:
                {
                    AnimCast();
                    float range = Factory.FlatDistance(transform.position, aim);
                    Vector3 target = range > 16f ? transform.position + Factory.Flat(aim - transform.position).normalized * 16f : aim;
                    MeteorFx.Cast(this, new Vector3(target.x, 0f, target.z), 70f * SpellMultiplier);
                    NetClient.I?.SendFx("meteor", transform.position, target);
                    break;
                }
            }
        }

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
            var equipped = new List<Item>();
            foreach (var kv in Inventory.Equipped) if (kv.Value != null) equipped.Add(kv.Value);
            var active = new List<QuestSave>();
            foreach (var q in Quests.Active) active.Add(new QuestSave { id = q.Def.Id, kills = q.Kills });

            return new SaveData
            {
                level = Level, xp = Xp, gold = Gold, look = Look,
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

        public void LoadSave(SaveData s)
        {
            if (s == null || s.level <= 0) return;
            Level = s.level;
            Xp = s.xp;
            Gold = s.gold;
            Strength = s.str; Dexterity = s.dex; Intelligence = s.intel; Vitality = s.vit;
            StatPoints = s.statPoints;
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
            if (s.equipped != null)
                foreach (var it in s.equipped)
                    if (it != null && it.Slot != EquipSlot.None && !string.IsNullOrEmpty(it.Name))
                        Inventory.Equipped[it.Slot] = it;

            var pos = new Vector3(s.x, 0, s.z);
            if (s.x > 0 && WorldGrid.Instance.IsWalkable(pos)) transform.position = pos;
            RecalculateStats();
            Health = s.hp > 0 ? Mathf.Min(s.hp, MaxHealth) : MaxHealth;
            Mana = Mathf.Min(s.mana, MaxMana);
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

        protected override void OnDamaged(Combatant source, int amount)
        {
            if (action == Action.Gather) StopGathering();
            CameraRig.Shake(Mathf.Clamp(amount / MaxHealth, 0.05f, 0.3f));
            if (Health > 0f && amount > MaxHealth * 0.04f) view?.Hit();
        }

        protected override void Die(Combatant killer)
        {
            path.Clear();
            action = Action.None;
            AttackTarget = null;
            view?.Die();
            GameUI.Log("You have been slain" + (killer != null ? " by " + killer.DisplayName : "") + ".", new Color(1f, 0.3f, 0.3f));
        }

        public void Respawn()
        {
            int lost = Gold / 10;
            Gold -= lost;
            IsDead = false;
            Health = MaxHealth;
            Mana = MaxMana;
            transform.position = GameManager.I.SpawnPoint;
            if (model != null) model.Root.localRotation = Quaternion.identity;
            view?.Revive();
            path.Clear();
            GameUI.Log("You awaken in Hollowmere. You lost " + lost + " gold.", new Color(1f, 0.6f, 0.3f));
            NetClient.I?.SaveNow();
            FxPulse.Ring(transform.position, new Color(1f, 1f, 0.8f), 3f, 0.8f);
        }
    }
}
