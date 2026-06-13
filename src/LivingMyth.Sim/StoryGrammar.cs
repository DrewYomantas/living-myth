namespace LivingMyth.Sim;

/// <summary>
/// The causal story grammar: a deterministic read-model over the chronicle, beside
/// Echoes/Feed/Scoring. It turns the web of Event.Causes links into STRUCTURED connector
/// claims — therefore / but / unresolved-until — that the viewer voices as language.
///
/// The provenance contract (PROJECT_STATE.md "Truth model V1") is enforced here by
/// construction: a ChainLink can only name a cause that is literally in the effect's
/// Causes list, gap years are real arithmetic, "but" comes only from the authored rule
/// table (never the generic fallback), and "the chronicle does not say" is an authored
/// allow-list over rootless events (the default is silence). No English lives here —
/// copy is the viewer's job (godot/StoryCopy.cs, audited by docs/VISUAL_STYLE.md).
///
/// Zero Rng, zero Record() calls, zero mutation of sim state: this module cannot move
/// the verify baseline. Cost class: Annotate is one Trace-equivalent (card-open one-shot,
/// never per-tick); OpenWars is one chronicle pass (recap-open). The `story` console gate
/// proves every claim against the recorded evidence on the verify seeds.
/// </summary>
public enum ConnectorKind
{
    Therefore,         // proven consequence (the Causes link itself)
    But,               // proven complication/reversal — authored rules only
    UnresolvedUntil,   // a grievance provably open across the whole gap (revenge)
}

/// <summary>Classification of a rootless event (no Causes). HonestUnknown is strictly
/// allow-listed — everything unrecognized defaults to Routine (say nothing), so the
/// "chronicle does not say" line can never flood or overclaim.</summary>
public enum OriginKind
{
    RecordedMotive,    // the event text already states why (ambition, old age, the founding)
    ThresholdState,    // a state threshold crossing made it happen (famine, boom, custom)
    HonestUnknown,     // the sim genuinely does not record why — the player may write a telling
    Routine,           // ordinary life (births, weddings) — no origin line at all
}

/// <summary>A proven edge from an event back to its proximate cause.</summary>
public sealed class ChainLink
{
    public ConnectorKind Kind { get; }
    public string RuleId { get; }
    public int CauseEventId { get; }   // always ∈ effect.Causes — gate-enforced
    public int GapYears { get; }       // effect.Year - cause.Year, real arithmetic

    public ChainLink(ConnectorKind kind, string ruleId, int causeEventId, int gapYears)
    {
        Kind = kind;
        RuleId = ruleId;
        CauseEventId = causeEventId;
        GapYears = gapYears;
    }
}

public sealed class OriginInfo
{
    public OriginKind Kind { get; }
    public string CopyKey { get; }        // "prophet" | "schism" | "forbidden-bond" | "" — viewer copy selector
    public int? SubjectPersonId { get; }  // set when the honest-unknown copy names a person

    public OriginInfo(OriginKind kind, string copyKey = "", int? subjectPersonId = null)
    {
        Kind = kind;
        CopyKey = copyKey;
        SubjectPersonId = subjectPersonId;
    }
}

/// <summary>One event in an annotated chain. Link is the proven edge to its proximate
/// cause (null for roots); Origin classifies a root (null when the event has causes).</summary>
public sealed class ChainStep
{
    public Event Event { get; }
    public ChainLink? Link { get; }
    public OriginInfo? Origin { get; }

    public ChainStep(Event ev, ChainLink? link, OriginInfo? origin)
    {
        Event = ev;
        Link = link;
        Origin = origin;
    }
}

public sealed class AnnotatedChain
{
    public int TargetEventId { get; }
    /// <summary>Same membership as Chronicle.Trace, ordered by event id — record order,
    /// which guarantees every cause precedes its effect (ids are strictly increasing).</summary>
    public List<ChainStep> Steps { get; }

    public AnnotatedChain(int targetEventId, List<ChainStep> steps)
    {
        TargetEventId = targetEventId;
        Steps = steps;
    }
}

/// <summary>A murder still provably unresolved: the victim's line never answered it.</summary>
public sealed class OpenGrievance
{
    public int VictimId { get; }
    public int KillerId { get; }
    public int MurderEventId { get; }
    public int MurderYear { get; }
    public bool KillerAlive { get; }   // false ⇒ the sim can provably never resolve it

    public OpenGrievance(int victimId, int killerId, int murderEventId, int murderYear, bool killerAlive)
    {
        VictimId = victimId;
        KillerId = killerId;
        MurderEventId = murderEventId;
        MurderYear = murderYear;
        KillerAlive = killerAlive;
    }
}

/// <summary>A war no peace event has ever cited as its cause.</summary>
public sealed class OpenWar
{
    public int WarEventId { get; }
    public int DeclaredYear { get; }

    public OpenWar(int warEventId, int declaredYear)
    {
        WarEventId = warEventId;
        DeclaredYear = declaredYear;
    }
}

public static class StoryGrammar
{
    /// <summary>The authored But-set: the only rules allowed to claim a reversal. The
    /// story gate asserts no But ever fires outside this set.</summary>
    public static readonly HashSet<string> ButRules = new()
    { "persecution-of-faith", "scandal-breaks", "honor-killing", "war-despite-peace", "ways-shed", "ways-grate",
      "famine-despite-protection", "death-despite-blessing" };

    /// <summary>Annotate the full causal chain behind one event (card-open one-shot —
    /// the same Trace cost the catch-up panel already pays).</summary>
    public static AnnotatedChain Annotate(World world, int eventId)
    {
        var chain = world.Chronicle.Trace(eventId);
        chain.Sort((a, b) => a.Id.CompareTo(b.Id));   // record order: causes always precede effects
        var steps = new List<ChainStep>(chain.Count);
        foreach (var e in chain)
            steps.Add(e.Causes.Count > 0
                ? new ChainStep(e, LinkFor(world, e), null)
                : new ChainStep(e, null, ClassifyOrigin(e)));
        return new AnnotatedChain(eventId, steps);
    }

    /// <summary>The single proven edge behind one event — the guard card's "why" line.
    /// Null when the event records no causes.</summary>
    public static ChainLink? ProximateLink(World world, Event effect)
        => effect.Causes.Count == 0 ? null : LinkFor(world, effect);

    /// <summary>Classify one LITERAL recorded edge: cause → effect, but only when the cause
    /// is truly in the effect's Causes list — null otherwise. The replay consequence rail's
    /// honest connector (an effect's proximate link may point at a different cause; this
    /// names the specific edge being walked). Same rule table, zero new claims.</summary>
    public static ChainLink? LinkBetween(World world, Event cause, Event effect)
    {
        if (!effect.Causes.Contains(cause.Id)) return null;
        var (kind, ruleId) = Classify(world, cause, effect);
        return new ChainLink(kind, ruleId, cause.Id, effect.Year - cause.Year);
    }

    /// <summary>Pick the proximate cause (latest year, tie → highest id — the most recent
    /// recorded reason) and classify the edge through the rule table.</summary>
    private static ChainLink LinkFor(World world, Event effect)
    {
        var cause = world.Chronicle.Get(effect.Causes[0]);
        for (int i = 1; i < effect.Causes.Count; i++)
        {
            var c = world.Chronicle.Get(effect.Causes[i]);
            if (c.Year > cause.Year || (c.Year == cause.Year && c.Id > cause.Id)) cause = c;
        }
        var (kind, ruleId) = Classify(world, cause, effect);
        return new ChainLink(kind, ruleId, cause.Id, effect.Year - cause.Year);
    }

    /// <summary>The connector rule table V1 (PROJECT_STATE.md). First match wins; the
    /// fallback is generic Therefore — Causes literally means "events that led to this
    /// one" (Chronicle.cs), so Therefore is the only safe default. But is never generic.</summary>
    private static (ConnectorKind kind, string ruleId) Classify(World world, Event cause, Event effect)
    {
        // Revenge: the grievance was provably open the whole gap — the victim's Avenged
        // flag flips only on this event, and one revenge per victim is the sim contract.
        if (effect.Type == "murder" && effect.Tags.Contains("revenge") && cause.Type == "murder"
            && cause.Participants.Count > 0 && effect.Participants.Count > 0
            && world.People.TryGetValue(cause.Participants[0], out var origVictim)
            && origVictim.MurderEventId == cause.Id
            && origVictim.Avenged
            && origVictim.KillerId == effect.Participants[0])
            return (ConnectorKind.UnresolvedUntil, "revenge-unresolved");

        if (effect.Type == "martyr" && cause.Type == "murder")
            return (ConnectorKind.Therefore, "martyr-made");
        if (effect.Type == "succession" && cause.Type is "death" or "murder")
            return (ConnectorKind.Therefore, "succession");
        if (effect.Type == "leadership" && cause.Type is "death" or "murder")
            return (ConnectorKind.Therefore, "leaderless");

        // A faith proclaimed/held → an adherent killed for holding it. Reversal by rule.
        // Primordial faiths trace to the world-founding event, so "founding" matches too.
        if (effect.Type == "murder" && effect.Tags.Contains("persecution")
            && cause.Type is "prophet" or "schism" or "founding")
            return (ConnectorKind.But, "persecution-of-faith");

        if (effect.Type == "justice" && cause.Type == "murder")
            return (ConnectorKind.Therefore, "justice-served");

        if (effect.Type == "scandal" && cause.Type == "romance" && cause.Tags.Contains("forbidden"))
            return (ConnectorKind.But, "scandal-breaks");
        if (effect.Type == "murder" && effect.Tags.Contains("honor")
            && cause.Type == "romance" && cause.Tags.Contains("forbidden"))
            return (ConnectorKind.But, "honor-killing");
        if (effect.Type == "romance" && effect.Tags.Contains("peace")
            && cause.Type == "romance" && cause.Tags.Contains("forbidden"))
            return (ConnectorKind.Therefore, "union-blessed");
        if (effect.Type == "marriage" && cause.Type == "romance" && cause.Tags.Contains("forbidden"))
            return (ConnectorKind.Therefore, "union-wed");

        if (effect.Type == "war" && cause.Type == "rumor")
            return (ConnectorKind.Therefore, "war-of-whispers");
        // The one edge where generic-Therefore would lie: a blessed union EASED tension
        // (AddTension with a negative amount still lands the event in grievance memory),
        // yet war came regardless. Authored as the reversal it is.
        if (effect.Type == "war" && cause.Type == "romance" && cause.Tags.Contains("peace"))
            return (ConnectorKind.But, "war-despite-peace");
        if (effect.Type == "war")
            return (ConnectorKind.Therefore, "war-from-grievance");

        // A war brings armies to its front, and the fighting falls upon a place.
        if (effect.Type == "battle" && cause.Type == "war")
            return (ConnectorKind.Therefore, "war-to-battle");

        if (effect.Type == "peace" && cause.Type == "war")
            return (ConnectorKind.Therefore, "peace-made");
        if (effect.Type == "territory" && effect.Tags.Contains("war") && cause.Type == "peace")
            return (ConnectorKind.Therefore, "land-seized");
        if (effect.Type == "territory" && effect.Tags.Contains("abandonment") && cause.Type is "death" or "murder")
            return (ConnectorKind.Therefore, "land-abandoned");

        if (effect.Type == "rumor")
            return (ConnectorKind.Therefore, "talk-of-deed");   // gossip never invents truth

        // God-hand evidence (divine pressure V1): a famine arriving under an active doom was
        // truly pressed down by it (the prosperity drag is mechanical fact); one arriving
        // despite an active protection is the authored reversal — the shield stood and was
        // overcome. Both edges exist only because Economy() recorded the divine cause.
        if (effect.Type == "famine" && cause.Type == "divine" && cause.Tags.Contains("doom"))
            return (ConnectorKind.Therefore, "famine-under-doom");
        if (effect.Type == "famine" && cause.Type == "divine" && cause.Tags.Contains("protect"))
            return (ConnectorKind.But, "famine-despite-protection");

        if (effect.Type == "famine_end" && cause.Type == "famine")
            return (ConnectorKind.Therefore, "famine-breaks");   // the land that starved recovers

        if (effect.Type == "death" && cause.Type == "famine")
            return (ConnectorKind.Therefore, "famine-death");
        if (effect.Type == "death" && cause.Type == "battle")
            return (ConnectorKind.Therefore, "battle-death");
        if (effect.Type == "death" && cause.Type == "war")
            return (ConnectorKind.Therefore, "war-death");
        // A blessed life ends anyway: the multiplier truly leaned this very roll, so the
        // reversal is mechanical fact, not mood. Curse deaths stay the plain therefore.
        if (effect.Type == "death" && cause.Type == "divine" && cause.Tags.Contains("blessing"))
            return (ConnectorKind.But, "death-despite-blessing");
        if (effect.Type == "death" && cause.Type == "divine")
            return (ConnectorKind.Therefore, "curse-death");
        if (effect.Type == "death" && cause.Type == "murder")
            return (ConnectorKind.Therefore, "executed");   // Kill(killer, "executed…", cause: murder)

        if (effect.Type == "custom" && effect.Tags.Contains("fade") && cause.Type == "custom")
            return (ConnectorKind.But, "ways-shed");
        if (effect.Type == "custom" && effect.Tags.Contains("clash") && cause.Type == "custom")
            return (ConnectorKind.But, "ways-grate");
        if (effect.Type == "custom" && effect.Tags.Contains("diffusion") && cause.Type == "custom")
            return (ConnectorKind.Therefore, "ways-spread");

        return (ConnectorKind.Therefore, "generic-cause");
    }

    /// <summary>Classify a rootless event. HonestUnknown is an allow-list; the default
    /// is Routine — silence — so the unknown-origin line can never overclaim.</summary>
    public static OriginInfo ClassifyOrigin(Event e)
    {
        switch (e.Type)
        {
            case "prophet":
                return new OriginInfo(OriginKind.HonestUnknown, "prophet",
                    e.Participants.Count > 0 ? e.Participants[0] : null);
            case "schism":
                return new OriginInfo(OriginKind.HonestUnknown, "schism");
            case "romance" when e.Tags.Contains("forbidden"):
                return new OriginInfo(OriginKind.HonestUnknown, "forbidden-bond");

            case "founding":      // the chronicle's first words are their own origin
            case "leadership":    // "eldest of their people" — the rule is stated
            case "territory":     // settlement / abandonment — the text states it
            case "divine":        // the god's hand (the player) is the recorded agent
            case "friction":      // "over the worship of X and Y" — motive stated
            case "death":         // manner stated ("of a fever", "of old age at N")
            case "murder":        // motive in text ("in a grasp for power", "for the heresy of…")
                return new OriginInfo(OriginKind.RecordedMotive);

            case "famine":        // harvest crossed the famine threshold
            case "famine_end":    // harvest climbed back (always also cause-linked to its onset)
            case "boom":
            case "trade":
            case "custom":        // a value axis crossed the identity threshold
            case "war":           // tension passed the war threshold (grievances usually recorded)
                return new OriginInfo(OriginKind.ThresholdState);

            default:              // births, weddings, anything unrecognized — say nothing
                return new OriginInfo(OriginKind.Routine);
        }
    }

    /// <summary>Murders still provably unanswered among the given people: Murdered and
    /// never Avenged. KillerAlive=false means the sim can never resolve it (the revenge
    /// scan drops victims whose killer is gone). O(|peopleIds|).</summary>
    public static List<OpenGrievance> OpenGrievances(World world, IReadOnlyCollection<int> peopleIds)
    {
        var outp = new List<OpenGrievance>();
        foreach (int id in peopleIds.OrderBy(i => i))
        {
            if (!world.People.TryGetValue(id, out var v)) continue;
            if (!v.Murdered || v.Avenged || v.KillerId is not int kid || v.MurderEventId is not int mid) continue;
            bool killerAlive = world.People.TryGetValue(kid, out var k) && k.Alive;
            outp.Add(new OpenGrievance(v.Id, kid, mid, world.Chronicle.Get(mid).Year, killerAlive));
        }
        return outp;
    }

    /// <summary>Wars with no peace event citing them as cause — still burning, provably.
    /// One chronicle pass (recap-open cost class, never per-tick).</summary>
    public static List<OpenWar> OpenWars(World world)
    {
        var resolved = new HashSet<int>();
        var wars = new List<Event>();
        foreach (var e in world.Chronicle.Events)
        {
            if (e.Type == "war") wars.Add(e);
            else if (e.Type == "peace")
                foreach (int c in e.Causes) resolved.Add(c);
        }
        var outp = new List<OpenWar>();
        foreach (var w in wars)
            if (!resolved.Contains(w.Id))
                outp.Add(new OpenWar(w.Id, w.Year));
        return outp;
    }
}
