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

    [RelayCommand(CanExecute = nameof(CanRecalculate))]
    public async Task RecalculateAsync()
    {
        if (CanRecalculate())
        {
            Track.RouteBuilder.Profile = new Routing.Profile(DownhillCost, DownhillCutoff, UphillCost, UphillCutoff, BikerPower);
            CompareSession = await Track.Points.StartCompareSessionAsync();
            await Track.RouteBuilder.RecalculateAllAsync();
        }
    }

    private bool CanRecalculate()
    {
        return Track is not null && CompareSession is null;
    }
}