using System.Diagnostics;
using cycloid.Routing;

namespace cycloid;

partial class Track
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    private class Segment
    {
        public class StartDistanceComparer : IComparer<Segment>
        {
            public static readonly StartDistanceComparer Instance = new();

            private StartDistanceComparer()
            { }

            public int Compare(Segment? x, Segment? y) => x!.Start.Distance.CompareTo(y!.Start.Distance);
        }

        public RouteSection Section { get; set; }
        public int StartIndex { get; set; } = -1;
        public TrackPoint.CommonValues Start { get; set; }
        public TrackPoint[] Points { get; set; }
        public float MinAltitude { get; set; } = float.PositiveInfinity;
        public float MaxAltitude { get; set; } = float.NegativeInfinity;
        public int FileId { get; set; }
        public bool Linked { get; set; }

        public TrackPoint.CommonValues Values => Points is { Length: > 0 } ? Points[^1].Values : default;

#if DEBUG
        public string DebuggerDisplay => $"{(Points is null ? "?" : Values.DebuggerDisplay)} {(Linked ? $"Start=({Start.DebuggerDisplay})" : "")}";
#endif
    }
}