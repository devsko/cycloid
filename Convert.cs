using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using cycloid.Info;
using Windows.Devices.Geolocation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace cycloid;

public static class Convert
{
    private const float _maxGradient = 14f;
    private const int _gradientSteps = 29;

    private static readonly Geopoint _emptyGeopoint = new(new BasicGeoposition());

    private static readonly (Brush Positive, Brush Negative) _differenceBrushes = (new SolidColorBrush(Colors.Red), new SolidColorBrush(Colors.Green));
    private static readonly (Brush[] Ascending, Brush[] Descending) _gradientBrushes = CreateGradientBrushes();

    private static readonly Dictionary<InfoType, BitmapImage> _infoTypeIcons = CreateInfoTypeIcons();


    private static Dictionary<InfoType, BitmapImage> CreateInfoTypeIcons()
    {
        return InfoCategory
            .All
            .Where(category => !category.Hide)
            .SelectMany(category => category.Types)
            .ToDictionary(type => type, type => new BitmapImage { UriSource = new Uri($"ms-appx:///Assets/{type}.png") });
    }

    private static (Brush[], Brush[]) CreateGradientBrushes()
    {
        Vector3[] stopsAscending =
        [
            new(0x80, 0x80, 0x80), // gray
                new(0xff, 0xff, 0x00), // yellow
                new(0xff, 0x80, 0x00), // orange
                new(0xff, 0x00, 0x00), // red
                new(0xff, 0x00, 0xff), // magenta
            ];
        Vector3[] stopsDescending =
        [
            new(0x80, 0x80, 0x80), // gray
                new(0x00, 0x80, 0x00), // dark green
                new(0x00, 0xff, 0x00), // green
                new(0x00, 0xff, 0xff), // cyan
                new(0x80, 0x00, 0x80), // violet
            ];

        return (CreateBrushes(stopsAscending), CreateBrushes(stopsDescending));

        static Brush[] CreateBrushes(Vector3[] stops)
        {
            int stepsPerSection = (int)((_gradientSteps - 1) / (stops.Length - 1.5f));
            float steps = (stops.Length - 1.5f) * stepsPerSection;
            float delta = _maxGradient / steps;

            List<Brush> brushes = [];
            int section;
            for (section = 0; section < stops.Length - 1; section++)
            {
                brushes.AddRange(
                    Enumerable
                        .Range(0, section == 0 ? stepsPerSection / 2 : stepsPerSection)
                        .Select(step =>
                        {
                            var color = Vector3.Lerp(stops[section], stops[section + 1], (float)step / (section == 0 ? stepsPerSection / 2 : stepsPerSection));
                            return new SolidColorBrush(Color.FromArgb(255, (byte)color.X, (byte)color.Y, (byte)color.Z));
                        }));
            }
            brushes.Add(new SolidColorBrush(Color.FromArgb(255, (byte)stops[section].X, (byte)stops[section].Y, (byte)stops[section].Z)));

            return [.. brushes];
        }
    }

    public static Brush GradientToBrush(float gradient)
    {
        gradient = Math.Clamp(gradient, -_maxGradient, _maxGradient);

        return (gradient >= 0 ? _gradientBrushes.Ascending : _gradientBrushes.Descending)[(int)((Math.Abs(gradient)) / _maxGradient * (_gradientSteps - 1))];
    }

    public static Brush DifferenceBrush(float value)
    {
        if (Math.Abs(value) < 1)
        {
            return null;
        }
        else if (value > 0)
        {
            return _differenceBrushes.Positive;
        }
        else if (value < 0)
        {
            return _differenceBrushes.Negative;
        }

        return null;
    }

    public static Geopoint ToGeopoint(TrackPoint point) => point.IsValid ? new Geopoint((BasicGeoposition)point) : _emptyGeopoint;

    public static Visibility VisibleIf(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility VisibleIfNotNull(object value) => value is null ? Visibility.Collapsed : Visibility.Visible;
    
    public static Visibility VisibleIfValid(TrackPoint value) => !value.IsValid ? Visibility.Collapsed : Visibility.Visible;

    public static Visibility VisibleIfValid(Selection value) => !value.IsValid ? Visibility.Collapsed : Visibility.Visible;

    public static Visibility VisibleIfValid(Info.InfoPoint value) => !value.IsValid ? Visibility.Collapsed : Visibility.Visible;

    public static Visibility VisibleIfNotNullOrEmpty(string value) => string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;

    public static Visibility VisibleIfNotVisible(Visibility visibility) => visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;

    public static ImageSource InfoTypeIcon(InfoType type) => _infoTypeIcons[type];

    public static bool IsValid(Selection value) => value.IsValid;

    public static bool IsNotNull(object value) => value is not null;
}