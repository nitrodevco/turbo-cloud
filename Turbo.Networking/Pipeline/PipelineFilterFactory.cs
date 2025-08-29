using System;
using Microsoft.Extensions.DependencyInjection;
using SuperSocket.ProtoBase;
using Turbo.Core.Networking.Session;
using Turbo.Core.Packets.Messages;
using Turbo.Packets.Incoming;

namespace Turbo.Networking.Pipeline;

public class PipelineFilterFactory(IServiceProvider sp) : PipelineFilterFactoryBase<IClientPacket>
{
    protected override IPipelineFilter<IClientPacket> Create() =>
        ActivatorUtilities.CreateInstance<PipelineFilter>(sp);
}
