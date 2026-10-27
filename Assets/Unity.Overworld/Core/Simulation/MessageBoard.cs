using System;
using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>A message shown on screen: its text, font and colour, where it is on the 640 x 480 screen and how faded.</summary>
    public sealed class ShownMessage
    {
        internal ShownMessage(string caption, HudFont font, byte r, byte g, byte b, int x, int y)
        {
            Caption = caption;
            Font = font;
            R = r;
            G = g;
            B = b;
            A = 255;
            X = x;
            Y = y;
        }

        public string Caption { get; }
        public HudFont Font { get; }
        public byte R { get; }
        public byte G { get; }
        public byte B { get; }

        /// <summary>Its alpha: 255 when shown, down by the fade each tick; gone at 0.</summary>
        public byte A { get; internal set; }

        public int X { get; }

        /// <summary>The top of the text, from the screen centre downward by the drift each tick.</summary>
        public int Y { get; internal set; }

        public override string ToString() => "\"" + Caption + "\" at (" + X + ", " + Y + ") alpha " + A;
    }

    /// <summary>
    /// The original's message queue (Gui/Messages.vb addMessage, MessageHandler, renderMessages; RECONSTRUCTION 13), one
    /// step a 60 Hz tick: a message waits in the queue (up to the maximum, a message added to a full queue replacing the
    /// last); the first waiting one is shown at the screen centre, less half the caption's width at the original's 6 pixels
    /// a character, once the last one shown has drifted more than the spacing below the centre or is gone; each shown
    /// message then loses the fade from 255 and drifts down each tick, and is gone at alpha 0 (the step that would take
    /// it below 0 sets it to 0 without drifting, as the original's byte overflow did). Used for the day period
    /// announcements, the script failures and the tutorial (<see cref="SayTutorial"/>).
    /// Presentation reads <see cref="Shown"/>.
    /// </summary>
    public sealed class MessageBoard : ITickSystem
    {
        readonly List<ShownMessage> waiting = new List<ShownMessage>();
        readonly List<ShownMessage> shown = new List<ShownMessage>();
        ShownMessage last;

        public MessageBoard(HudData hud)
        {
            Hud = hud ?? throw new ArgumentNullException(nameof(hud));
            Tuning = hud.Messages;
            CentreX = hud.ScreenWidth / 2;
            CentreY = hud.ScreenHeight / 2;
        }

        public HudData Hud { get; }
        public MessageTuning Tuning { get; }
        public int CentreX { get; }
        public int CentreY { get; }

        /// <summary>The messages on screen now, oldest first.</summary>
        public IReadOnlyList<ShownMessage> Shown => shown;

        /// <summary>How many wait to be shown.</summary>
        public int Waiting => waiting.Count;

        public MessageBoard Install(Game game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            game.AddSystem(this);
            return this;
        }

        /// <summary>addMessage: queues <paramref name="caption"/> in <paramref name="font"/> and the colour.</summary>
        public void Add(string caption, HudFont font, byte r, byte g, byte b)
        {
            var message = new ShownMessage(caption ?? string.Empty, font, r, g, b, 0, 0);
            if (waiting.Count < Tuning.MaxMessages) waiting.Add(message);
            else waiting[waiting.Count - 1] = message;
        }

        /// <summary>sayTutorial: queues the tutorial's lines (hud.json "tutorial"), the blank ones spacing them out.</summary>
        public void SayTutorial()
        {
            var t = Hud.Tutorial;
            if (t == null) return;
            foreach (var line in t.Lines) Add(line, t.Font, t.Colour.R, t.Colour.G, t.Colour.B);
        }

        public void Step(Game game, long tick, ICollection<IGameEvent> events)
        {
            // MessageHandler: show the next one once the last has moved on.
            if (waiting.Count > 0 && (last == null || last.A == 0 || last.Y > CentreY + Tuning.Spacing))
            {
                var next = waiting[0];
                waiting.RemoveAt(0);
                last = new ShownMessage(next.Caption, next.Font, next.R, next.G, next.B,
                    CentreX - next.Caption.Length * Tuning.CharacterWidth / 2, CentreY);
                shown.Add(last);
            }

            // renderMessages: fade and drift, then free the ones at alpha 0.
            for (var i = 0; i < shown.Count; i++)
            {
                var m = shown[i];
                if (m.A > 0)
                {
                    if (m.A < Tuning.Fade)
                    {
                        m.A = 0;
                        continue;
                    }

                    m.A = (byte)(m.A - Tuning.Fade);
                }

                m.Y += Tuning.Drift;
            }

            shown.RemoveAll(m => m.A == 0);
        }
    }
}
