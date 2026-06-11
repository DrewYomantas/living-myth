using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using LivingMyth.Sim;

// The map: a procedural island with named, terrain-typed regions. Each region is a colored
// territory owned by the people who hold it (neutral grey = wilderness); each living person is
// a dot scattered within one of their faction's regions. Pure rendering — it reads the sim's
// spatial data (World.Regions, Faction control), never mutates it. Click a dot to inspect that
// person; click a territory to inspect who holds it (or that it's unclaimed); hover for its name.
public partial class MapView : Control
{
    public World? World;
    public HashSet<int>? Marked;            // followed bloodline — ringed cyan
    public HashSet<int>? Souls;             // souls followed as individuals — ringed warm gold (the player's mark)
    public Action<int>? PersonPicked;
    public Action<int, Vector2>? SoulPicked;   // a followed soul's marker clicked — Main opens the glimpse
    public Action<string>? FactionPicked;
    public Action<int>? RegionPicked;       // region id — Main decides faction vs. unclaimed

    private readonly List<(Vector2 pos, float r, int id)> _dots = new();
    private readonly List<(Vector2 pos, float r, int id)> _regionHits = new();
    private List<Vector2>? _islandNorm;     // island outline in normalized [0,1] space, built once
    private List<Vector2>? _shallowsNorm;   // island outline scaled out slightly — the shallows rim
    private int _hoverRegion = -1;

    // Transient gold rings on regions where a notable event just landed — pure rendering, aged
    // off real time. region id -> seconds remaining.
    private readonly Dictionary<int, float> _regionPulses = new();
    private const float PulseDuration = 1.2f;

    // Followed-soul presence (the divine bookmark): a breathing halo so a watched life reads
    // as alive, plus a brief flare when a newly shown saga event names the soul. Pure
    // rendering over the existing deterministic scatter — no new position precision implied.
    private readonly Dictionary<int, float> _soulPulses = new();
    private const float SoulPulseDuration = 1.4f;
    private float _breath;
    private readonly List<(Vector2 pos, int id)> _soulScreen = new();   // souls drawn this frame, for name tags

    // Place memory (V1): real anchored events leave subtle marks on the land. Only events that
    // truly carry Event.RegionId may mark — Main classifies the stream and feeds marks in here.
    // Capped per region (the oldest yields); alpha ages by sim year, deterministically — no RNG.
    public enum MarkKind { FoundingStone, WarScar, AbandonCairn, CultureRibbon }
    private readonly Dictionary<int, List<(MarkKind kind, int year, int eventId)>> _placeMarks = new();
    private const int MarksPerRegion = 4;
    private static readonly float[] MarkAngles = { 3.6f, 5.5f, 1.1f, 2.4f };   // fixed slots ringing the centre

    public void AddPlaceMark(int regionId, MarkKind kind, int year, int eventId)
    {
        if (!_placeMarks.TryGetValue(regionId, out var list)) { list = new(); _placeMarks[regionId] = list; }
        list.Add((kind, year, eventId));
        if (list.Count > MarksPerRegion) list.RemoveAt(0);
    }

    public IReadOnlyList<(MarkKind kind, int year, int eventId)> MarksFor(int regionId)
        => _placeMarks.TryGetValue(regionId, out var list) ? list
           : Array.Empty<(MarkKind kind, int year, int eventId)>();

    // Camera: zoom (1 = fit-to-view) + pan (screen-space offset), folded into the draw transform
    // so hit-testing — which records post-transform positions during _Draw — stays correct for free.
    private float _zoom = 1f;
    private Vector2 _pan = Vector2.Zero;
    private bool _leftDown;
    private bool _dragging;
    private Vector2 _dragStart;
    private const float MinZoom = 1f, MaxZoom = 5f;
    private const float DragThreshold = 4f;

    // Drama camera: gently ease toward a pulsing region, but never fight the player. Any manual
    // pan/zoom sets a cooldown that suppresses the auto-ease so we don't yank the view away.
    public bool CameraFollow = true;
    private int _easeTargetRegion = -1;
    private float _manualCamCooldown;
    private const float FollowZoom = 1.9f;
    private const float ManualCamCooldownSecs = 4f;

    private const float RegionRadiusNorm = 0.072f;
    private const float Pad = 18f;
    private float MapSide() => Mathf.Min(Size.X, Size.Y) - Pad * 2f;

    // Old-world palette (V2 handoff, warmed toward the Batch 1 atlas references): muted
    // teal-slate water with a shallows rim, dry-grass-warmed land, muted banner colors.
    // Faction color is a cloth/banner accent — territory paint stays restrained.
    private static readonly Color Sea = new("22424d");
    private static readonly Color Shallows = new("2e5560");
    private static readonly Color Land = new("474c31");
    private static readonly Color Coast = new("5d6242");
    private static readonly Color Neutral = new("6f6a58");          // unclaimed wilderness
    private static readonly Dictionary<string, Color> FactionColors = new()
    {
        ["highland"] = new Color("6b7a99"),
        ["shore"] = new Color("4f8f89"),
        ["wood"] = new Color("5d8a4e"),
    };

    public string? SelectedFactionId;   // set by Main while a faction inspector is open — label emphasis only
    public int SelectedRegionId = -1;   // set by Main while the Region Lens is open — gold lens ring only

    // Place-seed marker palette: timber/thatch/stone/dirt, per DESIGN.md ("ancient, not generic").
    private static readonly Color Timber = new("6e5639");
    private static readonly Color RoofDark = new("55432c");
    private static readonly Color Thatch = new("a3854f");
    private static readonly Color StoneMark = new("90908a");
    private static readonly Color TreeGreen = new("55703f");
    private static readonly Color TrunkBrown = new("4a3a26");
    private static readonly Color RoadDirt = new("9c7c4a");
    private static readonly Color FieldGold = new("8f7c43");
    private static readonly Color TentCloth = new("7d6b50");
    private static readonly Color MarkInk = new(0.16f, 0.12f, 0.07f);
    private const float CaptionZoom = 2.0f;   // place tags appear at/above this zoom

    // Soft dark tags behind map text — readable over any terrain without a full parchment panel.
    private static readonly StyleBoxFlat LabelTag = MakeTag(new Color(0.12f, 0.09f, 0.05f, 0.42f), null);
    private static readonly StyleBoxFlat LabelTagSelected = MakeTag(new Color(0.12f, 0.09f, 0.05f, 0.62f), Ui.LensGold);

    // Parchment place tags — the atlas pill under each place marker (region name + place kind).
    private static readonly StyleBoxFlat PlaceTag = Ui.ParchmentTag();
    private static readonly StyleBoxFlat PlaceTagSelected = Ui.ParchmentTag(selected: true);
    // Watched-soul name tag — gold-bordered, the player's mark made legible at any zoom.
    private static readonly StyleBoxFlat SoulNameTag = Ui.ParchmentTag(selected: true);
    private static StyleBoxFlat MakeTag(Color bg, Color? border)
    {
        var sb = new StyleBoxFlat { BgColor = bg };
        sb.SetCornerRadiusAll(6);
        if (border is Color bc) { sb.BorderColor = bc; sb.SetBorderWidthAll(1); }
        return sb;
    }

    public override void _Ready() => MouseFilter = MouseFilterEnum.Stop;

    public void PulseRegion(int regionId)
    {
        _regionPulses[regionId] = PulseDuration;
        if (CameraFollow && _manualCamCooldown <= 0f) _easeTargetRegion = regionId;
    }

    public void PulseSoul(int personId) => _soulPulses[personId] = SoulPulseDuration;

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (_manualCamCooldown > 0f) _manualCamCooldown -= dt;
        _breath = Mathf.Wrap(_breath + dt, 0f, Mathf.Tau);

        foreach (var id in _regionPulses.Keys.ToList())
        {
            float v = _regionPulses[id] - dt;
            if (v <= 0f) _regionPulses.Remove(id);
            else _regionPulses[id] = v;
        }
        foreach (var id in _soulPulses.Keys.ToList())
        {
            float v = _soulPulses[id] - dt;
            if (v <= 0f) _soulPulses.Remove(id);
            else _soulPulses[id] = v;
        }

        // Drama camera: lean toward the pulsing region while its pulse lives, unless the player
        // just took manual control. Eases zoom + pan together; clamped to map bounds.
        if (CameraFollow && _manualCamCooldown <= 0f && _easeTargetRegion >= 0 && World is not null
            && _easeTargetRegion < World.Regions.Count && _regionPulses.ContainsKey(_easeTargetRegion))
        {
            float side = MapSide();
            var center = Size / 2f;
            var origin = new Vector2((Size.X - side) / 2f, (Size.Y - side) / 2f);
            _zoom = Mathf.Lerp(_zoom, Mathf.Max(_zoom, FollowZoom), dt * 2.5f);
            var r = World.Regions[_easeTargetRegion];
            var b = origin + new Vector2(r.X, r.Y) * side;
            _pan = _pan.Lerp(-(b - center) * _zoom, dt * 2.5f);
            ClampPan();
        }
        else _easeTargetRegion = -1;

        // Main redraws the map every frame; no QueueRedraw needed here.
    }

    // ----- camera control (called by buttons in Main and by wheel/drag below) -----

    public void ZoomBy(float factor) => ZoomAt(_zoom * factor, Size / 2f);

    public void ResetCamera() { _zoom = 1f; _pan = Vector2.Zero; MarkManual(); QueueRedraw(); }

    private void ZoomAt(float newZoom, Vector2 anchor)
    {
        newZoom = Mathf.Clamp(newZoom, MinZoom, MaxZoom);
        var center = Size / 2f;
        var b = (anchor - center - _pan) / _zoom + center;   // world-base point under the anchor
        _zoom = newZoom;
        _pan = anchor - center - (b - center) * _zoom;        // keep that point under the anchor
        ClampPan();
        MarkManual();
        QueueRedraw();
    }

    private void ClampPan()
    {
        float over = Mathf.Max(0f, MapSide() * (_zoom - 1f) / 2f);   // 0 at fit zoom → forces center
        _pan = new Vector2(Mathf.Clamp(_pan.X, -over, over), Mathf.Clamp(_pan.Y, -over, over));
    }

    private void MarkManual() { _manualCamCooldown = ManualCamCooldownSecs; _easeTargetRegion = -1; }

    private static float Frac(float v) => v - Mathf.Floor(v);

    private Color RegionColor(Region r)
        => r.ControllingFactionId is string fid ? FactionColors.GetValueOrDefault(fid, Neutral) : Neutral;

    // Draw order (fixed): 1 water → 2 shallows rim + land/coast (+ faint adjacency) →
    // 3 territory tint → 4 roads → 5a place-memory marks → 5 place-seed markers → 6 people dots
    // → 7 follow/leader highlights (inside DrawPeople) + region lens ring → 8 labels (faction
    // tags → parchment place tags → watched-soul tags → hover tag) → 9 event pulses.
    // New layers slot into this order deliberately; pulses stay topmost so drama always reads.
    public override void _Draw()
    {
        _dots.Clear();
        _regionHits.Clear();
        _soulScreen.Clear();
        DrawRect(new Rect2(Vector2.Zero, Size), Sea);                       // 1. water
        var font = GetThemeDefaultFont();
        if (World is null || font is null) return;

        // Normalized [0,1] -> screen: fit a centered square so the island never distorts, then
        // apply the camera (zoom about the viewport centre, then pan). Everything drawn through P
        // — and every hit-test position recorded below — shares this transform, so clicks map back.
        float side = MapSide();
        var origin = new Vector2((Size.X - side) / 2f, (Size.Y - side) / 2f);
        var camCenter = Size / 2f;
        Vector2 P(float nx, float ny) => (origin + new Vector2(nx, ny) * side - camCenter) * _zoom + camCenter + _pan;
        float regionR = RegionRadiusNorm * side * _zoom;

        BuildIsland();                                                      // 2. land
        if (_islandNorm is not null)
        {
            if (_shallowsNorm is not null)                                  // shallows rim under the coast
                DrawColoredPolygon(_shallowsNorm.Select(p => P(p.X, p.Y)).ToArray(), Shallows);
            var poly = _islandNorm.Select(p => P(p.X, p.Y)).ToArray();
            DrawColoredPolygon(poly, Land);
            DrawPolyline(poly.Append(poly[0]).ToArray(), Coast, 2f, true);
        }

        // Faint adjacency graph beneath the territories — the connective tissue of the island.
        foreach (var a in World.Regions)
            foreach (var bid in a.AdjacentRegionIds)
                if (bid > a.Id)
                    DrawLine(P(a.X, a.Y), P(World.Regions[bid].X, World.Regions[bid].Y),
                             new Color(1, 1, 1, 0.035f), 1f);

        // 3. territory tint: a banner-cloth hint, deliberately subordinate to markers and labels.
        // The selected people's ring firms up so their lands read while their inspector is open.
        foreach (var r in World.Regions)
        {
            var c = P(r.X, r.Y);
            var col = RegionColor(r);
            bool sel = r.ControllingFactionId is string f && f == SelectedFactionId;
            DrawCircle(c, regionR, col with { A = r.ControllingFactionId is null ? 0.10f : 0.20f });
            DrawArc(c, regionR, 0, Mathf.Tau, 40, col with { A = sel ? 0.85f : 0.40f },
                    _hoverRegion == r.Id ? 3f : (sel ? 2.2f : 1.2f));
            _regionHits.Add((c, regionR, r.Id));
        }

        DrawRoads(P);                                                       // 4. roads
        DrawPlaceMemory(P, regionR);                                        // 5a. place memory
        DrawPlaceSeeds(P, regionR, font);                                   // 5. place markers
        DrawPeople(P, regionR, font);                                       // 6+7. people + highlights

        // 7b. region lens ring — the inspected place, ringed gold beneath the labels.
        if (SelectedRegionId >= 0 && SelectedRegionId < World.Regions.Count)
        {
            var sr = World.Regions[SelectedRegionId];
            DrawArc(P(sr.X, sr.Y), regionR + 3f, 0, Mathf.Tau, 48, Ui.LensGold with { A = 0.85f }, 2.5f);
        }

        var placed = DrawFactionLabels(P, font);                            // 8. labels
        DrawPlaceTags(P, regionR, placed);
        DrawSoulTags(placed);

        if (_hoverRegion >= 0 && _hoverRegion < World.Regions.Count)
            DrawHoverTag(P, regionR, font);

        // 9. event pulses — transient gold rings, always on top so drama reads through labels.
        foreach (var (rid, pulse) in _regionPulses)
        {
            if (rid < 0 || rid >= World.Regions.Count) continue;
            var r = World.Regions[rid];
            float t = pulse / PulseDuration;                   // 1 -> 0 over the pulse's life
            float ring = regionR * (1f + (1f - t) * 0.8f);     // expands outward as it fades
            DrawArc(P(r.X, r.Y), ring, 0, Mathf.Tau, 48, Ui.GoldGlow with { A = t * 0.9f }, 3f);
        }
    }

    // Roads: dirt paths between adjacent regions held by the same people — the only
    // high-confidence links the current data supports without scanning history. Each path's
    // kink comes from a stable pair hash, never the sim's Rng.
    private void DrawRoads(Func<float, float, Vector2> P)
    {
        foreach (var a in World!.Regions)
        {
            if (a.ControllingFactionId is not string fid) continue;
            foreach (var bid in a.AdjacentRegionIds)
            {
                if (bid <= a.Id || World.Regions[bid].ControllingFactionId != fid) continue;
                var b = World.Regions[bid];
                var p1 = P(a.X, a.Y);
                var p2 = P(b.X, b.Y);
                var s = p1.Lerp(p2, 0.12f);     // trimmed ends so paths don't pierce the markers
                var e = p1.Lerp(p2, 0.88f);
                var dir = e - s;
                uint h = PlaceSeeds.Hash(World.Seed, a.Id * 131 + bid, 2);
                var mid = (s + e) / 2f + new Vector2(-dir.Y, dir.X).Normalized()
                          * dir.Length() * (((h & 0xff) / 255f - 0.5f) * 0.22f);
                DrawPolyline(new[] { s, mid, e }, RoadDirt with { A = 0.30f }, 2f, true);
            }
        }
    }

    // Place seeds: deterministic viewer-derived landmark glyphs (see PlaceSeeds.cs). Visual
    // identity only — the sim has no settlements; nothing here is sim state or touches its Rng.
    // Held regions fly a small faction banner; parchment name tags are drawn later (layer 8) so
    // they sit above people dots — see DrawPlaceTags.
    private (Vector2 c, float s) MarkerAnchor(Func<float, float, Vector2> P, float regionR, Region r)
    {
        var c = P(r.X, r.Y) + PlaceSeeds.Offset(World!.Seed, r.Id) * regionR * 0.30f;
        return (c, Mathf.Clamp(regionR * 0.30f, 7f, 30f));
    }

    private void DrawPlaceSeeds(Func<float, float, Vector2> P, float regionR, Font font)
    {
        foreach (var r in World!.Regions)
        {
            var (c, s) = MarkerAnchor(P, regionR, r);
            DrawPlaceMarker(c, s, PlaceSeeds.KindOf(World, r));
            if (r.ControllingFactionId is string fid)
                DrawBannerFlag(c + new Vector2(s * 0.85f, s * 0.5f), s,
                               FactionColors.GetValueOrDefault(fid, Neutral));
        }
    }

    // 5a. Place memory: scars and stones from real anchored events, drawn beneath the place
    // markers and labels so the land remembers without shouting. Fixed slot angles keep each
    // mark's spot stable; old marks fade toward a faint floor but never vanish until evicted.
    private void DrawPlaceMemory(Func<float, float, Vector2> P, float regionR)
    {
        foreach (var (rid, marks) in _placeMarks)
        {
            if (rid < 0 || rid >= World!.Regions.Count) continue;
            var r = World.Regions[rid];
            var center = P(r.X, r.Y);
            float s = Mathf.Clamp(regionR * 0.16f, 4f, 13f);
            for (int i = 0; i < marks.Count; i++)
            {
                float age = World.Year - marks[i].year;
                float a = Mathf.Lerp(0.85f, 0.30f, Mathf.Clamp(age / 250f, 0f, 1f));
                var c = center + new Vector2(Mathf.Cos(MarkAngles[i]), Mathf.Sin(MarkAngles[i]))
                        * regionR * 0.55f;
                DrawMemoryMark(c, s, marks[i].kind, a);
            }
        }
    }

    private void DrawMemoryMark(Vector2 c, float s, MarkKind kind, float a)
    {
        switch (kind)
        {
            case MarkKind.FoundingStone:   // a standing stone raised where a people first held land
                DrawRect(new Rect2(c.X - s * 0.18f, c.Y - s * 0.85f, s * 0.36f, s * 1.05f), StoneMark with { A = a });
                DrawRect(new Rect2(c.X - s * 0.3f, c.Y - s * 0.98f, s * 0.6f, s * 0.16f), StoneMark.Darkened(0.2f) with { A = a });
                break;
            case MarkKind.WarScar:         // a scorch and a snapped banner pole where land was seized
                DrawLine(c + new Vector2(-s * 0.45f, s * 0.35f), c + new Vector2(s * 0.45f, -s * 0.35f),
                         Ui.Ember.Darkened(0.25f) with { A = a }, Mathf.Max(1.2f, s * 0.14f));
                DrawLine(c + new Vector2(-s * 0.45f, -s * 0.35f), c + new Vector2(s * 0.45f, s * 0.35f),
                         Ui.Ember.Darkened(0.25f) with { A = a }, Mathf.Max(1.2f, s * 0.14f));
                DrawLine(c + new Vector2(0, s * 0.4f), c + new Vector2(s * 0.3f, -s * 0.6f),
                         MarkInk with { A = a * 0.8f }, Mathf.Max(1f, s * 0.1f));
                break;
            case MarkKind.AbandonCairn:    // stones stacked over holds the wild crept back across
                DrawCircle(c + new Vector2(-s * 0.22f, s * 0.1f), s * 0.2f, StoneMark with { A = a });
                DrawCircle(c + new Vector2(s * 0.22f, s * 0.1f), s * 0.2f, StoneMark with { A = a });
                DrawCircle(c + new Vector2(0, -s * 0.18f), s * 0.18f, StoneMark.Lightened(0.1f) with { A = a });
                break;
            case MarkKind.CultureRibbon:   // a custom took root (or faded, clashed, spread) here
                DrawPolyline(new[]
                {
                    c + new Vector2(-s * 0.4f, s * 0.3f),
                    c + new Vector2(-s * 0.1f, -s * 0.25f),
                    c + new Vector2(s * 0.15f, s * 0.25f),
                    c + new Vector2(s * 0.45f, -s * 0.3f),
                }, Ui.Violet with { A = a }, Mathf.Max(1.2f, s * 0.16f), true);
                break;
        }
    }

    // Parchment place tags: region name over its place-kind hint, pinned under the marker.
    // Zoom-gated like the old captions were; the selected region's tag shows at any zoom so the
    // Region Lens always has its place named on the map. Readability beats completeness: one
    // nudge-down attempt when crowded, then skip — a far-flung tag is worse than a missing one.
    private void DrawPlaceTags(Func<float, float, Vector2> P, float regionR, List<Rect2> placed)
    {
        foreach (var r in World!.Regions)
        {
            bool sel = r.Id == SelectedRegionId;
            if (!sel && _zoom < CaptionZoom) continue;
            var (c, s) = MarkerAnchor(P, regionR, r);
            string name = r.Name;
            string sub = PlaceSeeds.Label(PlaceSeeds.KindOf(World, r));
            float w = Mathf.Max(Ui.SerifBold.GetStringSize(name, HorizontalAlignment.Left, -1, 13).X,
                                Ui.SmallCaps.GetStringSize(sub, HorizontalAlignment.Left, -1, 10).X);
            var rect = new Rect2(c.X - w / 2f - 8, c.Y + s * 0.9f + 6, w + 16, 34);
            if (!sel)
            {
                if (placed.Any(p => p.Intersects(rect))) rect.Position += new Vector2(0, 36);
                if (placed.Any(p => p.Intersects(rect))) continue;
            }
            placed.Add(rect);
            (sel ? PlaceTagSelected : PlaceTag).Draw(GetCanvasItem(), rect);
            DrawString(Ui.SerifBold, rect.Position + new Vector2(8, 15), name,
                HorizontalAlignment.Left, -1, 13, Ui.InkDeep);
            DrawString(Ui.SmallCaps, rect.Position + new Vector2(8, 28), sub,
                HorizontalAlignment.Left, -1, 10, Ui.Faded);
        }
    }

    private void DrawPlaceMarker(Vector2 c, float s, PlaceSeeds.Kind kind)
    {
        switch (kind)
        {
            case PlaceSeeds.Kind.HillFort:
                DrawArc(c, s * 0.9f, 0, Mathf.Tau, 20, StoneMark with { A = 0.85f }, Mathf.Max(1.2f, s * 0.1f));
                DrawHut(c + new Vector2(0, s * 0.1f), s * 0.7f);
                break;
            case PlaceSeeds.Kind.WatchPost:
                DrawRect(new Rect2(c.X - s * 0.14f, c.Y - s * 0.9f, s * 0.28f, s * 1.1f), StoneMark);
                DrawRect(new Rect2(c.X - s * 0.3f, c.Y - s * 1.05f, s * 0.6f, s * 0.18f), RoofDark);
                break;
            case PlaceSeeds.Kind.Cairn:
                DrawCircle(c + new Vector2(-s * 0.22f, 0), s * 0.2f, StoneMark);
                DrawCircle(c + new Vector2(s * 0.22f, 0), s * 0.2f, StoneMark);
                DrawCircle(c + new Vector2(0, -s * 0.28f), s * 0.18f, StoneMark.Lightened(0.1f));
                break;
            case PlaceSeeds.Kind.Grove:
                DrawTree(c + new Vector2(-s * 0.35f, s * 0.15f), s * 0.9f);
                DrawTree(c + new Vector2(s * 0.3f, s * 0.05f), s * 1.1f);
                DrawTree(c + new Vector2(0, s * 0.4f), s * 0.7f);
                break;
            case PlaceSeeds.Kind.Shrine:
                DrawRect(new Rect2(c.X - s * 0.16f, c.Y - s * 0.5f, s * 0.32f, s * 0.75f), StoneMark);
                DrawRect(new Rect2(c.X - s * 0.3f, c.Y - s * 0.62f, s * 0.6f, s * 0.14f), StoneMark.Darkened(0.15f));
                DrawCircle(c + new Vector2(0, -s * 0.8f), s * 0.11f, Ui.Gold);
                break;
            case PlaceSeeds.Kind.Camp:
                DrawTent(c + new Vector2(-s * 0.3f, s * 0.25f), s * 0.6f);
                DrawTent(c + new Vector2(s * 0.32f, s * 0.3f), s * 0.45f);
                break;
            case PlaceSeeds.Kind.Ford:
                DrawLine(c + new Vector2(-s * 0.6f, -s * 0.12f), c + new Vector2(s * 0.6f, -s * 0.12f), Sea.Lightened(0.25f) with { A = 0.8f }, Mathf.Max(1.2f, s * 0.1f));
                DrawLine(c + new Vector2(-s * 0.6f, s * 0.16f), c + new Vector2(s * 0.6f, s * 0.16f), Sea.Lightened(0.25f) with { A = 0.8f }, Mathf.Max(1.2f, s * 0.1f));
                for (int i = -1; i <= 1; i++)
                    DrawLine(c + new Vector2(i * s * 0.3f, -s * 0.3f), c + new Vector2(i * s * 0.3f, s * 0.34f), Timber, Mathf.Max(1.4f, s * 0.14f));
                break;
            case PlaceSeeds.Kind.FarmCluster:
                DrawRect(new Rect2(c.X - s * 0.7f, c.Y - s * 0.05f, s * 0.85f, s * 0.26f), FieldGold with { A = 0.85f });
                DrawRect(new Rect2(c.X - s * 0.55f, c.Y + s * 0.3f, s * 0.85f, s * 0.26f), FieldGold.Darkened(0.12f) with { A = 0.85f });
                DrawHut(c + new Vector2(s * 0.45f, -s * 0.3f), s * 0.5f);
                break;
            case PlaceSeeds.Kind.MarketHamlet:
                DrawHut(c + new Vector2(-s * 0.35f, 0), s * 0.55f);
                DrawHut(c + new Vector2(s * 0.3f, s * 0.2f), s * 0.45f);
                DrawRect(new Rect2(c.X + s * 0.05f, c.Y + s * 0.42f, s * 0.4f, s * 0.16f), Thatch);
                break;
            case PlaceSeeds.Kind.Ruins:
                DrawRect(new Rect2(c.X - s * 0.4f, c.Y - s * 0.45f, s * 0.18f, s * 0.6f), StoneMark with { A = 0.8f });
                DrawRect(new Rect2(c.X + s * 0.15f, c.Y - s * 0.2f, s * 0.18f, s * 0.35f), StoneMark with { A = 0.7f });
                DrawLine(c + new Vector2(-s * 0.15f, s * 0.25f), c + new Vector2(s * 0.45f, s * 0.12f), StoneMark with { A = 0.6f }, Mathf.Max(1.2f, s * 0.12f));
                break;
        }
    }

    private void DrawHut(Vector2 c, float w)
    {
        float h = w * 0.62f;
        DrawRect(new Rect2(c.X - w / 2f, c.Y - h / 2f, w, h), Timber);
        DrawColoredPolygon(new[]
        {
            new Vector2(c.X - w * 0.62f, c.Y - h / 2f),
            new Vector2(c.X + w * 0.62f, c.Y - h / 2f),
            new Vector2(c.X, c.Y - h / 2f - w * 0.55f),
        }, RoofDark);
    }

    private void DrawTree(Vector2 basePos, float s)
    {
        DrawLine(basePos, basePos + new Vector2(0, -s * 0.45f), TrunkBrown, Mathf.Max(1f, s * 0.12f));
        DrawCircle(basePos + new Vector2(0, -s * 0.62f), s * 0.34f, TreeGreen);
    }

    private void DrawTent(Vector2 basePos, float w)
    {
        DrawColoredPolygon(new[]
        {
            basePos + new Vector2(-w / 2f, 0),
            basePos + new Vector2(w / 2f, 0),
            basePos + new Vector2(0, -w * 0.8f),
        }, TentCloth);
    }

    // Faction identity as cloth: a small pennant beside the place marker, not territory paint.
    private void DrawBannerFlag(Vector2 basePos, float s, Color col)
    {
        var top = basePos + new Vector2(0, -s);
        DrawLine(basePos, top, MarkInk with { A = 0.85f }, 1.2f);
        DrawColoredPolygon(new[] { top, top + new Vector2(s * 0.45f, s * 0.15f), top + new Vector2(0, s * 0.3f) }, col);
    }

    // Hover tag: region name + terrain/holder/place-hint on a soft dark backing.
    private void DrawHoverTag(Func<float, float, Vector2> P, float regionR, Font font)
    {
        var r = World!.Regions[_hoverRegion];
        var c = P(r.X, r.Y);
        string holder = r.ControllingFactionId is string fid ? World.Factions[fid].Name : "unclaimed";
        string l1 = r.Name;
        string l2 = $"{r.TerrainType} · {holder} · {PlaceSeeds.Label(PlaceSeeds.KindOf(World, r))}";
        float w = Mathf.Max(font.GetStringSize(l1, HorizontalAlignment.Center, -1, 14).X,
                            font.GetStringSize(l2, HorizontalAlignment.Center, -1, 11).X);
        var rect = new Rect2(c.X - w / 2f - 8, c.Y - regionR - 46, w + 16, 38);
        LabelTag.Draw(GetCanvasItem(), rect);
        DrawString(font, new Vector2(rect.Position.X + 8, rect.Position.Y + 16), l1,
            HorizontalAlignment.Center, w, 14, Ui.Parchment);
        DrawString(font, new Vector2(rect.Position.X + 8, rect.Position.Y + 31), l2,
            HorizontalAlignment.Center, w, 11, Ui.RowBorder);
    }

    // 8b. watched-soul name tags: at most a handful of souls are ever followed, so this stays
    // O(followed) per frame. Overlap-skip against the label rects already placed — a watched
    // name dodging a place tag beats two unreadable tags.
    private void DrawSoulTags(List<Rect2> placed)
    {
        foreach (var (pos, id) in _soulScreen)
        {
            if (!World!.People.TryGetValue(id, out var p)) continue;
            string name = "★ " + p.Name;
            float w = Ui.SmallCaps.GetStringSize(name, HorizontalAlignment.Left, -1, 10).X;
            var rect = new Rect2(pos.X - w / 2f - 6, pos.Y + 15f, w + 12, 17);
            if (placed.Any(pr => pr.Intersects(rect))) rect.Position += new Vector2(0, 19);
            if (placed.Any(pr => pr.Intersects(rect))) continue;
            placed.Add(rect);
            SoulNameTag.Draw(GetCanvasItem(), rect);
            DrawString(Ui.SmallCaps, rect.Position + new Vector2(6, 12), name,
                HorizontalAlignment.Left, -1, 10, Ui.InkDeep);
        }
    }

    private void DrawPeople(Func<float, float, Vector2> P, float regionR, Font font)
    {
        // Each faction's regions, in id order, so person-to-region placement is deterministic.
        var facRegions = new Dictionary<string, List<Region>>();
        foreach (var r in World!.Regions)
            if (r.ControllingFactionId is string fid)
            {
                if (!facRegions.TryGetValue(fid, out var list)) { list = new(); facRegions[fid] = list; }
                list.Add(r);
            }

        foreach (var p in World.Living())
        {
            var col = FactionColors.GetValueOrDefault(p.FactionId, Neutral);
            Vector2 center;
            if (facRegions.TryGetValue(p.FactionId, out var regs) && regs.Count > 0)
            {
                var rg = regs[p.Id % regs.Count];   // stable region per person
                center = P(rg.X, rg.Y);
            }
            else center = P(0.5f, 0.5f);            // landless fallback (extinct/ghost faction)

            var off = new Vector2(Frac(p.Id * 0.61803398875f) * 2f - 1f,
                                  Frac(p.Id * 0.75487766624f) * 2f - 1f) * (regionR * 0.62f);
            if (off.Length() > regionR * 0.72f) off = off.Normalized() * regionR * 0.72f;
            var pos = center + off;

            float r = (p.IsLeader ? 6.5f : 3.8f) * _zoom;
            var dot = p.Cursed ? Ui.Ember : (p.Sex == "f" ? col.Lightened(0.28f) : col);
            DrawCircle(pos, r, dot);
            if (p.IsLeader) DrawArc(pos, r + 2.5f, 0, Mathf.Tau, 20, Ui.LensGold, 1.6f);
            float hitR = Mathf.Max(r, 6f);
            if (Souls is not null && Souls.Contains(p.Id))
            {
                // The divine bookmark: a gold halo that never shrinks below findable at fit
                // zoom, breathing softly while the soul lives; a saga sighting flares it.
                float halo = Mathf.Max(r + 4.5f, 10f);
                DrawArc(pos, halo, 0, Mathf.Tau, 24, Ui.GoldGlow, 2.4f);
                float breathe = 0.20f + 0.16f * Mathf.Sin(_breath * 2.2f + p.Id);
                DrawArc(pos, halo + 3.5f, 0, Mathf.Tau, 24, Ui.GoldGlow with { A = breathe }, 1.6f);
                if (_soulPulses.TryGetValue(p.Id, out float sp))
                {
                    float t = sp / SoulPulseDuration;
                    DrawArc(pos, halo + (1f - t) * 10f, 0, Mathf.Tau, 24, Ui.GoldGlow with { A = t * 0.9f }, 2.2f);
                }
                _soulScreen.Add((pos, p.Id));
                hitR = Mathf.Max(hitR, halo);
            }
            else if (Marked is not null && Marked.Contains(p.Id))
                DrawArc(pos, r + 4.5f, 0, Mathf.Tau, 24, new Color("7fc8d8"), 2f);
            _dots.Add((pos, hitR, p.Id));
        }
    }

    private List<Rect2> DrawFactionLabels(Func<float, float, Vector2> P, Font font)
    {
        // Greedy de-overlap: factions whose centroids land close (mid-island neighbours) would
        // stack their tags; nudge any colliding tag downward until it clears the ones already drawn.
        // Returns the claimed rects so the place tags drawn next can dodge them too.
        var placed = new List<Rect2>();
        foreach (var f in World!.Config.Factions)
        {
            var fac = World.Factions[f.Id];
            var held = World.Regions.Where(r => r.ControllingFactionId == f.Id).ToList();
            if (held.Count == 0) continue;

            var centroid = P(held.Average(r => r.X), held.Average(r => r.Y));
            string sub = $"pop {fac.Members.Count} · {(fac.LeaderId is int lid ? World.People[lid].Name : "(none)")}";
            var col = FactionColors.GetValueOrDefault(f.Id, Neutral);
            bool sel = f.Id == SelectedFactionId;

            // Tag backing keeps names readable over any terrain; selection earns a gold-bordered,
            // darker tag — prominence through hierarchy, not size.
            float w = Mathf.Max(font.GetStringSize(fac.Name, HorizontalAlignment.Center, -1, 14).X,
                                font.GetStringSize(sub, HorizontalAlignment.Center, -1, 11).X);
            var rect = new Rect2(centroid.X - w / 2f - 7, centroid.Y - 16, w + 14, 34);
            for (int guard = 0; guard < 8 && placed.Any(p => p.Intersects(rect)); guard++)
                rect.Position += new Vector2(0, 38);
            placed.Add(rect);
            (sel ? LabelTagSelected : LabelTag).Draw(GetCanvasItem(), rect);
            DrawString(font, new Vector2(rect.Position.X + 7, rect.Position.Y + 14), fac.Name,
                HorizontalAlignment.Center, w, 14, Ui.Parchment);
            DrawString(font, new Vector2(rect.Position.X + 7, rect.Position.Y + 29), sub,
                HorizontalAlignment.Center, w, 11, col.Lightened(0.35f));
        }
        return placed;
    }

    private void BuildIsland()
    {
        if (_islandNorm is not null || World is null) return;
        var rng = new Rng(World.Seed);          // own stream — never touches the sim's Rng
        int pts = rng.RandInt(16, 24);
        var list = new List<Vector2>(pts);
        for (int i = 0; i < pts; i++)
        {
            float ang = i / (float)pts * Mathf.Tau;
            float noise = 1f + (rng.RandInt(0, 100) / 100f - 0.5f) * 0.36f;   // ±18%
            float rad = 0.47f * noise;
            list.Add(new Vector2(0.5f + Mathf.Cos(ang) * rad, 0.5f + Mathf.Sin(ang) * rad));
        }
        _islandNorm = list;

        // Shallows rim: the same silhouette scaled slightly outward about its centroid — pure
        // derived geometry, zero extra rng draws, so the island outline stays frame-identical.
        var centroid = Vector2.Zero;
        foreach (var p in list) centroid += p;
        centroid /= list.Count;
        _shallowsNorm = list.Select(p => centroid + (p - centroid) * 1.045f).ToList();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion mm)
        {
            if (_leftDown)
            {
                if (!_dragging && mm.Position.DistanceTo(_dragStart) > DragThreshold) _dragging = true;
                if (_dragging) { _pan += mm.Relative; ClampPan(); MarkManual(); QueueRedraw(); }
                return;
            }
            int hover = NearestRegion(mm.Position);
            if (hover != _hoverRegion) { _hoverRegion = hover; QueueRedraw(); }
            return;
        }
        if (@event is not InputEventMouseButton mb) return;

        // Wheel zooms about the cursor so the point under the mouse stays put.
        if (mb.Pressed && mb.ButtonIndex == MouseButton.WheelUp) { ZoomAt(_zoom * 1.15f, mb.Position); return; }
        if (mb.Pressed && mb.ButtonIndex == MouseButton.WheelDown) { ZoomAt(_zoom / 1.15f, mb.Position); return; }
        if (mb.ButtonIndex != MouseButton.Left) return;

        if (mb.Pressed) { _leftDown = true; _dragging = false; _dragStart = mb.Position; return; }

        // Left released: a drag was a pan; a click (no drag) selects.
        _leftDown = false;
        if (_dragging) { _dragging = false; return; }
        Select(mb.Position);
    }

    private void Select(Vector2 pos)
    {
        int bestId = -1;
        float bestD = float.MaxValue;
        foreach (var d in _dots)
        {
            float dist = pos.DistanceTo(d.pos);
            if (dist <= d.r + 3 && dist < bestD) { bestD = dist; bestId = d.id; }
        }
        if (bestId >= 0)
        {
            // A watched soul's marker opens the lighter glimpse card; everyone else the inspector.
            if (Souls is not null && Souls.Contains(bestId) && SoulPicked is not null)
                SoulPicked(bestId, pos);
            else PersonPicked?.Invoke(bestId);
            return;
        }

        int region = NearestRegion(pos);
        if (region >= 0) RegionPicked?.Invoke(region);
    }

    private int NearestRegion(Vector2 pos)
    {
        int best = -1;
        float bestD = float.MaxValue;
        foreach (var h in _regionHits)
        {
            float dist = pos.DistanceTo(h.pos);
            if (dist <= h.r && dist < bestD) { bestD = dist; best = h.id; }
        }
        return best;
    }
}
