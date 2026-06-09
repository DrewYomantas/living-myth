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

    private const float RegionRadiusNorm = 0.072f;

    private static readonly Color Sea = new("0e2230");
    private static readonly Color Land = new("23302a");
    private static readonly Color Neutral = new("55665b");          // unclaimed wilderness
    private static readonly Dictionary<string, Color> FactionColors = new()
    {
        ["highland"] = new Color("6b7a99"),
        ["shore"] = new Color("3aa6a0"),
        ["wood"] = new Color("5a9e57"),
    };

    public override void _Ready() => MouseFilter = MouseFilterEnum.Stop;

    public void PulseRegion(int regionId) => _regionPulses[regionId] = PulseDuration;

    public override void _Process(double delta)
    {
        if (_regionPulses.Count == 0) return;
        foreach (var id in _regionPulses.Keys.ToList())
        {
            float v = _regionPulses[id] - (float)delta;
            if (v <= 0f) _regionPulses.Remove(id);
            else _regionPulses[id] = v;
        }
        // Main redraws the map every frame; no QueueRedraw needed here.
    }

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

        // Normalized [0,1] -> screen, fitting a centered square so the island never distorts.
        const float pad = 18f;
        float side = Mathf.Min(Size.X, Size.Y) - pad * 2f;
        var origin = new Vector2((Size.X - side) / 2f, (Size.Y - side) / 2f);
        Vector2 P(float nx, float ny) => origin + new Vector2(nx, ny) * side;
        float regionR = RegionRadiusNorm * side;

        BuildIsland();
        if (_islandNorm is not null)
        {
            var poly = _islandNorm.Select(p => P(p.X, p.Y)).ToArray();
            DrawColoredPolygon(poly, Land);
            DrawPolyline(poly.Append(poly[0]).ToArray(), new Color("33463c"), 2f, true);
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
            DrawCircle(c, regionR, col with { A = r.ControllingFactionId is null ? 0.30f : 0.48f });
            DrawArc(c, regionR, 0, Mathf.Tau, 40, col with { A = 0.8f }, _hoverRegion == r.Id ? 3f : 1.5f);
            if (_regionPulses.TryGetValue(r.Id, out var pulse))
            {
                float t = pulse / PulseDuration;                   // 1 -> 0 over the pulse's life
                float ring = regionR * (1f + (1f - t) * 0.8f);     // expands outward as it fades
                DrawArc(c, ring, 0, Mathf.Tau, 48, new Color(1f, 0.85f, 0.3f, t * 0.9f), 3f);
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
                HorizontalAlignment.Center, -1, 14, modulate: Colors.White);
            DrawString(font, c + new Vector2(0, -regionR + 8), $"{r.TerrainType} · {holder}",
                HorizontalAlignment.Center, -1, 11, modulate: new Color("b7c3cb"));
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

            float r = p.IsLeader ? 6.5f : 3.8f;
            var dot = p.Cursed ? new Color("d24a64") : (p.Sex == "f" ? col.Lightened(0.28f) : col);
            DrawCircle(pos, r, dot);
            if (p.IsLeader) DrawArc(pos, r + 2.5f, 0, Mathf.Tau, 20, new Color("ffd54a"), 1.6f);
            if (Marked is not null && Marked.Contains(p.Id))
                DrawArc(pos, r + 4.5f, 0, Mathf.Tau, 24, new Color("5fd8ff"), 2f);
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
                HorizontalAlignment.Center, 120, 14, modulate: Colors.White);
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
            int hover = NearestRegion(mm.Position);
            if (hover != _hoverRegion) { _hoverRegion = hover; QueueRedraw(); }
            return;
        }
        if (@event is not InputEventMouseButton mb || !mb.Pressed || mb.ButtonIndex != MouseButton.Left)
            return;
        var pos = mb.Position;

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
