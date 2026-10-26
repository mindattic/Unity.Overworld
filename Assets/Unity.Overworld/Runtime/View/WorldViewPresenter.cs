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
        /// <summary>
        /// Sorting orders of the layers: backdrop, then tiles, then the pickups in index order from <see cref="PickupsOrder"/>
        /// (renderItem, layer 1, behind the heroes), then the sorted objects from <see cref="ObjectsOrder"/> up.
        /// </summary>
        public const int BackdropOrder = -1000;
        public const int TilesOrder = 0;
        public const int PickupsOrder = 100;
        public const int ObjectsOrder = 1000;

        /// <summary>The world cursor is drawn after everything (renderCursor is the frame's last 3D draw; sorting orders stop at 32767).</summary>
        public const int CursorOrder = 30000;

        /// <summary>The status icons are drawn in front of the heroes and every object (renderIcon, layer 4), under the cursor and team button.</summary>
        public const int StatusIconOrder = CursorOrder - 10;

        /// <summary>The tent is drawn after the tiles and before the pickups (renderTent, first of layer 1).</summary>
        public const int TentOrder = TilesOrder + 50;

        /// <summary>The ring menu's select frame, then its icons, come after the status icons (renderRingIcon, layer 4).</summary>
        public const int RingSelectOrder = StatusIconOrder + 1;

        public const int RingIconsOrder = StatusIconOrder + 2;

        /// <summary>The weather, then the clouds, are layer 4's last draws (renderRain, renderCloud): after the status icons and the ring, under the HUD's quads and the cursor.</summary>
        public const int WeatherOrder = StatusIconOrder + 3;

        public const int CloudsOrder = StatusIconOrder + 4;

        /// <summary>The range circles' dots are drawn behind the heroes, after the pickups and drops (renderCircle, layer 1).</summary>
        public const int CircleOrder = ObjectsOrder - 1;

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

        /// <summary>The sleep whose fade dims the world (null: never dimmed).</summary>
        public SleepCycle Sleep { get; private set; }

        /// <summary>
        /// Dims the world by <paramref name="sleep"/>'s ambient from now on: the light is multiplied by it, so a sleep
        /// fades the world to black and back (Sun.checkSleep's device ambient, RECONSTRUCTION 6.4); the HUD is not dimmed.
        /// </summary>
        public void DimBy(SleepCycle sleep)
        {
            Sleep = sleep;
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

        readonly List<MeshRenderer> pickupRenderers = new List<MeshRenderer>();

        /// <summary>The pickups drawn (null: none).</summary>
        public Pickups Pickups { get; private set; }

        /// <summary>One quad per pickup, by index; disabled while it is picked up or beyond the render range.</summary>
        public IReadOnlyList<MeshRenderer> PickupRenderers => pickupRenderers;

        /// <summary>
        /// Draws <paramref name="pickups"/> from now on (Items.renderItem): each unpicked pickup within the camera's render
        /// range as an item-size quad of its picture at its place, after the tiles and behind the heroes, in index order.
        /// </summary>
        public void ShowPickups(Pickups pickups)
        {
            Pickups = pickups;
            if (pickups != null && pickupRenderers.Count == 0)
            {
                var root = new GameObject("Pickups").transform;
                root.SetParent(transform, false);
                var size = pickups.Data.ItemSize;
                foreach (var p in pickups.All)
                {
                    var r = NewRenderer("Pickup " + p.Index + " " + p.Name, Quad(size, size / 2f), p.File, PickupsOrder + p.Index, root);
                    r.transform.localPosition = new Vector3(p.X, p.Y, 0f);
                    pickupRenderers.Add(r);
                }
            }

            Refresh();
        }

        readonly List<MeshRenderer> dropRenderers = new List<MeshRenderer>();
        Transform dropRoot;

        /// <summary>One quad per drop slot used so far, by slot; disabled while its item is picked up or beyond the render range.</summary>
        public IReadOnlyList<MeshRenderer> DropRenderers => dropRenderers;

        /// <summary>
        /// renderDropItem: each dropped item not picked up again, within the camera's render range, as an item-size quad
        /// of its inventory picture at its place, after the pickups (in slot order) and behind the heroes.
        /// </summary>
        void RefreshDrops()
        {
            if (Pickups == null) return;
            var drops = Pickups.Drops;
            while (dropRenderers.Count < drops.Count)
            {
                if (dropRoot == null)
                {
                    dropRoot = new GameObject("Drops").transform;
                    dropRoot.SetParent(transform, false);
                }

                var size = Pickups.Data.ItemSize;
                var order = Mathf.Min(PickupsOrder + Pickups.All.Count + dropRenderers.Count, CircleOrder - 1);
                dropRenderers.Add(NewRenderer("Drop " + dropRenderers.Count, Quad(size, size / 2f), null, order, dropRoot));
            }

            for (var i = 0; i < drops.Count; i++)
            {
                var d = drops[i];
                var r = dropRenderers[i];
                r.transform.localPosition = new Vector3(d.X, d.Y, 0f);
                SetTexture(r, d.File);
                r.enabled = !d.PickedUp && View.Camera.InRange(d.X, d.Y) && HasTexture(r);
            }
        }

        /// <summary>The marker on an item's chosen target (select_hero.png), once <see cref="ShowTarget"/> is called.</summary>
        public MeshRenderer TargetRenderer { get; private set; }

        /// <summary>The pack whose chosen target is marked (null: none).</summary>
        public InventoryControl Inventory { get; private set; }

        PlayerId targetPlayer;

        /// <summary>
        /// Marks <paramref name="player"/>'s chosen target from now on (renderHeroSelect): while an item waits for its
        /// target, <paramref name="marker"/>'s picture on that hero's quad, after the heroes and objects, in the world's light.
        /// </summary>
        public void ShowTarget(InventoryControl inventory, PlayerId player, TargetMarkerDefinition marker)
        {
            Inventory = inventory;
            targetPlayer = player;
            if (TargetRenderer == null && marker != null && Party != null)
            {
                var size = Party.Data.HeroSize;
                TargetRenderer = NewRenderer("Target", Quad(size, size / 2f), marker.Picture, StatusIconOrder - 1);
            }

            Refresh();
        }

        void RefreshTarget(float alpha)
        {
            if (TargetRenderer == null || Inventory == null || Party == null) return;
            var v = Inventory.For(targetPlayer);
            if (!v.Choosing || v.Target < 0 || v.Target >= Party.Heroes.Count)
            {
                TargetRenderer.enabled = false;
                return;
            }

            var hero = Party.Heroes[v.Target];
            var x = Mathf.LerpUnclamped(hero.PreviousX, hero.X, alpha);
            var y = Mathf.LerpUnclamped(hero.PreviousY, hero.Y, alpha);
            TargetRenderer.transform.localPosition = new Vector3(x, y, 0f);
            TargetRenderer.enabled = View.Camera.InRange(x, y) && HasTexture(TargetRenderer);
        }

        readonly List<MeshRenderer> ringIcons = new List<MeshRenderer>();
        readonly List<MeshRenderer> circleDots = new List<MeshRenderer>();
        PlayerId ringPlayer;

        /// <summary>The ring menu drawn (null: none), once <see cref="ShowRing"/> is called.</summary>
        public RingMenu Ring { get; private set; }

        /// <summary>The ring's select frame (ring_select.png).</summary>
        public MeshRenderer RingSelectRenderer { get; private set; }

        /// <summary>The ring's icons, in ring order (Move, End, Skill, Item, Attack).</summary>
        public IReadOnlyList<MeshRenderer> RingIcons => ringIcons;

        /// <summary>The range circle's dots, in dot order.</summary>
        public IReadOnlyList<MeshRenderer> CircleDots => circleDots;

        /// <summary>
        /// Draws <paramref name="player"/>'s battle ring menu and range circles from now on (renderRingSelect,
        /// renderRingIcon, renderCircle): while the ring is open, the select frame and then each entry's icon around the
        /// leader (eased with it), after the status icons; while a circle is shown, its dots around the leader in their
        /// colours, behind the heroes. Each culled by the render range, in the world's light.
        /// </summary>
        public void ShowRing(Party party, PlayerId player)
        {
            if (party == null) return;
            ringPlayer = player;
            Ring = party.RingOf(player);
            if (RingSelectRenderer == null)
            {
                var root = new GameObject("Ring menu").transform;
                root.SetParent(transform, false);
                var t = Ring.Ring;
                RingSelectRenderer = NewRenderer("Ring select", Quad(t.SelectSize, t.SelectSize / 2f), t.SelectPicture, RingSelectOrder, root);
                foreach (var e in t.Entries)
                    ringIcons.Add(NewRenderer("Ring " + e.Entry, Quad(t.IconSize, t.IconSize / 2f), e.Picture, RingIconsOrder, root));
                var c = Ring.Circle;
                for (var i = 0; i < Ring.DotCount; i++)
                    circleDots.Add(NewRenderer("Circle dot " + i, Quad(c.DotSize, c.DotSize / 2f), c.GreenPicture, CircleOrder, root));
            }

            Refresh();
        }

        void RefreshRing(float alpha)
        {
            if (Ring == null || Party == null) return;
            var hero = Party.Leader(ringPlayer);
            var hx = Mathf.LerpUnclamped(hero.PreviousX, hero.X, alpha);
            var hy = Mathf.LerpUnclamped(hero.PreviousY, hero.Y, alpha);
            var size = Party.Data.HeroSize;

            Ring.SelectOffset(out var sx, out var sy);
            RingSelectRenderer.transform.localPosition = new Vector3(hx + sx, hy + sy, 0f);
            RingSelectRenderer.enabled = Ring.Open && View.Camera.InRange(hx + sx, hy + sy) && HasTexture(RingSelectRenderer);
            for (var i = 0; i < ringIcons.Count; i++)
            {
                Ring.IconOffset(i, size, out var ix, out var iy);
                var r = ringIcons[i];
                r.transform.localPosition = new Vector3(hx + ix, hy + iy, 0f);
                r.enabled = Ring.Open && View.Camera.InRange(hx + ix, hy + iy) && HasTexture(r);
            }

            for (var i = 0; i < circleDots.Count; i++)
            {
                Ring.DotOffset(i, size, out var dx, out var dy);
                var r = circleDots[i];
                r.transform.localPosition = new Vector3(hx + dx, hy + dy, 0f);
                SetTexture(r, Ring.Circle.Picture(Ring.DotColour(i)));
                r.enabled = Ring.CircleShown && View.Camera.InRange(hx + dx, hy + dy) && HasTexture(r);
            }
        }

        /// <summary>The tent's quad (renderTent), culled by the render range on x and y.</summary>
        public MeshRenderer TentRenderer { get; private set; }

        /// <summary>The weather drawn (null: none).</summary>
        public Weather Weather { get; private set; }

        /// <summary>The weather's one renderer: every particle a quad of one mesh, in index order.</summary>
        public MeshRenderer WeatherRenderer { get; private set; }

        /// <summary>The weather's quads (how many the last frame showed: <see cref="QuadBatch.LastShown"/>).</summary>
        public QuadBatch WeatherQuads { get; private set; }

        /// <summary>
        /// Draws <paramref name="weather"/> from now on (renderRain): every particle within the render range on x, y and z
        /// as a 0.04 quad of the mode's picture at its place, eased between its last two ticks, in the world's light; none
        /// while the weather is Clear. One mesh, drawn after the status icons and before the clouds.
        /// </summary>
        public void ShowWeather(Weather weather, Game source = null)
        {
            Weather = weather;
            if (source != null && game == null) game = source;
            if (weather != null && WeatherRenderer == null)
            {
                WeatherQuads = new QuadBatch("Weather particles", weather.Count);
                WeatherRenderer = NewRenderer("Weather", WeatherQuads.Mesh, weather.Current.Texture, WeatherOrder);
            }

            Refresh();
        }

        void RefreshWeather(float alpha)
        {
            if (Weather == null || WeatherRenderer == null) return;
            var kind = Weather.Current;
            if (!kind.Drawn)
            {
                WeatherRenderer.enabled = false;
                return;
            }

            SetTexture(WeatherRenderer, kind.Texture);
            var w = Weather.Data.Weather;
            for (var i = 0; i < Weather.Count; i++)
            {
                var p = Weather[i];
                var x = Mathf.LerpUnclamped(p.PreviousX, p.X, alpha);
                var y = Mathf.LerpUnclamped(p.PreviousY, p.Y, alpha);
                var z = Mathf.LerpUnclamped(p.PreviousZ, p.Z, alpha);
                if (View.IsVisible(p.X, p.Y, p.Z)) WeatherQuads.Set(i, x, y, z, w.Width, w.HalfHeight);
                else WeatherQuads.Hide(i);
            }

            WeatherQuads.Apply();
            WeatherRenderer.enabled = HasTexture(WeatherRenderer);
        }

        /// <summary>The clouds drawn (null: none).</summary>
        public Clouds Clouds { get; private set; }

        /// <summary>The clouds' one renderer: every cloud a quad of one mesh over one picture of the cloud and balloon art, in index order.</summary>
        public MeshRenderer CloudsRenderer { get; private set; }

        public QuadBatch CloudQuads { get; private set; }

        /// <summary>The cloud1, cloud2 and balloon pictures packed into one, so one mesh keeps the original's draw order (the first, farthest, first).</summary>
        public Texture2D CloudAtlas { get; private set; }

        /// <summary>
        /// Draws <paramref name="clouds"/> from now on (renderCloud): every cloud within the render range on x, y and z as a
        /// quad as wide and high as its width, of its picture, eased between its last two ticks, in the world's light, in
        /// index order. One mesh, drawn after the weather and under the HUD's quads and the cursor.
        /// </summary>
        public void ShowClouds(Clouds clouds, Game source = null)
        {
            Clouds = clouds;
            if (source != null && game == null) game = source;
            if (clouds != null && CloudsRenderer == null)
            {
                var files = new List<string>();
                for (var i = 0; i < clouds.Count; i++)
                    if (!files.Contains(clouds.File(i))) files.Add(clouds.File(i));
                var pictures = new Texture2D[files.Count];
                for (var i = 0; i < files.Count; i++) pictures[i] = textures.Load(files[i]);

                var rects = new Rect[files.Count];
                if (System.Array.TrueForAll(pictures, p => p != null && p.isReadable))
                {
                    CloudAtlas = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "Overworld cloud atlas" };
                    rects = CloudAtlas.PackTextures(pictures, 2, 1024, false);
                    CloudAtlas.filterMode = FilterMode.Bilinear;
                    CloudAtlas.wrapMode = TextureWrapMode.Clamp;
                }
                else
                {
                    Debug.LogError("Unity.Overworld: the cloud pictures (" + string.Join(", ", files) + ") must be imported and readable to be packed; the clouds are not drawn.");
                }

                var uvs = new Rect[clouds.Count];
                for (var i = 0; i < clouds.Count; i++) uvs[i] = rects[files.IndexOf(clouds.File(i))];
                CloudQuads = new QuadBatch("Cloud quads", clouds.Count, uvs);
                CloudsRenderer = NewRenderer("Clouds", CloudQuads.Mesh, null, CloudsOrder);
                if (CloudAtlas != null)
                {
                    CloudsRenderer.GetPropertyBlock(block);
                    block.SetTexture(MainTex, CloudAtlas);
                    CloudsRenderer.SetPropertyBlock(block);
                }
            }

            Refresh();
        }

        void RefreshClouds(float alpha)
        {
            if (Clouds == null || CloudsRenderer == null) return;
            for (var i = 0; i < Clouds.Count; i++)
            {
                var c = Clouds[i];
                var x = Mathf.LerpUnclamped(c.PreviousX, c.X, alpha);
                var y = Mathf.LerpUnclamped(c.PreviousY, c.Y, alpha);
                var width = Clouds.Width(i);
                if (View.IsVisible(c.X, c.Y, c.Z)) CloudQuads.Set(i, x, y, c.Z, width, width / 2f);
                else CloudQuads.Hide(i);
            }

            CloudQuads.Apply();
            CloudsRenderer.enabled = CloudAtlas != null;
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

            var tent = view.Tent;
            TentRenderer = NewRenderer("Tent", Quad(tent.Width, tent.HalfHeight), tent.File, TentOrder);
            TentRenderer.transform.localPosition = new Vector3(tent.X, tent.Y, tent.Z);

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

            if (Daylight != null || Sleep != null)
            {
                var light = Daylight != null ? Daylight.Light : new LightColour(255, 255, 255);
                if (Sleep != null) light = Sleep.Lit(light);
                material.color = new Color32(light.R, light.G, light.B, 255);
            }

            foreach (var t in tiles) t.Value.enabled = View.IsVisible(t.Key) && HasTexture(t.Value);
            TentRenderer.enabled = View.IsVisible(View.Tent) && HasTexture(TentRenderer);

            if (Pickups != null)
                for (var i = 0; i < pickupRenderers.Count; i++)
                {
                    var p = Pickups.All[i];
                    pickupRenderers[i].enabled = !p.PickedUp && View.Camera.InRange(p.X, p.Y) && HasTexture(pickupRenderers[i]);
                }

            RefreshDrops();

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
            RefreshTarget(alpha);
            RefreshRing(alpha);
            RefreshWeather(alpha);
            RefreshClouds(alpha);

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
            if (WeatherQuads != null) Release(WeatherQuads.Mesh);
            if (CloudQuads != null) Release(CloudQuads.Mesh);
            Release(CloudAtlas);
        }

        static void Release(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}
