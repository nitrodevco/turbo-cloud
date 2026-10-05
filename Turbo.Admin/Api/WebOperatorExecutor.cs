using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Admin.Api;

/// <summary>
/// A staff member running an operator command from the admin panel: the executor kind
/// <c>docs/commands.md</c> left open for a remote console. It is the player, with exactly the
/// player's nodes, so the panel can never do more than its user could in game, and every use is
/// logged like theirs. It has no room, so the room commands refuse it. What a command answers is
/// collected here and returned with the request.
/// </summary>
internal sealed class WebOperatorExecutor(
    PlayerId playerId,
    string name,
    IGrainFactory grainFactory
) : IOperatorExecutor
{
    private const string REPLY = "reply";
    private const string NOTICE = "notice";

    private readonly Lock _lock = new();
    private readonly List<CommandOutputLine> _lines = [];

    public PlayerId? PlayerId => playerId;

    public string Name => name;

    public string Source => "panel";

    public RoomId? RoomId => null;

    public IReadOnlyList<PlayerId> RoomPlayerIds { get; } = [];

    public IReadOnlyList<CommandOutputLine> Lines
    {
        get
        {
            lock (_lock)
                return [.. _lines];
        }
    }

    public Task<bool> HasAsync(string node, CancellationToken ct) =>
        grainFactory.HasPermissionAsync(playerId, node, ct);

    public Task ReplyAsync(string text, CancellationToken ct)
    {
        lock (_lock)
            _lines.Add(new CommandOutputLine(REPLY, text));

        return Task.CompletedTask;
    }

    public Task NoticeAsync(IReadOnlyList<string> lines, CancellationToken ct)
    {
        lock (_lock)
        {
            foreach (var line in lines)
                _lines.Add(new CommandOutputLine(NOTICE, line));
        }

        return Task.CompletedTask;
    }
}
