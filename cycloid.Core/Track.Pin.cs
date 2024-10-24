namespace cycloid;

partial class Track
{
    public abstract class Pin()
    {
        public event Action<TimeSpan> Changed;

        public TrackPoint CurrentPoint { get; protected set; }
        
        protected Index CurrentIndex { get; private set; }

        public abstract PointCollection Points { get; }

        public bool IsAtEndOfTrack => CurrentIndex == Points.LastIndex();

        public Pin CreateChild(TimeSpan difference) => new ChildPin(this, difference);

        public void GoTo(TimeSpan time)
        {
            (CurrentPoint, CurrentIndex) = Points.AdvanceTo(time, time < CurrentPoint.Time ? default : CurrentIndex);
            Changed?.Invoke(CurrentPoint.Time);
        }
    }

    public sealed class RootPin : Pin
    {
        public override PointCollection Points { get; }

        public RootPin(Track track, TrackPoint point)
        {
            Points = track.Points;
            CurrentPoint = Points.First();

            if (point.IsValid)
            {
                GoTo(point.Time);
            }
        }

        public void AdvanceBy(TimeSpan difference)
        {
            GoTo(Clamp(CurrentPoint.Time + difference, TimeSpan.Zero, Points.Last().Time));

            static TimeSpan Clamp(TimeSpan value, TimeSpan min, TimeSpan max) => value < min ? min : value > max ? max : value;
        }
    }

    private sealed class ChildPin : Pin
    {
        private readonly Pin _parent;
        private readonly TimeSpan _difference;

        public ChildPin(Pin parent, TimeSpan difference)
        {
            _parent = parent;
            _difference = difference;
            CurrentPoint = Points.First();
            
            GoTo(_parent.CurrentPoint.Time + _difference);

            _parent.Changed += parentTime => GoTo(parentTime + _difference);
        }

        public override PointCollection Points => _parent.Points;
    }

}