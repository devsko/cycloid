using System;
using System.Collections.Generic;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using cycloid.Info;
using cycloid.Routing;
using Windows.Storage;

namespace cycloid;

public partial class Track : ObservableObject
{
    public IStorageFile File { get; set; }

    public RouteBuilder RouteBuilder { get; }

    public PointCollection Points { get; }

    public List<PointOfInterest> PointsOfInterest { get; }

    public Track(IStorageFile file)
    {
        File = file;
        RouteBuilder = new RouteBuilder();
        Points = new PointCollection(this);
        PointOfInterest goal = new()
        {
            Name = "Goal",
            Type = InfoType.Goal,
            Category = InfoCategory.Section,
        };
        goal.InitOnTrackCount(1);
        PointsOfInterest = [goal];
    }

    public string Name => File is null ? "" : Path.GetFileNameWithoutExtension(File.Name);

    public string FilePosition(float distance)
    {
        if (float.IsNaN(distance) || (distance == 0 && Points.Count == 0))
        {
            return "";
        }

        (int fileId, float fileDistance) = Points.FilePosition(distance);

        return $"{fileId} / {fileDistance / 1_000:N1}";
    }

    // TODO Track.DistanceFromStart/TimeFromStart/DistanceToEnd/TimeToEnd

    public string DistanceFromStart(float distance) => "100 km";

    public string TimeFromStart(TimeSpan time) => "12:34";

    public string DistanceToEnd(float distance) => "100 km";

    public string TimeToEnd(TimeSpan time) => "12:34";
}