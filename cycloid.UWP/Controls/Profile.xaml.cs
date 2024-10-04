using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
using cycloid.Routing;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace cycloid.Controls;

public sealed partial class Profile : ViewModelControl,
    IRecipient<TrackChanged>,
    IRecipient<HoverPointChanged>,
    IRecipient<CurrentPointChanged>,
    IRecipient<SelectionChanged>,
    IRecipient<CurrentSectionChanged>,
    IRecipient<RouteChanged>,
    IRecipient<BringTrackIntoViewMessage>
{
    [Flags]
    private enum Change
    {
        _SetHorizontalSize = 1,
        _EnsureGraph = 4,
        _VerticalRuler = 8,
        _Marker = 16,
        _VerticalTranslation = 32,
        _Bottom = 64,

        Zoom = _SetHorizontalSize,
        Track = _SetHorizontalSize | _EnsureGraph | _VerticalRuler | _Marker | _Bottom,
        HorizontalSize = _EnsureGraph | _Marker,
        Scroll = _EnsureGraph,
        VerticalSize = _VerticalRuler | _Marker | _VerticalTranslation,
        MaxElevation = _VerticalRuler | _Marker,
    }

    private const float GraphBottomMarginRatio = .08f;
    private const float GraphTopMarginRatio = .1f;
    private const float HorizontalRulerTickMinimumGap = 50;
    private const float VerticalRulerTickMinimumGap = 25;

    private readonly Throttle<double, Profile> _setHoverPointThrottle = new(
        static (x, @this) => @this.SetHoverPoint(x),
        TimeSpan.FromMilliseconds(100));

    private readonly PeriodicAction<Profile, int> _periodicScroll = new(
        static (@this, amount) => @this.ScrollToRelative(amount), 
        TimeSpan.FromMilliseconds(50));

    public double HorizontalZoom
    {
        get => (double)GetValue(HorizontalZoomProperty);
        set => SetValue(HorizontalZoomProperty, value);
    }

    public static readonly DependencyProperty HorizontalZoomProperty = 
        DependencyProperty.Register(nameof(HorizontalZoom), typeof(double), typeof(Profile), new PropertyMetadata(1d, (d, e) => ((Profile)d).HorizontalZoomChanged(e)));

    private float _maxElevation;
    private float _elevationDiff;

    private double _horizontalSize;
    private double _horizontalScale;

    private int _horizontalRulerStartTick;
    private int _horizontalRulerEndTick;

    private double _scrollerOffset;

    private bool _isOuterSizeChange;

    public Profile()
    {
        InitializeComponent();

        StrongReferenceMessenger.Default.Register<TrackChanged>(this);
        StrongReferenceMessenger.Default.Register<HoverPointChanged>(this);
        StrongReferenceMessenger.Default.Register<CurrentPointChanged>(this);
        StrongReferenceMessenger.Default.Register<SelectionChanged>(this);
        StrongReferenceMessenger.Default.Register<CurrentSectionChanged>(this);
        StrongReferenceMessenger.Default.Register<BringTrackIntoViewMessage>(this);
    }

    //public ObservableCollection<TrackPoi> TrackPois
    //{
    //    get => _trackPois;
    //    set
    //    {
    //        if (!object.Equals(_sections, value))
    //        {
    //            _trackPois = value;
    //            _trackPois.CollectionChanged += (_, args) =>
    //            {
    //                if (args.Action == NotifyCollectionChangedAction.Add)
    //                {
    //                    OnTrackPoiAdded((TrackPoi)args.NewItems[0]);
    //                }
    //            };
    //        }
    //    }
    //}

    // OnTrackPoiAdded
    // OnCurrentPointChanged

    private void HorizontalZoomChanged(DependencyPropertyChangedEventArgs _)
    {
        if (ViewModel.HasTrack)
        {
            ProcessChangeAsync(Change.Zoom).FireAndForget();
        }
    }

    private double GetOffset(TrackPoint point) => point.IsValid ? point.Distance * _horizontalScale - _scrollerOffset : 0;

    private async Task ProcessChangeAsync(Change change)
    {
        while (change != 0)
        {
            if ((change & Change._SetHorizontalSize) != 0)
            {
                change &= ~Change._SetHorizontalSize;

                double horizontalSize = ActualWidth * HorizontalZoom;
                if (_horizontalSize != horizontalSize || _trackTotalDistance != ViewModel.Track.Points.Total.Distance)
                {
                    double fixPoint = GetFixPoint();
                    double oldScrollerOffset = _scrollerOffset;
                    double oldHorizontalSize = _horizontalSize;

                    Root.Width = horizontalSize;
                    Scroller.UpdateLayout();
                    _horizontalSize = Root.ActualWidth;

                    double ratio = _horizontalSize / oldHorizontalSize - 1;
                    if (!double.IsInfinity(ratio))
                    {
                        Scroller.ChangeView(Math.Clamp(fixPoint * ratio + oldScrollerOffset, 0, Math.Max(0, _horizontalSize - ActualWidth)), null, null, true);
                    }

                    _trackTotalDistance = ViewModel.Track.Points.Total.Distance;
                    _horizontalScale = _horizontalSize / _trackTotalDistance;

                    if (!double.IsInfinity(_horizontalScale))
                    {
                        GraphTransform.ScaleX = _horizontalScale;
                        GraphBottomTransform.ScaleX = _horizontalScale;
                    }

                    ResetTrack();
                    ResetSelection();
                    ResetSection();
                    ResetHorizontalRuler();

                    RelocateTrackPois();

                    change |= Change.HorizontalSize;

                    double GetFixPoint()
                    {
                        if (ViewModel.CurrentPoint.IsValid)
                        {
                            double point = ViewModel.CurrentPoint.Distance * _horizontalScale;
                            if (point >= _scrollerOffset && point <= _scrollerOffset + ActualWidth)
                            {
                                return point;
                            }
                        }

                        return ActualWidth / 2 + _scrollerOffset;
                    }
                }
            }
            if ((change & Change._EnsureGraph) != 0)
            {
                change &= ~Change._EnsureGraph;

                using (await ViewModel.Track.RouteBuilder.ChangeLock.EnterAsync(default))
                {
                    if (ViewModel.Track.Points.IsEmpty)
                    {
                        if (_maxElevation != 0)
                        {
                            _maxElevation = 0;
                            _elevationDiff = 0;
                            change |= Change.MaxElevation;
                        }
                    }
                    else
                    {
                        EnsureTrack();
                        EnsureSelection();
                        EnsureSection();
                        EnsureHorizontalRuler();

                        float maxElevation = ViewModel.Track.Points
                            .Enumerate((float)(_scrollerOffset / _horizontalScale), (float)((ActualWidth + _scrollerOffset) / _horizontalScale))
                            .Max(point => point.Altitude);

                        if (_maxElevation != maxElevation)
                        {
                            _maxElevation = maxElevation;
                            _elevationDiff = maxElevation - ViewModel.Track.Points.MinAltitude;
                            change |= Change.MaxElevation;
                        }
                    }
                }
            }
            if ((change & Change._VerticalRuler) != 0)
            {
                change &= ~Change._VerticalRuler;

                VerticalRuler.Children.Clear();

                if (_elevationDiff != 0)
                {
                    GraphTransform.ScaleY = -ActualHeight / _elevationDiff / (1 + GraphBottomMarginRatio + GraphTopMarginRatio);
                    GraphBottomTransform.ScaleY = -ActualHeight;

                    DrawVerticalRuler();
                }
            }
            if ((change & Change._Marker) != 0)
            {
                change &= ~Change._Marker;

                UpdateMarker(ViewModel.CurrentPoint, CurrentPointLine1, CurrentPointLine2, CurrentPointCircle);
                UpdateMarker(ViewModel.HoverPoint, HoverPointLine1, HoverPointLine2, HoverPointCircle);
            }
            if ((change & Change._VerticalTranslation) != 0)
            {
                change &= ~Change._VerticalTranslation;

                GraphTransform.TranslateY = ActualHeight * (1 - GraphBottomMarginRatio);
                GraphBottomTransform.TranslateY = ActualHeight;
                Canvas.SetTop(TrackPois, ActualHeight - 9);
            }
            if ((change & Change._Bottom) != 0)
            {
                change &= ~Change._Bottom;

                GraphBottom.Rect = new Rect(0, 0, ViewModel.Track?.Points.Total.Distance ?? 0, GraphBottomMarginRatio);
            }
        }
    }

    private void RelocateTrackPois()
    {
        //for (int i = 0; i < TrackPois.Children.Count; i++)
        //    Canvas.SetLeft(TrackPois.Children[i], _trackPois[i].Point.Distance * _horizontalScale);
    }

    private void UpdateMarker(TrackPoint point, LineGeometry line1, LineGeometry line2, EllipseGeometry circle)
    {
        if (point.IsValid)
        {
            double x = point.Distance * _horizontalScale;
            double y = (point.Altitude - ViewModel.Track.Points.MinAltitude) * -ActualHeight / _elevationDiff / (1 + GraphBottomMarginRatio + GraphTopMarginRatio) + ActualHeight * (1 - GraphBottomMarginRatio);

            line1.StartPoint = new Point(x, 14);
            line1.EndPoint = new Point(x, y - 2);
            line2.StartPoint = new Point(x, y + 2);
            line2.EndPoint = new Point(x, ActualHeight);
            circle.Center = new Point(x, y);
        }
    }

    private void ViewModelControl_Loaded(object sender, RoutedEventArgs e)
    {
        _graphTransform = (Transform)Root.Resources["GraphTransform"];
    }

    private void ViewModelControl_SizeChanged(object _1, SizeChangedEventArgs _2)
    {
        // TODO
        // Weird behavior if HorizontalZoom equals 1 and a restored app window is maximized!

        Scroller.MaxWidth = ActualWidth;
        Root.Width = ActualWidth * HorizontalZoom;
        _isOuterSizeChange = true;
    }

    private void Scroller_ViewChanged(object _1, ScrollViewerViewChangedEventArgs _2)
    {
        _scrollerOffset = Scroller.HorizontalOffset;

        ProcessChangeAsync(Change.Scroll).FireAndForget();

        Point pointer = new(
            Window.Current.CoreWindow.PointerPosition.X - Window.Current.Bounds.X,
            Window.Current.CoreWindow.PointerPosition.Y - Window.Current.Bounds.Y);

        Panel panel = VisualTreeHelper.FindElementsInHostCoordinates(pointer, this).OfType<Panel>().FirstOrDefault();
        if (panel is not null && (panel == Root || panel.FindParents().Contains(Root)))
        {
            ViewModel.HoverPoint = ViewModel.Track.Points.Search((float)(Window.Current.Content.TransformToVisual(Root).TransformPoint(pointer).X / _horizontalScale)).Point;
        }
    }

    private void Root_SizeChanged(object _1, SizeChangedEventArgs _2)
    {
        if (_isOuterSizeChange)
        {
            _isOuterSizeChange = false;
            ProcessChangeAsync(!ViewModel.HasTrack ? Change.VerticalSize : Change.Zoom | Change.VerticalSize).FireAndForget();
        }
    }

    private bool _isCaptured;
    private bool _isEntered;

    private void Root_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isCaptured = Root.CapturePointer(e.Pointer);
        ViewModel.StartSelection(5 / _horizontalScale);
    }

    private void Root_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        Root.ReleasePointerCapture(e.Pointer);
    }

    private void Root_PointerMoved(object _, PointerRoutedEventArgs e)
    {
        if (ViewModel.HasTrack)
        {
            Window.Current.CoreWindow.PointerCursor =
                ViewModel.GetHoveredSelectionBorder(5 / _horizontalScale) == null
                ? new CoreCursor(CoreCursorType.Arrow, 0)
                : new CoreCursor(CoreCursorType.SizeWestEast, 10);

            double x = e.GetCurrentPoint(Root).Position.X;

            double subPixel = ActualWidth - e.GetCurrentPoint(this).Position.X;
            if (subPixel > 0 && subPixel < 1)
            {
                x += subPixel;
            }

            if (_isCaptured)
            {
                if (x >= _scrollerOffset + ActualWidth - 3)
                {
                    _periodicScroll.Start(this, 10);
                }
                else if (x <= _scrollerOffset + 3)
                {
                    _periodicScroll.Start(this, -10);
                }
                else
                {
                    _periodicScroll.Stop();
                }
            }

            if (!_periodicScroll.IsRunning)
            {
                _setHoverPointThrottle.Next(x, this);
            }
        }
    }

    private void ScrollToRelative(int amount)
    {
        Scroller.ChangeView(_scrollerOffset + amount, null, null, true);
        Scroller.UpdateLayout();
        SetHoverPoint(Scroller.HorizontalOffset + (amount < 0 ? 0 : ActualWidth));
    }

    private void SetHoverPoint(double x)
    {
        HoverPointValues.Enabled = true;
        float distance = Math.Clamp((float)(x / _horizontalScale), 0, ViewModel.Track.Points.Total.Distance);
        ViewModel.HoverPoint = ViewModel.Track.Points.Search(distance).Point;
        ViewModel.ContinueSelection();
    }

    private void Root_PointerEntered(object _1, PointerRoutedEventArgs _2)
    {
        _isEntered = true;
    }

    private void Root_PointerExited(object _1, PointerRoutedEventArgs _2)
    {
        _isEntered = false;
        if (!_isCaptured)
        {
            PointerExit();
        }
    }

    private void PointerExit()
    {
        _setHoverPointThrottle.Clear();
        ViewModel.HoverPoint = TrackPoint.Invalid;
        HoverPointValues.Enabled = false;
        Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Arrow, 0);
    }

    private void Root_PointerCaptureLost(object _1, PointerRoutedEventArgs _2)
    {
        _isCaptured = false;
        _periodicScroll.Stop();
        ViewModel.EndSelection();
        if (!_isEntered)
        {
            PointerExit();
        }
    }

    private void Root_Tapped(object _1, TappedRoutedEventArgs _2)
    {
        if (ViewModel.HasTrack && ViewModel.HoverPoint.IsValid)
        {
            if (!ViewModel.IsEditMode)
            {
                ViewModel.CurrentPoint = ViewModel.HoverPoint;

                foreach (OnTrack section in ViewModel.Sections)
                {
                    if (section.IsCurrent(ViewModel.HoverPoint.Distance))
                    {
                        ViewModel.CurrentSection = section;
                        break;
                    }
                }
            }
        }
    }

    private void Root_DoubleTapped(object _1, DoubleTappedRoutedEventArgs _2)
    {
        if (ViewModel.HasTrack && ViewModel.HoverPoint.IsValid)
        {
            StrongReferenceMessenger.Default.Send(new BringTrackIntoViewMessage(ViewModel.HoverPoint));
        }
    }

    private void ZoomOut_Click(object _1, RoutedEventArgs _2)
    {
        HorizontalZoom = Math.Max(1, HorizontalZoom / 1.5);
    }

    private void ZoomIn_Click(object _1, RoutedEventArgs _2)
    {
        HorizontalZoom *= 1.5f;
    }

    void IRecipient<TrackChanged>.Receive(TrackChanged message)
    {
        HorizontalZoom = 1;
        Root.Width = double.NaN;
        _horizontalSize = 0;
        _maxElevation = 0;
        _elevationDiff = 0;
        ProcessChangeAsync(Change.Track).FireAndForget();
        if (message.OldValue is not null)
        {
            StrongReferenceMessenger.Default.Unregister<RouteChanged>(this);
        }
        if (message.NewValue is not null)
        {
            StrongReferenceMessenger.Default.Register<RouteChanged>(this);
        }
    }

    void IRecipient<HoverPointChanged>.Receive(HoverPointChanged message)
    {
        UpdateMarker(message.Value, HoverPointLine1, HoverPointLine2, HoverPointCircle);
    }

    void IRecipient<CurrentPointChanged>.Receive(CurrentPointChanged message)
    {
        UpdateMarker(message.Value, CurrentPointLine1, CurrentPointLine2, CurrentPointCircle);
    }

    void IRecipient<SelectionChanged>.Receive(SelectionChanged _)
    {
        ResetSelection();
        EnsureSelection();
    }

    void IRecipient<CurrentSectionChanged>.Receive(CurrentSectionChanged _)
    {
        ResetSection();
        EnsureSection();
    }

    void IRecipient<RouteChanged>.Receive(RouteChanged message)
    {
        ProcessChangeAsync(Change.Track).FireAndForget();
    }

    private bool IsInView(float distance)
    {
        return distance >= Scroller.HorizontalOffset / _horizontalScale && distance <= (Scroller.HorizontalOffset + ActualWidth) / _horizontalScale;
    }

    void IRecipient<BringTrackIntoViewMessage>.Receive(BringTrackIntoViewMessage message)
    {
        float distance1 = message.Value.Item1.Distance;
        if (message.Value.Item2.IsValid)
        {
            float distance2 = message.Value.Item2.Distance;
            HorizontalZoom = Math.Min(15, _trackTotalDistance * .9 / Math.Abs(distance1 - distance2));
            Scroller.ChangeView(((distance1 + distance2) * _horizontalScale - ActualWidth) / 2, null, null);
        }
        else
        {
            if (!IsInView(distance1))
            {
                Scroller.ChangeView(distance1 * _horizontalScale - ActualWidth / 2, null, null);
            }
        }
    }
}
