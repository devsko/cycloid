using System;
using System.Globalization;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using static System.Net.Mime.MediaTypeNames;

namespace cycloid.Controls;

public sealed partial class StreetView : UserControl
{
    private static readonly DispatcherTimer _requestTimeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
    private static readonly string _apiKey = App.Current.Configuration["Google:ServiceApiKey"];

    private bool _requestWaiting;

    public event EventHandler IsCollapsedChanged;

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
        DependencyProperty.Register(nameof(HeadingOffset), typeof(double), typeof(StreetView), new PropertyMetadata(0d, (sender, _) => ((StreetView)sender).HeadingOffsetChanged()));

    public TrackPoint? Location
    {
        get => (TrackPoint?)GetValue(LocationProperty);
        set => SetValue(LocationProperty, value);
    }

    public static readonly DependencyProperty LocationProperty =
        DependencyProperty.Register(nameof(Location), typeof(TrackPoint?), typeof(StreetView), new PropertyMetadata(null, (sender, _) => ((StreetView)sender).LocationChanged()));

    public StreetView()
    {
        InitializeComponent();

        ApplicationViewStates.CurrentStateChanged += (_, _) =>
        {
            DragableBehavior.Reset();
            DragableBehavior.IsEnabled = !IsCollapsed;

            IsCollapsedChanged?.Invoke(this, EventArgs.Empty);
            if (!IsCollapsed)
                UpdateImageUri();
        };

        CollapseButton.Click += (sender, e) => VisualStateManager.GoToState(this, "CollapsedState", true);
        ExpandButton.Click += (sender, e) => VisualStateManager.GoToState(this, "ExpandedState", true);

        SizeChanged += (sender, args) =>
        {
            ExpandedScale.CenterX = ActualWidth / 2;
            ExpandedScale.CenterY = ActualHeight / 2;
        };

        Image.ImageOpened += (_, _) => EndRequest();
        Image.ImageFailed += (_, _) => EndRequest();
        _requestTimeout.Tick += (_, _) => EndRequest();

        bool transitioned = VisualStateManager.GoToState(this, "CollapsedState", false);
        
        void EndRequest()
        {
            _requestTimeout.Stop();
            RequestPending = false;
            if (_requestWaiting)
            {
                _requestWaiting = false;
                UpdateImageUri();
            }
        }
    }

    public bool IsCollapsed => ApplicationViewStates.CurrentState?.Name == "CollapsedState";

    private void HeadingOffsetChanged()
    {
        UpdateImageUri();
    }

    private void LocationChanged()
    {
        UpdateImageUri();
    }

    private void UpdateImageUri()
    {
        if (Location == null)
        {
            ImageSource.UriSource = null;
            return;
        }

        Uri uri = new(FormattableString.Invariant($"https://maps.googleapis.com/maps/api/streetview?size={ImageWidth}x{ImageHeight}&location={Location.Value.Latitude},{Location.Value.Longitude}&heading={Location.Value.Heading + (float)HeadingOffset}&pitch=0&fov=90&return_error_code=true&source=outdoor&key={_apiKey}"));

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
}
