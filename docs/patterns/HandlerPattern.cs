using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Navigator;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Navigator.Enums;

namespace Docs.Patterns;

// Reference-only sample, and nothing compiles it — check it against the handler it mirrors,
// Turbo.PacketHandlers/Navigator/CanCreateRoomMessageHandler.cs, before copying it.
//
// The shape, in order: guard the input, call grains or a domain service, map what comes back
// onto a composer. A handler is `public class` and not sealed, because AssemblyExplorer
// discovers it by reflection and skips non-public types; it takes its dependencies through a
// primary constructor, as every handler in the repository does.
//
// Not here, deliberately: no database context or repository; no try/catch around the body,
// because PackageHandler already logs every handler failure with the packet header and session
// (a handler catches only a typed exception it turns into a reply, as the catalog ones do); no
// `IConfiguration` to read a limit, because a limit belongs to the grain that enforces it; no
// local send helper; no `ct.ThrowIfCancellationRequested()` and no null check on `message`,
// which the parser built.
//
// A packet the server has no system for yet gets a stub of this same shape whose body is
// `await ValueTask.CompletedTask.ConfigureAwait(false);` and whose summary says what is missing.

/// <summary>
/// Sent when the client opens the "create room" window. Says what the handler answers, and
/// where a reply is conditional, what the client does with each branch.
/// </summary>
public class HandlerPattern(INavigatorService navigatorService)
    : IMessageHandler<CanCreateRoomMessage>
{
    private readonly INavigatorService _navigatorService = navigatorService;

    public async ValueTask HandleAsync(
        CanCreateRoomMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        // Guard clauses first, brace-less, returning rather than throwing: an unauthenticated
        // session, or an id the client sent that cannot name anything.
        if (ctx.PlayerId <= 0)
            return;

        var (canCreate, roomLimit) = await _navigatorService
            .CanCreateRoomAsync(ctx.PlayerId, ct)
            .ConfigureAwait(false);

        // Replying to the session that sent this packet is `ctx.SendComposerAsync`, which needs
        // no grain hop. Pushing to some other player is
        // `grainFactory.SendComposerToPlayerAsync`; to a room, `RoomGrain.SendComposerToRoomAsync`.
        // A composer field the protocol defines carries the enum, not an int the call site casts.
        await ctx.SendComposerAsync(
                new CanCreateRoomMessageComposer
                {
                    Result = canCreate
                        ? RoomCreationResultType.Allowed
                        : RoomCreationResultType.RoomLimitReached,
                    RoomLimit = roomLimit,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
