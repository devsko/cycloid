using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace cycloid;

public sealed partial class ErrorDialog : ContentDialog
{
    public string ErrorMessage { get; init; }

    public ErrorDialog()
    {
        InitializeComponent();
    }

    public ErrorDialog(string errorMessage) : this()
    {
        ErrorMessage = errorMessage;

        Rect size = Window.Current.Bounds;
        MessageBox.Text = ErrorMessage;
        MessageBox.Height = size.Height * .5;
    }

    private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        DataPackage data = new();
        data.SetText(ErrorMessage);
        Clipboard.SetContent(data);

        args.Cancel = true;
    }
}
