using System.Collections.Frozen;
using System.Numerics;
using CommunityToolkit.WinUI;
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
    private CompositionContainerShape _surfacesContainer;
    private ShapeVisual _visual;

    private Color _trackGraphOutlineColor;
    private Color _trackGraphFillColor;
    private double _trackGraphOpacity;
    private Color _sectionGraphFillColor;
    private double _sectionGraphOpacity;
    private Color _selectionGraphFillColor;
    private double _selectionGraphOpacity;

    private FrozenDictionary<Surface, CompositionColorBrush> _surfaceBrushes;
    private readonly ThemeListener _themeListener = new();

    [GeneratedDependencyProperty(DefaultValue = true)]
    public partial bool ShowSurface { get; set; }

    partial void OnShowSurfaceChanged(bool newValue)
    {
        _surfacesContainer.Scale = newValue ? new Vector2(1, 1) : default;
    }

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

        CompositionColorBrush red = compositor.CreateColorBrush(Colors.Red);
        CompositionColorBrush orange = compositor.CreateColorBrush(Colors.Orange);

        _surfaceBrushes = new Dictionary<Surface, CompositionColorBrush>
        {
            { Surface.Unknown, compositor.CreateColorBrush(Colors.Goldenrod) },
            { Surface.UnknownLikelyPaved, compositor.CreateColorBrush(Color.FromArgb(0x40, Colors.Yellow.R, Colors.Yellow.G, Colors.Yellow.B)) },
            { Surface.UnknownLikelyUnpaved, red },

            { Surface.paved, null },
            { Surface.asphalt, null },
            { Surface.chipseal, null },
            { Surface.concrete, null },
            { Surface.paving_stones, null },

            { Surface.sett, orange },
            { Surface.unhewn_cobblestone, orange },
            { Surface.cobblestone, orange },
            { Surface.metal, compositor.CreateColorBrush(Colors.DeepSkyBlue) },
            { Surface.wood, compositor.CreateColorBrush(Colors.Brown) },
            { Surface.stepping_stones, compositor.CreateColorBrush(Colors.GreenYellow) },
            { Surface.rubber, compositor.CreateColorBrush(Colors.PaleVioletRed) },

            { Surface.unpaved, red },
            { Surface.compacted, red },
            { Surface.fine_gravel, red },
            { Surface.gravel, red },
            { Surface.shells, red },
            { Surface.rock, red },
            { Surface.pebblestone, red },
            { Surface.ground, red },
            { Surface.dirt, red },
            { Surface.earth, red },
            { Surface.grass, red },
            { Surface.grass_paver, red },
            { Surface.metal_grid, red },
            { Surface.mud, red },
            { Surface.sand, red },
            { Surface.woodchips, red },
            { Surface.snow, red },
            { Surface.ice, red },
            { Surface.salt, red },
        }.ToFrozenDictionary();

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

        _surfacesContainer = compositor.CreateContainerShape();
        _container.Shapes.Add(_surfacesContainer);

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

        foreach (CompositionShape shape in _surfacesContainer.Shapes)
        {
            shape.Dispose();
        }
        _surfacesContainer.Shapes.Clear();

        float minElevation = float.PositiveInfinity;
        float maxElevation = float.NegativeInfinity;

        using (CanvasPathBuilder trackBuilder = new(_resourceCreator))
        using (CanvasPathBuilder sectionBuilder = new(_resourceCreator))
        using (CanvasPathBuilder selectionBuilder = new(_resourceCreator))
        {
            Dictionary<CompositionColorBrush, CanvasPathBuilder> surfaceBuilders = [];

            IEnumerator<(float Distance, float Altitude, Surface Surface)> enumerator = ViewModel.Track.Points.EnumerateByDistance(startDistance, endDistance, 1 / _horizontalScale).GetEnumerator();
            if (enumerator.MoveNext())
            {
                trackBuilder.BeginFigure(0, -1000);

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

                (float distance, float altitude, Surface surface) = enumerator.Current;
                trackBuilder.AddLine(0, altitude - TrackPoint.MinAltitudeValue);

                CompositionColorBrush currentSurfaceBrush = null;
                CanvasPathBuilder currentSurfaceBuilder = null;

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

                    currentSurfaceBuilder?.AddLine(distance, altitude - TrackPoint.MinAltitudeValue);

                    CompositionColorBrush surfaceBrush = _surfaceBrushes[surface];
                    if (surfaceBrush != currentSurfaceBrush)
                    {
                        if (currentSurfaceBuilder is not null)
                        {
                            currentSurfaceBuilder.EndFigure(CanvasFigureLoop.Open);
                            currentSurfaceBuilder = null;
                        }
                        currentSurfaceBrush = surfaceBrush;
                        if (currentSurfaceBrush is not null)
                        {
                            if (!surfaceBuilders.TryGetValue(currentSurfaceBrush, out currentSurfaceBuilder))
                            {
                                currentSurfaceBuilder = new CanvasPathBuilder(_resourceCreator);
                                surfaceBuilders.Add(currentSurfaceBrush, currentSurfaceBuilder);
                            }
                            currentSurfaceBuilder.BeginFigure(distance, altitude - TrackPoint.MinAltitudeValue);
                        }
                    }

                    if (!enumerator.MoveNext())
                    {
                        trackBuilder.AddLine(_trackTotalDistance, altitude - TrackPoint.MinAltitudeValue);
                        currentSurfaceBuilder?.AddLine(_trackTotalDistance, altitude - TrackPoint.MinAltitudeValue);
                        currentSurfaceBuilder?.EndFigure(CanvasFigureLoop.Open);
                        break;
                    }

                    (distance, altitude, surface) = enumerator.Current;
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

                Compositor compositor = Window.Current.Compositor;
                foreach (KeyValuePair<CompositionColorBrush, CanvasPathBuilder> pair in surfaceBuilders)
                {
                    CompositionSpriteShape surfaceShape = compositor.CreateSpriteShape(compositor.CreatePathGeometry(new CompositionPath(CanvasGeometry.CreatePath(pair.Value))));
                    pair.Value.Dispose();
                    surfaceShape.StrokeBrush = pair.Key;
                    surfaceShape.StrokeThickness = 3;
                    surfaceShape.IsStrokeNonScaling = true;
                    _surfacesContainer.Shapes.Add(surfaceShape);
                }
            }

            _trackGeometry.Path = new CompositionPath(CanvasGeometry.CreatePath(trackBuilder));
            _sectionGeometry.Path = new CompositionPath(CanvasGeometry.CreatePath(sectionBuilder));
            _selectionGeometry.Path = new CompositionPath(CanvasGeometry.CreatePath(selectionBuilder));
        }

        return (minElevation, maxElevation);
    }
}
