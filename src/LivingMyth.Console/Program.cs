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
    case "divine": DivineCmd(Years(200)); break;
    case "save": SaveCmd(Years(60)); break;
    case "sites": SitesCmd(Years(120)); break;
    case "replay": ReplayCmd(Years(120)); break;
    case "harvest": HarvestCmd(Years(120)); break;
    case "plague": PlagueCmd(Years(120)); break;
    case "migration": MigrationCmd(Years(120)); break;
    case "prejudice": PrejudiceCmd(Years(120)); break;
    case "creeping": CreepingDeathCmd(Years(1000)); break;
    case "paint": PaintCmd(Seed(7), Years(120)); break;
    default:
        Console.WriteLine("commands: run | divergence | surface | verify | homes | story | canon | divine | save | sites | replay | harvest | plague | migration | prejudice | creeping | paint");
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

// ----------------------------------------------------------------------------- paint
// Headless atlas render: run the sim, then write the SurfacePainter's pixels to a PNG. This is
// the SAME read-model the Godot viewer paints with, so the image is byte-faithful to the map
// the player sees. Pure presentation evidence — painting draws zero Rng, so `verify` is unmoved.
//   dotnet run -- paint --seed 7 --years 120 --out atlas.png [--scale 4]
void PaintCmd(int seed, int years)
{
    var (config, names) = Load();
    int cap = GetInt("--cap", -1);
    if (cap >= 0) config.Params["carrying_capacity"] = cap;
    var world = new World(seed, config, names);
    world.Run(years);

    int side = SurfacePainter.Side;
    var rgb = SurfacePainter.Paint(world);

    int scale = Math.Max(1, GetInt("--scale", 4));   // nearest-neighbour upscale for legible PNGs
    int outSide = side * scale;
    var big = new byte[outSide * outSide * 3];
    for (int y = 0; y < outSide; y++)
        for (int x = 0; x < outSide; x++)
        {
            int si = ((y / scale) * side + (x / scale)) * 3;
            int di = (y * outSide + x) * 3;
            big[di] = rgb[si]; big[di + 1] = rgb[si + 1]; big[di + 2] = rgb[si + 2];
        }

    int oi = Array.IndexOf(args, "--out");
    string outPath = oi >= 0 && oi + 1 < args.Length
        ? args[oi + 1]
        : Path.Combine(AppContext.BaseDirectory, $"atlas_seed{seed}_yr{world.Year}.png");
    PngWriter.Write(outPath, outSide, outSide, big);
    Console.WriteLine($"Painted {world.Island} (seed {seed}, year {world.Year}) — {outSide}×{outSide} → {outPath}");
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

// ------------------------------------------------------------------------- divine

// Proof gate for god-hand divine pressure V1 + the editable world surface: pressure records
// are deterministic, targets validate, the curse stays traceable, pressure-influenced events
// cause-link to the real divine acts, terrain edits are deterministic and real, the
// RegionId/HomeRegionId channels stay unmixed, and the sim remains canon-blind.
void DivineCmd(int years)
{
    Console.WriteLine($"Divine pressure gate ({years} yrs): the god's hand is explicit state, deterministic,");
    Console.WriteLine("honestly cause-linked, and the surface edits are real and replayable.");
    var bad = new List<string>();

    // The scripted hand: the same acts at year 0, then the world runs free.
    World RunScript(int seed)
    {
        var (cfg, names) = Load();
        var w = new World(seed, cfg, names);
        w.SeedWorld();
        var f = w.Config.Factions;
        // Deterministic targets: the two eldest of the first people are blessed (old enough
        // to die naturally inside the window), the youngest adult of the second is cursed.
        var elders = w.FactionMembers(f[0].Id).OrderByDescending(p => p.Age(w.Year)).ThenBy(p => p.Id).Take(2).ToList();
        foreach (var e in elders) w.BlessPerson(e);
        var cursed = w.FactionMembers(f[1].Id).Where(p => p.Age(w.Year) >= 18)
            .OrderBy(p => p.Age(w.Year)).ThenBy(p => p.Id).First();
        w.PlantCurse(cursed);
        w.ProtectFaction(f[0].Id);
        w.DoomFaction(f[1].Id);
        int target = w.Factions[f[2].Id].ControlledRegions.Count > 0
            ? w.Factions[f[2].Id].ControlledRegions.Select(int.Parse).Min() : 0;
        w.SeedOmen(target);
        // A region whose seat sits in rock or water honestly refuses the edit (null) —
        // walk regions in id order until the land takes each act. Deterministic.
        foreach (var r in w.Regions) if (w.SeedForest(r.Id) is not null) break;
        foreach (var r in w.Regions) if (w.CallSpring(r.Id) is not null) break;
        for (int i = 0; i < years; i++) w.Tick();
        return w;
    }

    string Ledger(World w) => string.Join("\n", w.DivinePressures.Select(p =>
        $"{p.Id}|{p.Kind}|{p.TargetType}|{p.TargetId}|{p.StartYear}|{p.SourceEventId}|{p.ExpiresYear?.ToString() ?? "-"}"));

    // ---- target validation: the hand cannot act on what is not there ----
    {
        var (cfg, names) = Load();
        var w = new World(7, cfg, names);
        w.SeedWorld();
        for (int i = 0; i < 60; i++) w.Tick();   // let some souls die
        bool Throws(Action a) { try { a(); return false; } catch (ArgumentException) { return true; } }
        var deadSoul = w.People.Values.Where(p => !p.Alive).OrderBy(p => p.Id).First();
        var alive = w.Living()[0];
        w.BlessPerson(alive);
        bool ok = Throws(() => w.BlessPerson(deadSoul))
               && Throws(() => w.BlessPerson(alive))                   // double-bless
               && Throws(() => w.ProtectFaction("no-such-people"))
               && Throws(() => w.SeedOmen(-1))
               && Throws(() => w.SeedForest(9999));
        Console.WriteLine($"  validation: {(ok ? "OK" : "FAIL")}");
        if (!ok) bad.Add("validation");
    }

    int blessLinkedDeaths = 0, doomLinkedFamines = 0, curseFallout = 0;
    foreach (int seed in new[] { 7, 42 })
    {
        var w = RunScript(seed);
        var w2 = RunScript(seed);
        var seedBad = new List<string>();

        // Determinism: chronicle, ledger, and surface state all byte-identical.
        if (w.Chronicle.Render() != w2.Chronicle.Render()) seedBad.Add("chronicle differs between identical scripts");
        if (Ledger(w) != Ledger(w2)) seedBad.Add("fate ledger differs between identical scripts");
        if (w.Surface.StateHash() != w2.Surface.StateHash()) seedBad.Add("surface state differs between identical scripts");

        // Terrain edits are real: the touched surface differs from a pristine one, and the
        // forest genuinely thickened around the seeded seat.
        var (cfgP, namesP) = Load();
        var pristine = new World(seed, cfgP, namesP);
        pristine.SeedWorld();
        if (pristine.Surface.StateHash() == w.Surface.StateHash()) seedBad.Add("terrain edits left no trace on the surface");
        var forestEv = w.Chronicle.Events.FirstOrDefault(e => e.Tags.Contains("terrain") && e.Tags.Contains("forest"));
        var springEv = w.Chronicle.Events.FirstOrDefault(e => e.Tags.Contains("terrain") && e.Tags.Contains("water"));
        if (forestEv is null || springEv is null) seedBad.Add("terrain acts recorded no events");
        else
        {
            int rid = forestEv.RegionId!.Value;
            var r = w.Regions[rid];
            var (scx, scy) = WorldSurface.CellOf(r.X, r.Y);
            float vegNow = w.Surface.VegetationAt(scx, scy);
            float vegWas = pristine.Surface.VegetationAt(scx, scy);
            if (vegNow <= vegWas) seedBad.Add($"seeded forest did not raise vegetation ({vegWas:0.00} -> {vegNow:0.00})");
        }

        // Channel honesty: divine acts never carry a home anchor; person-target acts carry
        // no place at all; region-target acts are anchored exactly where the hand touched.
        foreach (var e in w.Chronicle.Events.Where(e => e.Type == "divine"))
        {
            if (e.HomeRegionId is not null) seedBad.Add($"divine event #{e.Id} carries a home anchor");
            bool personAct = e.Tags.Contains("curse") || e.Tags.Contains("blessing");
            bool regionAct = e.Tags.Contains("omen") || e.Tags.Contains("terrain");
            if (personAct && e.RegionId is not null) seedBad.Add($"person-target act #{e.Id} claims a place");
            if (regionAct && e.RegionId is null) seedBad.Add($"region-target act #{e.Id} lost its anchor");
        }

        // Cause-link honesty: every pressure-influenced edge points at the recorded act,
        // and the grammar classifies it with the authored rule.
        int curseId = w.CurseEvent!.Id;
        var blessIds = w.DivinePressures.Where(p => p.Kind == DivinePressureKind.Bless)
            .Select(p => p.SourceEventId).ToHashSet();
        var doomId = w.DivinePressures.First(p => p.Kind == DivinePressureKind.Doom).SourceEventId;
        var protectId = w.DivinePressures.First(p => p.Kind == DivinePressureKind.Protect).SourceEventId;
        foreach (var e in w.Chronicle.Events)
        {
            if (e.Causes.Contains(curseId)) curseFallout++;
            if (e.Type == "death" && e.Causes.Any(blessIds.Contains))
            {
                blessLinkedDeaths++;
                var link = StoryGrammar.ProximateLink(w, e)!;
                if (blessIds.Contains(link.CauseEventId) && link.RuleId != "death-despite-blessing")
                    seedBad.Add($"blessed death #{e.Id} classified '{link.RuleId}'");
            }
            if (e.Type == "famine" && e.Causes.Contains(doomId))
            {
                doomLinkedFamines++;
                var link = StoryGrammar.ProximateLink(w, e)!;
                if (link.CauseEventId == doomId && link.RuleId != "famine-under-doom")
                    seedBad.Add($"doomed famine #{e.Id} classified '{link.RuleId}'");
            }
            if (e.Type == "famine" && e.Causes.Contains(protectId))
            {
                var link = StoryGrammar.ProximateLink(w, e)!;
                if (link.CauseEventId == protectId
                    && (link.RuleId != "famine-despite-protection" || link.Kind != ConnectorKind.But))
                    seedBad.Add($"famine under protection #{e.Id} classified '{link.RuleId}' ({link.Kind})");
            }
            // Grammar safety net on the scripted world: But stays authored-only.
            if (e.Causes.Count > 0)
            {
                var l = StoryGrammar.ProximateLink(w, e)!;
                if (l.Kind == ConnectorKind.But && !StoryGrammar.ButRules.Contains(l.RuleId))
                    seedBad.Add($"event #{e.Id} BUT from non-authored rule '{l.RuleId}'");
            }
        }

        Console.WriteLine($"  seed {seed,3}: {(seedBad.Count == 0 ? "OK" : "FAIL")}  {w.Chronicle.Events.Count} events, " +
            $"{w.DivinePressures.Count} pressures, surface {w.Surface.Edits.Count} edits (hash {w.Surface.StateHash():x16})");
        foreach (var b in seedBad.Take(5)) Console.WriteLine($"           {b}");
        bad.AddRange(seedBad);
    }

    // The influences must actually have happened somewhere across the suite — a gate that
    // only checks vacuous conditionals proves nothing.
    Console.WriteLine($"  influence: {curseFallout} curse-fallout, {blessLinkedDeaths} blessed deaths, {doomLinkedFamines} doomed famines");
    if (curseFallout == 0) bad.Add("curse never traced to any fallout");
    if (blessLinkedDeaths == 0) bad.Add("no blessed death ever cause-linked (suite too quiet?)");
    if (doomLinkedFamines == 0) bad.Add("no doomed famine ever cause-linked (suite too quiet?)");

    // The sim stays canon-blind (reflection re-assert; the canon gate proves it behaviorally).
    {
        var canonTypes = new[] { typeof(PlayerCanonStore), typeof(CanonNote), typeof(CanonFile) };
        var simTypes = new[] { typeof(World), typeof(Chronicle), typeof(Event), typeof(Person),
                               typeof(Faction), typeof(Region), typeof(WorldSurface), typeof(DivinePressure) };
        bool Touches(Type t) => canonTypes.Contains(t)
            || (t.IsGenericType && t.GetGenericArguments().Any(Touches));
        const System.Reflection.BindingFlags all =
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static;
        bool blind = simTypes.All(t =>
            t.GetFields(all).All(fl => !Touches(fl.FieldType)) &&
            t.GetProperties(all).All(pr => !Touches(pr.PropertyType)));
        Console.WriteLine($"  canon-blind: {(blind ? "OK" : "FAIL")}");
        if (!blind) bad.Add("a sim type can see the player canon");
    }

    Console.WriteLine(bad.Count == 0 ? "\nDIVINE PRESSURE HOLDS." : $"\n{bad.Count} CHECK(S) BROKE THE CONTRACT.");
    Environment.Exit(bad.Count == 0 ? 0 : 1);
}

// -------------------------------------------------------------------------- sites

// Proof gate for Sites V1 + the Event.SiteId anchoring contract (shipped 2026-06-12, the
// deliberate milestone the old absence-assertion guarded): generation is deterministic
// across double runs, every site stands on a real cell of its own region, type honesty
// holds cell-by-cell, names are unique, EVERY event's SiteId equals the single authored
// convention table (SiteAnchors.Expected — recomputed here, so the rule cannot drift),
// life events stay memory-only, and the replay-beat helper never invents a place.
void SitesCmd(int years)
{
    Console.WriteLine($"Sites gate ({years} yrs): deterministic terrain-honest sites, anchoring");
    Console.WriteLine("conventions hold event-by-event (SiteAnchors.Expected), replay beats honest.");
    int failures = 0;
    int suiteBattles = 0, suiteBattlesSited = 0;

    foreach (int seed in new[] { 1, 18, 42, 7 })
    {
        var (c1, n1) = Load();
        var w = new World(seed, c1, n1); w.Run(years);
        var sites = w.Sites;
        var bad = new List<string>();

        // Coverage + per-site honesty.
        var nameSeen = new HashSet<string>(StringComparer.Ordinal);
        int min = int.MaxValue, max = 0;
        foreach (var region in w.Regions)
        {
            var local = sites.ForRegion(region.Id);
            min = Math.Min(min, local.Count);
            max = Math.Max(max, local.Count);
            if (local.Count is < 3 or > 7)
                bad.Add($"region {region.Id} has {local.Count} sites (want 3..7)");
            if (local.Count > 0 && !local[0].IsSeat)
                bad.Add($"region {region.Id} first site is not the seat");
        }
        for (int si = 0; si < sites.All.Count; si++)
        {
            var s = sites.All[si];
            if (s.Id != si) bad.Add($"site id {s.Id} is not its index {si}");
            if (s.RegionId < 0 || s.RegionId >= w.Regions.Count)
                bad.Add($"site {s.Id} names region {s.RegionId} which does not exist");
            if (s.CellX < 0 || s.CellX >= WorldSurface.Size || s.CellY < 0 || s.CellY >= WorldSurface.Size)
                bad.Add($"site {s.Id} cell ({s.CellX},{s.CellY}) out of bounds");
            else
            {
                if (w.Surface.RegionAt(s.CellX, s.CellY) != s.RegionId)
                    bad.Add($"site {s.Id} ({s.Name}) stands outside its own region");
                if (!SiteIndex.FitsCell(w.Surface, s.CellX, s.CellY, s.Type))
                    bad.Add($"site {s.Id} ({s.Name}) claims {s.Type} on land that contradicts it");
            }
            if (string.IsNullOrWhiteSpace(s.Name)) bad.Add($"site {s.Id} has no name");
            else if (!nameSeen.Add(s.Name)) bad.Add($"site name '{s.Name}' is not unique");
            // The holder is derived, never stored: it must equal the region's holder, always.
            string? holder = SiteIndex.HolderOf(w, s);
            if (holder != w.Regions[s.RegionId].ControllingFactionId)
                bad.Add($"site {s.Id} holder drifted from its region's holder");
        }

        // Determinism: a second identical run yields a byte-identical site index — and the
        // index built AFTER terraform edits is identical too (it derives from the pristine
        // surface by construction).
        var (c2, n2) = Load();
        var w2 = new World(seed, c2, n2);
        w2.SeedWorld();
        _ = w2.SeedForest(w2.Regions[0].Id);   // edit FIRST — sites must not see it
        for (int i = 0; i < years; i++) w2.Tick();
        if (sites.CanonString() != w2.Sites.CanonString())
            bad.Add("site index differs between identical runs (or saw a terraform edit)");

        // Anchor channels still hold (the homes gate proves this fully; re-assert cheaply).
        foreach (var e in w.Chronicle.Events.Where(e => e.Type is "birth" or "death" or "murder"))
            if (e.RegionId is not null || e.SiteId is not null)
            { bad.Add($"life event #{e.Id} claims a literal place"); break; }

        // The anchoring contract, event by event: SiteId must equal the ONE authored
        // convention table — recomputed here so the rule can never drift in World alone.
        int anchored = 0;
        foreach (var e in w.Chronicle.Events)
        {
            int? expect = SiteAnchors.Expected(w, e.Type, e.Tags, e.RegionId);
            if (e.SiteId != expect)
            { bad.Add($"event #{e.Id} ({e.Type}) anchors to site {e.SiteId?.ToString() ?? "-"}, convention says {expect?.ToString() ?? "-"}"); break; }
            if (e.SiteId is int esid)
            {
                anchored++;
                if (esid < 0 || esid >= sites.All.Count)
                { bad.Add($"event #{e.Id} anchors to site {esid} which does not exist"); break; }
                if (e.RegionId is not int erid2 || sites.Get(esid).RegionId != erid2)
                { bad.Add($"event #{e.Id}'s site anchor lies outside its own region"); break; }
            }
        }

        // Battle Sites V1: war/battle events anchor to the front's stronghold; battle deaths
        // stay home-remembered, never claiming the battle's ground (the channels never mix).
        int battles = w.Chronicle.Events.Count(e => e.Type == "battle");
        int battlesSited = w.Chronicle.Events.Count(e => e.Type == "battle" && e.SiteId is not null);
        suiteBattles += battles; suiteBattlesSited += battlesSited;
        foreach (var e in w.Chronicle.Events.Where(e => e.Type == "battle"))
        {
            if (e.HomeRegionId is not null)
            { bad.Add($"battle #{e.Id} carries a home anchor (battles are placed, not remembered)"); break; }
            if (e.SiteId is not null && e.RegionId is null)
            { bad.Add($"battle #{e.Id} has a site but no region"); break; }
        }

        // Replay beats: honest and deterministic. Walk the most-caused event's chain.
        var target = w.Chronicle.Events.LastOrDefault(e => e.Causes.Count > 0);
        if (target is not null)
        {
            // Beats compare against a CLEAN identical run — w2 carries a terraform edit,
            // whose recorded event honestly shifts its chronicle ids.
            var (c3, n3) = Load();
            var w3 = new World(seed, c3, n3); w3.Run(years);
            var beats = Replay.BeatsFor(w, target.Id);
            var beats2 = Replay.BeatsFor(w3, target.Id);
            for (int i = 1; i < beats.Count; i++)
                if (beats[i].EventId <= beats[i - 1].EventId)
                { bad.Add("replay beats not in record order"); break; }
            foreach (var b in beats)
            {
                var e = w.Chronicle.Get(b.EventId);
                if (b.SiteId != e.SiteId) { bad.Add($"replay beat #{b.EventId} re-aimed its site anchor"); break; }
                if (b.RegionId != e.RegionId) { bad.Add($"replay beat #{b.EventId} re-aimed its region anchor"); break; }
                if (b.CauseEventId is int cid && !e.Causes.Contains(cid))
                { bad.Add($"replay beat #{b.EventId} claims a cause not in its Causes"); break; }
            }
            string Canon(List<ReplayBeat> bs) => string.Join("\n", bs.Select(b =>
                $"{b.EventId}|{b.Year}|{b.RegionId?.ToString() ?? "-"}|{b.SiteId?.ToString() ?? "-"}|{b.Connector}|{b.CauseEventId?.ToString() ?? "-"}|{b.Category}"));
            if (Canon(beats) != Canon(beats2)) bad.Add("replay beats differ between identical runs");
        }

        var typeCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var s in sites.All)
            typeCounts[SiteIndex.TypeLabel(s.Type)] = typeCounts.GetValueOrDefault(SiteIndex.TypeLabel(s.Type)) + 1;
        Console.WriteLine($"  seed {seed,3}: {(bad.Count == 0 ? "OK" : "FAIL")}  {sites.All.Count} sites over {w.Regions.Count} regions ({min}-{max}/region) · {anchored} site-anchored events · {battles} battles ({battlesSited} sited)");
        Console.WriteLine($"           {string.Join(", ", typeCounts.Select(kv => $"{kv.Key} {kv.Value}"))}");
        foreach (var b in bad.Take(5)) Console.WriteLine($"           {b}");
        if (bad.Count > 0) failures++;
    }

    // Non-vacuous: the battle convention must actually have been exercised across the suite,
    // and at least one battle must have anchored to a real stronghold site.
    Console.WriteLine($"  battles across the suite: {suiteBattles} ({suiteBattlesSited} site-anchored)");
    if (suiteBattles == 0) { Console.WriteLine("           no battles fought — the war engine recorded none"); failures++; }
    if (suiteBattlesSited == 0) { Console.WriteLine("           no battle ever anchored to a site (convention vacuous?)"); failures++; }

    Console.WriteLine(failures == 0 ? "\nSITES CONTRACT HOLDS." : $"\n{failures} CHECK(S) BROKE THE CONTRACT.");
    Environment.Exit(failures == 0 ? 0 : 1);
}

// --------------------------------------------------------------------------- save

// Proof gate for the world-save contract (Persistence V1): the save is an INPUT JOURNAL —
// divine acts with years + identity snapshots, follows, attention state. It must roundtrip,
// replay deterministically (a fresh run + the journal == the original session's world),
// never touch a clean sim unless explicitly applied, quarantine drifted targets, preserve
// corrupt/future files, and stay fully apart from the player canon.
void SaveCmd(int years)
{
    Console.WriteLine($"World-save gate ({years} yrs): the journal roundtrips, replays deterministically,");
    Console.WriteLine("never alters a clean sim unless applied, and drifted targets quarantine.");
    var bad = new List<string>();
    void Check(string name, bool ok, string? detail = null)
    {
        Console.WriteLine($"  {name}: {(ok ? "OK" : "FAIL")}{(ok || detail is null ? "" : "  " + detail)}");
        if (!ok) bad.Add(name);
    }

    string path = Path.Combine(Path.GetTempPath(), $"lm_save_gate_{Guid.NewGuid():N}.json");
    string canonPath = Path.Combine(Path.GetTempPath(), $"lm_save_gate_canon_{Guid.NewGuid():N}.json");
    try
    {
        string Ledger(World w) => string.Join("\n", w.DivinePressures.Select(p =>
            $"{p.Id}|{p.Kind}|{p.TargetType}|{p.TargetId}|{p.StartYear}|{p.SourceEventId}|{p.ExpiresYear?.ToString() ?? "-"}"));

        // The "live session": acts at year 0, more mid-run, all journaled as they land.
        (World w, PlayerWorldStore store) LiveSession()
        {
            var (cfg, names) = Load();
            var w = new World(7, cfg, names);
            w.SeedWorld();
            var (store, _) = PlayerWorldStore.LoadOrNew(path, 7);
            void Act(Event? ev)
            {
                if (ev is null) return;
                var pr = w.DivinePressures.Last(p => p.SourceEventId == ev.Id);
                store.RecordAct(w, pr);
            }
            var f = w.Config.Factions;
            var elder = w.FactionMembers(f[0].Id).OrderByDescending(p => p.Age(w.Year)).ThenBy(p => p.Id).First();
            Act(w.BlessPerson(elder));
            Act(w.ProtectFaction(f[0].Id));
            foreach (var r in w.Regions) if (w.SeedForest(r.Id) is Event fe) { Act(fe); break; }
            for (int y = 0; y < years; y++)
            {
                w.Tick();
                if (w.Year == 20)
                {
                    var cursee = w.FactionMembers(f[1].Id).Where(p => p.Age(w.Year) >= 18)
                        .OrderBy(p => p.Age(w.Year)).ThenBy(p => p.Id).First();
                    Act(w.PlantCurse(cursee));
                    Act(w.DoomFaction(f[1].Id));
                }
                if (w.Year == 35)
                {
                    int target = w.Factions[f[2].Id].ControlledRegions.Count > 0
                        ? w.Factions[f[2].Id].ControlledRegions.Select(int.Parse).Min() : 0;
                    Act(w.SeedOmen(target));
                    foreach (var r in w.Regions) if (w.CallSpring(r.Id) is Event se) { Act(se); break; }
                }
            }
            return (w, store);
        }

        // 1. Missing file -> empty writable store, no file conjured.
        var (s0, warn0) = PlayerWorldStore.LoadOrNew(path, 7);
        Check("load-missing", s0.ActCount == 0 && warn0 is null && !File.Exists(path) && !s0.ReadOnly);

        // 2. Live session journals + saves; the file roundtrips deep-equal.
        var (live, liveStore) = LiveSession();
        var souls = new[] { live.Living()[0].Id };
        var lands = new[] { 0, 3 };
        liveStore.SetFollows(live, souls, new[] { live.Living()[1].Id }, new[] { "highland" }, lands);
        liveStore.SetLastSeen(new Dictionary<int, int> { [souls[0]] = live.Chronicle.Events.Count - 1 });
        liveStore.ResumeYear = live.Year;
        liveStore.Save();
        var (s1, warn1) = PlayerWorldStore.LoadOrNew(path, 7);
        bool same = warn1 is null && s1.ActCount == liveStore.ActCount && s1.ResumeYear == live.Year
            && s1.Follows.Souls.SequenceEqual(liveStore.Follows.Souls)
            && s1.Follows.Bloodlines.SequenceEqual(liveStore.Follows.Bloodlines)
            && s1.Follows.Peoples.SequenceEqual(liveStore.Follows.Peoples)
            && s1.Follows.Lands.SequenceEqual(liveStore.Follows.Lands)
            && s1.LastSeen.Count == 1 && s1.LastSeen[souls[0]] == live.Chronicle.Events.Count - 1;
        for (int i = 0; same && i < s1.ActCount; i++)
        {
            var a = liveStore.Acts[i];
            var b = s1.Acts[i];
            same &= a.Seq == b.Seq && a.Kind == b.Kind && a.TargetType == b.TargetType
                 && a.TargetId == b.TargetId && a.Year == b.Year
                 && a.Snapshot.Count == b.Snapshot.Count
                 && a.Snapshot.All(kv => b.Snapshot.TryGetValue(kv.Key, out var v) && v == kv.Value);
        }
        Check("roundtrip", same);

        // 3. Replay determinism: a fresh world + the journal == the live session's world,
        //    chronicle, fate ledger, and surface all byte-identical.
        World Replay(PlayerWorldStore store)
        {
            var (cfg, names) = Load();
            var w = new World(7, cfg, names);
            w.SeedWorld();
            store.ApplyDue(w);
            for (int y = 0; y < years; y++) { w.Tick(); store.ApplyDue(w); }
            return w;
        }
        var (s2, _) = PlayerWorldStore.LoadOrNew(path, 7);
        var replayed = Replay(s2);
        Check("replay-deterministic",
            replayed.Chronicle.Render() == live.Chronicle.Render()
            && Ledger(replayed) == Ledger(live)
            && replayed.Surface.StateHash() == live.Surface.StateHash()
            && s2.QuarantinedActs.Count == 0,
            $"events {replayed.Chronicle.Events.Count} vs {live.Chronicle.Events.Count}, quarantined {s2.QuarantinedActs.Count}");

        // 4. Edits restore: the replayed surface genuinely differs from a pristine one
        //    (the forest and spring came back without the player's hand this session).
        var (cfgP, namesP) = Load();
        var pristine = new World(7, cfgP, namesP);
        pristine.SeedWorld();
        for (int y = 0; y < years; y++) pristine.Tick();
        Check("edits-restore", replayed.Surface.StateHash() != pristine.Surface.StateHash()
            && replayed.Surface.Edits.Count == live.Surface.Edits.Count);

        // 5. A loaded-but-unapplied journal alters nothing: a clean run with the store
        //    merely in scope is byte-identical to a pristine run.
        var (s3, _) = PlayerWorldStore.LoadOrNew(path, 7);
        var (cfgC, namesC) = Load();
        var untouched = new World(7, cfgC, namesC);
        untouched.SeedWorld();
        for (int y = 0; y < years; y++) untouched.Tick();
        Check("unapplied-inert", untouched.Chronicle.Render() == pristine.Chronicle.Render()
            && untouched.DivinePressures.Count == 0 && s3.ActCount > 0);

        // 6. Follows restore deterministically against the replayed world; an invalid
        //    land id (file tampering / drift) is dropped, never half-applied.
        var (fSouls, fLines, fPeoples, fLands, fDropped) = s2.RestoreFollows(replayed);
        Check("follows-restore", fSouls.SequenceEqual(souls) && fPeoples.SequenceEqual(new[] { "highland" })
            && fLands.SequenceEqual(lands) && fLines.Count == 1 && fDropped.Count == 0);
        s2.Follows.Lands.Add(9999);
        var (_, _, _, fLands2, fDropped2) = s2.RestoreFollows(replayed);
        Check("follows-quarantine", fLands2.SequenceEqual(lands) && fDropped2.Count == 1);

        // 7. A drifted act target quarantines on replay: skipped, kept, never misapplied.
        var (s4, _) = PlayerWorldStore.LoadOrNew(path, 7);
        var blessAct = s4.Acts.First(a => a.Kind == "bless");
        blessAct.Snapshot["name"] = "Someone Else Entirely";
        var replayedDrift = Replay(s4);
        bool blessApplied = replayedDrift.DivinePressures.Any(p => p.Kind == DivinePressureKind.Bless);
        s4.Save();
        var (s5, _) = PlayerWorldStore.LoadOrNew(path, 7);
        // Skipping the bless honestly diverges the drift-world, so LATER person-target
        // acts may quarantine too (their pids can belong to different souls there) —
        // the contract is: the tampered act never applies, and nothing is destroyed.
        Check("act-quarantine", !blessApplied && s4.QuarantinedActs.Contains(blessAct)
            && s5.ActCount == s4.ActCount, $"quarantined {s4.QuarantinedActs.Count}");

        // 8. Corrupt file: preserved byte-for-byte, store read-only, writes refuse.
        File.WriteAllText(path, "{ this is not json");
        var (sBad, warnBad) = PlayerWorldStore.LoadOrNew(path, 7);
        bool saveThrew = false;
        try { sBad.Save(); } catch (InvalidOperationException) { saveThrew = true; }
        Check("corrupt-file", sBad.ActCount == 0 && warnBad is not null && sBad.ReadOnly && !sBad.FutureSchema
                           && saveThrew && File.ReadAllText(path) == "{ this is not json");

        // 9. Future schema: preserved untouched, read-only, flagged.
        File.WriteAllText(path, "{\"schema_version\": 99, \"seed\": 7, \"acts\": []}");
        var (sFut, warnFut) = PlayerWorldStore.LoadOrNew(path, 7);
        Check("future-schema", sFut.ActCount == 0 && warnFut is not null && sFut.ReadOnly && sFut.FutureSchema);

        // 10. Canon separation: the world save never touches the canon file, and no sim
        //     type can see the world-save types (reflection, same proof as the canon gate).
        File.WriteAllText(canonPath, "{\"schema_version\": 1, \"seed\": 7, \"notes\": []}");
        string canonBytes = File.ReadAllText(canonPath);
        var saveTypes = new[] { typeof(PlayerWorldStore), typeof(WorldAct), typeof(WorldSaveFile), typeof(WorldFollows) };
        var simTypes = new[] { typeof(World), typeof(Chronicle), typeof(Event), typeof(Person),
                               typeof(Faction), typeof(Religion), typeof(Region), typeof(WorldSurface),
                               typeof(DivinePressure), typeof(Rng) };
        bool Touches(Type t) => saveTypes.Contains(t)
            || (t.IsGenericType && t.GetGenericArguments().Any(Touches));
        const System.Reflection.BindingFlags all =
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static;
        bool blind = simTypes.All(t =>
            t.GetFields(all).All(fl => !Touches(fl.FieldType)) &&
            t.GetProperties(all).All(pr => !Touches(pr.PropertyType)));
        Check("canon-separate-and-sim-blind", blind && File.ReadAllText(canonPath) == canonBytes);
    }
    finally
    {
        foreach (var p in new[] { path, path + ".tmp", canonPath, canonPath + ".tmp" })
            if (File.Exists(p)) File.Delete(p);
    }

    Console.WriteLine(bad.Count == 0 ? "\nWORLD SAVE CONTRACT HOLDS." : $"\n{bad.Count} CHECK(S) BROKE THE CONTRACT.");
    Environment.Exit(bad.Count == 0 ? 0 : 1);
}

// ------------------------------------------------------------------------- verify

// -------------------------------------------------------------------------- harvest

// Proof gate for Harvest Economy V1: the per-region harvest is the economy's ground truth and
// faction Prosperity derives from it. Proves, event-by-event and faction-by-faction:
//   (1) derivation — Prosperity == the mean of controlled-region Harvests; InFamine/FamineEvent/
//       InBoom are the worst/any rollup of the region flags;
//   (2) landless neutrality — a people holding no land reads Prosperity 1.0, never in famine;
//   (3) anchoring — every famine/boom/famine_end carries a valid RegionId and NEVER a SiteId
//       (the convention table agrees: SiteAnchors.Expected is null for all three);
//   (4) famine_end — each one answers an earlier famine in the SAME region (cause + region match);
//   (5) channel honesty — economy events never carry a home anchor; famine deaths keep RegionId
//       null (the grief stays home-memory anchored), re-asserted cheaply;
//   (6) determinism — a second identical run yields a byte-identical harvest state.
void HarvestCmd(int years)
{
    Console.WriteLine($"Harvest gate ({years} yrs): per-region harvest is ground truth; faction");
    Console.WriteLine("Prosperity derives from it; famine/plenty/famine_end anchor to land, not site.");
    int failures = 0;
    int suiteAnchored = 0, suiteFamineEnds = 0, suiteLandless = 0;

    // Terrain-Typed Harvest V1 non-vacuity, pooled across the WHOLE seed suite (single-seed
    // snapshots of the small plains biome are too noisy): harvest values grouped by terrain, and
    // cumulative famine counts grouped by the terrain of their region. Both robust over the full run.
    var suiteHarvestByTerrain = new Dictionary<string, List<double>>();
    var suiteFaminesByTerrain = new Dictionary<string, int>();

    foreach (int seed in new[] { 1, 18, 42, 7 })
    {
        var (c1, n1) = Load();
        var w = new World(seed, c1, n1);
        // Run year-by-year so terrain-mean harvest is TIME-averaged over the whole run (the robust
        // measure of where each terrain's walk centers, governed by its revert target) — an
        // end-of-run snapshot of the high-volatility plains biome is too noisy to order reliably.
        var seedHarvestByTerrain = new Dictionary<string, List<double>>();
        w.SeedWorld();
        for (int yr = 0; yr < years; yr++)
        {
            w.Tick();
            foreach (var r in w.Regions)
            {
                if (!suiteHarvestByTerrain.TryGetValue(r.TerrainType, out var sl)) { sl = new(); suiteHarvestByTerrain[r.TerrainType] = sl; }
                sl.Add(r.Harvest);
                if (!seedHarvestByTerrain.TryGetValue(r.TerrainType, out var dl)) { dl = new(); seedHarvestByTerrain[r.TerrainType] = dl; }
                dl.Add(r.Harvest);
            }
        }
        var bad = new List<string>();

        // (1)+(2) Derivation + landless neutrality, faction by faction.
        foreach (var f in w.Config.Factions.Select(cf => w.Factions[cf.Id]))
        {
            var owned = f.ControlledRegions.Select(int.Parse).OrderBy(x => x).ToList();
            if (owned.Count == 0)
            {
                suiteLandless++;
                if (f.Prosperity != 1.0 || f.InFamine || f.InBoom || f.FamineEvent is not null)
                    bad.Add($"landless {f.Id} not neutral (P={f.Prosperity}, famine={f.InFamine}, boom={f.InBoom})");
                continue;
            }
            double sum = 0.0; bool anyBoom = false; Region? worst = null;
            foreach (var rid in owned)
            {
                var r = w.Regions[rid];
                sum += r.Harvest;
                if (r.InBoom) anyBoom = true;
                if (r.InFamine && (worst is null || r.Harvest < worst.Harvest)) worst = r;
            }
            if (Math.Abs(f.Prosperity - sum / owned.Count) > 1e-9)
                bad.Add($"{f.Id} Prosperity {f.Prosperity} != mean harvest {sum / owned.Count}");
            if (f.InFamine != (worst is not null))
                bad.Add($"{f.Id} InFamine {f.InFamine} disagrees with its lands");
            if (!ReferenceEquals(f.FamineEvent, worst?.FamineEvent))
                bad.Add($"{f.Id} FamineEvent is not its worst land's onset");
            if (f.InBoom != anyBoom)
                bad.Add($"{f.Id} InBoom {f.InBoom} disagrees with its lands");
        }

        // (3)+(4)+(5) Event anchoring, famine_end pairing, channel honesty.
        var faminesByRegion = new Dictionary<int, List<Event>>();
        int anchored = 0, famineEnds = 0;
        foreach (var e in w.Chronicle.Events)
        {
            if (e.Type is not ("famine" or "boom" or "famine_end")) continue;
            anchored++;
            if (e.RegionId is not int rid || rid < 0 || rid >= w.Regions.Count)
            { bad.Add($"{e.Type} #{e.Id} has no valid RegionId"); break; }
            if (e.SiteId is not null)
            { bad.Add($"{e.Type} #{e.Id} leaked a SiteId ({e.SiteId})"); break; }
            if (e.HomeRegionId is not null)
            { bad.Add($"{e.Type} #{e.Id} carries a home anchor (economy is placed, not remembered)"); break; }
            // The ONE convention table must agree these never anchor to a site.
            if (SiteAnchors.Expected(w, e.Type, e.Tags, e.RegionId) is int leak)
            { bad.Add($"convention anchors {e.Type} #{e.Id} to site {leak} — expected none"); break; }
            if (e.Type == "famine")
            {
                if (!faminesByRegion.TryGetValue(rid, out var fl)) { fl = new(); faminesByRegion[rid] = fl; }
                fl.Add(e);
            }
            else if (e.Type == "famine_end")
            {
                famineEnds++;
                var onset = e.Causes.Select(cid => w.Chronicle.Get(cid))
                    .FirstOrDefault(c => c.Type == "famine" && c.RegionId == rid && c.Year <= e.Year);
                if (onset is null)
                { bad.Add($"famine_end #{e.Id} answers no earlier famine in region {rid}"); break; }
            }
        }
        // Per region, recoveries never outnumber the hungers they answer.
        foreach (var (rid, fl) in faminesByRegion)
        {
            int ends = w.Chronicle.Events.Count(e => e.Type == "famine_end" && e.RegionId == rid);
            if (ends > fl.Count)
                bad.Add($"region {rid} has {ends} famine_end > {fl.Count} famine");
        }
        // Famine deaths keep the home-memory channel: caused by a famine, but RegionId stays null.
        foreach (var e in w.Chronicle.Events.Where(e => e.Type is "death" or "murder" or "birth"))
            if (e.RegionId is not null || e.SiteId is not null)
            { bad.Add($"life event #{e.Id} claims a literal place"); break; }

        suiteAnchored += anchored; suiteFamineEnds += famineEnds;

        // (B) Pool famine counts by the terrain of their region (cumulative over the full run).
        foreach (var e in w.Chronicle.Events)
        {
            if (e.Type != "famine" || e.RegionId is not int frid || frid < 0 || frid >= w.Regions.Count) continue;
            string t = w.Regions[frid].TerrainType;
            suiteFaminesByTerrain[t] = suiteFaminesByTerrain.GetValueOrDefault(t) + 1;
        }

        // (6) Determinism: a second identical run yields byte-identical harvest state.
        var (c2, n2) = Load();
        var w2 = new World(seed, c2, n2); w2.Run(years);
        if (HarvestCanon(w) != HarvestCanon(w2))
            bad.Add("harvest state differs between identical runs");

        Console.WriteLine($"  seed {seed,3}: {(bad.Count == 0 ? "OK" : "FAIL")}  {anchored} land-anchored economy events · {famineEnds} famine_end · {w.Regions.Count} regions");
        string means = string.Join("  ", seedHarvestByTerrain.OrderBy(kv => kv.Key)
            .Select(kv => $"{kv.Key}={kv.Value.Average():F3}(n{kv.Value.Count})"));
        Console.WriteLine($"           terrain means: {means}");
        foreach (var b in bad.Take(5)) Console.WriteLine($"           {b}");
        if (bad.Count > 0) failures++;
    }

    Console.WriteLine($"  suite: {suiteAnchored} land-anchored economy events, {suiteFamineEnds} recoveries, {suiteLandless} landless-faction checks");

    // (A) Suite-pooled terrain-MEAN ordering — proves the fertility lever (per-terrain revert
    // target). Fertility is balance-constrained: a strong sub-1.0 highland mean dooms highland-heavy
    // seeds (it chronically suppresses faction prosperity), so the balance-safe band keeps means in a
    // TIGHT band. Plains-fertility is the one robust mean signal (target 1.05 well clear of forest);
    // highland-poorer is real but small, asserted with a smaller margin. Coast-safety is proven by
    // famine RATE in (B), not mean (at the safe band coast/highland means are near-equal by design).
    var suiteBad = new List<string>();
    const double plainsMargin = 0.02;     // plains is clearly more fertile
    const double highlandMargin = 0.005;  // highland is poorer — small but a stable n>3000 suite mean
    double? Mean(string t) => suiteHarvestByTerrain.TryGetValue(t, out var l) && l.Count > 0 ? l.Average() : (double?)null;
    double? hi = Mean("highland"), fo = Mean("forest"), pl = Mean("plains");
    Console.WriteLine($"  suite terrain means: " + string.Join("  ", suiteHarvestByTerrain.OrderBy(kv => kv.Key)
        .Select(kv => $"{kv.Key}={kv.Value.Average():F3}(n{kv.Value.Count})")));
    if (hi is double mh && fo is double mf1 && !(mf1 - mh > highlandMargin))
        suiteBad.Add($"highland mean {mh:F3} not < forest mean {mf1:F3} by {highlandMargin} (terrain not harsher)");
    if (pl is double mp && fo is double mf2 && !(mp - mf2 > plainsMargin))
        suiteBad.Add($"plains mean {mp:F3} not > forest mean {mf2:F3} by {plainsMargin} (terrain not fertile)");

    // (B) Famine-RATE concentration by terrain (the robust harshness/safety proof — integrates the
    // volatility lever over the whole run, normalized per region·year so region-count can't skew it):
    // highland must famine MORE than forest (harsher), coast must famine LESS than highland (safer/steady).
    int hiFam = suiteFaminesByTerrain.GetValueOrDefault("highland");
    int coFam = suiteFaminesByTerrain.GetValueOrDefault("coast");
    int foFam = suiteFaminesByTerrain.GetValueOrDefault("forest");
    int hiExposure = suiteHarvestByTerrain.GetValueOrDefault("highland")?.Count ?? 0;  // region·year samples
    int coExposure = suiteHarvestByTerrain.GetValueOrDefault("coast")?.Count ?? 0;
    int foExposure = suiteHarvestByTerrain.GetValueOrDefault("forest")?.Count ?? 0;
    double hiFamRate = hiExposure > 0 ? (double)hiFam / hiExposure : 0;                 // famines per region·year
    double coFamRate = coExposure > 0 ? (double)coFam / coExposure : 0;
    double foFamRate = foExposure > 0 ? (double)foFam / foExposure : 0;
    Console.WriteLine($"  suite famines by terrain: " + string.Join("  ", suiteFaminesByTerrain.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}")));
    if (hiExposure > 0 && hiFam == 0)
        suiteBad.Add("highland regions exist but never famined (terrain volatility inert)");
    if (foExposure > 0 && hiExposure > 0 && !(hiFamRate > foFamRate))
        suiteBad.Add($"highland famine rate {hiFamRate:F4} not > forest {foFamRate:F4} (highland not harsher)");
    if (coFamRate > hiFamRate)
        suiteBad.Add($"coast famine rate {coFamRate:F4} > highland {hiFamRate:F4} (terrain risk inverted)");

    foreach (var b in suiteBad) Console.WriteLine($"  SUITE FAIL: {b}");
    if (suiteBad.Count > 0) failures++;

    Console.WriteLine(failures == 0 ? "\nHARVEST ECONOMY HOLDS." : $"\n{failures} SEED(S) FAILED.");
    Environment.Exit(failures == 0 ? 0 : 1);
}

string HarvestCanon(World w)
{
    var sb = new System.Text.StringBuilder();
    foreach (var r in w.Regions)
        sb.Append(r.Id).Append('|').Append(r.Harvest.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
          .Append('|').Append(r.InFamine ? 1 : 0).Append('|').Append(r.InBoom ? 1 : 0).Append('\n');
    foreach (var f in w.Config.Factions.Select(cf => w.Factions[cf.Id]))
        sb.Append(f.Id).Append('=').Append(f.Prosperity.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
    return sb.ToString();
}

void PlagueCmd(int years)
{
    Console.WriteLine($"Plague gate ({years} yrs): per-region pestilence drives plague; faction InPlague");
    Console.WriteLine("derives from its worst-stricken land; plague/plague_end anchor to land, not site;");
    Console.WriteLine("contagion reads a frozen snapshot (order-independent) and the stream is deterministic.");
    int failures = 0;
    int suitePlagues = 0, suitePlagueEnds = 0, suiteSpreadEvidence = 0, suiteLandless = 0;

    foreach (int seed in new[] { 1, 18, 42, 7 })
    {
        var (c1, n1) = Load();
        var w = new World(seed, c1, n1); w.Run(years);
        var bad = new List<string>();

        // (1)+(2) Derivation + landless neutrality, faction by faction (final-tick state: the rollup
        // ran last in Pestilence(), so faction flags must equal the worst-stricken controlled land).
        foreach (var f in w.Config.Factions.Select(cf => w.Factions[cf.Id]))
        {
            var owned = f.ControlledRegions.Select(int.Parse).OrderBy(x => x).ToList();
            if (owned.Count == 0)
            {
                suiteLandless++;
                if (f.InPlague || f.PlagueEvent is not null)
                    bad.Add($"landless {f.Id} not plague-neutral (plague={f.InPlague})");
                continue;
            }
            Region? worst = null;
            foreach (var rid in owned)
            {
                var r = w.Regions[rid];
                if (r.InPlague && (worst is null || r.Pestilence > worst.Pestilence)) worst = r;
            }
            if (f.InPlague != (worst is not null))
                bad.Add($"{f.Id} InPlague {f.InPlague} disagrees with its lands");
            if (!ReferenceEquals(f.PlagueEvent, worst?.PlagueEvent))
                bad.Add($"{f.Id} PlagueEvent is not its worst-stricken land's onset");
        }

        // (3)+(4)+(5) Event anchoring (RegionId valid, SiteId/HomeRegionId null, convention agrees
        // none), plague_end pairs to a same-region earlier onset.
        var plaguesByRegion = new Dictionary<int, List<Event>>();
        int plagues = 0, plagueEnds = 0;
        foreach (var e in w.Chronicle.Events)
        {
            if (e.Type is not ("plague" or "plague_end")) continue;
            if (e.RegionId is not int rid || rid < 0 || rid >= w.Regions.Count)
            { bad.Add($"{e.Type} #{e.Id} has no valid RegionId"); break; }
            if (e.SiteId is not null)
            { bad.Add($"{e.Type} #{e.Id} leaked a SiteId ({e.SiteId})"); break; }
            if (e.HomeRegionId is not null)
            { bad.Add($"{e.Type} #{e.Id} carries a home anchor (a plague is placed, not remembered)"); break; }
            if (SiteAnchors.Expected(w, e.Type, e.Tags, e.RegionId) is int leak)
            { bad.Add($"convention anchors {e.Type} #{e.Id} to site {leak} — expected none"); break; }
            if (e.Type == "plague")
            {
                plagues++;
                if (!plaguesByRegion.TryGetValue(rid, out var pl)) { pl = new(); plaguesByRegion[rid] = pl; }
                pl.Add(e);
            }
            else
            {
                plagueEnds++;
                var onset = e.Causes.Select(cid => w.Chronicle.Get(cid))
                    .FirstOrDefault(c => c.Type == "plague" && c.RegionId == rid && c.Year <= e.Year);
                if (onset is null)
                { bad.Add($"plague_end #{e.Id} answers no earlier plague in region {rid}"); break; }
            }
        }
        // Per region, recoveries never outnumber the outbreaks they answer.
        foreach (var (rid, pl) in plaguesByRegion)
        {
            int ends = w.Chronicle.Events.Count(e => e.Type == "plague_end" && e.RegionId == rid);
            if (ends > pl.Count)
                bad.Add($"region {rid} has {ends} plague_end > {pl.Count} plague");
        }
        // (6) Plague deaths keep the home-memory channel: a plague death is caused by the outbreak,
        // but the death itself stays home-anchored — RegionId/SiteId null (the four channels never mix).
        foreach (var e in w.Chronicle.Events.Where(e => e.Type is "death" or "murder" or "birth"))
            if (e.RegionId is not null || e.SiteId is not null)
            { bad.Add($"life event #{e.Id} claims a literal place"); break; }
        // A plague death's cause must actually be a plague onset (cause-link honesty).
        foreach (var e in w.Chronicle.Events.Where(e => e.Type == "death" && e.Text.Contains("in the pestilence")))
            if (!e.Causes.Select(cid => w.Chronicle.Get(cid)).Any(c => c.Type == "plague"))
            { bad.Add($"pestilence death #{e.Id} is not cause-linked to a plague"); break; }

        // (7) Snapshot contagion is deterministic: pestilence state (the order-sensitive part — a
        // live-neighbour read would diverge here) is byte-identical across an independent run.
        var (c2, n2) = Load();
        var w2 = new World(seed, c2, n2); w2.Run(years);
        if (PlagueCanon(w) != PlagueCanon(w2))
            bad.Add("pestilence/plague state differs between identical runs (contagion not order-independent)");
        // (8) Double-run determinism: the whole chronicle is byte-identical.
        if (w.Chronicle.Render() != w2.Chronicle.Render())
            bad.Add("chronicle differs between identical runs");

        // Spread evidence (reporting + suite non-vacuity): outbreaks whose region had an adjacent
        // region already infected the PRIOR year — contagion actually carried, not just sparked.
        int spread = 0;
        foreach (var (rid, pl) in plaguesByRegion)
            foreach (var onset in pl)
            {
                bool neighbourInfectedPrior = w.Regions[rid].AdjacentRegionIds.Any(nid =>
                    w.Chronicle.Events.Any(pe => pe.Type == "plague" && pe.RegionId == nid
                        && pe.Year <= onset.Year
                        && !w.Chronicle.Events.Any(pe2 => pe2.Type == "plague_end" && pe2.RegionId == nid
                            && pe2.Year < onset.Year && pe2.Year >= pe.Year)));
                if (neighbourInfectedPrior) { spread++; break; }
            }

        suitePlagues += plagues; suitePlagueEnds += plagueEnds; suiteSpreadEvidence += spread;
        Console.WriteLine($"  seed {seed,3}: {(bad.Count == 0 ? "OK" : "FAIL")}  {plagues} outbreaks · {plagueEnds} burned out · {spread} region(s) lit by a plagued neighbour");
        foreach (var b in bad.Take(5)) Console.WriteLine($"           {b}");
        if (bad.Count > 0) failures++;
    }

    Console.WriteLine($"  suite: {suitePlagues} outbreaks, {suitePlagueEnds} recoveries, {suiteSpreadEvidence} contagion-spread cases, {suiteLandless} landless checks");

    // (9) Non-vacuity: the engine must actually fire (else every contract above is vacuously true).
    if (suitePlagues == 0)
    {
        Console.WriteLine("  SUITE FAIL: no plague ever broke out — the engine is inert, contracts vacuous");
        failures++;
    }

    Console.WriteLine(failures == 0 ? "\nDISEASE & PLAGUE HOLDS." : $"\n{failures} SEED(S) FAILED.");
    Environment.Exit(failures == 0 ? 0 : 1);
}

string PlagueCanon(World w)
{
    var sb = new System.Text.StringBuilder();
    foreach (var r in w.Regions)
        sb.Append(r.Id).Append('|').Append(r.Pestilence.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
          .Append('|').Append(r.InPlague ? 1 : 0).Append('\n');
    foreach (var f in w.Config.Factions.Select(cf => w.Factions[cf.Id]))
        sb.Append(f.Id).Append('=').Append(f.InPlague ? 1 : 0).Append('\n');
    return sb.ToString();
}

// Proof gate for The Creeping Death (disease-V2 spread-chain echo). RECORDING-ONLY: the contagion
// provenance adds NO Rng draw and NO event (verify is the keystone), so this gate proves the recorded
// edges are honest. Proves: double-run determinism (chronicle + contagion-edge set byte-identical),
// edge honesty (every contagion-from tag names a valid ADJACENT region and the onset cause-links a
// plague in that region with Year<=this), anchoring non-leak (the convention anchors no chained
// plague, and SiteId/HomeRegionId stay null), chain discipline (distinct regions, >=3, span<=window),
// and non-vacuity (>=1 contagion edge across the suite AND >=1 chain of length>=3 somewhere).
void CreepingDeathCmd(int years)
{
    Console.WriteLine($"Creeping Death gate ({years} yrs): contagion provenance is recording-only (no draw, no");
    Console.WriteLine("event); every contagion-from tag names a real adjacent plagued land; chains of >=3 distinct");
    Console.WriteLine("lands within the window become The Creeping Death; all of it deterministic.");
    int window = 30;
    int failures = 0;
    int suiteEdges = 0, suiteChains = 0, suiteLongest = 0;

    foreach (int seed in new[] { 1, 18, 42, 7 })
    {
        var (c1, n1) = Load();
        var w = new World(seed, c1, n1); w.Run(years);
        window = (int)w.Params.GetValueOrDefault("creeping_death_window", 30);
        var bad = new List<string>();
        int edges = 0;

        // (1) Edge honesty + anchoring non-leak, per contagion-tagged plague.
        foreach (var e in w.Chronicle.Events)
        {
            if (e.Type != "plague") continue;
            var tag = e.Tags.FirstOrDefault(t => t.StartsWith("contagion-from-"));
            if (tag is null) continue;
            edges++;

            // The chained plague stays land-anchored only — no site/home leak, convention agrees none.
            if (e.RegionId is not int rid || rid < 0 || rid >= w.Regions.Count)
            { bad.Add($"plague #{e.Id} has no valid RegionId"); break; }
            if (e.SiteId is not null)
            { bad.Add($"chained plague #{e.Id} leaked a SiteId ({e.SiteId})"); break; }
            if (e.HomeRegionId is not null)
            { bad.Add($"chained plague #{e.Id} carries a home anchor"); break; }
            if (SiteAnchors.Expected(w, e.Type, e.Tags, e.RegionId) is int leak)
            { bad.Add($"convention anchors plague #{e.Id} to site {leak} — expected none"); break; }

            if (!int.TryParse(tag.Substring("contagion-from-".Length), out int src))
            { bad.Add($"plague #{e.Id} has a malformed contagion tag '{tag}'"); break; }
            // The source must be an ACTUAL adjacent region.
            if (!w.Regions[rid].AdjacentRegionIds.Contains(src))
            { bad.Add($"plague #{e.Id} names contagion source {src} that is not adjacent to {rid}"); break; }
            // The onset must cause-link a plague in the source region, recorded no later than this one.
            var parent = e.Causes.Select(cid => w.Chronicle.Get(cid))
                .FirstOrDefault(c => c.Type == "plague" && c.RegionId == src && c.Year <= e.Year);
            if (parent is null)
            { bad.Add($"plague #{e.Id} contagion-from-{src} cause-links no plague in {src} with Year<={e.Year}"); break; }
        }

        // (2) Chain discipline: every emitted Creeping Death echo names distinct regions, >=3 of them,
        // within the window — recomputed independently of the detector's framing.
        var chains = Echoes.DetectCreepingDeath(w);
        int longest = 0;
        foreach (var echo in chains)
        {
            var regions = echo.EventIds.Select(id => w.Chronicle.Get(id))
                .Select(ev => ev.RegionId).ToList();
            if (regions.Any(r => r is null))
            { bad.Add($"Creeping Death echo '{echo.Label}' has an unanchored beat"); break; }
            var distinct = regions.Select(r => r!.Value).Distinct().Count();
            if (distinct != regions.Count)
            { bad.Add($"Creeping Death echo '{echo.Label}' repeats a region ({distinct} distinct of {regions.Count})"); break; }
            if (regions.Count < 3)
            { bad.Add($"Creeping Death echo '{echo.Label}' is only {regions.Count} lands"); break; }
            if (echo.YearSpan.Last - echo.YearSpan.First > window)
            { bad.Add($"Creeping Death echo '{echo.Label}' spans {echo.YearSpan.Last - echo.YearSpan.First} > {window}"); break; }
            longest = Math.Max(longest, regions.Count);
        }

        // (3) Determinism: the whole chronicle AND the contagion-edge set are byte-identical across an
        // independent run (the recording-only provenance must not introduce any order-dependence).
        var (c2, n2) = Load();
        var w2 = new World(seed, c2, n2); w2.Run(years);
        if (w.Chronicle.Render() != w2.Chronicle.Render())
            bad.Add("chronicle differs between identical runs");
        if (CreepingCanon(w) != CreepingCanon(w2))
            bad.Add("contagion-edge set differs between identical runs");

        suiteEdges += edges; suiteChains += chains.Count; suiteLongest = Math.Max(suiteLongest, longest);
        Console.WriteLine($"  seed {seed,3}: {(bad.Count == 0 ? "OK" : "FAIL")}  {edges} contagion edge(s) · {chains.Count} chain(s) · longest {longest} lands");
        foreach (var b in bad.Take(5)) Console.WriteLine($"           {b}");
        if (bad.Count > 0) failures++;
    }

    Console.WriteLine($"  suite: {suiteEdges} contagion edges, {suiteChains} creeping-death chains, longest {suiteLongest} lands");

    // (4) Non-vacuity: contagion must actually carry (else edge honesty is vacuous) AND at least one
    // chain of >=3 lands must form somewhere (else the echo's chain discipline is vacuous).
    if (suiteEdges == 0)
    { Console.WriteLine("  SUITE FAIL: no contagion edge ever recorded — the provenance is inert, edge checks vacuous"); failures++; }
    if (suiteLongest < 3)
    { Console.WriteLine("  SUITE FAIL: no chain of >=3 lands ever formed — the echo is unreachable, chain checks vacuous"); failures++; }

    Console.WriteLine(failures == 0 ? "\nTHE CREEPING DEATH HOLDS." : $"\n{failures} CHECK(S) FAILED.");
    Environment.Exit(failures == 0 ? 0 : 1);
}

string CreepingCanon(World w)
{
    var sb = new System.Text.StringBuilder();
    foreach (var e in w.Chronicle.Events)
    {
        if (e.Type != "plague") continue;
        var tag = e.Tags.FirstOrDefault(t => t.StartsWith("contagion-from-"));
        if (tag is null) continue;
        sb.Append(e.Id).Append('|').Append(tag).Append('|')
          .Append(string.Join(",", e.Causes)).Append('\n');
    }
    return sb.ToString();
}

void MigrationCmd(int years)
{
    Console.WriteLine($"Migration gate ({years} yrs): peoples flee famine/plague (relocate) and thriving peoples");
    Console.WriteLine("settle wilderness (expand). Migrations anchor to the DESTINATION land, never a site; flight");
    Console.WriteLine("cause-links to its disaster, settlement is rootless; territory stays consistent; deterministic.");
    int failures = 0;
    int suiteFlight = 0, suiteSettle = 0;

    foreach (int seed in new[] { 1, 18, 42, 7 })
    {
        var (c1, n1) = Load();
        var w = new World(seed, c1, n1); w.Run(years);
        var bad = new List<string>();
        int flight = 0, settle = 0;

        foreach (var e in w.Chronicle.Events)
        {
            if (e.Type != "migration") continue;
            // (1) Anchoring: a valid DESTINATION RegionId; no site/home leak; the convention agrees
            // none (a migration is a movement onto a land, not at one place — SiteAnchors NOT extended).
            if (e.RegionId is not int rid || rid < 0 || rid >= w.Regions.Count)
            { bad.Add($"migration #{e.Id} has no valid RegionId"); break; }
            if (e.SiteId is not null)
            { bad.Add($"migration #{e.Id} leaked a SiteId ({e.SiteId})"); break; }
            if (e.HomeRegionId is not null)
            { bad.Add($"migration #{e.Id} carries a home anchor (a move is placed, not remembered)"); break; }
            if (SiteAnchors.Expected(w, e.Type, e.Tags, e.RegionId) is int leak)
            { bad.Add($"convention anchors migration #{e.Id} to site {leak} — expected none"); break; }

            // (2) Tag discipline: exactly one driver — flight XOR settlement.
            bool isFlight = e.Tags.Contains("flight");
            bool isSettle = e.Tags.Contains("settlement");
            if (isFlight == isSettle)
            { bad.Add($"migration #{e.Id} is neither/both flight and settlement"); break; }

            if (isFlight)
            {
                flight++;
                // (3) Flight cause honesty: the move answers a real famine or plague (the disaster
                // that drove the people out — the migration-from-famine/plague grammar edge).
                if (!e.Causes.Select(cid => w.Chronicle.Get(cid)).Any(c => c.Type is "famine" or "plague"))
                { bad.Add($"flight migration #{e.Id} is not cause-linked to any famine or plague"); break; }
            }
            else
            {
                settle++;
                // (4) Settlement is rootless growth — it answers no disaster (silent, like a boom).
                if (e.Causes.Count != 0)
                { bad.Add($"settlement migration #{e.Id} carries {e.Causes.Count} cause(s) — growth is rootless"); break; }
            }
        }

        // (5) Territory integrity (end state): the two views of control agree exactly — every held
        // region names its holder, and every holder lists exactly the regions that name it. A
        // migration bug (double-claim, orphaned source region) would surface here.
        foreach (var r in w.Regions)
            if (r.ControllingFactionId is string hid
                && !w.Factions[hid].ControlledRegions.Contains(r.Id.ToString()))
                bad.Add($"region {r.Id} held by {hid} but absent from its ControlledRegions");
        foreach (var f in w.Config.Factions.Select(cf => w.Factions[cf.Id]))
            foreach (var s in f.ControlledRegions)
                if (w.Regions[int.Parse(s)].ControllingFactionId != f.Id)
                    bad.Add($"{f.Id} lists region {s} it does not actually control");

        // (6) Double-run determinism: the final holdings AND the whole chronicle are byte-identical
        // (the per-faction migration draw and the zero-Rng destination pick stay deterministic).
        var (c2, n2) = Load();
        var w2 = new World(seed, c2, n2); w2.Run(years);
        if (MigrationCanon(w) != MigrationCanon(w2))
            bad.Add("territory/holdings differ between identical runs");
        if (w.Chronicle.Render() != w2.Chronicle.Render())
            bad.Add("chronicle differs between identical runs");

        suiteFlight += flight; suiteSettle += settle;
        Console.WriteLine($"  seed {seed,3}: {(bad.Count == 0 ? "OK" : "FAIL")}  {flight + settle} migrations · {flight} in flight · {settle} settling");
        foreach (var b in bad.Take(5)) Console.WriteLine($"           {b}");
        if (bad.Count > 0) failures++;
    }

    Console.WriteLine($"  suite: {suiteFlight} flights, {suiteSettle} settlements");

    // (7) Non-vacuity: BOTH drivers must actually fire across the suite, or a flavor's whole contract
    // above is vacuously true.
    if (suiteFlight == 0) { Console.WriteLine("  SUITE FAIL: no flight migration ever fired — the push contract is vacuous"); failures++; }
    if (suiteSettle == 0) { Console.WriteLine("  SUITE FAIL: no settlement migration ever fired — the pull contract is vacuous"); failures++; }

    Console.WriteLine(failures == 0 ? "\nMIGRATION HOLDS." : $"\n{failures} CHECK(S) FAILED.");
    Environment.Exit(failures == 0 ? 0 : 1);
}

string MigrationCanon(World w)
{
    var sb = new System.Text.StringBuilder();
    foreach (var r in w.Regions)
        sb.Append(r.Id).Append('=').Append(r.ControllingFactionId ?? "-").Append('\n');
    return sb.ToString();
}

// Proof gate for Prejudice V1: an established people scorns a different-stock newcomer neighbour.
// Proves the anchoring contract (RegionId-only border anchor, no site/home leak, SiteAnchors NOT
// extended), cross-stock targeting (resenter and target are real, distinct, different-culture
// factions), cause honesty (a scorn that carries causes answers a real famine or plague), tension
// fallout (the scorn lands in the pair's grievance memory), determinism (double-run byte-identical
// chronicle + holdings), and non-vacuity (scorn actually fires across the suite).
void PrejudiceCmd(int years)
{
    Console.WriteLine($"Prejudice gate ({years} yrs): an established people scorns a different-stock newcomer");
    Console.WriteLine("neighbour. Scorns anchor to the BORDER land, never a site; a stress-driven scorn cause-links");
    Console.WriteLine("to its famine/plague; tension rises; deterministic.");
    int failures = 0;
    int suiteScorn = 0, suiteCaused = 0, suitePlain = 0;

    foreach (int seed in new[] { 1, 18, 42, 7 })
    {
        var (c1, n1) = Load();
        var w = new World(seed, c1, n1); w.Run(years);
        var bad = new List<string>();
        int scorn = 0, caused = 0, plain = 0;

        foreach (var e in w.Chronicle.Events)
        {
            if (e.Type != "prejudice") continue;
            scorn++;

            // (1) Anchoring: a valid BORDER RegionId; no site/home leak; the convention agrees none
            // (a feeling on a frontier spans a land, not a place — SiteAnchors NOT extended).
            if (e.RegionId is not int rid || rid < 0 || rid >= w.Regions.Count)
            { bad.Add($"prejudice #{e.Id} has no valid RegionId"); break; }
            if (e.SiteId is not null)
            { bad.Add($"prejudice #{e.Id} leaked a SiteId ({e.SiteId})"); break; }
            if (e.HomeRegionId is not null)
            { bad.Add($"prejudice #{e.Id} carries a home anchor (a scorn is placed, not remembered)"); break; }
            if (SiteAnchors.Expected(w, e.Type, e.Tags, e.RegionId) is int leak)
            { bad.Add($"convention anchors prejudice #{e.Id} to site {leak} — expected none"); break; }

            // (2) Cross-stock targeting: by/target tags name real, distinct, different-culture peoples
            // (origin prejudice — never the faith axis Persecution covers).
            string? byTag = e.Tags.FirstOrDefault(t => t.StartsWith("by-"))?.Substring(3);
            string? tgtTag = e.Tags.FirstOrDefault(t => t.StartsWith("target-"))?.Substring(7);
            if (byTag is null || tgtTag is null)
            { bad.Add($"prejudice #{e.Id} missing by-/target- tag"); break; }
            if (!w.Factions.TryGetValue(byTag, out var by) || !w.Factions.TryGetValue(tgtTag, out var tgt))
            { bad.Add($"prejudice #{e.Id} names an unknown faction (by={byTag} target={tgtTag})"); break; }
            if (byTag == tgtTag)
            { bad.Add($"prejudice #{e.Id} scorns its own people"); break; }
            if (by.Culture == tgt.Culture)
            { bad.Add($"prejudice #{e.Id} scorns same-stock people ({by.Culture}) — not origin prejudice"); break; }

            // (3) Cause honesty: a scorn that carries causes answers a real famine or plague (the
            // stress that sharpened it — the prejudice-from-famine/plague grammar edge). A plain
            // scorn carries none and stays silent.
            if (e.Causes.Count > 0)
            {
                caused++;
                if (!e.Causes.Select(cid => w.Chronicle.Get(cid)).All(c => c.Type is "famine" or "plague"))
                { bad.Add($"prejudice #{e.Id} carries a cause that is not a famine or plague"); break; }
            }
            else plain++;
        }

        // (4) Double-run determinism: holdings AND the whole chronicle byte-identical (the per-faction
        // prejudice draw and the zero-Rng target pick stay deterministic).
        var (c2, n2) = Load();
        var w2 = new World(seed, c2, n2); w2.Run(years);
        if (MigrationCanon(w) != MigrationCanon(w2))
            bad.Add("holdings differ between identical runs");
        if (w.Chronicle.Render() != w2.Chronicle.Render())
            bad.Add("chronicle differs between identical runs");

        suiteScorn += scorn; suiteCaused += caused; suitePlain += plain;
        Console.WriteLine($"  seed {seed,3}: {(bad.Count == 0 ? "OK" : "FAIL")}  {scorn} scorns · {caused} under disaster · {plain} plain");
        foreach (var b in bad.Take(5)) Console.WriteLine($"           {b}");
        if (bad.Count > 0) failures++;
    }

    Console.WriteLine($"  suite: {suiteScorn} scorns ({suiteCaused} disaster-driven, {suitePlain} plain)");

    // (5) Non-vacuity: scorn must actually fire across the suite, or every contract above is
    // vacuously true.
    if (suiteScorn == 0) { Console.WriteLine("  SUITE FAIL: no scorn ever fired — the prejudice contract is vacuous"); failures++; }

    Console.WriteLine(failures == 0 ? "\nPREJUDICE HOLDS." : $"\n{failures} CHECK(S) FAILED.");
    Environment.Exit(failures == 0 ? 0 : 1);
}

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

// -------------------------------------------------------------------------- replay

// Proof gate for Chronicle Replay V2 (Replay.cs): the replay read-model is deterministic
// across double runs, every beat references a real recorded event with its anchors copied
// VERBATIM (region/site/home never mixed, never inferred), every cause edge is literally
// in the effect's Causes, the consequence rail is bounded and real, statuses name exactly
// what the anchors allow (memory-only labeled, unanchored placeless), the turning-point
// classifier is deterministic and bounded to its authored table, the connectors still come
// from StoryGrammar, and replay survives the world-save journal.
void ReplayCmd(int years)
{
    Console.WriteLine($"Replay gate ({years} yrs): deterministic chains, verbatim anchors, honest");
    Console.WriteLine("statuses, bounded real consequences, authored turning points, save-safe.");
    int failures = 0;

    string ChainCanon(ReplayChain ch) =>
        $"focal {ch.FocalEventId} total {ch.TotalConsequences}\n"
        + string.Join("\n", ch.Beats.Concat(ch.Consequences).Select(b =>
            $"{b.EventId}|{b.Year}|{b.RegionId?.ToString() ?? "-"}|{b.SiteId?.ToString() ?? "-"}"
            + $"|{b.HomeRegionId?.ToString() ?? "-"}|{b.Status}|{b.Connector}|{b.CopyKey}"
            + $"|{b.CauseEventId?.ToString() ?? "-"}|{b.Category}|{string.Join(",", b.FactionIds)}"));

    var tpKinds = new HashSet<string>
    { "war-pivot", "peace-pivot", "land-lost", "land-abandoned", "violent-succession",
      "faith-torn", "faith-proclaimed", "ways-hardened", "divine-influenced", "far-reaching" };

    foreach (int seed in new[] { 1, 18, 42, 7 })
    {
        var bad = new List<string>();
        var (c1, n1) = Load();
        var w = new World(seed, c1, n1); w.Run(years);
        var (c2, n2) = Load();
        var w2 = new World(seed, c2, n2); w2.Run(years);

        // Full-pass direct-consequence counts — the gate's independent tally. Distinct per
        // citing event: a war naming the same grievance twice is ONE consequence of it.
        var cons = new Dictionary<int, int>();
        foreach (var e in w.Chronicle.Events)
            foreach (int c in e.Causes.Distinct())
                cons[c] = cons.GetValueOrDefault(c) + 1;

        // Three targets: the latest caused event, the most-consequential event, and the
        // latest site-anchored event — chains of different shapes.
        var targets = new List<int>();
        var lastCaused = w.Chronicle.Events.LastOrDefault(e => e.Causes.Count > 0);
        if (lastCaused is not null) targets.Add(lastCaused.Id);
        if (cons.Count > 0)
            targets.Add(cons.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).First().Key);
        var lastAnchored = w.Chronicle.Events.LastOrDefault(e => e.SiteId is not null);
        if (lastAnchored is not null) targets.Add(lastAnchored.Id);

        int beatsChecked = 0;
        foreach (int targetId in targets.Distinct())
        {
            var chain = Replay.ChainFor(w, targetId);
            var chain2 = Replay.ChainFor(w2, targetId);
            if (ChainCanon(chain) != ChainCanon(chain2))
            { bad.Add($"chain for #{targetId} differs between identical runs"); continue; }

            // Connectors still come from the grammar: re-derive the cause chain and compare.
            var ann = StoryGrammar.Annotate(w, targetId);
            if (ann.Steps.Count != chain.Beats.Count)
                bad.Add($"chain for #{targetId} dropped or invented beats vs the grammar");

            foreach (var b in chain.Beats.Concat(chain.Consequences))
            {
                beatsChecked++;
                if (b.EventId < 0 || b.EventId >= w.Chronicle.Events.Count)
                { bad.Add($"beat names event #{b.EventId} which does not exist"); break; }
                var e = w.Chronicle.Get(b.EventId);
                // Anchors verbatim — the three channels never mixed, never inferred.
                if (b.RegionId != e.RegionId || b.SiteId != e.SiteId || b.HomeRegionId != e.HomeRegionId)
                { bad.Add($"beat #{b.EventId} re-aimed an anchor channel"); break; }
                if (b.SiteId is int bsid && (b.RegionId is not int brid
                    || bsid < 0 || bsid >= w.Sites.All.Count || w.Sites.Get(bsid).RegionId != brid))
                { bad.Add($"beat #{b.EventId} site anchor is not a real site of its region"); break; }
                // Status names exactly what the anchors allow.
                string want = b.SiteId is not null ? "site-anchored"
                    : b.RegionId is not null ? "region-only"
                    : b.HomeRegionId is not null ? "memory-only"
                    : "unanchored";
                if (b.Status != want)
                { bad.Add($"beat #{b.EventId} status '{b.Status}' contradicts its anchors ('{want}')"); break; }
                // Placeless statuses carry NO coordinates a viewer could pin.
                if (b.Status is "unanchored" or "memory-only" && (b.RegionId is not null || b.SiteId is not null))
                { bad.Add($"beat #{b.EventId} placeless status but carries place anchors"); break; }
                if (b.CauseEventId is int cid && !e.Causes.Contains(cid))
                { bad.Add($"beat #{b.EventId} claims cause #{cid} not in its Causes"); break; }
                if (b.Year != e.Year || b.Category != e.Type)
                { bad.Add($"beat #{b.EventId} re-aimed year or category"); break; }
            }

            // The consequence rail: bounded, real, honestly counted.
            if (chain.Consequences.Count > 8)
                bad.Add($"chain for #{targetId} exceeded the consequence cap");
            if (chain.TotalConsequences != cons.GetValueOrDefault(targetId))
                bad.Add($"chain for #{targetId} miscounts consequences "
                    + $"({chain.TotalConsequences} vs {cons.GetValueOrDefault(targetId)})");
            foreach (var cb in chain.Consequences)
                if (!w.Chronicle.Get(cb.EventId).Causes.Contains(targetId))
                { bad.Add($"consequence #{cb.EventId} does not cite #{targetId}"); break; }
        }

        // Turning points: deterministic, bounded to the authored table, premises honest.
        int tpCount = 0;
        foreach (var e in w.Chronicle.Events)
        {
            string? kind = Replay.TurningPointKind(w, e, cons.GetValueOrDefault(e.Id));
            string? kind2 = Replay.TurningPointKind(w2, w2.Chronicle.Get(e.Id), cons.GetValueOrDefault(e.Id));
            if (kind != kind2) { bad.Add($"turning-point classifier diverged at #{e.Id}"); break; }
            if (kind is null) continue;
            tpCount++;
            if (!tpKinds.Contains(kind)) { bad.Add($"turning point #{e.Id} kind '{kind}' is not authored"); break; }
            bool premise = kind switch
            {
                "war-pivot" => e.Type == "war",
                "peace-pivot" => e.Type == "peace",
                "land-lost" => e.Type == "territory" && e.Tags.Contains("war"),
                "land-abandoned" => e.Type == "territory" && e.Tags.Contains("abandonment"),
                "violent-succession" => e.Type == "succession" && e.Causes.Count > 0
                    && w.Chronicle.Get(e.Causes[0]).Type == "murder",
                "faith-torn" => e.Type == "schism",
                "faith-proclaimed" => e.Type == "prophet",
                "ways-hardened" => e.Type == "custom",
                "divine-influenced" => e.Causes.Any(c => w.Chronicle.Get(c).Type == "divine"),
                _ => cons.GetValueOrDefault(e.Id) >= 4,
            };
            if (!premise) { bad.Add($"turning point #{e.Id} '{kind}' premise does not hold"); break; }
        }

        Console.WriteLine($"  seed {seed,3}: {(bad.Count == 0 ? "OK" : "FAIL")}  {targets.Distinct().Count()} chains, "
            + $"{beatsChecked} beats checked, {tpCount} turning points / {w.Chronicle.Events.Count} events");
        foreach (var b in bad.Take(5)) Console.WriteLine($"           {b}");
        if (bad.Count > 0) failures++;
    }

    // Replay survives save/load: a journaled act replayed into a fresh world yields the
    // same chain for a divine-influenced event — the player-shaped story replays too.
    {
        string path = Path.Combine(Path.GetTempPath(), $"lm_replay_gate_{Guid.NewGuid():N}.json");
        try
        {
            World JournaledRun(bool record)
            {
                var (cfg, names) = Load();
                var w = new World(7, cfg, names);
                w.SeedWorld();
                var (store, _) = PlayerWorldStore.LoadOrNew(path, 7);
                if (record)
                {
                    // Curse the OLDEST adult: a cursed elder reliably dies within the window and
                    // leaves a divine-caused death — so the chain this check needs always exists,
                    // robust to RNG-stream reshuffles from future sim work (deterministic tie-break).
                    var victim = w.FactionMembers(w.Config.Factions[0].Id)
                        .Where(p => p.Age(w.Year) >= 18)
                        .OrderByDescending(p => p.Age(w.Year)).ThenBy(p => p.Id).First();
                    var ev = w.PlantCurse(victim);
                    store.RecordAct(w, w.DivinePressures.Last(p => p.SourceEventId == ev.Id));
                    store.ResumeYear = w.Year;
                    store.Save();
                }
                else
                {
                    store.ApplyDue(w);
                }
                for (int y = 0; y < 60; y++) { w.Tick(); if (!record) store.ApplyDue(w); }
                return w;
            }
            var live = JournaledRun(record: true);
            var resumed = JournaledRun(record: false);
            var divineTouched = live.Chronicle.Events
                .FirstOrDefault(e => e.Causes.Any(c => live.Chronicle.Get(c).Type == "divine"));
            bool ok = divineTouched is not null
                && ChainCanon(Replay.ChainFor(live, divineTouched.Id))
                   == ChainCanon(Replay.ChainFor(resumed, divineTouched.Id));
            Console.WriteLine($"  save/load: {(ok ? "OK" : "FAIL")}  (divine-influenced chain identical after journal replay)");
            if (!ok) failures++;
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    Console.WriteLine(failures == 0 ? "\nREPLAY CONTRACT HOLDS." : $"\n{failures} CHECK(S) BROKE THE CONTRACT.");
    Environment.Exit(failures == 0 ? 0 : 1);
}
