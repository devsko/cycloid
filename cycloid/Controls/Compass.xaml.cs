using CommunityToolkit.WinUI;
using Windows.UI.Composition;

namespace cycloid.Controls;

public sealed partial class Compass : TrackPointControl
{
    private readonly Visual _rootVisual;
    private int _rootRounds;
    private readonly Visual _needleVisual;
    private int _needleRounds;

    [GeneratedDependencyProperty(IsLocalCacheEnabled = true)]
    public partial float Heading { get; set; }

    public Compass()
    {
        InitializeComponent();

        _rootVisual = Root.GetVisual();
        _needleVisual = Needle.GetVisual();
    }

    private double HeadingToRootAngle(float heading) => HeadingToAngle(-heading, _rootVisual, ref _rootRounds);

    private double HeadingToNeedleAngle(float heading) => HeadingToAngle(heading, _needleVisual, ref _needleRounds);

    private static double HeadingToAngle(float rotation, Visual visual, ref int rounds)
    {
        if (rotation < 0)
        {
            rotation += 360;
        }
        float current = visual.RotationAngleInDegrees - rounds * 360;
        if (rotation is >= 270 and <= 360 && current is >= 0 and <= 90)
        {
            rounds--;
        }
        else if (rotation is >= 0 and <= 90 && current is >= 270 and <= 360)
        {
            rounds++;
        }

        return rotation + rounds * 360;
    }
}
