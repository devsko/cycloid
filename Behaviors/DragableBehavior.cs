using Microsoft.Xaml.Interactivity;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;

namespace cycloid.Behaviors;

public class DragableBehavior : Behavior<FrameworkElement>
{
    private Thickness _originalMargin;
    private Thickness _startMargin;

    public bool IsEnabled
    {
        get => (bool)GetValue(IsEnabledProperty);
        set => SetValue(IsEnabledProperty, value);
    }

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.Register(nameof(IsEnabled), typeof(bool), typeof(DragableBehavior), new PropertyMetadata(true));

    protected override void OnAttached()
    {
        AssociatedObject.ManipulationMode = ManipulationModes.TranslateX | ManipulationModes.TranslateY;
        AssociatedObject.ManipulationStarted += AssociatedObject_ManipulationStarted;
        AssociatedObject.ManipulationDelta += AssociatedObject_ManipulationDelta;
        // TODO Horizontal/Vertical alignment
        _originalMargin = AssociatedObject.Margin = new Thickness(
            -100_000, -100_000, AssociatedObject.Margin.Right, AssociatedObject.Margin.Bottom);
    }

    protected override void OnDetaching()
    {
        AssociatedObject.ManipulationStarted -= AssociatedObject_ManipulationStarted;
        AssociatedObject.ManipulationDelta -= AssociatedObject_ManipulationDelta;
    }

    private void AssociatedObject_ManipulationStarted(object _1, ManipulationStartedRoutedEventArgs _2)
    {
        if (IsEnabled)
        {
            _startMargin = AssociatedObject.Margin;
        }
    }

    private void AssociatedObject_ManipulationDelta(object _, ManipulationDeltaRoutedEventArgs args)
    {
        Point translation = args.Cumulative.Translation;
        if (IsEnabled)
        {
            AssociatedObject.Margin = new Thickness(
                _startMargin.Left, _startMargin.Top, _startMargin.Right - translation.X, _startMargin.Bottom - translation.Y);
        }
    }

    public void Reset()
    {
        AssociatedObject.Margin = _originalMargin;
    }
}
