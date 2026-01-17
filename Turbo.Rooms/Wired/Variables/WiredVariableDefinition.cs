using System;
using System.Collections.Generic;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Rooms.Wired.Variables;

public sealed class WiredVariableDefinition : IWiredVariableDefinition
{
    private string _name = string.Empty;
    private WiredVariableTargetType _targetType;
    private WiredAvailabilityType _availabilityType;
    private WiredInputSourceType _inputSourceType;
    private WiredVariableFlags _flags;
    private Dictionary<int, string> _textConnectors = [];

    private WiredVariableSnapshot? _snapshot;

    public string Name
    {
        get => _name;
        set
        {
            if (_name == value)
                return;

            _name = value;
            _snapshot = null;
        }
    }

    public WiredVariableTargetType TargetType
    {
        get => _targetType;
        set
        {
            if (_targetType == value)
                return;

            _targetType = value;
            _snapshot = null;
        }
    }

    public WiredAvailabilityType AvailabilityType
    {
        get => _availabilityType;
        set
        {
            if (_availabilityType == value)
                return;

            _availabilityType = value;
            _snapshot = null;
        }
    }

    public WiredInputSourceType InputSourceType
    {
        get => _inputSourceType;
        set
        {
            if (_inputSourceType == value)
                return;

            _inputSourceType = value;
            _snapshot = null;
        }
    }

    public WiredVariableFlags Flags
    {
        get => _flags;
        set
        {
            if (_flags == value)
                return;

            _flags = value;
            _snapshot = null;
        }
    }

    public Dictionary<int, string> TextConnectors
    {
        get => _textConnectors;
        set
        {
            if (_textConnectors == value)
                return;

            _textConnectors = value;
            _snapshot = null;
        }
    }

    public WiredVariableSnapshot GetSnapshot() => _snapshot ??= BuildSnapshot();

    private WiredVariableSnapshot BuildSnapshot()
    {
        var key = new WiredVariableKey(TargetType, Name).GetHashCode();

        return new()
        {
            VariableId = key,
            VariableHash = BuildHash(this, key),
            VariableName = Name,
            AvailabilityType = AvailabilityType,
            InputSourceType = InputSourceType,
            AlwaysAvailable = Flags.Has(WiredVariableFlags.AlwaysAvailable),
            CanCreateAndDelete = Flags.Has(WiredVariableFlags.CanCreateAndDelete),
            HasValue = Flags.Has(WiredVariableFlags.HasValue),
            CanWriteValue = Flags.Has(WiredVariableFlags.CanWriteValue),
            CanInterceptChanges = Flags.Has(WiredVariableFlags.CanInterceptChanges),
            IsInvisible = Flags.Has(WiredVariableFlags.IsInvisible),
            CanReadCreationTime = Flags.Has(WiredVariableFlags.CanReadCreationTime),
            CanReadLastUpdateTime = Flags.Has(WiredVariableFlags.CanReadLastUpdateTime),
            HasTextConnector = Flags.Has(WiredVariableFlags.HasTextConnector),
            TextConnectors = TextConnectors,
            IsStored = Flags.Has(WiredVariableFlags.IsStored),
        };
    }

    private static long BuildHash(WiredVariableDefinition definition, int key)
    {
        ulong hashCode = 0;

        hashCode = CombineXor(hashCode, (ulong)key);
        hashCode = CombineXor(hashCode, (ulong)(int)definition.TargetType);
        hashCode = CombineXor(hashCode, (ulong)(int)definition.AvailabilityType);
        hashCode = CombineXor(hashCode, (ulong)(int)definition.InputSourceType);
        hashCode = CombineXor(hashCode, (ulong)definition.Flags);

        if (definition.TextConnectors is not null)
        {
            foreach (var s in definition.TextConnectors.Values)
                hashCode = CombineXor(hashCode, Fnv1a64(s ?? ""));
        }

        return unchecked((long)hashCode);
    }

    private static ulong Fnv1a64(string s)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;

        ulong hash = offset;

        foreach (var ch in s)
        {
            hash ^= (byte)ch;
            hash *= prime;
            hash ^= (byte)(ch >> 8);
            hash *= prime;
        }

        return hash;
    }

    private static ulong CombineXor(ulong hash, ulong value)
    {
        value ^= value >> 33;
        value *= 0xff51afd7ed558ccdUL;
        value ^= value >> 33;
        value *= 0xc4ceb9fe1a85ec53UL;
        value ^= value >> 33;

        return hash ^ value;
    }
}
