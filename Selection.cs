namespace cycloid;

public record struct Selection(TrackPoint Start, TrackPoint End) : ICanBeInvalid<Selection>
{
    public static readonly Selection Invalid = new(TrackPoint.Invalid, TrackPoint.Invalid);

    public bool IsValid => Start.IsValid && End.IsValid;

    Selection ICanBeInvalid<Selection>.Invalid => Invalid;
}