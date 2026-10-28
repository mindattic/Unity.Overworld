using System;
using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>
    /// The developer tools (U.OVW-T47; RECONSTRUCTION 5.1, 5.2 and 14.3; BIBLE "Debug keys"), developer.json: the switch
    /// that turns them on (off in the shipped build) and every figure they use: the - and = game-speed keys' step and
    /// floor, the P / O path dots (Path.vb), the wall lines' picture (renderwall) and the L key's test message.
    /// </summary>
    public sealed class DeveloperData
    {
        /// <summary>The developer tools are on: the debug keys answer and the wall lines are drawn (false as shipped).</summary>
        public bool Enabled { get; internal set; }

        /// <summary>What each press of - or = takes off or adds to the game speed, every frame it is held (0.01).</summary>
        public float GameSpeedStep { get; internal set; }

        /// <summary>The game speed never goes below this (0).</summary>
        public float GameSpeedFloor { get; internal set; }

        public PathTuning Path { get; internal set; }

        /// <summary>The wall lines' picture (gfx/gui/terrain.PNG, one orange pixel): drawn only with the tools on.</summary>
        public string WallPicture { get; internal set; }

        /// <summary>The L key's message (addMessage("WHAt's up, this worked!!!!", fontArial, 255, 255, 255)).</summary>
        public string TestText { get; internal set; }

        public HudFont TestFont { get; internal set; }
        public LightColour TestColour { get; internal set; }

        internal static DeveloperData Read(IContentSource source, string file, HudData hud, List<string> errors)
        {
            var root = ContentFiles.Open(source, file, errors);
            if (root == null) return null;
            var fonts = hud?.Fonts ?? Array.Empty<HudFont>();
            var data = new DeveloperData { Enabled = root.Flag("enabled") };

            var speed = root.Section("gameSpeed");
            data.GameSpeedStep = speed.Positive("step");
            data.GameSpeedFloor = speed.Number("floor", 0f);
            speed.Done();

            data.Path = PathTuning.Read(root.Section("path"));

            var walls = root.Section("walls");
            data.WallPicture = walls.Text("picture");
            walls.Done();

            var test = root.Section("testMessage");
            data.TestText = test.Text("text");
            var name = test.Text("font");
            foreach (var f in fonts) if (f.Name == name) data.TestFont = f;
            if (data.TestFont == null && name.Length > 0) test.Fail("font", "\"" + name + "\" is not one of hud.json's fonts");
            data.TestColour = TimeData.ReadColour(test, "colour");
            test.Done();

            root.Done();
            return data;
        }
    }

    /// <summary>
    /// The path dots (Path.vb, RECONSTRUCTION 14.3), developer.json "path": in "set" mode the leader lays a dot of
    /// <see cref="Picture"/> whenever its centre is more than <see cref="Range"/> along its facing axis from the last dot,
    /// up to <see cref="Slots"/> slots (slot 0 is the unused start, far off at <see cref="Start"/>); O clears the last
    /// <see cref="Clear"/>.
    /// </summary>
    public sealed class PathTuning
    {
        /// <summary>The dot's picture (gfx/gui/path.PNG).</summary>
        public string Picture { get; internal set; }

        /// <summary>The dot's quad: this wide and half as high above and below its point (pathSize, 0.02).</summary>
        public float Size { get; internal set; }

        /// <summary>A new dot is laid once the leader's centre is not within this of the last on its facing axis (pathRange, 0.05).</summary>
        public float Range { get; internal set; }

        /// <summary>A dot is laid this far above the leader's centre (0.01).</summary>
        public float Lift { get; internal set; }

        /// <summary>The slots, slot 0 included (maxPaths 1000: 1001); once full the last is laid again and again.</summary>
        public int Slots { get; internal set; }

        /// <summary>O clears this many (clearpath(20)).</summary>
        public int Clear { get; internal set; }

        /// <summary>Slot 0's place, never drawn: the first dot is always laid (-1000, -1000).</summary>
        public DataPoint Start { get; internal set; }

        internal static PathTuning Read(ContentReader r)
        {
            var t = new PathTuning
            {
                Picture = r.Text("picture"),
                Size = r.Positive("size"),
                Range = r.Positive("range"),
                Lift = r.Number("lift"),
                Slots = r.Integer("slots", 2, 100000),
                Clear = r.Integer("clear", 1),
                Start = r.Pair("start"),
            };
            r.Done();
            return t;
        }
    }
}
