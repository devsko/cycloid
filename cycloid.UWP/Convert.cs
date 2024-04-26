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

    private static readonly Dictionary<Surface, Brush> _surfaceBrushes = CreateSurfaceBrushes();

    private static Dictionary<InfoType, BitmapImage> CreateInfoTypeIcons()
    {
        return InfoCategory
            .All
            .Where(category => !category.Hide)
            .SelectMany(category => category.Types)
            .ToDictionary(type => type, type => new BitmapImage { UriSource = new Uri($"ms-appx:///Assets/{type}.png") });
    }

    private static Dictionary<Surface, Brush> CreateSurfaceBrushes()
    {
        Brush trackGraphOutlineBrush = (Brush)App.Current.Resources["TrackGraphOutline"];
        Brush orange = new SolidColorBrush(Colors.Orange);
        Brush red = new SolidColorBrush(Colors.Red);

        return new Dictionary<Surface, Brush>()
        {
            { cycloid.Surface.Unknown, new SolidColorBrush(Colors.Goldenrod) },
            { cycloid.Surface.UnknownLikelyPaved, new SolidColorBrush(Colors.Yellow) },
            { cycloid.Surface.UnknownLikelyUnpaved, red },

            { cycloid.Surface.paved, trackGraphOutlineBrush },
            { cycloid.Surface.asphalt, trackGraphOutlineBrush },
            { cycloid.Surface.chipseal, trackGraphOutlineBrush },
            { cycloid.Surface.concrete, trackGraphOutlineBrush },
            { cycloid.Surface.paving_stones, trackGraphOutlineBrush },
            { cycloid.Surface.sett, orange },
            { cycloid.Surface.unhewn_cobblestone, orange },
            { cycloid.Surface.cobblestone, orange },
            { cycloid.Surface.metal, new SolidColorBrush(Colors.Blue) },
            { cycloid.Surface.wood, new SolidColorBrush(Colors.Brown) },
            { cycloid.Surface.stepping_stones, new SolidColorBrush(Colors.GreenYellow) },
            { cycloid.Surface.rubber, new SolidColorBrush(Colors.PaleVioletRed) },

            { cycloid.Surface.unpaved, red },
            { cycloid.Surface.compacted, red },
            { cycloid.Surface.fine_gravel, red },
            { cycloid.Surface.gravel, red },
            { cycloid.Surface.shells, red },
            { cycloid.Surface.rock, red },
            { cycloid.Surface.pebblestone, red },
            { cycloid.Surface.ground, red },
            { cycloid.Surface.dirt, red },
            { cycloid.Surface.earth, red },
            { cycloid.Surface.grass, red },
            { cycloid.Surface.grass_paver, red },
            { cycloid.Surface.metal_grid, red },
            { cycloid.Surface.mud, red },
            { cycloid.Surface.sand, red },
            { cycloid.Surface.woodchips, red },
            { cycloid.Surface.snow, red },
            { cycloid.Surface.ice, red },
            { cycloid.Surface.salt, red },
        };
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
        return value > 1e-5
            ? _differenceBrushes.Positive
            : _differenceBrushes.Negative;
    }

    public static Brush ReverseDifferenceBrush(float value)
    {
        return value > -1e-5
            ? _differenceBrushes.Negative
            : _differenceBrushes.Positive;
    }

    public static Brush DifferenceBrush(TimeSpan value)
    {
        return value.TotalHours > 1e-5
            ? _differenceBrushes.Positive
            : _differenceBrushes.Negative;
    }

    public static Brush SurfaceBrush(Surface value)
    {
        return _surfaceBrushes[value];
    }

    public static string Surface(Surface value)
    {
        return value switch
        {
            cycloid.Surface.Unknown => "unknown",
            cycloid.Surface.UnknownLikelyPaved => "[paved]",
            cycloid.Surface.UnknownLikelyUnpaved => "[unpaved]",
            cycloid.Surface.paving_stones => "paving stones",
            cycloid.Surface.unhewn_cobblestone => "unhewen cobblestone",
            cycloid.Surface.stepping_stones => "stepping stones",
            cycloid.Surface.fine_gravel => "fine gravel",
            cycloid.Surface.grass_paver => "grass paver",
            cycloid.Surface.metal_grid => "metal grid",
            _ => value.ToString()
        };
    }

    public static Visibility SurfaceToVisibilityy(Surface value) => value <= cycloid.Surface.paving_stones ? Visibility.Collapsed : Visibility.Visible;

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

    public static bool Not(bool value) => !value;

    // IsNull / IsNotNull is not working!
}