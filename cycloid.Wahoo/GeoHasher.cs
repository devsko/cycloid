namespace cycloid.Wahoo;

public static class GeoHasher
{
    private const string Base32 = "0123456789bcdefghjkmnpqrstuvwxyz";

    private struct Interval
    {
        public double Min;
        public double Max;
        public double Value;

        public int Bisect()
        {
            double center = (Max + Min) / 2;
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

    public static void Encode(double latitude, double longitude, Span<char> hash)
    {
        Interval latInterval = new() { Min = -90, Max = 90, Value = latitude };
        Interval lonInterval = new() { Min = -180, Max = 180, Value = longitude };

        int pos = 0;
        bool even = false;
        while (pos < hash.Length)
        {
            int ch = 0;
            for (int i = 0; i < 5; i++)
                ch = ch << 1 | ((even = !even) ? ref lonInterval : ref latInterval).Bisect();

            hash[pos++] = Base32[ch];
        }
    }
}
