using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using LivingMyth.Sim;

// The Cast: a persistent dramatis-personae panel — the standing answer to "who is who".
// A small capped roster of the souls the player follows plus the key figures their other
// follows imply (eldest of a followed line, leaders of followed peoples, holders of
// watched lands), each with their sigil, name, role, and age. Always on screen while
// anything is followed; hidden entirely when nothing is (no empty chrome).
//
// Cost discipline: label refresh is O(cap) once per shown year; the membership recompute
// (which walks the marked bloodline) runs only when flagged dirty — a follow changed or a
// YOURS event streamed — never as a standing per-tick history scan.
public sealed partial class CastPanel : PanelContainer
{
    private const int Cap = 8;

    private Func<World> _world = null!;
    private HashSet<int> _souls = null!;
    private HashSet<int> _seedPeople = null!;
    private HashSet<int> _marked = null!;
    private HashSet<string> _markedFactions = null!;
    private HashSet<int> _followedRegions = null!;
    private Dictionary<int, int> _lastSeen = null!;
    private Action<int> _onPick = null!;

    private VBoxContainer _list = null!;
    private Button _toggle = null!;
    private bool _collapsed;
    private List<(int Pid, string Role)> _members = new();
    private string _signature = "";

    public void Setup(Func<World> world, HashSet<int> souls, HashSet<int> seedPeople,
                      HashSet<int> marked, HashSet<string> markedFactions,
                      HashSet<int> followedRegions, Dictionary<int, int> lastSeen,
                      Action<int> onPick)
    {
        _world = world;
        _souls = souls;
        _seedPeople = seedPeople;
        _marked = marked;
        _markedFactions = markedFactions;
        _followedRegions = followedRegions;
        _lastSeen = lastSeen;
        _onPick = onPick;
        BuildUi();
    }

    private void BuildUi()
    {
        Visible = false;
        AddThemeStyleboxOverride("panel", Ui.PanelBox());
        CustomMinimumSize = new Vector2(240, 0);

        var margin = new MarginContainer();
        foreach (var s in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{s}", 10);
        AddChild(margin);

        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 6);
        margin.AddChild(vb);

        var hdr = new HBoxContainer();
        vb.AddChild(hdr);
        var cap = Ui.SectionLabel("The Cast", 12);
        cap.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        cap.TooltipText = "the souls your follows make matter — marks are a viewer's hand, not sim state";
        hdr.AddChild(cap);
        _toggle = new Button { Text = "—", CustomMinimumSize = new Vector2(24, 22), TooltipText = "fold the cast away" };
        Ui.StyleButton(_toggle);
        _toggle.Pressed += () =>
        {
            _collapsed = !_collapsed;
            _list.Visible = !_collapsed;
            _toggle.Text = _collapsed ? "❖" : "—";
            _toggle.TooltipText = _collapsed ? "unfold the cast" : "fold the cast away";
        };
        hdr.AddChild(_toggle);

        _list = new VBoxContainer();
        _list.AddThemeConstantOverride("separation", 4);
        vb.AddChild(_list);
    }

    /// <summary>Refresh the roster. Pass membershipDirty=true when a follow changed or a
    /// YOURS event streamed; otherwise only the cheap label signature is reconsidered.</summary>
    public void Refresh(bool membershipDirty)
    {
        var w = _world();
        if (membershipDirty) RecomputeMembers(w);

        Visible = _members.Count > 0;
        if (_members.Count == 0) { _signature = ""; return; }

        // Rebuild rows only when something visible changed (ages tick yearly, deaths flip).
        var sig = new System.Text.StringBuilder();
        foreach (var (pid, role) in _members)
            if (w.People.TryGetValue(pid, out var p))
                sig.Append(pid).Append('|').Append(p.Alive ? p.Age(w.Year) : -1).Append('|').Append(role).Append(';');
        string s = sig.ToString();
        if (s == _signature) return;
        _signature = s;

        foreach (var child in _list.GetChildren()) child.QueueFree();
        foreach (var (pid, role) in _members)
            if (w.People.TryGetValue(pid, out var p))
                _list.AddChild(BuildEntry(w, p, role));
    }

    // Membership, in player-intent order: explicit soul follows first, then the figures the
    // other follows imply. Deterministic ordering throughout; deduped; capped.
    private void RecomputeMembers(World w)
    {
        _members.Clear();
        var seen = new HashSet<int>();
        void Add(int? pid, string role)
        {
            if (_members.Count >= Cap || pid is not int id) return;
            if (!w.People.ContainsKey(id) || !seen.Add(id)) return;
            _members.Add((id, role));
        }

        foreach (int pid in _souls.OrderBy(i => i))
            Add(pid, "a soul you follow");

        // The living head of a followed bloodline — eldest of the marked line. The marked
        // set grows with history, so this walk only happens on dirty refreshes.
        if (_seedPeople.Count > 0)
        {
            Person? eldest = null;
            foreach (int pid in _marked)
                if (w.People.TryGetValue(pid, out var p) && p.Alive
                    && (eldest is null || p.BirthYear < eldest.BirthYear
                        || (p.BirthYear == eldest.BirthYear && p.Id < eldest.Id)))
                    eldest = p;
            Add(eldest?.Id, "eldest of the line you follow");
        }

        foreach (string fid in _markedFactions.OrderBy(f => f, StringComparer.Ordinal))
            if (w.Factions.TryGetValue(fid, out var fac))
                Add(fac.LeaderId, $"leads {fac.Name}");

        foreach (int rid in _followedRegions.OrderBy(r => r))
            if (rid >= 0 && rid < w.Regions.Count
                && w.Regions[rid].ControllingFactionId is string hid
                && w.Factions.TryGetValue(hid, out var holder))
                Add(holder.LeaderId, $"holds {w.Regions[rid].Name}");
    }

    private Control BuildEntry(World w, Person p, string role)
    {
        var row = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        var normal = Ui.RowBox();
        var hover = Ui.RowBox(Ui.RowBgWarm, Ui.RowBorderHover);
        row.AddThemeStyleboxOverride("panel", normal);
        row.MouseEntered += () => row.AddThemeStyleboxOverride("panel", hover);
        row.MouseExited += () => row.AddThemeStyleboxOverride("panel", normal);
        int pid = p.Id;
        row.GuiInput += ev =>
        {
            if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                _onPick(pid);
        };
        // The last beat the saga actually showed of them — hover memory, honest by source.
        row.TooltipText = _lastSeen.TryGetValue(pid, out int lsId)
            ? $"you last saw them Yr {w.Chronicle.Get(lsId).Year}: {w.Chronicle.Get(lsId).Text}"
            : "nothing of them has crossed the saga yet — click to inspect";

        var hb = new HBoxContainer();
        hb.AddThemeConstantOverride("separation", 8);
        row.AddChild(hb);

        var sig = PersonSigils.Of(w, pid);
        var chip = new PanelContainer
        {
            CustomMinimumSize = new Vector2(22, 22),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        chip.AddThemeStyleboxOverride("panel", Ui.ChipBox(sig.Tint));
        var glyph = new Label
        {
            Text = sig.Glyph,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        glyph.AddThemeFontSizeOverride("font_size", 11);
        glyph.AddThemeColorOverride("font_color", Ui.ParchmentHi);
        chip.AddChild(glyph);
        hb.AddChild(chip);

        var body = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        body.AddThemeConstantOverride("separation", 0);
        hb.AddChild(body);
        var name = new Label
        {
            Text = p.Name,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        name.AddThemeFontOverride("font", Ui.SerifBold);
        name.AddThemeFontSizeOverride("font_size", 13);
        name.AddThemeColorOverride("font_color", Ui.InkDeep);
        body.AddChild(name);
        var roleL = new Label
        {
            Text = role,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        roleL.AddThemeFontSizeOverride("font_size", 10);
        roleL.AddThemeColorOverride("font_color", Ui.FadedSub);
        body.AddChild(roleL);

        var status = new Label
        {
            Text = p.Alive ? p.Age(w.Year).ToString() : "✝",
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            TooltipText = p.Alive ? "their age" : $"died Yr {p.DeathYear}",
        };
        status.AddThemeFontSizeOverride("font_size", 11);
        status.AddThemeColorOverride("font_color", Ui.Faded);
        hb.AddChild(status);

        return row;
    }
}
