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
    private Button _soulBtn = null!;
    private Button _lensFactionBtn = null!;
    private string? _lensFactionId;          // faction behind the Region Lens hand-off button
    private int? _selectedPersonId;
    private string? _selectedFactionId;
    private readonly HashSet<int> _seedPeople = new();      // the people the player explicitly marked
    private readonly HashSet<int> _marked = new();          // their full bloodline, expanded
    private readonly HashSet<string> _markedFactions = new();
    private readonly HashSet<int> _followedSouls = new();   // souls followed as individuals — never expanded into kin
    private readonly HashSet<int> _followedRegions = new(); // lands watched as places — tales anchored here and lives remembered here are YOURS
    private Button _regionBtn = null!;
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
    // Player canon — the third ledger (PROJECT_STATE.md "Truth model V1"): the player's
    // hand over the world, loaded once per session, written only on an explicit save,
    // never read by the sim. The editor pauses time and restores the pace it took.
    private PlayerCanonStore _canon = null!;
    private CanonPanel _canonPanel = null!;
    private bool _canonReturnsToGuard;   // the desk was opened FROM the held card — return there on close
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
    // off / followed (a major event touches what you follow, or a followed death) / all
    // (any major event). V2 adds following one specific soul (no bloodline expansion) and a
    // memorial treatment when that soul dies. Pausing is wall-clock presentation only — it
    // never changes how many times or in what order Tick() runs.
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
    private bool _pendingGuardIsSoulDeath;                // the dead was followed as a soul, not just as kin
    private int _pendingGuardPrevSeen = -1;               // their last-seen event BEFORE this one (-1 none)
    private ColorRect _guardBackdrop = null!;             // memorial dim — the world holds its breath
    private HSeparator _guardRule = null!;
    private PanelContainer _guardChip = null!;            // memorial medallion: the event-class glyph
    private Label _guardChipGlyph = null!;
    private Button _guardReturnBtn = null!;               // floating way back to the held card
    private bool _guardWasMemorial;                       // restore the dim on return
    private int _guardFocusPid = -1;                      // the held moment, kept re-renderable
    private bool _guardIsDeath;
    private int _guardPrevSeen = -1;
    private bool _guardReturnable;                        // true while the world still holds its breath
    private bool _returnIsRecap;                          // the held card is a chapter recap, not a guard card

    // Chapter recaps (docs/TIME_AND_STORY_PACING.md, roadmap 3). A chapter is a span of SHOWN
    // years — Drama/Focus Time, never sim state. It closes after ChapterYears shown years, or
    // early on an arc closure: a myth echo carding, or a followed soul's memorial. Closing only
    // QUEUES the recap (a chip by the year card); the card itself shows when the player next
    // pauses, or on chip click — the stream is never auto-paused for a recap, and a queued recap
    // never interrupts a focus-guard card. Only the latest unread chapter is offered.
    private const int ChapterYears = 25;                  // shown years per chapter (≈ a generation)
    private int _chapterStartYear;
    private int _chapterStartEventId;
    private int _chapterShownYears;
    private string? _chapterCloseReason;                  // non-null = arc closure armed this frame
    private readonly System.Collections.Generic.Dictionary<int, int> _chapterRepBase = new();      // followed soul -> reputation when the chapter (or the follow) began
    private readonly System.Collections.Generic.Dictionary<string, int> _chapterRegionBase = new();// followed people -> region count when the chapter (or the follow) began
    private readonly List<(string Archetype, string Label, int Anchor)> _chapterEchoes = new();
    // Living soul glimpse (Living Diorama V1): a small parchment card for a watched soul,
    // opened by clicking their map marker. Non-modal and never pauses — the memorial always
    // outranks it (guard card + backdrop are built later, so they draw above).
    private Panel _glimpsePanel = null!;
    private Label _glimpseTitle = null!;
    private Label _glimpseSub = null!;
    private RichTextLabel _glimpseBody = null!;
    private Button _glimpseThreadBtn = null!;
    private int _glimpsePid = -1;
    private int _glimpseThreadEvent = -1;
    private static readonly Vector2 GlimpseSize = new(280, 270);

    private RecapSnapshot? _queuedRecap;
    private Panel _recapPanel = null!;
    private Label _recapSub = null!;
    private RichTextLabel _recapBody = null!;
    private Button _recapChip = null!;                    // "a chapter closed" — click to read
    private bool _wasRunning = true;                      // pause-transition edge for auto-showing a queued recap

    private sealed class RecapSnapshot
    {
        public int StartYear, EndYear, StartEventId, EndEventId;
        public string Reason = "";
        public List<(string Archetype, string Label, int Anchor)> Echoes = new();
        public System.Collections.Generic.Dictionary<int, int> RepBase = new();
        public System.Collections.Generic.Dictionary<string, int> RegionBase = new();
    }

    private sealed class FeedVisRow { public Node Node = null!; public bool Yours; public int Weight; }

    public override void _Ready()
    {
        var (config, names) = DataLoader.Load();
        _world = new World(Seed, config, names);
        _world.SeedWorld();
        _lastEventCount = 0;
        _lastEchoYear = _world.Year;

        LoadCanon();
        Ui.LoadFonts();
        BuildUi();
        _map.World = _world;
        _map.Marked = _marked;       // same HashSet, mutated in place — map sees follows live
        _map.Souls = _followedSouls;
        _map.FollowedRegions = _followedRegions;
        StreamNewHeadlines();
        if (_pendingGuardEventId is not null) ShowGuardCard();   // unreachable today; hardening
        StartChapter(0);   // chapter one opens on the founding events themselves
        RefreshTimeBar();
        _map.QueueRedraw();
    }

    // Load the player's canon book for this seed. An unreadable file is set aside as
    // .bak (never destroyed) and a fresh book opens; a file from a NEWER build stays
    // untouched and this session simply cannot write (affordances hide on ReadOnly).
    private void LoadCanon()
    {
        string path = ProjectSettings.GlobalizePath($"user://canon_seed{Seed}.json");
        var (canon, warning) = PlayerCanonStore.LoadOrNew(path, Seed);
        if (warning is not null)
        {
            GD.PushWarning($"player canon: {warning}");
            if (canon.ReadOnly && !canon.FutureSchema)
            {
                try
                {
                    System.IO.File.Move(path, path + ".bak", overwrite: false);
                    GD.PushWarning($"player canon: unreadable file set aside as {path}.bak");
                    (canon, _) = PlayerCanonStore.LoadOrNew(path, Seed);
                }
                catch (System.Exception ex) when (ex is System.IO.IOException or System.UnauthorizedAccessException)
                { /* a .bak already exists or the file is locked — stay read-only this session */ }
            }
        }
        _canon = canon;
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
                _chapterShownYears++;
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
            if (_chapterCloseReason is not null || _chapterShownYears >= ChapterYears)
                CloseChapter(_chapterCloseReason ?? "a generation told");
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

        _map = new MapView { PersonPicked = OnPersonPicked, SoulPicked = OnSoulGlimpse, FactionPicked = OnFactionPicked, RegionPicked = OnRegionPicked };
        root.AddChild(_map);
        _map.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _map.OffsetRight = -FeedWidth;
        _map.OffsetBottom = -BottomH;

        BuildFeed(root);
        BuildBottomBar(root);
        BuildYearCard(root);
        BuildInspector(root);
        BuildCatchup(root);
        BuildGlimpse(root);
        BuildRecap(root);
        BuildGuardCard(root);   // last: the guard card (and its return chip) sits above everything
        BuildCanonPanel(root);  // very last: the player's writing desk outranks every card while open
    }

    private void BuildCanonPanel(Control root)
    {
        _canonPanel = new CanonPanel();
        root.AddChild(_canonPanel);
        _canonPanel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _canonPanel.Setup(_canon, () => _world, () => _running, r => _running = r, OnCanonClosed);
    }

    // After the writing desk closes: re-render whatever surface should now carry the
    // telling, then — if a guard moment was still held beneath — bring the card back the
    // same way the return chip would, with its body rebuilt so a fresh inscription shows.
    private void OnCanonClosed(bool changed)
    {
        if (changed)
        {
            if (_catchupPanel.Visible) RenderCatchup();
            if (_selectedPersonId is int pid) OnPersonPicked(pid);
            else if (_selectedFactionId is string fid) OnFactionPicked(fid);
            else if (_map.SelectedRegionId >= 0) OnRegionPicked(_map.SelectedRegionId);
        }
        // Return to the held card only when the desk was opened FROM it — a dismissed
        // card keeps its chip as an offer, never a forced return.
        if (_canonReturnsToGuard && _guardReturnable && !_running && !_guardPanel.Visible
            && !_catchupPanel.Visible && !_recapPanel.Visible && !_returnIsRecap)
        {
            RerenderHeldGuardCard();
            _guardBackdrop.Visible = _guardWasMemorial;
            _guardPanel.Visible = true;
        }
        _canonReturnsToGuard = false;
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
        close.Pressed += () =>
        {
            _inspectorPanel.Visible = false;
            _map.SelectedFactionId = null;
            _map.SelectedRegionId = -1;
            // Forget the selection too, or a later canon save re-renders (and re-opens)
            // an inspector the player deliberately dismissed.
            _selectedPersonId = null;
            _selectedFactionId = null;
        };
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

        _regionBtn = new Button { Text = "☆ Follow this land", Visible = false,
            TooltipText = "Watch this place — tales anchored here and lives remembered here surface as yours" };
        Ui.StyleButton(_regionBtn);
        _regionBtn.Pressed += OnFollowRegionPressed;
        vb.AddChild(_regionBtn);

        // Fate verbs pinned at the bottom: the curse tool (a living, not-yet-cursed person only)
        // and the Yours channel (follow this one soul / a bloodline / a people).
        _soulBtn = new Button { Text = "☆ Follow this soul", Visible = false,
            TooltipText = "Watch this one life — their death becomes a memorial moment" };
        Ui.StyleButton(_soulBtn);
        _soulBtn.Pressed += OnFollowSoulPressed;
        vb.AddChild(_soulBtn);

        _followBtn = new Button { Text = "☆ Follow", Visible = false,
            TooltipText = "Watch a whole line — kin and descendants join as they are born" };
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
        // The thread is walkable: e: retargets this panel in place, canon: opens the
        // writing desk above it (OnCanonClosed re-renders the thread on save).
        _catchup.MetaClicked += meta => OnInspectorLink(meta.AsString());
        scroll.AddChild(_catchup);
    }

    // ------------------------------------------------------- living soul glimpse

    private void BuildGlimpse(Control root)
    {
        _glimpsePanel = new Panel { Visible = false, Size = GlimpseSize };
        root.AddChild(_glimpsePanel);
        var box = Ui.PanelBox();
        box.BorderColor = Ui.Gold;
        _glimpsePanel.AddThemeStyleboxOverride("panel", box);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        foreach (var s in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{s}", 12);
        _glimpsePanel.AddChild(margin);

        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 4);
        margin.AddChild(vb);

        var hdr = new HBoxContainer();
        vb.AddChild(hdr);
        var cap = Ui.SectionLabel("★ a soul you watch");
        cap.AddThemeColorOverride("font_color", Ui.Gold);
        cap.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hdr.AddChild(cap);
        var close = new Button { Text = "✕", CustomMinimumSize = new Vector2(24, 24) };
        Ui.StyleButton(close);
        close.Pressed += () => _glimpsePanel.Visible = false;
        hdr.AddChild(close);

        _glimpseTitle = new Label { Text = "" };
        _glimpseTitle.AddThemeFontOverride("font", Ui.SerifBold);
        _glimpseTitle.AddThemeFontSizeOverride("font_size", 18);
        _glimpseTitle.AddThemeColorOverride("font_color", Ui.InkDeep);
        vb.AddChild(_glimpseTitle);
        _glimpseSub = new Label { Text = "" };
        _glimpseSub.AddThemeFontSizeOverride("font_size", 12);
        _glimpseSub.AddThemeColorOverride("font_color", Ui.FadedSub);
        vb.AddChild(_glimpseSub);

        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        vb.AddChild(scroll);
        _glimpseBody = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(232, 0),
        };
        _glimpseBody.AddThemeFontSizeOverride("normal_font_size", 13);
        _glimpseBody.AddThemeColorOverride("default_color", Ui.Ink);
        _glimpseBody.AddThemeFontOverride("bold_font", Ui.SerifBold);
        _glimpseBody.MetaClicked += meta => { _glimpsePanel.Visible = false; OnInspectorLink(meta.AsString()); };
        scroll.AddChild(_glimpseBody);

        var btns = new HBoxContainer();
        btns.AddThemeConstantOverride("separation", 6);
        vb.AddChild(btns);
        _glimpseThreadBtn = new Button { Text = "thread", TooltipText = "How We Got Here, from their latest tale" };
        Ui.StyleButton(_glimpseThreadBtn);
        _glimpseThreadBtn.Pressed += () =>
        {
            if (_glimpseThreadEvent < 0) return;
            _glimpsePanel.Visible = false;
            OpenCatchup(_glimpseThreadEvent);
        };
        btns.AddChild(_glimpseThreadBtn);
        var record = new Button { Text = "the record", TooltipText = "Open the full inspector" };
        Ui.StyleButton(record);
        record.Pressed += () =>
        {
            if (_glimpsePid < 0) return;
            _glimpsePanel.Visible = false;
            OnPersonPicked(_glimpsePid);
        };
        btns.AddChild(record);
        var unfollow = new Button { Text = "★ unfollow" };
        Ui.StyleButton(unfollow);
        unfollow.Pressed += () =>
        {
            if (_glimpsePid >= 0 && _followedSouls.Remove(_glimpsePid)) _map.QueueRedraw();
            _glimpsePanel.Visible = false;
        };
        btns.AddChild(unfollow);
    }

    // The glimpse answers, from real fields only: who is this, are they alive, whose are
    // they, what has the saga shown me of them. The deeds list is a one-shot scan on click —
    // inspector cost class, never per-tick.
    private void OnSoulGlimpse(int pid, Vector2 mapPos)
    {
        if (!_world.People.TryGetValue(pid, out var p)) return;
        _glimpsePid = pid;
        var fac = _world.Factions[p.FactionId];
        _glimpseTitle.Text = p.Name;
        _glimpseSub.Text = $"of {fac.Name}"
            + (p.IsLeader ? " · leader" : p.EverLeader ? " · once their leader" : "");

        var sb = new StringBuilder();
        sb.AppendLine(p.Alive
            ? $"alive · age {p.Age(_world.Year)} · born Yr {p.BirthYear}"
            : $"died Yr {p.DeathYear}");
        if (ReputationDisplay(p.Reputation) is (string repText, string repColor))
            sb.AppendLine($"[color=#{repColor}]{repText}[/color]");
        if (p.Children.Count > 0)
            sb.AppendLine($"{p.Children.Count} {(p.Children.Count == 1 ? "child" : "children")}");
        sb.AppendLine();
        if (_lastSeenEvent.TryGetValue(pid, out var lsId))
        {
            var ls = _world.Chronicle.Get(lsId);
            sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]you last saw them: Yr {ls.Year} —[/color] {Link("e:" + ls.Id, ls.Text)}");
        }
        else
            sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]nothing of them has crossed the saga since you began to follow[/color]");

        var deeds = _world.Chronicle.Events.Where(e => e.Participants.Contains(pid)).TakeLast(3).ToList();
        _glimpseThreadEvent = deeds.Count > 0 ? deeds[^1].Id : -1;
        _glimpseThreadBtn.Visible = _glimpseThreadEvent >= 0;
        if (deeds.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(SectionCap("Their recent deeds"));
            foreach (var e in deeds)
            {
                var cls = Ui.ClassOf(e.Type);
                sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]Yr {e.Year}[/color] [color=#{Ui.Hex(cls.Color)}]{cls.Glyph}[/color] {Link("e:" + e.Id, e.Text)}");
            }
        }
        _glimpseBody.Text = sb.ToString();

        // Near the marker, clamped to the map area so the card never covers the feed or dock.
        var pos = mapPos + new Vector2(18, -GlimpseSize.Y / 2f);
        pos.X = Mathf.Clamp(pos.X, 8, Mathf.Max(8, _map.Size.X - GlimpseSize.X - 8));
        pos.Y = Mathf.Clamp(pos.Y, 8, Mathf.Max(8, _map.Size.Y - GlimpseSize.Y - 8));
        _glimpsePanel.Position = pos;
        _glimpsePanel.Visible = true;
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
            // Place memory: a truly anchored event of a marking kind scars its region.
            if (events[i].RegionId is int mrid && ClassifyMark(events[i]) is MapView.MarkKind mk)
                _map.AddPlaceMark(mrid, mk, events[i].Year, events[i].Id);
            // Life memory: a cairn-worthy life raises a memorial cairn at the home of its line
            // (Event.HomeRegionId) — remembered there, never a claim of where it happened.
            if (events[i].HomeRegionId is int hrid && IsCairnWorthy(events[i]))
                _map.AddHomeMark(hrid, events[i].Year, events[i].Id);
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

            // A specifically watched soul in the tale earns the row a gold side rule and
            // flares their map halo — only when they truly are a participant.
            bool soul = false;
            if (_followedSouls.Count > 0)
                foreach (var pid in e.Participants)
                    if (_followedSouls.Contains(pid)) { soul = true; break; }

            var row = AddFeedRow(e, imp, yours, soul);
            // Last-seen memory records only what was actually shown (this row, or a guard
            // card — see ShowGuardCard), so "you last saw…" never cites an undisplayed event.
            if (yours && row is not null) RememberSeen(e);
            if (soul && row is not null)
                foreach (var pid in e.Participants)
                    if (_followedSouls.Contains(pid)) _map.PulseSoul(pid);
            // Yours always gets the spotlight; otherwise a high importance bar (well above
            // chattiness) catches divine/war/founding and ignores routine births/deaths.
            if (row is not null && (yours || imp >= NotableBar))
            {
                notableSeen = true;
                PulseFeedRow(row);
                if (e.RegionId is int rid) _map.PulseRegion(rid);
            }
        }
        // An open glimpse is a snapshot; if its soul died this tick (e.g. guard off, no
        // memorial to close it), retire it rather than keep asserting "alive". O(1).
        if (_glimpsePanel.Visible && _glimpsePid >= 0
            && _world.People.TryGetValue(_glimpsePid, out var gp) && !gp.Alive)
            _glimpsePanel.Visible = false;
        _lastEventCount = events.Count;
        return notableSeen;
    }

    // Which remembered lives raise a memorial cairn at home (Life Memory marks). Murders always —
    // violent grief is carried home; deaths only of those who ever led, so cairns stay rare enough
    // to read (a plain death never clears the bar). Births never mark — a cairn is a memorial.
    // EverLeader is final by death, so the gate never depends on playback pacing.
    private bool IsCairnWorthy(Event e) => e.Type switch
    {
        "murder" => true,
        "death" => e.Participants.Count > 0
            && _world.People.TryGetValue(e.Participants[0], out var p) && p.EverLeader,
        _ => false,
    };

    // Which anchored events scar the land (Place Memory V1). Rumors carry a RegionId too but
    // deliberately don't mark — gossip is social, not a physical scar on a place.
    private static MapView.MarkKind? ClassifyMark(Event e) => e.Type switch
    {
        "territory" when e.Tags.Contains("founding") => MapView.MarkKind.FoundingStone,
        "territory" when e.Tags.Contains("war") => MapView.MarkKind.WarScar,
        "territory" when e.Tags.Contains("abandonment") => MapView.MarkKind.AbandonCairn,
        "custom" => MapView.MarkKind.CultureRibbon,
        _ => null,
    };

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
            if (_marked.Contains(pid) || _followedSouls.Contains(pid)) return true;
            if (_markedFactions.Count > 0 && _world.People.TryGetValue(pid, out var p)
                && _markedFactions.Contains(p.FactionId)) return true;
        }
        return RegionYours(e);
    }

    // A followed land claims its own story through the two honest channels only: tales truly
    // anchored here (RegionId) and lives remembered here (HomeRegionId) — never inference.
    private bool RegionYours(Event e)
        => _followedRegions.Count > 0
        && ((e.RegionId is int rid && _followedRegions.Contains(rid))
            || (e.HomeRegionId is int hrid && _followedRegions.Contains(hrid)));

    // One Saga row, per the handoff anatomy: event-class chip → small-caps label + faded year
    // → one-to-two-line body. Hover warms the border; the whole row opens "How We Got Here".
    private Control BuildFeedRowControl(Event e, int imp, bool yours, bool soul)
    {
        var cls = Ui.ClassOf(e.Type);
        var row = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        var normal = Ui.RowBox(yours ? Ui.RowBgWarm : Ui.RowBg, yours ? Ui.Gold : Ui.RowBorder);
        var hover = Ui.RowBox(Ui.RowBgWarm, soul ? Ui.Gold : Ui.RowBorderHover);
        // A watched soul's tale carries a gold side rule — quieter than a card, louder than gold trim.
        if (soul) { normal.BorderWidthLeft = 4; hover.BorderWidthLeft = 4; }
        row.AddThemeStyleboxOverride("panel", normal);
        row.MouseEntered += () => row.AddThemeStyleboxOverride("panel", hover);
        row.MouseExited += () => row.AddThemeStyleboxOverride("panel", normal);
        row.TooltipText = (soul ? "a watched soul is in this tale — " : "")
            + (e.Causes.Count > 0 ? "follows from an earlier tale — " : "")
            + $"weight {imp} — click: how we got here";
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
        // ⤷ marks a tale with recorded causes — Causes.Count only, no grammar on the hot path.
        var year = new Label { Text = (e.Causes.Count > 0 ? "⤷ " : "") + $"Yr {e.Year}", MouseFilter = Control.MouseFilterEnum.Ignore };
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

    private Control? AddFeedRow(Event e, int imp, bool yours, bool soul = false)
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

        var row = BuildFeedRowControl(e, imp, yours, soul);
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
        _chapterEchoes.Add((echo.Archetype, echo.Label, anchorEventId));
        _chapterCloseReason ??= "a myth echoed";   // an echo carding closes the chapter


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

    // Decide whether this event interrupts. A followed death always cards (the player asked
    // to be told); otherwise a major event (imp >= NotableBar, boosted) cards when it's
    // YOURS, or for any event in "all" mode. First trigger in a tick wins, except a death,
    // which outranks an earlier recap — and a followed *soul's* death (the memorial) outranks
    // a bloodline death. Known limit: two soul deaths in one tick — the first keeps the
    // memorial, the second remains a feed row.
    private void MaybeArmGuard(Event e, bool yours, int imp)
    {
        if (_guardMode == GuardMode.Off) return;

        if (yours && (e.Type is "death" or "murder"))
            foreach (var pid in e.Participants)
                if ((_marked.Contains(pid) || _followedSouls.Contains(pid))
                    && _world.People.TryGetValue(pid, out var dp) && !dp.Alive)
                {
                    bool soul = _followedSouls.Contains(pid);
                    if (_pendingGuardEventId is null || !_pendingGuardIsDeath
                        || (soul && !_pendingGuardIsSoulDeath))
                        ArmGuard(e, pid, isDeath: true, isSoulDeath: soul);
                    return;
                }

        if (_pendingGuardEventId is not null || imp < NotableBar) return;
        if (yours || _guardMode == GuardMode.All)
            ArmGuard(e, FirstMarked(e), isDeath: false, isSoulDeath: false);
    }

    private void ArmGuard(Event e, int? focusPid, bool isDeath, bool isSoulDeath)
    {
        _pendingGuardEventId = e.Id;
        _pendingGuardFocusPid = focusPid;
        _pendingGuardIsDeath = isDeath;
        _pendingGuardIsSoulDeath = isSoulDeath;
        _pendingGuardPrevSeen = focusPid is int pid ? _lastSeenEvent.GetValueOrDefault(pid, -1) : -1;
    }

    // The card's focus: a followed soul first (the player's most specific mark), then kin.
    private int? FirstMarked(Event e)
    {
        foreach (var pid in e.Participants)
            if (_followedSouls.Contains(pid)) return pid;
        foreach (var pid in e.Participants)
            if (_marked.Contains(pid)) return pid;
        return null;
    }

    private void RememberSeen(Event e)
    {
        foreach (var pid in e.Participants)
            if (_marked.Contains(pid) || _followedSouls.Contains(pid)) _lastSeenEvent[pid] = e.Id;
    }

    // Consume the armed trigger: pause the world and show the card. The sim is untouched —
    // _running only gates whether _Process keeps calling Tick(). A followed soul's death
    // gets the memorial treatment: dimmed world, larger ceremonial frame, the dead's name
    // and record given real weight — all of it chronicle truth, nothing invented.
    private void ShowGuardCard()
    {
        if (_pendingGuardEventId is not int eid) return;
        var e = _world.Chronicle.Get(eid);
        int? focusPid = _pendingGuardFocusPid;
        bool isDeath = _pendingGuardIsDeath;
        bool memorial = _pendingGuardIsSoulDeath;
        int prevSeen = _pendingGuardPrevSeen;
        _pendingGuardEventId = null;
        _pendingGuardFocusPid = null;
        _pendingGuardIsDeath = false;
        _pendingGuardIsSoulDeath = false;
        _pendingGuardPrevSeen = -1;

        _running = false;
        _glimpsePanel.Visible = false;   // the moment outranks a living glimpse
        _guardEventId = eid;
        _guardWasMemorial = memorial;
        _guardFocusPid = focusPid ?? -1;
        _guardIsDeath = isDeath;
        _guardPrevSeen = prevSeen;
        _guardReturnable = true;
        _returnIsRecap = false;
        _guardReturnBtn.Text = memorial ? "↩ Return to the memorial" : "↩ Return to the tale";
        if (memorial) _chapterCloseReason ??= "a followed soul's tale ended";   // a memorial closes the chapter
        _guardTitle.Text = isDeath ? "Their Tale Ends" : "Focus Guard";

        var cls = Ui.ClassOf(e.Type);
        StyleGuardCard(memorial, cls);
        // A real place pulses first; failing that, the home the soul is remembered in.
        if (memorial && (e.RegionId ?? e.HomeRegionId) is int prid) _map.PulseRegion(prid);

        _guardBody.Text = BuildGuardBody(e, focusPid, isDeath, memorial, prevSeen);
        _guardBackdrop.Visible = memorial;
        _guardPanel.Visible = true;
        RememberSeen(e);   // the card itself is a sighting
    }

    // Re-render the held card's body from the stored moment — used when the writing desk
    // closes over a held card, so a fresh memorial inscription shows on the way back.
    private void RerenderHeldGuardCard()
    {
        if (_guardEventId < 0) return;
        var e = _world.Chronicle.Get(_guardEventId);
        _guardBody.Text = BuildGuardBody(e, _guardFocusPid >= 0 ? _guardFocusPid : null,
            _guardIsDeath, _guardWasMemorial, _guardPrevSeen);
    }

    // The card body, pure string-building: every line is chronicle truth or a grammar-
    // proven claim — nothing invented, nothing stored.
    private string BuildGuardBody(Event e, int? focusPid, bool isDeath, bool memorial, int prevSeen)
    {
        var cls = Ui.ClassOf(e.Type);
        var sb = new StringBuilder();
        string lead = memorial ? "a soul you followed has died — the world holds its breath"
            : isDeath ? "a tale of a bloodline you follow closes — the world waits"
            : focusPid is not null ? "fate touches what you follow — the world waits"
            : RegionYours(e) ? $"fate touches {StoryCopy.Hint("a land you watch", "followed land")} — the world waits"
            : "a great deed marks the age — the world waits";
        sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]{lead}[/color]");
        sb.AppendLine();
        // Place language stays honest: "in X" only for a true event place; a home anchor is
        // memory ("remembered in"), never where it happened; no anchor at all is said plainly.
        string where = e.RegionId is int rid && _world.RegionName(rid) is string rn
            ? $"  [color=#{Ui.Hex(Ui.Faded)}]· in {rn}[/color]"
            : e.HomeRegionId is int hrid && _world.RegionName(hrid) is string hrn
            ? $"  [color=#{Ui.Hex(Ui.Faded)}]· {(e.Type == "birth" ? $"of a line {StoryCopy.Hint("rooted in", "rooted in")} {hrn}"
                : e.Type == "murder" ? $"{StoryCopy.Hint("remembered in", "remembered in")} {hrn}, the home of the slain's line"
                : $"{StoryCopy.Hint("remembered in", "remembered in")} {hrn}, the home of their line")}[/color]"
            : memorial ? $"  [color=#{Ui.Hex(Ui.Faded)}]· the chronicle records no place for this passing[/color]" : "";
        sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]Yr {e.Year}[/color]  [color=#{Ui.Hex(cls.Color)}]{cls.Glyph} {cls.Label.ToUpperInvariant()}[/color]  [b]{e.Text}[/b]{where}");
        // Why this touched your guard: the one proven cause behind the moment, voiced
        // through the grammar — never invented. Clicking it walks into the full thread.
        if (StoryGrammar.ProximateLink(_world, e) is ChainLink why)
        {
            var causeEv = _world.Chronicle.Get(why.CauseEventId);
            sb.AppendLine($"[color=#{Ui.Hex(Ui.FadedSub)}][i]{StoryCopy.WhyLead(why)}[/i] Yr {causeEv.Year} —[/color] {Link("e:" + causeEv.Id, causeEv.Text)}");
        }
        // Said only when the mark truly stands (cairn-worthy lives only — a follow alone never
        // raises one): home-memory language, never a death place. A murder's cairn belongs to
        // the slain's line — the card's focus may be the killer, so "their" would misattribute.
        if (e.HomeRegionId is int cairnRid && _map.HasHomeMark(cairnRid, e.Id)
            && _world.RegionName(cairnRid) is string cairnName)
            sb.AppendLine($"[color=#8a5d12]∆ a {StoryCopy.Hint("memorial cairn", "memorial cairn")} is raised in {cairnName}, "
                + $"the home of {(e.Type == "murder" ? "the slain's line" : "their line")}[/color]");

        if (focusPid is int pid && _world.People.TryGetValue(pid, out var p))
        {
            if (isDeath)
            {
                int died = p.DeathYear ?? e.Year;
                var fac = _world.Factions[p.FactionId];
                sb.AppendLine();
                if (memorial)
                {
                    // The memorial centerpiece: name in ceremony, every line real sim state.
                    sb.AppendLine($"[center][font_size=24][b]{p.Name}[/b][/font_size][/center]");
                    sb.AppendLine($"[center][color=#{Ui.Hex(Ui.FadedSub)}]of {fac.Name}"
                        + (p.EverLeader ? ", once their leader" : "")
                        + $" · born Yr {p.BirthYear} — died Yr {died} · {died - p.BirthYear} years[/color][/center]");
                    if (ReputationDisplay(p.Reputation) is (string mt, string mc))
                        sb.AppendLine($"[center][color=#{mc}]{mt}[/color][/center]");
                    if (p.Children.Count > 0)
                        sb.AppendLine($"[center][color=#{Ui.Hex(Ui.FadedSub)}]{p.Children.Count} {(p.Children.Count == 1 ? "child carries" : "children carry")} the line[/color][/center]");
                    // A murdered soul leaves an open grievance — stored sim state
                    // (Murdered && !Avenged), claimed no wider than the state itself:
                    // "unavenged", never "unpunished" (justice may be recorded apart),
                    // never "they will be avenged" (the chronicle does not know that).
                    if (p.Murdered && !p.Avenged)
                        sb.AppendLine($"[center][color=#{Ui.Hex(Ui.FadedSub)}]this murder lies unavenged[/color][/center]");
                    // The player's inscription — their hand, never the chronicle's voice.
                    if (_canon.Get($"p:{p.Id}", CanonNoteType.Inscription) is CanonNote insc
                        && _canon.StateOf(insc, _world) == CanonNoteState.Active)
                    {
                        sb.AppendLine($"[center][i]“{EscapeBb(insc.Text)}”[/i][/center]");
                        sb.AppendLine($"[center][color=#{Ui.Hex(Ui.FadedSub)}]— your hand[/color]  {Link($"canon:inscription:p:{p.Id}", "✎")}[/center]");
                    }
                    else if (!_canon.ReadOnly)
                        sb.AppendLine($"[center]{Link($"canon:inscription:p:{p.Id}", "✎ set a memorial inscription")}[/center]");
                }
                else
                {
                    sb.AppendLine(SectionCap("The record"));
                    sb.AppendLine($"{p.Name} — born Yr {p.BirthYear}, died Yr {died}, age {died - p.BirthYear}");
                    if (ReputationDisplay(p.Reputation) is (string repText, string repColor))
                        sb.AppendLine($"[color=#{repColor}]{repText}[/color]");
                    sb.AppendLine($"children: {p.Children.Count}");
                }
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
            if (prevSeen >= 0 && prevSeen != e.Id)
            {
                var ls = _world.Chronicle.Get(prevSeen);
                sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]you last saw {p.Name}: Yr {ls.Year} —[/color] {Link("e:" + ls.Id, ls.Text)}");
            }
            else
                sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]nothing of {p.Name} has crossed the saga since you began to follow[/color]");
        }

        return sb.ToString();
    }

    // Restyle the one guard panel per moment: the memorial earns the ceremonial frame —
    // a deeper gold border, a wider centered card, the event medallion, a gold rule —
    // while recap and bloodline-death cards keep the familiar weight.
    private void StyleGuardCard(bool memorial, Ui.EventClass cls)
    {
        var box = Ui.PanelBox(memorial ? 12 : 10);
        box.BorderColor = Ui.Gold;
        box.SetBorderWidthAll(memorial ? 3 : 2);
        if (memorial) { box.BgColor = Ui.RowBgWarm; box.ShadowSize = 16; }
        _guardPanel.AddThemeStyleboxOverride("panel", box);
        _guardPanel.OffsetLeft = memorial ? -320 : -280;
        _guardPanel.OffsetRight = memorial ? 320 : 280;
        _guardPanel.OffsetTop = memorial ? -250 : -200;
        _guardPanel.OffsetBottom = memorial ? 250 : 200;
        _guardTitle.AddThemeFontSizeOverride("font_size", memorial ? 25 : 21);
        _guardRule.AddThemeStyleboxOverride("separator",
            new StyleBoxFlat { BgColor = memorial ? Ui.Gold : Ui.RowBorder, ContentMarginTop = memorial ? 2 : 1 });
        _guardChip.Visible = memorial;
        if (memorial)
        {
            _guardChip.AddThemeStyleboxOverride("panel", Ui.ChipBox(cls.Color));
            _guardChipGlyph.Text = cls.Glyph;
        }
    }

    private void BuildGuardCard(Control root)
    {
        // The memorial dim: a soft ink veil over the atlas while a followed soul's card is
        // open. Presentation only; it also swallows clicks so the moment isn't walked past.
        _guardBackdrop = new ColorRect
        {
            Visible = false,
            Color = Ui.InkDeep with { A = 0.42f },
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        root.AddChild(_guardBackdrop);
        _guardBackdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);

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
        _guardChip = new PanelContainer
        {
            CustomMinimumSize = new Vector2(30, 30),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            Visible = false,
        };
        _guardChipGlyph = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _guardChipGlyph.AddThemeFontSizeOverride("font_size", 14);
        _guardChipGlyph.AddThemeColorOverride("font_color", Ui.ParchmentHi);
        _guardChip.AddChild(_guardChipGlyph);
        hb.AddChild(_guardChip);
        _guardTitle = new Label
        {
            Text = "Focus Guard",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TooltipText = StoryCopy.Glossary["guard"],
        };
        _guardTitle.AddThemeFontOverride("font", Ui.SerifBold);
        _guardTitle.AddThemeFontSizeOverride("font_size", 21);
        _guardTitle.AddThemeColorOverride("font_color", Ui.InkDeep);
        hb.AddChild(_guardTitle);
        var close = new Button { Text = "✕", CustomMinimumSize = new Vector2(28, 28), TooltipText = "Close (stay paused)" };
        Ui.StyleButton(close);
        close.Pressed += CloseGuardCard;
        hb.AddChild(close);

        _guardRule = new HSeparator();
        _guardRule.AddThemeStyleboxOverride("separator", new StyleBoxFlat { BgColor = Ui.RowBorder, ContentMarginTop = 1 });
        vb.AddChild(_guardRule);

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
        _guardBody.MetaClicked += meta => { CloseGuardCard(); OnInspectorLink(meta.AsString(), fromGuardCard: true); };
        scroll.AddChild(_guardBody);

        var btns = new HBoxContainer();
        btns.AddThemeConstantOverride("separation", 8);
        vb.AddChild(btns);
        var resume = new Button { Text = "▶ Resume" };
        Ui.StyleButton(resume, active: true);
        resume.Pressed += () => { CloseGuardCard(); _running = true; };
        btns.AddChild(resume);
        var trace = new Button { Text = "How We Got Here" };
        Ui.StyleButton(trace);
        trace.Pressed += () => { CloseGuardCard(); if (_guardEventId >= 0) OpenCatchup(_guardEventId); };
        btns.AddChild(trace);

        // The way back: chasing a link off the card (a deed, a child) shouldn't lose the
        // moment. While the world still holds its breath this chip reopens the held card;
        // once time moves again the moment has passed and the chip goes with it.
        _guardReturnBtn = new Button { Text = "↩ Return to the tale", Visible = false };
        Ui.StyleButton(_guardReturnBtn, active: true);
        root.AddChild(_guardReturnBtn);
        _guardReturnBtn.AnchorLeft = 0.5f; _guardReturnBtn.AnchorRight = 0.5f;
        _guardReturnBtn.AnchorTop = 0; _guardReturnBtn.AnchorBottom = 0;
        _guardReturnBtn.OffsetLeft = -115; _guardReturnBtn.OffsetRight = 115;
        _guardReturnBtn.OffsetTop = 10; _guardReturnBtn.OffsetBottom = 42;
        _guardReturnBtn.Pressed += () =>
        {
            _catchupPanel.Visible = false;
            if (_returnIsRecap) { _recapPanel.Visible = true; return; }
            _guardBackdrop.Visible = _guardWasMemorial;
            _guardPanel.Visible = true;
        };
    }

    private void CloseGuardCard()
    {
        _guardPanel.Visible = false;
        _guardBackdrop.Visible = false;
    }

    // ------------------------------------------------------------- chapter recaps

    private void StartChapter(int fromEventId)
    {
        _chapterStartYear = _world.Year;
        _chapterStartEventId = fromEventId;
        _chapterShownYears = 0;
        _chapterCloseReason = null;
        _chapterEchoes.Clear();
        _chapterRepBase.Clear();
        foreach (var pid in _followedSouls)
            if (_world.People.TryGetValue(pid, out var p)) _chapterRepBase[pid] = p.Reputation;
        _chapterRegionBase.Clear();
        foreach (var fid in _markedFactions) _chapterRegionBase[fid] = RegionCount(fid);
    }

    private int RegionCount(string fid)
    {
        int n = 0;
        foreach (var r in _world.Regions) if (r.ControllingFactionId == fid) n++;
        return n;
    }

    private void CloseChapter(string reason)
    {
        _queuedRecap = new RecapSnapshot
        {
            StartYear = _chapterStartYear,
            EndYear = _world.Year,
            StartEventId = _chapterStartEventId,
            EndEventId = _world.Chronicle.Events.Count,
            Reason = reason,
            Echoes = new(_chapterEchoes),
            RepBase = new(_chapterRepBase),
            RegionBase = new(_chapterRegionBase),
        };
        _recapChip.Text = $"❖ Years {_chapterStartYear}–{_world.Year} — a chapter closed";
        StartChapter(_world.Chronicle.Events.Count);
    }

    // Render and show the queued recap; pauses the world. All content comes from the chapter's
    // own chronicle slice plus the snapshots taken when it began — the only history-wide work is
    // BuildReverse for matured importance, a one-shot on card open (same cost class as the echo
    // scan), never per-tick.
    private void ShowRecapCard()
    {
        if (_queuedRecap is not RecapSnapshot snap) return;
        _queuedRecap = null;
        _running = false;
        _glimpsePanel.Visible = false;
        _guardReturnable = true;
        _returnIsRecap = true;
        _guardReturnBtn.Text = "↩ Return to the chapter";

        _recapSub.Text = $"Years {snap.StartYear}–{snap.EndYear} · {snap.Reason}";
        var events = _world.Chronicle.Events;
        var reverse = Scoring.BuildReverse(_world);

        var scored = new List<(int Score, Event E)>();
        int births = 0;
        var lost = new List<Event>();
        // Followed lands, the chapter's two honest channels counted apart: tales anchored
        // here (RegionId) vs lives remembered here (HomeRegionId) — never merged.
        var landTales = new System.Collections.Generic.Dictionary<int, int>();
        var landLives = new System.Collections.Generic.Dictionary<int, int>();
        for (int i = snap.StartEventId; i < snap.EndEventId; i++)
        {
            var e = events[i];
            scored.Add((Scoring.Importance(e, _world, reverse), e));
            if (e.Type == "birth" && IsYours(e)) births++;
            if ((e.Type is "death" or "murder")
                && e.Participants.Any(pid => _marked.Contains(pid) || _followedSouls.Contains(pid)))
                lost.Add(e);
            if (_followedRegions.Count > 0)
            {
                if (e.RegionId is int trid && _followedRegions.Contains(trid))
                    landTales[trid] = landTales.GetValueOrDefault(trid) + 1;
                if (e.HomeRegionId is int lrid && _followedRegions.Contains(lrid))
                    landLives[lrid] = landLives.GetValueOrDefault(lrid) + 1;
            }
        }
        scored.Sort((a, b) => b.Score.CompareTo(a.Score));

        var sb = new StringBuilder();
        sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]the saga closes a chapter — {snap.Reason}[/color]");
        sb.AppendLine();
        sb.AppendLine(SectionCap("Loudest of the age"));
        if (scored.Count == 0)
            sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}](a quiet age — the chronicle recorded nothing)[/color]");
        foreach (var (_, e) in scored.Take(3))
        {
            var cls = Ui.ClassOf(e.Type);
            sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]Yr {e.Year}[/color] [color=#{Ui.Hex(cls.Color)}]{cls.Glyph}[/color] {Link("e:" + e.Id, e.Text)}");
        }

        if (_followedSouls.Count > 0 || _seedPeople.Count > 0 || _markedFactions.Count > 0
            || _followedRegions.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(SectionCap("Your threads"));
            bool any = false;
            if (births > 0)
            {
                sb.AppendLine($"{births} born into what you follow");
                any = true;
            }
            var lostLines = new List<string>();
            foreach (var e in lost)
            {
                var who = e.Participants.Where(pid => _marked.Contains(pid) || _followedSouls.Contains(pid))
                    .Select(pid => _world.People.TryGetValue(pid, out var dp) ? dp.Name : null)
                    .FirstOrDefault(n => n is not null);
                if (who is not null)
                    lostLines.Add($"lost: {who} — [color=#{Ui.Hex(Ui.Faded)}]Yr {e.Year}[/color] {Link("e:" + e.Id, e.Text)}");
            }
            foreach (var line in lostLines.Take(6)) { sb.AppendLine(line); any = true; }
            if (lostLines.Count > 6) sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]…and {lostLines.Count - 6} more losses[/color]");
            foreach (var (pid, baseRep) in OrderedRepBase(snap))
            {
                if (!_followedSouls.Contains(pid) || !_world.People.TryGetValue(pid, out var p)) continue;
                string before = RepBandWord(baseRep);
                string after = RepBandWord(p.Reputation);
                if (before == after) continue;
                // A name moves as memory, not as debug output: darkens/brightens by the
                // real reputation delta, band words glossed on hover.
                string turn = p.Reputation < baseRep ? "darkens" : "brightens";
                sb.AppendLine($"{p.Name}'s name {turn}: {StoryCopy.Hint(before, before)} → {StoryCopy.Hint(after, after)}");
                any = true;
            }
            foreach (var fid in snap.RegionBase.Keys.OrderBy(k => k))
            {
                if (!_markedFactions.Contains(fid)) continue;
                int now = RegionCount(fid);
                int was = snap.RegionBase[fid];
                if (now == was) continue;
                sb.AppendLine($"{_world.Factions[fid].Name} — {was} → {now} regions {(now > was ? "(gained)" : "(lost)")}");
                any = true;
            }
            foreach (var rid in _followedRegions.OrderBy(r => r))
            {
                int tales = landTales.GetValueOrDefault(rid), lives = landLives.GetValueOrDefault(rid);
                if (tales == 0 && lives == 0) continue;
                var channels = new List<string>();
                if (tales > 0) channels.Add($"{tales} tale{(tales == 1 ? "" : "s")} anchored here");
                if (lives > 0) channels.Add($"{lives} {(lives == 1 ? "life" : "lives")} remembered here");
                sb.AppendLine($"{Link("r:" + rid, _world.Regions[rid].Name)} — {string.Join(" · ", channels)}");
                any = true;
            }
            // Still unresolved — proven open threads, never mood: unavenged murders among
            // the souls and kin you follow (Murdered && !Avenged is stored sim state), and
            // wars no peace event has ever answered. Honest copy only — the chronicle does
            // not know what filled the gap, so nothing here says "plotted" or "brooded".
            var watched = new HashSet<int>(_marked);
            watched.UnionWith(_followedSouls);
            var grievances = StoryGrammar.OpenGrievances(_world, watched);
            var openWars = StoryGrammar.OpenWars(_world);
            if (grievances.Count > 0 || openWars.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine(SectionCap("Still unresolved"));
                foreach (var g in grievances.Take(3))
                {
                    // "Unavenged" is exactly what Murdered && !Avenged proves — justice may
                    // have been recorded separately (executions never flip Avenged), so the
                    // copy must never widen to "unpunished".
                    string vName = _world.People[g.VictimId].Name;
                    sb.AppendLine(g.KillerAlive
                        ? $"the murder of {vName} [color=#{Ui.Hex(Ui.Faded)}](Yr {g.MurderYear})[/color] is unavenged — {_world.Year - g.MurderYear} years and counting  {Link("e:" + g.MurderEventId, "the deed")}"
                        : $"the murder of {vName} [color=#{Ui.Hex(Ui.Faded)}](Yr {g.MurderYear})[/color] was never avenged — the killer is gone  {Link("e:" + g.MurderEventId, "the deed")}");
                    any = true;
                }
                if (grievances.Count > 3)
                    sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]…and {grievances.Count - 3} more grievances unanswered[/color]");
                foreach (var ow in openWars.Take(2))
                {
                    var we = _world.Chronicle.Get(ow.WarEventId);
                    sb.AppendLine($"{Link("e:" + ow.WarEventId, we.Text)} [color=#{Ui.Hex(Ui.Faded)}]— declared Yr {ow.DeclaredYear}, no peace has been made[/color]");
                    any = true;
                }
            }
            if (!any) sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]your threads passed the age quietly[/color]");
        }

        if (snap.Echoes.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(SectionCap("Myths that echoed"));
            foreach (var (arch, label, anchor) in snap.Echoes)
                sb.AppendLine($"[color=#8a5d12]◆ {arch}[/color] — "
                    + (anchor >= 0 ? Link("e:" + anchor, label) : label));
        }

        _recapBody.Text = sb.ToString();
        _recapPanel.Visible = true;
    }

    // Snapshot dict in stable (id) order for rendering — presentation only, no RNG involved.
    private static IEnumerable<(int, int)> OrderedRepBase(RecapSnapshot snap)
        => snap.RepBase.OrderBy(kv => kv.Key).Select(kv => (kv.Key, kv.Value));

    private void BuildRecap(Control root)
    {
        _recapPanel = new Panel { Visible = false };
        root.AddChild(_recapPanel);
        _recapPanel.AnchorLeft = 0.5f; _recapPanel.AnchorRight = 0.5f;
        _recapPanel.AnchorTop = 0.5f; _recapPanel.AnchorBottom = 0.5f;
        _recapPanel.OffsetLeft = -300; _recapPanel.OffsetRight = 300;
        _recapPanel.OffsetTop = -230; _recapPanel.OffsetBottom = 230;
        _recapPanel.AddThemeStyleboxOverride("panel", Ui.PanelBox());

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        foreach (var s in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{s}", 14);
        _recapPanel.AddChild(margin);

        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 8);
        margin.AddChild(vb);

        var hb = new HBoxContainer();
        hb.AddThemeConstantOverride("separation", 8);
        vb.AddChild(hb);
        var titles = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        titles.AddThemeConstantOverride("separation", 0);
        hb.AddChild(titles);
        var title = new Label { Text = "A Chapter Closes" };
        title.AddThemeFontOverride("font", Ui.SerifBold);
        title.AddThemeFontSizeOverride("font_size", 21);
        title.AddThemeColorOverride("font_color", Ui.InkDeep);
        titles.AddChild(title);
        _recapSub = new Label { Text = "" };
        _recapSub.AddThemeFontSizeOverride("font_size", 12);
        _recapSub.AddThemeColorOverride("font_color", Ui.FadedSub);
        titles.AddChild(_recapSub);
        var close = new Button { Text = "✕", CustomMinimumSize = new Vector2(28, 28), TooltipText = "Close (stay paused)" };
        Ui.StyleButton(close);
        close.Pressed += () => _recapPanel.Visible = false;
        hb.AddChild(close);

        var rule = new HSeparator();
        rule.AddThemeStyleboxOverride("separator", new StyleBoxFlat { BgColor = Ui.RowBorder, ContentMarginTop = 1 });
        vb.AddChild(rule);

        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        vb.AddChild(scroll);
        _recapBody = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(548, 0),
        };
        _recapBody.AddThemeColorOverride("default_color", Ui.Ink);
        _recapBody.AddThemeFontOverride("bold_font", Ui.SerifBold);
        _recapBody.MetaClicked += meta => { _recapPanel.Visible = false; OnInspectorLink(meta.AsString()); };
        scroll.AddChild(_recapBody);

        var btns = new HBoxContainer();
        btns.AddThemeConstantOverride("separation", 8);
        vb.AddChild(btns);
        var resume = new Button { Text = "▶ Resume" };
        Ui.StyleButton(resume, active: true);
        resume.Pressed += () => { _recapPanel.Visible = false; _running = true; };
        btns.AddChild(resume);

        // The chip: a closed chapter waits by the year card until read (or superseded).
        _recapChip = new Button { Visible = false };
        Ui.StyleButton(_recapChip);
        root.AddChild(_recapChip);
        _recapChip.AnchorLeft = 0; _recapChip.AnchorRight = 0;
        _recapChip.AnchorTop = 0; _recapChip.AnchorBottom = 0;
        _recapChip.OffsetLeft = 264; _recapChip.OffsetRight = 264 + 290;
        _recapChip.OffsetTop = 10; _recapChip.OffsetBottom = 42;
        _recapChip.Pressed += ShowRecapCard;
    }

    // -------------------------------------------------------------- inspectors

    private void OpenCatchup(int eventId)
    {
        _glimpsePanel.Visible = false;   // the glimpse z-orders above the catch-up card
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

        // The annotated chain: same membership as Trace, in record order (causes always
        // precede effects), with every proven connector attached. Card-open one-shot.
        var ann = StoryGrammar.Annotate(_world, id);
        var target = _world.Chronicle.Get(id);

        var sb = new StringBuilder();
        sb.AppendLine($"[b]{target.Text}[/b]");
        sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]{(_catchupQuick ? "the turning points" : "the full thread")} that led here[/color]");
        sb.AppendLine();

        var shown = _catchupQuick
            ? ann.Steps.Where(s => s.Event.Id == id || s.Event.Type is not ("birth" or "death" or "marriage")).ToList()
            : ann.Steps;
        if (shown.Count <= 1)
            sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}](this one stands alone — no deeper causes recorded)[/color]");
        var rendered = new HashSet<int>();
        int lastRendered = -1;
        foreach (var step in shown)
        {
            var e = step.Event;
            bool isTarget = e.Id == id;
            // Voice the proven connector only when its cause row is visible above — a
            // connector is never aimed at a row the current view has hidden. And when
            // interleaved branches put another tale between cause and effect, the year is
            // named, so the voicing can never visually re-aim at a neighbouring row.
            if (step.Link is ChainLink link && rendered.Contains(link.CauseEventId))
            {
                string phrase = StoryCopy.ConnectorPhrase(link);
                if (link.CauseEventId != lastRendered)
                    phrase += $" (Yr {_world.Chronicle.Get(link.CauseEventId).Year} above)";
                sb.AppendLine($"      [color=#{Ui.Hex(Ui.FadedSub)}][i]{phrase}[/i][/color]");
            }
            var cls = Ui.ClassOf(e.Type);
            string year = $"[color=#{Ui.Hex(Ui.Faded)}]Yr {e.Year}[/color]";
            // Hint() is a no-op for labels without a glossary entry, so every chip can ask.
            string chip = $"[color=#{Ui.Hex(cls.Color)}]{cls.Glyph} {StoryCopy.Hint(cls.Label.ToUpperInvariant(), cls.Label.ToLowerInvariant())}[/color]";
            string body = isTarget ? $"[b]{e.Text}[/b]" : e.Text;
            string where = e.RegionId is int rid && _world.RegionName(rid) is string rn
                ? $"  [color=#{Ui.Hex(Ui.Faded)}]· in {rn}[/color]" : "";
            string line = $"{year}  {chip}  {body}{where}";
            sb.AppendLine(isTarget ? $"[bgcolor=#{Ui.Hex(Ui.RowBgWarm)}]{line}[/bgcolor]" : line);
            rendered.Add(e.Id);
            lastRendered = e.Id;
            // A war fed by many grievances says so — Causes.Count is recorded fact.
            if (e.Type == "war" && e.Causes.Count > 1)
                sb.AppendLine($"      [color=#{Ui.Hex(Ui.FadedSub)}][i]fed by {e.Causes.Count} recorded grievances[/i][/color]");
            // An honest unknown at a chain root is said plainly, under its row.
            if (step.Origin is OriginInfo origin && StoryCopy.OriginLine(origin, _world) is string oline)
                sb.AppendLine($"      [color=#{Ui.Hex(Ui.Faded)}][i]{oline}[/i][/color]{CanonGapAffordance(origin, e)}");
            // The player's hand, kept apart from the record, beneath the row it glosses.
            if (CanonNoteLine($"e:{e.Id}", CanonNoteType.ChroniclerNote) is string cline)
                sb.AppendLine(cline);
            else if (isTarget && !_canon.ReadOnly && step.Origin?.Kind != OriginKind.HonestUnknown)
                sb.AppendLine($"      {Link($"canon:note:e:{e.Id}", "✎ add a chronicler's note")}");
        }
        _catchup.Text = sb.ToString();
    }

    // The door into the player's telling for an honest gap: "the chronicle does not
    // record what stirred her" earns a quiet "✎ write what stirred her". Once a note
    // exists it renders on its own line instead, so the door never doubles the telling.
    private string CanonGapAffordance(OriginInfo origin, Event e)
    {
        if (_canon.ReadOnly || _canon.Get($"e:{e.Id}", CanonNoteType.ChroniclerNote) is not null) return "";
        return StoryCopy.WriteAffordance(origin, _world) is string label
            ? $"  {Link($"canon:note:e:{e.Id}", label)}" : "";
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
            if (!_markedFactions.Remove(fid))
            {
                _markedFactions.Add(fid);
                _chapterRegionBase[fid] = RegionCount(fid);   // recap deltas measure from the follow
            }
            RecomputeMarked();
            OnFactionPicked(fid);
        }
    }

    // Follow one soul, not their line: no bloodline expansion, no viral growth at birth —
    // just this person's id, watched until the player lets go.
    private void OnFollowSoulPressed()
    {
        if (_selectedPersonId is not int pid) return;
        if (!_followedSouls.Remove(pid))
        {
            _followedSouls.Add(pid);   // toggle on
            // Recap deltas measure from the follow (or chapter start, whichever is later).
            if (_world.People.TryGetValue(pid, out var p)) _chapterRepBase[pid] = p.Reputation;
        }
        _map.QueueRedraw();
        OnPersonPicked(pid);
    }

    // Follow a land, not a people: the place itself becomes the player's mark. Only the two
    // honest channels speak for it — tales truly anchored here and lives remembered here —
    // the chronicle is never asked to invent more.
    private void OnFollowRegionPressed()
    {
        int rid = _map.SelectedRegionId;
        if (rid < 0 || rid >= _world.Regions.Count) return;
        if (!_followedRegions.Remove(rid)) _followedRegions.Add(rid);
        _map.QueueRedraw();
        OnRegionPicked(rid);
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

    // Short band words for reputation transitions (same thresholds as ReputationDisplay;
    // the silent middle band gets a readable word instead of the old debug-ish "unremarked").
    private static string RepBandWord(int rep) => rep switch
    {
        >= 3 => "admired",
        >= 1 => "well spoken of",
        <= -3 => "infamous",
        <= -1 => "whispered against",
        _ => "little known",
    };

    private void OnPersonPicked(int id)
    {
        if (!_world.People.TryGetValue(id, out var p)) return;
        _selectedPersonId = id;
        _selectedFactionId = null;
        _map.SelectedFactionId = null;
        _map.SelectedRegionId = -1;
        _glimpsePanel.Visible = false;
        _lensFactionBtn.Visible = false;
        _regionBtn.Visible = false;
        _curseBtn.Visible = p.Alive && !p.Cursed;
        bool soulFollowed = _followedSouls.Contains(id);
        _soulBtn.Visible = p.Alive || soulFollowed;   // a dead soul can still be let go
        _soulBtn.Text = soulFollowed ? "★ Following this soul — unfollow" : "☆ Follow this soul";
        Ui.StyleButton(_soulBtn, soulFollowed);
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
        if (soulFollowed) sb.AppendLine($"[color=#8a5d12][b]★ you are watching this soul[/b][/color]\n");
        if (ReputationDisplay(p.Reputation) is (string repText, string repColor))
        {
            sb.AppendLine(SectionCap("Reputation"));
            sb.AppendLine($"[color=#{repColor}][b]{repText}[/b][/color]\n");
        }
        sb.AppendLine(SectionCap("The record"));
        sb.AppendLine($"status: {status}");
        sb.AppendLine(p.HomeRegionId is int hr
            ? $"home: {Link("r:" + hr, _world.Regions[hr].Name)}"
            : "home: —");
        sb.AppendLine($"faith: {faith}");
        sb.AppendLine($"spouse: {spouse}");
        sb.AppendLine($"children: {p.Children.Count}");
        if ((_marked.Contains(id) || _followedSouls.Contains(id)) && _lastSeenEvent.TryGetValue(id, out var lsId))
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

        // The player's hand: a telling for this soul; once the tale has ended, the
        // inscription too. A telling outlives its subject — dead souls keep their lore.
        sb.AppendLine();
        if (CanonBlock($"p:{id}", CanonNoteType.Telling) is string telling) sb.Append(telling);
        else if (!_canon.ReadOnly) sb.AppendLine(Link($"canon:telling:p:{id}", $"✎ write a telling of {p.Name}"));
        if (!p.Alive)
        {
            if (CanonBlock($"p:{id}", CanonNoteType.Inscription) is string insc) sb.Append(insc);
            else if (!_canon.ReadOnly) sb.AppendLine(Link($"canon:inscription:p:{id}", "✎ set a memorial inscription"));
        }

        _inspector.Text = sb.ToString();
        _inspectorPanel.Visible = true;
    }

    // Inspector cross-links: e:<event id> opens How We Got Here, r:<region id> the Region
    // Lens, f:<faction id> the faction inspector. The link targets are real ids the panels
    // already render from — no new lookups.
    private void OnInspectorLink(string link, bool fromGuardCard = false)
    {
        if (link.StartsWith("e:") && int.TryParse(link[2..], out var eid)) OpenCatchup(eid);
        else if (link.StartsWith("r:") && int.TryParse(link[2..], out var rid)) OnRegionPicked(rid);
        else if (link.StartsWith("f:")) OnFactionPicked(link[2..]);
        else if (link.StartsWith("canon:")) OpenCanonEditor(link[6..], fromGuardCard);
    }

    private static string Link(string target, string text)
        => $"[color=#8a5d12][url={target}]{text}[/url][/color]";

    // ------------------------------------------------------------- player canon

    // Link target shape: canon:{typeKey}:{entityKey} — e.g. canon:telling:p:12,
    // canon:note:e:5012, canon:legend:r:3, canon:say:f:highland.
    private static string TypeKeyOf(CanonNoteType t) => t switch
    {
        CanonNoteType.Telling => "telling",
        CanonNoteType.ChroniclerNote => "note",
        CanonNoteType.Inscription => "inscription",
        CanonNoteType.PlaceLegend => "legend",
        _ => "say",
    };

    // RichTextLabel renders BBCode — the player's own brackets must stay ink, not markup.
    private static string EscapeBb(string s) => s.Replace("[", "[lb]");

    private void OpenCanonEditor(string spec, bool fromGuardCard = false)
    {
        if (_canon.ReadOnly) return;
        _canonReturnsToGuard = fromGuardCard;
        int sep = spec.IndexOf(':');
        if (sep <= 0) return;
        CanonNoteType? type = spec[..sep] switch
        {
            "telling" => CanonNoteType.Telling,
            "note" => CanonNoteType.ChroniclerNote,
            "inscription" => CanonNoteType.Inscription,
            "legend" => CanonNoteType.PlaceLegend,
            "say" => CanonNoteType.PeopleSay,
            _ => null,
        };
        string entityKey = spec[(sep + 1)..];
        if (type is null) return;

        string title = StoryCopy.CanonLabel(type.Value), context = "";
        if (entityKey.StartsWith("p:") && int.TryParse(entityKey[2..], out int pid)
            && _world.People.TryGetValue(pid, out var p))
        {
            title = type == CanonNoteType.Inscription ? $"An inscription for {p.Name}" : $"A telling of {p.Name}";
            context = $"of {_world.Factions[p.FactionId].Name} · born Yr {p.BirthYear}"
                + (p.Alive ? "" : $" — died Yr {p.DeathYear}");
        }
        else if (entityKey.StartsWith("e:") && int.TryParse(entityKey[2..], out int ceid)
            && ceid >= 0 && ceid < _world.Chronicle.Events.Count)
        {
            var ce = _world.Chronicle.Get(ceid);
            title = "A chronicler's note";
            context = $"Yr {ce.Year} — {ce.Text}";
        }
        else if (entityKey.StartsWith("r:") && int.TryParse(entityKey[2..], out int crid)
            && crid >= 0 && crid < _world.Regions.Count)
        {
            var cr = _world.Regions[crid];
            title = $"The legend of {cr.Name}";
            context = $"{cr.TerrainType} — what the place is said to be";
        }
        else if (entityKey.StartsWith("f:") && _world.Factions.TryGetValue(entityKey[2..], out var cf))
        {
            title = $"What the people say of {cf.Name}";
            context = $"{cf.Culture} culture · of {cf.Homeland}";
        }
        else return;   // the entity isn't in this world right now — no editor over nothing

        _canonPanel.Open(entityKey, type.Value, title, context);
    }

    // One full canon block for an entity's own card: label, the telling, the hand.
    // Null when no active note — empty notes render nothing, dormant/quarantined stay dark.
    private string? CanonBlock(string entityKey, CanonNoteType type)
    {
        var note = _canon.Get(entityKey, type);
        if (note is null || _canon.StateOf(note, _world) != CanonNoteState.Active) return null;
        var sb = new StringBuilder();
        sb.AppendLine(SectionCap(StoryCopy.CanonLabel(type)));
        sb.AppendLine($"[i]{EscapeBb(note.Text)}[/i]");
        sb.AppendLine($"[color=#{Ui.Hex(Ui.FadedSub)}]— your hand, Yr {note.CreatedYear}[/color]  {Link($"canon:{TypeKeyOf(type)}:{entityKey}", "✎ edit")}");
        return sb.ToString();
    }

    // One collapsed canon line for busy surfaces (~90-char preview).
    private string? CanonNoteLine(string entityKey, CanonNoteType type)
    {
        var note = _canon.Get(entityKey, type);
        if (note is null || _canon.StateOf(note, _world) != CanonNoteState.Active) return null;
        string preview = note.Text.Length > 90 ? note.Text[..90].TrimEnd() + "…" : note.Text;
        return $"      [color=#{Ui.Hex(Ui.FadedSub)}][i]{StoryCopy.CanonLabel(type)}: “{EscapeBb(preview)}” — your hand[/i][/color]  {Link($"canon:{TypeKeyOf(type)}:{entityKey}", "✎")}";
    }

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
        _glimpsePanel.Visible = false;
        _curseBtn.Visible = false;
        _followBtn.Visible = false;
        _soulBtn.Visible = false;
        _lensFactionId = holder?.Id;
        _lensFactionBtn.Visible = holder is not null;
        if (holder is not null) _lensFactionBtn.Text = $"⚑ Inspect {holder.Name}";
        bool landFollowed = _followedRegions.Contains(regionId);
        _regionBtn.Visible = true;
        _regionBtn.Text = landFollowed ? "★ Following this land — unfollow" : "☆ Follow this land";
        Ui.StyleButton(_regionBtn, landFollowed);

        _inspectorTitle.Text = region.Name;
        _inspectorSub.Text = $"{region.TerrainType} · {(holder is null ? "wilderness" : $"held by {holder.Name}")}";

        var sb = new StringBuilder();
        if (landFollowed) sb.AppendLine($"[color=#8a5d12][b]★ you are watching this land[/b][/color]\n");
        sb.AppendLine(SectionCap("The land"));
        sb.AppendLine($"terrain: {region.TerrainType}");
        sb.AppendLine(holder is null
            ? "held by: no one — unclaimed wilderness"
            : $"held by: {Link("f:" + holder.Id, holder.Name)}");
        sb.AppendLine($"map hint: {PlaceSeeds.Label(PlaceSeeds.KindOf(_world, region))} — a viewer's mark, not sim state");
        sb.AppendLine();
        // The player's hand: what this place is said to be.
        if (CanonBlock($"r:{regionId}", CanonNoteType.PlaceLegend) is string legend)
        { sb.Append(legend); sb.AppendLine(); }
        else if (!_canon.ReadOnly)
        { sb.AppendLine(Link($"canon:legend:r:{regionId}", "✎ set a place legend")); sb.AppendLine(); }
        sb.AppendLine(SectionCap("Neighbouring lands"));
        foreach (var nid in region.AdjacentRegionIds)
        {
            var n = _world.Regions[nid];
            string nh = n.ControllingFactionId is string nf ? _world.Factions[nf].Name : "wild";
            sb.AppendLine($"{Link("r:" + n.Id, n.Name)} — {n.TerrainType} · {nh}");
        }
        sb.AppendLine();
        sb.AppendLine(SectionCap("Marks upon the land"));
        var marks = _map.MarksFor(regionId);
        if (marks.Count == 0)
            sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]unmarked — no recorded event has scarred this place[/color]");
        for (int i = marks.Count - 1; i >= 0; i--)   // newest first
        {
            var m = marks[i];
            var me = _world.Chronicle.Get(m.eventId);
            sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]Yr {m.year}[/color] {MarkLabel(m.kind)} — {Link("e:" + m.eventId, me.Text)}");
        }
        sb.AppendLine();
        int homeTotal = _regionActivity.HomeTotalFor(regionId);
        sb.AppendLine(SectionCap("Lives rooted here")
            + (homeTotal > 0 ? $" [color=#{Ui.Hex(Ui.Faded)}]({homeTotal} remembered)[/color]" : ""));
        sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]home memory of lines rooted in this place — not where these moments happened[/color]");
        var cairns = _map.HomeMarksFor(regionId);
        for (int i = cairns.Count - 1; i >= 0; i--)   // newest first
        {
            var ce = _world.Chronicle.Get(cairns[i].eventId);
            sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]Yr {cairns[i].year}[/color] [color=#8a5d12]∆ memorial cairn[/color] — {Link("e:" + ce.Id, ce.Text)}");
        }
        var homeRecent = _regionActivity.HomeRecentFor(regionId);
        if (homeRecent.Count == 0)
            sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]no lives are yet remembered here[/color]");
        for (int i = homeRecent.Count - 1; i >= 0; i--)   // newest first
        {
            if (_map.HasHomeMark(regionId, homeRecent[i])) continue;   // already shown as its cairn row
            var he = _world.Chronicle.Get(homeRecent[i]);
            var hcls = Ui.ClassOf(he.Type);
            sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]Yr {he.Year}[/color] [color=#{Ui.Hex(hcls.Color)}]{hcls.Glyph}[/color] {Link("e:" + he.Id, he.Text)}");
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
        sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]much of history carries no place anchor yet — a followed land speaks only when tales are anchored here or lives are remembered here[/color]");

        _inspector.Text = sb.ToString();
        _inspectorPanel.Visible = true;
    }

    private static string MarkLabel(MapView.MarkKind kind) => kind switch
    {
        MapView.MarkKind.FoundingStone => "[color=#90908a]⌑ standing stone[/color]",
        MapView.MarkKind.WarScar => $"[color=#{Ui.Hex(Ui.Ember)}]✕ war scar[/color]",
        MapView.MarkKind.AbandonCairn => "[color=#90908a]∴ cairn[/color]",
        _ => $"[color=#{Ui.Hex(Ui.Violet)}]❧ custom ribbon[/color]",
    };

    private void OnFactionPicked(string fid)
    {
        _selectedPersonId = null;
        _selectedFactionId = fid;
        _map.SelectedFactionId = fid;
        _map.SelectedRegionId = -1;
        _glimpsePanel.Visible = false;
        _lensFactionBtn.Visible = false;
        _regionBtn.Visible = false;
        _curseBtn.Visible = false;
        _soulBtn.Visible = false;
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
        // The player's hand: what is said of this people.
        sb.AppendLine();
        if (CanonBlock($"f:{fid}", CanonNoteType.PeopleSay) is string say) sb.Append(say);
        else if (!_canon.ReadOnly) sb.AppendLine(Link($"canon:say:f:{fid}", "✎ write what the people say"));
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
        bool guardActive = _guardMode != GuardMode.Off
            && (_followedSouls.Count > 0 || _seedPeople.Count > 0 || _markedFactions.Count > 0
                || _followedRegions.Count > 0);
        _guardLabel.Visible = guardActive;
        if (guardActive)
        {
            var parts = new List<string>();
            if (_followedSouls.Count > 0) parts.Add($"{_followedSouls.Count} soul{(_followedSouls.Count == 1 ? "" : "s")}");
            if (_seedPeople.Count > 0) parts.Add($"{_seedPeople.Count} bloodline{(_seedPeople.Count == 1 ? "" : "s")}");
            if (_markedFactions.Count > 0) parts.Add($"{_markedFactions.Count} people{(_markedFactions.Count == 1 ? "" : "s")}");
            if (_followedRegions.Count > 0) parts.Add($"{_followedRegions.Count} land{(_followedRegions.Count == 1 ? "" : "s")}");
            _guardLabel.Text = "⛨ guard watches " + string.Join(" · ", parts);
            // The tooltip names up to two watched souls, so a hover recalls who you are
            // waiting on. Sorted by id for a stable order; souls are always few.
            if (_followedSouls.Count > 0)
            {
                var names = _followedSouls.OrderBy(i => i).Take(2)
                    .Select(i => _world.People.TryGetValue(i, out var sp) ? sp.Name : null)
                    .Where(n => n is not null);
                _guardLabel.TooltipText = "watched souls: " + string.Join(", ", names)
                    + (_followedSouls.Count > 2 ? $" — and {_followedSouls.Count - 2} more" : "");
            }
            else _guardLabel.TooltipText = "";
        }
        _playBtn.Text = _running ? "❚❚ Pause" : "▶ Play";
        _chatLabel.Text = $"chattiness ≥ {(int)_chatSlider.Value}";
        if (_running) _guardReturnable = false;   // time moved on; the held moment has passed
        bool cardUp = _guardPanel.Visible || _catchupPanel.Visible || _recapPanel.Visible
            || _pendingGuardEventId is not null || _canonPanel.IsOpen;
        _guardReturnBtn.Visible = _guardReturnable && !cardUp;
        // A queued recap shows on the transition INTO a pause (never over another card —
        // the focus guard always outranks it) and otherwise waits on its chip.
        if (_wasRunning && !_running && _queuedRecap is not null && !cardUp) ShowRecapCard();
        _wasRunning = _running;
        _recapChip.Visible = _queuedRecap is not null && !cardUp;
    }
}
