using cycloid.Wahoo;

namespace cycloid.Test;

public class GeoHasherTests
{
    [Theory]
    [InlineData(44.85027, 7.193301, "spvp5swpb03c")]
    [InlineData(40.71014375091961, -74.0132859349251, "dr5reg11v2vf")]
    [InlineData(1e-9, 1e-9, "s00000000000")]
    [InlineData(-1e-9, -1e-9, "7zzzzzzzzzzz")]
    [InlineData(1e-9, -1e-9, "ebpbpbpbpbpb")]
    [InlineData(-1e-9, 1e-9, "kpbpbpbpbpbp")]
    [InlineData(90, 1e-9, "upbpbpbpbpbp")]
    [InlineData(90, -1e-9, "gzzzzzzzzzzz")]
    [InlineData(-90, 1e-9, "h00000000000")]
    [InlineData(-90, -1e-9, "5bpbpbpbpbpb")]
    public void GeoHasherEncodes(double latitude, double longitude, string geoHash)
    {
        Span<char> hash = stackalloc char[12];
        GeoHasher.Encode(latitude, longitude, hash);

        Assert.Equal(geoHash, hash.ToString());
    }
}
