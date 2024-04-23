namespace cycloid.Wahoo;

public static class GeoHasher
{
    private static readonly char[] base32Chars = "0123456789bcdefghjkmnpqrstuvwxyz".ToCharArray();
    private static readonly int[] bits = [16, 8, 4, 2, 1];

    public static string Encode(double latitude, double longitude, int precision = 12)
    {
        double[] latInterval = [-90.0, 90.0];
        double[] lonInterval = [-180.0, 180.0];

        Span<char> hash = stackalloc char[precision];
        bool isEven = true;
        int bit = 0;
        int ch = 0;

        int pos = 0;
        while (pos < precision)
        {
            double mid;

            if (isEven)
            {
                mid = (lonInterval[0] + lonInterval[1]) / 2;

                if (longitude >= mid)
                {
                    ch |= bits[bit];
                    lonInterval[0] = mid;
                }
                else
                {
                    lonInterval[1] = mid;
                }
            }
            else
            {
                mid = (latInterval[0] + latInterval[1]) / 2;

                if (latitude >= mid)
                {
                    ch |= bits[bit];
                    latInterval[0] = mid;
                }
                else
                {
                    latInterval[1] = mid;
                }
            }

            isEven = !isEven;

            if (bit < 4)
            {
                bit++;
            }
            else
            {
                hash[pos++] = base32Chars[ch];
                bit = 0;
                ch = 0;
            }
        }

        return hash.ToString();
    }
}
