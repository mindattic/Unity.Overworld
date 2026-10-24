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
                r.Check(w.Durability <= w.MaxDurability, "durability", "is above maxDurability");
                if (list.Exists(x => x.Name == w.Name)) r.Fail("name", "\"" + w.Name + "\" is defined twice");
                r.Done();
                list.Add(w);
            }

            if (list.Count > max) errors.Add(file + ".weapons: " + list.Count + " weapons exceed maxWeapons " + max);
            root.Done();
            return new WeaponData(max, frames, list);
        }
    }
}
