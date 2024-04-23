using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace cycloid.Controls;

public class PointerPanel : Panel
{
    public PointerPanel()
    {
        Background = new SolidColorBrush(Colors.Transparent);
        Visibility = Visibility.Collapsed;
    }

    public bool IsEnabled
    {
        get => Visibility == Visibility.Visible;
        set => Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(1e5, 1e5);
    }
}
