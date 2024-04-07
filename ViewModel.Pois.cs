using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using cycloid.Info;
using Microsoft.VisualStudio.Threading;

namespace cycloid;

public class PointOfInterestCommandParameter
{
    public MapPoint Location { get; set; }
    public InfoType Type { get; set; }
}

partial class ViewModel
{
    [ObservableProperty]
    private OnTrack _currentSection;

    [ObservableProperty]
    private InfoPoint _hoverInfo = InfoPoint.Invalid;

    public ObservableCollection<OnTrack> Sections { get; } = [];

    public ObservableCollection<OnTrack> Points { get; } = [];

    public event Action<OnTrack> OnTrackAdded;
    public event Action<OnTrack, OnTrack> CurrentSectionChanged;
    public event Action<InfoPoint, InfoPoint> HoverInfoChanged;

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

        await AddOnTrackPointsAsync(pointOfInterest, null);

        Track.PointsOfInterest.Add(pointOfInterest);
        pointOfInterest.PropertyChanged += PointOfInterest_PropertyChanged;

        OnTrack onTrack = (pointOfInterest.IsSection ? Sections : Points).First(onTrack => onTrack.PointOfInterest == pointOfInterest);
        if (pointOfInterest.IsSection)
        {
            CurrentSection = onTrack;
        }

        OnTrackAdded?.Invoke(onTrack);

        await SaveTrackAsync();
    }

    private bool CanAddPointOfInterest()
    {
        return Mode is Modes.Sections or Modes.POIs && Track is not null;
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

    private async Task AddOnTrackPointsAsync(PointOfInterest pointOfInterest, SynchronizationContext ui)
    {
        bool isSection = pointOfInterest.IsSection;
        IList<OnTrack> onTracks = isSection ? Sections : Points;

        (TrackPoint TrackPoint, float Distance)[] trackPoints = Track.Points.GetNearPoints(pointOfInterest.Location, maxCrossTrackDistance: isSection ? 50 : 2000, minDistanceDelta: 1500);

        if (ui is not null)
        {
            await ui;
        }

        bool initialize = pointOfInterest.OnTrackCount is null || pointOfInterest.OnTrackCount.Value != trackPoints.Length;

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

                onTracks.Insert(index, new OnTrack(onTracks)
                {
                    TrackPoint = trackPoint,
                    PointOfInterest = pointOfInterest,
                    TrackFilePosition = Track.FilePosition(trackPoint.Distance),
                    TrackMaskBitPosition = i,
                    Values = values,
                });

                OnPropertyChanged(nameof(OnTrackCount));
            }
            i++;
        }

        if (offTrack)
        {
            onTracks.Add(OnTrack.CreateOffTrack(pointOfInterest, onTracks));

            OnPropertyChanged(nameof(OnTrackCount));
        }

        if (initialize)
        {
            pointOfInterest.InitOnTrackCount(i);
        }
    }

    partial void OnCurrentSectionChanged(OnTrack oldValue, OnTrack newValue)
    {
        CurrentSectionChanged?.Invoke(oldValue, newValue);
    }

    partial void OnHoverInfoChanged(InfoPoint oldValue, InfoPoint newValue)
    {
        HoverInfoChanged?.Invoke(oldValue, newValue);
    }

    private void PointOfInterest_PropertyChanged(object sender, PropertyChangedEventArgs _)
    {
        SaveTrackAsync().FireAndForget();
    }
}