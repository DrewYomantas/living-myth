using Godot;
using LivingMyth.Sim;

// Viewer-derived "place seeds": a deterministic visual identity for each region — hill-fort,
// grove, ford, ruins — so the map whispers "there are places here" before the sim has real
// settlements. Pure presentation: derived from existing region/faction state plus a stable
// FNV-style hash of (world seed, region id). No sim state is added and the sim's Rng is never
// consumed, so this can never move the verify baseline.
public static class PlaceSeeds
{
    public enum Kind { HillFort, WatchPost, Cairn, Grove, Shrine, Camp, Ford, FarmCluster, MarketHamlet, Ruins }

    // FNV-1a over the ids with a final avalanche — stable across runs and processes (never
    // string.GetHashCode, which .NET randomizes per process).
    public static uint Hash(int worldSeed, int regionId, int salt = 0)
    {
        unchecked
        {
            uint h = 2166136261u;
            h = (h ^ (uint)worldSeed) * 16777619u;
            h = (h ^ (uint)regionId) * 16777619u;
            h = (h ^ (uint)salt) * 16777619u;
            h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
            return h;
        }
    }

    // Terrain + held/wild decide the family; the hash picks within it. Claimed land reads
    // settled (forts, hamlets, farms), wilderness reads remembered (cairns, ruins, wild groves).
    public static Kind KindOf(World world, Region r)
    {
        uint h = Hash(world.Seed, r.Id);
        bool held = r.ControllingFactionId is not null;
        return r.TerrainType switch
        {
            "highland" => held ? Pick(h, Kind.HillFort, Kind.WatchPost) : Pick(h, Kind.Cairn, Kind.Ruins),
            "forest" => held ? Pick(h, Kind.Grove, Kind.Shrine, Kind.Camp) : Pick(h, Kind.Grove, Kind.Ruins),
            "coast" => held ? Pick(h, Kind.Ford, Kind.MarketHamlet) : Pick(h, Kind.Ford, Kind.Ruins),
            _ => held ? Pick(h, Kind.FarmCluster, Kind.MarketHamlet) : Pick(h, Kind.Camp, Kind.Ruins),
        };
    }

    private static Kind Pick(uint h, params Kind[] options) => options[h % (uint)options.Length];

    public static string Label(Kind k) => k switch
    {
        Kind.HillFort => "hill-fort",
        Kind.WatchPost => "watch post",
        Kind.Cairn => "old cairn",
        Kind.Grove => "sacred grove",
        Kind.Shrine => "wayside shrine",
        Kind.Camp => "camp",
        Kind.Ford => "ford",
        Kind.FarmCluster => "farm cluster",
        Kind.MarketHamlet => "market hamlet",
        _ => "old ruins",
    };

    // Stable [-1,1]² nudge off the region centre, so markers don't all sit dead-centre under
    // hover labels and overlapping regions don't stack their glyphs.
    public static Vector2 Offset(int worldSeed, int regionId)
    {
        uint h = Hash(worldSeed, regionId, 1);
        return new Vector2((h & 0xff) / 255f * 2f - 1f, ((h >> 8) & 0xff) / 255f * 2f - 1f);
    }
}
