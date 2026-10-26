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

        /// <summary>The world cursor is drawn after everything (renderCursor is the frame's last 3D draw; sorting orders stop at 32767).</summary>
        public const int CursorOrder = 30000;

        /// <summary>The status icons are drawn in front of the heroes and every object (renderIcon, layer 4), under the cursor and team button.</summary>
        public const int StatusIconOrder = CursorOrder - 10;

        static readonly int MainTex = Shader.PropertyToID("_MainTex");

        readonly List<KeyValuePair<AreaTileDefinition, MeshRenderer>> tiles = new List<KeyValuePair<AreaTileDefinition, MeshRenderer>>();
        readonly Dictionary<(DrawableKind, int), MeshRenderer> objects = new Dictionary<(DrawableKind, int), MeshRenderer>();
        readonly Dictionary<(float, float), Mesh> quads = new Dictionary<(float, float), Mesh>();
        readonly List<MeshRenderer> statusIcons = new List<MeshRenderer>();
        readonly List<Material> iconMaterials = new List<Material>();
        TextureLibrary textures;
        Material material;
        MaterialPropertyBlock block;

        public WorldView View { get; private set; }
        public Camera Camera { get; private set; }
        public MeshRenderer Backdrop { get; private set; }

        /// <summary>The world cursor's quad, once <see cref="ShowCursor"/> is called.</summary>
        public MeshRenderer CursorRenderer { get; private set; }

        /// <summary>The world cursor drawn (null: none).</summary>
        public WorldCursor Cursor { get; private set; }

        /// <summary>Where each hero stands and what it shows: the start poses, or the party's each frame once <see cref="Follow"/> is called.</summary>
        public HeroPose[] Poses { get; set; }

        /// <summary>The party drawn, when following one (null draws <see cref="Poses"/> as set).</summary>
        public Party Party { get; private set; }

        Game game;

        /// <summary>The clock whose light the world is drawn in (null: white).</summary>
        public WorldClock Daylight { get; private set; }

        /// <summary>The colour every world quad is drawn in now (the original's material colour; the HUD is not lit).</summary>
        public Color Tint => material != null ? material.color : Color.white;

        /// <summary>
        /// Lights the world by <paramref name="clock"/> from now on: every quad (backdrop, tiles, objects, the world cursor)
        /// is multiplied by its light, as the original's material diffuse and ambient were (renderTime, RECONSTRUCTION 6.2).
        /// </summary>
        public void LightBy(WorldClock clock)
        {
            Daylight = clock;
            Refresh();
        }

        /// <summary>Draws <paramref name="party"/>'s heroes from now on, placed between their last two ticks of <paramref name="source"/>.</summary>
        public void Follow(Party party, Game source)
        {
            Party = party;
            game = source;
            BuildStatusIcons();
            Refresh();
        }

        /// <summary>The status icon above each hero (StatusIcon.vb), in party order; disabled while the hero is Fine.</summary>
        public IReadOnlyList<MeshRenderer> StatusIcons => statusIcons;

        /// <summary>One quad per hero, each with its own material so a poisoned hero's icon can be tinted.</summary>
        void BuildStatusIcons()
        {
            if (Party == null || statusIcons.Count > 0) return;
            var v = Party.Data.Vitals;
            var root = new GameObject("Status icons").transform;
            root.SetParent(transform, false);
            foreach (var hero in Party.Heroes)
            {
                var r = NewRenderer("Status " + hero.Name, Quad(v.IconSize, v.IconSize / 2f), null, StatusIconOrder, root);
                var m = new Material(material) { name = "Overworld status icon " + hero.Name };
                r.sharedMaterial = m;
                iconMaterials.Add(m);
                statusIcons.Add(r);
            }
        }

        /// <summary>Draws <paramref name="cursor"/> from now on: its picture, at its position plus the draw offset, over everything.</summary>
        public void ShowCursor(WorldCursor cursor)
        {
            Cursor = cursor;
            if (cursor != null && CursorRenderer == null)
            {
                var def = cursor.Definition;
                CursorRenderer = NewRenderer("Cursor", Quad(def.Size, def.Size / 2f), cursor.File, CursorOrder);
            }

            Refresh();
        }

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
            var alpha = game != null ? Mathf.Clamp01(game.Alpha) : 1f;
            Camera.transform.localPosition = Vector3.LerpUnclamped(
                new Vector3(cam.PreviousX, cam.PreviousY, cam.PreviousZ), new Vector3(cam.X, cam.Y, cam.Z), alpha);
            Camera.transform.localRotation = Quaternion.identity;

            if (Daylight != null)
            {
                var light = Daylight.Light;
                material.color = new Color32(light.R, light.G, light.B, 255);
            }

            foreach (var t in tiles) t.Value.enabled = View.IsVisible(t.Key) && HasTexture(t.Value);

            if (Party != null) Poses = View.Poses(Party, game != null ? game.Alpha : 1f);

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

            RefreshStatusIcons(alpha);

            if (Cursor != null && CursorRenderer != null)
            {
                var offset = Cursor.Definition.DrawOffset;
                var x = Mathf.LerpUnclamped(Cursor.PreviousX, Cursor.X, alpha);
                var y = Mathf.LerpUnclamped(Cursor.PreviousY, Cursor.Y, alpha);
                CursorRenderer.transform.localPosition = new Vector3(x + offset.X, y + offset.Y, Cursor.Z);
                SetTexture(CursorRenderer, Cursor.File);
                CursorRenderer.enabled = HasTexture(CursorRenderer);
            }
        }

        /// <summary>
        /// renderIcon: each hero's status picture at its position plus the icon offset (eased like the hero), in the
        /// world's light; the poisoned animation is also tinted (BIBLE). A Fine hero's icon is hidden (the original drew blank).
        /// </summary>
        void RefreshStatusIcons(float alpha)
        {
            if (Party == null) return;
            var v = Party.Data.Vitals;
            var light = material.color;
            for (var i = 0; i < statusIcons.Count && i < Party.Heroes.Count; i++)
            {
                var hero = Party.Heroes[i];
                var r = statusIcons[i];
                var x = Mathf.LerpUnclamped(hero.PreviousX, hero.X, alpha);
                var y = Mathf.LerpUnclamped(hero.PreviousY, hero.Y, alpha);
                var z = Mathf.LerpUnclamped(hero.PreviousZ, hero.Z, alpha);
                r.transform.localPosition = new Vector3(x + v.IconOffset.X, y + v.IconOffset.Y, z);
                var tint = hero.Status == HeroStatus.Poisoned ? (Color)new Color32(v.PoisonedTint.R, v.PoisonedTint.G, v.PoisonedTint.B, 255) : Color.white;
                iconMaterials[i].color = light * tint;
                SetTexture(r, hero.StatusIcon);
                r.enabled = hero.StatusIcon != null && HasTexture(r);
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
            foreach (var m in iconMaterials) Release(m);
        }

        static void Release(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}
