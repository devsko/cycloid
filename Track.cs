using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using cycloid.Routing;
using cycloid.Serizalization;
using Windows.Storage;

namespace cycloid;

public partial class Track : ObservableObject
{
    public readonly record struct Index(int SegmentIndex, int PointIndex)
    {
        public static readonly Index Invalid = new(-1, -1);

        public bool IsValid => this != Invalid;
    }

    public IStorageFile File { get; }

    public RouteBuilder RouteBuilder { get; } = new();

    public PointCollection Points { get; }

    public event Action Loaded;

    public Track(IStorageFile file)
    {
        File = file;
        Points = new PointCollection(RouteBuilder);
    }

    public string Name => File is null ? "" : Path.GetFileNameWithoutExtension(File.Name);

    public async Task LoadAsync()
    {
        await Serializer.LoadAsync(this);
        Loaded?.Invoke();
    }

    public string FilePosition(TrackPoint point)
    {
        if (point.Distance == 0 && Points.Count == 0)
        {
            return "";
        }

        (int fileId, float distance) = Points.FilePosition(point);

        return $"{fileId} / {distance / 1_000:N1}";
    }

    public string DistanceFromStart(TrackPoint point) => "100 km";

    public string TimeFromStart(TrackPoint point) => "12:34";

    public string DistanceToEnd(TrackPoint point) => "100 km";

    public string TimeToEnd(TrackPoint point) => "12:34";
}