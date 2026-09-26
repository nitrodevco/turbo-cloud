using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Users;

public class CreateGuildMessageHandler(
    IGrainFactory grainFactory,
    ILogger<CreateGuildMessageHandler> logger
) : IMessageHandler<CreateGuildMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly ILogger<CreateGuildMessageHandler> _logger = logger;

    public async ValueTask HandleAsync(
        CreateGuildMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var result = await _grainFactory
            .GetPlayerGuildGrain(ctx.PlayerId)
            .CreateGuildAsync(message.Request, ct)
            .ConfigureAwait(false);

        if (result.Succeeded)
        {
            await ctx.SendComposerAsync(
                    new GuildCreatedMessageComposer
                    {
                        RoomId = result.RoomId,
                        GuildId = result.GuildId,
                    },
                    ct
                )
                .ConfigureAwait(false);

            return;
        }

        if (ToEditFailure(result.Failure) is not { } reason)
        {
            // InsufficientCredits and CreationFailed reach here, and the hotel publishes no text
            // for either: neither is something the player did, and saying one of the four
            // reasons that do have text would put the wrong sentence on screen.
            _logger.LogInformation(
                "Group creation refused for player {PlayerId}: {Reason}",
                ctx.PlayerId.Value,
                result.Failure
            );

            return;
        }

        await ctx.SendComposerAsync(new GuildEditFailedMessageComposer { Reason = reason }, ct)
            .ConfigureAwait(false);
    }

    private static GuildEditFailedType? ToEditFailure(GuildCreationFailureType? failure) =>
        failure switch
        {
            GuildCreationFailureType.InvalidName => GuildEditFailedType.InvalidName,
            GuildCreationFailureType.TooManyGroups => GuildEditFailedType.TooManyGroups,
            GuildCreationFailureType.RoomAlreadyHomeroom => GuildEditFailedType.RoomAlreadyHomeroom,
            GuildCreationFailureType.ClubRequired => GuildEditFailedType.ClubRequired,
            _ => null,
        };
}
