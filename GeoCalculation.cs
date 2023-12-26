using System;

namespace cycloid;

public static class GeoCalculation
{
    // http://www.movable-type.co.uk/scripts/latlong.html

    public const int EarthRadius = 6371000;

    public static float Distance<T1, T2>(T1 from, T2 to) where T1 : IMapPoint where T2 : IMapPoint =>
        Distance(from.Latitude, from.Longitude, to.Latitude, to.Longitude);

    public static float Distance(float fromLat, float fromLon, float toLat, float toLon)
    {
        float lat1 = ToRadians(fromLat);
        float lat2 = ToRadians(toLat);
        float lon1 = ToRadians(fromLon);
        float lon2 = ToRadians(toLon);

        float sinDLatHalf = MathF.Sin((lat2 - lat1) / 2);
        float sinDLonHalf = MathF.Sin((lon2 - lon1) / 2);
        float cosLat1 = MathF.Cos(lat1);
        float cosLat2 = MathF.Cos(lat2);

        return 2 * EarthRadius * MathF.Asin(MathF.Sqrt(
            MathF.Pow(sinDLatHalf, 2) +
                cosLat2 * cosLat1 * MathF.Pow(sinDLonHalf, 2)));
    }

    public static float Heading<T1, T2>(T1 from, T2 to) where T1 : IMapPoint where T2 : IMapPoint =>
        Heading(from.Latitude, from.Longitude, to.Latitude, to.Longitude);

    public static float Heading(float fromLat, float fromLon, float toLat, float toLon)
    {
        float lat1 = ToRadians(fromLat);
        float lat2 = ToRadians(toLat);
        float lon1 = ToRadians(fromLon);
        float lon2 = ToRadians(toLon);

        float sinDLon = MathF.Sin(lon2 - lon1);
        float cosDLon = MathF.Cos(lon2 - lon1);
        float sinLat1 = MathF.Sin(lat1);
        float sinLat2 = MathF.Sin(lat2);
        float cosLat1 = MathF.Cos(lat1);
        float cosLat2 = MathF.Cos(lat2);

        return (ToDegrees(MathF.Atan2(
            cosLat2 * sinDLon,
            cosLat1 * sinLat2 - sinLat1 * cosLat2 * cosDLon)) + 360) % 360;
    }

    public static (double Distance, double Heading) DistanceAndHeading<T1, T2>(T1 from, T2 to) where T1 : IMapPoint where T2 : IMapPoint
        => DistanceAndHeading(from.Latitude, from.Longitude, to.Latitude, to.Longitude);

    public static (double Distance, double Heading) DistanceAndHeading(float fromLat, float fromLon, float toLat, float toLon)
    {
        float lat1 = ToRadians(fromLat);
        float lat2 = ToRadians(toLat);
        float lon1 = ToRadians(fromLon);
        float lon2 = ToRadians(toLon);

        double sinDLatHalf = Math.Sin((lat2 - lat1) / 2);
        double sinDLonHalf = Math.Sin((lon2 - lon1) / 2);
        double cosLat1 = Math.Cos(lat1);
        double cosLat2 = Math.Cos(lat2);

        double sinDLon = Math.Sin(lon2 - lon1);
        double cosDLon = Math.Cos(lon2 - lon1);
        double sinLat1 = Math.Sin(lat1);
        double sinLat2 = Math.Sin(lat2);

        double distance = 2 * EarthRadius * Math.Asin(Math.Sqrt(
            Math.Pow(sinDLatHalf, 2) +
                cosLat2 * cosLat1 * Math.Pow(sinDLonHalf, 2)));

        double heading = (ToDegrees(Math.Atan2(
            cosLat2 * sinDLon,
            cosLat1 * sinLat2 - sinLat1 * cosLat2 * cosDLon)) + 360) % 360;

        return (distance, heading);
    }

    public static (float Lat, float Lon) Add<T>(T start, double heading, double distance) where T : IMapPoint
    {
        double d = distance / EarthRadius;
        double sinLat =
            Math.Sin(ToRadians(start.Latitude)) *
            Math.Cos(d) +
            Math.Cos(ToRadians(start.Latitude)) *
            Math.Sin(d) *
            Math.Cos(ToRadians(heading));

        return (
            Lat: (float)ToDegrees(Math.Asin(sinLat)),
            Lon: start.Longitude +
                (float)ToDegrees(Math.Atan2(
                    Math.Sin(ToRadians(heading)) *
                    Math.Sin(d) *
                    Math.Cos(ToRadians(start.Latitude)),
                    Math.Cos(d) -
                    Math.Sin(ToRadians(start.Latitude)) *
                    sinLat)));
    }

    //// https://stackoverflow.com/questions/32771458/distance-from-lat-lng-point-to-minor-arc-segment
    //public static (float? Fraction, float Distance) CrossTrackDistance<T>(TrackPoint p1, TrackPoint p2, T p3) where T : IMapPoint
    //{
    //    (double dist12, double heading12) = (p2.Distance - p1.Distance, p1.Heading);
    //    (double dist13, double heading13) = DistanceAndHeading(p1, p3);

    //    double diff = (heading13 - heading12 + 360) % 360;
    //    if (diff > 180)
    //    {
    //        diff = 360 - diff;
    //    }

    //    if (diff > 90)
    //    {
    //        return (0, (float)dist13);
    //    }

    //    double dxt = Math.Asin(Math.Sin(dist13 / EarthRadius) * Math.Sin(ToRadians(diff))) * EarthRadius;
    //    float dist14 = (float)(Math.Acos(Math.Cos(dist13 / EarthRadius) / Math.Cos(dxt / EarthRadius)) * EarthRadius);

    //    if (dist14 > dist12)
    //    {
    //        return (null, float.MaxValue);
    //    }

    //    return ((float)(dist14 / dist12), (float)dxt);
    //}

    private static float ToRadians(float degrees) => degrees * MathF.PI / 180;
    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
    private static float ToDegrees(float radians) => radians * 180 / MathF.PI;
    private static double ToDegrees(double radians) => radians * 180 / Math.PI;
}
