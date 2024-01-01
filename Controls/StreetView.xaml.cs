using System;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace cycloid.Controls;

public sealed partial class StreetView : PointControl
{
    private readonly DispatcherTimer _requestTimeout;
    private readonly string _apiKey = App.Current.Configuration["Google:ServiceApiKey"];

    private bool _requestWaiting;
    private Size _collapsedSize;
    private Size _collapsedMargin;

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

    public bool IsCollapsed => ApplicationViewStates.CurrentState?.Name == "CollapsedState";

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

    private void ExpandButton_Click(object sender, RoutedEventArgs e)
    {
        _collapsedSize = new Size(ActualWidth, ActualHeight);
        _collapsedMargin = new Size(Margin.Right, Margin.Bottom);
        VisualStateManager.GoToState(this, "ExpandedState", true);
    }

    private void CollapseButton_Click(object sender, RoutedEventArgs e)
    {
        TranslateXAnimation.To = ActualWidth + Margin.Right - (_collapsedSize.Width + _collapsedMargin.Width);
        TranslateYAnimation.To = ActualHeight + Margin.Bottom - (_collapsedSize.Height + _collapsedMargin.Height);
        ScaleXAnimation.To = _collapsedSize.Width / ActualWidth;
        ScaleYAnimation.To = _collapsedSize.Height / ActualHeight;

        VisualStateManager.GoToState(this, "CollapsedState", true);
    }

    private void Image_ImageOpened(object sender, RoutedEventArgs e)
    {
        EndRequest();
    }

    private void Image_ImageFailed(object sender, ExceptionRoutedEventArgs e)
    {
        EndRequest();
    }

    private void ApplicationViewStates_CurrentStateChanged(object sender, VisualStateChangedEventArgs e)
    {
        DragableBehavior.Reset();
        DragableBehavior.IsEnabled = !IsCollapsed;

        if (!IsCollapsed)
        {
            UpdateImageUri();
        }
    }
}
