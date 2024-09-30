using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Windows.Storage;
using Windows.UI.Xaml;

namespace cycloid.Controls;

public sealed partial class StreetView : TrackPointControl
{
    public bool IsCollapsed
    {
        get => (bool)GetValue(IsCollapsedProperty);
        set => SetValue(IsCollapsedProperty, value);
    }

    public static readonly DependencyProperty IsCollapsedProperty =
        DependencyProperty.Register(nameof(IsCollapsed), typeof(bool), typeof(StreetView), new PropertyMetadata(true, (sender, e) => ((StreetView)sender).IsCollapsedChanged(e)));

    public StreetView()
    {
        InitializeComponent();

        VisualStateManager.GoToState(this, "CollapsedState", false);
    }

    protected override void PointChanged(DependencyPropertyChangedEventArgs e)
    {
        if (!IsCollapsed)
        {
            SetLocation();
        }
    }

    private async void IsCollapsedChanged(DependencyPropertyChangedEventArgs _)
    {
        DragableBehavior.IsEnabled = !IsCollapsed;

        if (!IsCollapsed)
        {
            bool large = Window.Current.Bounds.Width > 1_600 && Window.Current.Bounds.Height > 800;
            (Root.Height, Root.Width) = large ? (480, 720) : (320, 480);
            
            StorageFile htmlFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/StreetView.html"));
            Uri fileUri = new Uri(htmlFile.Path);

            WebView.NavigationCompleted += WebView_NavigationCompleted;
            WebView.Source = fileUri;
        }

        void WebView_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            sender.NavigationCompleted -= WebView_NavigationCompleted;
            SetLocation();
        }
    }

    private void SetLocation()
    {
        if (Point.IsValid)
        {
            _ = WebView.ExecuteScriptAsync(FormattableString.Invariant($"setLocation({Point.Latitude}, {Point.Longitude}, {Point.Heading});"));
        }
    }
}
