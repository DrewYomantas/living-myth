using System.Text.Json;
using System.Text.Json.Serialization;

namespace LivingMyth.Sim;

/// <summary>
/// Player-authored canon: the player's hand laid over the world — tellings, chronicler's
/// notes, memorial inscriptions, place legends, what-the-people-say. This is the THIRD
/// ledger of the truth model (PROJECT_STATE.md "Truth model V1"): never Recorded Fact,
/// never Mechanical Truth. The sim NEVER reads this store — World/Chronicle/entities hold
/// no reference to these types, and the `canon` console gate proves it by reflection and
/// by a behavioral double-run.
///
/// Lives in the Sim library only because that is the shared, Godot-free home both the
/// console gate and the viewer can reach. The file path is injected: the viewer passes a
/// globalized user:// path, the gate a temp file.
///
/// Identity across sessions: the viewer re-runs the sim from year 0 every launch, so a
/// note attached to event id 5012 (written at year 300 last session) is DORMANT until
/// this run's chronicle reaches that id again; entities are then confirmed against a
/// per-note identity snapshot. A snapshot mismatch (the sim build changed and ids
/// drifted) QUARANTINES the note — kept in the file, never rendered against the wrong
/// entity. Ids are never reused, so once Active a note can only stay Active.
///
/// Reserved for future schema versions (documented, deliberately NOT built in V1):
/// display_name_override (display-layer renames; Person.Name/Event.Text stay immutable),
/// tags (structured myth tags), promoted_mechanical_meaning (the nudge system's hook).
/// </summary>
public enum CanonNoteType { Telling, ChroniclerNote, Inscription, PlaceLegend, PeopleSay }

public enum CanonNoteState
{
    Active,        // entity exists this run and matches the identity snapshot
    Dormant,       // entity id not reached yet this run (event unrecorded, soul unborn, faith unfounded)
    Quarantined,   // entity exists but identity mismatches (sim-build drift) — never rendered, kept in file
}

public sealed class CanonNote
{
    public string EntityKey { get; set; } = "";   // "p:12" | "e:5012" | "r:3" | "f:highland" | "rel:2" (rel reserved — no viewer surface yet)
    public CanonNoteType NoteType { get; set; }
    public string Text { get; set; } = "";
    public int CreatedYear { get; set; }          // sim year when written
    public string UpdatedUtc { get; set; } = "";  // wall-clock stamp; injectable so the gate can prove roundtrips
    public string Source { get; set; } = "player";
    public Dictionary<string, string> Snapshot { get; set; } = new();
}

/// <summary>Serialization root — versioned so renames and structured tags can arrive later.</summary>
public sealed class CanonFile
{
    public int SchemaVersion { get; set; } = PlayerCanonStore.SchemaVersion;
    public int Seed { get; set; }
    public string AppNote { get; set; } =
        "player canon — kept apart from the simulated record; the sim never reads this file";
    public List<CanonNote> Notes { get; set; } = new();
}

public sealed class PlayerCanonStore
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
    private readonly List<CanonNote> _notes;
    private readonly HashSet<(CanonNote, World)> _confirmedActive = new();   // per world — identity is a claim about one run

    public int Seed { get; }
    public int Count => _notes.Count;

    /// <summary>True when the on-disk file must never be overwritten (unreadable, or
    /// written by a newer build). The viewer hides write affordances; Upsert/Delete throw.</summary>
    public bool ReadOnly { get; }
    /// <summary>True when ReadOnly is due to a newer schema (a valid file from the future
    /// — preserve it). False+ReadOnly means unreadable (the caller may .bak it and reload).</summary>
    public bool FutureSchema { get; }

    private PlayerCanonStore(string path, int seed, List<CanonNote> notes, bool readOnly, bool futureSchema)
    {
        _path = path;
        Seed = seed;
        _notes = notes;
        ReadOnly = readOnly;
        FutureSchema = futureSchema;
    }

    /// <summary>Never writes; never touches a bad file. Missing file → empty writable
    /// store. Unreadable file → empty read-only store + warning (the caller decides
    /// whether to rename it aside). Newer schema → empty read-only store + warning,
    /// the future file preserved untouched.</summary>
    public static (PlayerCanonStore store, string? loadWarning) LoadOrNew(string filePath, int seed)
    {
        if (!File.Exists(filePath))
            return (new PlayerCanonStore(filePath, seed, new(), readOnly: false, futureSchema: false), null);
        try
        {
            var file = JsonSerializer.Deserialize<CanonFile>(File.ReadAllText(filePath), Options);
            if (file is null)
                return (new PlayerCanonStore(filePath, seed, new(), true, false),
                        "canon file held nothing readable — starting fresh in memory, file preserved");
            if (file.SchemaVersion > SchemaVersion)
                return (new PlayerCanonStore(filePath, seed, new(), true, true),
                        $"canon file is schema v{file.SchemaVersion}, newer than this build (v{SchemaVersion}) — preserved untouched, not loaded");
            var notes = file.Notes
                .Where(n => n is not null && !string.IsNullOrWhiteSpace(n.Text) && !string.IsNullOrEmpty(n.EntityKey))
                .ToList();
            return (new PlayerCanonStore(filePath, seed, notes, false, false), null);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return (new PlayerCanonStore(filePath, seed, new(), true, false),
                    $"canon file unreadable ({ex.GetType().Name}) — starting fresh in memory, file preserved");
        }
    }

    /// <summary>V1 holds at most one note per (entity, type).</summary>
    public CanonNote? Get(string entityKey, CanonNoteType type)
        => _notes.FirstOrDefault(n => n.EntityKey == entityKey && n.NoteType == type);

    /// <summary>All notes on one entity, in NoteType order (deterministic display).</summary>
    public IReadOnlyList<CanonNote> AllFor(string entityKey)
        => _notes.Where(n => n.EntityKey == entityKey).OrderBy(n => n.NoteType).ToList();

    /// <summary>Lazy, render-time identity check — a load-time check could not tell a
    /// stale note from one whose entity simply hasn't been re-simulated yet this session.
    /// Memoized once Active (ids are never reused, so identity can only be confirmed).</summary>
    public CanonNoteState StateOf(CanonNote note, World world)
    {
        if (_confirmedActive.Contains((note, world))) return CanonNoteState.Active;
        var state = Evaluate(note, world);
        if (state == CanonNoteState.Active) _confirmedActive.Add((note, world));
        return state;
    }

    private static CanonNoteState Evaluate(CanonNote note, World world)
    {
        int sep = note.EntityKey.IndexOf(':');
        if (sep <= 0 || sep == note.EntityKey.Length - 1) return CanonNoteState.Quarantined;
        string kind = note.EntityKey[..sep];
        string rest = note.EntityKey[(sep + 1)..];

        bool Match(string key, string live)
            => note.Snapshot.TryGetValue(key, out var want) && want == live;

        switch (kind)
        {
            case "p":   // people are added at birth and never removed — missing = unborn this run
                if (!int.TryParse(rest, out int pid)) return CanonNoteState.Quarantined;
                if (!world.People.TryGetValue(pid, out var p)) return CanonNoteState.Dormant;
                return Match("name", p.Name) && Match("birth_year", p.BirthYear.ToString())
                    ? CanonNoteState.Active : CanonNoteState.Quarantined;
            case "e":   // event ids are chronicle indexes — beyond Count = not recorded yet this run
                if (!int.TryParse(rest, out int eid) || eid < 0) return CanonNoteState.Quarantined;
                if (eid >= world.Chronicle.Events.Count) return CanonNoteState.Dormant;
                var ev = world.Chronicle.Get(eid);
                return Match("type", ev.Type) && Match("year", ev.Year.ToString()) && Match("text", ev.Text)
                    ? CanonNoteState.Active : CanonNoteState.Quarantined;
            case "r":   // regions are fixed at seeding — out of range can never become valid
                if (!int.TryParse(rest, out int rid) || rid < 0 || rid >= world.Regions.Count)
                    return CanonNoteState.Quarantined;
                var r = world.Regions[rid];
                return Match("name", r.Name) && Match("terrain", r.TerrainType)
                    ? CanonNoteState.Active : CanonNoteState.Quarantined;
            case "f":   // factions are fixed from config
                if (!world.Factions.TryGetValue(rest, out var f)) return CanonNoteState.Quarantined;
                return Match("name", f.Name) ? CanonNoteState.Active : CanonNoteState.Quarantined;
            case "rel": // faiths appear over time (reserved key — no viewer surface yet)
                if (!int.TryParse(rest, out int relid)) return CanonNoteState.Quarantined;
                if (!world.Religions.TryGetValue(relid, out var rel)) return CanonNoteState.Dormant;
                return Match("name", rel.Name) ? CanonNoteState.Active : CanonNoteState.Quarantined;
            default:
                return CanonNoteState.Quarantined;
        }
    }

    /// <summary>Write or replace the note for (entityKey, type). Empty/whitespace text
    /// deletes instead. The entity must exist in the given world right now (notes are
    /// written about things the player can see) — the identity snapshot is built from it.</summary>
    public void Upsert(string entityKey, CanonNoteType type, string text, World world, string? updatedUtc = null)
    {
        if (ReadOnly) throw new InvalidOperationException("canon store is read-only (file preserved untouched)");
        RequireKeyShape(entityKey, type);
        if (string.IsNullOrWhiteSpace(text)) { Delete(entityKey, type); return; }

        var snapshot = BuildSnapshot(entityKey, world)
            ?? throw new ArgumentException($"entity '{entityKey}' does not exist in this world", nameof(entityKey));

        var note = Get(entityKey, type);
        if (note is null)
        {
            note = new CanonNote { EntityKey = entityKey, NoteType = type, CreatedYear = world.Year };
            _notes.Add(note);
        }
        note.Text = text.Trim();
        note.UpdatedUtc = updatedUtc ?? DateTime.UtcNow.ToString("o");
        note.Snapshot = snapshot;
        _confirmedActive.RemoveWhere(k => k.Item1 == note);   // re-confirm against the fresh snapshot
    }

    public void Delete(string entityKey, CanonNoteType type)
    {
        if (ReadOnly) throw new InvalidOperationException("canon store is read-only (file preserved untouched)");
        _notes.RemoveAll(n => n.EntityKey == entityKey && n.NoteType == type);
    }

    /// <summary>V1 note-type ↔ entity-kind contract: tellings and inscriptions belong to
    /// people, chronicler's notes to events, legends to places, people-say to peoples.</summary>
    private static void RequireKeyShape(string entityKey, CanonNoteType type)
    {
        string want = type switch
        {
            CanonNoteType.Telling => "p:",
            CanonNoteType.Inscription => "p:",
            CanonNoteType.ChroniclerNote => "e:",
            CanonNoteType.PlaceLegend => "r:",
            CanonNoteType.PeopleSay => "f:",
            _ => "?",
        };
        if (!entityKey.StartsWith(want, StringComparison.Ordinal))
            throw new ArgumentException($"{type} notes attach to '{want}…' keys, got '{entityKey}'", nameof(entityKey));
    }

    private static Dictionary<string, string>? BuildSnapshot(string entityKey, World world)
    {
        int sep = entityKey.IndexOf(':');
        if (sep <= 0) return null;
        string kind = entityKey[..sep];
        string rest = entityKey[(sep + 1)..];
        switch (kind)
        {
            case "p":
                return int.TryParse(rest, out int pid) && world.People.TryGetValue(pid, out var p)
                    ? new() { ["name"] = p.Name, ["birth_year"] = p.BirthYear.ToString() } : null;
            case "e":
                if (!int.TryParse(rest, out int eid) || eid < 0 || eid >= world.Chronicle.Events.Count) return null;
                var ev = world.Chronicle.Get(eid);
                return new() { ["type"] = ev.Type, ["year"] = ev.Year.ToString(), ["text"] = ev.Text };
            case "r":
                return int.TryParse(rest, out int rid) && rid >= 0 && rid < world.Regions.Count
                    ? new() { ["name"] = world.Regions[rid].Name, ["terrain"] = world.Regions[rid].TerrainType } : null;
            case "f":
                return world.Factions.TryGetValue(rest, out var f) ? new() { ["name"] = f.Name } : null;
            case "rel":
                return int.TryParse(rest, out int relid) && world.Religions.TryGetValue(relid, out var rel)
                    ? new() { ["name"] = rel.Name } : null;
            default:
                return null;
        }
    }

    /// <summary>Atomic write: serialize to a sibling .tmp, then move over the target —
    /// a crash mid-save can never leave a half-written canon file.</summary>
    public void Save()
    {
        if (ReadOnly) throw new InvalidOperationException("canon store is read-only (file preserved untouched)");
        var file = new CanonFile { Seed = Seed, Notes = _notes };
        string tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(file, Options));
        File.Move(tmp, _path, overwrite: true);
    }
}
