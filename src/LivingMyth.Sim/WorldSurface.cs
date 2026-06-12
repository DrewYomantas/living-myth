namespace LivingMyth.Sim;

/// <summary>
/// The island's editable skin: a deterministic cell grid (terrain / elevation / vegetation /
/// region bridge) generated from the world seed and the region layout — the renderable,
/// terraformable world surface the concept art calls for. Three binding rules:
///
/// 1. ZERO sim coupling. Generation uses pure coordinate hashes (not even an Rng stream),
///    the tick never reads a cell, and nothing here ever calls Record() — so creating or
///    editing a surface can never move the verify baseline. Regions remain the sim's
///    spatial truth; cells are the world's skin, bridged to regions by nearest-seat.
/// 2. Deterministic and replayable. Same seed + same edit sequence ⇒ identical state
///    (StateHash proves it in the `divine` console gate). Edits are journaled.
/// 3. Edits are explicit state, not visual lies. The god-hand APIs on World wrap every
///    edit in a recorded chronicle event; the viewer redraws from Version, never from
///    its own imagination.
/// </summary>
public enum SurfaceTerrain : byte
{
    Ocean, Shallows, Coast, Plains, Forest, Highland, Wetland, River, Lake,
}

public sealed class WorldSurface
{
    public const int Size = 96;                      // cells per side; the island fits [0,1]²
    public int Seed { get; }
    /// <summary>Bumps on every edit — the viewer's rebuild-the-texture signal.</summary>
    public int Version { get; private set; }

    private readonly SurfaceTerrain[] _terrain = new SurfaceTerrain[Size * Size];
    private readonly float[] _elevation = new float[Size * Size];   // 0 ocean floor … ~1 peaks
    private readonly float[] _vegetation = new float[Size * Size];  // 0 bare … 1 dense forest
    private readonly short[] _regionOf = new short[Size * Size];    // nearest region (land), -1 water

    /// <summary>The journal of player terraforming, in order — replaying it on a fresh
    /// surface of the same seed reproduces the state exactly.</summary>
    public List<(string Kind, int RegionId)> Edits { get; } = new();

    public SurfaceTerrain TerrainAt(int cx, int cy) => _terrain[Idx(cx, cy)];
    public float ElevationAt(int cx, int cy) => _elevation[Idx(cx, cy)];
    public float VegetationAt(int cx, int cy) => _vegetation[Idx(cx, cy)];
    public int RegionAt(int cx, int cy) => _regionOf[Idx(cx, cy)];

    private static int Idx(int cx, int cy) => cy * Size + cx;
    private static bool In(int c) => c >= 0 && c < Size;

    public static (int cx, int cy) CellOf(float nx, float ny)
        => (Math.Clamp((int)(nx * Size), 0, Size - 1), Math.Clamp((int)(ny * Size), 0, Size - 1));

    /// <summary>Region under a normalized map point, -1 over open water — the viewer's
    /// land hit-test (clicks finally land on the terrain itself, not abstract circles).</summary>
    public int RegionAtNorm(float nx, float ny)
    {
        if (nx < 0 || nx >= 1 || ny < 0 || ny >= 1) return -1;
        var (cx, cy) = CellOf(nx, ny);
        return _regionOf[Idx(cx, cy)];
    }

    private static bool IsWater(SurfaceTerrain t)
        => t is SurfaceTerrain.Ocean or SurfaceTerrain.Shallows or SurfaceTerrain.River or SurfaceTerrain.Lake;

    // ---- deterministic coordinate hash noise (no sequential state, order-independent) ----

    private static float Hash01(int seed, int x, int y)
    {
        unchecked
        {
            uint h = (uint)seed * 0x9E3779B1u;
            h ^= (uint)x * 0x85EBCA77u;
            h = (h ^ (h >> 13)) * 0xC2B2AE3Du;
            h ^= (uint)y * 0x27D4EB2Fu;
            h = (h ^ (h >> 16)) * 0x165667B1u;
            return (h ^ (h >> 15)) / 4294967296f;
        }
    }

    private static float ValueNoise(int seed, float fx, float fy)
    {
        int x0 = (int)MathF.Floor(fx), y0 = (int)MathF.Floor(fy);
        float tx = fx - x0, ty = fy - y0;
        tx = tx * tx * (3f - 2f * tx);   // smoothstep
        ty = ty * ty * (3f - 2f * ty);
        float a = Hash01(seed, x0, y0), b = Hash01(seed, x0 + 1, y0);
        float c = Hash01(seed, x0, y0 + 1), d = Hash01(seed, x0 + 1, y0 + 1);
        return (a + (b - a) * tx) + ((c + (d - c) * tx) - (a + (b - a) * tx)) * ty;
    }

    private static float Fbm(int seed, float fx, float fy, int octaves)
    {
        float sum = 0f, amp = 0.5f, freq = 1f, norm = 0f;
        for (int o = 0; o < octaves; o++)
        {
            sum += ValueNoise(seed + o * 7919, fx * freq, fy * freq) * amp;
            norm += amp;
            amp *= 0.5f;
            freq *= 2f;
        }
        return sum / norm;
    }

    // ---- generation ----

    public WorldSurface(int seed, IReadOnlyList<Region> regions)
    {
        Seed = seed;
        int sElev = seed * 31 + 11, sMoist = seed * 53 + 29;

        // Pass 1 — height: a radial island with a noisy coastline, shaped by the regions
        // the sim already placed (highland seats lift the land, coast seats lower it).
        for (int cy = 0; cy < Size; cy++)
            for (int cx = 0; cx < Size; cx++)
            {
                float nx = (cx + 0.5f) / Size, ny = (cy + 0.5f) / Size;
                float radial = MathF.Sqrt((nx - 0.5f) * (nx - 0.5f) + (ny - 0.5f) * (ny - 0.5f));
                float h = 0.60f - radial * 1.18f + (Fbm(sElev, nx * 3.6f, ny * 3.6f, 4) - 0.5f) * 0.46f;
                foreach (var r in regions)
                {
                    float dx = nx - r.X, dy = ny - r.Y;
                    float w = MathF.Exp(-(dx * dx + dy * dy) / 0.012f);
                    h += r.TerrainType switch
                    {
                        "highland" => 0.20f * w,
                        "coast" => -0.09f * w,
                        "forest" => 0.03f * w,
                        _ => 0.01f * w,
                    };
                }
                _elevation[Idx(cx, cy)] = h;
            }

        // Every region seat must stand on land — the sim says people hold these places.
        foreach (var r in regions)
        {
            var (scx, scy) = CellOf(r.X, r.Y);
            for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                {
                    int x = scx + dx, y = scy + dy;
                    if (!In(x) || !In(y)) continue;
                    float floor = 0.10f - 0.025f * MathF.Max(Math.Abs(dx), Math.Abs(dy));
                    if (_elevation[Idx(x, y)] < floor) _elevation[Idx(x, y)] = floor;
                }
        }

        // Pass 2 — terrain classes + vegetation + region bridge.
        for (int cy = 0; cy < Size; cy++)
            for (int cx = 0; cx < Size; cx++)
            {
                int i = Idx(cx, cy);
                float nx = (cx + 0.5f) / Size, ny = (cy + 0.5f) / Size;
                float h = _elevation[i];
                if (h < -0.045f) { _terrain[i] = SurfaceTerrain.Ocean; _regionOf[i] = -1; continue; }
                if (h < 0f) { _terrain[i] = SurfaceTerrain.Shallows; _regionOf[i] = -1; continue; }

                // Land: bridge to the nearest region seat (the sim's spatial truth).
                short best = -1;
                float bestD = float.MaxValue;
                foreach (var r in regions)
                {
                    float dx = nx - r.X, dy = ny - r.Y;
                    float d = dx * dx + dy * dy;
                    if (d < bestD) { bestD = d; best = (short)r.Id; }
                }
                _regionOf[i] = best;

                float moist = Fbm(sMoist, nx * 5.2f, ny * 5.2f, 3);
                float forestPull = 0f;
                foreach (var r in regions)
                {
                    if (r.TerrainType != "forest") continue;
                    float dx = nx - r.X, dy = ny - r.Y;
                    forestPull += MathF.Exp(-(dx * dx + dy * dy) / 0.010f);
                }
                float veg = Math.Clamp(moist * 0.85f + MathF.Min(forestPull, 1f) * 0.42f - h * 0.30f, 0f, 1f);
                _vegetation[i] = veg;

                _terrain[i] =
                    h < 0.045f ? SurfaceTerrain.Coast
                    : h > 0.46f ? SurfaceTerrain.Highland
                    : veg > 0.52f ? SurfaceTerrain.Forest
                    : (h < 0.085f && moist > 0.62f) ? SurfaceTerrain.Wetland
                    : SurfaceTerrain.Plains;
            }

        CarveRivers();
    }

    /// <summary>Up to three rivers: the highest inland cell in each angular third of the
    /// island walks downhill to the sea (or pools into a small lake at a dead end). Pure
    /// gradient descent over the generated heights — deterministic, no draws.</summary>
    private void CarveRivers()
    {
        var sources = new (int cx, int cy, float h)[3];
        for (int k = 0; k < 3; k++) sources[k] = (-1, -1, float.MinValue);
        for (int cy = 0; cy < Size; cy++)
            for (int cx = 0; cx < Size; cx++)
            {
                int i = Idx(cx, cy);
                if (_elevation[i] < 0.40f || IsWater(_terrain[i])) continue;
                float ang = MathF.Atan2(cy - Size / 2f, cx - Size / 2f) + MathF.PI;   // 0..2π
                int third = Math.Clamp((int)(ang / (MathF.Tau / 3f)), 0, 2);
                if (_elevation[i] > sources[third].h) sources[third] = (cx, cy, _elevation[i]);
            }

        foreach (var (sx, sy, _) in sources)
        {
            if (sx < 0) continue;
            int cx = sx, cy = sy;
            for (int step = 0; step < 500; step++)
            {
                int i = Idx(cx, cy);
                if (_terrain[i] is SurfaceTerrain.Ocean or SurfaceTerrain.Shallows) break;
                if (_terrain[i] != SurfaceTerrain.River) _terrain[i] = SurfaceTerrain.River;

                int bx = -1, by = -1;
                float bh = _elevation[i];
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int x = cx + dx, y = cy + dy;
                        if (!In(x) || !In(y)) continue;
                        if (_terrain[Idx(x, y)] == SurfaceTerrain.River) continue;   // never loop back
                        if (_elevation[Idx(x, y)] < bh) { bh = _elevation[Idx(x, y)]; bx = x; by = y; }
                    }
                if (bx < 0)
                {
                    // Dead end: the water pools. A small honest lake, then stop.
                    _terrain[i] = SurfaceTerrain.Lake;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int x = cx + dx, y = cy + dy;
                            if (In(x) && In(y) && !IsWater(_terrain[Idx(x, y)]) && _elevation[Idx(x, y)] < _elevation[i] + 0.02f)
                                _terrain[Idx(x, y)] = SurfaceTerrain.Wetland;
                        }
                    break;
                }
                cx = bx; cy = by;
            }
        }
    }

    // ---- terraforming edits (the god-hand pathway; World wraps these in recorded events) ----

    /// <summary>Raise vegetation around a region's seat; open ground past the threshold
    /// becomes forest. Returns cells changed. Deterministic; journaled; bumps Version.</summary>
    public int SeedForestAt(int regionId, float seatNx, float seatNy)
    {
        var (scx, scy) = CellOf(seatNx, seatNy);
        const float R = 4.6f;
        int changed = 0;
        for (int dy = -5; dy <= 5; dy++)
            for (int dx = -5; dx <= 5; dx++)
            {
                int x = scx + dx, y = scy + dy;
                if (!In(x) || !In(y)) continue;
                float d = MathF.Sqrt(dx * dx + dy * dy);
                if (d > R) continue;
                int i = Idx(x, y);
                if (IsWater(_terrain[i]) || _terrain[i] == SurfaceTerrain.Highland) continue;
                float before = _vegetation[i];
                _vegetation[i] = Math.Clamp(before + 0.42f * (1f - d / R), 0f, 1f);
                if (_vegetation[i] > before + 0.001f) changed++;
                if (_terrain[i] is SurfaceTerrain.Plains or SurfaceTerrain.Coast && _vegetation[i] > 0.5f)
                    _terrain[i] = SurfaceTerrain.Forest;
            }
        if (changed > 0) { Edits.Add(("forest", regionId)); Version++; }
        return changed;
    }

    /// <summary>Call a spring near a region's seat: the lowest open cell becomes a small
    /// lake, ringed by wetland. Returns cells changed. Deterministic; journaled.</summary>
    public int CallSpringAt(int regionId, float seatNx, float seatNy)
    {
        var (scx, scy) = CellOf(seatNx, seatNy);
        int lx = -1, ly = -1;
        float lh = float.MaxValue;
        for (int dy = -3; dy <= 3; dy++)
            for (int dx = -3; dx <= 3; dx++)
            {
                int x = scx + dx, y = scy + dy;
                if (!In(x) || !In(y)) continue;
                int i = Idx(x, y);
                if (IsWater(_terrain[i])) continue;
                if (_elevation[i] < lh) { lh = _elevation[i]; lx = x; ly = y; }
            }
        if (lx < 0) return 0;

        int changed = 0;
        _terrain[Idx(lx, ly)] = SurfaceTerrain.Lake;
        changed++;
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int x = lx + dx, y = ly + dy;
                if (!In(x) || !In(y)) continue;
                int i = Idx(x, y);
                if (IsWater(_terrain[i]) || _terrain[i] == SurfaceTerrain.Highland) continue;
                bool corner = dx != 0 && dy != 0;
                _terrain[i] = corner ? SurfaceTerrain.Wetland : SurfaceTerrain.Lake;
                changed++;
            }
        Edits.Add(("spring", regionId));
        Version++;
        return changed;
    }

    /// <summary>FNV-1a over the full editable state — the determinism fingerprint the
    /// `divine` gate compares across identical runs.</summary>
    public ulong StateHash()
    {
        unchecked
        {
            ulong h = 14695981039346656037UL;
            void Mix(byte b) { h ^= b; h *= 1099511628211UL; }
            for (int i = 0; i < _terrain.Length; i++)
            {
                Mix((byte)_terrain[i]);
                Mix((byte)Math.Clamp((int)(_vegetation[i] * 255f), 0, 255));
                Mix((byte)(_regionOf[i] & 0xff));
                Mix((byte)((_regionOf[i] >> 8) & 0xff));
            }
            return h;
        }
    }
}
