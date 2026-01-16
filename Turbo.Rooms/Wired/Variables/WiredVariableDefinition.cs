using System.Collections.Generic;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Rooms.Wired.Variables;

public sealed class WiredVariableDefinition : IWiredVariableDefinition
{
    private string _key = string.Empty;
    private WiredVariableTargetType _target;
    private WiredAvailabilityType _availabilityType;
    private WiredInputSourceType _inputSourceType;
    private WiredVariableFlags _flags;
    private List<string> _textConnectors = [];

    private WiredVariableSnapshot? _snapshot;
    private bool _isDirty = true;

    public string Key
    {
        get => _key;
        set
        {
            if (_key == value)
                return;

            _key = value;
            _isDirty = true;
        }
    }

    public WiredVariableTargetType Target
    {
        get => _target;
        set
        {
            if (_target == value)
                return;

            _target = value;
            _isDirty = true;
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
            _isDirty = true;
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
            _isDirty = true;
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
            _isDirty = true;
        }
    }

    public List<string> TextConnectors
    {
        get => _textConnectors;
        set
        {
            if (_textConnectors == value)
                return;

            _textConnectors = value;
            _isDirty = true;
        }
    }

    public WiredVariableSnapshot GetSnapshot()
    {
        if (_snapshot is not null && !_isDirty)
            return _snapshot;

        _isDirty = false;

        long hashCode = 0;

        hashCode ^= Key.GetHashCode();
        hashCode ^= Target.GetHashCode();
        hashCode ^= AvailabilityType.GetHashCode();
        hashCode ^= InputSourceType.GetHashCode();
        hashCode ^= Flags.GetHashCode();
        hashCode ^= TextConnectors.GetHashCode();

        _snapshot = new()
        {
            VariableHash = hashCode,
            VariableKey = Key,
            AvailabilityType = AvailabilityType,
            VariableType = InputSourceType,
            AlwaysAvailable = Flags.Has(WiredVariableFlags.AlwaysAvailable),
            CanCreateAndDelete = Flags.Has(WiredVariableFlags.CanCreateAndDelete),
            HasValue = Flags.Has(WiredVariableFlags.HasValue),
            CanWriteValue = Flags.Has(WiredVariableFlags.CanWriteValue),
            CanInterceptChanges = Flags.Has(WiredVariableFlags.CanInterceptChanges),
            IsInvisible = Flags.Has(WiredVariableFlags.IsInvisible),
            CanReadCreationTime = Flags.Has(WiredVariableFlags.CanReadCreationTime),
            CanReadLastUpdateTime = Flags.Has(WiredVariableFlags.CanReadLastUpdateTime),
            HasTextConnector = Flags.Has(WiredVariableFlags.HasTextConnector),
            TextConnector = Flags.Has(WiredVariableFlags.HasTextConnector) ? ["default"] : [],
            IsStored = Flags.HasFlag(WiredVariableFlags.IsStored),
        };

        return _snapshot;
    }

    public override int GetHashCode() => (int)GetSnapshot().VariableHash;
}
