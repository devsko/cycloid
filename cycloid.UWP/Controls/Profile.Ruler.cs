using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Shapes;

namespace cycloid.Controls;

partial class Profile
{
    private void ResetHorizontalRuler()
    {
        HorizontalRuler.Children.Clear();

        _horizontalRulerStartTick = int.MaxValue;
        _horizontalRulerEndTick = -1;
    }

    private void EnsureHorizontalRuler()
    {
        int gap = CalculateTickGap(ViewModel.Track.Points.Total.Distance, _horizontalSize, HorizontalRulerTickMinimumGap);
        int startTick = Math.Max(1, (int)(_scrollerOffset / _horizontalScale / gap));
        int endTick = (int)((ActualWidth + _scrollerOffset) / _horizontalScale / gap);

        if (_horizontalRulerStartTick > _horizontalRulerEndTick)
        {
            DrawHorizontalRuler(startTick, endTick);
        }
        else
        {
            if (startTick < _horizontalRulerStartTick)
            {
                DrawHorizontalRuler(startTick, _horizontalRulerStartTick - 1);
            }

            if (endTick > _horizontalRulerEndTick)
            {
                DrawHorizontalRuler(_horizontalRulerEndTick + 1, endTick);
            }
        }

        _horizontalRulerStartTick = Math.Min(_horizontalRulerStartTick, startTick);
        _horizontalRulerEndTick = Math.Max(_horizontalRulerEndTick, endTick);

        void DrawHorizontalRuler(int from, int to)
        {
            for (int tick = from; tick <= to; tick++)
            {
                int distance = tick * gap;
                double left = distance * _horizontalScale;
                HorizontalRuler.Children.Add(new Line
                {
                    X1 = left,
                    X2 = left,
                    Y1 = 14,
                    Y2 = 24,
                    Stroke = _trackGraphOutlineBrush,
                    StrokeThickness = .5,
                });
                TextBlock text = new()
                {
                    Text = ((float)distance / 1000).ToString($"N{(gap < 1000 ? '1' : '0')}"),
                    FontSize = 9,
                    HorizontalTextAlignment = TextAlignment.Center,
                    Width = 50,
                };
                Canvas.SetLeft(text, left - 25);
                HorizontalRuler.Children.Add(text);
            }
        }
    }

    private void DrawVerticalRuler()
    {
        float minElevation = ViewModel.Track.Points.MinAltitude;
        double sizeY = _elevationDiff * (1 + GraphBottomMarginRatio + GraphTopMarginRatio);
        double scaleY = ActualHeight / sizeY;
        int gap = CalculateTickGap(sizeY, ActualHeight, VerticalRulerTickMinimumGap);

        for (int tick = ((int)(minElevation / gap) + 1) * gap; tick < _maxElevation; tick += gap)
        {
            double top = (tick - minElevation) * -scaleY + ActualHeight * (1 - GraphBottomMarginRatio);
            VerticalRuler.Children.Add(new Line
            {
                X1 = 0,
                X2 = ActualWidth,
                Y1 = top,
                Y2 = top,
                Stroke = _trackGraphOutlineBrush,
                StrokeThickness = .5,
            });
            TextBlock text = new()
            {
                Text = tick.ToString("N0"),
                FontSize = 9,
            };
            Canvas.SetTop(text, top - 12);
            Canvas.SetLeft(text, 2);
            VerticalRuler.Children.Add(text);
        }
    }

    private static int CalculateTickGap(double size, double pixel, double minimumGap)
    {
        double gap = size / Math.Floor(pixel / minimumGap - .5);
        int factor = 1;
        while (gap >= 5 * factor)
        {
            factor *= 10;
        }

        return 
            gap < factor 
            ? factor 
            : gap < 2 * factor 
            ? 2 * factor 
            : 5 * factor;
    }
}