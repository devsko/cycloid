using Windows.UI.Xaml.Markup;

namespace cycloid;

public class ConfigurationValue : MarkupExtension
{
    public string Key { get; set; }

    protected override object ProvideValue()
    {
        return App.Current.Configuration[Key];
    }
}
