// M3 (Yours channel) DONE: Follow button on both inspectors marks a bloodline/people; YOURS rows
// are gold-tagged + weight-boosted in the feed and followed dots are ringed cyan in MapView. The
// marked-set check is inline + O(living), and the bloodline grows virally at birth (not via a
// per-tick Feed.BuildFeed). This pass applied the V2 mythic-parchment UI handoff (year card,
// Saga feed v2 with event-class chips, sectioned inspectors, grouped time dock, parchment
// "How We Got Here") — presentation only, the sim tick path is untouched. See PROJECT_STATE.md.
// Living-atlas foundation pass: framed dock groups, parchment map place tags, warmed atlas
// palette — viewer styling only, per docs/VISUAL_STYLE.md.
using Godot;
using System;
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
    // Map-first panel economy (docs/VISUAL_STYLE.md "Panel economy contract"): the map owns
    // the screen unless the player explicitly opens the chronicle. Watch Mode keeps panels
    // compact; Inspect Mode docks one inspector to the left column; Chronicle Mode (catch-up
    // full thread, recap, memorial, writing desk) is the only license to cover more.
    private const int FeedWidth = 300;
    private const int BottomH = 78;

    private World _world = null!;
    private Control _root = null!;
    private MapView _map = null!;
    private VBoxContainer _feedList = null!;      // the world's channel
    private VBoxContainer _yoursList = null!;     // your story's channel — pinned above the world
    private Label _yoursHeader = null!;
    private Label _worldHeader = null!;
    private readonly List<FeedVisRow> _yoursVis = new();
    private RichTextLabel _inspector = null!;
    private Panel _inspectorPanel = null!;
    private ColorRect _inspectorAccent = null!;   // heraldic holder-colored stripe atop the lens
    private Label _inspectorTitle = null!;
    private Label _inspectorSub = null!;
    private Button _curseBtn = null!;
    // God-hand V1: the divine verbs live on the inspector of their target (bless/curse a
    // soul, protect/doom a people, omen/forest/spring a land) — same pattern as the curse.
    private Button _blessBtn = null!;
    private Button _protectBtn = null!;
    private Button _doomBtn = null!;
    private Button _omenBtn = null!;
    private Button _forestBtn = null!;
    private Button _springBtn = null!;
    private FateLedger _fateLedger = null!;
    private RememberedPlaces _places = null!;
    // Chronicle Replay (viewer-only over Replay.ChainFor): the focal event's cause chain
    // retold on the dimmed atlas. Beats with a true SiteId/RegionId get numbered map marks
    // along real cause edges; memory-only and unanchored beats live ONLY in the rail and
    // the beat card — never a fake pin. Entering replay pauses time (Chronicle Mode) and
    // restores the pace it took.
    private ReplayChain? _replayChain;
    private int _replayBeat;                       // index into _replayChain.Beats
    private bool _replayWasRunning;
    private Panel _replayPanel = null!;
    private RichTextLabel _replayBody = null!;
    private Label _replayCount = null!;
    private HSlider _replaySlider = null!;
    private bool _replaySliderGuard;               // suppress feedback while we set the slider
    // Consequence index for the ledger: events whose causes name a divine act, maintained
    // incrementally in StreamNewHeadlines — O(new events), never a history scan.
    private readonly HashSet<int> _divineSources = new();
    private readonly System.Collections.Generic.Dictionary<int, List<int>> _divineConsequences = new();
    private static readonly List<int> NoConsequences = new();
    private const int OmenBoost = 25;   // omen = attention, honestly: a surfacing weight, no roll changes
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
    private Button _dioramaBtn = null!;                     // Region Lens → open the diorama bridge
    private DioramaView? _diorama;                          // the diorama overlay, when open (read-only)
    private bool _dioramaWasRunning;                        // time-state to restore when the diorama closes
    private const int YoursBoost = 70;                      // weight added to a marked-bloodline event
    private const int FeedWindow = 60;                      // rolling world-feed window
    private const int YoursWindow = 14;                     // rolling your-story window (its own section)
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

    // The world save (Persistence V1): an input journal, never a snapshot — every divine
    // act with its year + target snapshot, the follows, and the attention state. On launch
    // the deterministic sim fast-forwards to the saved year with each act re-applied at
    // its recorded year, so the player-shaped world returns exactly. Saved on every act,
    // every follow change, every transition into pause, and on window close.
    private PlayerWorldStore _worldStore = null!;
    private bool _catchingUp;            // resume fast-forward: indexes update, but no cards, no pulses
    // Self-capture (dev evidence only): when LM_SHOTS is set to a directory, the viewer builds a
    // fresh world, fast-forwards, and writes in-engine PNGs of the atlas + a region lens, then
    // quits — never touching the player's save. Empty string = normal interactive launch.
    private string _capture = "";
    private int _ticksSinceSave;
    private const int AutosaveTicks = 200;        // crash-safety cadence (shown ticks)
    private const int CatchupFeedRows = 70;       // feed rows actually built for replayed history

    // The Cast (dramatis personae): the standing answer to "who is who". Membership
    // recomputes only when dirty (a follow changed, or a YOURS event streamed) — never a
    // standing per-tick scan of the ever-growing marked set.
    private CastPanel _cast = null!;
    private bool _castDirty = true;

    // Living introductions: when someone ENTERS the player's story (takes a watched seat,
    // is born to a followed soul, slays or weds one of yours), a small ambient card names
    // them — so the memorial stops being the first time the game frames a person. Non-modal,
    // never pauses, one at a time, wall-clock fade; each soul is introduced at most once.
    private PanelContainer _threadCard = null!;
    private RichTextLabel _threadBody = null!;
    private Tween? _threadTween;
    private int _threadPid = -1;
    private readonly HashSet<int> _introduced = new();
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
    // Watch Mode guard voice: a compact top toast (why-chip + the tale + verbs) instead of
    // the center card. The full card opens only on an explicit click — or immediately for
    // a memorial, the one moment that has earned the ceremony.
    private PanelContainer _guardToast = null!;
    private RichTextLabel _guardToastBody = null!;
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
    private Panel _helpPanel = null!;                     // the Guide — controls + map legend + your hand (Chronicle Mode)
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
        _capture = OS.GetEnvironment("LM_SHOTS");   // dev evidence capture: a directory, else ""

        var (config, names) = DataLoader.Load();
        _world = new World(Seed, config, names);
        _world.SeedWorld();
        _lastEventCount = 0;

        LoadCanon();
        LoadWorldStore();
        // Capture mode never reads the player's journal or follows and never saves — it paints a
        // pristine, deterministic world purely for screenshots.
        bool resumed = _capture == "" && ReplayWorldJournal();   // fast-forward, acts re-applied in place
        _lastEchoYear = _world.Year;
        if (_capture == "") RestoreFollows();
        Ui.LoadFonts();
        BuildUi();
        _map.World = _world;
        _map.Marked = _marked;       // same HashSet, mutated in place — map sees follows live
        _map.Souls = _followedSouls;
        _map.FollowedRegions = _followedRegions;
        _catchingUp = resumed;       // replayed history feeds the indexes, never cards or pulses
        StreamNewHeadlines();
        _catchingUp = false;
        _pendingGuardEventId = null;   // replayed history never interrupts (gated above; hardening)
        if (resumed) PrimeEchoMemory();   // echoes of the replayed years are old news, not punctuation
        CastChanged();   // hidden while nothing is followed; built ready
        StartChapter(resumed ? _world.Chronicle.Events.Count : 0);   // a fresh chapter opens NOW
        if (resumed) _running = false;   // the resumed world waits for the player
        RefreshTimeBar();
        _map.QueueRedraw();
        if (_capture != "") { _ = CaptureSequence(); return; }   // dev evidence run: shoot + quit
        if (!resumed) ShowHelp();        // first sight of a fresh age: open the Guide (pauses; "Begin watching" dismisses)
    }

    // F3 opens the North Star Diorama for the currently selected region (most-built held region
    // if nothing is selected) as a read-only overlay. Esc / "← Atlas" closes it, atlas intact.
    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Keycode: Key.F3 } && _diorama == null)
            OpenDiorama(_map.SelectedRegionId >= 0 ? _map.SelectedRegionId : MostBuiltRegion());
    }

    // Open the diorama as a read-only overlay over the live world (never a scene swap — the
    // atlas, follows, and save stay exactly as they are underneath). Viewer-only by construction.
    // TIME FREEZES while it is open (like Chronicle Mode): the diorama's chrome is a snapshot of
    // the opened year, so we pause Tick() and restore the prior play state on close — close returns
    // you to the same year you left. (Pacing-only; never changes Tick() count or order — verify-safe.)
    private void OpenDiorama(int regionId)
    {
        if (_diorama != null) return;
        if (regionId < 0 || regionId >= _world.Regions.Count) regionId = MostBuiltRegion();
        _dioramaWasRunning = _running;
        _running = false;                         // freeze time; the diorama owns the moment
        _diorama = new DioramaView { SourceWorld = _world, SourceRegionId = regionId, OnClose = CloseDiorama };
        _diorama.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_diorama);                       // last child → draws over the atlas + its panels
    }

    private void CloseDiorama()
    {
        _diorama?.QueueFree();
        _diorama = null;
        _running = _dioramaWasRunning;            // back to the atlas at the same year you left
    }

    // The most-built held region (fallback when nothing is selected); any most-built region if landless.
    private int MostBuiltRegion()
    {
        int best = -1, bestN = -1;
        foreach (var r in _world.Regions)
            if (r.ControllingFactionId != null)
            {
                int n = _world.Sites.ForRegion(r.Id).Count;
                if (n > bestN) { bestN = n; best = r.Id; }
            }
        if (best < 0)
            for (int i = 0; i < _world.Regions.Count; i++)
            {
                int n = _world.Sites.ForRegion(i).Count;
                if (n > bestN) { bestN = n; best = i; }
            }
        return Math.Max(0, best);
    }

    // Dev evidence only (LM_SHOTS): fast-forward a fresh world, then write in-engine PNGs of the
    // atlas and a region lens so a screenshot is the REAL viewer, not a mock. Never saves.
    private async System.Threading.Tasks.Task CaptureSequence()
    {
        _running = false;
        DisplayServer.WindowSetSize(new Vector2I(1600, 920));   // crisp evidence frames
        _catchingUp = true;                       // feed indexes/marks, no cards or pulses
        int target = _world.Config.StartYear + 120;
        while (_world.Year < target) { _world.Tick(); StreamNewHeadlines(); }
        _catchingUp = false;
        _pendingGuardEventId = null;
        CastChanged();
        RefreshTimeBar();
        _map.ResetCamera();
        _map.QueueRedraw();

        await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);
        Shot("01_atlas");

        // The most-built held land makes the richest lens shot.
        int rid = -1, bestSites = -1;
        foreach (var r in _world.Regions)
            if (r.ControllingFactionId is not null)
            {
                int n = _world.Sites.ForRegion(r.Id).Count;
                if (n > bestSites) { bestSites = n; rid = r.Id; }
            }
        if (rid >= 0) { OnRegionPicked(rid); _map.FocusRegion(rid); }
        await ToSignal(GetTree().CreateTimer(1.8), SceneTreeTimer.SignalName.Timeout);
        Shot("02_region_lens");

        // The production bridge: open the diorama overlay for the SAME selected region of the
        // LIVE world (the real OpenDiorama path, not the standalone seed-7 scene).
        if (rid >= 0)
        {
            OpenDiorama(rid);
            await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
            Shot("03_diorama_bridge");
            CloseDiorama();
        }

        // Fallback: a wild / sparse region (unclaimed — no banner, honest "unclaimed country").
        int wild = -1, wbest = -1;
        foreach (var r in _world.Regions)
            if (r.ControllingFactionId is null)
            {
                int n = _world.Sites.ForRegion(r.Id).Count;
                if (n > wbest) { wbest = n; wild = r.Id; }
            }
        if (wild >= 0)
        {
            await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
            OpenDiorama(wild);
            await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
            Shot("04_diorama_fallback");
            CloseDiorama();
        }

        await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
        GetTree().Quit();
    }

    private void Shot(string name)
    {
        var img = GetViewport().GetTexture().GetImage();
        string path = System.IO.Path.Combine(_capture, name + ".png");
        var err = img.SavePng(path);
        GD.Print($"[capture] {name}.png → {path} ({err})");
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest && _worldStore is not null) SaveWorldStore();
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

    // The world save loads exactly like the canon: unreadable files are set aside as
    // .bak (never destroyed), future-schema files stay untouched and read-only.
    private void LoadWorldStore()
    {
        string path = ProjectSettings.GlobalizePath($"user://world_seed{Seed}.json");
        var (store, warning) = PlayerWorldStore.LoadOrNew(path, Seed);
        if (warning is not null)
        {
            GD.PushWarning($"world save: {warning}");
            if (store.ReadOnly && !store.FutureSchema)
            {
                try
                {
                    System.IO.File.Move(path, path + ".bak", overwrite: false);
                    GD.PushWarning($"world save: unreadable file set aside as {path}.bak");
                    (store, _) = PlayerWorldStore.LoadOrNew(path, Seed);
                }
                catch (System.Exception ex) when (ex is System.IO.IOException or System.UnauthorizedAccessException)
                { /* a .bak already exists or the file is locked — stay read-only this session */ }
            }
        }
        _worldStore = store;
    }

    // Replay the journal: re-apply each saved act when the run reaches its year, ticking
    // forward to the saved resume year. Deterministic sim + deterministic re-application
    // = the same world as the last session (the `save` gate proves it byte-identically).
    private bool ReplayWorldJournal()
    {
        void Apply()
        {
            foreach (var (_, ev) in _worldStore.ApplyDue(_world))
                if (ev is not null) _divineSources.Add(ev.Id);   // the ledger's consequence roots
        }
        Apply();
        int target = Math.Max(_worldStore.ResumeYear, _world.Year);
        while (_world.Year < target)
        {
            _world.Tick();
            Apply();
        }
        foreach (var q in _worldStore.QuarantinedActs)
            GD.PushWarning($"world save: act #{q.Seq} ({q.Kind} {q.TargetType} {q.TargetId}, Yr {q.Year}) "
                + "no longer matches this world — quarantined, kept in the file");
        return _worldStore.ActCount > 0 || _worldStore.ResumeYear > _world.Config.StartYear
            || _worldStore.Follows.Souls.Count + _worldStore.Follows.Bloodlines.Count
             + _worldStore.Follows.Peoples.Count + _worldStore.Follows.Lands.Count > 0;
    }

    // Follows come back after the fast-forward (on a faithful replay every previously
    // followed soul exists again at the resume year); drift drops the follow with a
    // warning rather than ever re-attaching the mark to a different soul.
    private void RestoreFollows()
    {
        var (souls, lines, peoples, lands, dropped) = _worldStore.RestoreFollows(_world);
        _followedSouls.UnionWith(souls);
        _seedPeople.UnionWith(lines);
        _markedFactions.UnionWith(peoples);
        _followedRegions.UnionWith(lands);
        var (people, _) = Feed.ExpandMarked(_world, _seedPeople, _markedFactions);
        _marked.UnionWith(people);
        foreach (var kv in _worldStore.LastSeen)
            if (kv.Value >= 0 && kv.Value < _world.Chronicle.Events.Count)
                _lastSeenEvent[kv.Key] = kv.Value;
        foreach (var d in dropped)
            GD.PushWarning($"world save: follow {d} no longer matches this world — dropped");
    }

    // Echoes detected over replayed history are memory, not news — mark them seen so the
    // next echo scan cards only what happens from here on.
    private void PrimeEchoMemory()
    {
        foreach (var echo in Echoes.DetectAll(_world))
            if (echo.YearSpan.First > _echoSeen.GetValueOrDefault(echo.Archetype, int.MinValue))
                _echoSeen[echo.Archetype] = echo.YearSpan.First;
    }

    // One funnel for every write: resume year, follows, and attention state travel
    // together, atomically. Refuses quietly on a read-only store (file preserved).
    private void SaveWorldStore()
    {
        if (_capture != "") return;   // dev capture must never overwrite the player's world
        if (_worldStore.ReadOnly) return;
        _worldStore.ResumeYear = _world.Year;
        _worldStore.SetFollows(_world, _followedSouls, _seedPeople, _markedFactions, _followedRegions);
        _worldStore.SetLastSeen(_lastSeenEvent);
        try { _worldStore.Save(); }
        catch (System.Exception ex) when (ex is System.IO.IOException or System.UnauthorizedAccessException)
        { GD.PushWarning($"world save: could not write ({ex.GetType().Name})"); }
        _ticksSinceSave = 0;
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
            bool ticked = false;
            while (_accum >= interval && budget-- > 0)
            {
                _accum -= interval;
                _world.Tick();
                ticked = true;
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
            if (ticked) { _cast.Refresh(_castDirty); _castDirty = false; }   // O(cap) labels; membership only when dirty
            if (ticked && ++_ticksSinceSave >= AutosaveTicks) SaveWorldStore();   // crash-safety heartbeat
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

        _map = new MapView
        {
            PersonPicked = OnPersonPicked, SoulPicked = OnSoulGlimpse, FactionPicked = OnFactionPicked,
            RegionPicked = OnRegionPicked, SitePicked = OnSitePicked,
            ReplayBeatPicked = OnReplayBeatPicked, TurningPicked = OnTurningPicked,
        };
        root.AddChild(_map);
        _map.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _map.OffsetRight = -FeedWidth;
        _map.OffsetBottom = -BottomH;

        BuildFeed(root);
        BuildBottomBar(root);
        BuildYearCard(root);
        BuildLeftDock(root);    // cast + inspector share one left column — structurally unstackable
        BuildThreadCard(root);
        BuildCatchup(root);
        BuildFateLedger(root);
        BuildRememberedPlaces(root);
        BuildReplayPanel(root);
        BuildGlimpse(root);
        BuildRecap(root);
        BuildHelp(root);        // the Guide — opened by the player (Chronicle Mode reading surface)
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

    // The left column: cast on top (compact by default), the one inspector surface below.
    // A VBox makes stacking impossible — the old bug was both panels pinned at (12,132).
    private void BuildLeftDock(Control root)
    {
        var dock = new VBoxContainer();
        root.AddChild(dock);
        dock.AnchorLeft = 0; dock.AnchorTop = 0; dock.AnchorRight = 0; dock.AnchorBottom = 1;
        dock.OffsetLeft = 12; dock.OffsetTop = 132;
        dock.OffsetRight = 12 + 330; dock.OffsetBottom = -(BottomH + 10);
        dock.AddThemeConstantOverride("separation", 8);

        _cast = new CastPanel { SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
        dock.AddChild(_cast);
        // A cast click is "find them": inspect AND lean the lens onto their place in the world.
        _cast.Setup(() => _world, _followedSouls, _seedPeople, _marked, _markedFactions,
                    _followedRegions, _lastSeenEvent,
                    pid => { OnPersonPicked(pid); _map.FocusPerson(pid); });

        BuildInspector(dock);
    }

    // The introduction card: top-center, ambient (glimpse rank — below every pausing card),
    // click to inspect the newcomer, fades on its own.
    private void BuildThreadCard(Control root)
    {
        _threadCard = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        root.AddChild(_threadCard);
        _threadCard.AnchorLeft = 0.5f; _threadCard.AnchorRight = 0.5f;
        _threadCard.AnchorTop = 0; _threadCard.AnchorBottom = 0;
        _threadCard.OffsetLeft = -210; _threadCard.OffsetRight = 210;
        _threadCard.OffsetTop = 48;
        var box = Ui.PanelBox(8);
        box.BorderColor = Ui.Gold;
        _threadCard.AddThemeStyleboxOverride("panel", box);
        _threadCard.GuiInput += ev =>
        {
            if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } && _threadPid >= 0)
            {
                _threadCard.Visible = false;
                OnPersonPicked(_threadPid);
            }
        };

        var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        foreach (var s in new[] { "left", "right" }) margin.AddThemeConstantOverride($"margin_{s}", 12);
        foreach (var s in new[] { "top", "bottom" }) margin.AddThemeConstantOverride($"margin_{s}", 7);
        _threadCard.AddChild(margin);
        _threadBody = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(396, 0),
        };
        _threadBody.AddThemeColorOverride("default_color", Ui.Ink);
        _threadBody.AddThemeFontOverride("bold_font", Ui.SerifBold);
        margin.AddChild(_threadBody);
    }

    // Who just entered your story? Rare by design — only followed-soul kin, watched seats,
    // and those who strike at what you follow. Each soul introduced at most once.
    private void MaybeIntroduce(Event e)
    {
        if (_followedSouls.Count == 0 && _marked.Count == 0
            && _markedFactions.Count == 0 && _followedRegions.Count == 0) return;
        int? pid = null;
        string? line = null;
        switch (e.Type)
        {
            case "succession" when e.Participants.Count > 0
                && _world.People.TryGetValue(e.Participants[0], out var heir)
                && heir.Alive && heir.IsLeader:   // streamed after the full tick — the heir may already be gone
                bool watchedSeat = _markedFactions.Contains(heir.FactionId)
                    || (_followedRegions.Count > 0 && _world.Factions.TryGetValue(heir.FactionId, out var hf)
                        && hf.ControlledRegions.Any(s => int.TryParse(s, out int wr) && _followedRegions.Contains(wr)));
                if (watchedSeat)
                {
                    pid = heir.Id;
                    line = $"now leads {_world.Factions[heir.FactionId].Name}";
                    _castDirty = true;   // a watched seat changed hands — the roster must follow
                }
                break;
            case "birth" when e.Participants.Count >= 3:
                foreach (int par in new[] { e.Participants[1], e.Participants[2] })
                    if (_followedSouls.Contains(par))
                    { pid = e.Participants[0]; line = $"child of {_world.People[par].Name}, a soul you follow"; break; }
                break;
            case "murder" when e.Participants.Count >= 2:
                int victim = e.Participants[0], killer = e.Participants[1];
                if ((_marked.Contains(victim) || _followedSouls.Contains(victim))
                    && !_marked.Contains(killer) && !_followedSouls.Contains(killer))
                {
                    pid = killer;
                    line = $"slew {_world.People[victim].Name}, "
                        + (_followedSouls.Contains(victim) ? "a soul you followed" : "of the line you follow");
                }
                break;
            case "marriage" when e.Participants.Count >= 2:
                bool aSoul = _followedSouls.Contains(e.Participants[0]);
                bool bSoul = _followedSouls.Contains(e.Participants[1]);
                if (aSoul != bSoul)
                {
                    pid = aSoul ? e.Participants[1] : e.Participants[0];
                    line = $"wed to {_world.People[aSoul ? e.Participants[0] : e.Participants[1]].Name}, a soul you follow";
                }
                break;
        }
        if (pid is not int id || line is null) return;
        if (_followedSouls.Contains(id) || !_introduced.Add(id)) return;   // never re-introduce, never introduce a known soul
        ShowThreadCard(id, line);
    }

    private void ShowThreadCard(int pid, string line)
    {
        if (!_world.People.TryGetValue(pid, out var p)) return;
        _threadPid = pid;
        _threadBody.Text =
            $"[color=#{Ui.Hex(Ui.Faded)}]A NEW THREAD[/color]  {PersonSigils.Bb(_world, pid)} [b]{p.Name}[/b] — {line}"
            + $"  [color=#{Ui.Hex(Ui.FadedSub)}]· click to meet them[/color]";
        _threadTween?.Kill();
        _threadCard.Modulate = Colors.White;
        _threadCard.Visible = true;
        // Wall-clock linger then fade — presentation only, never touches the tick.
        _threadTween = _threadCard.CreateTween();
        _threadTween.TweenInterval(6.0);
        _threadTween.TweenProperty(_threadCard, "modulate:a", 0f, 1.4f);
        _threadTween.TweenCallback(Callable.From(() => { _threadCard.Visible = false; _threadCard.Modulate = Colors.White; }));
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

        // Two channels, one scroll: your story pinned above the world's churn, so the
        // tales you asked for stop fighting the firehose for the same rows. Headers only
        // appear while anything is followed — no empty chrome.
        var channels = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        channels.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(channels);

        _yoursHeader = Ui.SectionLabel("Your story", 11);
        _yoursHeader.AddThemeColorOverride("font_color", Ui.Gold);
        _yoursHeader.Visible = false;
        channels.AddChild(_yoursHeader);
        _yoursList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _yoursList.AddThemeConstantOverride("separation", 8);
        channels.AddChild(_yoursList);

        _worldHeader = Ui.SectionLabel("The world", 11);
        _worldHeader.Visible = false;
        channels.AddChild(_worldHeader);
        _feedList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _feedList.AddThemeConstantOverride("separation", 8);
        channels.AddChild(_feedList);

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
        foreach (var s in new[] { "left", "right" })
            margin.AddThemeConstantOverride($"margin_{s}", 10);
        foreach (var s in new[] { "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{s}", 5);
        bar.AddChild(margin);

        var hb = new HBoxContainer();
        hb.AddThemeConstantOverride("separation", 12);
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
        // Visual Pipeline Spike V1 — opt-in overlay of Blender placeholder dioramas (off by default).
        var spikeBtn = new Button { Text = "▦ spike", TooltipText = "Visual Pipeline Spike V1 — overlay Blender placeholder dioramas on the atlas (opt-in, viewer-only test)" };
        spikeBtn.Pressed += () => { _map.SpikeAssetsEnabled = !_map.SpikeAssetsEnabled; Ui.StyleButton(spikeBtn, _map.SpikeAssetsEnabled); };
        lensRow.AddChild(spikeBtn);
        Ui.StyleButton(spikeBtn, _map.SpikeAssetsEnabled);

        // --- Fate group: the ledger of the player's hand ---
        var fateRow = DockGroup(hb, "Fate");
        var ledgerBtn = new Button { Text = "✦ ledger", TooltipText = "The Fate Ledger — every act of your hand, and what the chronicle traced to it" };
        Ui.StyleButton(ledgerBtn);
        ledgerBtn.Pressed += () =>
        {
            if (_fateLedger.Visible) { _fateLedger.Visible = false; return; }
            _catchupPanel.Visible = false;   // one reading sheet at a time (panel economy)
            _places.Visible = false;
            if (_replayChain is not null) CloseReplay();
            _fateLedger.Open();
        };
        fateRow.AddChild(ledgerBtn);
        var placesBtn = new Button { Text = "❖ places", TooltipText = "Remembered Places — every place the record has truly touched, anchors named honestly" };
        Ui.StyleButton(placesBtn);
        placesBtn.Pressed += () =>
        {
            if (_places.Visible) { _places.Visible = false; return; }
            _catchupPanel.Visible = false;   // one reading sheet at a time (panel economy)
            _fateLedger.Visible = false;
            if (_replayChain is not null) CloseReplay();
            _places.Open();
        };
        fateRow.AddChild(placesBtn);

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

        // --- Guide group: how to read the atlas, and a fresh start (far right) ---
        var guideRow = DockGroup(hb, "Guide");
        var helpBtn = new Button { Text = "? Guide", TooltipText = "How to watch — controls, the map's marks, and the powers of your hand" };
        Ui.StyleButton(helpBtn);
        helpBtn.Pressed += ShowHelp;
        guideRow.AddChild(helpBtn);
        var newWorldBtn = new Button { Text = "✶ New World", TooltipText = "Begin a fresh age — discards your saved acts, follows, and progress (your written canon is kept)" };
        Ui.StyleButton(newWorldBtn);
        newWorldBtn.Pressed += ConfirmNewWorld;
        guideRow.AddChild(newWorldBtn);

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

    private void BuildInspector(Control parent)
    {
        // Lives inside the left dock under the cast: docked side inspector, fills the
        // column down to the bottom bar, never floats over another panel.
        _inspectorPanel = new Panel { Visible = false, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        parent.AddChild(_inspectorPanel);
        _inspectorPanel.AddThemeStyleboxOverride("panel", Ui.PanelBox());

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        foreach (var s in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{s}", 12);
        _inspectorPanel.AddChild(margin);

        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 6);
        margin.AddChild(vb);

        // A heraldic stripe in the holder's cloth color crowns the lens — the inspected place or
        // person wears whose land it is, so the panel reads as a chronicle page, not a data table.
        _inspectorAccent = new ColorRect { Color = Ui.Gold, CustomMinimumSize = new Vector2(0, 5) };
        vb.AddChild(_inspectorAccent);

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
            _map.SelectedSiteId = -1;
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

        _dioramaBtn = new Button { Text = "⛰ Enter the Diorama", Visible = false,
            TooltipText = "See this land up close — an isometric diorama of its real places (read-only view)" };
        Ui.StyleButton(_dioramaBtn);
        _dioramaBtn.Pressed += () => { if (_map.SelectedRegionId >= 0) OpenDiorama(_map.SelectedRegionId); };
        vb.AddChild(_dioramaBtn);

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

        _blessBtn = new Button { Text = "✦ Lay Blessing on this soul", Visible = false,
            TooltipText = "Fate leans gently toward this life — the death roll eases, never a guarantee" };
        Ui.StyleButton(_blessBtn, active: true);
        _blessBtn.Pressed += OnBlessPressed;
        vb.AddChild(_blessBtn);

        _protectBtn = new Button { Text = "❧ Protect this people", Visible = false,
            TooltipText = "For a season of years, famine weighs lighter on them and fortune mends faster" };
        Ui.StyleButton(_protectBtn, active: true);
        _protectBtn.Pressed += () => OnFactionAct(fid => _world.ProtectFaction(fid));
        vb.AddChild(_protectBtn);

        _doomBtn = new Button { Text = "☄ Pronounce Doom on this people", Visible = false,
            TooltipText = "For a season of years, their fortunes run thin and famine bites deeper" };
        Ui.StyleButton(_doomBtn, active: true, activeBg: Ui.Ember);
        _doomBtn.AddThemeColorOverride("font_color", new Color("f2e9d2"));
        _doomBtn.AddThemeColorOverride("font_hover_color", new Color("f2e9d2"));
        _doomBtn.AddThemeColorOverride("font_pressed_color", new Color("f2e9d2"));
        _doomBtn.Pressed += () => OnFactionAct(fid => _world.DoomFaction(fid));
        vb.AddChild(_doomBtn);

        _omenBtn = new Button { Text = "✶ Seed an Omen here", Visible = false,
            TooltipText = "The eye of fate turns here — this land's tales surface louder while the omen hangs" };
        Ui.StyleButton(_omenBtn, active: true);
        _omenBtn.Pressed += () => OnRegionAct(rid => _world.SeedOmen(rid));
        vb.AddChild(_omenBtn);

        _forestBtn = new Button { Text = "✿ Seed a Forest", Visible = false,
            TooltipText = "Raise a forest across this land — real terrain, recorded as your act (rock and water refuse it)" };
        Ui.StyleButton(_forestBtn, active: true);
        _forestBtn.Pressed += () => OnRegionAct(rid => _world.SeedForest(rid));
        vb.AddChild(_forestBtn);

        _springBtn = new Button { Text = "≈ Call a Spring", Visible = false,
            TooltipText = "Call water from the earth — a small lake and wetland, recorded as your act" };
        Ui.StyleButton(_springBtn, active: true);
        _springBtn.Pressed += () => OnRegionAct(rid => _world.CallSpring(rid));
        vb.AddChild(_springBtn);
    }

    private void BuildFateLedger(Control root)
    {
        _fateLedger = new FateLedger();
        root.AddChild(_fateLedger);
        _fateLedger.Setup(() => _world,
            srcId => _divineConsequences.TryGetValue(srcId, out var list) ? list : NoConsequences,
            link => OnInspectorLink(link));
    }

    private void BuildRememberedPlaces(Control root)
    {
        _places = new RememberedPlaces();
        root.AddChild(_places);
        _places.Setup(() => _world, _regionActivity, link => OnInspectorLink(link));
    }

    // ----------------------------------------------------- chronicle replay (viewer)

    // The beat card + scrubber: a compact parchment strip over the map's lower left while
    // the replay path owns the stage. The rail stays the catch-up sheet (numbered, with
    // the current beat marked) — unplaced beats live there honestly, never on the map.
    private void BuildReplayPanel(Control root)
    {
        _replayPanel = new Panel { Visible = false };
        root.AddChild(_replayPanel);
        _replayPanel.AnchorLeft = 0; _replayPanel.AnchorRight = 0;
        _replayPanel.AnchorTop = 1; _replayPanel.AnchorBottom = 1;
        _replayPanel.OffsetLeft = 8; _replayPanel.OffsetRight = 396;
        _replayPanel.OffsetTop = -(BottomH + 218); _replayPanel.OffsetBottom = -(BottomH + 8);
        var box = Ui.PanelBox();
        box.BorderColor = Ui.Gold;
        _replayPanel.AddThemeStyleboxOverride("panel", box);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        foreach (var s in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{s}", 12);
        _replayPanel.AddChild(margin);

        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 6);
        margin.AddChild(vb);

        var hdr = new HBoxContainer();
        hdr.AddThemeConstantOverride("separation", 8);
        vb.AddChild(hdr);
        var cap = Ui.SectionLabel("⟲ chronicle replay");
        cap.AddThemeColorOverride("font_color", Ui.Gold);
        cap.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hdr.AddChild(cap);
        _replayCount = new Label();
        _replayCount.AddThemeFontSizeOverride("font_size", 12);
        _replayCount.AddThemeColorOverride("font_color", Ui.FadedSub);
        hdr.AddChild(_replayCount);
        var close = new Button { Text = "✕", CustomMinimumSize = new Vector2(24, 24) };
        Ui.StyleButton(close);
        close.Pressed += CloseReplay;
        hdr.AddChild(close);

        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        vb.AddChild(scroll);
        _replayBody = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(350, 0),
        };
        _replayBody.AddThemeFontSizeOverride("normal_font_size", 13);
        _replayBody.AddThemeColorOverride("default_color", Ui.Ink);
        _replayBody.AddThemeFontOverride("bold_font", Ui.SerifBold);
        _replayBody.MetaClicked += meta => OnInspectorLink(meta.AsString());
        scroll.AddChild(_replayBody);

        var controls = new HBoxContainer();
        controls.AddThemeConstantOverride("separation", 6);
        vb.AddChild(controls);
        var prev = new Button { Text = "◀", CustomMinimumSize = new Vector2(34, 0), TooltipText = "Step back a beat" };
        Ui.StyleButton(prev);
        prev.Pressed += () => StepReplay(-1);
        controls.AddChild(prev);
        _replaySlider = new HSlider
        {
            MinValue = 1, MaxValue = 2, Step = 1,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        _replaySlider.ValueChanged += v =>
        {
            if (_replaySliderGuard || _replayChain is null) return;
            _replayBeat = Mathf.Clamp((int)v - 1, 0, _replayChain.Beats.Count - 1);
            RenderReplayBeat();
        };
        controls.AddChild(_replaySlider);
        var next = new Button { Text = "▶", CustomMinimumSize = new Vector2(34, 0), TooltipText = "Step forward a beat" };
        Ui.StyleButton(next);
        next.Pressed += () => StepReplay(+1);
        controls.AddChild(next);
    }

    private void StepReplay(int dir)
    {
        if (_replayChain is null) return;
        _replayBeat = Mathf.Clamp(_replayBeat + dir, 0, _replayChain.Beats.Count - 1);
        RenderReplayBeat();
    }

    // A numbered map mark was clicked — scrub straight to that beat (numbers are 1-based
    // rail positions, so the mapping back is direct).
    private void OnReplayBeatPicked(int number)
    {
        if (_replayChain is null) return;
        _replayBeat = Mathf.Clamp(number - 1, 0, _replayChain.Beats.Count - 1);
        RenderReplayBeat();
    }

    // A turning-point mark was clicked — open the pivot's thread (the header names the
    // turning point and offers the replay).
    private void OnTurningPicked(int eventId) => OpenCatchup(eventId);

    private void OpenReplay(int eventId)
    {
        _replayChain = Replay.ChainFor(_world, eventId);
        _replayBeat = 0;                          // start at the chain's origin, retell forward
        _replayWasRunning = _running;
        _running = false;                         // Chronicle Mode: the retelling owns time
        RefreshTimeBar();
        BuildReplayMarks();
        _replayPanel.Visible = true;
        RenderReplayBeat();
    }

    private void CloseReplay()
    {
        _replayPanel.Visible = false;
        _replayChain = null;
        _map.ReplayActive = false;
        _map.ReplayMarks = null;
        _map.ReplayEdges = null;
        _running = _replayWasRunning;
        RefreshTimeBar();
        if (_catchupPanel.Visible) RenderCatchup();   // clear the ► beat marker
    }

    // Build the map overlay from the chain: marks ONLY for honestly anchored beats (a true
    // site cell, else the region's heart), numbered by full-rail position so the rail and
    // the map agree; edges are the real recorded cause links between marked beats, the
    // proximate-cause spine bold and every other branch faint.
    private void BuildReplayMarks()
    {
        if (_replayChain is null) return;
        var beats = _replayChain.Beats;
        var marks = new List<MapView.ReplayMark>();
        var markOfEvent = new System.Collections.Generic.Dictionary<int, int>();
        for (int i = 0; i < beats.Count; i++)
        {
            var b = beats[i];
            Vector2? norm = null;
            if (b.SiteId is int sid)
            {
                var s = _world.Sites.Get(sid);
                norm = new Vector2(s.Nx, s.Ny);
            }
            else if (b.RegionId is int rid)
            {
                var r = _world.Regions[rid];
                norm = new Vector2(r.X, r.Y);
            }
            if (norm is not Vector2 n) continue;   // memory-only/unanchored: rail only, no pin
            markOfEvent[b.EventId] = marks.Count;
            marks.Add(new MapView.ReplayMark { Norm = n, Number = i + 1 });
        }

        // The spine: the proximate-cause walk back from the focal event.
        var spine = new HashSet<int>();
        var byEvent = new System.Collections.Generic.Dictionary<int, ReplayBeat>();
        foreach (var b in beats) byEvent[b.EventId] = b;
        int cur = _replayChain.FocalEventId;
        while (byEvent.TryGetValue(cur, out var sb))
        {
            spine.Add(cur);
            if (sb.CauseEventId is not int cid || !byEvent.ContainsKey(cid)) break;
            cur = cid;
        }

        var edges = new List<(int a, int b, bool spine)>();
        foreach (var b in beats)
        {
            if (b.CauseEventId is not int cause) continue;
            if (!markOfEvent.TryGetValue(cause, out int ma) || !markOfEvent.TryGetValue(b.EventId, out int mb))
                continue;   // an edge draws only when BOTH ends are honestly placed
            edges.Add((ma, mb, spine.Contains(b.EventId) && spine.Contains(cause)));
        }

        _map.ReplayMarks = marks;
        _map.ReplayEdges = edges;
        _map.ReplayActive = true;
    }

    private void RenderReplayBeat()
    {
        if (_replayChain is null) return;
        var beats = _replayChain.Beats;
        var b = beats[_replayBeat];
        var e = _world.Chronicle.Get(b.EventId);

        _replaySliderGuard = true;
        _replaySlider.MaxValue = beats.Count;
        _replaySlider.Value = _replayBeat + 1;
        _replaySliderGuard = false;
        _replayCount.Text = $"beat {_replayBeat + 1} of {beats.Count} · Yr {beats[0].Year}–{beats[^1].Year}";

        if (_map.ReplayMarks is not null)
            foreach (var m in _map.ReplayMarks) m.Current = m.Number == _replayBeat + 1;

        var sb = new StringBuilder();
        var cls = Ui.ClassOf(e.Type);
        // The proven connector voiced above the beat, exactly like the thread does.
        if (_replayBeat > 0 && StoryGrammar.ProximateLink(_world, e) is ChainLink link)
            sb.AppendLine($"[color=#{Ui.Hex(Ui.FadedSub)}][i]{StoryCopy.ConnectorPhrase(link)}[/i][/color]");
        sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]Yr {e.Year}[/color] [color=#{Ui.Hex(cls.Color)}]{cls.Glyph}[/color] [b]{e.Text}[/b]");
        string place = StoryCopy.AnchorPhrase(_world, e) is string ap
            ? $"{ap} · {StoryCopy.StatusLabel(b.Status)}"
            : StoryCopy.StatusLabel(b.Status);
        sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]{place}[/color]");
        if (b.Status is "memory-only" or "unanchored")
            sb.AppendLine($"[color=#{Ui.Hex(Ui.FadedSub)}][i]this beat draws no pin — the map never claims a place the record does not[/i][/color]");
        sb.AppendLine($"      {Link("e:" + e.Id, "open this tale's thread")}");
        _replayBody.Text = sb.ToString();

        // The lens follows the retelling onto each truly placed beat.
        if (b.SiteId is int fsid) _map.FocusSite(fsid);
        else if (b.RegionId is int frid) _map.FocusRegion(frid);

        if (_catchupPanel.Visible) RenderCatchup();   // keep the rail's ► marker in step
    }

    // One funnel for every divine act: ledger the source, surface the event immediately,
    // and honor a guard trigger if the act itself crossed it.
    private void RecordDivine(Event? ev)
    {
        if (ev is null) return;
        _divineSources.Add(ev.Id);
        JournalAct(ev);
        StreamNewHeadlines();
        if (_pendingGuardEventId is not null) ShowGuardCard();
    }

    // Every live act of the hand lands in the world save as it happens — the journal
    // entry is derived from the act's own DivinePressure, so the two ledgers can't drift.
    private void JournalAct(Event ev)
    {
        if (_worldStore.ReadOnly) return;
        for (int i = _world.DivinePressures.Count - 1; i >= 0; i--)
            if (_world.DivinePressures[i].SourceEventId == ev.Id)
            {
                _worldStore.RecordAct(_world, _world.DivinePressures[i]);
                SaveWorldStore();
                return;
            }
    }

    private void OnBlessPressed()
    {
        if (_selectedPersonId is not int id || !_world.People.TryGetValue(id, out var p)) return;
        if (!p.Alive || p.Blessed) return;
        RecordDivine(_world.BlessPerson(p));
        OnPersonPicked(id);
    }

    private void OnFactionAct(Func<string, Event> act)
    {
        if (_selectedFactionId is not string fid) return;
        try { RecordDivine(act(fid)); }
        catch (ArgumentException) { /* the verb's visibility gate should prevent this */ }
        OnFactionPicked(fid);
    }

    private void OnRegionAct(Func<int, Event?> act)
    {
        int rid = _map.SelectedRegionId;
        if (rid < 0 || rid >= _world.Regions.Count) return;
        try { RecordDivine(act(rid)); }   // a null act (land refused) records nothing, honestly
        catch (ArgumentException) { }
        OnRegionPicked(rid);
    }

    private bool OmenActive(int regionId)
    {
        foreach (var pr in _world.DivinePressures)
            if (pr.Kind == DivinePressureKind.Omen && pr.TargetId == regionId.ToString() && pr.IsActive(_world))
                return true;
        return false;
    }

    private void BuildCatchup(Control root)
    {
        // A right side sheet over the feed rail, not a center modal: quick beats read beside
        // the living map. "Full thread" widens the sheet — that deeper read is Chronicle Mode,
        // entered on purpose. RenderCatchup drives the width.
        _catchupPanel = new Panel { Visible = false };
        root.AddChild(_catchupPanel);
        _catchupPanel.AnchorLeft = 1; _catchupPanel.AnchorRight = 1;
        _catchupPanel.AnchorTop = 0; _catchupPanel.AnchorBottom = 1;
        _catchupPanel.OffsetLeft = -408; _catchupPanel.OffsetRight = -8;
        _catchupPanel.OffsetTop = 10; _catchupPanel.OffsetBottom = -(BottomH + 6);
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
        title.AddThemeFontSizeOverride("font_size", 18);
        title.AddThemeColorOverride("font_color", Ui.InkDeep);
        hb.AddChild(title);
        _catchupQuickBtn = new Button { Text = "Quick beats" };
        _catchupQuickBtn.Pressed += () => { _catchupQuick = true; RenderCatchup(); };
        hb.AddChild(_catchupQuickBtn);
        _catchupFullBtn = new Button { Text = "Full thread" };
        _catchupFullBtn.Pressed += () => { _catchupQuick = false; RenderCatchup(); };
        hb.AddChild(_catchupFullBtn);
        var replayBtn = new Button { Text = "⟲ Replay", TooltipText = "Retell this chain on the map — anchored beats draw the path; unplaced beats stay in this rail" };
        replayBtn.Pressed += () => { if (_catchupEventId is int eid) OpenReplay(eid); };
        hb.AddChild(replayBtn);
        var close = new Button { Text = "✕", CustomMinimumSize = new Vector2(28, 28) };
        Ui.StyleButton(close);
        close.Pressed += () =>
        {
            _catchupPanel.Visible = false;
            if (_replayChain is not null) CloseReplay();   // the rail closing ends the retelling
        };
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
            CustomMinimumSize = new Vector2(340, 0),
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
            if (_glimpsePid >= 0 && _followedSouls.Remove(_glimpsePid))
            { _map.QueueRedraw(); CastChanged(); SaveWorldStore(); }
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
        // Active omens, snapshotted once per stream pass — O(pressures), a handful at most.
        HashSet<int>? omens = null;
        foreach (var pr in _world.DivinePressures)
            if (pr.Kind == DivinePressureKind.Omen && pr.IsActive(_world)
                && int.TryParse(pr.TargetId, out int omenRid))
                (omens ??= new()).Add(omenRid);

        for (int i = _lastEventCount; i < events.Count; i++)
        {
            _regionActivity.Observe(events[i]);
            foreach (var c in events[i].Causes)
            {
                _consCount[c] = _consCount.GetValueOrDefault(c) + 1;
                // The fate ledger's consequence trail: anything the chronicle traces to an
                // act of the hand. Incremental, capped per act so the lists stay bounded.
                if (_divineSources.Contains(c))
                {
                    if (!_divineConsequences.TryGetValue(c, out var dl)) { dl = new(); _divineConsequences[c] = dl; }
                    if (dl.Count < 40) dl.Add(events[i].Id);
                }
            }
            // Place memory: a truly anchored event of a marking kind scars its region. A famine
            // onset takes its own one-slot scar store (it recurs, so it never crowds the rare
            // founding/war/battle ring); every other marking kind shares the 4-slot place ring.
            if (events[i].RegionId is int mrid)
            {
                if (events[i].Type == "famine")
                    _map.AddFamineScar(mrid, events[i].Year, events[i].Id);
                else if (ClassifyMark(events[i]) is MapView.MarkKind mk)
                    _map.AddPlaceMark(mrid, mk, events[i].Year, events[i].Id);
            }
            // Life memory: a cairn-worthy life raises a memorial cairn at the home of its line
            // (Event.HomeRegionId) — remembered there, never a claim of where it happened.
            if (events[i].HomeRegionId is int hrid && IsCairnWorthy(events[i]))
                _map.AddHomeMark(hrid, events[i].Year, events[i].Id);
            // Turning points: the authored pivot classifier marks the map ONLY where the
            // event truly stands (its site cell, else its land's heart) — placeless pivots
            // never pin. Consequences aren't known at stream time, so far-reaching pivots
            // surface later through the thread header, honestly.
            if (Replay.TurningPointKind(_world, events[i], 0) is not null)
            {
                if (events[i].SiteId is int tsid)
                {
                    var ts = _world.Sites.Get(tsid);
                    _map.AddTurningMark(ts.Nx, ts.Ny, events[i].Id, events[i].Year);
                }
                else if (events[i].RegionId is int trid)
                {
                    var tr = _world.Regions[trid];
                    _map.AddTurningMark(tr.X, tr.Y, events[i].Id, events[i].Year);
                }
            }
        }

        for (int i = _lastEventCount; i < events.Count; i++)
        {
            var e = events[i];
            // Grow a followed bloodline as its descendants are born — O(new events), the same
            // viral-at-birth trick the curse uses. Avoids re-expanding the whole pedigree per tick.
            if (_marked.Count > 0 && e.Type == "birth" && e.Participants.Any(_marked.Contains))
                foreach (var pid in e.Participants) _marked.Add(pid);

            bool personYours = PersonYours(e);
            bool yours = personYours || RegionYours(e);
            // Feel-test tuning: a followed land's rooted plain births/deaths are memory,
            // not drama — half boost and no dramatic beat, or a populous watched land
            // throttles high-speed playback with every life rooted there. Murders and
            // events touching followed PEOPLE keep their full weight.
            bool quietRegionLife = yours && !personYours && e.Type is "birth" or "death";
            if (yours) _castDirty = true;   // a YOURS event can change the cast (births, deaths, successions)
            int imp = Scoring.ImportanceFast(e, _world, _consCount);
            if (yours) imp += quietRegionLife ? YoursBoost / 2 : YoursBoost;
            // An omen is attention made honest: tales truly anchored in the marked land
            // surface louder while it hangs. A weight, never a mechanical effect.
            if (omens is not null && e.RegionId is int erid && omens.Contains(erid)) imp += OmenBoost;
            // The guard trigger runs before the chattiness gate: a follow is an explicit ask,
            // so a followed soul's fate registers even when the feed is quiet. Introductions
            // run there too — meeting someone shouldn't depend on the chattiness slider.
            // Replayed history (the resume fast-forward) is memory, not the present: it
            // feeds every index above but never cards, introduces, pulses, or remembers.
            if (!_catchingUp)
            {
                MaybeArmGuard(e, yours, imp);
                MaybeIntroduce(e);   // own early-outs; a watched seat's heir isn't YOURS by participants
            }
            if (imp < threshold) continue;
            if (_catchingUp && i < events.Count - CatchupFeedRows) continue;   // only recent history earns rows

            // A specifically watched soul in the tale earns the row a gold side rule and
            // flares their map halo — only when they truly are a participant.
            bool soul = false;
            if (_followedSouls.Count > 0)
                foreach (var pid in e.Participants)
                    if (_followedSouls.Contains(pid)) { soul = true; break; }

            var row = AddFeedRow(e, imp, yours, soul);
            // Last-seen memory records only what was actually shown (this row, or a guard
            // card — see ShowGuardCard), so "you last saw…" never cites an undisplayed event.
            // Catch-up rows restore the feed's recent window without rewriting that memory.
            if (yours && row is not null && !_catchingUp) RememberSeen(e);
            if (soul && row is not null && !_catchingUp)
                foreach (var pid in e.Participants)
                    if (_followedSouls.Contains(pid)) _map.PulseSoul(pid);
            // Yours always gets the spotlight (quiet region-life excepted); otherwise a high
            // importance bar catches divine/war/founding and ignores routine births/deaths.
            if (row is not null && !_catchingUp && ((yours && !quietRegionLife) || imp >= NotableBar))
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
        "battle" => MapView.MarkKind.Battle,
        // Famine is handled apart (AddFamineScar — its own one-slot store, RegionId never SiteId);
        // famine_end and boom don't scar — the parched ground is the memory, recovery doesn't mark.
        _ => null,
    };

    private static void PulseFeedRow(Control row)
    {
        row.Modulate = new Color(1.7f, 1.55f, 0.7f);   // warm flash, fades back to white
        row.CreateTween()
           .TweenProperty(row, "modulate", Colors.White, 0.9f)
           .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }

    private bool FollowingAnything()
        => _followedSouls.Count > 0 || _seedPeople.Count > 0
        || _markedFactions.Count > 0 || _followedRegions.Count > 0;

    // Does this event touch a marked bloodline or a marked people? Inline + O(participants),
    // so it stays off the heavier Feed.BuildFeed path while keeping the live feed O(living).
    private bool PersonYours(Event e)
    {
        foreach (var pid in e.Participants)
        {
            if (_marked.Contains(pid) || _followedSouls.Contains(pid)) return true;
            if (_markedFactions.Count > 0 && _world.People.TryGetValue(pid, out var p)
                && _markedFactions.Contains(p.FactionId)) return true;
        }
        return false;
    }

    private bool IsYours(Event e) => PersonYours(e) || RegionYours(e);

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

        // The cast mark: the first followed participant's sigil rides the row, so a known
        // soul is recognizable in the stream without reading the name. O(participants),
        // and only when anything is followed.
        if (_followedSouls.Count > 0 || _marked.Count > 0)
            foreach (var pid in e.Participants)
                if (_followedSouls.Contains(pid) || _marked.Contains(pid))
                {
                    var sig = PersonSigils.Of(_world, pid);
                    var mark = new Label
                    {
                        Text = sig.Glyph,
                        VerticalAlignment = VerticalAlignment.Center,
                        MouseFilter = Control.MouseFilterEnum.Ignore,
                        SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                    };
                    mark.AddThemeFontSizeOverride("font_size", 13);
                    mark.AddThemeColorOverride("font_color", sig.Tint);
                    hb.AddChild(mark);
                    break;
                }

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
            // While you're focused on follows, the world compresses to one line per tale
            // unless it's genuinely loud — quieter weather, same honesty.
            MaxLinesVisible = !yours && FollowingAnything() && imp < NotableBar ? 1 : 2,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        text.AddThemeFontSizeOverride("font_size", 13);
        text.AddThemeColorOverride("font_color", Ui.Ink);
        body.AddChild(text);

        // While focused, the world's quiet rows recede a step — they stay readable but never
        // compete with your story. Loud rows (NotableBar+) keep full presence; only notable
        // rows ever get the pulse tween, so the dim is never overwritten.
        if (!yours && FollowingAnything() && imp < NotableBar)
            row.Modulate = new Color(1, 1, 1, 0.78f);

        return row;
    }

    private Control? AddFeedRow(Event e, int imp, bool yours, bool soul = false)
    {
        // Each channel keeps its own rolling window — your story can never flood the
        // world's, nor be drowned by it. Newest wins within a channel (feed semantics);
        // the old weakest-YOURS displacement died with the shared window.
        var row = BuildFeedRowControl(e, imp, yours, soul);
        var list = yours ? _yoursList : _feedList;
        var vis = yours ? _yoursVis : _feedVis;
        int window = yours ? YoursWindow : FeedWindow;
        list.AddChild(row);
        list.MoveChild(row, 0);   // newest on top
        vis.Insert(0, new FeedVisRow { Node = row, Yours = yours, Weight = imp });
        while (vis.Count > window)
        {
            var oldest = vis[vis.Count - 1];
            oldest.Node.QueueFree();
            vis.RemoveAt(vis.Count - 1);
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
        if (memorial)
        {
            // The one earned ceremony: the world dims and the card takes the center.
            _catchupPanel.Visible = false;   // a reading sheet never sits under the veil
            _guardBackdrop.Visible = true;
            _guardPanel.Visible = true;
        }
        else
        {
            // Every other guard moment pauses but stays out of the way: a compact toast.
            _guardBackdrop.Visible = false;
            ShowGuardToast(e, focusPid);
        }
        RememberSeen(e);   // the toast/card itself is a sighting
    }

    // The Watch Mode guard voice: why you care first, then the tale (with honest place
    // language), then the verbs. The map stays the stage — a true place pulses under it.
    private void ShowGuardToast(Event e, int? focusPid)
    {
        var cls = Ui.ClassOf(e.Type);
        bool yours = focusPid is not null || _guardIsDeath || RegionYours(e);
        string why = _guardIsDeath ? "a tale of a bloodline you follow closes"
            : focusPid is not null ? "fate touches what you follow"
            : RegionYours(e) ? "fate touches a land you watch"
            : "a great deed marks the age";
        // Honest anchors only: "in {X}" for a true place; a home anchor is memory, said in
        // remembered-home language with its own warm tint; no anchor stays silent.
        string where = e.RegionId is int rid && _world.RegionName(rid) is string rn
            ? $"  [color=#{Ui.Hex(Ui.Faded)}]· in {rn}[/color]"
            : e.HomeRegionId is int hrid && _world.RegionName(hrid) is string hrn
            ? $"  [color=#8a5d12]· {(e.Type == "birth" ? "of a line rooted in" : "remembered in")} {hrn}[/color]"
            : "";
        _guardToastBody.Text =
            $"[color=#{Ui.Hex(Ui.Gold)}]{(yours ? "★" : "✦")} {why}[/color] [color=#{Ui.Hex(Ui.FadedSub)}]— the world waits[/color]\n"
            + $"[color=#{Ui.Hex(Ui.Faded)}]Yr {e.Year}[/color] [color=#{Ui.Hex(cls.Color)}]{cls.Glyph}[/color] [b]{e.Text}[/b]{where}";
        if (e.RegionId is int prid) _map.PulseRegion(prid);   // point the eye at the true place
        _threadTween?.Kill();
        _threadCard.Visible = false;   // the toast and an introduction never share the slot
        _guardToast.Visible = true;
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
                    sb.AppendLine($"[center][font_size=24]{PersonSigils.Bb(_world, p.Id)} [b]{p.Name}[/b][/font_size][/center]");
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
            else if (p.Alive)
            {
                // Mid-life framing: the recap shouldn't first arrive at the grave. A compact
                // "tale so far" — every line real sim state, one-shot scan on card open.
                sb.AppendLine();
                sb.AppendLine(SectionCap("Their tale so far"));
                sb.AppendLine($"{PersonSigils.Bb(_world, pid)} {p.Name} — age {p.Age(_world.Year)}, born Yr {p.BirthYear}"
                    + (p.IsLeader ? $" · leads {_world.Factions[p.FactionId].Name}"
                       : p.EverLeader ? " · once a leader" : ""));
                if (ReputationDisplay(p.Reputation) is (string srt, string src))
                    sb.AppendLine($"[color=#{src}]{srt}[/color]");
                if (p.Children.Count > 0) sb.AppendLine($"children: {p.Children.Count}");
                var sofar = _world.Chronicle.Events.Where(t => t.Id != e.Id && t.Participants.Contains(pid)).TakeLast(3).ToList();
                foreach (var t in sofar)
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

        BuildGuardToast(root);
    }

    // The compact guard toast: top-center, two lines + verbs, never covers the map's heart.
    private void BuildGuardToast(Control root)
    {
        _guardToast = new PanelContainer { Visible = false };
        root.AddChild(_guardToast);
        _guardToast.AnchorLeft = 0.5f; _guardToast.AnchorRight = 0.5f;
        _guardToast.AnchorTop = 0; _guardToast.AnchorBottom = 0;
        _guardToast.OffsetLeft = -270; _guardToast.OffsetRight = 270;
        _guardToast.OffsetTop = 10;
        var box = Ui.PanelBox(8);
        box.BorderColor = Ui.Gold;
        box.SetBorderWidthAll(2);
        _guardToast.AddThemeStyleboxOverride("panel", box);

        var margin = new MarginContainer();
        foreach (var s in new[] { "left", "right" }) margin.AddThemeConstantOverride($"margin_{s}", 12);
        foreach (var s in new[] { "top", "bottom" }) margin.AddThemeConstantOverride($"margin_{s}", 8);
        _guardToast.AddChild(margin);

        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 6);
        margin.AddChild(vb);

        _guardToastBody = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            CustomMinimumSize = new Vector2(512, 0),
        };
        _guardToastBody.AddThemeFontSizeOverride("normal_font_size", 13);
        _guardToastBody.AddThemeColorOverride("default_color", Ui.Ink);
        _guardToastBody.AddThemeFontOverride("bold_font", Ui.SerifBold);
        vb.AddChild(_guardToastBody);

        var btns = new HBoxContainer();
        btns.AddThemeConstantOverride("separation", 8);
        vb.AddChild(btns);
        var resume = new Button { Text = "▶ Resume" };
        Ui.StyleButton(resume, active: true);
        resume.Pressed += () => { _guardToast.Visible = false; _running = true; };
        btns.AddChild(resume);
        var open = new Button { Text = "the full tale", TooltipText = "Open the full guard card" };
        Ui.StyleButton(open);
        open.Pressed += () =>
        {
            _guardToast.Visible = false;
            _guardBackdrop.Visible = _guardWasMemorial;
            _guardPanel.Visible = true;
        };
        btns.AddChild(open);
        var thread = new Button { Text = "↳ how we got here", TooltipText = "Trace the causes behind this moment" };
        Ui.StyleButton(thread);
        thread.Pressed += () =>
        {
            _guardToast.Visible = false;
            if (_guardEventId >= 0) OpenCatchup(_guardEventId);
        };
        btns.AddChild(thread);
        var spacer = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        btns.AddChild(spacer);
        var close = new Button { Text = "✕", CustomMinimumSize = new Vector2(26, 26), TooltipText = "Close (stay paused)" };
        Ui.StyleButton(close);
        close.Pressed += () => _guardToast.Visible = false;
        btns.AddChild(close);
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
        _catchupPanel.Visible = false;   // one major reading surface at a time
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

    // The Guide — the one onboarding surface. A player-invoked reading card (Chronicle Mode,
    // so it may take the centre): how to watch, what every mark on the atlas means, and the
    // powers of the hand. Static content built once; legend glyphs/colours mirror MapView and
    // the binding tables in docs/VISUAL_STYLE.md, so it never drifts from what's drawn.
    private void BuildHelp(Control root)
    {
        _helpPanel = new Panel { Visible = false };
        root.AddChild(_helpPanel);
        _helpPanel.AnchorLeft = 0.5f; _helpPanel.AnchorRight = 0.5f;
        _helpPanel.AnchorTop = 0.5f; _helpPanel.AnchorBottom = 0.5f;
        _helpPanel.OffsetLeft = -330; _helpPanel.OffsetRight = 330;
        _helpPanel.OffsetTop = -262; _helpPanel.OffsetBottom = 262;
        _helpPanel.AddThemeStyleboxOverride("panel", Ui.PanelBox());

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        foreach (var s in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{s}", 16);
        _helpPanel.AddChild(margin);

        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 8);
        margin.AddChild(vb);

        var hb = new HBoxContainer();
        hb.AddThemeConstantOverride("separation", 8);
        vb.AddChild(hb);
        var titles = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        titles.AddThemeConstantOverride("separation", 0);
        hb.AddChild(titles);
        var title = new Label { Text = "The Watcher's Guide" };
        title.AddThemeFontOverride("font", Ui.SerifBold);
        title.AddThemeFontSizeOverride("font_size", 21);
        title.AddThemeColorOverride("font_color", Ui.InkDeep);
        titles.AddChild(title);
        var sub = new Label { Text = "How to watch an age unfold — and shape it" };
        sub.AddThemeFontSizeOverride("font_size", 12);
        sub.AddThemeColorOverride("font_color", Ui.FadedSub);
        titles.AddChild(sub);
        var close = new Button { Text = "✕", CustomMinimumSize = new Vector2(28, 28), TooltipText = "Close" };
        Ui.StyleButton(close);
        close.Pressed += () => _helpPanel.Visible = false;
        hb.AddChild(close);

        var rule = new HSeparator();
        rule.AddThemeStyleboxOverride("separator", new StyleBoxFlat { BgColor = Ui.RowBorder, ContentMarginTop = 1 });
        vb.AddChild(rule);

        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        vb.AddChild(scroll);
        var body = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(606, 0),
        };
        body.AddThemeColorOverride("default_color", Ui.Ink);
        body.AddThemeFontOverride("bold_font", Ui.SerifBold);
        scroll.AddChild(body);

        string faded = Ui.Hex(Ui.Faded);
        string gold = Ui.Hex(Ui.Gold);
        string cyan = "7fc8d8";
        string ember = Ui.Hex(Ui.Ember);
        string violet = "9b6dc8";
        var sb = new StringBuilder();
        sb.AppendLine($"[color=#{faded}]You are a watcher over a living age. It unfolds on its own; you may slow it, follow the lives that matter to you, and lay a god's hand upon it.[/color]");
        sb.AppendLine();

        sb.AppendLine(SectionCap("How to watch"));
        sb.AppendLine($"[b]Time[/b] — [b]▶/❚❚[/b] play & pause · the speed ladder [b]0.25×–16×[/b] (linger → ages) · [b]✦ drama[/b] slows the loud moments · [b]⛨ guard[/b] pauses when fate touches what you follow.");
        sb.AppendLine($"[b]The lens[/b] — scroll to zoom, drag to pan, [b]⤢[/b] resets the view · [b]✦ follow drama[/b] leans the lens toward where things happen.");
        sb.AppendLine($"[b]Inspect[/b] — click a land, a person (the dots), or a site to open its card. Underlined links jump between people, places, and peoples.");
        sb.AppendLine($"[b]Read[/b] — click any tale in the Saga feed to see [i]how it happened[/i]. [b]✦ ledger[/b] holds your acts; [b]❖ places[/b] holds every remembered place.");
        sb.AppendLine();

        sb.AppendLine(SectionCap("The marks upon the map"));
        sb.AppendLine($"[color=#{faded}]Dots are people standing in their land — leaders ringed gold, women's dots a shade lighter, a [color=#{ember}]cursed[/color] soul burning ember.[/color]");
        sb.AppendLine($"[b]⌑[/b]  a standing stone — a people's [b]founding seat[/b]");
        sb.AppendLine($"[color=#{ember}][b]⚔[/b][/color]  crossed swords — a [b]battle[/b] was fought here");
        sb.AppendLine($"[color=#{ember}][b]✕[/b][/color]  scorch & a snapped pole — land [b]seized in war[/b]");
        sb.AppendLine($"[b]∴[/b]  a scattered cairn — a land [b]abandoned[/b] when its people died out");
        sb.AppendLine($"[color=#b07a2e][b]▦[/b][/color]  cracked ochre earth — a [b]famine[/b] struck this land");
        sb.AppendLine($"[color=#{gold}][b]⊟[/b][/color]  stacked stones & a gold light — a [b]memorial cairn[/b], at the home of a remembered leader");
        sb.AppendLine($"[color=#{violet}][b]❧[/b][/color]  a violet ribbon — a [b]custom[/b] was born or faded here");
        sb.AppendLine($"[color=#{gold}][b]◆[/b][/color]  a gold diamond — a [b]turning point[/b]; click it to trace the chain");
        sb.AppendLine($"[color=#{faded}]Marks fade with the years — the rare ones endure; the land's hungers pass.[/color]");
        sb.AppendLine();

        sb.AppendLine(SectionCap("The rings of your hand"));
        sb.AppendLine($"[color=#{gold}][b]◌[/b][/color]  a breathing gold halo — a [b]soul[/b] you follow");
        sb.AppendLine($"[color=#{cyan}][b]◌[/b][/color]  a cyan ring — a [b]bloodline[/b] you follow (its kin, and the children to come)");
        sb.AppendLine($"[color=#{gold}][b]◌[/b][/color]  a quiet steady ring on a land — a [b]land[/b] you follow");
        sb.AppendLine($"[color=#f2e2b0][b]◌[/b][/color]  a pale-gold ring — a [b]blessed[/b] soul · [color=#{violet}][b]✶[/b][/color]  an [b]omen[/b] you laid upon a land");
        sb.AppendLine();

        sb.AppendLine(SectionCap("The powers of your hand"));
        sb.AppendLine($"[color=#{faded}]Open any card to act — the verbs live on the thing they touch:[/color]");
        sb.AppendLine($"• Click a [b]soul[/b] → [b]Bless[/b] or [b]Curse[/b] them, or [b]Follow[/b] them.");
        sb.AppendLine($"• Click a [b]people[/b] → [b]Protect[/b] or [b]Doom[/b] them.");
        sb.AppendLine($"• Click a [b]land[/b] → lay an [b]Omen[/b], [b]Seed a Forest[/b], [b]Call a Spring[/b] — or [b]Follow[/b] it.");
        sb.AppendLine($"[color=#{faded}]What you follow becomes [b]Your Story[/b] (pinned atop the feed); the guard can pause time when it turns. Every act is written in the Fate Ledger.[/color]");
        sb.AppendLine();

        sb.AppendLine(SectionCap("Your world"));
        sb.AppendLine($"[color=#{faded}]Your acts, follows, and progress save on their own — close and return to the same age. [b]✶ New World[/b] begins a fresh age (your written canon is kept).[/color]");

        body.Text = sb.ToString();

        var btns = new HBoxContainer();
        btns.AddThemeConstantOverride("separation", 8);
        vb.AddChild(btns);
        var got = new Button { Text = "▶ Begin watching" };
        Ui.StyleButton(got, active: true);
        got.Pressed += () => { _helpPanel.Visible = false; _running = true; };
        btns.AddChild(got);
    }

    // Open the Guide. A reading surface, so it pauses the age (Chronicle Mode) and clears any
    // other major reading panel — never sits under a guard card or memorial.
    private void ShowHelp()
    {
        _running = false;
        _glimpsePanel.Visible = false;
        _catchupPanel.Visible = false;
        _fateLedger.Visible = false;
        _places.Visible = false;
        if (_replayChain is not null) CloseReplay();
        _helpPanel.Visible = true;
        _helpPanel.MoveToFront();
    }

    // Begin a fresh age. Discards the player's world save (acts / follows / resume position)
    // for this seed so the deterministic world replays from year 0 again; the player's written
    // canon book is deliberately KEPT. A confirmation gates the reset — it throws away progress.
    private void ConfirmNewWorld()
    {
        _running = false;
        var dlg = new ConfirmationDialog
        {
            Title = "Begin a fresh age?",
            DialogText = "This discards your saved acts, follows, and progress for this world.\n"
                + "Your written canon (tellings, inscriptions, legends) is kept.\n\nStart over from the first year?",
            OkButtonText = "✶ New World",
            CancelButtonText = "Keep watching",
        };
        _root.AddChild(dlg);
        dlg.Confirmed += DoNewWorld;   // reloads the scene, freeing the dialog with it
        dlg.Canceled += dlg.QueueFree;
        dlg.PopupCentered();
    }

    private void DoNewWorld()
    {
        // Drop the world journal (acts + follows + resume year). The store funnels every write,
        // so disabling it here prevents the close-handler from re-saving the world we're leaving.
        string path = ProjectSettings.GlobalizePath($"user://world_seed{Seed}.json");
        try
        {
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
        catch (System.Exception ex) when (ex is System.IO.IOException or System.UnauthorizedAccessException)
        { GD.PushWarning($"new world: could not remove the save ({ex.GetType().Name})"); }
        _worldStore = null!;   // the close-handler guards on null; the reload rebuilds a fresh store
        GetTree().ReloadCurrentScene();
    }

    // -------------------------------------------------------------- inspectors

    private void OpenCatchup(int eventId)
    {
        _glimpsePanel.Visible = false;   // the glimpse z-orders above the catch-up card
        _fateLedger.Visible = false;     // one reading sheet at a time (panel economy)
        _places.Visible = false;
        // Retargeting the rail ends a retelling aimed at a different event — the map path
        // and the rail must never tell two stories at once.
        if (_replayChain is not null && _replayChain.FocalEventId != eventId) CloseReplay();
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
        // Quick beats stay a slim sheet; the full thread earns reading width (Chronicle Mode).
        _catchupPanel.OffsetLeft = _catchupQuick ? -408 : -628;
        _catchup.CustomMinimumSize = new Vector2(_catchupQuick ? 340 : 560, 0);

        // The annotated chain: same membership as Trace, in record order (causes always
        // precede effects), with every proven connector attached. Card-open one-shot.
        var ann = StoryGrammar.Annotate(_world, id);
        var target = _world.Chronicle.Get(id);
        // The chain shape (consequence rail included) — reuse the open retelling's when it
        // is aimed at this same event, else one card-open build.
        var chain = _replayChain?.FocalEventId == id ? _replayChain : Replay.ChainFor(_world, id);
        int? currentBeatEvent = _replayChain?.FocalEventId == id && _replayChain.Beats.Count > _replayBeat
            ? _replayChain.Beats[_replayBeat].EventId : null;

        var sb = new StringBuilder();
        sb.AppendLine($"[b]{target.Text}[/b]");
        // A pivot announces itself: the authored turning-point kind, then what truly
        // changed — the people named, their peoples, and the honest place.
        if (Replay.TurningPointKind(_world, target, _consCount.GetValueOrDefault(id)) is string tpKind)
        {
            sb.AppendLine($"[color=#8a5d12][b]✦ TURNING POINT — {StoryCopy.TurningPointLabel(tpKind)}[/b][/color]");
            var touched = new List<string>();
            foreach (int pid in target.Participants.Take(3))
                if (_world.People.TryGetValue(pid, out var tp))
                    touched.Add($"{PersonSigils.Bb(_world, pid)} {tp.Name}");
            var tfacs = new List<string>();
            foreach (int pid in target.Participants)
                if (_world.People.TryGetValue(pid, out var tp) && !tfacs.Contains(tp.FactionId))
                    tfacs.Add(tp.FactionId);
            if (touched.Count > 0)
                sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]touches: {string.Join(" · ", touched)}[/color]");
            if (tfacs.Count > 0)
                sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]peoples: {string.Join(" · ", tfacs.Select(f => Link("f:" + f, _world.Factions[f].Name)))}[/color]");
            if (StoryCopy.AnchorPhrase(_world, target) is string tplace)
                sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]{tplace}[/color]");
        }
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
            // The honest anchor: "at {site}" only for a true SiteId, "in {region}" for a
            // land, "remembered in" for home memory — never bare guesses.
            string where = StoryCopy.AnchorPhrase(_world, e) is string ap
                ? $"  [color=#{Ui.Hex(Ui.Faded)}]· {ap}[/color]" : "";
            // While a retelling runs, the rail marks the beat the map is standing on.
            string marker = currentBeatEvent == e.Id ? "[color=#8a5d12]► [/color]" : "";
            string line = $"{marker}{year}  {chip}  {body}{where}";
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

        // What grew from this: the bounded direct-consequence rail — real recorded edges
        // only, the connector naming each literal focal→consequence link.
        if (chain.TotalConsequences > 0)
        {
            sb.AppendLine();
            sb.AppendLine(SectionCap("What grew from this")
                + $" [color=#{Ui.Hex(Ui.Faded)}]({chain.TotalConsequences} recorded)[/color]");
            foreach (var cb in chain.Consequences)
            {
                var ce = _world.Chronicle.Get(cb.EventId);
                var ccls = Ui.ClassOf(ce.Type);
                string cwhere = StoryCopy.AnchorPhrase(_world, ce) is string cap2
                    ? $"  [color=#{Ui.Hex(Ui.Faded)}]· {cap2}[/color]" : "";
                sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]Yr {ce.Year}[/color] [color=#{Ui.Hex(ccls.Color)}]{ccls.Glyph}[/color] {Link("e:" + ce.Id, ce.Text)}{cwhere}");
            }
            if (chain.TotalConsequences > chain.Consequences.Count)
                sb.AppendLine($"[color=#{Ui.Hex(Ui.FadedSub)}]…and {chain.TotalConsequences - chain.Consequences.Count} more traced to this[/color]");
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
        RecordDivine(_world.PlantCurse(p));   // ledgered like every act of the hand
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
        SaveWorldStore();   // a follow is part of the player-shaped world — it persists
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
        CastChanged();
        OnPersonPicked(pid);
        SaveWorldStore();
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
        CastChanged();
        OnRegionPicked(rid);
        SaveWorldStore();
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
        CastChanged();
    }

    // A follow changed — the cast roster recomputes now, not next tick. Unfollowing
    // everything also retires the your-story rows: gold rows without a follow behind
    // them would sit as unlabeled chrome above the world channel forever.
    private void CastChanged()
    {
        _cast.Refresh(membershipDirty: true);
        _castDirty = false;
        if (!FollowingAnything())
        {
            foreach (var r in _yoursVis) r.Node.QueueFree();
            _yoursVis.Clear();
        }
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

    // The connection between a person and the player's follows, in priority order —
    // O(kin + held regions), real relations only, home language per the binding contract.
    private string? CareLine(Person p)
    {
        if (_marked.Contains(p.Id)) return "of the line you follow";
        if (_markedFactions.Contains(p.FactionId))
            return p.IsLeader
                ? $"leads {_world.Factions[p.FactionId].Name}, a people you follow"
                : $"of {_world.Factions[p.FactionId].Name}, a people you follow";
        if (p.SpouseId is int sp && _followedSouls.Contains(sp))
            return $"wed to {_world.People[sp].Name}, a soul you follow";
        foreach (int par in p.Parents)
            if (_followedSouls.Contains(par)) return $"child of {_world.People[par].Name}, a soul you follow";
        foreach (int ch in p.Children)
            if (_followedSouls.Contains(ch)) return $"parent of {_world.People[ch].Name}, a soul you follow";
        if (_followedRegions.Count > 0)
        {
            // Iterate the followed set in id order, not the faction's HashSet — which
            // region gets named must never vary with hash order between runs.
            foreach (int rid in _followedRegions.OrderBy(r => r))
                if (rid >= 0 && rid < _world.Regions.Count
                    && _world.Regions[rid].ControllingFactionId == p.FactionId)
                    return $"their people hold {_world.Regions[rid].Name}, a land you watch";
            if (p.HomeRegionId is int hr && _followedRegions.Contains(hr))
                return $"of a line rooted in {_world.Regions[hr].Name}, a land you watch";
        }
        return null;
    }

    private void OnPersonPicked(int id)
    {
        if (!_world.People.TryGetValue(id, out var p)) return;
        _selectedPersonId = id;
        _selectedFactionId = null;
        _map.SelectedFactionId = null;
        _map.SelectedRegionId = -1;
        _map.SelectedSiteId = -1;
        _glimpsePanel.Visible = false;
        _lensFactionBtn.Visible = false;
        _regionBtn.Visible = false;
        _dioramaBtn.Visible = false;
        _curseBtn.Visible = p.Alive && !p.Cursed;
        _blessBtn.Visible = p.Alive && !p.Blessed;
        _protectBtn.Visible = false;
        _doomBtn.Visible = false;
        _omenBtn.Visible = false;
        _forestBtn.Visible = false;
        _springBtn.Visible = false;
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

        _inspectorAccent.Color = FactionTint(p.FactionId);
        _inspectorTitle.Text = p.Name;
        _inspectorSub.Text = $"{(p.Sex == "f" ? "woman" : "man")} of {fac.Name}"
            + (p.IsLeader ? " · leader" : "");

        var sb = new StringBuilder();
        if (p.Cursed) sb.AppendLine($"[color=#{Ui.Hex(Ui.Ember)}][b]✳ CURSED[/b] — a god's mark lies on this bloodline[/color]\n");
        if (p.Blessed) sb.AppendLine($"[color=#8a5d12][b]✦ {StoryCopy.Hint("BLESSED", "blessed")}[/b] — fate leans kindly toward this soul[/color]\n");
        // Why you care, said first: the connection to what you follow, with their sigil —
        // every person card opens by answering "who is this to me?"
        if (soulFollowed)
            sb.AppendLine($"[color=#8a5d12][b]{PersonSigils.Bb(_world, id)} ★ you are watching this soul[/b][/color]\n");
        else if (CareLine(p) is string care)
            sb.AppendLine($"[color=#8a5d12][b]{PersonSigils.Bb(_world, id)} {care}[/b][/color]\n");
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
        _cast.SetCollapsed(true);   // panel economy: the inspector takes the column, the cast folds to sigils
        _inspectorPanel.Visible = true;
    }

    // Inspector cross-links: e:<event id> opens How We Got Here, r:<region id> the Region
    // Lens, f:<faction id> the faction inspector. The link targets are real ids the panels
    // already render from — no new lookups.
    private void OnInspectorLink(string link, bool fromGuardCard = false)
    {
        if (link.StartsWith("e:") && int.TryParse(link[2..], out var eid)) OpenCatchup(eid);
        else if (link.StartsWith("r:") && int.TryParse(link[2..], out var rid)) OnRegionPicked(rid);
        else if (link.StartsWith("s:") && int.TryParse(link[2..], out var sid)) OnSitePicked(sid);
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
    // The holder's cloth color (mirrors the atlas faction palette); wilderness wears stone grey.
    private static Color FactionTint(string? fid) => fid switch
    {
        "highland" => new Color("6b7a99"),
        "shore" => new Color("4f8f89"),
        "wood" => new Color("5d8a4e"),
        _ => Ui.Stone,
    };

    private void OnRegionPicked(int regionId)
    {
        if (regionId < 0 || regionId >= _world.Regions.Count) return;
        var region = _world.Regions[regionId];
        var holder = region.ControllingFactionId is string hid ? _world.Factions[hid] : null;

        _selectedPersonId = null;
        _selectedFactionId = null;
        _map.SelectedFactionId = null;
        _map.SelectedRegionId = regionId;
        _map.SelectedSiteId = -1;
        _glimpsePanel.Visible = false;
        _curseBtn.Visible = false;
        _blessBtn.Visible = false;
        _protectBtn.Visible = false;
        _doomBtn.Visible = false;
        _followBtn.Visible = false;
        _soulBtn.Visible = false;
        _omenBtn.Visible = !OmenActive(regionId);
        _forestBtn.Visible = true;
        _springBtn.Visible = true;
        _lensFactionId = holder?.Id;
        _lensFactionBtn.Visible = holder is not null;
        if (holder is not null) _lensFactionBtn.Text = $"⚑ Inspect {holder.Name}";
        bool landFollowed = _followedRegions.Contains(regionId);
        _regionBtn.Visible = true;
        _regionBtn.Text = landFollowed ? "★ Following this land — unfollow" : "☆ Follow this land";
        Ui.StyleButton(_regionBtn, landFollowed);
        _dioramaBtn.Visible = true;

        _inspectorAccent.Color = FactionTint(holder?.Id);
        _inspectorTitle.Text = region.Name;
        _inspectorSub.Text = $"{region.TerrainType} · {(holder is null ? "wilderness" : $"held by {holder.Name}")}";

        var sb = new StringBuilder();
        if (landFollowed) sb.AppendLine($"[color=#8a5d12][b]★ you are watching this land[/b][/color]\n");
        if (OmenActive(regionId))
            sb.AppendLine($"[color=#{Ui.Hex(Ui.Violet)}][b]✶ {StoryCopy.Hint("an omen hangs over this land", "omen")}[/b] — the eye of fate is turned here[/color]\n");
        sb.AppendLine(SectionCap("The land"));
        sb.AppendLine($"terrain: {region.TerrainType}");
        sb.AppendLine(holder is null
            ? "held by: no one — unclaimed wilderness"
            : $"held by: {Link("f:" + holder.Id, holder.Name)}");
        // Culture made visible where you stand (M7 surfaced): the holder's hardened ways.
        if (holder is not null && holder.CustomOriginEvent.Count > 0)
            sb.AppendLine($"ways of the holder: [color=#{Ui.Hex(Ui.Violet)}]{string.Join(", ", holder.CustomOriginEvent.Keys.OrderBy(c => c))}[/color]");
        sb.AppendLine();
        // Sites V1: the land's real places — deterministic, terrain-honest, clickable.
        var localSites = _world.Sites.ForRegion(regionId);
        sb.AppendLine(SectionCap("Places of this land"));
        if (localSites.Count == 0)
            sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]the sea claimed every cell of this land — no places stand[/color]");
        foreach (var s in localSites)
        {
            int stales = _regionActivity.SiteTotalFor(s.Id);
            sb.AppendLine($"{Link("s:" + s.Id, s.Name)} — {SiteIndex.TypeLabel(s.Type)}"
                + (s.IsSeat ? $"  [color=#{Ui.Hex(Ui.Faded)}]· the seat[/color]" : "")
                + (stales > 0 ? $"  [color=#8a5d12]· {stales} tale{(stales == 1 ? "" : "s")}[/color]" : ""));
        }
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
        // Harvest memory (Harvest Economy V1 payoff): the land's own hunger and plenty. The
        // current condition reads Region.InFamine/InBoom directly — sim ground truth, forced
        // false for wilderness, so it never lies. Famine/plenty fall on the LAND (RegionId);
        // those who starved are remembered at their homeland, not here — the channels never blur.
        sb.AppendLine(SectionCap("Harvest memory"));
        sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]{StoryCopy.TerrainHarvestCharacter(region.TerrainType)}[/color]");
        var conditionColor = region.InFamine ? Ui.Ochre : region.InBoom ? Ui.Moss : Ui.Faded;
        sb.AppendLine($"[color=#{Ui.Hex(conditionColor)}]{StoryCopy.HarvestConditionPhrase(region.InFamine, region.InBoom)}[/color]");
        var harvest = _regionActivity.RecentFor(regionId)
            .Select(id => _world.Chronicle.Get(id))
            .Where(e => e.Type is "famine" or "famine_end" or "boom")
            .ToList();
        for (int i = harvest.Count - 1; i >= 0; i--)   // newest first
        {
            var he = harvest[i];
            var hcls = Ui.ClassOf(he.Type);
            sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]Yr {he.Year}[/color] [color=#{Ui.Hex(hcls.Color)}]{hcls.Glyph}[/color] {Link("e:" + he.Id, he.Text)}");
        }
        sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]famine and plenty fall on the land itself — those who starved are remembered at their homeland, not here[/color]");
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
            if (e.Type is "famine" or "famine_end" or "boom") continue;   // shown under Harvest memory
            var cls = Ui.ClassOf(e.Type);
            // The site suffix appears ONLY for a true Event.SiteId — the convention table's
            // anchor, never an inference from the region.
            string atSite = e.SiteId is int esid
                ? $"  [color=#8a5d12]· at {Link("s:" + esid, _world.Sites.Get(esid).Name)}[/color]" : "";
            sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]Yr {e.Year}[/color] [color=#{Ui.Hex(cls.Color)}]{cls.Glyph}[/color] {Link("e:" + e.Id, e.Text)}{atSite}");
        }
        sb.AppendLine();
        sb.AppendLine(SectionCap("Not yet in the record"));
        sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]people are not yet site-anchored — the atlas scatters each people across their lands[/color]");
        sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]tales anchor to single places only where the record is sure — founding seats, war strongholds, sworn ways; everything else belongs to the land, said plainly[/color]");
        sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]much of history carries no place anchor at all — a followed land speaks only when tales are anchored here or lives are remembered here[/color]");

        _inspector.Text = sb.ToString();
        _cast.SetCollapsed(true);   // panel economy: the inspector takes the column, the cast folds to sigils
        _inspectorPanel.Visible = true;
    }

    // The Site Card (Sites V1): a small parchment card for one real place. Every line is
    // honest data — the site contract (name/type/cell/region), the region's live holder,
    // and the LAND's anchored tales clearly labeled as such (events do not yet anchor to
    // single places; that contract is deferred and said plainly). No population, no
    // buildings, no daily life — those are not modeled, so the card never claims them.
    private void OnSitePicked(int siteId)
    {
        if (siteId < 0 || siteId >= _world.Sites.All.Count) return;
        var site = _world.Sites.Get(siteId);
        var region = _world.Regions[site.RegionId];
        var holder = region.ControllingFactionId is string hid ? _world.Factions[hid] : null;

        _selectedPersonId = null;
        _selectedFactionId = null;
        _map.SelectedFactionId = null;
        _map.SelectedRegionId = site.RegionId;   // the land lights up; its places stay named
        _map.SelectedSiteId = siteId;
        _glimpsePanel.Visible = false;
        _curseBtn.Visible = false;
        _blessBtn.Visible = false;
        _protectBtn.Visible = false;
        _doomBtn.Visible = false;
        _omenBtn.Visible = false;
        _forestBtn.Visible = false;
        _springBtn.Visible = false;
        _followBtn.Visible = false;
        _soulBtn.Visible = false;
        _regionBtn.Visible = false;
        _dioramaBtn.Visible = true;   // site picks still scope to a region — offer its diorama
        _lensFactionId = null;
        _lensFactionBtn.Visible = false;

        _inspectorAccent.Color = FactionTint(region.ControllingFactionId);
        _inspectorTitle.Text = site.Name;
        _inspectorSub.Text = $"{SiteIndex.TypeLabel(site.Type)} · {region.Name}";

        var sb = new StringBuilder();
        if (_followedRegions.Contains(region.Id))
            sb.AppendLine($"[color=#8a5d12][b]★ a place of {region.Name}, a land you watch[/b][/color]\n");
        if (OmenActive(region.Id))
            sb.AppendLine($"[color=#{Ui.Hex(Ui.Violet)}][b]✶ an omen hangs over {region.Name}[/b] — and so over this place[/color]\n");
        sb.AppendLine(SectionCap("The place"));
        sb.AppendLine($"a {SiteIndex.TypeLabel(site.Type)}"
            + (site.IsSeat ? $" — the seat of {region.Name}" : ""));
        string ground = _world.Surface.TerrainAt(site.CellX, site.CellY).ToString().ToLowerInvariant();
        sb.AppendLine($"stands on {ground} ground in {Link("r:" + region.Id, region.Name)} ({region.TerrainType})");
        sb.AppendLine(holder is null
            ? "held by no one — this land is unclaimed wilderness"
            : $"held, with all {region.Name}, by {Link("f:" + holder.Id, holder.Name)}");
        sb.AppendLine();
        // The land's legend covers its places in V1 — legends attach to lands, said plainly.
        if (CanonBlock($"r:{region.Id}", CanonNoteType.PlaceLegend) is string legend)
        { sb.Append(legend); sb.AppendLine(); }
        else if (!_canon.ReadOnly)
        { sb.AppendLine(Link($"canon:legend:r:{region.Id}", $"✎ set a legend for {region.Name} and its places")); sb.AppendLine(); }
        // Site memory (anchoring conventions V1): the tales that TRULY belong to this one
        // place — Event.SiteId, assigned only by the authored convention table.
        int siteTotal = _regionActivity.SiteTotalFor(siteId);
        sb.AppendLine(SectionCap("Tales at this place")
            + (siteTotal > 0 ? $" [color=#{Ui.Hex(Ui.Faded)}]({siteTotal} recorded)[/color]" : ""));
        var kinds = _regionActivity.SiteKindsFor(siteId);
        if (kinds.Count > 0)
        {
            // "Known for" from real recorded counts only — never flavor.
            var known = kinds.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
                .Take(3).Select(kv => StoryCopy.KnownForPhrase(kv.Key, kv.Value));
            sb.AppendLine($"[color=#8a5d12]known for: {string.Join("; ", known)}[/color]");
        }
        var siteTales = _regionActivity.SiteRecentFor(siteId);
        if (siteTales.Count == 0)
            sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]no recorded tales here yet[/color]");
        for (int i = siteTales.Count - 1; i >= 0; i--)   // newest first
        {
            var e = _world.Chronicle.Get(siteTales[i]);
            var cls = Ui.ClassOf(e.Type);
            sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]Yr {e.Year}[/color] [color=#{Ui.Hex(cls.Color)}]{cls.Glyph}[/color] {Link("e:" + e.Id, e.Text)}");
        }
        sb.AppendLine();
        // The hand upon this land: region-targeted divine acts — said as the land's, since
        // omens and works aim at lands, not single places.
        var hand = _world.DivinePressures
            .Where(p => p.TargetType == "region" && int.TryParse(p.TargetId, out int prid) && prid == region.Id)
            .ToList();
        if (hand.Count > 0)
        {
            sb.AppendLine(SectionCap("The hand upon this land"));
            foreach (var pr in hand)
            {
                var ae = _world.Chronicle.Get(pr.SourceEventId);
                string glyph = pr.Kind == DivinePressureKind.Omen ? "✶" : pr.Kind == DivinePressureKind.ForestSeeded ? "✿" : "≈";
                sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]Yr {pr.StartYear}[/color] [color=#{Ui.Hex(Ui.Violet)}]{glyph}[/color] {Link("e:" + ae.Id, ae.Text)}");
            }
            sb.AppendLine();
        }
        // The wider land's record, clearly its own: anchored to the land, not this place.
        var regionTales = _regionActivity.RecentFor(region.Id)
            .Where(eid => _world.Chronicle.Get(eid).SiteId != siteId).ToList();
        if (regionTales.Count > 0)
        {
            sb.AppendLine(SectionCap($"Tales of {region.Name}"));
            sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]the land's wider record — anchored to {region.Name}, not to this single place[/color]");
            for (int i = regionTales.Count - 1; i >= 0; i--)   // newest first
            {
                var e = _world.Chronicle.Get(regionTales[i]);
                var cls = Ui.ClassOf(e.Type);
                sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]Yr {e.Year}[/color] [color=#{Ui.Hex(cls.Color)}]{cls.Glyph}[/color] {Link("e:" + e.Id, e.Text)}");
            }
            sb.AppendLine();
        }
        sb.AppendLine(SectionCap("Not yet in the record"));
        sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]no one dwells here by name — people are not yet site-anchored[/color]");
        sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]this place claims no population, buildings, or stores — its presence and its anchored tales are all the record holds[/color]");

        _inspector.Text = sb.ToString();
        _cast.SetCollapsed(true);   // panel economy: the inspector takes the column, the cast folds to sigils
        _inspectorPanel.Visible = true;
        _map.FocusSite(siteId);     // a place card is a "find it" ask — ease the lens onto it
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
        _map.SelectedSiteId = -1;
        _glimpsePanel.Visible = false;
        _lensFactionBtn.Visible = false;
        _regionBtn.Visible = false;
        _dioramaBtn.Visible = false;
        _curseBtn.Visible = false;
        _blessBtn.Visible = false;
        _omenBtn.Visible = false;
        _forestBtn.Visible = false;
        _springBtn.Visible = false;
        _soulBtn.Visible = false;
        _followBtn.Visible = true;
        _followBtn.Text = _markedFactions.Contains(fid) ? "★ Following — unfollow" : "☆ Follow this people";
        Ui.StyleButton(_followBtn, _markedFactions.Contains(fid));
        var fac = _world.Factions[fid];
        var members = _world.FactionMembers(fid);
        string leader = fac.LeaderId is int lid ? _world.People[lid].Name : "(none)";
        var dom = _world.DominantReligion(fid);
        _protectBtn.Visible = members.Count > 0 && fac.ProtectUntilYear <= _world.Year;
        _doomBtn.Visible = members.Count > 0 && fac.DoomUntilYear <= _world.Year;

        _inspectorAccent.Color = FactionTint(fac.Id);
        _inspectorTitle.Text = fac.Name;
        _inspectorSub.Text = $"{fac.Culture} culture · of {fac.Homeland}";

        var sb = new StringBuilder();
        if (fac.ProtectUntilYear > _world.Year)
            sb.AppendLine($"[color=#8a5d12][b]❧ {StoryCopy.Hint("UNDER PROTECTION", "protected")}[/b] — until Yr {fac.ProtectUntilYear}[/color]\n");
        if (fac.DoomUntilYear > _world.Year)
            sb.AppendLine($"[color=#{Ui.Hex(Ui.Ember)}][b]☄ {StoryCopy.Hint("UNDER A DOOM", "doomed")}[/b] — until Yr {fac.DoomUntilYear}[/color]\n");
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
        // Each held land names its seat — a real Sites V1 place, not a viewer hint.
        var lands = fac.ControlledRegions.Select(int.Parse).OrderBy(i => i)
            .Select(i => _world.Regions[i]).ToList();
        if (lands.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(SectionCap("Their lands"));
            foreach (var r in lands.Take(8))
            {
                var seat = _world.Sites.SeatOf(r.Id);
                sb.AppendLine($"{Link("r:" + r.Id, r.Name)} — {r.TerrainType}"
                    + (seat is not null ? $" · seat: {Link("s:" + seat.Id, seat.Name)}" : ""));
            }
            if (lands.Count > 8)
                sb.AppendLine($"[color=#{Ui.Hex(Ui.Faded)}]…and {lands.Count - 8} more[/color]");
        }
        sb.AppendLine();
        sb.AppendLine(SectionCap("Eldest among them"));
        foreach (var p in members.OrderByDescending(p => p.Age(_world.Year)).Take(8))
            sb.AppendLine($"{p.Name} — age {p.Age(_world.Year)}{(p.IsLeader ? $"  [color=#8a5d12]· leader[/color]" : "")}");

        _inspector.Text = sb.ToString();
        _cast.SetCollapsed(true);   // panel economy: the inspector takes the column, the cast folds to sigils
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
        bool following = FollowingAnything();
        // Channel headers exist only when there genuinely are two channels on screen.
        _yoursHeader.Visible = following && _yoursVis.Count > 0;
        _worldHeader.Visible = _yoursHeader.Visible;
        bool guardActive = _guardMode != GuardMode.Off && following;
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
        if (_running && _guardToast.Visible) _guardToast.Visible = false;   // the moment passed unread
        if (_wasRunning && !_running) SaveWorldStore();   // every settle into pause is a safe place to keep the world
        bool cardUp = _guardPanel.Visible || _catchupPanel.Visible || _recapPanel.Visible
            || _guardToast.Visible || _fateLedger.Visible
            || _pendingGuardEventId is not null || _canonPanel.IsOpen;
        _guardReturnBtn.Visible = _guardReturnable && !cardUp;
        // A queued recap shows on the transition INTO a pause (never over another card —
        // the focus guard always outranks it) and otherwise waits on its chip.
        if (_wasRunning && !_running && _queuedRecap is not null && !cardUp) ShowRecapCard();
        _wasRunning = _running;
        _recapChip.Visible = _queuedRecap is not null && !cardUp;
    }
}
