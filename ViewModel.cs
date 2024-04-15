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

public class SetMapCenterMessage(MapPoint location) : ValueChangedMessage<MapPoint>(location);

public class ModeChanged(object sender, Modes oldValue, Modes newValue) : PropertyChangedMessage<Modes>(sender, null, oldValue, newValue);

public class TrackChanged(object sender, Track oldValue, Track newValue) : PropertyChangedMessage<Track>(sender, null, oldValue, newValue);

public class HoverPointChanged(object sender, TrackPoint oldValue, TrackPoint newValue) : PropertyChangedMessage<TrackPoint>(sender, null, oldValue, newValue);

public class SelectionChanged(object sender, Selection oldValue, Selection newValue) : PropertyChangedMessage<Selection>(sender, null, oldValue, newValue);

public partial class ViewModel : ObservableObject,
    IRecipient<TrackComplete>
{
    private readonly SynchronizationContext _ui;

    private Modes _mode;
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
                    if (isEditMode)
                    {
                        RemoveAllOnTrackPoints();
                    }
                    else
                    {
                        CreateAllOnTrackPoints();
                    }
                }

                OnPropertyChanged(nameof(PoisEnabled));
                OnPropertyChanged(nameof(PoisVisible));

                RecalculateCommand.NotifyCanExecuteChanged();
                AddPointOfInterestCommand.NotifyCanExecuteChanged();
                RemoveCurrentSectionCommand.NotifyCanExecuteChanged();

                StrongReferenceMessenger.Default.Send(new ModeChanged(this, oldValue, value));
            }
        }
    }

    private Track _track;
    public Track Track
    {
        get => _track;
        set
        {
            Track oldValue = _track;
            if (SetProperty(ref _track, value))
            {
                DisconnectRouting(oldValue);

                TrackIsInitialized = false;
                CurrentPoint = TrackPoint.Invalid;
                HoverPoint = TrackPoint.Invalid;

                ConnectRouting(value);

                SaveTrackAsCommand.NotifyCanExecuteChanged();
                RecalculateCommand.NotifyCanExecuteChanged();
                AddPointOfInterestCommand.NotifyCanExecuteChanged();
                RemoveCurrentSectionCommand.NotifyCanExecuteChanged();

                StrongReferenceMessenger.Default.Send(new TrackChanged(this, oldValue, value));
            }
        }
    }

    private bool _trackIsInitialized;
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

                RecalculateCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private TrackPoint _currentPoint = TrackPoint.Invalid;
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

    private Selection _currentSelection = Selection.Invalid;
    public Selection CurrentSelection
    {
        get => _currentSelection;
        set
        {
            Selection oldValue = _currentSelection;
            if (SetProperty(ref _currentSelection, value))
            {
                StrongReferenceMessenger.Default.Send(new SelectionChanged(this, oldValue, value));
            }
        }
    }

    private TrackPoint _hoverPoint = TrackPoint.Invalid;
    public TrackPoint HoverPoint
    {
        get => _hoverPoint;
        set
        {
            TrackPoint oldValue = _hoverPoint;
            if (SetProperty(ref _hoverPoint, value))
            {
                StrongReferenceMessenger.Default.Send(new HoverPointChanged(this, oldValue, value));
            }
        }
    }

    private bool _mapHoverPointValuesEnabled;
    public bool MapHoverPointValuesEnabled
    {
        get => _mapHoverPointValuesEnabled;
        set
        {
            if (SetProperty(ref _mapHoverPointValuesEnabled, value))
            {
                OnPropertyChanged(nameof(ProfileHoverPointValuesEnabled));
            }
        }
    }

    private string _status;
    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

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
    }

    public bool ProfileHoverPointValuesEnabled
    {
        get => !MapHoverPointValuesEnabled;
        set => MapHoverPointValuesEnabled = !value;
    }

    public class SelectionStarting(TrackPoint point)
    {
        public TrackPoint Point => point;
    }

    private TrackPoint _selectionCapture = TrackPoint.Invalid;
    public void StartSelection()
    {
        CurrentSelection = Selection.Invalid;
        if (HoverPoint.IsValid)
        {
            StrongReferenceMessenger.Default.Send(new SelectionStarting(HoverPoint));
            _selectionCapture = HoverPoint;
        }
    }

    public void ContinueSelection()
    {
        if (_selectionCapture.IsValid && HoverPoint.IsValid)
        {
            CurrentSelection = _selectionCapture.Distance < HoverPoint.Distance
                ? new Selection(_selectionCapture, HoverPoint)
                : new Selection(HoverPoint, _selectionCapture);
        }
    }

    public void EndSelection()
    {
        _selectionCapture = TrackPoint.Invalid;
        if (CurrentSelection.End.Distance - CurrentSelection.Start.Distance < 10)
        {
            CurrentSelection = Selection.Invalid;
        }
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

    void IRecipient<TrackComplete>.Receive(TrackComplete message)
    {
        RemoveAllOnTrackPoints();
        InitializePointsOfInterest();
        TrackIsInitialized = true;
    }
}