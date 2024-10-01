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

        WebView.Visibility = Visibility.Collapsed;
        WebView.CoreWebView2Initialized += WebView_CoreWebView2Initialized;

        VisualStateManager.GoToState(this, "CollapsedState", false);
    }

    private void WebView_CoreWebView2Initialized(WebView2 sender, CoreWebView2InitializedEventArgs args)
    {
        WebView.CoreWebView2.Settings.IsWebMessageEnabled = true;
        WebView.NavigationCompleted += WebView_NavigationCompleted;
        WebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
    }

    private void CoreWebView2_WebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        string status = args.TryGetWebMessageAsString();
        WebView.Visibility = status == "OK" ? Visibility.Visible : Visibility.Collapsed;
    }

    protected override void PointChanged(DependencyPropertyChangedEventArgs e)
    {
        if (!IsCollapsed)
        {
            Update();
        }
    }

    private async void IsCollapsedChanged(DependencyPropertyChangedEventArgs __)
    {
        DragableBehavior.IsEnabled = !IsCollapsed;

        if (IsCollapsed)
        {
            WebView.Visibility = Visibility.Collapsed;
        }
        else
        {
            bool large = Window.Current.Bounds.Width > 1_600 && Window.Current.Bounds.Height > 800;
            (Root.Height, Root.Width) = large ? (480, 720) : (320, 480);
            
            StorageFile htmlFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/StreetView.html"));
            Uri fileUri = new(htmlFile.Path);

            WebView.Source = fileUri;
        }
    }

    private void Update()
    {
        if (Point.IsValid)
        {
            _ = SetLocationAsync();
        }
        else
        {
            WebView.Visibility = Visibility.Collapsed;
        }
    }

    private bool isWebViewInitialized;

    private void WebView_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        isWebViewInitialized = true;
        Update();
    }

    private async Task SetLocationAsync()
    {
        if (isWebViewInitialized && Point.IsValid)
        {
            await WebView.ExecuteScriptAsync(FormattableString.Invariant($"setLocation({Point.Latitude}, {Point.Longitude}, {Point.Heading});"));
        }
    }
}
