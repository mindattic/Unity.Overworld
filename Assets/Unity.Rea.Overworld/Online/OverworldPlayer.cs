using System;
using MindAttic.Overworld.Core;
using Unity.Multiplayer;      // NetEntity, NetPlayer
using Unity.Multiplayer.Core; // [ToServer], [Synced]
using UnityEngine;

namespace MindAttic.Overworld.Online
{
    /// <summary>
    /// One online player (U.OVW-T46): the NetworkManager's Player Prefab, so every connection gets one, carrying its
    /// seat. The server gives it the connection's seat from the session's roster (<see cref="Seat"/>, stable across a
    /// reconnect), and the seat is the <see cref="Core.PlayerId"/> its commands play as in the one authority's
    /// <see cref="Game"/> (<see cref="OverworldPeer.PlayerOf"/>): seat 0 is the scene's own player; any other seat joins
    /// the authority's game (<see cref="JoinGame"/>) for as long as the player is here and leads its own hero, as the
    /// party's per-player leader always allowed (player 2 leads Belmont, and so on).
    /// <para>On the owning client its screen's game forwards every command its <c>HeroInput</c> makes
    /// (<see cref="Game.Forward"/>) to <see cref="Send"/>, instead of changing its own game; the server decodes each one as
    /// this seat's (<see cref="CommandCodec"/>), so a client only ever acts as itself, and submits it. The server writes
    /// the seat's leader back (<see cref="Leader"/>, <see cref="Hero"/>), and the whole game travels in
    /// <see cref="OverworldWorldSync"/>'s snapshots.</para>
    /// </summary>
    public partial class OverworldPlayer : NetEntity
    {
        /// <summary>The seat before the server has given one.</summary>
        public const int NoSeat = -1;

        /// <summary>
        /// Most commands a second the server takes from one player: the move and view inputs go once a frame each, so this
        /// is a fast display's frame rate twice over with room for the key presses beside them.
        /// </summary>
        public const int CommandRate = 400;

        // client -> server: one encoded ICommand (CommandCodec), in the order the client made them.
        [ToServer(RateLimit = CommandRate, Burst = CommandRate * 2)]
        public partial void Send(byte[] command);

        // Written only by the server; every peer, late joiners included, reads them through Seat, Leader and Hero.
        [Synced] private int _seat = NoSeat;
        [Synced] private int _leader = -1;
        [Synced] private Vector2 _hero;

        private bool _joined;
        private Func<ICommand, bool> _forward;

        /// <summary>Server: the authority this player's commands go to. Null on a client.</summary>
        public OverworldPeer Authority { get; private set; }

        /// <summary>Owning client: the screen whose commands this player carries to the server. Null elsewhere.</summary>
        public OverworldPeer Screen { get; private set; }

        /// <summary>The player this seat plays as in the authority's game (player 0, none, until seated).</summary>
        public PlayerId PlayerId => IsSeated ? OverworldPeer.PlayerOf(Seat) : PlayerId.None;

        /// <summary>Whether the server has seated this player.</summary>
        public bool IsSeated => Seat != NoSeat;

        /// <summary>Server: commands received from this player and applied by the authority.</summary>
        public int CommandsApplied { get; private set; }

        /// <summary>Server: received bytes that did not decode to a command, and were ignored.</summary>
        public int CommandsRefused { get; private set; }

        /// <summary>The owner's name, from the roster ("Player" until the roster has it).</summary>
        public string DisplayName => Roster(out NetPlayer player) ? player.DisplayName : "Player";

        /// <summary>
        /// Sends <paramref name="command"/> to the authority as this seat's (the owning client's own call; what its
        /// screen's <see cref="Game.Forward"/> does for every player command). False for one that is never sent
        /// (<see cref="CommandCodec.IsSent"/>) or before the player is spawned.
        /// </summary>
        public bool SendCommand(ICommand command)
        {
            if (!IsSpawned || !CommandCodec.IsSent(command)) return false;
            Send(CommandCodec.Encode(command));
            return true;
        }

        protected override void OnSpawned()
        {
            if (IsServer)
            {
                Seat = Owner.HasSeat ? Owner.SeatIndex : 0;
                Authority = OverworldAuthority.For(Session);
                if (Authority != null)
                {
                    var id = OverworldPeer.PlayerOf(Seat);
                    if (!Authority.Game.HasPlayer(id))
                        _joined = Authority.Game.Submit(new JoinGame(id)).Accepted;
                    WriteBack();
                }
            }

            if (IsOwner && !IsServer)
            {
                Screen = OverworldAuthority.For(Session);
                if (Screen != null)
                {
                    _forward = SendCommand;
                    Screen.Game.Forward = _forward;
                }
            }
        }

        protected override void OnDespawned()
        {
            if (Authority != null && _joined)
            {
                // What they held is let go before they leave, so their hero stops where it is.
                var id = OverworldPeer.PlayerOf(Seat);
                Authority.Game.Submit(new SetMoveInput(id, default));
                Authority.Game.Submit(new LeaveGame(id));
            }

            if (Screen != null && Screen.Game.Forward == _forward) Screen.Game.Forward = null;

            Authority = null;
            Screen = null;
            _forward = null;
            _joined = false;
        }

        // Runs on the server for every Send: the bytes are this seat's, whatever player id the client put in them.
        private void OnSend(byte[] command)
        {
            if (Authority == null || !IsSeated) return;
            var decoded = CommandCodec.Decode(command, PlayerId);
            if (decoded == null)
            {
                CommandsRefused++;
                return;
            }

            Authority.Game.Submit(decoded);
            CommandsApplied++;
        }

        private void LateUpdate()
        {
            if (!IsSpawned) return;
            if (IsServer) WriteBack();
        }

        /// <summary>Server: the hero the seat leads and where it stands, from the authority's party.</summary>
        private void WriteBack()
        {
            var party = Authority != null && IsSeated ? Authority.Party : null;
            if (party == null || party.Heroes.Count == 0) return;
            var index = party.LeaderOf(PlayerId);
            var hero = party.Heroes[index];
            Leader = index;
            Hero = new Vector2(hero.X, hero.Y);
        }

        private bool Roster(out NetPlayer player)
        {
            player = null;
            return Session != null && Session.Roster != null && Session.Roster.TryGet(OwnerClientId, out player);
        }
    }
}
