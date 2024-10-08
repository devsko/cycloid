using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Windows.Storage;
using Windows.UI.Xaml;

namespace cycloid.Controls;

public sealed partial class StreetView : TrackPointControl
{
    private readonly AsyncThrottle<TrackPoint, StreetView> _updateThrottle = new(
        static (value, @this, cancellationToken) => @this.SetLocationAsync(value, cancellationToken),
        TimeSpan.FromSeconds(1));

    private bool _isWebViewInitialized;
    private TaskCompletionSource<object> _setLocationTcs;

    public string GoogleApiKey { get; set; }

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

    private async void WebView_CoreWebView2Initialized(WebView2 sender, CoreWebView2InitializedEventArgs args)
    {
        WebView.CoreWebView2.Settings.IsWebMessageEnabled = true;
        WebView.NavigationCompleted += WebView_NavigationCompleted;
        WebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

        StorageFile templateFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Controls/StreetView.html"));
        string htmlTemplate = await FileIO.ReadTextAsync(templateFile);
        StorageFile htmlFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("StreetView.html", CreationCollisionOption.ReplaceExisting);
        await FileIO.WriteTextAsync(htmlFile, htmlTemplate.Replace("{{GoogleApiKey}}", GoogleApiKey));
        
        WebView.CoreWebView2.Navigate(htmlFile.Path);
    }

    private void CoreWebView2_WebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        string status = args.TryGetWebMessageAsString();
        WebView.Visibility = status == "OK" ? Visibility.Visible : Visibility.Collapsed;
        _setLocationTcs?.TrySetResult(null);
    }

    private void WebView_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        _isWebViewInitialized = true;
        Update();
    }

    protected override void PointChanged(DependencyPropertyChangedEventArgs e)
    {
        if (_isWebViewInitialized && !IsCollapsed)
        {
            Update();
        }
    }

    private void IsCollapsedChanged(DependencyPropertyChangedEventArgs __)
    {
        DragableBehavior.IsEnabled = !IsCollapsed;

        if (IsCollapsed)
        {
            WebView.Visibility = Visibility.Collapsed;
        }
        else
        {
            bool large = Window.Current.Bounds.Width > 1_600 && Window.Current.Bounds.Height > 800;
            (ContentRoot.Height, ContentRoot.Width) = large ? (480, 720) : (320, 480);

            if (_isWebViewInitialized)
            {
                Update();
            }
            else
            {
                _ = WebView.EnsureCoreWebView2Async();
            }
        }
    }

    private void Update()
    {
        if (Point.IsValid)
        {
            _updateThrottle.Next(Point, this);
        }
        else
        {
            WebView.Visibility = Visibility.Collapsed;
        }
    }

    private async Task SetLocationAsync(TrackPoint point, CancellationToken _)
    {
        _setLocationTcs = new();
        await WebView.ExecuteScriptAsync(FormattableString.Invariant(
            $"setLocation({point.Latitude}, {point.Longitude}, {point.Heading});"));
        await _setLocationTcs.Task;
    }
}
