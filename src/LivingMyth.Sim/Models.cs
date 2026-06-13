namespace LivingMyth.Sim;

/// <summary>
/// The things that exist in the world: people, factions, religions. Kept deliberately
/// simple; every field here is real data the simulation knows and the chronicle can
/// answer for.
/// </summary>
public sealed class Person
{
    public int Id { get; }
    public string Name { get; }
    public string FactionId { get; set; }
    public int BirthYear { get; }
    public string Sex { get; }                 // "m" or "f"

    public bool Alive { get; set; } = true;
    public int? DeathYear { get; set; }
    public int? SpouseId { get; set; }
    public List<int> Parents { get; } = new();
    public List<int> Children { get; } = new();
    public bool IsLeader { get; set; }

    // crime / memory
    public int? KillerId { get; set; }         // who murdered this person, if anyone
    public bool Murdered { get; set; }
    public bool Avenged { get; set; }
    public int? MurderEventId { get; set; }    // the chronicle event of their murder
    public bool Cursed { get; set; }           // seed flag, used by the god's hand
    public bool EverLeader { get; set; }       // was ever a leader (stays true after death)
    public int? ReligionId { get; set; }
    public bool IsProphet { get; set; }

    // gossip / reputation (M8): social ripples from real events. Reputation is public standing
    // (-5 infamous … 0 … +5 admired); LastRumorYear is a per-person cooldown so one name can't
    // spam the rumor mill. Both are bounded, deterministic, and ride on the chronicle.
    public int Reputation { get; set; }
    public int LastRumorYear { get; set; } = int.MinValue;

    // home contract (anchoring V1): the region this person's line is rooted in. Founders get
    // their people's founding seat (the same region the founding-territory event anchors);
    // newborns inherit father's home, else mother's. Never reassigned — home is heritage, not
    // current control. Null = the chronicle honestly does not record where this line is rooted.
    public int? HomeRegionId { get; set; }

    // god-hand V1: a blessing leans fate gently toward this one life (bless_death_multiplier
    // on the existing death roll — subtle, never a guarantee). BlessEvent is the recorded act,
    // so a blessed soul's eventual death can cause-link honestly back to the player's hand.
    public bool Blessed { get; set; }
    public Event? BlessEvent { get; set; }

    public Person(int id, string name, string factionId, int birthYear, string sex)
    {
        Id = id;
        Name = name;
        FactionId = factionId;
        BirthYear = birthYear;
        Sex = sex;
    }

    public int Age(int year) => year - BirthYear;
}

public sealed class Religion
{
    public int Id { get; }
    public string Name { get; }
    public string Deity { get; }
    public int? FounderId { get; }
    public int FoundedYear { get; }
    public int? ParentId { get; }              // the faith this one split from, if any
    public int? OriginEventId { get; set; }    // chronicle event that founded this faith — set at creation
    public HashSet<int> Members { get; } = new();

    public Religion(int id, string name, string deity, int? founderId = null,
                    int foundedYear = 0, int? parentId = null)
    {
        Id = id;
        Name = name;
        Deity = deity;
        FounderId = founderId;
        FoundedYear = foundedYear;
        ParentId = parentId;
    }
}

public sealed class Faction
{
    public string Id { get; }
    public string Name { get; }                // e.g. "the Highland Clans"
    public string Culture { get; }             // used for name flavor, customs later
    public string Homeland { get; }
    public int? LeaderId { get; set; }
    public HashSet<int> Members { get; } = new();   // living member ids
    public HashSet<string> ControlledRegions { get; } = new();   // region ids (as strings) this people holds
    public int? LastDeathEventId { get; set; }      // most recent death/murder of a member — cause for abandonment
    public int FoundedYear { get; set; }

    // economy (M4 → Harvest Economy V1): Prosperity/InFamine/InBoom/FamineEvent are now DERIVED
    // each tick from this people's controlled regions (Economy(): Prosperity = mean harvest;
    // InFamine = its worst controlled region starves; FamineEvent = that region's onset event;
    // InBoom = any controlled region feasts). Landless peoples hold neutral 1.0 and never famine.
    // Births/deaths/culture still read these fields unchanged — only their source moved to the land.
    public double Prosperity { get; set; } = 1.0;   // 0.0 starving … 1.0 neutral … 2.0 thriving (mean of controlled regions)
    public bool InFamine { get; set; }              // worst controlled region in famine — death pressure
    public bool InBoom { get; set; }                // any controlled region in boom
    public Event? FamineEvent { get; set; }         // worst region's famine onset event, for death cause-chains

    // culture (M7): per-faction value axes (valor/piety/cunning/harmony) drift over time and
    // harden into named customs at threshold; customs drive clash (tension) and diffusion (peace).
    public Dictionary<string, double> Values { get; } = new();          // axis -> 0..1, seeded from culture baseline
    public Dictionary<string, int> CustomOriginEvent { get; } = new();  // held custom -> event that birthed it (cause-link + Vanished Way span)

    // god-hand V1: protection/doom windows. Self-expiring by year comparison (UntilYear > Year),
    // so the tick needs no expiry scan; the event ids let famines under pressure cause-link
    // honestly back to the recorded divine act. Both default inert (0 / null).
    public int ProtectUntilYear { get; set; }
    public int? ProtectEventId { get; set; }
    public int DoomUntilYear { get; set; }
    public int? DoomEventId { get; set; }

    public Faction(string id, string name, string culture, string homeland)
    {
        Id = id;
        Name = name;
        Culture = culture;
        Homeland = homeland;
    }
}

/// <summary>
/// The fate ledger's unit: one act of the god's hand, as explicit state. Every pressure
/// names its kind, target, start year, and the chronicle event that recorded the act —
/// so the viewer's ledger, the catch-up chains, and the `divine` gate all read the same
/// truth. Mechanics stay subtle multipliers on existing rolls; a pressure never adds RNG
/// draws, so a world with no pressures is byte-identical to one where the type doesn't
/// exist (the verify baseline cannot move).
/// </summary>
public enum DivinePressureKind { Bless, Curse, Protect, Doom, Omen, ForestSeeded, SpringCalled }

public sealed class DivinePressure
{
    public int Id { get; }
    public DivinePressureKind Kind { get; }
    public string TargetType { get; }    // "person" | "faction" | "region"
    public string TargetId { get; }      // person/region id as string, faction id verbatim
    public int StartYear { get; }
    public int SourceEventId { get; }    // the recorded divine act — the cause-link root
    public int? ExpiresYear { get; }     // null = unbound (a curse on a bloodline, a terrain act)

    public DivinePressure(int id, DivinePressureKind kind, string targetType, string targetId,
                          int startYear, int sourceEventId, int? expiresYear)
    {
        Id = id;
        Kind = kind;
        TargetType = targetType;
        TargetId = targetId;
        StartYear = startYear;
        SourceEventId = sourceEventId;
        ExpiresYear = expiresYear;
    }

    /// <summary>Whether the pressure still presses. Terrain acts are instants — done, not
    /// active; a curse runs with the bloodline; a blessing with the blessed life.</summary>
    public bool IsActive(World world) => Kind switch
    {
        DivinePressureKind.Bless => int.TryParse(TargetId, out int pid)
            && world.People.TryGetValue(pid, out var p) && p.Alive,
        DivinePressureKind.Curse => true,
        DivinePressureKind.Protect or DivinePressureKind.Doom or DivinePressureKind.Omen
            => ExpiresYear is int ey && world.Year < ey,
        _ => false,
    };
}

/// <summary>
/// A named patch of the island. The sim's spatial foundation: terrain shapes who settles it,
/// control changes hands in war, and X,Y (normalized 0–1 within the island bounds) give the
/// viewer a place to draw it. Adjacency is the connectivity graph wars and (later) culture
/// spread will travel along. Generated deterministically from the world seed.
/// </summary>
public sealed class Region
{
    public int Id { get; }
    public string Name { get; }
    public string TerrainType { get; }                 // "forest" | "highland" | "coast" | "plains"
    public string? ControllingFactionId { get; set; }  // null = unclaimed wilderness
    public List<int> AdjacentRegionIds { get; } = new();
    public float X { get; }
    public float Y { get; }

    // Harvest Economy V1: the per-region harvest random-walk is the economy's ground truth
    // (faction Prosperity derives from the mean of its controlled regions). Every region
    // carries Harvest, but only a held region (ControllingFactionId != null) emits
    // famine/plenty/famine_end events — anchored to RegionId, never SiteId.
    public double Harvest { get; set; } = 1.0;       // 0.0 starving … 1.0 neutral … 2.0 thriving
    public bool InFamine { get; set; }               // below famine_threshold — event dedup
    public Event? FamineEvent { get; set; }          // current famine's onset event, for death cause-chains
    public bool InBoom { get; set; }                 // above boom_threshold — event dedup
    public int LastBoomYear { get; set; }            // last "plenty continues" beat (sustained booms re-emit)

    public Region(int id, string name, string terrainType, float x, float y)
    {
        Id = id;
        Name = name;
        TerrainType = terrainType;
        X = x;
        Y = y;
    }
}
