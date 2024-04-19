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
public class RequestDeleteSelectionDetails(SelectionDescription selection) : AsyncRequestMessage<bool>()
{
    public SelectionDescription Selection { get; } = selection;
}
public class RequestPasteSelectionDetails(SelectionDescription selection, WayPoint pasteAt) : AsyncRequestMessage<PasteSelectionDetails>()
{
    public SelectionDescription Selection { get; } = selection;

    public WayPoint PasteAt { get; } = pasteAt;
}

public record class SelectionDescription(string SourceTrack, string StartLocation, string EndLocation, int WayPointCount);

public class PasteSelectionDetails
{
    public bool Reverse { get; set; }

    public bool AtStartOrBefore { get; set; }
}

partial class ViewModel
{
    private const string DataFormat = "cycloid/route";

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
                DeleteSelectionCommand.NotifyCanExecuteChanged();

                StrongReferenceMessenger.Default.Send(new SelectionChanged(value));
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanDeleteSelection))]
    public async Task DeleteSelectionAsync()
    {
        WayPoint[] wayPoints = await GetSelectedWayPointsAsync();
        if (wayPoints.Length == 0)
        {
            Status = "No waypoints copied.";
            return;
        }

        string startLocation = await GetAddressAsync(new Geopoint(wayPoints[0].Location), shorter: true);
        string endLocation = await GetAddressAsync(new Geopoint(wayPoints[^1].Location), shorter: true);

        if (await StrongReferenceMessenger.Default.Send(
            new RequestDeleteSelectionDetails(new SelectionDescription(null, startLocation, endLocation, wayPoints.Length))))
        {
            await Track.RouteBuilder.DeletePointsAsync(wayPoints);
        }
    }

    private bool CanDeleteSelection()
    {
        return IsEditMode && CurrentSelection.IsValid;
    }

    [RelayCommand(CanExecute = nameof(CanCopySelection))]
    public async Task CopySelectionAsync()
    {
        WayPoint[] wayPoints = await GetSelectedWayPointsAsync();
        if (wayPoints.Length == 0)
        {
            Status = "No waypoints copied.";
            return;
        }

        PointOfInterest[] pointsOfInterest = 
            Sections
            .Concat(Points)
            .Where(poi => !poi.IsOffTrack && poi.TrackPoint.Distance >= CurrentSelection.Start.Distance && poi.TrackPoint.Distance <= CurrentSelection.End.Distance)
            .Select(poi => poi.PointOfInterest)
            .ToHashSet()
            .ToArray();

        InMemoryRandomAccessStream stream = new();
        var output = stream.GetOutputStreamAt(0).AsStreamForWrite();
        await Serializer.SerializeAsync(
            output, 
            Track.File.Name, 
            await GetAddressAsync(new Geopoint(wayPoints[0].Location), shorter: true),
            await GetAddressAsync(new Geopoint(wayPoints[^1].Location), shorter: true),
            wayPoints, 
            pointsOfInterest
        ).ConfigureAwait(false);

        DataPackage package = new() { RequestedOperation = DataPackageOperation.Copy };
        package.SetData(DataFormat, stream);
        Clipboard.SetContent(package);
        Clipboard.Flush();

        Status = $"{wayPoints.Length} waypoints copied.";
    }

    private bool CanCopySelection()
    {
        return CurrentSelection.IsValid;
    }

    [RelayCommand(CanExecute = nameof(CanPasteWayPoints))]
    public async Task PasteWayPointsAsync()
    {
        string sourceTrack, startLocation, endLocation;
        WayPoint[] wayPoints;
        PointOfInterest[] pointsOfInterest;
        using (IRandomAccessStream stream = (IRandomAccessStream)await Clipboard.GetContent().GetDataAsync(DataFormat))
        using (Stream input = stream.GetInputStreamAt(0).AsStreamForRead())
        {
            (sourceTrack, startLocation, endLocation, wayPoints, pointsOfInterest) = await Serializer.DeserializeSelectionAsync(input);
        }

        PasteSelectionDetails pasteDetails = await StrongReferenceMessenger.Default.Send(
            new RequestPasteSelectionDetails(new SelectionDescription(sourceTrack, startLocation, endLocation, wayPoints.Length), HoveredWayPoint));

        if (pasteDetails is null)
        {
            return;
        }

        if (pasteDetails.Reverse)
        {
            Array.Reverse(wayPoints);
            for (int i = 0; i < wayPoints.Length; i++)
            {
                if (wayPoints[i].IsDirectRoute)
                {
                    wayPoints[i].IsDirectRoute = false;
                    wayPoints[i - 1].IsDirectRoute = true;
                }
            }
        }

        WayPoint pasteAt = HoveredWayPoint;
        if (pasteAt is not null && pasteDetails.AtStartOrBefore)
        {
            int index = Track.RouteBuilder.Points.IndexOf(pasteAt);
            pasteAt = index == 0 ? null : Track.RouteBuilder.Points[index - 1];
        }
        else if (pasteAt is null && !pasteDetails.AtStartOrBefore)
        {
            pasteAt = Track.RouteBuilder.Points.LastOrDefault();
        }

        await Track.RouteBuilder.InsertPointsAsync(wayPoints, pasteAt);

        using (await Track.RouteBuilder.ChangeLock.EnterAsync(default))
        {
            foreach (PointOfInterest poi in pointsOfInterest)
            {
                Track.PointsOfInterest.Add(poi);
            }
        }

        Status = $"{wayPoints.Length} way points pasted.";
    }

    private bool CanPasteWayPoints()
    {
        try
        {
            return IsEditMode && Clipboard.GetContent().AvailableFormats.Contains(DataFormat);
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

    private async Task<WayPoint[]> GetSelectedWayPointsAsync()
    {
        (WayPoint[] WayPoints, TrackPoint.CommonValues[] Starts) segments = await Track.Points.GetSegmentStartsAsync(default);
        IEnumerable<WayPoint> wayPoints = segments.WayPoints
            .Zip(segments.Starts, (wayPoint, start) => (WayPoint: wayPoint, Start: start.Distance))
            .SkipWhile(tuple => tuple.Start < CurrentSelection.Start.Distance)
            .TakeWhile(tuple => tuple.Start <= CurrentSelection.End.Distance)
            .Select(tuple => tuple.WayPoint);

        if (CurrentSelection.End.Equals(Track.Points.Last()))
        {
            wayPoints = wayPoints.Append(segments.WayPoints.Last());
        }

        return wayPoints.ToArray();
    }
}