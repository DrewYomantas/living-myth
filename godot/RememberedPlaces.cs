using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LivingMyth.Sim;

// Remembered Places: the atlas's memory, as a readable list — every place the record has
// truly touched, newest first. A right side sheet (panel economy: shares the reading slot
// with How We Got Here and the Fate Ledger). Pure read-model over the chronicle channels
// RegionActivity already indexes incrementally; content rebuilds on open/filter, never per
// tick.
//
// The honesty contract (binding in docs/VISUAL_STYLE.md "Anchor language"): every row says
// exactly how it is anchored — "at {site}" only for a true Event.SiteId, "in {region}" for
// RegionId-only, "remembered in {region}" for home memory (never a location). There is no
// "succession" filter on purpose: successions carry no place anchor, so the chip would be
// honest only as an always-empty button — and fake-dead affordances are banned.
public sealed partial class RememberedPlaces : Panel
{
    private Func<World> _world = null!;
    private RegionActivity _activity = null!;
    private Action<string> _onLink = null!;
    private RichTextLabel _body = null!;
    private string _filter = "all";
    private readonly List<(Button btn, string key)> _chips = new();
    private const int MaxRows = 36;

    private static readonly (string key, string label)[] Filters =
    {
        ("all", "all"), ("war", "war & land"), ("harvest", "harvest"), ("plague", "plague"),
        ("ways", "ways"), ("divine", "divine"), ("terrain", "terrain"), ("memory", "memory"),
    };

    public void Setup(Func<World> world, RegionActivity activity, Action<string> onLink)
    {
        _world = world;
        _activity = activity;
        _onLink = onLink;
        BuildUi();
    }

    private void BuildUi()
    {
        Visible = false;
        AnchorLeft = 1; AnchorRight = 1; AnchorTop = 0; AnchorBottom = 1;
        OffsetLeft = -448; OffsetRight = -8; OffsetTop = 10; OffsetBottom = -84;
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
        var title = new Label { Text = "Remembered Places" };
        title.AddThemeFontOverride("font", Ui.SerifBold);
        title.AddThemeFontSizeOverride("font_size", 18);
        title.AddThemeColorOverride("font_color", Ui.InkDeep);
        titles.AddChild(title);
        titles.AddChild(Ui.SectionLabel("where the record has truly touched the land", 11));
        var close = new Button { Text = "✕", CustomMinimumSize = new Vector2(28, 28) };
        Ui.StyleButton(close);
        close.Pressed += () => Visible = false;
        hb.AddChild(close);

        var chipRow = new HBoxContainer();
        chipRow.AddThemeConstantOverride("separation", 4);
        vb.AddChild(chipRow);
        foreach (var (key, label) in Filters)
        {
            var chip = new Button { Text = label };
            chip.AddThemeFontSizeOverride("font_size", 11);
            string k = key;
            chip.Pressed += () => { _filter = k; Render(); };
            chipRow.AddChild(chip);
            _chips.Add((chip, key));
        }

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
            CustomMinimumSize = new Vector2(360, 0),
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

    // The filter bucket of one anchored event — recorded type+tags only.
    private static string FilterOf(Event e) => e.Type switch
    {
        "territory" => "war",
        "battle" => "war",
        "famine" => "harvest",
        "famine_end" => "harvest",
        "boom" => "harvest",
        "plague" => "plague",
        "plague_end" => "plague",
        "custom" => "ways",
        "divine" when e.Tags.Contains("terrain") => "terrain",
        "divine" => "divine",
        _ => "other",
    };

    private void Render()
    {
        foreach (var (btn, key) in _chips) Ui.StyleButton(btn, key == _filter);
        var w = _world();
        var rows = new List<(int year, int order, string filter, string bb)>();
        var seen = new HashSet<int>();

        // Site-anchored memory: events that truly belong to one modeled place.
        foreach (var s in w.Sites.All)
            foreach (int eid in _activity.SiteRecentFor(s.Id))
            {
                var e = w.Chronicle.Get(eid);
                if (!seen.Add(eid)) continue;
                var cls = Ui.ClassOf(e.Type);
                rows.Add((e.Year, eid, FilterOf(e),
                    $"[color=#{Hex(Ui.Faded)}]Yr {e.Year}[/color] [color=#{Hex(cls.Color)}]{cls.Glyph}[/color] "
                    + $"[b]{Link("s:" + s.Id, s.Name)}[/b] — {Link("e:" + eid, e.Text)}"
                    + $"\n      [color=#{Hex(Ui.FadedSub)}]at {s.Name}, in {w.RegionName(s.RegionId)}[/color]"));
            }

        // Region-anchored memory (no single place): the land remembers, honestly wide.
        foreach (var r in w.Regions)
            foreach (int eid in _activity.RecentFor(r.Id))
            {
                var e = w.Chronicle.Get(eid);
                if (e.SiteId is not null || !seen.Add(eid)) continue;
                var cls = Ui.ClassOf(e.Type);
                rows.Add((e.Year, eid, FilterOf(e),
                    $"[color=#{Hex(Ui.Faded)}]Yr {e.Year}[/color] [color=#{Hex(cls.Color)}]{cls.Glyph}[/color] "
                    + $"[b]{Link("r:" + r.Id, r.Name)}[/b] — {Link("e:" + eid, e.Text)}"
                    + $"\n      [color=#{Hex(Ui.FadedSub)}]in {r.Name} — no single place[/color]"));
            }

        // Home memory: lives remembered at the root of their line — never "it happened here".
        // Cairn-worthy only (the memorial bar the map's cairns use): murders always carry
        // their grief home; plain deaths only of those who ever led. Routine births and
        // deaths stay in the lens's home channel — this panel is for memory that marks.
        foreach (var r in w.Regions)
            foreach (int eid in _activity.HomeRecentFor(r.Id))
            {
                var e = w.Chronicle.Get(eid);
                bool cairnWorthy = e.Type == "murder"
                    || (e.Type == "death" && e.Participants.Count > 0
                        && w.People.TryGetValue(e.Participants[0], out var dp) && dp.EverLeader);
                if (!cairnWorthy || !seen.Add(eid)) continue;
                var cls = Ui.ClassOf(e.Type);
                rows.Add((e.Year, eid, "memory",
                    $"[color=#{Hex(Ui.Faded)}]Yr {e.Year}[/color] [color=#{Hex(cls.Color)}]{cls.Glyph}[/color] "
                    + $"[b]{Link("r:" + r.Id, r.Name)}[/b] — {Link("e:" + eid, e.Text)}"
                    + $"\n      [color=#{Hex(Ui.FadedSub)}]remembered in {r.Name} — not where it happened[/color]"));
            }

        var sb = new StringBuilder();
        var shown = rows
            .Where(r => _filter == "all" || r.filter == _filter)
            .OrderByDescending(r => r.year).ThenByDescending(r => r.order)
            .Take(MaxRows).ToList();
        if (shown.Count == 0)
        {
            sb.AppendLine($"[color=#{Hex(Ui.Faded)}]no place memory here yet — the record has not touched the land this way[/color]");
        }
        else
        {
            foreach (var row in shown) sb.AppendLine(row.bb);
            int hidden = rows.Count(r => _filter == "all" || r.filter == _filter) - shown.Count;
            if (hidden > 0)
                sb.AppendLine($"[color=#{Hex(Ui.FadedSub)}]…and {hidden} older remembered moments[/color]");
        }
        sb.AppendLine();
        sb.AppendLine($"[color=#{Hex(Ui.FadedSub)}]every row names its anchor honestly: at a place (a true site anchor), " +
                      "in a land (no single place), or remembered at a home (memory, never a location)[/color]");
        _body.Text = sb.ToString();
    }
}
