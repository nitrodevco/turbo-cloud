using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Entities.Hotel;
using Turbo.Primitives.Hotel.Grains;

namespace Turbo.Operations.Grains;

/// <summary>
/// The welcome message for the whole hotel, one grain. It is read from <c>hotel_settings</c> on
/// activation and kept in memory, since every login asks; staff saving a new one writes it
/// through and replaces the copy here.
/// </summary>
internal sealed class WelcomeMessageGrain(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    ILogger<IWelcomeMessageGrain> logger
) : Grain, IWelcomeMessageGrain
{
    internal const string SETTING_KEY = "welcome_message";

    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;
    private readonly ILogger<IWelcomeMessageGrain> _logger = logger;

    private string _message = string.Empty;

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        // A login must not fail over its welcome, so a message that can't be read is logged and
        // treated as none; saving one from the panel replaces it.
        try
        {
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            _message =
                await dbCtx
                    .HotelSettings.AsNoTracking()
                    .Where(x => x.Key == SETTING_KEY)
                    .Select(x => x.Value)
                    .FirstOrDefaultAsync(ct)
                ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load the welcome message; logins will show none");
        }
    }

    public Task<string> GetMessageAsync(CancellationToken ct) => Task.FromResult(_message);

    public async Task<string> SetMessageAsync(string message, CancellationToken ct)
    {
        var value = Normalize(message);

        if (value.Length > HotelSettingEntity.VALUE_MAX_LENGTH)
            throw new ArgumentOutOfRangeException(
                nameof(message),
                $"The welcome message can be at most {HotelSettingEntity.VALUE_MAX_LENGTH} characters."
            );

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var setting = await dbCtx.HotelSettings.FirstOrDefaultAsync(x => x.Key == SETTING_KEY, ct);

        if (setting is null)
            dbCtx.HotelSettings.Add(new HotelSettingEntity { Key = SETTING_KEY, Value = value });
        else
            setting.Value = value;

        await dbCtx.SaveChangesAsync(ct);

        _message = value;

        return value;
    }

    /// <summary>Trimmed, with the line breaks a browser sends made plain.</summary>
    internal static string Normalize(string? message) =>
        (message ?? string.Empty).Replace("\r\n", "\n").Trim();
}
