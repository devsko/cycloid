using System;

namespace cycloid;

public class Track
{
    public string FilePosition(TrackPoint point) => $"22,8 / 1";

    public string DistanceFromStart(TrackPoint point) => "100 km";

    public string TimeFromStart(TrackPoint point) => "12:34";

    public string DistanceToEnd(TrackPoint point) => "100 km";

    public string TimeToEnd(TrackPoint point) => "12:34";
}