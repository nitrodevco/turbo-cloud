using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Turbo.Primitives.Authentication;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Accounts;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Web.Configuration;

namespace Turbo.Web.Commands;

public sealed record CreatePlayerArguments(string Name, string? Gender = null);

/// <summary>
/// <c>createplayer &lt;name&gt; [male|female]</c>. Makes a player and a ticket to sign them in once,
/// as a link to the client when <c>Turbo:Web:ClientUrl</c> is set. It is how a hotel without the
/// public site or Discord gets its first player: run from the server console, and with
/// <c>TURBO_OWNER_NAME</c> naming them, that player becomes the hotel's owner.
/// </summary>
[Command(
    "createplayer",
    Description = "Create a player and a ticket to sign in as them",
    Category = CommandCategories.ADMINISTRATION
)]
[RequiresPermission(PermissionNodes.Admin.PLAYERS_CREATE)]
public sealed class CreatePlayerCommand(
    IPlayerAccountService accounts,
    ILoginTicketService tickets,
    IOptions<WebConfig> web
) : IOperatorCommand<CreatePlayerArguments>
{
    /// <summary>Long enough to open the client after reading the console; the login uses it up.</summary>
    public static readonly TimeSpan TICKET_LIFETIME = TimeSpan.FromHours(1);

    private const string REFUSED = "refused";
    private const string BAD_GENDER = "bad_gender";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string>
        {
            [REFUSED] = "No player was created: %0%",
            [BAD_GENDER] = "The gender is male or female.",
        };

    public async ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        CreatePlayerArguments arguments,
        CancellationToken ct
    )
    {
        AvatarGenderType gender;

        switch (arguments.Gender?.Trim().ToLowerInvariant())
        {
            case null or "" or "m" or "male":
                gender = AvatarGenderType.Male;
                break;
            case "f" or "female":
                gender = AvatarGenderType.Female;
                break;
            default:
                return CommandResult.Fail(BAD_GENDER);
        }

        var created = await accounts
            .CreateAsync(new NewPlayer(arguments.Name, null, gender, null), ct)
            .ConfigureAwait(false);

        if (created.Created is not { } player)
            return CommandResult.Fail(REFUSED, created.Error ?? "");

        var ticket = await tickets
            .IssueAsync(player, TICKET_LIFETIME, reusable: false, ct)
            .ConfigureAwait(false);
        var lines = new List<string>
        {
            $"Created player {arguments.Name.Trim()} (id {player.Value.ToString(CultureInfo.InvariantCulture)}).",
            $"Their sign-in ticket works once, until {ticket.ExpiresAtUtc?.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)}:",
        };
        var clientUrl = web.Value.ClientUrl;

        if (clientUrl.Contains("{ticket}", StringComparison.Ordinal))
            await ctx
                .Executor.LinkNoticeAsync(
                    lines,
                    "Open the client",
                    clientUrl.Replace(
                        "{ticket}",
                        Uri.EscapeDataString(ticket.Ticket),
                        StringComparison.Ordinal
                    ),
                    ct
                )
                .ConfigureAwait(false);
        else
            await ctx.Executor.NoticeAsync([.. lines, ticket.Ticket], ct).ConfigureAwait(false);

        return CommandResult.Ok;
    }
}
