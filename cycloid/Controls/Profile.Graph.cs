using CommunityToolkit.WinUI.Helpers;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;

namespace cycloid.Controls;

partial class Profile
{
    private float _trackTotalDistance;

    private ICanvasResourceCreator _resourceCreator;
    private CompositionPathGeometry _trackGeometry;
    private CompositionPathGeometry _sectionGeometry;
    private CompositionPathGeometry _selectionGeometry;
    private CompositionContainerShape _container;
    private ShapeVisual _visual;

    private Color _trackGraphOutlineColor;
    private Color _trackGraphFillColor;
    private double _trackGraphOpacity;
    private Color _sectionGraphFillColor;
    private double _sectionGraphOpacity;
    private Color _selectionGraphFillColor;
    private double _selectionGraphOpacity;

    private readonly ThemeListener _themeListener = new();

    private void UpdateTheme()
    {
        _trackGraphOutlineColor = (Color)App.Current.Resources["TrackGraphOutline"];
        _trackGraphFillColor = (Color)App.Current.Resources["TrackGraphFill"];
        _trackGraphOpacity = (double)App.Current.Resources["TrackGraphOpacity"];
        _sectionGraphFillColor = (Color)App.Current.Resources["SectionGraphFill"];
        _sectionGraphOpacity = (double)App.Current.Resources["SectionGraphOpacity"];
        _selectionGraphFillColor = (Color)App.Current.Resources["SelectionGraphFill"];
        _selectionGraphOpacity = (double)App.Current.Resources["SelectionGraphOpacity"];
        
        ((CompositionSpriteShape)_container.Shapes[0]).StrokeBrush = Window.Current.Compositor.CreateColorBrush(_trackGraphOutlineColor);
        ((CompositionSpriteShape)_container.Shapes[0]).FillBrush = Window.Current.Compositor.CreateColorBrush(Color.FromArgb((byte)(_trackGraphOpacity * 255), _trackGraphFillColor.R, _trackGraphFillColor.G, _trackGraphFillColor.B));
        ((CompositionSpriteShape)_container.Shapes[1]).FillBrush = Window.Current.Compositor.CreateColorBrush(Color.FromArgb((byte)(_sectionGraphOpacity * 255), _sectionGraphFillColor.R, _sectionGraphFillColor.G, _sectionGraphFillColor.B));
        ((CompositionSpriteShape)_container.Shapes[2]).FillBrush = Window.Current.Compositor.CreateColorBrush(Color.FromArgb((byte)(_selectionGraphOpacity * 255), _selectionGraphFillColor.R, _selectionGraphFillColor.G, _selectionGraphFillColor.B));
    }

    private void InitializeVisual()
    {
        _resourceCreator = new CanvasDevice();

        Compositor compositor = Window.Current.Compositor;
        _container = compositor.CreateContainerShape();

        _trackGeometry = compositor.CreatePathGeometry();
        CompositionSpriteShape shape = compositor.CreateSpriteShape(_trackGeometry);
        shape.StrokeThickness = .2f;
        shape.IsStrokeNonScaling = true;
        _container.Shapes.Add(shape);

        _sectionGeometry = compositor.CreatePathGeometry();
        shape = compositor.CreateSpriteShape(_sectionGeometry);
        _container.Shapes.Add(shape);

        _selectionGeometry = compositor.CreatePathGeometry();
        shape = compositor.CreateSpriteShape(_selectionGeometry);
        _container.Shapes.Add(shape);

        _visual = compositor.CreateShapeVisual();
        _visual.IsPixelSnappingEnabled = true;
        _visual.Shapes.Add(_container);

        ElementCompositionPreview.SetElementChildVisual(Root, _visual);

        UpdateTheme();

        _themeListener.ThemeChanged += _ => UpdateTheme();
    }

    private (float MinElevation, float MaxElevation) EnsureTrack()
    {
        double margin = ActualWidth / 8;

        return EnsureGraph(
            XToDistance(Math.Max(0, _scrollerOffset - margin)), 
            XToDistance(Math.Min(Root.ActualWidth, ActualWidth + _scrollerOffset + margin)));
    }

    private (float MinElevation, float MaxElevation) EnsureGraph(float startDistance, float endDistance)
    {
        float sectionStart = ViewModel.CurrentSection is null ? -1 : ViewModel.CurrentSection.Start.Distance;
        float sectionEnd = ViewModel.CurrentSection is null ? -1 : ViewModel.CurrentSection.End.Distance;

        float selectionStart = ViewModel.CurrentSelection.IsValid ? ViewModel.CurrentSelection.Start.Distance : -1;
        float selectionEnd = ViewModel.CurrentSelection.IsValid ? ViewModel.CurrentSelection.End.Distance : -1;

        float minElevation = float.PositiveInfinity;
        float maxElevation = float.NegativeInfinity;

        using (CanvasPathBuilder trackBuilder = new(_resourceCreator))
        using (CanvasPathBuilder sectionBuilder = new(_resourceCreator))
        using (CanvasPathBuilder selectionBuilder = new(_resourceCreator))
        {
            IEnumerator<(float Distance, float Altitude, Surface Surface)> enumerator = ViewModel.Track.Points.EnumerateByDistance(startDistance, endDistance, 1 / _horizontalScale).GetEnumerator();
            if (enumerator.MoveNext())
            {
                trackBuilder.BeginFigure(0, -1000); // -1000 to ensure margin is filled

                bool hasSection = sectionStart <= endDistance && sectionEnd >= startDistance;
                if (hasSection)
                {
                    sectionBuilder.BeginFigure(sectionStart, -1000);
                }

                bool hasSelection = selectionStart <= endDistance && selectionEnd >= startDistance;
                if (hasSelection)
                {
                    selectionBuilder.BeginFigure(selectionStart, -1000);
                }

                (float distance, float altitude, _) = enumerator.Current;
                trackBuilder.AddLine(0, altitude - TrackPoint.MinAltitudeValue);

                while (true)
                {
                    minElevation = Math.Min(minElevation, altitude);
                    maxElevation = Math.Max(maxElevation, altitude);
                    trackBuilder.AddLine(distance, altitude - TrackPoint.MinAltitudeValue);
                    
                    if (hasSection && distance > sectionStart && distance < sectionEnd)
                    {
                        sectionBuilder.AddLine(distance, altitude - TrackPoint.MinAltitudeValue);
                    }
                    if (hasSelection && distance > selectionStart && distance < selectionEnd)
                    {
                        selectionBuilder.AddLine(distance, altitude - TrackPoint.MinAltitudeValue);
                    }
                    if (!enumerator.MoveNext())
                    {
                        trackBuilder.AddLine(_trackTotalDistance, altitude - TrackPoint.MinAltitudeValue);
                        break;
                    }

                    (distance, altitude, _) = enumerator.Current;
                }

                trackBuilder.AddLine(_trackTotalDistance, -1000);
                trackBuilder.EndFigure(CanvasFigureLoop.Closed);

                if (hasSection)
                {
                    sectionBuilder.AddLine(sectionEnd, -1000);
                    sectionBuilder.EndFigure(CanvasFigureLoop.Closed);
                }

                if (hasSelection)
                {
                    selectionBuilder.AddLine(selectionEnd, -1000);
                    selectionBuilder.EndFigure(CanvasFigureLoop.Closed);
                }
            }

            _trackGeometry.Path = new CompositionPath(CanvasGeometry.CreatePath(trackBuilder));
            _sectionGeometry.Path = new CompositionPath(CanvasGeometry.CreatePath(sectionBuilder));
            _selectionGeometry.Path = new CompositionPath(CanvasGeometry.CreatePath(selectionBuilder));
        }

        return (minElevation, maxElevation);
    }

    //private readonly Dictionary<Brush, Path> _surfacePaths = [];
    //private Path _currentSurfacePath;
    //private PolyLineSegment _currentSurfaceLine;

    //private void EnsureGraph(PolyLineSegment graph, float startDistance, float endDistance, ref float currentStartDistance, ref float currentEndDistance, bool surfacePaths = false)
    //{
    //    void Reset(PointCollection points, ref float currentStartDistance, ref float currentEndDistance, bool surfacePaths)
    //    {
    //        points.Clear();
    //        currentStartDistance = currentEndDistance = -1;
    //        if (surfacePaths)
    //        {
    //            _currentSurfaceLine = null;
    //            _currentSurfacePath = null;
    //            foreach (Path path in _surfacePaths.Values)
    //            {
    //                Root.Children.Remove(path);
    //            }
    //            _surfacePaths.Clear();
    //        }
    //    }


    //    void AddSurfacePathPoint(Surface surface, float distance, float altitude)
    //    {
    //        Point point = new(distance, altitude);
    //        Brush surfaceBrush = Convert.SurfaceBrush(surface);

    //        if (!_surfacePaths.TryGetValue(surfaceBrush, out Path path))
    //        {
    //            path = new()
    //            {
    //                Stroke = surfaceBrush,
    //                StrokeThickness = surfaceBrush == _trackGraphOutlineBrush
    //                    ? .5 
    //                    : surface == Surface.UnknownLikelyPaved
    //                        ? .75
    //                        : 3,
    //                Data = new PathGeometry { Transform = _graphTransform }
    //            };
    //            Root.Children.Add(path);
    //            _surfacePaths.Add(surfaceBrush, path);
    //        }
    //        if (path == _currentSurfacePath)
    //        {
    //            _currentSurfaceLine.Points.Add(point);
    //        }
    //        else
    //        {
    //            if (_currentSurfacePath is not null)
    //            {
    //                _currentSurfaceLine.Points.Add(point);
    //            }
    //            _currentSurfacePath = path;
    //            _currentSurfaceLine = new PolyLineSegment { Points = { point } };
    //            ((PathGeometry)path.Data).Figures.Add(new PathFigure 
    //            { 
    //                Segments = { _currentSurfaceLine }, 
    //                StartPoint = point 
    //            });
    //        }
    //    }
    //}
}
