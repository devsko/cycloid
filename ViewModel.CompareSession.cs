using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace cycloid;

partial class ViewModel
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RecalculateCommand))]
    private Track.CompareSession _compareSession;

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

    public event Action<Track.CompareSession, Track.CompareSession> CompareSessionChanged;

    partial void OnCompareSessionChanged(Track.CompareSession oldValue, Track.CompareSession newValue)
    {
        CompareSessionChanged?.Invoke(oldValue, newValue);
    }

    [RelayCommand(CanExecute = nameof(CanRecalculate))]
    public async Task RecalculateAsync(CancellationToken cancellationToken)
    {
        if (CanRecalculate())
        {
            Routing.Profile profile = Track.RouteBuilder.Profile;
            try
            {
                Track.RouteBuilder.Profile = new Routing.Profile(DownhillCost, DownhillCutoff, UphillCost, UphillCutoff, BikerPower);
                CompareSession = new Track.CompareSession(Track, (await Track.Points.GetSegmentsAsync(cancellationToken)));
                await Track.RouteBuilder.RecalculateAllAsync();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                CompareSession.Rollback();
                CompareSession = null;
                Track.RouteBuilder.Profile = profile;
            }
        }
    }

    private bool CanRecalculate()
    {
        return Track is not null && TrackIsInitialized && CompareSession is null;
    }
}