using CommunityToolkit.WinUI;
using CommunityToolkit.WinUI.Animations;
using Microsoft.Xaml.Interactivity;
using System;
using System.Numerics;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;

namespace cycloid.Behaviors;

public class DragableBehavior : Behavior<FrameworkElement>
{
    private Vector3? _originalOffset;
    private Vector3 _startOffset;

    public event EventHandler DragCompleted;

    public bool IsEnabled
    {
        get => (bool)GetValue(IsEnabledProperty);
        set => SetValue(IsEnabledProperty, value);
    }

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.Register(nameof(IsEnabled), typeof(bool), typeof(DragableBehavior), new PropertyMetadata(true, (sender, e) => ((DragableBehavior)sender).EnabledChanged(e)));

    protected override void OnAttached()
    {
        AssociatedObject.ManipulationMode = ManipulationModes.TranslateX | ManipulationModes.TranslateY;
        AssociatedObject.ManipulationStarted += AssociatedObject_ManipulationStarted;
        AssociatedObject.ManipulationDelta += AssociatedObject_ManipulationDelta;
        AssociatedObject.ManipulationCompleted += AssociatedObject_ManipulationCompleted;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.ManipulationStarted -= AssociatedObject_ManipulationStarted;
        AssociatedObject.ManipulationDelta -= AssociatedObject_ManipulationDelta;
        AssociatedObject.ManipulationCompleted -= AssociatedObject_ManipulationCompleted;
    }

    public void Reset()
    {
        if (_originalOffset is Vector3 originalOffset)
        {
            AnimationBuilder
                .Create()
                .Offset(originalOffset)
                .StartAsync(AssociatedObject)
                .FireAndForget();
        }
    }

    private void EnabledChanged(DependencyPropertyChangedEventArgs _)
    {
        if (IsEnabled)
        {
            _originalOffset = null;
        }
        else
        {
            Reset();
        }
    }

    private void AssociatedObject_ManipulationStarted(object _1, ManipulationStartedRoutedEventArgs _2)
    {
        if (IsEnabled)
        {
            _startOffset = AssociatedObject.GetVisual().Offset;
            if (_startOffset == default)
            {
                _startOffset = AssociatedObject.ActualOffset;
            }
            
            _originalOffset ??= _startOffset;
        }
    }

    private void AssociatedObject_ManipulationDelta(object _, ManipulationDeltaRoutedEventArgs args)
    {
        Point translation = args.Cumulative.Translation;
        if (IsEnabled)
        {
            AssociatedObject.GetVisual().Offset = _startOffset + translation.ToVector3();
        }
    }

    private void AssociatedObject_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
    {
        DragCompleted?.Invoke(this, EventArgs.Empty);
    }
}
