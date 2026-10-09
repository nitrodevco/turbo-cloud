using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Guilds.Grains;

/// <summary>The group's side of its forum, which <c>GuildForumGrain</c> owns.</summary>
internal sealed partial class GuildGrain
{
    public async Task OnForumOpenedAsync(CancellationToken ct)
    {
        if (_state.Guild is not { } guild || guild.HasForum)
            return;

        _state.Guild = guild with { HasForum = true };

        await PublishChangedAsync(guild.OwnerId, ct);
    }
}
