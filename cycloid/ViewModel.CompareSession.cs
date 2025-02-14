using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace cycloid;

partial class ViewModel :
    IRecipient<CompareSessionChanged>
{
    private TrackDifference _previousDifference;

    [ObservableProperty]
    public partial TrackDifference CurrentDifference { get; set; }

    [ObservableProperty]
    public partial int DownhillCost { get; set; }

    [ObservableProperty]
    public partial float DownhillCutoff { get; set; }

    [ObservableProperty]
    public partial int UphillCost { get; set; }

    [ObservableProperty]
    public partial float UphillCutoff { get; set; }

    [ObservableProperty]
    public partial int BikerPower { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CompareSessionState))]
    [NotifyCanExecuteChangedFor(nameof(CompareSessionCommand))]
    public partial bool TrackIsRecalculating { get; set; }

    partial void OnCurrentDifferenceChanged(TrackDifference oldValue, TrackDifference newValue)
    {
        _previousDifference = newValue is null ? oldValue : null;
    }

    partial void OnDownhillCostChanged(int value)
    {
        if (Track.Points.IsEmpty)
        {
            Track.RouteBuilder.Profile = Track.RouteBuilder.Profile with { DownhillCost = value };
        }
    }

    partial void OnDownhillCutoffChanged(float value)
    {
        if (Track.Points.IsEmpty)
        {
            Track.RouteBuilder.Profile = Track.RouteBuilder.Profile with { DownhillCutoff = value };
        }
    }

    partial void OnUphillCostChanged(int value)
    {
        if (Track.Points.IsEmpty)
        {
            Track.RouteBuilder.Profile = Track.RouteBuilder.Profile with { UphillCost = value };
        }
    }

    partial void OnUphillCutoffChanged(float value)
    {
        if (Track.Points.IsEmpty)
        {
            Track.RouteBuilder.Profile = Track.RouteBuilder.Profile with { UphillCutoff = value };
        }
    }

    partial void OnBikerPowerChanged(int value)
    {
        if (Track.Points.IsEmpty)
        {
            Track.RouteBuilder.Profile = Track.RouteBuilder.Profile with { BikerPower = value };
        }
    }

    public bool HasCompareSession => Track?.CompareSession is not null;

    public string CompareSessionCommandName => Track?.CompareSession is not null ? "Accept" : "Restore point";

    public string CompareSessionState =>
        Track?.CompareSession is null
        ? ""
        : Track.CompareSession.Differences.Count switch
        {
            0 => "No differences",
            1 => "1 difference",
            int n => $"{n} differences",
        } +
        (TrackIsRecalculating ? $" ({Track.CompareSession.OriginalSegmentsCount - Track.RouteBuilder.ChangeLock.RunningCalculationCounter} / {Track.CompareSession.OriginalSegmentsCount})" : "");

    [RelayCommand(CanExecute = nameof(CanCompareSession))]
    public async Task CompareSessionAsync(CancellationToken cancellationToken)
    {
        if (Track.CompareSession is null)
        {
            try
            {
                Track.CompareSession = new CompareSession(Track, (await Track.Points.GetSegmentsAsync(cancellationToken)));
                await SaveTrackAsync();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            { }
        }
        else
        {
            Track.CompareSession.Differences.Clear();
            Track.CompareSession.Dispose();
            Track.CompareSession = null;
            await SaveTrackAsync();
        }
    }

    private bool CanCompareSession()
    {
        return IsEditMode && Track is not null && TrackIsInitialized && !TrackIsRecalculating;
    }

    [RelayCommand(CanExecute = nameof(CanRecalculate))]
    public async Task RecalculateAsync(CancellationToken cancellationToken)
    {
        try
        {
            TrackIsRecalculating = true;
            Track.RouteBuilder.Profile = new Routing.Profile(DownhillCost, DownhillCutoff, UphillCost, UphillCutoff, BikerPower);
            await Track.RouteBuilder.RecalculateAllAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        { }
        finally
        {
            TrackIsRecalculating = false;
        }
    }

    private bool CanRecalculate()
    {
        return HasCompareSession;
    }

    [RelayCommand(CanExecute = nameof(CanCancelCompareSession))]
    public async Task CancelCompareSessionAsync()
    {
        RecalculateCommand.Cancel();
        CompareSessionCommand.Cancel();
        if (Track.CompareSession is not null)
        {
            Track.CompareSession.Differences.Clear();
            await Track.CompareSession.RollbackAsync();
            Track.CompareSession = null;
            await SaveTrackAsync();
        }
        (DownhillCost, DownhillCutoff, UphillCost, UphillCutoff, BikerPower) = (Track.RouteBuilder.Profile.DownhillCost, Track.RouteBuilder.Profile.DownhillCutoff, Track.RouteBuilder.Profile.UphillCost, Track.RouteBuilder.Profile.UphillCutoff, Track.RouteBuilder.Profile.BikerPower);
    }

    private bool CanCancelCompareSession()
    {
        return HasCompareSession;
    }

    private void CompareSessionDifferences_CollectionChanged(object _, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(CompareSessionState));

        if (e.Action == NotifyCollectionChangedAction.Replace && CurrentDifference is null && (TrackDifference)e.OldItems[0] == _previousDifference)
        {
            CurrentDifference = (TrackDifference)e.NewItems[0];
            _previousDifference = null;
        }
    }

    void IRecipient<CompareSessionChanged>.Receive(CompareSessionChanged message)
    {
        if (message.OldValue is not null)
        {
            message.OldValue.Differences.CollectionChanged -= CompareSessionDifferences_CollectionChanged;
        }
        if (message.NewValue is not null)
        {
            message.NewValue.Differences.CollectionChanged += CompareSessionDifferences_CollectionChanged;
        }

        OnPropertyChanged(nameof(HasCompareSession));
        OnPropertyChanged(nameof(CompareSessionState));
        OnPropertyChanged(nameof(CompareSessionCommandName));
        OnPropertyChanged(nameof(CanEditProfile));

        RecalculateCommand.NotifyCanExecuteChanged();
        CancelCompareSessionCommand.NotifyCanExecuteChanged();
    }
}