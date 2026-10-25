using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Database.Context;
using Turbo.Database.Entities.Players;
using Turbo.Events;
using Turbo.Players.Configuration;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Accounts;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Events;
using Turbo.Primitives.Policies;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Players.Accounts;

/// <summary>
/// <see cref="IPlayerAccountService"/>: a <c>players</c> row, the name checked by
/// <see cref="PlayerNames"/> and free whatever its case, as the hotel's lookups treat it, then
/// the hotel's registration policies (<see cref="IRegistrationPolicy"/>). Every refusal raises
/// <see cref="PlayerRegistrationFailedEvent"/>, every player made <see cref="PlayerRegisteredEvent"/>.
/// </summary>
public sealed class PlayerAccountService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOwnerBootstrap owner,
    IWordFilter wordFilter,
    IOptions<OnboardingConfig> onboarding,
    IPlayerNamePolicy namePolicy,
    ILogger<IPlayerAccountService> logger,
    EventSystem? events = null,
    IGamePolicies? policies = null
) : IPlayerAccountService
{
    public const int MOTTO_MAX_LENGTH = 38;
    public const int FIGURE_MAX_LENGTH = 279;

    /// <summary>
    /// The figure a new player wears when none is given, by gender: clothing and colours anyone
    /// may wear, so a player outside the club keeps it as it is.
    /// </summary>
    public const string MALE_FIGURE =
        "hr-115-42.hd-195-19.ch-3030-82.lg-275-1408.fa-1201.ca-1804-64";
    public const string FEMALE_FIGURE = "hr-515-42.hd-600-1.ch-635-82.lg-716-66-66.sh-735-68";

    public async Task<NewPlayerResult> CreateAsync(NewPlayer player, CancellationToken ct)
    {
        var made = await MakeAsync(player, ct).ConfigureAwait(false);

        if (made.Created is null)
            events?.PublishCommitted(
                new PlayerRegistrationFailedEvent
                {
                    Name = player.Name,
                    Reason = made.Error ?? string.Empty,
                    Source = player.Source,
                    RemoteAddress = player.RemoteAddress,
                }
            );

        return made;
    }

    private async Task<NewPlayerResult> MakeAsync(NewPlayer player, CancellationToken ct)
    {
        if (PlayerNames.Check(player.Name) is { } refused)
            return NewPlayerResult.Refused(refused);

        if (!wordFilter.IsClean(player.Name.Trim()) || namePolicy.IsBlocked(player.Name))
            return NewPlayerResult.Refused(PlayerNames.FILTERED);

        var name = player.Name.Trim();
        var motto = player.Motto?.Trim() ?? string.Empty;
        var figure = string.IsNullOrWhiteSpace(player.Figure)
            ? player.Gender == AvatarGenderType.Female
                ? FEMALE_FIGURE
                : MALE_FIGURE
            : player.Figure.Trim();

        if (motto.Length > MOTTO_MAX_LENGTH)
            return NewPlayerResult.Refused($"A motto is up to {MOTTO_MAX_LENGTH} characters.");

        if (figure.Length > FIGURE_MAX_LENGTH)
            return NewPlayerResult.Refused($"A figure is up to {FIGURE_MAX_LENGTH} characters.");

        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var key = PlayerNames.Key(name);

        if (await db.Players.AnyAsync(x => x.NameKey == key, ct).ConfigureAwait(false))
            return NewPlayerResult.Refused($"There is already a player called {name}.");

        // The hotel's plugins have the last word: an address's cap, a look of their own.
        if (policies is not null)
        {
            var decision = await policies
                .DecideRegistrationAsync(
                    new RegistrationRequest(
                        name,
                        motto,
                        figure,
                        player.Gender,
                        player.Source,
                        player.RemoteAddress
                    ),
                    ct
                )
                .ConfigureAwait(false);

            if (!decision.Allowed)
                return NewPlayerResult.Refused(
                    decision.Reason ?? "New accounts can't be made right now."
                );
        }

        var entity = new PlayerEntity
        {
            Name = name,
            Motto = motto,
            Figure = figure,
            Gender = player.Gender,
            PlayerStatus = PlayerStatusType.Offline,
        };

        // Made while the new user flow is on: its steps wait for their first login.
        if (onboarding.Value.Enabled)
        {
            entity.OnboardingNamePending = true;
            entity.OnboardingRoomPending = true;
        }

        db.Players.Add(entity);

        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // Taken between the look and the write: the name's unique index says so.
            return NewPlayerResult.Refused($"There is already a player called {name}.");
        }

        logger.LogInformation("Created player {PlayerId} ({PlayerName})", entity.Id, name);

        await owner.PlayerCreatedAsync(new PlayerId(entity.Id), name, ct).ConfigureAwait(false);

        events?.PublishCommitted(
            new PlayerRegisteredEvent
            {
                PlayerId = new PlayerId(entity.Id),
                Name = name,
                Source = player.Source,
                RemoteAddress = player.RemoteAddress,
            }
        );

        return NewPlayerResult.Done(entity.Id);
    }
}
