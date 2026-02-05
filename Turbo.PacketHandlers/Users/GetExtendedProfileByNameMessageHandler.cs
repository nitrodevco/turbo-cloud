using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Database.Context;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Users;

namespace Turbo.PacketHandlers.Users;

public class GetExtendedProfileByNameMessageHandler
    : IMessageHandler<GetExtendedProfileByNameMessage>
{
    private readonly IGrainFactory _grainFactory;
    private readonly IDbContextFactory<TurboDbContext> _dbContextFactory;

    public GetExtendedProfileByNameMessageHandler(
        IGrainFactory grainFactory,
        IDbContextFactory<TurboDbContext> dbContextFactory
    )
    {
        _grainFactory = grainFactory;
        _dbContextFactory = dbContextFactory;
    }

    public async ValueTask HandleAsync(
        GetExtendedProfileByNameMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(message.UserName))
            return;

        try
        {
            // Get player data from database by username
            await using var dbCtx = await _dbContextFactory.CreateDbContextAsync(ct);

            var player = await dbCtx.Players
                .AsNoTracking()
                .Include(p => p.PlayerCurrencies)
                .FirstOrDefaultAsync(p => p.Name == message.UserName, ct);

            if (player == null)
                return;

            var response = new ExtendedProfileMessageComposer
            {
                UserId = player.Id,
                UserName = player.Name ?? "Unknown",
                Figure = player.Figure ?? "hr-115-42.hd-195-19.ch-3030-82.lg-275-1408.fa-1201.ca-1804-64",
                Motto = player.Motto ?? "",
                CreationDate = player.CreatedAt.ToString("yyyy-MM-dd"),
                AchievementScore = 0,
                FriendCount = 0, // TODO: Query from messenger_friends when available
                IsFriend = false, // TODO: Check friendship when messenger_friends is available
                IsFriendRequestSent = false, // TODO: Check messenger_requests when available
                IsOnline = true, // TODO: Check if player has active room or session
                Guilds = new List<GuildInfo>(), // TODO: Fetch guilds from database when guild system is implemented
                LastAccessSinceInSeconds = 0,
                OpenProfileWindow = true,
                IsHidden = false,
                AccountLevel = 1, // TODO: Get from database when account level is implemented
                IntegerField24 = 0,
                StarGemCount = 0,
                BooleanField26 = false,
                BooleanField27 = false
            };

            await ctx.SendComposerAsync(response, ct).ConfigureAwait(false);
        }
        catch
        {
            // TODO: Log error
        }
    }
}
