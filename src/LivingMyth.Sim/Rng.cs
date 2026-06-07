namespace LivingMyth.Sim;

/// <summary>
/// Deterministic randomness. The whole game leans on this: a single seed must reproduce
/// the exact same history every time, so nothing in the sim ever uses an ambient RNG —
/// it all flows through one seeded Rng. This is also what makes the divergence test
/// possible: same seed, same world, change one nudge, watch it diverge.
///
/// Uses an explicit SplitMix64 stream so determinism does NOT depend on the .NET runtime's
/// internal System.Random algorithm. Not bit-compatible with Python's Mersenne Twister
/// (by design — we only require stable, reproducible results within C#).
/// </summary>
public sealed class Rng
{
    private ulong _state;

    public int Seed { get; }

    public Rng(int seed)
    {
        Seed = seed;
        _state = unchecked((ulong)seed + 0x9E3779B97F4A7C15UL);
    }

    private ulong NextULong()
    {
        unchecked
        {
            _state += 0x9E3779B97F4A7C15UL;
            ulong z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    /// <summary>A double in [0, 1) with full 53-bit resolution.</summary>
    private double NextDouble() => (NextULong() >> 11) * (1.0 / 9007199254740992.0);

    /// <summary>True with probability p (0.0 to 1.0).</summary>
    public bool Chance(double p) => NextDouble() < p;

    /// <summary>Pick one item from a list.</summary>
    public T Pick<T>(IReadOnlyList<T> seq) => seq[(int)(NextULong() % (ulong)seq.Count)];

    /// <summary>Whole number between a and b, inclusive.</summary>
    public int RandInt(int a, int b) => a + (int)(NextULong() % (ulong)(b - a + 1));

    /// <summary>Shuffle a list in place (Fisher–Yates, high to low).</summary>
    public void Shuffle<T>(IList<T> seq)
    {
        for (int i = seq.Count - 1; i > 0; i--)
        {
            int j = (int)(NextULong() % (ulong)(i + 1));
            (seq[i], seq[j]) = (seq[j], seq[i]);
        }
    }
}
