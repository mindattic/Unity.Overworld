using System.Globalization;
using MindAttic.Overworld.Core;
using UnityEngine;

namespace MindAttic.Overworld.Runtime
{
    /// <summary>
    /// The developer tools (U.OVW-T47; BIBLE "Debug keys"): installed last, so the path dots are laid after the heroes have
    /// moved (checkPathPoints ran in the draw); on when developer.json says so (off as shipped) or with
    /// <c>-overworld-developer</c>. For scripted runs, <c>-overworld-developer-keys &lt;key,...&gt;</c> presses those keys
    /// once each at the start (f1, f2, f3, f5, g, h, u, l, p, o; <c>minus:n</c> and <c>equals:n</c> hold - or = for n frames)
    /// and turns the tools on.
    /// </summary>
    public sealed partial class OverworldBootstrap
    {
        public const string DeveloperArgument = "-overworld-developer";
        public const string DeveloperKeysArgument = "-overworld-developer-keys";

        public DeveloperTools Developer { get; private set; }

        void InstallDeveloper(string[] args, Game game)
        {
            var keys = ArgumentAfter(args, DeveloperKeysArgument);
            bool? on = HasFlag(args, DeveloperArgument) || keys != null ? true : (bool?)null;
            Developer = new DeveloperTools(Content.Developer, Party, Clock, Messages, on).Install(game);
            World.ShowDeveloper(Developer, Content.Walls);
            Input.Developer = Developer;
            if (Developer.Enabled) Debug.Log("Unity.Overworld: the developer tools are on.");
            if (keys == null) return;

            Input.UseDevices = false;
            foreach (var raw in keys.Split(','))
            {
                var key = raw.Trim().ToLowerInvariant();
                var times = 1;
                var colon = key.IndexOf(':');
                if (colon > 0)
                {
                    if (!int.TryParse(key.Substring(colon + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out times) || times < 1)
                    {
                        Debug.LogError("Unity.Overworld: " + DeveloperKeysArgument + " wants key:times with a whole number of times, not '" + raw + "'.");
                        continue;
                    }

                    key = key.Substring(0, colon);
                }

                var command = DeveloperKey(key);
                if (command == null)
                {
                    if (key.Length > 0) Debug.LogError("Unity.Overworld: " + DeveloperKeysArgument + " does not know the key '" + raw + "'.");
                    continue;
                }

                for (var i = 0; i < times; i++)
                {
                    var result = game.Submit(command);
                    if (!result.Accepted) Debug.LogError("Unity.Overworld: the developer key " + key + " was refused: " + result.Reason);
                }
            }

            Debug.Log("Unity.Overworld: developer keys " + keys + ": game speed " + Developer.GameSpeed.ToString(CultureInfo.InvariantCulture) +
                      ", ring icons " + Party.RingOf(PlayerId.One).Count + ", path " + Developer.Trail.Mode + ", wireframe " + Developer.Wireframe +
                      ", textures " + Developer.Textures + ", culling " + Developer.BackFaceCulling + ", linear filter " + Developer.LinearFilter);
        }

        static ICommand DeveloperKey(string key)
        {
            var p = PlayerId.One;
            switch (key)
            {
                case "f1": return new ToggleRenderSwitch(p, RenderSwitch.Wireframe);
                case "f2": return new ToggleRenderSwitch(p, RenderSwitch.Textures);
                case "f3": return new ToggleRenderSwitch(p, RenderSwitch.BackFaceCulling);
                case "f5": return new ToggleRenderSwitch(p, RenderSwitch.LinearFilter);
                case "g": return new ResizeRing(p, -1);
                case "h": return new ResizeRing(p, 1);
                case "u": return new ToggleHeroMode(p);
                case "l": return new ShowTestMessage(p);
                case "p": return new CyclePathMode(p);
                case "o": return new ClearPath(p);
                case "minus": return new ChangeGameSpeed(p, -1);
                case "equals": return new ChangeGameSpeed(p, 1);
                default: return null;
            }
        }
    }
}
