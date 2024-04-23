using CommunityToolkit.WinUI;
using Windows.UI.Xaml.Controls;

namespace cycloid.Controls;

public class ViewModelControl : UserControl
{
    protected ViewModel ViewModel => (ViewModel)this.FindResource("ViewModel");
}