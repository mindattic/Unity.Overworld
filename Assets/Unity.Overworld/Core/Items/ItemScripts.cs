using System;
using System.Collections.Generic;
using System.Globalization;

namespace MindAttic.Overworld.Core
{
    /// <summary>
    /// The part of the script language (Script.vb, RECONSTRUCTION 9) the items' own scripts need before the script story
    /// (A14) brings the whole interpreter, run through the Core commands that already exist:
    /// <list type="bullet">
    /// <item><c>CHANGE HERO|HERO1|HERO2|HERO3 HP|HEALTH|LIFE, MP|MANA|MAGIC, AP|STAMINA|ENERGY n</c>: a +/- amount is
    /// added (capped at the maximum, as cmdChangeHero did, and kept at 0 or more like every other change of a vital), any
    /// other number assigned (a leading = stripped) within 0 and the maximum, as <see cref="SetHeroVitals"/>;</item>
    /// <item><c>CHANGE HERO STATUS EXHAUSTED|TIRED|ANGRY|ENRAGED|POISONED|POISON|FINE</c>;</item>
    /// <item><c>CHANGE HERO GOLD|MONEY n</c> (the party's gold, added or assigned);</item>
    /// <item><c>CHANGE HERO AI LAY|LAYDOWN|SLEEP</c>, with a <c>CHANGE TIME HOUR h</c> in the same script the draught's
    /// sleep until that hour (<see cref="SleepUntil"/>, BIBLE), without one a sleep of the chosen hours; <c>AI
    /// REST|SIT|KNEEL</c> kneels; <c>CHANGE TIME HOUR h</c> alone sets the clock;</item>
    /// <item><c>CHANGE INVENTORY OPEN|CLOSE ON|TRUE|OFF|FALSE</c>.</item>
    /// </list>
    /// HERO is the hero the item is used on (selectedHero). A script with any other command (the Rune's teleport, the
    /// books, the Rusty Sword's WEAPON) is not run at all and the run reports false, so the item stays in the pack until
    /// the story that runs it.
    /// </summary>
    public sealed class ItemScripts : IScriptRunner
    {
        public ItemScripts(Party party, Inventory inventory, SleepCycle sleep = null)
        {
            Party = party ?? throw new ArgumentNullException(nameof(party));
            Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            Sleep = sleep;
        }

        public Party Party { get; }
        public Inventory Inventory { get; }

        /// <summary>The sleep a draught starts and the clock it sets (null: no AI LAYDOWN or TIME HOUR runs).</summary>
        public SleepCycle Sleep { get; }

        /// <summary>The panel a script opens or closes (null: no CHANGE INVENTORY runs).</summary>
        public InventoryControl Control { get; set; }

        enum Kind { Vital, Status, Gold, Lay, Kneel, Hour, Inventory }

        sealed class Action
        {
            public Kind Kind;
            public int Hero;
            public string Item;
            public bool Add;
            public int Amount;
            public HeroStatus? Status;
            public bool Open;
        }

        public bool Run(Game game, PlayerId player, int hero, string script, long tick, ICollection<IGameEvent> events)
        {
            var actions = Parse(script, hero);
            if (actions == null) return false;

            int? hour = null;
            var laid = false;
            foreach (var a in actions)
            {
                if (a.Kind == Kind.Hour) hour = a.Amount;
                if (a.Kind == Kind.Lay) laid = true;
            }

            foreach (var a in actions)
            {
                switch (a.Kind)
                {
                    case Kind.Vital:
                        ChangeVital(a, tick, events);
                        break;
                    case Kind.Status:
                        if (a.Status.HasValue) Party.SetVitals(a.Hero, null, null, null, a.Status, tick, events);
                        break;
                    case Kind.Gold:
                        var before = Inventory.Gold;
                        Inventory.Gold = a.Add ? before + a.Amount : a.Amount;
                        events.Add(new GoldChanged(tick, player, Inventory.Gold, before));
                        break;
                    case Kind.Lay:
                        events.Add(Sleep.LayDown(player, a.Hero, hour, tick));
                        break;
                    case Kind.Kneel:
                        var h = Party.Heroes[a.Hero];
                        h.Kneeling = true;
                        h.State = HeroState.Kneeling;
                        break;
                    case Kind.Hour:
                        if (!laid) Sleep.Clock.SetHour(player, a.Amount, null, true, tick, events);
                        break;
                    case Kind.Inventory:
                        Control.SetOpen(player, a.Open, tick, events);
                        break;
                }
            }

            return true;
        }

        /// <summary>cmdChangeHero's HP, MP and AP: an addition capped at the maximum, or an assignment within 0 and the maximum.</summary>
        void ChangeVital(Action a, long tick, ICollection<IGameEvent> events)
        {
            var hero = Party.Heroes[a.Hero];
            int current, max;
            switch (a.Item)
            {
                case "HP": current = hero.Health; max = hero.MaxHealth; break;
                case "MP": current = hero.Mana; max = hero.MaxMana; break;
                default: current = hero.Stamina; max = hero.MaxStamina; break;
            }

            var value = a.Add ? (int)Math.Min((long)max, (long)current + a.Amount) : a.Amount;
            switch (a.Item)
            {
                case "HP": Party.SetVitals(a.Hero, value, null, null, null, tick, events); break;
                case "MP": Party.SetVitals(a.Hero, null, value, null, null, tick, events); break;
                default: Party.SetVitals(a.Hero, null, null, value, null, tick, events); break;
            }
        }

        /// <summary>Every command of <paramref name="script"/> as an action, or null when any is not one this runner knows.</summary>
        List<Action> Parse(string script, int hero)
        {
            if (string.IsNullOrWhiteSpace(script)) return null;
            if (hero < 0 || hero >= Party.Heroes.Count) return null;
            var actions = new List<Action>();
            foreach (var part in script.ToUpperInvariant().Split(new[] { " | " }, StringSplitOptions.None))
            {
                var w = part.Trim().Split(' ');
                if (w.Length != 4 || w[0] != "CHANGE") return null;
                var action = new Action();
                switch (w[1])
                {
                    case "HERO": action.Hero = hero; break;
                    case "HERO1": action.Hero = 0; break;
                    case "HERO2": action.Hero = 1; break;
                    case "HERO3": action.Hero = 2; break;
                    case "TIME":
                        if (w[2] != "HOUR" || Sleep == null || !int.TryParse(w[3], NumberStyles.None, CultureInfo.InvariantCulture, out var h) || h > 23) return null;
                        action.Kind = Kind.Hour;
                        action.Amount = h;
                        actions.Add(action);
                        continue;
                    case "INVENTORY":
                        if (Control == null || (w[2] != "OPEN" && w[2] != "CLOSE")) return null;
                        bool? on = w[3] == "ON" || w[3] == "TRUE" ? true : w[3] == "OFF" || w[3] == "FALSE" ? false : (bool?)null;
                        if (!on.HasValue) continue; // cmdChangeInventory ignored any other parameter
                        action.Kind = Kind.Inventory;
                        action.Open = w[2] == "OPEN" ? on.Value : !on.Value;
                        actions.Add(action);
                        continue;
                    default:
                        return null;
                }

                if (action.Hero >= Party.Heroes.Count) return null;
                switch (w[2])
                {
                    case "HP": case "HEALTH": case "LIFE":
                    case "MP": case "MANA": case "MAGIC":
                    case "AP": case "STAMINA": case "ENERGY":
                    case "GOLD": case "MONEY":
                        if (!Number(w[3], out action.Add, out action.Amount)) return null;
                        action.Kind = w[2] == "GOLD" || w[2] == "MONEY" ? Kind.Gold : Kind.Vital;
                        action.Item = w[2] == "HEALTH" || w[2] == "LIFE" ? "HP" : w[2] == "MANA" || w[2] == "MAGIC" ? "MP" : w[2] == "STAMINA" || w[2] == "ENERGY" ? "AP" : w[2];
                        break;
                    case "STATUS":
                        action.Kind = Kind.Status;
                        switch (w[3])
                        {
                            case "EXHAUSTED": case "TIRED": action.Status = HeroStatus.Exhausted; break;
                            case "ANGRY": case "ENRAGED": action.Status = HeroStatus.Angry; break;
                            case "POISONED": case "POISON": action.Status = HeroStatus.Poisoned; break;
                            case "FINE": action.Status = HeroStatus.Fine; break;
                        }

                        break;
                    case "AI":
                        if (w[3] == "LAY" || w[3] == "LAYDOWN" || w[3] == "SLEEP")
                        {
                            if (Sleep == null) return null;
                            action.Kind = Kind.Lay;
                        }
                        else if (w[3] == "REST" || w[3] == "SIT" || w[3] == "KNEEL") action.Kind = Kind.Kneel;
                        else return null;

                        break;
                    default:
                        return null;
                }

                actions.Add(action);
            }

            return actions;
        }

        /// <summary>isArithmetic: a leading + or - adds; a leading = is stripped and assigns; anything else assigns.</summary>
        static bool Number(string text, out bool add, out int amount)
        {
            add = text.StartsWith("+", StringComparison.Ordinal) || text.StartsWith("-", StringComparison.Ordinal);
            if (text.StartsWith("=", StringComparison.Ordinal)) text = text.Substring(1);
            return int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out amount);
        }
    }
}
