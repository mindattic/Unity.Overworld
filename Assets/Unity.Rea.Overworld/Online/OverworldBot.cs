using System.Threading.Tasks;
using MindAttic.Overworld.Core;
using Unity.Multiplayer; // IBotDriver, NetBot, [NetBotDriver]
using UnityEngine;
using UnityEngine.Scripting;

namespace MindAttic.Overworld.Online
{
    /// <summary>
    /// The game's bot (U.OVW-T46): launched with <c>-bot A</c> (NetBotTest, <c>mp-host game smoke</c>), the kit joins by
    /// code with no menus and hands the connected session here. It plays like a player does, with the same commands
    /// <c>HeroInput</c> makes, sent through its <see cref="OverworldPlayer"/>: it waits for its seat, holds the right arrow
    /// (role A) or the left (any other role) so the hero it leads walks, and passes once the authority has moved it.
    /// </summary>
    [NetBotDriver, Preserve]
    public sealed class OverworldBot : IBotDriver
    {
        /// <summary>World units the authority must move the bot's hero before it passes.</summary>
        public const float PassDistance = 0.5f;

        public static MoveInput InputFor(string role) => role == "A" ? MoveInput.Arrows(right: true) : MoveInput.Arrows(left: true);

        public async Task RunAsync(NetBot bot)
        {
            var me = await bot.LocalPlayer<OverworldPlayer>();
            // The server seats it and writes its hero in the same spawn, so the start arrives with the seat.
            await bot.Until(() => me.IsSeated && me.Leader >= 0, "the server to seat the bot");
            var start = me.Hero;
            var input = InputFor(bot.Role);

            while (Vector2.Distance(me.Hero, start) < PassDistance)
            {
                // The player id in a command is ignored: the server plays it as the bot's own seat.
                me.SendCommand(new SetMoveInput(me.PlayerId, input));
                await bot.Seconds(0.05f);
                if (bot.SecondsLeft <= 0f) break;
            }

            me.SendCommand(new SetMoveInput(me.PlayerId, default));
            if (Vector2.Distance(me.Hero, start) < PassDistance)
            {
                bot.Fail("the authority never moved seat " + me.Seat + "'s hero " + me.Leader + " from " + start + " (now " + me.Hero + ")");
                return;
            }

            bot.Pass("seat " + me.Seat + " walked hero " + me.Leader + " from " + start + " to " + me.Hero + " as " + me.DisplayName);
        }
    }
}
