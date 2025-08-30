using Org.BouncyCastle.Tls;
using Turbo.Authorization.Players.Requirements;
using Turbo.Authorization.Policy;
using Turbo.Core.Authorization;
using Turbo.Core.Networking.Session;

namespace Turbo.Authorization.Players.Policies;

public class LoginPolicy : OperationPolicy<ISessionContext>
{
    public LoginPolicy()
        : base()
    {
        AllOf(new NotBannedRequirement<ISessionContext>());
    }
}
