using System;

namespace cycloid;

public readonly record struct Selection(TrackPoint Start, TrackPoint End) : ICanBeInvalid<Selection>
{
    public static readonly Selection Invalid = new(TrackPoint.Invalid, TrackPoint.Invalid);

    public bool IsValid => Start.IsValid && End.IsValid;

    public float Distance => IsValid ? End.Distance - Start.Distance : default;

    public TimeSpan Duration => IsValid ? End.Time - Start.Time : default;

    public float Speed => IsValid ? Distance / 1_000 / (float)Duration.TotalHours : default;

    public float Ascent => IsValid ? End.Values.Ascent - Start.Values.Ascent : default;

    public float Descent => IsValid ? End.Values.Descent - Start.Values.Descent : default;

    Selection ICanBeInvalid<Selection>.Invalid => Invalid;
}