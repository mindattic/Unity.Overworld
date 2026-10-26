using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>The way a hero faces (the original's direction).</summary>
    public enum Facing
    {
        Left,
        Right,
        Up,
        Down,
    }

    /// <summary>A hero's mode (the original's aHeroMode).</summary>
    public enum HeroMode
    {
        Adventure,
        Battle,
    }

    /// <summary>A hero's AI state (the original's heroAiState).</summary>
    public enum HeroState
    {
        Standing,
        Walking,
        Running,
        Kneeling,
        Resting,
        Drawing,
        Battle,
    }

    /// <summary>One of the three party heroes with every figure resolved (the shared defaults with the hero's own overrides).</summary>
    public sealed class HeroDefinition
    {
        public int Id { get; internal set; }
        public string Name { get; internal set; }
        public DataPoint Start { get; internal set; }
        public string Weapon { get; internal set; }
        public HeroState State { get; internal set; }
        public Facing Facing { get; internal set; }
        public HeroMode Mode { get; internal set; }

        public int Health { get; internal set; }
        public int MaxHealth { get; internal set; }
        public int Mana { get; internal set; }
        public int MaxMana { get; internal set; }
        public int Stamina { get; internal set; }
        public int MaxStamina { get; internal set; }
        public int Strength { get; internal set; }
        public int Agility { get; internal set; }
        public int Intelligence { get; internal set; }

        /// <summary>World units per frame.</summary>
        public float WalkSpeed { get; internal set; }

        public float RunSpeed { get; internal set; }
        public float HaltSpeed { get; internal set; }

        /// <summary>Seconds per animation frame (or per regeneration step for <see cref="RegenRate"/>).</summary>
        public float StandRate { get; internal set; }

        public float WalkRate { get; internal set; }
        public float RunRate { get; internal set; }
        public float KneelRate { get; internal set; }
        public float RegenRate { get; internal set; }
        public float BattleRate { get; internal set; }
        public float DrawRate { get; internal set; }

        /// <summary>Zero-based upper bounds the original passed (5 means six frames).</summary>
        public int WalkFrames { get; internal set; }

        public int RunFrames { get; internal set; }
        public int DrawFrames { get; internal set; }
        public int BattleFrames { get; internal set; }
        public int FaceFrames { get; internal set; }
        public int HaltFrames { get; internal set; }
        public int KneelFrames { get; internal set; }

        /// <summary>The weight this hero adds to the party's limit: Strength + Agility / 2.</summary>
        public float WeightLimit => Strength + Agility / 2f;

        internal HeroDefinition Copy() => (HeroDefinition)MemberwiseClone();
    }

    /// <summary>The three heroes (Chrono, Belmont, Flagg) as the original's Engine.initHero built them.</summary>
    public sealed class HeroData
    {
        internal HeroData(float heroSize, string spriteSet, float moveDistancePerAgility, HeroSprites sprites, LocomotionTuning locomotion, List<HeroDefinition> heroes)
        {
            HeroSize = heroSize;
            SpriteSet = spriteSet;
            MoveDistancePerAgility = moveDistancePerAgility;
            Sprites = sprites;
            Locomotion = locomotion;
            Heroes = heroes;
        }

        public float HeroSize { get; }

        /// <summary>The art set all three heroes share.</summary>
        public string SpriteSet { get; }

        /// <summary>moveDistanceMax = Agility * this (0.25 for Agility 5).</summary>
        public float MoveDistancePerAgility { get; }

        /// <summary>The pictures of the art set: standing, walking and running in each facing.</summary>
        public HeroSprites Sprites { get; }

        /// <summary>How heroes walk, run and follow the cursor.</summary>
        public LocomotionTuning Locomotion { get; }

        public IReadOnlyList<HeroDefinition> Heroes { get; }

        /// <summary>The party's carrying limit: the sum of every hero's weight limit.</summary>
        public float PartyWeightLimit
        {
            get
            {
                var total = 0f;
                foreach (var h in Heroes) total += h.WeightLimit;
                return total;
            }
        }

        internal static HeroData Read(IContentSource source, string file, WeaponData weapons, List<string> errors)
        {
            var root = ContentFiles.Open(source, file, errors);
            if (root == null) return null;
            var heroSize = root.Positive("heroSize");
            var spriteSet = root.Text("spriteSet");
            var commonReader = root.Section("common");
            var common = ReadCommon(commonReader, out var perAgility);
            commonReader.Done();
            var sprites = HeroSprites.Read(root.Section("sprites"), common.WalkFrames, common.RunFrames);
            var locomotion = LocomotionTuning.Read(root.Section("locomotion"));

            // Each hero is the common block with its own keys laid over it (Belmont's walk rate and speeds).
            var list = new List<HeroDefinition>();
            foreach (var r in root.Objects("heroes"))
            {
                var h = common.Copy();
                h.Id = r.Integer("id", 0);
                h.Name = r.Text("name");
                h.Start = r.Pair("start");
                h.Weapon = r.Text("weapon");
                h.State = r.Choice<HeroState>("state");
                if (r.Has("walkRate")) h.WalkRate = r.Positive("walkRate");
                if (r.Has("walkSpeed")) h.WalkSpeed = r.Positive("walkSpeed");
                if (r.Has("runSpeed")) h.RunSpeed = r.Positive("runSpeed");
                r.Check(h.RunSpeed >= h.WalkSpeed, "runSpeed", "is below walkSpeed");
                r.Check(h.Id == list.Count, "id", "must be " + list.Count + ", the hero's place in the party (is " + h.Id + ")");
                if (weapons != null && weapons.Find(h.Weapon) == null)
                    r.Fail("weapon", "\"" + h.Weapon + "\" is not a weapon in the weapons file");
                r.Done();
                list.Add(h);
            }

            root.Done();
            return new HeroData(heroSize, spriteSet, perAgility, sprites, locomotion, list);
        }

        static HeroDefinition ReadCommon(ContentReader c, out float perAgility)
        {
            var v = new HeroDefinition
            {
                Health = c.Integer("health", 0),
                MaxHealth = c.Integer("maxHealth", 1),
                Mana = c.Integer("mana", 0),
                MaxMana = c.Integer("maxMana", 1),
                Stamina = c.Integer("stamina", 0),
                MaxStamina = c.Integer("maxStamina", 1),
                Strength = c.Integer("strength", 0),
                Agility = c.Integer("agility", 0),
                Intelligence = c.Integer("intelligence", 0),
                Facing = c.Choice<Facing>("facing"),
                Mode = c.Choice<HeroMode>("mode"),
                WalkSpeed = c.Positive("walkSpeed"),
                RunSpeed = c.Positive("runSpeed"),
                HaltSpeed = c.Positive("haltSpeed"),
                StandRate = c.Positive("standRate"),
                WalkRate = c.Positive("walkRate"),
                RunRate = c.Positive("runRate"),
                KneelRate = c.Positive("kneelRate"),
                RegenRate = c.Positive("regenRate"),
                BattleRate = c.Positive("battleRate"),
                DrawRate = c.Positive("drawRate"),
                WalkFrames = c.Integer("walkFrames", 0),
                RunFrames = c.Integer("runFrames", 0),
                DrawFrames = c.Integer("drawFrames", 0),
                BattleFrames = c.Integer("battleFrames", 0),
                FaceFrames = c.Integer("faceFrames", 0),
                HaltFrames = c.Integer("haltFrames", 0),
                KneelFrames = c.Integer("kneelFrames", 0),
            };
            perAgility = c.Positive("moveDistancePerAgility");
            c.Check(v.Health <= v.MaxHealth, "health", "is above maxHealth");
            c.Check(v.Mana <= v.MaxMana, "mana", "is above maxMana");
            c.Check(v.Stamina <= v.MaxStamina, "stamina", "is above maxStamina");
            c.Check(v.RunSpeed >= v.WalkSpeed, "runSpeed", "is below walkSpeed");
            return v;
        }
    }
}
