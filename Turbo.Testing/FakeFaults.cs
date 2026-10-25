using System;
using System.Collections.Generic;

namespace Turbo.Testing;

/// <summary>
/// The failures a test queued on a fake (<c>hotel.Wallet.FailNext()</c>): each call that would
/// change something takes the next one, changes nothing and keeps no receipt, as a write that
/// failed on a hotel does, so a retry under the same reference goes through.
/// </summary>
internal sealed class FakeFaults
{
    private readonly Queue<Exception?> _next = new();

    public void FailNext(int count, Exception? throws)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);

        for (var i = 0; i < count; i++)
            _next.Enqueue(throws);
    }

    /// <summary>True when this call is refused; throws when the test gave an exception.</summary>
    public bool Trip()
    {
        if (!_next.TryDequeue(out var throws))
            return false;

        return throws is null ? true : throw throws;
    }
}
