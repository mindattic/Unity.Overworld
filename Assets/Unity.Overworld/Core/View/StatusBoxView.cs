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
    /// right, its name in (255 - r, 255, b). The battle line ("NN %" or "Ready!") is the battle story's (A18) and the
    /// hero-select brackets the interface story's (A20).
    /// </summary>
    public static class StatusBoxView
    {
        public static IReadOnlyList<HudLine> Lines(StatusBoxDefinition box, Party party, int leader, LightColour light)
        {
            var lines = new List<HudLine>();
            if (box == null || party == null || leader < 0 || leader >= party.Heroes.Count) return lines;
            var values = new TextColour(255, (byte)(255 - light.R), 255, (byte)(255 - light.B));
            Column(lines, box, party.Heroes[leader], 0f, new TextColour(255, 255, 255, 255), values);
            if (!party.Team) return lines;

            var offset = box.TeamSpacing;
            var teamName = new TextColour(255, (byte)(255 - light.R), 255, light.B);
            for (var h = 0; h < party.Heroes.Count; h++)
            {
                if (h == leader) continue;
                Column(lines, box, party.Heroes[h], offset, teamName, values);
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

        static void Row(List<HudLine> lines, StatusBoxDefinition box, float x, int row, string value, int max, TextColour colour)
        {
            var dy = row * box.RowSpacing;
            lines.Add(new HudLine(value, x + box.Value.X, box.Y + box.Value.Y + dy, box.Font, colour));
            lines.Add(new HudLine("  / " + max, x + box.Maximum.X, box.Y + box.Maximum.Y + dy, box.Font, colour));
        }
    }
}
