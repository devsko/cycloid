using Microsoft.Toolkit.Uwp.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml.Controls;

namespace cycloid.Controls;

public sealed partial class Compass : UserControl
{
    private readonly Visual _visual;
    private int _rounds;

    public Compass()
    {
        InitializeComponent();

        _visual = Grid.GetVisual();
    }

    private ViewModel ViewModel => (ViewModel)this.FindResource(nameof(ViewModel));

    private double HeadingToRotationAngle(float heading)
    {
        double rotation = -heading + 360;
        var current = _visual.RotationAngleInDegrees - _rounds * 360;
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
