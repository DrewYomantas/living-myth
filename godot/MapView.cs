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
    public Action<int>? PersonPicked;
    public Action<string>? FactionPicked;
    public Action<int>? RegionPicked;       // region id — Main decides faction vs. unclaimed

    private readonly List<(Vector2 pos, float r, int id)> _dots = new();
    private readonly List<(Vector2 pos, float r, int id)> _regionHits = new();
    private List<Vector2>? _islandNorm;     // island outline in normalized [0,1] space, built once
    private int _hoverRegion = -1;

    // Transient gold rings on regions where a notable event just landed — pure rendering, aged
    // off real time. region id -> seconds remaining.
    private readonly Dictionary<int, float> _regionPulses = new();
    private const float PulseDuration = 1.2f;

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

    // Old-world palette (V2 handoff): faded slate water, moss/dry-grass land, muted banner
    // colors. Faction color is a cloth/banner accent — territory paint stays restrained.
    private static readonly Color Sea = new("1f3340");
    private static readonly Color Land = new("3e4733");
    private static readonly Color Coast = new("55604a");
    private static readonly Color Neutral = new("6f6a58");          // unclaimed wilderness
    private static readonly Dictionary<string, Color> FactionColors = new()
    {
        ["highland"] = new Color("6b7a99"),
        ["shore"] = new Color("4f8f89"),
        ["wood"] = new Color("5d8a4e"),
    };

    public override void _Ready() => MouseFilter = MouseFilterEnum.Stop;

    public void PulseRegion(int regionId)
    {
        _regionPulses[regionId] = PulseDuration;
        if (CameraFollow && _manualCamCooldown <= 0f) _easeTargetRegion = regionId;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (_manualCamCooldown > 0f) _manualCamCooldown -= dt;

        foreach (var id in _regionPulses.Keys.ToList())
        {
            float v = _regionPulses[id] - dt;
            if (v <= 0f) _regionPulses.Remove(id);
            else _regionPulses[id] = v;
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

    public override void _Draw()
    {
        _dots.Clear();
        _regionHits.Clear();
        DrawRect(new Rect2(Vector2.Zero, Size), Sea);
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

        BuildIsland();
        if (_islandNorm is not null)
        {
            var poly = _islandNorm.Select(p => P(p.X, p.Y)).ToArray();
            DrawColoredPolygon(poly, Land);
            DrawPolyline(poly.Append(poly[0]).ToArray(), Coast, 2f, true);
        }

        // Faint adjacency graph beneath the territories — the connective tissue of the island.
        foreach (var a in World.Regions)
            foreach (var bid in a.AdjacentRegionIds)
                if (bid > a.Id)
                    DrawLine(P(a.X, a.Y), P(World.Regions[bid].X, World.Regions[bid].Y),
                             new Color(1, 1, 1, 0.07f), 1f);

        // Territories.
        foreach (var r in World.Regions)
        {
            var c = P(r.X, r.Y);
            var col = RegionColor(r);
            // Muted territorial hints, not giant faction paint: a soft tint plus a thin ring.
            DrawCircle(c, regionR, col with { A = r.ControllingFactionId is null ? 0.18f : 0.30f });
            DrawArc(c, regionR, 0, Mathf.Tau, 40, col with { A = 0.65f }, _hoverRegion == r.Id ? 3f : 1.5f);
            if (_regionPulses.TryGetValue(r.Id, out var pulse))
            {
                float t = pulse / PulseDuration;                   // 1 -> 0 over the pulse's life
                float ring = regionR * (1f + (1f - t) * 0.8f);     // expands outward as it fades
                DrawArc(c, ring, 0, Mathf.Tau, 48, new Color(0.96f, 0.78f, 0.43f, t * 0.9f), 3f);
            }
            _regionHits.Add((c, regionR, r.Id));
        }

        DrawPeople(P, regionR, font);
        DrawFactionLabels(P, font);

        // Hover tooltip: region name + who holds it.
        if (_hoverRegion >= 0 && _hoverRegion < World.Regions.Count)
        {
            var r = World.Regions[_hoverRegion];
            var c = P(r.X, r.Y);
            string holder = r.ControllingFactionId is string fid ? World.Factions[fid].Name : "unclaimed";
            DrawString(font, c + new Vector2(0, -regionR - 8), $"{r.Name}",
                HorizontalAlignment.Center, -1, 14, modulate: new Color("f2e5c2"));
            DrawString(font, c + new Vector2(0, -regionR + 8), $"{r.TerrainType} · {holder}",
                HorizontalAlignment.Center, -1, 11, modulate: new Color("c9b288"));
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
            var dot = p.Cursed ? new Color("b0432e") : (p.Sex == "f" ? col.Lightened(0.28f) : col);
            DrawCircle(pos, r, dot);
            if (p.IsLeader) DrawArc(pos, r + 2.5f, 0, Mathf.Tau, 20, new Color("d8a843"), 1.6f);
            if (Marked is not null && Marked.Contains(p.Id))
                DrawArc(pos, r + 4.5f, 0, Mathf.Tau, 24, new Color("7fc8d8"), 2f);
            _dots.Add((pos, Mathf.Max(r, 6f), p.Id));
        }
    }

    private void DrawFactionLabels(Func<float, float, Vector2> P, Font font)
    {
        foreach (var f in World!.Config.Factions)
        {
            var fac = World.Factions[f.Id];
            var held = World.Regions.Where(r => r.ControllingFactionId == f.Id).ToList();
            if (held.Count == 0) continue;

            var centroid = P(held.Average(r => r.X), held.Average(r => r.Y));
            int pop = fac.Members.Count;
            string leader = fac.LeaderId is int lid ? World.People[lid].Name : "(none)";
            var col = FactionColors.GetValueOrDefault(f.Id, Neutral);
            DrawString(font, centroid + new Vector2(-60, -2), fac.Name,
                HorizontalAlignment.Center, 120, 14, modulate: new Color("f2e5c2"));
            DrawString(font, centroid + new Vector2(-60, 15), $"pop {pop} · {leader}",
                HorizontalAlignment.Center, 120, 11, modulate: col.Lightened(0.35f));
        }
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
        if (bestId >= 0) { PersonPicked?.Invoke(bestId); return; }

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
