using System.Threading.Tasks;
using Turbo.Core.Networking.Session;

namespace Turbo.Core.Networking.Protocol;

public interface IPackageHandler
{
    public Task HandlePackageAsync(ISessionContext ctx, Package package);
}
