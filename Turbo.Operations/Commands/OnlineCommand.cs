using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Operations.Configuration;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Operations.Commands;

/// <summary>
/// <c>:online</c>. How many players are online. With <c>command.online.list</c> it lists them by
/// name too, up to <c>Turbo:Operations:OnlineListMaxNames</c>, ten to a line.
/// </summary>
[Command(
    "online",
    Description = "See how many players are online",
    Category = CommandCategories.ADMINISTRATION
)]
[RequiresPermission(PermissionNodes.Command.ONLINE)]
public sealed class OnlineCommand(
    IGrainFactory grainFactory,
    ISessionGateway sessionGateway,
    IOptions<OperationsConfig> config
) : IOperatorCommand<NoArguments>
{
    private const string COUNT = "count";
    private const int NAMES_PER_LINE = 10;

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string> { [COUNT] = "%0% players are online." };

    public async ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        NoArguments arguments,
        CancellationToken ct
    )
    {
        var online = sessionGateway.GetOnlinePlayerIds();
        var count = online.Count.ToString(CultureInfo.InvariantCulture);

        if (!await ctx.Executor.HasAsync(PermissionNodes.Command.ONLINE_LIST, ct))
            return CommandResult.Done(COUNT, count);

        var shown = online.Take(config.Value.OnlineListMaxNames).ToList();
        var names = await grainFactory.GetPlayerDirectoryGrain().GetPlayerNamesAsync(shown, ct);

        var lines = new List<string> { $"{count} players are online" };

        lines.AddRange(
            shown
                .Select(id => names.TryGetValue(id, out var name) ? name : id.ToString())
                .Order()
                .Chunk(NAMES_PER_LINE)
                .Select(chunk => string.Join(", ", chunk))
        );

        if (online.Count > shown.Count)
            lines.Add($"... and {online.Count - shown.Count} more");

        await ctx.Executor.NoticeAsync(lines, ct);

        return CommandResult.Ok;
    }
}
