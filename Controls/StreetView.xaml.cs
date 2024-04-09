using System;
using Windows.UI.Xaml;

namespace cycloid.Controls;

public sealed partial class StreetView : PointControl
{
    private readonly DispatcherTimer _requestTimeout;
    private readonly string _apiKey = App.Current.Configuration["Google:ServiceApiKey"];

    private bool _requestWaiting;

    public bool IsCollapsed
    {
        get => (bool)GetValue(IsCollapsedProperty);
        set => SetValue(IsCollapsedProperty, value);
    }

    public static readonly DependencyProperty IsCollapsedProperty =
        DependencyProperty.Register(nameof(IsCollapsed), typeof(bool), typeof(StreetView), new PropertyMetadata(true, (sender, e) => ((StreetView)sender).IsCollapsedChanged(e)));

    public double ImageWidth
    {
        get => (double)GetValue(ImageWidthProperty);
        set => SetValue(ImageWidthProperty, value);
    }

    public static readonly DependencyProperty ImageWidthProperty =
        DependencyProperty.Register(nameof(ImageWidth), typeof(double), typeof(StreetView), new PropertyMetadata(null));

    public double ImageHeight
    {
        get => (double)GetValue(ImageHeightProperty);
        set => SetValue(ImageHeightProperty, value);
    }

    public static readonly DependencyProperty ImageHeightProperty =
        DependencyProperty.Register(nameof(ImageHeight), typeof(double), typeof(StreetView), new PropertyMetadata(null));

    public bool RequestPending
    {
        get => (bool)GetValue(RequestPendingProperty);
        set => SetValue(RequestPendingProperty, value);
    }

    public static readonly DependencyProperty RequestPendingProperty =
        DependencyProperty.Register(nameof(RequestPending), typeof(bool), typeof(StreetView), new PropertyMetadata(false));

    public double HeadingOffset
    {
        get => (double)GetValue(HeadingOffsetProperty);
        set => SetValue(HeadingOffsetProperty, value);
    }

    public static readonly DependencyProperty HeadingOffsetProperty =
        DependencyProperty.Register(nameof(HeadingOffset), typeof(double), typeof(StreetView), new PropertyMetadata(0d, (sender, _) => ((StreetView)sender).UpdateImageUri()));

    public StreetView()
    {
        InitializeComponent();

        _requestTimeout = new() { Interval = TimeSpan.FromSeconds(5) };
        _requestTimeout.Tick += (_, _) => EndRequest();

        VisualStateManager.GoToState(this, "CollapsedState", false);
    }

    private void EndRequest()
    {
        _requestTimeout.Stop();
        RequestPending = false;
        if (_requestWaiting)
        {
            _requestWaiting = false;
            UpdateImageUri();
        }
    }

    private void UpdateImageUri()
    {
        if (!Point.IsValid)
        {
            ImageSource.UriSource = null;
            return;
        }

        Uri uri = new(FormattableString.Invariant($"https://maps.googleapis.com/maps/api/streetview?size={ImageWidth}x{ImageHeight}&location={Point.Latitude},{Point.Longitude}&heading={Point.Heading + (float)HeadingOffset}&pitch=0&fov=90&return_error_code=true&source=outdoor&key={_apiKey}"));

        if (uri == ImageSource.UriSource)
        {
            return;
        }

        _requestWaiting |= RequestPending;

        if (IsCollapsed)
        {
            return;
        }

        if (!RequestPending)
        {
            RequestPending = true;
            _requestWaiting = false;
            _requestTimeout.Start();
            ImageSource.UriSource = uri;
        }
    }

    private void Image_ImageOpened(object _1, RoutedEventArgs _2)
    {
        EndRequest();
    }

    private void Image_ImageFailed(object _1, ExceptionRoutedEventArgs _2)
    {
        EndRequest();
    }

    private void IsCollapsedChanged(DependencyPropertyChangedEventArgs _)
    {
        DragableBehavior.IsEnabled = !IsCollapsed;

        if (!IsCollapsed)
        {
            UpdateImageUri();
        }
    }
}
