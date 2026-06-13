namespace LivingMyth.Sim;

/// <summary>
/// Myth Echoes, the detection way. The simulation runs with no idea what Hamlet is. These
/// detectors read the finished chronicle afterward and notice when real events happen to
/// line up into a shape we recognize from old stories. They never make anything happen —
/// they just give the player (and the game) the "wait, this is basically a blood feud / a
/// doomed romance / a cursed line" jolt. Each Echo points at the real events that compose it.
/// </summary>
public sealed class Echo
{
    public string Archetype { get; }
    public string Label { get; }
    public List<int> EventIds { get; }
    public (int First, int Last) YearSpan { get; }

    public Echo(string archetype, string label, List<int> eventIds, (int, int) yearSpan)
    {
        Archetype = archetype;
        Label = label;
        EventIds = eventIds;
        YearSpan = yearSpan;
    }
}

public static class Echoes
{
    private static string SpanPhrase(int a, int b)
    {
        int n = b - a;
        if (n <= 0) return "within a single year";
        if (n == 1) return "over 1 year";
        return $"over {n} years";
    }

    private static Dictionary<int, Event> ById(World world)
        => world.Chronicle.Events.ToDictionary(e => e.Id);

    private static Dictionary<int, List<Event>> ReverseEvents(World world)
    {
        var rev = new Dictionary<int, List<Event>>();
        foreach (var e in world.Chronicle.Events)
            foreach (var c in e.Causes)
            {
                if (!rev.TryGetValue(c, out var list)) { list = new(); rev[c] = list; }
                list.Add(e);
            }
        return rev;
    }

    public static List<Echo> DetectCursedBloodline(World world)
    {
        if (world.CurseEvent is null) return new();
        var fallout = world.Chronicle.Events.Where(e => e.Causes.Contains(world.CurseEvent.Id)).ToList();
        if (fallout.Count < 3) return new();
        var target = world.People[world.CurseEvent.Participants[0]];
        var span = (world.CurseEvent.Year, fallout[^1].Year);
        string label = $"A curse on {target.Name}'s blood claimed {fallout.Count} lives {SpanPhrase(span.Item1, span.Item2)}.";
        var ids = new List<int> { world.CurseEvent.Id };
        ids.AddRange(fallout.Select(e => e.Id));
        return new() { new Echo("The Cursed Bloodline", label, ids, span) };
    }

    public static List<Echo> DetectBloodFeuds(World world)
    {
        var byid = ById(world);
        var murders = world.Chronicle.Events.Where(e => e.Type == "murder").ToList();

        Event RootOf(Event ev)
        {
            var cur = ev;
            while (true)
            {
                var murderCauses = cur.Causes.Where(c => byid.TryGetValue(c, out var ce) && ce.Type == "murder")
                                             .Select(c => byid[c]).ToList();
                if (murderCauses.Count == 0) return cur;
                cur = murderCauses[0];
            }
        }

        var groups = new Dictionary<int, HashSet<int>>();
        foreach (var m in murders)
        {
            var r = RootOf(m);
            if (!groups.TryGetValue(r.Id, out var set)) { set = new(); groups[r.Id] = set; }
            set.Add(m.Id);
            set.Add(r.Id);
        }

        var echoes = new List<Echo>();
        foreach (var (rootId, ids) in groups)
        {
            if (ids.Count < 3) continue;
            var members = ids.OrderBy(i => byid[i].Year).ToList();
            var span = (byid[members[0]].Year, byid[members[^1]].Year);
            string label = $"A blood feud of {members.Count} killings ran {SpanPhrase(span.Item1, span.Item2)}.";
            echoes.Add(new Echo("The Blood Feud", label, members, span));
        }
        return echoes;
    }

    public static List<Echo> DetectWarFromLove(World world)
    {
        var echoes = new List<Echo>();
        foreach (var war in world.Chronicle.Events.Where(e => e.Type == "war"))
        {
            var chain = world.Chronicle.Trace(war.Id);
            var romance = chain.FirstOrDefault(e => e.Tags.Contains("forbidden"));
            if (romance is null) continue;
            var ids = chain.Select(e => e.Id).ToList();
            var span = (romance.Year, war.Year);
            string ph = span.Item2 - span.Item1 > 0
                ? SpanPhrase(span.Item1, span.Item2).Replace("over", "").Trim() + " later"
                : "that same era";
            string label = $"A forbidden love in year {romance.Year} became a war {ph}.";
            echoes.Add(new Echo("A War Born of Forbidden Love", label, ids, span));
        }
        return echoes;
    }

    public static List<Echo> DetectHauntedHeir(World world)
    {
        var byid = ById(world);
        var rev = ReverseEvents(world);
        var echoes = new List<Echo>();
        foreach (var m in world.Chronicle.Events.Where(e => e.Type == "murder" && e.Tags.Contains("regicide")))
        {
            var consequences = rev.GetValueOrDefault(m.Id) ?? new();
            var succ = consequences.FirstOrDefault(e => e.Type == "succession");
            if (succ is null) continue;
            var ids = new List<int> { m.Id, succ.Id };
            var revenge = consequences.FirstOrDefault(e => e.Tags.Contains("revenge"));
            if (revenge is not null) ids.Add(revenge.Id);
            var span = (m.Year, byid[ids[^1]].Year);
            var victim = world.People.GetValueOrDefault(m.Participants[0]);
            var heir = world.People.GetValueOrDefault(succ.Participants[0]);
            string vname = victim?.Name ?? "a ruler";
            string hname = heir?.Name ?? "another";
            string label = $"{vname} was murdered for the throne; {hname} took the seat{(revenge is not null ? ", then paid for it in blood" : "")}.";
            echoes.Add(new Echo("The Haunted Heir", label, ids, span));
        }
        return echoes;
    }

    public static List<Echo> DetectPeopleErased(World world)
    {
        var echoes = new List<Echo>();
        foreach (var facId in world.Config.Factions.Select(f => f.Id))
        {
            var fac = world.Factions[facId];
            bool living = world.Living().Any(p => p.FactionId == fac.Id);
            bool ever = world.People.Values.Any(p => p.FactionId == fac.Id);
            if (living || !ever) continue;
            var deaths = world.Chronicle.Events.Where(e =>
                (e.Type == "death" || e.Type == "murder")
                && e.Participants.Any(pid => world.People.TryGetValue(pid, out var p) && p.FactionId == fac.Id)).ToList();
            var last = deaths.Count > 0 ? deaths[^1] : null;
            int year = last?.Year ?? world.Year;
            var ids = last is not null ? new List<int> { last.Id } : new List<int>();
            string label = $"{fac.Name} were wiped from the island by year {year}.";
            echoes.Add(new Echo("A People Erased", label, ids, (year, year)));
        }
        return echoes;
    }

    public static List<Echo> DetectFalseProphet(World world)
    {
        var echoes = new List<Echo>();
        var founders = new Dictionary<int, Religion>();
        foreach (var r in world.Religions.Values)
            if (r.FounderId is int fid) founders[fid] = r;
        var byid = ById(world);

        foreach (var ev in world.Chronicle.Events.Where(e => e.Type == "prophet"))
        {
            int prophetId = ev.Participants[0];
            var rel = founders.GetValueOrDefault(prophetId);
            var prophet = world.People.GetValueOrDefault(prophetId);
            if (rel is null || prophet is null) continue;
            int living = rel.Members.Count(pid => world.People.TryGetValue(pid, out var p) && p.Alive);
            bool martyred = !prophet.Alive && prophet.Murdered;
            var ids = new List<int> { ev.Id };
            if (martyred && prophet.MurderEventId is int me) ids.Add(me);
            string fate;
            if (martyred && living > 0) fate = $"was martyred, and {living} now keep the faith";
            else if (martyred) fate = "was martyred, and the faith died with the dust";
            else if (living >= 8) fate = $"and {living} souls now follow {rel.Name}";
            else if (living > 0) fate = $"and a small flame of {living} believers endures";
            else fate = $"but {rel.Name} faded to nothing";
            string label = $"{prophet.Name} founded {rel.Name}; {fate}.";
            int lastYear = ids.Select(i => byid[i].Year).Append(ev.Year).Max();
            echoes.Add(new Echo("The False Prophet", label, ids, (ev.Year, lastYear)));
        }
        return echoes;
    }

    public static List<Echo> DetectHolyWar(World world)
    {
        var echoes = new List<Echo>();
        foreach (var war in world.Chronicle.Events.Where(e => e.Type == "war" && e.Tags.Contains("holy")))
        {
            var chain = world.Chronicle.Trace(war.Id);
            var ids = chain.Select(e => e.Id).ToList();
            var span = (chain.Count > 0 ? chain[0].Year : war.Year, war.Year);
            echoes.Add(new Echo("The Holy War", "Faith turned to bloodshed: " + war.Text, ids, span));
        }
        return echoes;
    }

    public static List<Echo> DetectSchisms(World world)
    {
        var echoes = new List<Echo>();
        foreach (var ev in world.Chronicle.Events.Where(e => e.Type == "schism"))
            echoes.Add(new Echo("The Schism", ev.Text, new() { ev.Id }, (ev.Year, ev.Year)));
        return echoes;
    }

    /// <summary>A run of famines close in time — the island remembers it as one long hunger.</summary>
    public static List<Echo> DetectLongFamine(World world)
    {
        var famines = world.Chronicle.Events.Where(e => e.Type == "famine").OrderBy(e => e.Year).ToList();
        var echoes = new List<Echo>();
        List<Event>? group = null;
        var groups = new List<List<Event>>();
        foreach (var fe in famines)
        {
            if (group is null || fe.Year - group[^1].Year > 6) { group = new(); groups.Add(group); }
            group.Add(fe);
        }
        foreach (var g in groups.Where(g => g.Count >= 3))
        {
            var span = (g[0].Year, g[^1].Year);
            string label = $"A long famine of {g.Count} hungers ran {SpanPhrase(span.Item1, span.Item2)}.";
            echoes.Add(new Echo("The Long Famine", label, g.Select(e => e.Id).ToList(), span));
        }
        return echoes;
    }

    /// <summary>Sustained plenty — many booms packed into a short span become a golden age.</summary>
    public static List<Echo> DetectGoldenAge(World world)
    {
        var booms = world.Chronicle.Events.Where(e => e.Type == "boom").OrderBy(e => e.Year).ToList();
        var echoes = new List<Echo>();
        int i = 0;
        while (i < booms.Count)
        {
            int startYear = booms[i].Year;
            var window = new List<Event>();
            int j = i;
            while (j < booms.Count && booms[j].Year - startYear < 25) { window.Add(booms[j]); j++; }
            if (window.Count >= 2)
            {
                var span = (window[0].Year, window[^1].Year);
                string label = $"A golden age of {window.Count} bountiful seasons blessed the island {SpanPhrase(span.Item1, span.Item2)}.";
                echoes.Add(new Echo("The Golden Age", label, window.Select(e => e.Id).ToList(), span));
                i = j;   // don't overlap the next golden age onto this one's booms
            }
            else i++;
        }
        return echoes;
    }

    private static readonly HashSet<string> CustomWords = new() { "warlike", "devout", "scheming", "peaceable" };

    /// <summary>A custom a people held for a long span, then shed — the island remembers the people
    /// they used to be. Each fade event cause-links back to the custom's origin; the gap is the span.</summary>
    public static List<Echo> DetectVanishedWay(World world)
    {
        var byid = ById(world);
        var echoes = new List<Echo>();
        foreach (var fade in world.Chronicle.Events.Where(e => e.Type == "custom" && e.Tags.Contains("fade")))
        {
            if (fade.Causes.Count == 0 || !byid.TryGetValue(fade.Causes[0], out var origin)) continue;
            int years = fade.Year - origin.Year;
            if (years < 30) continue;
            string custom = fade.Tags.FirstOrDefault(t => CustomWords.Contains(t)) ?? "old";
            string people = fade.Participants.Count > 0 && world.People.TryGetValue(fade.Participants[0], out var p)
                ? world.Factions[p.FactionId].Name : "A people";
            string label = $"{people} were a {custom} people for {years} years, then the old ways faded.";
            echoes.Add(new Echo("The Vanished Way", label, new() { origin.Id, fade.Id }, (origin.Year, fade.Year)));
        }
        return echoes;
    }

    /// <summary>A person dogged by negative rumor — two or more dark whispers cause-linked to them,
    /// years apart (the per-person gossip cooldown guarantees the spread). The chronicle remembers a
    /// name that curdled. Threshold is 2, not 3: the sim spreads crime across many hands rather than
    /// concentrating it, so even over 5000 years no single name draws a third rumor — see the M8 note
    /// in CLAUDE.md.</summary>
    public static List<Echo> DetectBlackenedName(World world)
    {
        var byPerson = new Dictionary<int, List<Event>>();
        foreach (var r in world.Chronicle.Events.Where(e => e.Type == "rumor" && e.Tags.Contains("negative")))
            foreach (var pid in r.Participants)
            {
                if (!byPerson.TryGetValue(pid, out var list)) { list = new(); byPerson[pid] = list; }
                list.Add(r);
            }

        var echoes = new List<Echo>();
        foreach (var pid in byPerson.Keys.OrderBy(k => k))
        {
            var rumors = byPerson[pid].OrderBy(e => e.Year).ToList();
            if (rumors.Count < 2) continue;
            int span = rumors[^1].Year - rumors[0].Year;
            string name = world.People.TryGetValue(pid, out var p) ? p.Name : "A figure";
            string label = $"{name}'s name darkened under rumor after rumor over {span} years.";
            echoes.Add(new Echo("The Blackened Name", label, rumors.Select(e => e.Id).ToList(),
                (rumors[0].Year, rumors[^1].Year)));
        }
        return echoes;
    }

    /// <summary>A war whose causal trace passes through a rumor — whispers, not just grievance,
    /// helped carry two peoples to blows.</summary>
    public static List<Echo> DetectRumorWar(World world)
    {
        var echoes = new List<Echo>();
        foreach (var war in world.Chronicle.Events.Where(e => e.Type == "war"))
        {
            var chain = world.Chronicle.Trace(war.Id);
            if (!chain.Any(e => e.Type == "rumor")) continue;
            var ids = chain.Select(e => e.Id).ToList();
            var span = (chain.Count > 0 ? chain[0].Year : war.Year, war.Year);
            echoes.Add(new Echo("The War of Whispers", "Whispers helped carry the island to war: " + war.Text, ids, span));
        }
        return echoes;
    }

    /// <summary>A single modeled place that armies returned to again and again — three or more
    /// battles anchored to ONE site, across the wars of the age. The first echo keyed on a
    /// place (Event.SiteId), the chronicle remembering ground soaked by repeated war.</summary>
    public static List<Echo> DetectFieldOfBones(World world)
    {
        var bySite = new Dictionary<int, List<Event>>();
        foreach (var e in world.Chronicle.Events)
            if (e.Type == "battle" && e.SiteId is int sid)
            {
                if (!bySite.TryGetValue(sid, out var list)) { list = new(); bySite[sid] = list; }
                list.Add(e);
            }
        var echoes = new List<Echo>();
        foreach (var sid in bySite.Keys.OrderBy(k => k))
        {
            var battles = bySite[sid];
            if (battles.Count < 3) continue;
            var span = (battles[0].Year, battles[^1].Year);
            string name = world.Sites.Get(sid).Name;
            string label = $"{name} saw {battles.Count} battles {SpanPhrase(span.Item1, span.Item2)} — a field of bones.";
            echoes.Add(new Echo("The Field of Bones", label, battles.Select(e => e.Id).ToList(), span));
        }
        return echoes;
    }

    public static List<Echo> DetectAll(World world)
    {
        var echoes = new List<Echo>();
        echoes.AddRange(DetectCursedBloodline(world));
        echoes.AddRange(DetectBloodFeuds(world));
        echoes.AddRange(DetectWarFromLove(world));
        echoes.AddRange(DetectHauntedHeir(world));
        echoes.AddRange(DetectFalseProphet(world));
        echoes.AddRange(DetectHolyWar(world));
        echoes.AddRange(DetectSchisms(world));
        echoes.AddRange(DetectPeopleErased(world));
        echoes.AddRange(DetectLongFamine(world));
        echoes.AddRange(DetectGoldenAge(world));
        echoes.AddRange(DetectVanishedWay(world));
        echoes.AddRange(DetectBlackenedName(world));
        echoes.AddRange(DetectRumorWar(world));
        echoes.AddRange(DetectFieldOfBones(world));

        var seen = new HashSet<(string, string)>();
        var unique = new List<Echo>();
        foreach (var e in echoes)
        {
            var key = (e.Archetype, e.Label);
            if (!seen.Add(key)) continue;
            unique.Add(e);
        }
        unique.Sort((a, b) => a.YearSpan.First.CompareTo(b.YearSpan.First));
        return unique;
    }
}
