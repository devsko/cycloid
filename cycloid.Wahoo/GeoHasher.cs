using System.Numerics;

namespace cycloid.Wahoo;

public static class GeoHasher
{
    private const string Base32 = "0123456789bcdefghjkmnpqrstuvwxyz";

    private struct Interval<T> where T : IBinaryFloatingPointIeee754<T>
    {
        public static readonly T LonMin = T.CreateTruncating(-180);
        public static readonly T LonMax = T.CreateTruncating(180);
        public static readonly T LatMin = T.CreateTruncating(-90);
        public static readonly T LatMax = T.CreateTruncating(90);

        public T Min;
        public T Max;
        public T Value;

        public int Bisect()
        {
            T center = (Min + Max) / (T.One + T.One);
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
        Interval<T> lonInterval = new() { Min = Interval<T>.LonMin, Max = Interval<T>.LonMax, Value = longitude };
        Interval<T> latInterval = new() { Min = Interval<T>.LatMin, Max = Interval<T>.LatMax, Value = latitude };

        int pos = 0;
        bool even = true;
        while (pos < hash.Length)
        {
            int ch = even ? lonInterval.Bisect() : 0;
            ch = ch << 1 | latInterval.Bisect();
            ch = ch << 1 | lonInterval.Bisect();
            ch = ch << 1 | latInterval.Bisect();
            ch = ch << 1 | lonInterval.Bisect();
            hash[pos++] = Base32[even ? ch : ch << 1 | latInterval.Bisect()];
            even = !even;
        }
    }

    public static int GetHashCode(float latitude, float longitude)
    {
        Interval<float> lonInterval = new() { Min = Interval<float>.LonMin, Max = Interval<float>.LonMax, Value = longitude };
        Interval<float> latInterval = new() { Min = Interval<float>.LatMin, Max = Interval<float>.LatMax, Value = latitude };

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
