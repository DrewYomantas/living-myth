// M3 (Yours channel) DONE: Follow button on both inspectors marks a bloodline/people; YOURS rows
// are gold-tagged + weight-boosted in the feed and followed dots are ringed cyan in MapView. The
// marked-set check is inline + O(living), and the bloodline grows virally at birth (not via a
// per-tick Feed.BuildFeed). NEXT: visual/UX pass, then more pressure engines + echo packs.
// See PROJECT_STATE.md.
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
    private const float BaseInterval = 1.2f;   // real seconds per sim-year at 1×
    private static readonly float[] SpeedLadder = { 0.25f, 0.5f, 1f, 2f, 4f, 8f, 16f };
    private const int FeedWidth = 360;
    private const int BottomH = 100;

    private World _world = null!;
    private Control _root = null!;
    private MapView _map = null!;
    private VBoxContainer _feedList = null!;
    private RichTextLabel _inspector = null!;
    private Panel _inspectorPanel = null!;
    private Button _curseBtn = null!;
    private Button _followBtn = null!;
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
    private Label _yearLabel = null!;
    private Button _playBtn = null!;
    private Label _speedLabel = null!;
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

    private sealed class FeedVisRow { public Node Node = null!; public bool Yours; public int Weight; }

    public override void _Ready()
    {
        var (config, names) = DataLoader.Load();
        _world = new World(Seed, config, names);
        _world.SeedWorld();
        _lastEventCount = 0;
        _lastEchoYear = _world.Year;

        BuildUi();
        _map.World = _world;
        _map.Marked = _marked;       // same HashSet, mutated in place — map sees follows live
        StreamNewHeadlines();
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

        var root = _root;

        _map = new MapView { PersonPicked = OnPersonPicked, FactionPicked = OnFactionPicked, RegionPicked = OnRegionPicked };
        root.AddChild(_map);
        _map.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _map.OffsetRight = -FeedWidth;
        _map.OffsetBottom = -BottomH;

        BuildFeed(root);
        BuildBottomBar(root);
        BuildInspector(root);
        BuildCatchup(root);
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

        foreach (var s in SpeedLadder)
        {
            var b = new Button { Text = $"{s:0.##}×" };
            b.Pressed += () => SetSpeed(s);
            hb.AddChild(b);
        }
        _speedLabel = new Label();
        hb.AddChild(_speedLabel);

        var drama = new CheckButton { Text = "Drama", ButtonPressed = true };
        drama.Toggled += on => _dramaticPacing = on;
        hb.AddChild(drama);

        hb.AddChild(new VSeparator());

        var zoomOut = new Button { Text = "－" };
        zoomOut.Pressed += () => _map.ZoomBy(1f / 1.25f);
        hb.AddChild(zoomOut);
        var zoomIn = new Button { Text = "＋" };
        zoomIn.Pressed += () => _map.ZoomBy(1.25f);
        hb.AddChild(zoomIn);
        var camReset = new Button { Text = "⤢" };
        camReset.Pressed += () => _map.ResetCamera();
        hb.AddChild(camReset);
        var camFollow = new CheckButton { Text = "Cam", ButtonPressed = true };
        camFollow.Toggled += on => _map.CameraFollow = on;
        hb.AddChild(camFollow);

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

        // God hand: the curse tool. Only shown for a living, not-yet-cursed person.
        _curseBtn = new Button { Text = "⚡ Lay Curse on this bloodline", Visible = false };
        _curseBtn.Modulate = new Color("ff9aa8");
        _curseBtn.Pressed += OnCursePressed;
        vb.AddChild(_curseBtn);

        // The Yours channel: follow a bloodline / a people and their moments rise into the feed.
        _followBtn = new Button { Text = "☆ Follow", Visible = false };
        _followBtn.Modulate = new Color("ffe08a");
        _followBtn.Pressed += OnFollowPressed;
        vb.AddChild(_followBtn);

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

    private void BuildCatchup(Control root)
    {
        _catchupPanel = new Panel { Visible = false };
        root.AddChild(_catchupPanel);
        _catchupPanel.AnchorLeft = 0.5f; _catchupPanel.AnchorRight = 0.5f;
        _catchupPanel.AnchorTop = 0.5f; _catchupPanel.AnchorBottom = 0.5f;
        _catchupPanel.OffsetLeft = -290; _catchupPanel.OffsetRight = 290;
        _catchupPanel.OffsetTop = -240; _catchupPanel.OffsetBottom = 240;

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        foreach (var s in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{s}", 12);
        _catchupPanel.AddChild(margin);

        var vb = new VBoxContainer();
        margin.AddChild(vb);

        var hb = new HBoxContainer();
        vb.AddChild(hb);
        var title = new Label { Text = "HOW WE GOT HERE", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        title.AddThemeFontSizeOverride("font_size", 16);
        hb.AddChild(title);
        var quick = new Button { Text = "Quick beats" };
        quick.Pressed += () => { _catchupQuick = true; RenderCatchup(); };
        hb.AddChild(quick);
        var full = new Button { Text = "Full thread" };
        full.Pressed += () => { _catchupQuick = false; RenderCatchup(); };
        hb.AddChild(full);
        var close = new Button { Text = "✕" };
        close.Pressed += () => _catchupPanel.Visible = false;
        hb.AddChild(close);

        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        vb.AddChild(scroll);
        _catchup = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(540, 0),
        };
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

        // Maintain consequence counts incrementally so we never rebuild a reverse index over
        // the whole (ever-growing) chronicle. Update first, then score the new slice.
        for (int i = _lastEventCount; i < events.Count; i++)
            foreach (var c in events[i].Causes)
                _consCount[c] = _consCount.GetValueOrDefault(c) + 1;

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
            if (imp < threshold) continue;

            var row = AddFeedRow(e, imp, yours);
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

    private static void PulseFeedRow(RichTextLabel row)
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

    private RichTextLabel? AddFeedRow(Event e, int imp, bool yours)
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

        var lbl = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            MetaUnderlined = false,
            CustomMinimumSize = new Vector2(FeedWidth - 40, 0),
        };
        // Whole row is a link; clicking it opens the catch-up trace for this event.
        string tag = yours ? "[color=#ffd54a]★ YOURS[/color]  " : "";
        string yearCol = yours ? "#ffd54a" : "#7fd0a0";
        lbl.Text = $"[url={e.Id}]{tag}[color={yearCol}]Yr {e.Year}[/color]  {e.Text}  [color=#7e8a96](w{imp})[/color][/url]";
        lbl.MetaClicked += OnFeedMetaClicked;
        _feedList.AddChild(lbl);
        _feedList.MoveChild(lbl, 0);   // newest on top
        _feedVis.Insert(0, new FeedVisRow { Node = lbl, Yours = yours, Weight = imp });
        while (_feedVis.Count > FeedWindow)
        {
            var oldest = _feedVis[_feedVis.Count - 1];
            oldest.Node.QueueFree();
            _feedVis.RemoveAt(_feedVis.Count - 1);
        }
        return lbl;
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
        var lbl = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            MetaUnderlined = false,
            CustomMinimumSize = new Vector2(FeedWidth - 40, 0),
        };
        string body = $"[bgcolor=#3a2c0a]  [color=#ffcf3a]◆ MYTH ECHO[/color]  [color=#f3e3a8]{echo.Archetype}[/color]\n"
                    + $"  [color=#e8d79a]{echo.Label}[/color]  [/bgcolor]";
        lbl.Text = anchorEventId >= 0 ? $"[url={anchorEventId}]{body}[/url]" : body;
        lbl.MetaClicked += OnFeedMetaClicked;   // anchor is a real event id — reuses the catch-up trace
        _feedList.AddChild(lbl);
        _feedList.MoveChild(lbl, 0);
        _feedVis.Insert(0, new FeedVisRow { Node = lbl, Yours = false, Weight = int.MaxValue });
        while (_feedVis.Count > FeedWindow)
        {
            var oldest = _feedVis[_feedVis.Count - 1];
            oldest.Node.QueueFree();
            _feedVis.RemoveAt(_feedVis.Count - 1);
        }
    }

    // -------------------------------------------------------------- inspectors

    private void OnFeedMetaClicked(Variant meta)
    {
        if (!int.TryParse(meta.AsString(), out int id)) return;
        _catchupEventId = id;
        _catchupQuick = true;
        _catchupPanel.Visible = true;
        RenderCatchup();
    }

    private void RenderCatchup()
    {
        if (_catchupEventId is not int id) return;
        var chain = _world.Chronicle.Trace(id);   // event + all its causes, in year order
        var target = chain.FirstOrDefault(e => e.Id == id);

        var sb = new StringBuilder();
        sb.AppendLine($"[b]{(target is null ? "" : target.Text)}[/b]");
        sb.AppendLine($"[color=#7e8a96]{(_catchupQuick ? "turning points" : "the full thread")} that led here[/color]");
        sb.AppendLine();

        var shown = _catchupQuick
            ? chain.Where(e => e.Id == id || e.Type is not ("birth" or "death" or "marriage")).ToList()
            : chain;
        if (shown.Count <= 1)
            sb.AppendLine("[color=#7e8a96](this one stands alone — no deeper causes recorded)[/color]");
        foreach (var e in shown)
        {
            bool isTarget = e.Id == id;
            string year = $"[color=#7fd0a0]Yr {e.Year}[/color]";
            string body = isTarget ? $"[b]{e.Text}[/b]" : e.Text;
            string where = e.RegionId is int rid && _world.RegionName(rid) is string rn
                ? $"  [color=#7e8a96]· in {rn}[/color]" : "";
            sb.AppendLine($"{year}  [color=#9aa6b2][{e.Type}][/color]  {body}{where}");
        }
        _catchup.Text = sb.ToString();
    }

    private void OnCursePressed()
    {
        if (_selectedPersonId is not int id || !_world.People.TryGetValue(id, out var p)) return;
        if (!p.Alive || p.Cursed) return;
        _world.PlantCurse(p);
        StreamNewHeadlines();        // surface the divine act immediately
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

    private void OnPersonPicked(int id)
    {
        if (!_world.People.TryGetValue(id, out var p)) return;
        _selectedPersonId = id;
        _selectedFactionId = null;
        _curseBtn.Visible = p.Alive && !p.Cursed;
        _followBtn.Visible = true;
        _followBtn.Text = _seedPeople.Contains(id) ? "★ Following bloodline — unfollow" : "☆ Follow this bloodline";
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

    // Clicking a territory: hand off to the faction inspector if it's held, else show the
    // wilderness itself (terrain + that no one holds it).
    private void OnRegionPicked(int regionId)
    {
        if (regionId < 0 || regionId >= _world.Regions.Count) return;
        var region = _world.Regions[regionId];
        if (region.ControllingFactionId is string fid) { OnFactionPicked(fid); return; }

        _selectedPersonId = null;
        _selectedFactionId = null;
        _curseBtn.Visible = false;
        _followBtn.Visible = false;
        var sb = new StringBuilder();
        sb.AppendLine($"[b]{region.Name}[/b]");
        sb.AppendLine($"terrain: {region.TerrainType}");
        sb.AppendLine("[color=#7e8a96]unclaimed wilderness — no people hold this land[/color]");
        _inspector.Text = sb.ToString();
        _inspectorPanel.Visible = true;
    }

    private void OnFactionPicked(string fid)
    {
        _selectedPersonId = null;
        _selectedFactionId = fid;
        _curseBtn.Visible = false;
        _followBtn.Visible = true;
        _followBtn.Text = _markedFactions.Contains(fid) ? "★ Following — unfollow" : "☆ Follow this people";
        var fac = _world.Factions[fid];
        var members = _world.FactionMembers(fid);
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
        _yearLabel.Text = $"Year {_world.Year}     {_world.LivingCount} living     {_world.Chronicle.Events.Count} events";
        _playBtn.Text = _running ? "⏸ Pause" : "▶ Play";
        _speedLabel.Text = $"{_speed:0.##}×";
        _chatLabel.Text = $"chattiness ≥ {(int)_chatSlider.Value}";
    }
}
