using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace cycloid;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        InitializeComponent();
    }

    private void Page_Loaded(object _1, RoutedEventArgs _2)
    {
        ViewModel.ToggleHeatmapVisibleAsync().FireAndForget();
    }
}
