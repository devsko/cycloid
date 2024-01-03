using cycloid.Routing;
using System.IO;
using Windows.Storage;

namespace cycloid;

public partial class Track(IStorageFile file)
{
    public IStorageFile File { get; } = file;

    public RouteBuilder RouteBuilder { get; } = new();

    public PointCollection Points { get; } = new();

    public string Name => File is null ? "" : Path.GetFileNameWithoutExtension(File.Name);

    public string FilePosition(TrackPoint point) => $"22,8 / 1";

    public string DistanceFromStart(TrackPoint point) => "100 km";

    public string TimeFromStart(TrackPoint point) => "12:34";

    public string DistanceToEnd(TrackPoint point) => "100 km";

    public string TimeToEnd(TrackPoint point) => "12:34";
}