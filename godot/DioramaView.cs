using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using LivingMyth.Sim;

// North Star Diorama — the zoomed-in region slice of the living-atlas viewer. Renders
// Blender-authored diorama miniatures at REAL sim site positions over an isometric terrain
// plane, wrapped in parchment/brass North Star chrome with a warm-grade + grain + vignette post.
//
// 100% viewer-only / read-model: reads regions/sites/chronicle, never writes sim state, never
// saves. Two entry modes:
//   • PRODUCTION BRIDGE — opened as an overlay from the atlas (Region Lens "⛰ Enter the Diorama"
//     or F3) for the *currently selected region of the live world*. Main sets SourceWorld +
//     SourceRegionId + OnClose before adding it; "← Atlas"/Esc closes the overlay, atlas intact.
//   • STANDALONE/DEV — launched as its own scene (res://DioramaView.tscn) or for screenshots;
//     with no source it builds its own deterministic seed-7 world and picks the most-built region.
public partial class DioramaView : Control
{
    private const int Seed = 7;
    private const int Years = 462;   // a settled age — echoes the North Star reference frames

    // Production-bridge inputs (set by Main before AddChild); null ⇒ standalone/dev mode.
    public World? SourceWorld;
    public int SourceRegionId = -1;
    public System.Action? OnClose;

    private World _world = null!;
    private int _regionId;
    private Font _serif = null!, _sc = null!;
    private readonly Dictionary<string, Texture2D> _tex = new();

    private static readonly Color Parchment = new("f2e5c2");
    private static readonly Color ParchmentDeep = new("e7d4a8");
    private static readonly Color Ink = new("3a2c19");
    private static readonly Color InkSoft = new("6f5b3e");
    private static readonly Color Gold = new("c9973f");
    private static readonly Color Ember = new("b0432e");

    public override void _Ready()
    {
        if (OS.GetEnvironment("LM_DIORAMA_SHOT") != "")
            DisplayServer.WindowSetSize(new Vector2I(1600, 920));

        _serif = LoadFont("res://assets/fonts/Alegreya-VariableFont.ttf");
        _sc = LoadFont("res://assets/fonts/AlegreyaSC-Medium.ttf");
        LoadTextures();

        if (SourceWorld != null)
        {
            // production bridge: render the live world's currently selected region
            _world = SourceWorld;
            _regionId = SourceRegionId >= 0 && SourceRegionId < _world.Regions.Count ? SourceRegionId : PickRegion();
        }
        else
        {
            // standalone/dev: a fresh deterministic world, most-built region
            var (config, names) = DataLoader.Load();
            _world = new World(Seed, config, names);
            _world.SeedWorld();
            while (_world.Year < Years) _world.Tick();
            _regionId = PickCaptureRegion();
        }

        MouseFilter = MouseFilterEnum.Stop;   // overlay swallows clicks meant for the atlas beneath
        var bg = new ColorRect { Color = new Color("23323a"), MouseFilter = MouseFilterEnum.Stop };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var canvas = new DioramaCanvas { View = this };
        canvas.SetAnchorsPreset(LayoutPreset.FullRect);
        canvas.TextureFilter = TextureFilterEnum.Linear;
        AddChild(canvas);

        BuildChrome();
        BuildPost();

        if (OS.GetEnvironment("LM_DIORAMA_SHOT") is string shot && shot != "")
            _ = SelfShot(shot);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true } k && (k.Keycode == Key.Escape || k.Keycode == Key.F3))
        {
            GetViewport().SetInputAsHandled();   // don't let Main's F3 re-open while we close
            Close();
        }
    }

    private void Close()
    {
        if (OnClose != null) OnClose();                          // overlay mode: Main frees us
        else GetTree().ChangeSceneToFile("res://Main.tscn");     // standalone mode: swap back
    }

    private static Font LoadFont(string path)
    {
        var f = new FontFile();
        f.LoadDynamicFont(ProjectSettings.GlobalizePath(path));
        return f;
    }

    private void LoadTextures()
    {
        string dir = ProjectSettings.GlobalizePath("res://assets/diorama/");
        foreach (var f in System.IO.Directory.GetFiles(dir, "*.png"))
        {
            var img = Image.LoadFromFile(f);
            _tex[System.IO.Path.GetFileNameWithoutExtension(f)] = ImageTexture.CreateFromImage(img);
        }
    }

    public Texture2D? Tex(string key) => _tex.GetValueOrDefault(key);
    public World World => _world;
    public int RegionId => _regionId;
    public Font Serif => _serif;
    public Font Sc => _sc;

    private int PickRegion()
    {
        int best = -1, bestSites = -1;
        double bestH = -1;
        foreach (var r in _world.Regions)
        {
            if (r.ControllingFactionId == null) continue;
            int s = _world.Sites.ForRegion(r.Id).Count;
            if (s > bestSites || (s == bestSites && r.Harvest > bestH))
            {
                best = r.Id; bestSites = s; bestH = r.Harvest;
            }
        }
        if (best < 0)   // no held region — fall back to whoever has the most places
            foreach (var r in _world.Regions)
            {
                int s = _world.Sites.ForRegion(r.Id).Count;
                if (s > bestSites) { best = r.Id; bestSites = s; }
            }
        return Math.Max(0, best);
    }

    // Standalone capture only: honor LM_DIORAMA_TERRAIN (forest/coast/highland/plains, or "wild"
    // for an unclaimed region) so the screenshot harness can target a specific terrain; otherwise
    // the normal most-built pick. Never used on the production bridge path (Main passes a region).
    private int PickCaptureRegion()
    {
        string want = OS.GetEnvironment("LM_DIORAMA_TERRAIN");
        if (want == "") return PickRegion();
        bool wild = want == "wild";
        int best = -1, bestSites = -1;
        foreach (var r in _world.Regions)
        {
            bool held = r.ControllingFactionId != null;
            if (wild ? held : (!held || r.TerrainType != want)) continue;
            int s = _world.Sites.ForRegion(r.Id).Count;
            if (s > bestSites) { best = r.Id; bestSites = s; }
        }
        return best >= 0 ? best : PickRegion();
    }

    public static Color FactionTint(string? fid) => fid switch
    {
        "highland" => new Color("6b7a99"),
        "shore" => new Color("4f8f89"),
        "wood" => new Color("5d8a4e"),
        _ => new Color("8a8a86"),
    };

    // ---- chrome ---------------------------------------------------------------------------------
    private StyleBoxFlat PanelStyle(float alpha = 0.96f)
    {
        var sb = new StyleBoxFlat
        {
            BgColor = new Color(Parchment, alpha),
            BorderColor = Gold,
            ShadowColor = new Color(0, 0, 0, 0.4f),
            ShadowSize = 10,
        };
        sb.SetBorderWidthAll(2);
        sb.SetCornerRadiusAll(7);
        sb.SetContentMarginAll(14);
        return sb;
    }

    private Label Lab(string text, Font font, int size, Color col, bool wrap = false)
    {
        var l = new Label { Text = text };
        l.AddThemeFontOverride("font", font);
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", col);
        if (wrap) l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return l;
    }

    private void BuildChrome()
    {
        var r = _world.Regions[_regionId];
        string? holderId = r.ControllingFactionId;
        Faction? holder = holderId != null && _world.Factions.TryGetValue(holderId, out var hh) ? hh : null;
        var sites = _world.Sites.ForRegion(_regionId);
        string condition = r.InFamine ? "Famine grips the fields"
            : r.InBoom ? "A season of plenty"
            : "Steady harvests";

        // A) Year plate — top-left
        var plate = new PanelContainer { Position = new Vector2(20, 18) };
        plate.AddThemeStyleboxOverride("panel", PanelStyle());
        var pv = new VBoxContainer();
        pv.AddChild(Lab("LIVING MYTH", _sc, 13, Gold));
        pv.AddChild(Lab($"Year {_world.Year}", _serif, 32, Ink));
        pv.AddChild(Lab($"{_world.LivingCount} souls · {_world.Chronicle.Events.Count} tales", _serif, 14, InkSoft));
        plate.AddChild(pv);
        AddChild(plate);

        // B) Title — top-center
        var title = new VBoxContainer
        {
            Position = new Vector2(0, 22),
            Size = new Vector2(GetViewportRect().Size.X, 70),
        };
        title.Alignment = BoxContainer.AlignmentMode.Center;
        var tn = Lab(r.Name, _serif, 34, new Color("efe2bf"));
        tn.HorizontalAlignment = HorizontalAlignment.Center;
        tn.Size = new Vector2(GetViewportRect().Size.X, 40);
        string realm = holder != null ? $"realm of {holder.Name}" : "unclaimed country";
        var ts = Lab($"{Cap(r.TerrainType)} country · {realm}", _sc, 15, new Color("d8c79a"));
        ts.HorizontalAlignment = HorizontalAlignment.Center;
        ts.Size = new Vector2(GetViewportRect().Size.X, 22);
        title.AddChild(tn);
        title.AddChild(ts);
        AddChild(title);

        // C) Inspector card — left
        var card = new PanelContainer { Position = new Vector2(20, 104), CustomMinimumSize = new Vector2(312, 0) };
        card.AddThemeStyleboxOverride("panel", PanelStyle());
        var cv = new VBoxContainer();
        cv.AddThemeConstantOverride("separation", 7);
        var seat = sites.FirstOrDefault(s => s.IsSeat);
        cv.AddChild(Lab(r.Name, _serif, 24, Ink));
        cv.AddChild(Lab((seat != null ? SiteIndex.TypeLabel(seat.Type) + " seat" : "no seat yet") + " · " + Cap(r.TerrainType), _sc, 13, InkSoft));
        cv.AddChild(MakeRule());

        var holderRow = new HBoxContainer();
        holderRow.AddThemeConstantOverride("separation", 8);
        var chip = new ColorRect { Color = FactionTint(holderId), CustomMinimumSize = new Vector2(16, 16) };
        holderRow.AddChild(chip);
        holderRow.AddChild(Lab(holder != null ? $"Held by {holder.Name}" : "Unclaimed wilderland", _serif, 16, Ink));
        cv.AddChild(holderRow);
        cv.AddChild(Lab(condition, _serif, 15, r.InFamine ? Ember : InkSoft));
        cv.AddChild(MakeRule());

        cv.AddChild(Lab("KNOWN PLACES", _sc, 12, Gold));
        if (sites.Count == 0)
            cv.AddChild(Lab("·  no places yet named — an unwritten country", _serif, 14, InkSoft));
        foreach (var s in sites.Take(7))
            cv.AddChild(Lab($"·  {s.Name} — {SiteIndex.TypeLabel(s.Type)}", _serif, 14, Ink));
        cv.AddChild(MakeRule());

        string flavor = sites.Count == 0
            ? $"{r.Name} is a {r.TerrainType} country the record has not yet marked with a single place."
            : $"{r.Name} is a {r.TerrainType} country of {sites.Count} known places"
              + (holder != null ? $", held by {holder.Name}." : ", as yet unclaimed.");
        var fl = Lab(flavor, _serif, 14, InkSoft, wrap: true);
        fl.CustomMinimumSize = new Vector2(284, 0);
        cv.AddChild(fl);
        card.AddChild(cv);
        AddChild(card);

        // D) The Saga (here) — right
        var saga = new PanelContainer
        {
            Position = new Vector2(GetViewportRect().Size.X - 312, 104),
            CustomMinimumSize = new Vector2(292, 0),
        };
        saga.AddThemeStyleboxOverride("panel", PanelStyle());
        var sv = new VBoxContainer();
        sv.AddThemeConstantOverride("separation", 6);
        sv.AddChild(Lab("THE SAGA — HERE", _sc, 13, Gold));
        sv.AddChild(MakeRule());
        var siteIds = sites.Select(s => s.Id).ToHashSet();
        var here = _world.Chronicle.Events
            .Where(e => e.RegionId == _regionId || (e.SiteId is int sid && siteIds.Contains(sid)))
            .OrderByDescending(e => e.Year).Take(8).ToList();
        if (here.Count == 0) sv.AddChild(Lab("No tales yet sung here.", _serif, 14, InkSoft));
        foreach (var e in here)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            row.AddChild(new ColorRect { Color = EventColor(e.Type), CustomMinimumSize = new Vector2(10, 10) });
            var col = new VBoxContainer();
            col.AddChild(Lab($"{e.Type.ToUpperInvariant().Replace('_', ' ')}  ·  yr {e.Year}", _sc, 11, InkSoft));
            var txt = Lab(Trunc(e.Text, 60), _serif, 14, Ink, wrap: true);
            txt.CustomMinimumSize = new Vector2(238, 0);
            col.AddChild(txt);
            row.AddChild(col);
            sv.AddChild(row);
        }
        saga.AddChild(sv);
        AddChild(saga);

        // E) Legend — bottom-left
        var legend = new PanelContainer { Position = new Vector2(20, GetViewportRect().Size.Y - 132) };
        legend.AddThemeStyleboxOverride("panel", PanelStyle(0.92f));
        var lv = new VBoxContainer();
        foreach (var (name, c) in new (string, Color)[]
                 { ("Your lands", new Color("5d8a4e")), ("Allied", new Color("4f8f89")),
                   ("Neutral", new Color("8a8a86")), ("Contested", Ember) })
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            row.AddChild(new ColorRect { Color = c, CustomMinimumSize = new Vector2(13, 13) });
            row.AddChild(Lab(name, _serif, 13, Ink));
            lv.AddChild(row);
        }
        legend.AddChild(lv);
        AddChild(legend);

        // F) Honest bottom bar — this is a READ-ONLY view. No fake god-hand tools live here: the
        //    real verbs (Curse/Bless/Protect/Doom/Omen/Forest/Spring) stay in the atlas inspector,
        //    where they journal to the save. The brass plate keeps the North Star language, honest.
        var bar = new PanelContainer
        {
            Position = new Vector2(GetViewportRect().Size.X / 2 - 220, GetViewportRect().Size.Y - 60),
        };
        var barSb = PanelStyle(0.95f);
        barSb.BgColor = new Color("2a2117", 0.95f);
        barSb.BorderColor = Gold;
        bar.AddThemeStyleboxOverride("panel", barSb);
        var bh = new HBoxContainer();
        bh.AddThemeConstantOverride("separation", 10);
        bh.AddChild(Lab("◆", _serif, 14, Gold));
        bh.AddChild(Lab("REGION DIORAMA · READ-ONLY CHRONICLE VIEW · ART IN PROGRESS", _sc, 12, new Color("c8b48a")));
        bar.AddChild(bh);
        AddChild(bar);

        // G) Back to the atlas — a real button (Esc also returns)
        var back = BrassButton("← Back to the Atlas", Close);
        back.Position = new Vector2(GetViewportRect().Size.X - 218, GetViewportRect().Size.Y - 58);
        AddChild(back);
    }

    private Control MakeRule()
    {
        var r = new ColorRect { Color = new Color(Gold, 0.45f), CustomMinimumSize = new Vector2(0, 1) };
        return r;
    }

    private Button BrassButton(string text, System.Action onPressed)
    {
        var b = new Button { Text = text };
        b.AddThemeFontOverride("font", _sc);
        b.AddThemeFontSizeOverride("font_size", 14);
        var face = new StyleBoxFlat { BgColor = new Color("4a3a22"), BorderColor = Gold };
        face.SetBorderWidthAll(1); face.SetCornerRadiusAll(5); face.SetContentMarginAll(9);
        var hover = (StyleBoxFlat)face.Duplicate(); hover.BgColor = new Color("5d4a2b");
        var pressed = (StyleBoxFlat)face.Duplicate(); pressed.BgColor = new Color("3a2c19");
        b.AddThemeStyleboxOverride("normal", face);
        b.AddThemeStyleboxOverride("hover", hover);
        b.AddThemeStyleboxOverride("pressed", pressed);
        b.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        foreach (var c in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
            b.AddThemeColorOverride(c, new Color("e7d4a8"));
        b.Pressed += () => onPressed();
        return b;
    }

    private void BuildPost()
    {
        var layer = new CanvasLayer { Layer = 12 };
        var rect = new ColorRect { Color = Colors.White, MouseFilter = MouseFilterEnum.Ignore };
        rect.SetAnchorsPreset(LayoutPreset.FullRect);
        var shader = GD.Load<Shader>("res://shaders/parchment_post.gdshader");
        rect.Material = new ShaderMaterial { Shader = shader };
        layer.AddChild(rect);
        AddChild(layer);
    }

    private async System.Threading.Tasks.Task SelfShot(string dir)
    {
        await ToSignal(GetTree().CreateTimer(0.9), SceneTreeTimer.SignalName.Timeout);
        var img = GetViewport().GetTexture().GetImage();
        System.IO.Directory.CreateDirectory(dir);
        string name = OS.GetEnvironment("LM_DIORAMA_NAME");
        img.SavePng(System.IO.Path.Combine(dir, (name != "" ? name : "diorama_prototype") + ".png"));
        GetTree().Quit();
    }

    private static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);
    private static string Trunc(string s, int n) => s.Length <= n ? s : s.Substring(0, n - 1).TrimEnd() + "…";

    public static Color EventColor(string type) => type switch
    {
        "birth" => new Color("6f9e54"),
        "death" or "murder" => new Color("8a8a86"),
        "war_declared" or "battle" or "peace" => new Color("b0432e"),
        "famine" or "famine_end" or "boom" => new Color("b8862e"),
        "founding" or "abandonment" => new Color("c9973f"),
        _ => new Color("9a8b6a"),
    };
}

// The custom-draw surface: an ISOMETRIC ground plane (tilted diamonds) + the Blender diorama
// miniatures billboarded and depth-sorted on top + parchment label callouts. The oblique
// projection is what makes it read as a diorama rather than a top-down texture mat: the already-
// angled sprites gain volume, and you see roofs, walls, and tree silhouettes against the tilt.
public partial class DioramaCanvas : Control
{
    public DioramaView View = null!;

    private int _minCx, _minCy, _bw, _bh;
    private float _tw, _th, _ox, _oy;   // tile width/height (iso) + screen origin

    private static Color TerrainColor(SurfaceTerrain t) => t switch
    {
        SurfaceTerrain.Ocean => new Color("2c5159"),
        SurfaceTerrain.Shallows => new Color("3f6b73"),
        SurfaceTerrain.River or SurfaceTerrain.Lake => new Color("4f8f89"),
        SurfaceTerrain.Coast => new Color("9a8a5e"),
        SurfaceTerrain.Plains => new Color("8c8447"),
        SurfaceTerrain.Forest => new Color("50632f"),
        SurfaceTerrain.Highland => new Color("6f6f5a"),
        SurfaceTerrain.Wetland => new Color("5f7048"),
        _ => new Color("8c8447"),
    };

    public override void _Ready()
    {
        var w = View.World;
        // centre a fixed zoom window on the region's heart (centroid of its cells) — framing the
        // full bbox of an irregular region leaves dead space; the North Star zooms into the core.
        long sx = 0, sy = 0, cnt = 0;
        for (int y = 0; y < WorldSurface.Size; y++)
            for (int x = 0; x < WorldSurface.Size; x++)
                if (w.Surface.RegionAt(x, y) == View.RegionId) { sx += x; sy += y; cnt++; }
        int ccx = cnt > 0 ? (int)(sx / cnt) : 48, ccy = cnt > 0 ? (int)(sy / cnt) : 48;
        const int half = 13;
        _bw = _bh = 2 * half + 1;
        _minCx = Math.Clamp(ccx - half, 0, WorldSurface.Size - _bw);
        _minCy = Math.Clamp(ccy - half, 0, WorldSurface.Size - _bh);
        Resized += QueueRedraw;
    }

    // iso projection: cell (cx,cy) → screen. The grid is rotated 45° and foreshortened in Y.
    private Vector2 Iso(float cx, float cy)
    {
        float a = cx - _minCx, b = cy - _minCy;
        return new(_ox + (a - b) * _tw * 0.5f, _oy + (a + b) * _th * 0.5f);
    }

    private static readonly Vector2[] GroundUV = { new(0, 0), new(1, 0), new(1, 1), new(0, 1) };

    private static bool IsWater(SurfaceTerrain t) =>
        t is SurfaceTerrain.Ocean or SurfaceTerrain.Shallows or SurfaceTerrain.River or SurfaceTerrain.Lake;

    // Which painterly ground swatch backs a terrain cell. Only the proof-kit terrains (coast/
    // forest/highland + water) get a tile; everything else falls back to the flat colour draw.
    private static string? GroundKey(SurfaceTerrain t) => t switch
    {
        SurfaceTerrain.Forest => "ground_forest",
        SurfaceTerrain.Coast => "ground_coast",
        SurfaceTerrain.Highland => "ground_highland",
        SurfaceTerrain.Ocean or SurfaceTerrain.Shallows or SurfaceTerrain.River or SurfaceTerrain.Lake => "ground_water",
        _ => null,
    };

    public override void _Draw()
    {
        var w = View.World;
        // LM_DIORAMA_RAW=1 forces the pre-art-pipeline render (flat ground, no foam/roads/pulses)
        // so the same region can be captured before vs after from one deterministic build.
        bool raw = OS.GetEnvironment("LM_DIORAMA_RAW") != "";
        var size = Size;
        float span = _bw + _bh;
        _tw = Math.Min(size.X * 1.28f * 2f / span, size.Y * 1.0f * 2f / (span * 0.55f));
        _th = _tw * 0.55f;
        _ox = size.X / 2f - (_bw - _bh) * 0.5f * _tw * 0.5f;
        _oy = size.Y / 2f - (_bw + _bh - 2) * 0.5f * _th * 0.5f;

        // settlement clearings: keep foliage off the sites so roofs/stones read in open ground
        var siteCells = w.Sites.ForRegion(View.RegionId)
            .Select(s => new Vector2(s.Nx * WorldSurface.Size, s.Ny * WorldSurface.Size)).ToList();
        bool NearSite(float cx, float cy)
        {
            foreach (var sc in siteCells)
                if (Math.Abs(cx - sc.X) < 2.6f && Math.Abs(cy - sc.Y) < 2.6f) return true;
            return false;
        }
        // low-frequency clearing mask — opens ~quarter of the canopy so earth/paths/water show
        bool Clearing(int cx, int cy) => Hash(cx / 3, cy / 3, Seed: 9) % 100 < 26;

        // 1) ground: tilted iso diamonds, one per cell, coloured by terrain with a NW raking key
        //    light over the elevation field (carves relief), region cells bright, neighbours dimmed.
        for (int b = 0; b < _bh; b++)
            for (int a = 0; a < _bw; a++)
            {
                int cx = _minCx + a, cy = _minCy + b;
                var t = w.Surface.TerrainAt(cx, cy);
                bool inRegion = w.Surface.RegionAt(cx, cy) == View.RegionId;
                uint gh = Hash(cx, cy, Seed: 3);
                float j = ((gh & 0xff) / 255f - 0.5f) * 0.10f;
                int cl = Math.Max(0, cx - 1), cr = Math.Min(WorldSurface.Size - 1, cx + 1);
                int ct = Math.Max(0, cy - 1), cb = Math.Min(WorldSurface.Size - 1, cy + 1);
                float relief = (w.Surface.ElevationAt(cl, cy) - w.Surface.ElevationAt(cr, cy)) * 0.6f
                             + (w.Surface.ElevationAt(cx, ct) - w.Surface.ElevationAt(cx, cb)) * 0.4f;
                float shade = Math.Clamp(relief * 2.2f, -0.16f, 0.20f);
                var col = TerrainColor(t);
                col = new Color(Math.Clamp(col.R + j + shade, 0, 1), Math.Clamp(col.G + j + shade, 0, 1), Math.Clamp(col.B + j + shade * 0.9f, 0, 1));
                if (!inRegion) col = col.Darkened(0.18f).Lerp(new Color("2c3138"), 0.2f);
                var c0 = Iso(cx, cy); var c1 = Iso(cx + 1, cy);
                var c2 = Iso(cx + 1, cy + 1); var c3 = Iso(cx, cy + 1);
                var ctr = (c0 + c1 + c2 + c3) * 0.25f;
                Vector2 Ex(Vector2 p) => ctr + (p - ctr) * 1.04f;
                var poly = new[] { Ex(c0), Ex(c1), Ex(c2), Ex(c3) };
                // textured ground: the painterly Blender/Krita swatch carries the colour; the
                // relief/jitter rides as a brightness modulate so the NW raking light still reads.
                string? gkey = raw ? null : GroundKey(t);
                var gtex = gkey != null ? View.Tex(gkey) : null;
                if (gtex != null)
                {
                    float v = Math.Clamp(0.94f + shade + j, 0.5f, 1f);
                    var mod = inRegion ? new Color(v, v, Math.Clamp(v - 0.02f, 0f, 1f))
                                       : new Color(v * 0.66f, v * 0.68f, v * 0.72f);
                    DrawColoredPolygon(poly, mod, GroundUV, gtex);
                }
                else DrawColoredPolygon(poly, col);
                // shore foam: a pale fringe on every water-cell edge that meets land (bounds-safe —
                // TerrainAt does not clamp, so an unguarded edge neighbour would read OOB)
                if (!raw && IsWater(t))
                    foreach (var (dx, dy, p, q) in new (int, int, Vector2, Vector2)[]
                             { (-1, 0, c0, c3), (1, 0, c1, c2), (0, -1, c0, c1), (0, 1, c3, c2) })
                    {
                        int nx = cx + dx, ny = cy + dy;
                        if (nx >= 0 && nx < WorldSurface.Size && ny >= 0 && ny < WorldSurface.Size
                            && !IsWater(w.Surface.TerrainAt(nx, ny)))
                            DrawLine(Ex(p), Ex(q), new Color("dfe7d6", 0.62f), 2f);
                    }
            }

        var sprites = new List<(Texture2D tex, Rect2 rect, Color mod, float sortY)>();

        void Place(string key, float cx, float cy, float tilesTall, Color? mod = null)
        {
            var tex = View.Tex(key);
            if (tex == null) return;
            var p = Iso(cx + 0.5f, cy + 0.5f);
            float sz = tilesTall * _tw;
            var rect = new Rect2(p.X - sz / 2f, p.Y - sz * 0.82f, sz, sz);
            sprites.Add((tex, rect, mod ?? Colors.White, p.Y));
        }

        // 2) scatter foliage/terrain over every land cell in view — region cells bright, the
        //    neighbouring lands dimmed, so the diorama reads edge-to-edge like the North Star
        for (int cy = _minCy; cy < _minCy + _bh; cy++)
            for (int cx = _minCx; cx < _minCx + _bw; cx++)
            {
                var t = w.Surface.TerrainAt(cx, cy);
                bool inRegion = w.Surface.RegionAt(cx, cy) == View.RegionId;
                Color mod = inRegion ? Colors.White : new Color(0.74f, 0.76f, 0.78f);
                if (NearSite(cx, cy)) continue;        // settlement clearing — buildings get open ground
                bool clearing = Clearing(cx, cy);      // open earth/path/water shows through
                // up to two scattered features per cell, each at a hashed sub-cell offset and a
                // hashed tree variant — breaks the regular-grid / repeated-stamp read
                for (int slot = 0; slot < 2; slot++)
                {
                    uint h = Hash(cx * 2 + slot, cy, Seed: 7);
                    float roll = (h & 0xffff) / 65535f;
                    float jx = ((int)((h >> 16) & 0xff) / 255f - 0.5f) * 0.9f;
                    float jy = ((int)((h >> 24) & 0xff) / 255f - 0.5f) * 0.9f;
                    int variant = (int)((h >> 8) % 3) + 1;
                    string broad = $"tree_broadleaf_{variant}", coni = $"tree_conifer_{variant}";
                    string? key = null; float tall = 2.6f;
                    switch (t)
                    {
                        case SurfaceTerrain.Forest:
                            if (clearing) { if (slot == 0 && roll < 0.25f) { key = "rocks"; tall = 1.5f; } }
                            else if (slot == 0 || roll < 0.5f) { key = (h & 1) == 0 ? broad : coni; tall = 2.9f; }
                            break;
                        case SurfaceTerrain.Highland:
                            if (slot == 0) { if (roll < 0.4f) { key = "rocks"; tall = 1.9f; } else if (!clearing) { key = coni; tall = 2.6f; } }
                            else if (roll < 0.35f && !clearing) { key = coni; tall = 2.3f; }
                            break;
                        case SurfaceTerrain.Plains:
                            if (clearing) break;
                            if (slot == 0) { key = roll < 0.5f ? broad : (roll < 0.74f ? coni : "rocks"); tall = roll < 0.74f ? 2.3f : 1.5f; }
                            else if (roll < 0.4f) { key = broad; tall = 2.1f; }
                            break;
                        case SurfaceTerrain.Coast:
                            if (slot == 0 && roll < 0.55f && !clearing) { key = broad; tall = 2.2f; }
                            break;
                        case SurfaceTerrain.Wetland:
                            if (slot == 0 && roll < 0.5f && !clearing) { key = broad; tall = 2.3f; }
                            break;
                    }
                    if (key != null) Place(key, cx + jx, cy + jy, tall * (0.72f + roll * 0.6f), mod);
                }
            }

        // 3) sites: type-keyed buildings + a banner at the seat
        var sites = w.Sites.ForRegion(View.RegionId);
        var labels = new List<(string name, string type, Vector2 feet, Color dot)>();
        string? holderId = w.Regions[View.RegionId].ControllingFactionId;
        var holderTint = DioramaView.FactionTint(holderId);
        foreach (var s in sites)
        {
            float cx = s.Nx * WorldSurface.Size - 0.5f;
            float cy = s.Ny * WorldSurface.Size - 0.5f;
            (string key, float tall) = s.Type switch
            {
                SiteType.MarketVillage => ("house_b", 2.4f),
                SiteType.HillFort => ("keep", 3.0f),
                SiteType.WatchPost => ("watchtower", 2.6f),
                SiteType.SacredGrove => ("standing_stones", 2.2f),
                SiteType.OldBarrow => ("standing_stones", 2.2f),
                SiteType.CairnField => ("standing_stones", 2.2f),
                SiteType.Shrine => ("shrine", 1.9f),
                SiteType.Farmstead => ("field", 2.4f),
                SiteType.FishingDock => ("dock", 2.6f),
                SiteType.WildernessCamp => ("house_a", 1.8f),
                _ => ("house_a", 1.8f),
            };
            if (s.Type == SiteType.MarketVillage)
            {
                Place("house_a", cx - 1.0f, cy + 0.6f, 1.7f);
                Place("house_a", cx + 1.0f, cy + 0.5f, 1.6f);
            }
            if (s.Type == SiteType.Farmstead) Place("house_a", cx + 1.2f, cy - 0.4f, 1.6f);
            Place(key, cx, cy, tall);
            if (s.IsSeat && holderId != null) Place("banner", cx + 0.9f, cy - 0.9f, 2.4f, holderTint);
            labels.Add((s.Name, SiteIndex.TypeLabel(s.Type), Iso(cx + 0.5f, cy + 0.5f), holderTint));
        }

        // 3b) roads — warm dirt paths from the seat out to every other known place
        Vector2 Feet(float nx, float ny) => Iso(nx * WorldSurface.Size, ny * WorldSurface.Size);
        if (!raw && holderId != null && sites.Count > 1)
        {
            var seat = sites.FirstOrDefault(s => s.IsSeat) ?? sites[0];
            var sf = Feet(seat.Nx, seat.Ny);
            foreach (var s in sites)
            {
                if (s.Id == seat.Id) continue;
                var f = Feet(s.Nx, s.Ny);
                DrawLine(sf, f, new Color("382a17", 0.5f), _tw * 0.30f);
                DrawLine(sf, f, new Color("8a6f43", 0.72f), _tw * 0.16f);
            }
        }

        // 3c) pulse markers — the most recent site-anchored tales get an ember glyph, tinted to
        //     the event class (war-red / harvest-ochre / founding-gold); read-only, no new facts
        if (!raw)
            foreach (var e in w.Chronicle.Events
                         .Where(e => e.SiteId is int sid && sites.Any(x => x.Id == sid))
                         .OrderByDescending(e => e.Year).Take(3))
            {
                var s = sites.First(x => x.Id == e.SiteId!.Value);
                Place("pulse_marker", s.Nx * WorldSurface.Size - 0.5f, s.Ny * WorldSurface.Size - 0.5f,
                      1.5f, DioramaView.EventColor(e.Type));
            }

        // 4) painter's algorithm — far (low Y) first
        foreach (var sp in sprites.OrderBy(s => s.sortY))
            DrawTextureRect(sp.tex, sp.rect, false, sp.mod);

        // 5) parchment label callouts on top, with collision avoidance for dense clusters.
        //    Pills are clamped clear of the title/year band (top) and the bottom bar, then nudged
        //    downward off one another so a tight knot of sites stays legible instead of stacking.
        var pill = new StyleBoxFlat { BgColor = new Color("f2e5c2", 0.95f), BorderColor = new Color("c9973f") };
        pill.SetBorderWidthAll(1); pill.SetCornerRadiusAll(5); pill.SetContentMarginAll(5);
        const float topBand = 96f;          // year plate + title live above this
        const float bottomBand = 72f;       // legend + bottom bar live below this
        bool avoid = OS.GetEnvironment("LM_DIORAMA_NOAVOID") == "";   // capture-only "before" toggle
        var placed = new List<Rect2>();
        var pills = labels.Select(l =>
        {
            var nameSize = View.Serif.GetStringSize(l.name, HorizontalAlignment.Left, -1, 15);
            float pw = nameSize.X + 30, ph = 34;
            var anchor = new Vector2(l.feet.X - pw / 2f, l.feet.Y - _tw * 2.6f);
            anchor.X = Math.Clamp(anchor.X, 6, Math.Max(6, Size.X - pw - 6));
            anchor.Y = Math.Clamp(anchor.Y, topBand, Math.Max(topBand, Size.Y - bottomBand - ph));
            return (l.name, l.type, l.feet, l.dot, pw, ph, anchor);
        }).OrderBy(p => p.anchor.Y).ThenBy(p => p.anchor.X).ToList();

        foreach (var (name, type, feet, dot, pw, ph, anchor0) in pills)
        {
            var anchor = anchor0;
            var rect = new Rect2(anchor, new Vector2(pw, ph));
            for (int guard = 0; avoid && guard < 40 && placed.Any(r => r.Intersects(rect.Grow(4f))); guard++)
            {
                anchor.Y += ph + 5f;
                if (anchor.Y > Size.Y - bottomBand - ph) { anchor.Y = anchor0.Y; anchor.X += pw * 0.5f; }
                rect = new Rect2(anchor, new Vector2(pw, ph));
            }
            placed.Add(rect);
            DrawLine(new Vector2(feet.X, feet.Y - _tw * 0.5f),
                     new Vector2(anchor.X + pw / 2f, anchor.Y + ph), new Color("3a2c19", 0.5f), 1.5f);
            DrawStyleBox(pill, rect);
            DrawCircle(anchor + new Vector2(12, ph / 2f), 5, dot);
            DrawString(View.Serif, anchor + new Vector2(22, 15), name, HorizontalAlignment.Left, -1, 15, new Color("3a2c19"));
            DrawString(View.Sc, anchor + new Vector2(22, 29), type, HorizontalAlignment.Left, -1, 10, new Color("6f5b3e"));
        }
    }

    private static uint Hash(int a, int b, int Seed)
    {
        unchecked
        {
            uint h = 2166136261u;
            h = (h ^ (uint)Seed) * 16777619u;
            h = (h ^ (uint)a) * 16777619u;
            h = (h ^ (uint)b) * 16777619u;
            h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
            return h;
        }
    }
}
