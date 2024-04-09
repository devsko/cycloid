using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
using cycloid.Routing;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace cycloid.Controls;

public sealed partial class Profile : ViewModelControl,
    IRecipient<TrackChanged>,
    IRecipient<HoverPointChanged>,
    IRecipient<CurrentSectionChanged>,
    IRecipient<RouteChanged>
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

    private readonly Throttle<PointerRoutedEventArgs, Profile> _pointerMovedThrottle = new(
        static (e, @this) => @this.ThrottledPointerMoved(e),
        TimeSpan.FromMilliseconds(70));

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

    private int _trackIndexStep;

    private int _horizontalRulerStartTick;
    private int _horizontalRulerEndTick;

    private double _scrollerOffset;

    private bool _isOuterSizeChange;

    public Profile()
    {
        InitializeComponent();

        StrongReferenceMessenger.Default.Register<TrackChanged>(this);
        StrongReferenceMessenger.Default.Register<HoverPointChanged>(this);
        StrongReferenceMessenger.Default.Register<CurrentSectionChanged>(this);
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
        if (ViewModel.Track is not null)
        {
            ProcessChangeAsync(Change.Zoom).FireAndForget();
        }
    }

    private void ThrottledPointerMoved(PointerRoutedEventArgs e)
    {
        HoverPointValues.Enabled = true;
        ViewModel.HoverPoint = ViewModel.Track.Points.Search((float)(e.GetCurrentPoint(Root).Position.X / _horizontalScale)).Point;
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
                    _trackIndexStep = Math.Max(1, (int)((ViewModel.Track.Points.Count - 1) / _horizontalSize));

                    if (!double.IsInfinity(_horizontalScale))
                    {
                        GraphTransform.ScaleX = _horizontalScale;
                        GraphBottomTransform.ScaleX = _horizontalScale;
                    }

                    ResetTrack();
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
                    if (ViewModel.Track.Points.Count > 0)
                    {
                        EnsureTrack();
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

                if (_elevationDiff != 0)
                {
                    GraphTransform.ScaleY = -ActualHeight / _elevationDiff / (1 + GraphBottomMarginRatio + GraphTopMarginRatio);
                    GraphBottomTransform.ScaleY = -ActualHeight;

                    VerticalRuler.Children.Clear();
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

    private void ViewModelControl_SizeChanged(object _1, SizeChangedEventArgs _2)
    {
        // TODO
        // Weird behavior if HorizontalZoom equals 1 and a restored app window is maximized!

        Root.Width = ActualWidth * HorizontalZoom;
        _isOuterSizeChange = true;
    }

    private void Scroller_ViewChanged(object _1, ScrollViewerViewChangedEventArgs _2)
    {
        _scrollerOffset = Scroller.HorizontalOffset;

        if (Graph.Points.Count > 2000)
        {
            ResetTrack();
        }

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
            ProcessChangeAsync(ViewModel.Track is null ? Change.VerticalSize : Change.Zoom | Change.VerticalSize).FireAndForget();
        }
    }

    private void Root_PointerMoved(object _, PointerRoutedEventArgs e)
    {
        if (ViewModel.Track is not null)
        {
            _pointerMovedThrottle.Next(e, this);
        }
    }

    private void Root_PointerExited(object _1, PointerRoutedEventArgs _2)
    {
        _pointerMovedThrottle.Clear();
        ViewModel.HoverPoint = TrackPoint.Invalid;
        HoverPointValues.Enabled = false;
    }

    private void Root_Tapped(object _1, TappedRoutedEventArgs _2)
    {
        if (ViewModel.Track is not null && ViewModel.HoverPoint.IsValid)
        {
            if (ViewModel.Mode is Modes.Sections or Modes.POIs)
            {
                foreach (OnTrack section in ViewModel.Sections)
                {
                    if (section.IsCurrent(ViewModel.HoverPoint.Distance))
                    {
                        ViewModel.CurrentSection = section;
                        break;
                    }
                }
            }

            //
            //ViewModel.CurrentPoint = ViewModel.Track.Pin.Search(distance);
            //if (!Camera.AutoUpdate)
            //    Camera.Set(setView: false);
        }
    }

    private void Root_DoubleTapped(object _1, DoubleTappedRoutedEventArgs _2)
    {
        if (ViewModel.Track is not null && ViewModel.HoverPoint.IsValid)
        {
            StrongReferenceMessenger.Default.Send(new SetMapCenterMessage(ViewModel.HoverPoint));
        }
    }

    private void ZoomOut_Click(object _1, RoutedEventArgs _2)
    {
        HorizontalZoom = Math.Max(1, HorizontalZoom / 1.25);
    }

    private void ZoomIn_Click(object _1, RoutedEventArgs _2)
    {
        HorizontalZoom *= 1.25f;
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
        UpdateMarker(message.NewValue, HoverPointLine1, HoverPointLine2, HoverPointCircle);
    }

    void IRecipient<CurrentSectionChanged>.Receive(CurrentSectionChanged message)
    {
        ResetSection();
        EnsureSection();
    }

    void IRecipient<RouteChanged>.Receive(RouteChanged message)
    {
        ProcessChangeAsync(Change.Track).FireAndForget();
    }
}
