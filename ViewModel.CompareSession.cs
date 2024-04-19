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
    private bool _recalculationComplete;
    private int _downhillCost;
    private float _downhillCutoff;
    private int _uphillCost;
    private float _uphillCutoff;
    private int _bikerPower;
    private Routing.Profile _originalProfile;

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
                OnPropertyChanged(nameof(RecalculateCommandName));
                OnPropertyChanged(nameof(CompareSessionState));

                RecalculateCommand.NotifyCanExecuteChanged();
                CancelCommand.NotifyCanExecuteChanged();

                StrongReferenceMessenger.Default.Send(new CompareSessionChanged(this, oldValue, value));
            }
        }
    }

    public bool RecalculationComplete
    {
        get => _recalculationComplete;
        set
        {
            if (SetProperty(ref _recalculationComplete, value))
            {
                OnPropertyChanged(nameof(RecalculateCommandName));
                OnPropertyChanged(nameof(CompareSessionState));

                RecalculateCommand.NotifyCanExecuteChanged();
            }
        }
    }

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
        (RecalculationComplete ? "" : $" ({CompareSession.OriginalSegmentsCount - Track.RouteBuilder.ChangeLock.RunningCalculationCounter} / {CompareSession.OriginalSegmentsCount})");

    public string RecalculateCommandName => RecalculationComplete ? "Accept" : "Restore point";

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
                CompareSession.Dispose();
                CompareSession = null;
                RecalculationComplete = false;
            }
        }
    }

    private bool CanRecalculate()
    {
        return Mode is Modes.Edit && Track is not null && TrackIsInitialized && (CompareSession is null || RecalculationComplete);
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
