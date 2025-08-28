using System;
using Microsoft.Extensions.DependencyInjection;
using SuperSocket.Server;
using SuperSocket.Server.Abstractions.Session;
using Turbo.Core.Configuration;
using Turbo.Core.Networking.Session;

namespace Turbo.Networking.Session;

public class SessionContextFactory(IServiceProvider sp) : ISessionFactory
{
    public Type SessionType => typeof(SessionContext);

    public IAppSession Create() => ActivatorUtilities.CreateInstance<SessionContext>(sp);
}
