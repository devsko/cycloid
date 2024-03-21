using System;

namespace cycloid;

public static class Format
{
    public static string Latitude(double value) => $"{Coordinate(value)}{(value > 0 ? "N" : "S")}";

    public static string Longitude(double value) => $"{Coordinate(value)}{(value > 0 ? "E" : "W")}";

    public static string Coordinate(double value)
    {
        value = Math.Abs(value);
        double degrees = Math.Truncate(value);
        value = (value - degrees) * 60;
        double minutes = Math.Truncate(value);
        value = (value - minutes) * 60;
        double seconds = value;

        return $"{degrees:N0}°{minutes:N0}'{seconds:N0}\"";
    }

    public static string Distance(float value) => $"{value / 1000:N1} km";

    public static string ShortDistance(float value) => $"{value:N0} m";

    public static string Duration(TimeSpan value) => value.Days == 0 ? $"{value:hh\\:mm}" : $"{value:d\\.hh\\:mm}";

    public static string Altitude(float value) => $"{value:N0} m";

    public static string Gradient(float gradient) => $"{gradient:N1} %";

    public static string Speed(float speed) => speed.ToString("N1") + " km/h";
}