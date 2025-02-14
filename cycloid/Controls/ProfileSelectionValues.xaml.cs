using System.Windows.Input;
using CommunityToolkit.WinUI;

namespace cycloid.Controls;

public sealed partial class ProfileSelectionValues : SelectionValuesControl
{
    [GeneratedDependencyProperty(DefaultValue = true)]
    public partial bool EnableCommands { get; set; }

    [GeneratedDependencyProperty]
    public partial ICommand CopyCommand { get; set; }

    [GeneratedDependencyProperty]
    public partial ICommand DeleteCommand { get; set; }

    public ProfileSelectionValues()
    {
        InitializeComponent();
    }
}
