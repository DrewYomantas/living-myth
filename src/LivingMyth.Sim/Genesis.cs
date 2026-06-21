namespace LivingMyth.Sim;

/// <summary>
/// A player-authored people for the genesis builder. Plain data the Godot builder serializes and
/// the Sim consumes at boot — authored content like config.json/names.json, so the Sim stays
/// Godot-free. When no spec is given, the world boots from config exactly as before, so the verify
/// baseline (598/751/809/1065) cannot move. With a spec, the authored people is added and the
/// world's other (rival) peoples generate as usual; the result is deterministic per (seed + spec).
/// </summary>
public sealed class GenesisSpec
{
    public string PeopleName { get; set; } = "the People";
    public string Homeland { get; set; } = "their homeland";
    public string HomelandTerrain { get; set; } = "highland";   // forest | highland | coast | plains
    public string NamingStyle { get; set; } = "highland";        // an existing names.json given-names key
    public int StartPop { get; set; } = 18;

    // The ethos that drives this people's customs, clashes, and wars. Keys: valor/piety/cunning/harmony,
    // each in [0,1]. Becomes the people's drift baseline, not just its opening values.
    public Dictionary<string, double> Axes { get; set; } = new();

    public string? FaithName { get; set; }    // null → a default faith name is derived
    public string? FaithDeity { get; set; }

    // Optional pre-adopted customs (warlike/devout/scheming/peaceable) so the authored ethos reads
    // from year one; if empty, customs still emerge naturally once an axis runs high.
    public List<string> StartingCustoms { get; set; } = new();

    // Optional authored founding lineage (leader first); if empty, founders generate as usual.
    public List<GenesisFounder> Founders { get; set; } = new();
}

/// <summary>One authored founding soul. Refs are indices into GenesisSpec.Founders, for kinship.</summary>
public sealed class GenesisFounder
{
    public string Name { get; set; } = "";
    public string Sex { get; set; } = "m";    // "m" | "f"
    public int Age { get; set; } = 30;
    public bool Leader { get; set; }
    public int? SpouseRef { get; set; }
    public List<int> ChildRefs { get; set; } = new();
}
