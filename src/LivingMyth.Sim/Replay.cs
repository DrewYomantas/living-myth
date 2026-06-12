namespace LivingMyth.Sim;

/// <summary>
/// Chronicle Replay preparation: one beat per event in a causal chain, in a shape a
/// future visual "How We Got Here" path can walk without inventing anything. A pure
/// read-model over StoryGrammar.Annotate — zero Rng, zero mutation, baseline-inert.
///
/// Honesty rules: RegionId is the event's own anchor (where it happened) or null;
/// SiteId is ALWAYS null in V1 — no event carries a site anchor yet (Event.SiteId is
/// deferred; the `sites` gate asserts the field does not exist), and this helper must
/// never infer one. Connector/CauseEventId come only from the proven grammar edge.
/// </summary>
public sealed class ReplayBeat
{
    public int EventId { get; }
    public int Year { get; }
    public int? RegionId { get; }          // the event's true place anchor, never inferred
    public int? SiteId { get; }            // always null in V1 — sites are not event anchors yet
    public IReadOnlyList<int> Participants { get; }
    public string Connector { get; }       // "therefore" | "but" | "unresolved-until" | origin kind for roots
    public int? CauseEventId { get; }      // the proven proximate cause, when one exists
    public string Category { get; }        // the event's recorded type — the display class key

    public ReplayBeat(int eventId, int year, int? regionId, IReadOnlyList<int> participants,
                      string connector, int? causeEventId, string category)
    {
        EventId = eventId;
        Year = year;
        RegionId = regionId;
        SiteId = null;
        Participants = participants;
        Connector = connector;
        CauseEventId = causeEventId;
        Category = category;
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
            string connector = step.Link is ChainLink link
                ? link.Kind switch
                {
                    ConnectorKind.But => "but",
                    ConnectorKind.UnresolvedUntil => "unresolved-until",
                    _ => "therefore",
                }
                : step.Origin!.Kind switch
                {
                    OriginKind.HonestUnknown => "unknown-origin",
                    OriginKind.ThresholdState => "threshold",
                    OriginKind.RecordedMotive => "recorded-motive",
                    _ => "routine",
                };
            beats.Add(new ReplayBeat(e.Id, e.Year, e.RegionId, e.Participants,
                connector, step.Link?.CauseEventId, e.Type));
        }
        return beats;
    }
}
