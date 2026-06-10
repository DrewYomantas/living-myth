using System.Collections.Generic;
using LivingMyth.Sim;

// Viewer read-model for the Region Lens: which chronicle events are anchored to each region.
// The sim already stamps Event.RegionId where it knows the place (territory changes exactly;
// culture/rumor events at the faction's primary region; personal events not yet) — this just
// indexes that truth incrementally as events stream in, O(new events) per tick, never a
// history scan. Read-only over sim data; lives entirely in the viewer.
public sealed class RegionActivity
{
    private const int KeepPerRegion = 6;
    private readonly Dictionary<int, List<int>> _recent = new();   // region id -> event ids, oldest..newest
    private readonly Dictionary<int, int> _total = new();

    public void Observe(Event e)
    {
        if (e.RegionId is not int rid) return;
        _total[rid] = _total.GetValueOrDefault(rid) + 1;
        if (!_recent.TryGetValue(rid, out var list)) { list = new(); _recent[rid] = list; }
        list.Add(e.Id);
        if (list.Count > KeepPerRegion) list.RemoveAt(0);
    }

    public int TotalFor(int regionId) => _total.GetValueOrDefault(regionId);

    public IReadOnlyList<int> RecentFor(int regionId)
        => _recent.TryGetValue(regionId, out var list) ? list : System.Array.Empty<int>();
}
