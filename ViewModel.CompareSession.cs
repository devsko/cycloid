using System;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace cycloid;

partial class ViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecalculateCommandName))]
    [NotifyPropertyChangedFor(nameof(CompareSessionState))]
    [NotifyCanExecuteChangedFor(nameof(RecalculateCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private Track.CompareSession _compareSession;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecalculateCommandName))]
    [NotifyPropertyChangedFor(nameof(CompareSessionState))]
    [NotifyCanExecuteChangedFor(nameof(RecalculateCommand))]
    private bool _recalculationComplete;

    [ObservableProperty]
    private int _downhillCost;

    [ObservableProperty]
    private float _downhillCutoff;

    [ObservableProperty]
    private int _uphillCost;

    [ObservableProperty]
    private float _uphillCutoff;

    [ObservableProperty]
    private int _bikerPower;

    private Routing.Profile _originalProfile;

    public event Action<Track.CompareSession, Track.CompareSession> CompareSessionChanged;

    public string CompareSessionState => 
        CompareSession is null 
        ? "" 
        : (CompareSession.Differences.Count) switch 
        {
            0 => "No differences",
            1 => "1 difference",
            int n => $"{n} differences",
        } + 
        (RecalculationComplete ? "" : $" ({CompareSession.OriginalSegmentsCount - TrackCalculationCounter} / {CompareSession.OriginalSegmentsCount})");

    public string RecalculateCommandName => RecalculationComplete ? "Accept" : "Recalculate";

    partial void OnCompareSessionChanged(Track.CompareSession oldValue, Track.CompareSession newValue)
    {
        if (oldValue is not null)
        {
            oldValue.Differences.CollectionChanged -= CompareSessionDifferences_CollectionChanged;
        }
        if (newValue is not null)
        {
            newValue.Differences.CollectionChanged += CompareSessionDifferences_CollectionChanged;
        }
        CompareSessionChanged?.Invoke(oldValue, newValue);
    }

    [RelayCommand(CanExecute = nameof(CanRecalculate))]
    public async Task RecalculateAsync(CancellationToken cancellationToken)
    {
        if (CanRecalculate())
        {
            if (CompareSession is null)
            {
                _originalProfile = Track.RouteBuilder.Profile;
                try
                {
                    Track.RouteBuilder.Profile = new Routing.Profile(DownhillCost, DownhillCutoff, UphillCost, UphillCutoff, BikerPower);
                    CompareSession = new Track.CompareSession(Track, (await Track.Points.GetSegmentsAsync(cancellationToken)));
                    await Track.RouteBuilder.RecalculateAllAsync(cancellationToken);
                    RecalculationComplete = true;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                { }
            }
            else
            {
                CompareSession.Differences.Clear();
                CompareSession = null;
                RecalculationComplete = false;
            }
        }
    }

    private bool CanRecalculate()
    {
        return Track is not null && TrackIsInitialized && (CompareSession is null || RecalculationComplete);
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    public async Task CancelAsync()
    {
        RecalculateCommand.Cancel();
        CompareSession.Differences.Clear();
        await CompareSession.RollbackAsync();
        CompareSession = null;
        RecalculationComplete = false;
        Track.RouteBuilder.Profile = _originalProfile;
        (DownhillCost, DownhillCutoff, UphillCost, UphillCutoff, BikerPower) = (_originalProfile.DownhillCost, _originalProfile.DownhillCutoff, _originalProfile.UphillCost, _originalProfile.UphillCutoff, _originalProfile.BikerPower);
    }

    private bool CanCancel()
    {
        return CompareSession is not null;
    }

    private void CompareSessionDifferences_CollectionChanged(object _1, NotifyCollectionChangedEventArgs _2)
    {
        OnPropertyChanged(nameof(CompareSessionState));
    }
}