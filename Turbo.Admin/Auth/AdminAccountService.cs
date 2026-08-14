using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Database.Entities.Admin;

namespace Turbo.Admin.Auth;

internal sealed class AdminAccountService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IPasswordHasher passwordHasher,
    ILogger<AdminAccountService> logger
) : IAdminAccountService
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly ILogger<AdminAccountService> _logger = logger;

    public async Task<AdminUserEntity?> ValidateCredentialsAsync(
        string username,
        string password,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return null;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var adminUser = await dbCtx
            .AdminUsers.SingleOrDefaultAsync(x => x.Username == username, ct)
            .ConfigureAwait(false);

        if (adminUser is null || !_passwordHasher.Verify(password, adminUser.PasswordHash))
            return null;

        try
        {
            await dbCtx
                .AdminUsers.Where(x => x.Id == adminUser.Id)
                .ExecuteUpdateAsync(up => up.SetProperty(x => x.LastLoginAt, DateTime.UtcNow), ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to update LastLoginAt for admin user {Username}",
                username
            );
        }

        return adminUser;
    }

    public async Task<bool> CreateAdminAsync(
        string username,
        string password,
        AdminRoleType role,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return false;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var exists = await dbCtx
            .AdminUsers.AnyAsync(x => x.Username == username, ct)
            .ConfigureAwait(false);

        if (exists)
            return false;

        dbCtx.AdminUsers.Add(
            new AdminUserEntity
            {
                Username = username,
                PasswordHash = _passwordHasher.Hash(password),
                Role = role,
            }
        );

        try
        {
            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create admin user {Username}", username);

            return false;
        }

        return true;
    }
}
