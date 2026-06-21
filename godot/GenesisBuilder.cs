using Godot;
using LivingMyth.Sim;

// The Peoples Builder: the player authors THEIR people at genesis — name, ethos (which genuinely
// drives their customs and wars), faith, homeland, founding leader — then the world grows around
// them with generated rivals. A full-screen parchment overlay; on Begin it hands a GenesisSpec back
// to Main, which boots the world from it. Presentation only — the Sim consumes the spec, never this.
public partial class GenesisBuilder : Control
{
    // Begin(spec, seed): spec == null means "quick start" (the classic generated world).
    public System.Action<GenesisSpec?, int> OnBegin = null!;
    public int Seed = 7;

    private static readonly string[] Terrains = { "highland", "coast", "forest", "plains" };
    private static readonly string[] NameStyles = { "highland", "shore", "wood" };
    private static readonly string[] Axes = { "valor", "piety", "cunning", "harmony" };
    private static readonly System.Collections.Generic.Dictionary<string, string> AxisCustom = new()
    { ["valor"] = "warlike", ["piety"] = "devout", ["cunning"] = "scheming", ["harmony"] = "peaceable" };

    private LineEdit _name = null!, _faith = null!, _deity = null!, _leaderName = null!;
    private OptionButton _terrain = null!, _style = null!, _leaderSex = null!;
    private SpinBox _pop = null!, _leaderAge = null!, _seedSpin = null!;
    private readonly System.Collections.Generic.Dictionary<string, HSlider> _axis = new();
    private Label _ethosRead = null!;

    public override void _Ready()
    {
        // Dim the world behind, capture all input.
        AddChild(new ColorRect { Color = new Color(0.04f, 0.03f, 0.015f, 0.62f), MouseFilter = MouseFilterEnum.Stop });
        GetChild<ColorRect>(0).SetAnchorsPreset(LayoutPreset.FullRect);

        // Panel sized to the viewport (centered, 560 wide, full height minus a margin) so it never
        // overflows the window. Title + action row are pinned; the fields scroll between them.
        var panel = new PanelContainer();
        AddChild(panel);
        panel.AddThemeStyleboxOverride("panel", Ui.PanelBox(12));
        panel.AnchorLeft = 0.5f; panel.AnchorRight = 0.5f; panel.AnchorTop = 0; panel.AnchorBottom = 1;
        panel.GrowHorizontal = GrowDirection.Both;
        panel.OffsetTop = 24; panel.OffsetBottom = -24;
        panel.CustomMinimumSize = new Vector2(560, 0);

        var margin = new MarginContainer();
        foreach (var s in new[] { "left", "right" }) margin.AddThemeConstantOverride($"margin_{s}", 22);
        foreach (var s in new[] { "top", "bottom" }) margin.AddThemeConstantOverride($"margin_{s}", 16);
        panel.AddChild(margin);

        var outer = new VBoxContainer();
        outer.AddThemeConstantOverride("separation", 8);
        margin.AddChild(outer);

        outer.AddChild(Title("Forge a People"));
        outer.AddChild(Sub("Author the people whose tale you will follow. Their ethos shapes the customs they keep — and the quarrels they pick. The world grows rival peoples around them."));

        // Scrolling body: everything the player edits, so it stays reachable on any window height.
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        outer.AddChild(scroll);
        var vb = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        vb.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(vb);

        _name = Field(vb, "Their name", "the Emberkin");
        _terrain = Dropdown(vb, "Homeland", Terrains, 0);

        vb.AddChild(Cap("Ethos — who they are"));
        foreach (var ax in Axes) _axis[ax] = AxisSlider(vb, ax);
        _ethosRead = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _ethosRead.AddThemeColorOverride("font_color", Ui.Gold);
        vb.AddChild(_ethosRead);

        vb.AddChild(Cap("Faith & founders"));
        _faith = Field(vb, "Their faith", "the Ember Pact");
        _deity = Field(vb, "Their god", "the Kindler");
        _style = Dropdown(vb, "Naming style", NameStyles, 0);
        _pop = Spin(vb, "Starting souls", 8, 40, 18);
        _leaderName = Field(vb, "Their leader", "Varra");
        var lrow = new HBoxContainer { }; lrow.AddThemeConstantOverride("separation", 10);
        _leaderSex = new OptionButton(); _leaderSex.AddItem("woman"); _leaderSex.AddItem("man"); Ui.StyleButton(_leaderSex);
        lrow.AddChild(LabelFor("leads as a")); lrow.AddChild(_leaderSex);
        _leaderAge = new SpinBox { MinValue = 18, MaxValue = 90, Value = 40 };
        lrow.AddChild(LabelFor("aged")); lrow.AddChild(_leaderAge);
        vb.AddChild(lrow);

        vb.AddChild(Cap("Presets"));
        var presets = new HBoxContainer(); presets.AddThemeConstantOverride("separation", 8);
        presets.AddChild(Preset("⚔ Warlike", "the Ironborn", "highland", "highland", 0.85, 0.45, 0.5, 0.2, "the Iron Creed", "the Spear-Father", "Brann", false));
        presets.AddChild(Preset("☾ Devout", "the Hallowed", "forest", "wood", 0.35, 0.85, 0.3, 0.55, "the Quiet Light", "the Dawn Mother", "Sela", true));
        presets.AddChild(Preset("⚖ Cunning", "the Tidetraders", "coast", "shore", 0.4, 0.4, 0.85, 0.45, "the Salt Bargain", "the Deep Keeper", "Doran", false));
        presets.AddChild(Preset("❧ Peaceable", "the Greenfolk", "plains", "wood", 0.25, 0.5, 0.4, 0.85, "the Gentle Vow", "the Harvest Hearth", "Mara", true));
        vb.AddChild(presets);

        // Pinned action row — always visible regardless of scroll / window height.
        outer.AddChild(new HSeparator());
        var btns = new HBoxContainer(); btns.AddThemeConstantOverride("separation", 10);
        btns.AddChild(LabelFor("Seed"));
        _seedSpin = new SpinBox { MinValue = 0, MaxValue = 999999, Value = Seed }; btns.AddChild(_seedSpin);
        btns.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });   // spacer
        var quick = new Button { Text = "Quick start" }; Ui.StyleButton(quick);
        quick.Pressed += () => OnBegin?.Invoke(null, (int)_seedSpin.Value);
        btns.AddChild(quick);
        var begin = new Button { Text = "✦ Begin their tale" }; Ui.StyleButton(begin, active: true);
        begin.Pressed += () => OnBegin?.Invoke(BuildSpec(), (int)_seedSpin.Value);
        btns.AddChild(begin);
        outer.AddChild(btns);

        RefreshEthos();
    }

    private GenesisSpec BuildSpec()
    {
        var spec = new GenesisSpec
        {
            PeopleName = Trim(_name, "the People"),
            Homeland = $"their {Terrains[_terrain.Selected]} home",
            HomelandTerrain = Terrains[_terrain.Selected],
            NamingStyle = NameStyles[_style.Selected],
            StartPop = (int)_pop.Value,
            FaithName = Trim(_faith, null), FaithDeity = Trim(_deity, null),
        };
        foreach (var ax in Axes) spec.Axes[ax] = System.Math.Round(_axis[ax].Value, 2);
        spec.Founders.Add(new GenesisFounder
        {
            Name = Trim(_leaderName, "their eldest"),
            Sex = _leaderSex.Selected == 0 ? "f" : "m",
            Age = (int)_leaderAge.Value,
            Leader = true,
        });
        return spec;
    }

    private void RefreshEthos()
    {
        var traits = new System.Collections.Generic.List<string>();
        foreach (var ax in Axes) if (_axis[ax].Value >= 0.6) traits.Add(AxisCustom[ax]);
        _ethosRead.Text = traits.Count == 0
            ? "A people of no strong bent — they may drift either way."
            : "They will be a " + string.Join(", ", traits) + " people.";
    }

    // ---- tiny UI helpers (parchment-styled) ----
    private Label Title(string t)
    { var l = new Label { Text = t }; l.AddThemeFontOverride("font", Ui.SerifBold); l.AddThemeFontSizeOverride("font_size", 26); l.AddThemeColorOverride("font_color", Ui.InkDeep); return l; }
    private Label Sub(string t)
    { var l = new Label { Text = t, AutowrapMode = TextServer.AutowrapMode.WordSmart }; l.AddThemeColorOverride("font_color", Ui.FadedSub); l.AddThemeFontSizeOverride("font_size", 13); return l; }
    private Control Cap(string t) => Ui.SectionLabel(t, 12);
    private Label LabelFor(string t)
    { var l = new Label { Text = t, SizeFlagsVertical = SizeFlags.ShrinkCenter }; l.AddThemeColorOverride("font_color", Ui.Ink); return l; }

    private LineEdit Field(VBoxContainer vb, string label, string placeholder)
    {
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 10);
        var l = LabelFor(label); l.CustomMinimumSize = new Vector2(120, 0); row.AddChild(l);
        var e = new LineEdit { PlaceholderText = placeholder, Text = placeholder, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(e); vb.AddChild(row); return e;
    }
    private OptionButton Dropdown(VBoxContainer vb, string label, string[] items, int sel)
    {
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 10);
        var l = LabelFor(label); l.CustomMinimumSize = new Vector2(120, 0); row.AddChild(l);
        var o = new OptionButton(); foreach (var it in items) o.AddItem(it); o.Selected = sel; Ui.StyleButton(o);
        row.AddChild(o); vb.AddChild(row); return o;
    }
    private SpinBox Spin(VBoxContainer vb, string label, int lo, int hi, int val)
    {
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 10);
        var l = LabelFor(label); l.CustomMinimumSize = new Vector2(120, 0); row.AddChild(l);
        var s = new SpinBox { MinValue = lo, MaxValue = hi, Value = val }; row.AddChild(s); vb.AddChild(row); return s;
    }
    private HSlider AxisSlider(VBoxContainer vb, string axis)
    {
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 10);
        var l = LabelFor(axis); l.CustomMinimumSize = new Vector2(120, 0); row.AddChild(l);
        var s = new HSlider { MinValue = 0, MaxValue = 1, Step = 0.05, Value = 0.5, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        s.ValueChanged += _ => RefreshEthos();
        row.AddChild(s); vb.AddChild(row); return s;
    }

    private Button Preset(string text, string name, string terrain, string style,
                          double valor, double piety, double cunning, double harmony,
                          string faith, string deity, string leader, bool leaderFemale)
    {
        var b = new Button { Text = text }; Ui.StyleButton(b);
        b.Pressed += () =>
        {
            _name.Text = name;
            _terrain.Selected = System.Array.IndexOf(Terrains, terrain);
            _style.Selected = System.Array.IndexOf(NameStyles, style);
            _axis["valor"].Value = valor; _axis["piety"].Value = piety;
            _axis["cunning"].Value = cunning; _axis["harmony"].Value = harmony;
            _faith.Text = faith; _deity.Text = deity;
            _leaderName.Text = leader; _leaderSex.Selected = leaderFemale ? 0 : 1;
            RefreshEthos();
        };
        return b;
    }

    private static string Trim(LineEdit e, string? fallback)
    {
        var t = e.Text?.Trim() ?? "";
        return t.Length > 0 ? t : (fallback ?? "");
    }
}
