namespace Turbo.Grains.Players;

using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

using Orleans;
using Orleans.Serialization;

/// <summary>
/// Represents the persistent state of a player in the system.
/// </summary>
[GenerateSerializer]
public sealed partial class PlayerState : INotifyPropertyChanged
{
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    [Id(0)]
    private string _name = string.Empty;
    [Id(1)]
    private string _motto = string.Empty;
    [Id(2)]
    private string _figure = string.Empty;
    [Id(3)]
    private bool _initialized;

    /// <summary>
    /// Gets or sets the player's display name.
    /// </summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    /// <summary>
    /// Gets or sets the player's motto or status message.
    /// </summary>
    public string Motto
    {
        get => _motto;
        set => SetProperty(ref _motto, value);
    }

    /// <summary>
    /// Gets or sets the player's avatar figure string.
    /// </summary>
    public string Figure
    {
        get => _figure;
        set => SetProperty(ref _figure, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether gets or sets whether the state has been initialized from external sources.
    /// </summary>
    public bool Initialized
    {
        get => _initialized;
        set => SetProperty(ref _initialized, value);
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;

        OnPropertyChanged(name);

        return true;
    }
}
