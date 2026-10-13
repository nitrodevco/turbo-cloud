using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Moderator;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.PacketHandlers.Moderator;

/// <summary>The issue handler window moved or resized (<c>IssueHandler.sendWindowPreferences</c>): kept for the next login.</summary>
[RequiresPermission(PermissionNodes.Moderation.TOOL)]
public class ModToolPreferencesMessageHandler(IModerationIssueService issues)
    : IMessageHandler<ModToolPreferencesMessage>
{
    private readonly IModerationIssueService _issues = issues;

    public async ValueTask HandleAsync(
        ModToolPreferencesMessage message,
        MessageContext ctx,
        CancellationToken ct
    ) =>
        await _issues
            .SavePreferencesAsync(
                ctx.PlayerId,
                new ModToolPreferencesSnapshot
                {
                    WindowX = message.WindowX,
                    WindowY = message.WindowY,
                    WindowWidth = message.WindowWidth,
                    WindowHeight = message.WindowHeight,
                },
                ct
            )
            .ConfigureAwait(false);
}
