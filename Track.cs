using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using cycloid.Routing;
using cycloid.Serizalization;
using Windows.Storage;

namespace cycloid;

public partial class Track : ObservableObject
{
    public IStorageFile File { get; }

    public RouteBuilder RouteBuilder { get; }

    public PointCollection Points { get; }

    public List<PointOfInterest> PointsOfInterest { get; }

    public event Action Loaded;

    public Track(IStorageFile file)
    {
        File = file;
        RouteBuilder = new RouteBuilder();
        Points = new PointCollection(this);
        PointsOfInterest = [];
    }

    public string Name => File is null ? "" : Path.GetFileNameWithoutExtension(File.Name);

    public async Task LoadAsync()
    {
        await Serializer.LoadAsync(this);
        Loaded?.Invoke();
    }

    public string FilePosition(float distance)
    {
        if (distance == 0 && Points.Count == 0)
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