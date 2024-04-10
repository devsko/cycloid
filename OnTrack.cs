using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;

namespace cycloid;

public partial class OnTrack(IList<OnTrack> onTracks) : ObservableObject
{
    public static OnTrack CreateOffTrack(PointOfInterest pointOfInterest, IList<OnTrack> onTracks)
    {
        return new OnTrack(onTracks)
        {
            TrackPoint = new TrackPoint(pointOfInterest.Location.Latitude, pointOfInterest.Location.Longitude),
            PointOfInterest = pointOfInterest,
            TrackFilePosition = "?",
            IsOffTrack = true,
        };
    }

    private readonly IList<OnTrack> _onTracks = onTracks;

    private string _trackFilePosition;
    public string TrackFilePosition
    {
        get => _trackFilePosition;
        set => SetProperty(ref _trackFilePosition, value);
    }

    private TrackPoint.CommonValues _values;
    public TrackPoint.CommonValues Values
    {
        get => _values;
        set
        {
            if (SetProperty(ref _values, value))
            {
                OnPropertyChanged(nameof(Distance));
                OnPropertyChanged(nameof(Time));
                OnPropertyChanged(nameof(Speed));
            }
        }
    }

    public TrackPoint TrackPoint { get; set; }

    public PointOfInterest PointOfInterest { get; set; }

    public int TrackMaskBitPosition { get; set; }

    public bool IsOffTrack { get; private set; }

    public string Name
    {
        get => PointOfInterest.Name;
        set
        {
            if (!string.Equals(Name, value))
            {
                PointOfInterest.Name = value;
                RaisePropertyChanged();
            }
        }
    }

    public TrackPoint.CommonValues Start => TrackPoint.Values - Values;

    public TrackPoint.CommonValues End => TrackPoint.Values;

    public float? Distance => IsOffTrack ? null : Values.Distance;

    public TimeSpan? Time => IsOffTrack ? null : Values.Time;

    public float? Speed => IsOffTrack ? null : Values.Distance / 1000 / (float)Values.Time.TotalHours;

    public bool IsCurrent(float distance)
    {
        if (PointOfInterest.IsSection)
        {
            return distance >= Start.Distance && distance <= End.Distance;
        }
        else
        {
            int index = _onTracks.IndexOf(this);
            if (distance <= TrackPoint.Distance)
            {
                return index == 0 || distance > (TrackPoint.Distance + _onTracks[index - 1].TrackPoint.Distance) / 2;
            }
            else
            {
                return index >= _onTracks.Count - 1 || distance <= (TrackPoint.Distance + _onTracks[index + 1].TrackPoint.Distance) / 2;
            }
        }
    }

    private void RaisePropertyChanged([CallerMemberName] string propertyName = null)
    {
        foreach (OnTrack onTrack in _onTracks)
        {
            if (onTrack.PointOfInterest == PointOfInterest)
            {
                onTrack.OnPropertyChanged(propertyName);
            }
        }
    }
}
