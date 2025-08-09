using System;
using System.ComponentModel;
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

    public string Name
    {
        get => _name;
        set { if (_name != value) { _name = value; OnChanged(nameof(Name)); } }
    }

    public string Motto
    {
        get => _motto;
        set { if (_motto != value) { _motto = value; OnChanged(nameof(Motto)); } }
    }

    public string Figure
    {
        get => _figure;
        set { if (_figure != value) { _figure = value; OnChanged(nameof(Figure)); } }
    }

    private void OnChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}