using Microsoft.Xaml.Interactivity;
using Windows.UI.Xaml;

namespace cycloid.Behaviors;

public class PropertyChangedTrigger : Trigger
{
    public object Target
    {
        get => GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    public static readonly DependencyProperty TargetProperty =
        DependencyProperty.Register(nameof(Target), typeof(bool), typeof(PropertyChangedTrigger), new PropertyMetadata(null, (sender, e) => ((PropertyChangedTrigger)sender).PropertyChanged(e)));

    private void PropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        Interaction.ExecuteActions(AssociatedObject, Actions, e);
    }
}
