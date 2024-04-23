using System;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;

namespace cycloid.Controls;

public sealed partial class DeleteSelectionDialog : ContentDialog
{
    private SelectionDescription Selection { get; }

    public DeleteSelectionDialog()
    {
        InitializeComponent();
    }

    public DeleteSelectionDialog(SelectionDescription selection) : this()
    {
        Selection = selection;
    }

    public async Task<bool> GetResultAsync()
    {
        return await ShowAsync() == ContentDialogResult.Primary;
    }
}
