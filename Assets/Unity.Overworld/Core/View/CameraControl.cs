using System;
using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>
    /// What a player does to the view in one frame (RECONSTRUCTION 5.1 and 5.3): the mouse's relative motion in the
    /// virtual 640 x 480 screen's pixels (y down, as DirectInput gave it), the wheel's direction, and the held view keys.
    /// </summary>
    public readonly struct ViewInput : IEquatable<ViewInput>
    {
        public ViewInput(float mouseX, float mouseY, int wheel, bool zoom, bool fast,
            bool strafeLeft = false, bool strafeRight = false, bool strafeUp = false, bool strafeDown = false,
            bool isometric = false, bool deisometric = false)
        {
            MouseX = mouseX;
            MouseY = mouseY;
            Wheel = wheel;
            Zoom = zoom;
            Fast = fast;
            StrafeLeft = strafeLeft;
            StrafeRight = strafeRight;
            StrafeUp = strafeUp;
            StrafeDown = strafeDown;
            Isometric = isometric;
            Deisometric = deisometric;
        }

        /// <summary>The mouse's motion this frame, in virtual pixels (right and down are positive).</summary>
        public float MouseX { get; }

        public float MouseY { get; }

        /// <summary>The wheel this frame: positive toward the screen (zoom in), negative away (zoom out), 0 still.</summary>
        public int Wheel { get; }

        /// <summary>The zoom button (the middle mouse button) is held.</summary>
        public bool Zoom { get; }

        /// <summary>Shift is held: edge scrolling is faster.</summary>
        public bool Fast { get; }

        /// <summary>Numpad 4, 6, 8 and 2.</summary>
        public bool StrafeLeft { get; }

        public bool StrafeRight { get; }
        public bool StrafeUp { get; }
        public bool StrafeDown { get; }

        /// <summary>X and Z: the isometric offset, +/- on both axes.</summary>
        public bool Isometric { get; }

        public bool Deisometric { get; }

        /// <summary>Only the mouse moved by (x, y) virtual pixels.</summary>
        public static ViewInput Motion(float x, float y) => new ViewInput(x, y, 0, false, false);

        public bool Equals(ViewInput o) =>
            MouseX.Equals(o.MouseX) && MouseY.Equals(o.MouseY) && Wheel == o.Wheel && Zoom == o.Zoom && Fast == o.Fast &&
            StrafeLeft == o.StrafeLeft && StrafeRight == o.StrafeRight && StrafeUp == o.StrafeUp && StrafeDown == o.StrafeDown &&
            Isometric == o.Isometric && Deisometric == o.Deisometric;

        public override bool Equals(object obj) => obj is ViewInput o && Equals(o);

        public override int GetHashCode() => MouseX.GetHashCode() ^ (MouseY.GetHashCode() * 31) ^ (Wheel * 7919) ^ (Zoom ? 1 : 0) ^ (Fast ? 2 : 0);
    }

    /// <summary>Sends what <see cref="Player"/> does to the view this frame. Motion and wheel steps add up until a tick uses them.</summary>
    public sealed class SetViewInput : ICommand
    {
        public SetViewInput(PlayerId player, ViewInput input) { Player = player; Input = input; }
        public PlayerId Player { get; }
        public ViewInput Input { get; }
    }

    /// <summary>The F key: the free camera on or off (the zoom limits and the hold around the leader lifted).</summary>
    public sealed class ToggleFreeCamera : ICommand
    {
        public ToggleFreeCamera(PlayerId player) { Player = player; }
        public PlayerId Player { get; }
    }

    /// <summary>The E key: examine mode on or off (the eye cursor).</summary>
    public sealed class ToggleExamine : ICommand
    {
        public ToggleExamine(PlayerId player) { Player = player; }
        public PlayerId Player { get; }
    }

    /// <summary>
    /// Puts the player's world cursor at (<see cref="X"/>, <see cref="Y"/>) on the ground, as CHANGE CURSOR did (and for
    /// scripted runs that point it at something); the virtual mouse is not moved.
    /// </summary>
    public sealed class PlaceCursor : ICommand
    {
        public PlaceCursor(PlayerId player, float x, float y) { Player = player; X = x; Y = y; }
        public PlayerId Player { get; }
        public float X { get; }
        public float Y { get; }
    }

    public sealed class FreeCameraChanged : IGameEvent
    {
        public FreeCameraChanged(long tick, PlayerId player, bool on) { Tick = tick; Player = player; On = on; }
        public long Tick { get; }
        public PlayerId Player { get; }
        public bool On { get; }
    }

    public sealed class ExamineChanged : IGameEvent
    {
        public ExamineChanged(long tick, PlayerId player, bool on) { Tick = tick; Player = player; On = on; }
        public long Tick { get; }
        public PlayerId Player { get; }
        public bool On { get; }
    }

    /// <summary>
    /// The world cursor (Cursor/Cursor.vb's Cursor object): a point on the ground the mouse moves, the virtual screen
    /// mouse behind it that finds the edges, and the picture it shows.
    /// </summary>
    public sealed class WorldCursor
    {
        public WorldCursor(CursorDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            MouseX = definition.ScreenWidth / 2f;
            MouseY = definition.ScreenHeight / 2f;
            Settle();
        }

        public CursorDefinition Definition { get; }

        /// <summary>The cursor's position on the ground (the quad is drawn from here plus the draw offset).</summary>
        public float X { get; internal set; }

        public float Y { get; internal set; }
        public float Z { get; internal set; }

        /// <summary>Where the cursor was at the start of the last tick.</summary>
        public float PreviousX { get; private set; }

        public float PreviousY { get; private set; }

        /// <summary>The virtual mouse on the 640 x 480 screen, y down, held within the edge margin.</summary>
        public float MouseX { get; internal set; }

        public float MouseY { get; internal set; }

        public CursorSprite Sprite { get; internal set; }

        /// <summary>The picture file it shows now.</summary>
        public string File => Definition.File(Sprite);

        public bool AtLeftEdge => MouseX <= Definition.EdgeMargin;
        public bool AtRightEdge => MouseX >= Definition.ScreenWidth - Definition.EdgeMargin;
        public bool AtTopEdge => MouseY <= Definition.EdgeMargin;
        public bool AtBottomEdge => MouseY >= Definition.ScreenHeight - Definition.EdgeMargin;

        internal void Settle()
        {
            PreviousX = X;
            PreviousY = Y;
        }

        public override string ToString() => Sprite + " cursor at (" + X + ", " + Y + "), mouse (" + MouseX + ", " + MouseY + ")";
    }

    /// <summary>One player's view: the camera, the world cursor, and the centring, zoom and examine state.</summary>
    public sealed class Viewpoint
    {
        internal ViewInput Held;
        internal float PendingX, PendingY;
        internal int PendingWheel;

        internal Viewpoint(PlayerId player, ViewData view)
        {
            Player = player;
            Camera = new WorldCamera(view.Camera);
            Cursor = new WorldCursor(view.Cursor);
        }

        public PlayerId Player { get; }
        public WorldCamera Camera { get; }
        public WorldCursor Cursor { get; }

        /// <summary>The camera is easing back to centre on the leader (the original's CenterOnHero).</summary>
        public bool Centring { get; internal set; }

        /// <summary>The zoom button is held (bMouseZoom): no edge scrolling or edge pictures.</summary>
        public bool Zooming { get; internal set; }

        /// <summary>Examine mode (bExamine): the eye cursor.</summary>
        public bool Examine { get; internal set; }

        public bool FreeCamera => Camera.FreeCamera;
    }

    /// <summary>
    /// The camera and the world cursor (U.OVW-US-A6; RECONSTRUCTION 3.2 and 5.3), per player. Each tick, after the
    /// party has moved (install it after the <see cref="Party"/>), it does what the original's frame did around the hero:
    /// getKeyboard's isometric and numpad moves, getMouse's virtual mouse, wheel and drag zoom (and the centring a held
    /// move button asks for), KeysUp, renderCursor's cursor motion, edge scroll, hold around the leader, edge pictures and
    /// carry, then initCamera's zoom limits and centring. Accepts <see cref="SetViewInput"/>, <see cref="ToggleFreeCamera"/>
    /// and <see cref="ToggleExamine"/>; presentation reads <see cref="For"/>.
    /// </summary>
    public sealed class CameraControl : ITickSystem
    {
        readonly Dictionary<PlayerId, Viewpoint> viewpoints = new Dictionary<PlayerId, Viewpoint>();

        public CameraControl(ViewData view, Party party)
        {
            View = view ?? throw new ArgumentNullException(nameof(view));
            Party = party ?? throw new ArgumentNullException(nameof(party));
            if (view.Cursor == null || view.Control == null) throw new ArgumentException("The view has no cursor or camera control.", nameof(view));
        }

        public ViewData View { get; }
        public Party Party { get; }
        CameraControlDefinition Control => View.Control;

        /// <summary>
        /// A use of the wheel that comes before the zoom (getMouse: with items in reach the wheel cycles them,
        /// <see cref="Pickups.UseWheel"/>): given the player and the wheel's steps, true when it took them. Null: the wheel zooms.
        /// </summary>
        public Func<PlayerId, int, bool> WheelFirst { get; set; }

        /// <summary>Registers the view commands and the per-tick rules on <paramref name="game"/>, after the party's.</summary>
        public CameraControl Install(Game game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            game.Register<SetViewInput>(HandleInput);
            game.Register<ToggleFreeCamera>(HandleFreeCamera);
            game.Register<ToggleExamine>(HandleExamine);
            game.Register<PlaceCursor>(HandlePlaceCursor);
            game.AddSystem(this);
            game.EventRaised += OnEvent;
            return this;
        }

        /// <summary>
        /// A new leader (changeHero, actionHeroSelect): the player's camera centres on it (CenterOnHero), and when it was
        /// chosen with [ or ] the cursor snaps onto it (half a hero across and up from its position).
        /// </summary>
        void OnEvent(IGameEvent e)
        {
            if (!(e is LeaderChanged changed)) return;
            var v = For(changed.Player);
            v.Centring = true;
            if (!changed.Cycled) return;
            var hero = Party.Heroes[changed.Hero];
            var half = Party.Data.HeroSize / 2f;
            v.Cursor.X = hero.X + half;
            v.Cursor.Y = hero.Y + half;
            v.Cursor.Settle();
        }

        /// <summary>The view of <paramref name="player"/> (made at the start position the first time it is asked for).</summary>
        public Viewpoint For(PlayerId player)
        {
            if (!viewpoints.TryGetValue(player, out var v)) viewpoints[player] = v = new Viewpoint(player, View);
            return v;
        }

        CommandResult HandleInput(Game game, SetViewInput c)
        {
            var v = For(c.Player);
            v.Held = c.Input;
            v.PendingX += c.Input.MouseX;
            v.PendingY += c.Input.MouseY;
            v.PendingWheel += Math.Sign(c.Input.Wheel);
            return CommandResult.Accept();
        }

        CommandResult HandleFreeCamera(Game game, ToggleFreeCamera c)
        {
            var v = For(c.Player);
            v.Camera.FreeCamera = !v.Camera.FreeCamera;
            return CommandResult.Accept(new FreeCameraChanged(game.Tick, c.Player, v.Camera.FreeCamera));
        }

        CommandResult HandleExamine(Game game, ToggleExamine c)
        {
            var v = For(c.Player);
            v.Examine = !v.Examine;
            return CommandResult.Accept(new ExamineChanged(game.Tick, c.Player, v.Examine));
        }

        /// <summary>
        /// rendercursor2D's overTeam / overInventoryBox: the virtual mouse at (x, y) is on one of the HUD's buttons, where
        /// checkScroll did not scroll left. Null: never.
        /// </summary>
        public Func<float, float, bool> OverButton { get; set; }

        CommandResult HandlePlaceCursor(Game game, PlaceCursor c)
        {
            if (float.IsNaN(c.X) || float.IsNaN(c.Y) || float.IsInfinity(c.X) || float.IsInfinity(c.Y))
                return CommandResult.Reject("The cursor is placed at a finite point.");
            var cursor = For(c.Player).Cursor;
            cursor.X = c.X;
            cursor.Y = c.Y;
            cursor.Settle();
            return CommandResult.Accept();
        }

        public void Step(Game game, long tick, ICollection<IGameEvent> events)
        {
            foreach (var player in game.Players) Step(For(player), Party.Leader(player), Party.InputOf(player).Move);
        }

        void Step(Viewpoint v, Hero hero, bool moveHeld)
        {
            var c = Control;
            var cam = v.Camera;
            var cursor = v.Cursor;
            var def = cursor.Definition;
            var input = v.Held;
            var dx = v.PendingX;
            var dy = v.PendingY;
            var wheel = v.PendingWheel;
            v.PendingX = 0f;
            v.PendingY = 0f;
            v.PendingWheel = 0;
            cam.Settle();
            cursor.Settle();

            // getKeyboard: the isometric offset and the numpad strafes.
            if (input.Isometric) cam.Move(c.Isometric, c.Isometric);
            if (input.Deisometric) cam.Move(-c.Isometric, -c.Isometric);
            if (input.StrafeRight) cam.Move(c.Strafe.X, 0f);
            if (input.StrafeLeft) cam.Move(-c.Strafe.X, 0f);
            if (input.StrafeUp) cam.Move(0f, c.Strafe.Y);
            if (input.StrafeDown) cam.Move(0f, -c.Strafe.Y);

            // getMouse: the virtual mouse, held 32 px in from the edges; the wheel; the move button asks for centring;
            // the zoom button drags the zoom by how far the mouse is above or below the centre line.
            cursor.MouseX = Math.Min(Math.Max(cursor.MouseX + dx, def.EdgeMargin), def.ScreenWidth - def.EdgeMargin);
            cursor.MouseY = Math.Min(Math.Max(cursor.MouseY + dy, def.EdgeMargin), def.ScreenHeight - def.EdgeMargin);
            if (wheel != 0 && !(WheelFirst != null && WheelFirst(v.Player, wheel))) cam.Move(0f, 0f, wheel * c.WheelStep);
            if (moveHeld) v.Centring = true;
            if (input.Zoom && hero.Mode == HeroMode.Adventure)
            {
                v.Zooming = true;
                var centre = def.ScreenHeight / 2f;
                if (cursor.MouseY > centre + c.DragDeadZone || cursor.MouseY < centre - c.DragDeadZone)
                    cam.Move(0f, 0f, c.DragRate * (centre - cursor.MouseY));
                cursor.Sprite = CursorSprite.Zoom;
            }

            // KeysUp: the zoom button let go.
            if (!input.Zoom)
            {
                cursor.Sprite = CursorSprite.Arrow;
                v.Zooming = false;
            }

            // renderCursor: the cursor follows the mouse's motion (one pixel is 0.00172 whatever the zoom).
            cursor.X += dx * def.WorldPerPixel;
            cursor.Y -= dy * def.WorldPerPixel;

            if (!v.Centring && hero.State != HeroState.Kneeling) Scroll(v, hero, input.Fast);

            // The hold around the leader. The original applied it in free camera too, which undid the free camera's
            // scrolling; the free camera lifts it (BIBLE, fixed defects).
            if (!cam.FreeCamera)
            {
                var x = Math.Min(Math.Max(cam.X, hero.X + c.ScrollLeft), hero.X + c.ScrollRight);
                var y = Math.Min(Math.Max(cam.Y, hero.Y + c.ScrollDown), hero.Y + c.ScrollUp);
                cam.Place(x, y);
            }

            if (!v.Zooming) EdgeCursor(v);

            // checkHeroEdge: beyond the carry box the leader's velocity carries the camera and the cursor.
            if (cam.X > hero.X + c.CarryRight || cam.X < hero.X + c.CarryLeft)
            {
                cam.Move(hero.DX, 0f);
                cursor.X += hero.DX;
            }

            if (cam.Y > hero.Y + c.CarryUp || cam.Y < hero.Y + c.CarryDown)
            {
                cam.Move(0f, hero.DY);
                cursor.Y += hero.DY;
            }

            // initCamera: the zoom limits, then centring, the cursor moving with the camera.
            cam.Clamp();
            if (v.Centring) Centre(v, hero);
        }

        /// <summary>checkScroll: the mouse at an edge moves the camera that way while the leader is within the limits.</summary>
        void Scroll(Viewpoint v, Hero hero, bool fast)
        {
            if (v.Zooming) return;
            var c = Control;
            var cam = v.Camera;
            var cursor = v.Cursor;
            var free = cam.FreeCamera;
            var step = fast ? c.Speed * c.FastFactor : c.Speed;
            if (cursor.AtRightEdge && (hero.X + c.ScrollRight > cam.X || free)) cam.Move(step, 0f);
            // The left edge does not scroll while the mouse is on the team or inventory button (overTeam, overInventoryBox).
            if (cursor.AtLeftEdge && (hero.X + c.ScrollLeft < cam.X || free) && !(OverButton != null && OverButton(cursor.MouseX, cursor.MouseY))) cam.Move(-step, 0f);
            if (cursor.AtBottomEdge && (hero.Y + c.ScrollDown < cam.Y || free)) cam.Move(0f, -step);
            if (cursor.AtTopEdge && (hero.Y + c.ScrollUp > cam.Y || free)) cam.Move(0f, step);
        }

        /// <summary>checkCursorEdge: at an edge the cursor sits at the screen's edge and shows the scroll arrow.</summary>
        static void EdgeCursor(Viewpoint v)
        {
            var cursor = v.Cursor;
            var def = cursor.Definition;
            var cam = v.Camera;
            if (cursor.AtRightEdge)
            {
                cursor.Sprite = CursorSprite.ScrollRight;
                cursor.X = cam.X + def.EdgeRight;
            }
            else if (cursor.AtLeftEdge)
            {
                cursor.X = cam.X + def.EdgeLeft;
                cursor.Sprite = CursorSprite.ScrollLeft;
            }
            else
            {
                cursor.Sprite = v.Examine ? CursorSprite.Eye : CursorSprite.Arrow;
            }

            if (cursor.AtBottomEdge)
            {
                cursor.Sprite = CursorSprite.ScrollDown;
                cursor.Y = cam.Y + def.EdgeDown;
            }
            else if (cursor.AtTopEdge)
            {
                cursor.Sprite = CursorSprite.ScrollUp;
                cursor.Y = cam.Y + def.EdgeUp;
            }
        }

        /// <summary>CenterOnHero: the camera (and the cursor with it) eases toward the leader until it is close.</summary>
        void Centre(Viewpoint v, Hero hero)
        {
            var c = Control;
            var size = Party.Data.HeroSize;
            var cam = v.Camera;
            var targetX = hero.X + size / 2f + c.CentreOffset.X;
            var targetY = hero.Y - size / 2f + c.CentreOffset.Y;
            var runX = c.CentreRate * (targetX - cam.X);
            var riseY = c.CentreRate * (targetY - cam.Y);
            cam.Move(c.Speed * runX, c.Speed * riseY);
            v.Cursor.X += c.Speed * runX;
            v.Cursor.Y += c.Speed * riseY;

            if (WorldCamera.Near(cam.X, targetX + c.CentreArriveOffset.X, c.CentreArriveRange) &&
                WorldCamera.Near(cam.Y, targetY + c.CentreArriveOffset.Y, c.CentreArriveRange))
                v.Centring = false;
        }
    }
}
