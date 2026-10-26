using UnityEngine;
using UnityEngine.Rendering;

namespace MindAttic.Overworld.Runtime
{
    /// <summary>
    /// Many of setupVertex's quads in one dynamic mesh, drawn in index order by one renderer: the weather's 2,501 particles
    /// and the 401 clouds are one draw each rather than thousands of objects. Each quad is placed every frame
    /// (<see cref="Set"/>) or folded to a point so nothing of it is drawn (<see cref="Hide"/>), then <see cref="Apply"/>
    /// uploads the frame. The triangles never change, so the original's draw order (index order) is kept.
    /// </summary>
    public sealed class QuadBatch
    {
        readonly Vector3[] vertices;

        /// <param name="name">The mesh's name.</param>
        /// <param name="count">How many quads.</param>
        /// <param name="uvRects">Each quad's part of the picture (null: every quad shows the whole picture).</param>
        public QuadBatch(string name, int count, Rect[] uvRects = null)
        {
            Count = count;
            vertices = new Vector3[count * 4];
            var uvs = new Vector2[count * 4];
            var colours = new Color32[count * 4];
            var triangles = new int[count * 6];
            for (var i = 0; i < count; i++)
            {
                var r = uvRects != null ? uvRects[i] : new Rect(0f, 0f, 1f, 1f);
                var v = i * 4;
                uvs[v] = new Vector2(r.xMin, r.yMin);
                uvs[v + 1] = new Vector2(r.xMax, r.yMin);
                uvs[v + 2] = new Vector2(r.xMax, r.yMax);
                uvs[v + 3] = new Vector2(r.xMin, r.yMax);
                for (var c = 0; c < 4; c++) colours[v + c] = new Color32(255, 255, 255, 255);
                var t = i * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 2;
                triangles[t + 2] = v + 1;
                triangles[t + 3] = v;
                triangles[t + 4] = v + 3;
                triangles[t + 5] = v + 2;
            }

            Mesh = new Mesh { name = name };
            if (vertices.Length > 65535) Mesh.indexFormat = IndexFormat.UInt32;
            Mesh.MarkDynamic();
            Mesh.vertices = vertices;
            Mesh.uv = uvs;
            Mesh.colors32 = colours;
            Mesh.triangles = triangles;
            // The quads move all over the world every frame: bounds that hold all of it, so Unity never culls the batch.
            Mesh.bounds = new Bounds(Vector3.zero, new Vector3(10000f, 10000f, 10000f));
        }

        public Mesh Mesh { get; }

        public int Count { get; }

        /// <summary>How many quads are shown in the frame being built.</summary>
        public int Shown { get; private set; }

        /// <summary>
        /// Puts quad <paramref name="index"/> at (x, y, z) as setupVertex built it: x to x + width, y - halfHeight to
        /// y + halfHeight.
        /// </summary>
        public void Set(int index, float x, float y, float z, float width, float halfHeight)
        {
            var v = index * 4;
            vertices[v] = new Vector3(x, y - halfHeight, z);
            vertices[v + 1] = new Vector3(x + width, y - halfHeight, z);
            vertices[v + 2] = new Vector3(x + width, y + halfHeight, z);
            vertices[v + 3] = new Vector3(x, y + halfHeight, z);
            Shown++;
        }

        /// <summary>Folds quad <paramref name="index"/> to a point: it covers no pixel.</summary>
        public void Hide(int index)
        {
            var v = index * 4;
            vertices[v] = vertices[v + 1] = vertices[v + 2] = vertices[v + 3] = Vector3.zero;
        }

        /// <summary>Uploads the frame's quads and starts counting the next frame's.</summary>
        public void Apply()
        {
            Mesh.SetVertices(vertices, 0, vertices.Length, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            LastShown = Shown;
            Shown = 0;
        }

        /// <summary>How many quads the last applied frame showed.</summary>
        public int LastShown { get; private set; }

        /// <summary>Where quad <paramref name="index"/>'s first corner (x, y - halfHeight, z) is in the frame being built.</summary>
        public Vector3 Corner(int index) => vertices[index * 4];
    }
}
