using MindAttic.Overworld.Core;
using UnityEngine;

namespace MindAttic.Overworld.Runtime
{
    /// <summary>
    /// Draws the team button (Gui/TeamBtn.vb renderteamBox, RECONSTRUCTION 13): icon_team while the party follows,
    /// icon_solo while it kneels, on the 28 x 28 square at (18, 75) of the virtual 640 x 480 screen. The original drew it
    /// in screen pixels on a 640 x 480 back buffer the window stretched, so the square is placed and sized as that same
    /// fraction of the camera's view: a quad just in front of the camera, over the world and under the cursor.
    /// Built in code (<see cref="Create"/>); reads <see cref="Core.Party.Team"/> and never changes game state.
    /// </summary>
    public sealed class TeamButtonPresenter : MonoBehaviour
    {
        /// <summary>Over every world object, under the world cursor (<see cref="WorldViewPresenter.CursorOrder"/>).</summary>
        public const int ButtonOrder = WorldViewPresenter.CursorOrder - 1;

        /// <summary>How far in front of the camera the quad is drawn (beyond its 0.01 near plane).</summary>
        public const float Distance = 0.05f;

        static readonly int MainTex = Shader.PropertyToID("_MainTex");

        TextureLibrary textures;
        Material material;
        Mesh mesh;
        MaterialPropertyBlock block;
        string shown;

        public Camera Camera { get; private set; }
        public TeamButtonDefinition Definition { get; private set; }
        public CursorDefinition VirtualScreen { get; private set; }
        public Party Party { get; private set; }
        public MeshRenderer Renderer { get; private set; }

        /// <summary>The picture it shows now (the original path).</summary>
        public string File => shown;

        /// <summary>Builds the button in front of <paramref name="camera"/>; <paramref name="screen"/> gives the virtual screen's size.</summary>
        public static TeamButtonPresenter Create(Camera camera, TeamButtonDefinition definition, CursorDefinition screen, Party party, TextureLibrary textures = null)
        {
            var go = new GameObject("Team button");
            go.transform.SetParent(camera.transform, false);
            var presenter = go.AddComponent<TeamButtonPresenter>();
            presenter.Build(camera, definition, screen, party, textures ?? new TextureLibrary());
            return presenter;
        }

        void Build(Camera camera, TeamButtonDefinition definition, CursorDefinition screen, Party party, TextureLibrary library)
        {
            Camera = camera;
            Definition = definition;
            VirtualScreen = screen;
            Party = party;
            textures = library;
            block = new MaterialPropertyBlock();
            material = SpriteMaterials.Create("Overworld team button");
            mesh = QuadMeshes.Quad(1f, 0.5f);
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            Renderer = gameObject.AddComponent<MeshRenderer>();
            Renderer.sharedMaterial = material;
            Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Renderer.receiveShadows = false;
            Renderer.sortingOrder = ButtonOrder;
            Refresh();
        }

        void LateUpdate() => Refresh();

        /// <summary>Places the square for the camera's current view and shows the team or solo picture.</summary>
        public void Refresh()
        {
            if (Camera == null || Definition == null) return;
            var height = 2f * Distance * Mathf.Tan(Camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var width = height * Camera.aspect;
            var sx = width / VirtualScreen.ScreenWidth;
            var sy = height / VirtualScreen.ScreenHeight;
            transform.localRotation = Quaternion.identity;
            transform.localScale = new Vector3(Definition.Width * sx, Definition.Height * sy, 1f);
            transform.localPosition = new Vector3(
                -width / 2f + Definition.X * sx,
                height / 2f - (Definition.Y + Definition.Height / 2f) * sy,
                Distance);

            var file = Definition.Picture(Party == null || Party.Team);
            if (file != shown)
            {
                shown = file;
                Renderer.GetPropertyBlock(block);
                var texture = textures.Load(file);
                if (texture != null) block.SetTexture(MainTex, texture);
                else block.Clear();
                Renderer.SetPropertyBlock(block);
                Renderer.enabled = texture != null;
            }
        }

        void OnDestroy()
        {
            Release(mesh);
            Release(material);
        }

        static void Release(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}
