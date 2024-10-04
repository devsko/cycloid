using System;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace cycloid;

partial class ViewModel :
    IRecipient<CompareSessionChanged>
{
    private int _downhillCost;
    private float _downhillCutoff;
    private int _uphillCost;
    private float _uphillCutoff;
    private int _bikerPower;
    private bool _trackIsRecalculating;

    public bool HasCompareSession => Track?.CompareSession is not null;

    public int DownhillCost
    {
        get => _downhillCost;
        set
        {
            if (SetProperty(ref _downhillCost, value))
            {
                if (Track.Points.IsEmpty)
                {
                    Track.RouteBuilder.Profile = Track.RouteBuilder.Profile with { DownhillCost = value };
                }
            }
        }
    }

    public float DownhillCutoff
    {
        get => _downhillCutoff;
        set
        {
            if (SetProperty(ref _downhillCutoff, value))
            {
                if (Track.Points.IsEmpty)
                {
                    Track.RouteBuilder.Profile = Track.RouteBuilder.Profile with { DownhillCutoff = value };
                }
            }
        }
    }

    public int UphillCost
    {
        get => _uphillCost;
        set
        {
            if (SetProperty(ref _uphillCost, value))
            {
                if (Track.Points.IsEmpty)
                {
                    Track.RouteBuilder.Profile = Track.RouteBuilder.Profile with { UphillCost = value };
                }
            }
        }
    }

    public float UphillCutoff
    {
        get => _uphillCutoff;
        set
        {
            if (SetProperty(ref _uphillCutoff, value))
            {
                if (Track.Points.IsEmpty)
                {
                    Track.RouteBuilder.Profile = Track.RouteBuilder.Profile with { UphillCutoff = value };
                }
            }
        }
    }

    public int BikerPower
    {
        get => _bikerPower;
        set
        {
            if (SetProperty(ref _bikerPower, value))
            {
                if (Track.Points.IsEmpty)
                {
                    Track.RouteBuilder.Profile = Track.RouteBuilder.Profile with { BikerPower = value };
                }
            }
        }
    }

    public bool TrackIsRecalculating
    {
        get => _trackIsRecalculating;
        set
        {
            if (SetProperty(ref _trackIsRecalculating, value))
            {
                OnPropertyChanged(nameof(CompareSessionState));

                CompareSessionCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string CompareSessionState =>
        Track?.CompareSession is null
        ? ""
        : (Track.CompareSession.Differences.Count) switch
        {
            0 => "No differences",
            1 => "1 difference",
            int n => $"{n} differences",
        } +
        (TrackIsRecalculating ? $" ({Track.CompareSession.OriginalSegmentsCount - Track.RouteBuilder.ChangeLock.RunningCalculationCounter} / {Track.CompareSession.OriginalSegmentsCount})" : "");

    public string CompareSessionCommandName => Track?.CompareSession is not null ? "Accept" : "Restore point";

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

    private void CompareSessionDifferences_CollectionChanged(object _1, NotifyCollectionChangedEventArgs _2)
    {
        OnPropertyChanged(nameof(CompareSessionState));
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