using Godot;
using LivingMyth.Sim;

// Viewer-derived "person sigils": a deterministic visual identity for each soul — the same
// mark and tint everywhere their name appears, so recognizing your cast stops depending on
// reading name-strings in a stream. The Cast milestone's V1 stand-in for portraits (Batch 2
// references show where this goes). Pure presentation, the PlaceSeeds pattern: a stable FNV
// hash of (world seed, person id) picks from authored parts; no sim state is added and the
// sim's Rng is never consumed, so this can never move the verify baseline.
public static class PersonSigils
{
    public readonly record struct Sigil(string Glyph, Color Tint);

    // Dingbat-block marks (same block as the proven event glyphs ✦ ✺ ❧, so the font
    // fallback chain covers them), deliberately disjoint from Ui.ClassOf's set — a person
    // mark must never read as an event class.
    private static readonly string[] Glyphs =
    { "✣", "✤", "✥", "✱", "✸", "✽", "❁", "❃", "❋", "✠", "❂", "✜" };

    // Inked tints that hold on parchment — distinct from one another, dark enough for text,
    // kept apart from the event-class chip colors.
    private static readonly Color[] Tints =
    {
        new("8a3324"), new("8a5d12"), new("4e7d43"), new("3f6e92"),
        new("6d5694"), new("2e6560"), new("6b4a2b"), new("7a3b5e"),
        new("b0432e"), new("38506e"), new("5a5a52"), new("96731f"),
    };

    public static Sigil Of(World world, int personId)
    {
        uint h = PlaceSeeds.Hash(world.Seed, personId, salt: 7);
        return new Sigil(Glyphs[h % (uint)Glyphs.Length], Tints[(h >> 8) % (uint)Tints.Length]);
    }

    /// <summary>Inline BBCode form for RichTextLabel surfaces.</summary>
    public static string Bb(World world, int personId)
    {
        var s = Of(world, personId);
        return $"[color=#{s.Tint.ToHtml(false)}]{s.Glyph}[/color]";
    }
}
