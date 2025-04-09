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

public class CurrentPointChanged(TrackPoint value) : ValueChangedMessage<TrackPoint>(value);

public partial class ViewModel : ObservableObject,
    IRecipient<TrackComplete>
{
    private readonly SynchronizationContext _ui;

    public InfoCache Infos { get; } = new();

    public Strava Strava { get; } = new();

    public Osm Osm { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddPointOfInterestCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveCurrentSectionCommand))]
    [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
    public partial Modes Mode { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTrack))]
    [NotifyPropertyChangedFor(nameof(CanEditProfile))]
    [NotifyCanExecuteChangedFor(nameof(CompareSessionCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddPointOfInterestCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveCurrentSectionCommand))]
    [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    public partial Track Track { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TrackName))]
    public partial TrackListItem TrackItem { get; set; }

    private bool _creteFile;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CompareSessionCommand))]
    public partial bool TrackIsInitialized { get; set; }

    [ObservableProperty]
    public partial TrackPoint CurrentPoint { get; set; } = TrackPoint.Invalid;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MapHoverPointVisible))]
    public partial TrackPoint HoverPoint { get; set; } = TrackPoint.Invalid;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MapHoverPointValuesEnabled))]
    [NotifyPropertyChangedFor(nameof(MapHoverPointVisible))]
    public partial bool ProfileHoverPointValuesEnabled { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; }

    public ViewModel()
    {
        _ui = SynchronizationContext.Current ?? throw new InvalidOperationException();

        StrongReferenceMessenger.Default.Register<TrackComplete>(this);
        StrongReferenceMessenger.Default.Register<CompareSessionChanged>(this);
        StrongReferenceMessenger.Default.Register<TrackListItemPinnedChanged>(this);
    }

    partial void OnModeChanged(Modes oldValue, Modes newValue)
    {
        if (oldValue == Modes.Train && IsPlaying)
        {
            PlayCancelCommand.Execute(null);
        }

        bool isEditMode = newValue == Modes.Edit;
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

        StrongReferenceMessenger.Default.Send(new ModeChanged(this, oldValue, newValue));
    }

    partial void OnTrackChanged(Track oldValue, Track newValue)
    {
        oldValue?.CompareSession?.Dispose();
        DisconnectRouting(oldValue);
        oldValue?.ClearViewState();

        Mode = Modes.Edit;
        TrackIsInitialized = false;
        CurrentPoint = TrackPoint.Invalid;
        HoverPoint = TrackPoint.Invalid;
        CurrentSelection = Selection.Invalid;

        ConnectRouting(newValue);

        StrongReferenceMessenger.Default.Send(new TrackChanged(this, oldValue, newValue));
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

    partial void OnCurrentPointChanged(TrackPoint value)
    {
        _getAddressThrottle.Next(value, this);

        StrongReferenceMessenger.Default.Send(new CurrentPointChanged(value));
    }

    partial void OnHoverPointChanged(TrackPoint value)
    {
        StrongReferenceMessenger.Default.Send(new HoverPointChanged(value));
    }

    public bool IsEditMode => Mode == Modes.Edit;

    public bool HasTrack => Track is not null;

    public string TrackName => TrackItem is null ? string.Empty : TrackItem.Name;

    public bool MapHoverPointVisible => HoverPoint.IsValid && (!IsEditMode || ProfileHoverPointValuesEnabled);

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

    public bool CanEditProfile => Track is not null && (Track.Points.IsEmpty || Track.CompareSession is not null);

    [RelayCommand]
    public static Task OpenLocationAsync(MapPoint location)
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