using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using LivingMyth.Sim;

// The map: island + three peoples as readable colored regions, each living person a dot
// (leaders ringed gold, cursed tinted red). Placeholder art on purpose. Click a dot to
// inspect that person; click empty region to inspect the people. Pure rendering — it reads
// the sim, never mutates it.
public partial class MapView : Control
{
    public World? World;
    public HashSet<int>? Marked;            // followed bloodline — ringed cyan
    public Action<int>? PersonPicked;
    public Action<string>? FactionPicked;

    private readonly List<(Vector2 pos, float r, int id)> _dots = new();

    private static readonly Color Sea = new("0e2230");
    private static readonly Dictionary<string, Color> FactionColors = new()
    {
        ["highland"] = new Color("6b7a99"),
        ["shore"] = new Color("3aa6a0"),
        ["wood"] = new Color("5a9e57"),
    };

    public override void _Ready() => MouseFilter = MouseFilterEnum.Stop;

    private static float Frac(float v) => v - Mathf.Floor(v);

    public override void _Draw()
    {
        _dots.Clear();
        var size = Size;
        DrawRect(new Rect2(Vector2.Zero, size), Sea);
        var font = GetThemeDefaultFont();
        if (World is null || font is null) return;

        var facs = World.Config.Factions;
        int n = facs.Count;
        const float pad = 14f;
        float colW = (size.X - pad * (n + 1)) / n;

        for (int i = 0; i < n; i++)
        {
            var f = facs[i];
            var fac = World.Factions[f.Id];
            var col = FactionColors.GetValueOrDefault(f.Id, new Color("888888"));
            float x0 = pad + i * (colW + pad);
            var rect = new Rect2(x0, 44, colW, size.Y - 84);
            DrawRect(rect, col with { A = 0.16f });
            DrawRect(rect, col with { A = 0.55f }, false, 2f);

            var members = World.FactionMembers(f.Id);
            int pop = members.Count;
            string leader = fac.LeaderId is int lid ? World.People[lid].Name : "(none)";
            DrawString(font, new Vector2(x0 + 6, 30), $"{fac.Name}",
                HorizontalAlignment.Left, -1, 15, modulate: Colors.White);
            DrawString(font, new Vector2(x0 + 6, size.Y - 24), $"pop {pop}  ·  led by {leader}",
                HorizontalAlignment.Left, colW - 12, 12, modulate: new Color("c8d2dc"));

            foreach (var p in members)
            {
                float fx = Frac(p.Id * 0.61803398875f);
                float fy = Frac(p.Id * 0.75487766624f);
                var pos = new Vector2(rect.Position.X + 12 + fx * (rect.Size.X - 24),
                                      rect.Position.Y + 16 + fy * (rect.Size.Y - 36));
                float r = p.IsLeader ? 7f : 4f;
                var dot = p.Cursed ? new Color("d24a64") : (p.Sex == "f" ? col.Lightened(0.28f) : col);
                DrawCircle(pos, r, dot);
                if (p.IsLeader) DrawArc(pos, r + 2.5f, 0, Mathf.Tau, 20, new Color("ffd54a"), 1.6f);
                if (Marked is not null && Marked.Contains(p.Id))
                    DrawArc(pos, r + 4.5f, 0, Mathf.Tau, 24, new Color("5fd8ff"), 2f);
                _dots.Add((pos, Mathf.Max(r, 6f), p.Id));
            }
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton mb || !mb.Pressed || mb.ButtonIndex != MouseButton.Left)
            return;
        var pos = mb.Position;

        int bestId = -1;
        float bestD = float.MaxValue;
        foreach (var d in _dots)
        {
            float dist = pos.DistanceTo(d.pos);
            if (dist <= d.r + 3 && dist < bestD) { bestD = dist; bestId = d.id; }
        }
        if (bestId >= 0) { PersonPicked?.Invoke(bestId); return; }

        if (World is null) return;
        var facs = World.Config.Factions;
        int n = facs.Count;
        const float pad = 14f;
        float colW = (Size.X - pad * (n + 1)) / n;
        for (int i = 0; i < n; i++)
        {
            float x0 = pad + i * (colW + pad);
            if (pos.X >= x0 && pos.X <= x0 + colW) { FactionPicked?.Invoke(facs[i].Id); return; }
        }
    }
}
