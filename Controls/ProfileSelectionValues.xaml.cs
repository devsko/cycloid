using System.Windows.Input;
using Windows.UI.Xaml;

namespace cycloid.Controls;

public sealed partial class ProfileSelectionValues : SelectionValuesControl
{
    public ICommand CopyCommand
    {
        get => (ICommand)GetValue(CopyCommandProperty);
        set => SetValue(CopyCommandProperty, value);
    }

    public static readonly DependencyProperty CopyCommandProperty = DependencyProperty.Register(nameof(CopyCommand), typeof(ICommand), typeof(ProfileSelectionValues), new PropertyMetadata(null));

    public ICommand DeleteCommand
    {
        get => (ICommand)GetValue(DeleteCommandProperty);
        set => SetValue(DeleteCommandProperty, value);
    }

    public static readonly DependencyProperty DeleteCommandProperty = DependencyProperty.Register(nameof(DeleteCommand), typeof(ICommand), typeof(ProfileSelectionValues), new PropertyMetadata(null));

    public ProfileSelectionValues()
    {
        InitializeComponent();
    }
}
