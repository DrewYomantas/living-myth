namespace LivingMyth.Sim;

/// <summary>
/// The simulation. Events cause other events: murder (ambition and revenge), forbidden
/// cross-faction romance with real fallout, war that breaks out when tension runs high,
/// and a full religion engine. Every event stores what caused it, so the chronicle is a
/// web of linked threads, not just a list.
///
/// Determinism note (the port rule): C# dictionaries and hash sets are not order-stable
/// like Python's insertion-ordered dicts, so every iteration that can affect RNG draws or
/// results is given an explicit order — people/religions by ascending id, factions in
/// config order, and member sets sorted before use. Same seed -> identical history.
/// </summary>
public sealed class World
{
    public int Seed { get; }
    public Rng Rng { get; }
    public ConfigData Config { get; }
    public Dictionary<string, double> Params { get; }
    public NamesData Names { get; }

    public int Year { get; private set; }
    public Dictionary<int, Person> People { get; } = new();
    private readonly List<Person> _peopleOrder = new();        // insertion order == id order
    public Dictionary<string, Faction> Factions { get; } = new();
    private readonly List<string> _factionOrder = new();        // config order
    public Chronicle Chronicle { get; } = new();
    private int _nextPid;

    public string Island { get; }
    public Dictionary<(string, string), double> Tension { get; } = new();
    public Dictionary<(string, string), List<int>> Grievances { get; } = new();
    private readonly List<War> _activeWars = new();
    private readonly HashSet<int> _unavengedVictimIds = new();   // bounds the revenge scan (distinct victims)
    public Event? CurseEvent { get; private set; }

    public Dictionary<int, Religion> Religions { get; } = new();
    private readonly List<Religion> _religionOrder = new();
    private int _nextRid;

    /// <summary>Cursor into the chronicle: Gossip() only looks at events recorded since the last
    /// year, never the whole history — keeps the gossip pass O(this year's events).</summary>
    private int _lastGossipEventCount;
    private static readonly Dictionary<int, int> NoConsequences = new();   // fresh events have none yet
    private static readonly HashSet<string> GossipTypes = new()
    { "murder", "scandal", "romance", "prophet", "martyr", "trade", "boom", "custom" };

    /// <summary>The island's regions, in id order (id == list index). Generated once in
    /// SeedWorld; control changes hands through war.</summary>
    public List<Region> Regions { get; } = new();
    public string? RegionName(int id) => id >= 0 && id < Regions.Count ? Regions[id].Name : null;

    /// <summary>The island's editable skin (WorldSurface.cs): generated lazily from the seed
    /// and region layout via pure coordinate hashes — no Rng draws, never read by the tick,
    /// so touching it can never move the verify baseline. The viewer renders it; the
    /// god-hand terrain verbs edit it through recorded events.</summary>
    private WorldSurface? _surface;
    public WorldSurface Surface => _surface ??= BuildSurface();

    /// <summary>Sites V1 (Sites.cs): the local place layer — a read-model, never read by
    /// the tick. Built in the same breath as the surface, so it always derives from the
    /// PRISTINE terrain: no terraform edit can exist before the index does (edits go
    /// through Surface, which constructs both first). Zero Rng draws — baseline-inert.</summary>
    private SiteIndex? _sites;
    public SiteIndex Sites
    {
        get { _ = Surface; return _sites!; }
    }

    private WorldSurface BuildSurface()
    {
        var surface = new WorldSurface(Seed, Regions);
        _sites = new SiteIndex(Seed, Regions, surface, Names);
        return surface;
    }

    private sealed class War
    {
        public (string, string) Pair;
        public int YearsLeft;
        public Event DeclaredEvent = null!;
        public int BattlesFought;    // running tally: ordinal battle naming + the peace toll
        public int Fallen;           // souls killed across all this war's battles
        // Note: the front is recomputed per battle (RecordBattle) so it follows the map as
        // control shifts — there's deliberately no stored front to drift out of date.
    }

    public World(int seed, ConfigData config, NamesData names)
    {
        Seed = seed;
        Rng = new Rng(seed);
        Config = config;
        Params = config.Params;
        Names = names;
        Year = config.StartYear;
        Island = Rng.Pick(names.IslandNames);
    }

    // ---------- mortality curves ----------

    public static double DeathChance(int age)
    {
        if (age < 3) return 0.04;
        if (age < 40) return 0.006;
        if (age < 55) return 0.02;
        if (age < 70) return 0.06;
        if (age < 85) return 0.15;
        return 0.35;
    }

    private static string DeathReason(int age)
    {
        if (age < 3) return "in infancy";
        if (age < 40) return "of a fever";
        if (age < 60) return "of illness";
        return "of old age";
    }

    private static (string, string) PairKey(string a, string b)
        => string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);

    // ---------- helpers ----------

    private int NewPid() => _nextPid++;

    public Person CreatePerson(string factionId, int age, string? sex = null)
    {
        sex ??= Rng.Pick(new[] { "m", "f" });
        string culture = Factions[factionId].Culture;
        string name = Disambiguate(Rng.Pick(Names.GivenNames[culture][sex]), factionId);
        var p = new Person(NewPid(), name, factionId, birthYear: Year - age, sex: sex);
        People[p.Id] = p;
        _peopleOrder.Add(p);
        Factions[factionId].Members.Add(p.Id);
        return p;
    }

    /// <summary>Keep names unique among the living of one people: a clash becomes "[Name] the
    /// younger", then "[Name] of [homeland]". Runtime-only — the names pool is untouched, and the
    /// check is a boolean over the (order-independent) living member set, so it draws no RNG.</summary>
    private string Disambiguate(string name, string factionId)
    {
        var fac = Factions[factionId];
        bool Taken(string n) => fac.Members.Any(id => People[id].Name == n);
        if (!Taken(name)) return name;
        string younger = $"{name} the younger";
        if (!Taken(younger)) return younger;
        return $"{name} of {fac.Homeland}";
    }

    /// <summary>Living people in ascending-id order. Built from the per-faction living-member
    /// sets, so it costs O(living) — not O(everyone who ever lived). Identical ordering to a
    /// filter over all people, which keeps the simulation deterministic.</summary>
    public List<Person> Living()
    {
        var ids = new List<int>();
        foreach (var f in Factions.Values) ids.AddRange(f.Members);
        ids.Sort();
        var outp = new List<Person>(ids.Count);
        foreach (var id in ids) outp.Add(People[id]);
        return outp;
    }

    /// <summary>O(factions) living headcount — cheap to call every frame.</summary>
    public int LivingCount
    {
        get
        {
            int n = 0;
            foreach (var f in Factions.Values) n += f.Members.Count;
            return n;
        }
    }

    /// <summary>Living members, sorted by id so random picks stay deterministic.</summary>
    public List<Person> FactionMembers(string fid)
        => Factions[fid].Members.OrderBy(id => id).Select(id => People[id]).ToList();

    private IEnumerable<string> FactionsSorted() => _factionOrder.OrderBy(f => f, StringComparer.Ordinal);

    private bool CloselyRelated(Person a, Person b)
    {
        if (a.Parents.Intersect(b.Parents).Any()) return true;
        if (b.Parents.Contains(a.Id) || a.Parents.Contains(b.Id)) return true;
        return false;
    }

    /// <summary>Spouse, parents, children, siblings (living).</summary>
    private List<Person> KinOf(Person p)
    {
        var ids = new HashSet<int>(p.Parents);
        ids.UnionWith(p.Children);
        if (p.SpouseId is int sp) ids.Add(sp);
        foreach (var parentId in p.Parents)
            if (People.TryGetValue(parentId, out var parent))
                ids.UnionWith(parent.Children);
        ids.Remove(p.Id);
        return ids.OrderBy(i => i)
                  .Where(i => People.TryGetValue(i, out var q) && q.Alive)
                  .Select(i => People[i]).ToList();
    }

    private void AddTension(string fa, string fb, double amount, Event ev)
    {
        if (fa == fb) return;
        var key = PairKey(fa, fb);
        Tension[key] = Tension.GetValueOrDefault(key) + amount;
        if (!Grievances.TryGetValue(key, out var list)) { list = new(); Grievances[key] = list; }
        list.Add(ev.Id);
        if (list.Count > 4) list.RemoveRange(0, list.Count - 4);
    }

    private static Person Oldest(IEnumerable<Person> bySortedId, int year)
        => bySortedId.Aggregate((best, cur) => cur.Age(year) > best.Age(year) ? cur : best);

    /// <summary>Event.SiteId at record time: the one authored convention table
    /// (SiteAnchors.Expected, Sites.cs — the `sites` gate recomputes and asserts it).
    /// Zero Rng, so anchoring can never move the verify counts.</summary>
    private int? AnchorSite(string etype, List<string> tags, int? regionId)
        => SiteAnchors.Expected(this, etype, tags, regionId);

    // ---------- world setup ----------

    public void SeedWorld()
    {
        foreach (var f in Config.Factions)
        {
            Factions[f.Id] = new Faction(f.Id, f.Name, f.Culture, f.Homeland);
            _factionOrder.Add(f.Id);
        }
        var founding = Chronicle.Record(Year, "founding",
            $"The world begins. Three peoples share the island of {Island}.",
            tags: new() { "founding" });

        foreach (var f in Config.Factions)
        {
            var fac = Factions[f.Id];
            for (int i = 0; i < f.StartPop; i++)
                CreatePerson(f.Id, age: Rng.RandInt(1, 60));
            var leader = Oldest(FactionMembers(f.Id), Year);
            fac.LeaderId = leader.Id;
            leader.IsLeader = true;
            leader.EverLeader = true;
            Chronicle.Record(Year, "leadership",
                $"{leader.Name} of {fac.Name}, eldest of their people, leads them from {fac.Homeland}.",
                participants: new() { leader.Id }, tags: new() { "leadership" });
        }
        SeedCulture();
        GenerateMap();
        SeedReligions(founding.Id);
    }

    /// <summary>Copy each people's culture baseline into its live value vector. No RNG — a
    /// deterministic init, so its placement among seeding steps can't shift the verify counts.</summary>
    private void SeedCulture()
    {
        foreach (var fid in _factionOrder)
        {
            var f = Factions[fid];
            var bsl = CultureValueBaseline.GetValueOrDefault(f.Culture);
            foreach (var axis in ValueAxes)
                f.Values[axis] = bsl?.GetValueOrDefault(axis, 0.5) ?? 0.5;
        }
    }

    // ---------- the island map ----------

    private static readonly Dictionary<string, string> CultureTerrain = new()
    {
        ["highland"] = "highland",   // the Highland Clans take the high crags
        ["shore"] = "coast",         // the Shorefolk take the coast
        ["wood"] = "forest",         // the Wood Tribes take the forest
    };

    // ---------- culture catalog (M7) ----------

    private static readonly string[] ValueAxes = { "valor", "piety", "cunning", "harmony" };

    /// <summary>Where each people's values sit by default; drift mean-reverts toward this.</summary>
    private static readonly Dictionary<string, Dictionary<string, double>> CultureValueBaseline = new()
    {
        ["highland"] = new() { ["valor"] = 0.62, ["piety"] = 0.50, ["cunning"] = 0.38, ["harmony"] = 0.40 },
        ["shore"] = new() { ["valor"] = 0.40, ["piety"] = 0.42, ["cunning"] = 0.58, ["harmony"] = 0.60 },
        ["wood"] = new() { ["valor"] = 0.45, ["piety"] = 0.60, ["cunning"] = 0.40, ["harmony"] = 0.55 },
    };

    private static readonly Dictionary<string, string> AxisCustom = new()
    { ["valor"] = "warlike", ["piety"] = "devout", ["cunning"] = "scheming", ["harmony"] = "peaceable" };

    private static readonly Dictionary<string, string> CustomAxis = new()
    { ["warlike"] = "valor", ["devout"] = "piety", ["scheming"] = "cunning", ["peaceable"] = "harmony" };

    /// <summary>Opposing axes — a people clashes culturally when it holds the custom of one and a
    /// neighbour holds the custom of the other. Only the canonical halves (valor, piety) are
    /// iterated so each pair is considered once.</summary>
    private static readonly Dictionary<string, string> AxisOpposite = new()
    { ["valor"] = "harmony", ["piety"] = "cunning" };

    private static readonly Dictionary<string, string> CustomBecome = new()
    {
        ["warlike"] = "a warlike people, quick to the spear",
        ["devout"] = "a devout people, bound to their gods",
        ["scheming"] = "a scheming people, schooled in intrigue",
        ["peaceable"] = "a peaceable people, slow to anger",
    };

    private static readonly Dictionary<string, string> CustomFade = new()
    {
        ["warlike"] = "lay down the warlike ways of their forebears",
        ["devout"] = "let the old devotions lapse",
        ["scheming"] = "turn from the politics of the knife",
        ["peaceable"] = "lose their gentle temper",
    };

    /// <summary>A deterministic float in [0,1) drawn from Rng (Rng exposes only int/bool draws).</summary>
    private float Frac01() => Rng.RandInt(0, 99999) / 100000f;

    private static double Dist2(Region a, Region b)
    {
        double dx = a.X - b.X, dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }

    private static string Roman(int n) => n switch
    {
        2 => "II", 3 => "III", 4 => "IV", 5 => "V", 6 => "VI", 7 => "VII", 8 => "VIII", _ => n.ToString()
    };

    /// <summary>Procedurally lay out the island: 20–28 terrain-typed regions placed in rough
    /// bands (highland inland, coast on the rim), wired into a nearest-neighbour adjacency graph,
    /// then handed to the peoples whose culture matches the terrain. Every draw is from Rng, so
    /// the same seed always yields the same map.</summary>
    private void GenerateMap()
    {
        int count = Rng.RandInt(20, 28);

        // Target mix ~35/25/25/15 (forest/highland/coast/plains), jittered a little per seed.
        double wForest = 0.35 + (Frac01() - 0.5) * 0.10;
        double wHigh = 0.25 + (Frac01() - 0.5) * 0.08;
        double wCoast = 0.25 + (Frac01() - 0.5) * 0.08;
        double wPlains = 0.15 + (Frac01() - 0.5) * 0.06;
        double wSum = wForest + wHigh + wCoast + wPlains;

        // Per-terrain name bags: shuffled, drawn without replacement; if a terrain outgrows its
        // pool the bag refills and later names get a numeral suffix so every region stays unique.
        var bag = new Dictionary<string, List<string>>();
        var cycle = new Dictionary<string, int>();
        foreach (var t in new[] { "forest", "highland", "coast", "plains" })
        {
            var pool = new List<string>(Names.RegionNames.GetValueOrDefault(t) ?? new List<string> { t });
            Rng.Shuffle(pool);
            bag[t] = pool;
            cycle[t] = 0;
        }
        string NextName(string terrain)
        {
            var q = bag[terrain];
            if (q.Count == 0)
            {
                var pool = new List<string>(Names.RegionNames.GetValueOrDefault(terrain) ?? new List<string> { terrain });
                Rng.Shuffle(pool);
                q.AddRange(pool);
                cycle[terrain]++;
            }
            string name = q[0];
            q.RemoveAt(0);
            return cycle[terrain] > 0 ? $"{name} {Roman(cycle[terrain] + 1)}" : name;
        }

        for (int i = 0; i < count; i++)
        {
            double roll = Frac01() * wSum;
            string terrain = roll < wForest ? "forest"
                : roll < wForest + wHigh ? "highland"
                : roll < wForest + wHigh + wCoast ? "coast" : "plains";

            // Place by terrain band within a disk so the island reads as a place, not noise.
            (float lo, float hi) = terrain switch
            {
                "highland" => (0.00f, 0.42f),
                "coast" => (0.62f, 1.00f),
                "plains" => (0.20f, 0.72f),
                _ => (0.18f, 0.78f),       // forest
            };
            const float maxR = 0.40f;
            float rr = (lo + (hi - lo) * Frac01()) * maxR;
            float ang = Frac01() * MathF.Tau;
            float x = 0.5f + MathF.Cos(ang) * rr;
            float y = 0.5f + MathF.Sin(ang) * rr;
            Regions.Add(new Region(Regions.Count, NextName(terrain), terrain, x, y));
        }

        // Adjacency: each region links to its 3 nearest neighbours, made symmetric.
        foreach (var a in Regions)
            foreach (var b in Regions.Where(r => r.Id != a.Id)
                                     .OrderBy(r => Dist2(a, r)).ThenBy(r => r.Id).Take(3))
            {
                if (!a.AdjacentRegionIds.Contains(b.Id)) a.AdjacentRegionIds.Add(b.Id);
                if (!b.AdjacentRegionIds.Contains(a.Id)) b.AdjacentRegionIds.Add(a.Id);
            }
        foreach (var r in Regions) r.AdjacentRegionIds.Sort();

        // Hand each region to the people whose culture matches its terrain; plains stay wilderness.
        foreach (var region in Regions)
        {
            var owner = Config.Factions.FirstOrDefault(f =>
                CultureTerrain.GetValueOrDefault(f.Culture) == region.TerrainType);
            if (owner is not null) Claim(region, Factions[owner.Id]);
        }

        // Floor: no founding people starts landless — grant a wilderness region if shut out.
        foreach (var f in Config.Factions)
        {
            if (!CultureTerrain.ContainsKey(f.Culture) || Factions[f.Id].ControlledRegions.Count > 0) continue;
            var free = Regions.FirstOrDefault(r => r.ControllingFactionId is null);
            if (free is not null) Claim(free, Factions[f.Id]);
        }

        // Record each people's founding territory into the chronicle.
        foreach (var f in Config.Factions)
        {
            var fac = Factions[f.Id];
            if (fac.ControlledRegions.Count == 0) continue;
            var owned = fac.ControlledRegions.Select(s => Regions[int.Parse(s)]).OrderBy(r => r.Id).ToList();
            var foundingTags = new List<string> { "territory", "founding" };
            Chronicle.Record(Year, "territory",
                $"{fac.Name} hold the lands of {string.Join(", ", owned.Select(r => r.Name))}.",
                participants: fac.LeaderId is int lid ? new() { lid } : null,
                tags: foundingTags, regionId: owned[0].Id,
                siteId: AnchorSite("territory", foundingTags, owned[0].Id));

            // Root the founding generation at the same seat the event above anchors. Draws no
            // RNG and records nothing, so it cannot move the verify baseline. Landless peoples
            // (loop skipped above) honestly stay null.
            foreach (int pid in fac.Members.OrderBy(id => id))
                People[pid].HomeRegionId = owned[0].Id;
        }
    }

    private static void Claim(Region region, Faction fac)
    {
        region.ControllingFactionId = fac.Id;
        fac.ControlledRegions.Add(region.Id.ToString());
    }

    // ---------- the god's hand (divine pressure V1) ----------

    /// <summary>The fate ledger: every act of the god's hand, in order. Append-only,
    /// deterministic, never read by the tick except through the explicit per-target
    /// fields the acts set (Person.Blessed/Cursed, Faction protect/doom windows).</summary>
    public List<DivinePressure> DivinePressures { get; } = new();
    private int _nextPressureId;

    private DivinePressure AddPressure(DivinePressureKind kind, string targetType, string targetId,
                                       Event source, int? expiresYear)
    {
        var pr = new DivinePressure(_nextPressureId++, kind, targetType, targetId, Year, source.Id, expiresYear);
        DivinePressures.Add(pr);
        return pr;
    }

    private int DurationParam(string key, int def) => (int)Params.GetValueOrDefault(key, def);

    private Region ValidRegion(int regionId)
        => regionId >= 0 && regionId < Regions.Count ? Regions[regionId]
        : throw new ArgumentException($"no such region: {regionId}");

    private Faction ValidLivingFaction(string fid)
        => Factions.TryGetValue(fid, out var fac) && fac.Members.Count > 0 ? fac
        : throw new ArgumentException($"no living faction: {fid}");

    /// <summary>Lay a curse on one person and their bloodline. Plants a flag and records the
    /// act. It deliberately consumes no randomness, so a cursed run stays perfectly in step
    /// with a clean run until the curse actually changes an outcome. That's the butterfly.</summary>
    public Event PlantCurse(Person person)
    {
        if (person.Cursed) throw new ArgumentException($"{person.Name} is already cursed");
        person.Cursed = true;
        var ev = Chronicle.Record(Year, "divine",
            $"A curse is laid upon {person.Name} of {Factions[person.FactionId].Name} and all their blood.",
            participants: new() { person.Id }, tags: new() { "divine", "curse" });
        CurseEvent = ev;
        AddPressure(DivinePressureKind.Curse, "person", person.Id.ToString(), ev, null);
        return ev;
    }

    /// <summary>Bless one life: fate leans gently toward them (bless_death_multiplier on the
    /// existing death roll — the same draw, a kinder threshold; never a guarantee). Their
    /// eventual natural death cause-links back to this act, honestly.</summary>
    public Event BlessPerson(Person person)
    {
        if (!person.Alive) throw new ArgumentException($"{person.Name} is dead — the dead are past blessing");
        if (person.Blessed) throw new ArgumentException($"{person.Name} is already blessed");
        person.Blessed = true;
        var ev = Chronicle.Record(Year, "divine",
            $"A blessing is laid upon {person.Name} of {Factions[person.FactionId].Name}; fate leans kindly toward them.",
            participants: new() { person.Id }, tags: new() { "divine", "blessing" });
        person.BlessEvent = ev;
        AddPressure(DivinePressureKind.Bless, "person", person.Id.ToString(), ev, null);
        return ev;
    }

    /// <summary>Shield a people for a season of years: famine weighs lighter on them and
    /// their fortunes mend a little faster. Modest, windowed, self-expiring.</summary>
    public Event ProtectFaction(string factionId)
    {
        var fac = ValidLivingFaction(factionId);
        if (fac.ProtectUntilYear > Year) throw new ArgumentException($"{fac.Name} already stand under protection");
        int until = Year + DurationParam("protect_duration_years", 50);
        var ev = Chronicle.Record(Year, "divine",
            $"A divine protection settles over {fac.Name}; for a time, hardship will weigh lighter on them.",
            participants: fac.LeaderId is int lid ? new() { lid } : null,
            tags: new() { "divine", "protect" });
        fac.ProtectUntilYear = until;
        fac.ProtectEventId = ev.Id;
        AddPressure(DivinePressureKind.Protect, "faction", factionId, ev, until);
        return ev;
    }

    /// <summary>Pronounce a doom over a people for a season of years: their fortunes run
    /// thin and famine bites deeper. Modest, windowed, self-expiring.</summary>
    public Event DoomFaction(string factionId)
    {
        var fac = ValidLivingFaction(factionId);
        if (fac.DoomUntilYear > Year) throw new ArgumentException($"{fac.Name} already labor under a doom");
        int until = Year + DurationParam("doom_duration_years", 50);
        var ev = Chronicle.Record(Year, "divine",
            $"A doom is pronounced upon {fac.Name}; for a time, their fortunes will run thin.",
            participants: fac.LeaderId is int lid ? new() { lid } : null,
            tags: new() { "divine", "doom" });
        fac.DoomUntilYear = until;
        fac.DoomEventId = ev.Id;
        AddPressure(DivinePressureKind.Doom, "faction", factionId, ev, until);
        return ev;
    }

    /// <summary>Seed an omen over a land. Honest scope: attention, not mechanics — the viewer
    /// surfaces this land's tales while the omen hangs; no roll anywhere changes. The act is
    /// truly anchored (the omen IS at this place), so RegionId is honest.</summary>
    public Event SeedOmen(int regionId)
    {
        var region = ValidRegion(regionId);
        var ev = Chronicle.Record(Year, "divine",
            $"A strange omen hangs over {region.Name}; the eye of fate turns there.",
            tags: new() { "divine", "omen" }, regionId: regionId);
        AddPressure(DivinePressureKind.Omen, "region", regionId.ToString(), ev,
            Year + DurationParam("omen_duration_years", 40));
        return ev;
    }

    /// <summary>Terraform: raise a forest around a region's seat. The surface edit is the
    /// real state change; the recorded event is the honest witness (truly anchored — the
    /// forest IS at this place). Null when the land had no room to change.</summary>
    public Event? SeedForest(int regionId)
    {
        var region = ValidRegion(regionId);
        if (Surface.SeedForestAt(regionId, region.X, region.Y) == 0) return null;
        var ev = Chronicle.Record(Year, "divine",
            $"A forest rises across {region.Name} where no seed was sown.",
            tags: new() { "divine", "terrain", "forest" }, regionId: regionId);
        AddPressure(DivinePressureKind.ForestSeeded, "region", regionId.ToString(), ev, null);
        return ev;
    }

    /// <summary>Terraform: call a spring from the earth near a region's seat — a small lake
    /// ringed by wetland. Null when no open ground would take it.</summary>
    public Event? CallSpring(int regionId)
    {
        var region = ValidRegion(regionId);
        if (Surface.CallSpringAt(regionId, region.X, region.Y) == 0) return null;
        var ev = Chronicle.Record(Year, "divine",
            $"A spring breaks from the earth of {region.Name}, and water gathers where none ran before.",
            tags: new() { "divine", "terrain", "water" }, regionId: regionId);
        AddPressure(DivinePressureKind.SpringCalled, "region", regionId.ToString(), ev, null);
        return ev;
    }

    // ---------- religion ----------

    private int NewRid() => _nextRid++;

    private Religion NewReligion(string name, string deity, Person? founder = null, Religion? parent = null)
    {
        var rel = new Religion(NewRid(), name, deity,
            founderId: founder?.Id, foundedYear: Year, parentId: parent?.Id);
        Religions[rel.Id] = rel;
        _religionOrder.Add(rel);
        return rel;
    }

    private void SetReligion(Person person, Religion religion)
    {
        if (person.ReligionId is int rid && Religions.TryGetValue(rid, out var old))
            old.Members.Remove(person.Id);
        person.ReligionId = religion.Id;
        religion.Members.Add(person.Id);
    }

    public Religion? DominantReligion(string fid)
    {
        var counts = new Dictionary<int, int>();
        foreach (var p in FactionMembers(fid))
            if (p.ReligionId is int rid)
                counts[rid] = counts.GetValueOrDefault(rid) + 1;
        if (counts.Count == 0) return null;
        int best = counts.Keys.OrderBy(k => k).OrderByDescending(k => counts[k]).First();
        return Religions.GetValueOrDefault(best);
    }

    private string FaithName()
    {
        var frags = Names.FaithFragments;
        return $"{Rng.Pick(frags["prefix"])} {Rng.Pick(frags["concept"])}";
    }

    private void SeedReligions(int foundingEventId)
    {
        foreach (var f in Config.Factions)
        {
            var data = Names.Religions[f.Culture];
            var rel = NewReligion(data.Name, data.Deity);
            rel.OriginEventId = foundingEventId;   // primordial faiths trace back to the world's founding
            foreach (var p in FactionMembers(f.Id))
                SetReligion(p, rel);
        }
    }

    private void DoReligion()
    {
        Prophets();
        Schisms();
        Conversions();
        Persecution();
        ReligiousFriction();
    }

    private void Prophets()
    {
        foreach (var fid in FactionsSorted())
        {
            if (!Rng.Chance(Params["prophet_chance_per_year"])) continue;
            var adults = FactionMembers(fid).Where(p => p.Age(Year) >= 20 && !p.IsProphet).ToList();
            if (adults.Count < 4) continue;
            var prophet = Rng.Pick(adults.OrderBy(p => p.Id).ToList());
            var rel = NewReligion(FaithName(), "a new revelation", founder: prophet);
            prophet.IsProphet = true;
            SetReligion(prophet, rel);
            var followers = adults.Where(p => p.Id != prophet.Id).ToList();
            Rng.Shuffle(followers);
            int taken = Rng.RandInt(1, 4);
            if (prophet.Reputation > 0) taken += 1;   // a respected voice wins one more early follower
            foreach (var p in followers.Take(taken))
                SetReligion(p, rel);
            var prophetEv = Chronicle.Record(Year, "prophet",
                $"{prophet.Name} of {Factions[fid].Name} proclaims a new faith, {rel.Name}, and is hailed as its first prophet.",
                participants: new() { prophet.Id }, tags: new() { "religion", "prophet" });
            rel.OriginEventId = prophetEv.Id;
        }
    }

    private void Schisms()
    {
        foreach (var rel in _religionOrder.ToList())
        {
            var members = rel.Members.OrderBy(id => id).Select(id => People[id]).Where(p => p.Alive).ToList();
            if (members.Count < (int)Params["schism_min_members"] || !Rng.Chance(Params["schism_chance_per_year"])) continue;
            var heretic = NewReligion(FaithName(), rel.Deity, parent: rel);
            Rng.Shuffle(members);
            var breakaway = members.Take(Math.Max(2, members.Count / 3)).ToList();
            foreach (var p in breakaway) SetReligion(p, heretic);
            var schismEv = Chronicle.Record(Year, "schism",
                $"{rel.Name} is torn by schism: {breakaway.Count} break away to found {heretic.Name} over matters of doctrine.",
                tags: new() { "religion", "schism", "heresy" });
            heretic.OriginEventId = schismEv.Id;
        }
    }

    private void Conversions()
    {
        foreach (var fid in FactionsSorted())
        {
            if (!Rng.Chance(Params["conversion_chance_per_year"])) continue;
            var dom = DominantReligion(fid);
            if (dom is null) continue;
            var minority = FactionMembers(fid).Where(p => p.ReligionId != dom.Id && !p.IsProphet).ToList();
            if (minority.Count > 0)
                SetReligion(Rng.Pick(minority.OrderBy(p => p.Id).ToList()), dom);
        }
    }

    private void Persecution()
    {
        foreach (var fid in FactionsSorted())
        {
            if (!Rng.Chance(Params["persecution_chance_per_year"])) continue;
            var members = FactionMembers(fid);
            var dom = DominantReligion(fid);
            if (dom is null) continue;
            var minority = members.Where(p => p.ReligionId != dom.Id && p.Age(Year) >= 14).ToList();
            var enforcers = members.Where(p => p.ReligionId == dom.Id && p.Age(Year) >= 18).ToList();
            if (minority.Count == 0 || enforcers.Count == 0) continue;
            var victim = Rng.Pick(minority.OrderBy(p => p.Id).ToList());
            var killer = Rng.Pick(enforcers.OrderBy(p => p.Id).ToList());
            var vrel = victim.ReligionId is int vr ? Religions.GetValueOrDefault(vr) : null;
            string faith = vrel?.Name ?? "a forbidden faith";
            string text = $"{killer.Name} of {Factions[fid].Name} has {victim.Name} put to death for the heresy of {faith}.";
            // Trace the killing back to the founding of the faith it punished, so the catch-up
            // panel walks to the heresy's origin instead of "stands alone". No RNG touched.
            var causes = vrel?.OriginEventId is int oid ? new List<int> { oid } : null;
            Murder(killer, victim, text, causes, new() { "religion", "heresy", "persecution" });
        }
    }

    private void ReligiousFriction()
    {
        var fids = FactionsSorted().ToList();
        for (int i = 0; i < fids.Count; i++)
            for (int j = i + 1; j < fids.Count; j++)
            {
                string fa = fids[i], fb = fids[j];
                var da = DominantReligion(fa);
                var db = DominantReligion(fb);
                if (da is null || db is null || da.Id == db.Id) continue;
                if (!Rng.Chance(Params["religious_friction_chance_per_year"])) continue;
                var ev = Chronicle.Record(Year, "friction",
                    $"Bad blood grows between {Factions[fa].Name} and {Factions[fb].Name} over the worship of {da.Deity} and {db.Deity}.",
                    tags: new() { "religion", "friction" });
                AddTension(fa, fb, 2.0, ev);
            }
    }

    // ---------- economy (prosperity → famine / boom / trade) ----------

    // Terrain-Typed Harvest V1: the harvest walk's volatility + mean-revert TARGET depend on the
    // region's IMMUTABLE TerrainType — coast steady, plains fertile-but-swingy, highland harsh +
    // volatile, forest the unchanged baseline. Pure param lookups: ZERO new Rng draws (the single
    // RandInt(-1,1) per region per year is unchanged). Revert rate stays global.
    private (double vol, double target, double rate) TerrainHarvestParams(string terrain) =>
        terrain switch
        {
            "coast"    => (Params.GetValueOrDefault("harvest_vol_coast",    0.6),
                           Params.GetValueOrDefault("harvest_target_coast",  1.0),
                           Params["economy_prosperity_revert"]),
            "plains"   => (Params.GetValueOrDefault("harvest_vol_plains",   1.4),
                           Params.GetValueOrDefault("harvest_target_plains", 1.15),
                           Params["economy_prosperity_revert"]),
            "highland" => (Params.GetValueOrDefault("harvest_vol_highland", 1.3),
                           Params.GetValueOrDefault("harvest_target_highland", 0.78),
                           Params["economy_prosperity_revert"]),
            "forest"   => (Params.GetValueOrDefault("harvest_vol_forest",   1.0),
                           Params.GetValueOrDefault("harvest_target_forest", 1.0),
                           Params["economy_prosperity_revert"]),
            _          => throw new ArgumentException($"Unknown terrain type: {terrain}"),
        };

    private void Economy()
    {
        // Harvest Economy V1: the harvest random-walk is per REGION (the economy's ground
        // truth) — faction Prosperity derives from the mean of its controlled regions below.
        // Every region's harvest walks (list order == id order, deterministic), but only a held
        // region emits famine/plenty/famine_end events, anchored to RegionId (never SiteId — a
        // famine spans a land, it isn't at one site).
        foreach (var r in Regions)
        {
            int step = Rng.RandInt(-1, 1);
            var (vol, target, rate) = TerrainHarvestParams(r.TerrainType);
            r.Harvest += step * Params["economy_prosperity_step"] * vol;
            r.Harvest += (target - r.Harvest) * rate;

            var holder = r.ControllingFactionId is string hid ? Factions[hid] : null;
            // God-hand pressure biases the holder's lands — a flat bias on the SAME draw while
            // the window holds, never an extra draw. Inert (0-width windows) without player acts.
            if (holder is not null)
            {
                if (holder.ProtectUntilYear > Year)
                    r.Harvest += Params.GetValueOrDefault("protect_prosperity_bias", 0.02);
                if (holder.DoomUntilYear > Year)
                    r.Harvest -= Params.GetValueOrDefault("doom_prosperity_drag", 0.02);
            }
            r.Harvest = Math.Clamp(r.Harvest, 0.0, 2.0);

            // Only a held land has people to starve or feast; wilderness harvest walks silently.
            if (holder is null) { r.InFamine = false; r.InBoom = false; r.FamineEvent = null; continue; }

            if (!r.InFamine && r.Harvest < Params["famine_threshold"])
            {
                r.InFamine = true;
                // A famine arriving under an active doom or protection cause-links to the divine
                // act, honestly: the doom truly pressed it down ("therefore"); the protection
                // truly stood against it and was overcome ("but").
                var divineCauses = new List<int>();
                if (holder.DoomUntilYear > Year && holder.DoomEventId is int de) divineCauses.Add(de);
                if (holder.ProtectUntilYear > Year && holder.ProtectEventId is int pe) divineCauses.Add(pe);
                r.FamineEvent = Chronicle.Record(Year, "famine",
                    $"Famine grips {r.Name}.",
                    participants: holder.LeaderId is int fl ? new() { fl } : null,
                    causes: divineCauses.Count > 0 ? divineCauses : null,
                    tags: new() { "economy", "scarcity" },
                    regionId: r.Id);
                // A starving people leans on its neighbours: each famine onset pushes aggression
                // outward once, toward every other people that still has living members.
                foreach (var otherId in FactionsSorted())
                    if (otherId != holder.Id && Factions[otherId].Members.Count > 0)
                        AddTension(holder.Id, otherId, 1.5, r.FamineEvent);
            }
            else if (r.InFamine && r.Harvest >= Params["famine_threshold"])
            {
                r.InFamine = false;
                // Famine's-end is a real, region-anchored beat (the chapter-closing event the
                // recaps have been missing), cause-linked back to the onset it answers.
                Chronicle.Record(Year, "famine_end",
                    $"The land recovers; the famine in {r.Name} breaks.",
                    participants: holder.LeaderId is int el ? new() { el } : null,
                    causes: r.FamineEvent is Event fe ? new() { fe.Id } : null,
                    tags: new() { "economy", "recovery" },
                    regionId: r.Id);
                r.FamineEvent = null;
            }

            // A boom is one sustained high-harvest spell, so (unlike famine, which flickers near
            // its floor) it re-emits a "plenty continues" beat every boom_beat_years — that lets a
            // long golden age accumulate enough events for DetectGoldenAge to recognise it.
            if (r.Harvest > Params["boom_threshold"])
            {
                if (!r.InBoom || Year - r.LastBoomYear >= (int)Params["boom_beat_years"])
                {
                    bool onset = !r.InBoom;
                    r.InBoom = true;
                    r.LastBoomYear = Year;
                    Chronicle.Record(Year, "boom",
                        onset ? $"A season of plenty blesses {r.Name}." : $"Plenty still blesses {r.Name}.",
                        participants: holder.LeaderId is int bl ? new() { bl } : null,
                        tags: new() { "economy", "boom" },
                        regionId: r.Id);
                }
            }
            else if (r.InBoom && r.Harvest <= Params["boom_threshold"])
            {
                r.InBoom = false;
            }
        }

        // Derive each people's Prosperity (the compatibility surface births/culture/deaths read)
        // plus its famine/boom rollups from the lands it holds. No draws — pure aggregation. Runs
        // before trade so the trade guard reads this tick's fresh mean (as the M4 walk did).
        foreach (var fid in FactionsSorted())
            DeriveProsperity(Factions[fid]);

        // Trade: prospering neighbours exchange goods, which lifts both lands and eases tension
        // between them (couples to the war system). Sorted-pair loop mirrors ReligiousFriction.
        var fids = FactionsSorted().ToList();
        for (int i = 0; i < fids.Count; i++)
            for (int j = i + 1; j < fids.Count; j++)
            {
                var fa = Factions[fids[i]];
                var fb = Factions[fids[j]];
                if (fa.Prosperity <= 1.0 || fb.Prosperity <= 1.0) continue;
                if (!Rng.Chance(Params["trade_chance_per_year"])) continue;

                var participants = new List<int>();
                if (fa.LeaderId is int la) participants.Add(la);
                if (fb.LeaderId is int lb) participants.Add(lb);
                Chronicle.Record(Year, "trade",
                    $"{fa.Name} and {fb.Name} grow rich on trade between them.",
                    participants: participants.Count > 0 ? participants : null,
                    tags: new() { "economy", "trade" });

                // The wealth lifts the trading peoples' lands; re-derive the two of them at once
                // (no RNG) so end-of-tick Prosperity stays the exact controlled-region mean.
                BumpHarvest(fa, Params["economy_prosperity_step"]);
                BumpHarvest(fb, Params["economy_prosperity_step"]);
                DeriveProsperity(fa);
                DeriveProsperity(fb);
                var key = PairKey(fa.Id, fb.Id);
                Tension[key] = Math.Max(0.0, Tension.GetValueOrDefault(key) - Params["trade_tension_reduction"]);
            }
    }

    /// <summary>Roll a people's controlled-region harvests up into the compatibility surface
    /// (Prosperity = mean) and the famine/boom flags (worst region starves / any region feasts).
    /// Landless peoples hold neutral 1.0 and never famine. No RNG — region ids walked in sorted
    /// order so the worst-famine tie-break is deterministic (lowest id).</summary>
    private void DeriveProsperity(Faction f)
    {
        if (f.ControlledRegions.Count == 0)
        {
            f.Prosperity = 1.0;
            f.InFamine = false;
            f.InBoom = false;
            f.FamineEvent = null;
            return;
        }
        double sum = 0.0;
        bool anyBoom = false;
        Region? worstFamine = null;
        foreach (var rid in f.ControlledRegions.Select(int.Parse).OrderBy(x => x))
        {
            var r = Regions[rid];
            sum += r.Harvest;
            if (r.InBoom) anyBoom = true;
            if (r.InFamine && (worstFamine is null || r.Harvest < worstFamine.Harvest))
                worstFamine = r;
        }
        f.Prosperity = sum / f.ControlledRegions.Count;
        f.InFamine = worstFamine is not null;
        f.FamineEvent = worstFamine?.FamineEvent;
        f.InBoom = anyBoom;
    }

    private void BumpHarvest(Faction f, double amount)
    {
        foreach (var s in f.ControlledRegions)
        {
            var r = Regions[int.Parse(s)];
            r.Harvest = Math.Min(2.0, r.Harvest + amount);
        }
    }

    // ---------- disease (pestilence → plague / contagion) ----------

    /// <summary>Disease & Plague V1, shaped like Economy() but driven by epidemic dynamics rather
    /// than a symmetric walk: each region's Pestilence decays toward 0 (outbreaks burn out — acute,
    /// not chronic), is SPARKED by the one yearly draw (famine raises the odds — a starving land
    /// breeds sickness), and SPREADS by contagion from infected neighbours. Runs after Economy (so
    /// it can read this tick's InFamine) and before Deaths (so mortality reads InPlague). Crossing
    /// plague_threshold emits a region-anchored `plague`; falling back below emits `plague_end`.
    ///
    /// CONTAGION READS A FROZEN PREVIOUS-YEAR SNAPSHOT, never the live in-loop values: the snapshot
    /// is taken before any region updates, so the spread a region feels cannot depend on whether its
    /// neighbours were visited earlier or later this tick. That is what keeps the engine order-
    /// independent (and the determinism gate green) despite the cross-region coupling.</summary>
    private void Pestilence()
    {
        // Freeze last year's pestilence for contagion (the snapshot contract). Index == region id.
        var prev = new double[Regions.Count];
        for (int i = 0; i < Regions.Count; i++) prev[i] = Regions[i].Pestilence;

        foreach (var r in Regions)   // id order == list order, deterministic
        {
            var holder = r.ControllingFactionId is string hid ? Factions[hid] : null;

            // Exactly ONE new draw per region per year: the outbreak spark roll. Epidemics need a
            // host population — the spark probability scales with the holder people's density (a
            // founding tribe of 17 rarely sparks; a settled people of 45+ sparks at full rate), and
            // a famine raises it (a starving land breeds sickness). Wilderness has no host → chance 0.
            // Crucially the DRAW is unconditional (Chance(0) still consumes its ULong), so every
            // region draws exactly once in id order regardless of who holds it — consumption is fixed.
            double density = holder is null ? 0.0
                : Math.Min(1.0, holder.Members.Count / Params.GetValueOrDefault("plague_density_full", 45.0));
            double sparkChance = density * (r.InFamine
                ? Params["plague_spark_chance_famine"]
                : Params["plague_spark_chance"]);
            bool spark = Rng.Chance(sparkChance);

            // Contagion: each adjacent region that was infected LAST YEAR (frozen snapshot) presses
            // sickness across the border. Pure deterministic read — zero draws.
            double contagion = 0.0;
            foreach (var nid in r.AdjacentRegionIds)
                if (nid >= 0 && nid < prev.Length && prev[nid] >= Params["plague_threshold"])
                    contagion += Params["plague_contagion"];

            // Burn out first, then let this year's spark + contagion land on top (so a fresh spark
            // clears the threshold the same year, and a quiet year decays an old outbreak away).
            r.Pestilence -= r.Pestilence * Params["plague_decay"];
            if (spark) r.Pestilence += Params["plague_spark"];
            r.Pestilence += contagion;

            // God-hand pressure biases the holder's lands — a flat bias on the SAME state, never a
            // draw (inert without player acts). Protection eases the sickness; doom breeds it.
            if (holder is not null)
            {
                if (holder.ProtectUntilYear > Year)
                    r.Pestilence -= Params.GetValueOrDefault("plague_protect_bias", 0.03);
                if (holder.DoomUntilYear > Year)
                    r.Pestilence += Params.GetValueOrDefault("plague_doom_bias", 0.03);
            }
            r.Pestilence = Math.Clamp(r.Pestilence, 0.0, 2.0);

            // Only a held land has people to sicken; wilderness pestilence still walks + spreads
            // (so contagion can cross empty country) but emits no event and kills no one.
            if (holder is null) { r.InPlague = false; r.PlagueEvent = null; continue; }

            if (!r.InPlague && r.Pestilence >= Params["plague_threshold"])
            {
                r.InPlague = true;
                // Honest cause-links: an outbreak in a starving land was bred by that famine
                // ("therefore"); one under an active doom was pressed up by it; one despite an
                // active protection broke through a shield that truly lowered the pestilence ("but").
                var causes = new List<int>();
                if (r.InFamine && r.FamineEvent is Event fe) causes.Add(fe.Id);
                if (holder.DoomUntilYear > Year && holder.DoomEventId is int de) causes.Add(de);
                if (holder.ProtectUntilYear > Year && holder.ProtectEventId is int pe) causes.Add(pe);
                r.PlagueEvent = Chronicle.Record(Year, "plague",
                    $"A pestilence breaks out in {r.Name}.",
                    participants: holder.LeaderId is int pl ? new() { pl } : null,
                    causes: causes.Count > 0 ? causes : null,
                    tags: new() { "disease", "pestilence" },
                    regionId: r.Id);
            }
            else if (r.InPlague && r.Pestilence < Params["plague_threshold"])
            {
                r.InPlague = false;
                // Plague's-end is a real, region-anchored chapter-closing beat, cause-linked back
                // to the onset it answers (so a recovery is never rootless).
                Chronicle.Record(Year, "plague_end",
                    $"The pestilence in {r.Name} burns out.",
                    participants: holder.LeaderId is int el ? new() { el } : null,
                    causes: r.PlagueEvent is Event pe ? new() { pe.Id } : null,
                    tags: new() { "disease", "recovery" },
                    regionId: r.Id);
                r.PlagueEvent = null;
            }
        }

        // Roll each people's plague state up from the lands it holds (no draws — pure aggregation):
        // InPlague = its WORST-stricken controlled region is plagued; PlagueEvent = that region's
        // onset (highest Pestilence wins; sorted-id walk makes the tie-break deterministic).
        foreach (var fid in FactionsSorted())
            DerivePestilence(Factions[fid]);
    }

    private void DerivePestilence(Faction f)
    {
        f.InPlague = false;
        f.PlagueEvent = null;
        if (f.ControlledRegions.Count == 0) return;
        Region? worst = null;
        foreach (var rid in f.ControlledRegions.Select(int.Parse).OrderBy(x => x))
        {
            var r = Regions[rid];
            if (r.InPlague && (worst is null || r.Pestilence > worst.Pestilence))
                worst = r;
        }
        f.InPlague = worst is not null;
        f.PlagueEvent = worst?.PlagueEvent;
    }

    // ---------- migration (the people move — flight & settlement) ----------

    /// <summary>Migration V1: peoples move in response to the land. Each people gets exactly ONE
    /// draw per year (a fixed-cost <see cref="Rng.Chance"/> — Chance(0) still consumes its ULong,
    /// like the plague spark — so consumption stays deterministic regardless of who is eligible),
    /// deciding WHETHER it migrates this year; its condition decides WHICH kind. A people whose
    /// worst land lies in famine or plague FLEES (relocate: abandon that stricken region, settle
    /// the best adjacent wilderness); a thriving, populous people SETTLES (expand: claim adjacent
    /// wilderness, keep its holds). Flight takes priority — crisis over growth. The destination is
    /// ALWAYS adjacent UNCLAIMED wilderness (contesting held land is war, out of scope), chosen
    /// deterministically (highest harvest, lowest-id tie — zero draws). A people never abandons its
    /// last region; HomeRegionId is untouched (lineage stays immutable, as under conquest). Runs
    /// after Pestilence (so it reads this tick's region famine/plague) and before Deaths — a people
    /// that fled the famine this year is re-derived out of its death pressure (the reward for
    /// moving). Migration events anchor to the DESTINATION RegionId, never SiteId (a migration is a
    /// movement onto a land, not at one place — SiteAnchors is NOT extended).</summary>
    private void Migration()
    {
        foreach (var fid in FactionsSorted())
        {
            var fac = Factions[fid];
            // Eligibility decides the probability; the DRAW happens regardless (fixed cost).
            bool flightEligible = (fac.InFamine || fac.InPlague) && fac.ControlledRegions.Count > 1;
            bool settleEligible = fac.InBoom && fac.Members.Count >= (int)Params["migration_settle_min_pop"];
            double chance = flightEligible ? Params["migration_flight_chance"]
                          : settleEligible ? Params["migration_settle_chance"]
                          : 0.0;
            if (!Rng.Chance(chance)) continue;

            bool moved = flightEligible ? FleeStrickenLand(fac)
                       : settleEligible ? SettleNewLand(fac)
                       : false;
            // A people that just moved escapes (or enters) this tick's pressure: re-derive its
            // land-mood off the new holdings so Deaths reads the truth. Zero RNG — pure aggregation,
            // the same idempotent re-derive trade uses. Skipped when nothing moved.
            if (moved)
            {
                DeriveProsperity(fac);
                DerivePestilence(fac);
            }
        }
    }

    /// <summary>Flight: abandon the worst stricken controlled region (lowest harvest among its
    /// famine/plague lands, lowest-id tie) and settle the best adjacent wilderness. No-op (the draw
    /// still spent) when nothing is stricken or no peaceful land borders it. Returns whether it
    /// moved.</summary>
    private bool FleeStrickenLand(Faction fac)
    {
        Region? src = null;
        foreach (var rid in fac.ControlledRegions.Select(int.Parse).OrderBy(x => x))
        {
            var r = Regions[rid];
            if (!r.InFamine && !r.InPlague) continue;
            if (src is null || r.Harvest < src.Harvest) src = r;
        }
        if (src is null) return false;
        var dest = BestAdjacentWilderness(src);
        if (dest is null) return false;   // nowhere peaceful to go — they endure

        fac.ControlledRegions.Remove(src.Id.ToString());
        src.ControllingFactionId = null;
        Claim(dest, fac);
        fac.LastMigrationYear = Year;   // newcomers on a new land — a window of vulnerability to scorn

        var causes = new List<int>();
        string reason;
        if (src.InPlague && src.PlagueEvent is Event pe) { causes.Add(pe.Id); reason = "the pestilence"; }
        else if (src.InFamine && src.FamineEvent is Event fe) { causes.Add(fe.Id); reason = "the famine"; }
        else reason = src.InPlague ? "the pestilence" : "the famine";
        var tags = new List<string> { "migration", "flight" };
        Chronicle.Record(Year, "migration",
            $"{fac.Name} abandon {src.Name}, fleeing {reason}, and settle {dest.Name}.",
            participants: fac.LeaderId is int l ? new() { l } : null,
            causes: causes.Count > 0 ? causes : null,
            tags: tags, regionId: dest.Id);
        return true;
    }

    /// <summary>Settlement: a thriving people claims the best unclaimed wilderness bordering ANY of
    /// its holds (frontier spread), keeping its land. No-op when boxed in. Returns whether it
    /// moved.</summary>
    private bool SettleNewLand(Faction fac)
    {
        Region? dest = null;
        foreach (var rid in fac.ControlledRegions.Select(int.Parse).OrderBy(x => x))
        {
            var cand = BestAdjacentWilderness(Regions[rid]);
            if (cand is not null && (dest is null || cand.Harvest > dest.Harvest))
                dest = cand;
        }
        if (dest is null) return false;

        Claim(dest, fac);
        fac.LastMigrationYear = Year;   // a freshly spread people are newcomers on the frontier too
        var tags = new List<string> { "migration", "settlement" };
        Chronicle.Record(Year, "migration",
            $"{fac.Name} spread into {dest.Name}, thriving and many.",
            participants: fac.LeaderId is int l ? new() { l } : null,
            tags: tags, regionId: dest.Id);
        return true;
    }

    /// <summary>The best unclaimed wilderness adjacent to a region: highest harvest, lowest-id tie
    /// (the adjacency list is sorted ascending, so first-wins on a strict greater-than keeps the
    /// lowest id). Zero RNG — a pure deterministic read over the map.</summary>
    private Region? BestAdjacentWilderness(Region from)
    {
        Region? best = null;
        foreach (var nid in from.AdjacentRegionIds)
        {
            if (nid < 0 || nid >= Regions.Count) continue;
            var n = Regions[nid];
            if (n.ControllingFactionId is not null) continue;   // unclaimed only — never war
            if (best is null || n.Harvest > best.Harvest) best = n;
        }
        return best;
    }

    // ---------- prejudice (the outsider force) ----------

    /// <summary>Prejudice V1: the social force the migration arc set up. An ESTABLISHED people
    /// (rooted, not itself a recent newcomer) turns on a NEWCOMER neighbour of different stock —
    /// origin prejudice, distinct from the faith-keyed <see cref="Persecution"/>. Each people gets
    /// exactly ONE draw per year (a fixed-cost <see cref="Rng.Chance"/> — Chance(0) still consumes
    /// its ULong, like the plague spark and the migration draw — so consumption stays deterministic
    /// regardless of who is eligible); eligibility (established, and stress sharpening the odds)
    /// decides the probability, never WHETHER the draw happens. On a hit it finds a different-culture
    /// people that migrated within the window and shares a border (deterministic — sorted faction
    /// order, first match, zero further Rng), records a `scorn` event anchored to the BORDER region
    /// (RegionId-only — a feeling on a frontier, never a site; SiteAnchors is NOT extended), raises
    /// tension toward the newcomers (feeding the existing war machinery, like gossip), and darkens
    /// the newcomers' figurehead's standing (the group stigma surfaced on its leader, reusing the
    /// gossip Reputation scale). It invents no new killing — scorn stokes war through tension, the
    /// payoff the sim already balances. Runs after Gossip (so the year's whispers are in) and before
    /// MaybeDeclareWars (so a scorn can tip a pair into war the same tick).</summary>
    private void Prejudice()
    {
        foreach (var fid in FactionsSorted())
        {
            var e = Factions[fid];
            // Established = present in force AND settled long enough to be no recent newcomer itself.
            // Stress (its own famine/plague) sharpens scapegoating. The DRAW happens regardless (fixed
            // cost), so consumption never depends on who is eligible.
            bool established = e.Members.Count >= (int)Params["prejudice_min_pop"]
                && Year - e.LastMigrationYear >= (int)Params["prejudice_established_window"];
            double chance = established
                ? ((e.InFamine || e.InPlague) ? Params["prejudice_chance_stressed"]
                                              : Params["prejudice_chance_per_year"])
                : 0.0;
            if (!Rng.Chance(chance)) continue;

            var target = FindNewcomerNeighbour(e);
            if (target is null) continue;   // the draw is spent; nobody to scorn this year
            ScornNewcomers(e, target);
        }
    }

    /// <summary>The newcomer a people would resent: a different-culture neighbour that moved within
    /// the window and shares a border. Sorted faction order, first match — zero Rng. Null when none
    /// qualifies.</summary>
    private Faction? FindNewcomerNeighbour(Faction e)
    {
        int window = (int)Params["prejudice_newcomer_window"];
        foreach (var oid in FactionsSorted())
        {
            if (oid == e.Id) continue;
            var o = Factions[oid];
            if (o.Members.Count == 0) continue;
            if (Year - o.LastMigrationYear >= window) continue;   // not a newcomer anymore
            if (o.Culture == e.Culture) continue;                 // origin prejudice = different stock
            if (!SharesBorder(e, o)) continue;
            return o;
        }
        return null;
    }

    /// <summary>Whether any region one people holds is adjacent to a region the other holds. Pure
    /// read over control + the fixed adjacency graph — zero Rng.</summary>
    private bool SharesBorder(Faction a, Faction b)
    {
        foreach (var rid in a.ControlledRegions.Select(int.Parse).OrderBy(x => x))
            foreach (var nid in Regions[rid].AdjacentRegionIds)
                if (nid >= 0 && nid < Regions.Count && Regions[nid].ControllingFactionId == b.Id)
                    return true;
        return false;
    }

    /// <summary>Record one scorn: a `prejudice` event anchored to the resenter's border holding,
    /// tension toward the newcomers, and a darkened standing for their leader. The target faction is
    /// carried in a `target-{id}` tag so The Unwelcome echo can key on it. Zero Rng.</summary>
    private void ScornNewcomers(Faction e, Faction target)
    {
        int? borderRegion = null;
        foreach (var rid in e.ControlledRegions.Select(int.Parse).OrderBy(x => x))
            if (Regions[rid].AdjacentRegionIds.Any(n =>
                    n >= 0 && n < Regions.Count && Regions[n].ControllingFactionId == target.Id))
            { borderRegion = rid; break; }

        var causes = new List<int>();
        string when;
        if (e.InPlague && e.PlagueEvent is Event pe) { causes.Add(pe.Id); when = ", as the pestilence gnaws,"; }
        else if (e.InFamine && e.FamineEvent is Event fe) { causes.Add(fe.Id); when = ", in the hungry years,"; }
        else when = "";

        var tags = new List<string> { "prejudice", "scorn", "cross-faction", $"by-{e.Id}", $"target-{target.Id}" };
        var ev = Chronicle.Record(Year, "prejudice",
            $"{e.Name}{when} name {target.Name} unwelcome newcomers and turn against them.",
            participants: WarLeaders(e.Id, target.Id),
            causes: causes.Count > 0 ? causes : null,
            tags: tags, regionId: borderRegion);
        AddTension(e.Id, target.Id, Params["prejudice_tension"], ev);
        // The group stigma falls on the newcomers' figurehead — reuses the gossip standing scale.
        if (target.LeaderId is int tl && People.TryGetValue(tl, out var lead) && lead.Alive)
            lead.Reputation = Math.Clamp(lead.Reputation - (int)Params["prejudice_reputation_step"], -5, 5);
    }

    // ---------- culture (values → customs → clash / diffusion) ----------

    private int? PrimaryRegion(Faction f)
        => f.ControlledRegions.Count == 0 ? null : f.ControlledRegions.Select(int.Parse).Min();

    /// <summary>The cultural pressure engine, shaped like Economy(): each people's four value
    /// axes random-walk toward a culture baseline (biased by its current condition), harden into
    /// named customs at threshold and fade below a floor (hysteresis via CustomOriginEvent), then
    /// neighbours clash over opposing customs (tension, feeds war) or lend customs to each other
    /// (eases tension, like trade). Bounded O(factions²) over the three peoples; every iteration
    /// that draws RNG is explicitly ordered, so the same seed yields the same culture.</summary>
    private void Culture()
    {
        foreach (var fid in FactionsSorted())
        {
            var f = Factions[fid];
            if (f.Members.Count == 0) continue;
            var baseline = CultureValueBaseline.GetValueOrDefault(f.Culture);
            bool atWar = _activeWars.Any(w => w.Pair.Item1 == fid || w.Pair.Item2 == fid);
            double step = Params["culture_drift_step"];

            foreach (var axis in ValueAxes)
            {
                double v = f.Values.GetValueOrDefault(axis, 0.5);
                double bsl = baseline?.GetValueOrDefault(axis, 0.5) ?? 0.5;
                v += Rng.RandInt(-1, 1) * step;
                v += (bsl - v) * Params["culture_revert"];
                if (atWar && axis == "valor") v += step;          // war hardens martial temper
                if (f.InFamine && axis == "cunning") v += step;   // scarcity breeds guile
                if (f.InBoom && axis == "harmony") v += step;     // plenty breeds goodwill
                if (f.CustomOriginEvent.ContainsKey(AxisCustom[axis]))
                    v += step * 0.5;   // tradition hardens: a held custom reinforces its own axis,
                                       // so identities persist for generations until a real downturn breaks them
                f.Values[axis] = Math.Clamp(v, 0.0, 1.0);
            }

            // Threshold crossings: adopt a custom when its axis runs high, shed it when it sinks.
            foreach (var axis in ValueAxes)
            {
                string custom = AxisCustom[axis];
                double v = f.Values[axis];
                bool held = f.CustomOriginEvent.ContainsKey(custom);
                var leader = f.LeaderId is int lid ? new List<int> { lid } : null;
                if (!held && v >= Params["culture_identity_threshold"])
                {
                    var bornTags = new List<string> { "culture", custom };
                    int? bornRegion = PrimaryRegion(f);
                    var ev = Chronicle.Record(Year, "custom",
                        $"{f.Name} become {CustomBecome[custom]}.",
                        participants: leader, tags: bornTags, regionId: bornRegion,
                        siteId: AnchorSite("custom", bornTags, bornRegion));
                    f.CustomOriginEvent[custom] = ev.Id;
                }
                else if (held && v <= Params["culture_identity_drop"])
                {
                    var fadeTags = new List<string> { "culture", "fade", custom };
                    int? fadeRegion = PrimaryRegion(f);
                    Chronicle.Record(Year, "custom",
                        $"{f.Name} {CustomFade[custom]}.",
                        participants: leader, causes: new() { f.CustomOriginEvent[custom] },
                        tags: fadeTags, regionId: fadeRegion,
                        siteId: AnchorSite("custom", fadeTags, fadeRegion));
                    f.CustomOriginEvent.Remove(custom);
                }
            }
        }

        var fids = FactionsSorted().ToList();

        // Cultural clash: neighbours holding opposing customs grate on each other, raising tension.
        for (int i = 0; i < fids.Count; i++)
            for (int j = i + 1; j < fids.Count; j++)
            {
                var fa = Factions[fids[i]];
                var fb = Factions[fids[j]];
                if (fa.Members.Count == 0 || fb.Members.Count == 0) continue;
                foreach (var axis in new[] { "valor", "piety" })
                {
                    string cA = AxisCustom[axis];
                    string cB = AxisCustom[AxisOpposite[axis]];
                    bool dir1 = fa.CustomOriginEvent.ContainsKey(cA) && fb.CustomOriginEvent.ContainsKey(cB);
                    bool dir2 = fa.CustomOriginEvent.ContainsKey(cB) && fb.CustomOriginEvent.ContainsKey(cA);
                    if (!(dir1 || dir2)) continue;
                    if (!Rng.Chance(Params["culture_clash_chance_per_year"])) continue;
                    var (holdA, holdB) = dir1 ? (cA, cB) : (cB, cA);
                    var ev = Chronicle.Record(Year, "custom",
                        $"The {holdA} ways of {fa.Name} and the {holdB} ways of {fb.Name} grate against each other.",
                        causes: new() { fa.CustomOriginEvent[holdA], fb.CustomOriginEvent[holdB] },
                        tags: new() { "culture", "clash" }, regionId: PrimaryRegion(fa));
                    AddTension(fa.Id, fb.Id, Params["culture_clash_tension"], ev);
                    break;   // one clash per pair per year
                }
            }

        // Cultural diffusion: the more prosperous neighbour lends a custom it holds to the other,
        // softening the feeling between them (the cultural twin of trade).
        for (int i = 0; i < fids.Count; i++)
            for (int j = i + 1; j < fids.Count; j++)
            {
                var fa = Factions[fids[i]];
                var fb = Factions[fids[j]];
                if (fa.Members.Count == 0 || fb.Members.Count == 0) continue;
                if (!Rng.Chance(Params["culture_diffusion_chance_per_year"])) continue;
                var (donor, recv) = fa.Prosperity >= fb.Prosperity ? (fa, fb) : (fb, fa);
                var spreadable = donor.CustomOriginEvent.Keys
                    .Where(c => !recv.CustomOriginEvent.ContainsKey(c))
                    .OrderBy(c => c, StringComparer.Ordinal).ToList();
                if (spreadable.Count == 0) continue;
                string custom = spreadable[0];
                recv.Values[CustomAxis[custom]] =
                    Math.Max(recv.Values[CustomAxis[custom]], Params["culture_identity_threshold"]);
                var ev = Chronicle.Record(Year, "custom",
                    $"{recv.Name} take up the {custom} ways of {donor.Name}.",
                    participants: recv.LeaderId is int rl ? new() { rl } : null,
                    causes: new() { donor.CustomOriginEvent[custom] },
                    tags: new() { "culture", "diffusion", "cross-faction", custom },
                    regionId: PrimaryRegion(recv));
                recv.CustomOriginEvent[custom] = ev.Id;
                var key = PairKey(fa.Id, fb.Id);
                Tension[key] = Math.Max(0.0, Tension.GetValueOrDefault(key) - Params["culture_diffusion_tension_reduction"]);
            }
    }

    // ---------- gossip / reputation (social ripples over real events) ----------

    /// <summary>Who a rumor is about. The actor of a killing (last participant), otherwise the
    /// first living participant by id (or the first participant if none survive). Deterministic —
    /// no Rng.</summary>
    private int? RumorSubject(Event e)
    {
        if (e.Participants.Count == 0) return null;
        if (e.Type == "murder") return e.Participants[^1];   // the one who did the killing
        foreach (var id in e.Participants.OrderBy(i => i))
            if (People.TryGetValue(id, out var p) && p.Alive) return id;
        return e.Participants[0];
    }

    /// <summary>Classify a source event into a rumor: reputation delta (±1/0), tension direction
    /// (+1 harm, −1 ease, 0 none), and the line the chronicle remembers. Returns null for events
    /// that don't carry socially. Pure flavor/polarity — draws no Rng.</summary>
    private static (int rep, int tens, string text)? RumorOf(Event e, Person s, Faction fac)
    {
        switch (e.Type)
        {
            case "murder":
                if (e.Tags.Contains("persecution"))
                    return (-1, +1, $"Talk of {s.Name}'s cruelty against unbelievers spreads through {fac.Name}.");
                if (e.Tags.Contains("regicide") || e.Tags.Contains("ambition"))
                    return (-1, +1, $"Dark whispers trail {s.Name} after a leader's killing.");
                return (-1, +1, $"Stories of the killing stain {s.Name}'s name.");
            case "scandal":
                return (-1, 0, $"Whispers spread that {s.Name} cannot be trusted.");
            case "romance":
                return e.Tags.Contains("forbidden")
                    ? (-1, +1, $"Gossip of {s.Name}'s forbidden love runs between the peoples.")
                    : ((int, int, string)?)null;
            case "prophet":
                return (+1, 0, $"Word of {s.Name}'s revelation spreads among {fac.Name}.");
            case "martyr":
                return (+1, 0, $"{s.Name}'s martyrdom passes into the songs of {fac.Name}.");
            case "trade":
                return (+1, -1, $"Word of {fac.Name}'s generous trade softens old suspicion.");
            case "boom":
                return (+1, 0, $"Songs of plenty lift the name of {fac.Name}.");
            case "custom":
                return e.Tags.Contains("diffusion")
                    ? (+1, -1, $"Tales of {fac.Name} taking up new ways travel between the peoples.")
                    : ((int, int, string)?)null;
            default:
                return null;
        }
    }

    /// <summary>The gossip layer: real events leave social ripples. Reads only this year's new
    /// chronicle events (bounded cursor — no all-history scan), and for the notable few rolls a
    /// rumor that shifts a person's reputation and, across factions, nudges tension. Every rumor
    /// cause-links to the real event, so catch-up always walks back to the thing that happened —
    /// gossip never invents truth. Capped at 2 rumors/year, one per source, per-person cooldown,
    /// and rumor events are never themselves gossiped (no recursion). All Rng draws are in event-
    /// index order, so the same seed yields the same talk.</summary>
    private void Gossip()
    {
        int upto = Chronicle.Events.Count;   // snapshot: ignore the rumors this pass appends
        int rumorsThisYear = 0;
        for (int i = _lastGossipEventCount; i < upto && rumorsThisYear < 2; i++)
        {
            var e = Chronicle.Events[i];
            if (!GossipTypes.Contains(e.Type)) continue;
            if (RumorSubject(e) is not int subjId) continue;
            var s = People[subjId];
            var fac = Factions[s.FactionId];
            if (RumorOf(e, s, fac) is not (int rep, int tens, string text) cat) continue;

            if (Scoring.ImportanceFast(e, this, NoConsequences) < Params["gossip_min_importance"]) continue;
            // Cooldown — guard the int.MinValue sentinel so the first rumor isn't lost to overflow.
            if (s.LastRumorYear != int.MinValue && Year - s.LastRumorYear < (int)Params["gossip_cooldown_years"]) continue;

            // Culture tilts the odds: a scheming people spreads slander more readily; a peaceable
            // one spreads the talk that mends fences.
            double chance = Params["gossip_chance_per_event"];
            if (rep < 0 && fac.CustomOriginEvent.ContainsKey("scheming")) chance += 0.10;
            if (tens < 0 && fac.CustomOriginEvent.ContainsKey("peaceable")) chance += 0.10;
            if (!Rng.Chance(chance)) continue;

            var rumorEv = Chronicle.Record(Year, "rumor", text,
                participants: new() { s.Id }, causes: new() { e.Id },
                tags: new() { "rumor", "reputation", rep < 0 ? "negative" : "positive" },
                regionId: PrimaryRegion(fac));
            if (s.Alive)
                s.Reputation = Math.Clamp(s.Reputation + rep * (int)Params["gossip_reputation_step"], -5, 5);
            s.LastRumorYear = Year;
            rumorsThisYear++;

            // Cross-faction fallout: the factions named in the source event grow more (or less)
            // suspicious of each other. The rumor id lands in their grievance memory, so a war it
            // helps cause traces back through the whisper to the real deed.
            var facs = new List<string>();
            foreach (var id in e.Participants)
                if (People.TryGetValue(id, out var p) && !facs.Contains(p.FactionId)) facs.Add(p.FactionId);
            facs.Sort(StringComparer.Ordinal);
            double step = Params["gossip_tension_step"];
            if (tens > 0)
            {
                if (facs.Count >= 2) AddTension(facs[0], facs[1], step, rumorEv);
                else if (facs.Count == 1 && Rng.Chance(Params["gossip_cross_faction_chance"]))
                {
                    string? other = FactionsSorted().FirstOrDefault(f => f != facs[0] && Factions[f].Members.Count > 0);
                    if (other is not null) AddTension(facs[0], other, step, rumorEv);
                }
            }
            else if (tens < 0 && facs.Count >= 2)
            {
                var key = PairKey(facs[0], facs[1]);
                Tension[key] = Math.Max(0.0, Tension.GetValueOrDefault(key) - step);
            }
        }
        _lastGossipEventCount = Chronicle.Events.Count;
    }

    // ---------- the yearly tick ----------

    public void Tick()
    {
        Year += 1;
        Economy();
        Pestilence();
        Migration();
        ProcessWars();
        Deaths();
        Crime();
        ForbiddenRomance();
        Marriages();
        Births();
        DoReligion();
        Culture();
        Gossip();
        Prejudice();
        MaybeDeclareWars();
        DecayTension();
        ReleaseExtinctLands();
        // Re-settle the derived land-mood rollups AFTER territory has finished changing this tick
        // (ProcessWars conquests, extinct-land release): Economy/Pestilence derived them mid-tick,
        // before those moves, so the END-OF-TICK snapshot would otherwise read a stale Prosperity/
        // famine/plague for any people whose holdings just changed. This is pure aggregation (no RNG)
        // and behaviourally INERT — nothing reads these flags again until next tick's Economy/
        // Pestilence overwrite them before any use — so it keeps verify byte-identical while making
        // the land-mood invariant (Prosperity == controlled-region mean; landless ⇒ neutral) hold at
        // every tick boundary the gates and viewer observe.
        foreach (var fid in FactionsSorted())
        {
            DeriveProsperity(Factions[fid]);
            DerivePestilence(Factions[fid]);
        }
    }

    /// <summary>When a people dies out (no war needed — internal collapse counts), their land
    /// returns to wilderness so the map never shows holds owned by the dead. Runs after every
    /// death source for the year. Self-limiting: releasing empties ControlledRegions, so the
    /// Count guard never lets a faction fire this twice. Deterministic — no Rng, off an already-
    /// deterministic death.</summary>
    private void ReleaseExtinctLands()
    {
        foreach (var fid in _factionOrder)
        {
            var fac = Factions[fid];
            if (fac.Members.Count != 0 || fac.ControlledRegions.Count == 0) continue;
            var freed = fac.ControlledRegions.Select(s => Regions[int.Parse(s)]).OrderBy(r => r.Id).ToList();
            foreach (var region in freed) region.ControllingFactionId = null;
            fac.ControlledRegions.Clear();
            var abandonTags = new List<string> { "territory", "abandonment" };
            Chronicle.Record(Year, "territory",
                $"{fac.Name} are gone; their holds fall silent and the wild creeps back over {string.Join(", ", freed.Select(r => r.Name))}.",
                causes: fac.LastDeathEventId is int d ? new() { d } : null,
                tags: abandonTags, regionId: freed[0].Id,
                siteId: AnchorSite("territory", abandonTags, freed[0].Id));
        }
    }

    // ---------- death (natural and violent) ----------

    private void RemoveFromLife(Person p)
    {
        p.Alive = false;
        p.DeathYear = Year;
        Factions[p.FactionId].Members.Remove(p.Id);
        if (p.ReligionId is int rid && Religions.TryGetValue(rid, out var rel))
            rel.Members.Remove(p.Id);
        if (p.SpouseId is int sp && People.TryGetValue(sp, out var spouse))
            spouse.SpouseId = null;
        p.IsLeader = false;
    }

    private void Deaths()
    {
        foreach (var p in Living())   // living in id order — same set/order as before, O(living)
        {
            // Divine pressure modulates the SAME roll — multipliers on the existing draw,
            // never an extra draw, so a pressure-free run stays byte-identical (verify-safe).
            double dc = DeathChance(p.Age(Year));
            if (p.Cursed) dc = Math.Min(0.95, dc * Params["curse_death_multiplier"]);
            if (p.Blessed) dc *= Params.GetValueOrDefault("bless_death_multiplier", 0.7);
            var fac = Factions[p.FactionId];
            // Plague and famine multipliers STACK on the same roll (a starving, plagued people dies
            // hardest); divine protect/doom modulate each, mirroring famine. Multipliers only — no
            // extra draw — so a pressure-free, plague-free run stays byte-identical.
            if (fac.InPlague)
            {
                double pm = Params["plague_death_multiplier"];
                if (fac.ProtectUntilYear > Year)
                    pm = 1.0 + (pm - 1.0) * Params.GetValueOrDefault("protect_plague_relief", 0.5);
                if (fac.DoomUntilYear > Year)
                    pm = 1.0 + (pm - 1.0) * Params.GetValueOrDefault("doom_plague_burden", 1.5);
                dc = Math.Min(0.95, dc * pm);
            }
            if (fac.InFamine)
            {
                double fm = Params["famine_death_multiplier"];
                if (fac.ProtectUntilYear > Year)
                    fm = 1.0 + (fm - 1.0) * Params.GetValueOrDefault("protect_famine_relief", 0.5);
                if (fac.DoomUntilYear > Year)
                    fm = 1.0 + (fm - 1.0) * Params.GetValueOrDefault("doom_famine_burden", 1.5);
                dc = Math.Min(0.95, dc * fm);
            }
            if (Rng.Chance(dc))
            {
                // Proximate-cause priority: curse > plague > famine > blessing > natural. One cause
                // per death (Kill stays single-cause in V1); the stacked multipliers already shaped
                // the roll — this only names which force the chronicle records as the proximate one.
                if (p.Cursed && CurseEvent is not null)
                    Kill(p, reason: "as the old curse takes them", cause: CurseEvent);
                else if (fac.InPlague && fac.PlagueEvent is not null)
                    Kill(p, reason: "in the pestilence", cause: fac.PlagueEvent);
                else if (fac.InFamine && fac.FamineEvent is not null)
                    Kill(p, reason: "in the famine", cause: fac.FamineEvent);
                else if (p.Blessed && p.BlessEvent is not null)
                    Kill(p, cause: p.BlessEvent);   // the blessing was on them; it could not hold
                else
                    Kill(p);
            }
        }
    }

    private Event Kill(Person p, string? reason = null, Event? cause = null)
    {
        int age = p.Age(Year);
        bool wasLeader = p.IsLeader;
        var fac = Factions[p.FactionId];
        RemoveFromLife(p);
        string text = $"{p.Name} of {fac.Name} dies {reason ?? DeathReason(age)} at {age}.";
        var ev = Chronicle.Record(Year, "death", text, participants: new() { p.Id },
            causes: cause is null ? null : new() { cause.Id }, tags: new() { "death" },
            homeRegionId: p.HomeRegionId);   // remembered at the home of their line, not a death place
        fac.LastDeathEventId = ev.Id;
        if (wasLeader) Succeed(fac, p, ev);
        return ev;
    }

    private (Person heir, Event ev)? Succeed(Faction fac, Person deadLeader, Event causeEv)
    {
        var children = deadLeader.Children
            .Select(c => People[c])
            .Where(p => p.Alive && p.FactionId == fac.Id).ToList();
        Person heir;
        string text;
        if (children.Count > 0)
        {
            heir = Oldest(children.OrderBy(p => p.Id), Year);
            string heirName = heir.Name + (heir.Name == deadLeader.Name ? " the younger" : "");
            text = $"{heirName} inherits the leadership of {fac.Name} from {deadLeader.Name}.";
        }
        else
        {
            var members = FactionMembers(fac.Id);
            if (members.Count == 0)
            {
                fac.LeaderId = null;
                Chronicle.Record(Year, "leadership", $"{fac.Name} are left leaderless.",
                    causes: new() { causeEv.Id }, tags: new() { "leadership" });
                return null;
            }
            heir = Oldest(members, Year);
            string heirName = heir.Name + (heir.Name == deadLeader.Name ? " the younger" : "");
            text = $"{heirName}, now eldest of {fac.Name}, takes up the leadership after {deadLeader.Name}.";
        }
        fac.LeaderId = heir.Id;
        heir.IsLeader = true;
        heir.EverLeader = true;
        var ev = Chronicle.Record(Year, "succession", text,
            participants: new() { heir.Id, deadLeader.Id },
            causes: new() { causeEv.Id }, tags: new() { "leadership", "succession" });
        return (heir, ev);
    }

    // ---------- crime ----------

    private (Event ev, (Person heir, Event ev)? succ) Murder(
        Person killer, Person victim, string text, List<int>? causes, List<string> tags)
    {
        bool wasLeader = victim.IsLeader;
        var fac = Factions[victim.FactionId];
        RemoveFromLife(victim);
        var allTags = new List<string> { "crime", "murder" };
        allTags.AddRange(tags);
        var ev = Chronicle.Record(Year, "murder", text,
            participants: new() { victim.Id, killer.Id }, causes: causes, tags: allTags,
            homeRegionId: victim.HomeRegionId);   // the victim's line carries the grief — never a murder site
        fac.LastDeathEventId = ev.Id;
        victim.KillerId = killer.Id;
        victim.Murdered = true;
        victim.MurderEventId = ev.Id;
        _unavengedVictimIds.Add(victim.Id);
        if (killer.FactionId != victim.FactionId)
            AddTension(killer.FactionId, victim.FactionId, 3.0, ev);
        if (victim.IsProphet && victim.ReligionId is int vrid && Religions.TryGetValue(vrid, out var rel))
        {
            var pool = FactionMembers(victim.FactionId)
                .Where(p => p.ReligionId != rel.Id && p.Age(Year) >= 14).ToList();
            Rng.Shuffle(pool);
            var gained = pool.Take(Rng.RandInt(2, 5)).ToList();
            foreach (var p in gained) SetReligion(p, rel);
            Chronicle.Record(Year, "martyr",
                $"{victim.Name}'s death makes a martyr, and {rel.Name} swells with {gained.Count} new believers.",
                participants: new() { victim.Id }, causes: new() { ev.Id }, tags: new() { "religion", "martyr" });
        }
        var succ = wasLeader ? Succeed(fac, victim, ev) : null;
        return (ev, succ);
    }

    private void Crime()
    {
        AmbitionKillings();
        RevengeKillings();
    }

    private void AmbitionKillings()
    {
        foreach (var f in _factionOrder.Select(id => Factions[id]).ToList())
        {
            if (f.LeaderId is null || !Rng.Chance(Params["ambition_murder_chance_per_year"])) continue;
            var leader = People[f.LeaderId.Value];
            var kin = KinOf(leader).Where(p => p.FactionId == f.Id && p.Age(Year) >= 18).ToList();
            if (kin.Count == 0) continue;
            var killer = Rng.Pick(kin.OrderBy(p => p.Id).ToList());
            string text = $"{killer.Name} murders {leader.Name}, leader of {f.Name}, in a grasp for power.";
            var (ev, succ) = Murder(killer, leader, text, null, new() { "ambition", "regicide" });
            bool tookThrone = succ is not null && succ.Value.heir.Id == killer.Id;
            double discovery = Params["murder_discovery_chance"] * (tookThrone ? 0.4 : 1.0);
            // A name already blackened by gossip is watched more closely; a trusted one, less so.
            discovery *= Math.Clamp(1.0 + (-killer.Reputation * 0.08), 0.7, 1.5);
            if (killer.Alive && Rng.Chance(discovery))
            {
                if (Rng.Chance(0.5) && killer.Alive)
                {
                    Kill(killer, reason: "executed for the murder", cause: ev);
                    Chronicle.Record(Year, "justice",
                        $"{killer.Name} is put to death once the murder of {leader.Name} comes to light.",
                        participants: new() { killer.Id }, causes: new() { ev.Id }, tags: new() { "law", "justice" });
                }
                else
                {
                    Chronicle.Record(Year, "justice",
                        $"{killer.Name} is exiled when the murder of {leader.Name} is uncovered.",
                        participants: new() { killer.Id }, causes: new() { ev.Id }, tags: new() { "law", "justice" });
                }
            }
        }
    }

    private void RevengeKillings()
    {
        // Drop victims who are avenged or whose killer is gone (they can never trigger and
        // would be skipped anyway), so the scan stays bounded over very long runs.
        _unavengedVictimIds.RemoveWhere(id =>
        {
            var v = People[id];
            return v.Avenged || v.KillerId is not int k
                   || !People.TryGetValue(k, out var killer) || !killer.Alive;
        });
        var victims = _unavengedVictimIds.OrderBy(id => id).Select(id => People[id]).ToList();
        foreach (var victim in victims)
        {
            var killer = People[victim.KillerId!.Value];
            var avengers = KinOf(victim).Where(p => p.Age(Year) >= 16 && p.Alive).ToList();
            if (avengers.Count == 0 || !Rng.Chance(Params["revenge_chance_per_year"])) continue;
            var avenger = Rng.Pick(avengers.OrderBy(p => p.Id).ToList());
            string text = $"{avenger.Name} avenges {victim.Name} by killing {killer.Name}.";
            Murder(avenger, killer, text,
                victim.MurderEventId is int me ? new() { me } : null, new() { "revenge" });
            victim.Avenged = true;
        }
    }

    // ---------- forbidden romance ----------

    private void ForbiddenRomance()
    {
        if (!Rng.Chance(Params["forbidden_romance_chance_per_year"])) return;
        var singles = Living().Where(p => p.SpouseId is null && p.Age(Year) >= 18 && p.Age(Year) <= 50).ToList();
        var byFaction = new Dictionary<string, List<Person>>();
        foreach (var p in singles)
        {
            if (!byFaction.TryGetValue(p.FactionId, out var list)) { list = new(); byFaction[p.FactionId] = list; }
            list.Add(p);
        }
        var eligible = byFaction.Where(kv => kv.Value.Count > 0).Select(kv => kv.Key).OrderBy(f => f, StringComparer.Ordinal).ToList();
        if (eligible.Count < 2) return;
        var combos = new List<List<string>>();
        for (int i = 0; i < eligible.Count; i++)
            for (int j = i + 1; j < eligible.Count; j++)
                combos.Add(new() { eligible[i], eligible[j] });
        var chosen = Rng.Pick(combos);
        string fa = chosen[0], fb = chosen[1];
        var a = Rng.Pick(byFaction[fa].OrderBy(p => p.Id).ToList());
        var b = Rng.Pick(byFaction[fb].OrderBy(p => p.Id).ToList());
        var bond = Chronicle.Record(Year, "romance",
            $"A forbidden bond forms between {a.Name} of {Factions[fa].Name} and {b.Name} of {Factions[fb].Name}.",
            participants: new() { a.Id, b.Id }, tags: new() { "romance", "forbidden", "cross-faction" });
        ResolveRomance(a, b, bond);
    }

    private void ResolveRomance(Person a, Person b, Event bond)
    {
        double tension = Tension.GetValueOrDefault(PairKey(a.FactionId, b.FactionId));
        int roll = Rng.RandInt(1, 100) + (int)(tension * 4);
        if (roll <= 35)
        {
            Marry(a, b, crossFaction: true, cause: bond);
            var ev = Chronicle.Record(Year, "romance",
                "Their union is blessed, and for a time it softens the feeling between the two peoples.",
                participants: new() { a.Id, b.Id }, causes: new() { bond.Id }, tags: new() { "romance", "peace" });
            AddTension(a.FactionId, b.FactionId, -3.5, ev);
        }
        else if (roll <= 70)
        {
            Chronicle.Record(Year, "scandal",
                $"The affair is exposed. {a.Name} and {b.Name} are driven apart in disgrace.",
                participants: new() { a.Id, b.Id }, causes: new() { bond.Id }, tags: new() { "scandal" });
            AddTension(a.FactionId, b.FactionId, 1.5, bond);
        }
        else
        {
            var (lover, other) = Rng.Chance(0.5) ? (a, b) : (b, a);
            var kin = KinOf(other).Where(p => p.FactionId == other.FactionId && p.Age(Year) >= 16).ToList();
            Person killer = kin.Count > 0 ? Rng.Pick(kin.OrderBy(p => p.Id).ToList()) : other;
            string text = $"The bond ends in blood: {killer.Name} kills {lover.Name} to end the disgrace.";
            var (ev, _) = Murder(killer, lover, text, new() { bond.Id }, new() { "tragedy", "honor" });
            AddTension(a.FactionId, b.FactionId, 4.0, ev);
        }
    }

    // ---------- ordinary marriage and birth ----------

    private void Marriages()
    {
        foreach (var fid in FactionsSorted())
        {
            var singles = FactionMembers(fid)
                .Where(p => p.SpouseId is null && p.Age(Year) >= 18 && p.Age(Year) <= 55).ToList();
            Rng.Shuffle(singles);
            var used = new HashSet<int>();
            foreach (var p in singles)
            {
                if (used.Contains(p.Id) || !Rng.Chance(Params["marriage_chance_per_year"])) continue;
                var partners = singles.Where(q => !used.Contains(q.Id) && q.Id != p.Id
                    && q.SpouseId is null && q.Sex != p.Sex && !CloselyRelated(p, q)).ToList();
                if (partners.Count == 0) continue;
                var partner = Rng.Pick(partners.OrderBy(q => q.Id).ToList());
                Marry(p, partner, crossFaction: false);
                used.Add(p.Id);
                used.Add(partner.Id);
            }
        }
    }

    private void Marry(Person a, Person b, bool crossFaction, Event? cause = null)
    {
        a.SpouseId = b.Id;
        b.SpouseId = a.Id;
        string text;
        List<string> tags;
        if (crossFaction)
        {
            text = $"{a.Name} of {Factions[a.FactionId].Name} weds {b.Name} of {Factions[b.FactionId].Name}, a union across two peoples.";
            tags = new() { "romance", "marriage", "cross-faction" };
        }
        else
        {
            text = $"{a.Name} and {b.Name} of {Factions[a.FactionId].Name} are wed.";
            tags = new() { "romance", "marriage" };
        }
        Chronicle.Record(Year, "marriage", text, participants: new() { a.Id, b.Id },
            causes: cause is null ? null : new() { cause.Id }, tags: tags);
    }

    private void Births()
    {
        // Logistic carrying capacity: a people's birth rate falls toward zero as it nears
        // its capacity, so population rises then plateaus instead of exploding. Set
        // carrying_capacity to 0 (or omit it) to disable and get raw exponential growth.
        double cap = Params.GetValueOrDefault("carrying_capacity", 0.0);
        var facPop = new Dictionary<string, int>();
        if (cap > 0)
            foreach (var fid in _factionOrder) facPop[fid] = Factions[fid].Members.Count;

        var couples = new SortedSet<(int, int)>();
        foreach (var p in Living())
            if (p.SpouseId is int sp && People.TryGetValue(sp, out var spouse) && spouse.Alive)
                couples.Add((Math.Min(p.Id, sp), Math.Max(p.Id, sp)));
        foreach (var (aId, bId) in couples)
        {
            var pa = People[aId];
            var pb = People[bId];
            var mother = pa.Sex == "f" ? pa : pb;
            var father = ReferenceEquals(mother, pa) ? pb : pa;
            if (mother.Sex != "f" || father.Sex != "m") continue;
            if (!(mother.Age(Year) >= 18 && mother.Age(Year) <= 44)) continue;
            if (mother.Children.Count >= (int)Params["max_children"]) continue;

            double chance = Params["birth_chance_per_couple_per_year"];
            if (cap > 0)
                chance *= Math.Max(0.0, 1.0 - facPop[father.FactionId] / cap);
            chance *= 0.7 + 0.3 * Factions[father.FactionId].Prosperity;   // 0.7 famine … 1.0 neutral … 1.3 boom
            if (Rng.Chance(chance))
                Birth(mother, father);
        }
    }

    private void Birth(Person mother, Person father)
    {
        var child = CreatePerson(father.FactionId, age: 0);
        child.HomeRegionId = father.HomeRegionId ?? mother.HomeRegionId;
        child.Parents.Add(mother.Id);
        child.Parents.Add(father.Id);
        if (mother.Cursed || father.Cursed) child.Cursed = true;
        Religion? faith = (father.ReligionId is int fr ? Religions.GetValueOrDefault(fr) : null)
                          ?? (mother.ReligionId is int mr ? Religions.GetValueOrDefault(mr) : null);
        if (faith is not null) SetReligion(child, faith);
        mother.Children.Add(child.Id);
        father.Children.Add(child.Id);
        Chronicle.Record(Year, "birth",
            $"{child.Name} is born to {mother.Name} and {father.Name} of {Factions[father.FactionId].Name}.",
            participants: new() { child.Id, mother.Id, father.Id }, tags: new() { "birth" },
            homeRegionId: child.HomeRegionId);   // the root of the new life's line, not a birthplace
    }

    // ---------- war ----------

    /// <summary>Does this region carry a stronghold — the defensible ground a war is fought
    /// over (a hill fort, watch post, or river ford)? Pure read over the immutable site
    /// index; no Rng.</summary>
    private bool HasStronghold(int rid)
        => Sites.FirstOfTypes(rid, SiteType.HillFort, SiteType.WatchPost, SiteType.RiverFord) is not null;

    /// <summary>The border region a war between two peoples is fought over: a region held by
    /// one combatant whose land touches the other's. Prefers a region carrying a stronghold
    /// (the defensible ground worth the blood), then lowest id. Null when the two hold no
    /// adjacent land — the war has no fixed front and its battles are placeless raids. Zero
    /// Rng (a read over current control + the fixed adjacency graph), so resolving the front
    /// can never move the verify baseline.</summary>
    private int? FrontRegion(string fa, string fb)
    {
        bool HeldBy(int rid, string fid) => rid >= 0 && rid < Regions.Count && Regions[rid].ControllingFactionId == fid;
        var candidates = new List<int>();
        foreach (var r in Regions)
        {
            string? owner = r.ControllingFactionId;
            if (owner != fa && owner != fb) continue;
            string foe = owner == fa ? fb : fa;
            if (r.AdjacentRegionIds.Any(id => HeldBy(id, foe))) candidates.Add(r.Id);
        }
        if (candidates.Count == 0) return null;
        return candidates.OrderByDescending(HasStronghold).ThenBy(rid => rid).First();
    }

    /// <summary>Both sides' current leaders, so war / battle / peace events are
    /// faction-attributed — the chapter-closing gap the recaps noted (peace once carried no
    /// faction ids). Deterministic, no Rng.</summary>
    private List<int>? WarLeaders(string fa, string fb)
    {
        var ids = new List<int>();
        if (Factions[fa].LeaderId is int la) ids.Add(la);
        if (Factions[fb].LeaderId is int lb) ids.Add(lb);
        return ids.Count > 0 ? ids : null;
    }

    private void MaybeDeclareWars()
    {
        var atWar = _activeWars.Select(w => w.Pair).ToHashSet();
        foreach (var (key, level) in Tension.OrderBy(kv => kv.Key.Item1, StringComparer.Ordinal)
                                            .ThenBy(kv => kv.Key.Item2, StringComparer.Ordinal)
                                            .Select(kv => (kv.Key, kv.Value)).ToList())
        {
            if (atWar.Contains(key) || level < Params["war_tension_threshold"]) continue;
            string fa = key.Item1, fb = key.Item2;
            if (FactionMembers(fa).Count < 8 || FactionMembers(fb).Count < 8) continue;
            var grievanceIds = Grievances.GetValueOrDefault(key) ?? new();
            bool holy = grievanceIds.Any(g =>
                Chronicle.Get(g).Tags.Intersect(new[] { "religion", "heresy", "friction" }).Any());
            string text;
            List<string> tags;
            if (holy)
            {
                text = $"A holy war erupts between {Factions[fa].Name} and {Factions[fb].Name} over the worship of their gods.";
                tags = new() { "war", "holy", "religion" };
            }
            else
            {
                text = $"War breaks out between {Factions[fa].Name} and {Factions[fb].Name}.";
                tags = new() { "war" };
            }
            // The front (a real border region) is resolved with zero Rng, so it must be read
            // BEFORE the YearsLeft draw to keep the stream's only war-declaration draw unmoved.
            int? front = FrontRegion(fa, fb);
            var ev = Chronicle.Record(Year, "war", text,
                participants: WarLeaders(fa, fb),
                causes: grievanceIds.Count > 0 ? new List<int>(grievanceIds) : null, tags: tags,
                regionId: front, siteId: AnchorSite("war", tags, front));
            _activeWars.Add(new War { Pair = key, YearsLeft = Rng.RandInt(1, 2), DeclaredEvent = ev });
            Tension[key] = 1.0;
        }
    }

    private void ProcessWars()
    {
        foreach (var war in _activeWars.ToList())
        {
            string fa = war.Pair.Item1, fb = war.Pair.Item2;
            // The year's fighting becomes a recorded BATTLE the first time blood is drawn —
            // lazily, so a standoff year (both sides roll zero) records no battle and the
            // chronicle never invents a fight that did not happen. The casualty rolls below
            // are the exact ones the war already made; wrapping them in a battle adds events
            // without touching the Rng stream (verify moves only by the battle count).
            Event? battleEv = null;
            foreach (var fid in new[] { fa, fb })
            {
                var members = FactionMembers(fid).Where(p => p.Age(Year) >= 16).ToList();
                int casualties = Rng.RandInt(0, 2);
                for (int c = 0; c < casualties; c++)
                {
                    if (members.Count == 0) break;
                    var fallen = Rng.Pick(members.OrderBy(p => p.Id).ToList());
                    members.Remove(fallen);
                    battleEv ??= RecordBattle(war);
                    Kill(fallen, reason: "in the fighting", cause: battleEv);
                    war.Fallen++;
                }
            }
            war.YearsLeft -= 1;
            if (war.YearsLeft <= 0)
            {
                _activeWars.Remove(war);
                var peace = Chronicle.Record(Year, "peace", PeaceText(war),
                    participants: WarLeaders(fa, fb),
                    causes: new() { war.DeclaredEvent.Id }, tags: new() { "war", "peace" });
                TransferTerritory(Factions[fa], Factions[fb], peace.Id);
                Tension[war.Pair] = 2.0;
            }
        }
    }

    /// <summary>Record one battle of an ongoing war at its front — called once per war-year,
    /// the first time blood is drawn. Anchored to the front region and (when one stands
    /// there) its stronghold, via the one authored convention table; both leaders witness it;
    /// caused by the war's declaration. Draws no Rng — it only narrates casualties the war
    /// already rolled, so it adds an event without disturbing the stream.</summary>
    private Event RecordBattle(War war)
    {
        string fa = war.Pair.Item1, fb = war.Pair.Item2;
        var tags = new List<string> { "war", "battle" };
        int? front = FrontRegion(fa, fb);   // recomputed each battle — the front follows the map
        int? siteId = AnchorSite("battle", tags, front);
        string place = siteId is int sid ? $" at {Sites.Get(sid).Name}"
            : front is int fr ? $" in {Regions[fr].Name}"
            : "";
        string na = Factions[fa].Name, nb = Factions[fb].Name;
        string text = war.BattlesFought == 0
            ? $"{na} and {nb} meet in battle{place}."
            : $"{na} and {nb} clash again{place}.";
        var ev = Chronicle.Record(Year, "battle", text,
            participants: WarLeaders(fa, fb),
            causes: new() { war.DeclaredEvent.Id }, tags: tags,
            regionId: front, siteId: siteId);
        war.BattlesFought++;
        return ev;
    }

    /// <summary>The peace event's line — names both peoples and the war's toll (battles
    /// fought, souls fallen), so a chapter can close on a war's end. No Rng.</summary>
    private string PeaceText(War war)
    {
        string na = Factions[war.Pair.Item1].Name, nb = Factions[war.Pair.Item2].Name;
        if (war.BattlesFought == 0)
            return $"{na} and {nb} make peace, the war spent without a pitched battle.";
        string battles = war.BattlesFought == 1 ? "a single battle" : $"{war.BattlesFought} battles";
        string fallen = war.Fallen == 1 ? "one soul" : $"{war.Fallen} souls";
        return $"After {battles} and {fallen} fallen, {na} and {nb} make peace, though the grudge lingers.";
    }

    /// <summary>The end of a war redraws the map: the stronger side (more living members; a coin
    /// flip on a tie) takes 1–2 of the loser's regions, preferring those bordering its own land.
    /// A people never loses its last region — that floor keeps every faction on the map.</summary>
    private void TransferTerritory(Faction a, Faction b, int peaceEventId)
    {
        int popA = a.Members.Count, popB = b.Members.Count;
        var (winner, loser) = popA != popB
            ? (popA > popB ? (a, b) : (b, a))
            : (Rng.Chance(0.5) ? (a, b) : (b, a));

        int available = loser.ControlledRegions.Count - 1;   // never take the last region
        if (available <= 0) return;
        int take = Math.Min(Rng.RandInt(1, 2), available);

        bool BordersWinner(Region r) => r.AdjacentRegionIds.Any(id => Regions[id].ControllingFactionId == winner.Id);
        var seized = loser.ControlledRegions.Select(s => Regions[int.Parse(s)])
            .OrderByDescending(BordersWinner).ThenBy(r => r.Id).Take(take).ToList();

        foreach (var region in seized)
        {
            loser.ControlledRegions.Remove(region.Id.ToString());
            Claim(region, winner);
            var seizeTags = new List<string> { "territory", "war" };
            Chronicle.Record(Year, "territory",
                $"{winner.Name} seize {region.Name} from {loser.Name}.",
                causes: new() { peaceEventId }, tags: seizeTags, regionId: region.Id,
                siteId: AnchorSite("territory", seizeTags, region.Id));
        }
    }

    private void DecayTension()
    {
        foreach (var key in Tension.Keys.ToList())
            Tension[key] = Math.Max(0.0, Tension[key] - Params["tension_decay_per_year"]);
    }

    public void Run(int years)
    {
        SeedWorld();
        for (int i = 0; i < years; i++) Tick();
    }
}
