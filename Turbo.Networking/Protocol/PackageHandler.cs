using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SuperSocket.Connection;
using Turbo.Core.Networking.Protocol;
using Turbo.Core.Networking.Session;

namespace Turbo.Networking.Protocol;

public class PackageHandler(ILogger<PackageHandler> logger) : IPackageHandler
{
    private readonly ILogger<PackageHandler> _logger = logger;

    public async Task HandlePackageAsync(ISessionContext ctx, Package package)
    {
        switch (package.Type)
        {
            case PackageType.Policy:
                const string Policy =
                    "<?xml version=\"1.0\"?>\r\n"
                    + "<!DOCTYPE cross-domain-policy SYSTEM \"/xml/dtds/cross-domain-policy.dtd\">\r\n"
                    + "<cross-domain-policy>\r\n"
                    + "<allow-access-from domain=\"*\" to-ports=\"*\" />\r\n"
                    + "</cross-domain-policy>\0"; // note the NUL

                await ctx.SendAsync(Encoding.Default.GetBytes(Policy));
                await ctx.CloseAsync(CloseReason.ServerShutdown);
                break;
            case PackageType.Client:
                _logger.LogDebug(
                    $"[{ctx.SessionID}] packet header: {package.Client.Header}, remaining: {package.Client.Remaining} bytes, end={package.Client.End}"
                );
                break;
        }

        await Task.CompletedTask;
    }
}
