using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace cycloid.Controls;

public sealed partial class Bing : UserControl
{
    public string Address
    {
        get => (string)GetValue(AddressProperty);
        set => SetValue(AddressProperty, value);
    }

    public static readonly DependencyProperty AddressProperty =
        DependencyProperty.Register(nameof(Address), typeof(string), typeof(Bing), new PropertyMetadata(null, (sender, _) => ((Bing)sender).AddressChanged()));

    public Bing()
    {
        InitializeComponent();
    }

    private void AddressChanged()
    {
        Flash.Start();
    }
}
