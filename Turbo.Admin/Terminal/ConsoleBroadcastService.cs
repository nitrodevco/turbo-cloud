using System;
using System.Collections.Generic;
using System.Linq;

namespace Turbo.Admin.Terminal;

public sealed class ConsoleBroadcastService : IConsoleBroadcaster
{
    private const int MAX_BUFFERED_LINES = 2000;

    private readonly object _gate = new();
    private readonly LinkedList<string> _buffer = new();

    public event Action<string>? LineWritten;

    public void Publish(string line)
    {
        lock (_gate)
        {
            _buffer.AddLast(line);

            if (_buffer.Count > MAX_BUFFERED_LINES)
                _buffer.RemoveFirst();
        }

        LineWritten?.Invoke(line);
    }

    public IReadOnlyList<string> GetHistory()
    {
        lock (_gate)
        {
            return [.. _buffer];
        }
    }
}
