using System.Collections.Generic;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Revisions.Revision20260909;

namespace Turbo.LoadBots.Protocol.Decoders;

/// <summary>
/// A wired box as its editor opens it: what it holds now and what it accepts, which is all a
/// bot needs to send a save the server will take.
/// </summary>
public sealed record WiredBox
{
    public required WiredKind Kind { get; init; }
    public required int ObjectId { get; init; }
    public required int SpriteId { get; init; }
    public required int FurniLimit { get; init; }
    public required IReadOnlyList<int> StuffIds { get; init; }
    public required IReadOnlyList<int> StuffIds2 { get; init; }
    public required string StringParam { get; init; }
    public required IReadOnlyList<int> IntParams { get; init; }
    public required IReadOnlyList<string> VariableIds { get; init; }
    public required IReadOnlyList<int> FurniSources { get; init; }
    public required IReadOnlyList<int> UserSources { get; init; }
    public required int Code { get; init; }

    /// <summary>An action's delay or a condition's quantifier.</summary>
    public int DefinitionInt { get; init; }
    public bool SelectorFilter { get; init; }
    public bool SelectorInvert { get; init; }

    /// <summary>For each furni slot, the sources it may take (protocol ids).</summary>
    public required IReadOnlyList<IReadOnlyList<int>> AllowedFurniSources { get; init; }

    /// <summary>For each user slot, the sources it may take (protocol ids).</summary>
    public required IReadOnlyList<IReadOnlyList<int>> AllowedUserSources { get; init; }
    public required IReadOnlyList<int> DefaultFurniSources { get; init; }
    public required IReadOnlyList<int> DefaultUserSources { get; init; }
    public required bool AllowWallFurni { get; init; }
    public required IReadOnlyList<int> DefaultIntParams { get; init; }
}

/// <summary>
/// Decoders for the wired editor messages, mirroring
/// <c>Turbo.Revisions/Revision20260909/Serializers/Userdefinedroomevents/Data/WiredDataSerializer.cs</c>.
/// </summary>
public static class WiredDecoders
{
    /// <summary>The kind of box each editor message opens, or null for any other message.</summary>
    public static WiredKind? KindOf(int header) =>
        header switch
        {
            MessageComposer.WiredFurniTriggerMessageComposer => WiredKind.Trigger,
            MessageComposer.WiredFurniActionMessageComposer => WiredKind.Action,
            MessageComposer.WiredFurniConditionMessageComposer => WiredKind.Condition,
            MessageComposer.WiredFurniSelectorMessageComposer => WiredKind.Selector,
            MessageComposer.WiredFurniAddonMessageComposer => WiredKind.Addon,
            MessageComposer.WiredFurniVariableMessageComposer => WiredKind.Variable,
            _ => null,
        };

    public static WiredBox Box(WiredKind kind, PacketReader reader)
    {
        var furniLimit = reader.Int();
        var stuffIds = reader.Ints();
        var stuffIds2 = reader.Ints();
        var spriteId = reader.Int();
        var objectId = reader.Int();
        var stringParam = reader.String();
        var intParams = reader.Ints();
        var variableIds = reader.Strings();
        var furniSources = reader.Ints();
        var userSources = reader.Ints();
        var code = reader.Int();

        // Definition specifics: what the kind's save message carries after the picked furni.
        var definitionInt = 0;
        var filter = false;
        var invert = false;

        switch (kind)
        {
            case WiredKind.Action:
            case WiredKind.Condition:
                definitionInt = reader.Int();
                break;
            case WiredKind.Selector:
                filter = reader.Bool();
                invert = reader.Bool();
                break;
            default:
                break;
        }

        _ = reader.Bool(); // advanced mode

        var allowedFurni = SourceLists(reader);
        var allowedUsers = SourceLists(reader);
        var defaultFurni = reader.Ints();
        var defaultUsers = reader.Ints();
        var allowWall = reader.Bool();

        // Type specifics: only a condition has any, its quantifier type and whether it negates.
        if (kind is WiredKind.Condition)
        {
            _ = reader.Byte();
            _ = reader.Bool();
        }

        var contexts = reader.Count();

        for (var i = 0; i < contexts; i++)
            SkipContext(reader);

        var defaultIntParams = reader.Ints();

        return new WiredBox
        {
            Kind = kind,
            ObjectId = objectId,
            SpriteId = spriteId,
            FurniLimit = furniLimit,
            StuffIds = stuffIds,
            StuffIds2 = stuffIds2,
            StringParam = stringParam,
            IntParams = intParams,
            VariableIds = variableIds,
            FurniSources = furniSources,
            UserSources = userSources,
            Code = code,
            DefinitionInt = definitionInt,
            SelectorFilter = filter,
            SelectorInvert = invert,
            AllowedFurniSources = allowedFurni,
            AllowedUserSources = allowedUsers,
            DefaultFurniSources = defaultFurni,
            DefaultUserSources = defaultUsers,
            AllowWallFurni = allowWall,
            DefaultIntParams = defaultIntParams,
        };
    }

    /// <summary>WiredValidationError: the localization key and its parameters.</summary>
    public static string ValidationError(PacketReader reader) => reader.String();

    private static List<IReadOnlyList<int>> SourceLists(PacketReader reader)
    {
        var count = reader.Count();
        var lists = new List<IReadOnlyList<int>>(count);

        for (var i = 0; i < count; i++)
            lists.Add(reader.Ints());

        return lists;
    }

    private static void SkipContext(PacketReader reader)
    {
        var type = (WiredContextType)reader.Int();

        switch (type)
        {
            case WiredContextType.AllVariablesInRoom:
                _ = reader.Int(); // variables hash
                break;
            case WiredContextType.FurniVariableInfo:
            case WiredContextType.UserVariableInfo:
                SkipVariable(reader);
                var holders = reader.Count();
                for (var i = 0; i < holders; i++)
                {
                    _ = reader.Int();
                    _ = reader.Int();
                }
                break;
            case WiredContextType.VariableInfoAndValue:
                SkipVariable(reader);
                _ = reader.Int();
                break;
            default:
                // The serializer writes nothing after the type for the remaining kinds.
                break;
        }
    }

    /// <summary>Skips one variable (<c>WiredVariableSerializer</c>).</summary>
    private static void SkipVariable(PacketReader reader)
    {
        _ = reader.String(); // id
        _ = reader.Int(); // type
        _ = reader.String(); // name
        _ = reader.Int(); // availability
        _ = reader.Int(); // target

        var flags = 0;

        // Nine flags, in WiredVariableFlags order; the ninth says text connectors follow.
        for (var i = 0; i < 9; i++)
        {
            if (reader.Bool())
                flags |= 1 << i;
        }

        if ((flags & (int)WiredVariableFlags.HasTextConnector) == 0)
            return;

        var connectors = reader.Count();

        for (var i = 0; i < connectors; i++)
        {
            _ = reader.Int();
            _ = reader.String();
        }
    }

    public static bool SameInts(IReadOnlyList<int> a, IReadOnlyList<int> b)
    {
        if (a.Count != b.Count)
            return false;

        for (var i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i])
                return false;
        }

        return true;
    }

    public static string Describe(IReadOnlyList<int> values) => $"[{string.Join(',', values)}]";
}
