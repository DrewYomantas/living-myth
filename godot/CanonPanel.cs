using Godot;
using System;
using LivingMyth.Sim;

// The player's writing desk: a modal parchment editor for canon notes — tellings,
// chronicler's notes, inscriptions, place legends, what-the-people-say. Presentation
// only; it talks to the PlayerCanonStore and never to the sim. Opening pauses the world
// (writing is a held moment) and closing restores exactly the pace it took — unlike a
// guard interruption, this is the player's own side quest.
public sealed partial class CanonPanel : Control
{
    private const int MaxChars = 500;

    private PlayerCanonStore _store = null!;
    private Func<World> _world = null!;
    private Func<bool> _isRunning = null!;
    private Action<bool> _setRunning = null!;
    private Action<bool> _onClosed = null!;

    private Label _title = null!;
    private Label _context = null!;
    private TextEdit _text = null!;
    private Label _counter = null!;
    private Button _removeBtn = null!;

    private string _entityKey = "";
    private CanonNoteType _noteType;
    private bool _wasRunning;
    private bool _hadNote;

    public bool IsOpen => Visible;

    public void Setup(PlayerCanonStore store, Func<World> world,
                      Func<bool> isRunning, Action<bool> setRunning, Action<bool> onClosed)
    {
        _store = store;
        _world = world;
        _isRunning = isRunning;
        _setRunning = setRunning;
        _onClosed = onClosed;
        BuildUi();
    }

    private void BuildUi()
    {
        Visible = false;
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;   // children handle input; the veil swallows the rest

        // The same ink veil the memorial uses — the world waits while the player writes.
        var veil = new ColorRect
        {
            Color = Ui.InkDeep with { A = 0.42f },
            MouseFilter = MouseFilterEnum.Stop,
        };
        AddChild(veil);
        veil.SetAnchorsPreset(LayoutPreset.FullRect);

        var panel = new Panel();
        AddChild(panel);
        panel.AnchorLeft = 0.5f; panel.AnchorRight = 0.5f;
        panel.AnchorTop = 0.5f; panel.AnchorBottom = 0.5f;
        panel.OffsetLeft = -290; panel.OffsetRight = 290;
        panel.OffsetTop = -185; panel.OffsetBottom = 185;
        var box = Ui.PanelBox(12);
        box.BorderColor = Ui.Gold;
        box.SetBorderWidthAll(2);
        panel.AddThemeStyleboxOverride("panel", box);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        foreach (var s in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{s}", 16);
        panel.AddChild(margin);

        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 8);
        margin.AddChild(vb);

        vb.AddChild(Ui.SectionLabel("Your hand — kept apart from the record", 11));

        _title = new Label { Text = "" };
        _title.AddThemeFontOverride("font", Ui.SerifBold);
        _title.AddThemeFontSizeOverride("font_size", 20);
        _title.AddThemeColorOverride("font_color", Ui.InkDeep);
        vb.AddChild(_title);

        _context = new Label
        {
            Text = "",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MaxLinesVisible = 2,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
        };
        _context.AddThemeFontSizeOverride("font_size", 12);
        _context.AddThemeColorOverride("font_color", Ui.Faded);
        vb.AddChild(_context);

        _text = new TextEdit
        {
            WrapMode = TextEdit.LineWrappingMode.Boundary,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 130),
        };
        _text.AddThemeColorOverride("font_color", Ui.Ink);
        _text.AddThemeColorOverride("background_color", Ui.RowBg);   // parchment page, not editor chrome
        var page = Ui.RowBox(Ui.RowBg, Ui.RowBorder);
        _text.AddThemeStyleboxOverride("normal", page);
        _text.AddThemeStyleboxOverride("focus", Ui.RowBox(Ui.RowBg, Ui.Gold));
        _text.TextChanged += OnTextChanged;
        vb.AddChild(_text);

        _counter = new Label { Text = $"0 / {MaxChars}", HorizontalAlignment = HorizontalAlignment.Right };
        _counter.AddThemeFontSizeOverride("font_size", 11);
        _counter.AddThemeColorOverride("font_color", Ui.FadedSub);
        vb.AddChild(_counter);

        var btns = new HBoxContainer();
        btns.AddThemeConstantOverride("separation", 8);
        vb.AddChild(btns);
        var save = new Button { Text = "✎ Set it down" };
        Ui.StyleButton(save, active: true);
        save.Pressed += OnSave;
        btns.AddChild(save);
        _removeBtn = new Button { Text = "Unwrite it", TooltipText = "remove this telling from your canon" };
        Ui.StyleButton(_removeBtn);
        _removeBtn.Pressed += OnRemove;
        btns.AddChild(_removeBtn);
        var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        btns.AddChild(spacer);
        var cancel = new Button { Text = "Leave it" };
        Ui.StyleButton(cancel);
        cancel.Pressed += () => Close(changed: false);
        btns.AddChild(cancel);
    }

    public void Open(string entityKey, CanonNoteType type, string title, string contextLine)
    {
        _entityKey = entityKey;
        _noteType = type;
        _title.Text = title;
        _context.Text = contextLine;
        // A quarantined note (sim-build drift) belongs to a different telling of this id —
        // never preload its text under the new entity's title.
        var existing = _store.Get(entityKey, type);
        if (existing is not null && _store.StateOf(existing, _world()) != CanonNoteState.Active)
            existing = null;
        _hadNote = existing is not null;
        _text.Text = existing?.Text ?? "";
        _removeBtn.Visible = _hadNote;
        OnTextChanged();

        _wasRunning = _isRunning();
        _setRunning(false);
        Visible = true;
        _text.GrabFocus();
    }

    private void OnTextChanged()
    {
        if (_text.Text.Length > MaxChars)
        {
            // Hold the cap without fighting the caret: trim and park it at the end.
            // Never split a surrogate pair at the boundary.
            int cut = MaxChars;
            if (char.IsHighSurrogate(_text.Text[cut - 1])) cut--;
            _text.Text = _text.Text[..cut];
            _text.SetCaretLine(_text.GetLineCount() - 1);
            _text.SetCaretColumn(_text.GetLine(_text.GetLineCount() - 1).Length);
        }
        _counter.Text = $"{_text.Text.Length} / {MaxChars}";
    }

    private void OnSave()
    {
        // Empty text is an unwrite — the store deletes on whitespace by contract.
        bool changed = _hadNote || !string.IsNullOrWhiteSpace(_text.Text);
        if (changed)
        {
            _store.Upsert(_entityKey, _noteType, _text.Text, _world());
            _store.Save();
        }
        Close(changed);
    }

    private void OnRemove()
    {
        _store.Delete(_entityKey, _noteType);
        _store.Save();
        Close(changed: true);
    }

    private void Close(bool changed)
    {
        Visible = false;
        _setRunning(_wasRunning);
        _onClosed(changed);
    }
}
