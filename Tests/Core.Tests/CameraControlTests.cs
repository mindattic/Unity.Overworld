using System;
using System.Collections.Generic;
using System.IO;
using MindAttic.Overworld.Core;
using NUnit.Framework;
using StringAssert = NUnit.Framework.Legacy.StringAssert;

namespace MindAttic.Overworld.Tests
{
    /// <summary>
    /// U.OVW-US-A6: the world cursor and the camera (RECONSTRUCTION 3.2, 5.1, 5.3). The mouse moves the cursor 0.00172 per
    /// pixel; a virtual 640 x 480 mouse held 32 px in from the edges scrolls the camera 0.02 a tick (0.06 with Shift)
    /// while the leader is within +0.6 / -0.45 and +0.35 / -0.4, the camera held there; the cursor shows the arrow,
    /// the scroll arrows, the zoom or the eye; a held move button centres the camera on the leader, the cursor moving
    /// with it; the wheel and the middle-button drag zoom between -1 and -3.5 (free camera: unlimited); X / Z and the
    /// numpad move the camera.
    /// </summary>
    public class CameraControlTests
    {
        const float Eps = 1e-4f;

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

        static readonly ContentRegistry Registry = ContentRegistry.Load(new DirectoryContentSource(ContentDirectory()));

        Game game;
        Party party;
        CameraControl control;
        Viewpoint view;
        Hero chrono;
        List<IGameEvent> events;

        [SetUp]
        public void SetUp()
        {
            game = new Game();
            party = new Party(Registry.Heroes).Install(game);
            control = new CameraControl(Registry.View, party).Install(game);
            Assert.IsTrue(game.Submit(new JoinGame(PlayerId.One)).Accepted);
            view = control.For(PlayerId.One);
            chrono = party.Leader(PlayerId.One);
            events = new List<IGameEvent>();
            game.EventRaised += events.Add;
        }

        void Send(ViewInput input) => Assert.IsTrue(game.Submit(new SetViewInput(PlayerId.One, input)).Accepted);

        void Run(int ticks)
        {
            for (var i = 0; i < ticks; i++) game.Step();
        }

        /// <summary>Moves the virtual mouse to (x, y) on the 640 x 480 screen and runs a tick.</summary>
        void MouseTo(float x, float y, bool zoom = false, bool fast = false)
        {
            Send(new ViewInput(x - view.Cursor.MouseX, y - view.Cursor.MouseY, 0, zoom, fast));
            game.Step();
        }

        /// <summary>Holds the keys of <paramref name="input"/> with no mouse motion.</summary>
        void Hold(bool zoom = false, bool fast = false, bool left = false, bool right = false, bool up = false, bool down = false, bool iso = false, bool deiso = false) =>
            Send(new ViewInput(0, 0, 0, zoom, fast, left, right, up, down, iso, deiso));

        // ---- data ----------------------------------------------------------------------------------------------------

        [Test]
        public void TheCursorAndCameraNumbersAreTheOriginals()
        {
            var cursor = Registry.View.Cursor;
            Assert.AreEqual(0.05f, cursor.Size);
            Assert.AreEqual(new DataPoint(0.02f, -0.042f), cursor.DrawOffset);
            Assert.AreEqual(0.00172f, cursor.WorldPerPixel);
            Assert.AreEqual(640f, cursor.ScreenWidth);
            Assert.AreEqual(480f, cursor.ScreenHeight);
            Assert.AreEqual(32f, cursor.EdgeMargin);
            Assert.AreEqual(-0.531f, cursor.EdgeLeft);
            Assert.AreEqual(0.46f, cursor.EdgeRight);
            Assert.AreEqual(0.37f, cursor.EdgeUp);
            Assert.AreEqual(-0.348f, cursor.EdgeDown);
            Assert.AreEqual("gfx/cursor/arrow.PNG", cursor.File(CursorSprite.Arrow));
            Assert.AreEqual("gfx/cursor/scroll_left.PNG", cursor.File(CursorSprite.ScrollLeft));
            Assert.AreEqual("gfx/cursor/scroll_right.PNG", cursor.File(CursorSprite.ScrollRight));
            Assert.AreEqual("gfx/cursor/scroll_up.PNG", cursor.File(CursorSprite.ScrollUp));
            Assert.AreEqual("gfx/cursor/scroll_down.PNG", cursor.File(CursorSprite.ScrollDown));
            Assert.AreEqual("gfx/cursor/zoom.PNG", cursor.File(CursorSprite.Zoom));
            Assert.AreEqual("gfx/cursor/eye.PNG", cursor.File(CursorSprite.Eye));

            var c = Registry.View.Control;
            Assert.AreEqual(0.02f, c.Speed);
            Assert.AreEqual(3f, c.FastFactor);
            Assert.AreEqual(0.15f, c.WheelStep);
            Assert.AreEqual(0.002f, c.DragRate);
            Assert.AreEqual(5f, c.DragDeadZone);
            Assert.AreEqual(8f, c.CentreRate);
            Assert.AreEqual((-0.45f, 0.6f, -0.4f, 0.35f), (c.ScrollLeft, c.ScrollRight, c.ScrollDown, c.ScrollUp));
            Assert.AreEqual((-0.3f, 0.45f, -0.3f, 0.25f), (c.CarryLeft, c.CarryRight, c.CarryDown, c.CarryUp));
            Assert.AreEqual(new DataPoint(0.05f, 0.02f), c.Strafe);
            Assert.AreEqual(0.25f, c.Isometric);
        }

        [Test]
        public void ACentringRateThatOvershootsIsRejected()
        {
            var disk = new DirectoryContentSource(ContentDirectory());
            var original = disk.Read("view.json");
            var changed = original.Replace("\"centreRate\": 8", "\"centreRate\": 80");
            Assert.AreNotEqual(original, changed);
            var e = Assert.Throws<ContentException>(() => ContentRegistry.Load(new MemoryContentSource(disk).Set("view.json", changed)));
            StringAssert.Contains("view.json.cameraControl: 'centreRate' times 'speed' must be below 1", e.Message);
        }

        // ---- the cursor ------------------------------------------------------------------------------------------------

        [Test]
        public void TheCursorStartsAtTheOriginWithTheArrowAndTheMouseAtTheScreenCentre()
        {
            Assert.AreEqual(0f, view.Cursor.X);
            Assert.AreEqual(0f, view.Cursor.Y);
            Assert.AreEqual((320f, 240f), (view.Cursor.MouseX, view.Cursor.MouseY));
            Assert.AreEqual(CursorSprite.Arrow, view.Cursor.Sprite);
            Assert.AreEqual((0f, 0f, -1f), (view.Camera.X, view.Camera.Y, view.Camera.Z));
        }

        [Test]
        public void TheMouseMovesTheCursor0Point00172APixelWithScreenDownAsWorldDown()
        {
            Send(ViewInput.Motion(100, 50));
            game.Step();
            Assert.AreEqual(0.172f, view.Cursor.X, 1e-6f);
            Assert.AreEqual(-0.086f, view.Cursor.Y, 1e-6f);
            Assert.AreEqual((420f, 290f), (view.Cursor.MouseX, view.Cursor.MouseY));
        }

        [Test]
        public void MotionSentOverSeveralFramesAddsUpUntilTheTickUsesIt()
        {
            Send(ViewInput.Motion(10, 0));
            Send(ViewInput.Motion(15, 0));
            game.Step();
            Assert.AreEqual(25 * 0.00172f, view.Cursor.X, 1e-6f);
            game.Step();
            Assert.AreEqual(25 * 0.00172f, view.Cursor.X, 1e-6f, "the motion is used once");
        }

        [Test]
        public void TheVirtualMouseIsHeld32PixelsInFromEveryEdge()
        {
            MouseTo(-500, -500);
            Assert.AreEqual((32f, 32f), (view.Cursor.MouseX, view.Cursor.MouseY));
            MouseTo(5000, 5000);
            Assert.AreEqual((608f, 448f), (view.Cursor.MouseX, view.Cursor.MouseY));
        }

        // ---- edge scrolling --------------------------------------------------------------------------------------------

        [Test]
        public void TheRightEdgeScrolls0Point02ATickAndPutsTheCursorAtTheEdgeWithTheScrollArrow()
        {
            MouseTo(608, 240);
            Assert.AreEqual(0.02f, view.Camera.X, Eps);
            Assert.AreEqual(CursorSprite.ScrollRight, view.Cursor.Sprite);
            Assert.AreEqual(view.Camera.X + 0.46f, view.Cursor.X, Eps);
            Run(4);
            Assert.AreEqual(0.1f, view.Camera.X, Eps);
        }

        [Test]
        public void ShiftScrollsThreeTimesAsFast()
        {
            MouseTo(608, 240, fast: true);
            Run(4);
            Assert.AreEqual(0.3f, view.Camera.X, Eps);
        }

        [Test]
        public void EachEdgeScrollsAsFarAsTheOriginalsLimitsAroundTheLeaderAndShowsItsArrow()
        {
            MouseTo(608, 240);
            Run(100);
            Assert.AreEqual(chrono.X + 0.6f, view.Camera.X, Eps);

            MouseTo(32, 240);
            Assert.AreEqual(CursorSprite.ScrollLeft, view.Cursor.Sprite);
            Assert.AreEqual(view.Camera.X - 0.531f, view.Cursor.X, Eps);
            Run(100);
            Assert.AreEqual(chrono.X - 0.45f, view.Camera.X, Eps);

            MouseTo(320, 32);
            Assert.AreEqual(CursorSprite.ScrollUp, view.Cursor.Sprite);
            Assert.AreEqual(view.Camera.Y + 0.37f, view.Cursor.Y, Eps);
            Run(100);
            Assert.AreEqual(chrono.Y + 0.35f, view.Camera.Y, Eps);

            MouseTo(320, 448);
            Assert.AreEqual(CursorSprite.ScrollDown, view.Cursor.Sprite);
            Assert.AreEqual(view.Camera.Y - 0.348f, view.Cursor.Y, Eps);
            Run(100);
            Assert.AreEqual(chrono.Y - 0.4f, view.Camera.Y, Eps);

            MouseTo(320, 240);
            Assert.AreEqual(CursorSprite.Arrow, view.Cursor.Sprite);
        }

        [Test]
        public void AKneelingLeaderDoesNotScroll()
        {
            chrono.Kneeling = true;
            chrono.State = HeroState.Kneeling;
            MouseTo(608, 240);
            Run(10);
            Assert.AreEqual(0f, view.Camera.X, Eps);
            Assert.AreEqual(CursorSprite.ScrollRight, view.Cursor.Sprite, "the edge picture still shows");
        }

        [Test]
        public void TheFreeCameraScrollsBeyondTheHoldAroundTheLeader()
        {
            Assert.IsTrue(game.Submit(new ToggleFreeCamera(PlayerId.One)).Accepted);
            Assert.IsTrue(view.FreeCamera);
            Assert.IsInstanceOf<FreeCameraChanged>(events[events.Count - 1]);
            MouseTo(608, 240);
            Run(59);
            Assert.AreEqual(1.2f, view.Camera.X, Eps);

            Assert.IsTrue(game.Submit(new ToggleFreeCamera(PlayerId.One)).Accepted);
            game.Step();
            Assert.AreEqual(chrono.X + 0.6f, view.Camera.X, Eps, "back in the hold once the free camera is off");
        }

        // ---- zoom ------------------------------------------------------------------------------------------------------

        [Test]
        public void TheWheelZooms0Point15AStepBetweenMinus1AndMinus3Point5()
        {
            Send(new ViewInput(0, 0, -1, false, false));
            game.Step();
            Assert.AreEqual(-1.15f, view.Camera.Z, Eps);
            Send(new ViewInput(0, 0, 1, false, false));
            Send(new ViewInput(0, 0, 1, false, false));
            game.Step();
            Assert.AreEqual(-1f, view.Camera.Z, Eps, "zooming in stops at -1");

            for (var i = 0; i < 30; i++)
            {
                Send(new ViewInput(0, 0, -1, false, false));
                game.Step();
            }

            Assert.AreEqual(-3.5f, view.Camera.Z, Eps, "zooming out stops at -3.5");
        }

        [Test]
        public void TheFreeCameraZoomsPastTheLimits()
        {
            game.Submit(new ToggleFreeCamera(PlayerId.One));
            for (var i = 0; i < 30; i++)
            {
                Send(new ViewInput(0, 0, -1, false, false));
                game.Step();
            }

            Assert.AreEqual(-1f - 30 * 0.15f, view.Camera.Z, Eps);
        }

        [Test]
        public void TheMiddleButtonDragsTheZoomByHowFarTheMouseIsFromTheCentreLine()
        {
            Hold(zoom: true);
            game.Step();
            Assert.AreEqual(-1f, view.Camera.Z, Eps, "no change at the centre");
            Assert.AreEqual(CursorSprite.Zoom, view.Cursor.Sprite);
            Assert.IsTrue(view.Zooming);

            MouseTo(320, 340, zoom: true);
            Assert.AreEqual(-1f + 0.002f * (240 - 340), view.Camera.Z, Eps, "below the centre zooms out");
            Assert.AreEqual(CursorSprite.Zoom, view.Cursor.Sprite);
            game.Step();
            Assert.AreEqual(-1.4f, view.Camera.Z, Eps);

            MouseTo(320, 243, zoom: true);
            var z = view.Camera.Z;
            game.Step();
            Assert.AreEqual(z, view.Camera.Z, Eps, "within 5 pixels of the centre nothing changes");

            MouseTo(320, 140, zoom: true);
            Assert.AreEqual(z + 0.2f, view.Camera.Z, Eps, "above the centre zooms in");

            MouseTo(320, 240);
            Assert.IsFalse(view.Zooming);
            Assert.AreEqual(CursorSprite.Arrow, view.Cursor.Sprite);
        }

        [Test]
        public void ZoomingStopsEdgeScrollingAndKeepsTheZoomPicture()
        {
            MouseTo(608, 240, zoom: true);
            Run(5);
            Assert.AreEqual(0f, view.Camera.X, Eps);
            Assert.AreEqual(CursorSprite.Zoom, view.Cursor.Sprite);
        }

        [Test]
        public void TheMiddleButtonDoesNotZoomInBattleMode()
        {
            chrono.Mode = HeroMode.Battle;
            MouseTo(320, 400, zoom: true);
            Assert.AreEqual(-1f, view.Camera.Z, Eps);
            Assert.IsFalse(view.Zooming);
        }

        // ---- examine ---------------------------------------------------------------------------------------------------

        [Test]
        public void ExamineModeShowsTheEyeExceptAtAnEdge()
        {
            Assert.IsTrue(game.Submit(new ToggleExamine(PlayerId.One)).Accepted);
            Assert.IsTrue(view.Examine);
            Assert.IsInstanceOf<ExamineChanged>(events[events.Count - 1]);
            game.Step();
            Assert.AreEqual(CursorSprite.Eye, view.Cursor.Sprite);
            Assert.AreEqual("gfx/cursor/eye.PNG", view.Cursor.File);

            MouseTo(608, 240);
            Assert.AreEqual(CursorSprite.ScrollRight, view.Cursor.Sprite);

            game.Submit(new ToggleExamine(PlayerId.One));
            MouseTo(320, 240);
            Assert.AreEqual(CursorSprite.Arrow, view.Cursor.Sprite);
        }

        // ---- centring and carry ----------------------------------------------------------------------------------------

        [Test]
        public void HoldingTheMoveButtonCentresTheCameraOnTheLeaderAndTheCursorMovesWithIt()
        {
            MouseTo(608, 240);
            Run(100);
            MouseTo(320, 240);
            Assert.AreEqual(0.6f, view.Camera.X, Eps);

            // The move button with the cursor on the leader's own aim point: the leader stays, the camera centres.
            var t = Registry.Heroes.Locomotion;
            var size = Registry.Heroes.HeroSize;
            var aimX = chrono.X + size * t.HeroPoint.X - t.CursorOffset.X;
            var aimY = chrono.Y + size * t.HeroPoint.Y - t.CursorOffset.Y;
            Assert.IsTrue(game.Submit(new SetMoveInput(PlayerId.One, MoveInput.Toward(aimX, aimY))).Accepted);
            var cameraBefore = view.Camera.X;
            var cursorBefore = view.Cursor.X;
            game.Step();
            Assert.IsTrue(view.Centring);
            var target = chrono.X + size / 2f;
            Assert.AreEqual(cameraBefore + 0.02f * 8 * (target - cameraBefore), view.Camera.X, Eps);
            Assert.AreEqual(view.Camera.X - cameraBefore, view.Cursor.X - cursorBefore, Eps, "the cursor moves with the camera");

            Assert.IsTrue(game.Submit(new SetMoveInput(PlayerId.One, default)).Accepted);
            Run(200);
            Assert.IsFalse(view.Centring, "centring ends near the leader");
            Assert.AreEqual(target, view.Camera.X, 0.05f + 0.02f);
            Assert.AreEqual(chrono.Y - size / 2f + 0.03f, view.Camera.Y, 0.05f);
        }

        [Test]
        public void WhileCentringTheEdgesDoNotScroll()
        {
            Assert.IsTrue(game.Submit(new SetMoveInput(PlayerId.One, MoveInput.Toward(chrono.X, chrono.Y))).Accepted);
            MouseTo(608, 240);
            Assert.IsTrue(view.Centring);
            var x = view.Camera.X;
            Run(3);
            Assert.Less(Math.Abs(view.Camera.X - x), 0.02f * 3, "only the centring moved it");
        }

        [Test]
        public void TheLeaderWalkingAwayDragsTheCameraWithIt()
        {
            MouseTo(608, 240);
            Run(100);
            MouseTo(320, 240);
            Assert.IsTrue(game.Submit(new SetMoveInput(PlayerId.One, MoveInput.Arrows(left: true))).Accepted);
            Run(200);
            Assert.Less(chrono.X, -0.5f);
            NUnit.Framework.Assert.That(view.Camera.X - chrono.X, Is.InRange(0.45f, 0.6f + Eps), "the camera keeps within the hold, carried along");
        }

        // ---- debug moves -----------------------------------------------------------------------------------------------

        [Test]
        public void TheNumpadStrafesAndXAndZGiveTheIsometricOffset()
        {
            game.Submit(new ToggleFreeCamera(PlayerId.One));
            Hold(right: true);
            game.Step();
            Assert.AreEqual(0.05f, view.Camera.X, Eps);
            Hold(up: true);
            game.Step();
            Assert.AreEqual(0.02f, view.Camera.Y, Eps);
            Hold(left: true, down: true);
            game.Step();
            Assert.AreEqual((0f, 0f), (Round(view.Camera.X), Round(view.Camera.Y)));
            Hold(iso: true);
            Run(2);
            Assert.AreEqual((0.5f, 0.5f), (Round(view.Camera.X), Round(view.Camera.Y)));
            Hold(deiso: true);
            game.Step();
            Assert.AreEqual((0.25f, 0.25f), (Round(view.Camera.X), Round(view.Camera.Y)));
        }

        [Test]
        public void WithoutTheFreeCameraTheDebugMovesStayInTheHold()
        {
            Hold(iso: true);
            Run(5);
            Assert.AreEqual(chrono.X + 0.6f, view.Camera.X, Eps);
            Assert.AreEqual(chrono.Y + 0.35f, view.Camera.Y, Eps);
        }

        // ---- players ---------------------------------------------------------------------------------------------------

        [Test]
        public void EachPlayerHasItsOwnViewAroundTheHeroItLeads()
        {
            var two = new PlayerId(2);
            Assert.IsTrue(game.Submit(new JoinGame(two)).Accepted);
            Assert.IsTrue(game.Submit(new SetViewInput(two, ViewInput.Motion(288, 0))).Accepted);
            game.Step();
            Assert.AreEqual(0f, view.Cursor.X, "player 1's cursor did not move");
            var second = control.For(two);
            Assert.AreEqual(CursorSprite.ScrollRight, second.Cursor.Sprite);
            Assert.AreEqual(0.02f, second.Camera.X, Eps);
            Assert.IsFalse(game.Submit(new SetViewInput(new PlayerId(3), ViewInput.Motion(1, 0))).Accepted, "a player not in the game is refused");
        }

        [Test]
        public void TheCameraAndCursorRememberWhereTheTickStarted()
        {
            MouseTo(608, 240);
            var before = view.Camera.X;
            game.Step();
            Assert.AreEqual(before, view.Camera.PreviousX, Eps);
            Assert.AreEqual(before + 0.02f, view.Camera.X, Eps);
        }

        static float Round(float v) => (float)Math.Round(v, 4);
    }
}
