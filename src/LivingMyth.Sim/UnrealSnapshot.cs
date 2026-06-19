using System.Text.Json;
using System.Text.Json.Serialization;

namespace LivingMyth.Sim;

/// <summary>
/// Godot Snapshot Bridge V1 — a small, deterministic, honest JSON export of a Living Myth
/// world, designed to DRIVE the separate Unreal Engine diorama sandbox (it never reads back).
///
/// This is a pure read-model in the Sim family (like Sites/Replay/SurfacePainter): it draws
/// ZERO Rng, is never read by `Tick`, and is a deterministic function of finished world state,
/// so a snapshot off a given (seed, year) is byte-identical run to run and the `verify` baseline
/// cannot move. It fabricates nothing: every field is real recorded data or a deterministic
/// rule over it. Missing optional data is `null` plus an entry in <c>ExportWarnings</c> — never
/// invented. The RegionId (where it happened) vs HomeRegionId (where it is remembered) channels
/// are kept strictly apart, exactly as the chronicle records them.
/// </summary>
public static class UnrealExport
{
    public const string SchemaVersion = "1.0.0";
    public const string GeneratedBy = "LivingMyth.Console unreal-snapshot";

    private const int MaxPeopleHighlights = 40;
    private const int MaxMemoryMarkers = 60;
    private const int MaxChronicleBeats = 7;
    private const int MinChronicleBeats = 3;
    private const int MaxMarkerPersons = 8;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        // Honesty: nulls are emitted, never dropped — a null field is a real signal to Unreal.
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Serialize a snapshot to the canonical, deterministic JSON form the gate and
    /// the export both write — single-sourced so the file on disk is the bytes the gate checked.</summary>
    public static string ToJson(UnrealSnapshot snap) => JsonSerializer.Serialize(snap, JsonOpts);

    // Event types that carry the fortunes of a people across a whole land (not a built place):
    // these read as a "pulse" over a region rather than a place mark.
    private static readonly HashSet<string> FactionPulseTypes = new(StringComparer.Ordinal)
    { "famine", "famine_end", "boom", "plague", "plague_end", "migration", "prejudice" };

    // The events a chronicle path should prefer (significant beats, never births/marriages).
    private static readonly HashSet<string> BeatTypes = new(StringComparer.Ordinal)
    { "battle", "war", "peace", "murder", "succession", "founding", "territory",
      "famine", "plague", "migration", "prejudice", "martyr", "prophet", "schism" };

    public static UnrealSnapshot Build(World w, int seed)
    {
        var warnings = new List<string>();

        // --- cheap per-region tallies over the chronicle (one pass, the two anchor channels apart) ---
        var trueEventCount = new int[w.Regions.Count];   // events that truly happened here (RegionId)
        var homeMemoryCount = new int[w.Regions.Count];  // lives remembered here (HomeRegionId, not a place)
        foreach (var e in w.Chronicle.Events)
        {
            if (e.RegionId is int r && r >= 0 && r < trueEventCount.Length) trueEventCount[r]++;
            if (e.HomeRegionId is int h && h >= 0 && h < homeMemoryCount.Length) homeMemoryCount[h]++;
        }

        // --- faction founding seats, recovered from the chronicle's own founding anchors ---
        var foundingSeat = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var e in w.Chronicle.Events.Where(e =>
                     e.Type == "territory" && e.Tags.Contains("founding") && e.RegionId is int))
            foreach (int pid in e.Participants)
                if (w.People.TryGetValue(pid, out var p))
                    foundingSeat.TryAdd(p.FactionId, e.RegionId!.Value);

        // ---------------------------------------------------------------- regions
        var regions = new List<RegionDto>(w.Regions.Count);
        foreach (var region in w.Regions)
        {
            var seat = w.Sites.SeatOf(region.Id);
            regions.Add(new RegionDto
            {
                Id = region.Id,
                Name = string.IsNullOrWhiteSpace(region.Name) ? null : region.Name,
                Terrain = region.TerrainType,
                X = region.X,
                Y = region.Y,
                ControllingFactionId = region.ControllingFactionId,
                HomeMemoryCount = homeMemoryCount[region.Id],
                TrueEventCount = trueEventCount[region.Id],
                SuggestedUnrealRole = SuggestedRole(w, region, seat),
            });
        }

        // ---------------------------------------------------------------- factions
        var factions = new List<FactionDto>();
        bool anyColorMissing = false;
        foreach (var def in w.Config.Factions)
        {
            var f = w.Factions[def.Id];
            anyColorMissing = true;   // the sim authors no faction colour — see symbolicColor
            var traits = new List<string>();
            if (!string.IsNullOrWhiteSpace(f.Culture)) traits.Add("culture:" + f.Culture);
            foreach (var custom in f.CustomOriginEvent.Keys.OrderBy(k => k, StringComparer.Ordinal))
                traits.Add("custom:" + custom);
            if (f.InFamine) traits.Add("in-famine");
            if (f.InBoom) traits.Add("in-boom");
            if (f.InPlague) traits.Add("in-plague");

            factions.Add(new FactionDto
            {
                Id = f.Id,
                Name = f.Name,
                Color = null,                          // not modeled in sim data — honest null
                SymbolicColor = SymbolicColor(f.Id),   // a deterministic, clearly-derived render hint
                SeatRegionId = foundingSeat.TryGetValue(f.Id, out int s) ? s : null,
                Prosperity = f.Prosperity,
                LeaderPersonId = f.LeaderId,
                Traits = traits,
            });
        }
        if (anyColorMissing)
            warnings.Add("faction.color is not modeled in sim data (null); symbolicColor is a deterministic derived hint, not authored lore.");

        // ---------------------------------------------------------------- sites
        var sites = new List<SiteDto>(w.Sites.All.Count);
        foreach (var st in w.Sites.All)
            sites.Add(new SiteDto
            {
                Id = st.Id,
                RegionId = st.RegionId,
                Name = st.Name,
                Type = st.Type,
                TypeLabel = SiteIndex.TypeLabel(st.Type),
                IsSeat = st.IsSeat,
                X = st.Nx,
                Y = st.Ny,
                DisplayRole = SiteDisplayRole(st.Type),
            });

        // ---------------------------------------------------------------- people highlights
        var highlightIds = new SortedSet<int>();
        foreach (var f in w.Factions.Values)
            if (f.LeaderId is int lid && w.People.TryGetValue(lid, out var lead) && lead.Alive)
                highlightIds.Add(lid);
        foreach (var p in w.People.Values)
            if (p.Alive && p.IsProphet) highlightIds.Add(p.Id);

        var peopleHighlights = new List<PersonDto>();
        bool currentRegionUnmodeled = false;
        foreach (int pid in highlightIds.Take(MaxPeopleHighlights))
        {
            var p = w.People[pid];
            var roles = new List<string>();
            if (p.IsLeader) roles.Add("leader");
            if (p.IsProphet) roles.Add("prophet");
            if (p.EverLeader && !p.IsLeader) roles.Add("former-leader");
            currentRegionUnmodeled = true;
            peopleHighlights.Add(new PersonDto
            {
                Id = p.Id,
                Name = p.Name,
                FactionId = p.FactionId,
                HomeRegionId = p.HomeRegionId,
                CurrentRegionId = null,   // a person's present location is not modeled — honest null
                RoleTags = roles,
                Alive = p.Alive,
                BirthYear = p.BirthYear,
                DeathYear = p.DeathYear,
                Age = p.Age(w.Year),
            });
        }
        if (peopleHighlights.Count == 0)
            warnings.Add("no living leaders or prophets to highlight at this year (peopleHighlights is empty).");
        if (currentRegionUnmodeled)
            warnings.Add("person.currentRegionId is not modeled by the sim (always null); homeRegionId is the remembered-home anchor, not a current location.");

        // ---------------------------------------------------------------- importance (cheap, incremental)
        var reverse = Scoring.BuildReverse(w);
        var consequence = new Dictionary<int, int>(reverse.Count);
        foreach (var kv in reverse) consequence[kv.Key] = kv.Value.Count;
        int Importance(Event e) => Scoring.ImportanceFast(e, w, consequence);

        // ---------------------------------------------------------------- chronicle path (3..7 beats)
        var beatCandidates = w.Chronicle.Events
            .Where(e => BeatTypes.Contains(e.Type))
            .OrderByDescending(Importance).ThenBy(e => e.Year).ThenBy(e => e.Id)
            .Take(MaxChronicleBeats)
            .OrderBy(e => e.Year).ThenBy(e => e.Id)
            .ToList();
        if (beatCandidates.Count < MinChronicleBeats)
            warnings.Add($"chroniclePath has only {beatCandidates.Count} beat(s); the world recorded fewer than {MinChronicleBeats} significant events at this year.");

        var pathIds = new HashSet<int>(beatCandidates.Select(e => e.Id));
        var chroniclePath = new List<BeatDto>(beatCandidates.Count);
        for (int i = 0; i < beatCandidates.Count; i++)
        {
            var e = beatCandidates[i];
            string? causalHint = e.Causes.Count > 0
                ? StoryGrammar.ProximateLink(w, e)?.RuleId
                : null;
            chroniclePath.Add(new BeatDto
            {
                BeatIndex = i,
                EventId = e.Id,
                Year = e.Year,
                Type = e.Type,
                RegionId = e.RegionId,
                HomeRegionId = e.HomeRegionId,
                Label = BeatLabel(e),
                CausalHint = causalHint,
            });
        }

        // ---------------------------------------------------------------- memory markers (bounded, anchored)
        var anchored = w.Chronicle.Events
            .Where(e => e.RegionId is not null || e.HomeRegionId is not null)
            .OrderByDescending(Importance).ThenBy(e => e.Year).ThenBy(e => e.Id)
            .Take(MaxMemoryMarkers)
            .OrderBy(e => e.Year).ThenBy(e => e.Id)
            .ToList();

        var memoryMarkers = new List<MarkerDto>(anchored.Count);
        foreach (var e in anchored)
        {
            var personIds = e.Participants.Take(MaxMarkerPersons).ToList();
            var facSet = new SortedSet<string>(StringComparer.Ordinal);
            foreach (int pid in e.Participants)
                if (w.People.TryGetValue(pid, out var p)) facSet.Add(p.FactionId);
            if (e.RegionId is int rid && rid >= 0 && rid < w.Regions.Count
                && w.Regions[rid].ControllingFactionId is string holder)
                facSet.Add(holder);

            memoryMarkers.Add(new MarkerDto
            {
                EventId = e.Id,
                Year = e.Year,
                Type = e.Type,
                RegionId = e.RegionId,
                HomeRegionId = e.HomeRegionId,
                MarkerKind = MarkerKindOf(e, pathIds),
                Label = BeatLabel(e),
                Description = string.IsNullOrWhiteSpace(e.Text) ? null : e.Text,
                InvolvedFactionIds = facSet.ToList(),
                InvolvedPersonIds = personIds,
            });
        }

        // ---------------------------------------------------------------- camera hints
        int focusRegion = -1, focusBest = 0;
        for (int i = 0; i < trueEventCount.Length; i++)
            if (trueEventCount[i] > focusBest) { focusBest = trueEventCount[i]; focusRegion = i; }

        BoundsDto? bounds = null;
        if (w.Regions.Count > 0)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var r in w.Regions)
            {
                minX = Math.Min(minX, r.X); maxX = Math.Max(maxX, r.X);
                minY = Math.Min(minY, r.Y); maxY = Math.Max(maxY, r.Y);
            }
            bounds = new BoundsDto { MinX = minX, MinY = minY, MaxX = maxX, MaxY = maxY };
        }
        var camera = new CameraHintsDto
        {
            PreferredMode = "atlas",
            RegionFocusId = focusRegion >= 0 ? focusRegion : null,
            Bounds = bounds,
        };
        if (focusRegion < 0)
            warnings.Add("cameraHints.regionFocusId is null — no region carries a recorded place-event yet.");

        // ---------------------------------------------------------------- assemble
        return new UnrealSnapshot
        {
            SchemaVersion = SchemaVersion,
            GeneratedBy = GeneratedBy,
            Seed = seed,
            Year = w.Year,
            WorldName = string.IsNullOrWhiteSpace(w.Island) ? null : w.Island,
            Counts = new CountsDto
            {
                Regions = regions.Count,
                Factions = factions.Count,
                Sites = sites.Count,
                PeopleAlive = w.Living().Count,
                PeopleEver = w.People.Count,
                Events = w.Chronicle.Events.Count,
                MemoryMarkers = memoryMarkers.Count,
                ChronicleBeats = chroniclePath.Count,
            },
            Regions = regions,
            Factions = factions,
            Sites = sites,
            PeopleHighlights = peopleHighlights,
            MemoryMarkers = memoryMarkers,
            ChroniclePath = chroniclePath,
            CameraHints = camera,
            ExportWarnings = warnings,
        };
    }

    // --- deterministic derivation rules (no Rng, no fabrication) -------------------------------

    /// <summary>A single Unreal render role for a region, derived only from real data: a held
    /// region reads as a settlement; an unheld region whose sites are predominantly sacred/funerary
    /// reads as ruin_or_sacred; otherwise the terrain maps directly. Never invents geography.</summary>
    private static string SuggestedRole(World w, Region region, Site? seat)
    {
        if (region.ControllingFactionId is not null) return "settlement";
        var local = w.Sites.ForRegion(region.Id);
        if (local.Count > 0)
        {
            int sacred = local.Count(s => s.Type is SiteType.Shrine or SiteType.SacredGrove
                                              or SiteType.OldBarrow or SiteType.CairnField);
            if (sacred * 2 >= local.Count) return "ruin_or_sacred";
        }
        return region.TerrainType switch
        {
            "forest" => "forest",
            "highland" => "highland",
            "coast" => "coast",
            "plains" => "grassland",
            _ => "unknown",
        };
    }

    private static string SiteDisplayRole(SiteType t) => t switch
    {
        SiteType.MarketVillage => "market",
        SiteType.FishingDock => "dock",
        SiteType.HillFort or SiteType.WatchPost => "fortification",
        SiteType.SacredGrove or SiteType.Shrine => "sacred",
        SiteType.OldBarrow or SiteType.CairnField => "ruin",
        SiteType.RiverFord => "ford",
        SiteType.Farmstead => "farm",
        _ => "camp",
    };

    /// <summary>The marker channel, in priority order so the four kinds stay deterministic and
    /// honest: a top chronicle beat reads as a beat; a remembered-home anchor (HomeRegionId with
    /// NO RegionId) is a memory cairn, never claimed as where the event happened; a land-fortune
    /// event over a region is a faction pulse; any other true place anchor is a place mark.</summary>
    private static string MarkerKindOf(Event e, HashSet<int> pathIds)
    {
        if (pathIds.Contains(e.Id)) return "chronicle_beat";
        if (e.RegionId is null && e.HomeRegionId is not null) return "home_memory_cairn";
        if (e.RegionId is not null && FactionPulseTypes.Contains(e.Type)) return "faction_pulse";
        if (e.RegionId is not null) return "true_place_mark";
        return "home_memory_cairn";   // unreachable given the anchored filter, but honest fallback
    }

    private static string BeatLabel(Event e)
    {
        string title = e.Type.Length > 0
            ? char.ToUpperInvariant(e.Type[0]) + e.Type[1..].Replace('_', ' ')
            : "Event";
        return $"{title} (Year {e.Year})";
    }

    /// <summary>A deterministic, clearly-derived render hint — NOT authored lore. FNV-1a over the
    /// faction id (the WorldSurface/Sites hash family), mapped to a "#RRGGBB" string.</summary>
    private static string SymbolicColor(string factionId)
    {
        unchecked
        {
            uint h = 2166136261u;
            foreach (char c in factionId) h = (h ^ c) * 16777619u;
            h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
            int r = (int)((h & 0xFF));
            int g = (int)((h >> 8) & 0xFF);
            int b = (int)((h >> 16) & 0xFF);
            return $"#{r:X2}{g:X2}{b:X2}";
        }
    }
}

// --------------------------------------------------------------------------- DTOs (stable order)
// Plain serializable shapes. Property declaration order IS the JSON field order (System.Text.Json),
// so the file layout is stable; no dictionaries are emitted, so there is no ordering ambiguity.

public sealed class UnrealSnapshot
{
    public string SchemaVersion { get; set; } = "";
    public string GeneratedBy { get; set; } = "";
    public int Seed { get; set; }
    public int Year { get; set; }
    public string? WorldName { get; set; }
    public CountsDto Counts { get; set; } = new();
    public List<RegionDto> Regions { get; set; } = new();
    public List<FactionDto> Factions { get; set; } = new();
    public List<SiteDto> Sites { get; set; } = new();
    public List<PersonDto> PeopleHighlights { get; set; } = new();
    public List<MarkerDto> MemoryMarkers { get; set; } = new();
    public List<BeatDto> ChroniclePath { get; set; } = new();
    public CameraHintsDto CameraHints { get; set; } = new();
    public List<string> ExportWarnings { get; set; } = new();
}

public sealed class CountsDto
{
    public int Regions { get; set; }
    public int Factions { get; set; }
    public int Sites { get; set; }
    public int PeopleAlive { get; set; }
    public int PeopleEver { get; set; }
    public int Events { get; set; }
    public int MemoryMarkers { get; set; }
    public int ChronicleBeats { get; set; }
}

public sealed class RegionDto
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string Terrain { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    public string? ControllingFactionId { get; set; }
    public int HomeMemoryCount { get; set; }
    public int TrueEventCount { get; set; }
    public string SuggestedUnrealRole { get; set; } = "";
}

public sealed class FactionDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Color { get; set; }
    public string SymbolicColor { get; set; } = "";
    public int? SeatRegionId { get; set; }
    public double Prosperity { get; set; }
    public int? LeaderPersonId { get; set; }
    public List<string> Traits { get; set; } = new();
}

public sealed class SiteDto
{
    public int Id { get; set; }
    public int RegionId { get; set; }
    public string Name { get; set; } = "";
    public SiteType Type { get; set; }
    public string TypeLabel { get; set; } = "";
    public bool IsSeat { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public string DisplayRole { get; set; } = "";
}

public sealed class PersonDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string FactionId { get; set; } = "";
    public int? HomeRegionId { get; set; }
    public int? CurrentRegionId { get; set; }
    public List<string> RoleTags { get; set; } = new();
    public bool Alive { get; set; }
    public int BirthYear { get; set; }
    public int? DeathYear { get; set; }
    public int Age { get; set; }
}

public sealed class MarkerDto
{
    public int EventId { get; set; }
    public int Year { get; set; }
    public string Type { get; set; } = "";
    public int? RegionId { get; set; }
    public int? HomeRegionId { get; set; }
    public string MarkerKind { get; set; } = "";
    public string Label { get; set; } = "";
    public string? Description { get; set; }
    public List<string> InvolvedFactionIds { get; set; } = new();
    public List<int> InvolvedPersonIds { get; set; } = new();
}

public sealed class BeatDto
{
    public int BeatIndex { get; set; }
    public int EventId { get; set; }
    public int Year { get; set; }
    public string Type { get; set; } = "";
    public int? RegionId { get; set; }
    public int? HomeRegionId { get; set; }
    public string Label { get; set; } = "";
    public string? CausalHint { get; set; }
}

public sealed class CameraHintsDto
{
    public string PreferredMode { get; set; } = "atlas";
    public int? RegionFocusId { get; set; }
    public BoundsDto? Bounds { get; set; }
}

public sealed class BoundsDto
{
    public float MinX { get; set; }
    public float MinY { get; set; }
    public float MaxX { get; set; }
    public float MaxY { get; set; }
}
