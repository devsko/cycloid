using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace cycloid;

public partial class OnTrack : ObservableObject
{
    private readonly IList<OnTrack> _onTracks;
    private readonly TrackPoint _trackPoint;
    private TrackPoint.CommonValues _values;

    public PointOfInterest PointOfInterest { get; set; }

    public bool IsOffTrack { get; set; }
 
    public string TrackFilePosition { get; set; }

    public int MaskBitPosition { get; set; }

    public OnTrack(IList<OnTrack> onTracks, TrackPoint trackPoint, PointOfInterest pointOfInterest, string trackFilePosition, int maskBitPosition)
    {
        _onTracks = onTracks;
        _trackPoint = trackPoint;
        PointOfInterest = pointOfInterest;
        TrackFilePosition = trackFilePosition;
        MaskBitPosition = maskBitPosition;

        int? index = null;
        TrackPoint.CommonValues previousValues = default;
        OnTrack next = null;
        for (int i = 0; i < _onTracks.Count; i++)
        {
            OnTrack onTrack = _onTracks[i];
            if (onTrack.IsOffTrack)
            {
                break;
            }
            if (index is null && onTrack._trackPoint.Distance > _trackPoint.Distance)
            {
                index = i;
            }
            if (onTrack.PointOfInterest.Category == PointOfInterest.Category)
            {
                if (index is null)
                {
                    previousValues = onTrack._trackPoint.Values;
                }
                else
                {
                    next = onTrack;
                    break;
                }
            }
        }

        Values = _trackPoint.Values - previousValues;
        if (next is not null)
        {
            next.Values -= Values;
        }

        _onTracks.Insert(index ?? _onTracks.Count, this);
    }

    public OnTrack(IList<OnTrack> onTracks, PointOfInterest pointOfInterest)
    {
        _onTracks = onTracks;
        PointOfInterest = pointOfInterest;
        IsOffTrack = true;
        TrackFilePosition = "?";
        MaskBitPosition = -1;

        onTracks.Add(this);
    }

    public MapPoint Location => IsOffTrack || !PointOfInterest.IsSection ? PointOfInterest.Location : _trackPoint;

    public float? Distance => IsOffTrack ? null : Values.Distance;

    public float? Ascent => IsOffTrack ? null : Values.Ascent;

    public float? Descent => IsOffTrack ? null : Values.Descent;

    public TimeSpan? Time => IsOffTrack ? null : Values.Time;

    public float? Speed => IsOffTrack ? null : Values.Distance / 1000 / (float)Values.Time.TotalHours;

    public TrackPoint TrackPoint => IsOffTrack ? throw new InvalidOperationException() : _trackPoint;

    public string Name
    {
        get => PointOfInterest.Name;
        set
        {
            if (!string.Equals(Name, value))
            {
                PointOfInterest.Name = value;
                foreach (OnTrack onTrack in _onTracks)
                {
                    if (onTrack.PointOfInterest == PointOfInterest)
                    {
                        onTrack.OnPropertyChanged(nameof(Name));
                    }
                }
            }
        }
    }

    public OnTrack GetPrevious()
    {
        int index = _onTracks.IndexOf(this);

        return index == 0 ? null : _onTracks[index - 1];
    }

    public TrackPoint.CommonValues Start => IsOffTrack ? default : _trackPoint.Values - Values;

    public TrackPoint.CommonValues End => IsOffTrack ? default : _trackPoint.Values;

    private TrackPoint.CommonValues Values
    {
        get => _values;
        set
        {
            if (SetProperty(ref _values, value))
            {
                OnPropertyChanged(nameof(Distance));
                OnPropertyChanged(nameof(Ascent));
                OnPropertyChanged(nameof(Descent));
                OnPropertyChanged(nameof(Time));
                OnPropertyChanged(nameof(Speed));
            }
        }
    }

    public OnTrack Remove()
    {
        int index = _onTracks.IndexOf(this);
        _onTracks.RemoveAt(index);

        OnTrack next = _onTracks
            .Skip(index)
            .FirstOrDefault(onTrack =>
                !onTrack.IsOffTrack &&
                onTrack.PointOfInterest.Category == PointOfInterest.Category);

        if (next is not null)
        {
            next.Values += Values;
        }

        if (index == _onTracks.Count)
        {
            index--;
        }

        return index >= 0 ? _onTracks[index] : null;
    }

    public bool IsCurrent(float distance)
    {
        if (PointOfInterest.IsSection)
        {
            return distance >= Start.Distance && distance <= End.Distance;
        }
        else
        {
            int index = _onTracks.IndexOf(this);
            if (distance <= _trackPoint.Distance)
            {
                return index == 0 || distance > (_trackPoint.Distance + _onTracks[index - 1]._trackPoint.Distance) / 2;
            }
            else
            {
                return index >= _onTracks.Count - 1 || distance <= (_trackPoint.Distance + _onTracks[index + 1]._trackPoint.Distance) / 2;
            }
        }
    }
}
