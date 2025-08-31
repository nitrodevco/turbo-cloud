using System;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Turbo.Events;

public class Mailbox
{
    public readonly Channel<Func<Task>> Queue;
    public readonly Task PumpTask;

    public Mailbox()
    {
        Queue = Channel.CreateUnbounded<Func<Task>>(
            new UnboundedChannelOptions { SingleReader = true }
        );
        PumpTask = Task.Run(PumpAsync);
    }

    private async Task PumpAsync()
    {
        var r = Queue.Reader;
        await foreach (var job in r.ReadAllAsync().ConfigureAwait(false))
            await job().ConfigureAwait(false);
    }
}
