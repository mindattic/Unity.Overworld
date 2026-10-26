using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>The original's aWeaponType.</summary>
    public enum WeaponType
    {
        Sword,
        Axe,
        Spear,
        Blunt,
        Arrow,
    }

    /// <summary>One engine weapon (Weapons.loadWeapon): the rating, the slashing/piercing/blunt split, durability, skill, weight and range.</summary>
    public sealed class WeaponDefinition
    {
        public string Name { get; internal set; }
        public string Description { get; internal set; }
        public float Rating { get; internal set; }
        public float Slashing { get; internal set; }
        public float Piercing { get; internal set; }
        public float Blunt { get; internal set; }
        public float Durability { get; internal set; }
        public float MaxDurability { get; internal set; }
        public float SkillRequired { get; internal set; }
        public WeaponType Type { get; internal set; }
        public float Weight { get; internal set; }
        public float Range { get; internal set; }

        /// <summary>The sprite folder under gfx/weapon holding the four sets (left, right, draw-left, draw-right).</summary>
        public string Sprites { get; internal set; }

        /// <summary>The battle-stance frames shown over a hero facing left or down (weapon_left_1..6), in frame order.</summary>
        public IReadOnlyList<string> Left { get; internal set; } = new string[0];

        /// <summary>The battle-stance frames over a hero facing right or up (weapon_right_1..6).</summary>
        public IReadOnlyList<string> Right { get; internal set; } = new string[0];

        /// <summary>The drawing frames over a hero facing left or down (draw\weapon_draw_left_1..6).</summary>
        public IReadOnlyList<string> DrawLeft { get; internal set; } = new string[0];

        /// <summary>The drawing frames over a hero facing right or up (draw\weapon_draw_right_1..6).</summary>
        public IReadOnlyList<string> DrawRight { get; internal set; } = new string[0];

        /// <summary>
        /// renderWeapon: the frame shown over a hero in <paramref name="state"/> facing <paramref name="facing"/> at
        /// <paramref name="frame"/> (the hero's own frame number): drawing, the draw set's; in the battle stance, the
        /// stance's; null (texBlank) in any other state.
        /// </summary>
        public string Frame(HeroState state, Facing facing, int frame)
        {
            var left = facing == Facing.Left || facing == Facing.Down;
            IReadOnlyList<string> set;
            if (state == HeroState.Drawing) set = left ? DrawLeft : DrawRight;
            else if (state == HeroState.Battle) set = left ? Left : Right;
            else return null;
            return frame >= 0 && frame < set.Count ? set[frame] : null;
        }

        /// <summary>Every frame of the weapon, once each.</summary>
        public IReadOnlyList<string> Files
        {
            get
            {
                var list = new List<string>();
                foreach (var set in new[] { Left, Right, DrawLeft, DrawRight }) list.AddRange(set);
                return list;
            }
        }
    }

    /// <summary>The two weapons the engine defines.</summary>
    public sealed class WeaponData
    {
        internal WeaponData(int maxWeapons, int spriteFrames, List<WeaponDefinition> weapons)
        {
            MaxWeapons = maxWeapons;
            SpriteFrames = spriteFrames;
            Weapons = weapons;
        }

        public int MaxWeapons { get; }
        public int SpriteFrames { get; }
        public IReadOnlyList<WeaponDefinition> Weapons { get; }

        public WeaponDefinition Find(string name)
        {
            foreach (var w in Weapons) if (w.Name == name) return w;
            return null;
        }

        internal static WeaponData Read(IContentSource source, string file, List<string> errors)
        {
            var root = ContentFiles.Open(source, file, errors);
            if (root == null) return null;
            var max = root.Integer("maxWeapons", 1);
            var frames = root.Integer("spriteFrames", 1);
            var list = new List<WeaponDefinition>();
            foreach (var r in root.Objects("weapons"))
            {
                var w = new WeaponDefinition
                {
                    Name = r.Text("name"),
                    Description = r.Text("description"),
                    Rating = r.Positive("rating"),
                    Slashing = r.Number("slashing", 0f, 1f),
                    Piercing = r.Number("piercing", 0f, 1f),
                    Blunt = r.Number("blunt", 0f, 1f),
                    Durability = r.Number("durability", 0f),
                    MaxDurability = r.Positive("maxDurability"),
                    SkillRequired = r.Number("skillRequired", 0f),
                    Type = r.Choice<WeaponType>("type"),
                    Weight = r.Positive("weight"),
                    Range = r.Positive("range"),
                    Sprites = r.Text("sprites"),
                };
                var framesReader = r.Section("frames");
                w.Left = Frames(framesReader, "left", w.Sprites, frames);
                w.Right = Frames(framesReader, "right", w.Sprites, frames);
                w.DrawLeft = Frames(framesReader, "drawLeft", w.Sprites, frames);
                w.DrawRight = Frames(framesReader, "drawRight", w.Sprites, frames);
                framesReader.Done();
                r.Check(w.Durability <= w.MaxDurability, "durability", "is above maxDurability");
                if (list.Exists(x => x.Name == w.Name)) r.Fail("name", "\"" + w.Name + "\" is defined twice");
                r.Done();
                list.Add(w);
            }

            if (list.Count > max) errors.Add(file + ".weapons: " + list.Count + " weapons exceed maxWeapons " + max);
            root.Done();
            return new WeaponData(max, frames, list);
        }

        /// <summary>One frame set: spriteFrames pictures, each a relative path in the weapon's own folder under gfx/weapon.</summary>
        static IReadOnlyList<string> Frames(ContentReader r, string key, string folder, int frames)
        {
            var files = r.Texts(key);
            r.Check(files.Count == frames, key, "must list " + frames + " frames, the weapons' spriteFrames (has " + files.Count + ")");
            var prefix = "gfx/weapon/" + folder + "/";
            foreach (var file in files)
                if (!file.StartsWith(prefix, System.StringComparison.Ordinal) || file.IndexOf('\\') >= 0 || file.EndsWith("/", System.StringComparison.Ordinal))
                    r.Fail(key, "must hold forward-slashed pictures under " + prefix + " (has \"" + file + "\")");
            return files;
        }
    }
}
