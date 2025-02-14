using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
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
    private (Track.Index Start, Track.Index End, MapPoint NorthWest, MapPoint SouthEast)[] _boundingBoxes;

    public ObservableCollection<OnTrack> Sections { get; } = [];

    public ObservableCollection<OnTrack> Points { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveCurrentSectionCommand))]
    public partial OnTrack CurrentSection { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveCurrentPoiCommand))]
    public partial OnTrack CurrentPoi { get; set; }

    [ObservableProperty]
    public partial InfoPoint HoverInfo { get; set; } = InfoPoint.Invalid;

    partial void OnCurrentSectionChanged(OnTrack value)
    {
        StrongReferenceMessenger.Default.Send(new CurrentSectionChanged(value));
    }

    partial void OnHoverInfoChanged(InfoPoint value)
    {
        StrongReferenceMessenger.Default.Send(new HoverInfoChanged(value));
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

        CalculateBoundingBoxes();
        CreateAllOnTrackPointsAsync().FireAndForget();

        async Task CreateAllOnTrackPointsAsync()
        {
            SynchronizationContext ui = SynchronizationContext.Current;

            using (await Track.RouteBuilder.ChangeLock.EnterAsync(default))
            {
                TrackPoint lastTrackPoint = Track.Points.Last();
                _ = new OnTrack(
                    Sections, 
                    lastTrackPoint, 
                    Track.PointsOfInterest.Single(poi => poi.Type == InfoType.Goal), 
                    Track.FilePosition(lastTrackPoint.Distance), 
                    0);
                OnPropertyChanged(nameof(OnTrackCount));

                // iterate over a copy to prevent CollectionChangedException
                foreach (PointOfInterest pointOfInterest in Track.PointsOfInterest.Where(poi => poi.Type != InfoType.Goal).ToArray()) 
                {
                    await TaskScheduler.Default;
                    await AddOnTrackPointsAsync(pointOfInterest, ui).ConfigureAwait(false);
                }
            }

            ExportCommand.NotifyCanExecuteChanged();
        }
    }

    private void RemoveAllOnTrackPoints()
    {
        Sections.Clear();
        Points.Clear();
        ClearBoundingBoxes();

        OnPropertyChanged(nameof(OnTrackCount));
        ExportCommand.NotifyCanExecuteChanged();
    }


    private IEnumerable<TrackPoint> GetTrackPoints(PointOfInterest pointOfInterest)
    {
        float maxDistance = pointOfInterest.IsSection ? 50 : 2_000;
        Track.Index? startIndex = null;
        for (int i = 0; i < _boundingBoxes.Length; i++)
        {
            (Track.Index start, Track.Index end, MapPoint northWest, MapPoint southEast) = _boundingBoxes[i];
            bool isRelevant = IsRelevant(northWest, southEast);
            if (isRelevant)
            {
                startIndex ??= start;
            }
            if (startIndex is not null && (!isRelevant || i == _boundingBoxes.Length - 1))
            {
                foreach ((TrackPoint point, _) in Track.Points.GetNearPoints(pointOfInterest.Location, startIndex.Value, end, maxDistance, minDistanceDelta: 1_000))
                {
                    yield return point;
                }
                startIndex = null;
            }
        }

        bool IsRelevant(MapPoint northWest, MapPoint southEast)
        {
            const double latitudeDistance = GeoCalculation.EarthRadius * 2 * Math.PI / 4 / 90;

            double latitudeDegree = maxDistance / latitudeDistance;
            MapPoint location = pointOfInterest.Location;
            if (location.Latitude > northWest.Latitude + latitudeDegree ||
                location.Latitude < southEast.Latitude - latitudeDegree)
            {
                return false;
            }
            if (location.Longitude >= northWest.Longitude && location.Longitude <= southEast.Longitude ||
                location.Longitude > southEast.Longitude && GeoCalculation.Distance(location.Latitude, location.Longitude, location.Latitude, southEast.Longitude) <= maxDistance ||
                location.Longitude < northWest.Longitude && GeoCalculation.Distance(location.Latitude, location.Longitude, location.Latitude, northWest.Longitude) <= maxDistance)
            {
                return true;
            }

            return false;
        }
    }

    private async Task<OnTrack> AddOnTrackPointsAsync(PointOfInterest pointOfInterest, SynchronizationContext ui)
    {
        TrackPoint[] trackPoints = GetTrackPoints(pointOfInterest).ToArray();

        if (ui is not null)
        {
            await ui;
        }

        bool initialize = pointOfInterest.OnTrackCount is null || pointOfInterest.OnTrackCount.Value != trackPoints.Length;

        IList<OnTrack> onTracks = pointOfInterest.IsSection ? Sections : Points;
        OnTrack firstOnTrack = null;
        int i = 0;
        foreach (TrackPoint trackPoint in trackPoints)
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

    private void ClearBoundingBoxes()
    {
        _boundingBoxes = null;
    }

    private void CalculateBoundingBoxes()
    {
        const int bucketSize = 500;

        if (Track is null)
        {
            return;
        }

        // TODO 0-Meridian / Equator

        _boundingBoxes = new (Track.Index, Track.Index, MapPoint, MapPoint)[(Track.Points.Count - 1) / bucketSize + 1];
        int i = 0;
        Track.Index startIndex = default;
        Track.Index endIndex = default;
        float north = 0, west = 180, south = 90, east = 0;
        foreach ((MapPoint Location, Track.Index Index) point in Track.Points.EnumerateWithIndex())
        {
            if (++i % bucketSize == 0)
            {
                _boundingBoxes[i / bucketSize - 1] = (startIndex, endIndex, new MapPoint(north, west), new MapPoint(south, east));
                startIndex = point.Index;
                (north, west, south, east) = (point.Location.Latitude, point.Location.Longitude, point.Location.Latitude, point.Location.Longitude);
            }
            else
            {
                north = Math.Max(north, point.Location.Latitude);
                south = Math.Min(south, point.Location.Latitude);
                east = Math.Max(east, point.Location.Longitude);
                west = Math.Min(west, point.Location.Longitude);
            }
            endIndex = point.Index;
        }
        _boundingBoxes[^1] = (startIndex, endIndex, new MapPoint(north, west), new MapPoint(south, east));
    }
}