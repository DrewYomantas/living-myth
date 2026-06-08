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
    default:
        Console.WriteLine("commands: run | divergence | surface | verify");
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
