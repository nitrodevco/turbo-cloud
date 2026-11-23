using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Turbo.Import;

/// <summary>
/// What an import did: the rows written to each table, and every row it left out or changed on
/// the way, grouped by why, with a few examples each. Nothing is left out silently: a row the
/// target cannot hold (its owner was deleted, its furniture is unknown) is counted here.
/// </summary>
public sealed class ImportReport
{
    private const int EXAMPLES = 5;

    private readonly Dictionary<string, long> _written = [with(StringComparer.Ordinal)];
    private readonly Dictionary<string, Tally> _notes = [with(StringComparer.Ordinal)];
    private readonly List<string> _order = [];

    public void Wrote(string table, long rows)
    {
        if (rows == 0)
            return;

        if (!_written.ContainsKey(table))
            _order.Add(table);

        _written[table] = _written.GetValueOrDefault(table) + rows;
    }

    /// <summary>
    /// Records that something was left out or changed. <paramref name="kind"/> groups them (one
    /// line in the summary); <paramref name="example"/> says which row, for the first few.
    /// </summary>
    public void Note(string kind, string example)
    {
        if (!_notes.TryGetValue(kind, out var note))
            _notes[kind] = note = new Tally();

        note.Count++;

        if (note.Examples.Count < EXAMPLES)
            note.Examples.Add(example);
    }

    public long WrittenTo(string table) => _written.GetValueOrDefault(table);

    public long NotesOf(string kind) => _notes.TryGetValue(kind, out var note) ? note.Count : 0;

    public override string ToString()
    {
        var text = new StringBuilder();

        text.AppendLine("Rows written:");

        foreach (var table in _order)
            text.AppendLine($"  {table, -40} {_written[table], 12:N0}");

        if (_notes.Count == 0)
        {
            text.AppendLine("Nothing was left out or changed.");

            return text.ToString();
        }

        text.AppendLine("Left out or changed:");

        foreach (var (kind, note) in _notes.OrderByDescending(x => x.Value.Count))
        {
            text.AppendLine($"  {note.Count, 10:N0}  {kind}");

            foreach (var example in note.Examples)
                text.AppendLine($"              e.g. {example}");
        }

        return text.ToString();
    }

    private sealed class Tally
    {
        public long Count;
        public readonly List<string> Examples = [];
    }
}
