using Orleans;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

namespace Turbo.PacketHandlers.Userdefinedroomevents;

public class UpdateVariableMessageHandler(IGrainFactory grainFactory)
    : UpdateWiredMessageHandler<UpdateVariableMessage>(grainFactory);
