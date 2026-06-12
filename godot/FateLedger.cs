using Godot;
using System;
using System.Collections.Generic;
using System.Text;
using LivingMyth.Sim;

// The Fate Ledger: the sacred record of the player's hand over the world — every blessing,
// curse, protection, doom, omen, and work upon the land, with the consequences the chronicle
// honestly traced back to each act. A right side sheet (panel economy: shares the reading
// slot with How We Got Here). Pure read-model over World.DivinePressures + the consequence
// index Main maintains incrementally — content rebuilds on open, never per tick.
public sealed partial class FateLedger : Panel
{
    private Func<World> _world = null!;
    private Func<int, IReadOnlyList<int>> _consequencesOf = null!;
    private Action<string> _onLink = null!;
    private RichTextLabel _body = null!;

    public void Setup(Func<World> world, Func<int, IReadOnlyList<int>> consequencesOf, Action<string> onLink)
    {
        _world = world;
        _consequencesOf = consequencesOf;
        _onLink = onLink;
        BuildUi();
    }

    private void BuildUi()
    {
        Visible = false;
        AnchorLeft = 1; AnchorRight = 1; AnchorTop = 0; AnchorBottom = 1;
        OffsetLeft = -428; OffsetRight = -8; OffsetTop = 10; OffsetBottom = -84;
        var box = Ui.PanelBox();
        box.BorderColor = Ui.Gold;
        AddThemeStyleboxOverride("panel", box);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        foreach (var s in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{s}", 14);
        AddChild(margin);

        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 8);
        margin.AddChild(vb);

        var hb = new HBoxContainer();
        hb.AddThemeConstantOverride("separation", 8);
        vb.AddChild(hb);
        var titles = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        titles.AddThemeConstantOverride("separation", 0);
        hb.AddChild(titles);
        var title = new Label { Text = "The Fate Ledger" };
        title.AddThemeFontOverride("font", Ui.SerifBold);
        title.AddThemeFontSizeOverride("font_size", 18);
        title.AddThemeColorOverride("font_color", Ui.InkDeep);
        titles.AddChild(title);
        var sub = Ui.SectionLabel("your hand upon the world", 11);
        titles.AddChild(sub);
        var close = new Button { Text = "✕", CustomMinimumSize = new Vector2(28, 28) };
        Ui.StyleButton(close);
        close.Pressed += () => Visible = false;
        hb.AddChild(close);

        var rule = new HSeparator();
        rule.AddThemeStyleboxOverride("separator", new StyleBoxFlat { BgColor = Ui.Gold, ContentMarginTop = 1 });
        vb.AddChild(rule);

        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        vb.AddChild(scroll);
        _body = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(340, 0),
        };
        _body.AddThemeColorOverride("default_color", Ui.Ink);
        _body.AddThemeFontOverride("bold_font", Ui.SerifBold);
        _body.MetaClicked += meta => _onLink(meta.AsString());
        scroll.AddChild(_body);
    }

    public void Open()
    {
        Render();
        Visible = true;
    }

    private static string Hex(Color c) => Ui.Hex(c);

    private string Link(string target, string text)
        => $"[color=#8a5d12][url={target}]{text}[/url][/color]";

    private void Render()
    {
        var w = _world();
        var sb = new StringBuilder();
        if (w.DivinePressures.Count == 0)
        {
            sb.AppendLine($"[color=#{Hex(Ui.Faded)}]your hand has not yet touched the world[/color]");
            sb.AppendLine();
            sb.AppendLine($"[color=#{Hex(Ui.FadedSub)}]bless or curse a soul from their card · protect or doom a people · " +
                          "seed an omen, a forest, or a spring from a land's lens[/color]");
            _body.Text = sb.ToString();
            return;
        }

        var sections = new (DivinePressureKind Kind, string Title)[]
        {
            (DivinePressureKind.Bless, "Blessed souls"),
            (DivinePressureKind.Curse, "Cursed souls"),
            (DivinePressureKind.Protect, "Protected peoples"),
            (DivinePressureKind.Doom, "Doomed peoples"),
            (DivinePressureKind.Omen, "Omen-marked lands"),
            (DivinePressureKind.ForestSeeded, "Works upon the land"),
        };
        foreach (var (kind, sectionTitle) in sections)
        {
            bool terrain = kind == DivinePressureKind.ForestSeeded;
            var rows = new List<DivinePressure>();
            foreach (var pr in w.DivinePressures)
                if (pr.Kind == kind || (terrain && pr.Kind == DivinePressureKind.SpringCalled))
                    rows.Add(pr);
            if (rows.Count == 0) continue;

            sb.AppendLine($"[color=#{Hex(Ui.Faded)}]{sectionTitle.ToUpperInvariant()}[/color]");
            foreach (var pr in rows)
            {
                sb.AppendLine(LedgerRow(w, pr, terrain));
                // The consequences the chronicle honestly traced to this act — newest last.
                var cons = _consequencesOf(pr.SourceEventId);
                int shown = Math.Min(2, cons.Count);
                for (int i = cons.Count - shown; i < cons.Count; i++)
                {
                    var ce = w.Chronicle.Get(cons[i]);
                    var cls = Ui.ClassOf(ce.Type);
                    sb.AppendLine($"      [color=#{Hex(Ui.Faded)}]Yr {ce.Year}[/color] " +
                        $"[color=#{Hex(cls.Color)}]{cls.Glyph}[/color] {Link("e:" + ce.Id, ce.Text)}");
                }
                if (cons.Count > shown)
                    sb.AppendLine($"      [color=#{Hex(Ui.FadedSub)}]…and {cons.Count - shown} more traced to this act[/color]");
            }
            sb.AppendLine();
        }
        _body.Text = sb.ToString();
    }

    private string LedgerRow(World w, DivinePressure pr, bool terrain)
    {
        string target = pr.TargetType switch
        {
            "person" when int.TryParse(pr.TargetId, out int pid) && w.People.TryGetValue(pid, out var p)
                => $"{PersonSigils.Bb(w, pid)} [b]{p.Name}[/b]" + (p.Alive ? "" : $" [color=#{Hex(Ui.Faded)}]✝ Yr {p.DeathYear}[/color]"),
            "faction" when w.Factions.TryGetValue(pr.TargetId, out var f) => $"[b]{f.Name}[/b]",
            "region" when int.TryParse(pr.TargetId, out int rid) && w.RegionName(rid) is string rn
                => $"[b]{Link("r:" + rid, rn)}[/b]",
            _ => pr.TargetId,
        };
        string state = terrain ? $"[color=#{Hex(Ui.Moss)}]wrought[/color]"
            : pr.IsActive(w)
                ? $"[color=#8a5d12]holds[/color]" + (pr.ExpiresYear is int ey ? $" [color=#{Hex(Ui.FadedSub)}](until Yr {ey})[/color]" : "")
                : $"[color=#{Hex(Ui.Faded)}]faded[/color]";
        string glyph = pr.Kind switch
        {
            DivinePressureKind.Bless => "✦",
            DivinePressureKind.Curse => "✳",
            DivinePressureKind.Protect => "❧",
            DivinePressureKind.Doom => "☄",
            DivinePressureKind.Omen => "✶",
            DivinePressureKind.ForestSeeded => "✿",
            _ => "≈",
        };
        return $"{glyph} {target} — [color=#{Hex(Ui.Faded)}]Yr {pr.StartYear}[/color] · {state}  {Link("e:" + pr.SourceEventId, "the act")}";
    }
}
