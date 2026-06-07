using Godot;
using System.Linq;
using System.Text;
using LivingMyth.Sim;

// M1 viewer: watch history unfold on the proven sim. A map of the island and its peoples,
// time controls, the live "rising" feed, and click-to-inspect panels. The simulation is a
// standalone class library with zero Godot dependency — this scene only drives and renders it.
public partial class Main : Node
{
    private const int Seed = 7;
    private const float BaseInterval = 0.5f;   // seconds per year at 1x
    private const int FeedWidth = 360;
    private const int BottomH = 100;

    private World _world = null!;
    private Control _root = null!;
    private MapView _map = null!;
    private VBoxContainer _feedList = null!;
    private RichTextLabel _inspector = null!;
    private Panel _inspectorPanel = null!;
    private Label _yearLabel = null!;
    private Button _playBtn = null!;
    private Label _speedLabel = null!;
    private HSlider _chatSlider = null!;
    private Label _chatLabel = null!;

    private bool _running = true;
    private float _speed = 1f;
    private float _accum;
    private int _lastEventCount;
    private int _feedRows;

    public override void _Ready()
    {
        var (config, names) = DataLoader.Load();
        _world = new World(Seed, config, names);
        _world.SeedWorld();
        _lastEventCount = 0;

        BuildUi();
        _map.World = _world;
        StreamNewHeadlines();
        RefreshTimeBar();
        _map.QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (_running)
        {
            _accum += (float)delta;
            float interval = BaseInterval / _speed;
            int budget = 6;   // cap ticks per frame so we never spiral trying to catch up
            while (_accum >= interval && budget-- > 0)
            {
                _accum -= interval;
                _world.Tick();
                StreamNewHeadlines();
            }
            if (_accum > interval * 6) _accum = 0f;   // drop any backlog
        }
        _map.QueueRedraw();
        RefreshTimeBar();
    }

    // ------------------------------------------------------------------ UI build

    private void BuildUi()
    {
        _root = new Control();
        AddChild(_root);
        // A Control parented to a plain Node won't auto-fill the viewport; drive its size.
        UpdateRootSize();
        GetViewport().SizeChanged += UpdateRootSize;

        var root = _root;

        _map = new MapView { PersonPicked = OnPersonPicked, FactionPicked = OnFactionPicked };
        root.AddChild(_map);
        _map.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _map.OffsetRight = -FeedWidth;
        _map.OffsetBottom = -BottomH;

        BuildFeed(root);
        BuildBottomBar(root);
        BuildInspector(root);
    }

    private void UpdateRootSize()
    {
        _root.Position = Vector2.Zero;
        _root.Size = GetViewport().GetVisibleRect().Size;
    }

    private void BuildFeed(Control root)
    {
        var panel = new PanelContainer();
        root.AddChild(panel);
        panel.AnchorLeft = 1; panel.AnchorRight = 1; panel.AnchorTop = 0; panel.AnchorBottom = 1;
        panel.OffsetLeft = -FeedWidth; panel.OffsetRight = 0; panel.OffsetTop = 0; panel.OffsetBottom = -BottomH;

        var margin = new MarginContainer();
        foreach (var s in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{s}", 10);
        panel.AddChild(margin);

        var vb = new VBoxContainer();
        margin.AddChild(vb);

        var hdr = new Label { Text = "THE FEED — what's rising" };
        hdr.AddThemeFontSizeOverride("font_size", 16);
        vb.AddChild(hdr);

        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        vb.AddChild(scroll);

        _feedList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(_feedList);
    }

    private void BuildBottomBar(Control root)
    {
        var bar = new PanelContainer();
        root.AddChild(bar);
        bar.AnchorLeft = 0; bar.AnchorRight = 1; bar.AnchorTop = 1; bar.AnchorBottom = 1;
        bar.OffsetLeft = 0; bar.OffsetRight = 0; bar.OffsetTop = -BottomH; bar.OffsetBottom = 0;

        var margin = new MarginContainer();
        foreach (var s in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{s}", 12);
        bar.AddChild(margin);

        var hb = new HBoxContainer();
        hb.AddThemeConstantOverride("separation", 10);
        margin.AddChild(hb);

        _playBtn = new Button { Text = "⏸ Pause", CustomMinimumSize = new Vector2(96, 0) };
        _playBtn.Pressed += TogglePlay;
        hb.AddChild(_playBtn);

        foreach (var s in new[] { 1f, 2f, 4f, 8f })
        {
            var b = new Button { Text = $"{s:0}×" };
            b.Pressed += () => SetSpeed(s);
            hb.AddChild(b);
        }
        _speedLabel = new Label();
        hb.AddChild(_speedLabel);

        hb.AddChild(new VSeparator());

        _yearLabel = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        hb.AddChild(_yearLabel);

        _chatLabel = new Label();
        hb.AddChild(_chatLabel);
        _chatSlider = new HSlider
        {
            MinValue = 30, MaxValue = 140, Value = 60, Step = 5,
            CustomMinimumSize = new Vector2(150, 0),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        _chatSlider.ValueChanged += _ => RefreshTimeBar();
        hb.AddChild(_chatSlider);
    }

    private void BuildInspector(Control root)
    {
        _inspectorPanel = new Panel { Visible = false };
        root.AddChild(_inspectorPanel);
        _inspectorPanel.AnchorLeft = 0; _inspectorPanel.AnchorTop = 0;
        _inspectorPanel.AnchorRight = 0; _inspectorPanel.AnchorBottom = 0;
        _inspectorPanel.OffsetLeft = 16; _inspectorPanel.OffsetTop = 56;
        _inspectorPanel.OffsetRight = 16 + 330; _inspectorPanel.OffsetBottom = 56 + 340;

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        foreach (var s in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{s}", 10);
        _inspectorPanel.AddChild(margin);

        var vb = new VBoxContainer();
        margin.AddChild(vb);

        var hb = new HBoxContainer();
        vb.AddChild(hb);
        var title = new Label { Text = "INSPECT", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        title.AddThemeFontSizeOverride("font_size", 15);
        hb.AddChild(title);
        var close = new Button { Text = "✕" };
        close.Pressed += () => _inspectorPanel.Visible = false;
        hb.AddChild(close);

        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        vb.AddChild(scroll);
        _inspector = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(300, 0),
        };
        scroll.AddChild(_inspector);
    }

    // -------------------------------------------------------------- live feed

    private void StreamNewHeadlines()
    {
        var events = _world.Chronicle.Events;
        if (_lastEventCount >= events.Count) return;
        int threshold = (int)_chatSlider.Value;
        var reverse = Scoring.BuildReverse(_world);
        for (int i = _lastEventCount; i < events.Count; i++)
        {
            var e = events[i];
            int imp = Scoring.ImportanceFast(e, _world, reverse);
            if (imp >= threshold) AddFeedRow(e, imp);
        }
        _lastEventCount = events.Count;
    }

    private void AddFeedRow(Event e, int imp)
    {
        var lbl = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            CustomMinimumSize = new Vector2(FeedWidth - 40, 0),
        };
        lbl.Text = $"[color=#7fd0a0]Yr {e.Year}[/color]  {e.Text}  [color=#7e8a96](w{imp})[/color]";
        _feedList.AddChild(lbl);
        _feedList.MoveChild(lbl, 0);   // newest on top
        _feedRows++;
        while (_feedRows > 60 && _feedList.GetChildCount() > 0)
        {
            _feedList.GetChild(_feedList.GetChildCount() - 1).QueueFree();
            _feedRows--;
        }
    }

    // -------------------------------------------------------------- inspectors

    private void OnPersonPicked(int id)
    {
        if (!_world.People.TryGetValue(id, out var p)) return;
        var fac = _world.Factions[p.FactionId];
        string faith = p.ReligionId is int r && _world.Religions.TryGetValue(r, out var rr) ? rr.Name : "—";
        string spouse = p.SpouseId is int s && _world.People.TryGetValue(s, out var sp) ? $"{sp.Name} (#{s})" : "—";
        string status = p.Alive ? $"alive, age {p.Age(_world.Year)}" : $"died in year {p.DeathYear}";

        var sb = new StringBuilder();
        sb.AppendLine($"[b]{p.Name}[/b]  [color=#7e8a96]#{p.Id}[/color]");
        sb.AppendLine($"{fac.Name}");
        sb.AppendLine($"{(p.Sex == "f" ? "woman" : "man")}{(p.IsLeader ? "  ·  [color=#ffd54a]LEADER[/color]" : "")}{(p.Cursed ? "  ·  [color=#d24a64]CURSED[/color]" : "")}");
        sb.AppendLine($"status: {status}");
        sb.AppendLine($"faith: {faith}");
        sb.AppendLine($"spouse: {spouse}");
        sb.AppendLine($"children: {p.Children.Count}");
        sb.AppendLine();
        sb.AppendLine("[b]Their thread[/b]");
        var theirs = _world.Chronicle.Events.Where(e => e.Participants.Contains(id)).TakeLast(8).ToList();
        if (theirs.Count == 0) sb.AppendLine("[color=#7e8a96](no recorded events yet)[/color]");
        foreach (var e in theirs)
            sb.AppendLine($"[color=#7fd0a0]Yr {e.Year}[/color] — {e.Text}");

        _inspector.Text = sb.ToString();
        _inspectorPanel.Visible = true;
    }

    private void OnFactionPicked(string fid)
    {
        var fac = _world.Factions[fid];
        var members = _world.Living().Where(p => p.FactionId == fid).ToList();
        string leader = fac.LeaderId is int lid ? $"{_world.People[lid].Name} (#{lid})" : "(none)";
        var dom = _world.DominantReligion(fid);

        var sb = new StringBuilder();
        sb.AppendLine($"[b]{fac.Name}[/b]");
        sb.AppendLine($"culture: {fac.Culture}");
        sb.AppendLine($"homeland: {fac.Homeland}");
        sb.AppendLine($"living: {members.Count}");
        sb.AppendLine($"leader: {leader}");
        sb.AppendLine($"dominant faith: {dom?.Name ?? "—"}");
        sb.AppendLine();
        sb.AppendLine("[b]Eldest among them[/b]");
        foreach (var p in members.OrderByDescending(p => p.Age(_world.Year)).Take(8))
            sb.AppendLine($"{p.Name} (#{p.Id}) — age {p.Age(_world.Year)}{(p.IsLeader ? " ·  leader" : "")}");

        _inspector.Text = sb.ToString();
        _inspectorPanel.Visible = true;
    }

    // -------------------------------------------------------------- controls

    private void TogglePlay() => _running = !_running;

    private void SetSpeed(float s)
    {
        _speed = s;
        if (!_running) _running = true;
    }

    private void RefreshTimeBar()
    {
        _yearLabel.Text = $"Year {_world.Year}     {_world.Living().Count} living     {_world.Chronicle.Events.Count} events";
        _playBtn.Text = _running ? "⏸ Pause" : "▶ Play";
        _speedLabel.Text = $"{_speed:0}×";
        _chatLabel.Text = $"chattiness ≥ {(int)_chatSlider.Value}";
    }
}
