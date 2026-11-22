using System.Threading;
using System.Threading.Tasks;
using Turbo.Database.Entities.Messenger;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Texts;

namespace Turbo.Import.Morningstar.Steps;

/// <summary>
/// With <c>--chatlogs</c>: what was said in rooms (<c>chatlogs_room</c>, for moderators) and in
/// private (<c>chatlogs_private</c>, Turbo's messenger history, as delivered). Morningstar kept
/// them only while <c>save.room.chats</c> / <c>save.private.chats</c> were on, so they may be
/// empty. A line from or to a player, or in a room, that was not copied is left out.
/// </summary>
internal sealed class ChatlogsStep : IImportStep
{
    public string Name => "chat logs";

    public bool Applies(ImportContext ctx) => ctx.Options.Chatlogs;

    public async Task RunAsync(ImportContext ctx, CancellationToken ct)
    {
        var room = ctx.Batch<RoomChatlogEntity>("room_chatlogs", 10_000);

        await foreach (
            var row in ctx.Source.ReadAsync("chatlogs_room", "timestamp", ct).ConfigureAwait(false)
        )
        {
            var roomId = row.Int("room_id");
            var from = row.Int("user_from_id");
            var to = row.Int("user_to_id");
            var message = row.Text("message");

            if (
                !ctx.RoomOwners.ContainsKey(roomId)
                || !ctx.Players.Contains(from)
                || message.Length == 0
            )
                continue;

            await room.AddAsync(
                    new RoomChatlogEntity
                    {
                        CreatedAt = row.UnixTime("timestamp") ?? ctx.Now,
                        RoomEntityId = roomId,
                        PlayerEntityId = from,
                        TargetPlayerEntityId = ctx.Players.Contains(to) ? to : null,
                        Message = ClientText.Truncate(
                            message,
                            RoomChatlogEntity.MESSAGE_MAX_LENGTH
                        ),
                        RoomEntity = null!,
                        PlayerEntity = null!,
                    },
                    ct
                )
                .ConfigureAwait(false);
        }

        await room.FlushAsync(ct).ConfigureAwait(false);

        var messages = ctx.Batch<MessengerMessageEntity>("messenger_messages", 10_000);

        await foreach (
            var row in ctx
                .Source.ReadAsync("chatlogs_private", "timestamp", ct)
                .ConfigureAwait(false)
        )
        {
            var from = row.Int("user_from_id");
            var to = row.Int("user_to_id");
            var message = row.Text("message");

            if (
                from == to
                || !ctx.Players.Contains(from)
                || !ctx.Players.Contains(to)
                || message.Length == 0
            )
                continue;

            var at = row.UnixTime("timestamp") ?? ctx.Now;

            await messages
                .AddAsync(
                    new MessengerMessageEntity
                    {
                        CreatedAt = at,
                        SenderPlayerEntityId = from,
                        ReceiverPlayerEntityId = to,
                        Message = ClientText.Truncate(
                            message,
                            ctx.Options.Limits.MessengerMessageMaxLength
                        ),
                        Timestamp = at,
                        Delivered = true,
                        SenderPlayerEntity = null!,
                        ReceiverPlayerEntity = null!,
                    },
                    ct
                )
                .ConfigureAwait(false);
        }

        await messages.FlushAsync(ct).ConfigureAwait(false);
    }
}
