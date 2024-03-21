using cycloid.Routing;
using System;
using System.Collections.Generic;
using Windows.Devices.Geolocation;

namespace cycloid;

public readonly partial struct TrackPoint(float latitude, float longitude, float altitude = 0f, TimeSpan time = default, float distance = 0f, float heading = 0f, float gradient = 0f, float speed = 0f, float ascent = 0f, float descent = 0f) : IRoutePoint, IEquatable<TrackPoint>
{
    public class DistanceComparer : IComparer<TrackPoint>
    {
        public static readonly DistanceComparer Instance = new();
        private DistanceComparer()
        { }
        public int Compare(TrackPoint x, TrackPoint y) => x.Distance.CompareTo(y.Distance);
    }

    public static readonly TrackPoint Invalid = new(float.NaN, float.NaN);

    private readonly float _latitude = latitude;
    private readonly float _longitude = longitude;
    private readonly CommonValues _values = new(distance, time, ascent, descent);
    private readonly ushort _altitude = (ushort)((altitude + 50) * 10); // 0.1 meters above -50 meters msl
    private readonly short _heading = (short)(heading * 10); // 0.1 °
    private readonly short _gradient = (short)(gradient * 10); // 0.1 %
    private readonly short _speed = (short)(speed * 10); // 0.1 km/h

    public float Latitude => _latitude;

    public float Longitude => _longitude;

    public float Altitude => ((float)_altitude / 10) - 50;

    public TimeSpan Time => _values.Time;

    public float Distance => _values.Distance;

    public float Heading => (float)_heading / 10;

    public float Gradient => (float)_gradient / 10;

    public float Speed => (float)_speed / 10;

    public CommonValues Values
    {
        get => _values;
        init => _values = value;
    }

    public bool IsValid => !float.IsNaN(Latitude);

    public static explicit operator TrackPoint(BasicGeoposition position) => new((float)position.Latitude, (float)position.Longitude, 0);

    public static explicit operator BasicGeoposition(TrackPoint point) =>  point.IsValid 
        ? new() { Latitude = point.Latitude, Longitude = point.Longitude } 
        : throw new ArgumentException("invalid", nameof(point));

    public static TrackPoint Lerp(TrackPoint previous, TrackPoint next, float fraction)
    {
        if (fraction == 0)
        {
            return previous;
        }

        if (fraction == 1)
        {
            return next;
        }

        var distance = fraction * (next.Distance - previous.Distance);
        (var latitude, var longitude) = GeoCalculation.Add(previous, previous.Heading, distance);

        return new TrackPoint(
            latitude, 
            longitude,
            previous.Altitude + fraction * (next.Altitude - previous.Altitude),
            previous.Time + fraction * (next.Time - previous.Time),
            previous.Distance + distance,
            previous.Heading,
            previous.Gradient,
            previous.Speed);
    }

    public bool Equals(TrackPoint other) =>
        other._latitude == _latitude &&
        other._longitude == _longitude &&
        other._altitude == _altitude;

    public bool LocationEquals(TrackPoint other) =>
        other._latitude == _latitude &&
        other._longitude == _longitude;
}
