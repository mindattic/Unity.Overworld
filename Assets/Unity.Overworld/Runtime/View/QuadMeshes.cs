using UnityEngine;

namespace MindAttic.Overworld.Runtime
{
    /// <summary>
    /// The original's two pieces of geometry, rebuilt as meshes: setupVertex's quad and loadCube's box. Direct3D and
    /// Unity are both left-handed with Y up, so positions carry over as they are; texture V is flipped (Direct3D's
    /// v = 0 is the top of the picture, Unity's is the bottom).
    /// </summary>
    public static class QuadMeshes
    {
        /// <summary>
        /// setupVertex(width, halfHeight): a quad from x 0 to <paramref name="width"/> and y -<paramref name="halfHeight"/>
        /// to +<paramref name="halfHeight"/> at z 0, the whole picture on it, top of the picture at the top.
        /// </summary>
        public static Mesh Quad(float width, float halfHeight)
        {
            var mesh = new Mesh { name = "Quad " + width + " x " + halfHeight * 2 };
            mesh.vertices = new[]
            {
                new Vector3(0f, -halfHeight, 0f),
                new Vector3(width, -halfHeight, 0f),
                new Vector3(width, halfHeight, 0f),
                new Vector3(0f, halfHeight, 0f),
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            mesh.colors32 = White(4);
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// loadCube's 36 vertices (a unit box around the origin, every face carrying the whole picture), exactly as listed,
        /// for the backdrop. It is seen from inside, so it is drawn without back-face culling.
        /// </summary>
        public static Mesh Box()
        {
            // x, y, z, tu, tv in the original's order: top, bottom, left, right, back, front.
            float[] v =
            {
                -0.5f, 0.5f, 0.5f, 0, 0, 0.5f, 0.5f, 0.5f, 1, 0, -0.5f, 0.5f, -0.5f, 0, 1,
                0.5f, 0.5f, -0.5f, 1, 1, -0.5f, 0.5f, -0.5f, 0, 1, 0.5f, 0.5f, 0.5f, 1, 0,
                -0.5f, -0.5f, -0.5f, 1, 0, 0.5f, -0.5f, 0.5f, 0, 1, -0.5f, -0.5f, 0.5f, 0, 0,
                0.5f, -0.5f, 0.5f, 0, 1, -0.5f, -0.5f, -0.5f, 1, 0, 0.5f, -0.5f, -0.5f, 1, 1,
                -0.5f, 0.5f, -0.5f, 0, 0, -0.5f, -0.5f, -0.5f, 1, 0, -0.5f, 0.5f, 0.5f, 0, 1,
                -0.5f, 0.5f, 0.5f, 0, 1, -0.5f, -0.5f, -0.5f, 1, 0, -0.5f, -0.5f, 0.5f, 1, 1,
                0.5f, 0.5f, -0.5f, 0, 0, 0.5f, 0.5f, 0.5f, 1, 0, 0.5f, -0.5f, -0.5f, 0, 1,
                0.5f, -0.5f, 0.5f, 1, 1, 0.5f, -0.5f, -0.5f, 0, 1, 0.5f, 0.5f, 0.5f, 1, 0,
                -0.5f, 0.5f, -0.5f, 0, 0, 0.5f, 0.5f, -0.5f, 1, 0, -0.5f, -0.5f, -0.5f, 0, 1,
                0.5f, -0.5f, -0.5f, 1, 1, -0.5f, -0.5f, -0.5f, 0, 1, 0.5f, 0.5f, -0.5f, 1, 0,
                0.5f, 0.5f, 0.5f, 1, 0, -0.5f, 0.5f, 0.5f, 0, 0, -0.5f, -0.5f, 0.5f, 0, 1,
                -0.5f, -0.5f, 0.5f, 0, 1, 0.5f, -0.5f, 0.5f, 1, 1, 0.5f, 0.5f, 0.5f, 1, 0,
            };

            const int count = 36;
            var positions = new Vector3[count];
            var uvs = new Vector2[count];
            var triangles = new int[count];
            for (var i = 0; i < count; i++)
            {
                positions[i] = new Vector3(v[i * 5], v[i * 5 + 1], v[i * 5 + 2]);
                uvs[i] = new Vector2(v[i * 5 + 3], 1f - v[i * 5 + 4]);
                triangles[i] = i;
            }

            var mesh = new Mesh { name = "Backdrop box" };
            mesh.vertices = positions;
            mesh.uv = uvs;
            mesh.colors32 = White(count);
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        static Color32[] White(int count)
        {
            var colours = new Color32[count];
            for (var i = 0; i < count; i++) colours[i] = new Color32(255, 255, 255, 255);
            return colours;
        }
    }
}
