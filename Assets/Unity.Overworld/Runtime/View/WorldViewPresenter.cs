using System.Collections.Generic;
using MindAttic.Overworld.Core;
using UnityEngine;

namespace MindAttic.Overworld.Runtime
{
    /// <summary>
    /// Draws Core's <see cref="WorldView"/> the way Engine.oneFrameRender did (RECONSTRUCTION 2.4): the frame cleared to
    /// magenta, the backdrop box first, then the area tiles, then the heroes, prop bottoms and weapons back to front by Y,
    /// each culled by the camera's render range. The original layered by draw order (everything at z 0), so every
    /// renderer here is a transparent sprite-shader quad ordered by <see cref="Renderer.sortingOrder"/>.
    /// Built in code (<see cref="Create"/>); reads the view and never changes game state.
    /// </summary>
    public sealed class WorldViewPresenter : MonoBehaviour
    {
        /// <summary>Sorting orders of the layers: backdrop, then tiles, then the sorted objects from <see cref="ObjectsOrder"/> up.</summary>
        public const int BackdropOrder = -1000;
        public const int TilesOrder = 0;
        public const int ObjectsOrder = 300;

        static readonly int MainTex = Shader.PropertyToID("_MainTex");

        readonly List<KeyValuePair<AreaTileDefinition, MeshRenderer>> tiles = new List<KeyValuePair<AreaTileDefinition, MeshRenderer>>();
        readonly Dictionary<(DrawableKind, int), MeshRenderer> objects = new Dictionary<(DrawableKind, int), MeshRenderer>();
        readonly Dictionary<(float, float), Mesh> quads = new Dictionary<(float, float), Mesh>();
        TextureLibrary textures;
        Material material;
        MaterialPropertyBlock block;

        public WorldView View { get; private set; }
        public Camera Camera { get; private set; }
        public MeshRenderer Backdrop { get; private set; }

        /// <summary>Where each hero stands and what it shows; the start poses until locomotion moves them.</summary>
        public HeroPose[] Poses { get; set; }

        /// <summary>The tiles' renderers, in the view's tile order.</summary>
        public IReadOnlyList<KeyValuePair<AreaTileDefinition, MeshRenderer>> Tiles => tiles;

        public MeshRenderer ObjectRenderer(DrawableKind kind, int index) => objects.TryGetValue((kind, index), out var r) ? r : null;

        /// <summary>Builds the camera, backdrop, tiles and objects for <paramref name="view"/> under one root object.</summary>
        public static WorldViewPresenter Create(WorldView view, TextureLibrary textures = null)
        {
            var root = new GameObject("World");
            var presenter = root.AddComponent<WorldViewPresenter>();
            presenter.Build(view, textures ?? new TextureLibrary());
            return presenter;
        }

        void Build(WorldView view, TextureLibrary library)
        {
            View = view;
            textures = library;
            block = new MaterialPropertyBlock();
            material = new Material(Shader.Find("Sprites/Default")) { name = "Overworld sprite" };
            Poses = view.StartPoses();

            var def = view.Camera.Definition;
            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(transform, false);
            Camera = cameraObject.AddComponent<Camera>();
            Camera.orthographic = false;
            Camera.fieldOfView = def.FieldOfView;
            Camera.nearClipPlane = def.Near;
            Camera.farClipPlane = def.Far;
            Camera.clearFlags = CameraClearFlags.SolidColor;
            var c = view.View.Backdrop.ClearColor;
            Camera.backgroundColor = new Color32(c[0], c[1], c[2], c[3]);
            cameraObject.tag = "MainCamera";

            var b = view.View.Backdrop;
            Backdrop = NewRenderer("Backdrop", QuadMeshes.Box(), b.Texture, BackdropOrder);
            Backdrop.transform.localScale = new Vector3(b.Width, b.Height, b.Depth);

            var tileRoot = new GameObject("Area tiles").transform;
            tileRoot.SetParent(transform, false);
            var size = view.Grid.TileSize;
            foreach (var tile in view.Grid.Tiles)
            {
                var r = NewRenderer(tile.Name, Quad(size, size / 2f), tile.File, TilesOrder, tileRoot);
                r.transform.localPosition = new Vector3(tile.X, tile.Y, 0f);
                tiles.Add(new KeyValuePair<AreaTileDefinition, MeshRenderer>(tile, r));
            }

            var objectRoot = new GameObject("Objects").transform;
            objectRoot.SetParent(transform, false);
            foreach (var d in view.Objects(Poses))
                objects[(d.Kind, d.Index)] = NewRenderer(d.Kind + " " + d.Index, Quad(d.Width, d.HalfHeight), d.File, ObjectsOrder, objectRoot);

            Refresh();
        }

        void LateUpdate() => Refresh();

        /// <summary>Applies the camera, the culling and the draw order for this frame.</summary>
        public void Refresh()
        {
            if (View == null) return;
            var cam = View.Camera;
            Camera.transform.localPosition = new Vector3(cam.X, cam.Y, cam.Z);
            Camera.transform.localRotation = Quaternion.identity;

            foreach (var t in tiles) t.Value.enabled = View.IsVisible(t.Key) && HasTexture(t.Value);

            var order = View.BackToFront(Poses);
            for (var i = 0; i < order.Count; i++)
            {
                var d = order[i];
                if (!objects.TryGetValue((d.Kind, d.Index), out var r)) continue;
                r.transform.localPosition = new Vector3(d.X, d.Y, d.Z);
                r.sortingOrder = ObjectsOrder + i;
                SetTexture(r, d.File);
                r.enabled = View.IsVisible(d) && HasTexture(r);
            }
        }

        MeshRenderer NewRenderer(string name, Mesh mesh, string file, int order, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingOrder = order;
            SetTexture(r, file);
            r.enabled = HasTexture(r);
            return r;
        }

        Mesh Quad(float width, float halfHeight)
        {
            if (!quads.TryGetValue((width, halfHeight), out var mesh))
                quads[(width, halfHeight)] = mesh = QuadMeshes.Quad(width, halfHeight);
            return mesh;
        }

        readonly Dictionary<MeshRenderer, string> shown = new Dictionary<MeshRenderer, string>();

        void SetTexture(MeshRenderer r, string file)
        {
            if (shown.TryGetValue(r, out var current) && current == file) return;
            shown[r] = file;
            r.GetPropertyBlock(block);
            var texture = textures.Load(file);
            if (texture != null) block.SetTexture(MainTex, texture);
            else block.Clear();
            r.SetPropertyBlock(block);
        }

        bool HasTexture(MeshRenderer r) => shown.TryGetValue(r, out var file) && textures.Load(file) != null;

        /// <summary>The picture a renderer shows (the original path), or null.</summary>
        public string FileOf(MeshRenderer r) => shown.TryGetValue(r, out var file) ? file : null;

        void OnDestroy()
        {
            foreach (var m in quads.Values) Release(m);
            if (Backdrop != null) Release(Backdrop.GetComponent<MeshFilter>().sharedMesh);
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
