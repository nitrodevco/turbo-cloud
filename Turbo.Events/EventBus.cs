using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Events.Abstractions;
using Turbo.Events.Abstractions.Registry;
using Turbo.Events.Configuration;
using Turbo.Pipeline.Abstractions.Delegates;
using Turbo.Pipeline.Core;
using Turbo.Pipeline.Core.Envelope;
using Turbo.Primitives;

namespace Turbo.Events;

public class EventBus(
    Channel<EventEnvelope> ch,
    EventConfig cfg,
    IServiceProvider root,
    ILogger<EventBus> logger
) : EnvelopeBus<EventEnvelope, IEvent, EventContext, EventConfig>(ch, cfg, root, logger), IEventBus
{
    private readonly OrderedPerKeyDispatcher<string> _perTag = new(perKeyCapacity: 512);

    public override ValueTask EnqueueEnvelopeAsync(EventEnvelope env, CancellationToken ct)
    {
        var key = env.Tag ?? "";

        return _perTag.EnqueueAsync(
            key,
            async _ =>
            {
                await ProcessOne(env, ct).ConfigureAwait(false);
            },
            ct
        );
    }

    protected override EventEnvelope CreateEnvelope(
        IEvent interaction,
        object? args,
        string? tag,
        TaskCompletionSource? tcs
    )
    {
        return new EventEnvelope
        {
            Data = interaction!,
            Tag = tag ?? null,
            SyncTcs = tcs,
        };
    }

    protected override Type GetHandlerForType(Type interactionType)
    {
        return typeof(IEventHandler<>).MakeGenericType(interactionType);
    }

    protected override Type GetBehaviorForType(Type interactionType)
    {
        return typeof(IEventBehavior<>).MakeGenericType(interactionType);
    }

    protected override EventContext CreateContextForEnvelope(
        EventEnvelope envelope,
        IServiceProvider sp
    )
    {
        return new EventContext { Services = sp };
    }

    protected override async Task ExecuteEnvelopesSequentially(
        EventEnvelope env,
        EventContext ctx,
        object[]? handlers,
        HandlerInvoker invoker,
        CancellationToken ct
    )
    {
        if (
            env == null
            || ctx == null
            || ctx.IsAborted
            || handlers == null
            || handlers.Length == 0
            || invoker == null
        )
            return;

        foreach (var h in handlers)
        {
            try
            {
                await invoker(h, env.Data, ctx, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (_cfg.SwallowHandlerExceptions)
                    _cfg.OnError?.Invoke(env.Data, ex);
                else
                    throw;
            }
        }
    }

    protected override async Task ExecuteEnvelopesParallel(
        EventEnvelope env,
        EventContext ctx,
        object[]? handlers,
        HandlerInvoker invoker,
        CancellationToken ct
    )
    {
        if (
            env == null
            || ctx == null
            || ctx.IsAborted
            || handlers == null
            || handlers.Length == 0
            || invoker == null
        )
            return;

        await ParallelHelpers
            .RunBoundedAsync(
                handlers,
                _cfg.DegreeOfParallelism,
                h => invoker(h, env.Data, ctx, ct),
                ct
            )
            .ConfigureAwait(false);
    }
}
