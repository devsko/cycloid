using System;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace cycloid;

public class CompareSessionChanged(object sender, Track.CompareSession oldValue, Track.CompareSession newValue) : PropertyChangedMessage<Track.CompareSession>(sender, null, oldValue, newValue);

partial class ViewModel
{
    private Track.CompareSession _compareSession;
    private int _downhillCost;
    private float _downhillCutoff;
    private int _uphillCost;
    private float _uphillCutoff;
    private int _bikerPower;

    public Track.CompareSession CompareSession
    {
        get => _compareSession;
        set
        {
            Track.CompareSession oldValue = _compareSession;
            if (SetProperty(ref _compareSession, value))
            {
                if (oldValue is not null)
                {
                    oldValue.Differences.CollectionChanged -= CompareSessionDifferences_CollectionChanged;
                }
                if (value is not null)
                {
                    value.Differences.CollectionChanged += CompareSessionDifferences_CollectionChanged;
                }

                OnPropertyChanged(nameof(CanEditProfile));
                OnPropertyChanged(nameof(CompareSessionCommandName));
                OnPropertyChanged(nameof(CompareSessionState));
                OnPropertyChanged(nameof(HasCompareSession));

                RecalculateCommand.NotifyCanExecuteChanged();
                CancelCompareSessionCommand.NotifyCanExecuteChanged();

                StrongReferenceMessenger.Default.Send(new CompareSessionChanged(this, oldValue, value));
            }
        }
    }

    public bool HasCompareSession => CompareSession is not null;

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

    public string CompareSessionState =>
        CompareSession is null
        ? ""
        : (CompareSession.Differences.Count) switch
        {
            0 => "No differences",
            1 => "1 difference",
            int n => $"{n} differences",
        } +
        (TrackIsCalculating ? $" ({CompareSession.OriginalSegmentsCount - Track.RouteBuilder.ChangeLock.RunningCalculationCounter} / {CompareSession.OriginalSegmentsCount})" : "");

    public string CompareSessionCommandName => CompareSession is not null ? "Accept" : "Restore point";

    [RelayCommand(CanExecute = nameof(CanCompareSession))]
    public async Task CompareSessionAsync(CancellationToken cancellationToken)
    {
        if (CompareSession is null)
        {
            try
            {
                CompareSession = new Track.CompareSession(Track, (await Track.Points.GetSegmentsAsync(cancellationToken)));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            { }
        }
        else
        {
            CompareSession.Differences.Clear();
            CompareSession.Dispose();
            CompareSession = null;
        }
    }

    private bool CanCompareSession()
    {
        return IsEditMode && Track is not null && TrackIsInitialized && !TrackIsCalculating;
    }

    [RelayCommand(CanExecute = nameof(CanRecalculate))]
    public async Task RecalculateAsync(CancellationToken cancellationToken)
    {
        try
        {
            Track.RouteBuilder.Profile = new Routing.Profile(DownhillCost, DownhillCutoff, UphillCost, UphillCutoff, BikerPower);
            await Track.RouteBuilder.RecalculateAllAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        { }
    }

    private bool CanRecalculate()
    {
        return CompareSession is not null;
    }

    [RelayCommand(CanExecute = nameof(CanCancelCompareSession))]
    public async Task CancelCompareSessionAsync()
    {
        RecalculateCommand.Cancel();
        CompareSessionCommand.Cancel();
        CompareSession.Differences.Clear();
        await CompareSession.RollbackAsync();
        CompareSession = null;
        (DownhillCost, DownhillCutoff, UphillCost, UphillCutoff, BikerPower) = (Track.RouteBuilder.Profile.DownhillCost, Track.RouteBuilder.Profile.DownhillCutoff, Track.RouteBuilder.Profile.UphillCost, Track.RouteBuilder.Profile.UphillCutoff, Track.RouteBuilder.Profile.BikerPower);
    }

    private bool CanCancelCompareSession()
    {
        return CompareSession is not null;
    }

    private void CompareSessionDifferences_CollectionChanged(object _1, NotifyCollectionChangedEventArgs _2)
    {
        OnPropertyChanged(nameof(CompareSessionState));
    }
}
