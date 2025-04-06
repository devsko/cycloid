using Windows.UI.Xaml.Controls;

namespace cycloid.Controls;

public partial class ViewModelControl : UserControl
{
    public ViewModel ViewModel { get; } = App.Current.ViewModel;
}