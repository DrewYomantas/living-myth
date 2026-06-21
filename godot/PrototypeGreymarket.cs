// NOTE (2026-06-16): Krita-inking the 3 new ground swatches (ground_grass/dirt/plaza) was tried
// and ABANDONED — the headless ground chain applied cleanly (added to GROUNDS in krita_paintover.py)
// but the result is IMPERCEPTIBLE in-engine: grounds are heavily C#-tinted at draw time and the
// gentle ground ink (opacity 80) washes out entirely. Before/after F5 shots are identical even at 3x
// crop (docs/visual_pass/northstar_v0/ink_check/). Originals left un-inked on purpose. To make ground
// grain read it would take a much stronger overlay or a tint-aware pass — not worth it now; the next
// static-polish lever is elsewhere (focal darkening / atmospheric depth). See next-build-sequencing.
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using LivingMyth.Sim;

// North Star v0 — "Greymarket, a market village by the water".
//
// A STANDALONE, additive prototype scene (res://PrototypeGreymarket.tscn). It is NOT wired into
// Main and references nothing of the production atlas/diorama flow. Its job is to PROVE the look
// of the North Star hero reference (Visual references/gpt-northstar-site-view-greymarket.png):
// a dense, hand-composed, warm golden-hour isometric market village that FILLS the frame, framed
// by parchment chronicle chrome, visibly INHABITED (a real crowd), with roads threading the town.
//
// HONESTY: the building/stall/crowd LAYOUT is an authored ("mocked") composition — the persistent
// PROTOTYPE ribbon says so. Only the NAMES (place, holder people, an inspectable soul + recent
// beats) are pulled honestly from a booted seed-7 world. Nothing here touches the sim; it is a
// pure read + custom draw. By construction it cannot move the verify baseline.
public partial class PrototypeGreymarket : Control
{
	private const int Seed = 7;
	private const int Years = 462;

	private World _world = null!;
	private int _regionId;
	private Faction? _holder;
	private Person? _hero;
	private string _placeName = "Greymarket";
	private Font _serif = null!, _sc = null!;
	private readonly Dictionary<string, Texture2D> _tex = new();

	// parchment / brass language (mirrors DioramaView + Ui)
	private static readonly Color Parchment = new("f2e5c2");
	private static readonly Color Ink = new("3a2c19");
	private static readonly Color InkSoft = new("6f5b3e");
	private static readonly Color Gold = new("c9973f");
	private static readonly Color Ember = new("b0432e");

	public override void _Ready()
	{
		bool shot = OS.GetEnvironment("LM_NS_SHOT") != "";
		if (shot) DisplayServer.WindowSetSize(new Vector2I(1600, 920));

		_serif = LoadFont("res://assets/fonts/Alegreya-VariableFont.ttf");
		_sc = LoadFont("res://assets/fonts/AlegreyaSC-Medium.ttf");
		LoadTextures();
		BootWorld();

		MouseFilter = MouseFilterEnum.Stop;

		// a warm parchment-blue sea, NOT black — the hero frame never shows a void
		var bg = new ColorRect { Color = new Color("2a3f49") };
		bg.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(bg);

		string mode = OS.GetEnvironment("LM_NS_MODE");
		if (mode == "") mode = "wide";

		var canvas = new GreymarketCanvas { View = this, Mode = mode };
		canvas.SetAnchorsPreset(LayoutPreset.FullRect);
		canvas.TextureFilter = TextureFilterEnum.Linear;
		canvas.TextureRepeat = TextureRepeatEnum.Enabled;   // continuous ground grain across many cells
		AddChild(canvas);

		BuildChrome(mode);
		BuildPost();

		if (shot) _ = SelfShot(OS.GetEnvironment("LM_NS_SHOT"));
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (e is InputEventKey { Pressed: true } k && k.Keycode == Key.Escape)
			GetTree().Quit();
	}

	// Boot a deterministic world purely for honest NAMES (place / holder / an inspectable soul +
	// their recent beats). Never ticked again, never written. Pick a real coastal/held region.
	private void BootWorld()
	{
		var (config, names) = DataLoader.Load();
		_world = new World(Seed, config, names);
		_world.SeedWorld();
		while (_world.Year < Years) _world.Tick();

		// prefer a held coastal/plains region with sites + a living holder, else most-built held
		int best = -1, bestScore = -1;
		foreach (var r in _world.Regions)
		{
			if (r.ControllingFactionId == null) continue;
			int sites = _world.Sites.ForRegion(r.Id).Count;
			int score = sites + (r.TerrainType is "coast" or "plains" ? 6 : 0);
			if (score > bestScore) { bestScore = score; best = r.Id; }
		}
		_regionId = best >= 0 ? best : 0;
		var reg = _world.Regions[_regionId];
		_holder = reg.ControllingFactionId != null && _world.Factions.TryGetValue(reg.ControllingFactionId, out var h) ? h : null;

		// a real market-village name if the region has one; else keep "Greymarket"
		var market = _world.Sites.ForRegion(_regionId).FirstOrDefault(s => s.Type == SiteType.MarketVillage)
					 ?? _world.Sites.ForRegion(_regionId).FirstOrDefault(s => s.IsSeat);
		if (market != null) _placeName = market.Name;

		// an inspectable living soul of the holder people — prefer the leader, else most-storied
		if (_holder != null)
		{
			var members = _world.FactionMembers(_holder.Id).Where(p => p.Alive).ToList();
			_hero = members.FirstOrDefault(p => p.Id == _holder.LeaderId)
					?? members.OrderByDescending(p => Math.Abs(p.Reputation)).FirstOrDefault()
					?? members.FirstOrDefault();
		}
	}

	public List<(string type, int year, string text)> HeroBeats()
	{
		if (_hero == null) return new();
		return _world.Chronicle.Events
			.Where(e => e.Participants.Contains(_hero.Id) || e.Tags.Contains($"by-{_hero.FactionId}"))
			.OrderByDescending(e => e.Year)
			.Take(2)
			.Select(e => (e.Type, e.Year, e.Text))
			.ToList();
	}

	public List<(string type, int year, string text)> RegionBeats(int n)
	{
		var siteIds = _world.Sites.ForRegion(_regionId).Select(s => s.Id).ToHashSet();
		return _world.Chronicle.Events
			.Where(e => e.RegionId == _regionId || (e.SiteId is int sid && siteIds.Contains(sid)))
			.OrderByDescending(e => e.Year).Take(n)
			.Select(e => (e.Type, e.Year, e.Text)).ToList();
	}

	public List<string> KnownPlaces() =>
		_world.Sites.ForRegion(_regionId).Take(6)
			.Select(s => $"{s.Name} — {SiteIndex.TypeLabel(s.Type)}").ToList();

	public World World => _world;
	public int RegionId => _regionId;
	public Faction? Holder => _holder;
	public Person? Hero => _hero;
	public string PlaceName => _placeName;
	public Font Serif => _serif;
	public Font Sc => _sc;
	public Texture2D? Tex(string key) => _tex.GetValueOrDefault(key);

	public Color HolderTint => _holder?.Id switch
	{
		"highland" => new Color("6b7a99"),
		"shore" => new Color("4f8f89"),
		"wood" => new Color("5d8a4e"),
		_ => new Color("b07a3a"),
	};

	private static Font LoadFont(string path) => ResourceLoader.Load<Font>(path);

	private void LoadTextures()
	{
		string dir = ProjectSettings.GlobalizePath("res://assets/diorama/");
		foreach (var f in System.IO.Directory.GetFiles(dir, "*.png"))
		{
			var img = Image.LoadFromFile(f);
			_tex[System.IO.Path.GetFileNameWithoutExtension(f)] = ImageTexture.CreateFromImage(img);
		}
	}

	// ---- chrome --------------------------------------------------------------------------------
	private StyleBoxFlat PanelStyle(float alpha = 0.96f, string bg = "f2e5c2")
	{
		var sb = new StyleBoxFlat
		{
			BgColor = new Color(bg, alpha),
			BorderColor = Gold,
			ShadowColor = new Color(0, 0, 0, 0.45f),
			ShadowSize = 12,
		};
		sb.SetBorderWidthAll(2);
		sb.SetCornerRadiusAll(7);
		sb.SetContentMarginAll(14);
		return sb;
	}

	private Label Lab(string text, Font font, int size, Color col, bool wrap = false, float wrapW = 0)
	{
		var l = new Label { Text = text };
		l.AddThemeFontOverride("font", font);
		l.AddThemeFontSizeOverride("font_size", size);
		l.AddThemeColorOverride("font_color", col);
		if (wrap) { l.AutowrapMode = TextServer.AutowrapMode.WordSmart; if (wrapW > 0) l.CustomMinimumSize = new Vector2(wrapW, 0); }
		return l;
	}

	private Control Rule(float alpha = 0.45f)
		=> new ColorRect { Color = new Color(Gold, alpha), CustomMinimumSize = new Vector2(0, 1) };

	private void BuildChrome(string mode)
	{
		var vp = GetViewportRect().Size;
		var reg = _world.Regions[_regionId];
		string holderName = _holder?.Name ?? "no people";

		// A) Year plate — top-left
		var plate = new PanelContainer { Position = new Vector2(22, 18) };
		plate.AddThemeStyleboxOverride("panel", PanelStyle());
		var pv = new VBoxContainer();
		pv.AddChild(Lab("LIVING MYTH", _sc, 13, Gold));
		pv.AddChild(Lab($"Year {_world.Year}", _serif, 32, Ink));
		pv.AddChild(Lab($"{_world.LivingCount} souls · {_world.Chronicle.Events.Count} tales", _serif, 14, InkSoft));
		plate.AddChild(pv);
		AddChild(plate);

		// B) Title cartouche — top-center
		var cart = new PanelContainer { Position = new Vector2(vp.X / 2 - 165, 18), CustomMinimumSize = new Vector2(330, 0) };
		var cartSb = PanelStyle(0.93f);
		cart.AddThemeStyleboxOverride("panel", cartSb);
		var cv = new VBoxContainer();
		cv.AddThemeConstantOverride("separation", 1);
		var t1 = Lab(_placeName, _serif, 30, Ink);
		t1.HorizontalAlignment = HorizontalAlignment.Center; t1.CustomMinimumSize = new Vector2(300, 0);
		cv.AddChild(t1);
		cv.AddChild(Rule(0.6f));
		var t2 = Lab($"market village · realm of {holderName}", _sc, 13, InkSoft);
		t2.HorizontalAlignment = HorizontalAlignment.Center; t2.CustomMinimumSize = new Vector2(300, 0);
		cv.AddChild(t2);
		cart.AddChild(cv);
		AddChild(cart);

		// C) Left gazetteer card — the hero touch: an inspectable real soul
		bool inspect = mode == "inspect";
		float gw = inspect ? 332 : 300;
		var card = new PanelContainer { Position = new Vector2(22, 116), CustomMinimumSize = new Vector2(gw, 0) };
		var cardSb = PanelStyle(inspect ? 0.99f : 0.97f);
		if (inspect) cardSb.BorderColor = new Color("e2b34e");
		card.AddThemeStyleboxOverride("panel", cardSb);
		var gv = new VBoxContainer();
		gv.AddThemeConstantOverride("separation", 6);
		gv.AddChild(Lab(_placeName, _serif, 24, Ink));
		gv.AddChild(Lab($"market village · {Cap(reg.TerrainType)} country", _sc, 12, InkSoft));
		gv.AddChild(Rule());

		var hr = new HBoxContainer(); hr.AddThemeConstantOverride("separation", 8);
		hr.AddChild(new ColorRect { Color = HolderTint, CustomMinimumSize = new Vector2(15, 15) });
		hr.AddChild(Lab($"Held by {holderName}", _serif, 15, Ink));
		gv.AddChild(hr);
		gv.AddChild(Lab(reg.InFamine ? "A hungry year — the harvest fails." : reg.InBoom ? "A fat year — the stalls overflow." : "A steady season of trade and pilgrims.", _serif, 14, reg.InFamine ? Ember : InkSoft, wrap: true, wrapW: gw - 30));
		gv.AddChild(Rule());

		gv.AddChild(Lab("KNOWN PLACES", _sc, 12, Gold));
		foreach (var p in KnownPlaces())
			gv.AddChild(Lab("·  " + p, _serif, 13, Ink));
		gv.AddChild(Rule());

		// the inspectable soul: drawn sigil glyph + name + a real recent beat + brass buttons
		if (_hero != null)
		{
			var soul = new HBoxContainer(); soul.AddThemeConstantOverride("separation", 10);
			soul.AddChild(new SigilGlyph { Tint = HolderTint, Seed = _hero.Id, CustomMinimumSize = new Vector2(inspect ? 52 : 42, inspect ? 52 : 42) });
			var scol = new VBoxContainer(); scol.AddThemeConstantOverride("separation", 0);
			scol.AddChild(Lab(_hero.Name, _serif, inspect ? 20 : 18, Ink));
			scol.AddChild(Lab(_hero.Id == _holder?.LeaderId ? $"leader of {holderName}" : $"of {holderName}", _sc, 11, InkSoft));
			soul.AddChild(scol);
			gv.AddChild(soul);
			var beats = HeroBeats();
			if (beats.Count > 0)
				gv.AddChild(Lab($"recently: {Trunc(beats[0].text, inspect ? 90 : 64)}", _serif, 13, InkSoft, wrap: true, wrapW: gw - 30));

			var btns = new HBoxContainer(); btns.AddThemeConstantOverride("separation", 8);
			btns.AddChild(Brass("Inspect"));
			btns.AddChild(Brass("Follow"));
			gv.AddChild(btns);
		}
		card.AddChild(gv);
		AddChild(card);

		// D) Right saga — narrow, secondary
		var saga = new PanelContainer { Position = new Vector2(vp.X - 300, 116), CustomMinimumSize = new Vector2(278, 0) };
		saga.AddThemeStyleboxOverride("panel", PanelStyle(0.95f));
		var sv = new VBoxContainer(); sv.AddThemeConstantOverride("separation", 7);
		sv.AddChild(Lab("THE SAGA — HERE", _sc, 13, Gold));
		sv.AddChild(Rule());
		var beatsR = RegionBeats(7);
		if (beatsR.Count == 0) sv.AddChild(Lab("No tales sung here yet.", _serif, 13, InkSoft));
		foreach (var (type, year, text) in beatsR)
		{
			var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8);
			row.AddChild(new ColorRect { Color = EventColor(type), CustomMinimumSize = new Vector2(10, 10) });
			var col = new VBoxContainer(); col.AddThemeConstantOverride("separation", 0);
			col.AddChild(Lab($"{type.ToUpperInvariant().Replace('_', ' ')} · yr {year}", _sc, 10, InkSoft));
			col.AddChild(Lab(Trunc(text, 52), _serif, 13, Ink, wrap: true, wrapW: 224));
			row.AddChild(col);
			sv.AddChild(row);
		}
		saga.AddChild(sv);
		AddChild(saga);

		// E) Faction legend — bottom-left
		var legend = new PanelContainer { Position = new Vector2(22, vp.Y - 128) };
		legend.AddThemeStyleboxOverride("panel", PanelStyle(0.9f));
		var lv = new VBoxContainer(); lv.AddThemeConstantOverride("separation", 3);
		foreach (var (name, c) in new (string, Color)[]
				 { ("Your lands", HolderTint), ("Allied", new Color("4f8f89")), ("Neutral", new Color("8a8a86")), ("Contested", Ember) })
		{
			var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8);
			row.AddChild(new ColorRect { Color = c, CustomMinimumSize = new Vector2(13, 13) });
			row.AddChild(Lab(name, _serif, 13, Ink));
			lv.AddChild(row);
		}
		legend.AddChild(lv);
		AddChild(legend);

		// F) Bronze-medallion verb bar — bottom-center (read-only / visual)
		var verbs = new VerbBar { View = this, CustomMinimumSize = new Vector2(470, 64) };
		verbs.Position = new Vector2(vp.X / 2 - 235, vp.Y - 84);
		AddChild(verbs);

		// G) Speed pips — bottom-right
		var pips = new PanelContainer { Position = new Vector2(vp.X - 300, vp.Y - 84) };
		var pipSb = PanelStyle(0.95f, "2a2117"); pipSb.BorderColor = Gold;
		pips.AddThemeStyleboxOverride("panel", pipSb);
		var ph = new HBoxContainer(); ph.AddThemeConstantOverride("separation", 10);
		ph.AddChild(Lab("▶", _serif, 15, new Color("c8b48a")));
		foreach (var s in new[] { "1x", "2x", "4x" })
			ph.AddChild(Lab(s, _sc, 13, s == "1x" ? Gold : new Color("8c7a52")));
		pips.AddChild(ph);
		AddChild(pips);

		// H) Persistent honesty ribbon — bottom-right edge
		var ribbon = new PanelContainer { Position = new Vector2(vp.X - 446, vp.Y - 34) };
		var rsb = new StyleBoxFlat { BgColor = new Color("2a2117", 0.9f), BorderColor = new Color("b07a3a") };
		rsb.SetBorderWidthAll(1); rsb.SetCornerRadiusAll(4); rsb.SetContentMarginAll(6);
		ribbon.AddThemeStyleboxOverride("panel", rsb);
		ribbon.AddChild(Lab("PROTOTYPE — illustrative composition, not sim truth", _sc, 11, new Color("d8b87a")));
		AddChild(ribbon);
	}

	private Button Brass(string text)
	{
		var b = new Button { Text = text };
		b.AddThemeFontOverride("font", _sc);
		b.AddThemeFontSizeOverride("font_size", 13);
		var face = new StyleBoxFlat { BgColor = new Color("4a3a22"), BorderColor = Gold };
		face.SetBorderWidthAll(1); face.SetCornerRadiusAll(5); face.SetContentMarginAll(8);
		var hover = (StyleBoxFlat)face.Duplicate(); hover.BgColor = new Color("5d4a2b");
		b.AddThemeStyleboxOverride("normal", face);
		b.AddThemeStyleboxOverride("hover", hover);
		b.AddThemeStyleboxOverride("pressed", face);
		b.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		foreach (var c in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
			b.AddThemeColorOverride(c, new Color("e7d4a8"));
		return b;
	}

	private void BuildPost()
	{
		var layer = new CanvasLayer { Layer = 12 };
		var rect = new ColorRect { Color = Colors.White, MouseFilter = MouseFilterEnum.Ignore };
		rect.SetAnchorsPreset(LayoutPreset.FullRect);
		var shader = GD.Load<Shader>("res://shaders/parchment_post.gdshader");
		rect.Material = new ShaderMaterial { Shader = shader };
		layer.AddChild(rect);
		AddChild(layer);
	}

	private async System.Threading.Tasks.Task SelfShot(string dir)
	{
		await ToSignal(GetTree().CreateTimer(0.9), SceneTreeTimer.SignalName.Timeout);
		var img = GetViewport().GetTexture().GetImage();
		System.IO.Directory.CreateDirectory(dir);
		string name = OS.GetEnvironment("LM_NS_NAME");
		img.SavePng(System.IO.Path.Combine(dir, (name != "" ? name : "ns_v0") + ".png"));
		GetTree().Quit();
	}

	private static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);
	private static string Trunc(string s, int n) => s.Length <= n ? s : s.Substring(0, n - 1).TrimEnd() + "…";

	public static Color EventColor(string type) => type switch
	{
		"birth" => new Color("6f9e54"),
		"death" or "murder" => new Color("8a8a86"),
		"war_declared" or "battle" or "peace" => new Color("b0432e"),
		"famine" or "famine_end" or "boom" => new Color("b8862e"),
		"plague" or "plague_end" => new Color("6d5694"),
		"founding" or "abandonment" => new Color("c9973f"),
		"prejudice" => new Color("9a5a3a"),
		_ => new Color("9a8b6a"),
	};
}

// A tiny deterministic per-soul sigil — a few struck strokes in a parchment medallion, echoing
// PersonSigils. Visual only.
public partial class SigilGlyph : Control
{
	public Color Tint;
	public int Seed;
	public override void _Draw()
	{
		var s = Size;
		var c = s * 0.5f;
		float r = Math.Min(s.X, s.Y) * 0.46f;
		DrawCircle(c, r, new Color("efe0ba"));
		DrawArc(c, r, 0, Mathf.Tau, 28, new Color("c9973f"), 2f, true);
		uint h = (uint)(Seed * 2654435761u);
		int strokes = 3 + (int)(h % 3);
		for (int i = 0; i < strokes; i++)
		{
			h = h * 1664525u + 1013904223u;
			float a0 = (h & 0xffff) / 65535f * Mathf.Tau;
			h = h * 1664525u + 1013904223u;
			float a1 = (h & 0xffff) / 65535f * Mathf.Tau;
			DrawLine(c + Vector2.Right.Rotated(a0) * r * 0.7f, c + Vector2.Right.Rotated(a1) * r * 0.7f, Tint.Darkened(0.1f), 2f);
		}
		DrawCircle(c, r * 0.16f, Tint);
	}
}

// The bronze-medallion verb bar from the reference: embossed circular medallions, active ones
// warm, future ones greyed. Read-only / visual.
public partial class VerbBar : Control
{
	public PrototypeGreymarket View = null!;
	public override void _Draw()
	{
		var active = new (string glyph, string label)[] { ("◉", "Inspect"), ("★", "Follow"), ("✖", "Curse"), ("✦", "Bless") };
		var future = new (string glyph, string label)[] { ("◇", "Prophecy"), ("✷", "Plague"), ("△", "Terrain") };
		float x = 6, gap = 8, d = 48;
		void Med(string glyph, string label, bool on)
		{
			var ctr = new Vector2(x + d / 2, d / 2 + 4);
			DrawCircle(ctr + new Vector2(0, 2), d / 2, new Color(0, 0, 0, 0.3f));
			DrawCircle(ctr, d / 2, on ? new Color("5a4322") : new Color("3a342c"));
			DrawArc(ctr, d / 2 - 1, 0, Mathf.Tau, 24, on ? new Color("d8a843") : new Color("5a5448"), 2f, true);
			DrawString(View.Serif, ctr + new Vector2(-9, 6), glyph, HorizontalAlignment.Left, -1, 19, on ? new Color("f4d98a") : new Color("7a7468"));
			DrawString(View.Sc, new Vector2(x, d + 16), label, HorizontalAlignment.Left, -1, 10, on ? new Color("e7d4a8") : new Color("8a8472"));
			x += d + gap;
		}
		foreach (var (g, l) in active) Med(g, l, true);
		x += 6;
		foreach (var (g, l) in future) Med(g, l, false);
	}
}

// The composition surface: an authored isometric market village drawn over a tilted ground plane.
// Everything but the place/person NAMES is a hand-placed mock — the ribbon says so. The layout is
// fixed (deterministic), built once on a ~22x22 working grid, then projected iso with golden-hour
// light, long SE contact shadows, atmospheric haze at the edges, and a warm radial market glow.
public partial class GreymarketCanvas : Control
{
	public PrototypeGreymarket View = null!;
	public string Mode = "wide";

	private const int GW = 22, GH = 22;
	private float _tw, _th, _ox, _oy;

	// golden-hour key: warm top-left, cool shadow bottom-right. Per-cell tint multiplier.
	private Color KeyLight(float gx, float gy)
	{
		float t = Mathf.Clamp((gx + gy) / (GW + GH), 0, 1);
		var warm = new Color(1.14f, 1.03f, 0.80f);
		var cool = new Color(0.82f, 0.84f, 0.92f);
		return warm.Lerp(cool, t);
	}

	private Vector2 Iso(float gx, float gy)
		=> new(_ox + (gx - gy) * _tw * 0.5f, _oy + (gx + gy) * _th * 0.5f);

	private static uint Hash(int a, int b, int seed = 1)
	{
		unchecked
		{
			uint h = 2166136261u;
			h = (h ^ (uint)seed) * 16777619u;
			h = (h ^ (uint)a) * 16777619u;
			h = (h ^ (uint)b) * 16777619u;
			h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
			return h;
		}
	}

	// 0 grass · 1 road · 2 water · 3 field · 4 shore-sand · 5 plaza
	private int[,] _terr = null!;
	private bool _built;
	private readonly List<(int gx, int gy)> _stalls = new();
	private readonly List<(float gx, float gy, int kind)> _buildings = new();
	private readonly List<(float gx, float gy)> _trees = new();
	private readonly List<(float gx, float gy, int variant)> _people = new();
	private readonly List<(float gx, float gy)> _fences = new();
	private float _seatX = 16f, _seatY = 8.5f;

	private void Build()
	{
		if (_built) return;
		_built = true;
		_terr = new int[GW, GH];

		// bay across the FRONT + a sand shore
		for (int y = 0; y < GH; y++)
			for (int x = 0; x < GW; x++)
			{
				float front = (x + y) / (float)(GW + GH);
				float wob = (Hash(x, y, 5) & 0xff) / 255f * 0.06f;
				if (front > 0.74f + wob) _terr[x, y] = 2;
				else if (front > 0.69f + wob) _terr[x, y] = 4;
			}
		// an inlet stream channelling inland from the bay along the EAST flank (kept clear of the
		// village clearing 7..16 / 8..16 so it never punches a dark water cell through the plaza)
		for (int i = 0; i < GH; i++)
		{
			int sy = GH - 1 - i;
			int sx = 18 - i / 4;
			if (sx >= 0 && sx < GW && sy >= 0 && sy < GH && _terr[sx, sy] == 0 && sy > GH * 0.55f
				&& !(sx >= 7 && sx <= 16 && sy >= 8 && sy <= 16)) _terr[sx, sy] = 2;
		}

		// ROAD SPINE: front corner → market plaza → shrine knoll, with branch lanes
		var spine = new (int x, int y)[] { (17, 19), (15, 16), (13, 13), (11, 11), (10, 9), (9, 6), (8, 3) };
		void Lane(int x0, int y0, int x1, int y1)
		{
			int steps = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
			for (int s = 0; s <= steps; s++)
			{
				int x = (int)Math.Round(Mathf.Lerp(x0, x1, s / (float)Math.Max(1, steps)));
				int y = (int)Math.Round(Mathf.Lerp(y0, y1, s / (float)Math.Max(1, steps)));
				if (x >= 0 && x < GW && y >= 0 && y < GH && _terr[x, y] != 2) _terr[x, y] = 1;
			}
		}
		for (int i = 0; i < spine.Length - 1; i++) Lane(spine[i].x, spine[i].y, spine[i + 1].x, spine[i + 1].y);
		Lane(11, 11, 6, 12);
		Lane(13, 13, 17, 13);
		Lane(10, 9, 6, 7);

		// FOCAL MARKET SQUARE — a generous OPEN plaza ~ (11,11). The brightest focal negative space;
		// nothing is placed in its interior — stalls + crowd RING it, the centre stays clear but for
		// the well/market-cross. A wider radius so it reads as a real square, not a gap.
		for (int y = 8; y <= 14; y++)
			for (int x = 8; x <= 14; x++)
				if (_terr[x, y] == 0 || _terr[x, y] == 1)
					if (Math.Abs(x - 11) + Math.Abs(y - 11) <= 4) _terr[x, y] = 5;

		// stalls frame the open square in ASYMMETRIC groups (not an even ring): a sparse west arc, a
		// denser EAST market knot (the focal cluster the eye reads first), and an open S/SE mouth that
		// invites the approach up the spine — composition flow, not a clock face. Centre stays clear.
		foreach (var (sx, sy) in new (int, int)[]
				 {
					 (8, 11), (9, 9),               // west arc — sparse
					 (11, 8), (13, 9),              // north arc
					 (14, 11), (14, 12), (13, 13),  // east knot — the dense market heart
					 (9, 13),                       // a lone SW stall; the S/SE mouth (11..12,14) stays open
				 })
			_stalls.Add((sx, sy));

		// BUILDINGS — rows that FRONT onto a street, NOT a circular pile. Each ROW lays dwellings
		// along a line, every house nudged perpendicular off the street so doors face it; rows are
		// pushed apart so lanes read between the blocks. kind: 0 house_a (warm timber, dominant),
		// 1 house_b (a few; warm-modulated at draw), 2 keep (the seat — placed exactly ONCE).
		bool Free(float gx, float gy)
		{
			int ix = (int)Math.Round(gx), iy = (int)Math.Round(gy);
			if (ix < 1 || ix >= GW - 1 || iy < 1 || iy >= GH - 1) return false;
			int t = _terr[ix, iy];
			return t == 0 || t == 1;   // grass or road-edge only; never plaza/water/sand/field
		}
		// a street-fronting terrace: n houses stepping along (dx,dy), offset (ox,oy) off the lane
		void Row(float x0, float y0, float dx, float dy, float ox, float oy, int n, int seed, int b2Every)
		{
			for (int i = 0; i < n; i++)
			{
				uint h = Hash(seed, i, 11);
				float jx = ((h & 0xff) / 255f - 0.5f) * 0.5f;
				float jy = (((h >> 8) & 0xff) / 255f - 0.5f) * 0.5f;
				float gx = x0 + dx * i + ox + jx, gy = y0 + dy * i + oy + jy;
				if (!Free(gx, gy)) continue;
				// 0 house_a (warm thatch, dominant) · 1 house_b (broad tile hall) · 3 house_c (tall
				// narrow slate towne-house) — three rooflines so a terrace never repeats one stamp
				int kind = (b2Every > 0 && i % b2Every == 0 && i > 0) ? 1 : ((h & 3) == 0 ? 3 : 0);
				_buildings.Add((gx, gy, kind));
			}
		}
		// west block — two terraces fronting the west lane, facing the plaza
		Row(6.0f, 11.0f, 1.0f, 0.15f, 0f, -0.9f, 4, 1, 3);
		Row(6.0f, 13.0f, 1.0f, 0.1f, 0f, 0.9f, 4, 2, 0);
		// east block — terraces fronting the east lane
		Row(14.5f, 10.0f, 1.0f, 0.2f, 0f, -0.9f, 4, 3, 3);
		Row(14.5f, 14.0f, 1.0f, 0.0f, 0f, 0.9f, 3, 4, 0);
		// south block toward the dock, fronting the spine
		Row(11.5f, 15.5f, 1.1f, 0.4f, -0.9f, 0f, 4, 5, 2);
		// north block up the spine toward the shrine road
		Row(8.0f, 7.0f, 1.0f, -0.3f, -0.9f, 0f, 3, 6, 0);
		// THE SEAT — a single keep on a slight rise NE of the plaza, fronting the east lane
		_seatX = 16.0f; _seatY = 8.5f;
		if (Free(_seatX, _seatY)) _buildings.Add((_seatX, _seatY, 2));

		// FIELDS QUARTER (NW flank) — proper furrowed plots, fenced. Mark plot cells + fence ring.
		for (int y = 4; y <= 7; y++)
			for (int x = 3; x <= 6; x++)
				if (_terr[x, y] == 0) _terr[x, y] = 3;
		for (int y = 4; y <= 7; y++) { _fences.Add((6.6f, y)); _fences.Add((2.6f, y)); }
		for (int x = 3; x <= 6; x++) { _fences.Add((x, 7.6f)); _fences.Add((x, 3.6f)); }

		// FOREST EDGE — a thick, overlapping band of woods framing the BACK and far flanks of the
		// village (the top/back of the iso frame, where gx+gy is small), thinning to a few clumps at
		// the side periphery. Dense + tightly jittered so the crowns OVERLAP into one canopy mass with
		// a real silhouette, not isolated toy puffs. The market clearing + fields stay open.
		for (int y = 0; y < GH; y++)
			for (int x = 0; x < GW; x++)
			{
				if (_terr[x, y] != 0) continue;                                  // grass only
				if (x >= 7 && x <= 16 && y >= 8 && y <= 16) continue;            // market clearing
				if (x >= 3 && x <= 6 && y >= 4 && y <= 7) continue;             // the fields quarter
				float edge = Math.Min(Math.Min(x, GW - 1 - x), Math.Min(y, GH - 1 - y));
				bool back = (x + y) <= 11;                                       // the far back band
				bool corner = (x < 6 && y < 8) || (x > 16 && y < 11) || (x > 18);
				bool periphery = edge < 2;
				if (!back && !corner && !periphery) continue;
				uint h = Hash(x, y, 7);
				float prob = back ? 0.93f : corner ? 0.86f : 0.66f;
				if ((h & 0xff) / 255f < prob)
				{
					float jx = (((h >> 8) & 0xff) / 255f - 0.5f) * 0.9f;
					float jy = (((h >> 16) & 0xff) / 255f - 0.5f) * 0.9f;
					_trees.Add((x + jx, y + jy));
				}
			}

		// PEOPLE — tiny folk implying a crowd. They RING the open plaza in an annulus (centre stays
		// clear), browsing the stalls; then a thinner scatter walks the streets, the shrine path,
		// the dock and the fields. Never a single filled central blob.
		void Crowd(float cx, float cy, float spread, int n, int seed)
		{
			for (int i = 0; i < n; i++)
			{
				uint h = Hash(seed, i, 23);
				float dx = ((h & 0xffff) / 65535f - 0.5f) * spread;
				float dy = (((h >> 8) & 0xffff) / 65535f - 0.5f) * spread;
				_people.Add((cx + dx, cy + dy, (int)((h >> 20) % 3)));
			}
		}
		// market ring: a thinner annulus of folk around the rim (clear centre)…
		for (int i = 0; i < 10; i++)
		{
			uint h = Hash(i, 0, 51);
			float ang = i / 10f * Mathf.Tau + ((h & 0xff) / 255f - 0.5f) * 0.4f;
			float rad = 2.6f + ((h >> 8) & 0xff) / 255f * 1.1f;     // annulus 2.6..3.7 — clear centre
			_people.Add((11 + Mathf.Cos(ang) * rad, 11 + Mathf.Sin(ang) * rad * 0.9f, (int)((h >> 16) % 3)));
		}
		// …and a denser BROWSING KNOT massed at the east market cluster, so the crowd weight matches
		// the stall weight and the focal heart reads as the busiest place (composition hierarchy).
		Crowd(13.4f, 11.6f, 2.4f, 6, 61);
		// a couple of folk crossing the open square (sparse, so it still reads open)
		Crowd(11, 11, 1.6f, 2, 7);
		// street-walkers along the spine + lanes
		Crowd(13.5f, 14.0f, 2.0f, 3, 2);
		Crowd(15.5f, 16.5f, 1.8f, 2, 3);
		Crowd(7.5f, 9.0f, 2.0f, 3, 4);
		// procession up the shrine knoll + a couple of field-workers + a dockhand
		Crowd(8.5f, 3.2f, 1.6f, 3, 5);
		Crowd(4.5f, 6.0f, 1.8f, 2, 6);
		Crowd(13.5f, 16.5f, 1.2f, 1, 8);
	}

	public override void _Ready() { Resized += QueueRedraw; }

	public override void _Draw()
	{
		Build();
		var size = Size;
		bool detail = Mode == "detail";
		// Fit the iso diamond to fill the frame. The diamond is GW+GH wide in iso-x units of
		// _tw/2 and (GW+GH)*_th/2 tall; size _tw so the wider of the two just overfills, then
		// centre the diamond's bbox in the frame (detail zooms onto the plaza ~11,11).
		float span = GW + GH;
		float fillW = size.X * (detail ? 2.0f : 1.46f);
		_tw = fillW * 2f / span;
		_th = _tw * 0.58f;
		// diamond centre in unprojected iso coords is ((GW-GH)/2,(GW+GH)/2)+? — origin so the
		// grid centre cell sits frame-centre, nudged up so the foreground bay sits low.
		float cgx = detail ? 11 : (GW - 1) / 2f;
		float cgy = detail ? 11 : (GH - 1) / 2f;
		_ox = size.X / 2f - (cgx - cgy) * _tw * 0.5f;
		_oy = size.Y * (detail ? 0.50f : 0.46f) - (cgx + cgy) * _th * 0.5f;

		DrawSeaHaze(size);
		DrawGround();
		DrawShoreFoam();
		DrawRoadInk();

		var draws = new List<(float sortY, Action draw)>();

		AddDockAndBoat(draws);
		AddBridge(draws);
		AddShrineKnoll(draws);

		var cartP = Iso(5.2f, 7.6f);
		draws.Add((cartP.Y, () => Billboard("field", 5.2f, 7.6f, 1.4f, KeyLight(5.2f, 7.6f))));

		foreach (var (gx, gy) in _trees)
		{
			float front = (gx + gy) / (float)(GW + GH);
			uint h = Hash((int)(gx * 4), (int)(gy * 4), 13);
			bool coni = (h & 1) == 0;
			int v = (int)((h >> 4) % 3) + 1;
			string key = (coni ? "tree_conifer_" : "tree_broadleaf_") + v;
			float scale = 2.3f + front * 1.7f;
			float haze = Mathf.Clamp(1f - front, 0f, 0.55f);
			var tint = KeyLight(gx, gy).Lerp(new Color("a9b29a"), haze * 0.7f);
			tint = front < 0.45f ? tint : tint.Darkened(0.06f);
			float fgx = gx, fgy = gy;
			var p = Iso(gx, gy);
			draws.Add((p.Y, () => Billboard(key, fgx, fgy, scale, tint)));
		}

		// soft ambient-occlusion pooled under the building blocks (drawn on the GROUND, behind the
		// billboards) so clusters sit in the earth instead of floating. One faint blob per house.
		foreach (var (gx, gy, kind) in _buildings)
		{
			var p = Iso(gx + 0.5f, gy + 0.5f);
			float u = _tw;
			DrawSetTransform(p + new Vector2(0, u * 0.04f), 0, new Vector2(1, 0.42f));
			DrawCircle(Vector2.Zero, u * (kind == 2 ? 0.6f : 0.42f), new Color(0.18f, 0.13f, 0.08f, 0.16f));
			DrawSetTransform(Vector2.Zero, 0, Vector2.One);
		}

		// WARM building tint: the house PNGs (esp. house_b's slate roof) read cold-grey, so multiply
		// toward thatch/timber on the lit side, a cooler shade on the shadow side, by golden key.
		foreach (var (gx, gy, kind) in _buildings)
		{
			float fgx = gx, fgy = gy; int fk = kind;
			uint h = Hash((int)(gx * 7), (int)(gy * 7), 17);
			bool smoke = (h & 3) != 0 && fk != 2;
			var p = Iso(gx, gy);
			// dwellings 1.7..2.0 tall; house_c (tall slate towne-house) reads taller; keep larger (2.8)
			float bscale = fk == 2 ? 2.8f
				: fk == 3 ? (2.1f + ((h >> 8) & 0xff) / 255f * 0.3f)
				: (1.7f + ((h >> 8) & 0xff) / 255f * 0.3f);
			string key = fk == 2 ? "keep" : (fk == 1 ? "house_b" : (fk == 3 ? "house_c" : "house_a"));
			// timber/thatch warmth; house_c's slate roof reads cold-grey, warm it a touch more
			var warm = fk == 2 ? new Color("ded2b8") : (fk == 3 ? new Color("ead0a6") : new Color("f0d2a2"));
			var tint = KeyLight(fgx, fgy) * warm;
			draws.Add((p.Y, () =>
			{
				Billboard(key, fgx, fgy, bscale, tint);
				if (smoke) DrawSmoke(fgx, fgy);
			}));
		}

		foreach (var (gx, gy) in _fences)
		{
			float fgx = gx, fgy = gy;
			var p = Iso(gx, gy);
			draws.Add((p.Y, () => DrawFence(fgx, fgy)));
		}

		AddWell(draws, 11, 11);
		int stallIdx = 0;
		foreach (var (sx, sy) in _stalls)
		{
			int fx = sx, fy = sy;
			string skey = (stallIdx++ % 2 == 0) ? "stall_a" : "stall_b";
			var p = Iso(sx, sy);
			draws.Add((p.Y, () => Billboard(skey, fx, fy, 1.7f, KeyLight(fx, fy))));
		}

		foreach (var (gx, gy) in new (float, float)[] { (9.5f, 10f), (12.5f, 12f) })
		{
			float fgx = gx, fgy = gy;
			var p = Iso(gx, gy);
			draws.Add((p.Y, () => Billboard("banner", fgx, fgy, 2.1f, View.HolderTint.Lerp(Colors.White, 0.25f))));
		}

		foreach (var (gx, gy, variant) in _people)
		{
			float fgx = gx, fgy = gy; int fv = variant;
			var p = Iso(gx, gy);
			draws.Add((p.Y, () => DrawFigure(fgx, fgy, fv)));
		}

		foreach (var d in draws.OrderBy(d => d.sortY)) d.draw();

		DrawMarketGlow(size);
		DrawEdgeHaze(size);

		if (Mode == "inspect" && View.Hero != null)
		{
			var heroP = Iso(11.6f, 11.2f);
			var pin = heroP + new Vector2(0, -28);
			var cardEdge = new Vector2(360, 478);
			// a clearer leader-line from the gazetteer card to the hero pin in the market
			DrawLine(cardEdge, pin, new Color("3a2c19", 0.7f), 2.2f);
			DrawCircle(cardEdge, 3.5f, new Color("3a2c19"));
			// a glowing callout pin (a small halo so it reads as "this is the one")
			for (int g = 4; g >= 1; g--) DrawCircle(pin, g * 6f, new Color(0.96f, 0.84f, 0.5f, 0.06f));
			DrawCircle(pin, 15, new Color("efe0ba", 0.96f));
			DrawArc(pin, 15, 0, Mathf.Tau, 24, new Color("d8a843"), 2.5f, true);
			DrawString(View.Serif, pin + new Vector2(-5, 6), View.Hero.Name.Substring(0, 1), HorizontalAlignment.Left, -1, 18, new Color("3a2c19"));
			// a downward tick from the pin to the figure's head
			DrawLine(pin + new Vector2(0, 15), heroP + new Vector2(0, -2), new Color("d8a843", 0.8f), 1.8f);
		}
	}

	private static readonly Vector2[] GroundUV = { new(0, 0), new(1, 0), new(1, 1), new(0, 1) };

	// 0 grass · 1 road · 2 water · 3 field · 4 shore-sand · 5 plaza
	private string GroundTex(int t) => t switch
	{
		2 => "ground_water",
		1 => "ground_dirt",
		3 => "ground_dirt",          // furrowed field — dirt grain, tinted gold below
		4 => "ground_coast",         // sand
		5 => "ground_plaza",         // trodden packed earth
		_ => "ground_grass",         // 0 grass — the dominant green country
	};

	// the per-cell zone TINT laid over the (near-neutral) grain texture. This is what paints the
	// broad terrain masses — meadow green, worn-dirt lanes, gold crop, packed plaza, pale sand.
	private Color GroundColor(int t) => t switch
	{
		0 => new Color("9fb56a"),    // meadow lift
		1 => new Color("b59264"),    // dirt lane
		2 => new Color("3a6b73"),
		3 => new Color("c2a84e"),    // gold crop
		4 => new Color("dcc596"),    // sand
		5 => new Color("c6a778"),    // plaza earth
		_ => new Color("9fb56a"),
	};

	private const float UvP = 5.0f;   // ground grain repeats every ~5 cells, not per cell (de-tiled)

	// golden-hour light + focal lift evaluated AT A GRID VERTEX, so adjacent cells share the exact
	// value at their shared corners. Drawing each cell with its four corner colours (gouraud) melts
	// the old per-cell lighting grid into one continuous painted surface.
	private Color VertLight(float vx, float vy)
	{
		var light = KeyLight(vx, vy);
		float dist = (Mathf.Abs(vx - 11) + Mathf.Abs(vy - 11)) / 18f;
		// a STRONGER focal swing than V1 — the market core lifts warm and bright, the periphery sinks
		// into shadow, so the eye lands on the square and the edges recede (composition hierarchy).
		float focal = Mathf.Clamp(0.23f - dist * 0.66f, -0.32f, 0.23f);
		uint h = Hash((int)(vx * 2), (int)(vy * 2), 3);
		float j = ((h & 0xff) / 255f - 0.5f) * 0.05f;
		return new Color(light.R * (1 + focal) + j, light.G * (1 + focal * 0.92f) + j, light.B * (1 + focal * 0.74f) + j);
	}

	// per-zone colour push over the grain texture (only where the texture isn't already the zone hue)
	private Color ZoneMul(int t) => t == 3 ? new Color(1.20f, 1.04f, 0.55f) : Colors.White;

	// how much of `zone` surrounds a grid VERTEX (0..1, eased). Drives the per-vertex alpha of a zone
	// overlay so its edges FEATHER into the meadow instead of cutting a hard tile line.
	private float ZoneCover(int vx, int vy, int zone)
	{
		int hit = 0;
		if (vx - 1 >= 0 && vy - 1 >= 0 && _terr[vx - 1, vy - 1] == zone) hit++;
		if (vx < GW && vy - 1 >= 0 && _terr[vx, vy - 1] == zone) hit++;
		if (vx - 1 >= 0 && vy < GH && _terr[vx - 1, vy] == zone) hit++;
		if (vx < GW && vy < GH && _terr[vx, vy] == zone) hit++;
		return Mathf.Pow(hit / 4f, 0.7f);
	}

	private Vector2[] CellUv(int gx, int gy) => new[]
	{
		new Vector2(gx / UvP, gy / UvP), new Vector2((gx + 1) / UvP, gy / UvP),
		new Vector2((gx + 1) / UvP, (gy + 1) / UvP), new Vector2(gx / UvP, (gy + 1) / UvP),
	};

	private Vector2[] CellPts(int gx, int gy) =>
		new[] { Iso(gx, gy), Iso(gx + 1, gy), Iso(gx + 1, gy + 1), Iso(gx, gy + 1) };

	private void DrawGround()
	{
		// 1) BASE MEADOW — every land cell painted with the continuous (de-tiled) grass grain,
		//    gouraud-lit at shared vertices: one unbroken green surface under the whole village.
		var grass = View.Tex("ground_grass");
		for (int gy = 0; gy < GH; gy++)
			for (int gx = 0; gx < GW; gx++)
			{
				if (_terr[gx, gy] == 2) continue;
				var pts = CellPts(gx, gy);
				var cols = new[] { VertLight(gx, gy), VertLight(gx + 1, gy), VertLight(gx + 1, gy + 1), VertLight(gx, gy + 1) };
				if (grass != null) DrawPolygon(pts, cols, CellUv(gx, gy), grass);
				else DrawColoredPolygon(pts, GroundColor(0) * cols[0]);
			}

		// 2) ZONE OVERLAYS — dirt lanes, gold fields, packed plaza, shore sand painted OVER the meadow
		//    with per-vertex alpha = ZoneCover, so every zone edge feathers into the grass instead of
		//    cutting a hard tile line. (Plaza centre stays solid; lane sides + field/beach edges fade.)
		foreach (int zone in new[] { 4, 3, 1, 5 })   // sand under, then field, lanes, plaza on top
		{
			var tex = View.Tex(GroundTex(zone));
			if (tex == null) continue;
			var zm = ZoneMul(zone);
			for (int gy = 0; gy < GH; gy++)
				for (int gx = 0; gx < GW; gx++)
				{
					if (_terr[gx, gy] != zone) continue;
					var pts = CellPts(gx, gy);
					Color C(int vx, int vy)
					{
						var c = VertLight(vx, vy) * zm;
						c.A = ZoneCover(vx, vy, zone);
						return c;
					}
					var cols = new[] { C(gx, gy), C(gx + 1, gy), C(gx + 1, gy + 1), C(gx, gy + 1) };
					DrawPolygon(pts, cols, CellUv(gx, gy), tex);
				}
		}

		// WORN-EARTH integration — soft trodden-dirt halos under the dwellings (cleared grass), so the
		// houses sit IN the ground instead of on a green carpet. Drawn before the water + foam.
		foreach (var (gx, gy, kind) in _buildings)
		{
			var p = Iso(gx + 0.5f, gy + 0.5f);
			float r = _tw * (kind == 2 ? 0.92f : 0.6f);
			for (int i = 3; i >= 1; i--)
			{
				DrawSetTransform(p, 0, new Vector2(1, 0.5f));
				DrawCircle(Vector2.Zero, r * i / 3f, new Color("8a6c40", 0.06f));
				DrawSetTransform(Vector2.Zero, 0, Vector2.One);
			}
		}

		// WATER PASS — its own grain texture + foam handled in DrawShoreFoam.
		for (int gy = 0; gy < GH; gy++)
			for (int gx = 0; gx < GW; gx++)
			{
				if (_terr[gx, gy] != 2) continue;
				var pts = new[] { Iso(gx, gy), Iso(gx + 1, gy), Iso(gx + 1, gy + 1), Iso(gx, gy + 1) };
				var mod = KeyLight(gx + 0.5f, gy + 0.5f);
				var wtex = View.Tex("ground_water");
				if (wtex != null) DrawColoredPolygon(pts, mod, GroundUV, wtex);
				else DrawColoredPolygon(pts, GroundColor(2) * mod);
			}
	}

	private static bool IsWater(int t) => t == 2;

	private void DrawShoreFoam()
	{
		for (int gy = 0; gy < GH; gy++)
			for (int gx = 0; gx < GW; gx++)
			{
				if (!IsWater(_terr[gx, gy])) continue;
				var c0 = Iso(gx, gy); var c1 = Iso(gx + 1, gy);
				var c2 = Iso(gx + 1, gy + 1); var c3 = Iso(gx, gy + 1);
				foreach (var (dx, dy, p, q) in new (int, int, Vector2, Vector2)[]
						 { (-1, 0, c0, c3), (1, 0, c1, c2), (0, -1, c0, c1), (0, 1, c3, c2) })
				{
					int nx = gx + dx, ny = gy + dy;
					if (nx >= 0 && nx < GW && ny >= 0 && ny < GH && !IsWater(_terr[nx, ny]))
						DrawLine(p, q, new Color("e8eede", 0.6f), 2.4f);
				}
			}
	}

	private void DrawRoadInk()
	{
		for (int gy = 0; gy < GH; gy++)
			for (int gx = 0; gx < GW; gx++)
			{
				if (_terr[gx, gy] != 1) continue;
				var a = Iso(gx + 0.5f, gy + 0.5f);
				foreach (var (dx, dy) in new (int, int)[] { (1, 0), (0, 1) })
				{
					int nx = gx + dx, ny = gy + dy;
					if (nx < GW && ny < GH && _terr[nx, ny] == 1)
					{
						var b = Iso(nx + 0.5f, ny + 0.5f);
						DrawLine(a, b, new Color("6a4f2a", 0.45f), _tw * 0.10f);
					}
				}
			}
	}

	private void DrawSeaHaze(Vector2 size)
	{
		DrawRect(new Rect2(0, 0, size.X, size.Y * 0.34f), new Color("3a4f57"));
		var mid = new Color("4a5b55");
		for (int i = 0; i < 16; i++)
			DrawRect(new Rect2(0, size.Y * 0.30f + i * 4, size.X, 4), new Color(mid.R, mid.G, mid.B, (1 - i / 16f) * 0.25f));
	}

	private void DrawMarketGlow(Vector2 size)
	{
		var c = Iso(11, 11);
		for (int i = 12; i >= 1; i--)
			DrawCircle(c, i * 30f, new Color(1f, 0.87f, 0.56f, 0.017f));
	}

	private void DrawEdgeHaze(Vector2 size)
	{
		var haze = new Color("d9c9a0");
		for (int i = 0; i < 10; i++)
		{
			float inset = i * 8f;
			DrawRect(new Rect2(inset, inset, size.X - inset * 2, size.Y - inset * 2), new Color(haze.R, haze.G, haze.B, (1 - i / 10f) * 0.05f), false, 8f);
		}
	}

	private void Billboard(string key, float gx, float gy, float tilesTall, Color mod)
	{
		var tex = View.Tex(key);
		if (tex == null) return;
		var p = Iso(gx + 0.5f, gy + 0.5f);
		float sz = tilesTall * _tw;
		var shadow = new Vector2(p.X + sz * 0.10f, p.Y - sz * 0.02f);
		DrawSetTransform(shadow, 0, new Vector2(1, 0.36f));
		DrawCircle(Vector2.Zero, sz * 0.26f, new Color(0, 0, 0, 0.22f));
		DrawSetTransform(Vector2.Zero, 0, Vector2.One);
		var rect = new Rect2(p.X - sz / 2f, p.Y - sz * 0.82f, sz, sz);
		DrawTextureRect(tex, rect, false, mod);
	}

	// billboard a tiny cloaked diorama figure (Blender asset), cloak re-tinted per variant so the
	// crowd reads as differently-robed folk — a stronger silhouette than the old drawn ball+rect.
	private void DrawFigure(float gx, float gy, int variant)
	{
		string key = variant == 2 ? "figure_staff" : "figure";
		// mods may exceed 1.0 — they LIFT the mid-tone tan cloak texture into a vivid robe while
		// the multiply keeps the baked ink outline/shading. Three robes for a lively, varied crowd.
		var cloak = variant switch
		{
			0 => new Color(1.85f, 1.05f, 0.52f),   // warm ochre robe
			1 => new Color(0.70f, 1.25f, 1.12f),   // cool teal traveller
			_ => new Color(1.70f, 0.70f, 0.48f),   // rust-red cloak
		};
		var tint = cloak * KeyLight(gx, gy);
		Billboard(key, gx, gy, 0.82f, tint);
	}

	private void DrawPerson(float gx, float gy, int variant)
	{
		var p = Iso(gx + 0.5f, gy + 0.5f);
		float u = _tw * 0.5f;
		float jitter = ((Hash((int)(gx * 9), (int)(gy * 9), 31) & 0xff) / 255f - 0.5f) * 0.5f;
		float s = u * (0.68f + jitter * 0.4f);   // tiny folk: ~1/4–1/5 a dwelling, with size variance
		DrawSetTransform(p + new Vector2(s * 0.16f, 0), 0, new Vector2(1, 0.4f));
		DrawCircle(Vector2.Zero, s * 0.34f, new Color(0, 0, 0, 0.24f));
		DrawSetTransform(Vector2.Zero, 0, Vector2.One);
		var cloth = variant switch
		{
			0 => View.HolderTint.Lerp(new Color("c96f3a"), 0.35f),
			1 => new Color("7a8f5a"),
			_ => new Color("b5733e"),
		};
		cloth = cloth * KeyLight(gx, gy);
		var skin = new Color("d8a878") * KeyLight(gx, gy);
		var feet = p;
		var body = feet + new Vector2(0, -s * 0.7f);
		DrawCircle(body, s * 0.34f, cloth);
		DrawRect(new Rect2(body.X - s * 0.30f, body.Y, s * 0.60f, s * 0.62f), cloth);
		DrawCircle(feet + new Vector2(0, -s * 1.18f), s * 0.20f, skin);
		if (variant == 2)
			DrawLine(feet + new Vector2(s * 0.28f, -s * 0.2f), feet + new Vector2(s * 0.28f, -s * 1.0f), new Color("6a4f2a"), s * 0.07f);
	}

	private void DrawStall(int gx, int gy)
	{
		var p = Iso(gx + 0.5f, gy + 0.5f);
		float u = _tw;
		DrawSetTransform(p + new Vector2(u * 0.1f, 0), 0, new Vector2(1, 0.4f));
		DrawCircle(Vector2.Zero, u * 0.5f, new Color(0, 0, 0, 0.2f));
		DrawSetTransform(Vector2.Zero, 0, Vector2.One);
		var light = KeyLight(gx, gy);
		float w = u * 0.62f, postH = u * 0.62f;
		var bl = p + new Vector2(-w / 2, 0);
		var br = p + new Vector2(w / 2, 0);
		DrawLine(bl, bl + new Vector2(0, -postH), new Color("6a4f2a"), u * 0.045f);
		DrawLine(br, br + new Vector2(0, -postH), new Color("6a4f2a"), u * 0.045f);
		// a flat striped awning panel (not a tall tent), faintly sloped toward the viewer
		uint hh = Hash(gx, gy, 41);
		var awnA = (((hh & 1) == 0) ? new Color("c4543a") : new Color("4f7a8a")) * light;
		var awnB = new Color("ecdcb0") * light;
		var top = p + new Vector2(0, -postH);
		var aTL = top + new Vector2(-w * 0.66f, -u * 0.04f); var aTR = top + new Vector2(w * 0.66f, -u * 0.04f);
		var aBL = top + new Vector2(-w * 0.5f, u * 0.12f); var aBR = top + new Vector2(w * 0.5f, u * 0.12f);
		var aMidT = (aTL + aTR) * 0.5f; var aMidB = (aBL + aBR) * 0.5f;
		DrawColoredPolygon(new[] { aTL, aMidT, aMidB, aBL }, awnA);
		DrawColoredPolygon(new[] { aMidT, aTR, aBR, aMidB }, awnB);
		// goods table under the awning
		DrawRect(new Rect2(p.X - w * 0.34f, p.Y - u * 0.24f, w * 0.68f, u * 0.22f), new Color("8a5a32") * light);
		DrawCircle(p + new Vector2(-w * 0.14f, -u * 0.2f), u * 0.05f, new Color("c97a3a"));
		DrawCircle(p + new Vector2(w * 0.12f, -u * 0.18f), u * 0.05f, new Color("9aa84a"));
	}

	private void DrawWellOrCross(float gx, float gy)
	{
		var p = Iso(gx + 0.5f, gy + 0.5f);
		float u = _tw;
		DrawSetTransform(p + new Vector2(u * 0.08f, 0), 0, new Vector2(1, 0.4f));
		DrawCircle(Vector2.Zero, u * 0.34f, new Color(0, 0, 0, 0.22f));
		DrawSetTransform(Vector2.Zero, 0, Vector2.One);
		var light = KeyLight(gx, gy);
		DrawCircle(p, u * 0.2f, new Color("8a857a") * light);
		DrawCircle(p, u * 0.13f, new Color("5a6a6e") * light);
		var l = p + new Vector2(-u * 0.18f, -u * 0.04f); var r = p + new Vector2(u * 0.18f, -u * 0.04f);
		DrawLine(l, l + new Vector2(0, -u * 0.4f), new Color("6a4f2a"), u * 0.04f);
		DrawLine(r, r + new Vector2(0, -u * 0.4f), new Color("6a4f2a"), u * 0.04f);
		var rt = p + new Vector2(0, -u * 0.56f);
		DrawColoredPolygon(new[] { l + new Vector2(0, -u * 0.36f), r + new Vector2(0, -u * 0.36f), rt }, new Color("9a5a36") * light);
	}

	private void AddWell(List<(float, Action)> draws, float gx, float gy)
	{
		var p = Iso(gx, gy);
		draws.Add((p.Y, () => DrawWellOrCross(gx, gy)));
	}

	private void DrawSmoke(float gx, float gy)
	{
		var p = Iso(gx + 0.5f, gy + 0.5f);
		float u = _tw;
		var top = p + new Vector2(u * 0.18f, -u * 1.5f);
		for (int i = 0; i < 4; i++)
		{
			float t = i / 4f;
			var c = top + new Vector2(Mathf.Sin(t * 6f) * u * 0.12f, -t * u * 0.5f);
			DrawCircle(c, u * (0.08f - t * 0.04f), new Color("e8e2d4", 0.32f - t * 0.06f));
		}
	}

	private void DrawFence(float gx, float gy)
	{
		var p = Iso(gx, gy);
		float u = _tw;
		var a = Iso(gx - 0.5f, gy); var b = Iso(gx + 0.5f, gy);
		DrawLine(a, b, new Color("7a5a32"), u * 0.05f);
		for (int k = 0; k <= 2; k++)
		{
			var pp = a.Lerp(b, k / 2f);
			DrawLine(pp, pp + new Vector2(0, -u * 0.26f), new Color("6a4f2a"), u * 0.04f);
		}
	}

	private void AddDockAndBoat(List<(float, Action)> draws)
	{
		// a warm drawn plank pier reaching into the bay (the dock PNG is too dark to tint warm)
		var dp = Iso(13.5f, 16.0f);
		draws.Add((dp.Y, () =>
		{
			var a = Iso(13.0f, 15.0f); var b = Iso(14.0f, 18.0f);
			var light = KeyLight(13.5f, 16.5f);
			DrawLine(a, b, new Color("a07a44") * light, _tw * 0.30f);
			DrawLine(a, b, new Color("c4a062") * light, _tw * 0.18f);
			for (int k = 0; k <= 6; k++)
			{
				var pp = a.Lerp(b, k / 6f);
				var perp = (b - a).Orthogonal().Normalized() * _tw * 0.18f;
				DrawLine(pp - perp, pp + perp, new Color("7a5a32"), 1.4f);
			}
			// a couple of pilings
			DrawLine(b, b + new Vector2(0, -_tw * 0.3f), new Color("6a5436"), _tw * 0.05f);
		}));
		var bp = Iso(15.4f, 18.2f);
		draws.Add((bp.Y, () =>
		{
			var p = Iso(15.4f, 18.2f);
			float u = _tw * 0.7f;
			var light = KeyLight(15, 18);
			var hull = new Color("8a6a3a") * light;
			DrawColoredPolygon(new[] { p + new Vector2(-u * 0.42f, 0), p + new Vector2(u * 0.42f, 0), p + new Vector2(u * 0.28f, u * 0.18f), p + new Vector2(-u * 0.28f, u * 0.18f) }, hull);
			DrawLine(p + new Vector2(-u * 0.42f, 0), p + new Vector2(u * 0.42f, 0), new Color("c9b487") * light, 2f);
			DrawLine(p, p + new Vector2(0, -u * 0.5f), new Color("6a5436"), u * 0.045f);
			DrawColoredPolygon(new[] { p + new Vector2(0, -u * 0.5f), p + new Vector2(u * 0.26f, -u * 0.16f), p + new Vector2(0, -u * 0.16f) }, new Color("efe2bf") * light);
		}));
	}

	private void AddBridge(List<(float, Action)> draws)
	{
		for (int gy = 8; gy < GH; gy++)
			for (int gx = 0; gx < GW; gx++)
			{
				if (_terr[gx, gy] != 1) continue;
				bool nearWater = false;
				foreach (var (dx, dy) in new (int, int)[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
				{
					int nx = gx + dx, ny = gy + dy;
					if (nx >= 0 && nx < GW && ny >= 0 && ny < GH && _terr[nx, ny] == 2) nearWater = true;
				}
				if (!nearWater) continue;
				int fx = gx, fy = gy;
				var p = Iso(gx, gy);
				draws.Add((p.Y + 1, () =>
				{
					var a = Iso(fx + 0.1f, fy + 0.5f); var b = Iso(fx + 0.9f, fy + 0.5f);
					var light = KeyLight(fx, fy);
					DrawLine(a, b, new Color("a87f48") * light, _tw * 0.24f);
					for (int k = 0; k <= 5; k++)
					{
						var pp = a.Lerp(b, k / 5f);
						DrawLine(pp + new Vector2(0, -_tw * 0.10f), pp + new Vector2(0, _tw * 0.10f), new Color("7a5a32"), 1.2f);
					}
				}));
				return;
			}
	}

	private void AddShrineKnoll(List<(float, Action)> draws)
	{
		var hp = Iso(8, 3.5f);
		draws.Add((hp.Y - 2, () => Billboard("hill", 8, 3.5f, 3.4f, KeyLight(8, 3.5f))));
		var sp = Iso(8, 2.6f);
		draws.Add((sp.Y, () => Billboard("standing_stones", 8, 2.6f, 2.2f, KeyLight(8, 2.6f))));
		var shp = Iso(9.2f, 3.2f);
		draws.Add((shp.Y, () => Billboard("shrine", 9.2f, 3.2f, 1.8f, KeyLight(9.2f, 3.2f))));
	}
}
