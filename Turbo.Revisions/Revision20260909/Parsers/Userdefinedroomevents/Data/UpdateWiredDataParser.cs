using System;
using System.Collections.Generic;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Revisions.Revision20260909.Parsers.Userdefinedroomevents.Data;

internal abstract class UpdateWiredDataParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var id = packet.PopInt();

        var intParams = packet.PopList(bytesPerItem: 4, p => p.PopInt());

        var stringParam = packet.PopString();

        var stuffIds = packet.PopList(bytesPerItem: 4, p => p.PopInt());

        var definitionSpecifics = ParseSpecifics(packet, GetRequiredDefinitionSpecifics());

        var furniSources = packet.PopList<WiredFurniSourceType[]>(
            bytesPerItem: 4,
            p => [WiredFurniSourceTypeExtensions.FromProtocolId((WiredSourceType)p.PopInt())]
        );

        var userSources = packet.PopList<WiredPlayerSourceType[]>(
            bytesPerItem: 4,
            p => [WiredPlayerSourceTypeExtensions.FromProtocolId((WiredSourceType)p.PopInt())]
        );

        var variableIds = packet.PopList(bytesPerItem: 2, p => p.PopString());

        var typeSpecifics = ParseSpecifics(packet, GetRequiredTypeSpecifics());

        var stuffIds2 = packet.PopList(bytesPerItem: 4, p => p.PopInt());

        var message = (UpdateWiredMessage)Activator.CreateInstance(UpdateMessageType)!;

        return message with
        {
            Id = id,
            IntParams = intParams,
            StringParam = stringParam,
            StuffIds = stuffIds,
            StuffIds2 = stuffIds2,
            DefinitionSpecifics = definitionSpecifics,
            FurniSources = furniSources,
            PlayerSources = userSources,
            VariableIds = variableIds,
            TypeSpecifics = typeSpecifics,
        };
    }

    public virtual Type UpdateMessageType => typeof(UpdateWiredMessage);

    public virtual List<object> GetRequiredDefinitionSpecifics() => [];

    public virtual List<object> GetRequiredTypeSpecifics() => [];

    private List<object> ParseSpecifics(IClientPacket packet, List<object> requiredSpecifics)
    {
        var specifics = new List<object>();

        foreach (var specific in requiredSpecifics)
        {
            if (specific is int)
                specifics.Add(packet.PopInt());
            else if (specific is string)
                specifics.Add(packet.PopString());
            else if (specific is bool)
                specifics.Add(packet.PopBoolean());
            else if (specific is byte)
                specifics.Add(packet.PopByte());
        }

        return specifics;
    }
}
