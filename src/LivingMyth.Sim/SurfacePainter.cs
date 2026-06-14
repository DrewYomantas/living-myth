using System;
using System.Collections.Generic;

namespace LivingMyth.Sim;

// The atlas skin, painted in pure C#. This is a READ-MODEL over the pristine WorldSurface —
// it draws ZERO Rng and is NEVER called by World.Tick, so it cannot move the verify baseline
// (same contract as Sites/StoryGrammar/Replay). Both surfaces that show the map render through
// it: the Godot viewer (MapView builds an Rgb8 image from Paint) and the console `paint`
// command (writes the same bytes to a PNG). One source of truth ⇒ a screenshot is byte-faithful
// to the viewer.
//
// The look is the locked North Star — "stylized semi-realistic fantasy pixel diorama, a living
// atlas": a painted sea with depth, a soft coast, mottled terrain with elevation contours, and
// inked political borders. Every value is a deterministic function of cell coordinates + the
// surface's own data; nothing here is random and nothing invents a fact the sim doesn't hold.
public static class SurfacePainter
{
    // Texels per cell. The atlas is nearest-filtered, so more texels = finer painted grain
    // and room for crisp 1-texel contour/border ink lines.
    public const int TexelsPerCell = 3;
    public static int Side => WorldSurface.Size * TexelsPerCell;

    // ---- palette (mirrors the warmed atlas hexes; single-sourced HERE for the surface) ----
    private readonly record struct Col(float R, float G, float B)
    {
        public static Col Hex(string h)
        {
            int v = Convert.ToInt32(h, 16);
            return new Col(((v >> 16) & 0xff) / 255f, ((v >> 8) & 0xff) / 255f, (v & 0xff) / 255f);
        }
        public Col Mul(float s) => new(R * s, G * s, B * s);
        public Col Darken(float a) => new(R * (1f - a), G * (1f - a), B * (1f - a));
        public Col Lighten(float a) => new(R + (1f - R) * a, G + (1f - G) * a, B + (1f - B) * a);
        public Col Lerp(Col t, float w) => new(R + (t.R - R) * w, G + (t.G - G) * w, B + (t.B - B) * w);
    }

    private static readonly Col Sea = Col.Hex("22424d");
    private static readonly Col Shallows = Col.Hex("2e5560");
    private static readonly Col Land = Col.Hex("474c31");
    private static readonly Col Neutral = Col.Hex("6f6a58");
    private static readonly Col Forest = Col.Hex("3f5230");
    private static readonly Col ForestDeep = Col.Hex("36482a");
    private static readonly Col Plains = Col.Hex("5d5e38");
    private static readonly Col Highland = Col.Hex("6a665a");
    private static readonly Col Wetland = Col.Hex("495843");
    private static readonly Col River = Col.Hex("3a6a74");
    private static readonly Col CoastSand = Col.Hex("6b6a48");

    // Painted-sea band: a depth gradient from a bright shore shallow out to deep water, with a
    // pale surf line where the water meets the land. The sea reads as a painted map-table sea,
    // not a flat fill.
    private static readonly Col WaterShore = Col.Hex("3b6b72");   // lit shallows hugging the coast
    private static readonly Col WaterDeep = Col.Hex("1a333d");   // cold deep ocean
    private static readonly Col Surf = Col.Hex("83a9a6");   // the pale foam line at the shore
    private static readonly Col Sand = Col.Hex("8f8154");   // warm beach where land meets sea
    private static readonly Col InkLine = Col.Hex("23190d");   // contour + political-border ink

    private static Col FactionColor(string fid) => fid switch
    {
        "highland" => Col.Hex("6b7a99"),
        "shore" => Col.Hex("4f8f89"),
        "wood" => Col.Hex("5d8a4e"),
        _ => Neutral,
    };

    private static Col TerrainColor(SurfaceTerrain t) => t switch
    {
        SurfaceTerrain.Ocean => Sea,
        SurfaceTerrain.Shallows => Shallows,
        SurfaceTerrain.Coast => CoastSand,
        SurfaceTerrain.Plains => Plains,
        SurfaceTerrain.Forest => Forest,
        SurfaceTerrain.Highland => Highland,
        SurfaceTerrain.Wetland => Wetland,
        SurfaceTerrain.River => River,
        SurfaceTerrain.Lake => River,
        _ => Land,
    };

    private static bool IsSea(SurfaceTerrain t) => t is SurfaceTerrain.Ocean or SurfaceTerrain.Shallows;

    // The handmade grain: a stable per-texel value hash in [-0.5, 0.5].
    private static float Speckle(int x, int y)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xff) / 255f - 0.5f;
        }
    }

    // Smooth value noise in [0,1] over a lattice hash — the coarse tonal mottling that keeps a
    // whole region from reading as one flat fill (paint, not bucket-fill).
    private static float Lattice(int x, int y)
    {
        unchecked
        {
            uint h = (uint)(x * 1597334677 + y * 3812015801);
            h = (h ^ (h >> 15)) * 2246822519u;
            h = (h ^ (h >> 13)) * 3266489917u;
            return ((h ^ (h >> 16)) & 0xffff) / 65535f;
        }
    }
    private static float Smooth(float t) => t * t * (3f - 2f * t);
    private static float ValueNoise(float x, float y)
    {
        int x0 = (int)MathF.Floor(x), y0 = (int)MathF.Floor(y);
        float fx = Smooth(x - x0), fy = Smooth(y - y0);
        float a = Lattice(x0, y0), b = Lattice(x0 + 1, y0);
        float c = Lattice(x0, y0 + 1), d = Lattice(x0 + 1, y0 + 1);
        return (a + (b - a) * fx) + ((c + (d - c) * fx) - (a + (b - a) * fx)) * fy;
    }

    /// <summary>Paint the atlas into a fresh RGB byte buffer (Side×Side, 3 bytes/texel, row-major).</summary>
    public static byte[] Paint(World world)
    {
        var rgb = new byte[Side * Side * 3];
        Paint(world, rgb);
        return rgb;
    }

    /// <summary>Paint into a caller-provided RGB buffer (length Side*Side*3).</summary>
    public static void Paint(World world, byte[] rgb)
    {
        var surface = world.Surface;
        int N = WorldSurface.Size;
        const int TS = TexelsPerCell;
        int S = Side;
        const float ContourStep = 0.14f;   // elevation band height between contour ink lines

        bool InB(int x, int y) => x >= 0 && y >= 0 && x < N && y < N;
        bool IsSeaAt(int x, int y) => !InB(x, y) || IsSea(surface.TerrainAt(x, y));
        // Land = anything the island holds above the waterline (rivers/lakes count as land-bound
        // for the coast/depth fields — they're inland threads, not the open sea).
        bool IsLandAt(int x, int y) => InB(x, y) && !IsSea(surface.TerrainAt(x, y));
        float Elev(int x, int y) => InB(x, y) ? MathF.Max(0f, surface.ElevationAt(x, y)) : 0f;
        int Faction(int x, int y)
        {
            if (!InB(x, y)) return -1;
            int rr = surface.RegionAt(x, y);
            return rr >= 0 && rr < world.Regions.Count && world.Regions[rr].ControllingFactionId is string f
                ? f.GetHashCode() : -1;
        }

        // Distance fields (bounded BFS, once per rebuild): how far each cell is from the open sea
        // (coast banding on land) and from land (depth gradient + surf in the water).
        int[] distToSea = Bfs(N, (x, y) => IsSeaAt(x, y));
        int[] distToLand = Bfs(N, (x, y) => IsLandAt(x, y));

        for (int cy = 0; cy < N; cy++)
            for (int cx = 0; cx < N; cx++)
            {
                int idx = cy * N + cx;
                var t = surface.TerrainAt(cx, cy);
                bool isSea = IsSea(t);
                float elev = surface.ElevationAt(cx, cy);

                // ---- the cell's painted base color (everything but the per-texel grain) ----
                Col cellCol;
                bool forestCell = false;

                if (isSea)
                {
                    // Painted sea: a depth ramp from lit shore-shallows out to cold deep water,
                    // a low-frequency swell for movement, and a pale surf line at the land's edge.
                    float depth = Math.Clamp(distToLand[idx] / 7f, 0f, 1f);
                    cellCol = WaterShore.Lerp(WaterDeep, depth);
                    float swell = ValueNoise(cx * 0.16f, cy * 0.16f) - 0.5f;
                    cellCol = cellCol.Mul(1f + swell * 0.10f);
                    if (distToLand[idx] <= 1) cellCol = cellCol.Lerp(Surf, 0.30f);   // the surf line
                }
                else if (t is SurfaceTerrain.River or SurfaceTerrain.Lake)
                {
                    cellCol = River.Mul(1f + (ValueNoise(cx * 0.3f, cy * 0.3f) - 0.5f) * 0.10f);
                }
                else
                {
                    var baseCol = TerrainColor(t);
                    forestCell = t == SurfaceTerrain.Forest;

                    // Relief: light from the NW. The gradient of the height field lifts sunlit
                    // slopes and sinks the shadowed ones — the land gains real form, not a flat tint.
                    float relief = ((Elev(cx - 1, cy) - Elev(cx + 1, cy))
                                    + (Elev(cx, cy - 1) - Elev(cx, cy + 1))) * 1.7f;
                    float amb = Math.Clamp(elev - 0.18f, -0.2f, 0.6f) * 0.12f;   // mild height tint
                    float mott = (ValueNoise(cx * 0.14f, cy * 0.14f) - 0.5f) * 0.13f;   // coarse paint
                    cellCol = baseCol.Mul(1f + amb + relief + mott);

                    // A warm beach where the land meets the sea — a soft painterly coast, not a
                    // hard darkened rim.
                    if (distToSea[idx] == 0) cellCol = cellCol.Lerp(Sand, 0.42f);
                    else if (distToSea[idx] == 1) cellCol = cellCol.Lerp(Sand, 0.18f);

                    // Faction cloth wash — the land wears its holder's color, restrained.
                    int rid = surface.RegionAt(cx, cy);
                    if (rid >= 0 && rid < world.Regions.Count
                        && world.Regions[rid].ControllingFactionId is string fid)
                        cellCol = cellCol.Lerp(FactionColor(fid), 0.12f);
                }

                // ---- crisp 1-texel ink lines on the cell's right/bottom edges ----
                // Contours trace the elevation bands (a topographic, illustrated atlas); political
                // ink traces where one people's land meets another's. Land only; sea stays open.
                bool contourR = false, contourD = false, borderR = false, borderD = false;
                if (!isSea)
                {
                    int band = (int)MathF.Floor(elev / ContourStep);
                    if (IsLandAt(cx + 1, cy))
                    {
                        if ((int)MathF.Floor(surface.ElevationAt(cx + 1, cy) / ContourStep) != band) contourR = true;
                        int fa = Faction(cx, cy), fb = Faction(cx + 1, cy);
                        if (fa != fb && (fa != -1 || fb != -1)) borderR = true;
                    }
                    if (IsLandAt(cx, cy + 1))
                    {
                        if ((int)MathF.Floor(surface.ElevationAt(cx, cy + 1) / ContourStep) != band) contourD = true;
                        int fa = Faction(cx, cy), fb = Faction(cx, cy + 1);
                        if (fa != fb && (fa != -1 || fb != -1)) borderD = true;
                    }
                }

                for (int sy = 0; sy < TS; sy++)
                    for (int sx = 0; sx < TS; sx++)
                    {
                        int px = cx * TS + sx, py = cy * TS + sy;
                        var col = cellCol;
                        // Forest canopy in two clustered tones, then a soft handmade grain.
                        if (forestCell && Speckle(px * 7, py * 5) > 0.12f) col = col.Lerp(ForestDeep, 0.55f);
                        col = col.Mul(1f + Speckle(px, py) * 0.075f);

                        // Edge ink falls on the last texel row/column so the lines stay hairline.
                        bool eR = sx == TS - 1, eD = sy == TS - 1;
                        if ((borderR && eR) || (borderD && eD)) col = col.Lerp(InkLine, 0.34f);
                        else if ((contourR && eR) || (contourD && eD)) col = col.Lerp(InkLine, 0.11f);

                        Put(rgb, px, py, S, col);
                    }
            }
    }

    // Multi-source BFS distance over the cell grid; sources are the cells the predicate marks.
    private static int[] Bfs(int n, Func<int, int, bool> isSource)
    {
        var dist = new int[n * n];
        Array.Fill(dist, -1);
        var q = new Queue<int>();
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                if (isSource(x, y)) { dist[y * n + x] = 0; q.Enqueue(y * n + x); }
        while (q.Count > 0)
        {
            int i = q.Dequeue(), cx = i % n, cy = i / n, d = dist[i];
            void Step(int x, int y)
            {
                if (x < 0 || y < 0 || x >= n || y >= n) return;
                int j = y * n + x;
                if (dist[j] == -1) { dist[j] = d + 1; q.Enqueue(j); }
            }
            Step(cx - 1, cy); Step(cx + 1, cy); Step(cx, cy - 1); Step(cx, cy + 1);
        }
        for (int i = 0; i < dist.Length; i++) if (dist[i] < 0) dist[i] = n;   // unreachable → "far"
        return dist;
    }

    private static void Put(byte[] rgb, int x, int y, int stride, Col c)
    {
        int i = (y * stride + x) * 3;
        rgb[i] = Byte(c.R);
        rgb[i + 1] = Byte(c.G);
        rgb[i + 2] = Byte(c.B);
    }

    private static byte Byte(float v) => (byte)Math.Clamp((int)(v * 255f + 0.5f), 0, 255);
}
