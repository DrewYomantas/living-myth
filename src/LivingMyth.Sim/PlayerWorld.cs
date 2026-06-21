using System.Text.Json;
using System.Text.Json.Serialization;

namespace LivingMyth.Sim;

/// <summary>
/// The player-shaped world, persisted: an INPUT JOURNAL, never a world snapshot. The
/// viewer re-runs the sim from year 0 every launch, so the only honest save is the
/// player's hand itself — every divine act with the year it was made and an identity
/// snapshot of its target, plus the follows and attention state of the session. Replaying
/// the journal against the same seed (each act re-applied when the run reaches its year,
/// in order) reproduces the previous session's world exactly, because the acts are the
/// only player input the sim ever feels.
///
/// Shares the PlayerCanonStore contract: versioned JSON at an injected path
/// (user://world_seed{N}.json from the viewer, a temp file from the `save` gate), a
/// corrupt file is preserved (read-only store, never overwritten), a future-schema file
/// is preserved untouched, saves are atomic. The sim never reads this store — applying
/// the journal is an explicit caller act (`ApplyDue`), and the `save` gate proves a
/// loaded-but-unapplied journal leaves a clean run byte-identical.
///
/// Acts whose target no longer matches its snapshot (sim-build drift moved the ids) are
/// QUARANTINED on replay: skipped, kept in the file, never applied to the wrong soul.
/// </summary>
public sealed class WorldAct
{
    public int Seq { get; set; }
    public string Kind { get; set; } = "";        // bless | curse | protect | doom | omen | forest | spring
    public string TargetType { get; set; } = "";  // person | faction | region
    public string TargetId { get; set; } = "";
    public int Year { get; set; }
    public Dictionary<string, string> Snapshot { get; set; } = new();
}

public sealed class WorldFollows
{
    public List<int> Souls { get; set; } = new();
    public List<int> Bloodlines { get; set; } = new();
    public List<string> Peoples { get; set; } = new();
    public List<int> Lands { get; set; } = new();
    /// <summary>Identity snapshots for person follows ("p:12" -> name/birth_year), so a
    /// follow can never silently re-attach to a different soul after sim-build drift.</summary>
    public Dictionary<string, Dictionary<string, string>> Snapshots { get; set; } = new();
}

/// <summary>Serialization root — versioned so later schema can arrive without breaking this one.</summary>
public sealed class WorldSaveFile
{
    public int SchemaVersion { get; set; } = PlayerWorldStore.SchemaVersion;
    public int Seed { get; set; }
    public string AppNote { get; set; } =
        "the player-shaped world — an input journal the viewer replays; never sim truth, never read by the sim";
    public int ResumeYear { get; set; }
    public List<WorldAct> Acts { get; set; } = new();
    public WorldFollows Follows { get; set; } = new();
    public Dictionary<int, int> LastSeen { get; set; } = new();   // person id -> last YOURS event actually shown
}

public sealed class PlayerWorldStore
{
    public const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly string _path;
    private readonly List<WorldAct> _acts;
    private readonly List<WorldAct> _quarantined = new();
    private int _applyCursor;

    public int Seed { get; }
    public int ResumeYear { get; set; }
    public WorldFollows Follows { get; private set; }
    public Dictionary<int, int> LastSeen { get; private set; }

    public bool ReadOnly { get; }
    public bool FutureSchema { get; }
    public int ActCount => _acts.Count;
    public IReadOnlyList<WorldAct> Acts => _acts;
    public IReadOnlyList<WorldAct> QuarantinedActs => _quarantined;

    private PlayerWorldStore(string path, int seed, WorldSaveFile file, bool readOnly, bool futureSchema)
    {
        _path = path;
        Seed = seed;
        _acts = file.Acts.Where(a => a is not null && a.Kind.Length > 0).OrderBy(a => a.Seq).ToList();
        ResumeYear = file.ResumeYear;
        Follows = file.Follows ?? new();
        LastSeen = file.LastSeen ?? new();
        ReadOnly = readOnly;
        FutureSchema = futureSchema;
    }

    /// <summary>Same loading contract as the canon store: never writes, never destroys.
    /// Missing -> empty writable. Unreadable -> empty read-only + warning (file preserved;
    /// the caller may set it aside as .bak). Newer schema -> empty read-only, file untouched.</summary>
    public static (PlayerWorldStore store, string? loadWarning) LoadOrNew(string filePath, int seed)
    {
        if (!File.Exists(filePath))
            return (new PlayerWorldStore(filePath, seed, new(), readOnly: false, futureSchema: false), null);
        try
        {
            var file = JsonSerializer.Deserialize<WorldSaveFile>(File.ReadAllText(filePath), Options);
            if (file is null)
                return (new PlayerWorldStore(filePath, seed, new(), true, false),
                        "world save held nothing readable — starting fresh in memory, file preserved");
            if (file.SchemaVersion > SchemaVersion)
                return (new PlayerWorldStore(filePath, seed, new(), true, true),
                        $"world save is schema v{file.SchemaVersion}, newer than this build (v{SchemaVersion}) — preserved untouched, not loaded");
            return (new PlayerWorldStore(filePath, seed, file, false, false), null);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return (new PlayerWorldStore(filePath, seed, new(), true, false),
                    $"world save unreadable ({ex.GetType().Name}) — starting fresh in memory, file preserved");
        }
    }

    // ---- journaling (the live session writes what the hand just did) ----

    /// <summary>Journal a divine act the world has ALREADY applied (the viewer's live
    /// funnel hands in the fresh DivinePressure). Advances the replay cursor past it,
    /// so a later ApplyDue can never apply the same act twice.</summary>
    public WorldAct RecordAct(World world, DivinePressure pressure)
    {
        if (ReadOnly) throw new InvalidOperationException("world save is read-only (file preserved untouched)");
        var act = new WorldAct
        {
            Seq = _acts.Count == 0 ? 0 : _acts[^1].Seq + 1,
            Kind = KindKey(pressure.Kind),
            TargetType = pressure.TargetType,
            TargetId = pressure.TargetId,
            Year = pressure.StartYear,
            Snapshot = SnapshotOf(world, pressure.TargetType, pressure.TargetId) ?? new(),
        };
        _acts.Add(act);
        _applyCursor = _acts.Count;
        return act;
    }

    private static string KindKey(DivinePressureKind kind) => kind switch
    {
        DivinePressureKind.Bless => "bless",
        DivinePressureKind.Curse => "curse",
        DivinePressureKind.Protect => "protect",
        DivinePressureKind.Doom => "doom",
        DivinePressureKind.Omen => "omen",
        DivinePressureKind.ForestSeeded => "forest",
        DivinePressureKind.Smite => "smite",
        _ => "spring",
    };

    private static Dictionary<string, string>? SnapshotOf(World world, string targetType, string targetId)
    {
        switch (targetType)
        {
            case "person":
                return int.TryParse(targetId, out int pid) && world.People.TryGetValue(pid, out var p)
                    ? new() { ["name"] = p.Name, ["birth_year"] = p.BirthYear.ToString() } : null;
            case "faction":
                return world.Factions.TryGetValue(targetId, out var f) ? new() { ["name"] = f.Name } : null;
            case "region":
                return int.TryParse(targetId, out int rid) && rid >= 0 && rid < world.Regions.Count
                    ? new() { ["name"] = world.Regions[rid].Name, ["terrain"] = world.Regions[rid].TerrainType } : null;
            default:
                return null;
        }
    }

    /// <summary>Replace the persisted follow sets with the session's current ones, with
    /// identity snapshots for every person follow. Sorted — the file stays diffable.</summary>
    public void SetFollows(World world, IEnumerable<int> souls, IEnumerable<int> bloodlines,
                           IEnumerable<string> peoples, IEnumerable<int> lands)
    {
        if (ReadOnly) throw new InvalidOperationException("world save is read-only (file preserved untouched)");
        var f = new WorldFollows
        {
            Souls = souls.OrderBy(i => i).ToList(),
            Bloodlines = bloodlines.OrderBy(i => i).ToList(),
            Peoples = peoples.OrderBy(s => s, StringComparer.Ordinal).ToList(),
            Lands = lands.OrderBy(i => i).ToList(),
        };
        foreach (int pid in f.Souls.Concat(f.Bloodlines).Distinct())
            if (world.People.TryGetValue(pid, out var p))
                f.Snapshots[$"p:{pid}"] = new() { ["name"] = p.Name, ["birth_year"] = p.BirthYear.ToString() };
        Follows = f;
    }

    public void SetLastSeen(IReadOnlyDictionary<int, int> lastSeen)
    {
        if (ReadOnly) throw new InvalidOperationException("world save is read-only (file preserved untouched)");
        LastSeen = lastSeen.ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    // ---- replay (a fresh run re-applies the journal as it reaches each year) ----

    /// <summary>Apply every journaled act due at the world's current year, in order.
    /// Validation is strict: a target that no longer matches its identity snapshot (or an
    /// act whose year the run has already passed) is quarantined — kept in the file,
    /// never applied to the wrong thing. Returns the acts applied with their recorded
    /// events (a terrain act the land refused returns a null event, honestly).</summary>
    public List<(WorldAct Act, Event? Ev)> ApplyDue(World world)
    {
        var applied = new List<(WorldAct, Event?)>();
        while (_applyCursor < _acts.Count)
        {
            var act = _acts[_applyCursor];
            if (act.Year > world.Year) break;
            _applyCursor++;
            if (act.Year < world.Year) { _quarantined.Add(act); continue; }
            try { applied.Add((act, ApplyOne(world, act))); }
            catch (ArgumentException) { _quarantined.Add(act); }
        }
        return applied;
    }

    private static bool Match(WorldAct act, string key, string live)
        => act.Snapshot.TryGetValue(key, out var want) && want == live;

    private static Event? ApplyOne(World w, WorldAct act)
    {
        switch (act.Kind)
        {
            case "bless":
            case "curse":
            case "smite":
                if (!int.TryParse(act.TargetId, out int pid) || !w.People.TryGetValue(pid, out var p)
                    || !Match(act, "name", p.Name) || !Match(act, "birth_year", p.BirthYear.ToString()))
                    throw new ArgumentException($"act #{act.Seq}: person target drifted");
                return act.Kind switch { "bless" => w.BlessPerson(p), "curse" => w.PlantCurse(p), _ => w.Smite(p) };
            case "protect":
            case "doom":
                if (!w.Factions.TryGetValue(act.TargetId, out var f) || !Match(act, "name", f.Name))
                    throw new ArgumentException($"act #{act.Seq}: faction target drifted");
                return act.Kind == "protect" ? w.ProtectFaction(f.Id) : w.DoomFaction(f.Id);
            case "omen":
            case "forest":
            case "spring":
                if (!int.TryParse(act.TargetId, out int rid) || rid < 0 || rid >= w.Regions.Count
                    || !Match(act, "name", w.Regions[rid].Name))
                    throw new ArgumentException($"act #{act.Seq}: region target drifted");
                return act.Kind switch
                {
                    "omen" => w.SeedOmen(rid),
                    "forest" => w.SeedForest(rid),
                    _ => w.CallSpring(rid),
                };
            default:
                throw new ArgumentException($"act #{act.Seq}: unknown kind '{act.Kind}'");
        }
    }

    /// <summary>Validate the persisted follows against this world (called after the
    /// resume fast-forward, when every previously-followed soul exists again on a
    /// faithful replay). Drift drops the follow with a note — never a silent re-attach.</summary>
    public (List<int> Souls, List<int> Bloodlines, List<string> Peoples, List<int> Lands, List<string> Dropped)
        RestoreFollows(World world)
    {
        var dropped = new List<string>();
        List<int> People(List<int> ids, string what)
        {
            var keep = new List<int>();
            foreach (int pid in ids)
            {
                bool ok = world.People.TryGetValue(pid, out var p)
                    && Follows.Snapshots.TryGetValue($"p:{pid}", out var snap)
                    && snap.GetValueOrDefault("name") == p.Name
                    && snap.GetValueOrDefault("birth_year") == p.BirthYear.ToString();
                if (ok) keep.Add(pid);
                else dropped.Add($"{what} p:{pid}");
            }
            return keep;
        }
        var souls = People(Follows.Souls, "soul");
        var lines = People(Follows.Bloodlines, "bloodline");
        var peoples = new List<string>();
        foreach (var fid in Follows.Peoples)
        {
            if (world.Factions.ContainsKey(fid)) peoples.Add(fid);
            else dropped.Add($"people f:{fid}");
        }
        var lands = new List<int>();
        foreach (int rid in Follows.Lands)
        {
            if (rid >= 0 && rid < world.Regions.Count) lands.Add(rid);
            else dropped.Add($"land r:{rid}");
        }
        return (souls, lines, peoples, lands, dropped);
    }

    /// <summary>Atomic write, exactly like the canon store: .tmp then move-over.</summary>
    public void Save()
    {
        if (ReadOnly) throw new InvalidOperationException("world save is read-only (file preserved untouched)");
        var file = new WorldSaveFile
        {
            Seed = Seed,
            ResumeYear = ResumeYear,
            Acts = _acts,   // quarantined acts stay in the list (and the file) — skipped, never destroyed
            Follows = Follows,
            LastSeen = LastSeen,
        };
        string tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(file, Options));
        File.Move(tmp, _path, overwrite: true);
    }
}
