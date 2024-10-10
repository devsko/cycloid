using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace cycloid;

public readonly partial struct TrackPoint(float latitude, float longitude, float altitude = 0f, TimeSpan time = default, float distance = 0f, float heading = 0f, float gradient = 0f, float speed = 0f, float ascent = 0f, float descent = 0f, Surface surface = Surface.Unknown) : IMapPoint, IEquatable<TrackPoint>, ICanBeInvalid<TrackPoint>
{
    public class DistanceComparer : IComparer<TrackPoint>
    {
        public static readonly DistanceComparer Instance = new();
        private DistanceComparer()
        { }
        public int Compare(TrackPoint x, TrackPoint y) => x.Distance.CompareTo(y.Distance);
    }

    public static readonly TrackPoint Invalid = new(float.NaN, float.NaN);

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
#if NETSTANDARD
            previous.Time + TimeSpan.FromTicks((long)(fraction * (next.Time - previous.Time).Ticks)),
#else
            previous.Time + fraction * (next.Time - previous.Time),
#endif
            previous.Distance + distance,
            previous.Heading,
            previous.Gradient,
            previous.Speed,
            previous.Values.Ascent + fraction * (next.Values.Ascent - previous.Values.Ascent),
            previous.Values.Descent + fraction * (next.Values.Descent - previous.Values.Descent),
            previous.Surface);
    }

    private readonly float _latitude = latitude;
    private readonly float _longitude = longitude;
    private readonly CommonValues _values = new(distance, time, ascent, descent);
    private readonly ushort _altitude = (ushort)((altitude + 50) * 10); // 0.1 meters above -50 meters msl
    private readonly short _heading = (short)(heading * 10); // 0.1 °
    private readonly short _gradient = (short)(gradient * 10); // 0.1 %
    private readonly short _speed = (short)(speed * 10); // 0.1 km/h
    private readonly Surface _surface = surface;

    public float Latitude => _latitude;

    public float Longitude => _longitude;

    public float Altitude => ((float)_altitude / 10) - 50;

    public TimeSpan Time => _values.Time;

    public float Distance => IsValid ? _values.Distance : float.NaN;

    public float Heading => (float)_heading / 10;

    public float Gradient => (float)_gradient / 10;

    public float Speed => (float)_speed / 10;

    public Surface Surface => _surface;

    public CommonValues Values
    {
        get => _values;
        init => _values = value;
    }

    public bool IsValid => !float.IsNaN(Latitude);

    TrackPoint ICanBeInvalid<TrackPoint>.Invalid => Invalid;

    public bool Equals(TrackPoint other) =>
        other._latitude == _latitude &&
        other._longitude == _longitude;

    public override bool Equals([NotNullWhen(true)] object obj) => 
        obj is TrackPoint other && Equals(other);

    public override int GetHashCode() =>
#if NETSTANDARD
        _latitude.GetHashCode() ^ _longitude.GetHashCode();
#else
        HashCode.Combine(_latitude, _longitude);
#endif

    public static bool operator ==(TrackPoint left, TrackPoint right) => 
        left.Equals(right);

    public static bool operator !=(TrackPoint left, TrackPoint right) => 
        !(left == right);
}
