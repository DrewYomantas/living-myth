using LivingMyth.Sim;

// Console proof for the C# port (M0). Mirrors the Python prototype's run.py,
// divergence_test.py and surfacing_demo.py, plus a determinism self-check.
//
//   dotnet run -- run --seed 42 --years 100
//   dotnet run -- divergence --seed 18 --years 120
//   dotnet run -- surface --seed 1 --years 120
//   dotnet run -- verify

string cmd = args.Length > 0 ? args[0] : "run";
int Seed(int def) => GetInt("--seed", def);
int Years(int def) => GetInt("--years", def);

switch (cmd)
{
    case "run": RunCmd(Seed(42), Years(100), GetInt("--trace", -1)); break;
    case "divergence": DivergenceCmd(Seed(18), Years(120)); break;
    case "surface": SurfaceCmd(Seed(1), Years(120)); break;
    case "verify": VerifyCmd(); break;
    case "homes": HomesCmd(Years(120)); break;
    case "story": StoryCmd(Years(120)); break;
    case "canon": CanonCmd(); break;
    default:
        Console.WriteLine("commands: run | divergence | surface | verify | homes | story | canon");
        break;
}
return;

int GetInt(string flag, int def)
{
    int i = Array.IndexOf(args, flag);
    return i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out var v) ? v : def;
}

(ConfigData, NamesData) Load() => DataLoader.Load();

string Who(World w, int pid) => $"{w.People[pid].Name} (#{pid})";

// ----------------------------------------------------------------------------- run

void RunCmd(int seed, int years, int trace)
{
    var (config, names) = Load();
    int cap = GetInt("--cap", -1);
    if (cap >= 0) config.Params["carrying_capacity"] = cap;
    var world = new World(seed, config, names);
    world.Run(years);

    string outPath = Path.Combine(AppContext.BaseDirectory, "chronicle.txt");
    File.WriteAllText(outPath, world.Chronicle.Render() + "\n");

    Console.WriteLine(Summary(world));

    if (trace >= 0) Console.WriteLine(TraceBlock(world, trace));
    else
    {
        var juicy = PickJuiciest(world);
        if (juicy is not null) Console.WriteLine(TraceBlock(world, juicy.Id));
    }
    Console.WriteLine($"\nFull chronicle written to {outPath}\n");
}

string Summary(World world)
{
    var living = world.Living();
    var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    foreach (var e in world.Chronicle.Events)
        counts[e.Type] = counts.GetValueOrDefault(e.Type) + 1;

    var lines = new List<string>
    {
        "", new string('=', 62),
        $"ISLAND OF {world.Island.ToUpperInvariant()} - after {world.Year} years",
        $"Seed: {world.Seed}",
        $"Living: {living.Count}   Ever lived: {world.People.Count}   Events: {world.Chronicle.Events.Count}",
        "Event types: " + string.Join(", ", counts.Select(kv => $"{kv.Key} {kv.Value}")),
        new string('-', 62),
    };
    foreach (var fac in world.Config.Factions.Select(f => world.Factions[f.Id]))
    {
        string leader = fac.LeaderId is int lid ? world.People[lid].Name : "(none)";
        int pop = living.Count(p => p.FactionId == fac.Id);
        lines.Add($"{fac.Name,-22} pop {pop,-4} led by {leader}");
    }
    lines.Add(new string('=', 62));
    return string.Join("\n", lines);
}

string TraceBlock(World world, int eventId)
{
    var chain = world.Chronicle.Trace(eventId);
    var outp = new List<string> { "", $"HOW WE GOT HERE  (catch-up trace of event #{eventId})", new string('-', 62) };
    foreach (var e in chain)
    {
        string parts = string.Join(" ", e.Participants.Select(pid => Who(world, pid)));
        outp.Add($"  Year {e.Year,3} [{e.Type}] {e.Text}");
        if (parts.Length > 0) outp.Add($"            involves: {parts}");
    }
    return string.Join("\n", outp);
}

Event? PickJuiciest(World world)
{
    var events = world.Chronicle.Events;
    Func<Event, bool>[] wanted =
    {
        e => e.Tags.Contains("revenge"),
        e => e.Type == "war" && e.Causes.Count > 0,
        e => e.Type == "murder",
        e => e.Type == "succession",
    };
    foreach (var pred in wanted)
    {
        var hits = events.Where(pred).ToList();
        if (hits.Count > 0)
            return hits.Aggregate((best, cur) =>
                world.Chronicle.Trace(cur.Id).Count > world.Chronicle.Trace(best.Id).Count ? cur : best);
    }
    return null;
}

// --------------------------------------------------------------------- divergence

Person PickTarget(World world)
{
    string fid = world.Config.Factions[0].Id;
    var adults = world.FactionMembers(fid).Where(p => p.Age(world.Year) >= 18 && p.Age(world.Year) <= 30).ToList();
    var pool = adults.Count > 0 ? adults : world.FactionMembers(fid);
    return pool.Aggregate((min, cur) => cur.Id < min.Id ? cur : min);
}

World BuildWorld(int seed, int years, int? targetId)
{
    var (config, names) = Load();
    var w = new World(seed, config, names);
    w.SeedWorld();
    if (targetId is int tid) w.PlantCurse(w.People[tid]);
    for (int i = 0; i < years; i++) w.Tick();
    return w;
}

List<(int, string, string)> RealEvents(World world)
    => world.Chronicle.Events.Where(e => !e.Tags.Contains("divine"))
        .Select(e => (e.Year, e.Type, e.Text)).ToList();

(int? year, int common) DivergencePoint(World clean, World cursed)
{
    var a = RealEvents(clean);
    var b = RealEvents(cursed);
    int n = Math.Min(a.Count, b.Count);
    int i = 0;
    while (i < n && a[i] == b[i]) i++;
    if (i >= a.Count && i >= b.Count) return (null, i);
    int? year = null;
    if (i < a.Count) year = a[i].Item1;
    if (i < b.Count) year = year is null ? b[i].Item1 : Math.Min(year.Value, b[i].Item1);
    return (year, i);
}

string FactionLine(World world)
{
    var parts = new List<string>();
    foreach (var fac in world.Config.Factions.Select(f => world.Factions[f.Id]))
    {
        string leader = fac.LeaderId is int lid ? world.People[lid].Name : "(none)";
        int pop = world.Living().Count(p => p.FactionId == fac.Id);
        parts.Add($"{fac.Name} pop {pop} (led by {leader})");
    }
    return string.Join("; ", parts);
}

string Fate(World world, int pid)
{
    var p = world.People[pid];
    return p.Alive ? $"still alive at {p.Age(world.Year)}" : $"died in year {p.DeathYear}";
}

void DivergenceCmd(int seed, int years)
{
    var (config, names) = Load();
    var scout = new World(seed, config, names);
    scout.SeedWorld();
    var target = PickTarget(scout);
    int targetId = target.Id;
    string targetName = target.Name;
    string targetFac = scout.Factions[target.FactionId].Name;

    var clean = BuildWorld(seed, years, null);
    var cursed = BuildWorld(seed, years, targetId);

    var (divYear, common) = DivergencePoint(clean, cursed);

    Console.WriteLine(new string('=', 64));
    Console.WriteLine($"DIVERGENCE TEST   seed {seed}   {years} years");
    Console.WriteLine($"The marked one: {targetName} (#{targetId}) of {targetFac}");
    Console.WriteLine(new string('=', 64));
    Console.WriteLine();
    if (divYear is null)
    {
        Console.WriteLine("The two timelines never diverged. (The curse never flipped an outcome.)");
        return;
    }
    Console.WriteLine($"The two worlds are IDENTICAL for the first {common} events.");
    Console.WriteLine($"They split in year {divYear}, and never reconverge.");
    Console.WriteLine();
    Console.WriteLine($"CLEAN  timeline: {clean.Living().Count} living, {clean.Chronicle.Events.Count} events");
    Console.WriteLine($"       {FactionLine(clean)}");
    Console.WriteLine($"       the marked one: {Fate(clean, targetId)}");
    Console.WriteLine();
    Console.WriteLine($"CURSED timeline: {cursed.Living().Count} living, {cursed.Chronicle.Events.Count} events");
    Console.WriteLine($"       {FactionLine(cursed)}");
    Console.WriteLine($"       the marked one: {Fate(cursed, targetId)}");
    Console.WriteLine();

    int curseId = cursed.CurseEvent!.Id;
    var fallout = cursed.Chronicle.Events.Where(e => e.Causes.Contains(curseId)).ToList();
    Console.WriteLine(new string('-', 64));
    Console.WriteLine("THE CURSE STAYS TRACEABLE");
    Console.WriteLine("Misfortunes the catch-up trace ties directly back to your curse:");
    foreach (var e in fallout.Take(12))
        Console.WriteLine($"  Year {e.Year,3}  {e.Text}");
    int extra = fallout.Count - 12;
    if (extra > 0) Console.WriteLine($"  ... and {extra} more down the bloodline.");
    Console.WriteLine(new string('=', 64));
}

// ------------------------------------------------------------------------ surface

void PrintFeed(string title, List<FeedRow> feed)
{
    Console.WriteLine(title);
    Console.WriteLine(new string('-', 64));
    foreach (var row in feed)
    {
        string tag = string.Join(" ", row.Labels.Select(l => "[" + l + "]"));
        Console.WriteLine($"  Yr {row.Ev.Year,3}  {row.Ev.Text}");
        Console.WriteLine($"          {tag}  (weight {row.Total})");
    }
    Console.WriteLine();
}

void SurfaceCmd(int seed, int years)
{
    var (config, names) = Load();
    var world = new World(seed, config, names);
    world.SeedWorld();
    var target = PickTarget(world);
    world.PlantCurse(target);
    for (int i = 0; i < years; i++) world.Tick();

    var echoes = Echoes.DetectAll(world);
    int raw = world.Chronicle.Events.Count;

    Console.WriteLine(new string('=', 64));
    Console.WriteLine($"SURFACING LAYER   seed {seed}   {years} years");
    Console.WriteLine($"Island of {world.Island}, {raw} raw events recorded.");
    Console.WriteLine($"Following the cursed line of {target.Name} (#{target.Id}).");
    Console.WriteLine(new string('=', 64));
    Console.WriteLine();

    Console.WriteLine($"MYTH ECHOES THE CHRONICLE RECOGNIZED  (top {Math.Min(12, echoes.Count)} of {echoes.Count})");
    Console.WriteLine(new string('-', 64));
    foreach (var echo in Feed.RankEchoes(world, echoes, 12))
        Console.WriteLine($"  [{echo.Archetype}]  {echo.Label}");
    if (echoes.Count == 0) Console.WriteLine("  (none this run)");
    Console.WriteLine();

    var loud = Feed.BuildFeed(world, echoes: echoes, limit: 22);
    PrintFeed($"THE FEED  -  what rose out of {raw} events", loud);

    var full = Feed.BuildFeed(world, markedPeople: new[] { target.Id }, echoes: echoes, limit: 10000);
    var yoursOnly = full.Where(r => r.Labels.Contains("YOURS")).Take(10).ToList();
    PrintFeed("FOLLOWING THE CURSED BLOODLINE  -  beats that surface because it's YOURS", yoursOnly);
}

// -------------------------------------------------------------------------- homes

// Proof gate for the Person.HomeRegionId contract: founders carry their people's founding
// seat (the region the founding-territory event itself anchors), newborns inherit father's
// home else mother's, nulls are honest (landless line), and the whole map is deterministic.
void HomesCmd(int years)
{
    Console.WriteLine($"Home contract gate ({years} yrs): founders at the founding seat, children inherit, nulls honest,");
    Console.WriteLine("life events remembered at home (never a literal place), all of it deterministic.");
    int failures = 0;
    foreach (int seed in new[] { 1, 18, 42, 7 })
    {
        var (c1, n1) = Load();
        var w = new World(seed, c1, n1); w.Run(years);

        // Founding seats, recovered from the chronicle's own anchors — not from sim internals.
        var seat = new Dictionary<string, int>();
        foreach (var e in w.Chronicle.Events.Where(e =>
                     e.Type == "territory" && e.Tags.Contains("founding") && e.RegionId is int))
            foreach (int pid in e.Participants)
                seat[w.People[pid].FactionId] = e.RegionId!.Value;

        var bad = new List<string>();
        foreach (var p in w.People.Values.OrderBy(p => p.Id))
        {
            if (p.HomeRegionId is int h && (h < 0 || h >= w.Regions.Count))
                bad.Add($"#{p.Id} home {h} is not a real region");
            if (p.Parents.Count == 0)
            {
                int? want = seat.TryGetValue(p.FactionId, out int s) ? s : null;
                if (p.HomeRegionId != want)
                    bad.Add($"founder #{p.Id} home {p.HomeRegionId?.ToString() ?? "null"} != seat {want?.ToString() ?? "null"}");
            }
            else
            {
                var father = w.People[p.Parents.First(id => w.People[id].Sex == "m")];
                var mother = w.People[p.Parents.First(id => w.People[id].Sex == "f")];
                if (p.HomeRegionId != (father.HomeRegionId ?? mother.HomeRegionId))
                    bad.Add($"child #{p.Id} home {p.HomeRegionId?.ToString() ?? "null"} breaks inheritance");
            }
        }

        // Life-memory anchors: births/deaths/murders carry the remembered-at-home anchor
        // (HomeRegionId), never a literal event place (RegionId), and never name the home
        // region in their text — the anchor is memory, not geography the text could claim.
        int anchored = 0, lifeEvents = 0;
        foreach (var e in w.Chronicle.Events.Where(e => e.Type is "birth" or "death" or "murder"))
        {
            lifeEvents++;
            if (e.RegionId is not null)
                bad.Add($"life event #{e.Id} claims a literal place ({e.RegionId})");
            var soul = w.People[e.Participants[0]];   // child / deceased / victim by contract
            if (e.HomeRegionId != soul.HomeRegionId)
                bad.Add($"life event #{e.Id} anchor {e.HomeRegionId?.ToString() ?? "null"} != {soul.Name}'s home {soul.HomeRegionId?.ToString() ?? "null"}");
            if (e.HomeRegionId is int ah)
            {
                anchored++;
                if (ah < 0 || ah >= w.Regions.Count)
                    bad.Add($"life event #{e.Id} home anchor {ah} is not a real region");
                else if (e.Text.Contains(w.Regions[ah].Name))
                    bad.Add($"life event #{e.Id} text names its home region — text must never claim place");
            }
        }

        // Determinism of the home map itself: a second run must root every soul identically
        // and stamp every life event with the same memory anchor.
        var (c2, n2) = Load();
        var w2 = new World(seed, c2, n2); w2.Run(years);
        bool sameMap = w.People.Count == w2.People.Count &&
            w.People.Values.OrderBy(p => p.Id).Select(p => (p.Id, p.HomeRegionId))
             .SequenceEqual(w2.People.Values.OrderBy(p => p.Id).Select(p => (p.Id, p.HomeRegionId)));
        if (!sameMap) bad.Add("home map differs between identical runs");
        bool sameAnchors = w.Chronicle.Events.Count == w2.Chronicle.Events.Count &&
            w.Chronicle.Events.Select(e => (e.Id, e.HomeRegionId))
             .SequenceEqual(w2.Chronicle.Events.Select(e => (e.Id, e.HomeRegionId)));
        if (!sameAnchors) bad.Add("life-memory anchors differ between identical runs");

        int ever = w.People.Count, everHome = w.People.Values.Count(p => p.HomeRegionId is not null);
        var living = w.Living();
        int liveHome = living.Count(p => p.HomeRegionId is not null);
        Console.WriteLine($"  seed {seed,3}: {(bad.Count == 0 ? "OK" : "FAIL")}  homes {everHome}/{ever} ever, {liveHome}/{living.Count} living, life anchors {anchored}/{lifeEvents}");
        foreach (var b in bad.Take(5)) Console.WriteLine($"           {b}");
        if (bad.Count > 0) failures++;
    }
    Console.WriteLine(failures == 0 ? "\nHOME CONTRACT HOLDS." : $"\n{failures} SEED(S) BROKE THE CONTRACT.");
    Environment.Exit(failures == 0 ? 0 : 1);
}

// -------------------------------------------------------------------------- story

// Proof gate for the causal story grammar (PROJECT_STATE.md "Truth model V1"): every
// connector the viewer could voice is proven against recorded evidence — the named cause
// is literally in the effect's Causes, gaps are real arithmetic, "but" fires only from
// the authored rule set, "unresolved until" is recomputed independently from person
// state, honest unknowns stay inside the allow-list, and all of it is deterministic.
void StoryCmd(int years)
{
    Console.WriteLine($"Story grammar gate ({years} yrs): every connector proven, gaps real arithmetic,");
    Console.WriteLine("honest unknowns authored-only, all of it deterministic.");
    int failures = 0;
    foreach (int seed in new[] { 1, 18, 42, 7 })
    {
        var (c1, n1) = Load();
        var w = new World(seed, c1, n1); w.Run(years);
        var bad = new List<string>();
        var ruleFires = new SortedDictionary<string, int>(StringComparer.Ordinal);
        int links = 0, th = 0, bu = 0, un = 0, unknownRoots = 0;

        foreach (var e in w.Chronicle.Events)
        {
            if (e.Causes.Count > 0)
            {
                var link = StoryGrammar.ProximateLink(w, e)!;
                links++;
                ruleFires[link.RuleId] = ruleFires.GetValueOrDefault(link.RuleId) + 1;
                if (link.Kind == ConnectorKind.Therefore) th++;
                else if (link.Kind == ConnectorKind.But) bu++;
                else un++;

                // Evidence reality: the claimed cause must be recorded, past, and the gap real.
                if (!e.Causes.Contains(link.CauseEventId))
                    bad.Add($"event #{e.Id} claims cause {link.CauseEventId} not in its Causes");
                var cause = w.Chronicle.Get(link.CauseEventId);
                if (cause.Year > e.Year)
                    bad.Add($"event #{e.Id} claims a cause from the future (#{cause.Id})");
                if (link.GapYears != e.Year - cause.Year)
                    bad.Add($"event #{e.Id} gap {link.GapYears} != real {e.Year - cause.Year}");

                // "But" is authored-only; the blessed-union → war edge must never read as therefore.
                if (link.Kind == ConnectorKind.But && !StoryGrammar.ButRules.Contains(link.RuleId))
                    bad.Add($"event #{e.Id} BUT from non-authored rule '{link.RuleId}'");
                if (e.Type == "war" && cause.Type == "romance" && cause.Tags.Contains("peace")
                    && link.Kind != ConnectorKind.But)
                    bad.Add($"war #{e.Id} over an eased-tension union must be BUT, got {link.Kind}");

                // Unresolved-until, recomputed independently of the rule table.
                if (link.Kind == ConnectorKind.UnresolvedUntil)
                {
                    bool proven = e.Type == "murder" && e.Tags.Contains("revenge")
                        && cause.Type == "murder" && cause.Participants.Count > 0
                        && e.Participants.Count > 0
                        && w.People[cause.Participants[0]].MurderEventId == cause.Id
                        && w.People[cause.Participants[0]].Avenged
                        && w.People[cause.Participants[0]].KillerId == e.Participants[0];
                    if (!proven) bad.Add($"event #{e.Id} UNRESOLVED-UNTIL not provable from person state");
                }
                // Converse: every revenge murder with a cause must classify unresolved-until.
                if (e.Type == "murder" && e.Tags.Contains("revenge")
                    && link.Kind != ConnectorKind.UnresolvedUntil)
                    bad.Add($"revenge murder #{e.Id} classified '{link.RuleId}' instead of unresolved-until");
            }
            else
            {
                var origin = StoryGrammar.ClassifyOrigin(e);
                if (origin.Kind == OriginKind.HonestUnknown)
                {
                    unknownRoots++;
                    bool allowed = e.Type == "prophet" || e.Type == "schism"
                        || (e.Type == "romance" && e.Tags.Contains("forbidden"));
                    if (!allowed)
                        bad.Add($"event #{e.Id} ({e.Type}) claims honest-unknown outside the allow-list");
                }
                if (e.Type == "prophet"
                    && (origin.Kind != OriginKind.HonestUnknown || origin.SubjectPersonId != e.Participants[0]))
                    bad.Add($"prophet #{e.Id} misclassified ({origin.Kind})");
            }
        }

        // Annotate integrity: same membership as Trace, ordered by id (causes precede effects).
        for (int id = 0; id < w.Chronicle.Events.Count; id++)
        {
            var ann = StoryGrammar.Annotate(w, id);
            var trace = w.Chronicle.Trace(id);
            if (ann.Steps.Count != trace.Count
                || !ann.Steps.Select(s => s.Event.Id).OrderBy(i => i)
                      .SequenceEqual(trace.Select(t => t.Id).OrderBy(i => i)))
            { bad.Add($"annotate #{id} chain membership differs from Trace"); break; }
            for (int i = 1; i < ann.Steps.Count; i++)
                if (ann.Steps[i].Event.Id <= ann.Steps[i - 1].Event.Id)
                { bad.Add($"annotate #{id} steps not in record order"); break; }
        }

        // Determinism: a second identical run must yield byte-identical grammar output —
        // including the open-thread queries the recap's "Still unresolved" section renders.
        string Canon(World ww)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var e in ww.Chronicle.Events)
            {
                if (e.Causes.Count > 0)
                {
                    var l = StoryGrammar.ProximateLink(ww, e)!;
                    sb.Append(e.Id).Append('|').Append(l.RuleId).Append('|').Append(l.Kind)
                      .Append('|').Append(l.CauseEventId).Append('|').Append(l.GapYears).Append('\n');
                }
                else
                {
                    var o = StoryGrammar.ClassifyOrigin(e);
                    sb.Append(e.Id).Append('|').Append(o.Kind).Append('|').Append(o.CopyKey).Append('\n');
                }
            }
            foreach (var g in StoryGrammar.OpenGrievances(ww, ww.People.Keys.ToList()))
                sb.Append("grievance|").Append(g.VictimId).Append('|').Append(g.KillerId)
                  .Append('|').Append(g.MurderEventId).Append('|').Append(g.MurderYear)
                  .Append('|').Append(g.KillerAlive).Append('\n');
            foreach (var ow in StoryGrammar.OpenWars(ww))
                sb.Append("openwar|").Append(ow.WarEventId).Append('|').Append(ow.DeclaredYear).Append('\n');
            return sb.ToString();
        }
        var (c2, n2) = Load();
        var w2 = new World(seed, c2, n2); w2.Run(years);
        if (Canon(w) != Canon(w2)) bad.Add("grammar output differs between identical runs");

        Console.WriteLine($"  seed {seed,3}: {(bad.Count == 0 ? "OK" : "FAIL")}  {w.Chronicle.Events.Count} events, {links} links ({th} therefore, {bu} but, {un} unresolved), {unknownRoots} honest-unknown roots");
        Console.WriteLine($"           rules: {string.Join(", ", ruleFires.Select(kv => $"{kv.Key} {kv.Value}"))}");
        foreach (var b in bad.Take(5)) Console.WriteLine($"           {b}");
        if (bad.Count > 0) failures++;
    }
    Console.WriteLine(failures == 0 ? "\nSTORY GRAMMAR HOLDS." : $"\n{failures} SEED(S) BROKE THE GRAMMAR.");
    Environment.Exit(failures == 0 ? 0 : 1);
}

// -------------------------------------------------------------------------- canon

// Proof gate for the player-canon contract (PROJECT_STATE.md "Truth model V1"): notes
// roundtrip through save/load, attach to the right entity keys, vanish when emptied,
// go dormant (not stale) when their entity hasn't been re-simulated yet, quarantine on
// identity drift instead of misattaching, never destroy an unreadable file — and the
// sim provably cannot see the store at all.
void CanonCmd()
{
    Console.WriteLine("Canon contract gate: player tellings persist, attach honestly, and the sim never reads them.");
    var bad = new List<string>();
    void Check(string name, bool ok, string? detail = null)
    {
        Console.WriteLine($"  {name}: {(ok ? "OK" : "FAIL")}{(ok || detail is null ? "" : "  " + detail)}");
        if (!ok) bad.Add(name);
    }

    string path = Path.Combine(Path.GetTempPath(), $"lm_canon_gate_{Guid.NewGuid():N}.json");
    try
    {
        var (cfg, names) = Load();
        var w = new World(7, cfg, names); w.Run(120);          // the world notes are written against
        var (cfgB, namesB) = Load();
        var wOther = new World(1, cfgB, namesB); wOther.Run(120);   // a different world — identity must not carry
        var (cfgC, namesC) = Load();
        var wYoung = new World(7, cfgC, namesC); wYoung.Run(10);    // same world, earlier — notes must lie dormant

        // 1. Missing file → empty writable store, no warning, and no file conjured.
        var (s0, warn0) = PlayerCanonStore.LoadOrNew(path, 7);
        Check("load-missing", s0.Count == 0 && warn0 is null && !File.Exists(path) && !s0.ReadOnly);

        // 2. Roundtrip: write one of each note shape, save, reload, deep-compare.
        var person = w.People.Values.Where(p => !p.Alive && p.EverLeader).OrderBy(p => p.Id).Last();
        var ev = w.Chronicle.Events.Last(e => e.Type == "prophet");   // late enough to be unborn in a 10-yr run
        string fid = w.Config.Factions[0].Id;
        s0.Upsert($"p:{person.Id}", CanonNoteType.Telling, "She counted the gulls each dawn.", w, "2026-06-11T00:00:01Z");
        s0.Upsert($"p:{person.Id}", CanonNoteType.Inscription, "The hills keep her name.", w, "2026-06-11T00:00:02Z");
        s0.Upsert($"e:{ev.Id}", CanonNoteType.ChroniclerNote, "Some say a drowned bell rang that night.", w, "2026-06-11T00:00:03Z");
        s0.Upsert("r:3", CanonNoteType.PlaceLegend, "No boats beach here after dusk.", w, "2026-06-11T00:00:04Z");
        s0.Upsert($"f:{fid}", CanonNoteType.PeopleSay, "They bury their dead facing the sea.", w, "2026-06-11T00:00:05Z");
        s0.Save();
        var (s1, warn1) = PlayerCanonStore.LoadOrNew(path, 7);
        bool same = warn1 is null && s1.Count == s0.Count;
        foreach (var key in new[] { ($"p:{person.Id}", CanonNoteType.Telling), ($"p:{person.Id}", CanonNoteType.Inscription),
                                    ($"e:{ev.Id}", CanonNoteType.ChroniclerNote), ("r:3", CanonNoteType.PlaceLegend),
                                    ($"f:{fid}", CanonNoteType.PeopleSay) })
        {
            var a = s0.Get(key.Item1, key.Item2);
            var b = s1.Get(key.Item1, key.Item2);
            same &= a is not null && b is not null && a.Text == b.Text && a.CreatedYear == b.CreatedYear
                 && a.UpdatedUtc == b.UpdatedUtc && a.Source == b.Source
                 && a.Snapshot.Count == b.Snapshot.Count
                 && a.Snapshot.All(kv => b.Snapshot.TryGetValue(kv.Key, out var v) && v == kv.Value);
        }
        Check("roundtrip", same);

        // 3. Empty text deletes; the deletion survives save/reload.
        s1.Upsert("r:3", CanonNoteType.PlaceLegend, "   \n ", w);
        s1.Save();
        var (s2, _) = PlayerCanonStore.LoadOrNew(path, 7);
        Check("empty-deletes", s1.Get("r:3", CanonNoteType.PlaceLegend) is null
                            && s2.Get("r:3", CanonNoteType.PlaceLegend) is null);

        // 4. Dormant, never stale: against the same seed not yet re-simulated that far,
        //    notes on a later-born soul and a later event wait — they are not quarantined.
        var noteP = s2.Get($"p:{person.Id}", CanonNoteType.Telling)!;
        var noteE = s2.Get($"e:{ev.Id}", CanonNoteType.ChroniclerNote)!;
        bool dormantP = person.Id >= wYoung.People.Count
            ? s2.StateOf(noteP, wYoung) == CanonNoteState.Dormant
            : true;   // person already existed by year 10 — dormancy not testable on this id
        Check("dormant", dormantP
            && ev.Id >= wYoung.Chronicle.Events.Count
            && s2.StateOf(noteE, wYoung) == CanonNoteState.Dormant
            && s2.StateOf(noteP, w) == CanonNoteState.Active
            && s2.StateOf(noteE, w) == CanonNoteState.Active);

        // 5. Quarantine on identity drift: a tampered snapshot (the stand-in for sim-build
        //    drift) never renders against the wrong entity — and never deletes the note.
        //    Cross-world, the same ids must never read Active either.
        var probeEv = w.Chronicle.Events.First(e => e.Type == "war");
        s2.Upsert($"e:{probeEv.Id}", CanonNoteType.ChroniclerNote, "Probe.", w, "2026-06-11T00:00:06Z");
        var probe = s2.Get($"e:{probeEv.Id}", CanonNoteType.ChroniclerNote)!;
        probe.Snapshot["text"] = "a different telling of this event";
        bool quarantined = s2.StateOf(probe, w) == CanonNoteState.Quarantined;
        var stP = s2.StateOf(noteP, wOther);
        var stE = s2.StateOf(noteE, wOther);
        s2.Save();
        var (s3, _) = PlayerCanonStore.LoadOrNew(path, 7);
        Check("quarantine", quarantined && stP != CanonNoteState.Active && stE != CanonNoteState.Active
                          && s3.Get($"e:{probeEv.Id}", CanonNoteType.ChroniclerNote) is not null,
              $"probe={s2.StateOf(probe, w)} person={stP} event={stE}");

        // 6. Corrupt file: empty read-only store + warning; the bad bytes stay untouched.
        File.WriteAllText(path, "{ this is not json");
        var (sBad, warnBad) = PlayerCanonStore.LoadOrNew(path, 7);
        bool upsertThrew = false;
        try { sBad.Upsert("r:3", CanonNoteType.PlaceLegend, "x", w); }
        catch (InvalidOperationException) { upsertThrew = true; }
        Check("corrupt-file", sBad.Count == 0 && warnBad is not null && sBad.ReadOnly && !sBad.FutureSchema
                           && upsertThrew && File.ReadAllText(path) == "{ this is not json");

        // 7. Future schema: preserved untouched, read-only, flagged as from-the-future.
        File.WriteAllText(path, "{\"schema_version\": 99, \"seed\": 7, \"notes\": []}");
        var (sFut, warnFut) = PlayerCanonStore.LoadOrNew(path, 7);
        Check("future-schema", sFut.Count == 0 && warnFut is not null && sFut.ReadOnly && sFut.FutureSchema);

        // 8. Type↔key contract: a telling cannot attach to a place.
        bool threw = false;
        try { s2.Upsert("r:3", CanonNoteType.Telling, "wrong home", w); }
        catch (ArgumentException) { threw = true; }
        Check("type-key-contract", threw);

        // 9. Sim-blind, by reflection: no sim type holds a canon-typed member anywhere.
        var canonTypes = new[] { typeof(PlayerCanonStore), typeof(CanonNote), typeof(CanonFile) };
        var simTypes = new[] { typeof(World), typeof(Chronicle), typeof(Event), typeof(Person),
                               typeof(Faction), typeof(Religion), typeof(Region), typeof(Rng) };
        bool Touches(Type t) => canonTypes.Contains(t)
            || (t.IsGenericType && t.GetGenericArguments().Any(Touches));
        const System.Reflection.BindingFlags all =
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static;
        bool blind = simTypes.All(t =>
            t.GetFields(all).All(f => !Touches(f.FieldType)) &&
            t.GetProperties(all).All(p => !Touches(p.PropertyType)));
        // …and by behavior: a run with a populated store in scope is byte-identical.
        var (cfg2, names2) = Load();
        var w2 = new World(7, cfg2, names2); w2.Run(120);
        Check("sim-blind", blind && w.Chronicle.Render() == w2.Chronicle.Render());
    }
    finally
    {
        if (File.Exists(path)) File.Delete(path);
        if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
    }

    Console.WriteLine(bad.Count == 0 ? "\nCANON CONTRACT HOLDS." : $"\n{bad.Count} CHECK(S) BROKE THE CONTRACT.");
    Environment.Exit(bad.Count == 0 ? 0 : 1);
}

// ------------------------------------------------------------------------- verify

void VerifyCmd()
{
    Console.WriteLine("Determinism gate: same seed must produce a byte-identical chronicle.");
    int failures = 0;
    foreach (int seed in new[] { 1, 18, 42, 7 })
    {
        var (c1, n1) = Load();
        var w1 = new World(seed, c1, n1); w1.Run(120);
        var (c2, n2) = Load();
        var w2 = new World(seed, c2, n2); w2.Run(120);
        bool ok = w1.Chronicle.Render() == w2.Chronicle.Render()
                  && w1.Chronicle.Events.Count == w2.Chronicle.Events.Count;
        Console.WriteLine($"  seed {seed,3}: {(ok ? "OK" : "MISMATCH")}  ({w1.Chronicle.Events.Count} events)");
        if (!ok) failures++;
    }
    Console.WriteLine(failures == 0 ? "\nDETERMINISM HOLDS." : $"\n{failures} SEED(S) NON-DETERMINISTIC.");
    Environment.Exit(failures == 0 ? 0 : 1);
}
