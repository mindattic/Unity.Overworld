using System;
using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>
    /// What the world the game plays must satisfy beyond being a valid record (U.OVW-T38). No code of the original
    /// ever read the 2007 database, so nothing ever proved it could be played; these rules do, every load:
    /// <list type="bullet">
    /// <item>it is the 2007 design (the only one with scenes and ground) and was read with its fixes;</item>
    /// <item>no record points at an area that does not exist (the original's own dangling references are errors here, not warnings);</item>
    /// <item>every area has a picture and a ground line, and no two pictures draw over each other at the same depth;</item>
    /// <item>every picture is named as a relative "gfx/..." path with forward slashes, as the asset import keys files;</item>
    /// <item>a new game has a start: an area, and every hero of heroes.json once, each standing on a ground line;</item>
    /// <item>every exit leads to another area, can be stood in, lands the party on ground outside that area's exits,
    /// and every area can be reached from the start and can get back to it.</item>
    /// </list>
    /// Pictures span x to x + width and y - height to y + height: the 2007 design gives the half-height, as its
    /// 2005 sibling engine's setupVertex built quads (RECONSTRUCTION 16.2).
    /// </summary>
    public static class WorldPlayability
    {
        /// <summary>How far, in world units, a point may sit from a ground line and still stand on it.</summary>
        public const float GroundTolerance = 0.001f;

        public static void Check(WorldData world, HeroData heroes, List<string> errors)
        {
            var file = world.Database.File;
            if (world.Kind != WorldKind.Root)
            {
                errors.Add(file + ": the " + world.Edition + " world cannot be the playable world: it has no scenes, ground, start or exits (only the 2007 design does)");
                return;
            }

            if (world.Fixes == null)
            {
                errors.Add(file + ": the playable world must be read with its fixes file (the manifest entry's 'fixes')");
                return;
            }

            foreach (var w in world.Warnings) errors.Add(w + " (not allowed in the world the game plays)");

            foreach (var a in world.Areas)
            {
                if (!world.Scenes.Exists(s => s.AreaId == a.Id)) errors.Add(file + ": area " + a.Id + " (" + a.Name + ") has no scene to show");
                if (!world.Terrain.Exists(t => t.AreaId == a.Id)) errors.Add(file + ": area " + a.Id + " (" + a.Name + ") has no ground line to stand on");
            }

            foreach (var e in world.Events)
                if (world.FindArea(e.AreaId) == null) errors.Add(file + ": Event " + e.Id + " is in area " + e.AreaId + ", which does not exist");

            for (var i = 0; i < world.Scenes.Count; i++)
            for (var j = i + 1; j < world.Scenes.Count; j++)
            {
                var a = world.Scenes[i];
                var b = world.Scenes[j];
                if (a.AreaId == b.AreaId && a.Z == b.Z &&
                    a.X < b.X + b.Width && b.X < a.X + a.Width &&
                    a.Y - a.Height < b.Y + b.Height && b.Y - b.Height < a.Y + a.Height)
                    errors.Add(file + ": Scene " + a.Id + " (" + a.Name + ") and Scene " + b.Id + " (" + b.Name + ") draw over each other at the same depth");
            }

            foreach (var s in world.Scenes) Picture(file, "Scene " + s.Id + " (" + s.Name + ")", s.Image, false, errors);
            foreach (var p in world.PropArt) Picture(file, "Prop " + p.Id + " (" + p.Name + ")", p.Image, false, errors);
            foreach (var n in world.Npcs) Picture(file, "Npc " + n.Id + " (" + n.Name + ")", n.Directory, true, errors);
            foreach (var a in world.Fixes.AssetSources) Picture(world.Fixes.File, "the asset source", a.Path, false, errors);

            CheckStart(world, heroes, file, errors);
            CheckExits(world, file, errors);
        }

        /// <summary>True when (x, y) lies on one of the area's ground lines.</summary>
        public static bool OnGround(WorldData world, int areaId, float x, float y)
        {
            foreach (var t in world.Terrain)
            {
                if (t.AreaId != areaId) continue;
                if (x < Math.Min(t.X1, t.X2) || x > Math.Max(t.X1, t.X2)) continue;
                var groundY = t.X2 == t.X1 ? t.Y1 : t.Y1 + (t.Y2 - t.Y1) * (x - t.X1) / (t.X2 - t.X1);
                if (Math.Abs(groundY - y) <= GroundTolerance) return true;
            }

            return false;
        }

        static void Picture(string file, string what, string path, bool folder, List<string> errors)
        {
            if (path.IndexOf('\\') >= 0 || path.StartsWith("/", StringComparison.Ordinal) || !path.StartsWith("gfx/", StringComparison.Ordinal))
                errors.Add(file + ": " + what + " names the picture \"" + path + "\" (must be a relative gfx/... path with forward slashes)");
            else if (folder != path.EndsWith("/", StringComparison.Ordinal))
                errors.Add(file + ": " + what + " names \"" + path + "\" (" + (folder ? "a folder must end with /" : "a picture must name a file") + ")");
        }

        static void CheckStart(WorldData world, HeroData heroes, string file, List<string> errors)
        {
            var start = world.Start;
            if (start == null)
            {
                errors.Add(file + ": the playable world has no start");
                return;
            }

            var where = world.Fixes.File + ".start";
            if (world.FindArea(start.AreaId) == null)
            {
                errors.Add(where + ": the start area " + start.AreaId + " does not exist");
                return;
            }

            if (heroes != null)
            {
                foreach (var h in heroes.Heroes)
                {
                    var count = 0;
                    foreach (var s in start.Heroes) if (s.HeroId == h.Id) count++;
                    if (count != 1) errors.Add(where + ": hero " + h.Id + " (" + h.Name + ") must be placed once (is placed " + count + " times)");
                }

                foreach (var s in start.Heroes)
                {
                    var known = false;
                    foreach (var h in heroes.Heroes) known |= h.Id == s.HeroId;
                    if (!known) errors.Add(where + ": hero " + s.HeroId + " is not in heroes.json");
                }
            }

            foreach (var s in start.Heroes)
            {
                if (!OnGround(world, start.AreaId, s.X, s.Y))
                    errors.Add(where + ": hero " + s.HeroId + " starts at (" + s.X + ", " + s.Y + "), which is not on a ground line of area " + start.AreaId);
                foreach (var e in world.Exits)
                    if (e.AreaId == start.AreaId && e.Contains(s.X, s.Y))
                        errors.Add(where + ": hero " + s.HeroId + " starts inside the exit \"" + e.Name + "\"");
            }
        }

        static void CheckExits(WorldData world, string file, List<string> errors)
        {
            var where = world.Fixes.File + ".exits";
            foreach (var e in world.Exits)
            {
                var name = "the exit \"" + e.Name + "\"";
                if (world.FindArea(e.AreaId) == null) { errors.Add(where + ": " + name + " is in area " + e.AreaId + ", which does not exist"); continue; }
                if (world.FindArea(e.ToAreaId) == null) { errors.Add(where + ": " + name + " leads to area " + e.ToAreaId + ", which does not exist"); continue; }
                if (e.AreaId == e.ToAreaId) errors.Add(where + ": " + name + " leads back into its own area");

                // A hero can stand in it: some ground of its area runs through the rectangle.
                var reachable = false;
                foreach (var t in world.Terrain)
                {
                    if (t.AreaId != e.AreaId) continue;
                    for (var k = 0; k <= 8 && !reachable; k++)
                    {
                        var x = e.X + e.Left + (e.Right - e.Left) * k / 8f;
                        if (x < Math.Min(t.X1, t.X2) || x > Math.Max(t.X1, t.X2)) continue;
                        var y = t.X2 == t.X1 ? t.Y1 : t.Y1 + (t.Y2 - t.Y1) * (x - t.X1) / (t.X2 - t.X1);
                        reachable = e.Contains(x, y);
                    }
                }

                if (!reachable) errors.Add(where + ": no ground of area " + e.AreaId + " runs through " + name + ", so no hero can stand in it");
                if (!OnGround(world, e.ToAreaId, e.ToX, e.ToY))
                    errors.Add(where + ": " + name + " lands the party at (" + e.ToX + ", " + e.ToY + "), which is not on a ground line of area " + e.ToAreaId);
                foreach (var back in world.Exits)
                    if (back.AreaId == e.ToAreaId && back.Contains(e.ToX, e.ToY))
                        errors.Add(where + ": " + name + " lands the party inside the exit \"" + back.Name + "\", which would send it straight back");
            }

            if (world.Start == null || world.FindArea(world.Start.AreaId) == null) return;
            var from = Reach(world, world.Start.AreaId, forward: true);
            var to = Reach(world, world.Start.AreaId, forward: false);
            foreach (var a in world.Areas)
            {
                if (!from.Contains(a.Id)) errors.Add(file + ": area " + a.Id + " (" + a.Name + ") cannot be reached from the start (no exit leads to it)");
                else if (!to.Contains(a.Id)) errors.Add(file + ": area " + a.Id + " (" + a.Name + ") has no way back to the start");
            }
        }

        static HashSet<int> Reach(WorldData world, int origin, bool forward)
        {
            var seen = new HashSet<int> { origin };
            var queue = new Queue<int>();
            queue.Enqueue(origin);
            while (queue.Count > 0)
            {
                var area = queue.Dequeue();
                foreach (var e in world.Exits)
                {
                    var next = forward ? (e.AreaId == area ? e.ToAreaId : -1) : (e.ToAreaId == area ? e.AreaId : -1);
                    if (next >= 0 && world.FindArea(next) != null && seen.Add(next)) queue.Enqueue(next);
                }
            }

            return seen;
        }
    }
}
