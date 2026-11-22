using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Database.Entities.Bots;
using Turbo.Import.Morningstar.Formats;
using Turbo.Primitives.Bots;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Texts;

namespace Turbo.Import.Morningstar.Steps;

/// <summary>
/// <c>bots</c>, ids kept: in their room where it was copied, else in the owner's inventory.
/// Morningstar separates chat lines with <c>\r</c>, Turbo with <c>\n</c>. Turbo's bots have no
/// kind: a bartender or a visitor log bot comes across as a plain bot, which the report says.
/// </summary>
internal sealed class BotsStep : IImportStep
{
    private const int MAX_CHAT_LINES = 30;

    public string Name => "bots";

    public async Task RunAsync(ImportContext ctx, CancellationToken ct)
    {
        var batch = ctx.Batch<BotEntity>("bots");

        await foreach (var row in ctx.Source.ReadAsync("bots", "id", ct).ConfigureAwait(false))
        {
            var id = row.Int("id");
            var owner = row.Int("user_id");

            if (!ctx.Players.Contains(owner))
            {
                ctx.Report.Note("bot left out: its owner was not copied", $"bot {id}");

                continue;
            }

            var room = row.Int("room_id");
            var inRoom = room > 0 && ctx.RoomOwners.ContainsKey(room);

            if (room > 0 && !inRoom)
                ctx.Report.Note(
                    "bot put in its owner's inventory: its room was not copied",
                    $"bot {id} room {room}"
                );
            var kind = row.Text("type", "generic").Trim().ToLowerInvariant();

            if (kind is not ("" or "generic"))
                ctx.Report.Note(
                    "bot copied as a plain bot: Turbo's bots have no kind",
                    $"bot {id} ({kind})"
                );

            var lines = BotChatLines.Split(row.String("chat_lines")).Take(MAX_CHAT_LINES);
            var chat = string.Join('\n', lines);

            chat = ClientText.Truncate(chat, BotEntity.CHAT_TEXT_MAX_LENGTH);

            var figure = row.Text("figure").Trim();

            await batch
                .AddAsync(
                    new BotEntity
                    {
                        Id = id,
                        CreatedAt = ctx.Now,
                        PlayerEntityId = owner,
                        RoomEntityId = inRoom ? room : null,
                        Name = ClientText.Truncate(
                            row.Text("name", "Bot").Trim(),
                            BotEntity.NAME_MAX_LENGTH
                        ),
                        Motto = ClientText.Truncate(
                            row.Text("motto").Trim(),
                            BotEntity.MOTTO_MAX_LENGTH
                        ),
                        Figure = ClientText.Truncate(figure, BotEntity.FIGURE_MAX_LENGTH),
                        Gender = Genders.Of(row.Text("gender")),
                        X = inRoom ? row.Int("x") : 0,
                        Y = inRoom ? row.Int("y") : 0,
                        Z = inRoom ? row.Double("z") : 0,
                        Rotation = Directions.Rotation(inRoom ? row.Int("rot") : 0),
                        FreeRoam = row.Bool("freeroam"),
                        ChatText = chat.Length == 0 ? null : chat,
                        AutoChat = row.Bool("chat_auto"),
                        ChatDelaySeconds = Math.Max(0, row.Int("chat_delay")),
                        MixSentences = row.Bool("chat_random"),
                        DanceType = row.Int("dance") is >= 0 and <= 4 and var dance
                            ? (AvatarDanceType)dance
                            : AvatarDanceType.None,
                    },
                    ct
                )
                .ConfigureAwait(false);
        }

        await batch.FlushAsync(ct).ConfigureAwait(false);
    }
}
