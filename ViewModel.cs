using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using cycloid.External;
using cycloid.Info;
using cycloid.Serizalization;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Geolocation;
using Windows.Storage.Streams;

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

public class HoverPointChanged(TrackPoint value) : ValueChangedMessage<TrackPoint>(value);

public class SelectionChanged(Selection value) : ValueChangedMessage<Selection>(value);

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
                    OnPropertyChanged(nameof(IsEditMode));
                    OnPropertyChanged(nameof(PoisEnabled));
                    OnPropertyChanged(nameof(PoisVisible));

                    RecalculateCommand.NotifyCanExecuteChanged();

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
                CurrentSelection = Selection.Invalid;

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
            if (SetProperty(ref _currentSelection, value))
            {
                CopySelectionCommand.NotifyCanExecuteChanged();
                StrongReferenceMessenger.Default.Send(new SelectionChanged(value));
            }
        }
    }

    private TrackPoint _hoverPoint = TrackPoint.Invalid;
    public TrackPoint HoverPoint
    {
        get => _hoverPoint;
        set
        {
            if (SetProperty(ref _hoverPoint, value))
            {
                StrongReferenceMessenger.Default.Send(new HoverPointChanged(value));
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

    public bool IsEditMode => Mode == Modes.Edit;

    public bool ProfileHoverPointValuesEnabled
    {
        get => !MapHoverPointValuesEnabled;
        set => MapHoverPointValuesEnabled = !value;
    }

    private TrackPoint _selectionCapture = TrackPoint.Invalid;

    [RelayCommand(CanExecute = nameof(CanCopySelection))]
    public async Task CopySelectionAsync()
    {
        (WayPoint[] WayPoints, TrackPoint.CommonValues[] Starts) segments = await Track.Points.GetSegmentStartsAsync(default);
        IEnumerable<WayPoint> wp = segments.WayPoints
            .Zip(segments.Starts, (wayPoint, start) => (WayPoint: wayPoint, Start: start.Distance))
            .SkipWhile(tuple => tuple.Start < CurrentSelection.Start.Distance)
            .TakeWhile(tuple => tuple.Start <= CurrentSelection.End.Distance)
            .Select(tuple => tuple.WayPoint);

        if (CurrentSelection.End.Equals(Track.Points.Last()))
        {
            wp = wp.Append(segments.WayPoints.Last());
        }

        WayPoint[] wayPoints = wp.ToArray();
        if (wayPoints.Length == 0)
        {
            Status = "no waypoints copied.";
            return;
        }

        InMemoryRandomAccessStream stream = new();
        await Serializer.SerializeAsync(stream.GetOutputStreamAt(0).AsStreamForWrite(), wayPoints);
        DataPackage data = new() { RequestedOperation = DataPackageOperation.Copy };
        data.SetData("cycloid/route", stream);
        data.SetText($"{wayPoints.Length} cycloid waypoints\r\n{await GetAddressAsync(new Geopoint(wayPoints[0].Location), shorter: true)} - {await GetAddressAsync(new Geopoint(wayPoints[^1].Location), shorter: true)}");
        Clipboard.SetContent(data);
        Clipboard.Flush();

        Status = $"{wayPoints.Length} waypoints copied.";
    }

    private bool CanCopySelection() => CurrentSelection.IsValid;

    [RelayCommand(CanExecute = nameof(CanPasteWayPoints))]
    public async Task PasteWayPointsAtStartAsync(bool reversed)
    {
        (IEnumerable<WayPoint> wayPoints, int length) = await GetWayPointsFromClipboardAsync(reversed);
        await Track.RouteBuilder.InsertPointsAsync(wayPoints, null);

        Status = $"{length} way points pasted.";
    }

    [RelayCommand(CanExecute = nameof(CanPasteWayPoints))]
    public async Task PasteWayPointsAsync(bool reversed)
    {
        (IEnumerable<WayPoint> wayPoints, int length) = await GetWayPointsFromClipboardAsync(reversed);
        await Track.RouteBuilder.InsertPointsAsync(wayPoints, HoveredWayPoint);

        Status = $"{length} way points pasted.";
    }

    private async Task<(IEnumerable<WayPoint> WayPoints, int Length)> GetWayPointsFromClipboardAsync(bool reversed)
    {
        IRandomAccessStream stream = await Clipboard.GetContent().GetDataAsync("cycloid/route") as IRandomAccessStream;
        WayPoint[] wayPoints = await Serializer.DeserializeAsync(stream.GetInputStreamAt(0).AsStreamForRead());
        IEnumerable<WayPoint> wp = wayPoints;
        if (reversed)
        {
            wp = wp.Reverse();
        }

        return (wp, wayPoints.Length);
    }

    private bool CanPasteWayPoints()
    {
        try
        {
            return Clipboard.GetContent().AvailableFormats.Contains("cycloid/route");
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public bool? GetHoveredSelectionBorder(double toleranceDistance)
    {
        float lowerLimit = HoverPoint.Distance - (float)toleranceDistance;
        float upperLimit = HoverPoint.Distance + (float)toleranceDistance;

        return 
            CurrentSelection.Start.Distance > lowerLimit && CurrentSelection.Start.Distance < upperLimit
            ? true
            : CurrentSelection.End.Distance > lowerLimit && CurrentSelection.End.Distance < upperLimit
            ? false
            : null;
    }

    public void StartSelection(double toleranceDistance)
    {
        if (HoverPoint.IsValid)
        {
            if (CurrentSelection.IsValid)
            {
                switch (GetHoveredSelectionBorder(toleranceDistance))
                {
                    case true:
                        _selectionCapture = CurrentSelection.End;
                        break;
                    case false:
                        _selectionCapture = CurrentSelection.Start;
                        break;
                    case null:
                        CurrentSelection = Selection.Invalid;
                        _selectionCapture = HoverPoint;
                        break;
                }
            }
            else
            {
                _selectionCapture = HoverPoint;
            }
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