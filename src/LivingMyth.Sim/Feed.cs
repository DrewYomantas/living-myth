namespace LivingMyth.Sim;

/// <summary>One ranked headline: its total weight, the event, and why it surfaced.</summary>
public readonly record struct FeedRow(int Total, Event Ev, List<string> Labels);

/// <summary>
/// The feed: what the player actually hears about. A century makes hundreds of events,
/// mostly routine. The feed decides which few rise to a headline, from three sources —
/// LOUD (objectively big), YOURS (touches a marked person/bloodline/people), RISING (still
/// causing more) — plus a nudge for anything in a detected Myth Echo. The editorial layer.
/// </summary>
public static class Feed
{
    private static HashSet<int> Bloodline(World world, int pid, HashSet<int>? seen = null)
    {
        seen ??= new();
        if (seen.Contains(pid) || !world.People.ContainsKey(pid)) return seen;
        seen.Add(pid);
        var p = world.People[pid];
        foreach (var rel in p.Parents.Concat(p.Children).ToList())
            Bloodline(world, rel, seen);
        return seen;
    }

    /// <summary>Turn marked people into their whole bloodline; keep marked faction ids.</summary>
    public static (HashSet<int> people, HashSet<string> factions) ExpandMarked(
        World world, IEnumerable<int> markedPeople, IEnumerable<string> markedFactions)
    {
        var peopleIds = new HashSet<int>();
        foreach (var m in markedPeople)
            if (world.People.ContainsKey(m))
                peopleIds.UnionWith(Bloodline(world, m));
        var factionIds = new HashSet<string>(markedFactions.Where(f => world.Factions.ContainsKey(f)));
        return (peopleIds, factionIds);
    }

    public static List<FeedRow> BuildFeed(World world, IEnumerable<int>? markedPeople = null,
        IEnumerable<string>? markedFactions = null, List<Echo>? echoes = null, int limit = 28)
    {
        markedPeople ??= Enumerable.Empty<int>();
        markedFactions ??= Enumerable.Empty<string>();
        echoes ??= new();
        var reverse = Scoring.BuildReverse(world);
        var (markedPpl, markedFac) = ExpandMarked(world, markedPeople, markedFactions);

        var echoOf = new Dictionary<int, string>();
        foreach (var echo in echoes)
            foreach (var eid in echo.EventIds)
                if (!echoOf.ContainsKey(eid)) echoOf[eid] = echo.Archetype;

        var scored = new List<FeedRow>();
        foreach (var e in world.Chronicle.Events)
        {
            int loud = Scoring.Importance(e, world, reverse);
            if (echoOf.ContainsKey(e.Id)) loud += 45;

            int yours = 0;
            bool touchesMark = e.Participants.Any(pid => markedPpl.Contains(pid))
                || e.Participants.Any(pid => world.People.TryGetValue(pid, out var p) && markedFac.Contains(p.FactionId));
            if (touchesMark) yours = 70;

            int rising = Math.Min((reverse.GetValueOrDefault(e.Id)?.Count ?? 0) * 6, 36);
            int total = loud + yours + rising;

            var labels = new List<string>();
            if (echoOf.TryGetValue(e.Id, out var arch)) labels.Add("ECHO:" + arch);
            if (yours > 0) labels.Add("YOURS");
            if (rising >= 18 && total >= 60) labels.Add("RISING");
            if (loud >= 70 && !labels.Contains("YOURS")) labels.Add("LOUD");
            if (labels.Count == 0) labels.Add("LOUD");
            scored.Add(new FeedRow(total, e, labels));
        }

        var top = scored.OrderByDescending(r => r.Total).Take(limit).ToList();
        top.Sort((a, b) => a.Ev.Year.CompareTo(b.Ev.Year));   // read in time order
        return top;
    }

    /// <summary>Echoes are noisy too; surface only the legendary few, by the importance of
    /// the events that compose them.</summary>
    public static List<Echo> RankEchoes(World world, List<Echo> echoes, int limit = 12)
    {
        var reverse = Scoring.BuildReverse(world);
        var byId = world.Chronicle.Events.ToDictionary(e => e.Id);
        var scored = echoes.Select(echo =>
        {
            var evs = echo.EventIds.Where(byId.ContainsKey).Select(i => byId[i]).ToList();
            int s = evs.Count > 0 ? evs.Max(e => Scoring.Importance(e, world, reverse)) : 0;
            return (s, echo);
        }).ToList();
        var outp = scored.OrderByDescending(t => t.s).Take(limit).Select(t => t.echo).ToList();
        outp.Sort((a, b) => a.YearSpan.First.CompareTo(b.YearSpan.First));
        return outp;
    }
}
