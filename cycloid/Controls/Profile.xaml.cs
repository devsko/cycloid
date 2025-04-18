﻿using System.Numerics;
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

        Zoom = _SetHorizontalSize,
        Track = _SetHorizontalSize | _EnsureGraph | _VerticalRuler | _Marker,
        HorizontalSize = _EnsureGraph | _Marker,
        Scroll = _EnsureGraph,
        VerticalSize = _VerticalRuler | _Marker | _VerticalTranslation,
        ElevationDiff = _VerticalRuler | _Marker,
    }

    private const float GraphBottomMargin = 13;
    private const float GraphBottomMarginRatio = .02f;
    private const float GraphTopMarginRatio = .05f;
    private const float HorizontalRulerTickMinimumGap = 50;
    private const float VerticalRulerTickMinimumGap = 25;

    private readonly PeriodicAction<Profile, int, double> _periodicScroll = new(
        static (@this, amount) =>
        {
            @this.Scroller.ChangeView(@this._scrollerOffset + amount, null, null, true);
            @this.Scroller.UpdateLayout();
        },
        static (@this, amount) => @this.Scroller.HorizontalOffset + (amount < 0 ? 0 : @this.ActualWidth),
        TimeSpan.FromMilliseconds(50));

    [GeneratedDependencyProperty(DefaultValue = 1.0)]
    public partial double HorizontalZoom { get; set; }

    partial void OnHorizontalZoomChanged(double newValue)
    {
        UpdateSplitterMargin();
        if (ViewModel.HasTrack)
        {
            ProcessChangeAsync(Change.Zoom).FireAndForget();
        }
    }

    private float _maxElevation;
    private float _minElevation;

    private double _horizontalSize;
    private double _horizontalScale;
    private double _verticalScale;

    private int _horizontalRulerStartTick;
    private int _horizontalRulerEndTick;

    private double _scrollerOffset;

    private bool _isOuterSizeChange;

    private GeneralTransform _windowToControl;

    public Profile()
    {
        InitializeComponent();
        InitializeVisual();

        _lineStrokeBrush = (SolidColorBrush)this.FindResource("TrackGraphOutlineBrush");

        StrongReferenceMessenger.Default.Register<TrackChanged>(this);
        StrongReferenceMessenger.Default.Register<HoverPointChanged>(this);
        StrongReferenceMessenger.Default.Register<CurrentPointChanged>(this);
        StrongReferenceMessenger.Default.Register<SelectionChanged>(this);
        StrongReferenceMessenger.Default.Register<CurrentSectionChanged>(this);
        StrongReferenceMessenger.Default.Register<BringTrackIntoViewMessage>(this);
    }

    private double GetOffset(TrackPoint point)
    {
        return point.IsValid ? point.Distance * _horizontalScale - _scrollerOffset : 0;
    }

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
                    Splitter.Width = horizontalSize;
                    Scroller.UpdateLayout();
                    if (Root.ActualWidth < ActualWidth)
                    {
                        // WORKAROUND: Maximizing while HorizontalZoom = 1
                        _isOuterSizeChange = true;
                    }
                    else
                    {
                        _horizontalSize = Root.ActualWidth;

                        double ratio = _horizontalSize / oldHorizontalSize - 1;
                        if (!double.IsInfinity(ratio))
                        {
                            // TODO
                            Scroller.ChangeView(Math.Clamp(fixPoint * ratio + oldScrollerOffset, 0, Math.Max(0, _horizontalSize - ActualWidth)), null, null, true);
                        }

                        _trackTotalDistance = ViewModel.Track.Points.Total.Distance;
                        _horizontalScale = _horizontalSize / _trackTotalDistance;

                        if (!double.IsInfinity(_horizontalScale))
                        {
                            _container.Scale = new Vector2((float)_horizontalScale, _container.Scale.Y);
                        }

                        ResetHorizontalRuler();

                        change |= Change.HorizontalSize;
                    }
                    double GetFixPoint()
                    {
                        // TODO current point nur wenn im sichtbaren Bereich
                        if (ViewModel.CurrentPoint.IsValid)
                        {
                            double point = ViewModel.CurrentPoint.Distance * _horizontalScale;
                            if (point >= _scrollerOffset && point <= _scrollerOffset + ActualWidth)
                            {
                                return point;
                            }
                        }

                        // TODO scroller in der 2. Hälfte, schnell größer/kleiner machen verschiebt den Scroller
                        return _scrollerOffset;
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
                            _minElevation = 0;
                            change |= Change.ElevationDiff;
                        }
                    }
                    else
                    {
                        (float minElevation, float maxElevation) = EnsureTrack();
                        EnsureHorizontalRuler();

                        if (_maxElevation != maxElevation || _minElevation != minElevation)
                        {
                            _maxElevation = maxElevation;
                            _minElevation = minElevation;
                            change |= Change.ElevationDiff;
                        }
                    }
                }
            }
            if ((change & Change._VerticalRuler) != 0)
            {
                change &= ~Change._VerticalRuler;

                VerticalRuler.Children.Clear();

                if (_maxElevation != _minElevation)
                {
                    _verticalScale = ((float)ActualHeight - GraphBottomMargin) / (1 + GraphBottomMarginRatio + GraphTopMarginRatio) / (_maxElevation - _minElevation);
                    _container.Scale = new Vector2(_container.Scale.X, -(float)_verticalScale);
                    _container.Offset = new Vector2(0, ElevationToY(_minElevation));

                    DrawVerticalRuler();
                }
            }
            if ((change & Change._Marker) != 0)
            {
                change &= ~Change._Marker;

                UpdateMarker(ViewModel.CurrentPoint, CurrentPointLine1, CurrentPointLine2, CurrentPointCircle, false);
                UpdateMarker(ViewModel.HoverPoint, HoverPointLine1, HoverPointLine2, HoverPointCircle, true);
                Canvas.SetLeft(HoverPointValues, GetOffset(ViewModel.HoverPoint));
            }
            if ((change & Change._VerticalTranslation) != 0)
            {
                change &= ~Change._VerticalTranslation;

                _container.Offset = new Vector2(0, ElevationToY(_minElevation));
            }
        }
    }

    private void UpdateMarker(TrackPoint point, LineGeometry line1, LineGeometry line2, EllipseGeometry circle, bool outsideScroller)
    {
        if (point.IsValid)
        {
            double x = DistanceToX(point.Distance);
            if (outsideScroller)
            {
                x -= Scroller.HorizontalOffset;
            }
            double y = ActualHeight * (1 - GraphBottomMarginRatio) - (ActualHeight - GraphBottomMargin) * (point.Altitude - _minElevation) / (_maxElevation - _minElevation) / (1 + GraphBottomMarginRatio + GraphTopMarginRatio) - GraphBottomMargin;

            line1.StartPoint = new Point(x, 14);
            line1.EndPoint = new Point(x, y - 2);
            line2.StartPoint = new Point(x, y + 2);
            line2.EndPoint = new Point(x, ActualHeight);
            circle.Center = new Point(x, y);
        }
    }

    public float XToDistance(double x) => Math.Clamp((float)(x / _horizontalScale), 0, ViewModel.Track.Points.Total.Distance);

    private float DistanceToX(float distance) => distance * (float)_horizontalScale;

    private float ElevationToY(float elevation) => (float)ActualHeight * (1 - GraphBottomMarginRatio) + (elevation - TrackPoint.MinAltitudeValue) * (float)_verticalScale - GraphBottomMargin;

    //private float YToElevation(float y) => y / (float)_verticalScale + TrackPoint.MinAltitudeValue;

    private void ViewModelControl_SizeChanged(object _1, SizeChangedEventArgs _2)
    {
        Scroller.MaxWidth = ActualWidth;
        Root.Width = ActualWidth * HorizontalZoom;
        Splitter.Width = ActualWidth * HorizontalZoom;
        _windowToControl = Window.Current.Content.TransformToVisual(this);
        _isOuterSizeChange = true;
    }

    private void Scroller_ViewChanged(object _1, ScrollViewerViewChangedEventArgs _2)
    {
        _scrollerOffset = Scroller.HorizontalOffset;
        SetHoverPoint(_windowToControl.TransformPoint(new(Window.Current.CoreWindow.PointerPosition.X - Window.Current.Bounds.X, 0)).X + _scrollerOffset);

        ProcessChangeAsync(Change.Scroll).FireAndForget();
    }

    private void Root_SizeChanged(object _1, SizeChangedEventArgs _2)
    {
        _visual.Size = new Vector2((float)Root.ActualWidth, (float)Root.ActualHeight);
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
            if (!Splitter.IsPointerOver)
            {
                Window.Current.CoreWindow.PointerCursor =
                    ViewModel.GetHoveredSelectionBorder(5 / _horizontalScale) == null
                    ? new CoreCursor(CoreCursorType.Arrow, 0)
                    : new CoreCursor(CoreCursorType.SizeWestEast, 10);
            }

            ScrollIfNeeded(e, _isCaptured,
                static (@this, _, x) => @this.SetHoverPoint(x), 
                0);
        }
    }

    public void ScrollIfNeeded<TState>(PointerRoutedEventArgs e, bool isCaptured, Action<Profile, TState, double> payload, TState state)
    {
        double x = e.GetCurrentPoint(Root).Position.X;

        double subPixel = ActualWidth - e.GetCurrentPoint(this).Position.X;
        if (subPixel is > 0 and < 1)
        {
            x += subPixel;
        }

        if (isCaptured)
        {
            if (x >= _scrollerOffset + ActualWidth - 3)
            {
                _periodicScroll.Start(this, 20, payload, state);
            }
            else if (x <= _scrollerOffset + 3)
            {
                _periodicScroll.Start(this, -20, payload, state);
            }
            else
            {
                _periodicScroll.Stop();
            }
        }

        if (!_periodicScroll.IsRunning)
        {
            payload(this, state, x);
        }
    }

    public void StopScroll()
    {
        _periodicScroll.Stop();
    }

    private void SetHoverPoint(double x)
    {
        if (Splitter.IsPointerOver)
        {
            ViewModel.HoverPoint = TrackPoint.Invalid;
        }
        else if (_isEntered)
        {
            HoverPointValues.Enabled = true;
            ViewModel.HoverPoint = ViewModel.Track.Points.Search(XToDistance(x)).Point;
            ViewModel.ContinueSelection();
        }
    }

    private void Root_PointerEntered(object _1, PointerRoutedEventArgs _2)
    {
        _isEntered = true;
    }

    private void Root_PointerExited(object _1, PointerRoutedEventArgs _2)
    {
        _isEntered = false;
        if (!_isCaptured && !Splitter.IsPointerCaptured)
        {
            PointerExit();
        }
    }

    private void PointerExit()
    {
        ViewModel.HoverPoint = TrackPoint.Invalid;
        HoverPointValues.Enabled = false;
        Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Arrow, 0);
    }

    private void Root_PointerCaptureLost(object _1, PointerRoutedEventArgs _2)
    {
        _isCaptured = false;
        StopScroll();
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

    private void UpdateSplitterMargin()
    {
        bool marginForScroller = HorizontalZoom > 1.0;
        Splitter.Height = TrackSplitter.SplitHeight + (marginForScroller ? 0 : TrackSplitter.MarginForScrollBar);
        Splitter.Margin = marginForScroller ? new(0, 0, 0, TrackSplitter.MarginForScrollBar) : default;
    }

    void IRecipient<TrackChanged>.Receive(TrackChanged message)
    {
        HorizontalZoom = 1;
        Root.Width = double.NaN;
        Splitter.Width = double.NaN;
        _horizontalSize = 0;
        _maxElevation = 0;
        _minElevation = 0;
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
        UpdateMarker(message.Value, HoverPointLine1, HoverPointLine2, HoverPointCircle, true);
    }

    void IRecipient<CurrentPointChanged>.Receive(CurrentPointChanged message)
    {
        UpdateMarker(message.Value, CurrentPointLine1, CurrentPointLine2, CurrentPointCircle, false);
    }

    void IRecipient<SelectionChanged>.Receive(SelectionChanged _)
    {
        EnsureTrack();
    }

    void IRecipient<CurrentSectionChanged>.Receive(CurrentSectionChanged _)
    {
        EnsureTrack();
    }

    void IRecipient<RouteChanged>.Receive(RouteChanged message)
    {
        ProcessChangeAsync(Change.Track).FireAndForget();
    }

    private bool IsInView(float distance)
    {
        float x = DistanceToX(distance);

        return x >= Scroller.HorizontalOffset && x <= Scroller.HorizontalOffset + ActualWidth;
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
