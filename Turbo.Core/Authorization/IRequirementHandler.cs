namespace Turbo.Core.Authorization;

using System.Threading;
using System.Threading.Tasks;

public interface IRequirementHandler<in TReq, in TCtx>
{
    Task<AuthorizationResult> HandleAsync(TReq requirement, TCtx context, CancellationToken ct = default);
}
