using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Guilds.Grains;

/// <summary>
/// Staff changing a group from the admin panel: what its owner could do, done by someone outside
/// it. Each goes the same way as the owner's own change - saved, the directory and homeroom told,
/// the owner's open window nudged - and names the staff member in the log.
/// </summary>
internal sealed partial class GuildGrain
{
    public async Task<bool> StaffRenameAsync(
        PlayerId staffId,
        string name,
        string description,
        CancellationToken ct
    )
    {
        if (_state.Guild is not { } guild)
            return false;

        var clampedName = Clamp(name, _guildConfig.NameMaxLength);

        if (clampedName.Length == 0)
            return false;

        await SaveAsync(
            entity =>
            {
                entity.Name = clampedName;
                entity.Description = Clamp(description, _guildConfig.DescriptionMaxLength);
            },
            ct
        );

        await PublishChangedAsync(guild.OwnerId, ct);

        _logger.LogInformation(
            "Staff {StaffId} renamed group {GuildId} to {Name}",
            staffId.Value,
            GuildId.Value,
            clampedName
        );

        return true;
    }

    public async Task<bool> StaffResetBadgeAsync(PlayerId staffId, CancellationToken ct)
    {
        if (_state.Guild is not { } guild)
            return false;

        var badgeCode = GuildBadgeCodes.Build(
            await _grainFactory.GetGuildDirectoryGrain().GetDefaultBadgePartsAsync(ct)
        );

        if (string.IsNullOrEmpty(badgeCode))
            return false;

        await SaveAsync(entity => entity.BadgeCode = badgeCode, ct);

        await PublishChangedAsync(guild.OwnerId, ct, repaintFurni: true);

        _logger.LogInformation(
            "Staff {StaffId} reset the badge of group {GuildId}",
            staffId.Value,
            GuildId.Value
        );

        return true;
    }

    public async Task<bool> StaffRemoveMemberAsync(
        PlayerId staffId,
        PlayerId targetId,
        CancellationToken ct
    )
    {
        if (_state.Guild is not { } guild || targetId == guild.OwnerId)
            return false;

        if (GetRank(targetId) is null)
            return false;

        await RemoveMemberAsync(targetId, ct);
        await PublishMembershipUpdatedAsync(targetId, ct);

        _logger.LogInformation(
            "Staff {StaffId} removed player {PlayerId} from group {GuildId}",
            staffId.Value,
            targetId.Value,
            GuildId.Value
        );

        return true;
    }

    public async Task<bool> StaffDeleteAsync(PlayerId staffId, CancellationToken ct)
    {
        if (_state.Guild is not { } guild)
            return false;

        var deleted = await DeleteAsync(guild, ct);

        if (deleted)
            _logger.LogInformation(
                "Staff {StaffId} deleted group {GuildId} ({Name})",
                staffId.Value,
                guild.GuildId.Value,
                guild.Name
            );

        return deleted;
    }

    private static string Clamp(string text, int max)
    {
        text = text.Trim();

        return text.Length <= max ? text : text[..max].TrimEnd();
    }
}
