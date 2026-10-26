using System;
using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>A line of HUD text to write this frame: its text, its point on the 640 x 480 screen, font and colour.</summary>
    public readonly struct HudLine
    {
        public HudLine(string text, float x, float y, HudFont font, TextColour colour)
        {
            Text = text;
            X = x;
            Y = y;
            Font = font;
            Colour = colour;
        }

        public string Text { get; }
        public float X { get; }
        public float Y { get; }
        public HudFont Font { get; }
        public TextColour Colour { get; }

        public override string ToString() => "'" + Text + "' at (" + X + ", " + Y + ")";
    }

    /// <summary>
    /// What the status box writes (Gui/StatusBox.vb renderStatus, RECONSTRUCTION 13), read from the party for one player:
    /// the hero that player leads, '  Name  ' in white, its "HP n  / max", "MP n  / max" and "AP n  / max" in the inverse
    /// of the world light's red and blue (green by day); in team mode each other hero in party order, a column further
    /// right, its name in (255 - r, 255, b). A hero in battle mode has a battle line under its column: its gauge as "NN %"
    /// (the others' "NN%") fading in as it fills, or "Ready!" when full. The hero-select brackets are the interface
    /// story's (A20).
    /// </summary>
    public static class StatusBoxView
    {
        public static IReadOnlyList<HudLine> Lines(StatusBoxDefinition box, Party party, int leader, LightColour light)
        {
            var lines = new List<HudLine>();
            if (box == null || party == null || leader < 0 || leader >= party.Heroes.Count) return lines;
            var values = new TextColour(255, (byte)(255 - light.R), 255, (byte)(255 - light.B));
            Column(lines, box, party.Heroes[leader], 0f, new TextColour(255, 255, 255, 255), values);
            BattleLine(lines, box, party.Heroes[leader], 0f, true, light);
            if (!party.Team) return lines;

            var offset = box.TeamSpacing;
            var teamName = new TextColour(255, (byte)(255 - light.R), 255, light.B);
            for (var h = 0; h < party.Heroes.Count; h++)
            {
                if (h == leader) continue;
                Column(lines, box, party.Heroes[h], offset, teamName, values);
                BattleLine(lines, box, party.Heroes[h], offset, false, light);
                offset += box.TeamSpacing;
            }

            return lines;
        }

        static void Column(List<HudLine> lines, StatusBoxDefinition box, Hero hero, float offset, TextColour name, TextColour values)
        {
            var x = box.X + offset;
            lines.Add(new HudLine("  " + hero.Name + "  ", x + box.Name.X, box.Y + box.Name.Y, box.Font, name));
            Row(lines, box, x, 0, "HP " + hero.Health, hero.MaxHealth, values);
            Row(lines, box, x, 1, "MP " + hero.Mana, hero.MaxMana, values);
            Row(lines, box, x, 2, "AP " + hero.Stamina, hero.MaxStamina, values);
        }

        /// <summary>
        /// renderStatus's battle line for a hero in battle mode: below its maximum, CInt(stamina / max x 100) as "NN %" (the
        /// leader) or "NN%" (the others) in (CByte(255 x percent / 100), 255 - r, 255, b); full, "Ready!" in (255, 255,
        /// 255, b) for the leader and (255, 255 - r, 255, b) for the others.
        /// </summary>
        static void BattleLine(List<HudLine> lines, StatusBoxDefinition box, Hero hero, float offset, bool leader, LightColour light)
        {
            if (hero.Mode != HeroMode.Battle || box.BattleFont == null) return;
            var x = box.X + offset + box.Battle.X;
            var y = box.Y + box.Battle.Y;
            if (hero.Stamina != hero.MaxStamina)
            {
                var percent = Percent(hero.Stamina, hero.MaxStamina);
                var alpha = (byte)Math.Max(0, Math.Min(255, Math.Round(255.0 * percent / 100.0, MidpointRounding.ToEven)));
                lines.Add(new HudLine(percent + (leader ? " %" : "%"), x, y, box.BattleFont, new TextColour(alpha, (byte)(255 - light.R), 255, light.B)));
            }
            else
            {
                lines.Add(new HudLine("Ready!", x, y, box.BattleFont, leader ? new TextColour(255, 255, 255, light.B) : new TextColour(255, (byte)(255 - light.R), 255, light.B)));
            }
        }

        /// <summary>CInt(stamina / max x 100): the gauge's share as a whole percent, rounded half to even.</summary>
        public static int Percent(int stamina, int max) => max <= 0 ? 0 : (int)Math.Round((double)stamina / max * 100.0, MidpointRounding.ToEven);

        static void Row(List<HudLine> lines, StatusBoxDefinition box, float x, int row, string value, int max, TextColour colour)
        {
            var dy = row * box.RowSpacing;
            lines.Add(new HudLine(value, x + box.Value.X, box.Y + box.Value.Y + dy, box.Font, colour));
            lines.Add(new HudLine("  / " + max, x + box.Maximum.X, box.Y + box.Maximum.Y + dy, box.Font, colour));
        }
    }
}
