using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LivingMyth.Sim;

/// <summary>
/// Authored content, loaded from the JSON data files (kept separate from logic exactly
/// like the Python prototype's data/ folder). POCOs mirror data/config.json and
/// data/names.json; snake_case JSON maps to PascalCase here.
/// </summary>
public sealed class FactionDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Culture { get; set; } = "";
    public string Homeland { get; set; } = "";
    public int StartPop { get; set; }
}

public sealed class ConfigData
{
    public int StartYear { get; set; }
    public List<FactionDef> Factions { get; set; } = new();

    [JsonPropertyName("params")]
    public Dictionary<string, double> Params { get; set; } = new();
}

public sealed class ReligionDef
{
    public string Name { get; set; } = "";
    public string Deity { get; set; } = "";
}

public sealed class NamesData
{
    public List<string> IslandNames { get; set; } = new();
    public Dictionary<string, ReligionDef> Religions { get; set; } = new();
    public Dictionary<string, List<string>> FaithFragments { get; set; } = new();
    public Dictionary<string, Dictionary<string, List<string>>> GivenNames { get; set; } = new();
}

public static class DataLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Locate the data folder shipped next to the Sim assembly.</summary>
    public static string DataDir()
    {
        var asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        return Path.Combine(asmDir, "data");
    }

    public static (ConfigData config, NamesData names) Load(string? dataDir = null)
    {
        dataDir ??= DataDir();
        var config = JsonSerializer.Deserialize<ConfigData>(
            File.ReadAllText(Path.Combine(dataDir, "config.json")), Options)!;
        var names = JsonSerializer.Deserialize<NamesData>(
            File.ReadAllText(Path.Combine(dataDir, "names.json")), Options)!;
        return (config, names);
    }
}
