using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using cycloid.Info;
using Microsoft.VisualStudio.Threading;

namespace cycloid;

public class OnTrackAdded(OnTrack onTrack)
{
    public OnTrack OnTrack => onTrack;
}

public class CurrentSectionChanged(object sender, OnTrack oldValue, OnTrack newValue) : PropertyChangedMessage<OnTrack>(sender, null, oldValue, newValue);

public class HoverInfoChanged(object sender, InfoPoint oldValue, InfoPoint newValue) : PropertyChangedMessage<InfoPoint>(sender, null, oldValue, newValue);

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
    public OnTrack CurrentSection
    {
        get => _currentSection;
        set
        {
            OnTrack oldValue = _currentSection;
            if (SetProperty(ref _currentSection, value))
            {
                RemoveCurrentSectionCommand.NotifyCanExecuteChanged();

                StrongReferenceMessenger.Default.Send(new CurrentSectionChanged(this, oldValue, value));
            }
        }
    }

    private OnTrack _currentPoi;
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

    private InfoPoint _hoverInfo = InfoPoint.Invalid;
    public InfoPoint HoverInfo
    {
        get => _hoverInfo;
        set
        {
            InfoPoint oldValue = _hoverInfo;
            if (SetProperty(ref _hoverInfo, value))
            {
                StrongReferenceMessenger.Default.Send(new HoverInfoChanged(this, oldValue, value));
            }
        }
    }

    public ObservableCollection<OnTrack> Sections { get; } = [];

    public ObservableCollection<OnTrack> Points { get; } = [];

    public int OnTrackCount => Sections.Count + Points.Count;

    [RelayCommand(CanExecute = nameof(CanAddPointOfInterest))]
    public async Task AddPointOfInterestAsync(PointOfInterestCommandParameter parameter)
    {
        bool convertInfo = HoverInfo.IsValid;

        InfoType type = convertInfo ? HoverInfo.Type : parameter.Type;
        PointOfInterest pointOfInterest = new()
        {
            Created = DateTime.UtcNow,
            Location = convertInfo ? HoverInfo.Location : parameter.Location,
            Type = type,
            Category = InfoCategory.Get(type),
            Name = convertInfo ? HoverInfo.Name : "",
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
    {
        return Mode is Modes.Sections or Modes.POIs && Track is not null;
    }

    [RelayCommand(CanExecute = nameof(CanRemoveCurrentSection))]
    public void RemoveCurrentSection()
    {
        CurrentSection = DeleteOnTrack(CurrentSection);
    }

    private bool CanRemoveCurrentSection()
    {
        return Mode is Modes.Sections && Track is not null && CurrentSection is not null && CurrentSection.PointOfInterest.Type != InfoType.Goal;
    }

    [RelayCommand(CanExecute = nameof(CanRemoveCurrentPoi))]
    public void RemoveCurrentPoi()
    {
        CurrentPoi = DeleteOnTrack(CurrentPoi);
    }

    private bool CanRemoveCurrentPoi()
    {
        return Mode is Modes.POIs && Track is not null && CurrentPoi is not null;
    }

    private OnTrack DeleteOnTrack(OnTrack onTrack)
    {
        PointOfInterest pointOfInterest = onTrack.PointOfInterest;
        bool isSection = pointOfInterest.IsSection;
        IList<OnTrack> onTracks = isSection ? Sections : Points;

        int index = onTracks.IndexOf(onTrack);
        int nextIndex = index == onTracks.Count - 1 ? index - 1 : index + 1;
        OnTrack nextOnTrack = nextIndex < 0 ? null : onTracks[nextIndex];

        if (isSection)
        {
            nextOnTrack.Values += onTrack.Values;
        }

        onTracks.RemoveAt(index);
        
        if (!onTrack.IsOffTrack)
        {
            pointOfInterest.ClearTrackMaskBit(onTrack.TrackMaskBitPosition);
        }

        if (onTrack.IsOffTrack || pointOfInterest.IsTrackMaskZero())
        {
            Track.PointsOfInterest.Remove(pointOfInterest);
        }

        SaveTrackAsync().FireAndForget();

        return nextOnTrack;
    }

    private void CreateAllOnTrackPoints()
    {
        if (Mode == Modes.Edit)
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
                Sections.Add(new OnTrack(Sections)
                {
                    TrackPoint = lastTrackPoint,
                    PointOfInterest = Track.PointsOfInterest.Single(poi => poi.Type == InfoType.Goal),
                    TrackFilePosition = Track.FilePosition(lastTrackPoint.Distance),
                    Values = lastTrackPoint.Values,
                });

                OnPropertyChanged(nameof(OnTrackCount));

                foreach (PointOfInterest pointOfInterest in Track.PointsOfInterest.Where(poi => poi.Type != InfoType.Goal))
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
        bool offTrack = true;
        foreach ((TrackPoint trackPoint, float distance) in trackPoints)
        {
            if (initialize || pointOfInterest.IsTrackMaskBitSet(i))
            {
                offTrack = false;

                (OnTrack next, int index) = onTracks
                    .Select((onTrack, index) => (onTrack, index))
                    .FirstOrDefault(tuple => tuple.onTrack.IsOffTrack || tuple.onTrack.TrackPoint.Distance >= trackPoint.Distance);

                TrackPoint.CommonValues values = default;
                if (isSection)
                {
                    Debug.Assert(next is not null, "Where is the goal POI?");

                    values = trackPoint.Values;
                    if (index > 0)
                    {
                        values -= Sections[index - 1].TrackPoint.Values;
                    }
                    next.Values -= values;
                }
                else if (next is null)
                {
                    index = onTracks.Count;
                }

                OnTrack onTrack = new(onTracks)
                {
                    TrackPoint = trackPoint,
                    PointOfInterest = pointOfInterest,
                    TrackFilePosition = Track.FilePosition(trackPoint.Distance),
                    TrackMaskBitPosition = i,
                    Values = values,
                };

                firstOnTrack ??= onTrack;
                onTracks.Insert(index, onTrack);

                OnPropertyChanged(nameof(OnTrackCount));
            }
            i++;
        }

        if (offTrack)
        {
            onTracks.Add(firstOnTrack = OnTrack.CreateOffTrack(pointOfInterest, onTracks));

            OnPropertyChanged(nameof(OnTrackCount));
        }

        if (initialize)
        {
            pointOfInterest.InitOnTrackCount(i);
        }

        return firstOnTrack;
    }

    private void PointOfInterest_PropertyChanged(object _1, PropertyChangedEventArgs _2)
    {
        SaveTrackAsync().FireAndForget();
    }
}