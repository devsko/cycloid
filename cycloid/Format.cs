namespace cycloid;

public static class Format
{
    public static string Numeric(int value) => $"{value:N0}";

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

    public static string ShortDistance(float value) => $"{Math.Abs(value):N0} m";

    public static string FlexDistance(float value) => Math.Abs(value) >= 10_000 ? Distance(Math.Abs(value)) : ShortDistance(value);

    public static string Duration(TimeSpan value) => value.Days == 0 ? $"{value:hh\\:mm}" : $"{value:d\\.hh\\:mm}";

    public static string Altitude(float value) => $"{value:N0} m";

    public static string Gradient(float gradient) => $"{gradient:N1} %";

    public static string Speed(float value) => $"{value:N1} km/h";


    public static string Distance(float? value) => value is null ? "" : $"{value.Value / 1000:N1} km";

    public static string Duration(TimeSpan? value) => value is null ? "" : value.Value.Days == 0 ? $"{value.Value:hh\\:mm}" : $"{value.Value:d\\.hh\\:mm}";

    public static string Altitude(float? value) => value is null ? "" : $"{value:N0}";

    public static string Speed(float? value) => value is null ? "" : $"{value.Value:N1} km/h";
}