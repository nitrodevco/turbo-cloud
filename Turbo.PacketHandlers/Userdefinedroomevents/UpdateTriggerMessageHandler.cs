using Orleans;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

namespace Turbo.PacketHandlers.Userdefinedroomevents;

public class UpdateTriggerMessageHandler(IGrainFactory grainFactory)
    : UpdateWiredMessageHandler<UpdateTriggerMessage>(grainFactory);
