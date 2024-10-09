using cycloid.Wahoo;

namespace cycloid.Test;

public class GeoHasherTests
{
    [Theory]
    [InlineData(47.76027170508629, 12.216676087056076, "u22xtyrztpvy")]
    public void GeoHasherEncodes(double latitude, double longitude, string geoHash)
    {
        Span<char> hash = stackalloc char[12];
        GeoHasher.Encode(latitude, longitude, hash);

        Assert.Equal(geoHash, hash.ToString());
    }
}
