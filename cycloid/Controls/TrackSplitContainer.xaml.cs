using System.ComponentModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using WinRT;

namespace cycloid.Controls;

[GeneratedBindableCustomProperty([nameof(LeftOfMarker), nameof(LeftOfGapText), nameof(GapTextOpacity), nameof(GapTextVisibility)], null)]
public sealed partial class TrackSplitContainer : ContentPresenter, INotifyPropertyChanged
{
    private static readonly PropertyChangedEventArgs _leftOfMarkerArgs = new(nameof(LeftOfMarker));
    private static readonly PropertyChangedEventArgs _leftOfGapTextArgs = new(nameof(LeftOfGapText));
    private static readonly PropertyChangedEventArgs _gapTextOpacityArgs = new(nameof(GapTextOpacity));
    private static readonly PropertyChangedEventArgs _gapTextVisibilityArgs = new(nameof(GapTextVisibility));

    private TrackSplitter _parent;
    private long _isPointerOverChangedToken;

    public event PropertyChangedEventHandler PropertyChanged;

    public TrackSplitContainer(TrackSplitter parent)
    {
        _parent = parent;
        InitializeComponent();

        _isPointerOverChangedToken = parent.RegisterPropertyChangedCallback(TrackSplitter.IsPointerOverProperty, Parent_IsPointerOverChanged);
        parent.SizeChanged += Parent_SizeChanged;
    }

    public double LeftOfMarker => DistanceToX(Split.Position);

    public double LeftOfGapText => DistanceToX(Split.DistanceToNext) / 2;

    public double GapTextOpacity => _parent.IsPointerOver ? 1.0 : 0.0;

    public Visibility GapTextVisibility => DistanceToX(Split.DistanceToNext) > 40 ? Visibility.Visible : Visibility.Collapsed;

    private TrackSplit Split => (TrackSplit)_parent.ItemFromContainer(this);

    public void SplitPropertyChanged(PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TrackSplit.Position))
        {
            PropertyChanged?.Invoke(this, _leftOfMarkerArgs);
        }
        else if (e.PropertyName == nameof(TrackSplit.DistanceToNext))
        {
            PropertyChanged?.Invoke(this, _leftOfGapTextArgs);
            PropertyChanged?.Invoke(this, _gapTextVisibilityArgs);
        }
    }

    private double DistanceToX(float distance)
    {
        return _parent.ActualWidth * distance / (App.Current.ViewModel.Track.Points.Total.Distance / 1_000);
    }

    private void Parent_IsPointerOverChanged(DependencyObject sender, DependencyProperty property)
    {
        PropertyChanged?.Invoke(this, _gapTextOpacityArgs);
    }

    private void Parent_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        PropertyChanged?.Invoke(this, _leftOfMarkerArgs);
        PropertyChanged?.Invoke(this, _leftOfGapTextArgs);
        PropertyChanged?.Invoke(this, _gapTextVisibilityArgs);
    }

    private void ContentPresenter_Unloaded(object sender, RoutedEventArgs e)
    {
        _parent.UnregisterPropertyChangedCallback(TrackSplitter.IsPointerOverProperty, _isPointerOverChangedToken);
        _parent.SizeChanged -= Parent_SizeChanged;
    }
}
