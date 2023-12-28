using System;

namespace cycloid;

public static class Format
{
    public static string Distance(float value) => $"{value / 1000:N1} km";

    public static string Duration(TimeSpan value) => value.Days == 0 ? $"{value:hh\\:mm}" : $"{value:d\\.hh\\:mm}";

    public static string Altitude(float value) => $"{value:N0} m";

    public static string Gradient(float gradient) => $"{gradient:N1} %";

    public static string Speed(float speed) => speed.ToString("N1") + " km/h";
}