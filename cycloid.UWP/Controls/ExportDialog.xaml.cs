using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace cycloid.Controls;

public sealed partial class ExportDialog : ContentDialog
{
    private RequestExportDetails Request { get; }
    private ExportDetails Response { get; set; }
    
    public ExportDialog()
    {
        InitializeComponent();
    }

    public ExportDialog(RequestExportDetails request) : this()
    {
        Request = request;
    }

    public async Task<ExportDetails> GetResultAsync()
    {
        Response = new();

        if (StorageApplicationPermissions.FutureAccessList.ContainsItem("Sections.csv"))
        {
            Response.SectionsFile = await StorageApplicationPermissions.FutureAccessList.GetFileAsync("Sections.csv");
            Response.Sections = true;
        }
        if (StorageApplicationPermissions.FutureAccessList.ContainsItem("Water.csv"))
        {
            Response.WaterFile = await StorageApplicationPermissions.FutureAccessList.GetFileAsync("Water.csv");
            Response.Water = true;
        }
        if (StorageApplicationPermissions.FutureAccessList.ContainsItem("POIs.csv"))
        {
            Response.PoisFile = await StorageApplicationPermissions.FutureAccessList.GetFileAsync("POIs.csv");
            Response.Pois = true;
        }
        if (StorageApplicationPermissions.FutureAccessList.ContainsItem("Tracks.zip"))
        {
            Response.TracksFile = await StorageApplicationPermissions.FutureAccessList.GetFileAsync("Tracks.zip");
            Response.Tracks = true;
        }
         
        if (await ShowAsync() != ContentDialogResult.Primary)
        {
            return null;
        }

        return Response;
    }

    private async void CheckBox_Checked(object sender, RoutedEventArgs e)
    {
        string name = (string)((CheckBox)sender).Content;

        if (Response[name] is null)
        {
            FileSavePicker picker = new() { SuggestedFileName = name };
            picker.FileTypeChoices.Add(Path.GetExtension(name)[1..].ToUpperInvariant(), [ Path.GetExtension(name) ]);
            if ((Response[name] = await picker.PickSaveFileAsync()) is null)
            {
                ((CheckBox)sender).IsChecked = false;
            }
            else
            {
                StorageApplicationPermissions.FutureAccessList.AddOrReplace(name, Response[name]);
            }
        }
    }
}
