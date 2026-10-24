using System;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Error;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.PacketHandlers.Userdefinedroomevents;

/// <summary>
/// Saving a wired box is the same request for every kind of box; only the message type differs.
/// The room validates and stores the update, and the editor is told either way so it never
/// hangs on a save that was refused: with the hotel text the box gave, or Habbo's server error
/// for a refusal it has no text for.
/// </summary>
public abstract class UpdateWiredMessageHandler<TMessage>(IGrainFactory grainFactory)
    : IMessageHandler<TMessage>
    where TMessage : UpdateWiredMessage
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    /// <summary>
    /// Habbo's own number for this save in the <c>ErrorReport</c> it answers a refusal with (not a
    /// header; the client never reads it). 0 where no refusal was recorded.
    /// </summary>
    protected virtual int ErrorReportMessageId => 0;

    public async ValueTask HandleAsync(TMessage message, MessageContext ctx, CancellationToken ct)
    {
        if (ctx is null || ctx.RoomId <= 0 || message.Id <= 0)
            return;

        var result = await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .ApplyWiredUpdateAsync(ctx.AsActionContext(), message.Id, message, ct)
            .ConfigureAwait(false);

        if (result.ServerErrorCode is { } errorCode)
        {
            await ctx.SendComposerAsync(
                    new ErrorReportEventMessageComposer
                    {
                        MessageId = ErrorReportMessageId,
                        ErrorCode = errorCode,
                        Time = DateTimeOffset.UtcNow,
                    },
                    ct
                )
                .ConfigureAwait(false);

            return;
        }

        if (!result.IsSaved)
        {
            await ctx.SendComposerAsync(
                    new WiredValidationErrorEventMessageComposer
                    {
                        LocalizationKey = result.ErrorKey ?? WiredSaveErrors.GENERIC,
                        Parameters = [.. result.Parameters],
                    },
                    ct
                )
                .ConfigureAwait(false);

            return;
        }

        // Through the player's presence, after what the save sent them there (a selector's
        // ObjectDataUpdate); straight to the session it would overtake it.
        await _grainFactory
            .SendComposerToPlayerAsync(ctx.PlayerId, new WiredSaveSuccessEventMessageComposer(), ct)
            .ConfigureAwait(false);
    }
}
