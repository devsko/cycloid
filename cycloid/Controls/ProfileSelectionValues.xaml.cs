using System.Windows.Input;
using Windows.UI.Xaml;

namespace cycloid.Controls;

public sealed partial class ProfileSelectionValues : SelectionValuesControl
{
    public bool EnableCommands
    {
        get => (bool)GetValue(EnableCommandsProperty);
        set => SetValue(EnableCommandsProperty, value);
    }

    public static readonly DependencyProperty EnableCommandsProperty = DependencyProperty.Register(nameof(EnableCommands), typeof(bool), typeof(ProfileSelectionValues), new PropertyMetadata(true));

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
