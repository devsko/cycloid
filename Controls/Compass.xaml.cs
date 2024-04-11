using CommunityToolkit.WinUI;
using Windows.UI.Composition;
using Windows.UI.Xaml;

namespace cycloid.Controls;

public sealed partial class Compass : PointControl
{
    private readonly Visual _rootVisual;
    private int _rootRounds;
    private readonly Visual _needleVisual;
    private int _needleRounds;

    public float Heading
    {
        get => (float)GetValue(HeadingProperty);
        set => SetValue(HeadingProperty, value);
    }

    public static readonly DependencyProperty HeadingProperty = DependencyProperty.Register(nameof(Heading), typeof(float), typeof(Compass), new PropertyMetadata(0f));

    public Compass()
    {
        InitializeComponent();

        _rootVisual = Root.GetVisual();
        _needleVisual = Needle.GetVisual();
    }

    private double HeadingToRootAngle(float heading)
    {
        double rotation = -heading + 360;
        var current = _rootVisual.RotationAngleInDegrees - _rootRounds * 360;
        if (rotation is >= 270 and <= 360 && current is > 0 and <= 90)
        {
            _rootRounds--;
        }
        else if (rotation is >= 0 and <= 90 && current is >= 270 and <= 360)
        {
            _rootRounds++;
        }

        return rotation + _rootRounds * 360;
    }

    private double HeadingToNeedleAngle(float heading)
    {
        double rotation = heading;
        var current = _needleVisual.RotationAngleInDegrees - _needleRounds * 360;
        if (rotation is >= 270 and <= 360 && current is > 0 and <= 90)
        {
            _needleRounds--;
        }
        else if (rotation is >= 0 and <= 90 && current is >= 270 and <= 360)
        {
            _needleRounds++;
        }

        return rotation + _needleRounds * 360;
    }
}
