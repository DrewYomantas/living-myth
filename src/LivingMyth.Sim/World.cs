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
    public WorldSurface Surface => _surface ??= new WorldSurface(Seed, Regions);

    private sealed class War
    {
        public (string, string) Pair;
        public int YearsLeft;
        public Event DeclaredEvent = null!;
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
            Chronicle.Record(Year, "territory",
                $"{fac.Name} hold the lands of {string.Join(", ", owned.Select(r => r.Name))}.",
                participants: fac.LeaderId is int lid ? new() { lid } : null,
                tags: new() { "territory", "founding" }, regionId: owned[0].Id);

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

    private void Economy()
    {
        // Per-faction prosperity random-walks around a neutral 1.0 (mean-reverting). Threshold
        // crossings emit one famine/boom event per episode (the InFamine/InBoom flags are the
        // hysteresis). All draws happen in FactionsSorted() order to stay deterministic.
        foreach (var fid in FactionsSorted())
        {
            var f = Factions[fid];
            int step = Rng.RandInt(-1, 1);
            f.Prosperity += step * Params["economy_prosperity_step"];
            f.Prosperity += (1.0 - f.Prosperity) * Params["economy_prosperity_revert"];
            // God-hand pressure: a flat bias on the walk while the window holds — same
            // draws, gently shifted values. Inert (0-width windows) without player acts.
            if (f.ProtectUntilYear > Year)
                f.Prosperity += Params.GetValueOrDefault("protect_prosperity_bias", 0.02);
            if (f.DoomUntilYear > Year)
                f.Prosperity -= Params.GetValueOrDefault("doom_prosperity_drag", 0.02);
            f.Prosperity = Math.Clamp(f.Prosperity, 0.0, 2.0);

            if (!f.InFamine && f.Prosperity < Params["famine_threshold"])
            {
                f.InFamine = true;
                // A famine arriving under an active doom or protection cause-links to the
                // divine act, honestly: the doom truly pressed it down ("therefore"); the
                // protection truly stood against it and was overcome ("but").
                var divineCauses = new List<int>();
                if (f.DoomUntilYear > Year && f.DoomEventId is int de) divineCauses.Add(de);
                if (f.ProtectUntilYear > Year && f.ProtectEventId is int pe) divineCauses.Add(pe);
                f.FamineEvent = Chronicle.Record(Year, "famine",
                    $"Famine grips {f.Name}.",
                    participants: f.LeaderId is int fl ? new() { fl } : null,
                    causes: divineCauses.Count > 0 ? divineCauses : null,
                    tags: new() { "economy", "scarcity" });
                // A starving people leans on its neighbours: each famine onset pushes aggression
                // outward once, toward every other people that still has living members.
                foreach (var otherId in FactionsSorted())
                    if (otherId != fid && Factions[otherId].Members.Count > 0)
                        AddTension(fid, otherId, 1.5, f.FamineEvent);
            }
            else if (f.InFamine && f.Prosperity >= Params["famine_threshold"])
            {
                f.InFamine = false;
                f.FamineEvent = null;
            }

            // A boom is one sustained high-prosperity spell, so (unlike famine, which flickers near
            // its floor) it re-emits a "plenty continues" beat every boom_beat_years — that lets a
            // long golden age accumulate enough events for DetectGoldenAge to recognise it.
            if (f.Prosperity > Params["boom_threshold"])
            {
                if (!f.InBoom || Year - f.LastBoomYear >= (int)Params["boom_beat_years"])
                {
                    bool onset = !f.InBoom;
                    f.InBoom = true;
                    f.LastBoomYear = Year;
                    Chronicle.Record(Year, "boom",
                        onset ? $"A season of plenty blesses {f.Name}." : $"Plenty still blesses {f.Name}.",
                        participants: f.LeaderId is int bl ? new() { bl } : null,
                        tags: new() { "economy", "boom" });
                }
            }
            else if (f.InBoom && f.Prosperity <= Params["boom_threshold"])
            {
                f.InBoom = false;
            }
        }

        // Trade: prospering neighbours exchange goods, which lifts both and eases tension between
        // them (couples to the war system). Sorted-pair loop mirrors ReligiousFriction.
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

                fa.Prosperity = Math.Min(2.0, fa.Prosperity + Params["economy_prosperity_step"]);
                fb.Prosperity = Math.Min(2.0, fb.Prosperity + Params["economy_prosperity_step"]);
                var key = PairKey(fa.Id, fb.Id);
                Tension[key] = Math.Max(0.0, Tension.GetValueOrDefault(key) - Params["trade_tension_reduction"]);
            }
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
                    var ev = Chronicle.Record(Year, "custom",
                        $"{f.Name} become {CustomBecome[custom]}.",
                        participants: leader, tags: new() { "culture", custom },
                        regionId: PrimaryRegion(f));
                    f.CustomOriginEvent[custom] = ev.Id;
                }
                else if (held && v <= Params["culture_identity_drop"])
                {
                    Chronicle.Record(Year, "custom",
                        $"{f.Name} {CustomFade[custom]}.",
                        participants: leader, causes: new() { f.CustomOriginEvent[custom] },
                        tags: new() { "culture", "fade", custom }, regionId: PrimaryRegion(f));
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
        ProcessWars();
        Deaths();
        Crime();
        ForbiddenRomance();
        Marriages();
        Births();
        DoReligion();
        Culture();
        Gossip();
        MaybeDeclareWars();
        DecayTension();
        ReleaseExtinctLands();
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
            Chronicle.Record(Year, "territory",
                $"{fac.Name} are gone; their holds fall silent and the wild creeps back over {string.Join(", ", freed.Select(r => r.Name))}.",
                causes: fac.LastDeathEventId is int d ? new() { d } : null,
                tags: new() { "territory", "abandonment" }, regionId: freed[0].Id);
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
                if (p.Cursed && CurseEvent is not null)
                    Kill(p, reason: "as the old curse takes them", cause: CurseEvent);
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
            var ev = Chronicle.Record(Year, "war", text,
                causes: grievanceIds.Count > 0 ? new List<int>(grievanceIds) : null, tags: tags);
            _activeWars.Add(new War { Pair = key, YearsLeft = Rng.RandInt(1, 2), DeclaredEvent = ev });
            Tension[key] = 1.0;
        }
    }

    private void ProcessWars()
    {
        foreach (var war in _activeWars.ToList())
        {
            string fa = war.Pair.Item1, fb = war.Pair.Item2;
            var decl = war.DeclaredEvent;
            foreach (var fid in new[] { fa, fb })
            {
                var members = FactionMembers(fid).Where(p => p.Age(Year) >= 16).ToList();
                int casualties = Rng.RandInt(0, 2);
                for (int c = 0; c < casualties; c++)
                {
                    if (members.Count == 0) break;
                    var fallen = Rng.Pick(members.OrderBy(p => p.Id).ToList());
                    members.Remove(fallen);
                    Kill(fallen, reason: "in the war", cause: decl);
                }
            }
            war.YearsLeft -= 1;
            if (war.YearsLeft <= 0)
            {
                _activeWars.Remove(war);
                var peace = Chronicle.Record(Year, "peace",
                    $"{Factions[fa].Name} and {Factions[fb].Name} make peace, though the grudge lingers.",
                    causes: new() { decl.Id }, tags: new() { "war", "peace" });
                TransferTerritory(Factions[fa], Factions[fb], peace.Id);
                Tension[war.Pair] = 2.0;
            }
        }
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
            Chronicle.Record(Year, "territory",
                $"{winner.Name} seize {region.Name} from {loser.Name}.",
                causes: new() { peaceEventId }, tags: new() { "territory", "war" }, regionId: region.Id);
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
