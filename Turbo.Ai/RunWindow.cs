using System;
using System.Collections.Generic;

namespace Turbo.Ai;

/// <summary>The runs of the last minute, oldest first.</summary>
internal sealed class RunWindow
{
    private static readonly TimeSpan WINDOW = TimeSpan.FromMinutes(1);

    private readonly LinkedList<(DateTimeOffset At, Guid Reservation)> _runs = new();

    public bool Allows(int limit, DateTimeOffset now)
    {
        while (_runs.First is { } oldest && now - oldest.Value.At >= WINDOW)
            _runs.RemoveFirst();

        return _runs.Count < limit;
    }

    public void Count(DateTimeOffset now, Guid reservation = default) =>
        _runs.AddLast((now, reservation));

    public void Undo(Guid reservation)
    {
        for (var node = _runs.First; node is not null; node = node.Next)
            if (node.Value.Reservation == reservation)
            {
                _runs.Remove(node);
                return;
            }
    }
}
