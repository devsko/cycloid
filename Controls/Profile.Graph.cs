using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;
using Windows.UI.Xaml.Media;

namespace cycloid.Controls;

partial class Profile
{
    private float _trackTotalDistance;

    private float _trackStartDistance;
    private float _trackEndDistance;

    private float _sectionStartDistance;
    private float _sectionEndDistance;

    private void ResetTrack()
    {
        Graph.Points.Clear();

        _trackStartDistance = float.PositiveInfinity;
        _trackEndDistance = -1;
    }

    private void ResetSection()
    {
        SectionGraph.Points.Clear();

        _sectionStartDistance = float.PositiveInfinity;
        _sectionEndDistance = -1;
    }

    private void EnsureTrack()
    {
        float startDistance = (float)(_scrollerOffset / _horizontalScale);
        float endDistance = (float)((ActualWidth + _scrollerOffset) / _horizontalScale);

        EnsureGraph(startDistance, endDistance, Graph, _trackStartDistance, _trackEndDistance);

        GraphFigure.StartPoint = Graph.Points[0];
        _trackStartDistance = Math.Min(_trackStartDistance, startDistance);
        _trackEndDistance = Math.Max(_trackEndDistance, endDistance);
    }

    private void EnsureSection()
    {
        if (ViewModel.CurrentSection is not null)
        {
            float startDistance = Math.Max(ViewModel.CurrentSection.Start.Distance, (float)(_scrollerOffset / _horizontalScale));
            float endDistance = Math.Min(ViewModel.CurrentSection.End.Distance, (float)((ActualWidth + _scrollerOffset) / _horizontalScale));

            if (startDistance <= endDistance)
            {
                EnsureGraph(startDistance, endDistance, SectionGraph, _sectionStartDistance, _sectionEndDistance);

                SectionGraphFigure.StartPoint = SectionGraph.Points[0];
                _sectionStartDistance = Math.Min(_sectionStartDistance, startDistance);
                _sectionEndDistance = Math.Max(_sectionEndDistance, endDistance);
            }
        }
    }

    private void EnsureGraph(float startDistance, float endDistance, PolyLineSegment graph, float currentStartDistance, float currentEndDistance)
    {
        float minElevation = ViewModel.Track.Points.MinAltitude;
        PointCollection points = graph.Points;

        if (currentStartDistance > currentEndDistance)
        {
            IterateTrack(startDistance, endDistance, false, false, p => points.Add(new Point(p.Distance, p.Elevation - minElevation)));
            points.Insert(0, new Point(points[0].X, 0));
            points.Add(new Point(points[points.Count - 1].X, 0));
        }
        else
        {
            if (startDistance < currentStartDistance)
            {
                points.RemoveAt(0);
                int i = 0;
                IterateTrack(startDistance, currentStartDistance, false, true, p => points.Insert(i++, new Point(p.Distance, p.Elevation - minElevation)));
                points.Insert(0, new Point(graph.Points[0].X, 0));
            }
            if (endDistance > currentEndDistance)
            {
                points.RemoveAt(points.Count - 1);
                IterateTrack(currentEndDistance, endDistance, true, false, p => points.Add(new Point(p.Distance, p.Elevation - minElevation)));
                points.Add(new Point(graph.Points[points.Count - 1].X, 0));
            }
        }
    }

    private void IterateTrack(float startDistance, float endDistance, bool skipFirst, bool skipLast, Action<(float Distance, float Elevation)> action)
    {
        IEnumerable<(float Distance, float Elevation)> points = ViewModel.Track.Points.Enumerate(startDistance, endDistance, _trackIndexStep);

        if (skipFirst)
        {
            points = points.Skip(1);
        }

        if (skipLast)
        {
            points = points.SkipLast(1);
        }

        foreach (var p in points)
        {
            action(p);
        }
    }
}
