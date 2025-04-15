using System.ComponentModel;
using System.Numerics;
using CommunityToolkit.WinUI;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace cycloid.Controls;

public sealed partial class TrackSplitter : ItemsControl
{
    private static readonly SolidColorBrush _transparent = new(Colors.Transparent);
    private static readonly SolidColorBrush _red = new(Colors.Red);

    private Profile _profile;
    private bool _isPointerOverContainer;

    [GeneratedDependencyProperty]
    public partial bool IsPointerOver { get; private set; }

    public bool IsPointerCaptured { get; private set; }

    public TrackSplitter()
    {
        InitializeComponent();

        RegisterPropertyChangedCallback(ItemsSourceProperty, (_, _) =>
        {
            if (ItemsSource is Track.SplitCollection splits)
            {
                foreach (TrackSplit split in splits)
                {
                    split.PropertyChanged += Split_PropertyChanged;
                }
                splits.CollectionChanged += (_, args) =>
                {
                    if (args.OldItems is not null)
                    {
                        foreach (TrackSplit oldSplit in args.OldItems)
                        {
                            oldSplit.PropertyChanged -= Split_PropertyChanged;
                        }
                    }
                    if (args.NewItems is not null)
                    {
                        foreach (TrackSplit newSplit in args.NewItems)
                        {
                            newSplit.PropertyChanged += Split_PropertyChanged;
                        }
                    }
                };
            }
            void Split_PropertyChanged(object sender, PropertyChangedEventArgs e)
            {
                ((TrackSplitContainer)ContainerFromItem(sender))?.SplitPropertyChanged(e);
            }
        });
    }

    protected override DependencyObject GetContainerForItemOverride()
    {
        TrackSplitContainer container = new(this);
        container.PointerEntered += Container_PointerEntered;
        container.PointerExited += Container_PointerExited;
        container.PointerMoved += Container_PointerMoved;
        container.PointerPressed += Container_PointerPressed;
        container.PointerReleased += Container_PointerReleased;
        container.PointerCaptureLost += Container_PointerCaptureLost;

        return container;
    }

    private static Brush RedIfInvalid(bool isValid) => isValid ? _transparent : _red;

    private void ItemsControl_Loaded(object sender, RoutedEventArgs e)
    {
        _profile = this.FindParent<Profile>();
    }

    private void ItemsControl_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (!_isPointerOverContainer)
        {
            Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Cross, 1);
        }
        IsPointerOver = true;
    }

    private void ItemsControl_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (!IsPointerCaptured)
        {
            Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Arrow, 0);
            IsPointerOver = false;
        }
    }

    private void ItemsControl_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (!_isPointerOverContainer)
        {
            ((Track.SplitCollection)ItemsSource).Add(new TrackSplit(_profile.XToDistance(e.GetPosition(this).X) / 1_000));
        }
    }

    private void Container_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.SizeWestEast, 10);
        _isPointerOverContainer = true;
    }

    private void Container_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        _isPointerOverContainer = false;
        if (!IsPointerCaptured)
        {
            Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Cross, 1);
        }
    }

    private void Container_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (IsPointerCaptured)
        {
            _profile.ScrollIfNeeded(e, true,
                static (profile, state, x) => state.Position = profile.XToDistance(x) / 1_000,
                (TrackSplit)ItemFromContainer((TrackSplitContainer)sender));
        }
    }

    private void Container_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        IsPointerCaptured = ((TrackSplitContainer)sender).CapturePointer(e.Pointer);
    }

    private void Container_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        ReleasePointerCapture(e.Pointer);
    }

    private void Container_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        IsPointerCaptured = false;
        _profile.StopScroll();

        if (!_isPointerOverContainer)
        {
            IsPointerOver =
                new Rect(default, ActualSize.ToPoint()).Contains(e.GetCurrentPoint(this).Position) &&
                new Rect(default, _profile.ActualSize.ToPoint()).Contains(e.GetCurrentPoint(_profile).Position);
            Window.Current.CoreWindow.PointerCursor = IsPointerOver ? new CoreCursor(CoreCursorType.Cross, 1) : new CoreCursor(CoreCursorType.Arrow, 0);
        }
    }

    private void MapControlBorder_Loaded(object sender, RoutedEventArgs e)
    {
        ((MapControlBorder)sender).OpacityTransition = new ScalarTransition { Duration = TimeSpan.FromSeconds(.6) };
    }
}
