using System.Threading.Tasks;
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

        //*********
        SetCurrentAsync().FireAndForget();

        async Task SetCurrentAsync()
        {
            ViewModel.CurrentPoint = new(46.46039124618558f, 10.089039490153148f, gradient: 5f, heading: 195);
            await Task.Delay(500);
            ViewModel.CurrentPoint = new(47.76031819349117f, 12.216661197615972f, gradient: 5f, heading: 195);
        }
    }
}
