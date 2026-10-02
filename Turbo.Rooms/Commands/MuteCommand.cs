using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Rooms.Configuration;

namespace Turbo.Rooms.Commands;

public sealed record MuteArguments(IRoomPlayer Target, int? Minutes = null);

/// <summary>
/// <c>:mute name [minutes]</c>, on the same terms as <see cref="KickCommand"/>: the room's own
/// rules decide who may mute whom, and the room caps the length.
/// </summary>
[Command(
    "mute",
    Description = "Mute a player in the room",
    Category = CommandCategories.MODERATION
)]
[RequiresPermission(PermissionNodes.Command.MUTE)]
public sealed class MuteCommand(IOptions<RoomConfig> config) : ICommand<MuteArguments>
{
    private const string MUTED = "muted";
    private const string REFUSED = "refused";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string>
        {
            [MUTED] = "%0% is muted for %1% minutes.",
            [REFUSED] = "You can't mute %0%.",
        };

    public async ValueTask<CommandResult> ExecuteAsync(
        ICommandContext ctx,
        MuteArguments arguments,
        CancellationToken ct
    )
    {
        var minutes = Math.Min(
            arguments.Minutes ?? config.Value.CommandDefaultMuteMinutes,
            config.Value.ChatMuteMaxDurationMinutes
        );

        if (minutes <= 0 || !await ctx.Room.MuteAsync(arguments.Target, minutes, ct))
            return CommandResult.Fail(REFUSED, arguments.Target.Name);

        return CommandResult.Done(MUTED, arguments.Target.Name, minutes.ToString());
    }
}
