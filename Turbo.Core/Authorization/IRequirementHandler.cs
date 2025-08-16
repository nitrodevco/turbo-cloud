using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Core.Authorization;

public interface IRequirementHandler<in TReq, in TCtx>
{
    Task<AuthorizationResult> HandleAsync(
        TReq requirement,
        TCtx context,
        CancellationToken ct = default
    );
}
