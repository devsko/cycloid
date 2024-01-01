using CommunityToolkit.WinUI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace cycloid.Controls;

public sealed partial class Compass : PointControl
{
    private readonly Visual _gridVisual;
    private int _rounds;

    public Compass()
    {
        InitializeComponent();

        _gridVisual = Grid.GetVisual();
    }

    private double HeadingToRotationAngle(float heading)
    {
        double rotation = -heading + 360;
        var current = _gridVisual.RotationAngleInDegrees - _rounds * 360;
        if (rotation is >= 270 and <= 360 && current is > 0 and <= 90)
        {
            _rounds--;
        }
        else if (rotation is >= 0 and <= 90 && current is >= 270 and <= 360)
        {
            _rounds++;
        }

        return rotation + _rounds * 360;
    }
}
