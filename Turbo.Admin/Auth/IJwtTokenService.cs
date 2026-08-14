using Turbo.Database.Entities.Admin;

namespace Turbo.Admin.Auth;

public interface IJwtTokenService
{
    string IssueToken(AdminUserEntity adminUser);
}
