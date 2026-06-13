namespace LivingMyth.Sim;

/// <summary>
/// Chronicle Replay V2 — the durable read-model behind the visual "How We Got Here" path.
/// One beat per event: the annotated cause chain (record order, causes always precede
/// effects) plus a bounded direct-consequence rail. Pure functions over StoryGrammar and
/// the chronicle — zero Rng, zero mutation, deterministic by construction; the `replay`
/// console gate proves it on the verify seeds.
///
/// Honesty rules (the anchor semantics, binding in PROJECT_STATE.md):
///  - RegionId / SiteId / HomeRegionId are copied VERBATIM from the event — never inferred,
///    never substituted for one another.
///  - Status names exactly what the anchors honestly allow: "site-anchored" (a real modeled
///    place), "region-only" (a land, no single place), "memory-only" (remembered at a home
///    root — NOT where it happened; the viewer must never pin it to the map), "unanchored"
///    (the chronicle does not place it — side-rail/timeline only).
///  - Connector and CopyKey come only from the proven grammar edge (or origin class) —
///    authored copy keys, never loose prose.
/// </summary>
public sealed class ReplayBeat
{
    public int EventId { get; }
    public int Year { get; }
    public int? RegionId { get; }          // where it happened — verbatim, never inferred
    public int? SiteId { get; }            // the modeled place it truly belongs to — verbatim
    public int? HomeRegionId { get; }      // where it is remembered — never a location claim
    public IReadOnlyList<int> Participants { get; }
    public IReadOnlyList<string> FactionIds { get; }   // the participants' peoples, first-named order
    public string Connector { get; }       // "therefore" | "but" | "unresolved-until" | origin kinds
    public int? CauseEventId { get; }      // the proven proximate cause, when one exists
    public string Category { get; }        // the event's recorded type — the display class key
    public string CopyKey { get; }         // authored rule/origin key ("war-of-whispers", "prophet", "")
    public string Status { get; }          // "site-anchored" | "region-only" | "memory-only" | "unanchored"

    public ReplayBeat(Event e, string connector, int? causeEventId, string copyKey,
                      IReadOnlyList<string> factionIds)
    {
        EventId = e.Id;
        Year = e.Year;
        RegionId = e.RegionId;
        SiteId = e.SiteId;
        HomeRegionId = e.HomeRegionId;
        Participants = e.Participants;
        FactionIds = factionIds;
        Connector = connector;
        CauseEventId = causeEventId;
        Category = e.Type;
        CopyKey = copyKey;
        Status = e.SiteId is not null ? "site-anchored"
            : e.RegionId is not null ? "region-only"
            : e.HomeRegionId is not null ? "memory-only"
            : "unanchored";
    }
}

/// <summary>One selected event's full replay shape: its cause chain (the beats, ending at
/// the focal event) and the bounded direct-consequence rail that followed from it.</summary>
public sealed class ReplayChain
{
    public int FocalEventId { get; }
    public List<ReplayBeat> Beats { get; }            // ancestry + focal, record order
    public List<ReplayBeat> Consequences { get; }     // direct consequences, record order, capped
    public int TotalConsequences { get; }             // the real count, even past the cap

    public ReplayChain(int focalEventId, List<ReplayBeat> beats,
                       List<ReplayBeat> consequences, int totalConsequences)
    {
        FocalEventId = focalEventId;
        Beats = beats;
        Consequences = consequences;
        TotalConsequences = totalConsequences;
    }
}

public static class Replay
{
    /// <summary>The replay-ready beats behind one event: the annotated chain in record
    /// order (causes always precede effects). Card-open cost class, never per-tick.</summary>
    public static List<ReplayBeat> BeatsFor(World world, int eventId)
    {
        var ann = StoryGrammar.Annotate(world, eventId);
        var beats = new List<ReplayBeat>(ann.Steps.Count);
        foreach (var step in ann.Steps)
        {
            var e = step.Event;
            string connector, copyKey;
            if (step.Link is ChainLink link)
            {
                connector = link.Kind switch
                {
                    ConnectorKind.But => "but",
                    ConnectorKind.UnresolvedUntil => "unresolved-until",
                    _ => "therefore",
                };
                copyKey = link.RuleId;
            }
            else
            {
                connector = step.Origin!.Kind switch
                {
                    OriginKind.HonestUnknown => "unknown-origin",
                    OriginKind.ThresholdState => "threshold",
                    OriginKind.RecordedMotive => "recorded-motive",
                    _ => "routine",
                };
                copyKey = step.Origin.CopyKey;
            }
            beats.Add(new ReplayBeat(e, connector, step.Link?.CauseEventId, copyKey,
                                     FactionsOf(world, e)));
        }
        return beats;
    }

    /// <summary>The full replay shape: cause beats plus the bounded direct-consequence rail.
    /// One chronicle pass for the consequences (Trace already costs the same) — card-open
    /// cost class, never per-tick or per-frame.</summary>
    public static ReplayChain ChainFor(World world, int eventId, int consequenceCap = 8)
    {
        var beats = BeatsFor(world, eventId);
        var focal = world.Chronicle.Get(eventId);
        var consequences = new List<ReplayBeat>();
        int total = 0;
        var events = world.Chronicle.Events;
        for (int i = eventId + 1; i < events.Count; i++)
        {
            var e = events[i];
            if (!e.Causes.Contains(eventId)) continue;
            total++;
            if (consequences.Count >= consequenceCap) continue;
            // The connector names THIS literal edge (focal → consequence), proven by the
            // grammar — the consequence's own proximate link may point elsewhere.
            var link = StoryGrammar.LinkBetween(world, focal, e)!;
            string connector = link.Kind switch
            {
                ConnectorKind.But => "but",
                ConnectorKind.UnresolvedUntil => "unresolved-until",
                _ => "therefore",
            };
            consequences.Add(new ReplayBeat(e, connector, eventId, link.RuleId,
                                            FactionsOf(world, e)));
        }
        return new ReplayChain(eventId, beats, consequences, total);
    }

    /// <summary>The participants' peoples, deduped in participant order. A person's
    /// FactionId is their last recorded people — real sim state, deterministic.</summary>
    private static List<string> FactionsOf(World world, Event e)
    {
        var fids = new List<string>();
        foreach (int pid in e.Participants)
            if (world.People.TryGetValue(pid, out var p) && !fids.Contains(p.FactionId))
                fids.Add(p.FactionId);
        return fids;
    }

    /// <summary>
    /// The bounded turning-point classifier: NOT every event — only the authored pivots
    /// below, first match wins. Deterministic over (event content, consequence count);
    /// the consequence count is whatever the caller honestly knows at ask time (the live
    /// viewer's running tally, or a full-pass count in the gate). Returns the authored
    /// kind key (viewer copy in StoryCopy.TurningPointLabel) or null.
    ///
    /// The rule table (binding in PROJECT_STATE.md):
    ///  war-pivot           — a war begins (type war)
    ///  peace-pivot         — a war ends (type peace)
    ///  land-lost           — land seized in war (territory + war)
    ///  land-abandoned      — a people's holds fall silent (territory + abandonment)
    ///  violent-succession  — a seat changes hands because its holder was murdered
    ///  faith-torn          — a schism splits a faith
    ///  faith-proclaimed    — a prophet founds a faith
    ///  ways-hardened       — a people's values harden into a named custom (custom born)
    ///  divine-influenced   — the chronicle traces this event directly to an act of the hand
    ///  far-reaching        — at least 4 recorded consequences grew from it
    /// </summary>
    public static string? TurningPointKind(World world, Event e, int consequenceCount)
    {
        switch (e.Type)
        {
            case "war": return "war-pivot";
            case "peace": return "peace-pivot";
            case "territory" when e.Tags.Contains("war"): return "land-lost";
            case "territory" when e.Tags.Contains("abandonment"): return "land-abandoned";
            case "succession" when e.Causes.Count > 0
                && world.Chronicle.Get(e.Causes[0]).Type == "murder":
                return "violent-succession";
            case "schism": return "faith-torn";
            case "prophet": return "faith-proclaimed";
            case "custom" when !e.Tags.Contains("fade") && !e.Tags.Contains("clash")
                && !e.Tags.Contains("diffusion"):
                return "ways-hardened";
        }
        foreach (int c in e.Causes)
            if (world.Chronicle.Get(c).Type == "divine") return "divine-influenced";
        if (consequenceCount >= 4) return "far-reaching";
        return null;
    }
}
