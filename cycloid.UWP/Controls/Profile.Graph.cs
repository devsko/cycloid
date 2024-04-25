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

    private float _selectionStartDistance;
    private float _selectionEndDistance;

    private float _sectionStartDistance;
    private float _sectionEndDistance;

    private void ResetTrack()
    {
        Graph.Points.Clear();

        _trackStartDistance = _trackEndDistance = -1;
    }

    private void ResetSelection()
    {
        SelectionGraph.Points.Clear();

        _selectionStartDistance = _selectionEndDistance = -1;
    }

    private void ResetSection()
    {
        SectionGraph.Points.Clear();

        _sectionStartDistance = _sectionEndDistance = -1;
    }

    private void EnsureTrack()
    {
        float startDistance = (float)(_scrollerOffset / _horizontalScale);
        float endDistance = (float)((ActualWidth + _scrollerOffset) / _horizontalScale);

        EnsureGraph(startDistance, endDistance, Graph, ref _trackStartDistance, ref _trackEndDistance);

        GraphFigure.StartPoint = Graph.Points[0];
    }

    private void EnsureSelection()
    {
        if (ViewModel.CurrentSelection.IsValid)
        {
            float startDistance = Math.Max(ViewModel.CurrentSelection.Start.Distance, (float)(_scrollerOffset / _horizontalScale));
            float endDistance = Math.Min(ViewModel.CurrentSelection.End.Distance, (float)((ActualWidth + _scrollerOffset) / _horizontalScale));

            if (startDistance <= endDistance)
            {
                EnsureGraph(startDistance, endDistance, SelectionGraph, ref _selectionStartDistance, ref _selectionEndDistance);

                SelectionGraphFigure.StartPoint = SelectionGraph.Points[0];
            }
        }
    }

    private void EnsureSection()
    {
        if (ViewModel.CurrentSection is not null)
        {
            float startDistance = Math.Max(ViewModel.CurrentSection.Start.Distance, (float)(_scrollerOffset / _horizontalScale));
            float endDistance = Math.Min(ViewModel.CurrentSection.End.Distance, (float)((ActualWidth + _scrollerOffset) / _horizontalScale));

            if (startDistance <= endDistance)
            {
                EnsureGraph(startDistance, endDistance, SectionGraph, ref _sectionStartDistance, ref _sectionEndDistance);

                SectionGraphFigure.StartPoint = SectionGraph.Points[0];
            }
        }
    }

    private void EnsureGraph(float startDistance, float endDistance, PolyLineSegment graph, ref float currentStartDistance, ref float currentEndDistance)
    {
        float minElevation = ViewModel.Track.Points.MinAltitude;
        PointCollection points = graph.Points;

        if (currentStartDistance >= 0)
        {
            float step = (float)(1 / (.5 * _horizontalScale));
            if (startDistance < currentStartDistance)
            {
                if ((currentEndDistance - startDistance) / step > 2_500)
                {
                    points.Clear();
                    currentStartDistance = currentEndDistance = -1;
                }
                else
                {
                    points.RemoveAt(0);
                    int i = 0;
                    IterateTrack(startDistance, currentStartDistance, false, true, p => points.Insert(i++, new Point(p.Distance, p.Altitude - minElevation)));
                    points.Insert(0, new Point(graph.Points[0].X, 0));
                    currentStartDistance = startDistance;
                }
            }
            else if (endDistance > currentEndDistance)
            {
                if ((endDistance - currentStartDistance) / step > 2_500)
                {
                    points.Clear();
                    currentStartDistance = currentEndDistance = -1;
                }
                else
                {
                    points.RemoveAt(points.Count - 1);
                    IterateTrack(currentEndDistance, endDistance, true, false, p => points.Add(new Point(p.Distance, p.Altitude - minElevation)));
                    points.Add(new Point(graph.Points[points.Count - 1].X, 0));
                    currentEndDistance = endDistance;
                }
            }
        }


        if (currentStartDistance < 0)
        {
            IterateTrack(startDistance, endDistance, false, false, p => points.Add(new Point(p.Distance, p.Altitude - minElevation)));
            points.Insert(0, new Point(points[0].X, 0));
            points.Add(new Point(points[points.Count - 1].X, 0));
            currentStartDistance = startDistance;
            currentEndDistance = endDistance;
        }
    }

    private void IterateTrack(float startDistance, float endDistance, bool skipFirst, bool skipLast, Action<(float Distance, float Altitude)> action)
    {
        IEnumerable<(float Distance, float Altitude)> points = ViewModel.Track.Points.EnumerateByDistance(startDistance, endDistance, (float)(1 / (.5 * _horizontalScale)));

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
