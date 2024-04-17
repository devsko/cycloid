using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using cycloid.Serizalization;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Geolocation;
using Windows.Storage.Streams;

namespace cycloid;

public class SelectionChanged(Selection value) : ValueChangedMessage<Selection>(value);

partial class ViewModel
{
    private Selection _currentSelection = Selection.Invalid;
    private TrackPoint _selectionCapture = TrackPoint.Invalid;

    public Selection CurrentSelection
    {
        get => _currentSelection;
        set
        {
            if (SetProperty(ref _currentSelection, value))
            {
                CopySelectionCommand.NotifyCanExecuteChanged();

                StrongReferenceMessenger.Default.Send(new SelectionChanged(value));
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanCopySelection))]
    public async Task CopySelectionAsync()
    {
        (WayPoint[] WayPoints, TrackPoint.CommonValues[] Starts) segments = await Track.Points.GetSegmentStartsAsync(default);
        IEnumerable<WayPoint> wp = segments.WayPoints
            .Zip(segments.Starts, (wayPoint, start) => (WayPoint: wayPoint, Start: start.Distance))
            .SkipWhile(tuple => tuple.Start < CurrentSelection.Start.Distance)
            .TakeWhile(tuple => tuple.Start <= CurrentSelection.End.Distance)
            .Select(tuple => tuple.WayPoint);

        if (CurrentSelection.End.Equals(Track.Points.Last()))
        {
            wp = wp.Append(segments.WayPoints.Last());
        }

        WayPoint[] wayPoints = wp.ToArray();
        if (wayPoints.Length == 0)
        {
            Status = "no waypoints copied.";
            return;
        }

        InMemoryRandomAccessStream stream = new();
        await Serializer.SerializeAsync(stream.GetOutputStreamAt(0).AsStreamForWrite(), wayPoints);
        DataPackage data = new() { RequestedOperation = DataPackageOperation.Copy };
        data.SetData("cycloid/route", stream);
        data.SetText($"{wayPoints.Length} cycloid waypoints\r\n{await GetAddressAsync(new Geopoint(wayPoints[0].Location), shorter: true)} - {await GetAddressAsync(new Geopoint(wayPoints[^1].Location), shorter: true)}");
        Clipboard.SetContent(data);
        Clipboard.Flush();

        Status = $"{wayPoints.Length} waypoints copied.";
    }

    private bool CanCopySelection() => CurrentSelection.IsValid;

    [RelayCommand(CanExecute = nameof(CanPasteWayPoints))]
    public async Task PasteWayPointsAtStartAsync(bool reversed)
    {
        (IEnumerable<WayPoint> wayPoints, int length) = await GetWayPointsFromClipboardAsync(reversed);
        await Track.RouteBuilder.InsertPointsAsync(wayPoints, null);

        Status = $"{length} way points pasted.";
    }

    [RelayCommand(CanExecute = nameof(CanPasteWayPoints))]
    public async Task PasteWayPointsAsync(bool reversed)
    {
        (IEnumerable<WayPoint> wayPoints, int length) = await GetWayPointsFromClipboardAsync(reversed);
        await Track.RouteBuilder.InsertPointsAsync(wayPoints, HoveredWayPoint);

        Status = $"{length} way points pasted.";
    }

    private async Task<(IEnumerable<WayPoint> WayPoints, int Length)> GetWayPointsFromClipboardAsync(bool reversed)
    {
        IRandomAccessStream stream = await Clipboard.GetContent().GetDataAsync("cycloid/route") as IRandomAccessStream;
        WayPoint[] wayPoints = await Serializer.DeserializeAsync(stream.GetInputStreamAt(0).AsStreamForRead());
        IEnumerable<WayPoint> wp = wayPoints;
        if (reversed)
        {
            wp = wp.Reverse();
        }

        return (wp, wayPoints.Length);
    }

    private bool CanPasteWayPoints()
    {
        try
        {
            return Clipboard.GetContent().AvailableFormats.Contains("cycloid/route");
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public bool? GetHoveredSelectionBorder(double toleranceDistance)
    {
        float lowerLimit = HoverPoint.Distance - (float)toleranceDistance;
        float upperLimit = HoverPoint.Distance + (float)toleranceDistance;

        return
            CurrentSelection.Start.Distance > lowerLimit && CurrentSelection.Start.Distance < upperLimit
            ? true
            : CurrentSelection.End.Distance > lowerLimit && CurrentSelection.End.Distance < upperLimit
            ? false
            : null;
    }

    public void StartSelection(double toleranceDistance)
    {
        if (HoverPoint.IsValid)
        {
            if (CurrentSelection.IsValid)
            {
                switch (GetHoveredSelectionBorder(toleranceDistance))
                {
                    case true:
                        _selectionCapture = CurrentSelection.End;
                        break;
                    case false:
                        _selectionCapture = CurrentSelection.Start;
                        break;
                    case null:
                        CurrentSelection = Selection.Invalid;
                        _selectionCapture = HoverPoint;
                        break;
                }
            }
            else
            {
                _selectionCapture = HoverPoint;
            }
        }
    }

    public void ContinueSelection()
    {
        if (_selectionCapture.IsValid && HoverPoint.IsValid)
        {
            CurrentSelection = _selectionCapture.Distance < HoverPoint.Distance
                ? new Selection(_selectionCapture, HoverPoint)
                : new Selection(HoverPoint, _selectionCapture);
        }
    }

    public void EndSelection()
    {
        _selectionCapture = TrackPoint.Invalid;
        if (CurrentSelection.End.Distance - CurrentSelection.Start.Distance < 10)
        {
            CurrentSelection = Selection.Invalid;
        }
    }
}