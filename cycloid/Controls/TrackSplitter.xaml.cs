using System.ComponentModel;
using CommunityToolkit.WinUI;
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

    private bool _isPointerOverContainer;

    [GeneratedDependencyProperty]
    public partial bool IsPointerOver { get; private set; }

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

        return container;
    }

    private static Brush RedIfInvalid(bool isValid) => isValid ? _transparent : _red;

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
        Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Arrow, 0);
        IsPointerOver = false;
    }

    private void Container_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.SizeWestEast, 10);
        _isPointerOverContainer = true;
    }

    private void Container_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Cross, 1);
        _isPointerOverContainer = false;
    }

    private void MapControlBorder_Loaded(object sender, RoutedEventArgs e)
    {
        ((MapControlBorder)sender).OpacityTransition = new ScalarTransition { Duration = TimeSpan.FromSeconds(.6) };
    }
}
