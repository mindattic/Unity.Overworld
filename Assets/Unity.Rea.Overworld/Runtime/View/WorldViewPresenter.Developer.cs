using MindAttic.Overworld.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace MindAttic.Overworld.Runtime
{
    /// <summary>
    /// What the developer tools draw (U.OVW-T47; RECONSTRUCTION 2.4, 5.2 and 14.3): the path dots (renderPath, after the
    /// tent and before the pickups), the wall lines (renderwall, after the drops; drawn only with the tools on, BIBLE) and
    /// the render switches: F1's wireframe on the world camera, F2's untextured quads and F3's back-face culling through the
    /// sprite shader's globals, and F5's filter on every world picture.
    /// </summary>
    public sealed partial class WorldViewPresenter
    {
        /// <summary>The path dots come after the tent and before the pickups (renderTent, renderPath, renderItem).</summary>
        public const int PathOrder = TentOrder + 1;

        /// <summary>The wall lines come after the pickups and drops and before the range circles (renderwall, renderCircle).</summary>
        public const int WallsOrder = CircleOrder - 1;

        /// <summary>The sprite shader's globals the F2 and F3 switches set (Rendering/Resources/Overworld/Sprite.shader).</summary>
        public const string UntexturedGlobal = "_OverworldUntextured";

        public const string CullBackGlobal = "_OverworldCullBack";

        static readonly int UntexturedId = Shader.PropertyToID(UntexturedGlobal);
        static readonly int CullBackId = Shader.PropertyToID(CullBackGlobal);

        /// <summary>The developer tools drawn (null: none), once <see cref="ShowDeveloper"/> is called.</summary>
        public DeveloperTools Developer { get; private set; }

        /// <summary>Every path slot a quad of one mesh, in slot order.</summary>
        public QuadBatch PathQuads { get; private set; }

        public MeshRenderer PathRenderer { get; private set; }

        /// <summary>The wall lines: one line per wall, one mesh.</summary>
        public MeshRenderer WallsRenderer { get; private set; }

        /// <summary>
        /// Draws <paramref name="tools"/> from now on: the path dots while the path is shown, <paramref name="walls"/>' lines
        /// while the tools are on, and the render switches.
        /// </summary>
        public void ShowDeveloper(DeveloperTools tools, WallData walls)
        {
            Developer = tools;
            if (tools == null) return;
            var root = new GameObject("Developer").transform;
            root.SetParent(transform, false);

            var path = tools.Trail.Tuning;
            PathQuads = new QuadBatch("Path dots", path.Slots);
            PathRenderer = NewRenderer("Path dots", PathQuads.Mesh, path.Picture, PathOrder, root);

            if (walls != null && walls.Walls.Count > 0)
            {
                var n = walls.Walls.Count;
                var vertices = new Vector3[n * 2];
                var uvs = new Vector2[n * 2];
                var colours = new Color32[n * 2];
                var indices = new int[n * 2];
                for (var i = 0; i < n; i++)
                {
                    var w = walls.Walls[i];
                    vertices[i * 2] = new Vector3(w.X1, w.Y1, 0f);
                    vertices[i * 2 + 1] = new Vector3(w.X2, w.Y2, 0f);
                    for (var v = i * 2; v < i * 2 + 2; v++)
                    {
                        uvs[v] = new Vector2(0.5f, 0.5f);
                        colours[v] = new Color32(255, 255, 255, 255);
                        indices[v] = v;
                    }
                }

                var mesh = new Mesh { name = "Wall lines", vertices = vertices, uv = uvs, colors32 = colours };
                mesh.SetIndices(indices, MeshTopology.Lines, 0);
                mesh.RecalculateBounds();
                quads[(-1f, -1f)] = mesh; // released with the quads
                WallsRenderer = NewRenderer("Wall lines", mesh, tools.Data.WallPicture, WallsOrder, root);
            }

            if (Camera != null && Camera.GetComponent<WireframeSwitch>() == null) Camera.gameObject.AddComponent<WireframeSwitch>().Tools = tools;
            Refresh();
        }

        void RefreshDeveloper()
        {
            var tools = Developer;
            if (tools == null) return;
            var trail = tools.Trail;
            if (PathQuads != null)
            {
                var size = trail.Tuning.Size;
                for (var i = 0; i < trail.Slots; i++)
                {
                    if (trail.Shown && trail.Active(i) && View.Camera.InRange(trail.X(i), trail.Y(i))) PathQuads.Set(i, trail.X(i), trail.Y(i), 0f, size, size / 2f);
                    else PathQuads.Hide(i);
                }

                PathQuads.Apply();
                PathRenderer.enabled = trail.Shown && HasTexture(PathRenderer);
            }

            if (WallsRenderer != null) WallsRenderer.enabled = tools.Enabled && HasTexture(WallsRenderer);

            Shader.SetGlobalFloat(UntexturedId, tools.Textures ? 0f : 1f);
            Shader.SetGlobalFloat(CullBackId, tools.BackFaceCulling ? 1f : 0f);
            textures.Filter = tools.LinearFilter ? (FilterMode?)null : FilterMode.Point;
        }
    }

    /// <summary>
    /// F1's wireframe (FillMode.WireFrame) on the camera it sits on: GL.wireframe is set around that camera's rendering,
    /// under URP (beginCameraRendering / endCameraRendering) or the built-in pipeline (OnPreRender / OnPostRender).
    /// </summary>
    public sealed class WireframeSwitch : MonoBehaviour
    {
        public DeveloperTools Tools { get; set; }

        Camera own;

        void OnEnable()
        {
            own = GetComponent<Camera>();
            RenderPipelineManager.beginCameraRendering += Begin;
            RenderPipelineManager.endCameraRendering += End;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= Begin;
            RenderPipelineManager.endCameraRendering -= End;
            GL.wireframe = false;
        }

        void Begin(ScriptableRenderContext context, Camera camera)
        {
            if (camera == own) GL.wireframe = Tools != null && Tools.Wireframe;
        }

        void End(ScriptableRenderContext context, Camera camera)
        {
            if (camera == own) GL.wireframe = false;
        }

        void OnPreRender() => GL.wireframe = Tools != null && Tools.Wireframe;

        void OnPostRender() => GL.wireframe = false;
    }
}
