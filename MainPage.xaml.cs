using Windows.System;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace cycloid;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        InitializeComponent();
    }

    private void Page_Loaded(object _1, RoutedEventArgs _2)
    {
        Map.SetCenterAsync("Stampfl Samerberg").FireAndForget();
        ViewModel.ToggleHeatmapVisibleAsync().FireAndForget();
        ViewModel.OpenLastTrackAsync().FireAndForget();
    }

    private void ViewModel_TrackChanged(Track _, Track track)
    {
        ApplicationView.GetForCurrentView().Title = track is null ? string.Empty : track.Name;
    }

    private void SearchText_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            Map.SetCenterAsync(((TextBox)sender).Text).FireAndForget();
            e.Handled = true;
        }
    }
}
