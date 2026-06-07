using Godot;
using LivingMyth.Sim;

// M1 scaffold: proves Godot can drive the standalone sim with zero sim-side Godot
// dependency. The real viewer (map, live feed, inspection panels, god tools, catch-up)
// gets built on top of this. The sim stays a plain class library — no Godot leaks in.
public partial class Main : Node
{
    public override void _Ready()
    {
        var (config, names) = DataLoader.Load();
        var world = new World(seed: 12345, config, names);
        world.SeedWorld();
        for (int i = 0; i < 50; i++) world.Tick();

        GD.Print($"[Living Myth] sim wired OK — island of {world.Island}, " +
                 $"{world.Living().Count} living, {world.Chronicle.Events.Count} events after {world.Year} years.");
        GD.Print("The sim is a standalone class library with zero Godot dependency. M1 renders it.");
    }
}
