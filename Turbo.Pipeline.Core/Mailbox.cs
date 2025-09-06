using System;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Turbo.Pipeline.Core;

public class Mailbox
{
    public readonly Channel<Func<Task>> Queue = Channel.CreateUnbounded<Func<Task>>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false }
    );

    public Mailbox()
    {
        _ = Task.Run(PumpAsync);
    }

    private async Task PumpAsync()
    {
        var r = Queue.Reader;
        await foreach (var job in r.ReadAllAsync().ConfigureAwait(false))
            await job().ConfigureAwait(false);
    }
}
