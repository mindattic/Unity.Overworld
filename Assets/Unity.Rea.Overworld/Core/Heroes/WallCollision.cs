using System;

namespace MindAttic.Overworld.Core
{
    /// <summary>
    /// The original's wall collision (Heroes.checkWall, horizontalWall, angledWall, verticalWall; RECONSTRUCTION 3.5),
    /// exactly as it pushed a hero back: it never moves the hero, it only rewrites the velocity the hero is about to move
    /// by. Each tick, per hero, before the move, the test point is the hero's position plus heroSize x probe, raised by
    /// the probe lift; then for every wall:
    /// <list type="bullet">
    /// <item>when the point's x lies between the wall's x ends (inclusive): a flat wall within reach x |dY| of the point's y
    /// sets dY to +/-push (up when the point is on or above the wall, down when below); a sloped wall does the same
    /// against the line's y at that x, and also zeroes dX when the point is within the angled stop of the line;</item>
    /// <item>otherwise, when the wall is vertical (an infinite slope): when the point's y lies between its ends and its x
    /// is within reach x |dX| of the wall, dX is set to +/-push.</item>
    /// </list>
    /// Since the velocity is checked before it is used, a hero never crosses a wall: one more step would have brought it
    /// within reach. The arithmetic is single precision, as the original's was.
    /// </summary>
    public static class WallCollision
    {
        /// <summary>The point the walls test for <paramref name="hero"/> (theHeroX, theHeroY).</summary>
        public static (float X, float Y) TestPoint(Hero hero, float heroSize, WallCollisionTuning tuning)
        {
            if (hero == null) throw new ArgumentNullException(nameof(hero));
            if (tuning == null) throw new ArgumentNullException(nameof(tuning));
            var x = hero.X + heroSize * tuning.Probe.X;
            var y = hero.Y + heroSize * tuning.Probe.Y + tuning.ProbeLift;
            return (x, y);
        }

        /// <summary>checkWall for one hero: every wall, in order, may rewrite its velocity.</summary>
        public static void Apply(Hero hero, WallData walls, float heroSize)
        {
            if (walls == null) throw new ArgumentNullException(nameof(walls));
            var t = walls.Collision;
            var (x, y) = TestPoint(hero, heroSize, t);
            foreach (var wall in walls.Walls)
            {
                // y = m x + b (for the vertical wall this is not a number, and nothing reads it).
                var intersectY = wall.Slope * x + wall.YIntercept;
                if (Between(x, wall.X1, wall.X2))
                {
                    if (wall.Slope == 0f) Horizontal(hero, wall, y, t);
                    else Angled(hero, y, intersectY, t);
                }
                else if (float.IsInfinity(wall.Slope))
                {
                    Vertical(hero, wall, x, y, t);
                }
            }
        }

        static void Horizontal(Hero hero, WallDefinition wall, float y, WallCollisionTuning t)
        {
            if (Near(y, wall.Y1, Math.Abs(hero.DY * t.Reach)))
                hero.DY = y >= wall.Y1 ? t.Push : -t.Push;
        }

        static void Vertical(Hero hero, WallDefinition wall, float x, float y, WallCollisionTuning t)
        {
            if (Between(y, wall.Y1, wall.Y2) && Near(x, wall.X1, Math.Abs(hero.DX * t.Reach)))
                hero.DX = x >= wall.X1 ? t.Push : -t.Push;
        }

        static void Angled(Hero hero, float y, float intersectY, WallCollisionTuning t)
        {
            if (Near(y, intersectY, Math.Abs(hero.DY * t.Reach)))
                hero.DY = y >= intersectY ? t.Push : -t.Push;
            if (Near(y, intersectY, t.AngledStop)) hero.DX = 0f;
        }

        /// <summary>A_near_B: a within range of b, both ends included.</summary>
        static bool Near(float a, float b, float range) => a <= b + range && a >= b - range;

        /// <summary>A_between_BC: a between b and c in either order, both ends included.</summary>
        static bool Between(float a, float b, float c) => (a >= b && a <= c) || (a >= c && a <= b);
    }
}
