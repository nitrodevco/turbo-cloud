using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Orleans;
using Orleans.Serialization;

namespace Turbo.Grains.Players;

[GenerateSerializer]
public sealed partial class PlayerState : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    [Id(0)] private string _name = string.Empty;
    [Id(1)] private string _motto = string.Empty;
    [Id(2)] private string _figure = string.Empty;
    [Id(3)] private bool _initialized;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Motto
    {
        get => _motto;
        set => SetProperty(ref _motto, value);
    }

    public string Figure
    {
        get => _figure;
        set => SetProperty(ref _figure, value);
    }

    public bool Initialized
    {
        get => _initialized;
        set => SetProperty(ref _initialized, value);
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;

        field = value;

        OnPropertyChanged(name);

        return true;
    }
}