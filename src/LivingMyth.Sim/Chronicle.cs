using System.Text;

namespace LivingMyth.Sim;

/// <summary>
/// World memory and the readable chronicle. This is the heart of the no-AI bet: every
/// event is a real record (when, who, what kind, and crucially WHAT CAUSED IT — a list of
/// earlier event ids). Those cause links are what make catch-up possible. We build them in
/// from day one because retrofitting causality is a nightmare. The text is just flavor over
/// the top of the real record. The record is the truth.
/// </summary>
public sealed class Event
{
    public int Id { get; }
    public int Year { get; }
    public string Type { get; }
    public string Text { get; }
    public List<int> Participants { get; }   // person ids
    public List<int> Causes { get; }         // ids of earlier events that led to this one
    public List<string> Tags { get; }

    public Event(int id, int year, string etype, string text,
                 List<int>? participants = null, List<int>? causes = null, List<string>? tags = null)
    {
        Id = id;
        Year = year;
        Type = etype;
        Text = text;
        Participants = participants ?? new();
        Causes = causes ?? new();
        Tags = tags ?? new();
    }
}

public sealed class Chronicle
{
    public List<Event> Events { get; } = new();
    private int _nextId;

    /// <summary>Write one event into history and hand it back, so the caller can use this
    /// event's id as the cause of a follow-up event.</summary>
    public Event Record(int year, string etype, string text,
                        List<int>? participants = null, List<int>? causes = null, List<string>? tags = null)
    {
        var ev = new Event(_nextId, year, etype, text, participants, causes, tags);
        _nextId++;
        Events.Add(ev);
        return ev;
    }

    /// <summary>Turn the record into a readable yearly history.</summary>
    /// <summary>Event ids are assigned sequentially and never removed, so id == list index.
    /// This O(1) lookup avoids rebuilding an id->event dictionary in hot per-tick paths.</summary>
    public Event Get(int id) => Events[id];

    public string Render()
    {
        var sb = new StringBuilder();
        int? currentYear = null;
        foreach (var ev in Events)
        {
            if (ev.Year != currentYear)
            {
                currentYear = ev.Year;
                sb.Append('\n');
                sb.Append("Year ").Append(ev.Year).Append('\n');
            }
            sb.Append("  - ").Append(ev.Text).Append('\n');
        }
        return sb.ToString().Trim();
    }

    /// <summary>Walk an event's causes backward into a flat list (the raw material for
    /// catch-up). Returns the event plus everything that led to it, sorted by year.</summary>
    public List<Event> Trace(int eventId)
    {
        var byId = new Dictionary<int, Event>();
        foreach (var e in Events) byId[e.Id] = e;

        var seen = new List<Event>();
        var seenIds = new HashSet<int>();
        var stack = new Stack<int>();
        stack.Push(eventId);
        while (stack.Count > 0)
        {
            int eid = stack.Pop();
            if (!byId.TryGetValue(eid, out var ev) || seenIds.Contains(ev.Id))
                continue;
            seen.Add(ev);
            seenIds.Add(ev.Id);
            foreach (var c in ev.Causes) stack.Push(c);
        }
        seen.Sort((a, b) => a.Year.CompareTo(b.Year));
        return seen;
    }
}
