using CommunityToolkit.WinUI;
using Windows.UI.Xaml.Controls;

namespace cycloid.Controls;

public sealed partial class Bing : UserControl
{
    public Bing()
    {
        InitializeComponent();
    }

    [GeneratedDependencyProperty]
    public partial string Address { get; set; }

    partial void OnAddressChanged(string newValue)
    {
        Flash.Start();
    }
}
