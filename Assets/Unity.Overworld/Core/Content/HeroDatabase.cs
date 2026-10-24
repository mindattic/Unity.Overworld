using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>A row of hero.mdb's Hero table (2004): the earlier design's single hero.</summary>
    public sealed class HeroRecord
    {
        public int Id { get; internal set; }
        public string Name { get; internal set; }
        public int Health { get; internal set; }
        public int MaxHealth { get; internal set; }
        public int Strength { get; internal set; }
        public int Agility { get; internal set; }
        public int Intelligence { get; internal set; }
        public int Speed { get; internal set; }
        public int WeaponId { get; internal set; }
        public int RelicId { get; internal set; }
    }

    /// <summary>A row of hero.mdb's Weapon table: the earlier weapon design (power and random, not slashing and piercing).</summary>
    public sealed class WeaponRecord
    {
        public int Id { get; internal set; }
        public string Name { get; internal set; }
        public int Power { get; internal set; }
        public int Random { get; internal set; }
        public int ClassId { get; internal set; }
        public int Durability { get; internal set; }
        public int MaxDurability { get; internal set; }

        /// <summary>The sprite folder as the database wrote it ("\gfx\weapon\axe\").</summary>
        public string Directory { get; internal set; }
    }

    /// <summary>
    /// hero.mdb (2004) as typed records. Its Armor, Armor_Head, Armor_Body, Armor_Arms, Armor_Feet, Relic and
    /// Weapon_class tables are empty in the original and are kept as raw (empty) tables on <see cref="Database"/>;
    /// <see cref="Warnings"/> names what refers into them.
    /// </summary>
    public sealed class HeroDatabase
    {
        public MdbDatabase Database { get; internal set; }
        public List<HeroRecord> Heroes { get; } = new List<HeroRecord>();
        public List<WeaponRecord> Weapons { get; } = new List<WeaponRecord>();
        public List<string> Warnings { get; } = new List<string>();

        internal static readonly string[] Schema =
        {
            "Armor: heroID Int32, headID Int32, bodyID Int32, armsID Int32, feetID Int32",
            "Armor_Arms: armsID Int32, armsNAME String, armsDEF Int32, armsRND Int32, armsDUR Int32, armsMAXDUR Int32, armsDESC String",
            "Armor_Body: bodyID Int32, bodyNAME String, bodyDEF Int32, bodyRND Int32, bodyDUR Int32, bodyMAXDUR Int32, bodyDESC String",
            "Armor_Feet: feetID Int32, feetNAME String, feetDEF Int32, feetRND Int32, feetDUR Int32, feetMAXDUR Int32, feetDESC String",
            "Armor_Head: headID Int32, headNAME String, headDEF Int32, headRND Int32, headDUR Int32, headMAXDUR Int32, headDESC String",
            "Hero: heroID Int32, heroNAME String, heroHP Int32, heroMAXHP Int32, heroSTR Int32, heroAGI Int32, heroINT Int32, heroSPD Int32, weapID Int32, relicID Int32",
            "Relic: relicID String, relicNAME String, relicDESC String, relicSTR_MOD String, relicINT_MOD String, relicSPD_MOD String, relicAGI_MOD String",
            "Weapon: weapID Int32, weapNAME String, weapPWR Int32, weapRND Int32, classID Int32, weapDUR Int32, weapMAXDUR Int32, weapDIR String",
            "Weapon_class: classID Int32, classNAME String",
        };

        internal static HeroDatabase Read(IContentSource source, string file, List<string> errors)
        {
            var db = MdbDatabase.Read(source, file, Schema, errors);
            if (db == null) return null;
            var result = new HeroDatabase { Database = db };

            foreach (var r in db.Table("Weapon")?.RowReaders ?? new List<ContentReader>())
            {
                result.Weapons.Add(new WeaponRecord
                {
                    Id = r.Integer("weapID"),
                    Name = r.Text("weapNAME"),
                    Power = r.Integer("weapPWR", 0),
                    Random = r.Integer("weapRND", 0),
                    ClassId = r.Integer("classID"),
                    Durability = r.Integer("weapDUR", 0),
                    MaxDurability = r.Integer("weapMAXDUR", 0),
                    Directory = r.Text("weapDIR"),
                });
                r.Done();
            }

            foreach (var r in db.Table("Hero")?.RowReaders ?? new List<ContentReader>())
            {
                var hero = new HeroRecord
                {
                    Id = r.Integer("heroID"),
                    Name = r.Text("heroNAME"),
                    Health = r.Integer("heroHP", 0),
                    MaxHealth = r.Integer("heroMAXHP", 1),
                    Strength = r.Integer("heroSTR", 0),
                    Agility = r.Integer("heroAGI", 0),
                    Intelligence = r.Integer("heroINT", 0),
                    Speed = r.Integer("heroSPD", 0),
                    WeaponId = r.Integer("weapID"),
                    RelicId = r.Integer("relicID"),
                };
                r.Check(hero.Health <= hero.MaxHealth, "heroHP", "is above heroMAXHP");
                r.Done();
                result.Heroes.Add(hero);
            }

            var seenWeapons = new HashSet<int>();
            foreach (var w in result.Weapons)
            {
                if (!seenWeapons.Add(w.Id)) errors.Add(file + ": Weapon has the id " + w.Id + " twice");
                if ((db.Table("Weapon_class")?.RowCount ?? 0) == 0)
                    result.Warnings.Add(file + ": Weapon " + w.Id + " (" + w.Name + ") has class " + w.ClassId + ", but Weapon_class is empty");
            }

            foreach (var h in result.Heroes)
            {
                if (!seenWeapons.Contains(h.WeaponId)) errors.Add(file + ": Hero " + h.Id + " (" + h.Name + ") wields weapon " + h.WeaponId + ", which does not exist");
                if ((db.Table("Relic")?.RowCount ?? 0) == 0)
                    result.Warnings.Add(file + ": Hero " + h.Id + " (" + h.Name + ") has relic " + h.RelicId + ", but Relic is empty");
            }

            return result;
        }
    }
}
