using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using cycloid.Info;
using Microsoft.VisualStudio.Threading;

namespace cycloid;

public class OnTrackAdded(OnTrack value) : ValueChangedMessage<OnTrack>(value);

public class CurrentSectionChanged(OnTrack value) : ValueChangedMessage<OnTrack>(value);

public class HoverInfoChanged(InfoPoint value) : ValueChangedMessage<InfoPoint>(value);

public class InfoCategoryVisibleChanged(object sender, bool pois, InfoCategory category, bool oldValue, bool newValue) : PropertyChangedMessage<bool>(sender, null, oldValue, newValue)
{
    public bool Pois => pois;
    public InfoCategory Category => category;
}

public class PointOfInterestCommandParameter
{
    public MapPoint Location { get; set; }
    public InfoType Type { get; set; }
}

partial class ViewModel
{
    private OnTrack _currentSection;
    private OnTrack _currentPoi;
    private InfoPoint _hoverInfo = InfoPoint.Invalid;

    public ObservableCollection<OnTrack> Sections { get; } = [];

    public ObservableCollection<OnTrack> Points { get; } = [];

    public OnTrack CurrentSection
    {
        get => _currentSection;
        set
        {
            if (SetProperty(ref _currentSection, value))
            {
                RemoveCurrentSectionCommand.NotifyCanExecuteChanged();

                StrongReferenceMessenger.Default.Send(new CurrentSectionChanged(value));
            }
        }
    }

    public OnTrack CurrentPoi
    {
        get => _currentPoi;
        set
        {
            OnTrack oldValue = _currentPoi;
            if (SetProperty(ref _currentPoi, value))
            {
                RemoveCurrentPoiCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public InfoPoint HoverInfo
    {
        get => _hoverInfo;
        set
        {
            if (SetProperty(ref _hoverInfo, value))
            {
                StrongReferenceMessenger.Default.Send(new HoverInfoChanged(value));
            }
        }
    }

    public int OnTrackCount => Sections.Count + Points.Count;

    [RelayCommand(CanExecute = nameof(CanAddPointOfInterest))]
    public async Task AddPointOfInterestAsync(PointOfInterestCommandParameter parameter)
    {
        string name = "";
        if (HoverInfo.IsValid)
        {
            parameter = new PointOfInterestCommandParameter { Location = HoverInfo.Location, Type = HoverInfo.Type };
            name = HoverInfo.Name;
        }
        else if (HoverPoint.IsValid)
        {
            parameter = new PointOfInterestCommandParameter { Location = HoverPoint, Type = parameter.Type };
        }

        InfoType type = parameter.Type;
        PointOfInterest pointOfInterest = new()
        {
            Created = DateTime.UtcNow,
            Location = parameter.Location,
            Type = type,
            Category = InfoCategory.Get(type),
            Name = name,
        };

        Mode = pointOfInterest.IsSection ? Modes.Sections : Modes.POIs;

        OnTrack onTrack = await AddOnTrackPointsAsync(pointOfInterest, null);

        StrongReferenceMessenger.Default.Send(new OnTrackAdded(onTrack));

        Track.PointsOfInterest.Add(pointOfInterest);
        pointOfInterest.PropertyChanged += PointOfInterest_PropertyChanged;

        if (pointOfInterest.IsSection)
        {
            CurrentSection = onTrack;
        }
        else
        {
            CurrentPoi = onTrack;
        }

        await SaveTrackAsync();
    }

    private bool CanAddPointOfInterest() 
        => Mode is Modes.Sections or Modes.POIs && Track is not null;

    [RelayCommand(CanExecute = nameof(CanRemoveCurrentSection))]
    public void RemoveCurrentSection() 
        => CurrentSection = DeleteOnTrack(CurrentSection);

    private bool CanRemoveCurrentSection() 
        => Mode is Modes.Sections && Track is not null && CurrentSection is not null && CurrentSection.PointOfInterest.Type != InfoType.Goal;

    [RelayCommand(CanExecute = nameof(CanRemoveCurrentPoi))]
    public void RemoveCurrentPoi() 
        => CurrentPoi = DeleteOnTrack(CurrentPoi);

    private bool CanRemoveCurrentPoi() 
        => Mode is Modes.POIs && Track is not null && CurrentPoi is not null;

    private OnTrack DeleteOnTrack(OnTrack onTrack)
    {
        OnTrack next = onTrack.Remove();
        PointOfInterest pointOfInterest = onTrack.PointOfInterest;

        if (!onTrack.IsOffTrack)
        {
            pointOfInterest.ClearTrackMaskBit(onTrack.MaskBitPosition);
        }

        if (onTrack.IsOffTrack || pointOfInterest.IsTrackMaskZero())
        {
            pointOfInterest.PropertyChanged -= PointOfInterest_PropertyChanged;
            Track.PointsOfInterest.Remove(pointOfInterest);
        }

        SaveTrackAsync().FireAndForget();

        return next;
    }

    private void CreateAllOnTrackPoints()
    {
        if (IsEditMode || Track.Points.IsEmpty)
        {
            return;
        }

        CreateAllOnTrackPointsAsync().FireAndForget();

        async Task CreateAllOnTrackPointsAsync()
        {
            SynchronizationContext ui = SynchronizationContext.Current;

            using (await Track.RouteBuilder.ChangeLock.EnterAsync(default))
            {
                TrackPoint lastTrackPoint = Track.Points.Last();
                new OnTrack(
                    Sections, 
                    lastTrackPoint, 
                    Track.PointsOfInterest.Single(poi => poi.Type == InfoType.Goal), 
                    Track.FilePosition(lastTrackPoint.Distance), 
                    0);
                OnPropertyChanged(nameof(OnTrackCount));

                foreach (PointOfInterest pointOfInterest in Track.PointsOfInterest.Where(poi => poi.Type != InfoType.Goal).ToArray()) 
                    // iterate over a copy to prevent CollectionChangedException
                {
                    await TaskScheduler.Default;
                    await AddOnTrackPointsAsync(pointOfInterest, ui).ConfigureAwait(false);
                }
            }
        }
    }

    private void RemoveAllOnTrackPoints()
    {
        Sections.Clear();
        Points.Clear();

        OnPropertyChanged(nameof(OnTrackCount));
    }

    private async Task<OnTrack> AddOnTrackPointsAsync(PointOfInterest pointOfInterest, SynchronizationContext ui)
    {
        bool isSection = pointOfInterest.IsSection;
        IList<OnTrack> onTracks = isSection ? Sections : Points;

        (TrackPoint TrackPoint, float Distance)[] trackPoints = Track.Points.GetNearPoints(pointOfInterest.Location, maxDistance: isSection ? 50 : 2000, minDistanceDelta: 1000);

        if (ui is not null)
        {
            await ui;
        }

        bool initialize = pointOfInterest.OnTrackCount is null || pointOfInterest.OnTrackCount.Value != trackPoints.Length;

        OnTrack firstOnTrack = null;
        int i = 0;
        foreach ((TrackPoint trackPoint, float distance) in trackPoints)
        {
            if (initialize || pointOfInterest.IsTrackMaskBitSet(i))
            {
                OnTrack onTrack = new(onTracks, trackPoint, pointOfInterest, Track.FilePosition(trackPoint.Distance), i);
                OnPropertyChanged(nameof(OnTrackCount));

                firstOnTrack ??= onTrack;
            }
            i++;
        }

        if (firstOnTrack is null)
        {
            firstOnTrack = new OnTrack(onTracks, pointOfInterest);
            OnPropertyChanged(nameof(OnTrackCount));
        }

        if (initialize)
        {
            pointOfInterest.InitOnTrackCount(i);
        }

        return firstOnTrack;
    }

    private void InitializePointsOfInterest()
    {
        foreach (PointOfInterest pointOfInterest in Track.PointsOfInterest)
        {
            pointOfInterest.PropertyChanged += PointOfInterest_PropertyChanged;
        }
        CreateAllOnTrackPoints();
    }

    private void PointOfInterest_PropertyChanged(object _1, PropertyChangedEventArgs _2)
    {
        SaveTrackAsync().FireAndForget();
    }
}