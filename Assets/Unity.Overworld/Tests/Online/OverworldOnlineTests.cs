// U.OVW-T46: online play on the Unity.Multiplayer kit's test fixtures (Unity.Multiplayer.Testing). Every test gets a
// real host or dedicated server and real clients over Unity Transport on a free loopback port. Each peer has its own
// game (OverworldAuthority.Resolve), as each would in its own process: the server's is the one authority, a client's is
// its screen, whose player commands are forwarded instead of changing its own game. The wire formats themselves are
// tested without Unity: dotnet test Tests/Core.Tests (NetWireTests).
using System.Collections;
using MindAttic.Overworld.Core;
using MindAttic.Overworld.Runtime;
using NUnit.Framework;
using Unity.Multiplayer;
using Unity.Multiplayer.Core;
using Unity.Multiplayer.Testing;
using UnityEngine;
using UnityEngine.TestTools;

namespace MindAttic.Overworld.Online.Tests
{
    internal static class OnlinePrefabs
    {
        // The Resources paths OnlineBuilder writes the prefabs to (Editor/OnlineBuilder.cs).
        public static GameObject Player => Load("Online/OverworldPlayer");
        public static GameObject WorldSync => Load("Online/OverworldWorldSync");

        static GameObject Load(string path)
        {
            var prefab = Resources.Load<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, "Resources/" + path + ".prefab is missing: run Overworld > Build Main Scene");
            return prefab;
        }

        static ContentRegistry content;

        /// <summary>
        /// A peer's game: the party from the shipped content, stepped by its own GameHost, with the screen's own player
        /// (player 1) in it when <paramref name="withLocalPlayer"/> (a dedicated server's game starts empty).
        /// </summary>
        public static OverworldPeer NewGame(string name, bool withLocalPlayer = true)
        {
            content ??= ContentRegistry.Load(new DirectoryContentSource(OverworldBootstrap.ContentDirectory));
            var game = new Game();
            var party = new Party(content.Heroes, content.Walls).Install(game);
            if (withLocalPlayer) Assert.That(game.Submit(new JoinGame(PlayerId.One)).Accepted, Is.True);
            var host = GameHost.Create(game);
            host.gameObject.name = name;
            return new OverworldPeer(game, party, host: host);
        }

        public static void Destroy(OverworldPeer peer)
        {
            if (peer != null && peer.Host != null) Object.Destroy(peer.Host.gameObject);
        }

        public static Hero LeaderOf(OverworldPeer peer, PlayerId player) => peer.Party.Leader(player);
    }

    /// <summary>A host and one client: the client's commands reach the host's game as its own seat, and come back.</summary>
    public class OverworldPlayerTests : NetSessionTest
    {
        OverworldPeer hostGame;
        OverworldPeer clientGame;

        public OverworldPlayerTests() : base(Topology.Host, clients: 1) { }

        // No seat held for a reconnect, so a player who leaves is gone at once (the leave test).
        protected override NetConfig Config()
        {
            var config = base.Config();
            config.ReconnectGraceSeconds = 0;
            return config;
        }

        protected override void OnSessionsCreated()
        {
            SetPlayerPrefab(OnlinePrefabs.Player);
            RegisterPrefab(OnlinePrefabs.WorldSync);
            Client.Identity = new PlayerIdentity("ann-token", "Ann", new PlayerColor(230, 80, 60));
            hostGame = OnlinePrefabs.NewGame("Host game");
            clientGame = OnlinePrefabs.NewGame("Client game");
            OverworldAuthority.Resolve = session => session == Server ? hostGame : session == Client ? clientGame : null;
        }

        protected override IEnumerator OnSessionsTearDown()
        {
            OverworldAuthority.Reset();
            OnlinePrefabs.Destroy(hostGame);
            OnlinePrefabs.Destroy(clientGame);
            yield break;
        }

        IEnumerator Seated(System.Action<OverworldPlayer, OverworldPlayer> found)
        {
            yield return StartAndJoinAll();
            var annOnHerClient = WaitForSpawn<OverworldPlayer>(Client, owner: Client.LocalClientId);
            yield return annOnHerClient;
            var annOnHost = WaitForSpawn<OverworldPlayer>(Server, owner: Client.LocalClientId);
            yield return annOnHost;
            yield return WaitUntil(() => annOnHerClient.Result.IsSeated, "Ann's client never learnt her seat");
            found(annOnHerClient.Result, annOnHost.Result);
        }

        [UnityTest]
        public IEnumerator EachConnection_GetsAPlayerCarryingItsSeat_AndTheAuthorityJoinsIt()
        {
            OverworldPlayer ann = null, annOnHost = null;
            yield return Seated((c, h) => { ann = c; annOnHost = h; });

            var hostOwn = WaitForSpawn<OverworldPlayer>(Server, owner: Server.LocalClientId);
            yield return hostOwn;

            Assert.That(ann.Seat, Is.EqualTo(annOnHost.Seat), "every peer reads the server's seat");
            Assert.That(ann.Seat, Is.Not.EqualTo(hostOwn.Result.Seat), "two connections, two seats");
            Assert.That(hostOwn.Result.PlayerId, Is.EqualTo(PlayerId.One), "the host's own seat is the scene's player");
            Assert.That(hostGame.Game.HasPlayer(ann.PlayerId), Is.True, "the authority joined Ann to its game");
            Assert.That(hostGame.Game.Players.Count, Is.EqualTo(2));
            Assert.That(hostGame.Game.IsRemote, Is.False, "the host is the authority: its commands apply to its own game");
            Assert.That(clientGame.Game.IsRemote, Is.True, "Ann's screen forwards its commands");
            yield return WaitUntil(() => ann.Leader == hostGame.Party.LeaderOf(ann.PlayerId), "Ann never learnt which hero she leads");
            yield return WaitUntil(() => annOnHost.DisplayName == "Ann", () => "the host sees her as " + annOnHost.DisplayName);
        }

        [UnityTest]
        public IEnumerator TheClientsCommands_GoToTheAuthority_AsItsOwnSeat_AndTheHeroComesBackSynced()
        {
            OverworldPlayer ann = null, annOnHost = null;
            yield return Seated((c, h) => { ann = c; annOnHost = h; });

            var hostHero = OnlinePrefabs.LeaderOf(hostGame, PlayerId.One);
            var hostStart = new Vector2(hostHero.X, hostHero.Y);
            var annHero = OnlinePrefabs.LeaderOf(hostGame, ann.PlayerId);
            var start = new Vector2(annHero.X, annHero.Y);
            var clientTickBefore = clientGame.Game.Tick;

            // What Ann's arrow key does: her screen's HeroInput submits as player 1; it is forwarded, not applied.
            for (var i = 0; i < 30; i++)
            {
                Assert.That(clientGame.Game.Submit(new SetMoveInput(PlayerId.One, MoveInput.Arrows(right: true))).Accepted, Is.True, "sent");
                yield return null;
            }

            yield return WaitUntil(() => Vector2.Distance(new Vector2(annHero.X, annHero.Y), start) > 0.2f,
                () => "the authority never walked Ann's hero; it is at " + annHero.X + ", " + annHero.Y);
            Assert.That(annOnHost.CommandsApplied, Is.GreaterThanOrEqualTo(30));
            Assert.That(hostGame.Game.HasPlayer(ann.PlayerId), Is.True);
            if (!ReferenceEquals(hostHero, annHero))
                Assert.That(Vector2.Distance(new Vector2(hostHero.X, hostHero.Y), hostStart), Is.LessThan(0.2f),
                    "the host's hero, whose id Ann's commands named, did not walk");

            yield return WaitUntil(() => Vector2.Distance(ann.Hero, start) > 0.2f, () => "Ann's client never saw her hero move; it is at " + ann.Hero);
            Assert.That(clientGame.Game.Tick, Is.EqualTo(clientTickBefore), "her screen's own game is not stepped while it is online");

            clientGame.Game.Submit(new SetMoveInput(PlayerId.One, default));

            // The authority's own commands never leave a screen.
            Assert.That(CommandCodec.IsSent(new JoinGame(PlayerId.One)), Is.False);
        }

        [UnityTest]
        public IEnumerator BytesThatAreNoCommand_AreRefusedByTheAuthority()
        {
            OverworldPlayer ann = null, annOnHost = null;
            yield return Seated((c, h) => { ann = c; annOnHost = h; });

            ann.Send(new byte[] { 99, 1, 2, 3 });
            ann.Send(new byte[0]);
            yield return WaitUntil(() => annOnHost.CommandsRefused == 2, () => "refused " + annOnHost.CommandsRefused);
            Assert.That(annOnHost.CommandsApplied, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator TheAuthoritysGame_ReachesTheClient_AsSnapshots()
        {
            OverworldPlayer ann = null, annOnHost = null;
            yield return Seated((c, h) => { ann = c; annOnHost = h; });

            var onHost = OverworldWorldSync.Serve(Server, OnlinePrefabs.WorldSync);
            Assert.That(OverworldWorldSync.For(Server), Is.SameAs(onHost), "found by its key on the server");
            Assert.That(OverworldWorldSync.Serve(Server, OnlinePrefabs.WorldSync), Is.SameAs(onHost), "one per session");

            OverworldWorldSync onClient = null;
            yield return WaitUntil(() => (onClient = OverworldWorldSync.For(Client)) != null, "the client never got the world sync by its key");
            yield return WaitUntil(() => onClient.Latest != null && onClient.Latest.Players.Count == 2,
                () => "the client's snapshot has " + (onClient.Latest == null ? "nothing" : onClient.Latest.Players.Count + " players"));

            var snapshot = onClient.Latest;
            Assert.That(snapshot.Heroes.Count, Is.EqualTo(hostGame.Party.Heroes.Count), "every hero travels");
            Assert.That(snapshot.TryGetPlayer(ann.PlayerId, out var annInSnapshot), Is.True, "Ann's seat is in the snapshot");
            Assert.That(annInSnapshot.Leader, Is.EqualTo(hostGame.Party.LeaderOf(ann.PlayerId)));
            Assert.That(snapshot.Tick, Is.GreaterThan(0L), "the authority's game is stepped");
        }

        [UnityTest]
        public IEnumerator APlayerWhoLeaves_IsTakenOutOfTheAuthoritysGame_AndTheirScreenPlaysLocallyAgain()
        {
            OverworldPlayer ann = null, annOnHost = null;
            yield return Seated((c, h) => { ann = c; annOnHost = h; });
            var seat = ann.PlayerId;

            Client.Leave();
            yield return WaitUntil(() => !hostGame.Game.HasPlayer(seat), "Ann's seat stayed in the authority's game");
            yield return WaitUntil(() => !clientGame.Game.IsRemote, "Ann's screen still forwards its commands");
            Assert.That(hostGame.Game.HasPlayer(PlayerId.One), Is.True, "the host's own player stays");
        }
    }

    /// <summary>The nightly smoke without Docker: a dedicated server and two OverworldBots that join by code and walk.</summary>
    public class OverworldBotTests : NetBotTest
    {
        OverworldPeer serverGame;

        public OverworldBotTests() : base(bots: 2) { }

        protected override float BotTimeoutSeconds => 60f;

        protected override void OnSessionsCreated()
        {
            SetPlayerPrefab(OnlinePrefabs.Player);
            RegisterPrefab(OnlinePrefabs.WorldSync);
            serverGame = OnlinePrefabs.NewGame("Server game", withLocalPlayer: false);
            // Only the server has a game: a bot has no screen, and plays through its OverworldPlayer's SendCommand.
            OverworldAuthority.Resolve = session => session == Server ? serverGame : null;
        }

        protected override IEnumerator OnSessionsTearDown()
        {
            OverworldAuthority.Reset();
            OnlinePrefabs.Destroy(serverGame);
            yield return base.OnSessionsTearDown();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TwoBotsJoinByCode_AndTheAuthorityWalksEachOnesHero()
        {
            yield return RunBots(role => new OverworldBot(), "A", "B");

            Assert.That(serverGame.Game.Players.Count, Is.EqualTo(2), "a dedicated server seats only its two bots");
            foreach (var bot in Bots) Assert.That(bot.PassMessage, Does.Contain("walked"));
        }
    }
}
