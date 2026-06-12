namespace LivingMyth.Sim;

/// <summary>
/// Sites V1 — the local place layer: 3–7 named, terrain-honest sites per region, generated
/// once at world seed from the PRISTINE surface. This is a SITE READ-MODEL, not yet a sim
/// contract: the tick never reads a site, no event carries a SiteId (deliberately deferred
/// — the `sites` gate asserts the field does not exist), and no site claims population,
/// buildings, resources, or activity. What a site honestly is: a stable id, a name, a
/// type chosen from the land it stands on, and a real surface cell inside its region.
///
/// Baseline-inert by construction, like WorldSurface: generation uses pure FNV-style
/// coordinate hashes (zero Rng draws), runs off seed + regions + the freshly generated
/// surface (World builds the index before any terraform edit can exist), and is proven
/// deterministic by the `sites` console gate. Holding is never stored — a site's holder
/// is derived live from its region's ControllingFactionId, so it can't go stale.
/// </summary>
public enum SiteType
{
    MarketVillage, WatchPost, SacredGrove, OldBarrow, RiverFord, Farmstead,
    HillFort, FishingDock, Shrine, CairnField, WildernessCamp,
}

public sealed class Site
{
    public int Id { get; }            // global, stable: generation order (regions by id, slots in order)
    public int RegionId { get; }
    public string Name { get; }
    public SiteType Type { get; }
    public int CellX { get; }
    public int CellY { get; }
    /// <summary>Normalized map position — the cell's centre, the same space Region.X/Y live in.</summary>
    public float Nx => (CellX + 0.5f) / WorldSurface.Size;
    public float Ny => (CellY + 0.5f) / WorldSurface.Size;
    /// <summary>True for the region's first site — the seat-like place nearest the region's heart.</summary>
    public bool IsSeat { get; }

    public Site(int id, int regionId, string name, SiteType type, int cellX, int cellY, bool isSeat)
    {
        Id = id;
        RegionId = regionId;
        Name = name;
        Type = type;
        CellX = cellX;
        CellY = cellY;
        IsSeat = isSeat;
    }
}

public sealed class SiteIndex
{
    private readonly List<Site> _all = new();
    private readonly List<List<Site>> _byRegion;

    public IReadOnlyList<Site> All => _all;
    public Site Get(int id) => _all[id];   // ids are list indexes, like chronicle events
    public IReadOnlyList<Site> ForRegion(int regionId)
        => regionId >= 0 && regionId < _byRegion.Count ? _byRegion[regionId] : Array.Empty<Site>();
    /// <summary>The region's seat-like site (every region with any land cell has one).</summary>
    public Site? SeatOf(int regionId)
        => regionId >= 0 && regionId < _byRegion.Count && _byRegion[regionId].Count > 0
            ? _byRegion[regionId][0] : null;

    /// <summary>The honest holder of a site: whoever holds its region right now. Derived,
    /// never stored — control changes hands in war and the site must never lag it.</summary>
    public static string? HolderOf(World world, Site site)
        => site.RegionId >= 0 && site.RegionId < world.Regions.Count
            ? world.Regions[site.RegionId].ControllingFactionId : null;

    public static string TypeLabel(SiteType t) => t switch
    {
        SiteType.MarketVillage => "market village",
        SiteType.WatchPost => "watch post",
        SiteType.SacredGrove => "sacred grove",
        SiteType.OldBarrow => "old barrow",
        SiteType.RiverFord => "river ford",
        SiteType.Farmstead => "farmstead",
        SiteType.HillFort => "hill fort",
        SiteType.FishingDock => "fishing dock",
        SiteType.Shrine => "shrine",
        SiteType.CairnField => "cairn field",
        _ => "wilderness camp",
    };

    private static string PatternKey(SiteType t) => t switch
    {
        SiteType.MarketVillage => "market_village",
        SiteType.WatchPost => "watch_post",
        SiteType.SacredGrove => "sacred_grove",
        SiteType.OldBarrow => "old_barrow",
        SiteType.RiverFord => "river_ford",
        SiteType.Farmstead => "farmstead",
        SiteType.HillFort => "hill_fort",
        SiteType.FishingDock => "fishing_dock",
        SiteType.Shrine => "shrine",
        SiteType.CairnField => "cairn_field",
        _ => "wilderness_camp",
    };

    /// <summary>The determinism fingerprint the `sites` gate compares across runs.</summary>
    public string CanonString()
        => string.Join("\n", _all.Select(s =>
            $"{s.Id}|{s.RegionId}|{s.Name}|{s.Type}|{s.CellX},{s.CellY}|{(s.IsSeat ? "seat" : "-")}"));

    // FNV-1a + avalanche, the PlaceSeeds/WorldSurface family — never string.GetHashCode.
    private static uint Hash(int seed, int a, int b, int c = 0)
    {
        unchecked
        {
            uint h = 2166136261u;
            h = (h ^ (uint)seed) * 16777619u;
            h = (h ^ (uint)a) * 16777619u;
            h = (h ^ (uint)b) * 16777619u;
            h = (h ^ (uint)c) * 16777619u;
            h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
            return h;
        }
    }

    private static bool IsLand(SurfaceTerrain t)
        => t is SurfaceTerrain.Coast or SurfaceTerrain.Plains or SurfaceTerrain.Forest
             or SurfaceTerrain.Highland or SurfaceTerrain.Wetland;

    public SiteIndex(int seed, IReadOnlyList<Region> regions, WorldSurface surface, NamesData names)
    {
        _byRegion = new List<List<Site>>(regions.Count);
        for (int i = 0; i < regions.Count; i++) _byRegion.Add(new List<Site>());
        var usedNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var region in regions)   // id order — generation order IS the id order
        {
            // Every land cell bridged to this region, in cell-index order (deterministic).
            var cells = new List<(int cx, int cy)>();
            bool hasCoast = false, hasRiver = false;
            for (int cy = 0; cy < WorldSurface.Size; cy++)
                for (int cx = 0; cx < WorldSurface.Size; cx++)
                {
                    if (surface.RegionAt(cx, cy) != region.Id) continue;
                    var t = surface.TerrainAt(cx, cy);
                    if (!IsLand(t)) continue;
                    cells.Add((cx, cy));
                    if (t == SurfaceTerrain.Coast) hasCoast = true;
                    if (!hasRiver && TouchesWater(surface, cx, cy, river: true)) hasRiver = true;
                }
            if (cells.Count == 0) continue;   // a region drowned by the coastline keeps no sites — honest

            int want = Math.Min(3 + (int)(Hash(seed, region.Id, 1) % 5), cells.Count);   // 3..7
            var chosen = new List<(int cx, int cy)>();

            for (int slot = 0; slot < want; slot++)
            {
                SiteType type = slot == 0
                    ? SiteType.MarketVillage   // placeholder — the seat is typed from its own cell below
                    : PickType(seed, region, slot, hasCoast, hasRiver);
                var cell = PickCell(seed, region, surface, cells, chosen, type, slot);
                if (cell is not (int px, int py)) continue;
                // Honesty over intent: the type must match the cell it truly stands on.
                // The seat is typed from its land; a slot whose pick fell back to an
                // unfitting cell demotes to a camp (which any land honestly carries).
                if (slot == 0) type = SeatTypeFromCell(surface, px, py);
                else if (!FitsCell(surface, px, py, type)) type = SiteType.WildernessCamp;
                chosen.Add((px, py));
                string name = PickName(seed, region.Id, slot, type, names, usedNames);
                var site = new Site(_all.Count, region.Id, name, type, px, py, isSeat: slot == 0);
                _all.Add(site);
                _byRegion[region.Id].Add(site);
            }
        }
    }

    private static bool TouchesWater(WorldSurface s, int cx, int cy, bool river)
    {
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int x = cx + dx, y = cy + dy;
                if (x < 0 || x >= WorldSurface.Size || y < 0 || y >= WorldSurface.Size) continue;
                var t = s.TerrainAt(x, y);
                if (river && t is SurfaceTerrain.River or SurfaceTerrain.Lake) return true;
                if (!river && t is SurfaceTerrain.Shallows or SurfaceTerrain.Ocean) return true;
            }
        return false;
    }

    /// <summary>Slot 0 — the seat-like place at the region's heart, typed honestly from
    /// the cell it actually stands on (never from a label the ground contradicts).</summary>
    private static SiteType SeatTypeFromCell(WorldSurface surface, int cx, int cy)
        => surface.TerrainAt(cx, cy) switch
        {
            SurfaceTerrain.Forest => SiteType.SacredGrove,
            SurfaceTerrain.Highland => SiteType.HillFort,
            SurfaceTerrain.Coast => TouchesWater(surface, cx, cy, river: false)
                ? SiteType.FishingDock : SiteType.Farmstead,
            SurfaceTerrain.Plains => SiteType.MarketVillage,
            _ => SiteType.WildernessCamp,   // wetland
        };

    /// <summary>The per-cell honesty contract — exactly what the `sites` gate asserts.</summary>
    public static bool FitsCell(WorldSurface surface, int cx, int cy, SiteType type)
    {
        var t = surface.TerrainAt(cx, cy);
        return type switch
        {
            SiteType.FishingDock => t == SurfaceTerrain.Coast && TouchesWater(surface, cx, cy, river: false),
            SiteType.RiverFord => TouchesWater(surface, cx, cy, river: true),
            SiteType.SacredGrove => t == SurfaceTerrain.Forest,
            SiteType.WatchPost or SiteType.HillFort or SiteType.OldBarrow or SiteType.CairnField
                => surface.ElevationAt(cx, cy) >= 0.20f,
            SiteType.Farmstead or SiteType.MarketVillage
                => t is SurfaceTerrain.Plains or SurfaceTerrain.Coast,
            _ => IsLand(t),
        };
    }

    /// <summary>The terrain-honest type menus (per the Sites V1 contract): coast favors
    /// docks/shore camps/fords, forest groves/camps/shrines, highland posts/forts/barrows,
    /// plains farms/markets. Water-dependent types only where the water truly is.</summary>
    private static SiteType PickType(int seed, Region region, int slot, bool hasCoast, bool hasRiver)
    {
        var menu = new List<SiteType>(region.TerrainType switch
        {
            "coast" => new[] { SiteType.FishingDock, SiteType.WildernessCamp, SiteType.Farmstead,
                               SiteType.Shrine, SiteType.RiverFord, SiteType.CairnField },
            "forest" => new[] { SiteType.SacredGrove, SiteType.WildernessCamp, SiteType.Shrine,
                                SiteType.OldBarrow, SiteType.RiverFord },
            "highland" => new[] { SiteType.WatchPost, SiteType.HillFort, SiteType.OldBarrow,
                                  SiteType.CairnField, SiteType.Shrine },
            _ => new[] { SiteType.Farmstead, SiteType.MarketVillage, SiteType.WatchPost,
                         SiteType.RiverFord, SiteType.Shrine, SiteType.CairnField },
        });
        menu.RemoveAll(t => (t == SiteType.RiverFord && !hasRiver)
                         || (t == SiteType.FishingDock && !hasCoast));
        if (menu.Count == 0) menu.Add(SiteType.WildernessCamp);
        return menu[(int)(Hash(seed, region.Id, 10, slot) % (uint)menu.Count)];
    }

    /// <summary>Pick a real cell for the site, honestly matched to its type: docks on the
    /// shore, fords by the river, groves in the trees, forts and barrows on the heights,
    /// farms and markets on open low ground. The seat sits nearest the region's heart.
    /// Among candidates a stable hash picks; sites keep a little distance when they can.</summary>
    private static (int, int)? PickCell(int seed, Region region, WorldSurface surface,
        List<(int cx, int cy)> cells, List<(int cx, int cy)> taken, SiteType type, int slot)
    {
        bool Fits((int cx, int cy) c) => FitsCell(surface, c.cx, c.cy, type);
        bool Clear((int cx, int cy) c)
            => taken.All(o => Math.Max(Math.Abs(o.cx - c.cx), Math.Abs(o.cy - c.cy)) >= 3);

        if (slot == 0)
        {
            // The seat: the free cell nearest the region's heart (ties break on cell order).
            var (scx, scy) = WorldSurface.CellOf(region.X, region.Y);
            (int, int)? best = null;
            int bestD = int.MaxValue;
            foreach (var c in cells)
            {
                int d = (c.cx - scx) * (c.cx - scx) + (c.cy - scy) * (c.cy - scy);
                if (d < bestD) { bestD = d; best = c; }
            }
            return best;
        }

        (int, int)? Pick(Func<(int cx, int cy), bool> ok)
        {
            (int, int)? best = null;
            uint bestH = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                if (!ok(cells[i])) continue;
                uint h = Hash(seed, region.Id * 131 + slot, 100, i);
                if (best is null || h > bestH) { bestH = h; best = cells[i]; }
            }
            return best;
        }
        // want <= cells.Count, so an untaken cell always exists — two sites never share one.
        return Pick(c => Fits(c) && Clear(c))
            ?? Pick(c => Fits(c) && !taken.Contains(c))
            ?? Pick(Clear)
            ?? Pick(c => !taken.Contains(c));
    }

    /// <summary>Authored fragments (data/names.json) hashed into a name, unique across the
    /// island: a clash steps deterministically through roots, then earns a numeral.</summary>
    private static string PickName(int seed, int regionId, int slot, SiteType type,
                                   NamesData names, HashSet<string> used)
    {
        var roots = names.SiteNameRoots.Count > 0 ? names.SiteNameRoots : new List<string> { "Old" };
        var patterns = names.SitePatterns.GetValueOrDefault(PatternKey(type))
            ?? new List<string> { "{root} " + TypeLabel(type) };
        int r0 = (int)(Hash(seed, regionId, 50, slot) % (uint)roots.Count);
        int p0 = (int)(Hash(seed, regionId, 60, slot) % (uint)patterns.Count);
        for (int step = 0; step < roots.Count * patterns.Count; step++)
        {
            string root = roots[(r0 + step) % roots.Count];
            string pat = patterns[(p0 + step / roots.Count) % patterns.Count];
            string name = pat.Replace("{root}", root);
            if (used.Add(name)) return name;
        }
        for (int n = 2; ; n++)   // every combination taken — a numeral keeps it honest and unique
        {
            string name = $"{patterns[p0].Replace("{root}", roots[r0])} {n}";
            if (used.Add(name)) return name;
        }
    }
}
