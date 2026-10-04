using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Admin.Configuration;
using Turbo.Admin.Links;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Commands;

public sealed record AdminSetupArguments(PlayerTarget? Who = null);

/// <summary>
/// <c>:adminsetup [player]</c>. A link to make the passkey that signs in to the admin panel. It
/// works once, for some hours, and replaces any passkeys the player has. On their own, a player
/// gets one only before they have a passkey; for somebody else it takes
/// <c>admin.passkeys.reset</c> and is bound by <see cref="AdminLinkPolicy"/>. The console names
/// anybody, which is how a hotel's first admin gets in.
/// </summary>
[Command(
    "adminsetup",
    Description = "Get a link to set up an admin panel passkey",
    Category = CommandCategories.ADMINISTRATION
)]
[RequiresPermission(PermissionNodes.Admin.PANEL)]
public sealed class AdminSetupCommand(
    IGrainFactory grainFactory,
    AdminLinkPolicy policy,
    IOptions<AdminConfig> config
) : IOperatorCommand<AdminSetupArguments>
{
    private const string DISABLED = "disabled";
    private const string NAME_PLAYER = "name_player";
    private const string ALREADY_SET_UP = "already_set_up";
    private const string NEEDS_RESET_NODE = "needs_reset_node";
    private const string OUTRANKS = "outranks";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string>
        {
            [DISABLED] = "The admin panel is not enabled on this hotel.",
            [NAME_PLAYER] = "Name the player to set up: adminsetup <player>.",
            [ALREADY_SET_UP] =
                "You already have a passkey. Add more on the panel's account page; an admin can replace a lost one.",
            [NEEDS_RESET_NODE] = "You can't set up someone else's passkey.",
            [OUTRANKS] = "%0% has permissions you don't, so you can't set up their passkey.",
        };

    public async ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        AdminSetupArguments arguments,
        CancellationToken ct
    )
    {
        var options = config.Value;

        if (!options.Enabled)
            return CommandResult.Fail(DISABLED);

        ResolvedPlayer target;

        if (arguments.Who is { } who)
        {
            var selection = await ctx.ResolveAsync(who, ct).ConfigureAwait(false);

            if (selection.Failure is { } failure)
                return failure;

            target = selection.Players[0];
        }
        else if (ctx.Executor.PlayerId is { } self)
        {
            target = new ResolvedPlayer(self, ctx.Executor.Name);
        }
        else
        {
            return CommandResult.Fail(NAME_PLAYER);
        }

        var decision = await policy
            .CheckAsync(ctx.Executor.PlayerId, target.Id, ct)
            .ConfigureAwait(false);

        switch (decision.Refusal)
        {
            case AdminLinkRefusal.AlreadySetUp:
                return CommandResult.Fail(ALREADY_SET_UP);
            case AdminLinkRefusal.NeedsResetNode:
                return CommandResult.Fail(NEEDS_RESET_NODE);
            case AdminLinkRefusal.OutranksIssuer:
                return CommandResult.Fail(OUTRANKS, target.Name);
        }

        var link = await grainFactory
            .GetAdminAuthGrain()
            .CreateSetupTokenAsync(target.Id, ct)
            .ConfigureAwait(false);

        // In the fragment, which browsers never send to a server, so it stays out of access logs
        // and referrers.
        await ctx
            .Executor.NoticeAsync(
                [
                    decision.ReplacesExisting
                        ? $"Passkey reset for {target.Name}: the link replaces all their passkeys."
                        : $"Passkey setup for {target.Name}.",
                    $"It works once, until {link.ExpiresAtUtc.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)}:",
                    $"{options.PanelUrl.TrimEnd('/')}/setup#token={link.Token}",
                ],
                ct
            )
            .ConfigureAwait(false);

        return CommandResult.Ok;
    }
}
