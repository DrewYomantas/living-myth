// M3 (Yours channel) DONE: Follow button on both inspectors marks a bloodline/people; YOURS rows
// are gold-tagged + weight-boosted in the feed and followed dots are ringed cyan in MapView. The
// marked-set check is inline + O(living), and the bloodline grows virally at birth (not via a
// per-tick Feed.BuildFeed). This pass applied the V2 mythic-parchment UI handoff (year card,
// Saga feed v2 with event-class chips, sectioned inspectors, grouped time dock, parchment
// "How We Got Here") — presentation only, the sim tick path is untouched. See PROJECT_STATE.md.
// Living-atlas foundation pass: framed dock groups, parchment map place tags, warmed atlas
// palette — viewer styling only, per docs/VISUAL_STYLE.md.
using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LivingMyth.Sim;

// M1 viewer: watch history unfold on the proven sim. A map of the island and its peoples,
// time controls, the live "rising" feed, and click-to-inspect panels. The simulation is a
// standalone class library with zero Godot dependency — this scene only drives and renders it.
public partial class Main : Node
{
    private const int Seed = 7;
    // Drama Time constants — see docs/TIME_AND_STORY_PACING.md (the four clocks). Wall-clock
    // presentation only; they never change Tick() count or order. TODO(focus-time): the next
    // pacing slice is chapter recaps (roadmap 3) — the focus guard (roadmap 2) shipped.
    private const float BaseInterval = 1.2f;   // real seconds per sim-year at 1×
    private static readonly float[] SpeedLadder = { 0.25f, 0.5f, 1f, 2f, 4f, 8f, 16f };
    // Pace names per docs/TIME_AND_STORY_PACING.md — the ladder in lives, not factors.
    private static readonly string[] PaceNames =
    {
        "linger — a followed life unfolds over minutes",
        "watch — slow enough to know the names",
        "unfold — the chronicle's natural pace",
        "drift — lives pass in under a minute",
        "hasten — a generation in moments",
        "sweep — a century in a quarter minute",
        "ages — centuries sweep past",
    };
    private const int FeedWidth = 320;
    private const int BottomH = 96;

    private World _world = null!;
    private Control _root = null!;
    private MapView _map = null!;
    private VBoxContainer _feedList = null!;
    private RichTextLabel _inspector = null!;
    private Panel _inspectorPanel = null!;
    private Label _inspectorTitle = null!;
    private Label _inspectorSub = null!;
    private Button _curseBtn = null!;
    private Button _followBtn = null!;
    private Button _lensFactionBtn = null!;
    private string? _lensFactionId;          // faction behind the Region Lens hand-off button
    private int? _selectedPersonId;
    private string? _selectedFactionId;
    private readonly HashSet<int> _seedPeople = new();      // the people the player explicitly marked
    private readonly HashSet<int> _marked = new();          // their full bloodline, expanded
    private readonly HashSet<string> _markedFactions = new();
    private const int YoursBoost = 70;                      // weight added to a marked-bloodline event
    private const int FeedWindow = 60;                      // rolling feed holds this many rows
    private const float YoursCapFraction = 0.6f;            // YOURS may fill at most this share of the window
    private Panel _catchupPanel = null!;
    private RichTextLabel _catchup = null!;
    private int? _catchupEventId;
    private bool _catchupQuick = true;
    private Button _catchupQuickBtn = null!;
    private Button _catchupFullBtn = null!;
    private Label _yearBig = null!;
    private Label _yearSub = null!;
    private Button _playBtn = null!;
    private readonly List<(Button btn, float speed)> _speedBtns = new();
    private Button _dramaBtn = null!;
    private Button _camBtn = null!;
    private HSlider _chatSlider = null!;
    private Label _chatLabel = null!;

    private bool _running = true;
    private float _speed = 1f;
    private float _accum;

    // Dramatic pacing: a notable tick briefly slows presentation to a crawl, then eases back.
    private const int NotableBar = 100;          // imp >= this = notable (well above default chattiness 60)
    private const float SlowdownWindow = 1.6f;   // real seconds a dramatic beat lasts
    private const float SlowdownFactor = 0.15f;  // effective speed multiplier at the trough (crawl)
    private bool _dramaticPacing = true;         // toggle, default on
    private float _slowdownRemaining;            // real seconds left in the current beat
    private int _lastEventCount;
    private readonly List<FeedVisRow> _feedVis = new();     // (node, yours, weight) per visible row, newest first
    private readonly System.Collections.Generic.Dictionary<int, int> _consCount = new();
    private readonly RegionActivity _regionActivity = new();   // Region Lens: per-region anchored events
    private const int EchoCadence = 8;                      // sim-years between echo scans (slow path, not per-tick)
    private int _lastEchoYear;
    private readonly System.Collections.Generic.Dictionary<string, int> _echoSeen = new();  // archetype -> latest carded start year
    // Echoes are punctuation, not narration. Three gates keep them rare: an archetype can't card
    // again for a cooldown, an echo must clear a significance bar, and only a few may card per window.
    private const int EchoArchetypeCooldown = 60;          // sim-years before the same archetype can card again
    private const int EchoSignificanceBar = 80;            // anchor-event importance an echo must clear
    private const int EchoWindowYears = 40;                // rolling window for the global cap
    private const int EchoWindowCap = 2;                   // at most this many echo cards per window
    private readonly System.Collections.Generic.Dictionary<string, int> _echoCardedAt = new();  // archetype -> sim-year last carded
    private readonly List<int> _recentEchoYears = new();   // sim-years of recently carded echoes (window-pruned)

    // Focus Time — the focus guard (docs/TIME_AND_STORY_PACING.md, roadmap 2). Pause-on-drama:
    // off / followed (a major event touches what you follow, or a followed soul dies) / all
    // (any major event). Pausing is wall-clock presentation only — it never changes how many
    // times or in what order Tick() runs.
    private enum GuardMode { Off, Followed, All }
    private GuardMode _guardMode = GuardMode.Followed;
    private Button _guardBtn = null!;
    private Label _guardLabel = null!;                    // year-card "guard watches…" signal
    private Panel _guardPanel = null!;
    private Label _guardTitle = null!;
    private RichTextLabel _guardBody = null!;
    private int _guardEventId = -1;                       // event behind the open card
    private readonly System.Collections.Generic.Dictionary<int, int> _lastSeenEvent = new();  // marked pid -> last YOURS event actually shown (feed row or guard card)
    private int? _pendingGuardEventId;                    // armed during streaming, consumed after the tick
    private int? _pendingGuardFocusPid;                   // the followed soul at the heart of it
    private bool _pendingGuardIsDeath;
    private int _pendingGuardPrevSeen = -1;               // their last-seen event BEFORE this one (-1 none)

    private sealed class FeedVisRow { public Node Node = null!; public bool Yours; public int Weight; }

    public override void _Ready()
    {
        var (config, names) = DataLoader.Load();
        _world = new World(Seed, config, names);
        _world.SeedWorld();
        _lastEventCount = 0;
        _lastEchoYear = _world.Year;

        Ui.LoadFonts();
        BuildUi();
        _map.World = _world;
        _map.Marked = _marked;       // same HashSet, mutated in place — map sees follows live
        StreamNewHeadlines();
        if (_pendingGuardEventId is not null) ShowGuardCard();   // unreachable today; hardening
        RefreshTimeBar();
        _map.QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (_running)
        {
            if (_slowdownRemaining > 0f) _slowdownRemaining -= (float)delta;

            // During a dramatic beat, ease from a crawl back up to the user's chosen speed across
            // the window — deepest right after the notable tick, smoothly recovering. Real-time
            // driven, so frame-rate independent. Never engages while paused (we're inside _running).
            float effSpeed = _speed;
            if (_dramaticPacing && _slowdownRemaining > 0f)
            {
                float t = Mathf.Clamp(_slowdownRemaining / SlowdownWindow, 0f, 1f);
                effSpeed = _speed * Mathf.Lerp(1f, SlowdownFactor, t * t);
            }

            _accum += (float)delta;
            float interval = BaseInterval / effSpeed;
            int budget = 6;   // cap ticks per frame so we never spiral trying to catch up
            while (_accum >= interval && budget-- > 0)
            {
                _accum -= interval;
                _world.Tick();
                bool notable = StreamNewHeadlines();
                if (_pendingGuardEventId is not null)
                {
                    ShowGuardCard();   // pauses; the world waits for the player
                    break;
                }
                if (notable && _dramaticPacing)
                {
                    // Re-arm to the full window (rather than stacking) so a burst holds one
                    // slowdown instead of stuttering; break so the beat breathes this frame.
                    _slowdownRemaining = SlowdownWindow;
                    break;
                }
            }
            if (_accum > interval * 6) _accum = 0f;   // drop any backlog
            MaybeDetectEchoes();
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

        // One theme at the root: old-style serif everywhere, ink on parchment.
        _root.Theme = new Theme { DefaultFont = Ui.Serif, DefaultFontSize = 14 };

        var root = _root;

        _map = new MapView { PersonPicked = OnPersonPicked, FactionPicked = OnFactionPicked, RegionPicked = OnRegionPicked };
        root.AddChild(_map);
        _map.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _map.OffsetRight = -FeedWidth;
        _map.OffsetBottom = -BottomH;

        BuildFeed(root);
        BuildBottomBar(root);
        BuildYearCard(root);
        BuildInspector(root);
        BuildCatchup(root);
        BuildGuardCard(root);
    }

    private void UpdateRootSize()
    {
        _root.Position = Vector2.Zero;
        _root.Size = GetViewport().GetVisibleRect().Size;
    }

    private void BuildYearCard(Control root)
    {
        var card = new Panel();
        root.AddChild(card);
        card.AnchorLeft = 0; card.AnchorTop = 0; card.AnchorRight = 0; card.AnchorBottom = 0;
        card.OffsetLeft = 12; card.OffsetTop = 10; card.OffsetRight = 12 + 240; card.OffsetBottom = 10 + 112;
        card.AddThemeStyleboxOverride("panel", Ui.PanelBox());

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        card.AddChild(margin);

        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 0);
        margin.AddChild(vb);

        var hdr = new HBoxContainer();
        vb.AddChild(hdr);
        var title = Ui.SectionLabel("Living Myth", 12);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hdr.AddChild(title);
        var sigil = new Label { Text = "✺" };
        sigil.AddThemeColorOverride("font_color", Ui.Gold);
        sigil.AddThemeFontSizeOverride("font_size", 15);
        hdr.AddChild(sigil);

        _yearBig = new Label { Text = "Year 0" };
        _yearBig.AddThemeFontOverride("font", Ui.SerifBold);
        _yearBig.AddThemeFontSizeOverride("font_size", 30);
        _yearBig.AddThemeColorOverride("font_color", Ui.InkDeep);
        vb.AddChild(_yearBig);

        _yearSub = new Label { Text = "" };
        _yearSub.AddThemeFontSizeOverride("font_size", 12);
        _yearSub.AddThemeColorOverride("font_color", Ui.FadedSub);
        vb.AddChild(_yearSub);

        _guardLabel = new Label { Visible = false, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
        _guardLabel.AddThemeFontSizeOverride("font_size", 11);
        _guardLabel.AddThemeColorOverride("font_color", Ui.Gold);
        vb.AddChild(_guardLabel);
    }

    private void BuildFeed(Control root)
    {
        var panel = new PanelContainer();
        root.AddChild(panel);
        panel.AnchorLeft = 1; panel.AnchorRight = 1; panel.AnchorTop = 0; panel.AnchorBottom = 1;
        panel.OffsetLeft = -FeedWidth; panel.OffsetRight = -8; panel.OffsetTop = 10; panel.OffsetBottom = -BottomH - 6;
        panel.AddThemeStyleboxOverride("panel", Ui.PanelBox());

        var margin = new MarginContainer();
        foreach (var s in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{s}", 10);
        panel.AddChild(margin);

        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 6);
        margin.AddChild(vb);

        var hdrRow = new HBoxContainer();
        vb.AddChild(hdrRow);
        var hdr = new Label { Text = "The Saga" };
        hdr.AddThemeFontOverride("font", Ui.SerifBold);
        hdr.AddThemeFontSizeOverride("font_size", 19);
        hdr.AddThemeColorOverride("font_color", Ui.InkDeep);
        hdr.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hdrRow.AddChild(hdr);
        var scope = Ui.SectionLabel("what's rising");
        scope.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
        hdrRow.AddChild(scope);

        var rule = new HSeparator();
        rule.AddThemeStyleboxOverride("separator", new StyleBoxFlat { BgColor = Ui.RowBorder, ContentMarginTop = 1 });
        vb.AddChild(rule);

        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        vb.AddChild(scroll);

        _feedList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _feedList.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_feedList);

        var hint = new Label { Text = "click a tale to see how it happened", HorizontalAlignment = HorizontalAlignment.Center };
        hint.AddThemeFontSizeOverride("font_size", 11);
        hint.AddThemeColorOverride("font_color", Ui.Faded);
        vb.AddChild(hint);
    }

    // A captioned, parchment-framed dock section — returns the inner row buttons get added to.
    private HBoxContainer DockGroup(HBoxContainer bar, string caption)
    {
        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 4);
        vb.Alignment = BoxContainer.AlignmentMode.Center;
        bar.AddChild(vb);
        var cap = Ui.SectionLabel(caption);
        cap.HorizontalAlignment = HorizontalAlignment.Center;
        vb.AddChild(cap);
        var frame = new PanelContainer();
        frame.AddThemeStyleboxOverride("panel", Ui.DockBox());
        vb.AddChild(frame);
        var hb = new HBoxContainer();
        hb.AddThemeConstantOverride("separation", 6);
        hb.Alignment = BoxContainer.AlignmentMode.Center;
        frame.AddChild(hb);
        return hb;
    }

    private void BuildBottomBar(Control root)
    {
        var bar = new PanelContainer();
        root.AddChild(bar);
        bar.AnchorLeft = 0; bar.AnchorRight = 1; bar.AnchorTop = 1; bar.AnchorBottom = 1;
        bar.OffsetLeft = 8; bar.OffsetRight = -8; bar.OffsetTop = -BottomH; bar.OffsetBottom = -6;
        bar.AddThemeStyleboxOverride("panel", Ui.PanelBox(12));

        var margin = new MarginContainer();
        foreach (var s in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{s}", 10);
        bar.AddChild(margin);

        var hb = new HBoxContainer();
        hb.AddThemeConstantOverride("separation", 14);
        margin.AddChild(hb);

        // --- Time group: play, speed ladder, drama ---
        var timeRow = DockGroup(hb, "Time");

        _playBtn = new Button { Text = "❚❚ Pause", CustomMinimumSize = new Vector2(86, 0) };
        Ui.StyleButton(_playBtn);
        _playBtn.Pressed += TogglePlay;
        timeRow.AddChild(_playBtn);

        for (int i = 0; i < SpeedLadder.Length; i++)
        {
            float sp = SpeedLadder[i];
            var b = new Button { Text = $"{sp:0.##}×", CustomMinimumSize = new Vector2(40, 0), TooltipText = PaceNames[i] };
            b.Pressed += () => SetSpeed(sp);
            timeRow.AddChild(b);
            _speedBtns.Add((b, sp));
        }

        _dramaBtn = new Button { Text = "✦ drama", TooltipText = "Notable moments briefly slow time" };
        _dramaBtn.Pressed += () => { _dramaticPacing = !_dramaticPacing; RestyleToggles(); };
        timeRow.AddChild(_dramaBtn);

        _guardBtn = new Button { TooltipText = "Focus guard — pause when fate touches what you follow: off / ★ followed / all major events" };
        _guardBtn.Pressed += () => { _guardMode = (GuardMode)(((int)_guardMode + 1) % 3); RestyleToggles(); };
        timeRow.AddChild(_guardBtn);

        // --- Lens group: zoom + camera ---
        var lensRow = DockGroup(hb, "Lens");

        var zoomOut = new Button { Text = "－", CustomMinimumSize = new Vector2(36, 0) };
        Ui.StyleButton(zoomOut);
        zoomOut.Pressed += () => _map.ZoomBy(1f / 1.25f);
        lensRow.AddChild(zoomOut);
        var zoomIn = new Button { Text = "＋", CustomMinimumSize = new Vector2(36, 0) };
        Ui.StyleButton(zoomIn);
        zoomIn.Pressed += () => _map.ZoomBy(1.25f);
        lensRow.AddChild(zoomIn);
        var camReset = new Button { Text = "⤢", TooltipText = "Reset the lens", CustomMinimumSize = new Vector2(36, 0) };
        Ui.StyleButton(camReset);
        camReset.Pressed += () => _map.ResetCamera();
        lensRow.AddChild(camReset);
        _camBtn = new Button { Text = "✦ follow drama", TooltipText = "The lens leans toward notable events" };
        _camBtn.Pressed += () => { _map.CameraFollow = !_map.CameraFollow; RestyleToggles(); };
        lensRow.AddChild(_camBtn);

        // --- Chronicle group: chattiness threshold ---
        var chronRow = DockGroup(hb, "Chronicle");
        _chatLabel = new Label();
        _chatLabel.AddThemeFontSizeOverride("font_size", 12);
        _chatLabel.AddThemeColorOverride("font_color", Ui.FadedSub);
        chronRow.AddChild(_chatLabel);
        _chatSlider = new HSlider
        {
            MinValue = 30, MaxValue = 140, Value = 60, Step = 5,
            CustomMinimumSize = new Vector2(140, 0),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        _chatSlider.ValueChanged += _ => RefreshTimeBar();
        chronRow.AddChild(_chatSlider);

        var spacer = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        hb.AddChild(spacer);

        RestyleToggles();
        RestyleSpeedButtons();
    }

    private void RestyleToggles()
    {
        Ui.StyleButton(_dramaBtn, _dramaticPacing);
        Ui.StyleButton(_camBtn, _map.CameraFollow);
        _guardBtn.Text = _guardMode switch
        {
            GuardMode.Followed => "⛨ guard: ★",
            GuardMode.All => "⛨ guard: all",
            _ => "⛨ guard: off",
        };
        Ui.StyleButton(_guardBtn, _guardMode != GuardMode.Off);
    }

    private void RestyleSpeedButtons()
    {
        foreach (var (btn, sp) in _speedBtns)
            Ui.StyleButton(btn, Mathf.IsEqualApprox(sp, _speed));
    }

    private void BuildInspector(Control root)
    {
        _inspectorPanel = new Panel { Visible = false };
        root.AddChild(_inspectorPanel);
        _inspectorPanel.AnchorLeft = 0; _inspectorPanel.AnchorTop = 0;
        _inspectorPanel.AnchorRight = 0; _inspectorPanel.AnchorBottom = 0;
        _inspectorPanel.OffsetLeft = 12; _inspectorPanel.OffsetTop = 132;
        _inspectorPanel.OffsetRight = 12 + 330; _inspectorPanel.OffsetBottom = 132 + 400;
        _inspectorPanel.AddThemeStyleboxOverride("panel", Ui.PanelBox());

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        foreach (var s in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{s}", 12);
        _inspectorPanel.AddChild(margin);

        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 6);
        margin.AddChild(vb);

        var hb = new HBoxContainer();
        vb.AddChild(hb);
        var titles = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        titles.AddThemeConstantOverride("separation", 0);
        hb.AddChild(titles);
        _inspectorTitle = new Label { Text = "" };
        _inspectorTitle.AddThemeFontOverride("font", Ui.SerifBold);
        _inspectorTitle.AddThemeFontSizeOverride("font_size", 20);
        _inspectorTitle.AddThemeColorOverride("font_color", Ui.InkDeep);
        titles.AddChild(_inspectorTitle);
        _inspectorSub = new Label { Text = "" };
        _inspectorSub.AddThemeFontSizeOverride("font_size", 12);
        _inspectorSub.AddThemeColorOverride("font_color", Ui.FadedSub);
        titles.AddChild(_inspectorSub);
        var close = new Button { Text = "✕", CustomMinimumSize = new Vector2(28, 28) };
        Ui.StyleButton(close);
        close.Pressed += () => { _inspectorPanel.Visible = false; _map.SelectedFactionId = null; _map.SelectedRegionId = -1; };
        hb.AddChild(close);

        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        vb.AddChild(scroll);
        _inspector = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(296, 0),
        };
        _inspector.AddThemeColorOverride("default_color", Ui.Ink);
        _inspector.AddThemeFontOverride("bold_font", Ui.SerifBold);
        _inspector.MetaClicked += meta => OnInspectorLink(meta.AsString());
        scroll.AddChild(_inspector);

        // Region Lens hand-off: from a region to the people who hold it (the old direct route).
        _lensFactionBtn = new Button { Visible = false };
        Ui.StyleButton(_lensFactionBtn);
        _lensFactionBtn.Pressed += () => { if (_lensFactionId is string fid) OnFactionPicked(fid); };
        vb.AddChild(_lensFactionBtn);

        // Fate verbs pinned at the bottom: the curse tool (a living, not-yet-cursed person only)
        // and the Yours channel (follow a bloodline / a people).
        _followBtn = new Button { Text = "☆ Follow", Visible = false };
        Ui.StyleButton(_followBtn);
        _followBtn.Pressed += OnFollowPressed;
        vb.AddChild(_followBtn);

        _curseBtn = new Button { Text = "✳ Lay Curse on this bloodline", Visible = false };
        Ui.StyleButton(_curseBtn, active: true, activeBg: Ui.Ember);
        _curseBtn.AddThemeColorOverride("font_color", new Color("f2e9d2"));
        _curseBtn.AddThemeColorOverride("font_hover_color", new Color("f2e9d2"));
        _curseBtn.AddThemeColorOverride("font_pressed_color", new Color("f2e9d2"));
        _curseBtn.Pressed += OnCursePressed;
        vb.AddChild(_curseBtn);
    }

    private void BuildCatchup(Control root)
    {
        _catchupPanel = new Panel { Visible = false };
        root.AddChild(_catchupPanel);
        _catchupPanel.AnchorLeft = 0.5f; _catchupPanel.AnchorRight = 0.5f;
        _catchupPanel.AnchorTop = 0.5f; _catchupPanel.AnchorBottom = 0.5f;
        _catchupPanel.OffsetLeft = -300; _catchupPanel.OffsetRight = 300;
        _catchupPanel.OffsetTop = -250; _catchupPanel.OffsetBottom = 250;
        _catchupPanel.AddThemeStyleboxOverride("panel", Ui.PanelBox());

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        foreach (var s in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{s}", 14);
        _catchupPanel.AddChild(margin);

        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 8);
        margin.AddChild(vb);

        var hb = new HBoxContainer();
        hb.AddThemeConstantOverride("separation", 8);
        vb.AddChild(hb);
        var title = new Label { Text = "How We Got Here", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        title.AddThemeFontOverride("font", Ui.SerifBold);
        title.AddThemeFontSizeOverride("font_size", 21);
        title.AddThemeColorOverride("font_color", Ui.InkDeep);
        hb.AddChild(title);
        _catchupQuickBtn = new Button { Text = "Quick beats" };
        _catchupQuickBtn.Pressed += () => { _catchupQuick = true; RenderCatchup(); };
        hb.AddChild(_catchupQuickBtn);
        _catchupFullBtn = new Button { Text = "Full thread" };
        _catchupFullBtn.Pressed += () => { _catchupQuick = false; RenderCatchup(); };
        hb.AddChild(_catchupFullBtn);
        var close = new Button { Text = "✕", CustomMinimumSize = new Vector2(28, 28) };
        Ui.StyleButton(close);
        close.Pressed += () => _catchupPanel.Visible = false;
        hb.AddChild(close);

        var rule = new HSeparator();
        rule.AddThemeStyleboxOverride("separator", new StyleBoxFlat { BgColor = Ui.RowBorder, ContentMarginTop = 1 });
        vb.AddChild(rule);

        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        vb.AddChild(scroll);
        _catchup = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(548, 0),
        };
        _catchup.AddThemeColorOverride("default_color", Ui.Ink);
        _catchup.AddThemeFontOverride("bold_font", Ui.SerifBold);
        scroll.AddChild(_catchup);
    }

    // -------------------------------------------------------------- live feed

    // Returns true if any *notable* event (high importance, or yours) was surfaced this call —
    // the trigger for a dramatic-pacing beat.
    private bool StreamNewHeadlines()
    {
        var events = _world.Chronicle.Events;
        if (_lastEventCount >= events.Count) return false;
        int threshold = (int)_chatSlider.Value;
        bool notableSeen = false;

        // Maintain consequence counts (and the Region Lens activity index) incrementally so we
        // never rebuild a reverse index over the whole (ever-growing) chronicle. Update first,
        // then score the new slice.
        for (int i = _lastEventCount; i < events.Count; i++)
        {
            _regionActivity.Observe(events[i]);
            foreach (var c in events[i].Causes)
                _consCount[c] = _consCount.GetValueOrDefault(c) + 1;
        }

        for (int i = _lastEventCount; i < events.Count; i++)
        {
            var e = events[i];
            // Grow a followed bloodline as its descendants are born — O(new events), the same
            // viral-at-birth trick the curse uses. Avoids re-expanding the whole pedigree per tick.
            if (_marked.Count > 0 && e.Type == "birth" && e.Participants.Any(_marked.Contains))
                foreach (var pid in e.Participants) _marked.Add(pid);

            bool yours = IsYours(e);
            int imp = Scoring.ImportanceFast(e, _world, _consCount);
            if (yours) imp += YoursBoost;
            // The guard trigger runs before the chattiness gate: a follow is an explicit ask,
            // so a followed soul's fate registers even when the feed is quiet.
            MaybeArmGuard(e, yours, imp);
            if (imp < threshold) continue;

            var row = AddFeedRow(e, imp, yours);
            // Last-seen memory records only what was actually shown (this row, or a guard
            // card — see ShowGuardCard), so "you last saw…" never cites an undisplayed event.
            if (yours && row is not null) RememberSeen(e);
            // Yours always gets the spotlight; otherwise a high importance bar (well above
            // chattiness) catches divine/war/founding and ignores routine births/deaths.
            if (row is not null && (yours || imp >= NotableBar))
            {
                notableSeen = true;
                PulseFeedRow(row);
                if (e.RegionId is int rid) _map.PulseRegion(rid);
            }
        }
        _lastEventCount = events.Count;
        return notableSeen;
    }

    private static void PulseFeedRow(Control row)
    {
        row.Modulate = new Color(1.7f, 1.55f, 0.7f);   // warm flash, fades back to white
        row.CreateTween()
           .TweenProperty(row, "modulate", Colors.White, 0.9f)
           .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }

    // Does this event touch a marked bloodline or a marked people? Inline + O(participants),
    // so it stays off the heavier Feed.BuildFeed path while keeping the live feed O(living).
    private bool IsYours(Event e)
    {
        foreach (var pid in e.Participants)
        {
            if (_marked.Contains(pid)) return true;
            if (_markedFactions.Count > 0 && _world.People.TryGetValue(pid, out var p)
                && _markedFactions.Contains(p.FactionId)) return true;
        }
        return false;
    }

    // One Saga row, per the handoff anatomy: event-class chip → small-caps label + faded year
    // → one-to-two-line body. Hover warms the border; the whole row opens "How We Got Here".
    private Control BuildFeedRowControl(Event e, int imp, bool yours)
    {
        var cls = Ui.ClassOf(e.Type);
        var row = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        var normal = Ui.RowBox(yours ? Ui.RowBgWarm : Ui.RowBg, yours ? Ui.Gold : Ui.RowBorder);
        var hover = Ui.RowBox(Ui.RowBgWarm, Ui.RowBorderHover);
        row.AddThemeStyleboxOverride("panel", normal);
        row.MouseEntered += () => row.AddThemeStyleboxOverride("panel", hover);
        row.MouseExited += () => row.AddThemeStyleboxOverride("panel", normal);
        row.TooltipText = $"weight {imp} — click: how we got here";
        int eventId = e.Id;
        row.GuiInput += ev =>
        {
            if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                OpenCatchup(eventId);
        };

        var hb = new HBoxContainer();
        hb.AddThemeConstantOverride("separation", 8);
        row.AddChild(hb);

        var chip = new PanelContainer
        {
            CustomMinimumSize = new Vector2(24, 24),
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        chip.AddThemeStyleboxOverride("panel", Ui.ChipBox(cls.Color));
        var glyph = new Label
        {
            Text = cls.Glyph,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        glyph.AddThemeFontSizeOverride("font_size", 11);
        glyph.AddThemeColorOverride("font_color", Ui.ParchmentHi);
        chip.AddChild(glyph);
        hb.AddChild(chip);

        var body = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
        body.AddThemeConstantOverride("separation", 1);
        hb.AddChild(body);

        var header = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        body.AddChild(header);
        var type = Ui.SectionLabel((yours ? "★ " : "") + cls.Label);
        type.AddThemeColorOverride("font_color", yours ? Ui.Gold : cls.Color);
        type.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        type.MouseFilter = Control.MouseFilterEnum.Ignore;
        header.AddChild(type);
        var year = new Label { Text = $"Yr {e.Year}", MouseFilter = Control.MouseFilterEnum.Ignore };
        year.AddThemeFontSizeOverride("font_size", 11);
        year.AddThemeColorOverride("font_color", Ui.Faded);
        header.AddChild(year);

        var text = new Label
        {
            Text = e.Text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MaxLinesVisible = 2,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        text.AddThemeFontSizeOverride("font_size", 13);
        text.AddThemeColorOverride("font_color", Ui.Ink);
        body.AddChild(text);

        return row;
    }

    private Control? AddFeedRow(Event e, int imp, bool yours)
    {
        // YOURS cap: a +70-boosted bloodline can otherwise flood the window. Non-YOURS rows
        // always get in (they already cleared the threshold). A YOURS row past the cap displaces
        // the weakest currently-visible YOURS row — and if it's itself the weakest, it's skipped.
        // O(visible rows), no history scan.
        if (yours)
        {
            int yoursCap = (int)(FeedWindow * YoursCapFraction);
            int visibleYours = 0;
            FeedVisRow? weakest = null;
            foreach (var r in _feedVis)
            {
                if (!r.Yours) continue;
                visibleYours++;
                if (weakest is null || r.Weight < weakest.Weight) weakest = r;
            }
            if (visibleYours >= yoursCap)
            {
                if (weakest is null || imp <= weakest.Weight) return null;   // new row is the weakest YOURS
                weakest.Node.QueueFree();
                _feedVis.Remove(weakest);
            }
        }

        var row = BuildFeedRowControl(e, imp, yours);
        _feedList.AddChild(row);
        _feedList.MoveChild(row, 0);   // newest on top
        _feedVis.Insert(0, new FeedVisRow { Node = row, Yours = yours, Weight = imp });
        while (_feedVis.Count > FeedWindow)
        {
            var oldest = _feedVis[_feedVis.Count - 1];
            oldest.Node.QueueFree();
            _feedVis.RemoveAt(_feedVis.Count - 1);
        }
        return row;
    }

    // ------------------------------------------------------------- myth echoes

    // Read pass over the finished chronicle, on a slow cadence (never per-tick). Any NEW echo —
    // an archetype seen for the first time, or a fresh instance of a known archetype that starts
    // later than the last one we carded — drops a distinctive gold card into the feed.
    private void MaybeDetectEchoes()
    {
        if (_world.Year - _lastEchoYear < EchoCadence) return;
        _lastEchoYear = _world.Year;

        var echoes = Echoes.DetectAll(_world);   // already de-duped + sorted by start year
        _recentEchoYears.RemoveAll(y => _world.Year - y >= EchoWindowYears);
        System.Collections.Generic.Dictionary<int, List<int>>? reverse = null;
        foreach (var echo in echoes)
        {
            if (_recentEchoYears.Count >= EchoWindowCap) break;   // window full — no more cards this scan

            int prev = _echoSeen.GetValueOrDefault(echo.Archetype, int.MinValue);
            if (echo.YearSpan.First <= prev) continue;            // already carded this (or an older) instance
            if (_world.Year - _echoCardedAt.GetValueOrDefault(echo.Archetype, int.MinValue) < EchoArchetypeCooldown)
                continue;                                         // this archetype carded too recently

            reverse ??= Scoring.BuildReverse(_world);
            int anchor = AnchorEvent(echo, reverse);
            if (anchor < 0 || Scoring.Importance(_world.Chronicle.Get(anchor), _world, reverse) < EchoSignificanceBar)
                continue;                                         // not weighty enough to be punctuation

            _echoSeen[echo.Archetype] = echo.YearSpan.First;
            _echoCardedAt[echo.Archetype] = _world.Year;
            _recentEchoYears.Add(_world.Year);
            AddEchoCard(echo, anchor);
        }
    }

    // The single most important event in the echo, so clicking the card opens the catch-up on the
    // heart of the story rather than an arbitrary beat. -1 if the echo names no events.
    private int AnchorEvent(Echo echo, System.Collections.Generic.Dictionary<int, List<int>> reverse)
    {
        int best = -1, bestScore = int.MinValue;
        foreach (var id in echo.EventIds)
        {
            int s = Scoring.Importance(_world.Chronicle.Get(id), _world, reverse);
            if (s > bestScore) { bestScore = s; best = id; }
        }
        return best;
    }

    private void AddEchoCard(Echo echo, int anchorEventId)
    {
        // Echo cards carry the only luminous treatment in the feed: warm fill, gold border.
        var row = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        var box = Ui.RowBox(Ui.RowBgWarm, Ui.Gold);
        box.SetBorderWidthAll(2);
        row.AddThemeStyleboxOverride("panel", box);
        if (anchorEventId >= 0)
        {
            row.GuiInput += ev =>
            {
                if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                    OpenCatchup(anchorEventId);
            };
            row.TooltipText = "click: how we got here";
        }

        var vb = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        vb.AddThemeConstantOverride("separation", 1);
        row.AddChild(vb);
        var hdr = Ui.SectionLabel("◆ Myth Echo — " + echo.Archetype);
        hdr.AddThemeColorOverride("font_color", new Color("8a5d12"));
        hdr.MouseFilter = Control.MouseFilterEnum.Ignore;
        vb.AddChild(hdr);
        var text = new Label
        {
            Text = echo.Label,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        text.AddThemeFontSizeOverride("font_size", 13);
        text.AddThemeColorOverride("font_color", Ui.Ink);
        vb.AddChild(text);

        _feedList.AddChild(row);
        _feedList.MoveChild(row, 0);
        _feedVis.Insert(0, new FeedVisRow { Node = row, Yours = false, Weight = int.MaxValue });
        while (_feedVis.Count > FeedWindow)
        {
            var oldest = _feedVis[_feedVis.Count - 1];
            oldest.Node.QueueFree();
            _feedVis.RemoveAt(_feedVis.Count - 1);
        }
    }

    // ------------------------------------------------------------- focus guard

    // Decide whether this event interrupts. A followed soul's death always cards (the player
    // asked to be told); otherwise a major event (imp >= NotableBar, boosted) cards when it's
    // YOURS, or for any event in "all" mode. First trigger in a tick wins, except a death,
    // which outranks an earlier recap.
    private void MaybeArmGuard(Event e, bool yours, int imp)
    {
        if (_guardMode == GuardMode.Off) return;

        if (yours && (e.Type is "death" or "murder"))
            foreach (var pid in e.Participants)
                if (_marked.Contains(pid) && _world.People.TryGetValue(pid, out var dp) && !dp.Alive)
                {
                    // Known limit: two followed deaths in one tick — the first keeps the
                    // card, the second remains a feed row.
                    if (_pendingGuardEventId is null || !_pendingGuardIsDeath)
                        ArmGuard(e, pid, isDeath: true);
                    return;
                }

        if (_pendingGuardEventId is not null || imp < NotableBar) return;
        if (yours || _guardMode == GuardMode.All)
            ArmGuard(e, FirstMarked(e), isDeath: false);
    }

    private void ArmGuard(Event e, int? focusPid, bool isDeath)
    {
        _pendingGuardEventId = e.Id;
        _pendingGuardFocusPid = focusPid;
        _pendingGuardIsDeath = isDeath;
        _pendingGuardPrevSeen = focusPid is int pid ? _lastSeenEvent.GetValueOrDefault(pid, -1) : -1;
    }

    private int? FirstMarked(Event e)
    {
        foreach (var pid in e.Participants)
            if (_marked.Contains(pid)) return pid;
        return null;
    }

    private void RememberSeen(Event e)
    {
        foreach (var pid in e.Participants)
            if (_marked.Contains(pid)) _lastSeenEvent[pid] = e.Id;
    }

    // Consume the armed trigger: pause the world and show the card. The sim is untouched —
    // _running only gates whether _Process keeps calling Tick().
    private void ShowGuardCard()
    {
        if (_pendingGuardEventId is not int eid) return;
        var e = _world.Chronicle.Get(eid);
        int? focusPid = _pendingGuardFocusPid;
        bool isDeath = _pendingGuardIsDeath;
        int prevSeen = _pendingGuardPrevSeen;
        _pendingGuardEventId = null;
        _pendingGuardFocusPid = null;
        _pendingGuardIsDeath = false;
        _pendingGuardPrevSeen = -1;

        _running = false;
        _guardEventId = eid;
        _guardTitle.Text = isDeath ? "Their Tale Ends" : "Focus Guard";

        var cls = Ui.ClassOf(e.Type);
        var sb = new StringBuilder();
        string lead = isDeath ? "a followed tale closes"
            : focusPid is not null ? "fate touches what you follow"
            : "a great deed marks the age";
        sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]{lead} — the world waits[/color]");
        sb.AppendLine();
        string where = e.RegionId is int rid && _world.RegionName(rid) is string rn
            ? $"  [color=#{Ui.Hex(Ui.Faded)}]· in {rn}[/color]" : "";
        sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]Yr {e.Year}[/color]  [color=#{Ui.Hex(cls.Color)}]{cls.Glyph} {cls.Label.ToUpperInvariant()}[/color]  [b]{e.Text}[/b]{where}");

        if (focusPid is int pid && _world.People.TryGetValue(pid, out var p))
        {
            if (isDeath)
            {
                int died = p.DeathYear ?? e.Year;
                sb.AppendLine();
                sb.AppendLine(SectionCap("The record"));
                sb.AppendLine($"{p.Name} — born Yr {p.BirthYear}, died Yr {died}, age {died - p.BirthYear}");
                if (ReputationDisplay(p.Reputation) is (string repText, string repColor))
                    sb.AppendLine($"[color=#{repColor}]{repText}[/color]");
                sb.AppendLine($"children: {p.Children.Count}");
                sb.AppendLine();
                sb.AppendLine(SectionCap("Their tale"));
                // One-shot scan on a card open — same cost class as a person-inspector click,
                // never per-tick.
                var theirs = _world.Chronicle.Events.Where(t => t.Participants.Contains(pid)).TakeLast(6).ToList();
                foreach (var t in theirs)
                {
                    var tc = Ui.ClassOf(t.Type);
                    sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]Yr {t.Year}[/color] [color=#{Ui.Hex(tc.Color)}]{tc.Glyph}[/color] {Link("e:" + t.Id, t.Text)}");
                }
            }
            sb.AppendLine();
            if (prevSeen >= 0 && prevSeen != eid)
            {
                var ls = _world.Chronicle.Get(prevSeen);
                sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]you last saw {p.Name}: Yr {ls.Year} —[/color] {Link("e:" + ls.Id, ls.Text)}");
            }
            else
                sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]nothing of {p.Name} has crossed the saga since you began to follow[/color]");
        }

        _guardBody.Text = sb.ToString();
        _guardPanel.Visible = true;
        RememberSeen(e);   // the card itself is a sighting
    }

    private void BuildGuardCard(Control root)
    {
        _guardPanel = new Panel { Visible = false };
        root.AddChild(_guardPanel);
        _guardPanel.AnchorLeft = 0.5f; _guardPanel.AnchorRight = 0.5f;
        _guardPanel.AnchorTop = 0.5f; _guardPanel.AnchorBottom = 0.5f;
        _guardPanel.OffsetLeft = -280; _guardPanel.OffsetRight = 280;
        _guardPanel.OffsetTop = -200; _guardPanel.OffsetBottom = 200;
        var box = Ui.PanelBox();
        box.BorderColor = Ui.Gold;   // gold marks the player's thread, per the style bible
        box.SetBorderWidthAll(2);
        _guardPanel.AddThemeStyleboxOverride("panel", box);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        foreach (var s in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{s}", 14);
        _guardPanel.AddChild(margin);

        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 8);
        margin.AddChild(vb);

        var hb = new HBoxContainer();
        hb.AddThemeConstantOverride("separation", 8);
        vb.AddChild(hb);
        _guardTitle = new Label { Text = "Focus Guard", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _guardTitle.AddThemeFontOverride("font", Ui.SerifBold);
        _guardTitle.AddThemeFontSizeOverride("font_size", 21);
        _guardTitle.AddThemeColorOverride("font_color", Ui.InkDeep);
        hb.AddChild(_guardTitle);
        var close = new Button { Text = "✕", CustomMinimumSize = new Vector2(28, 28), TooltipText = "Close (stay paused)" };
        Ui.StyleButton(close);
        close.Pressed += () => _guardPanel.Visible = false;
        hb.AddChild(close);

        var rule = new HSeparator();
        rule.AddThemeStyleboxOverride("separator", new StyleBoxFlat { BgColor = Ui.RowBorder, ContentMarginTop = 1 });
        vb.AddChild(rule);

        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        vb.AddChild(scroll);
        _guardBody = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(508, 0),
        };
        _guardBody.AddThemeColorOverride("default_color", Ui.Ink);
        _guardBody.AddThemeFontOverride("bold_font", Ui.SerifBold);
        _guardBody.MetaClicked += meta => { _guardPanel.Visible = false; OnInspectorLink(meta.AsString()); };
        scroll.AddChild(_guardBody);

        var btns = new HBoxContainer();
        btns.AddThemeConstantOverride("separation", 8);
        vb.AddChild(btns);
        var resume = new Button { Text = "▶ Resume" };
        Ui.StyleButton(resume, active: true);
        resume.Pressed += () => { _guardPanel.Visible = false; _running = true; };
        btns.AddChild(resume);
        var trace = new Button { Text = "How We Got Here" };
        Ui.StyleButton(trace);
        trace.Pressed += () => { _guardPanel.Visible = false; if (_guardEventId >= 0) OpenCatchup(_guardEventId); };
        btns.AddChild(trace);
    }

    // -------------------------------------------------------------- inspectors

    private void OpenCatchup(int eventId)
    {
        _catchupEventId = eventId;
        _catchupQuick = true;
        _catchupPanel.Visible = true;
        RenderCatchup();
    }

    private void RenderCatchup()
    {
        if (_catchupEventId is not int id) return;
        Ui.StyleButton(_catchupQuickBtn, _catchupQuick);
        Ui.StyleButton(_catchupFullBtn, !_catchupQuick);

        var chain = _world.Chronicle.Trace(id);   // event + all its causes, in year order
        var target = chain.FirstOrDefault(e => e.Id == id);

        var sb = new StringBuilder();
        sb.AppendLine($"[b]{(target is null ? "" : target.Text)}[/b]");
        sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]{(_catchupQuick ? "the turning points" : "the full thread")} that led here[/color]");
        sb.AppendLine();

        var shown = _catchupQuick
            ? chain.Where(e => e.Id == id || e.Type is not ("birth" or "death" or "marriage")).ToList()
            : chain;
        if (shown.Count <= 1)
            sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}](this one stands alone — no deeper causes recorded)[/color]");
        foreach (var e in shown)
        {
            bool isTarget = e.Id == id;
            var cls = Ui.ClassOf(e.Type);
            string year = $"[color=#{Ui.Hex(Ui.Faded)}]Yr {e.Year}[/color]";
            string chip = $"[color=#{Ui.Hex(cls.Color)}]{cls.Glyph} {cls.Label.ToUpperInvariant()}[/color]";
            string body = isTarget ? $"[b]{e.Text}[/b]" : e.Text;
            string where = e.RegionId is int rid && _world.RegionName(rid) is string rn
                ? $"  [color=#{Ui.Hex(Ui.Faded)}]· in {rn}[/color]" : "";
            string line = $"{year}  {chip}  {body}{where}";
            sb.AppendLine(isTarget ? $"[bgcolor=#{Ui.Hex(Ui.RowBgWarm)}]{line}[/bgcolor]" : line);
        }
        _catchup.Text = sb.ToString();
    }

    private void OnCursePressed()
    {
        if (_selectedPersonId is not int id || !_world.People.TryGetValue(id, out var p)) return;
        if (!p.Alive || p.Cursed) return;
        _world.PlantCurse(p);
        StreamNewHeadlines();        // surface the divine act immediately
        if (_pendingGuardEventId is not null) ShowGuardCard();
        _curseBtn.Visible = false;
        OnPersonPicked(id);          // re-render with CURSED state
    }

    private void OnFollowPressed()
    {
        if (_selectedPersonId is int pid)
        {
            if (!_seedPeople.Remove(pid)) _seedPeople.Add(pid);   // toggle
            RecomputeMarked();
            OnPersonPicked(pid);     // refresh the button label
        }
        else if (_selectedFactionId is string fid)
        {
            if (!_markedFactions.Remove(fid)) _markedFactions.Add(fid);
            RecomputeMarked();
            OnFactionPicked(fid);
        }
    }

    // Rebuild the followed bloodline from the explicit marks. The pedigree graph is permanent,
    // so re-expanding the seeds always yields every descendant born so far; future births then
    // extend it incrementally in StreamNewHeadlines. Only runs on a follow/unfollow press.
    private void RecomputeMarked()
    {
        var (people, _) = Feed.ExpandMarked(_world, _seedPeople, _markedFactions);
        _marked.Clear();
        _marked.UnionWith(people);
        _map.QueueRedraw();
    }

    private static string SectionCap(string text)
        => $"[color=#{Ui.Hex(Ui.Faded)}]{text.ToUpperInvariant()}[/color]";

    // Reputation is public memory: admired names render warm gold, infamous names ink-stained
    // dark — never cartoon-villain red. Unremarked people show nothing (sections appear only
    // when meaningful).
    private static (string text, string color)? ReputationDisplay(int rep) => rep switch
    {
        >= 3 => ("Admired — their name is spoken warmly", "8a5d12"),
        >= 1 => ("Well spoken of", "7a6a2a"),
        <= -3 => ("Infamous — a blackened name", "3a2418"),
        <= -1 => ("Whispered against", "5a4632"),
        _ => null,
    };

    private void OnPersonPicked(int id)
    {
        if (!_world.People.TryGetValue(id, out var p)) return;
        _selectedPersonId = id;
        _selectedFactionId = null;
        _map.SelectedFactionId = null;
        _map.SelectedRegionId = -1;
        _lensFactionBtn.Visible = false;
        _curseBtn.Visible = p.Alive && !p.Cursed;
        _followBtn.Visible = true;
        _followBtn.Text = _seedPeople.Contains(id) ? "★ Following bloodline — unfollow" : "☆ Follow this bloodline";
        Ui.StyleButton(_followBtn, _seedPeople.Contains(id));
        var fac = _world.Factions[p.FactionId];
        string faith = p.ReligionId is int r && _world.Religions.TryGetValue(r, out var rr) ? rr.Name : "—";
        string spouse = p.SpouseId is int s && _world.People.TryGetValue(s, out var sp) ? sp.Name : "—";
        string status = p.Alive ? $"alive, age {p.Age(_world.Year)}" : $"died in year {p.DeathYear}";

        _inspectorTitle.Text = p.Name;
        _inspectorSub.Text = $"{(p.Sex == "f" ? "woman" : "man")} of {fac.Name}"
            + (p.IsLeader ? " · leader" : "");

        var sb = new StringBuilder();
        if (p.Cursed) sb.AppendLine($"[color=#{Ui.Hex(Ui.Ember)}][b]✳ CURSED[/b] — a god's mark lies on this bloodline[/color]\n");
        if (ReputationDisplay(p.Reputation) is (string repText, string repColor))
        {
            sb.AppendLine(SectionCap("Reputation"));
            sb.AppendLine($"[color=#{repColor}][b]{repText}[/b][/color]\n");
        }
        sb.AppendLine(SectionCap("The record"));
        sb.AppendLine($"status: {status}");
        sb.AppendLine($"faith: {faith}");
        sb.AppendLine($"spouse: {spouse}");
        sb.AppendLine($"children: {p.Children.Count}");
        if (_marked.Contains(id) && _lastSeenEvent.TryGetValue(id, out var lsId))
        {
            var ls = _world.Chronicle.Get(lsId);
            sb.AppendLine($"last seen in the saga: [color=#{Ui.Hex(Ui.Faded)}]Yr {ls.Year}[/color] — {Link("e:" + ls.Id, ls.Text)}");
        }
        sb.AppendLine();
        sb.AppendLine(SectionCap("Their thread"));
        var theirs = _world.Chronicle.Events.Where(e => e.Participants.Contains(id)).TakeLast(8).ToList();
        if (theirs.Count == 0) sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}](no recorded events yet)[/color]");
        foreach (var e in theirs)
        {
            var cls = Ui.ClassOf(e.Type);
            sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]Yr {e.Year}[/color] [color=#{Ui.Hex(cls.Color)}]{cls.Glyph}[/color] {Link("e:" + e.Id, e.Text)}");
        }

        _inspector.Text = sb.ToString();
        _inspectorPanel.Visible = true;
    }

    // Inspector cross-links: e:<event id> opens How We Got Here, r:<region id> the Region
    // Lens, f:<faction id> the faction inspector. The link targets are real ids the panels
    // already render from — no new lookups.
    private void OnInspectorLink(string link)
    {
        if (link.StartsWith("e:") && int.TryParse(link[2..], out var eid)) OpenCatchup(eid);
        else if (link.StartsWith("r:") && int.TryParse(link[2..], out var rid)) OnRegionPicked(rid);
        else if (link.StartsWith("f:")) OnFactionPicked(link[2..]);
    }

    private static string Link(string target, string text)
        => $"[color=#8a5d12][url={target}]{text}[/url][/color]";

    // The Region Lens (foundation slice): clicking any territory inspects the place itself —
    // its land, its neighbours, and the tales anchored to it — instead of silently handing off
    // to the holder's faction panel (that's now one click deeper, via button or link). Anchored
    // tales are real chronicle events whose RegionId names this region; everything the sim
    // doesn't model yet is said plainly rather than faked.
    private void OnRegionPicked(int regionId)
    {
        if (regionId < 0 || regionId >= _world.Regions.Count) return;
        var region = _world.Regions[regionId];
        var holder = region.ControllingFactionId is string hid ? _world.Factions[hid] : null;

        _selectedPersonId = null;
        _selectedFactionId = null;
        _map.SelectedFactionId = null;
        _map.SelectedRegionId = regionId;
        _curseBtn.Visible = false;
        _followBtn.Visible = false;
        _lensFactionId = holder?.Id;
        _lensFactionBtn.Visible = holder is not null;
        if (holder is not null) _lensFactionBtn.Text = $"⚑ Inspect {holder.Name}";

        _inspectorTitle.Text = region.Name;
        _inspectorSub.Text = $"{region.TerrainType} · {(holder is null ? "wilderness" : $"held by {holder.Name}")}";

        var sb = new StringBuilder();
        sb.AppendLine(SectionCap("The land"));
        sb.AppendLine($"terrain: {region.TerrainType}");
        sb.AppendLine(holder is null
            ? "held by: no one — unclaimed wilderness"
            : $"held by: {Link("f:" + holder.Id, holder.Name)}");
        sb.AppendLine($"map hint: {PlaceSeeds.Label(PlaceSeeds.KindOf(_world, region))} — a viewer's mark, not sim state");
        sb.AppendLine();
        sb.AppendLine(SectionCap("Neighbouring lands"));
        foreach (var nid in region.AdjacentRegionIds)
        {
            var n = _world.Regions[nid];
            string nh = n.ControllingFactionId is string nf ? _world.Factions[nf].Name : "wild";
            sb.AppendLine($"{Link("r:" + n.Id, n.Name)} — {n.TerrainType} · {nh}");
        }
        sb.AppendLine();
        int total = _regionActivity.TotalFor(regionId);
        sb.AppendLine(SectionCap("Tales anchored here")
            + (total > 0 ? $" [color=#{Ui.Hex(Ui.Faded)}]({total} recorded)[/color]" : ""));
        var recent = _regionActivity.RecentFor(regionId);
        if (recent.Count == 0)
            sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]no anchored tales yet — recorded history has not named this place[/color]");
        for (int i = recent.Count - 1; i >= 0; i--)   // newest first
        {
            var e = _world.Chronicle.Get(recent[i]);
            var cls = Ui.ClassOf(e.Type);
            sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]Yr {e.Year}[/color] [color=#{Ui.Hex(cls.Color)}]{cls.Glyph}[/color] {Link("e:" + e.Id, e.Text)}");
        }
        sb.AppendLine();
        sb.AppendLine(SectionCap("Not yet in the record"));
        sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]people are not yet site-anchored — the atlas scatters each people across their lands[/color]");
        sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]settlements are not modeled yet — the place marker is a map hint only[/color]");

        _inspector.Text = sb.ToString();
        _inspectorPanel.Visible = true;
    }

    private void OnFactionPicked(string fid)
    {
        _selectedPersonId = null;
        _selectedFactionId = fid;
        _map.SelectedFactionId = fid;
        _map.SelectedRegionId = -1;
        _lensFactionBtn.Visible = false;
        _curseBtn.Visible = false;
        _followBtn.Visible = true;
        _followBtn.Text = _markedFactions.Contains(fid) ? "★ Following — unfollow" : "☆ Follow this people";
        Ui.StyleButton(_followBtn, _markedFactions.Contains(fid));
        var fac = _world.Factions[fid];
        var members = _world.FactionMembers(fid);
        string leader = fac.LeaderId is int lid ? _world.People[lid].Name : "(none)";
        var dom = _world.DominantReligion(fid);

        _inspectorTitle.Text = fac.Name;
        _inspectorSub.Text = $"{fac.Culture} culture · of {fac.Homeland}";

        var sb = new StringBuilder();
        sb.AppendLine(SectionCap("The record"));
        sb.AppendLine($"living: {members.Count}");
        sb.AppendLine($"leader: {leader}");
        sb.AppendLine($"dominant faith: {dom?.Name ?? "—"}");
        // Customs appear only once a value axis has hardened into one (M7 culture engine).
        var customs = fac.CustomOriginEvent.Keys.OrderBy(c => c).ToList();
        if (customs.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(SectionCap("Customs they keep"));
            foreach (var c in customs)
                sb.AppendLine($"[color=#{Ui.Hex(Ui.Violet)}]❧[/color] {c}");
        }
        // "Map role" is viewer language — the deterministic place marker drawn on the map,
        // not sim settlement data (the sim has no settlements yet).
        var lands = fac.ControlledRegions.Select(int.Parse).OrderBy(i => i)
            .Select(i => _world.Regions[i]).ToList();
        if (lands.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(SectionCap("Their lands"));
            foreach (var r in lands.Take(8))
                sb.AppendLine($"{Link("r:" + r.Id, r.Name)} — {r.TerrainType} · map role: {PlaceSeeds.Label(PlaceSeeds.KindOf(_world, r))}");
            if (lands.Count > 8)
                sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]…and {lands.Count - 8} more[/color]");
        }
        sb.AppendLine();
        sb.AppendLine(SectionCap("Eldest among them"));
        foreach (var p in members.OrderByDescending(p => p.Age(_world.Year)).Take(8))
            sb.AppendLine($"{p.Name} — age {p.Age(_world.Year)}{(p.IsLeader ? $"  [color=#8a5d12]· leader[/color]" : "")}");

        _inspector.Text = sb.ToString();
        _inspectorPanel.Visible = true;
    }

    // -------------------------------------------------------------- controls

    private void TogglePlay() => _running = !_running;

    private void SetSpeed(float s)
    {
        _speed = s;
        if (!_running) _running = true;
        RestyleSpeedButtons();
    }

    private void RefreshTimeBar()
    {
        _yearBig.Text = $"Year {_world.Year}";
        _yearSub.Text = $"{_world.LivingCount} souls · {_world.Chronicle.Events.Count} tales";
        bool guardActive = _guardMode != GuardMode.Off && (_seedPeople.Count > 0 || _markedFactions.Count > 0);
        _guardLabel.Visible = guardActive;
        if (guardActive)
        {
            var parts = new List<string>();
            if (_seedPeople.Count > 0) parts.Add($"{_seedPeople.Count} bloodline{(_seedPeople.Count == 1 ? "" : "s")}");
            if (_markedFactions.Count > 0) parts.Add($"{_markedFactions.Count} people{(_markedFactions.Count == 1 ? "" : "s")}");
            _guardLabel.Text = "⛨ guard watches " + string.Join(" · ", parts);
        }
        _playBtn.Text = _running ? "❚❚ Pause" : "▶ Play";
        _chatLabel.Text = $"chattiness ≥ {(int)_chatSlider.Value}";
    }
}
