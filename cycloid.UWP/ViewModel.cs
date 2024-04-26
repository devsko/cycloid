using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
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

public class BringTrackIntoViewMessage(TrackPoint trackPoint1, TrackPoint? trackPoint2 = null) : ValueChangedMessage<(TrackPoint, TrackPoint)>((trackPoint1, trackPoint2 ?? TrackPoint.Invalid));

public class BringLocationIntoViewMessage(MapPoint location) : ValueChangedMessage<MapPoint>(location);

public class ModeChanged(object sender, Modes oldValue, Modes newValue) : PropertyChangedMessage<Modes>(sender, null, oldValue, newValue);

public class TrackChanged(object sender, Track oldValue, Track newValue) : PropertyChangedMessage<Track>(sender, null, oldValue, newValue);

public class HoverPointChanged(TrackPoint value) : ValueChangedMessage<TrackPoint>(value);

public partial class ViewModel : ObservableObject,
    IRecipient<TrackComplete>
{
    private readonly SynchronizationContext _ui;
    private Modes _mode;
    private Track _track;
    private bool _trackIsInitialized;
    private TrackPoint _currentPoint = TrackPoint.Invalid;
    private TrackPoint _hoverPoint = TrackPoint.Invalid;
    private bool _profileHoverPointValuesEnabled;
    private string _status;

    public InfoCache Infos { get; } = new();

    public Strava Strava { get; } = new();

    public Osm Osm { get; } = new();

    public ViewModel()
    {
        _ui = SynchronizationContext.Current;

        if (_ui is null)
        {
            throw new InvalidOperationException();
        }

        StrongReferenceMessenger.Default.Register<TrackComplete>(this);
        StrongReferenceMessenger.Default.Register<CompareSessionChanged>(this);
    }

    public Modes Mode
    {
        get => _mode;
        set
        {
            Modes oldValue = _mode;
            if (SetProperty(ref _mode, value))
            {
                bool isEditMode = value == Modes.Edit;
                if (isEditMode != (oldValue == Modes.Edit))
                {
                    OnPropertyChanged(nameof(IsEditMode));
                    OnPropertyChanged(nameof(MapHoverPointVisible));
                    OnPropertyChanged(nameof(MapHoverPointValuesEnabled));
                    OnPropertyChanged(nameof(PoisEnabled));
                    OnPropertyChanged(nameof(PoisVisible));

                    CompareSessionCommand.NotifyCanExecuteChanged();
                    DeleteSelectionCommand.NotifyCanExecuteChanged();
                    PasteWayPointsCommand.NotifyCanExecuteChanged();

                    if (isEditMode)
                    {
                        RemoveAllOnTrackPoints();
                    }
                    else
                    {
                        CreateAllOnTrackPoints();
                    }
                }

                AddPointOfInterestCommand.NotifyCanExecuteChanged();
                RemoveCurrentSectionCommand.NotifyCanExecuteChanged();

                StrongReferenceMessenger.Default.Send(new ModeChanged(this, oldValue, value));
            }
        }
    }

    public bool IsEditMode => Mode == Modes.Edit;

    public Track Track
    {
        get => _track;
        set
        {
            Track oldValue = _track;
            if (SetProperty(ref _track, value))
            {
                oldValue?.CompareSession?.Dispose();
                DisconnectRouting(oldValue);
                oldValue?.ClearViewState();

                Mode = Modes.Edit;
                TrackIsInitialized = false;
                CurrentPoint = TrackPoint.Invalid;
                HoverPoint = TrackPoint.Invalid;
                CurrentSelection = Selection.Invalid;

                ConnectRouting(value);

                OnPropertyChanged(nameof(HasTrack));
                OnPropertyChanged(nameof(CanEditProfile));

                SaveTrackAsCommand.NotifyCanExecuteChanged();
                CompareSessionCommand.NotifyCanExecuteChanged();
                AddPointOfInterestCommand.NotifyCanExecuteChanged();
                RemoveCurrentSectionCommand.NotifyCanExecuteChanged();
                ExportWahooCommand.NotifyCanExecuteChanged();

                StrongReferenceMessenger.Default.Send(new TrackChanged(this, oldValue, value));
            }
        }
    }

    public bool HasTrack => Track is not null;

    public bool TrackIsInitialized
    {
        get => _trackIsInitialized;
        set
        {
            if (SetProperty(ref _trackIsInitialized, value))
            {
                if (value)
                {
                    DownhillCost = Track.RouteBuilder.Profile.DownhillCost;
                    DownhillCutoff = Track.RouteBuilder.Profile.DownhillCutoff;
                    UphillCost = Track.RouteBuilder.Profile.UphillCost;
                    UphillCutoff = Track.RouteBuilder.Profile.UphillCutoff;
                    BikerPower = Track.RouteBuilder.Profile.BikerPower;
                }

                CompareSessionCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public TrackPoint CurrentPoint
    {
        get => _currentPoint;
        set
        {
            if (SetProperty(ref _currentPoint, value))
            {
                _getAddressThrottle.Next(value, this);
            }
        }
    }

    public TrackPoint HoverPoint
    {
        get => _hoverPoint;
        set
        {
            if (SetProperty(ref _hoverPoint, value))
            {
                OnPropertyChanged(nameof(MapHoverPointVisible));

                StrongReferenceMessenger.Default.Send(new HoverPointChanged(value));
            }
        }
    }

    public bool MapHoverPointVisible => HoverPoint.IsValid && (!IsEditMode || ProfileHoverPointValuesEnabled);

    public bool ProfileHoverPointValuesEnabled
    {
        get => _profileHoverPointValuesEnabled;
        set
        {
            if (SetProperty(ref _profileHoverPointValuesEnabled, value))
            {
                OnPropertyChanged(nameof(MapHoverPointValuesEnabled));
                OnPropertyChanged(nameof(MapHoverPointVisible));
            }
        }
    }

    public bool MapHoverPointValuesEnabled
    {
        get => !ProfileHoverPointValuesEnabled && !IsEditMode;
        set
        {
            if (!IsEditMode)
            {
                ProfileHoverPointValuesEnabled = !value;
            }
        }
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public bool CanEditProfile => Track is not null && (Track.Points.IsEmpty || Track.CompareSession is not null);

    [RelayCommand]
    public Task OpenLocationAsync(MapPoint location)
    {
        DataPackage data = new();
        data.SetText(FormattableString.Invariant($"{location.Latitude},{location.Longitude}"));
        Clipboard.SetContent(data);

        return Task.CompletedTask;

        //return Launcher.LaunchUriAsync(new Uri(FormattableString.Invariant($"https://www.google.com/maps/search/?api=1&query={location.Latitude},{location.Longitude}"))).AsTask();
    }

    void IRecipient<TrackComplete>.Receive(TrackComplete message)
    {
        RemoveAllOnTrackPoints();
        InitializePointsOfInterest();
        TrackIsInitialized = true;
    }
}