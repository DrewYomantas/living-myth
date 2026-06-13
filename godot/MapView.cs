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
    public Action<int>? SitePicked;         // a Sites V1 site marker clicked — Main opens the Site Card
    public Action<int>? ReplayBeatPicked;   // a numbered replay mark clicked — Main scrubs to that beat
    public Action<int>? TurningPicked;      // a turning-point mark clicked — Main opens its thread

    // Chronicle Replay overlay (viewer-only): Main feeds ONLY honestly anchored beats here —
    // a beat with no SiteId/RegionId never gets a mark (it lives in the side rail instead).
    // Edges are real recorded cause links between anchored beats; the spine is the proximate-
    // cause walk from the focal event, everything else draws faint (real branches only).
    public sealed class ReplayMark
    {
        public Vector2 Norm;     // map-space position: the site's cell or the region's heart
        public int Number;       // the beat's 1-based number in the FULL rail (gaps = unplaced beats)
        public bool Current;
    }
    public List<ReplayMark>? ReplayMarks;
    public List<(int a, int b, bool spine)>? ReplayEdges;   // indexes into ReplayMarks
    public bool ReplayActive;
    private readonly List<(Vector2 pos, float r, int idx)> _replayScreen = new();

    // Turning-point pulses: the constellation of recent pivots. Fed by Main from the
    // authored classifier, ONLY for events with a real place anchor; capped, ages by sim
    // year. Pure rendering — clicking one opens the event's thread.
    private readonly List<(float nx, float ny, int eventId, int year)> _turningMarks = new();
    private const int TurningMarksKept = 12;
    private readonly List<(Vector2 pos, float r, int eventId)> _turningScreen = new();

    public void AddTurningMark(float nx, float ny, int eventId, int year)
    {
        _turningMarks.Add((nx, ny, eventId, year));
        if (_turningMarks.Count > TurningMarksKept) _turningMarks.RemoveAt(0);
    }

    private readonly List<(Vector2 pos, float r, int id)> _dots = new();
    private readonly List<(Vector2 pos, float r, int id)> _siteScreen = new();   // sites drawn this frame, for clicks + tags
    private int _hoverRegion = -1;

    // The living-atlas skin: WorldSurface rendered once into a nearest-filtered pixel
    // texture (2×2 texels per cell, hash-speckled), rebuilt only when the surface Version
    // bumps (a terraform edit) or territory changes hands — never per frame.
    private ImageTexture? _surfaceTex;
    private int _texSurfaceVersion = -1;
    private string _texTerritorySig = "";

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
    // TODO (next session — famine-scar polish, the #1 feel-test finding): famines recur often, so
    // FamineScar crowds rarer founding/war/battle marks out of the 4-slot ring. Give famine its own
    // scar store (like _homeMarks below) or cap it to 1-most-recent-per-region, and bump low-zoom
    // legibility. Then: terrain-typed harvest. (viewer-only — must hold verify 823/559/910/632.)
    public enum MarkKind { FoundingStone, WarScar, AbandonCairn, CultureRibbon, Battle, FamineScar }
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

    // Life memory (Place Memory V2): a remembered life raises a memorial cairn at the home of
    // its line — fed from Event.HomeRegionId only, never a death place. Its own store, slots,
    // and shape keep home memory visually apart from the true place marks above.
    private readonly Dictionary<int, List<(int year, int eventId)>> _homeMarks = new();
    private const int HomeMarksPerRegion = 3;
    private static readonly float[] HomeMarkAngles = { 0.35f, 2.95f, 4.45f };   // rim slots, clear of MarkAngles

    public void AddHomeMark(int regionId, int year, int eventId)
    {
        if (!_homeMarks.TryGetValue(regionId, out var list)) { list = new(); _homeMarks[regionId] = list; }
        list.Add((year, eventId));
        if (list.Count > HomeMarksPerRegion) list.RemoveAt(0);
    }

    public IReadOnlyList<(int year, int eventId)> HomeMarksFor(int regionId)
        => _homeMarks.TryGetValue(regionId, out var list) ? list
           : Array.Empty<(int year, int eventId)>();

    public bool HasHomeMark(int regionId, int eventId)
        => _homeMarks.TryGetValue(regionId, out var list) && list.Any(m => m.eventId == eventId);

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

    // Find-them focus: an explicit player ask (cast click) eases the lens onto a normalized
    // map point. Outranks the drama camera and ignores its cooldown; any manual pan/zoom
    // cancels it — the lens never fights the player.
    private Vector2 _focusNorm;
    private float _focusRemaining;
    private const float FocusEaseSecs = 1.4f;

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
    public int SelectedSiteId = -1;     // set by Main while a Site Card is open — small gold ring + its tag
    public HashSet<int>? FollowedRegions;   // shared with Main, mutated in place — lands the player watches

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
    private const float CaptionZoom = 2.0f;   // region place tags appear at/above this zoom
    private const float SiteTagZoom = 2.4f;   // individual site name tags appear at/above this zoom

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

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        TextureFilter = TextureFilterEnum.Nearest;   // crisp pixel-diorama cells, never smeared
    }

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

        // Find-them focus first: an explicit ask beats the drama camera.
        if (_focusRemaining > 0f)
        {
            _focusRemaining -= dt;
            float side = MapSide();
            var center = Size / 2f;
            var origin = new Vector2((Size.X - side) / 2f, (Size.Y - side) / 2f);
            _zoom = Mathf.Lerp(_zoom, Mathf.Max(_zoom, FollowZoom), dt * 3f);
            var b = origin + _focusNorm * side;
            _pan = _pan.Lerp(-(b - center) * _zoom, dt * 3f);
            ClampPan();
        }
        // Drama camera: lean toward the pulsing region while its pulse lives, unless the player
        // just took manual control. Eases zoom + pan together; clamped to map bounds.
        else if (CameraFollow && _manualCamCooldown <= 0f && _easeTargetRegion >= 0 && World is not null
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

    // Ease the lens onto a region's heart — the "find it" verb for places.
    public void FocusRegion(int regionId)
    {
        if (World is null || regionId < 0 || regionId >= World.Regions.Count) return;
        var r = World.Regions[regionId];
        _focusNorm = new Vector2(r.X, r.Y);
        _focusRemaining = FocusEaseSecs;
        _manualCamCooldown = 0f;
        _easeTargetRegion = -1;
    }

    // Ease the lens onto a person: their deterministic scatter region while alive (the same
    // placement DrawPeople uses — no new precision implied), else the home of their line.
    // Honest fallthrough: a dead, homeless soul moves the lens nowhere.
    public void FocusPerson(int personId)
    {
        if (World is null || !World.People.TryGetValue(personId, out var p)) return;
        if (p.Alive)
        {
            var regs = new List<Region>();
            foreach (var r in World.Regions)
                if (r.ControllingFactionId == p.FactionId) regs.Add(r);
            if (regs.Count > 0)
            {
                var rg = regs[p.Id % regs.Count];   // same stable region as DrawPeople
                _focusNorm = new Vector2(rg.X, rg.Y);
                _focusRemaining = FocusEaseSecs;
                _manualCamCooldown = 0f;
                _easeTargetRegion = -1;
                PulseSoul(personId);
                return;
            }
        }
        if (p.HomeRegionId is int hr) FocusRegion(hr);
    }

    // Ease the lens onto one real place — the "find it" verb for sites.
    public void FocusSite(int siteId)
    {
        if (World is null || siteId < 0 || siteId >= World.Sites.All.Count) return;
        var s = World.Sites.Get(siteId);
        _focusNorm = new Vector2(s.Nx, s.Ny);
        _focusRemaining = FocusEaseSecs;
        _manualCamCooldown = 0f;
        _easeTargetRegion = -1;
    }

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

    private void MarkManual() { _manualCamCooldown = ManualCamCooldownSecs; _easeTargetRegion = -1; _focusRemaining = 0f; }

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
        _soulScreen.Clear();
        _siteScreen.Clear();
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

        // 2+3. land: the WorldSurface skin — terrain cells, rivers, lakes, and the
        // territory wash baked into one pixel texture (rebuilt only on change).
        EnsureSurfaceTexture();
        if (_surfaceTex is not null)
        {
            var tl = P(0, 0);
            DrawTextureRect(_surfaceTex, new Rect2(tl, P(1, 1) - tl), false);
        }

        // The hovered region breathes a faint ring (recognition, not paint); the selected
        // people's lands firm up while their inspector is open.
        if (_hoverRegion >= 0 && _hoverRegion < World.Regions.Count)
        {
            var hr = World.Regions[_hoverRegion];
            DrawArc(P(hr.X, hr.Y), regionR, 0, Mathf.Tau, 40,
                    RegionColor(hr).Lightened(0.2f) with { A = 0.5f }, 2f);
        }
        if (SelectedFactionId is string selFid)
            foreach (var r in World.Regions)
                if (r.ControllingFactionId == selFid)
                    DrawArc(P(r.X, r.Y), regionR, 0, Mathf.Tau, 40,
                            RegionColor(r) with { A = 0.8f }, 2.2f);

        DrawRoads(P);                                                       // 4. roads + site paths
        DrawPlaceMemory(P, regionR);                                        // 5a. place memory
        DrawSites(P, regionR);                                              // 5. Sites V1 markers
        DrawPeople(P, regionR, font);                                       // 6+7. people + highlights

        // 7b. region lens ring — the inspected place, ringed gold beneath the labels.
        if (SelectedRegionId >= 0 && SelectedRegionId < World.Regions.Count)
        {
            var sr = World.Regions[SelectedRegionId];
            DrawArc(P(sr.X, sr.Y), regionR + 3f, 0, Mathf.Tau, 48, Ui.LensGold with { A = 0.85f }, 2.5f);
        }
        // 7b'. the inspected site, ringed small and gold — a place, not a whole land.
        if (SelectedSiteId >= 0 && SelectedSiteId < World.Sites.All.Count)
        {
            var ss = World.Sites.Get(SelectedSiteId);
            DrawArc(P(ss.Nx, ss.Ny), Mathf.Max(11f, regionR * 0.24f), 0, Mathf.Tau, 32,
                    Ui.LensGold with { A = 0.9f }, 2f);
        }

        // 7c. followed lands — a quiet persistent gold ring, the player's standing mark on a
        // place (fainter and tighter than the lens ring, steady unlike a pulse).
        if (FollowedRegions is { Count: > 0 })
            foreach (var rid in FollowedRegions)
            {
                if (rid < 0 || rid >= World.Regions.Count) continue;
                var fr = World.Regions[rid];
                DrawArc(P(fr.X, fr.Y), regionR + 1.5f, 0, Mathf.Tau, 48, Ui.Gold with { A = 0.45f }, 1.6f);
            }

        // 7d. omen marks — the eye of fate over a land: a violet star and a slow-breathing
        // ring, deliberately apart from place scars (events past) and home cairns (memory).
        foreach (var pr in World.DivinePressures)
        {
            if (pr.Kind != DivinePressureKind.Omen || !pr.IsActive(World)
                || !int.TryParse(pr.TargetId, out int orid) || orid >= World.Regions.Count) continue;
            var or = World.Regions[orid];
            var oc = P(or.X, or.Y);
            float oa = 0.45f + 0.25f * Mathf.Sin(_breath * 1.6f);
            DrawArc(oc, regionR * 0.85f, 0, Mathf.Tau, 40, Ui.Violet with { A = oa * 0.6f }, 1.8f);
            DrawString(font, oc + new Vector2(-7, -regionR * 0.85f - 6), "✶",
                HorizontalAlignment.Left, -1, 16, Ui.Violet with { A = Mathf.Min(1f, oa + 0.3f) });
        }

        DrawTurningMarks(P);                                                // 7e. turning points

        var placed = DrawFactionLabels(P, font);                            // 8. labels
        DrawPlaceTags(P, regionR, placed);
        DrawSiteTags(placed);
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

        DrawReplayOverlay(P);                                               // 10. chronicle replay
    }

    // 7e. Turning points: a small ember-gold diamond with a slow halo on each recent pivot —
    // only events with a TRUE place anchor ever mark (Main enforces it; this just draws).
    // Alpha ages by sim year so the constellation is always "recent history", never clutter.
    private void DrawTurningMarks(Func<float, float, Vector2> P)
    {
        _turningScreen.Clear();
        if (ReplayActive) return;   // the replay path owns the stage while it runs
        foreach (var (nx, ny, eventId, year) in _turningMarks)
        {
            float age = World!.Year - year;
            float a = Mathf.Lerp(0.95f, 0.25f, Mathf.Clamp(age / 80f, 0f, 1f));
            var c = P(nx, ny);
            float s = 7f;
            var col = Ui.GoldGlow with { A = a };
            DrawColoredPolygon(new[]
            {
                c + new Vector2(0, -s), c + new Vector2(s * 0.7f, 0),
                c + new Vector2(0, s), c + new Vector2(-s * 0.7f, 0),
            }, col);
            DrawColoredPolygon(new[]
            {
                c + new Vector2(0, -s * 0.45f), c + new Vector2(s * 0.32f, 0),
                c + new Vector2(0, s * 0.45f), c + new Vector2(-s * 0.32f, 0),
            }, Ui.Ember with { A = a });
            float halo = 0.25f + 0.18f * Mathf.Sin(_breath * 1.8f + eventId);
            DrawArc(c, s + 4f, 0, Mathf.Tau, 24, Ui.GoldGlow with { A = a * halo }, 1.5f);
            _turningScreen.Add((c, s + 5f, eventId));
        }
    }

    // 10. The Chronicle Replay path: dimmed atlas, real cause edges drawn as a glowing
    // trail, numbered parchment markers on the honestly anchored beats, the current beat
    // breathing. Marks/edges come pre-vetted from Main — nothing is placed here.
    private void DrawReplayOverlay(Func<float, float, Vector2> P)
    {
        _replayScreen.Clear();
        if (!ReplayActive || ReplayMarks is null) return;
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.06f, 0.045f, 0.03f, 0.45f));

        if (ReplayEdges is not null)
            foreach (var (a, b, spine) in ReplayEdges)
            {
                var pa = P(ReplayMarks[a].Norm.X, ReplayMarks[a].Norm.Y);
                var pb = P(ReplayMarks[b].Norm.X, ReplayMarks[b].Norm.Y);
                if (spine)
                {
                    DrawLine(pa, pb, Ui.GoldGlow with { A = 0.25f }, 7f);   // soft glow under
                    DrawLine(pa, pb, Ui.GoldGlow with { A = 0.9f }, 2.2f);
                }
                else
                    DrawLine(pa, pb, Ui.GoldGlow with { A = 0.30f }, 1.4f); // a real branch, faint
            }

        foreach (var m in ReplayMarks)
        {
            var c = P(m.Norm.X, m.Norm.Y);
            float r = m.Current ? 13f : 10f;
            DrawCircle(c, r, m.Current ? Ui.Parchment : Ui.Parchment with { A = 0.92f });
            DrawArc(c, r, 0, Mathf.Tau, 32, m.Current ? Ui.GoldGlow : Ui.Gold with { A = 0.85f },
                    m.Current ? 2.4f : 1.6f);
            if (m.Current)
            {
                float breathe = 0.35f + 0.3f * Mathf.Sin(_breath * 2.4f);
                DrawArc(c, r + 5f, 0, Mathf.Tau, 32, Ui.GoldGlow with { A = breathe }, 2f);
            }
            string num = m.Number.ToString();
            float w = Ui.SerifBold.GetStringSize(num, HorizontalAlignment.Left, -1, 12).X;
            DrawString(Ui.SerifBold, c + new Vector2(-w / 2f, 4.5f), num,
                HorizontalAlignment.Left, -1, 12, Ui.InkDeep);
            _replayScreen.Add((c, r + 3f, m.Number));
        }
    }

    // Roads + paths: dirt roads between adjacent regions held by the same people (seat site
    // to seat site — the road goes where the places truly are), and fainter local paths from
    // each region's seat out to its own sites. Path kinks come from a stable hash, never Rng.
    private void DrawRoads(Func<float, float, Vector2> P)
    {
        var sites = World!.Sites;

        // Local paths first, beneath the roads.
        foreach (var r in World.Regions)
        {
            var local = sites.ForRegion(r.Id);
            if (local.Count < 2) continue;
            var seat = P(local[0].Nx, local[0].Ny);
            for (int i = 1; i < local.Count; i++)
            {
                var dst = P(local[i].Nx, local[i].Ny);
                var s = seat.Lerp(dst, 0.10f);
                var e = seat.Lerp(dst, 0.90f);
                DrawPolyline(new[] { s, e }, RoadDirt with { A = 0.16f }, 1.4f, true);
            }
        }

        foreach (var a in World.Regions)
        {
            if (a.ControllingFactionId is not string fid) continue;
            foreach (var bid in a.AdjacentRegionIds)
            {
                if (bid <= a.Id || World.Regions[bid].ControllingFactionId != fid) continue;
                var b = World.Regions[bid];
                var seatA = sites.SeatOf(a.Id);
                var seatB = sites.SeatOf(b.Id);
                var p1 = seatA is not null ? P(seatA.Nx, seatA.Ny) : P(a.X, a.Y);
                var p2 = seatB is not null ? P(seatB.Nx, seatB.Ny) : P(b.X, b.Y);
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

    // The region's parchment tag anchors on its seat site — labels finally sit where the
    // place truly is, not on an abstract region centre.
    private (Vector2 c, float s) MarkerAnchor(Func<float, float, Vector2> P, float regionR, Region r)
    {
        var seat = World!.Sites.SeatOf(r.Id);
        var c = seat is not null ? P(seat.Nx, seat.Ny) : P(r.X, r.Y);
        return (c, Mathf.Clamp(regionR * 0.30f, 7f, 30f));
    }

    // Sites V1: every marker is a REAL place from the sim's site read-model — a stable id,
    // a name, a type the terrain honestly supports, a real surface cell. The seat of a held
    // region flies its people's banner (holder derived live, never stored).
    private void DrawSites(Func<float, float, Vector2> P, float regionR)
    {
        var sites = World!.Sites;
        float s = Mathf.Clamp(regionR * 0.17f, 5f, 16f);
        foreach (var site in sites.All)
        {
            var c = P(site.Nx, site.Ny);
            float ss = s * (site.IsSeat ? 1.3f : 1f);
            DrawSiteMarker(c, ss, site.Type);
            if (site.IsSeat && World.Regions[site.RegionId].ControllingFactionId is string holder)
                DrawBannerFlag(c + new Vector2(ss * 0.95f, ss * 0.55f), ss * 1.15f,
                               FactionColors.GetValueOrDefault(holder, Neutral));
            _siteScreen.Add((c, Mathf.Max(9f, ss), site.Id));
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

        // Memorial cairns sit at the rim of the home lands, farther out than the place marks —
        // a remembered life, never "it happened here".
        foreach (var (rid, marks) in _homeMarks)
        {
            if (rid < 0 || rid >= World!.Regions.Count) continue;
            var r = World.Regions[rid];
            var center = P(r.X, r.Y);
            float s = Mathf.Clamp(regionR * 0.16f, 4f, 13f);
            for (int i = 0; i < marks.Count; i++)
            {
                float age = World.Year - marks[i].year;
                float a = Mathf.Lerp(0.85f, 0.30f, Mathf.Clamp(age / 250f, 0f, 1f));
                var c = center + new Vector2(Mathf.Cos(HomeMarkAngles[i]), Mathf.Sin(HomeMarkAngles[i]))
                        * regionR * 0.78f;
                DrawMemorialCairn(c, s, a);
            }
        }
    }

    // A memorial cairn: stones stacked with intent, a small remembrance light kept at the top —
    // deliberately apart from the abandon cairn's scattered round stones, warm where ruin is cold.
    private void DrawMemorialCairn(Vector2 c, float s, float a)
    {
        DrawRect(new Rect2(c.X - s * 0.34f, c.Y + s * 0.05f, s * 0.68f, s * 0.22f), StoneMark with { A = a });
        DrawRect(new Rect2(c.X - s * 0.22f, c.Y - s * 0.18f, s * 0.44f, s * 0.2f), StoneMark.Lightened(0.08f) with { A = a });
        DrawRect(new Rect2(c.X - s * 0.11f, c.Y - s * 0.38f, s * 0.22f, s * 0.18f), StoneMark.Lightened(0.16f) with { A = a });
        DrawCircle(c + new Vector2(0, -s * 0.52f), Mathf.Max(1.2f, s * 0.1f), Ui.Gold with { A = a * 0.9f });
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
            case MarkKind.Battle:          // crossed swords where armies clashed — two blades,
            {                              // tips up, pommels at the hilts (distinct from the war scorch)
                var hiltL = c + new Vector2(-s * 0.42f, s * 0.5f);
                var hiltR = c + new Vector2(s * 0.42f, s * 0.5f);
                float bw = Mathf.Max(1.2f, s * 0.13f);
                DrawLine(hiltL, c + new Vector2(s * 0.42f, -s * 0.5f), Ui.Ember with { A = a }, bw);   // blade up-right
                DrawLine(hiltR, c + new Vector2(-s * 0.42f, -s * 0.5f), Ui.Ember with { A = a }, bw);  // blade up-left
                DrawCircle(hiltL, Mathf.Max(1.3f, s * 0.13f), MarkInk with { A = a });                 // pommels mark
                DrawCircle(hiltR, Mathf.Max(1.3f, s * 0.13f), MarkInk with { A = a });                 // the hilts
                break;
            }
            case MarkKind.AbandonCairn:    // stones stacked over holds the wild crept back across
                DrawCircle(c + new Vector2(-s * 0.22f, s * 0.1f), s * 0.2f, StoneMark with { A = a });
                DrawCircle(c + new Vector2(s * 0.22f, s * 0.1f), s * 0.2f, StoneMark with { A = a });
                DrawCircle(c + new Vector2(0, -s * 0.18f), s * 0.18f, StoneMark.Lightened(0.1f) with { A = a });
                break;
            case MarkKind.FamineScar:      // parched, cracked ground where the harvest failed —
            {                              // dry fissures in ochre, no stone and no red (not war, not ruin)
                var dry = Ui.Ochre.Darkened(0.1f) with { A = a };
                float fw = Mathf.Max(1f, s * 0.11f);
                DrawLine(c + new Vector2(-s * 0.45f, s * 0.16f), c + new Vector2(s * 0.45f, s * 0.16f), dry, fw);
                DrawLine(c + new Vector2(-s * 0.24f, s * 0.16f), c + new Vector2(-s * 0.33f, s * 0.52f), dry, fw * 0.8f);
                DrawLine(c + new Vector2(0f, s * 0.16f), c + new Vector2(s * 0.06f, s * 0.56f), dry, fw * 0.8f);
                DrawLine(c + new Vector2(s * 0.25f, s * 0.16f), c + new Vector2(s * 0.31f, s * 0.49f), dry, fw * 0.8f);
                break;
            }
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
            var local = World.Sites.ForRegion(r.Id);
            string sub = local.Count > 0
                ? $"{local.Count} place{(local.Count == 1 ? "" : "s")} · {r.TerrainType}"
                : r.TerrainType;
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

    // Sites V1 marker silhouettes, built from the same primitive vocabulary as the old
    // place seeds (timber/thatch/stone/dirt) — generated shapes, never imported art.
    private void DrawSiteMarker(Vector2 c, float s, SiteType type)
    {
        switch (type)
        {
            case SiteType.HillFort:
                DrawArc(c, s * 0.9f, 0, Mathf.Tau, 20, StoneMark with { A = 0.85f }, Mathf.Max(1.2f, s * 0.1f));
                DrawHut(c + new Vector2(0, s * 0.1f), s * 0.7f);
                break;
            case SiteType.WatchPost:
                DrawRect(new Rect2(c.X - s * 0.14f, c.Y - s * 0.9f, s * 0.28f, s * 1.1f), StoneMark);
                DrawRect(new Rect2(c.X - s * 0.3f, c.Y - s * 1.05f, s * 0.6f, s * 0.18f), RoofDark);
                break;
            case SiteType.CairnField:
                DrawCircle(c + new Vector2(-s * 0.34f, s * 0.06f), s * 0.17f, StoneMark);
                DrawCircle(c + new Vector2(s * 0.3f, s * 0.12f), s * 0.17f, StoneMark);
                DrawCircle(c + new Vector2(-0.02f * s, -s * 0.26f), s * 0.16f, StoneMark.Lightened(0.1f));
                DrawCircle(c + new Vector2(s * 0.05f, s * 0.34f), s * 0.13f, StoneMark.Darkened(0.1f));
                break;
            case SiteType.OldBarrow:
                // A long mound with a doorway stone — old earth, not loose stones.
                DrawColoredPolygon(new[]
                {
                    c + new Vector2(-s * 0.75f, s * 0.3f),
                    c + new Vector2(-s * 0.35f, -s * 0.32f),
                    c + new Vector2(s * 0.35f, -s * 0.32f),
                    c + new Vector2(s * 0.75f, s * 0.3f),
                }, Land.Lightened(0.18f));
                DrawRect(new Rect2(c.X - s * 0.12f, c.Y - s * 0.05f, s * 0.24f, s * 0.35f), StoneMark.Darkened(0.2f));
                break;
            case SiteType.SacredGrove:
                DrawTree(c + new Vector2(-s * 0.35f, s * 0.15f), s * 0.9f);
                DrawTree(c + new Vector2(s * 0.3f, s * 0.05f), s * 1.1f);
                DrawTree(c + new Vector2(0, s * 0.4f), s * 0.7f);
                break;
            case SiteType.Shrine:
                DrawRect(new Rect2(c.X - s * 0.16f, c.Y - s * 0.5f, s * 0.32f, s * 0.75f), StoneMark);
                DrawRect(new Rect2(c.X - s * 0.3f, c.Y - s * 0.62f, s * 0.6f, s * 0.14f), StoneMark.Darkened(0.15f));
                DrawCircle(c + new Vector2(0, -s * 0.8f), s * 0.11f, Ui.Gold);
                break;
            case SiteType.WildernessCamp:
                DrawTent(c + new Vector2(-s * 0.3f, s * 0.25f), s * 0.6f);
                DrawTent(c + new Vector2(s * 0.32f, s * 0.3f), s * 0.45f);
                break;
            case SiteType.RiverFord:
                DrawLine(c + new Vector2(-s * 0.6f, -s * 0.12f), c + new Vector2(s * 0.6f, -s * 0.12f), Sea.Lightened(0.25f) with { A = 0.8f }, Mathf.Max(1.2f, s * 0.1f));
                DrawLine(c + new Vector2(-s * 0.6f, s * 0.16f), c + new Vector2(s * 0.6f, s * 0.16f), Sea.Lightened(0.25f) with { A = 0.8f }, Mathf.Max(1.2f, s * 0.1f));
                for (int i = -1; i <= 1; i++)
                    DrawLine(c + new Vector2(i * s * 0.3f, -s * 0.3f), c + new Vector2(i * s * 0.3f, s * 0.34f), Timber, Mathf.Max(1.4f, s * 0.14f));
                break;
            case SiteType.Farmstead:
                DrawRect(new Rect2(c.X - s * 0.7f, c.Y - s * 0.05f, s * 0.85f, s * 0.26f), FieldGold with { A = 0.85f });
                DrawRect(new Rect2(c.X - s * 0.55f, c.Y + s * 0.3f, s * 0.85f, s * 0.26f), FieldGold.Darkened(0.12f) with { A = 0.85f });
                DrawHut(c + new Vector2(s * 0.45f, -s * 0.3f), s * 0.5f);
                break;
            case SiteType.MarketVillage:
                DrawHut(c + new Vector2(-s * 0.35f, 0), s * 0.55f);
                DrawHut(c + new Vector2(s * 0.3f, s * 0.2f), s * 0.45f);
                DrawHut(c + new Vector2(0, -s * 0.38f), s * 0.4f);
                DrawRect(new Rect2(c.X + s * 0.05f, c.Y + s * 0.42f, s * 0.4f, s * 0.16f), Thatch);
                break;
            case SiteType.FishingDock:
                // Planks running out over the water, a hut on the shore end.
                DrawLine(c + new Vector2(-s * 0.2f, -s * 0.05f), c + new Vector2(s * 0.7f, s * 0.25f), Timber, Mathf.Max(1.6f, s * 0.16f));
                DrawLine(c + new Vector2(-s * 0.2f, s * 0.18f), c + new Vector2(s * 0.55f, s * 0.45f), Timber.Darkened(0.12f), Mathf.Max(1.4f, s * 0.13f));
                DrawHut(c + new Vector2(-s * 0.42f, 0), s * 0.5f);
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
        int places = World.Sites.ForRegion(r.Id).Count;
        string l1 = r.Name;
        string l2 = $"{r.TerrainType} · {holder} · {places} place{(places == 1 ? "" : "s")}";
        float w = Mathf.Max(font.GetStringSize(l1, HorizontalAlignment.Center, -1, 14).X,
                            font.GetStringSize(l2, HorizontalAlignment.Center, -1, 11).X);
        var rect = new Rect2(c.X - w / 2f - 8, c.Y - regionR - 46, w + 16, 38);
        LabelTag.Draw(GetCanvasItem(), rect);
        DrawString(font, new Vector2(rect.Position.X + 8, rect.Position.Y + 16), l1,
            HorizontalAlignment.Center, w, 14, Ui.Parchment);
        DrawString(font, new Vector2(rect.Position.X + 8, rect.Position.Y + 31), l2,
            HorizontalAlignment.Center, w, 11, Ui.RowBorder);
    }

    // 8a'. site name tags: each real place names itself once the lens is close enough
    // (SiteTagZoom), and the inspected land's places are always named — the Region Lens
    // lists them, so the map must answer where they stand. Overlap-skip keeps it legible:
    // a missing tag beats two unreadable ones.
    private void DrawSiteTags(List<Rect2> placed)
    {
        bool showAll = _zoom >= SiteTagZoom;
        foreach (var (pos, r, id) in _siteScreen)
        {
            var site = World!.Sites.Get(id);
            bool sel = site.Id == SelectedSiteId;
            bool inLens = site.RegionId == SelectedRegionId;
            if (!sel && !showAll && !inLens) continue;
            string name = site.Name;
            string sub = SiteIndex.TypeLabel(site.Type);
            float w = Mathf.Max(Ui.SerifBold.GetStringSize(name, HorizontalAlignment.Left, -1, 11).X,
                                Ui.SmallCaps.GetStringSize(sub, HorizontalAlignment.Left, -1, 9).X);
            var rect = new Rect2(pos.X - w / 2f - 6, pos.Y + r + 2, w + 12, 28);
            if (!sel)
            {
                if (placed.Any(p => p.Intersects(rect))) rect.Position += new Vector2(0, 30);
                if (placed.Any(p => p.Intersects(rect))) continue;
            }
            placed.Add(rect);
            (sel ? PlaceTagSelected : PlaceTag).Draw(GetCanvasItem(), rect);
            DrawString(Ui.SerifBold, rect.Position + new Vector2(6, 12), name,
                HorizontalAlignment.Left, -1, 11, Ui.InkDeep);
            DrawString(Ui.SmallCaps, rect.Position + new Vector2(6, 24), sub,
                HorizontalAlignment.Left, -1, 9, Ui.Faded);
        }
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
            // The blessed mark: a thin steady pale-gold ring — quieter than the breathing
            // follow halo, paler than the leader's LensGold, deliberately apart from both.
            if (p.Blessed)
                DrawArc(pos, r + (p.IsLeader ? 4.4f : 2.6f), 0, Mathf.Tau, 20,
                        new Color("f2e2b0") with { A = 0.85f }, 1.3f);
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
            // Divine pressure as restrained cloth: a thin gold thread under a protected
            // people's tag, an ember one under a doomed people's. Honest state, never paint.
            var under = new Vector2(rect.Position.X + 4, rect.End.Y - 2);
            if (fac.ProtectUntilYear > World.Year)
                DrawLine(under, under + new Vector2(rect.Size.X - 8, 0), Ui.Gold with { A = 0.85f }, 1.6f);
            else if (fac.DoomUntilYear > World.Year)
                DrawLine(under, under + new Vector2(rect.Size.X - 8, 0), Ui.Ember with { A = 0.85f }, 1.6f);
        }
        return placed;
    }

    // ---- the living-atlas skin ----

    // Warm terrain palette per DESIGN.md guardrails (moss, dry grass, clay, stone, faded
    // slate water): one base color per terrain class, hash-speckled per texel so the land
    // reads as handmade pixel ground, never flat fill.
    private static readonly Color TerForest = new("3f5230");
    private static readonly Color TerForestDeep = new("36482a");
    private static readonly Color TerPlains = new("5d5e38");
    private static readonly Color TerHighland = new("6a665a");
    private static readonly Color TerWetlandC = new("495843");
    private static readonly Color TerRiver = new("3a6a74");
    private static readonly Color TerCoastSand = new("6b6a48");

    private static Color TerrainColor(SurfaceTerrain t) => t switch
    {
        SurfaceTerrain.Ocean => Sea,
        SurfaceTerrain.Shallows => Shallows,
        SurfaceTerrain.Coast => TerCoastSand,
        SurfaceTerrain.Plains => TerPlains,
        SurfaceTerrain.Forest => TerForest,
        SurfaceTerrain.Highland => TerHighland,
        SurfaceTerrain.Wetland => TerWetlandC,
        SurfaceTerrain.River => TerRiver,
        SurfaceTerrain.Lake => TerRiver,
        _ => Land,
    };

    private static float Speckle(int x, int y)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xff) / 255f - 0.5f;
        }
    }

    private string TerritorySignature()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var r in World!.Regions) sb.Append(r.ControllingFactionId ?? ".").Append('|');
        return sb.ToString();
    }

    // Rebuild the skin only when terrain was terraformed (Surface.Version) or a region
    // changed hands. 192×192 texels — sub-millisecond, and never on the steady frame path.
    private void EnsureSurfaceTexture()
    {
        var surface = World!.Surface;
        string sig = TerritorySignature();
        if (_surfaceTex is not null && _texSurfaceVersion == surface.Version && _texTerritorySig == sig)
            return;
        _texSurfaceVersion = surface.Version;
        _texTerritorySig = sig;

        const int TS = 2;   // texels per cell
        int S = WorldSurface.Size * TS;
        var img = Image.CreateEmpty(S, S, false, Image.Format.Rgba8);
        for (int cy = 0; cy < WorldSurface.Size; cy++)
            for (int cx = 0; cx < WorldSurface.Size; cx++)
            {
                var t = surface.TerrainAt(cx, cy);
                var baseCol = TerrainColor(t);
                float elev = surface.ElevationAt(cx, cy);
                int rid = surface.RegionAt(cx, cy);
                Color? cloth = null;
                if (rid >= 0 && rid < World.Regions.Count
                    && World.Regions[rid].ControllingFactionId is string fid
                    && t is not (SurfaceTerrain.River or SurfaceTerrain.Lake))
                    cloth = FactionColors.GetValueOrDefault(fid, Neutral);

                for (int sy = 0; sy < TS; sy++)
                    for (int sx = 0; sx < TS; sx++)
                    {
                        int px = cx * TS + sx, py = cy * TS + sy;
                        // Forest speckles in two tones (clustered canopy, not flat green);
                        // everything else gets a soft handmade grain + elevation shading.
                        var col = t == SurfaceTerrain.Forest && Speckle(px * 7, py * 5) > 0.12f
                            ? TerForestDeep : baseCol;
                        float shade = 1f + Math.Clamp(elev - 0.18f, -0.2f, 0.6f) * 0.22f
                                      + Speckle(px, py) * 0.085f;
                        col = new Color(col.R * shade, col.G * shade, col.B * shade);
                        if (cloth is Color cc) col = col.Lerp(cc, 0.13f);   // banner-cloth wash, restrained
                        img.SetPixel(px, py, col);
                    }
            }
        _surfaceTex = ImageTexture.CreateFromImage(img);
    }

    /// <summary>Inverse of the draw transform P — screen position back to normalized map space.</summary>
    private Vector2 ScreenToNorm(Vector2 pos)
    {
        float side = MapSide();
        var origin = new Vector2((Size.X - side) / 2f, (Size.Y - side) / 2f);
        var center = Size / 2f;
        var b = (pos - center - _pan) / _zoom + center;
        return (b - origin) / side;
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
        // While the replay path owns the stage, its numbered marks are the click targets.
        if (ReplayActive)
        {
            foreach (var (mp, mr, number) in _replayScreen)
                if (pos.DistanceTo(mp) <= mr + 3) { ReplayBeatPicked?.Invoke(number); return; }
        }

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

        // A site marker beats the land beneath it: places are now real click targets.
        int siteBest = -1;
        float siteD = float.MaxValue;
        foreach (var sd in _siteScreen)
        {
            float dist = pos.DistanceTo(sd.pos);
            if (dist <= sd.r + 3 && dist < siteD) { siteD = dist; siteBest = sd.id; }
        }
        if (siteBest >= 0 && SitePicked is not null) { SitePicked(siteBest); return; }

        // A turning-point pulse beats the land beneath it — the pivot asks to be read.
        foreach (var (tp, tr, teid) in _turningScreen)
            if (pos.DistanceTo(tp) <= tr + 3 && TurningPicked is not null) { TurningPicked(teid); return; }

        int region = NearestRegion(pos);
        if (region >= 0) RegionPicked?.Invoke(region);
    }

    // Land clicks resolve through the surface itself: the cell under the cursor names its
    // region (the WorldSurface bridge) — terrain is the hit target now, not abstract circles.
    private int NearestRegion(Vector2 pos)
    {
        if (World is null) return -1;
        var n = ScreenToNorm(pos);
        return World.Surface.RegionAtNorm(n.X, n.Y);
    }
}
