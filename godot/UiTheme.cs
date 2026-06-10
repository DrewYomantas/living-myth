using Godot;
using System.Collections.Generic;

// The mythic parchment UI language from the V2 design handoff (living-myth-v2): one palette,
// one panel recipe, one type system, and the event-class metadata the Saga feed and replay
// popup share. Presentation-only — nothing here touches the sim.
public static class Ui
{
    // ---- palette (handoff section 1) ----
    public static readonly Color Parchment = new("f2e5c2");
    public static readonly Color ParchmentHi = new("f4e8c8");
    public static readonly Color ParchmentLo = new("ecdab2");
    public static readonly Color Ink = new("3a2c19");
    public static readonly Color InkDeep = new("2f2310");
    public static readonly Color PanelBorder = new("5c4830");
    public static readonly Color Faded = new("8a744d");
    public static readonly Color FadedSub = new("7a6647");
    public static readonly Color RowBg = new("f7ecd0");
    public static readonly Color RowBgWarm = new("fbf0d4");
    public static readonly Color RowBorder = new("c9b288");
    public static readonly Color RowBorderHover = new("a8843c");
    public static readonly Color Gold = new("c9973f");
    public static readonly Color GoldGlow = new("f4c76d");
    public static readonly Color Ember = new("b0432e");
    public static readonly Color Ochre = new("b8862e");
    public static readonly Color Moss = new("4e7d43");
    public static readonly Color Slate = new("3f6e92");
    public static readonly Color Violet = new("6d5694");
    public static readonly Color Stone = new("8a8a86");
    public static readonly Color BtnFace = new("efe0ba");
    public static readonly Color BtnBorder = new("6a5436");

    // ---- fonts ----
    // Alegreya (OFL) with the engine's built-in font as glyph fallback, so the ⚔ ♛ ☾ marks
    // keep rendering even where the serif lacks them.
    public static Font Serif { get; private set; } = null!;
    public static Font SerifBold { get; private set; } = null!;
    public static Font SmallCaps { get; private set; } = null!;

    public static void LoadFonts()
    {
        var body = new FontFile();
        body.LoadDynamicFont(ProjectSettings.GlobalizePath("res://assets/fonts/Alegreya-VariableFont.ttf"));
        var sc = new FontFile();
        sc.LoadDynamicFont(ProjectSettings.GlobalizePath("res://assets/fonts/AlegreyaSC-Medium.ttf"));
        var fallback = ThemeDB.FallbackFont;
        if (body.Data is { Length: > 0 })
        {
            body.Fallbacks = new Godot.Collections.Array<Font> { fallback };
            Serif = body;
            var bold = new FontVariation { BaseFont = body };
            bold.VariationOpentype = new Godot.Collections.Dictionary { { "wght", 800 } };
            SerifBold = bold;
        }
        else { Serif = fallback; SerifBold = fallback; }
        if (sc.Data is { Length: > 0 })
        {
            sc.Fallbacks = new Godot.Collections.Array<Font> { fallback };
            SmallCaps = sc;
        }
        else SmallCaps = Serif;
    }

    // ---- panel recipe (handoff section 4) ----
    public static StyleBoxFlat PanelBox(int radius = 10)
    {
        var sb = new StyleBoxFlat
        {
            BgColor = ParchmentHi,
            BorderColor = PanelBorder,
            CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
            ShadowColor = new Color(0.05f, 0.035f, 0.015f, 0.5f),
            ShadowSize = 10,
            ShadowOffset = new Vector2(0, 6),
        };
        sb.SetBorderWidthAll(2);
        return sb;
    }

    public static StyleBoxFlat RowBox(Color? bg = null, Color? border = null, int radius = 8)
    {
        var sb = new StyleBoxFlat
        {
            BgColor = bg ?? RowBg,
            BorderColor = border ?? RowBorder,
            CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
            ContentMarginLeft = 9, ContentMarginRight = 9,
            ContentMarginTop = 7, ContentMarginBottom = 7,
        };
        sb.SetBorderWidthAll(1);
        return sb;
    }

    public static StyleBoxFlat ChipBox(Color bg)
    {
        var sb = new StyleBoxFlat { BgColor = bg, CornerRadiusTopLeft = 999, CornerRadiusTopRight = 999, CornerRadiusBottomLeft = 999, CornerRadiusBottomRight = 999 };
        return sb;
    }

    public static void StyleButton(Button b, bool active = false, Color? activeBg = null)
    {
        var face = active ? (activeBg ?? Gold) : BtnFace;
        var fg = active ? InkDeep : Ink;
        var normal = RowBox(face, BtnBorder);
        normal.ContentMarginLeft = 10; normal.ContentMarginRight = 10;
        normal.ContentMarginTop = 5; normal.ContentMarginBottom = 5;
        var hover = (StyleBoxFlat)normal.Duplicate();
        hover.BgColor = face.Lightened(0.06f);
        var pressed = (StyleBoxFlat)normal.Duplicate();
        pressed.BgColor = face.Darkened(0.06f);
        b.AddThemeStyleboxOverride("normal", normal);
        b.AddThemeStyleboxOverride("hover", hover);
        b.AddThemeStyleboxOverride("pressed", pressed);
        b.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        b.AddThemeColorOverride("font_color", fg);
        b.AddThemeColorOverride("font_hover_color", fg);
        b.AddThemeColorOverride("font_pressed_color", fg);
        b.AddThemeColorOverride("font_focus_color", fg);
    }

    public static Label SectionLabel(string text, int size = 11)
    {
        var l = new Label { Text = text.ToUpperInvariant() };
        l.AddThemeFontOverride("font", SmallCaps);
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", Faded);
        return l;
    }

    // ---- event-class metadata (handoff section 5: chip color + glyph + small-caps label) ----
    public readonly record struct EventClass(string Label, Color Color, string Glyph);

    private static readonly Dictionary<string, EventClass> Classes = new()
    {
        ["war"] = new("War", new Color("9c3b2e"), "⚔"),
        ["murder"] = new("Murder", new Color("9c3b2e"), "☠"),
        ["justice"] = new("Justice", new Color("9c3b2e"), "⚖"),
        ["scandal"] = new("Scandal", new Color("b06a2c"), "❢"),
        ["rumor"] = new("Rumor", new Color("b8862e"), "❝"),
        ["peace"] = new("Peace", new Color("4e7d43"), "❧"),
        ["trade"] = new("Trade", new Color("4e7d43"), "⚖"),
        ["boom"] = new("Plenty", new Color("4e7d43"), "✾"),
        ["famine"] = new("Famine", new Color("b06a2c"), "✺"),
        ["divine"] = new("Divine", new Color("a8402c"), "✶"),
        ["prophet"] = new("Prophecy", new Color("7c5a9b"), "☾"),
        ["schism"] = new("Schism", new Color("6d5694"), "❖"),
        ["martyr"] = new("Martyr", new Color("6d5694"), "✟"),
        ["custom"] = new("Custom", new Color("6d5694"), "❧"),
        ["succession"] = new("Succession", new Color("b8862e"), "♛"),
        ["leadership"] = new("Leadership", new Color("b8862e"), "♛"),
        ["founding"] = new("Founding", new Color("c9973f"), "⌂"),
        ["territory"] = new("Territory", new Color("8a8a86"), "⚑"),
        ["friction"] = new("Tension", new Color("b06a2c"), "⚑"),
        ["birth"] = new("Birth", new Color("8a8a86"), "✦"),
        ["death"] = new("Death", new Color("8a8a86"), "☾"),
        ["marriage"] = new("Marriage", new Color("8a744d"), "❦"),
        ["romance"] = new("Romance", new Color("b06a2c"), "❦"),
    };
    private static readonly EventClass Unknown = new("Tale", new Color("8a744d"), "◆");

    public static EventClass ClassOf(string eventType) => Classes.GetValueOrDefault(eventType, Unknown);

    public static string Hex(Color c) => c.ToHtml(false);
}
