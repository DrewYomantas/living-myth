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
    public int FoundedYear { get; set; }

    public Faction(string id, string name, string culture, string homeland)
    {
        Id = id;
        Name = name;
        Culture = culture;
        Homeland = homeland;
    }
}
