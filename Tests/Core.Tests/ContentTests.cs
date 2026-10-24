using System;
using System.IO;
using System.Linq;
using MindAttic.Overworld.Core;
using NUnit.Framework;
using CollectionAssert = NUnit.Framework.Legacy.CollectionAssert;
using StringAssert = NUnit.Framework.Legacy.StringAssert;

namespace MindAttic.Overworld.Tests
{
    /// <summary>
    /// Loads every data file under Assets/StreamingAssets/Content through Core and cross-checks it against the
    /// reconstruction record: section 8 (pickups), 10 (quests), 11 (books), 16 (both world.mdb files and hero.mdb),
    /// plus the heroes, weapons, walls and props of sections 3, 4 and 12.4. The validation tests break one file in
    /// memory and check the error names its file, key and line.
    /// </summary>
    public class ContentTests
    {
        static string ContentDirectory()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "Assets", "StreamingAssets", "Content");
                if (Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException("Assets/StreamingAssets/Content was not found above " + AppContext.BaseDirectory);
        }

        static readonly ContentRegistry Registry = ContentRegistry.Load(new DirectoryContentSource(ContentDirectory()));

        static string Flat(string text) => string.Join(" ", text.Split(new[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));

        /// <summary>The real files with one changed in memory.</summary>
        static MemoryContentSource Patched(string file, Func<string, string> change)
        {
            var disk = new DirectoryContentSource(ContentDirectory());
            var original = disk.Read(file);
            var changed = change(original);
            Assert.AreNotEqual(original, changed, "the patch to " + file + " changed nothing");
            return new MemoryContentSource(disk).Set(file, changed);
        }

        static ContentException Rejected(MemoryContentSource source)
        {
            return Assert.Throws<ContentException>(() => ContentRegistry.Load(source));
        }

        // ---- the manifest and the two worlds -------------------------------------------------------------------

        [Test]
        public void EveryFileLoadsWithNoErrors()
        {
            Assert.IsTrue(ContentRegistry.TryLoad(new DirectoryContentSource(ContentDirectory()), out var registry, out var errors), string.Join("\n", errors));
            Assert.IsNotNull(registry);
        }

        [Test]
        public void TheOwnersChosen2007WorldIsPlayableAndTheEngines2006WorldIsKeptAsReference()
        {
            Assert.AreEqual("2007", Registry.PlayableWorld.Edition);
            Assert.AreEqual(WorldKind.Root, Registry.PlayableWorld.Kind);
            Assert.AreEqual("2006", Registry.ReferenceWorld.Edition);
            Assert.AreEqual(WorldKind.Engine, Registry.ReferenceWorld.Kind);
            Assert.AreEqual(2, Registry.Worlds.Count);
            StringAssert.Contains("2007-03-08", Registry.PlayableWorld.Database.Source);
            StringAssert.Contains("2006-02-21", Registry.ReferenceWorld.Database.Source);
        }

        [Test]
        public void EngineWorld2006MatchesSection161()
        {
            var w = Registry.World("2006");
            Assert.AreEqual(new[] { "Foothills of Phar", "The Badlands" }, w.Areas.Select(a => a.Name).ToArray());
            Assert.AreEqual(new[] { 0, 1 }, w.Areas.Select(a => a.Id).ToArray());

            Assert.AreEqual(3, w.Events.Count);
            var test = w.Events.Single(e => e.Name == "Test");
            Assert.AreEqual((1, 2, 2), (test.AreaId, test.Id, test.ScriptId));
            Assert.AreEqual((-1f, -0.5f, 0.5f, 0f), (test.Left, test.Right, test.Top, test.Bottom));
            var dark = w.Events.Single(e => e.Name == "Dark Woods");
            Assert.AreEqual((0f, 0.5f, 1f, 0.5f, 0f, 0), (dark.AreaId, dark.Left, dark.Right, dark.Top, dark.Bottom, dark.ScriptId));
            var cinematic = w.Events.Single(e => e.Name == "Cinematic");
            Assert.AreEqual((1.05f, 2f, 0.5f, 0f, 1), (cinematic.Left, cinematic.Right, cinematic.Top, cinematic.Bottom, cinematic.ScriptId));
            Assert.IsFalse(test.HasOrigin);

            Assert.AreEqual("CHANGE HERO NAME RYAN", w.FindScript(2).Text);
            Assert.AreEqual("Name Ryan", w.FindScript(2).Name);
            Assert.AreEqual("CHANGE TIME DAYPERIOD NIGHT", w.FindScript(0).Text);
            Assert.AreEqual("Make Night", w.FindScript(0).Name);
            Assert.AreEqual("CHANGE CAM X +0.001", w.FindScript(1).Text);
            Assert.AreEqual("Pan Right", w.FindScript(1).Name);
            Assert.IsTrue(w.Scripts.All(s => s.Rate == 0f));

            Assert.AreEqual(new[] { "Health Root", "Moon Berry", "Mandrake" }, w.ItemTextures.Select(t => t.Name).ToArray());
            Assert.AreEqual(new[] { @"C:\tex.bmp", @"C:\tex2.bmp", @"C:\tex3.bmp" }, w.ItemTextures.Select(t => t.File).ToArray());

            Assert.AreEqual(3, w.Items.Count);
            Assert.AreEqual("CHANGE HERO HP +10", w.Items[0].Script);
            Assert.AreEqual("+10 HP", w.Items[0].Effect);
            Assert.AreEqual("Common healing root", w.Items[0].Description);
            Assert.AreEqual(0.1f, w.Items[0].Weight, 1e-6f);
            Assert.IsTrue(w.Items.All(i => i.Type == "OneTime" && i.Amount == 1));
            Assert.AreEqual("CHANGE HERO AI LAYDOWN | CHANGE TIME HOUR 22 | CHANGE INVENTORY OPEN FALSE", w.Items[1].Script);
            Assert.AreEqual("Induces a coma-like sleep until nightfall", w.Items[1].Effect);
            Assert.AreEqual("Mild sedative fruit", w.Items[1].Description);
            Assert.AreEqual(0.2f, w.Items[1].Weight, 1e-6f);
            Assert.AreEqual("CHANGE HERO STATUS POISONED", w.Items[2].Script);
            Assert.AreEqual("Poison", w.Items[2].Effect);

            Assert.AreEqual(3, w.Pickups.Count);
            Assert.AreEqual((0, 0, -0.2f, 0.01f, 0f), (w.Pickups[0].AreaId, w.Pickups[0].ItemId, w.Pickups[0].X, w.Pickups[0].Y, w.Pickups[0].Z));
            Assert.AreEqual((0, 1, 0.3f, 0.4f), (w.Pickups[1].AreaId, w.Pickups[1].ItemId, w.Pickups[1].X, w.Pickups[1].Y));
            Assert.AreEqual((0, 2, 0f, 0f), (w.Pickups[2].AreaId, w.Pickups[2].ItemId, w.Pickups[2].X, w.Pickups[2].Y));

            // Tables the 2006 database does not have stay empty.
            Assert.AreEqual(0, w.Scenes.Count + w.Terrain.Count + w.AreaProps.Count + w.Npcs.Count + w.Enemies.Count);
            Assert.AreEqual(6, w.Database.Tables.Count);
        }

        [Test]
        public void RootWorld2007MatchesSection162()
        {
            var w = Registry.World("2007");
            Assert.AreEqual(15, w.Database.Tables.Count);
            Assert.AreEqual(new[] { "Grasslands", "Lowlands" }, w.Areas.Select(a => a.Name).ToArray());
            Assert.AreEqual(new[] { 1, 2 }, w.Areas.Select(a => a.Id).ToArray());

            Assert.AreEqual(11, w.Scenes.Count);
            var s1 = w.Scenes.Single(s => s.Id == 1);
            Assert.AreEqual((1, "Grasslands 1", @"\gfx\area\West of Town\bgLandscape2_1x1.jpg"), (s1.AreaId, s1.Name, s1.Image));
            Assert.AreEqual((-1f, 0f, 0f, 2f), (s1.X, s1.Y, s1.Z, s1.Width));
            Assert.AreEqual(0.6666667f, s1.Height, 1e-6f);
            Assert.AreEqual(@"\gfx\area\West of Town\new.png", w.Scenes.Single(s => s.Id == 5).Image);
            var mountain = w.Scenes.Single(s => s.Id == 10);
            Assert.AreEqual((6f, 0f, 0.03f, 3f), (mountain.X, mountain.Y, mountain.Z, mountain.Width));
            Assert.AreEqual(1.222222f, mountain.Height, 1e-5f);
            var sky4 = w.Scenes.Single(s => s.Id == 9);
            Assert.AreEqual(7f, sky4.X);
            Assert.AreEqual(1.333333f, sky4.Y, 1e-5f);
            var sky8 = w.Scenes.Single(s => s.Id == 11);
            Assert.AreEqual((0, 8f), (sky8.AreaId, sky8.X));
            Assert.AreEqual(
                new[] { -1f, 1f, 3f, 5f, 7f, 9f, 11f, 13f },
                w.Scenes.Where(s => s.Id <= 8).OrderBy(s => s.Id).Select(s => s.X).ToArray());

            Assert.AreEqual(9, w.Terrain.Count);
            Assert.IsTrue(w.Terrain.All(t => t.AreaId == 1));
            var t1 = w.Terrain.Single(t => t.Id == 1);
            Assert.AreEqual((-1f, -0.3f, 5.5f, -0.3f), (t1.X1, t1.Y1, t1.X2, t1.Y2));
            var t6 = w.Terrain.Single(t => t.Id == 6);
            Assert.AreEqual((11f, -0.2f, 12f, -0.43f), (t6.X1, t6.Y1, t6.X2, t6.Y2));
            var t9 = w.Terrain.Single(t => t.Id == 9);
            Assert.AreEqual((1f, 0.05f, 1.25f, 0.05f), (t9.X1, t9.Y1, t9.X2, t9.Y2));

            Assert.AreEqual(9, w.PropArt.Count);
            Assert.AreEqual(
                new[] { (360, 193), (306, 112), (481, 117), (237, 214), (268, 185), (213, 248), (218, 177), (521, 124), (147, 102) },
                w.PropArt.OrderBy(p => p.Id).Select(p => (p.ImageWidth, p.ImageHeight)).ToArray());
            Assert.AreEqual(@"\gfx\prop\tree\orchard_3.png", w.FindPropArt(3).Image);
            Assert.AreEqual("Stonehenge", w.FindPropArt(8).Name);
            Assert.AreEqual(@"\gfx\prop\structure\gate.png", w.FindPropArt(9).Image);

            Assert.AreEqual(15, w.AreaProps.Count);
            Assert.IsTrue(w.AreaProps.All(p => p.AreaId == 1));
            var first = w.AreaProps[0];
            Assert.AreEqual((1, 1f, -0.35f, -0.05f, 1f, 0.25f), (first.PropId, first.X, first.Y, first.Z, first.Width, first.Height));
            var gate = w.AreaProps[11];
            Assert.AreEqual((9, 3f, -0.3f, -0.5f, 0.5f, 0.12f), (gate.PropId, gate.X, gate.Y, gate.Z, gate.Width, gate.Height));
            var last = w.AreaProps[14];
            Assert.AreEqual((9, 5f, -0.4f, -0.05f, 0.5f, 0.12f), (last.PropId, last.X, last.Y, last.Z, last.Width, last.Height));

            Assert.AreEqual(2, w.Npcs.Count);
            Assert.AreEqual(("Peasent Woman", 0.144f, 0.072f, @"\gfx\npc\woman\"), (w.Npcs[0].Name, w.Npcs[0].Width, w.Npcs[0].Height, w.Npcs[0].Directory));
            Assert.AreEqual("Magitek Armor", w.Npcs[1].Name);
            Assert.AreEqual(2, w.AreaNpcs.Count);
            Assert.AreEqual((1, 1, 1f, -0.2f, "stand"), (w.AreaNpcs[0].AreaId, w.AreaNpcs[0].NpcId, w.AreaNpcs[0].X, w.AreaNpcs[0].Y, w.AreaNpcs[0].State));
            Assert.AreEqual((2, 4f, -0.2f, "pursue"), (w.AreaNpcs[1].NpcId, w.AreaNpcs[1].X, w.AreaNpcs[1].Y, w.AreaNpcs[1].State));

            Assert.AreEqual(5, w.Enemies.Count);
            var expected = new[]
            {
                ("Werelynx", 6, 0.004f, 0.8f, "Defensive", "Walking", "Animal"),
                ("Botherfly", 3, 0.006f, 1.4f, "Aggressive", "Flying", "Insect"),
                ("Blue Slime", 2, 0.003f, 0.4f, "Passive", "Walking", "Paranormal"),
                ("Red Slime", 5, 0.004f, 0.7f, "Defensive", "Walking", "Paranormal"),
                ("Toadie", 4, 0.05f, 0.4f, "Aggressive", "Hopping", "Reptile"),
            };
            for (var i = 0; i < expected.Length; i++)
            {
                var e = w.Enemies[i];
                Assert.AreEqual(i + 1, e.Id);
                Assert.AreEqual(expected[i], (e.Name, e.MaxHealth, e.MoveSpeed, e.Range, e.State, e.Motion, e.Class));
            }

            Assert.AreEqual(new[] { "Paranormal", "Animal", "Reptile", "Insect", "Mechanical" }, w.EnemyClasses.ToArray());
            Assert.AreEqual(new[] { "Walking", "Flying", "Hopping", "Teleporting", "Stationary", "Swimming" }, w.EnemyMotions.ToArray());
            Assert.AreEqual(new[] { "Passive", "Aggressive", "Afraid", "Defensive" }, w.EnemyStates.ToArray());
            Assert.AreEqual(0, w.AreaEnemies.Count);
            Assert.AreEqual(0, w.Database.Table("Area_Enemies").RowCount);
            Assert.AreEqual(0, w.Database.Table("Enemy_Images").RowCount);
            Assert.AreEqual(9, w.Database.Table("Enemy_Images").Columns.Count);

            Assert.AreEqual(3, w.Events.Count);
            var poison = w.Events.Single(e => e.Name == "Poison Box");
            Assert.AreEqual((1, 1, true, 3f, 0f, -0.25f, 0.25f, -0.1f, -0.45f, 1),
                (poison.AreaId, poison.Id, poison.HasOrigin, poison.X, poison.Y, poison.Left, poison.Right, poison.Top, poison.Bottom, poison.ScriptId));
            var heal = w.Events.Single(e => e.Name == "Healing Box");
            Assert.AreEqual((2f, 2), (heal.X, heal.ScriptId));
            var woods = w.Events.Single(e => e.Name == "Entering Dark Woods");
            Assert.AreEqual((4f, -0.25f, 0f, 3), (woods.X, woods.Left, woods.Right, woods.ScriptId));

            Assert.AreEqual(("Poison", "CHANGE HERO HP -1", 0.5f), (w.FindScript(1).Name, w.FindScript(1).Text, w.FindScript(1).Rate));
            Assert.AreEqual(("Heal", "CHANGE HERO HP +1", 0.5f), (w.FindScript(2).Name, w.FindScript(2).Text, w.FindScript(2).Rate));
            Assert.AreEqual(("Night", "CHANGE TIME DAYPERIOD Night", 0f), (w.FindScript(3).Name, w.FindScript(3).Text, w.FindScript(3).Rate));
        }

        [Test]
        public void HeroMdbMatchesSection163()
        {
            var db = Registry.HeroDatabase;
            Assert.AreEqual(9, db.Database.Tables.Count);
            Assert.AreEqual(1, db.Heroes.Count);
            var h = db.Heroes[0];
            Assert.AreEqual((1, "Alexander", 50, 50, 1, 2, 0, 2, 1, 1), (h.Id, h.Name, h.Health, h.MaxHealth, h.Strength, h.Agility, h.Intelligence, h.Speed, h.WeaponId, h.RelicId));

            Assert.AreEqual(4, db.Weapons.Count);
            var axe = db.Weapons.Single(x => x.Id == 4);
            Assert.AreEqual(("Axe", 4, 3, 2, 24, 0, @"\gfx\weapon\axe\"), (axe.Name, axe.Power, axe.Random, axe.ClassId, axe.Durability, axe.MaxDurability, axe.Directory));
            var steel = db.Weapons.Single(x => x.Id == 1);
            Assert.AreEqual(("Steel Sword", 1, 1, 1, 10, 10, @"\gfx\weapon\steelsword\"), (steel.Name, steel.Power, steel.Random, steel.ClassId, steel.Durability, steel.MaxDurability, steel.Directory));
            var katana = db.Weapons.Single(x => x.Id == 2);
            Assert.AreEqual(("Katana", 2, 1, 1, 13, 0), (katana.Name, katana.Power, katana.Random, katana.ClassId, katana.Durability, katana.MaxDurability));
            var broad = db.Weapons.Single(x => x.Id == 3);
            Assert.AreEqual(("Broad Sword", 3, 2, 1, 24, 0, @"\gfx\weapon\broadsword\"), (broad.Name, broad.Power, broad.Random, broad.ClassId, broad.Durability, broad.MaxDurability, broad.Directory));

            foreach (var empty in new[] { "Armor", "Armor_Head", "Armor_Body", "Armor_Arms", "Armor_Feet", "Relic", "Weapon_class" })
                Assert.AreEqual(0, db.Database.Table(empty).RowCount, empty);
        }

        [Test]
        public void ARowInATableTheOriginalLeftEmptyIsReported()
        {
            var source = Patched("hero.json", t => t.Replace(
                "{\"name\": \"classID\", \"type\": \"Int32\"}, {\"name\": \"classNAME\", \"type\": \"String\"}],\n      \"rows\": [\n      ]",
                "{\"name\": \"classID\", \"type\": \"Int32\"}, {\"name\": \"classNAME\", \"type\": \"String\"}],\n      \"rows\": [\n        {\"classID\": 1, \"classNAME\": \"Blade\"}\n      ]"));
            var error = Rejected(source).Errors.Single();
            StringAssert.Contains("hero.json: Weapon_class has 1 row(s)", error);
            StringAssert.Contains("the original's table was empty", error);
        }

        [Test]
        public void TheOriginalsDanglingReferencesAreKeptAsWarningsNotRejected()
        {
            var warnings = Registry.Warnings;
            Assert.AreEqual(1, warnings.Count(w => w.Contains("Scene 11") && w.Contains("area 0")), string.Join("\n", warnings));
            Assert.AreEqual(4, warnings.Count(w => w.Contains("Weapon_class is empty")));
            Assert.AreEqual(1, warnings.Count(w => w.Contains("Relic is empty")));
            Assert.AreEqual(6, warnings.Count);
        }

        // ---- section 8: the pickups ----------------------------------------------------------------------------

        [Test]
        public void ThereAre372OriginalPickupsPlusTheRestoredNitroBean()
        {
            var p = Registry.Pickups;
            Assert.AreEqual(373, p.Pickups.Count);
            Assert.AreEqual(PickupData.OriginalCount, p.OriginalPickupCount);
            for (var i = 0; i < p.Pickups.Count; i++) Assert.AreEqual(i, p.Pickups[i].Index);
            Assert.AreEqual(0.05f, p.ItemSize);

            var nitro = p.Pickups[372];
            Assert.IsTrue(nitro.IsRestored);
            Assert.AreEqual(("Nitro Bean", "CHANGE HERO AP +200", "+200 Stamina", 120), (nitro.Name, nitro.Script, nitro.Effect, nitro.Value));
            Assert.AreEqual(new DataPoint(0.4f, -0.5f), nitro.Position);
            // Cheese overwrote the Nitro Bean at 102; the Coffee Bean it shares coordinates with is 101.
            Assert.AreEqual("Cheese", p.Pickups[102].Name);
            Assert.AreEqual(p.Pickups[101].Position, nitro.Position);
        }

        [Test]
        public void PickupGroupsMatchTheSection81Table()
        {
            var p = Registry.Pickups.Pickups;
            Assert.AreEqual(45, p.Count(x => x.Name == "Potato"));
            Assert.AreEqual(49, p.Count(x => x.Name == "Turnip"));
            Assert.AreEqual(89 + 120, p.Count(x => x.Name == "Gold"));
            Assert.AreEqual(89, p.Count(x => x.Texture == "gold5"));
            Assert.AreEqual(120, p.Count(x => x.Texture == "gold"));
            Assert.AreEqual(50, p.Count(x => x.Name == "Satchel"));
            Assert.AreEqual(Enumerable.Range(6, 45), p.Where(x => x.Name == "Potato").Select(x => x.Index));
            Assert.AreEqual(Enumerable.Range(52, 49), p.Where(x => x.Name == "Turnip").Select(x => x.Index));
            Assert.AreEqual(Enumerable.Range(112, 89), p.Where(x => x.Texture == "gold5").Select(x => x.Index));
            Assert.AreEqual(Enumerable.Range(201, 50), p.Where(x => x.Name == "Satchel").Select(x => x.Index));
            Assert.AreEqual(Enumerable.Range(251, 120), p.Where(x => x.Texture == "gold").Select(x => x.Index));

            Assert.IsTrue(p.Where(x => x.Index >= 112 && x.Index <= 370).All(x => x.IsRandom));
            Assert.AreEqual(89 + 50 + 120, p.Count(x => x.IsRandom));

            Assert.AreEqual(
                new[] { ItemType.SingleItem, ItemType.GroupItem, ItemType.Reusable, ItemType.Equipable, ItemType.UseOnPickup },
                Enum.GetValues(typeof(ItemType)).Cast<ItemType>().ToArray());
            Assert.AreEqual(2, p.Count(x => x.Type == ItemType.GroupItem && !x.IsRandom));
            Assert.AreEqual(1, p.Count(x => x.Type == ItemType.Equipable));
            Assert.AreEqual(5, p.Count(x => x.Type == ItemType.Reusable)); // four books and the Rune
            Assert.AreEqual(89 + 120, p.Count(x => x.Type == ItemType.UseOnPickup));
        }

        [Test]
        public void NamedPickupsCarryTheSection81Figures()
        {
            var p = Registry.Pickups.Pickups;
            void Row(int i, string name, float x, float y, string script, string effect, string description, float weight, ItemType type, int value, float grow)
            {
                var d = p[i];
                Assert.AreEqual(name, d.Name, "name " + i);
                Assert.AreEqual(x, d.Position.X, 1e-6f, "x " + i);
                Assert.AreEqual(y, d.Position.Y, 1e-6f, "y " + i);
                Assert.AreEqual(script, d.Script, "script " + i);
                Assert.AreEqual(effect, Flat(d.Effect), "effect " + i);
                Assert.AreEqual(description, Flat(d.Description), "description " + i);
                Assert.AreEqual(weight, d.Weight, 1e-6f, "weight " + i);
                Assert.AreEqual(type, d.Type, "type " + i);
                Assert.AreEqual(value, d.Value, "value " + i);
                Assert.AreEqual(grow, d.GrowSeconds, 1e-6f, "grow " + i);
            }

            Row(0, "Health Root", 0, -0.1f, "CHANGE HERO HP +10", "+10 Health", "Common healing root", 0.1f, ItemType.SingleItem, 12, 12);
            Row(1, "Moon Berry", -0.4f, 0.1f, "CHANGE HERO AI LAYDOWN | CHANGE TIME HOUR 22 | CHANGE INVENTORY OPEN FALSE", "Induces a coma-like sleep until nightfall", "Mild sedative fruit", 0.2f, ItemType.GroupItem, 40, 12);
            Row(2, "Mandrake", 0.5f, -0.4f, "CHANGE HERO STATUS POISONED", "Poison", "Very poisonous plant", 0.1f, ItemType.SingleItem, 5, 12);
            Row(3, "Crabweed", -0.5f, 0.4f, "CHANGE HERO HP +1", "+1 Health", "Weak healing herb", 0.3f, ItemType.SingleItem, 2, 12);
            Row(4, "Violet Caper", 0.2f, 0.1f, "CHANGE HERO AP +5", "+5 Stamina", "Mild stamina restoring fern", 0.1f, ItemType.SingleItem, 10, 12);
            Row(5, "Angel Vine", -0.2f, 0.25f, "CHANGE HERO HP +9999 | CHANGE HERO AP +9999 | CHANGE HERO MP +9999", "Full health, mana, and stamina restoration",
                "\"Truly, a gift from the Gods\" -Futhark Ald'jka, Master Alchemist, 64 CE", 0.1f, ItemType.SingleItem, 200, 100);
            Assert.AreEqual("mandrake", p[5].Texture);
            Row(51, "Rusty Sword", -0.55f, -0.45f, "CHANGE HERO WEAPON ITEM", "ATK: 5 DEF: 1 DUR: 2/10", "Poorly forged iron short sword with a rusty blade", 5.3f, ItemType.Equipable, 50, 0);
            Row(101, "Coffee Bean", 0.4f, -0.5f, "CHANGE HERO AP +15", "+15 Stamina", "Highly caffinated bean grown primarily in the subtropical Southlands", 0.1f, ItemType.SingleItem, 12, 0);
            Row(102, "Cheese", -0.5f, 0.15f, "CHANGE HERO HP +10 | CHANGE HERO AP +5", "+10 Health, +5 Stamina", "Dairy product of fermented milk", 0.2f, ItemType.SingleItem, 8, 0);
            Row(103, "Apple", -0.4f, 0.3f, "CHANGE HERO HP +5 | CHANGE HERO AP +5", "+5 Health, +5 Stamina", "Ripe red fruit", 0.1f, ItemType.SingleItem, 2, 10);
            Row(104, "Bramble Nut", 0, 0.6f, "CHANGE HERO HP -5 | CHANGE HERO AP +15", "-5 Health, +15 Stamina", "Nut encased in a sharp spiney shell", 0.1f, ItemType.SingleItem, 4, 10);
            Row(105, "Grimoire de'Alchemiste", 0.1f, -0.1f,
                "CHANGE BOOK COVER BROWN | CHANGE BOOK FONT CORSIVA8 | CHANGE BOOK TO ITEM | CHANGE BOOK NUMSYS ROMAN | CHANGE BOOK SHOW TRUE",
                "Alchemy", "Book about the essentials of alchemy -Huth Ondana, Master Alchemist, 12 CE", 3, ItemType.Reusable, 75, 0);
            Row(106, "Egg", 0.3f, -0.6f, "CHANGE HERO HP +2 | CHANGE HERO AP +1", "+2 Health, +1 Stamina", "Unfertilized chicken embryo", 0.1f, ItemType.SingleItem, 1, 2);
            Row(107, "Xim's Scrub", -0.1f, 0.6f, "CHANGE HERO STATUS FINE", "Cure Poison", "Plant that neutralizes toxins in body - Aldanos Xim, Herbalist, 33 CE", 0.1f, ItemType.SingleItem, 10, 15);
            Row(108, "Morningstar", -0.5f, 0.3f, "CHANGE HERO AI LAYDOWN | CHANGE TIME HOUR 5 | CHANGE INVENTORY OPEN FALSE", "Induces a coma-like sleep until daybreak", "Mild sedative fruit", 0.1f, ItemType.GroupItem, 40, 10);
            Row(109, "Dragon Almanac", -0.2f, -0.3f,
                "CHANGE BOOK COVER BLUE | CHANGE BOOK FONT CALISTO | CHANGE BOOK TO ITEM | CHANGE BOOK NUMSYS ROMAN | CHANGE BOOK SHOW TRUE",
                "Dragonology", "Book about dragons and their characteristics", 3, ItemType.Reusable, 25, 0);
            Row(110, "Poetry Book", -0.22f, -0.45f,
                "CHANGE BOOK COVER BROWN | CHANGE BOOK FONT CORSIVA8 | CHANGE BOOK TO ITEM | CHANGE BOOK NUMSYS ROMAN | CHANGE BOOK SHOW TRUE",
                "Poetry", "Book of assorted poems", 2.5f, ItemType.Reusable, 12, 0);
            Row(111, "Rune", 0.52f, -0.6f,
                "CHANGE INVENTORY OPEN FALSE | CHANGE HERO MANA -5 | CHANGE HERO Z -1 | CHANGE HERO X 0 | CHANGE HERO Y 0 | CHANGE CAMERA POS HERO",
                "Teleportation", "Magical rune capable of teleporting user", 2, ItemType.Reusable, 1000, 0);
            Row(371, "Matt's Book", 1, 0.5f,
                "CHANGE BOOK COVER BLUE | CHANGE BOOK FONT CALISTO | CHANGE BOOK TO ITEM | CHANGE BOOK NUMSYS ROMAN | CHANGE BOOK SHOW TRUE",
                "Misc", "Matt's book", 3, ItemType.Reusable, 25, 0);

            // The three groups of coin and bag pickups.
            Assert.AreEqual(("CHANGE HERO GOLD +5", "Currency", "Legal tender used to barter for goods and services", 0f, 5), (p[112].Script, p[112].Effect, Flat(p[112].Description), p[112].Weight, p[112].Value));
            Assert.AreEqual(("CHANGE HERO GOLD +25", "Container", "A small bag that gingles delightfully when shook", ItemType.GroupItem, 25), (p[201].Script, p[201].Effect, Flat(p[201].Description), p[201].Type, p[201].Value));
            Assert.AreEqual(("CHANGE HERO GOLD +1", "Currency", 1), (p[251].Script, p[251].Effect, p[251].Value));
        }

        [Test]
        public void PotatoAndTurnipFieldsFollowTheGridFormulaWithTheirInventoryIcons()
        {
            var p = Registry.Pickups.Pickups;
            for (var n = 0; n < 45; n++)
            {
                var d = p[6 + n];
                Assert.AreEqual(-0.4f + 0.05f * (n % 8), d.Position.X, 1e-5f, "potato x " + n);
                Assert.AreEqual(-0.5f - 0.05f * (n / 8), d.Position.Y, 1e-5f, "potato y " + n);
                Assert.AreEqual(("plant1", "potato", "CHANGE HERO HP +1 | CHANGE HERO AP +3", 2, 10f), (d.Texture, d.InventoryTexture, d.Script, d.Value, d.GrowSeconds));
            }

            for (var n = 0; n < 49; n++)
            {
                var d = p[52 + n];
                Assert.AreEqual(-0.4f + 0.05f * (n % 8), d.Position.X, 1e-5f, "turnip x " + n);
                Assert.AreEqual(-0.9f - 0.05f * (n / 8), d.Position.Y, 1e-5f, "turnip y " + n);
                Assert.AreEqual(("plant2", "turnip", "CHANGE HERO HP +1 | CHANGE HERO AP +1", 1, 10f), (d.Texture, d.InventoryTexture, d.Script, d.Value, d.GrowSeconds));
            }

            // Only the two crop fields have a separate inventory icon.
            Assert.AreEqual(45 + 49, p.Count(x => x.InventoryTexture != null));
        }

        [Test]
        public void RandomPickupsScatterOverTheSection81AreaAndAreReproducibleFromASeed()
        {
            var data = Registry.Pickups;
            var area = data.RandomArea;
            Assert.AreEqual((-3f, 2f, 10f, 10f, 1f), (area.StartX, area.StartY, area.Width, area.Height, area.YLift));

            var a = data.ResolvePositions(new Random(7).NextDouble);
            var b = data.ResolvePositions(new Random(7).NextDouble);
            var c = data.ResolvePositions(new Random(8).NextDouble);
            CollectionAssert.AreEqual(a, b);
            CollectionAssert.AreNotEqual(a, c);
            for (var i = 112; i <= 370; i++)
            {
                Assert.That(a[i].X, Is.InRange(-3f, 7f), "x " + i);
                Assert.That(a[i].Y, Is.InRange(-7f, 3f), "y " + i);
            }

            // Fixed pickups ignore the generator; the extremes of a draw map to the corners.
            Assert.AreEqual(new DataPoint(0, -0.1f), a[0]);
            Assert.AreEqual(new DataPoint(-3f, 3f), area.Place(0, 0));
            Assert.AreEqual(new DataPoint(7f, -7f), area.Place(1, 1));
        }

        [Test]
        public void EveryPickupTextureIsDeclaredAndEveryBookPickupNamesABook()
        {
            var data = Registry.Pickups;
            Assert.IsTrue(data.Pickups.All(p => data.Textures.Contains(p.Texture)));
            Assert.AreEqual(21, data.Textures.Count);
            CollectionAssert.AreEquivalent(new[] { "potato", "turnip" }, data.InventoryTextures);
            var bookPickups = data.Pickups.Where(p => p.Script.Contains("CHANGE BOOK TO ITEM")).ToList();
            CollectionAssert.AreEqual(new[] { 105, 109, 110, 371 }, bookPickups.Select(p => p.Index).ToArray());
            Assert.IsTrue(bookPickups.All(p => Registry.Books.Find(p.Name) != null));
            Assert.IsTrue(Registry.Pickups.Pickups.All(p => ScriptSyntax.Check(p.Script) == null));
        }

        // ---- section 10: quests --------------------------------------------------------------------------------

        [Test]
        public void QuestsMatchSection10()
        {
            var q = Registry.Quests;
            Assert.AreEqual(4, q.Quests.Count);
            Assert.AreEqual(2, q.Subquests.Count);

            void Quest(QuestDefinition d, string text, int exp, QuestStatus status, int objectives)
            {
                Assert.AreEqual((text, exp, status, objectives), (d.Text, d.Exp, d.Status, d.Objectives.Count));
                Assert.AreEqual(string.Empty, d.StartScript);
                Assert.AreEqual(string.Empty, d.EndScript);
            }

            void Objective(ObjectiveDefinition o, float x, float y, float range, int exp, string text, string end = "")
            {
                Assert.AreEqual(ObjectiveType.MoveTo, o.Type);
                Assert.AreEqual((x, y, range, exp, text, end), (o.X, o.Y, o.Range, o.Exp, o.Text, o.EndScript));
                Assert.AreEqual(string.Empty, o.StartScript);
            }

            Quest(q.Quests[0], "Farmer Brinn wants you to check the crops", 1000, QuestStatus.Active, 2);
            Objective(q.Quests[0].Objectives[0], 0, 0.3f, 0.1f, 100, "Check North Field");
            Objective(q.Quests[0].Objectives[1], 0, -0.3f, 0.1f, 150, "Check South Field");
            Quest(q.Quests[1], "Check the other crops", 2000, QuestStatus.Inactive, 1);
            Objective(q.Quests[1].Objectives[0], 0, 0, 0.1f, 200, "Check Center Field");
            Quest(q.Quests[2], "Last Quest", 2500, QuestStatus.Inactive, 2);
            Objective(q.Quests[2].Objectives[0], -0.3f, 0, 0.05f, 320, "Go West, my boy!");
            Objective(q.Quests[2].Objectives[1], 0.3f, 0, 0.05f, 300, "go East, my boy!", "CHANGE TIME HOUR 18");
            Quest(q.Quests[3], "Go westernly now", 3500, QuestStatus.Inactive, 1);
            Objective(q.Quests[3].Objectives[0], -5, 0, 0.1f, 400, "Very Far West");

            Quest(q.Subquests[0], "Check out the weird cirle", 350, QuestStatus.Active, 1);
            Objective(q.Subquests[0].Objectives[0], 0, -1, 0.05f, 200, "Go Way south");
            Quest(q.Subquests[1], "Walk up to 3", 1500, QuestStatus.Active, 1);
            Objective(q.Subquests[1].Objectives[0], 0, 3, 0.05f, 1200, "Go very far north");
        }

        // ---- sections 12.4, 4.1, 3.5, 3.4, 3.6: weapons, heroes, walls, props ----------------------------------

        [Test]
        public void TheTwoWeaponsMatchSection124()
        {
            var w = Registry.Weapons;
            Assert.AreEqual(2, w.Weapons.Count);
            Assert.AreEqual(100, w.MaxWeapons);
            Assert.AreEqual(6, w.SpriteFrames);

            var steel = w.Find("Steel Sword");
            Assert.AreEqual("A fine sword forged from steel with a leather grip", Flat(steel.Description));
            Assert.AreEqual((8f, 0.5f, 0.15f, 0f, 28f, 30f, 2f, WeaponType.Sword, 6f, 0.09f, "steelsword"),
                (steel.Rating, steel.Slashing, steel.Piercing, steel.Blunt, steel.Durability, steel.MaxDurability, steel.SkillRequired, steel.Type, steel.Weight, steel.Range, steel.Sprites));
            var axe = w.Find("Fine Axe");
            Assert.AreEqual("A fine axe for chopping", axe.Description);
            Assert.AreEqual((12f, 0.4f, 0f, 0.9f, 41f, 50f, 1f, WeaponType.Axe, 10f, 0.08f, "axe"),
                (axe.Rating, axe.Slashing, axe.Piercing, axe.Blunt, axe.Durability, axe.MaxDurability, axe.SkillRequired, axe.Type, axe.Weight, axe.Range, axe.Sprites));
        }

        [Test]
        public void TheThreeHeroesMatchSection41()
        {
            var h = Registry.Heroes;
            Assert.AreEqual(3, h.Heroes.Count);
            Assert.AreEqual(0.144f, h.HeroSize);
            Assert.AreEqual("chrono", h.SpriteSet);

            var rows = new[]
            {
                (0, "Chrono", 0f, 0f, "Fine Axe", 0.13f, 0.003f, 0.006f),
                (1, "Belmont", 0.3f, -0.05f, "Steel Sword", 0.1f, 0.005f, 0.01f),
                (2, "Flagg", -0.3f, 0.1f, "Steel Sword", 0.13f, 0.003f, 0.006f),
            };
            for (var i = 0; i < 3; i++)
            {
                var d = h.Heroes[i];
                Assert.AreEqual(rows[i], (d.Id, d.Name, d.Start.X, d.Start.Y, d.Weapon, d.WalkRate, d.WalkSpeed, d.RunSpeed));
                Assert.AreEqual((31, 50, 12, 20, 60, 60, 7, 5, 5), (d.Health, d.MaxHealth, d.Mana, d.MaxMana, d.Stamina, d.MaxStamina, d.Strength, d.Agility, d.Intelligence));
                Assert.AreEqual((Facing.Right, HeroMode.Adventure, HeroState.Standing), (d.Facing, d.Mode, d.State));
                Assert.AreEqual((0.0004f, 0.6f, 0.1f, 0.15f, 2f, 0.1f, 0.05f), (d.HaltSpeed, d.StandRate, d.RunRate, d.KneelRate, d.RegenRate, d.BattleRate, d.DrawRate));
                Assert.AreEqual((5, 5, 5, 5, 0, 0, 0), (d.WalkFrames, d.RunFrames, d.DrawFrames, d.BattleFrames, d.FaceFrames, d.HaltFrames, d.KneelFrames));
                Assert.AreEqual(9.5f, d.WeightLimit);
                Assert.IsNotNull(Registry.Weapons.Find(d.Weapon));
            }

            Assert.AreEqual(28.5f, h.PartyWeightLimit);
            Assert.AreEqual(0.25f, h.Heroes[0].Agility * h.MoveDistancePerAgility, 1e-6f);
        }

        [Test]
        public void TheSixWallsMatchSection35()
        {
            var w = Registry.Walls.Walls;
            var expected = new[]
            {
                (1f, 0f, 1f, 1f), (1f, 1f, 2f, 1f), (2f, 1f, 2.5f, 2f), (2.5f, 2f, 3f, 2f), (3f, 2f, 3.5f, 1.5f), (3.5f, 1.5f, 4f, 2f),
            };
            Assert.AreEqual(6, w.Count);
            for (var i = 0; i < 6; i++) Assert.AreEqual(expected[i], (w[i].X1, w[i].Y1, w[i].X2, w[i].Y2));

            Assert.IsTrue(w[0].IsVertical);
            Assert.IsTrue(float.IsPositiveInfinity(w[0].Slope));
            Assert.AreEqual(0f, w[1].Slope);
            Assert.AreEqual(1f, w[1].YIntercept);
            Assert.AreEqual(2f, w[2].Slope);
            Assert.AreEqual(-3f, w[2].YIntercept);
            Assert.AreEqual(0f, w[3].Slope);
            Assert.AreEqual(-1f, w[4].Slope);
            Assert.AreEqual(5f, w[4].YIntercept);
            Assert.AreEqual(1f, w[5].Slope);
            Assert.AreEqual(-2f, w[5].YIntercept);
            // The source's "Xintercept" is Y1 + slope * X1.
            Assert.AreEqual(1f + 2f * 2f, w[2].XIntercept);
        }

        [Test]
        public void ThePropsAndTentMatchSections34And36()
        {
            var p = Registry.Props;
            Assert.AreEqual(4, p.Props.Count);
            Assert.AreEqual(0.144f, p.HeroSize);
            var trees = new[] { (1f, 1f), (2f, 1f), (1.5f, 1f) };
            for (var i = 0; i < 3; i++)
            {
                var d = p.Props[i];
                Assert.AreEqual(trees[i], (d.X, d.Y));
                Assert.AreEqual((0.144f, 0.072f, 0.072f, "tree_bottom_1", "tree_top_1"), (d.Width, d.Height, d.BottomHeight, d.BottomTexture, d.TopTexture));
                Assert.AreEqual(d.X - 0.144f, d.TopX, 1e-6f);
                Assert.AreEqual(d.Y + 0.072f + 0.144f, d.TopY, 1e-6f);
            }

            var wall = p.Props[3];
            Assert.AreEqual((1f, 1.064f, 1f, 0.5f, "wall_medium"), (wall.X, wall.Y, wall.Width, wall.Height, wall.BottomTexture));
            Assert.AreEqual("tree_top_1", wall.TopTexture);

            Assert.AreEqual((-0.7f, -0.04f, 0.3f, "tent_128"), (p.Tent.X, p.Tent.Y, p.Tent.Size, p.Tent.Texture));
        }

        // ---- section 11: books ---------------------------------------------------------------------------------

        [Test]
        public void TheFiveBooksMatchSection11()
        {
            var b = Registry.Books.Books;
            Assert.AreEqual(
                new[] { "Colesium Vol I", "Dragon Almanac", "Grimoire de'Alchemiste", "Matt's Book", "Poetry Book" },
                b.Select(x => x.Title).ToArray());
            Assert.AreEqual(new[] { 10, 12, 8, 6, 16 }, b.Select(x => x.Pages.Count).ToArray());
            Assert.AreEqual(new[] { 941, 899, 302, 1, 1007 }, b.Select(x => x.WordCount).ToArray());

            StringAssert.Contains("Kain", string.Concat(Registry.Books.Find("Colesium Vol I").Pages));
            StringAssert.Contains("Alchemy is the fusing of science with magic", Flat(Registry.Books.Find("Grimoire de'Alchemiste").Pages[0]));
            StringAssert.Contains("To Anna, My best student and lover", Flat(Registry.Books.Find("Grimoire de'Alchemiste").Pages[7]));
            Assert.AreEqual("sdfsdfsdfdsf", Registry.Books.Find("Matt's Book").Pages[0]);
            Assert.AreEqual(string.Empty, Registry.Books.Find("Poetry Book").Pages[1]);
            // The page text keeps its hand-wrapped line breaks and the curly apostrophes.
            Assert.IsTrue(Registry.Books.Find("Dragon Almanac").Pages.Any(p => p.Contains("\r\n")));
            Assert.IsTrue(b.Any(x => x.Pages.Any(p => p.Contains('\u2019'))));
        }

        // ---- validation: every problem named by file, key and line --------------------------------------------

        [Test]
        public void AMissingFileIsNamed()
        {
            var source = new MemoryContentSource(new DirectoryContentSource(ContentDirectory())).Set("quests.json", null);
            StringAssert.Contains("quests.json: the file is missing", Rejected(source).Errors.Single());
        }

        [Test]
        public void BrokenJsonIsReportedWithItsLine()
        {
            var source = Patched("walls.json", t => t.Replace("\"walls\": [", "\"walls\": [,"));
            var errors = Rejected(source).Errors;
            Assert.AreEqual(1, errors.Count);
            StringAssert.StartsWith("walls.json: line 3", errors[0]);
        }

        [Test]
        public void AnUnknownKeyIsReportedWithFileKeyAndLine()
        {
            var source = Patched("pickups.json", t => t.Replace("\"name\": \"Egg\"", "\"nmae\": \"Egg\", \"name\": \"Egg\""));
            var error = Rejected(source).Errors.Single();
            StringAssert.Contains("pickups.json.pickups[", error);
            StringAssert.Contains("unknown key 'nmae'", error);
            StringAssert.Contains("(line ", error);
        }

        [Test]
        public void AnUndeclaredTextureIsReported()
        {
            var source = Patched("pickups.json", t => t.Replace("\"texture\": \"egg\"", "\"texture\": \"eggg\""));
            var error = Rejected(source).Errors.Single();
            StringAssert.Contains("pickups.json", error);
            StringAssert.Contains("'texture'", error);
            StringAssert.Contains("\"eggg\" is not in the textures list", error);
            StringAssert.Contains("(line ", error);
        }

        [Test]
        public void AScriptThatIsNotAScriptIsReported()
        {
            var source = Patched("quests.json", t => t.Replace("CHANGE TIME HOUR 18", "WAIT FOR NIGHT"));
            var error = Rejected(source).Errors.Single();
            StringAssert.Contains("quests.json.quests[2].objectives[1]: 'endScript'", error);
            StringAssert.Contains("does not start with CHANGE, MOVE or RUN", error);
        }

        [Test]
        public void AGapInThePickupIndicesIsReported()
        {
            var source = Patched("pickups.json", t => t.Replace("{\"from\": 106,", "{\"from\": 107,"));
            var errors = Rejected(source).Errors;
            Assert.IsTrue(errors.Any(e => e.Contains("pickups.json.pickups") && e.Contains("defined twice")), string.Join("\n", errors));
            Assert.IsTrue(errors.Any(e => e.Contains("indices must run without gaps (expected 106, found 107)")), string.Join("\n", errors));
        }

        [Test]
        public void AnEnumValueThatIsNotOneOfItsNamesIsReported()
        {
            // Enum.TryParse alone would accept "0" as SingleItem's numeric value; Choice<T> must require the name.
            var source = Patched("pickups.json", t => t.Replace("\"type\": \"SingleItem\", \"value\": 1, \"growSeconds\": 2}", "\"type\": \"0\", \"value\": 1, \"growSeconds\": 2}"));
            var error = Rejected(source).Errors.Single();
            StringAssert.Contains("'type' must be one of", error);
            StringAssert.Contains("(is \"0\")", error);
        }

        [Test]
        public void AValueOutOfRangeIsReportedWithItsRange()
        {
            var source = Patched("weapons.json", t => t.Replace("\"slashing\": 0.5", "\"slashing\": 1.5"));
            var error = Rejected(source).Errors.Single();
            StringAssert.Contains("weapons.json.weapons[0]: 'slashing' must be between 0 and 1 (is 1.5) (line ", error);
        }

        [Test]
        public void ADriftedDatabaseExportIsRejectedByItsSchema()
        {
            var source = Patched("world-2007.json", t => t.Replace("\"name\": \"npcWIDTH\"", "\"name\": \"npcWIDE\""));
            var errors = Rejected(source).Errors;
            Assert.IsTrue(errors.Any(e => e.Contains("world-2007.json.tables.Npc") && e.Contains("npcWIDE") && e.Contains("npcWIDTH")), string.Join("\n", errors));
        }

        [Test]
        public void ADanglingEventScriptInADatabaseIsReported()
        {
            var source = Patched("world-2007.json", t => t.Replace("\"eventBOTTOM\": -0.45, \"scriptID\": 3", "\"eventBOTTOM\": -0.45, \"scriptID\": 33"));
            var error = Rejected(source).Errors.Single();
            StringAssert.Contains("world-2007.json: Event 3 (Entering Dark Woods) runs script 33, which does not exist", error);
        }

        [Test]
        public void SeveralProblemsAreAllReportedInOneLoad()
        {
            var disk = new DirectoryContentSource(ContentDirectory());
            var source = new MemoryContentSource(disk)
                .Set("walls.json", disk.Read("walls.json").Replace("\"from\": [1, 0]", "\"from\": \"x\""))
                .Set("heroes.json", disk.Read("heroes.json").Replace("\"Fine Axe\"", "\"Rusty Axe\""))
                .Set("books.json", disk.Read("books.json").Replace("\"Poetry Book\"", "\"Colesium Vol I\""));
            var errors = Rejected(source).Errors;
            Assert.AreEqual(4, errors.Count, string.Join("\n", errors));
            Assert.IsTrue(errors.Any(e => e.Contains("walls.json") && e.Contains("'from'")));
            Assert.IsTrue(errors.Any(e => e.Contains("heroes.json") && e.Contains("Rusty Axe")));
            Assert.IsTrue(errors.Any(e => e.Contains("books.json") && e.Contains("defined twice")));
            // Renaming the Poetry Book also leaves its pickup opening a book that no longer exists.
            Assert.IsTrue(errors.Any(e => e.Contains("pickups.json") && e.Contains("Poetry Book")));
            // Renaming the Poetry Book also leaves its pickup opening a book that no longer exists.
            Assert.IsTrue(errors.Any(e => e.Contains("pickups.json") && e.Contains("Poetry Book")));
        }

        [Test]
        public void ThePlayableWorldMustBeOneOfTheListedWorldsAndPlayable()
        {
            // The 2006 database has no scenes, ground, start or exits, so it can be kept as a record but not played.
            var source = Patched(ContentRegistry.ManifestFile, t => t.Replace("\"playableWorld\": \"2007\"", "\"playableWorld\": \"2006\""));
            StringAssert.Contains("world-2006.json: the 2006 world cannot be the playable world", Rejected(source).Errors.Single());
            var bad = Patched(ContentRegistry.ManifestFile, t => t.Replace("\"playableWorld\": \"2007\"", "\"playableWorld\": \"2008\""));
            StringAssert.Contains("'playableWorld' \"2008\" is not one of the listed worlds", Rejected(bad).Errors.Single());
        }

        // ---- U.OVW-T38: the 2007 world as the game plays it ---------------------------------------------------

        const string FixesFile = "world-2007-fixes.json";

        [Test]
        public void ThePlayableWorldIsThe2007DesignReadWithItsFixesWhileTheRecordStaysAsTheOriginalHeldIt()
        {
            var play = Registry.PlayableWorld;
            var record = Registry.World("2007");
            Assert.AreNotSame(record, play);
            Assert.AreEqual(("2007", WorldKind.Root), (play.Edition, play.Kind));
            Assert.AreEqual(FixesFile, play.Fixes.File);
            Assert.IsNull(record.Fixes);
            Assert.IsEmpty(play.Warnings);

            // W2, W3: the stray sky strip joins the Grasslands and continues the sky row instead of covering Scene 9.
            var sky8 = play.Scenes.Single(s => s.Id == 11);
            Assert.AreEqual((1, 9f), (sky8.AreaId, sky8.X));
            Assert.AreEqual((0, 8f), (record.Scenes.Single(s => s.Id == 11).AreaId, record.Scenes.Single(s => s.Id == 11).X));

            // W5: every picture is its file's exact relative path.
            Assert.AreEqual("gfx/area/West of Town/new.PNG", play.Scenes.Single(s => s.Id == 5).Image);
            Assert.AreEqual("gfx/area/West of Town/mountain.PNG", play.Scenes.Single(s => s.Id == 10).Image);
            Assert.AreEqual("gfx/prop/structure/gate.PNG", play.FindPropArt(9).Image);
            Assert.AreEqual("gfx/npc/magitek/", play.Npcs[1].Directory);
            Assert.IsTrue(play.Scenes.Concat(record.Scenes).All(s => s.Image.Length > 0));
            Assert.IsFalse(play.Scenes.Select(s => s.Image).Concat(play.PropArt.Select(p => p.Image)).Concat(play.Npcs.Select(n => n.Directory)).Any(p => p.Contains('\\')));
            Assert.AreEqual(@"\gfx\area\West of Town\new.png", record.Scenes.Single(s => s.Id == 5).Image);

            // W6: the spelling.
            Assert.AreEqual("Peasant Woman", play.Npcs[0].Name);
            Assert.AreEqual("Peasent Woman", record.Npcs[0].Name);

            // W8: the Lowlands have a picture and ground.
            var lowlands = play.Scenes.Single(s => s.AreaId == 2);
            Assert.AreEqual((12, "gfx/area/West of Town/bgLandscape2_1x5.jpg", -1f, 2f), (lowlands.Id, lowlands.Image, lowlands.X, lowlands.Width));
            Assert.AreEqual(1, play.Terrain.Count(t => t.AreaId == 2));
            Assert.AreEqual((11, 9), (record.Scenes.Count, record.Terrain.Count));
            Assert.AreEqual((12, 10), (play.Scenes.Count, play.Terrain.Count));

            // Everything the fixes do not touch is read exactly as the record has it.
            Assert.AreEqual(record.AreaProps.Count, play.AreaProps.Count);
            Assert.AreEqual(record.Events.Select(e => (e.Id, e.X, e.ScriptId)), play.Events.Select(e => (e.Id, e.X, e.ScriptId)));
            Assert.AreEqual(record.Enemies.Select(e => e.Name), play.Enemies.Select(e => e.Name));
        }

        [Test]
        public void ANewGameStartsAtTheWestEndOfTheGrasslandsWithThePartyOnTheGround()
        {
            var play = Registry.PlayableWorld;
            Assert.AreEqual("W7", play.Start.Fix);
            Assert.AreEqual("Grasslands", play.FindArea(play.Start.AreaId).Name);
            Assert.AreEqual(new[] { (0, -0.4f), (1, -0.1f), (2, -0.7f) }, play.Start.Heroes.Select(h => (h.HeroId, h.X)).ToArray());
            // The 2006 heroes' order and spacing along x: Belmont 0.3 right of Chrono, Flagg 0.3 left.
            foreach (var h in play.Start.Heroes)
            {
                Assert.AreEqual(Registry.Heroes.Heroes[h.HeroId].Start.X, h.X - play.Start.Heroes[0].X, 1e-5f);
                Assert.IsTrue(WorldPlayability.OnGround(play, play.Start.AreaId, h.X, h.Y), "hero " + h.HeroId);
            }
        }

        [Test]
        public void TheGrasslandsAndLowlandsAreLinkedBothWays()
        {
            var exits = Registry.PlayableWorld.Exits;
            Assert.AreEqual(2, exits.Count);
            var east = exits.Single(e => e.AreaId == 1);
            var west = exits.Single(e => e.AreaId == 2);
            Assert.AreEqual((2, -0.6f, -0.3f), (east.ToAreaId, east.ToX, east.ToY));
            Assert.AreEqual((1, 14.5f, -0.43f), (west.ToAreaId, west.ToX, west.ToY));
            // Standing at the Grasslands' east end, on ground line 7, is inside the exit; arriving is not.
            Assert.IsTrue(east.Contains(14.9f, -0.43f));
            Assert.IsFalse(east.Contains(west.ToX, west.ToY));
            Assert.IsFalse(west.Contains(east.ToX, east.ToY));
        }

        [Test]
        public void EveryFixIsARowOfTheBiblesFixedDefectsTable()
        {
            var bible = File.ReadAllText(Path.Combine(ContentDirectory(), "..", "..", "..", "docs", "BIBLE.md"));
            var table = bible.Substring(bible.IndexOf("## Fixed defects", StringComparison.Ordinal));
            foreach (var fix in Registry.PlayableWorld.Fixes.Fixes)
                StringAssert.Contains("| " + fix.Id + " |", table, "docs/BIBLE.md's fixed-defects table has no row for " + fix.Id);
        }

        [Test]
        public void EveryPictureThePlayableWorldNamesIsAnIntactFileOfTheOriginal()
        {
            // The originals are read-only source material on the owner's machine; elsewhere this check cannot run.
            var parent = Environment.GetEnvironmentVariable("OVERWORLD_ORIGINALS") ?? @"R:\Projects\_PRE_2022\_PRE_2021\_PRE_2020\_PRE_2017";
            var root = Path.Combine(parent, "Overworld");
            if (!Directory.Exists(root)) Assert.Ignore("The original Overworld folder is not at " + root);

            // Each path segment must match the disk's own spelling, case included.
            string Exact(string baseDir, string relative)
            {
                var current = baseDir;
                foreach (var part in relative.TrimEnd('/').Split('/'))
                {
                    var match = Directory.GetFileSystemEntries(current).Select(Path.GetFileName).FirstOrDefault(n => string.Equals(n, part, StringComparison.Ordinal));
                    if (match == null) return null;
                    current = Path.Combine(current, match);
                }

                return current;
            }

            var play = Registry.PlayableWorld;
            var sources = play.Fixes.AssetSources.ToDictionary(a => a.Path, a => a.From);
            var pictures = play.Scenes.Select(s => s.Image).Concat(play.PropArt.Select(p => p.Image)).Distinct().ToList();
            foreach (var picture in pictures)
            {
                var file = sources.TryGetValue(picture, out var from) ? Exact(parent, from) : Exact(root, picture);
                Assert.IsNotNull(file, picture + " is not in the original (with that exact spelling)");
                var head = File.ReadAllBytes(file).Take(8).ToArray();
                var png = head.SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
                var jpg = head[0] == 0xFF && head[1] == 0xD8;
                Assert.IsTrue(png || jpg, picture + " (" + file + ") is not a PNG or JPEG");
            }

            foreach (var npc in play.Npcs)
            {
                var dir = Exact(root, npc.Directory);
                Assert.IsNotNull(dir, npc.Directory);
                Assert.AreEqual(11, Directory.GetFiles(dir, "npc_*.PNG").Length, npc.Directory);
            }

            // The fix is needed: the shipped new.PNG is all zero bytes.
            Assert.IsTrue(File.ReadAllBytes(Exact(root, "gfx/area/West of Town/new.PNG")).All(b => b == 0));
        }

        [Test]
        public void UndoingAFixIsCaughtByThePlayableWorldsRules()
        {
            // W2 undone: Scene 11 back in area 0.
            var w2 = Rejected(Patched(FixesFile, t => t.Replace("\"set\": {\"areaID\": 1}", "\"set\": {\"areaID\": 0}"))).Errors;
            Assert.IsTrue(w2.Any(e => e.Contains("Scene 11") && e.Contains("area 0") && e.Contains("not allowed in the world the game plays")), string.Join("\n", w2));

            // W3 undone: the two sky strips overlap.
            var w3 = Rejected(Patched(FixesFile, t => t.Replace("\"set\": {\"sceneX\": 9}", "\"set\": {\"sceneX\": 8}"))).Errors;
            StringAssert.Contains("Scene 9 (Grasslands Sky 4) and Scene 11 (Grasslands Sky 8) draw over each other at the same depth", w3.Single());

            // W5 undone for one picture.
            var w5 = Rejected(Patched(FixesFile, t => t.Replace("\"gfx/prop/structure/gate.PNG\"", "\"\\\\gfx\\\\prop\\\\structure\\\\gate.png\""))).Errors;
            StringAssert.Contains("Prop 9 (Gateway) names the picture", w5.Single());

            // W8 undone: the Lowlands empty again.
            var w8 = Rejected(Patched(FixesFile, t => t.Replace("\"table\": \"Scene\", \"add\"", "\"table\": \"Scene\", \"_add\""))).Errors;
            Assert.IsTrue(w8.Any(e => e.Contains("area 2 (Lowlands) has no scene to show")), string.Join("\n", w8));

            // W9 undone: no way to the Lowlands.
            var w9 = Rejected(Patched(FixesFile, t => t.Replace("\"area\": 1, \"name\": \"East to the Lowlands\"", "\"area\": 2, \"name\": \"East to the Lowlands\""))).Errors;
            Assert.IsTrue(w9.Any(e => e.Contains("area 2 (Lowlands) cannot be reached from the start")), string.Join("\n", w9));
        }

        [Test]
        public void ABrokenStartOrExitIsNamed()
        {
            var air = Rejected(Patched(FixesFile, t => t.Replace("{\"hero\": 1, \"x\": -0.1, \"y\": -0.3}", "{\"hero\": 1, \"x\": -0.1, \"y\": 0.2}"))).Errors;
            StringAssert.Contains("world-2007-fixes.json.start: hero 1 starts at (-0.1, 0.2), which is not on a ground line of area 1", air.Single());

            var twice = Rejected(Patched(FixesFile, t => t.Replace("{\"hero\": 2, \"x\": -0.7", "{\"hero\": 1, \"x\": -0.7"))).Errors;
            Assert.AreEqual(2, twice.Count, string.Join("\n", twice));
            Assert.IsTrue(twice.Any(e => e.Contains("hero 1 (Belmont) must be placed once (is placed 2 times)")), string.Join("\n", twice));
            Assert.IsTrue(twice.Any(e => e.Contains("hero 2 (Flagg) must be placed once (is placed 0 times)")), string.Join("\n", twice));

            var bounce = Rejected(Patched(FixesFile, t => t.Replace("\"toX\": -0.6", "\"toX\": -0.9"))).Errors;
            StringAssert.Contains("lands the party inside the exit \"West to the Grasslands\"", bounce.Single());
        }

        [Test]
        public void AFixThatMatchesNoRowOrNamesNoColumnIsReported()
        {
            var none = Rejected(Patched(FixesFile, t => t.Replace("\"where\": {\"npcID\": 1}, \"set\": {\"npcNAME\"", "\"where\": {\"npcID\": 7}, \"set\": {\"npcNAME\""))).Errors;
            StringAssert.Contains("fix W6's 'where' matches 0 rows of Npc (it must match exactly one)", none.Single());

            var column = Rejected(Patched(FixesFile, t => t.Replace("\"set\": {\"npcNAME\"", "\"set\": {\"npcTITLE\""))).Errors;
            StringAssert.Contains("fix W6's 'set' names the column 'npcTITLE', which Npc does not have", column.Single());

            var cited = Rejected(Patched(FixesFile, t => t.Replace("\"start\": {\"fix\": \"W7\"", "\"start\": {\"fix\": \"W70\""))).Errors;
            StringAssert.Contains("'start' cites the fix \"W70\", which is not in 'fixes'", cited.Single());
        }
    }
}
