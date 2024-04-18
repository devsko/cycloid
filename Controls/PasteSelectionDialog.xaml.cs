using System;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;

namespace cycloid.Controls;

public sealed partial class PasteSelectionDialog : ContentDialog
{
    private RequestPasteSelectionDetails Request { get; }

    private PasteSelectionDetails Response { get; } = new();

    private string Option1Text => Request.PasteAt is null ? "Paste at start" : "Paste before current point";

    private string Option2Text => Request.PasteAt is null ? "Paste at destination" : "Paste after current point";

    public PasteSelectionDialog()
    {
        InitializeComponent();
    }

    public PasteSelectionDialog(RequestPasteSelectionDetails message) : this()
    {
        Request = message;
    }

    public async Task<PasteSelectionDetails> GetResultAsync()
    {
        if (await ShowAsync() != ContentDialogResult.Primary)
        {
            return null;
        }

        return Response;
    }

}
