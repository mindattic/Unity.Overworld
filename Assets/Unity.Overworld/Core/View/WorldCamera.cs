using System;

namespace MindAttic.Overworld.Core
{
    /// <summary>
    /// The original's camera (Camera.initCamera, RECONSTRUCTION 3.2): a point in world units that always looks straight
    /// along +Z, its Z (the zoom) held between the nearest and farthest limits unless the free camera is on, and the
    /// render range that culls what is drawn. Presentation reads it; the rules that move it (edge scroll, centring,
    /// zoom) change it through its methods.
    /// </summary>
    public sealed class WorldCamera
    {
        public WorldCamera(CameraDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            X = definition.StartX;
            Y = definition.StartY;
            Z = definition.StartZ;
        }

        public CameraDefinition Definition { get; }

        public float X { get; private set; }
        public float Y { get; private set; }
        public float Z { get; private set; }

        /// <summary>When on (the F key), Z is not clamped.</summary>
        public bool FreeCamera { get; set; }

        /// <summary>renderRange = 1 + |Z|: how far from the camera, on each axis, a thing is still drawn.</summary>
        public float RenderRange => Definition.RenderRangeBase + Math.Abs(Z);

        /// <summary>Moves the camera to (x, y, z), then applies the zoom limits as initCamera did every frame.</summary>
        public void MoveTo(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
            Clamp();
        }

        /// <summary>Holds Z within [farthest, nearest] (-3.5 to -1) unless the free camera is on.</summary>
        public void Clamp()
        {
            if (FreeCamera) return;
            if (Z < Definition.FarthestZ) Z = Definition.FarthestZ;
            if (Z > Definition.NearestZ) Z = Definition.NearestZ;
        }

        /// <summary>The original's inRange: within the render range of the camera on both X and Y, edges included.</summary>
        public bool InRange(float x, float y) => Near(x, X, RenderRange) && Near(y, Y, RenderRange);

        /// <summary>The original's A_near_B: b - range &lt;= a &lt;= b + range.</summary>
        public static bool Near(float a, float b, float range) => a <= b + range && a >= b - range;
    }
}
