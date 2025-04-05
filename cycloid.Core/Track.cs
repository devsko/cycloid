using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using cycloid.Info;
using cycloid.Routing;

namespace cycloid;

public partial class Track : ObservableObject
{
    public RouteBuilder RouteBuilder { get; }

    public PointCollection Points { get; }

    public List<PointOfInterest> PointsOfInterest { get; }

    public Track(bool isNew)
    {
        RouteBuilder = new RouteBuilder();
        Points = new PointCollection(this);
        PointsOfInterest = [];
        if (isNew)
        {
            PointOfInterest goal = new()
            {
                Name = "Goal",
                Type = InfoType.Goal,
                Category = InfoCategory.Section,
            };
            goal.InitOnTrackCount(1);
            PointsOfInterest.Add(goal);
        }
    }

    public void ClearViewState()
    {
        if (CompareSession is not null)
        {
            CompareSession.Differences.Clear();
            OnPropertyChanged(nameof(CompareSession));
            StrongReferenceMessenger.Default.Send(new CompareSessionChanged(this, CompareSession, null));
        }
    }

    public string FilePosition(float distance)
    {
        if (float.IsNaN(distance) || (distance == 0 && Points.IsEmpty))
        {
            return "";
        }

        (int fileId, float fileDistance) = Points.FilePosition(distance);

        return $"{fileId} / {fileDistance / 1_000:N1}";
    }

    public float DistanceFromStart(float distance)
    {
        _ = this;

        return distance;
    }

    public TimeSpan TimeFromStart(TimeSpan time)
    {
        _ = this;

        return time;
    }

    public float DistanceToEnd(float distance) => Points.Total.Distance - distance;

    public TimeSpan TimeToEnd(TimeSpan time) => Points.Total.Time - time;
}