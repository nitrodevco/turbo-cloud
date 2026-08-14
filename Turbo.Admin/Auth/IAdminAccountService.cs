using System.Threading;
using System.Threading.Tasks;
using Turbo.Database.Entities.Admin;

namespace Turbo.Admin.Auth;

public interface IAdminAccountService
{
    Task<AdminUserEntity?> ValidateCredentialsAsync(
        string username,
        string password,
        CancellationToken ct
    );

    Task<bool> CreateAdminAsync(
        string username,
        string password,
        AdminRoleType role,
        CancellationToken ct
    );
}
