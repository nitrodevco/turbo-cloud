using Turbo.Authorization.Players.Requirements;
using Turbo.Authorization.Policy;
using Turbo.Networking.Abstractions.Session;

namespace Turbo.Authorization.Players.Policies;

public class LoginPolicy : OperationPolicy<ISessionContext>
{
    public LoginPolicy()
        : base()
    {
        AllOf(new NotBannedRequirement<ISessionContext>());
    }
}
