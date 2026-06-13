using System.Collections.Generic;
using LivingMyth.Sim;

// All causal-story and player-canon English lives here — one auditable file, mirrored by
// the binding tables in docs/VISUAL_STYLE.md ("Causal story language" / "Player canon
// display language"). The grammar (LivingMyth.Sim/StoryGrammar.cs) emits structured,
// evidence-backed kinds; this file only voices them. It must never add causality of its
// own: every phrase below is selected purely by ChainLink/OriginInfo fields.
public static class StoryCopy
{
    // ---- connector lead-ins (lines between record rows in How We Got Here) ----
    // The caller voices a connector only when the proven cause row is visible above —
    // never aimed at a hidden row.
    public static string ConnectorPhrase(ChainLink link) => link.Kind switch
    {
        ConnectorKind.UnresolvedUntil => link.GapYears >= 1
            ? $"the grievance lay unresolved for {link.GapYears} {YearWord(link.GapYears)}, until —"
            : "the grievance lay unresolved within the year, until —",
        ConnectorKind.But => link.RuleId switch
        {
            // God-hand reversals: the pressure truly leaned on this very roll — the
            // grammar proved it before this file is allowed to say it.
            "death-despite-blessing" => "but even the old blessing could not hold them —",
            "famine-despite-protection" => "but even under the protection laid upon them —",
            _ => link.GapYears >= 3 ? $"{link.GapYears} years on — but" : "but —",
        },
        _ => link.RuleId switch
        {
            "war-of-whispers" => "the whispers fed it — therefore,",
            "curse-death" => "the curse found another life — therefore,",
            "famine-under-doom" => "the doom upon them bore down — therefore,",
            _ => link.GapYears >= 3 ? $"{link.GapYears} years passed — therefore," : "therefore —",
        },
    };

    // ---- the guard card's one "why" line (lead before the linked cause) ----
    public static string WhyLead(ChainLink link) => link.Kind switch
    {
        ConnectorKind.UnresolvedUntil => link.GapYears >= 1
            ? $"answers a grievance left unresolved for {link.GapYears} {YearWord(link.GapYears)}:"
            : "answers a grievance of this same year:",
        ConnectorKind.But => "came despite what stood before:",
        _ => link.GapYears >= 3 ? $"follows from, {link.GapYears} years past:" : "follows from:",
    };

    private static string YearWord(int n) => n == 1 ? "year" : "years";

    // ---- honest-unknown origins ----
    // Only HonestUnknown roots speak (RecordedMotive/ThresholdState/Routine stay silent —
    // their event text already carries the truth). Returns null for everything else.
    public static string? OriginLine(OriginInfo origin, World world) =>
        origin.Kind != OriginKind.HonestUnknown ? null : origin.CopyKey switch
        {
            "prophet" => $"the chronicle does not record what first stirred {SubjectName(origin, world, "the prophet")}.",
            "schism" => "the chronicle does not record what doctrine divided them.",
            "forbidden-bond" => "what drew them together, the chronicle does not say.",
            _ => null,
        };

    // The matching write affordance — the door into the player's own telling.
    public static string? WriteAffordance(OriginInfo origin, World world) =>
        origin.Kind != OriginKind.HonestUnknown ? null : origin.CopyKey switch
        {
            "prophet" => $"✎ write what stirred {SubjectName(origin, world, "them")}",
            "schism" => "✎ write what divided them",
            "forbidden-bond" => "✎ write what drew them together",
            _ => null,
        };

    private static string SubjectName(OriginInfo origin, World world, string fallback)
        => origin.SubjectPersonId is int pid && world.People.TryGetValue(pid, out var p) ? p.Name : fallback;

    // ---- anchor language (binding — docs/VISUAL_STYLE.md "Anchor language") ----
    // The three channels never blur: "at {site}" ONLY for a true Event.SiteId, "in {region}"
    // for RegionId, "remembered in {region}" for HomeRegionId (a memory, never a location),
    // and an honest nothing when the chronicle does not place an event.
    public static string? AnchorPhrase(World w, Event e)
    {
        if (e.SiteId is int sid && e.RegionId is int srid)
            return $"at {w.Sites.Get(sid).Name}, in {w.RegionName(srid)}";
        if (e.RegionId is int rid) return $"in {w.RegionName(rid)}";
        if (e.HomeRegionId is int hid) return $"remembered in {w.RegionName(hid)}";
        return null;
    }

    // The replay rail's status words — one honest label per ReplayBeat.Status.
    public static string StatusLabel(string status) => status switch
    {
        "site-anchored" => "a true place",
        "region-only" => "a land, no single place",
        "memory-only" => "remembered at a home — not where it happened",
        _ => "unplaced — the chronicle does not say where",
    };

    // ---- turning points (the authored kinds from Replay.TurningPointKind) ----
    public static string TurningPointLabel(string kind) => kind switch
    {
        "war-pivot" => "a war begins",
        "peace-pivot" => "a peace is made",
        "land-lost" => "land changes hands",
        "land-abandoned" => "a people's holds fall silent",
        "violent-succession" => "a murdered seat passes on",
        "faith-torn" => "a faith is torn",
        "faith-proclaimed" => "a faith is proclaimed",
        "ways-hardened" => "a people's ways harden",
        "divine-influenced" => "your hand is in this",
        "far-reaching" => "its consequences run far",
        _ => "a turning point",
    };

    // ---- site memory: the honest "known for" words (real recorded counts only) ----
    public static string KnownForPhrase(string kind, int count) => kind switch
    {
        "founding" => "a people's first hold was raised here",
        "war" => count > 1 ? $"fought over {count} times" : "fought over once",
        "abandonment" => "holds fell silent here",
        "ways-sworn" => count > 1 ? $"ways were sworn here {count} times" : "ways were sworn here",
        "ways-shed" => "old ways were shed here",
        _ => count > 1 ? $"{count} recorded {kind}s" : $"a recorded {kind}",
    };

    // ---- player canon labels (the only five — docs/VISUAL_STYLE.md) ----
    public static string CanonLabel(CanonNoteType t) => t switch
    {
        CanonNoteType.Telling => "Your telling",
        CanonNoteType.ChroniclerNote => "Chronicler's note",
        CanonNoteType.Inscription => "Memorial inscription",
        CanonNoteType.PlaceLegend => "Place legend",
        CanonNoteType.PeopleSay => "What the people say",
        _ => "Your telling",
    };

    // ---- glossary ----
    // Small in-world glosses behind confusing terms. RichTextLabel surfaces wrap with
    // Hint() ([hint] BBCode); plain Labels reuse the same text via TooltipText.
    public static readonly Dictionary<string, string> Glossary = new()
    {
        ["little known"] = "no rumor has yet touched this name — standing is earned in the telling",
        ["whispered against"] = "rumors have begun to stain this name",
        ["well spoken of"] = "kind talk lifts this name",
        ["admired"] = "this name is spoken warmly across the peoples",
        ["infamous"] = "rumor upon rumor has blackened this name",
        ["martyr"] = "a prophet killed for their faith — their death swells the faith they leave behind",
        ["remembered in"] = "where the line is rooted — a memory anchor, not where this happened",
        ["rooted in"] = "the home of this line — heritage, not a place the chronicle witnessed",
        ["memorial cairn"] = "stones raised at the home of a line, for a murdered or once-leading soul",
        ["guard"] = "the focus guard pauses time when fate touches what you follow",
        ["followed land"] = "a watched land: tales anchored here and lives remembered here become yours",
        ["blessed"] = "fate leans gently toward this life — the death roll eases, never a guarantee",
        ["protected"] = "while the protection holds, famine weighs lighter and fortune mends a little faster",
        ["doomed"] = "while the doom holds, fortune runs thin and famine bites deeper",
        ["omen"] = "the eye of fate: this land's tales surface louder while the omen hangs — no roll changes",
    };

    // Glossary values are dropped raw into [hint="…"] — they must never contain a double
    // quote or a closing bracket, or the BBCode breaks. Keep new entries plain prose.
    public static string Hint(string text, string key)
        => Glossary.TryGetValue(key, out var tip) ? $"[hint=\"{tip}\"]{text}[/hint]" : text;
}
