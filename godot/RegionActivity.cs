using System.Collections.Generic;
using LivingMyth.Sim;

// Viewer read-model for the Region Lens: which chronicle events are anchored to each region.
// The sim already stamps Event.RegionId where it knows the place (territory changes exactly;
// culture/rumor events at the faction's primary region) and Event.HomeRegionId where a life
// event is remembered (births/deaths/murders at the lineage's home root) — this just
// indexes both truths incrementally as events stream in, O(new events) per tick, never a
// history scan. Read-only over sim data; lives entirely in the viewer.
public sealed class RegionActivity
{
    private const int KeepPerRegion = 6;
    private readonly Dictionary<int, List<int>> _recent = new();   // region id -> event ids, oldest..newest
    private readonly Dictionary<int, int> _total = new();

    // Home-memory channel: life events remembered at a lineage's home (Event.HomeRegionId).
    // Kept strictly apart from the place channel above — home memory is never "it happened here".
    private readonly Dictionary<int, List<int>> _homeRecent = new();
    private readonly Dictionary<int, int> _homeTotal = new();

    // Site-memory channel (Event.SiteId, the anchoring conventions V1): events that truly
    // belong to one modeled place. A site-anchored event is ALSO in its region's channel —
    // the site channel narrows, never replaces. Kind tallies feed the honest "known for"
    // line: counts of real recorded events only, never flavor.
    private const int KeepPerSite = 6;
    private readonly Dictionary<int, List<int>> _siteRecent = new();
    private readonly Dictionary<int, int> _siteTotal = new();
    private readonly Dictionary<int, Dictionary<string, int>> _siteKinds = new();

    public void Observe(Event e)
    {
        if (e.RegionId is int rid)
        {
            _total[rid] = _total.GetValueOrDefault(rid) + 1;
            if (!_recent.TryGetValue(rid, out var list)) { list = new(); _recent[rid] = list; }
            list.Add(e.Id);
            if (list.Count > KeepPerRegion) list.RemoveAt(0);
        }
        if (e.HomeRegionId is int hid)
        {
            _homeTotal[hid] = _homeTotal.GetValueOrDefault(hid) + 1;
            if (!_homeRecent.TryGetValue(hid, out var list)) { list = new(); _homeRecent[hid] = list; }
            list.Add(e.Id);
            if (list.Count > KeepPerRegion) list.RemoveAt(0);
        }
        if (e.SiteId is int sid)
        {
            _siteTotal[sid] = _siteTotal.GetValueOrDefault(sid) + 1;
            if (!_siteRecent.TryGetValue(sid, out var list)) { list = new(); _siteRecent[sid] = list; }
            list.Add(e.Id);
            if (list.Count > KeepPerSite) list.RemoveAt(0);
            if (!_siteKinds.TryGetValue(sid, out var kinds)) { kinds = new(); _siteKinds[sid] = kinds; }
            string kind = KindKey(e);
            kinds[kind] = kinds.GetValueOrDefault(kind) + 1;
        }
    }

    // The authored kind buckets behind "known for" — derived from recorded type+tags only.
    private static string KindKey(Event e) => e.Type switch
    {
        "territory" when e.Tags.Contains("founding") => "founding",
        "territory" when e.Tags.Contains("war") => "war",
        "territory" when e.Tags.Contains("abandonment") => "abandonment",
        "custom" when e.Tags.Contains("fade") => "ways-shed",
        "custom" => "ways-sworn",
        _ => e.Type,
    };

    public int TotalFor(int regionId) => _total.GetValueOrDefault(regionId);

    public IReadOnlyList<int> RecentFor(int regionId)
        => _recent.TryGetValue(regionId, out var list) ? list : System.Array.Empty<int>();

    public int HomeTotalFor(int regionId) => _homeTotal.GetValueOrDefault(regionId);

    public IReadOnlyList<int> HomeRecentFor(int regionId)
        => _homeRecent.TryGetValue(regionId, out var list) ? list : System.Array.Empty<int>();

    public int SiteTotalFor(int siteId) => _siteTotal.GetValueOrDefault(siteId);

    public IReadOnlyList<int> SiteRecentFor(int siteId)
        => _siteRecent.TryGetValue(siteId, out var list) ? list : System.Array.Empty<int>();

    /// <summary>Real recorded-event counts per authored kind bucket — the "known for" data.</summary>
    public IReadOnlyDictionary<string, int> SiteKindsFor(int siteId)
        => _siteKinds.TryGetValue(siteId, out var kinds) ? kinds : Empty;
    private static readonly Dictionary<string, int> Empty = new();
}
