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

    // economy (M4): per-faction prosperity drives famine/boom/trade and modulates births/deaths
    public double Prosperity { get; set; } = 1.0;   // 0.0 starving … 1.0 neutral … 2.0 thriving
    public bool InFamine { get; set; }              // below famine_threshold — death pressure + event dedup
    public bool InBoom { get; set; }                // above boom_threshold — event dedup
    public int LastBoomYear { get; set; }           // last "plenty continues" beat, so sustained booms re-emit
    public Event? FamineEvent { get; set; }         // current famine's onset event, for death cause-chains

    // culture (M7): per-faction value axes (valor/piety/cunning/harmony) drift over time and
    // harden into named customs at threshold; customs drive clash (tension) and diffusion (peace).
    public Dictionary<string, double> Values { get; } = new();          // axis -> 0..1, seeded from culture baseline
    public Dictionary<string, int> CustomOriginEvent { get; } = new();  // held custom -> event that birthed it (cause-link + Vanished Way span)

    public Faction(string id, string name, string culture, string homeland)
    {
        Id = id;
        Name = name;
        Culture = culture;
        Homeland = homeland;
    }
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

    public Region(int id, string name, string terrainType, float x, float y)
    {
        Id = id;
        Name = name;
        TerrainType = terrainType;
        X = x;
        Y = y;
    }
}
