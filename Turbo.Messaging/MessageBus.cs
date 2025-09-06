using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Messaging.Abstractions;
using Turbo.Messaging.Abstractions.Registry;
using Turbo.Messaging.Configuration;
using Turbo.Networking.Abstractions.Session;
using Turbo.Pipeline.Abstractions.Delegates;
using Turbo.Pipeline.Core;
using Turbo.Pipeline.Core.Envelope;
using Turbo.Primitives;

namespace Turbo.Messaging;

public class MessageBus(
    Channel<MessageEnvelope> ch,
    MessagingConfig cfg,
    IServiceProvider root,
    ILogger<MessageBus> logger
)
    : EnvelopeBus<MessageEnvelope, IMessageEvent, MessageContext, MessagingConfig>(
        ch,
        cfg,
        root,
        logger
    ),
        IMessageBus
{
    private readonly OrderedPerKeyDispatcher<string> _perUser = new(perKeyCapacity: 512);

    public override ValueTask EnqueueEnvelopeAsync(MessageEnvelope env, CancellationToken ct)
    {
        var key = cfg.PartitionKey(env.Session);

        return _perUser.EnqueueAsync(
            key,
            async _ =>
            {
                await ProcessOne(env, ct).ConfigureAwait(false);
            },
            ct
        );
    }

    protected override MessageEnvelope CreateEnvelope(
        IMessageEvent interaction,
        object? args,
        string? tag,
        TaskCompletionSource? tcs
    )
    {
        var session =
            args as ISessionContext
            ?? throw new ArgumentException("Session context is required", nameof(args));

        return new MessageEnvelope
        {
            Data = interaction!,
            Session = session,
            Tag = tag ?? null,
            SyncTcs = tcs,
        };
    }

    protected override Type GetHandlerForType(Type interactionType)
    {
        return typeof(IMessageHandler<>).MakeGenericType(interactionType);
    }

    protected override Type GetBehaviorForType(Type interactionType)
    {
        return typeof(IMessageBehavior<>).MakeGenericType(interactionType);
    }

    protected override MessageContext CreateContextForEnvelope(
        MessageEnvelope envelope,
        IServiceProvider sp
    )
    {
        return new MessageContext { Services = sp, Session = envelope.Session };
    }

    protected override async Task ExecuteEnvelopesSequentially(
        MessageEnvelope env,
        MessageContext ctx,
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
        MessageEnvelope env,
        MessageContext ctx,
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
