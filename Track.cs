using CommunityToolkit.Mvvm.ComponentModel;
using cycloid.Routing;
using System.Collections.Generic;
using System.IO;
using Windows.Storage;

namespace cycloid;

public partial class Track : ObservableObject
{
    public IStorageFile File { get; }

    public RouteBuilder RouteBuilder { get; } = new();

    public PointCollection Points { get; }

    public Track(IStorageFile file)
    {
        File = file;
        Points = new PointCollection(RouteBuilder);
    }

    public string Name => File is null ? "" : Path.GetFileNameWithoutExtension(File.Name);

    public string FilePosition(TrackPoint point) => $"22,8 / 1";

    public string DistanceFromStart(TrackPoint point) => "100 km";

    public string TimeFromStart(TrackPoint point) => "12:34";

    public string DistanceToEnd(TrackPoint point) => "100 km";

    public string TimeToEnd(TrackPoint point) => "12:34";
}