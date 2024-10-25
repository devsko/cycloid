using System;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;

namespace cycloid.Controls;

public sealed partial class PasteSelectionDialog : ContentDialog
{
    private SelectionDescription Selection { get; }

    private WayPoint PasteAt { get; }

    private PasteSelectionDetails Response { get; } = new();

    private string Option1Text => PasteAt is null ? "Paste at start" : "Paste before current point";

    private string Option2Text => PasteAt is null ? "Paste at destination" : "Paste after current point";

    public PasteSelectionDialog()
    {
        InitializeComponent();
    }

    public PasteSelectionDialog(SelectionDescription selection, WayPoint pasteAt) : this()
    {
        Selection = selection;
        PasteAt = pasteAt;
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
