namespace LivingMyth.Sim;

/// <summary>
/// How important is an event? One number per event, built from what kind it is, who it
/// touched, how tangled in cause and effect, and how much it went on to cause. The feed and
/// catch-up both lean on this one engine.
/// </summary>
public static class Scoring
{
    public static readonly Dictionary<string, int> TypeWeight = new()
    {
        ["founding"] = 60, ["divine"] = 80, ["war"] = 70, ["peace"] = 45,
        ["famine"] = 55, ["boom"] = 50, ["martyr"] = 50, ["murder"] = 40,
        ["prophet"] = 38, ["schism"] = 36, ["trade"] = 30, ["justice"] = 30,
        ["succession"] = 30, ["leadership"] = 25, ["scandal"] = 20,
        ["custom"] = 28, ["romance"] = 16, ["friction"] = 12, ["marriage"] = 8, ["death"] = 5, ["birth"] = 3,
    };

    public static readonly Dictionary<string, int> TagBonus = new()
    {
        ["regicide"] = 35, ["curse"] = 40, ["martyr"] = 30, ["holy"] = 25,
        ["tragedy"] = 22, ["schism"] = 20, ["heresy"] = 18, ["forbidden"] = 16,
        ["revenge"] = 15, ["prophet"] = 14, ["persecution"] = 12, ["cross-faction"] = 10,
        ["peace"] = 8, ["religion"] = 5, ["scarcity"] = 15,
        ["clash"] = 14, ["fade"] = 8, ["culture"] = 6, ["diffusion"] = 6,
    };

    /// <summary>event id -> ids of events that name it as a cause (its consequences).</summary>
    public static Dictionary<int, List<int>> BuildReverse(World world)
    {
        var rev = new Dictionary<int, List<int>>();
        foreach (var e in world.Chronicle.Events)
            foreach (var c in e.Causes)
            {
                if (!rev.TryGetValue(c, out var list)) { list = new(); rev[c] = list; }
                list.Add(e.Id);
            }
        return rev;
    }

    /// <summary>A cheaper importance for live streaming: skips the causal-trace-depth term
    /// (which rebuilds an index per call and is O(events)) and takes a precomputed
    /// consequence-count map instead of a full reverse index, so callers can maintain it
    /// incrementally. Keeps type/tag/leader/consequence weight so headlines still rank well.</summary>
    public static int ImportanceFast(Event ev, World world, IReadOnlyDictionary<int, int> consequenceCounts)
    {
        int score = TypeWeight.GetValueOrDefault(ev.Type, 10);
        foreach (var t in ev.Tags) score += TagBonus.GetValueOrDefault(t, 0);

        var factionsTouched = new HashSet<string>();
        foreach (var pid in ev.Participants)
        {
            if (!world.People.TryGetValue(pid, out var p)) continue;
            factionsTouched.Add(p.FactionId);
            if (p.EverLeader) score += 18;
        }
        if (factionsTouched.Count > 1) score += 10;
        score += Math.Min(consequenceCounts.GetValueOrDefault(ev.Id) * 4, 40);
        return score;
    }

    public static int Importance(Event ev, World world, Dictionary<int, List<int>> reverse)
    {
        int score = TypeWeight.GetValueOrDefault(ev.Type, 10);
        foreach (var t in ev.Tags) score += TagBonus.GetValueOrDefault(t, 0);

        var factionsTouched = new HashSet<string>();
        foreach (var pid in ev.Participants)
        {
            if (!world.People.TryGetValue(pid, out var p)) continue;
            factionsTouched.Add(p.FactionId);
            if (p.EverLeader) score += 18;
        }
        if (factionsTouched.Count > 1) score += 10;

        score += Math.Min(world.Chronicle.Trace(ev.Id).Count * 2, 40);
        score += Math.Min((reverse.GetValueOrDefault(ev.Id)?.Count ?? 0) * 4, 40);
        return score;
    }
}
