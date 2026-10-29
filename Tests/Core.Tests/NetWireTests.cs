using System;
using System.Collections.Generic;
using System.IO;
using MindAttic.Overworld.Core;
using NUnit.Framework;
using CollectionAssert = NUnit.Framework.Legacy.CollectionAssert;

namespace MindAttic.Overworld.Tests
{
    /// <summary>
    /// U.OVW-T46: the online wire formats (CommandCodec, WorldSnapshot), the game's forwarding to a remote authority and
    /// a player leaving it, without Unity. The Unity side (the kit's sessions, seats and bots) is tested by the PlayMode
    /// assembly Unity.Rea.Overworld.Online.Tests.
    /// </summary>
    public class NetWireTests
    {
        static readonly PlayerId Two = new PlayerId(2);

        static IEnumerable<ICommand> SentCommands()
        {
            var p = PlayerId.One;
            yield return new SetMoveInput(p, new MoveInput(true, false, true, false, true, true, 12.5f, -3.25f, true));
            yield return new SetViewInput(p, new ViewInput(1.5f, -2f, -1, true, false, true, false, true, false, true, false));
            yield return new ToggleExamine(p);
            yield return new ToggleFreeCamera(p);
            yield return new CycleLeader(p, -1);
            yield return new SelectHero(p, 2);
            yield return new ToggleTeam(p);
            yield return new Rest(p);
            yield return new Sleep(p);
            yield return new Sleep(p, 8);
            yield return new CyclePickup(p, 1);
            yield return new PickUp(p);
            yield return new ToggleInventory(p);
            yield return new ToggleQuickBar(p);
            yield return new MoveInventoryCursor(p, 7);
            yield return new SwapItem(p);
            yield return new DropItem(p);
            yield return new UseItem(p);
            yield return new QuickUse(p, 9);
            yield return new ChooseTarget(p, -1);
            yield return new ToggleQuestScreen(p);
            yield return new PressQuestKey(p, (QuestKey)1);
            yield return new ToggleBook(p);
            yield return new NextBook(p);
            yield return new TurnPage(p, 1);
            yield return new ToggleBattle(p);
            yield return new TurnRing(p, -1);
            yield return new ChooseFromRing(p);
            yield return new CycleWeather(p);
            yield return new ToggleOptions(p);
            yield return new PressOptionsKey(p, (OptionsKey)1);
            yield return new ToggleHeroScreen(p);
            yield return new ToggleFps(p);
            yield return new TogglePause(p);
            yield return new PressConsoleKey(p, "OemPeriod", true);
            yield return new RunScript(p, "MOVE 1 2", 1);
            yield return new RunScript(p, "SAY hi", null);
            yield return new RecallHero(p, 1);
        }

        [Test]
        public void EveryPlayerCommand_RoundTrips_AsTheSendersSeat()
        {
            foreach (var command in SentCommands())
            {
                Assert.IsTrue(CommandCodec.IsSent(command), command.GetType().Name + " is sent");
                var bytes = CommandCodec.Encode(command);
                var decoded = CommandCodec.Decode(bytes, Two);
                Assert.IsNotNull(decoded, command.GetType().Name + " decodes");
                Assert.AreEqual(command.GetType(), decoded.GetType());
                Assert.AreEqual(Two, decoded.Player, "the sender's seat, never the id the client named");
                CollectionAssert.AreEqual(bytes, CommandCodec.Encode(decoded), command.GetType().Name + " keeps every field");
            }
        }

        [Test]
        public void TheAuthoritysAndTheScriptedRunsCommands_AreNeverSent()
        {
            Assert.IsFalse(CommandCodec.IsSent(new JoinGame(PlayerId.One)));
            Assert.IsFalse(CommandCodec.IsSent(new LeaveGame(PlayerId.One)));
            Assert.IsFalse(CommandCodec.IsSent(new LoadWorld(PlayerId.One, null)));
            Assert.IsFalse(CommandCodec.IsSent(null));
            Assert.Throws<ArgumentException>(() => CommandCodec.Encode(new JoinGame(PlayerId.One)));
        }

        [Test]
        public void BytesThatAreNoCommand_DecodeToNothing()
        {
            Assert.IsNull(CommandCodec.Decode(null, Two));
            Assert.IsNull(CommandCodec.Decode(new byte[0], Two));
            Assert.IsNull(CommandCodec.Decode(new byte[] { 99, 1 }, Two), "another version");
            Assert.IsNull(CommandCodec.Decode(new byte[] { CommandCodec.Version, 250 }, Two), "no such command");
            Assert.IsNull(CommandCodec.Decode(new byte[] { CommandCodec.Version, 5 }, Two), "cut short");
            var rest = CommandCodec.Encode(new Rest(PlayerId.One));
            var longer = new byte[rest.Length + 1];
            Array.Copy(rest, longer, rest.Length);
            Assert.IsNull(CommandCodec.Decode(longer, Two), "trailing bytes");
        }

        [Test]
        public void AForwardingGame_SendsItsPlayerCommands_AndAppliesNothingElseOfThem()
        {
            var game = new Game();
            Assert.IsTrue(game.Submit(new JoinGame(PlayerId.One)).Accepted);
            var sent = new List<ICommand>();
            game.Forward = c => { sent.Add(c); return true; };
            Assert.IsTrue(game.IsRemote);

            var result = game.Submit(new Rest(PlayerId.One));
            Assert.IsTrue(result.Accepted, "sent");
            Assert.AreEqual(1, sent.Count);
            Assert.IsInstanceOf<Rest>(sent[0]);

            // The authority's own commands still apply here: joining is never forwarded.
            Assert.IsTrue(game.Submit(new JoinGame(Two)).Accepted);
            Assert.AreEqual(1, sent.Count);
            Assert.IsTrue(game.HasPlayer(Two));

            game.Forward = c => false;
            Assert.IsFalse(game.Submit(new Rest(PlayerId.One)).Accepted, "not sent");

            game.Forward = null;
            Assert.IsFalse(game.IsRemote);
        }

        [Test]
        public void APlayerWhoLeaves_IsOutOfTheGame_AndTheirCommandsAreRefused()
        {
            var game = new Game();
            var raised = new List<IGameEvent>();
            game.EventRaised += raised.Add;
            game.Submit(new JoinGame(PlayerId.One));
            game.Submit(new JoinGame(Two));

            Assert.IsTrue(game.Submit(new LeaveGame(Two)).Accepted);
            Assert.IsFalse(game.HasPlayer(Two));
            Assert.IsTrue(game.HasPlayer(PlayerId.One));
            Assert.IsInstanceOf<PlayerLeft>(raised[raised.Count - 1]);
            Assert.IsFalse(game.Submit(new LeaveGame(Two)).Accepted, "gone already");
            Assert.IsTrue(game.Submit(new JoinGame(Two)).Accepted, "and may come back");
        }

        static string ContentDirectory()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "Assets", "StreamingAssets", "Content");
                if (Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException("Assets/StreamingAssets/Content");
        }

        [Test]
        public void ASnapshot_CarriesEveryHeroAndPlayer_AndRoundTrips()
        {
            var content = ContentRegistry.Load(new DirectoryContentSource(ContentDirectory()));
            var game = new Game();
            var party = new Party(content.Heroes, content.Walls).Install(game);
            game.Submit(new JoinGame(PlayerId.One));
            game.Submit(new JoinGame(Two));
            game.Submit(new SetMoveInput(Two, MoveInput.Arrows(right: true)));
            for (var i = 0; i < 30; i++) game.Step();

            var snapshot = WorldSnapshot.Capture(game, party, null, null, 7);
            Assert.AreEqual(7, snapshot.Sequence);
            Assert.AreEqual(game.Tick, snapshot.Tick);
            Assert.AreEqual(party.Heroes.Count, snapshot.Heroes.Count);
            Assert.AreEqual(2, snapshot.Players.Count);
            Assert.IsTrue(snapshot.TryGetPlayer(Two, out var two));
            Assert.AreEqual(party.LeaderOf(Two), two.Leader);

            var bytes = snapshot.Encode();
            var decoded = WorldSnapshot.Decode(bytes);
            Assert.IsNotNull(decoded);
            CollectionAssert.AreEqual(bytes, decoded.Encode());
            var hero = party.Heroes[two.Leader];
            Assert.AreEqual(hero.X, decoded.Heroes[two.Leader].X);
            Assert.AreEqual(hero.Y, decoded.Heroes[two.Leader].Y);

            Assert.IsNull(WorldSnapshot.Decode(new byte[] { 99 }));
            Assert.IsNull(WorldSnapshot.Decode(new byte[] { WorldSnapshot.Version, 1, 2 }));
            Assert.AreEqual(0, WorldSnapshot.Capture(game, null, null, null, 1).Heroes.Count, "no party, no heroes");
        }
    }
}
