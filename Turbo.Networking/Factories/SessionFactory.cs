using System;
using DotNetty.Transport.Channels;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Core.Networking.Session;
using Turbo.Networking.Session;

namespace Turbo.Networking.Factories;

public class SessionFactory(IServiceProvider _provider) : ISessionFactory
{
    public ISession CreateSession(IChannelHandlerContext context)
    {
        return ActivatorUtilities.CreateInstance<Turbo.Networking.Session.Session>(_provider, context);
    }
}