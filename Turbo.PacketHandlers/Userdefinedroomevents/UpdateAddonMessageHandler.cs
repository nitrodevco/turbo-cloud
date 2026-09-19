using Orleans;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

namespace Turbo.PacketHandlers.Userdefinedroomevents;

public class UpdateAddonMessageHandler(IGrainFactory grainFactory)
    : UpdateWiredMessageHandler<UpdateAddonMessage>(grainFactory);
