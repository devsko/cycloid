using System.Numerics;

namespace cycloid.Wahoo;

public static class GeoHasher
{
    private const string Base32 = "0123456789bcdefghjkmnpqrstuvwxyz";

    private struct Interval<T> where T : IBinaryFloatingPointIeee754<T>
    {
        public static readonly T LatMin = T.CreateChecked(-90);
        public static readonly T LatMax = T.CreateChecked(90);
        public static readonly T LonMin = T.CreateChecked(-180);
        public static readonly T LonMax = T.CreateChecked(180);

        private static readonly T _half = T.CreateChecked(.5);

        public T Min;
        public T Max;
        public T Value;

        public int Bisect()
        {
            T center = T.Lerp(Min, Max, _half);
            if (Value >= center)
            {
                Min = center;
                return 1;
            }
            else
            {
                Max = center;
                return 0;
            }
        }
    }

    public static void Encode<T>(T latitude, T longitude, Span<char> hash) where T : IBinaryFloatingPointIeee754<T>
    {
        Interval<T> latInterval = new() { Min = Interval<T>.LatMin, Max = Interval<T>.LatMax, Value = latitude };
        Interval<T> lonInterval = new() { Min = Interval<T>.LonMin, Max = Interval<T>.LonMax, Value = longitude };

        int pos = 0;
        bool even = false;
        while (pos < hash.Length)
        {
            int ch = 0;
            for (int i = 0; i < 5; i++)
            {
                ch = ch << 1 | ((even = !even) ? ref lonInterval : ref latInterval).Bisect();
            }

            hash[pos++] = Base32[ch];
        }
    }

    public static int GetHashCode(float latitude, float longitude)
    {
        Interval<float> latInterval = new() { Min = Interval<float>.LatMin, Max = Interval<float>.LatMax, Value = latitude };
        Interval<float> lonInterval = new() { Min = Interval<float>.LonMin, Max = Interval<float>.LonMax, Value = longitude };

        int hash = lonInterval.Bisect();
        hash = hash << 1 | latInterval.Bisect();
        hash = hash << 1 | lonInterval.Bisect();
        hash = hash << 1 | latInterval.Bisect();
        hash = hash << 1 | lonInterval.Bisect();
        hash = hash << 1 | latInterval.Bisect();
        hash = hash << 1 | lonInterval.Bisect();
        hash = hash << 1 | latInterval.Bisect();
        hash = hash << 1 | lonInterval.Bisect();
        hash = hash << 1 | latInterval.Bisect();
        hash = hash << 1 | lonInterval.Bisect();
        hash = hash << 1 | latInterval.Bisect();
        hash = hash << 1 | lonInterval.Bisect();
        hash = hash << 1 | latInterval.Bisect();
        hash = hash << 1 | lonInterval.Bisect();
        hash = hash << 1 | latInterval.Bisect();
        hash = hash << 1 | lonInterval.Bisect();
        hash = hash << 1 | latInterval.Bisect();
        hash = hash << 1 | lonInterval.Bisect();
        hash = hash << 1 | latInterval.Bisect();
        hash = hash << 1 | lonInterval.Bisect();
        hash = hash << 1 | latInterval.Bisect();
        hash = hash << 1 | lonInterval.Bisect();
        hash = hash << 1 | latInterval.Bisect();
        hash = hash << 1 | lonInterval.Bisect();
        hash = hash << 1 | latInterval.Bisect();
        hash = hash << 1 | lonInterval.Bisect();
        hash = hash << 1 | latInterval.Bisect();
        hash = hash << 1 | lonInterval.Bisect();
        hash = hash << 1 | latInterval.Bisect();
        hash = hash << 1 | lonInterval.Bisect();
        hash = hash << 1 | latInterval.Bisect();

        return hash;
    }
}
