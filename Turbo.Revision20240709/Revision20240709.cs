using System;
using System.Collections.Generic;
using Turbo.Networking.Abstractions.Revisions;
using Turbo.Packets.Abstractions;
using Turbo.Packets.Outgoing.Handshake;
using Turbo.Packets.Outgoing.Users;
using Turbo.Revision20240709.Parsers.Handshake;
using Turbo.Revision20240709.Serializers.Handshake;
using Turbo.Revision20240709.Serializers.Users;

namespace Turbo.Revision20240709;

public class Revision20240709 : IRevision
{
    public string Revision => "WIN63-202407091256-704579380";

    public IDictionary<int, IParser> Parsers { get; } =
        new Dictionary<int, IParser>
        {
            {
                (int)MessageEvent.CompleteDiffieHandshakeMessageEvent,
                new CompleteDiffieHandshakeMessageParser()
            },
            { (int)MessageEvent.DisconnectMessageEvent, new DisconnectMessageParser() },
            { (int)MessageEvent.InfoRetrieveMessageEvent, new InfoRetrieveMessageParser() },
            {
                (int)MessageEvent.InitDiffieHandshakeMessageEvent,
                new InitDiffieHandshakeMessageParser()
            },
            { (int)MessageEvent.SSOTicketMessageEvent, new SSOTicketMessageParser() },
            { (int)MessageEvent.UniqueIDMessageEvent, new UniqueIdMessageParser() },
            { (int)MessageEvent.VersionCheckMessageEvent, new VersionCheckMessageParser() },
        };

    public IDictionary<Type, ISerializer> Serializers { get; } =
        new Dictionary<Type, ISerializer>
        {
            {
                typeof(AuthenticationOKMessage),
                new AuthenticationOKMessageSerializer(
                    MessageComposer.AuthenticationOKMessageComposer
                )
            },
            {
                typeof(CompleteDiffieHandshakeComposer),
                new CompleteDiffieHandshakeSerializer(
                    MessageComposer.CompleteDiffieHandshakeComposer
                )
            },
            {
                typeof(InitDiffieHandshakeComposer),
                new InitDiffieHandshakeSerializer(MessageComposer.InitDiffieHandshakeComposer)
            },
            {
                typeof(IsFirstLoginOfDayMessage),
                new IsFirstLoginOfDaySerializer(MessageComposer.IsFirstLoginOfDayComposer)
            },
            {
                typeof(NoobnessLevelMessage),
                new NoobnessLevelSerializer(MessageComposer.NoobnessLevelMessageComposer)
            },
            {
                typeof(UserObjectMessage),
                new UserObjectMessageSerializer(MessageComposer.UserObjectComposer)
            },
            {
                typeof(UserRightsMessage),
                new UserRightsMessageSerializer(MessageComposer.UserRightsMessageComposer)
            },
            {
                typeof(ScrSendUserInfoMessage),
                new ScrSendUserInfoSerializer(MessageComposer.ScrSendUserInfoComposer)
            },
        };
}
