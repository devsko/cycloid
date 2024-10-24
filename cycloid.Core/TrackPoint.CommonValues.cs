using System.Diagnostics;

namespace cycloid;

partial struct TrackPoint
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public readonly struct CommonValues
    {
        private readonly int _distance; // 0.1 meters
        private readonly int _time; // milliseconds
        private readonly int _ascent; // 0.1 meters
        private readonly int _descent; // 0.1 meters

        public CommonValues(float distance, TimeSpan time, float ascent, float descent)
            => (_distance, _time, _ascent, _descent) = ((int)(distance * 10), (int)time.TotalMilliseconds, (int)(ascent * 10), (int)(descent * 10));

        private CommonValues(int distance, int time, int ascent, int descent)
            => (_distance, _time, _ascent, _descent) = (distance, time, ascent, descent);

        public float Distance => (float)_distance / 10;

        public TimeSpan Time => TimeSpan.FromMilliseconds(_time);

        public float Ascent => (float)_ascent / 10;

        public float Descent => (float)_descent / 10;
        
        public static CommonValues operator +(CommonValues left, CommonValues right)
            => new (left._distance + right._distance, left._time + right._time, left._ascent + right._ascent, left._descent + right._descent);

        public static CommonValues operator -(CommonValues left, CommonValues right)
            => new(left._distance - right._distance, left._time - right._time, left._ascent - right._ascent, left._descent - right._descent);

#if DEBUG
        public string DebuggerDisplay => $"{Distance / 1000:N1} [{Time:d\\.hh\\:mm}]";
#endif
    }
}
