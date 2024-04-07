using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using cycloid.External;
using cycloid.Info;
using Windows.ApplicationModel.DataTransfer;

namespace cycloid;

public enum Modes
{
    Edit,
    Sections,
    POIs,
    Train,
}

public partial class ViewModel : ObservableObject
{
    private readonly SynchronizationContext _synchronizationContext;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PoisEnabled))]
    [NotifyPropertyChangedFor(nameof(PoisVisible))]
    private Modes _mode;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RecalculateCommand))]
    private Track _track;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RecalculateCommand))]
    private bool _trackIsInitialized;

    [ObservableProperty]
    private TrackPoint _currentPoint = TrackPoint.Invalid;

    [ObservableProperty]
    private TrackPoint _hoverPoint = TrackPoint.Invalid;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProfileHoverPointValuesEnabled))]
    private bool _mapHoverPointValuesEnabled;

    [ObservableProperty]
    private string _status;

    public InfoCache Infos { get; } = new();

    public Strava Strava { get; } = new();

    public Osm Osm { get; } = new();


    public event Action<Modes, Modes> ModeChanged;
    public event Action<Track, Track> TrackChanged;
    public event Action<TrackPoint, TrackPoint> HoverPointChanged;

    public ViewModel()
    {
        _synchronizationContext = SynchronizationContext.Current;

        if (_synchronizationContext is null)
        {
            throw new InvalidOperationException();
        }
    }

    public bool ProfileHoverPointValuesEnabled
    {
        get => !MapHoverPointValuesEnabled;
        set => MapHoverPointValuesEnabled = !value;
    }

    [RelayCommand]
    public Task OpenLocationAsync(MapPoint location)
    {
        DataPackage data = new();
        data.SetText(FormattableString.Invariant($"{location.Latitude},{location.Longitude}"));
        Clipboard.SetContent(data);

        return Task.CompletedTask;

        //return Launcher.LaunchUriAsync(new Uri(FormattableString.Invariant($"https://www.google.com/maps/search/?api=1&query={location.Latitude},{location.Longitude}"))).AsTask();
    }

    partial void OnModeChanged(Modes oldValue, Modes newValue)
    {
        bool isEditMode = newValue == Modes.Edit;
        if (isEditMode != (oldValue == Modes.Edit))
        {
            if (isEditMode)
            {
                RemoveAllOnTrackPoints();
            }
            else
            {
                CreateAllOnTrackPoints();
            }
        }

        ModeChanged?.Invoke(oldValue, newValue);
    }

    partial void OnTrackChanged(Track oldValue, Track newValue)
    {
        if (oldValue is not null)
        {
            DisconnectRouting(oldValue);
        }

        TrackIsInitialized = false;
        CurrentPoint = TrackPoint.Invalid;
        HoverPoint = TrackPoint.Invalid;

        if (newValue is not null)
        {
            ConnectRouting(newValue);
        }

        TrackChanged?.Invoke(oldValue, newValue);
    }

    partial void OnTrackIsInitializedChanged(bool value)
    {
        if (value)
        {
            DownhillCost = Track.RouteBuilder.Profile.DownhillCost;
            DownhillCutoff = Track.RouteBuilder.Profile.DownhillCutoff;
            UphillCost = Track.RouteBuilder.Profile.UphillCost;
            UphillCutoff = Track.RouteBuilder.Profile.UphillCutoff;
            BikerPower = Track.RouteBuilder.Profile.BikerPower;
        }
    }

    partial void OnHoverPointChanged(TrackPoint oldValue, TrackPoint newValue)
    {
        HoverPointChanged?.Invoke(oldValue, newValue);
    }
}